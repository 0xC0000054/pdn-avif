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

namespace AvifFileType
{
    internal enum YUVChromaSubsampling
    {
        /// <summary>
        /// YUV 4:2:0
        /// </summary>
        Subsampling420,

        /// <summary>
        /// YUV 4:2:2
        /// </summary>
        Subsampling422,

        /// <summary>
        /// YUV 4:4:4
        /// </summary>
        Subsampling444,

        /// <summary>
        /// YUV 4:0:0
        /// </summary>
        /// <remarks>
        /// Used internally for gray-scale images, not shown to the user.
        /// </remarks>
        Subsampling400,

        /// <summary>
        /// The RGB color values are used directly, without YUV conversion.
        /// </summary>
        /// <remarks>
        /// Used internally for lossless RGB encoding, not shown to the user.
        /// </remarks>
        IdentityMatrix
    }
}
