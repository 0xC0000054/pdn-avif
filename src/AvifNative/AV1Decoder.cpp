////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

#include "AvifNative.h"
#include "AV1Decoder.h"
#include "DecodedImageConverter.h"
#include "ScopedAOMCodec.h"
#include <aom/aom_decoder.h>
#include <aom/aomdx.h>
#include <aom/aom_image.h>

namespace
{
    DecoderStatus DecodeAV1Image(
        aom_codec_ctx_t* codec,
        const uint8_t* compressedImage,
        size_t compressedImageSize,
        const DecodeInfo* decodeInfo,
        aom_image_t** decodedImage)
    {
        *decodedImage = nullptr;

        const aom_codec_err_t error = aom_codec_decode(codec, compressedImage, compressedImageSize, nullptr);
        if (error != AOM_CODEC_OK)
        {
            return error == AOM_CODEC_MEM_ERROR ? DecoderStatus::OutOfMemory : DecoderStatus::DecodeFailed;
        }

        aom_codec_iter_t iter = nullptr;

        while (true)
        {
            aom_image_t* frame = aom_codec_get_frame(codec, &iter);

            if (frame != nullptr)
            {
                if (decodeInfo->allLayers)
                {
                    if (frame->spatial_id == decodeInfo->spatialLayerId)
                    {
                        *decodedImage = frame;
                        break;
                    }
                }
                else
                {
                    *decodedImage = frame;
                    break;
                }
            }
            else
            {
                break;
            }
        }

        return *decodedImage != nullptr ? DecoderStatus::Ok : DecoderStatus::DecodeFailed;
    }

    class ScopedAOMDecoder : public ScopedAOMCodec
    {
    public:
        ScopedAOMDecoder() : ScopedAOMCodec()
        {
            aom_codec_iface_t* iface = aom_codec_av1_dx();
            throw_on_error(aom_codec_dec_init(&codec, iface, nullptr, 0));
            initialized = true;
        }

        void ConfigureDecoderOptions(const DecodeInfo* info)
        {
            if (!initialized)
            {
                throw codec_init_error("ConfigureDecoderOptions called on an invalid object.");
            }

            throw_on_error(aom_codec_control(&codec, AV1D_SET_OUTPUT_ALL_LAYERS, static_cast<int>(info->allLayers)));
            throw_on_error(aom_codec_control(&codec, AV1D_SET_OPERATING_POINT, info->operatingPoint));
        }
    };
}

DecoderStatus DecodeColorImage(
    const uint8_t* compressedColorImage,
    size_t compressedColorImageSize,
    const CICPColorData* colorInfo,
    DecodeInfo* decodeInfo,
    BitmapData* decodedImage)
{
    if (!compressedColorImage || !compressedColorImageSize || !decodedImage)
    {
        return DecoderStatus::NullParameter;
    }

    DecoderStatus status = DecoderStatus::Ok;

    try
    {
        ScopedAOMDecoder codec;
        codec.ConfigureDecoderOptions(decodeInfo);

        // The image is owned by the decoder.

        aom_image_t* aomImage = nullptr;

        status = DecodeAV1Image(codec.get(),
                                compressedColorImage,
                                compressedColorImageSize,
                                decodeInfo,
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
    }
    catch (const std::bad_alloc&)
    {
        status = DecoderStatus::OutOfMemory;
    }
    catch (const codec_init_error&)
    {
        status = DecoderStatus::CodecInitFailed;
    }

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

    DecoderStatus status = DecoderStatus::Ok;

    try
    {
        ScopedAOMDecoder codec;
        codec.ConfigureDecoderOptions(decodeInfo);

        // The image is owned by the decoder.

        aom_image_t* aomImage = nullptr;

        status = DecodeAV1Image(codec.get(),
                                compressedAlphaImage,
                                compressedAlphaImageSize,
                                decodeInfo,
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
    }
    catch (const std::bad_alloc&)
    {
        status = DecoderStatus::OutOfMemory;
    }
    catch (const codec_init_error&)
    {
        status = DecoderStatus::CodecInitFailed;
    }

    return status;
}
