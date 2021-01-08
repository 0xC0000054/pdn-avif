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

#pragma once

#include "AvifNative.h"

struct YUVCoefficiants
{
    float kr;
    float kg;
    float kb;
};

void GetYUVCoefficiants(
    const CICPColorData& colorInfo,
    YUVCoefficiants& yuvData);
