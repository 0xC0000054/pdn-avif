////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace AvifFileType
{
    internal sealed class HomogeneousTileInfo
    {
        public HomogeneousTileInfo(Dictionary<int, int> duplicateColorTileMap,
                                   HashSet<int> homogeneousColorTiles,
                                   Dictionary<int, int> duplicateAlphaTileMap,
                                   HashSet<int> homogeneousAlphaTiles)
        {
            if (duplicateColorTileMap is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(duplicateColorTileMap));
            }

            if (homogeneousColorTiles is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(homogeneousColorTiles));
            }

            if (duplicateAlphaTileMap is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(duplicateAlphaTileMap));
            }

            if (homogeneousAlphaTiles is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(homogeneousAlphaTiles));
            }

            this.DuplicateColorTileMap = duplicateColorTileMap;
            this.HomogeneousColorTiles = homogeneousColorTiles;
            this.DuplicateAlphaTileMap = duplicateAlphaTileMap;
            this.HomogeneousAlphaTiles = homogeneousAlphaTiles;
        }

        public Dictionary<int, int> DuplicateColorTileMap { get; }

        public HashSet<int> HomogeneousColorTiles { get; }

        public Dictionary<int, int> DuplicateAlphaTileMap { get; }

        public HashSet<int> HomogeneousAlphaTiles { get; }
    }
}
