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
    // These values are from the ITU-T H.273 (2016) specification.
    // https://www.itu.int/rec/T-REC-H.273-201612-I/en

    internal enum CICPColorPrimaries : ushort
    {
        /// <summary>
        /// BT.709
        /// </summary>
        BT709 = 1,

        /// <summary>
        /// Unspecified
        /// </summary>
        Unspecified = 2,

        /// <summary>
        /// BT.470 System M (historical)
        /// </summary>
        BT470M = 4,

        /// <summary>
        /// BT.470 System B, G (historical)
        /// </summary>
        BT470BG = 5,

        /// <summary>
        /// BT.601
        /// </summary>
        BT601 = 6,

        /// <summary>
        /// SMPTE 240
        /// </summary>
        Smpte240 = 7,

        /// <summary>
        /// Generic film (color filters using illuminant C)
        /// </summary>
        GenericFilm = 8,

        /// <summary>
        /// BT.2020, BT.2100
        /// </summary>
        BT2020 = 9,

        /// <summary>
        /// SMPTE 428 (CIE 1921 XYZ)
        /// </summary>
        Xyz = 10,

        /// <summary>
        /// SMPTE RP 431-2
        /// </summary>
        Smpte431 = 11,

        /// <summary>
        /// SMPTE EG 432-1
        /// </summary>
        Smpte432 = 12,

        /// <summary>
        /// EBU Tech. 3213-E
        /// </summary>
        Ebu3213 = 22
    }
}
