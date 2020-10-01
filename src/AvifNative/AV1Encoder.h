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
#include "aom/aom_image.h"

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

EncoderStatus CompressAOMImages(
    const aom_image* color,
    const aom_image* alpha,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    CompressedAV1OutputAlloc outputAllocator,
    void** compressedColorImage,
    void** compressedAlphaImage);

#ifdef __cplusplus
}
#endif // __cplusplus
