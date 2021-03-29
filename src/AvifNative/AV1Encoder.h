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

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

EncoderStatus CompressAOMImage(
    const aom_image* image,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    CompressedAV1OutputAlloc outputAllocator,
    void** compressedImage);

#ifdef __cplusplus
}
#endif // __cplusplus
