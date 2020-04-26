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

namespace AvifFileType.AvifContainer
{
    // These values are from the "Color config semantics" section of the AV1 specification.
    // https://aomediacodec.github.io/av1-spec/

    internal enum NclxMatrixCoefficients : ushort
    {
        /// <summary>
        /// Identity matrix
        /// </summary>
        Identity = 0,

        /// <summary>
        /// BT.709
        /// </summary>
        BT709 = 1,

        /// <summary>
        /// Unspecified
        /// </summary>
        Unspecified = 2,

        /// <summary>
        /// For future use
        /// </summary>
        Reserved3 = 3,

        /// <summary>
        /// US FCC 73.628
        /// </summary>
        FCC = 4,

        /// <summary>
        /// BT.470 System B, G (historical)
        /// </summary>
        BT470BG = 5,

        /// <summary>
        /// BT.601
        /// </summary>
        BT601 = 6,

        /// <summary>
        /// SMPTE 240 M
        /// </summary>
        Smpte240 = 7,

        /// <summary>
        /// YCgCo
        /// </summary>
        YCgCo = 8,

        /// <summary>
        /// BT.2020 non-constant luminance, BT.2100 YCbCr
        /// </summary>
        BT2020NCL = 9,

        /// <summary>
        /// BT.2020 constant luminance
        /// </summary>
        BT2020CL = 10,

        /// <summary>
        /// SMPTE ST 2085 YDzDx
        /// </summary>
        Smpte2085 = 11,

        /// <summary>
        /// Chromaticity-derived non-constant luminance
        /// </summary>
        CromatNCL = 12,

        /// <summary>
        /// Chromaticity-derived constant luminance
        /// </summary>
        CromatCL = 13,

        /// <summary>
        /// BT.2100 ICtCp
        /// </summary>
        ICtCp = 14
    }
}
