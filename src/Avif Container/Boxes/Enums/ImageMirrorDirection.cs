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

namespace AvifFileType.AvifContainer
{
    internal enum ImageMirrorDirection
    {
        /// <summary>
        /// Mirror (flip) the image vertically.
        /// </summary>
        Vertical = 0,
        /// <summary>
        /// Mirror (flip) the image horizontally.
        /// </summary>
        Horizontal = 1
    }
}
