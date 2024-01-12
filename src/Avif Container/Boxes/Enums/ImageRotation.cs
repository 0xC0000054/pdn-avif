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

namespace AvifFileType.AvifContainer
{
    internal enum ImageRotation
    {
        /// <summary>
        /// No rotation required.
        /// </summary>
        RotateNone = 0,
        /// <summary>
        /// Rotate 90 degrees counter-clockwise.
        /// </summary>
        Rotate90CCW = 1,
        /// <summary>
        /// Rotate 180 degrees
        /// </summary>
        Rotate180 = 2,
        /// <summary>
        /// Rotate 270 degrees counter-clockwise.
        /// </summary>
        Rotate270CCW = 3
    }
}
