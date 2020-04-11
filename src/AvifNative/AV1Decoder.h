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

DecoderStatus DecompressAV1Image(
    const uint8_t* compressedColorImage,
    size_t compressedColorImageSize,
    const uint8_t* compressedAlphaImage,
    size_t compressedAlphaImageSize,
    const ColorConversionInfo* colorInfo,
    const DecodeInfo* decodeInfo,
    BitmapData* outputImage);
