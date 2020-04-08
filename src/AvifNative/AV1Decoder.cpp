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
#include "AV1Decoder.h"
#include "DecodedImageConverter.h"
#include "Memory.h"
#include <aom/aom_decoder.h>
#include <aom/aomdx.h>
#include <aom/aom_image.h>


namespace
{
    DecoderStatus DecodeAV1Image(
        aom_codec_ctx_t* codec,
        const uint8_t* compressedImage,
        size_t compressedImageSize,
        aom_image_t** decodedImage)
    {
        if (aom_codec_decode(codec, compressedImage, compressedImageSize, nullptr) != AOM_CODEC_OK)
        {
            *decodedImage = nullptr;
            return DecoderStatus::DecodeFailed;
        }

        aom_codec_iter_t iter = nullptr;

        *decodedImage = aom_codec_get_frame(codec, &iter);

        if (!*decodedImage)
        {
            return DecoderStatus::DecodeFailed;
        }
        else
        {
            return DecoderStatus::Ok;
        }
    }

    void SetAlphaToOpaque(const DecodedImageInfo* info, uint8_t* imageData)
    {
        for (uint32_t y = 0; y < info->height; ++y)
        {
            ColorBgra* ptr = reinterpret_cast<ColorBgra*>(imageData + (y * info->stride));

            for (uint32_t x = 0; x < info->width; ++x)
            {
                ptr->a = 255;
                ptr++;
            }
        }
    }

    DecoderStatus DecodeAlphaImage(
        aom_codec_ctx_t* codec,
        const uint8_t* compressedAlphaImage,
        size_t compressedAlphaImageSize,
        const DecodedImageInfo* decodedImageInfo,
        void* decodedImageData)
    {
        DecoderStatus status = DecoderStatus::Ok;

        if (compressedAlphaImage && compressedAlphaImageSize)
        {
            // The image is owned by the decoder

            aom_image_t* aomImage = nullptr;

            status = DecodeAV1Image(codec,
                                    compressedAlphaImage,
                                    compressedAlphaImageSize,
                                    &aomImage);

            if (status == DecoderStatus::Ok)
            {
                if (aomImage->d_w != decodedImageInfo->width ||
                    aomImage->d_h != decodedImageInfo->height)
                {
                    status = DecoderStatus::AlphaSizeMismatch;
                }
                else
                {
                    status = ConvertAlphaImage(aomImage,
                                               decodedImageInfo,
                                               static_cast<uint8_t*>(decodedImageData));
                }
            }
        }
        else
        {
            SetAlphaToOpaque(decodedImageInfo, static_cast<uint8_t*>(decodedImageData));
        }

        return status;
    }
}

DecoderStatus DecompressAV1Image(
    const uint8_t* compressedColorImage,
    size_t compressedColorImageSize,
    const uint8_t* compressedAlphaImage,
    size_t compressedAlphaImageSize,
    const ColorConversionInfo* colorInfo,
    DecodedImageInfo* decodedImageInfo,
    void** decodedImage
    )
{
    if (!compressedColorImage || !compressedColorImageSize || !decodedImageInfo || !decodedImage)
    {
        return DecoderStatus::NullParameter;
    }

    aom_codec_ctx_t codec;

    aom_codec_iface_t* iface = aom_codec_av1_dx();

    if (aom_codec_dec_init(&codec, iface, nullptr, 0) != AOM_CODEC_OK)
    {
        return DecoderStatus::CodecInitFailed;
    }
    // The image is owned by the decoder.

    aom_image_t* aomImage = nullptr;

    DecoderStatus status = DecodeAV1Image(&codec,
                                          compressedColorImage,
                                          compressedColorImageSize,
                                          &aomImage);

    if (status == DecoderStatus::Ok)
    {
        status = ConvertColorImage(aomImage, colorInfo, decodedImageInfo, decodedImage);

        if (status == DecoderStatus::Ok)
        {
            status = DecodeAlphaImage(&codec,
                                      compressedAlphaImage,
                                      compressedAlphaImageSize,
                                      decodedImageInfo,
                                      *decodedImage);

            if (status != DecoderStatus::Ok)
            {
                AvifMemory::Free(*decodedImage);
                *decodedImage = nullptr;
            }
        }
    }

    aom_codec_destroy(&codec);

    return status;
}
