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
#include "aom/aom_image.h"

aom_image_t* ConvertColorToAOMImage(
    const BitmapData* bgraImage,
    const CICPColorData& colorInfo,
    YUVChromaSubsampling yuvFormat,
    aom_img_fmt aomFormat);

aom_image_t* ConvertAlphaToAOMImage(const BitmapData* bgraImage);
