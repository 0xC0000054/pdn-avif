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

#pragma once

#include "AvifNative.h"
#include "YUVAImage.h"

EncoderStatus ConvertBitmapDataToYUVA(
    const BitmapData* bgraImage,
    bool includeTransparency,
    const ColorConversionInfo* colorInfo,
    YUVChromaSubsampling yuvFormat,
    YUVAImage* yuvaImage);
