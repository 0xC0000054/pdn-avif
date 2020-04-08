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

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

EncoderStatus CompressYUVAImage(
    const YUVAImage* image,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    void** compressedColorImage,
    size_t* compressedColorImageSize,
    void** compressedAlphaImage,
    size_t* compressedAlphaImageSize);

#ifdef __cplusplus
}
#endif // __cplusplus
