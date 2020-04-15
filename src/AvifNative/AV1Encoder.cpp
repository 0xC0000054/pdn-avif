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
//
// Portions of this file has been adapted from libavif, https://github.com/AOMediaCodec/libavif
/*
    Copyright 2019 Joe Drago. All rights reserved.

    Redistribution and use in source and binary forms, with or without
    modification, are permitted provided that the following conditions are met:

    1. Redistributions of source code must retain the above copyright notice, this
    list of conditions and the following disclaimer.

    2. Redistributions in binary form must reproduce the above copyright notice,
    this list of conditions and the following disclaimer in the documentation
    and/or other materials provided with the distribution.

    THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
    AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
    IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
    DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
    FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
    DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
    SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
    CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
    OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
    OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

#include "AV1Encoder.h"
#include "AvifNative.h"
#include "Memory.h"
#include "aom/aomcx.h"
#include "aom/aom_encoder.h"

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace
{
    struct AvifEncoderOptions
    {
        int threadCount;
        int quality;
        int cpuUsed;

        AvifEncoderOptions(int encoderThreadCount, const EncoderOptions* options)
        {
            threadCount = encoderThreadCount;
            // Map the quality value to the range used by AOM
            double value = (static_cast<double>(options->quality) * 63.0) / 100.0;
            quality = 63 - static_cast<int>(value + 0.5);

            switch (options->compressionMode)
            {
            case CompressionMode::Fast:
                cpuUsed = 8;
                break;
            case CompressionMode::Slow:
                cpuUsed = 0;
                break;
            case CompressionMode::Normal:
            default:
                cpuUsed = 4;
                break;
            }
        }
    };

    struct AvifEncoderOutput
    {
        void* buf;
        size_t sz;
    };

    int GetEncoderThreadCount()
    {
        // AOM limits encoders to this many threads
        // See MAX_NUM_THREADS in aom_util/aom_thread.h
        constexpr int aomMaxThreadCount = 64;

        int encoderThreadCount = 1;

        SYSTEM_INFO si = {};
        GetSystemInfo(&si);

        if (si.dwNumberOfProcessors < aomMaxThreadCount)
        {
            encoderThreadCount = static_cast<int>(si.dwNumberOfProcessors);
        }
        else
        {
            encoderThreadCount = aomMaxThreadCount;
        }

        return encoderThreadCount;
    }

    aom_img_fmt GetAOMChromaSubsampling(const YUVAImage* image, bool alpha)
    {
        if (alpha)
        {
            // Chroma sub-sampling does not matter for the alpha channel
            return AOM_IMG_FMT_I420;
        }
        else
        {
            switch (image->GetFormat())
            {
            case YUVFormat::I420:
                return AOM_IMG_FMT_I420;
            case YUVFormat::I422:
                return AOM_IMG_FMT_I422;
            case YUVFormat::I444:
                return AOM_IMG_FMT_I444;
            default:
                return AOM_IMG_FMT_NONE;
            }
        }
    }

    uint32_t GetUVHeight(const YUVAImage* image)
    {
        switch (image->GetFormat())
        {
        case YUVFormat::I420:
            return image->GetHeight() / 2;
        case YUVFormat::I422:
        case YUVFormat::I444:
        default:
            return image->GetHeight();
        }
    }

    void FillAOMImage(const YUVAImage* image, aom_image_t* aomImage, bool alpha)
    {
        aomImage->range = AOM_CR_FULL_RANGE;

        const uint32_t imageHeight = image->GetHeight();

        if (alpha)
        {
            aomImage->monochrome = 1;

            const size_t alphaPlaneStride = image->GetAlphaPlaneStride();

            for (uint32_t y = 0; y < imageHeight; ++y)
            {
                const uint8_t* const srcAlphaRow = image->GetAlphaRowPointerReadOnly(y);
                uint8_t* dstAlphaRow = &aomImage->planes[0][y * aomImage->stride[0]];
                memcpy_s(dstAlphaRow, alphaPlaneStride, srcAlphaRow, alphaPlaneStride);
            }

            // The alpha plane is always YUV 4:2:0
            const uint32_t uvHeight = imageHeight / 2;

            for (uint32_t y = 0; y < uvHeight; ++y)
            {
                // Zero out U and V
                memset(&aomImage->planes[1][y * aomImage->stride[1]], 0, aomImage->stride[1]);
                memset(&aomImage->planes[2][y * aomImage->stride[2]], 0, aomImage->stride[2]);
            }
        }
        else
        {
            const uint32_t uvHeight = GetUVHeight(image);

            const size_t yPlaneStride = image->GetYPlaneStride();
            const size_t uPlaneStride = image->GetUPlaneStride();
            const size_t vPlaneStride = image->GetVPlaneStride();


            // Copy the Y plane
            for (uint32_t y = 0; y < imageHeight; ++y)
            {
                const uint8_t* const srcRow = image->GetYRowPointerReadOnly(y);
                uint8_t* dstRow = &aomImage->planes[AOM_PLANE_Y][y * aomImage->stride[AOM_PLANE_Y]];
                memcpy_s(dstRow, yPlaneStride, srcRow, yPlaneStride);
            }

            // Copy the V plane
            for (uint32_t y = 0; y < uvHeight; ++y)
            {
                const uint8_t* const srcRow = image->GetURowPointerReadOnly(y);
                uint8_t* dstRow = &aomImage->planes[AOM_PLANE_U][y * aomImage->stride[AOM_PLANE_U]];
                memcpy_s(dstRow, uPlaneStride, srcRow, uPlaneStride);
            }

            // Copy the V plane
            for (uint32_t y = 0; y < uvHeight; ++y)
            {
                const uint8_t* const srcRow = image->GetVRowPointerReadOnly(y);
                uint8_t* dstRow = &aomImage->planes[AOM_PLANE_V][y * aomImage->stride[AOM_PLANE_V]];
                memcpy_s(dstRow, vPlaneStride, srcRow, vPlaneStride);
            }
        }
    }

    EncoderStatus InitializeEncoder(
        aom_codec_ctx* codec,
        aom_codec_iface_t* iface,
        const aom_codec_enc_cfg* cfg,
        const AvifEncoderOptions& encodeOptions)
    {
        const aom_codec_err_t error = aom_codec_enc_init(codec, iface, cfg, 0);
        if (error != AOM_CODEC_OK)
        {
            if (error == AOM_CODEC_MEM_ERROR)
            {
                return EncoderStatus::OutOfMemory;
            }
            else
            {
                return EncoderStatus::CodecInitFailed;
            }
        }

        aom_codec_control(codec, AOME_SET_CPUUSED, encodeOptions.cpuUsed);
        aom_codec_control(codec, AOME_SET_CQ_LEVEL, encodeOptions.quality);

        if (encodeOptions.quality == 0)
        {
            aom_codec_control(codec, AV1E_SET_LOSSLESS, 1);
        }
        aom_codec_control(codec, AV1E_SET_COLOR_RANGE, AOM_CR_FULL_RANGE);
        aom_codec_control(codec, AV1E_SET_FRAME_PARALLEL_DECODING, 0);
        aom_codec_control(codec, AV1E_SET_TILE_COLUMNS, 1);
        aom_codec_control(codec, AV1E_SET_TILE_ROWS, 1);
        if (cfg->g_threads > 1)
        {
            aom_codec_control(codec, AV1E_SET_ROW_MT, 1);
        }

        return EncoderStatus::Ok;
    }

    EncoderStatus DoOnePass(
        aom_codec_iface_t* iface,
        const aom_codec_enc_cfg* cfg,
        const AvifEncoderOptions& encodeOptions,
        ProgressContext* progressContext,
        const aom_image_t* frame,
        AvifEncoderOutput* output)
    {
        aom_codec_ctx codec;

        EncoderStatus status = InitializeEncoder(&codec, iface, cfg, encodeOptions);

        if (status == EncoderStatus::Ok)
        {
            const aom_codec_err_t encodeError = aom_codec_encode(&codec, frame, 0, 1, 0);

            if (encodeError == AOM_CODEC_OK)
            {
                aom_codec_iter_t iter = nullptr;
                bool flushed = false;

                while (true)
                {
                    const aom_codec_cx_pkt_t* pkt = aom_codec_get_cx_data(&codec, &iter);

                    if (pkt == nullptr)
                    {
                        if (flushed)
                        {
                            status = EncoderStatus::EncodeFailed;
                            break;
                        }

                        aom_codec_encode(&codec, nullptr, 0, 1, 0);
                        flushed = true;
                    }
                    else if (pkt->kind == AOM_CODEC_CX_FRAME_PKT)
                    {
                        if (progressContext->progressCallback(++progressContext->progressDone, progressContext->progressTotal))
                        {
                            output->buf = AvifMemory::Allocate(pkt->data.frame.sz);
                            if (output->buf)
                            {
                                memcpy_s(output->buf, pkt->data.frame.sz, pkt->data.frame.buf, pkt->data.frame.sz);
                                output->sz = pkt->data.frame.sz;
                            }
                            else
                            {
                                status = EncoderStatus::OutOfMemory;
                            }
                        }
                        else
                        {
                            status = EncoderStatus::UserCancelled;
                        }
                        break;
                    }
                }
            }
            else
            {
                status = EncoderStatus::EncodeFailed;
            }

            aom_codec_destroy(&codec);
        }

        return status;
    }

    EncoderStatus EncodeAOMImage(
        aom_codec_iface_t* iface,
        const AvifEncoderOptions& encodeOptions,
        ProgressContext* progressContext,
        const aom_image_t* frame,
        void** outputImage,
        size_t* outputImageSize)
    {
        aom_codec_enc_cfg_t aom_cfg;

        if (aom_codec_enc_config_default(iface, &aom_cfg, 0) != AOM_CODEC_OK)
        {
            return EncoderStatus::CodecInitFailed;
        }

        aom_cfg.g_limit = 1;
        aom_cfg.g_w = frame->d_w;
        aom_cfg.g_h = frame->d_h;
        aom_cfg.g_timebase.num = 1;
        aom_cfg.g_timebase.den = 24;
        aom_cfg.rc_end_usage = AOM_Q;
        aom_cfg.rc_min_quantizer = aom_cfg.rc_max_quantizer = encodeOptions.quality;
        aom_cfg.g_threads = encodeOptions.threadCount;
        aom_cfg.g_usage = AOM_USAGE_GOOD_QUALITY;

        // Set the profile to use based on the frame format.
        // See Annex A.2 in the AV1 Specification:
        // https://aomediacodec.github.io/av1-spec/av1-spec.pdf
        switch (frame->fmt)
        {
        case AOM_IMG_FMT_I420:
            aom_cfg.g_profile = 0;
            break;
        case AOM_IMG_FMT_I422:
            aom_cfg.g_profile = 2;
            break;
        case AOM_IMG_FMT_I444:
            aom_cfg.g_profile = 1;
            break;

        default:
            return EncoderStatus::UnknownYUVFormat;
        }

        AvifEncoderOutput output = {};
        aom_cfg.g_pass = AOM_RC_ONE_PASS;

        EncoderStatus error = DoOnePass(iface, &aom_cfg, encodeOptions, progressContext, frame, &output);

        if (error == EncoderStatus::Ok)
        {
            *outputImage = output.buf;
            *outputImageSize = output.sz;
        }

        return error;
    }

    EncoderStatus EncodeYUVAImage(
        aom_codec_iface_t* iface,
        const AvifEncoderOptions& encodeOptions,
        ProgressContext* progressContext,
        const YUVAImage* image,
        bool alpha,
        void** compressedImage,
        size_t* compressedImageSize)
    {
        const aom_img_fmt aomFormat = GetAOMChromaSubsampling(image, alpha);
        if (aomFormat == AOM_IMG_FMT_NONE)
        {
            return EncoderStatus::UnknownYUVFormat;
        }

        aom_image* frame = aom_img_alloc(nullptr, aomFormat, image->GetWidth(), image->GetHeight(), 16);
        if (!frame)
        {
            return EncoderStatus::OutOfMemory;
        }

        FillAOMImage(image, frame, alpha);

        EncoderStatus status = EncodeAOMImage(iface, encodeOptions, progressContext, frame,
                                              compressedImage, compressedImageSize);

        aom_img_free(frame);

        return status;
    }
}

EncoderStatus CompressYUVAImage(
    const YUVAImage* image,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    void** compressedColorImage,
    size_t* compressedColorImageSize,
    void** compressedAlphaImage,
    size_t* compressedAlphaImageSize)
{
    if (compressedColorImage && compressedColorImageSize)
    {
        *compressedColorImage = nullptr;
        *compressedColorImage = 0;
    }
    else
    {
        return EncoderStatus::NullParameter;
    }

    if (image->HasAlphaChannel())
    {
        if (compressedAlphaImage && compressedAlphaImageSize)
        {
            *compressedAlphaImage = nullptr;
            *compressedAlphaImageSize = 0;
        }
        else
        {
            return EncoderStatus::NullParameter;
        }
    }

    AvifEncoderOptions options(GetEncoderThreadCount(), encodeOptions);

    aom_codec_iface_t* iface = aom_codec_av1_cx();

    if (!progressContext->progressCallback(++progressContext->progressDone, progressContext->progressTotal))
    {
        return EncoderStatus::UserCancelled;
    }

    EncoderStatus status = EncodeYUVAImage(iface, options, progressContext, image,
                                           false, compressedColorImage, compressedColorImageSize);

    if (status == EncoderStatus::Ok && compressedAlphaImage && compressedAlphaImageSize)
    {
        status = EncodeYUVAImage(iface, options, progressContext, image,
                                 true, compressedAlphaImage, compressedAlphaImageSize);

        if (status == EncoderStatus::UserCancelled)
        {
            // Cleanup the color image if the user canceled the operation
            AvifMemory::Free(*compressedColorImage);
            *compressedColorImage = nullptr;
            *compressedColorImageSize = 0;
        }
    }

    return status;
}
