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
using PaintDotNet.Imaging;
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
            using (AvifReader reader = new AvifReader(input, leaveOpen: true))
            {
                Surface? surface = null;
                bool disposeSurface = true;

                try
                {
                    surface = reader.Decode();

                    doc = new Document(surface.Width, surface.Height);

                    AddAvifMetadataToDocument(doc, reader, imagingFactory);

                    doc.Layers.Add(Layer.CreateBackgroundLayer(surface, takeOwnership: true));
                    disposeSurface = false;
                }
                finally
                {
                    if (disposeSurface)
                    {
                        surface?.Dispose();
                    }
                }
            }

            return doc;
        }

        private static void AddAvifMetadataToDocument(Document doc,
                                                      AvifReader reader,
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

            CICPColorData? imageColorData = reader.ImageColorData;

            if (imageColorData.HasValue)
            {
                string? serializedValue = CICPSerializer.TrySerialize(imageColorData.Value);

                if (serializedValue != null)
                {
                    doc.Metadata.SetUserValue(AvifMetadataNames.CICPMetadataName, serializedValue);
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

            IColorContext? colorContext = GetColorContext(reader, imagingFactory);

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

        private static IColorContext? GetColorContext(AvifReader reader, IImagingFactory imagingFactory)
        {
            try
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
                    return ColorContextUtil.CreateFromCicpColorInfo(reader.ImageColorData, imagingFactory);
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
