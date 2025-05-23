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

#include "AvifNative.h"
#include "Memory.h"
#include "ChromaSubsampling.h"
#include "AV1Decoder.h"
#include "AV1Encoder.h"
#include "aom/aom.h"
#include "aom/aom_image.h"
#include <memory>

namespace AvifNative
{
    namespace details
    {
        struct aom_image_deleter
        {
            void operator()(aom_image* img) noexcept
            {
                if (img)
                {
                    aom_img_free(img);
                }
            }
        };
    }

    typedef std::unique_ptr<aom_image, details::aom_image_deleter> ScopedAOMImage;
}

DecoderStatus __stdcall DecodeImage(
    const uint8_t* compressedImage,
    size_t compressedImageSize,
    const CICPColorData* containerColorInfo,
    const DecoderLayerInfo* frameInfo,
    DecoderImageHandle** imageHandle,
    DecoderImageInfo* imageInfo)
{
    return DecoderLoadImage(
        compressedImage,
        compressedImageSize,
        containerColorInfo,
        frameInfo,
        imageHandle,
        imageInfo);
}

DecoderStatus __stdcall ReadColorImageData(
    const DecoderImageHandle* imageHandle,
    const CICPColorData* colorInfo,
    uint32_t tileColumnIndex,
    uint32_t tileRowIndex,
    BitmapData* outputImage)
{
    return DecoderConvertColorImage(imageHandle, colorInfo, tileColumnIndex, tileRowIndex, outputImage);
}

DecoderStatus __stdcall ReadAlphaImageData(
    const DecoderImageHandle* imageHandle,
    uint32_t tileColumnIndex,
    uint32_t tileRowIndex,
    BitmapData* outputImage)
{
    return DecoderConvertAlphaImage(imageHandle, tileColumnIndex, tileRowIndex, outputImage);
}

void FreeDecoderImageHandle(DecoderImageHandle* imageHandle)
{
    DecoderFreeImageHandle(imageHandle);
}

EncoderStatus __stdcall CompressColorImage(
    const BitmapData* image,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    const CICPColorData& colorInfo,
    CompressedAV1OutputAlloc outputAllocator,
    void** compressedColorImage)
{
    if (!image || !encodeOptions || !progressContext || !outputAllocator || !compressedColorImage)
    {
        return EncoderStatus::NullParameter;
    }

    const YUVChromaSubsampling yuvFormat = encodeOptions->yuvFormat;

    aom_img_fmt aomFormat;
    switch (yuvFormat)
    {
    case YUVChromaSubsampling::Subsampling400:
    case YUVChromaSubsampling::Subsampling420:
        aomFormat = AOM_IMG_FMT_I420;
        break;
    case YUVChromaSubsampling::Subsampling422:
        aomFormat = AOM_IMG_FMT_I422;
        break;
    case YUVChromaSubsampling::Subsampling444:
    case YUVChromaSubsampling::IdentityMatrix:
        aomFormat = AOM_IMG_FMT_I444;
        break;
    default:
        return EncoderStatus::UnknownYUVFormat;
    }

    AvifNative::ScopedAOMImage color(ConvertColorToAOMImage(image, colorInfo, yuvFormat, aomFormat));
    if (!color)
    {
        return EncoderStatus::OutOfMemory;
    }

    return CompressAOMColorImage(color.get(), encodeOptions, progressContext, outputAllocator, compressedColorImage);
}

EncoderStatus __stdcall CompressAlphaImage(
    const BitmapData* image,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    CompressedAV1OutputAlloc outputAllocator,
    void** compressedAlphaImage)
{
    if (!image || !encodeOptions || !progressContext || !outputAllocator || !compressedAlphaImage)
    {
        return EncoderStatus::NullParameter;
    }

    AvifNative::ScopedAOMImage alpha(ConvertAlphaToAOMImage(image));
    if (!alpha)
    {
        return EncoderStatus::OutOfMemory;
    }

    return CompressAOMAlphaImage(alpha.get(), encodeOptions, progressContext, outputAllocator, compressedAlphaImage);
}

const char* const __stdcall GetAOMVersionString()
{
    return aom_codec_version_str();
}
