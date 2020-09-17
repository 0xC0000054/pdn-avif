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
using AvifFileType.Interop;
using PaintDotNet;
using PaintDotNet.IO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace AvifFileType
{
    internal sealed class AvifReader
        : IDisposable
    {
        private bool disposed;
        private CICPColorData? imageColorData;
        private readonly AvifParser parser;
        private readonly uint primaryItemId;
        private readonly uint alphaItemId;
        private readonly CleanApertureBox cleanApertureBox;
        private readonly ImageRotateBox imageRotateBox;
        private readonly ImageMirrorBox imageMirrorBox;
        private readonly ImageGridInfo colorGridInfo;
        private readonly ImageGridInfo alphaGridInfo;
        private readonly ColorInformationBox colorInfoBox;

        /// <summary>
        /// Initializes a new instance of the <see cref="AvifReader"/> class.
        /// </summary>
        /// <param name="parser">The parser.</param>
        /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
        public AvifReader(Stream input, bool leaveOpen)
        {
            if (input is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(input));
            }

            // The parser is initialized first because it will throw an exception
            // if the AVIF file is invalid or not supported.
            this.parser = new AvifParser(input, leaveOpen);
            this.primaryItemId = this.parser.GetPrimaryItemId();
            this.alphaItemId = this.parser.GetAlphaItemId(this.primaryItemId);
            this.colorInfoBox = this.parser.TryGetColorInfoBox(this.primaryItemId);
            this.parser.GetTransformationProperties(this.primaryItemId,
                                                    out this.cleanApertureBox,
                                                    out this.imageRotateBox,
                                                    out this.imageMirrorBox);
            this.colorGridInfo = this.parser.TryGetImageGridInfo(this.primaryItemId);
            if (this.alphaItemId != 0)
            {
                this.alphaGridInfo = this.parser.TryGetImageGridInfo(this.alphaItemId);
            }
            else
            {
                this.alphaGridInfo = null;
            }

            if (this.colorInfoBox is NclxColorInformation nclx)
            {
                this.imageColorData = new CICPColorData
                {
                    colorPrimaries = nclx.ColorPrimaries,
                    transferCharacteristics = nclx.TransferCharacteristics,
                    matrixCoefficients = nclx.MatrixCoefficients,
                    fullRange = nclx.FullRange
                };
            }
        }

        public CICPColorData? ImageColorData => this.imageColorData;

        public Surface Decode()
        {
            VerifyNotDisposed();
            EnsureCompressedImagesAreAV1();
            EnsurePrimaryItemIsNotHidden();
            EnsureRequiredImagePropertiesAreSupported();

            Size colorSize = GetImageSize(this.primaryItemId, this.colorGridInfo, "color");

            Surface surface = new Surface(colorSize);
            bool disposeSurface = true;

            try
            {
                ProcessColorImage(surface);
                if (this.alphaItemId != 0)
                {
                    ProcessAlphaImage(surface);
                }
                else
                {
                    // The AVIF file does not have an alpha channel.
                    new UnaryPixelOps.SetAlphaChannelTo255().Apply(surface, surface.Bounds);
                }
                ApplyImageTransforms(ref surface);

                disposeSurface = false;
            }
            finally
            {
                // Free the surface if an exception was thrown when populating it.
                if (disposeSurface)
                {
                    surface.Dispose();
                    surface = null;
                }
            }

            return surface;
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;

                this.parser?.Dispose();
            }
        }

        public byte[] GetExifData()
        {
            VerifyNotDisposed();

            ItemLocationEntry entry = this.parser.TryGetExifLocation(this.primaryItemId);

            if (entry != null)
            {
                ulong length = entry.TotalItemSize;

                // Ignore any EXIF blocks that are larger than 2GB.
                if (length < int.MaxValue)
                {
                    using (AvifItemData itemData = this.parser.ReadItemData(entry))
                    using (Stream stream = itemData.GetStream())
                    {
                        // The EXIF data block has a header that indicates the number of bytes
                        // that come before the start of the TIFF header.
                        // See ISO/IEC 23008-12:2017 section A.2.1.

                        long tiffStartOffset = stream.TryReadUInt32BigEndian();

                        if (tiffStartOffset != -1)
                        {
                            long dataLength = (long)length - tiffStartOffset - sizeof(uint);

                            if (dataLength > 0)
                            {
                                if (tiffStartOffset != 0)
                                {
                                    stream.Position += tiffStartOffset;
                                }

                                byte[] bytes = new byte[dataLength];

                                stream.ProperRead(bytes, 0, bytes.Length);

                                return bytes;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public byte[] GetICCProfile()
        {
            byte[] iccProfileBytes = null;

            if (this.colorInfoBox is IccProfileColorInformation iccProfile)
            {
                iccProfileBytes = iccProfile.GetProfileBytes();
            }

            return iccProfileBytes;
        }

        public byte[] GetXmpData()
        {
            VerifyNotDisposed();

            ItemLocationEntry entry = this.parser.TryGetXmpLocation(this.primaryItemId);

            if (entry != null)
            {
                ulong length = entry.TotalItemSize;

                // Ignore any XMP packets that are larger than 2GB.
                if (length < int.MaxValue)
                {
                    using (AvifItemData itemData = this.parser.ReadItemData(entry))
                    {
                        return itemData.ToArray();
                    }
                }
            }

            return null;
        }

        private void ApplyImageTransforms(ref Surface surface)
        {
            // The image transforms must be applied in the following order:
            // Crop
            // Rotate
            // Flip horizontal or vertical

            if (this.cleanApertureBox != null)
            {
                ImageTransform.Crop(this.cleanApertureBox, ref surface);
            }

            if (this.imageRotateBox != null)
            {
                switch (this.imageRotateBox.Rotation)
                {
                    case ImageRotation.RotateNone:
                        break;
                    case ImageRotation.Rotate90CCW:
                        ImageTransform.Rotate90CCW(ref surface);
                        break;
                    case ImageRotation.Rotate180:
                        ImageTransform.Rotate180(surface);
                        break;
                    case ImageRotation.Rotate270CCW:
                        ImageTransform.Rotate270CCW(ref surface);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown ImageRotation value.");
                }
            }

            if (this.imageMirrorBox != null)
            {
                switch (this.imageMirrorBox.MirrorDirection)
                {
                    case ImageMirrorDirection.Vertical:
                        ImageTransform.FlipVertical(surface);
                        break;
                    case ImageMirrorDirection.Horizontal:
                        ImageTransform.FlipHorizontal(surface);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown ImageMirrorDirection value.");
                }
            }
        }

        private void CheckRequiredImageProperties(uint itemId, ImageGridInfo gridInfo, string imageName)
        {
            bool hasUnsupportedProperties = false;

            IItemInfoEntry entry = this.parser.TryGetItemInfoEntry(itemId);

            if (entry is null)
            {
                ExceptionUtil.ThrowFormatException($"The { imageName } image does not exist.");
            }
            else if (entry.ItemType == ItemInfoEntryTypes.AV01)
            {
                hasUnsupportedProperties = this.parser.HasUnsupportedEssentialProperties(itemId);
            }
            else if (entry.ItemType == ItemInfoEntryTypes.ImageGrid)
            {
                if (gridInfo is null)
                {
                    ExceptionUtil.ThrowFormatException($"The { imageName } image does not have any image grid information.");
                }

                IReadOnlyList<uint> childImageIds = gridInfo.ChildImageIds;

                for (int i = 0; i < childImageIds.Count; i++)
                {
                    if (this.parser.HasUnsupportedEssentialProperties(childImageIds[i]))
                    {
                        hasUnsupportedProperties = true;
                        break;
                    }
                }
            }
            else
            {
                ExceptionUtil.ThrowFormatException($"The { imageName } image is not a supported format.");
            }

            if (hasUnsupportedProperties)
            {
                ExceptionUtil.ThrowFormatException($"The { imageName } image has essential item properties that are not supported.");
            }
        }

        private void CheckImageItemType(uint itemId, ImageGridInfo gridInfo, string imageName, bool checkingGridChildren = false)
        {
            IItemInfoEntry entry = this.parser.TryGetItemInfoEntry(itemId);

            if (entry is null)
            {
                ExceptionUtil.ThrowFormatException($"The { imageName } image does not exist.");
            }
            else if (entry.ItemType != ItemInfoEntryTypes.AV01)
            {
                if (entry.ItemType == ItemInfoEntryTypes.ImageGrid)
                {
                    if (checkingGridChildren)
                    {
                        ExceptionUtil.ThrowFormatException("Nested image grids are not supported.");
                    }

                    if (gridInfo is null)
                    {
                        ExceptionUtil.ThrowFormatException($"The { imageName } image does not have any image grid information.");
                    }

                    IReadOnlyList<uint> childImageIds = gridInfo.ChildImageIds;

                    for (int i = 0; i < childImageIds.Count; i++)
                    {
                        CheckImageItemType(childImageIds[i], null, imageName, true);
                    }
                }
                else
                {
                    ExceptionUtil.ThrowFormatException($"The { imageName } image is not a supported format.");
                }
            }
        }

        private void DecodeColorImage(uint itemId, DecodeInfo decodeInfo, CICPColorData? colorConversionInfo, Surface fullSurface)
        {
            using (AvifItemData color = ReadColorImage(itemId))
            {
                AvifNative.DecompressColor(color, colorConversionInfo, decodeInfo, fullSurface);
            }
        }

        private void DecodeAlphaImage(uint itemId, DecodeInfo decodeInfo, Surface fullSurface)
        {
            using (AvifItemData alpha = ReadAlphaImage(itemId))
            {
                AvifNative.DecompressAlpha(alpha, decodeInfo, fullSurface);
            }
        }

        private void EnsureCompressedImagesAreAV1()
        {
            CheckImageItemType(this.primaryItemId, this.colorGridInfo, "color");

            if (this.alphaItemId != 0)
            {
                CheckImageItemType(this.alphaItemId, this.alphaGridInfo, "alpha");
            }
        }

        private void EnsurePrimaryItemIsNotHidden()
        {
            IItemInfoEntry entry = this.parser.TryGetItemInfoEntry(this.primaryItemId);

            if (entry is null)
            {
                ExceptionUtil.ThrowFormatException("The primary item does not exist.");
            }
            else if (entry.IsHidden)
            {
                ExceptionUtil.ThrowFormatException("The primary item cannot be marked as hidden.");
            }
        }

        private void EnsureRequiredImagePropertiesAreSupported()
        {
            CheckRequiredImageProperties(this.primaryItemId, this.colorGridInfo, "color");

            if (this.alphaItemId != 0)
            {
                CheckRequiredImageProperties(this.alphaItemId, this.alphaGridInfo, "alpha");
            }
        }

        private void FillAlphaImageGrid(Surface fullSurface)
        {
            this.alphaGridInfo.CheckAvailableTileCount();
            DecodeInfo decodeInfo = new DecodeInfo
            {
                expectedWidth = 0,
                expectedHeight = 0,
                usingFirstTileColorData = false
            };

            IReadOnlyList<uint> childImageIds = this.alphaGridInfo.ChildImageIds;

            // The tiles are encoded from top to bottom then left to right.

            for (int row = 0; row < this.alphaGridInfo.TileRowCount; row++)
            {
                decodeInfo.tileRowIndex = (uint)row;
                int startIndex = row * this.alphaGridInfo.TileColumnCount;

                for (int col = 0; col < this.alphaGridInfo.TileColumnCount; col++)
                {
                    decodeInfo.tileColumnIndex = (uint)col;

                    DecodeAlphaImage(childImageIds[startIndex + col], decodeInfo, fullSurface);
                }
            }
        }

        private void FillColorImageGrid(CICPColorData? colorInfo, Surface fullSurface)
        {
            this.colorGridInfo.CheckAvailableTileCount();
            DecodeInfo decodeInfo = new DecodeInfo
            {
                expectedWidth = 0,
                expectedHeight = 0,
                usingFirstTileColorData = false
            };

            IReadOnlyList<uint> childImageIds = this.colorGridInfo.ChildImageIds;

            // The tiles are encoded from top to bottom then left to right.

            for (int row = 0; row < this.colorGridInfo.TileRowCount; row++)
            {
                decodeInfo.tileRowIndex = (uint)row;
                int startIndex = row * this.colorGridInfo.TileColumnCount;

                for (int col = 0; col < this.colorGridInfo.TileColumnCount; col++)
                {
                    decodeInfo.tileColumnIndex = (uint)col;

                    DecodeColorImage(childImageIds[startIndex + col], decodeInfo, colorInfo, fullSurface);
                }
            }

            MaybeUseColorDataFromDecoder(decodeInfo);
        }

        private Size GetImageSize(uint itemId, ImageGridInfo gridInfo, string imageName)
        {
            IItemInfoEntry entry = this.parser.TryGetItemInfoEntry(itemId);

            uint width;
            uint height;

            if (entry.ItemType == ItemInfoEntryTypes.AV01)
            {
                ImageSpatialExtentsBox extents = this.parser.TryGetAssociatedItemProperty<ImageSpatialExtentsBox>(itemId);

                if (extents is null)
                {
                    ExceptionUtil.ThrowFormatException($"The { imageName } image size property was not found.");
                }

                width = extents.ImageWidth;
                height = extents.ImageHeight;
            }
            else if (entry.ItemType == ItemInfoEntryTypes.ImageGrid)
            {
                if (gridInfo is null)
                {
                    ExceptionUtil.ThrowFormatException($"The { imageName } image does not have any image grid information.");
                }

                width = gridInfo.OutputWidth;
                height = gridInfo.OutputHeight;
            }
            else
            {
                throw new FormatException($"The { imageName } image is not a supported format.");
            }

            if (width > int.MaxValue || height > int.MaxValue)
            {
                throw new FormatException($"The { imageName } image dimensions are too large.");
            }

            return new Size((int)width, (int)height);
        }

        private void MaybeUseColorDataFromDecoder(DecodeInfo decodeInfo)
        {
            if (!this.imageColorData.HasValue && decodeInfo.usingFirstTileColorData)
            {
                this.imageColorData = new CICPColorData
                {
                    colorPrimaries = decodeInfo.firstTileColorData.colorPrimaries,
                    transferCharacteristics = decodeInfo.firstTileColorData.transferCharacteristics,
                    matrixCoefficients = decodeInfo.firstTileColorData.matrixCoefficients,
                    fullRange = decodeInfo.firstTileColorData.fullRange
                };
            }
        }

        private void ProcessAlphaImage(Surface fullSurface)
        {
            if (this.alphaGridInfo != null)
            {
                FillAlphaImageGrid(fullSurface);
            }
            else
            {
                DecodeInfo decodeInfo = new DecodeInfo
                {
                    tileColumnIndex = 0,
                    tileRowIndex = 0,
                    expectedWidth = (uint)fullSurface.Width,
                    expectedHeight = (uint)fullSurface.Height,
                    usingFirstTileColorData = false
                };

                DecodeAlphaImage(this.alphaItemId, decodeInfo, fullSurface);
            }
        }

        private void ProcessColorImage(Surface fullSurface)
        {
            CICPColorData? colorConversionInfo = null;
            if (this.colorInfoBox is NclxColorInformation nclxColorInformation)
            {
                colorConversionInfo = new CICPColorData
                {
                    colorPrimaries = nclxColorInformation.ColorPrimaries,
                    transferCharacteristics = nclxColorInformation.TransferCharacteristics,
                    matrixCoefficients = nclxColorInformation.MatrixCoefficients,
                    fullRange = nclxColorInformation.FullRange
                };
            }

            if (this.colorGridInfo != null)
            {
                FillColorImageGrid(colorConversionInfo, fullSurface);
            }
            else
            {
                DecodeInfo decodeInfo = new DecodeInfo
                {
                    tileColumnIndex = 0,
                    tileRowIndex = 0,
                    expectedWidth = (uint)fullSurface.Width,
                    expectedHeight = (uint)fullSurface.Height,
                    usingFirstTileColorData = false
                };

                DecodeColorImage(this.primaryItemId, decodeInfo, colorConversionInfo, fullSurface);
                MaybeUseColorDataFromDecoder(decodeInfo);
            }
        }

        private AvifItemData ReadAlphaImage(uint itemId)
        {
            ItemLocationEntry entry = this.parser.TryGetItemLocation(itemId);

            if (entry is null)
            {
                ExceptionUtil.ThrowFormatException("The alpha image item location was not found.");
            }

            return ReadImageData(entry);
        }

        private AvifItemData ReadColorImage(uint itemId)
        {
            ItemLocationEntry entry = this.parser.TryGetItemLocation(itemId);

            if (entry is null)
            {
                ExceptionUtil.ThrowFormatException("The color image item location was not found.");
            }

            return ReadImageData(entry);
        }

        private AvifItemData ReadImageData(ItemLocationEntry entry)
        {
            if (entry is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(entry));
            }

            return this.parser.ReadItemData(entry);
        }

        private void VerifyNotDisposed()
        {
            if (this.disposed)
            {
                ExceptionUtil.ThrowObjectDisposedException(nameof(AvifReader));
            }
        }
    }
}
