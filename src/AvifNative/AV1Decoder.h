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

DecoderStatus DecodeColorImage(
    const uint8_t* compressedColorImage,
    size_t compressedColorImageSize,
    const ColorConversionInfo* colorInfo,
    DecodeInfo* decodeInfo,
    BitmapData* outputImage);

DecoderStatus DecodeAlphaImage(
    const uint8_t* compressedAlphaImage,
    size_t compressedAlphaImageSize,
    DecodeInfo* decodeInfo,
    BitmapData* outputImage);
