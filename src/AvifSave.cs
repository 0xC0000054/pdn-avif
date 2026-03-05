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
using AvifFileType.Interop;
using PaintDotNet;
using PaintDotNet.FileTypes;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;

using ExifColorSpace = AvifFileType.Exif.ExifColorSpace;

namespace AvifFileType
{
    internal static class AvifSave
    {
        public static void Save(IReadOnlyFileTypeDocument document,
                                Stream output,
                                int quality,
                                bool lossless,
                                bool losslessAlpha,
                                EncoderPreset encoderPreset,
                                YUVChromaSubsampling chromaSubsampling,
                                bool preserveExistingTileSize,
                                bool premultipliedAlpha,
                                ProgressEventHandler progressCallback,
                                IImagingFactory imagingFactory)
        {
            if (lossless)
            {
                losslessAlpha = true;
                // The premultiplied alpha conversion can cause the colors to drift, so it is disabled for lossless encoding.
                premultipliedAlpha = false;
            }

            IFileTypeCompositeBitmap docComposite;
            IFileTypeBitmapLock docCompositeLock;
            RegionPtr<ColorBgra32> docCompositeRegion;
            bool hasTransparency;
            {
                IFileTypeCompositeBitmap<ColorBgra32> docCompositeBgra32 = document.GetCompositeBitmap<ColorBgra32>();
                hasTransparency = HasTransparency(docCompositeBgra32, imagingFactory);

                if (premultipliedAlpha && hasTransparency)
                {
                    docComposite = document.GetCompositeBitmap<ColorPbgra32>();
                    docCompositeLock = docComposite.Lock();
                    docCompositeRegion = docCompositeLock.AsRegionPtr<ColorPbgra32>().Cast<ColorBgra32>();
                }
                else
                {
                    docComposite = docCompositeBgra32;
                    docCompositeLock = docComposite.Lock();
                    docCompositeRegion = docCompositeLock.AsRegionPtr<ColorBgra32>();
                }
            }

            AvifMetadata metadata = CreateAvifMetadata(document);

            // Images that have a non-sRGB ICC profile will not be automatically converted to gray scale.
            bool grayscale = metadata.IccProfile.IsEmpty && IsGrayscaleImage(docCompositeRegion);

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

            ImageGridMetadata? imageGridMetadata = TryGetImageGridMetadata(document,
                                                                           options.encoderPreset,
                                                                           options.yuvFormat,
                                                                           preserveExistingTileSize);

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
                Rectangle[] windowRectangles = GetTileWindowRectangles(imageGridMetadata, document.Size);
                HomogeneousTileInfo homogeneousTileInfo = GetHomogeneousTileInfo(docCompositeRegion, windowRectangles, hasTransparency);

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
                            RegionPtr<ColorBgra32> window = docCompositeRegion.Slice(windowRect);
                            AvifNative.CompressColorImage(window,
                                                          options,
                                                          ReportCompressionProgress,
                                                          ref progressDone,
                                                          progressTotal,
                                                          colorConversionInfo,
                                                          out color);

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
                                RegionPtr<ColorBgra32> window = docCompositeRegion.Slice(windowRect);
                                AvifNative.CompressAlphaImage(window,
                                                              options,
                                                              ReportCompressionProgress,
                                                              ref progressDone,
                                                              progressTotal,
                                                              out alpha);

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
                docCompositeLock.Dispose();
                docComposite.Dispose();
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

        private static AvifMetadata CreateAvifMetadata(IReadOnlyFileTypeDocument doc)
        {
            byte[]? exifBytes = null;
            byte[]? iccProfileBytes = null;
            byte[]? xmpBytes = null;

            ExifColorSpace exifColorSpace = ExifColorSpace.Srgb;

            // We do not set an ICC profile for sRGB images as AVIF can signal that
            // using its built-in color space encoding, and sRGB is the default for
            // images without an ICC profile.
            IColorContext colorContext = doc.GetColorContext();
            if (colorContext.Type != ColorContextType.ExifColorSpace
                || colorContext.ExifColorSpace != PaintDotNet.Imaging.ExifColorSpace.Srgb)
            {
                iccProfileBytes = colorContext.GetProfileBytes().ToArray();

                if (iccProfileBytes.Length > 0)
                {
                    exifColorSpace = ExifColorSpace.Uncalibrated;
                }
            }

            Dictionary<ExifPropertyPath, ExifValue>? propertyItems = GetExifMetadata(doc.Metadata);
            if (propertyItems != null)
            {
                propertyItems.Remove(ExifPropertyKeys.Image.InterColorProfile.Path);

                if (iccProfileBytes != null)
                {
                    // Remove the InteroperabilityIndex and related tags, these tags should
                    // not be written if the image has an ICC color profile.
                    propertyItems.Remove(ExifPropertyKeys.Interop.InteroperabilityIndex.Path);
                    propertyItems.Remove(ExifPropertyKeys.Interop.InteroperabilityVersion.Path);
                }

                exifBytes = new ExifWriter(doc.Size, propertyItems, exifColorSpace).CreateExifBlob();
            }

            XmpPacket? xmpPacket = doc.Metadata.Xmp.XmpPacket;
            if (xmpPacket != null)
            {
                string packetAsString = xmpPacket.ToString(XmpPacketWrapperType.ReadOnly);

                xmpBytes = System.Text.Encoding.UTF8.GetBytes(packetAsString);
            }

            return new AvifMetadata(exifBytes, iccProfileBytes, xmpBytes);
        }

        private static Dictionary<ExifPropertyPath, ExifValue>? GetExifMetadata(IReadOnlyFileTypeDocumentMetadata metadata)
        {
            Dictionary<ExifPropertyPath, ExifValue> items = new();
            foreach (KeyValuePair<ExifPropertyPath, IEnumerable<ExifValue>> property in metadata.Exif)
            {
                ExifValue? value = property.Value.FirstOrDefault();
                if (value is not null)
                {
                    items.Add(property.Key, value);
                }
            }

            return items.Count > 0 ? items : null;
        }

        private static HomogeneousTileInfo GetHomogeneousTileInfo(RegionPtr<ColorBgra32> region, Rectangle[] tileRects, bool includeAlphaTiles)
        {
            Dictionary<int, int> duplicateColorTileMap = [];
            HashSet<int> homogeneousColorTiles = [];
            Dictionary<int, int> duplicateAlphaTileMap = [];
            HashSet<int> homogeneousAlphaTiles = [];

            if (tileRects.Length > 1)
            {
                Dictionary<uint, int> homogeneousColorTileCache = [];
                Dictionary<uint, int> homogeneousAlphaTileCache = [];

                RegionPtr<uint> sourceRegion = region.Cast<uint>();

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

        private static Rectangle[] GetTileWindowRectangles(ImageGridMetadata? imageGridMetadata, SizeInt32 docSize)
        {
            Rectangle[] rects;

            if (imageGridMetadata is null)
            {
                rects = [new Rectangle(Point2Int32.Zero, docSize)];
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

        private static bool HasTransparency(IBitmapSource<ColorBgra32> source, IImagingFactory imagingFactory)
        {
            SizeInt32 sourceSize = source.Size;
            using IBitmap<ColorBgra32> rowCache = imagingFactory.CreateBitmap<ColorBgra32>(sourceSize.Width, 1);
            using IBitmapLock<ColorBgra32> rowCacheLock = rowCache.Lock(BitmapLockOptions.ReadWrite);
            RegionPtr<ColorBgra32> rowRegion = rowCacheLock.AsRegionPtr();

            for (int y = 0; y < sourceSize.Height; ++y)
            {
                source.CopyPixels(rowRegion, new Point2Int32(0, y));
                if (HasTransparency(rowRegion))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTransparency(RegionPtr<ColorBgra32> region)
        {
            foreach (RegionRowPtr<ColorBgra32> row in region.Rows)
            {
                foreach (ColorBgra32 color in row)
                {
                    if (color.A < 255)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsGrayscaleImage(RegionPtr<ColorBgra32> region)
        {
            foreach (RegionRowPtr<ColorBgra32> row in region.Rows)
            {
                foreach (ColorBgra32 color in row)
                {
                    if (!(color.R == color.G && color.G == color.B))
                    {
                        return false;
                    }
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

        private static ImageGridMetadata? TryCalculateBestTileSize(SizeInt32 docSize, EncoderPreset encoderPreset)
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
            int bestTileWidth = docSize.Width;
            int bestTileRowCount = 1;
            int bestTileHeight = docSize.Height;

            if (docSize.Width > maxTileSize)
            {
                for (int tileColumnCount = 2; tileColumnCount <= MaxTileCount; tileColumnCount++)
                {
                    int tileWidth = docSize.Width / tileColumnCount;

                    if (tileWidth < MinTileSize)
                    {
                        break;
                    }

                    if ((tileWidth & 1) == 0 && (tileWidth * tileColumnCount) == docSize.Width)
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

            if (docSize.Height > maxTileSize)
            {
                if (docSize.Width == docSize.Height)
                {
                    // Square images use the same number of horizontal and vertical tiles.
                    bestTileHeight = bestTileWidth;
                    bestTileRowCount = bestTileColumnCount;
                }
                else
                {
                    for (int tileRowCount = 2; tileRowCount <= MaxTileCount; tileRowCount++)
                    {
                        int tileHeight = docSize.Height / tileRowCount;

                        if (tileHeight < MinTileSize)
                        {
                            break;
                        }

                        if ((tileHeight & 1) == 0 && (tileHeight * tileRowCount) == docSize.Height)
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
                                                 (uint)docSize.Height,
                                                 (uint)docSize.Width,
                                                 (uint)bestTileHeight,
                                                 (uint)bestTileWidth);
            }

            return metadata;
        }

        private static ImageGridMetadata? TryGetImageGridMetadata(
            IReadOnlyFileTypeDocument document,
            EncoderPreset encoderPreset,
            YUVChromaSubsampling yuvFormat,
            bool preserveExistingTileSize)
        {
            ImageGridMetadata? metadata = null;

            // The VerySlow preset always encodes the image as a single tile.
            if (encoderPreset != EncoderPreset.VerySlow)
            {
                // The image must have an even size to be eligible for tiling.
                if ((document.Size.Width & 1) == 0 && (document.Size.Height & 1) == 0)
                {
                    if (preserveExistingTileSize)
                    {
                        document.Metadata.Custom.TryGetValue(AvifMetadataNames.ImageGridName, out string? value);
                        if (!string.IsNullOrEmpty(value))
                        {
                            ImageGridMetadata? serializedData = ImageGridMetadata.TryDeserialize(value);

                            if (serializedData != null
                                && serializedData.IsValidForImage((uint)document.Size.Width, (uint)document.Size.Height, yuvFormat))
                            {
                                metadata = serializedData;
                            }
                        }
                    }

                    metadata ??= TryCalculateBestTileSize(document.Size, encoderPreset);
                }
            }

            return metadata;
        }
    }
}
