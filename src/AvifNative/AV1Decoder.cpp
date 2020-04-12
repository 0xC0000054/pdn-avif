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
}

DecoderStatus DecodeColorImage(
    const uint8_t* compressedColorImage,
    size_t compressedColorImageSize,
    const ColorConversionInfo* colorInfo,
    DecodeInfo* decodeInfo,
    BitmapData* decodedImage)
{
    if (!compressedColorImage || !compressedColorImageSize || !decodedImage)
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
        // The expected width/height will be zero for the first tile in an image grid.
        if (decodeInfo->expectedWidth != 0 && aomImage->d_w != decodeInfo->expectedWidth ||
            decodeInfo->expectedHeight != 0 && aomImage->d_h != decodeInfo->expectedHeight)
        {
            status = DecoderStatus::ColorSizeMismatch;
        }
        else
        {
            status = ConvertColorImage(aomImage, colorInfo, decodeInfo, decodedImage);
        }
    }

    aom_codec_destroy(&codec);

    return status;
}

DecoderStatus DecodeAlphaImage(
    const uint8_t* compressedAlphaImage,
    size_t compressedAlphaImageSize,
    DecodeInfo* decodeInfo,
    BitmapData* outputImage)
{
    if (!compressedAlphaImage || !compressedAlphaImageSize || !outputImage)
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
        compressedAlphaImage,
        compressedAlphaImageSize,
        &aomImage);

    if (status == DecoderStatus::Ok)
    {
        // The expected width/height will be zero for the first tile in an image grid.
        if (decodeInfo->expectedWidth != 0 && aomImage->d_w != decodeInfo->expectedWidth ||
            decodeInfo->expectedHeight != 0 && aomImage->d_h != decodeInfo->expectedHeight)
        {
            status = DecoderStatus::AlphaSizeMismatch;
        }
        else
        {
            status = ConvertAlphaImage(aomImage, decodeInfo, outputImage);
        }
    }

    aom_codec_destroy(&codec);

    return status;
}
