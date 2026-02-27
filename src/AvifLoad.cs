////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020-2025 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.AvifContainer;
using AvifFileType.Exif;
using PaintDotNet;
using PaintDotNet.Dxgi;
using PaintDotNet.FileTypes;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.IO;

namespace AvifFileType
{
    internal static class AvifLoad
    {
        public static IFileTypeDocument Load(IFileTypeDocumentFactory factory, IImagingFactory imagingFactory, Stream input)
        {
            IFileTypeDocument<ColorBgra32>? doc = null;

            using (AvifReader reader = new AvifReader(input, leaveOpen: true, imagingFactory))
            {
                using (AvifReaderImage image = reader.Decode())
                {
                    doc = factory.CreateDocument<ColorBgra32>(image.Size);

                    IFileTypeBitmapLayer<ColorBgra32>? layer = null;
                    bool disposeLayer = true;

                    try
                    {
                        layer = doc.CreateBitmapLayer();
                        using IFileTypeBitmapSink<ColorBgra32> layerBitmapSink = layer.GetBitmap();

                        PixelFormat pixelFormat = image.WICPixelFormat;

                        if (pixelFormat == PixelFormats.Bgra32)
                        {
                            SetOutputLayerDataBgra32(image, layerBitmapSink);
                        }
                        else if (pixelFormat == PixelFormats.Rgba64)
                        {
                            SetOutputLayerDataRgba64(image, layerBitmapSink);
                        }
                        else if (pixelFormat == PixelFormats.Rgba128Float)
                        {
                            switch (image.HDRFormat)
                            {
                                case HDRFormat.PQ:
                                    SetOutputLayerDataRgba128PQ(doc, image, layerBitmapSink, imagingFactory);
                                    break;
                                default:
                                    throw new FormatException($"Unsupported HDR format: {image.HDRFormat}.");
                            }
                        }
                        else
                        {
                            ExceptionUtil.UnsupportedPixelFormat(pixelFormat);
                        }

                        doc.Layers.Add(layer);
                        AddAvifMetadataToDocument(doc, reader, image, imagingFactory);
                        disposeLayer = false;
                    }
                    finally
                    {
                        if (disposeLayer)
                        {
                            layer?.Dispose();
                        }
                    }
                }
            }

            return doc;
        }

        private static void AddAvifMetadataToDocument(IFileTypeDocument doc,
                                                      AvifReader reader,
                                                      AvifReaderImage image,
                                                      IImagingFactory imagingFactory)
        {
            AvifItemData? exif = reader.GetExifData();

            if (exif != null)
            {
                try
                {
                    ExifValueCollection? exifValues = ExifParser.Parse(exif);

                    if (exifValues != null)
                    {
                        exifValues.Remove(ExifPropertyKeys.Image.InterColorProfile.Path);
                        // The HEIF specification states that the EXIF orientation tag is only
                        // informational and should not be used to rotate the image.
                        // See https://github.com/strukturag/libheif/issues/227#issuecomment-642165942
                        exifValues.Remove(ExifPropertyKeys.Image.Orientation.Path);

                        using (IFileTypeExifMetadataTransaction exifTx = doc.Metadata.Exif.CreateTransaction())
                        {
                            exifTx.SetItems(exifValues);
                        }
                    }
                }
                finally
                {
                    exif.Dispose();
                }
            }

            ImageGridMetadata? imageGridMetadata = reader.ImageGridMetadata;

            if (imageGridMetadata != null)
            {
                // TODO: Instead of serializing the data to XML, maybe make use of IFileTypePropertyBag ability to deal with arbitrary T's that implement IParsable<T>
                // customTx.Add("AvifImageGrid.TileColumnCount", imageGridMetadata.TileColumnCount); // Add<T>() supports any IParsable<T>

                string serializedValue = imageGridMetadata.SerializeToString();

                if (serializedValue != null)
                {
                    using IFileTypeCustomMetadataTransaction customTx = doc.Metadata.Custom.CreateTransaction();
                    customTx.Add(AvifMetadataNames.ImageGridName, serializedValue);
                }
            }

            IColorContext? colorContext = GetColorContext(reader, image, imagingFactory);

            if (colorContext != null)
            {
                doc.SetColorContext(colorContext);
            }

            AvifItemData? xmp = reader.GetXmpData();

            if (xmp != null)
            {
                try
                {
                    using (Stream stream = xmp.GetStream())
                    {
                        XmpPacket? xmpPacket = XmpPacket.TryParse(stream);
                        using IFileTypeXmpMetadataTransaction xmpTx = doc.Metadata.Xmp.CreateTransaction();
                        xmpTx.XmpPacket = xmpPacket;
                    }
                }
                finally
                {
                    xmp.Dispose();
                }
            }
        }

        private static IColorContext? GetColorContext(AvifReader reader,
                                                      AvifReaderImage image,
                                                      IImagingFactory imagingFactory)
        {
            try
            {
                // HDR images will set their own color context as part of the conversion
                // to Bgra32.
                if (image.HDRFormat == HDRFormat.None)
                {
                    ReadOnlyMemory<byte> iccProfileBytes = reader.GetICCProfile();

                    if (iccProfileBytes.Length > 0)
                    {
                        if (IccProfileIsRgb(iccProfileBytes.Span))
                        {
                            return imagingFactory.CreateColorContext(iccProfileBytes.Span);
                        }
                    }
                    else
                    {
                        return ColorContextUtil.CreateFromCICPColorInfo(image.CICPColor, imagingFactory);
                    }
                }
            }
            catch (Exception)
            {
                // Do nothing
            }

            return null;

            static bool IccProfileIsRgb(ReadOnlySpan<byte> iccProfile)
            {
                ICCProfile.ProfileHeader header = new ICCProfile.ProfileHeader(iccProfile);

                return header.ColorSpace == ICCProfile.ProfileColorSpace.Rgb;
            }
        }

        private static void SetOutputLayerDataBgra32(AvifReaderImage image, IFileTypeBitmapSink<ColorBgra32> output)
        {
            using (IBitmapSource imageSource = image.IsPremultipliedAlpha ? image.Image.CreateFormatConverter<ColorPbgra32>() : image.Image.CreateRef())
            {
                output.WriteSource(imageSource);
            }
        }

        private static void SetOutputLayerDataRgba64(AvifReaderImage image, IFileTypeBitmapSink<ColorBgra32> output)
        {
            if (image.IsPremultipliedAlpha)
            {
                using (IBitmap asPrgba64 = image.Image.Cast<ColorPrgba64>())
                using (IBitmapSource<ColorBgra32> converted = asPrgba64.CreateFormatConverter<ColorBgra32>())
                {
                    output.WriteSource(converted);
                }
            }
            else
            {
                using (IBitmapSource<ColorBgra32> converted = image.Image.CreateFormatConverter<ColorBgra32>())
                {
                    output.WriteSource(converted);
                }
            }
        }

        private static void SetOutputLayerDataRgba128PQ(
            IFileTypeDocument document,
            AvifReaderImage image,
            IFileTypeBitmapSink<ColorBgra32> output,
            IImagingFactory imagingFactory)
        {
            using IBitmap sourceBitmap = image.IsPremultipliedAlpha ? image.Image.Cast<ColorPrgba128Float>() : image.Image.CreateRef();
            using IColorContext dp3ColorContext = imagingFactory.CreateColorContext(KnownColorSpace.DisplayP3);
            document.SetColorContext(dp3ColorContext);
            using IBitmapSource<ColorBgra32> outputBitmap = sourceBitmap.CreateColorTransformer<ColorBgra32>(DxgiColorSpace.RgbFullGamma2084NoneP2020, dp3ColorContext);
            output.WriteSource(outputBitmap);
        }
    }
}
