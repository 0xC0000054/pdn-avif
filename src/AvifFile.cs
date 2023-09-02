////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.AvifContainer;
using AvifFileType.Exif;
using AvifFileType.Interop;
using PaintDotNet;
using PaintDotNet.Collections;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;

using ExifColorSpace = AvifFileType.Exif.ExifColorSpace;

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
            Document? doc = null;

            using (AvifReader reader = new AvifReader(input, leaveOpen: true))
            {
                Surface? surface = null;
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
                                bool lossless,
                                bool losslessAlpha,
                                EncoderPreset encoderPreset,
                                YUVChromaSubsampling chromaSubsampling,
                                bool preserveExistingTileSize,
                                bool premultipliedAlpha,
                                Surface scratchSurface,
                                ProgressEventHandler progressCallback)
        {
            if (lossless)
            {
                losslessAlpha = true;
                // The premultiplied alpha conversion can cause the colors to drift, so it is disabled for lossless encoding.
                premultipliedAlpha = false;
            }

            scratchSurface.Fill(ColorBgra.TransparentBlack);
            document.CreateRenderer().Render(scratchSurface);

            bool grayscale = IsGrayscaleImage(scratchSurface);

            AvifMetadata metadata = CreateAvifMetadata(document);
            EncoderOptions options = new EncoderOptions
            {
                quality = quality,
                encoderPreset = encoderPreset,
                // YUV 4:0:0 is always used for gray-scale images because it
                // produces the smallest file size with no quality loss.
                yuvFormat = grayscale ? YUVChromaSubsampling.Subsampling400 : chromaSubsampling,
                maxThreads = Environment.ProcessorCount,
                lossless = lossless,
                losslessAlpha = losslessAlpha,
            };

            // Use BT.709 with sRGB transfer characteristics as the default.
            CICPColorData colorConversionInfo = new CICPColorData
            {
                colorPrimaries = CICPColorPrimaries.BT709,
                transferCharacteristics = CICPTransferCharacteristics.Srgb,
                matrixCoefficients = CICPMatrixCoefficients.BT709,
                fullRange = true
            };

            if (lossless && !grayscale)
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
                string? serializedData = docMetadata.GetUserValue(CICPMetadataName) ?? docMetadata.GetUserValue(NclxMetadataName);

                if (serializedData != null)
                {
                    CICPColorData? colorData = CICPSerializer.TryDeserialize(serializedData);

                    if (colorData.HasValue)
                    {
                        colorConversionInfo = colorData.Value;
                    }
                }
            }

            ImageGridMetadata? imageGridMetadata = TryGetImageGridMetadata(document,
                                                                           options.encoderPreset,
                                                                           options.yuvFormat,
                                                                           preserveExistingTileSize);

            bool hasTransparency = HasTransparency(scratchSurface);

            if (hasTransparency && premultipliedAlpha)
            {
                scratchSurface.ConvertToPremultipliedAlpha();
            }

            CompressedAV1ImageCollection colorImages = new CompressedAV1ImageCollection(imageGridMetadata?.TileCount ?? 1);
            CompressedAV1ImageCollection? alphaImages = hasTransparency ? new CompressedAV1ImageCollection(colorImages.Capacity) : null;

            // Progress is reported at the following stages:
            // 1. Before compressing the color image
            // 2. After compressing the color image
            // 3. Before compressing the alpha image (if present)
            // 4. After compressing the alpha image (if present)
            // 5. After writing the color image to the file
            // 6. After writing the alpha image to the file (if present)

            uint progressDone = 0;
            uint progressTotal = hasTransparency ? 6U : 3U;
            if (colorImages.Capacity > 1)
            {
                progressTotal *= (uint)colorImages.Capacity;
            }

            try
            {
                Rectangle[] windowRectangles = GetTileWindowRectangles(imageGridMetadata, document);
                HomogeneousTileInfo homogeneousTileInfo = GetHomogeneousTileInfo(scratchSurface, windowRectangles, hasTransparency);

                for (int i = 0; i < colorImages.Capacity; i++)
                {
                    // Homogeneous (single color) tiles will be compressed once and any subsequent tiles will reuse
                    // the compressed data from the first tile.
                    // This can significantly reduce the compression time for images that contain large areas of a
                    // single color.
                    if (homogeneousTileInfo.DuplicateColorTileMap.TryGetValue(i, out int duplicateTileIndex))
                    {
                        colorImages.Add(colorImages[duplicateTileIndex]);

                        progressDone += 2U;
                        progressCallback?.Invoke(null, new ProgressEventArgs(((double)progressDone / progressTotal) * 100.0, true));
                    }
                    else
                    {
                        CompressedAV1Image? color = null;

                        try
                        {
                            Rectangle windowRect = windowRectangles[i];
                            using (Surface window = scratchSurface.CreateWindow(windowRect))
                            {
                                AvifNative.CompressColorImage(window,
                                                              options,
                                                              ReportCompressionProgress,
                                                              ref progressDone,
                                                              progressTotal,
                                                              colorConversionInfo,
                                                              out color);
                            }

                            colorImages.Add(color);
                            color = null;
                        }
                        finally
                        {
                            color?.Dispose();
                        }
                    }

                    if (alphaImages != null)
                    {
                        if (homogeneousTileInfo.DuplicateAlphaTileMap.TryGetValue(i, out duplicateTileIndex))
                        {
                            alphaImages.Add(alphaImages[duplicateTileIndex]);

                            progressDone += 2U;
                            progressCallback?.Invoke(null, new ProgressEventArgs(((double)progressDone / progressTotal) * 100.0, true));
                        }
                        else
                        {
                            CompressedAV1Image? alpha = null;

                            try
                            {
                                Rectangle windowRect = windowRectangles[i];
                                using (Surface window = scratchSurface.CreateWindow(windowRect))
                                {
                                    AvifNative.CompressAlphaImage(window,
                                                                  options,
                                                                  ReportCompressionProgress,
                                                                  ref progressDone,
                                                                  progressTotal,
                                                                  out alpha);
                                }

                                alphaImages.Add(alpha);
                                alpha = null;
                            }
                            finally
                            {
                                alpha?.Dispose();
                            }
                        }
                    }
                }

                List<ColorInformationBox> colorInformationBoxes = new List<ColorInformationBox>(2);

                ReadOnlyMemory<byte> iccProfileBytes = metadata.IccProfile;
                if (iccProfileBytes.Length > 0)
                {
                    colorInformationBoxes.Add(new IccProfileColorInformation(iccProfileBytes));
                }

                colorInformationBoxes.Add(new NclxColorInformation(colorConversionInfo.colorPrimaries,
                                                                   colorConversionInfo.transferCharacteristics,
                                                                   colorConversionInfo.matrixCoefficients,
                                                                   colorConversionInfo.fullRange));

                AvifWriter writer = new AvifWriter(colorImages,
                                                   alphaImages,
                                                   homogeneousTileInfo,
                                                   premultipliedAlpha,
                                                   metadata,
                                                   imageGridMetadata,
                                                   options.yuvFormat,
                                                   colorInformationBoxes,
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
                    doc.Metadata.SetUserValue(CICPMetadataName, serializedValue);
                }
            }

            ImageGridMetadata? imageGridMetadata = reader.ImageGridMetadata;

            if (imageGridMetadata != null)
            {
                string serializedValue = imageGridMetadata.SerializeToString();

                if (serializedValue != null)
                {
                    doc.Metadata.SetUserValue(ImageGridName, serializedValue);
                }
            }

            ReadOnlyMemory<byte> iccProfileBytes = reader.GetICCProfile();

            if (iccProfileBytes.Length > 0)
            {
                doc.Metadata.AddExifPropertyItem(ExifSection.Image,
                                                 unchecked((ushort)ExifTagID.IccProfileData),
                                                 new ExifValue(ExifValueType.Undefined,
                                                               iccProfileBytes.Span));
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

        private static AvifMetadata CreateAvifMetadata(Document doc)
        {
            byte[]? exifBytes = null;
            byte[]? iccProfileBytes = null;
            byte[]? xmpBytes = null;

            Dictionary<ExifPropertyPath, ExifValue>? exifMetadata = GetExifMetadataFromDocument(doc);

            if (exifMetadata != null)
            {
                ExifColorSpace exifColorSpace = ExifColorSpace.Srgb;

                if (exifMetadata.TryGetValue(ExifPropertyKeys.Photo.ColorSpace.Path, out ExifValue? value))
                {
                    exifMetadata.Remove(ExifPropertyKeys.Photo.ColorSpace.Path);

                    if (MetadataHelpers.TryDecodeShort(value, out ushort colorSpace))
                    {
                        exifColorSpace = (ExifColorSpace)colorSpace;
                    }
                }

                ExifPropertyPath iccProfileKey = ExifPropertyKeys.Image.InterColorProfile.Path;

                if (exifMetadata.TryGetValue(iccProfileKey, out ExifValue? iccProfileItem))
                {
                    iccProfileBytes = iccProfileItem.Data.ToArrayEx();
                    exifMetadata.Remove(iccProfileKey);
                    exifColorSpace = ExifColorSpace.Uncalibrated;

                    // Remove the InteroperabilityIndex and related tags, these tags should
                    // not be written if the image has an ICC color profile.
                    exifMetadata.Remove(ExifPropertyKeys.Interop.InteroperabilityIndex.Path);
                    exifMetadata.Remove(ExifPropertyKeys.Interop.InteroperabilityVersion.Path);
                }

                exifBytes = new ExifWriter(doc, exifMetadata, exifColorSpace).CreateExifBlob();
            }

            XmpPacket? xmpPacket = doc.Metadata.TryGetXmpPacket();
            if (xmpPacket != null)
            {
                string packetAsString = xmpPacket.ToString(XmpPacketWrapperType.ReadOnly);

                xmpBytes = System.Text.Encoding.UTF8.GetBytes(packetAsString);
            }

            return new AvifMetadata(exifBytes, iccProfileBytes, xmpBytes);
        }

        private static Dictionary<ExifPropertyPath, ExifValue>? GetExifMetadataFromDocument(Document doc)
        {
            Dictionary<ExifPropertyPath, ExifValue>? items = null;

            ExifPropertyItem[] exifProperties = doc.Metadata.GetExifPropertyItems();

            if (exifProperties.Length > 0)
            {
                items = new Dictionary<ExifPropertyPath, ExifValue>(exifProperties.Length);

                foreach (ExifPropertyItem property in exifProperties)
                {
                    items.TryAdd(property.Path, property.Value);
                }
            }

            return items;
        }

        private static HomogeneousTileInfo GetHomogeneousTileInfo(Surface surface, Rectangle[] tileRects, bool includeAlphaTiles)
        {
            Dictionary<int, int> duplicateColorTileMap = new Dictionary<int, int>();
            HashSet<int> homogeneousColorTiles = new HashSet<int>();
            Dictionary<int, int> duplicateAlphaTileMap = new Dictionary<int, int>();
            HashSet<int> homogeneousAlphaTiles = new HashSet<int>();

            if (tileRects.Length > 1)
            {
                Dictionary<uint, int> homogeneousColorTileCache = new Dictionary<uint, int>();
                Dictionary<uint, int> homogeneousAlphaTileCache = new Dictionary<uint, int>();

                RegionPtr<uint> sourceRegion = surface.AsRegionPtr().Cast<uint>();

                for (int i = 0; i < tileRects.Length; i++)
                {
                    RectInt32 tileBounds = tileRects[i];
                    RegionPtr<uint> tileRegion = sourceRegion.Slice(tileBounds);

                    if (IsHomogeneousColorTile(tileRegion, out uint firstPixelBgr))
                    {
                        homogeneousColorTiles.Add(i);

                        if (homogeneousColorTileCache.TryGetValue(firstPixelBgr, out int duplicateTileIndex))
                        {
                            duplicateColorTileMap.Add(i, duplicateTileIndex);
                        }
                        else
                        {
                            homogeneousColorTileCache.Add(firstPixelBgr, i);
                        }
                    }

                    if (includeAlphaTiles)
                    {
                        if (IsHomogeneousAlphaTile(tileRegion, out uint firstPixelAlpha))
                        {
                            homogeneousAlphaTiles.Add(i);

                            if (homogeneousAlphaTileCache.TryGetValue(firstPixelAlpha, out int duplicateTileIndex))
                            {
                                duplicateAlphaTileMap.Add(i, duplicateTileIndex);
                            }
                            else
                            {
                                homogeneousAlphaTileCache.Add(firstPixelAlpha, i);
                            }
                        }
                    }
                }
            }

            return new HomogeneousTileInfo(duplicateColorTileMap,
                                           homogeneousColorTiles,
                                           duplicateAlphaTileMap,
                                           homogeneousAlphaTiles);
        }

        private static Rectangle[] GetTileWindowRectangles(ImageGridMetadata? imageGridMetadata, Document document)
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

        private static unsafe bool HasTransparency(Surface surface)
        {
            for (int y = 0; y < surface.Height; y++)
            {
                ColorBgra* ptr = surface.GetRowPointerUnchecked(y);
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

        private static unsafe bool IsGrayscaleImage(Surface surface)
        {
            for (int y = 0; y < surface.Height; y++)
            {
                ColorBgra* ptr = surface.GetRowPointerUnchecked(y);
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

        private static unsafe bool IsHomogeneousAlphaTile(RegionPtr<uint> region, out uint firstPixelAlpha)
        {
            const uint Mask = 0xff000000;

            firstPixelAlpha = region[0, 0] & Mask;

            foreach (RegionRowPtr<uint> row in region.Rows)
            {
                if (!BufferUtil.BitwiseAllEqualToMasked(row.Ptr, Mask, firstPixelAlpha, row.Width))
                {
                    return false;
                }
            }

            return true;
        }

        private static unsafe bool IsHomogeneousColorTile(RegionPtr<uint> region, out uint firstPixelBgr)
        {
            const uint Mask = 0x00ffffff;

            firstPixelBgr = region[0, 0] & Mask;

            foreach (RegionRowPtr<uint> row in region.Rows)
            {
                if (!BufferUtil.BitwiseAllEqualToMasked(row.Ptr, Mask, firstPixelBgr, row.Width))
                {
                    return false;
                }
            }

            return true;
        }

        private static ImageGridMetadata? TryCalculateBestTileSize(Document document, EncoderPreset encoderPreset)
        {
            // Although the HEIF specification (ISO/IEC 23008-12:2017) allows an image grid to have up to 256 tiles
            // in each direction (65536 total), the ISO base media file format (ISO/IEC 14496-12:2015) limits
            // an item reference box to 65535 items.
            // Because of this we limit the maximum number of tiles to 250.
            //
            // While this would result in the image using 62500 tiles in the worst case, it allows
            // memory usage to be minimized when encoding extremely wide and/or tall images.
            //
            // For example, a 65536x65536 pixel image would use a 128x128 grid of 512x512 pixel tiles.
            const int MaxTileCount = 250;
            // The MIAF specification (ISO/IEC 23000-22:2019) requires that the tile size be at least 64x64 pixels.
            const int MinTileSize = 64;

            int maxTileSize;

            switch (encoderPreset)
            {
                case EncoderPreset.Fast:
                    maxTileSize = 512;
                    break;
                case EncoderPreset.Medium:
                    maxTileSize = 1280;
                    break;
                case EncoderPreset.Slow:
                    maxTileSize = 1920;
                    break;
                case EncoderPreset.VerySlow:
                    // Tiles are not used for the very slow preset.
                    return null;
                default:
                    throw new InvalidEnumArgumentException(nameof(encoderPreset), (int)encoderPreset, typeof(EncoderPreset));
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

            ImageGridMetadata? metadata = null;

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

        private static ImageGridMetadata? TryGetImageGridMetadata(
            Document document,
            EncoderPreset encoderPreset,
            YUVChromaSubsampling yuvFormat,
            bool preserveExistingTileSize)
        {
            ImageGridMetadata? metadata = null;

            // The VerySlow preset always encodes the image as a single tile.
            if (encoderPreset != EncoderPreset.VerySlow)
            {
                // The image must have an even size to be eligible for tiling.
                if ((document.Width & 1) == 0 && (document.Height & 1) == 0)
                {
                    if (preserveExistingTileSize)
                    {
                        string? value = document.Metadata.GetUserValue(ImageGridName);

                        if (!string.IsNullOrEmpty(value))
                        {
                            ImageGridMetadata? serializedData = ImageGridMetadata.TryDeserialize(value);

                            if (serializedData != null
                                && serializedData.IsValidForImage((uint)document.Width, (uint)document.Height, yuvFormat))
                            {
                                metadata = serializedData;
                            }
                        }
                    }

                    if (metadata is null)
                    {
                        metadata = TryCalculateBestTileSize(document, encoderPreset);
                    }
                }
            }

            return metadata;
        }
    }
}
