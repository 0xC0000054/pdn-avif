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
using System.ComponentModel;
using System.Drawing;
using System.IO;

namespace AvifFileType
{
    internal static class AvifFile
    {
        private const string CICPMetadataName = "AvifCICPData";
        private const string ImageGridName = "AvifImageGrid";
        // This value is no longer written, but it is retained to
        // allow the data to be read from existing PDN files.
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
                         CompressionSpeed compressionSpeed,
                         YUVChromaSubsampling chromaSubsampling,
                         bool preserveExistingTileSize,
                         int? maxEncoderThreadsOverride,
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
                compressionSpeed = compressionSpeed,
                // YUV 4:0:0 is always used for gray-scale images because it
                // produces the smallest file size with no quality loss.
                yuvFormat = grayscale ? YUVChromaSubsampling.Subsampling400 : chromaSubsampling,
                maxThreads = maxEncoderThreadsOverride ?? Environment.ProcessorCount
            };

            // Use BT.709 with sRGB transfer characteristics as the default.
            CICPColorData colorConversionInfo = new CICPColorData
            {
                colorPrimaries = CICPColorPrimaries.BT709,
                transferCharacteristics = CICPTransferCharacteristics.Srgb,
                matrixCoefficients = CICPMatrixCoefficients.BT709,
                fullRange = true
            };

            if (quality == 100 && !grayscale)
            {
                // The Identity matrix coefficient places the RGB values into the YUV planes without any conversion.
                // This reduces the compression efficiency, but allows for fully lossless encoding.

                options.yuvFormat = YUVChromaSubsampling.IdentityMatrix;

                // These CICP color values are from the AV1 Bitstream & Decoding Process Specification.
                colorConversionInfo = new CICPColorData
                {
                    colorPrimaries = CICPColorPrimaries.BT709,
                    transferCharacteristics = CICPTransferCharacteristics.Srgb,
                    matrixCoefficients = CICPMatrixCoefficients.Identity,
                    fullRange = true
                };
            }
            else
            {
                Metadata docMetadata = document.Metadata;

                // Look for NCLX meta-data if the CICP meta-data was not found.
                // This preserves backwards compatibility with PDN files created by
                // previous versions of this plugin.
                string serializedData = docMetadata.GetUserValue(CICPMetadataName) ?? docMetadata.GetUserValue(NclxMetadataName);

                if (serializedData != null)
                {
                    CICPColorData? colorData = CICPSerializer.TryDeserialize(serializedData);

                    if (colorData.HasValue)
                    {
                        colorConversionInfo = colorData.Value;
                    }
                }
            }

            ImageGridMetadata imageGridMetadata = TryGetImageGridMetadata(document,
                                                                          options.compressionSpeed,
                                                                          options.yuvFormat,
                                                                          preserveExistingTileSize);

            bool hasTransparency = HasTransparency(scratchSurface);

            CompressedAV1ImageCollection colorImages = new CompressedAV1ImageCollection(imageGridMetadata?.TileCount ?? 1);
            CompressedAV1ImageCollection alphaImages = hasTransparency ? new CompressedAV1ImageCollection(colorImages.Capacity) : null;

            // Progress is reported at the following stages:
            // 1. Before converting the image to the YUV color space
            // 2. Before compressing the color image
            // 3. After compressing the color image
            // 4. After compressing the alpha image (if present)
            // 5. After writing the color image to the file
            // 6. After writing the alpha image to the file (if present)

            uint progressDone = 0;
            uint progressTotal = hasTransparency ? 6U : 4U;
            if (colorImages.Capacity > 1)
            {
                progressTotal *= (uint)colorImages.Capacity;
            }

            try
            {
                Rectangle[] windowRectangles = GetTileWindowRectangles(imageGridMetadata, document);

                for (int i = 0; i < colorImages.Capacity; i++)
                {
                    CompressedAV1Image color = null;
                    CompressedAV1Image alpha = null;

                    try
                    {
                        Rectangle windowRect = windowRectangles[i];
                        using (Surface window = scratchSurface.CreateWindow(windowRect))
                        {
                            if (hasTransparency)
                            {
                                AvifNative.CompressWithTransparency(window,
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
                                AvifNative.CompressWithoutTransparency(window,
                                                                       options,
                                                                       ReportCompressionProgress,
                                                                       ref progressDone,
                                                                       progressTotal,
                                                                       colorConversionInfo,
                                                                       out color);
                            }
                        }

                        colorImages.Add(color);
                        color = null;
                        if (hasTransparency)
                        {
                            alphaImages.Add(alpha);
                            alpha = null;
                        }
                    }
                    finally
                    {
                        color?.Dispose();
                        alpha?.Dispose();
                    }
                }


                ColorInformationBox colorInformationBox;

                byte[] iccProfileBytes = metadata.GetICCProfileBytesReadOnly();
                if (iccProfileBytes != null && iccProfileBytes.Length > 0)
                {
                    colorInformationBox = new IccProfileColorInformation(iccProfileBytes);
                }
                else
                {
                    colorInformationBox = new NclxColorInformation(colorConversionInfo.colorPrimaries,
                                                                   colorConversionInfo.transferCharacteristics,
                                                                   colorConversionInfo.matrixCoefficients,
                                                                   colorConversionInfo.fullRange);
                }

                AvifWriter writer = new AvifWriter(colorImages,
                                                   alphaImages,
                                                   metadata,
                                                   imageGridMetadata,
                                                   options.yuvFormat,
                                                   colorInformationBox,
                                                   progressCallback,
                                                   progressDone,
                                                   progressTotal);
                writer.WriteTo(output);
            }
            finally
            {
                colorImages?.Dispose();
                alphaImages?.Dispose();
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

                if (exifValues != null)
                {
                    exifValues.Remove(MetadataKeys.Image.InterColorProfile);
                    // The HEIF specification states that the EXIF orientation tag is only
                    // informational and should not be used to rotate the image.
                    // See https://github.com/strukturag/libheif/issues/227#issuecomment-642165942
                    exifValues.Remove(MetadataKeys.Image.Orientation);

                    foreach (MetadataEntry entry in exifValues)
                    {
                        doc.Metadata.AddExifPropertyItem(entry.CreateExifPropertyItem());
                    }
                }
            }

            CICPColorData? imageColorData = reader.ImageColorData;

            if (imageColorData.HasValue)
            {
                string serializedValue = CICPSerializer.TrySerialize(imageColorData.Value);

                if (serializedValue != null)
                {
                    doc.Metadata.SetUserValue(CICPMetadataName, serializedValue);
                }
            }

            ImageGridMetadata imageGridMetadata = reader.ImageGridMetadata;

            if (imageGridMetadata != null)
            {
                string serializedValue = imageGridMetadata.SerializeToString();

                if (serializedValue != null)
                {
                    doc.Metadata.SetUserValue(ImageGridName, serializedValue);
                }
            }

            byte[] iccProfileBytes = reader.GetICCProfile();

            if (iccProfileBytes != null)
            {
                doc.Metadata.AddExifPropertyItem(ExifSection.Image,
                                                 unchecked((ushort)ExifTagID.IccProfileData),
                                                 new ExifValue(ExifValueType.Undefined,
                                                               iccProfileBytes));
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

        private static Rectangle[] GetTileWindowRectangles(ImageGridMetadata imageGridMetadata, Document document)
        {
            Rectangle[] rects;

            if (imageGridMetadata is null)
            {
                rects = new Rectangle[] { document.Bounds };
            }
            else
            {
                rects = new Rectangle[imageGridMetadata.TileCount];

                // The tiles are encoded from top to bottom then left to right.

                int tileWidth = checked((int)imageGridMetadata.TileImageWidth);
                int tileHeight = checked((int)imageGridMetadata.TileImageHeight);

                for (int row = 0; row < imageGridMetadata.TileRowCount; row++)
                {
                    int startIndex = row * imageGridMetadata.TileColumnCount;
                    int y = row * tileHeight;

                    for (int col = 0; col < imageGridMetadata.TileColumnCount; col++)
                    {
                        int index = startIndex + col;
                        int x = col * tileWidth;

                        rects[index] = new Rectangle(x, y, tileWidth, tileHeight);
                    }
                }
            }

            return rects;
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

        private static ImageGridMetadata TryCalculateBestTileSize(
            Document document,
            CompressionSpeed compressionSpeed)
        {
            // This is the largest number of horizontal and vertical tiles that an image grid can have.
            // While this would result in the image using 65536 tiles in the worst case, it allows
            // memory usage to be minimized when encoding extremely wide and/or tall images.
            //
            // For example, a 65536x65536 pixel image would use a 128x128 grid of 512x512 pixel tiles.
            const int MaxTileCount = 256;
            // The MIAF specification (ISO/IEC 23000-22) requires that the tile size be at least 64x64 pixels.
            const int MinTileSize = 64;

            int maxTileSize;

            switch (compressionSpeed)
            {
                case CompressionSpeed.Fast:
                    maxTileSize = 512;
                    break;
                case CompressionSpeed.Medium:
                    maxTileSize = 1280;
                    break;
                case CompressionSpeed.Slow:
                    maxTileSize = 1920;
                    break;
                case CompressionSpeed.VerySlow:
                    // Tiles are not used for the very slow compression speed.
                    return null;
                default:
                    throw new InvalidEnumArgumentException(nameof(compressionSpeed), (int)compressionSpeed, typeof(CompressionSpeed));
            }

            int bestTileColumnCount = 1;
            int bestTileWidth = document.Width;
            int bestTileRowCount = 1;
            int bestTileHeight = document.Height;

            if (document.Width > maxTileSize)
            {
                for (int tileColumnCount = 2; tileColumnCount <= MaxTileCount; tileColumnCount++)
                {
                    int tileWidth = document.Width / tileColumnCount;

                    if (tileWidth < MinTileSize)
                    {
                        break;
                    }

                    if ((tileWidth & 1) == 0 && (tileWidth * tileColumnCount) == document.Width)
                    {
                        bestTileWidth = tileWidth;
                        bestTileColumnCount = tileColumnCount;

                        if (tileWidth <= maxTileSize)
                        {
                            break;
                        }
                    }
                }
            }

            if (document.Height > maxTileSize)
            {
                if (document.Width == document.Height)
                {
                    // Square images use the same number of horizontal and vertical tiles.
                    bestTileHeight = bestTileWidth;
                    bestTileRowCount = bestTileColumnCount;
                }
                else
                {
                    for (int tileRowCount = 2; tileRowCount <= MaxTileCount; tileRowCount++)
                    {
                        int tileHeight = document.Height / tileRowCount;

                        if (tileHeight < MinTileSize)
                        {
                            break;
                        }

                        if ((tileHeight & 1) == 0 && (tileHeight * tileRowCount) == document.Height)
                        {
                            bestTileHeight = tileHeight;
                            bestTileRowCount = tileRowCount;

                            if (tileHeight <= maxTileSize)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            ImageGridMetadata metadata = null;

            if (bestTileColumnCount > 1 || bestTileRowCount > 1)
            {
                metadata = new ImageGridMetadata(bestTileColumnCount,
                                                 bestTileRowCount,
                                                 (uint)document.Height,
                                                 (uint)document.Width,
                                                 (uint)bestTileHeight,
                                                 (uint)bestTileWidth);
            }

            return metadata;
        }

        private static ImageGridMetadata TryGetImageGridMetadata(
            Document document,
            CompressionSpeed compressionSpeed,
            YUVChromaSubsampling yuvFormat,
            bool preserveExistingTileSize)
        {
            ImageGridMetadata metadata = null;

            // The VerySlow compression speed always encodes the image as a single tile.
            if (compressionSpeed != CompressionSpeed.VerySlow)
            {
                // The image must have an even size to be eligible for tiling.
                if ((document.Width & 1) == 0 && (document.Height & 1) == 0)
                {
                    if (preserveExistingTileSize)
                    {
                        string value = document.Metadata.GetUserValue(ImageGridName);

                        if (!string.IsNullOrEmpty(value))
                        {
                            ImageGridMetadata serializedData = ImageGridMetadata.TryDeserialize(value);

                            if (serializedData != null
                                && serializedData.IsValidForImage((uint)document.Width, (uint)document.Height, yuvFormat))
                            {
                                metadata = serializedData;
                            }
                        }
                    }

                    if (metadata is null)
                    {
                        metadata = TryCalculateBestTileSize(document, compressionSpeed);
                    }
                }
            }

            return metadata;
        }
    }
}
