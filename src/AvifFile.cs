////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.AvifContainer;
using AvifFileType.Exif;
using AvifFileType.Interop;
using PaintDotNet;
using PaintDotNet.Imaging;
using System;
using System.Collections.Generic;
using System.IO;

namespace AvifFileType
{
    internal static class AvifFile
    {
        private const string NclxMetadataName = "AvifNclxData";

        public static Document Load(Stream input)
        {
            Document doc = null;

            using (AvifReader reader = new AvifReader(input, leaveOpen: true))
            {
                Surface surface = null;
                bool disposeSurface = true;

                try
                {
                    surface = reader.Decode();

                    doc = new Document(surface.Width, surface.Height);

                    AddAvifMetadataToDocument(doc, reader);

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

        public static void Save(Document document,
                         Stream output,
                         int quality,
                         CompressionMode compressionMode,
                         YUVChromaSubsampling chromaSubsampling,
                         Surface scratchSurface,
                         ProgressEventHandler progressCallback)
        {
            using (RenderArgs args = new RenderArgs(scratchSurface))
            {
                document.Render(args, true);
            }

            bool grayscale = IsGrayscaleImage(scratchSurface);

            AvifMetadata metadata = CreateAvifMetadata(document);
            EncoderOptions options = new EncoderOptions
            {
                quality = quality,
                compressionMode = compressionMode,
                // YUV 4:0:0 is always used for gray-scale images because it
                // produces the smallest file size with no quality loss.
                yuvFormat = grayscale ? YUVChromaSubsampling.Subsampling400 : chromaSubsampling
            };

            ColorConversionInfo colorConversionInfo = null;
            ColorInformationBox colorInformationBox = null;

            if (quality == 100 && !grayscale)
            {
                options.yuvFormat = YUVChromaSubsampling.IdentityMatrix;

                // These NCLX color values are from the AV1 Bitstream & Decoding Process Specification.
                const NclxColorPrimaries colorPrimaries = NclxColorPrimaries.BT709;
                const NclxTransferCharacteristics transferCharacteristics = NclxTransferCharacteristics.Srgb;
                const NclxMatrixCoefficients matrixCoefficients = NclxMatrixCoefficients.Identity;
                const bool fullRange = true;

                // The Identity matrix coefficient places the RGB values into the YUV planes without any conversion.
                // This reduces the compression efficiency, but allows for fully lossless encoding.

                colorConversionInfo = new ColorConversionInfo(colorPrimaries, transferCharacteristics, matrixCoefficients, fullRange);
                colorInformationBox = new NclxColorInformation(colorPrimaries, transferCharacteristics, matrixCoefficients, fullRange);
            }
            else
            {
                byte[] iccProfileBytes = metadata.GetICCProfileBytesReadOnly();
                if (iccProfileBytes != null && iccProfileBytes.Length > 0)
                {
                    // Gray-scale images do not need any color conversion information, it is only
                    // required when converting color images.
                    colorConversionInfo = grayscale ? null : new ColorConversionInfo(iccProfileBytes);
                    colorInformationBox = new IccProfileColorInformation(iccProfileBytes);
                }
                else
                {
                    // The NCLX color conversion information is not relevant for gray-scale images.
                    // The color conversion information determines the values used to subsample the YUV chroma planes,
                    // and a gray-scale image does not have chroma planes.
                    if (!grayscale)
                    {
                        string serializedNclx = document.Metadata.GetUserValue(NclxMetadataName);

                        if (serializedNclx != null)
                        {
                            NclxColorInformation nclxColorInformation = NclxSerializer.TryDeserialize(serializedNclx);

                            if (nclxColorInformation != null)
                            {
                                colorConversionInfo = new ColorConversionInfo(nclxColorInformation);
                                colorInformationBox = nclxColorInformation;
                            }
                        }
                    }
                }
            }

            CompressedAV1Image color = null;
            CompressedAV1Image alpha = null;

            bool hasTransparency = HasTransparency(scratchSurface);

            // Progress is reported at the following stages:
            // 1. Before converting the image to the YUV color space
            // 2. Before compressing the color image
            // 3. After compressing the color image
            // 4. After compressing the alpha image (if present)
            // 5. After writing the color image to the file
            // 6. After writing the alpha image to the file (if present)

            uint progressDone = 0;
            uint progressTotal = hasTransparency ? 6U : 4U;

            try
            {
                if (hasTransparency)
                {
                    AvifNative.CompressWithTransparency(scratchSurface,
                                                        options,
                                                        ReportCompressionProgress,
                                                        ref progressDone,
                                                        progressTotal,
                                                        colorConversionInfo,
                                                        out color,
                                                        out alpha);
                }
                else
                {
                    AvifNative.CompressWithoutTransparency(scratchSurface,
                                                           options,
                                                           ReportCompressionProgress,
                                                           ref progressDone,
                                                           progressTotal,
                                                           colorConversionInfo,
                                                           out color);
                }

                AvifWriter writer = new AvifWriter(color,
                                                   alpha,
                                                   metadata,
                                                   colorInformationBox,
                                                   progressCallback,
                                                   progressDone,
                                                   progressTotal);
                writer.WriteTo(output);
            }
            finally
            {
                color?.Dispose();
                alpha?.Dispose();
            }

            bool ReportCompressionProgress(uint done, uint total)
            {
                try
                {
                    progressCallback?.Invoke(null, new ProgressEventArgs(((double)done / total) * 100.0, true));
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        private static void AddAvifMetadataToDocument(Document doc, AvifReader reader)
        {
            byte[] exifBytes = reader.GetExifData();

            if (exifBytes != null)
            {
                ExifValueCollection exifValues = ExifParser.Parse(exifBytes);

                foreach (MetadataEntry entry in exifValues)
                {
                    doc.Metadata.AddExifPropertyItem(entry.CreateExifPropertyItem());
                }
            }

            ColorInformationBox colorInformation = reader.ColorInformationBox;

            if (colorInformation != null)
            {
                if (colorInformation is NclxColorInformation nclx)
                {
                    string serializedValue = NclxSerializer.TrySerialize(nclx);

                    if (serializedValue != null)
                    {
                        doc.Metadata.SetUserValue(NclxMetadataName, serializedValue);
                    }
                }
                else if (colorInformation is IccProfileColorInformation iccProfile)
                {
                    doc.Metadata.AddExifPropertyItem(ExifSection.Image,
                                                     unchecked((ushort)ExifTagID.IccProfileData),
                                                     new ExifValue(ExifValueType.Undefined,
                                                                   iccProfile.GetProfileBytes()));
                }
            }

            byte[] xmpBytes = reader.GetXmpData();

            if (xmpBytes != null)
            {
                XmpPacket xmpPacket = XmpPacket.TryParse(xmpBytes);
                if (xmpPacket != null)
                {
                    doc.Metadata.SetXmpPacket(xmpPacket);
                }
            }
        }

        private static AvifMetadata CreateAvifMetadata(Document doc)
        {
            byte[] exifBytes = null;
            byte[] iccProfileBytes = null;
            byte[] xmpBytes = null;

            Dictionary<MetadataKey, MetadataEntry> exifMetadata = GetExifMetadataFromDocument(doc);

            if (exifMetadata != null)
            {
                Exif.ExifColorSpace exifColorSpace = Exif.ExifColorSpace.Srgb;

                MetadataKey iccProfileKey = MetadataKeys.Image.InterColorProfile;

                if (exifMetadata.TryGetValue(iccProfileKey, out MetadataEntry iccProfileItem))
                {
                    iccProfileBytes = iccProfileItem.GetData();
                    exifMetadata.Remove(iccProfileKey);
                    exifColorSpace = Exif.ExifColorSpace.Uncalibrated;
                }

                exifBytes = new ExifWriter(doc, exifMetadata, exifColorSpace).CreateExifBlob();
            }

            XmpPacket xmpPacket = doc.Metadata.TryGetXmpPacket();
            if (xmpPacket != null)
            {
                string packetAsString = xmpPacket.ToString(XmpPacketWrapperType.ReadOnly);

                xmpBytes = System.Text.Encoding.UTF8.GetBytes(packetAsString);
            }

            return new AvifMetadata(exifBytes, iccProfileBytes, xmpBytes);
        }

        private static Dictionary<MetadataKey, MetadataEntry> GetExifMetadataFromDocument(Document doc)
        {
            Dictionary<MetadataKey, MetadataEntry> items = null;

            Metadata metadata = doc.Metadata;

            ExifPropertyItem[] exifProperties = metadata.GetExifPropertyItems();

            if (exifProperties.Length > 0)
            {
                items = new Dictionary<MetadataKey, MetadataEntry>(exifProperties.Length);

                foreach (ExifPropertyItem property in exifProperties)
                {
                    MetadataSection section;
                    switch (property.Path.Section)
                    {
                        case ExifSection.Image:
                            section = MetadataSection.Image;
                            break;
                        case ExifSection.Photo:
                            section = MetadataSection.Exif;
                            break;
                        case ExifSection.Interop:
                            section = MetadataSection.Interop;
                            break;
                        case ExifSection.GpsInfo:
                            section = MetadataSection.Gps;
                            break;
                        default:
                            throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                                                              "Unexpected {0} type: {1}",
                                                                              nameof(ExifSection),
                                                                              (int)property.Path.Section));
                    }

                    MetadataKey metadataKey = new MetadataKey(section, property.Path.TagID);

                    if (!items.ContainsKey(metadataKey))
                    {
                        byte[] clonedData = PaintDotNet.Collections.EnumerableExtensions.ToArrayEx(property.Value.Data);

                        items.Add(metadataKey, new MetadataEntry(metadataKey, (TagDataType)property.Value.Type, clonedData));
                    }
                }
            }

            return items;
        }

        private static unsafe bool IsGrayscaleImage(Surface surface)
        {
            for (int y = 0; y < surface.Height; y++)
            {
                ColorBgra* ptr = surface.GetRowAddressUnchecked(y);
                ColorBgra* ptrEnd = ptr + surface.Width;

                while (ptr < ptrEnd)
                {
                    if (!(ptr->R == ptr->G && ptr->G == ptr->B))
                    {
                        return false;
                    }
                    ptr++;
                }
            }

            return true;
        }

        private static unsafe bool HasTransparency(Surface surface)
        {
            for (int y = 0; y < surface.Height; y++)
            {
                ColorBgra* ptr = surface.GetRowAddressUnchecked(y);
                ColorBgra* ptrEnd = ptr + surface.Width;

                while (ptr < ptrEnd)
                {
                    if (ptr->A < 255)
                    {
                        return true;
                    }

                    ptr++;
                }
            }

            return false;
        }
    }
}
