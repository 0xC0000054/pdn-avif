////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
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
        public HomogeneousTileInfo(Dictionary<int, int> duplicateTileMap,
                                   HashSet<int> homogeneousTiles)
        {
            if (duplicateTileMap is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(duplicateTileMap));
            }

            if (homogeneousTiles is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(homogeneousTiles));
            }

            this.DuplicateTileMap = duplicateTileMap;
            this.HomogeneousTiles = homogeneousTiles;
        }

        public Dictionary<int, int> DuplicateTileMap { get; }

        public HashSet<int> HomogeneousTiles { get; }
    }
}
