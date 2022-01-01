////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

#pragma once

#include <stdint.h>

// This must be kept in sync with CICPColorPrimaries.cs
enum class CICPColorPrimaries : uint16_t
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
};

// This must be kept in sync with CICPTransferCharacteristics.cs
enum class CICPTransferCharacteristics : uint16_t
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
};

// This must be kept in sync with CICPMatrixCoefficients.cs
enum class CICPMatrixCoefficients : uint16_t
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
};
