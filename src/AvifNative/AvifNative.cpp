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

#include "AvifNative.h"
#include "Memory.h"
#include "ChromaSubsampling.h"
#include "AV1Decoder.h"
#include "AV1Encoder.h"
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

    typedef std::unique_ptr<aom_image, details::aom_image_deleter> unique_aom_image;
}

namespace
{
    EncoderStatus CompressWithAOM(
        const BitmapData* image,
        bool includeTransparency,
        const EncoderOptions* encodeOptions,
        ProgressContext* progressContext,
        const ColorConversionInfo* colorInfo,
        void** compressedColorImage,
        size_t* compressedColorImageSize,
        void** compressedAlphaImage,
        size_t* compressedAlphaImageSize)
    {
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
            aomFormat = AOM_IMG_FMT_I444;
            break;
        default:
            return EncoderStatus::UnknownYUVFormat;
        }

        AvifNative::unique_aom_image color(ConvertColorToAOMImage(image, colorInfo, yuvFormat, aomFormat));
        if (!color)
        {
            return EncoderStatus::OutOfMemory;
        }

        AvifNative::unique_aom_image alpha;
        if (includeTransparency)
        {
            alpha.reset(ConvertAlphaToAOMImage(image));
            if (!alpha)
            {
                return EncoderStatus::OutOfMemory;
            }
        }

        return CompressAOMImages(
            color.get(),
            alpha.get(),
            encodeOptions,
            progressContext,
            compressedColorImage,
            compressedColorImageSize,
            compressedAlphaImage,
            compressedAlphaImageSize);
    }
}

DecoderStatus __stdcall DecompressColorImage(
    const uint8_t* compressedColorImage,
    size_t compressedColorImageSize,
    const ColorConversionInfo* colorInfo,
    DecodeInfo* decodeInfo,
    BitmapData* outputImage)
{
    return DecodeColorImage(
        compressedColorImage,
        compressedColorImageSize,
        colorInfo,
        decodeInfo,
        outputImage);
}

DecoderStatus __stdcall DecompressAlphaImage(
    const uint8_t* compressedAlphaImage,
    size_t compressedAlphaImageSize,
    DecodeInfo* decodeInfo,
    BitmapData* outputImage)
{
    return DecodeAlphaImage(
        compressedAlphaImage,
        compressedAlphaImageSize,
        decodeInfo,
        outputImage);
}

EncoderStatus __stdcall CompressImage(
    const BitmapData* image,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    const ColorConversionInfo* colorInfo,
    void** compressedColorImage,
    size_t* compressedColorImageSize,
    void** compressedAlphaImage,
    size_t* compressedAlphaImageSize)
{
    if (!image || !encodeOptions || !progressContext || !compressedColorImage || !compressedColorImageSize)
    {
        return EncoderStatus::NullParameter;
    }

    if (!progressContext->progressCallback(++progressContext->progressDone, progressContext->progressTotal))
    {
        return EncoderStatus::UserCancelled;
    }

    const bool includeTransparency = compressedAlphaImage && compressedAlphaImageSize;

    return CompressWithAOM(
        image,
        includeTransparency,
        encodeOptions,
        progressContext,
        colorInfo,
        compressedColorImage,
        compressedColorImageSize,
        compressedAlphaImage,
        compressedAlphaImageSize);
}

bool __stdcall FreeImageData(void* imageData)
{
    if (imageData)
    {
        AvifMemory::Free(imageData);
    }

    return true;
}
