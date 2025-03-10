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
using PaintDotNet.Direct2D1;
using PaintDotNet.Direct2D1.Effects;
using PaintDotNet.Dxgi;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.IO;

namespace AvifFileType
{
    internal static class AvifLoad
    {
        public static Document Load(Stream input)
        {
            Document? doc = null;

            using (IImagingFactory imagingFactory = ImagingFactory.CreateRef())
            using (AvifReader reader = new AvifReader(input, leaveOpen: true, imagingFactory))
            {
                using (AvifReaderImage image = reader.Decode())
                {
                    doc = new Document(image.Size);

                    BitmapLayer? layer = null;
                    bool disposeLayer = true;

                    try
                    {
                        layer = Layer.CreateBackgroundLayer(image.Size);

                        PixelFormat pixelFormat = image.WICPixelFormat;

                        if (pixelFormat == PixelFormats.Bgra32)
                        {
                            SetOutputLayerDataBgra32(image, layer.Surface);
                        }
                        else if (pixelFormat == PixelFormats.Rgba64)
                        {
                            SetOutputLayerDataRgba64(image, layer.Surface);
                        }
                        else if (pixelFormat == PixelFormats.Rgba128Float)
                        {
                            switch (image.HDRFormat)
                            {
                                case HDRFormat.PQ:
                                    SetOutputLayerDataRgba128PQ(doc, image, layer.Surface, imagingFactory);
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

        private static void AddAvifMetadataToDocument(Document doc,
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

                        foreach (KeyValuePair<ExifPropertyPath, ExifValue> item in exifValues)
                        {
                            ExifPropertyPath path = item.Key;

                            doc.Metadata.AddExifPropertyItem(path.Section, path.TagID, item.Value);
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
                string serializedValue = imageGridMetadata.SerializeToString();

                if (serializedValue != null)
                {
                    doc.Metadata.SetUserValue(AvifMetadataNames.ImageGridName, serializedValue);
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
                        if (xmpPacket != null)
                        {
                            doc.Metadata.SetXmpPacket(xmpPacket);
                        }
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

        private static void SetOutputLayerDataBgra32(AvifReaderImage image, Surface output)
        {
            using (IBitmapLock bitmapLock = image.Image.Lock(BitmapLockOptions.Read))
            {
                RegionPtr<ColorBgra32> region = bitmapLock.AsRegionPtr<ColorBgra32>();

                region.CopyTo(output.AsRegionPtr().Cast<ColorBgra32>());
            }

            if (image.IsPremultipliedAlpha)
            {
                output.ConvertFromPremultipliedAlpha();
            }
        }

        private static void SetOutputLayerDataRgba64(AvifReaderImage image, Surface output)
        {
            if (image.IsPremultipliedAlpha)
            {
                using (IBitmap asPrgba64 = image.Image.Cast<ColorPrgba64>())
                using (IBitmapSource<ColorBgra32> converted = asPrgba64.CreateFormatConverter<ColorBgra32>())
                {
                    converted.CopyPixels(output.AsRegionPtr().Cast<ColorBgra32>());
                }
            }
            else
            {
                using (IBitmapSource<ColorBgra32> converted = image.Image.CreateFormatConverter<ColorBgra32>())
                {
                    converted.CopyPixels(output.AsRegionPtr().Cast<ColorBgra32>());
                }
            }
        }

        private static void SetOutputLayerDataRgba128PQ(
            Document document,
            AvifReaderImage image,
            Surface output,
            IImagingFactory imagingFactory)
        {
            using (IColorContext dp3ColorContext = imagingFactory.CreateColorContext(KnownColorSpace.DisplayP3))
            using (IDirect2DFactory d2dFactory = Direct2DFactory.Create())
            {
                // Our premultiplied image uses ColorRgba128, casting it to ColorPrgba128Float makes Direct2D treat the image as premultiplied.
                using (IBitmap bitmap = image.IsPremultipliedAlpha ? image.Image.Cast<ColorPrgba128Float>() : image.Image.CreateRef())
                using (IBitmapSource<ColorPbgra32> dp3Image = PQToColorContext(bitmap,
                                                                               imagingFactory,
                                                                               d2dFactory,
                                                                               dp3ColorContext))
                {
                    dp3Image.CopyPixels(output.AsRegionPtr().Cast<ColorPbgra32>());
                }

                document.SetColorContext(dp3ColorContext);
            }

            static IBitmapSource<ColorPbgra32> PQToColorContext(
                IBitmap bitmap,
                IImagingFactory imagingFactory,
                IDirect2DFactory d2dFactory,
                IColorContext colorContext)
            {
                return d2dFactory.CreateBitmapSourceFromImage<ColorPbgra32>(
                    bitmap.Size,
                    DevicePixelFormats.Prgba128Float,
                    delegate (IDeviceContext dc)
                    {
                        dc.EffectBufferPrecision = BufferPrecision.Float32;
                        using IDeviceImage srcImage = dc.CreateImageFromBitmap(bitmap, null, BitmapImageOptions.UseStraightAlpha | BitmapImageOptions.DisableColorSpaceConversion);
                        using IDeviceColorContext srcColorContext = dc.CreateColorContext(DxgiColorSpace.RgbFullGamma2084NoneP2020);
                        using IDeviceColorContext dstColorContext = dc.CreateColorContext(colorContext);

                        ColorManagementEffect colorMgmtEffect = new ColorManagementEffect(
                            dc,
                            srcImage,
                            srcColorContext,
                            dstColorContext,
                            ColorManagementAlphaMode.Straight);

                        return colorMgmtEffect;
                    });
            }
        }
    }
}
