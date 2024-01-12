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

    internal enum CICPTransferCharacteristics : ushort
    {
        /// <summary>
        /// For future use
        /// </summary>
        Reserved0 = 0,

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
        /// Linear
        /// </summary>
        Linear = 8,

        /// <summary>
        /// Logarithmic (100 : 1 range)
        /// </summary>
        Log100 = 9,

        /// <summary>
        /// Logarithmic (100 * Sqrt(10) : 1 range)
        /// </summary>
        Log100Sqrt10 = 10,

        /// <summary>
        /// IEC 61966-2-4
        /// </summary>
        IEC61966 = 11,

        /// <summary>
        /// BT.1361
        /// </summary>
        BT1361 = 12,

        /// <summary>
        /// sRGB or sYCC
        /// </summary>
        Srgb = 13,

        /// <summary>
        /// BT.2020 10-bit systems
        /// </summary>
        BT2020TenBit = 14,

        /// <summary>
        /// BT.2020 12-bit systems
        /// </summary>
        BT2020TwelveBit = 15,

        /// <summary>
        /// SMPTE ST 2084, ITU BT.2100 PQ
        /// </summary>
        Smpte2084 = 16,

        /// <summary>
        /// SMPTE ST 428
        /// </summary>
        Smpte428 = 17,

        /// <summary>
        /// BT.2100 HLG, ARIB STD-B67
        /// </summary>
        HLG = 18
    }
}
