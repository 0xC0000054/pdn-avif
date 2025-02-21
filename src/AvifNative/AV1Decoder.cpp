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
#include "AV1Decoder.h"
#include "DecodedImageConverter.h"
#include "ScopedAOMCodec.h"
#include <aom/aom_decoder.h>
#include <aom/aomdx.h>
#include <aom/aom_image.h>
#include <memory>

namespace
{
    class ScopedAOMDecoder : public ScopedAOMCodec
    {
    public:
        ScopedAOMDecoder() : ScopedAOMCodec(), decodedImage(nullptr)
        {
            aom_codec_iface_t* iface = aom_codec_av1_dx();
            throw_on_error(aom_codec_dec_init(&codec, iface, nullptr, 0));
            initialized = true;
        }

        void ConfigureDecoderOptions(const DecoderLayerInfo* info)
        {
            if (!initialized)
            {
                throw codec_init_error("ConfigureDecoderOptions called on an invalid object.");
            }

            throw_on_error(aom_codec_control(&codec, AV1D_SET_OUTPUT_ALL_LAYERS, static_cast<int>(info->allLayers)));
            throw_on_error(aom_codec_control(&codec, AV1D_SET_OPERATING_POINT, info->operatingPoint));
        }

        DecoderStatus DecodeFrame(
            const uint8_t* compressedImage,
            size_t compressedImageSize,
            const DecoderLayerInfo* layerInfo)
        {
            const aom_codec_err_t error = aom_codec_decode(&codec, compressedImage, compressedImageSize, nullptr);
            if (error != AOM_CODEC_OK)
            {
                return error == AOM_CODEC_MEM_ERROR ? DecoderStatus::OutOfMemory : DecoderStatus::DecodeFailed;
            }

            aom_codec_iter_t iter = nullptr;

            while (true)
            {
                aom_image_t* frame = aom_codec_get_frame(&codec, &iter);

                if (frame != nullptr)
                {
                    if (layerInfo->allLayers)
                    {
                        if (frame->spatial_id == layerInfo->spatialLayerId)
                        {
                            decodedImage = frame;
                            break;
                        }
                    }
                    else
                    {
                        decodedImage = frame;
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            return decodedImage != nullptr ? DecoderStatus::Ok : DecoderStatus::DecodeFailed;
        }

        aom_image_t* GetFrame() const noexcept
        {
            return decodedImage;
        }

    private:
        aom_image_t* decodedImage;
    };

    DecoderStatus CopyFrameInfo(
        const aom_image_t* aomImage,
        const CICPColorData* containerColorInfo,
        DecoderImageInfo& info)
    {
        info.width = aomImage->d_w;
        info.height = aomImage->d_h;
        info.bitDepth = aomImage->bit_depth;

        if (aomImage->monochrome)
        {
            info.chromaSubsampling = YUVChromaSubsampling::Subsampling400;
        }
        else if (containerColorInfo && containerColorInfo->matrixCoefficients == CICPMatrixCoefficients::Identity
            || !containerColorInfo && aomImage->mc == AOM_CICP_MC_IDENTITY)
        {
            info.chromaSubsampling = YUVChromaSubsampling::IdentityMatrix;
        }
        else
        {
            switch (aomImage->fmt)
            {
            case AOM_IMG_FMT_I420:
            case AOM_IMG_FMT_AOMI420:
            case AOM_IMG_FMT_I42016:
            case AOM_IMG_FMT_YV12:
            case AOM_IMG_FMT_AOMYV12:
            case AOM_IMG_FMT_YV1216:
                info.chromaSubsampling = YUVChromaSubsampling::Subsampling420;
                break;
            case AOM_IMG_FMT_I422:
            case AOM_IMG_FMT_I42216:
                info.chromaSubsampling = YUVChromaSubsampling::Subsampling422;
                break;
            case AOM_IMG_FMT_I444:
            case AOM_IMG_FMT_I44416:
                info.chromaSubsampling = YUVChromaSubsampling::Subsampling444;
                break;
            case AOM_IMG_FMT_NONE:
            default:
                return DecoderStatus::UnknownYUVFormat;
            }
        }

        info.cicpData.colorPrimaries = static_cast<CICPColorPrimaries>(aomImage->cp);
        info.cicpData.transferCharacteristics = static_cast<CICPTransferCharacteristics>(aomImage->tc);
        info.cicpData.matrixCoefficients = static_cast<CICPMatrixCoefficients>(aomImage->mc);
        info.cicpData.fullRange = aomImage->range == aom_color_range::AOM_CR_FULL_RANGE;

        return DecoderStatus::Ok;
    }
}

DecoderStatus DecoderLoadImage(
    const uint8_t* compressedImage,
    size_t compressedImageSize,
    const CICPColorData* containerColorInfo,
    const DecoderLayerInfo* layerInfo,
    DecoderImageHandle** imageHandle,
    DecoderImageInfo* imageInfo)
{
    if (!compressedImage || !compressedImageSize || !layerInfo || !imageHandle || !imageInfo)
    {
        return DecoderStatus::NullParameter;
    }

    DecoderStatus status = DecoderStatus::Ok;

    try
    {
        std::unique_ptr<ScopedAOMDecoder> codec = std::make_unique<ScopedAOMDecoder>();
        codec->ConfigureDecoderOptions(layerInfo);

        status = codec->DecodeFrame(compressedImage, compressedImageSize, layerInfo);

        if (status == DecoderStatus::Ok)
        {
            status = CopyFrameInfo(codec->GetFrame(), containerColorInfo, *imageInfo);

            if (status == DecoderStatus::Ok)
            {
                // Transfer ownership of the decoder to the output image structure.
                *imageHandle = reinterpret_cast<DecoderImageHandle*>(codec.release());
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

DecoderStatus DecoderConvertColorImage(
    const DecoderImageHandle* imageHandle,
    const CICPColorData* colorInfo,
    uint32_t tileColumnIndex,
    uint32_t tileRowIndex,
    BitmapData* outputImage)
{
    if (!imageHandle)
    {
        return DecoderStatus::NullParameter;
    }

    const ScopedAOMDecoder* decoder = reinterpret_cast<const ScopedAOMDecoder*>(imageHandle);

    return ConvertColorImage(decoder->GetFrame(), colorInfo, tileColumnIndex, tileRowIndex, outputImage);
}

DecoderStatus DecoderConvertAlphaImage(
    const DecoderImageHandle* imageHandle,
    uint32_t tileColumnIndex,
    uint32_t tileRowIndex,
    BitmapData* outputImage)
{
    if (!imageHandle)
    {
        return DecoderStatus::NullParameter;
    }

    const ScopedAOMDecoder* decoder = reinterpret_cast<const ScopedAOMDecoder*>(imageHandle);

    return ConvertAlphaImage(decoder->GetFrame(), tileColumnIndex, tileRowIndex, outputImage);
}

void DecoderFreeImageHandle(DecoderImageHandle* handle)
{
    if (handle)
    {
        ScopedAOMDecoder* decoder = reinterpret_cast<ScopedAOMDecoder*>(handle);
        delete decoder;
    }
}
