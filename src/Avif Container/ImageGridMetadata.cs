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

using System;
using System.Globalization;

namespace AvifFileType.AvifContainer
{
    internal sealed class ImageGridMetadata
    {
        private const string TileColumnCountPropertyName = "TileColumnCount";
        private const string TileRowCountPropertyName = "TileRowCount";
        private const string OutputHeightPropertyName = "OutputHeight";
        private const string OutputWidthPropertyName = "OutputWidth";
        private const string TileImageHeightPropertyName = "TileImageHeight";
        private const string TileImageWidthPropertyName = "TileImageWidth";

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

        public static ImageGridMetadata? TryDeserialize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!value.StartsWith("<ImageGridMetadata", StringComparison.Ordinal))
            {
                return null;
            }

            int tileColumnCount = (int)GetPropertyValue(value, TileColumnCountPropertyName);
            int tileRowCount = (int)GetPropertyValue(value, TileRowCountPropertyName);
            uint outputHeight = GetPropertyValue(value, OutputHeightPropertyName);
            uint outputWidth = GetPropertyValue(value, OutputWidthPropertyName);
            uint tileImageHeight = GetPropertyValue(value, TileImageHeightPropertyName);
            uint tileImageWidth = GetPropertyValue(value, TileImageWidthPropertyName);

            return new ImageGridMetadata(tileColumnCount, tileRowCount, outputHeight, outputWidth, tileImageHeight, tileImageWidth);
        }

        public string SerializeToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                                 "<ImageGridMetadata {0}=\"{1}\"{2}=\"{3}\"{4}=\"{5}\"{6}=\"{7}\"{8}=\"{9}\"{10}=\"{11}\" />",
                                 TileColumnCountPropertyName,
                                 this.TileColumnCount,
                                 TileRowCountPropertyName,
                                 this.TileRowCount,
                                 OutputHeightPropertyName,
                                 this.OutputHeight,
                                 OutputWidthPropertyName,
                                 this.OutputWidth,
                                 TileImageHeightPropertyName,
                                 this.TileImageHeight,
                                 TileImageWidthPropertyName,
                                 this.TileImageWidth);
        }

        private int GetTileCount()
        {
            return checked(this.TileColumnCount * this.TileRowCount);
        }

        private static uint GetPropertyValue(string haystack, string propertyName)
        {
            string needle = propertyName + "=\"";

            int valueStartIndex = haystack.IndexOf(needle, StringComparison.Ordinal) + needle.Length;
            int valueEndIndex = haystack.IndexOf('"', valueStartIndex);

            string propertyValue = haystack.Substring(valueStartIndex, valueEndIndex - valueStartIndex);

            return uint.Parse(propertyValue, CultureInfo.InvariantCulture);
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
