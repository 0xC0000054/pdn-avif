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
    internal sealed class AvifReader : IDisposable
    {
        private Stream stream;
        private bool disposed;
        private readonly bool leaveOpen;
        private readonly AvifParser parser;
        private readonly uint primaryItemId;
        private readonly uint alphaItemId;
        private readonly ColorInformationBox colorInfoBox;
        private readonly CleanApertureBox cleanApertureBox;
        private readonly ImageRotateBox imageRotateBox;
        private readonly ImageMirrorBox imageMirrorBox;
        private readonly ImageGridInfo colorGridInfo;
        private readonly ImageGridInfo alphaGridInfo;

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
            this.parser = new AvifParser(input, leaveOpen: true);
            this.stream = input;
            this.leaveOpen = leaveOpen;
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
        }

        public Surface Decode()
        {
            VerifyNotDisposed();
            EnsureCompressedImagesAreAV1();

            Size colorSize = GetImageSize(this.primaryItemId, this.colorGridInfo, "color");

            Surface surface = new Surface(colorSize);
            bool disposeSurface = true;

            try
            {
                ProcessColorImage(surface);
                if (this.alphaItemId != 0)
                {
                    Size alphaSize = GetImageSize(this.alphaItemId, this.alphaGridInfo, "alpha");

                    if (alphaSize != colorSize)
                    {
                        ExceptionUtil.ThrowFormatException("The alpha image size does not match the color image.");
                    }

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
                if (!this.leaveOpen)
                {
                    this.stream.Dispose();
                }

                this.stream = null;
            }
        }

        public byte[] GetExifData()
        {
            VerifyNotDisposed();

            ItemLocationEntry entry = this.parser.TryGetExifLocation(this.primaryItemId);

            if (entry != null)
            {
                ulong? offset = this.parser.TryCalculateItemOffset(entry);

                if (!offset.HasValue)
                {
                    // The EXIF data has an invalid file offset, ignore it.
                    return null;
                }

                ulong length = entry.Extent.Length;

                // Ignore any EXIF blocks that are larger than 2GB.
                if (length < int.MaxValue)
                {
                    this.stream.Position = (long)offset;

                    // The EXIF data block has a header that indicates the number of bytes
                    // that come before the start of the TIFF header.
                    // See ISO/IEC 23008-12:2017 section A.2.1.

                    long tiffStartOffset = this.stream.TryReadUInt32BigEndian();

                    if (tiffStartOffset != -1)
                    {
                        long dataLength = (long)length - tiffStartOffset - sizeof(uint);

                        if (dataLength > 0)
                        {
                            if (tiffStartOffset != 0)
                            {
                                this.stream.Position += tiffStartOffset;
                            }

                            byte[] bytes = new byte[dataLength];

                            this.stream.ProperRead(bytes, 0, bytes.Length);

                            return bytes;
                        }
                    }
                }
            }

            return null;
        }

        public byte[] GetIccProfile()
        {
            VerifyNotDisposed();

            if (this.colorInfoBox is IccProfileColorInformation iccColorInfo)
            {
                return iccColorInfo.GetProfileBytes();
            }

            return null;
        }

        public byte[] GetXmpData()
        {
            VerifyNotDisposed();

            ItemLocationEntry entry = this.parser.TryGetXmpLocation(this.primaryItemId);

            if (entry != null)
            {
                ulong? offset = this.parser.TryCalculateItemOffset(entry);

                if (!offset.HasValue)
                {
                    // The XMP data has an invalid file offset, ignore it.
                    return null;
                }

                ulong length = entry.Extent.Length;

                // Ignore any XMP packets that are larger than 2GB.
                if (length < int.MaxValue)
                {
                    this.stream.Position = (long)offset;

                    byte[] bytes = new byte[(int)length];

                    this.stream.ProperRead(bytes, 0, bytes.Length);

                    return bytes;
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
                Crop(ref surface);
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

        private void Crop(ref Surface surface)
        {
            if (this.cleanApertureBox.Width.Denominator == 0 ||
                this.cleanApertureBox.Height.Denominator == 0 ||
                this.cleanApertureBox.HorizontalOffset.Denominator == 0||
                this.cleanApertureBox.VerticalOffset.Denominator == 0)
            {
                return;
            }

            int cropWidth = this.cleanApertureBox.Width.ToInt32();
            int cropHeight = this.cleanApertureBox.Height.ToInt32();

            int offsetX = this.cleanApertureBox.HorizontalOffset.ToInt32();
            int offsetY = this.cleanApertureBox.VerticalOffset.ToInt32();

            int centerX = offsetX + ((surface.Width - 1) / 2);
            int centerY = offsetY + ((surface.Height - 1) / 2);

            int cropRectX = centerX - ((cropWidth - 1) / 2);
            int cropRectY = centerY - ((cropHeight - 1) / 2);

            Rectangle cropRect = new Rectangle(cropRectX, cropRectY, cropWidth, cropHeight);

            Surface temp = new Surface(cropWidth, cropHeight);
            try
            {
                temp.CopySurface(surface, cropRect);

                surface.Dispose();
                surface = temp;
                temp = null;
            }
            finally
            {
                temp?.Dispose();
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

        private Size GetImageSize(uint itemId, ImageGridInfo gridInfo, string imageName)
        {
            IItemInfoEntry entry = this.parser.TryGetItemInfoEntry(itemId);

            uint width;
            uint height;

            if (entry.ItemType == ItemInfoEntryTypes.AV01)
            {
                IItemProperty property = this.parser.TryGetAssociatedItemProperty(itemId, BoxTypes.ImageSpatialExtents);

                if (property == null)
                {
                    ExceptionUtil.ThrowFormatException($"The { imageName } image size property was not found.");
                }

                ImageSpatialExtentsBox extents = (ImageSpatialExtentsBox)property;

                width = extents.ImageWidth;
                height = extents.ImageHeight;
            }
            else if (entry.ItemType == ItemInfoEntryTypes.ImageGrid)
            {
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

        private void CheckImageItemType(uint itemId, ImageGridInfo gridInfo, string imageName, bool checkingGridChildren = false)
        {
            IItemInfoEntry entry = this.parser.TryGetItemInfoEntry(itemId);

            if (entry == null)
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

                    if (gridInfo == null)
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

        private void DecodeColorImage(uint itemId, DecodeInfo decodeInfo, ColorConversionInfo colorConversionInfo, Surface fullSurface)
        {
            SafeProcessHeapBuffer color = null;

            try
            {
                color = ReadColorImage(itemId);

                AvifNative.DecompressColor(color, colorConversionInfo, decodeInfo, fullSurface);
            }
            finally
            {
                color?.Dispose();
            }
        }

        private void DecodeAlphaImage(uint itemId, DecodeInfo decodeInfo, Surface fullSurface)
        {
            SafeProcessHeapBuffer alpha = null;

            try
            {
                alpha = ReadAlphaImage(itemId);

                AvifNative.DecompressAlpha(alpha, decodeInfo, fullSurface);
            }
            finally
            {
                alpha?.Dispose();
            }
        }

        private void FillAlphaImageGrid(Surface fullSurface)
        {
            this.alphaGridInfo.CheckAvailableTileCount();
            DecodeInfo decodeInfo = new DecodeInfo
            {
                expectedWidth = 0,
                expectedHeight = 0,
                usingTileNclxProfile = false
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

        private void FillColorImageGrid(ColorConversionInfo colorInfo, Surface fullSurface)
        {
            this.colorGridInfo.CheckAvailableTileCount();
            DecodeInfo decodeInfo = new DecodeInfo
            {
                expectedWidth = 0,
                expectedHeight = 0,
                usingTileNclxProfile = false
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
                    usingTileNclxProfile = false
                };

                DecodeAlphaImage(this.alphaItemId, decodeInfo, fullSurface);
            }
        }

        private void ProcessColorImage(Surface fullSurface)
        {
            ColorConversionInfo colorConversionInfo = null;
            if (this.colorInfoBox != null)
            {
                colorConversionInfo = new ColorConversionInfo(this.colorInfoBox);
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
                    usingTileNclxProfile = false
                };

                DecodeColorImage(this.primaryItemId, decodeInfo, colorConversionInfo, fullSurface);
            }
        }

        private SafeProcessHeapBuffer ReadAlphaImage(uint itemId)
        {
            ItemLocationEntry entry = this.parser.TryGetItemLocation(itemId);

            if (entry is null)
            {
                ExceptionUtil.ThrowFormatException("The alpha image item location was not found.");
            }

            SafeProcessHeapBuffer alphaImage = ReadImageData(entry);

            if (alphaImage is null)
            {
                ExceptionUtil.ThrowInvalidOperationException("The alpha image buffer is null.");
            }

            return alphaImage;
        }

        private SafeProcessHeapBuffer ReadColorImage(uint itemId)
        {
            ItemLocationEntry entry = this.parser.TryGetItemLocation(itemId);

            if (entry is null)
            {
                ExceptionUtil.ThrowFormatException("The color image item location was not found.");
            }

            SafeProcessHeapBuffer colorImage = ReadImageData(entry);

            if (colorImage is null)
            {
                ExceptionUtil.ThrowInvalidOperationException("The color image buffer is null.");
            }

            return colorImage;
        }

        private SafeProcessHeapBuffer ReadImageData(ItemLocationEntry entry)
        {
            if (entry is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(entry));
            }

            ulong? offset = this.parser.TryCalculateItemOffset(entry);

            if (!offset.HasValue)
            {
                ExceptionUtil.ThrowFormatException("The image data has an invalid file offset.");
            }

            ulong length = entry.Extent.Length;

            SafeProcessHeapBuffer buffer = SafeProcessHeapBuffer.Create(length);

            this.stream.Position = (long)offset;
            this.stream.ProperRead(buffer, length);

            return buffer;
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
