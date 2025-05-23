﻿////////////////////////////////////////////////////////////////////////
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

using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal sealed class ImageGridInfo
    {
        public ImageGridInfo(IReadOnlyList<uint> childImageIds, ImageGridDescriptor grid)
        {
            if (childImageIds is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(childImageIds));
            }

            if (grid is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(grid));
            }

            this.ChildImageIds = childImageIds;
            this.TileColumnCount = grid.ColumnsMinusOne + 1;
            this.TileRowCount = grid.RowsMinusOne + 1;
            this.OutputWidth = grid.OutputWidth;
            this.OutputHeight = grid.OutputHeight;
        }

        public ImageGridInfo(IReadOnlyList<uint> alphaImageIds, ImageGridInfo colorGridInfo)
        {
            if (alphaImageIds is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(alphaImageIds));
            }

            if (colorGridInfo is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(colorGridInfo));
            }

            this.ChildImageIds = alphaImageIds;
            this.TileColumnCount = colorGridInfo.TileColumnCount;
            this.TileRowCount = colorGridInfo.TileRowCount;
            this.OutputWidth = colorGridInfo.OutputWidth;
            this.OutputHeight = colorGridInfo.OutputHeight;
        }

        public IReadOnlyList<uint> ChildImageIds { get; }

        public int TileColumnCount { get; }

        public int TileRowCount { get; }

        public uint OutputWidth { get; }

        public uint OutputHeight { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"Columns: {this.TileColumnCount}, Rows: {this.TileRowCount}, OutputWidth {this.OutputWidth}, OutputHeight: {this.OutputHeight}";
            }
        }

        public void CheckAvailableTileCount()
        {
            int requiredTileCount = this.TileColumnCount * this.TileRowCount;

            if (this.ChildImageIds.Count != requiredTileCount)
            {
                ExceptionUtil.ThrowFormatException("The image grid does not have the required number of tiles.");
            }
        }
    }
}
