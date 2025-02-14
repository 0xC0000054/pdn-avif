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

namespace AvifFileType.AvifContainer
{
    /// <summary>
    /// Describes the AV1 chroma sample position
    /// </summary>
    internal enum ChromaSamplePosition
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Horizontally co-located with (0, 0) luma sample, vertical position in the middle between two luma samples.
        /// </summary>
        Vertical = 1,

        /// <summary>
        /// Co-located with (0, 0) luma sample.
        /// </summary>
        CoLocated = 2
    }
}
