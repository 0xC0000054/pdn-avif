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
        public static IFileTypeDocument Load(IFileTypeDocumentFactory factory, Stream input, IImagingFactory imagingFactory)
        {
            using AvifReader reader = new AvifReader(input, leaveOpen: true, imagingFactory);
            using AvifReaderImage image = reader.Decode();

            // Dispatch from pixel format enum to the pixel format's Color struct type
            PixelFormat pixelFormat = image.WICPixelFormat;

            if (pixelFormat == PixelFormats.Bgra32)
            {
                return image.IsPremultipliedAlpha
                    ? Load<ColorPbgra32>(factory, reader, image, imagingFactory)
                    : Load<ColorBgra32>(factory, reader, image, imagingFactory);
            }
            else if (pixelFormat == PixelFormats.Rgba64)
            {
                return image.IsPremultipliedAlpha
                    ? Load<ColorPrgba64>(factory, reader, image, imagingFactory)
                    : Load<ColorRgba64>(factory, reader, image, imagingFactory);
            }
            else if (pixelFormat == PixelFormats.Rgba128Float)
            {
                return image.IsPremultipliedAlpha
                    ? Load<ColorPrgba128Float>(factory, reader, image, imagingFactory)
                    : Load<ColorRgba128Float>(factory, reader, image, imagingFactory);
            }
            else
            {
                ExceptionUtil.UnsupportedPixelFormat(pixelFormat);
                return null!; // Unreachable
            }
        }

        private static IFileTypeDocument Load<TPixel>(IFileTypeDocumentFactory factory,
                                                      AvifReader reader,
                                                      AvifReaderImage image,
                                                      IImagingFactory imagingFactory)
                                                      where TPixel : unmanaged, INaturalPixelInfo
        {
            using IBitmap<TPixel> source = image.Image.Cast<TPixel>();
            IFileTypeDocument<TPixel> doc = factory.CreateDocument<TPixel>(image.Size);

            using IFileTypeBitmapLayer<TPixel> layer = doc.CreateBitmapLayer();
            using IFileTypeBitmapSink<TPixel> layerBitmapSink = layer.GetBitmap();

            PixelFormatNumericRepresentation formatRepresentation = default(TPixel).NumericRepresentation;

            if (formatRepresentation == PixelFormatNumericRepresentation.Float)
            {
                // Floating-point formats are HDR
                if (image.HDRFormat != HDRFormat.PQ)
                {
                    throw new FormatException($"Unsupported HDR format for PixelFormat.{default(TPixel).PixelFormat.GetName()}: {image.HDRFormat}.");
                }

                // Convert to scRGB and let PDN figure out how best to handle it (e.g. PDN 5.2 will transform to SDR BGRA32 Display P3)
                using IColorContext scRgbColorContext = imagingFactory.CreateColorContext(KnownColorSpace.ScRgb);
                doc.SetColorContext(scRgbColorContext);

                using IBitmapSource<TPixel> converted = source.CreateColorTransformer(DxgiColorSpace.RgbFullGamma2084NoneP2020, scRgbColorContext);
                layerBitmapSink.WriteSource(converted);
            }
            else if (formatRepresentation == PixelFormatNumericRepresentation.UnsignedInteger)
            {
                // Integer pixel formats are SDR
                if (image.HDRFormat != HDRFormat.None)
                {
                    throw new FormatException($"Unsupported HDR format for PixelFormat.{default(TPixel).PixelFormat.GetName()}: {image.HDRFormat}.");
                }

                layerBitmapSink.WriteSource(source);
            }
            else
            {
                // Fixed-point, signed integer, and indexed are not supported
                throw new FormatException($"PixelFormat.{default(TPixel).PixelFormat.GetName()} is unsupported because of its numeric representation: {formatRepresentation}");
            }

            doc.Layers.Add(layer);
            AddAvifMetadataToDocument(doc, reader, image, imagingFactory);

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
                using IFileTypeCustomMetadataTransaction customTx = doc.Metadata.Custom.CreateTransaction();
                imageGridMetadata.SerializeToPropertyBag(customTx);
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
                // HDR images will handle their own conversion from BT.2020 to scRGB
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
    }
}
