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

#pragma once

#include "AvifNative.h"

DecoderStatus DecoderLoadImage(
    const uint8_t* compressedImage,
    size_t compressedImageSize,
    const CICPColorData* containerColorInfo,
    const DecoderLayerInfo* layerInfo,
    DecoderImageHandle** imageHandle,
    DecoderImageInfo* imageInfo);

void DecoderFreeImageHandle(DecoderImageHandle* handle);

DecoderStatus DecoderConvertColorImage(
    const DecoderImageHandle* imageHandle,
    const CICPColorData* colorInfo,
    uint32_t tileColumnIndex,
    uint32_t tileRowIndex,
    BitmapData* outputImage);

DecoderStatus DecoderConvertAlphaImage(
    const DecoderImageHandle* imageHandle,
    uint32_t tileColumnIndex,
    uint32_t tileRowIndex,
    BitmapData* outputImage);
