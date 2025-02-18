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
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;
using System.Collections.Generic;
using System.IO;

namespace AvifFileType
{
    internal sealed class AvifReader
        : Disposable
    {
        private readonly AvifParser parser;
        private readonly uint primaryItemId;
        private readonly uint alphaItemId;
        private readonly CleanApertureBox? cleanApertureBox;
        private readonly ImageRotateBox? imageRotateBox;
        private readonly ImageMirrorBox? imageMirrorBox;
        private readonly ImageGridInfo? colorGridInfo;
        private readonly ImageGridInfo? alphaGridInfo;
        private readonly IccProfileColorInformation? iccProfileColorInformation;
        private readonly NclxColorInformation? nclxColorInformation;
        private readonly IImagingFactory imagingFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AvifReader"/> class.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="leaveOpen">true if the stream should be left open; otherwise, false.</param>
        /// <param name="imagingFactory">The imaging factory.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="input"/> is null.
        /// -or-
        /// <paramref name="imagingFactory"/> is null.
        /// </exception>
        public AvifReader(Stream input, bool leaveOpen, IImagingFactory imagingFactory)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(imagingFactory);

            // The parser is initialized first because it will throw an exception
            // if the AVIF file is invalid or not supported.
            this.parser = new AvifParser(input, leaveOpen);
            this.primaryItemId = this.parser.GetPrimaryItemId();
            this.alphaItemId = this.parser.GetAlphaItemId(this.primaryItemId);
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
                if (this.colorGridInfo != null)
                {
                    // Some images may associate the alpha image with the individual grid images
                    // instead of using a separate alpha image grid.
                    // See https://github.com/AOMediaCodec/libavif/issues/1203

                    List<uint> alphaImageIds = [];

                    foreach (uint colorItem in this.colorGridInfo.ChildImageIds)
                    {
                        uint alphaItem = this.parser.GetAlphaItemId(colorItem);

                        if (alphaItem != 0)
                        {
                            alphaImageIds.Add(alphaItem);
                        }
                        else
                        {
                            // The first image in the grid does not have an associated alpha image
                            // or only some of the grid images do.
                            //
                            // The image will be treated as not having an alpha channel.
                            break;
                        }
                    }

                    if (alphaImageIds.Count == this.colorGridInfo.ChildImageIds.Count)
                    {
                        this.alphaGridInfo = new ImageGridInfo(alphaImageIds, this.colorGridInfo);
                    }
                }
                else
                {
                    this.alphaGridInfo = null;
                }
            }

            // The HEIF specification allows an image to have up to one color information box of each type (ICC and/or NCLX).
            // See the HEIF specification Amendment 3, section 6.5.5.1.
            foreach (ColorInformationBox box in this.parser.EnumerateColorInformationBoxes(this.primaryItemId))
            {
                if (box is IccProfileColorInformation icc)
                {
                    if (this.iccProfileColorInformation != null)
                    {
                        ExceptionUtil.ThrowFormatException("The primary image has more than one ICC color information box.");
                    }

                    this.iccProfileColorInformation = icc;
                }
                else if (box is NclxColorInformation nclx)
                {
                    if (this.nclxColorInformation != null)
                    {
                        ExceptionUtil.ThrowFormatException("The primary image has more than one NCLX color information box.");
                    }

                    this.nclxColorInformation = nclx;
                }
            }
            this.imagingFactory = imagingFactory.CreateRef();
        }

        public ImageGridMetadata? ImageGridMetadata { get; private set; }

        public AvifReaderImage Decode()
        {
            VerifyNotDisposed();
            EnsureCompressedImagesAreAV1();
            EnsurePrimaryItemIsNotHidden();
            EnsureRequiredImagePropertiesAreSupported();

            AvifReaderImage outputImage = ProcessColorImage();

            try
            {
                if (this.alphaItemId != 0 || this.alphaGridInfo != null)
                {
                    ProcessAlphaImage(outputImage);
                }
                else
                {
                    AlphaChannelUtil.SetToOpaque(outputImage.Image);
                }
                ApplyImageTransforms(outputImage);
            }
            catch (Exception)
            {
                outputImage.Dispose();
                throw;
            }

            return outputImage;
        }

        public AvifItemData? GetExifData()
        {
            VerifyNotDisposed();

            ItemLocationEntry? entry = this.parser.TryGetExifLocation(this.primaryItemId);

            if (entry != null)
            {
                // The EXIF data block has a header consisting of a 4-byte unsigned integer that
                // indicates the number of bytes that come before the start of the TIFF header.
                // See ISO/IEC 23008-12:2017 section A.2.1.

                if (entry.TotalItemSize > sizeof(uint))
                {
                    return this.parser.ReadItemData(entry);
                }
            }

            return null;
        }

        public ReadOnlyMemory<byte> GetICCProfile()
        {
            return this.iccProfileColorInformation?.ProfileData ?? ReadOnlyMemory<byte>.Empty;
        }

        public AvifItemData? GetXmpData()
        {
            VerifyNotDisposed();

            ItemLocationEntry? entry = this.parser.TryGetXmpLocation(this.primaryItemId);

            return entry != null ? this.parser.ReadItemData(entry) : null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.parser?.Dispose();
            }
        }

        private static void CheckImageGridAndTileBounds(uint tileWidth, uint tileHeight, YUVChromaSubsampling tileChroma, ImageGridInfo gridInfo)
        {
            // The MIAF specification (ISO/IEC 23000-22:2019) requires that the tile size be at least 64x64.
            if (tileWidth < 64 || tileHeight < 64)
            {
                ExceptionUtil.ThrowFormatException("The image grid tile size must be at least 64x64.");
            }

            // HEIF (ISO/IEC 23008-12:2017), section 6.6.2.3.1:
            // The tiled input images shall completely “cover” the reconstructed image grid canvas...
            if ((tileWidth * gridInfo.TileColumnCount) < gridInfo.OutputWidth || (tileHeight * gridInfo.TileRowCount) < gridInfo.OutputHeight)
            {
                ExceptionUtil.ThrowFormatException("The image grid tiles do not cover the entire output image.");
            }

            if (tileChroma == YUVChromaSubsampling.Subsampling420 || tileChroma == YUVChromaSubsampling.Subsampling422)
            {
                if ((tileWidth & 1) != 0 || (gridInfo.OutputWidth & 1) != 0)
                {
                    ExceptionUtil.ThrowFormatException("The tile and output image width must be an even number.");
                }

                if (tileChroma == YUVChromaSubsampling.Subsampling420)
                {
                    if ((tileHeight & 1) != 0 || (gridInfo.OutputHeight & 1) != 0)
                    {
                        ExceptionUtil.ThrowFormatException("The tile and output image height must be an even number.");
                    }
                }
            }
        }

        private static AvifReaderImageFormat GetAvifReaderImageFormat(uint bitDepth, in CICPColorData colorData)
        {
            AvifReaderImageFormat format = AvifReaderImageFormat.Bgra32;

            if (bitDepth > 8)
            {
                // 10-bit, 12-bit or 16-bit.
                format = AvifReaderImageFormat.Rgba64;

                if (colorData.colorPrimaries == CICPColorPrimaries.BT2020)
                {
                    // Smpte2084 (SMPTE ST 2084, ITU BT.2100 PQ) is the only HDR format that we
                    // currently support reading.
                    if (colorData.transferCharacteristics == CICPTransferCharacteristics.Smpte2084)
                    {
                        format = AvifReaderImageFormat.Rgba128FloatPQ;
                    }
                }
            }

            return format;
        }

        private void ApplyImageTransforms(IOutputImageTransform outputImage)
        {
            // The image transforms must be applied in the following order:
            // Crop
            // Rotate
            // Flip horizontal or vertical

            if (this.cleanApertureBox != null)
            {
                outputImage.Crop(this.cleanApertureBox);
            }

            if (this.imageRotateBox != null)
            {
                switch (this.imageRotateBox.Rotation)
                {
                    case ImageRotation.RotateNone:
                        break;
                    case ImageRotation.Rotate90CCW:
                        outputImage.Rotate90CCW();
                        break;
                    case ImageRotation.Rotate180:
                        outputImage.Rotate180();
                        break;
                    case ImageRotation.Rotate270CCW:
                        outputImage.Rotate270CCW();
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
                        outputImage.FlipVertical();
                        break;
                    case ImageMirrorDirection.Horizontal:
                        outputImage.FlipHorizontal();
                        break;
                    default:
                        throw new InvalidOperationException("Unknown ImageMirrorDirection value.");
                }
            }
        }

        private void CheckImageItemType(uint itemId, ImageGridInfo? gridInfo, string imageName)
        {
            if (gridInfo != null)
            {
                IReadOnlyList<uint> childImageIds = gridInfo.ChildImageIds;

                for (int i = 0; i < childImageIds.Count; i++)
                {
                    CheckImageItemType(childImageIds[i], null, imageName);
                }
            }
            else
            {
                IItemInfoEntry? entry = this.parser.TryGetItemInfoEntry(itemId);

                if (entry is null)
                {
                    ExceptionUtil.ThrowFormatException($"The {imageName} image does not exist.");
                }
                else if (entry.ItemType != ItemInfoEntryTypes.AV01)
                {
                    if (entry.ItemType == ItemInfoEntryTypes.ImageGrid)
                    {
                        ExceptionUtil.ThrowFormatException("Nested image grids are not supported.");
                    }
                    else
                    {
                        ExceptionUtil.ThrowFormatException($"The {imageName} image is not a supported format.");
                    }
                }
            }
        }

        private void CheckRequiredImageProperties(uint itemId, ImageGridInfo? gridInfo, string imageName)
        {
            if (gridInfo != null)
            {
                IReadOnlyList<uint> childImageIds = gridInfo.ChildImageIds;

                for (int i = 0; i < childImageIds.Count; i++)
                {
                    this.parser.ValidateRequiredImageProperties(childImageIds[i]);
                }
            }
            else
            {
                IItemInfoEntry? entry = this.parser.TryGetItemInfoEntry(itemId);

                if (entry is null)
                {
                    ExceptionUtil.ThrowFormatException($"The {imageName} image does not exist.");
                }
                else if (entry.ItemType == ItemInfoEntryTypes.AV01)
                {
                    this.parser.ValidateRequiredImageProperties(itemId);
                }
                else
                {
                    ExceptionUtil.ThrowFormatException($"The {imageName} image is not a supported format.");
                }
            }
        }

        private DecoderImage ReadImage(uint itemId, CICPColorData? colorConversionInfo)
        {
            ItemLocationEntry? entry = this.parser.TryGetItemLocation(itemId);

            if (entry is null)
            {
                ExceptionUtil.ThrowFormatException("The color image item location was not found.");
            }

            LayerSelectorInfo? layerInfo = this.parser.GetLayerSelectorInfo(itemId, entry.TotalItemSize);
            // Read all of the image data, unless there are additional spatial layers beyond the one we want.
            ulong? numberOfBytesToRead = null;

            DecoderLayerInfo decoderLayerInfo;

            if (layerInfo != null)
            {
                decoderLayerInfo = new(layerInfo.SpatialLayerId, true, GetOperatingPoint(itemId));
                numberOfBytesToRead = layerInfo.LayerDataSize;
            }
            else
            {
                decoderLayerInfo = new(0, false, GetOperatingPoint(itemId));
            }

            using (AvifItemData color = this.parser.ReadItemData(entry, numberOfBytesToRead))
            {
                return AvifNative.DecodeImage(color, colorConversionInfo, decoderLayerInfo);
            }
        }

        private void EnsureCompressedImagesAreAV1()
        {
            CheckImageItemType(this.primaryItemId, this.colorGridInfo, "color");

            if (this.alphaItemId != 0 || this.alphaGridInfo != null)
            {
                CheckImageItemType(this.alphaItemId, this.alphaGridInfo, "alpha");
            }
        }

        private void EnsurePrimaryItemIsNotHidden()
        {
            IItemInfoEntry? entry = this.parser.TryGetItemInfoEntry(this.primaryItemId);

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

            if (this.alphaItemId != 0 || this.alphaGridInfo != null)
            {
                CheckRequiredImageProperties(this.alphaItemId, this.alphaGridInfo, "alpha");
            }
        }

        private void FillAlphaImageGrid(AvifReaderImage outputImage)
        {
            this.alphaGridInfo!.CheckAvailableTileCount();

            IReadOnlyList<uint> childImageIds = this.alphaGridInfo.ChildImageIds;
            bool firstTile = true;
            DecoderImageInfo? firstTileInfo = null;

            // The tiles are encoded from top to bottom then left to right.

            for (int row = 0; row < this.alphaGridInfo.TileRowCount; row++)
            {
                int startIndex = row * this.alphaGridInfo.TileColumnCount;

                for (int col = 0; col < this.alphaGridInfo.TileColumnCount; col++)
                {
                    using (DecoderImage image = ReadImage(childImageIds[startIndex + col], null))
                    {
                        if (firstTile)
                        {
                            firstTile = false;
                            firstTileInfo = image.Info;

                            // Skip the image grid validation if the image grid only has one tile.
                            // Some writers may use an image grid to crop a single image.
                            if (childImageIds.Count > 1)
                            {
                                CheckImageGridAndTileBounds(firstTileInfo.Width,
                                                            firstTileInfo.Height,
                                                            firstTileInfo.ChromaSubsampling,
                                                            this.alphaGridInfo);
                            }
                        }
                        else
                        {
                            if (image.Info != firstTileInfo)
                            {
                                ExceptionUtil.ThrowFormatException("The image tiles must use the same size, YUV format, bit depth, and NCLX data.");
                            }
                        }

                        AvifNative.ReadAlphaImageData(image, (uint)col, (uint)row, outputImage);
                    }
                }
            }
        }

        private AvifReaderImage FillColorImageGrid(SizeInt32 outputImageSize, CICPColorData? containerColorInfo)
        {
            AvifReaderImage? outputImage = null;

            this.colorGridInfo!.CheckAvailableTileCount();
            DecoderImageInfo? firstTileInfo = null;

            IReadOnlyList<uint> childImageIds = this.colorGridInfo.ChildImageIds;
            bool firstTile = true;
            CICPColorData colorData = new CICPColorData();

            try
            {
                // The tiles are encoded from top to bottom then left to right.
                for (int row = 0; row < this.colorGridInfo.TileRowCount; row++)
                {
                    int startIndex = row * this.colorGridInfo.TileColumnCount;

                    for (int col = 0; col < this.colorGridInfo.TileColumnCount; col++)
                    {
                        using (DecoderImage image = ReadImage(childImageIds[startIndex + col], containerColorInfo))
                        {
                            if (firstTile)
                            {
                                firstTile = false;
                                firstTileInfo = image.Info;

                                // Skip the image grid validation if the image grid only has one tile.
                                // Some writers may use an image grid to crop a single image.
                                if (childImageIds.Count > 1)
                                {
                                    CheckImageGridAndTileBounds(firstTileInfo.Width,
                                                                firstTileInfo.Height,
                                                                firstTileInfo.ChromaSubsampling,
                                                                this.colorGridInfo);
                                }

                                colorData = containerColorInfo ?? firstTileInfo.CICPColor;

                                outputImage = new(outputImageSize,
                                                  GetAvifReaderImageFormat(image.BitDepth, colorData),
                                                  colorData,
                                                  this.imagingFactory);
                            }
                            else
                            {
                                if (image.Info != firstTileInfo)
                                {
                                    ExceptionUtil.ThrowFormatException("The image tiles must use the same size, YUV format, bit depth, and NCLX data.");
                                }
                            }

                            AvifNative.ReadColorImageData(image, colorData, (uint)col, (uint)row, outputImage!);
                        }
                    }
                }

                this.ImageGridMetadata = new ImageGridMetadata(this.colorGridInfo, firstTileInfo!.Width, firstTileInfo.Height);
            }
            catch (Exception)
            {
                outputImage?.Dispose();
                throw;
            }

            return outputImage!;
        }

        private SizeInt32 GetImageSize(uint itemId, ImageGridInfo? gridInfo, string imageName)
        {
            IItemInfoEntry? entry = this.parser.TryGetItemInfoEntry(itemId);

            if (entry is null)
            {
                ExceptionUtil.ThrowFormatException($"The {imageName} image does not exist.");
            }

            uint width;
            uint height;

            if (entry.ItemType == ItemInfoEntryTypes.AV01)
            {
                ImageSpatialExtentsBox? extents = this.parser.TryGetAssociatedItemProperty<ImageSpatialExtentsBox>(itemId);

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

            return new SizeInt32((int)width, (int)height);
        }

        private byte GetOperatingPoint(uint itemId)
        {
            byte operatingPoint = 0;

            AV1OperatingPointBox? box = this.parser.TryGetAssociatedItemProperty<AV1OperatingPointBox>(itemId);

            if (box != null)
            {
                operatingPoint = box.OperatingPointIndex;
            }

            return operatingPoint;
        }

        private void ProcessAlphaImage(AvifReaderImage outputImage)
        {
            if (this.alphaGridInfo != null)
            {
                FillAlphaImageGrid(outputImage);
            }
            else
            {
                using (DecoderImage image = ReadImage(this.alphaItemId, null))
                {
                    AvifNative.ReadAlphaImageData(image, 0, 0, outputImage);
                }
            }

            if (this.parser.IsAlphaPremultiplied(this.primaryItemId, this.alphaItemId))
            {
                outputImage.IsPremultipliedAlpha = true;
            }
        }

        private AvifReaderImage ProcessColorImage()
        {
            AvifReaderImage outputImage;

            CICPColorData? containerColorData = null;
            if (this.nclxColorInformation != null)
            {
                containerColorData = new CICPColorData
                {
                    colorPrimaries = this.nclxColorInformation.ColorPrimaries,
                    transferCharacteristics = this.nclxColorInformation.TransferCharacteristics,
                    matrixCoefficients = this.nclxColorInformation.MatrixCoefficients,
                    fullRange = this.nclxColorInformation.FullRange
                };
            }

            SizeInt32 imageSize = GetImageSize(this.primaryItemId, this.colorGridInfo, "color");

            if (this.colorGridInfo != null)
            {
                outputImage = FillColorImageGrid(imageSize, containerColorData);
            }
            else
            {
                using (DecoderImage image = ReadImage(this.primaryItemId, containerColorData))
                {
                    CICPColorData colorData = containerColorData ?? image.CICPColor;

                    outputImage = new(imageSize,
                                      GetAvifReaderImageFormat(image.BitDepth, colorData),
                                      colorData,
                                      this.imagingFactory);

                    try
                    {
                        AvifNative.ReadColorImageData(image,
                                                      colorData,
                                                      0,
                                                      0,
                                                      outputImage);
                    }
                    catch (Exception)
                    {
                        outputImage.Dispose();
                        throw;
                    }
                }
            }

            return outputImage;
        }
    }
}
