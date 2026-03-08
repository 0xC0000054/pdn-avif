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

using PaintDotNet.FileTypes;
using System;
using System.Globalization;

namespace AvifFileType.AvifContainer
{
    internal sealed class ImageGridMetadata
    {
        private readonly Lazy<int> tileCount;

        public ImageGridMetadata(int tileColumnCount, int tileRowCount, uint outputHeight, uint outputWidth, uint tileImageHeight, uint tileImageWidth)
        {
            if (tileColumnCount < 1 || tileColumnCount > 256)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(tileColumnCount), $"Must be in the range of [1, 256], actual value: { tileColumnCount }.");
            }

            if (tileRowCount < 1 || tileRowCount > 256)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(tileRowCount), $"Must be in the range of [1, 256], actual value: { tileRowCount }.");
            }

            this.TileColumnCount = tileColumnCount;
            this.TileRowCount = tileRowCount;
            this.OutputHeight = outputHeight;
            this.OutputWidth = outputWidth;
            this.TileImageHeight = tileImageHeight;
            this.TileImageWidth = tileImageWidth;
            this.tileCount = new Lazy<int>(GetTileCount);
        }

        public ImageGridMetadata(ImageGridInfo gridInfo, uint tileImageHeight, uint tileImageWidth)
        {
            if (gridInfo is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(gridInfo));
            }

            this.TileColumnCount = gridInfo.TileColumnCount;
            this.TileRowCount = gridInfo.TileRowCount;
            this.OutputHeight = gridInfo.OutputHeight;
            this.OutputWidth = gridInfo.OutputWidth;
            this.TileImageHeight = tileImageHeight;
            this.TileImageWidth = tileImageWidth;
            this.tileCount = new Lazy<int>(GetTileCount);
        }

        public int TileColumnCount { get; }

        public int TileRowCount { get; }

        public uint OutputHeight { get; }

        public uint OutputWidth { get; }

        public uint TileImageHeight { get; }

        public uint TileImageWidth { get; }

        public int TileCount => this.tileCount.Value;

        public bool IsValidForImage(uint documentWidth, uint documentHeight, YUVChromaSubsampling yuvFormat)
        {
            return this.OutputWidth == documentWidth
                   && this.OutputHeight == documentHeight
                   && (this.TileColumnCount * this.TileImageWidth) == documentWidth
                   && (this.TileRowCount * this.TileImageHeight) == documentHeight
                   && IsValidForYUVFormat(yuvFormat);
        }

        public void SerializeToPropertyBag(IFileTypePropertyBag propertyBag)
        {
            propertyBag.Add($"{nameof(AvifFileType)}.{nameof(this.TileColumnCount)}", this.TileColumnCount);
            propertyBag.Add($"{nameof(AvifFileType)}.{nameof(this.TileRowCount)}", this.TileRowCount);
            propertyBag.Add($"{nameof(AvifFileType)}.{nameof(this.OutputHeight)}", this.OutputHeight);
            propertyBag.Add($"{nameof(AvifFileType)}.{nameof(this.OutputWidth)}", this.OutputWidth);
            propertyBag.Add($"{nameof(AvifFileType)}.{nameof(this.TileImageHeight)}", this.TileImageHeight);
            propertyBag.Add($"{nameof(AvifFileType)}.{nameof(this.TileImageWidth)}", this.TileImageWidth);
        }

        public static ImageGridMetadata? TryDeserializeFromPropertyBag(IReadOnlyFileTypePropertyBag propertyBag)
        {
            if (propertyBag.TryGetValue($"{nameof(AvifFileType)}.{nameof(TileColumnCount)}", out int tileColumnCount) &&
                propertyBag.TryGetValue($"{nameof(AvifFileType)}.{nameof(TileRowCount)}", out int tileRowCount) &&
                propertyBag.TryGetValue($"{nameof(AvifFileType)}.{nameof(OutputHeight)}", out uint outputHeight) &&
                propertyBag.TryGetValue($"{nameof(AvifFileType)}.{nameof(OutputWidth)}", out uint outputWidth) &&
                propertyBag.TryGetValue($"{nameof(AvifFileType)}.{nameof(TileImageHeight)}", out uint tileImageHeight) &&
                propertyBag.TryGetValue($"{nameof(AvifFileType)}.{nameof(TileImageWidth)}", out uint tileImageWidth))
            {
                return new ImageGridMetadata(tileColumnCount, tileRowCount, outputHeight, outputWidth, tileImageHeight, tileImageWidth);
            }
            else
            {
                return null;
            }
        }

        private int GetTileCount()
        {
            return checked(this.TileColumnCount * this.TileRowCount);
        }

        private bool IsValidForYUVFormat(YUVChromaSubsampling yuvFormat)
        {
            if (yuvFormat == YUVChromaSubsampling.Subsampling420 || yuvFormat == YUVChromaSubsampling.Subsampling422)
            {
                // Some of the YUV formats require the tile and image grid output sizes to be an even number.
                if ((this.TileColumnCount & 1) != 0 || (this.OutputWidth & 1) != 0)
                {
                    return false;
                }

                if (yuvFormat == YUVChromaSubsampling.Subsampling420)
                {
                    if ((this.TileRowCount & 1) != 0 || (this.OutputHeight & 1) != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
