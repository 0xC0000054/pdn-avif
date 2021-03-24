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
#include <aom/aom_image.h>

DecoderStatus ConvertColorImage(
    const aom_image_t* frame,
    const CICPColorData* containerColorInfo,
    DecodeInfo* decodeInfo,
    BitmapData* outputBGRAImageData);

DecoderStatus ConvertAlphaImage(
    const aom_image_t* frame,
    DecodeInfo* decodeInfo,
    BitmapData* outputBGRAImageData);
