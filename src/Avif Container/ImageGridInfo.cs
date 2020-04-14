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

using System.Collections.Generic;

namespace AvifFileType.AvifContainer
{
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

        public IReadOnlyList<uint> ChildImageIds { get; }

        public int TileColumnCount { get; }

        public int TileRowCount { get; }

        public uint OutputWidth { get; }

        public uint OutputHeight { get; }

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
