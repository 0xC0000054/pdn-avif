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
        }

        public Surface Decode()
        {
            VerifyNotDisposed();
            EnsureCompressedImagesAreAV1();

            Size colorSize = GetImageSize(this.primaryItemId, "color");

            Surface surface = new Surface(colorSize);
            bool disposeSurface = true;

            try
            {
                DecodeInfo decodeInfo = new DecodeInfo
                {
                    expectedWidth = (uint)colorSize.Width,
                    expectedHeight = (uint)colorSize.Height
                };

                DecodeColorImage(decodeInfo, surface);
                if (this.alphaItemId != 0)
                {
                    DecodeAlphaImage(decodeInfo, surface);
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

            ItemLocationEntry entry = this.parser.TryGetExifLocation(this.primaryItemId);

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
            CheckImageItemType(this.primaryItemId, "color");

            if (this.alphaItemId != 0)
            {
                CheckImageItemType(this.alphaItemId, "alpha");
            }
        }

        private Size GetImageSize(uint itemId, string imageName)
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
                throw new FormatException("AV1 image grids are not supported.");
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

        private void CheckImageItemType(uint itemId, string imageName)
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
                    ExceptionUtil.ThrowFormatException("AV1 image grids are not supported.");
                }
                else
                {
                    ExceptionUtil.ThrowFormatException($"The { imageName } image is not a supported format.");
                }
            }
        }

        private void DecodeColorImage(DecodeInfo decodeInfo, Surface fullSurface)
        {
            SafeProcessHeapBuffer color = null;

            try
            {
                color = ReadColorImage();

                ColorConversionInfo colorConversionInfo = null;
                if (this.colorInfoBox != null)
                {
                    colorConversionInfo = new ColorConversionInfo(this.colorInfoBox);
                }

                AvifNative.DecompressColor(color, colorConversionInfo, decodeInfo, fullSurface);
            }
            finally
            {
                color?.Dispose();
            }
        }

        private void DecodeAlphaImage(DecodeInfo decodeInfo, Surface fullSurface)
        {
            SafeProcessHeapBuffer alpha = null;

            try
            {
                alpha = ReadAlphaImage();

                AvifNative.DecompressAlpha(alpha, decodeInfo, fullSurface);
            }
            finally
            {
                alpha?.Dispose();
            }
        }

        private SafeProcessHeapBuffer ReadAlphaImage()
        {
            ItemLocationEntry entry = this.parser.TryGetItemLocation(this.alphaItemId);

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

        private SafeProcessHeapBuffer ReadColorImage()
        {
            ItemLocationEntry entry = this.parser.TryGetItemLocation(this.primaryItemId);

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
