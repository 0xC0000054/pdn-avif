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
#include "ScopedAOMCodec.h"
#include "aom/aomcx.h"
#include "aom/aom_encoder.h"
#include <array>

namespace
{
    struct AvifEncoderOptions
    {
        enum class ImageType
        {
            Color = 0,
            Alpha
        };

        int threadCount;
        int quality;
        int cpuUsed;
        int usage;
        bool lossless;

        AvifEncoderOptions(const EncoderOptions* options, ImageType imageType)
        {
            threadCount = ClampThreadCount(options->maxThreads);
            quality = ConvertQualityToAOMRange(options, imageType);
            usage = AOM_USAGE_ALL_INTRA;
            lossless = options->lossless || imageType == ImageType::Alpha && options->losslessAlpha;

            switch (options->encoderPreset)
            {
            case EncoderPreset::Fast:
                cpuUsed = 8;
                break;
            case EncoderPreset::Slow:
            case EncoderPreset::VerySlow:
                // The slow and very slow compression speeds use the same settings.
                // The difference between them is that Slow may split the image into smaller tiles
                // before encoding and Very Slow will always encode the image as a single tile.
                cpuUsed = 0;
                break;
            case EncoderPreset::Medium:
            default:
                cpuUsed = 4;
                break;
            }
        }

    private:
        static int ClampThreadCount(int32_t maxThreads)
        {
            // AOM limits encoders to this many threads
            // See MAX_NUM_THREADS in aom_util/aom_thread.h
            constexpr int aomMaxThreadCount = 64;

            if (maxThreads < 1)
            {
                return 1;
            }
            else if (maxThreads > aomMaxThreadCount)
            {
                return aomMaxThreadCount;
            }

            return maxThreads;
        }

        static constexpr std::array<int, 101> BuildAOMQualityLookupTable()
        {
            std::array<int, 101> table = {};

            for (size_t i = 0; i < table.size(); ++i)
            {
                // Map our quality settings to the range used by AOM
                //
                // We use a quality value range where 0 is the lowest and 100 is the highest
                // AOM uses a quality value range where 63 is the lowest and 0 is the highest
                double value = (static_cast<double>(i) * 63.0) / 100.0;

                table[i] = 63 - static_cast<int>(value + 0.5);
            }

            return table;
        }

        static int ConvertQualityToAOMRange(const EncoderOptions* options, ImageType imageType)
        {
            int32_t quality = options->quality;

            if (options->lossless || imageType == ImageType::Alpha && options->losslessAlpha)
            {
                quality = 100;
            }
            else
            {
                // Clamp the quality value to the lookup table range
                if (quality < 0)
                {
                    quality = 0;
                }
                else if (quality > 100)
                {
                    quality = 100;
                }
            }

            static constexpr std::array<int, 101> qualityTable = BuildAOMQualityLookupTable();

            return qualityTable[quality];
        }
    };

    class ScopedAOMEncoder : public ScopedAOMCodec
    {
    public:
        ScopedAOMEncoder(aom_codec_iface_t* iface, const aom_codec_enc_cfg* cfg) : ScopedAOMCodec()
        {
            throw_on_error(aom_codec_enc_init(&codec, iface, cfg, 0));
            initialized = true;
        }

        void ConfigureEncoderOptions(
            const aom_codec_enc_cfg* cfg,
            const AvifEncoderOptions& encodeOptions,
            const aom_image_t* frame)
        {
            if (!initialized)
            {
                throw codec_init_error("ConfigureEncoderOptions called on an invalid object.");
            }

            throw_on_error(aom_codec_control(&codec, AOME_SET_CPUUSED, encodeOptions.cpuUsed));
            throw_on_error(aom_codec_control(&codec, AOME_SET_CQ_LEVEL, encodeOptions.quality));

            if (encodeOptions.lossless)
            {
                throw_on_error(aom_codec_control(&codec, AV1E_SET_LOSSLESS, 1));
            }
            throw_on_error(aom_codec_control(&codec, AV1E_SET_COLOR_PRIMARIES, frame->cp));
            throw_on_error(aom_codec_control(&codec, AV1E_SET_TRANSFER_CHARACTERISTICS, frame->tc));
            throw_on_error(aom_codec_control(&codec, AV1E_SET_MATRIX_COEFFICIENTS, frame->mc));
            throw_on_error(aom_codec_control(&codec, AV1E_SET_COLOR_RANGE, frame->range));
            throw_on_error(aom_codec_control(&codec, AV1E_SET_FRAME_PARALLEL_DECODING, 0));
            throw_on_error(aom_codec_control(&codec, AV1E_SET_TILE_COLUMNS, 0));
            throw_on_error(aom_codec_control(&codec, AV1E_SET_TILE_ROWS, 0));
            if (cfg->g_threads > 1)
            {
                throw_on_error(aom_codec_control(&codec, AV1E_SET_ROW_MT, 1));
            }
            else
            {
                throw_on_error(aom_codec_control(&codec, AV1E_SET_ROW_MT, 0));
            }
        }
    };

    EncoderStatus DoOnePass(
        aom_codec_iface_t* iface,
        const aom_codec_enc_cfg* cfg,
        const AvifEncoderOptions& encodeOptions,
        ProgressContext* progressContext,
        const aom_image_t* frame,
        CompressedAV1OutputAlloc outputAllocator,
        void** output)
    {
        EncoderStatus status = EncoderStatus::Ok;

        try
        {
            ScopedAOMEncoder codec(iface, cfg);
            codec.ConfigureEncoderOptions(cfg, encodeOptions, frame);

            aom_codec_err_t encodeError = aom_codec_encode(codec.get(), frame, 0, 1, 0);

            if (encodeError == AOM_CODEC_OK)
            {
                aom_codec_iter_t iter = nullptr;
                bool flushed = false;

                while (true)
                {
                    const aom_codec_cx_pkt_t* pkt = aom_codec_get_cx_data(codec.get(), &iter);

                    if (pkt == nullptr)
                    {
                        if (flushed)
                        {
                            status = EncoderStatus::EncodeFailed;
                            break;
                        }

                        encodeError = aom_codec_encode(codec.get(), nullptr, 0, 1, 0);
                        if (encodeError != AOM_CODEC_OK)
                        {
                            status = encodeError == AOM_CODEC_MEM_ERROR ? EncoderStatus::OutOfMemory : EncoderStatus::EncodeFailed;
                            break;
                        }
                        flushed = true;
                    }
                    else if (pkt->kind == AOM_CODEC_CX_FRAME_PKT)
                    {
                        if (progressContext->progressCallback(++progressContext->progressDone, progressContext->progressTotal))
                        {
                            *output = outputAllocator(pkt->data.frame.sz);
                            if (*output)
                            {
                                memcpy_s(*output, pkt->data.frame.sz, pkt->data.frame.buf, pkt->data.frame.sz);
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
                status = encodeError == AOM_CODEC_MEM_ERROR ? EncoderStatus::OutOfMemory : EncoderStatus::EncodeFailed;
            }
        }
        catch (const std::bad_alloc&)
        {
            status = EncoderStatus::OutOfMemory;
        }
        catch (const codec_init_error&)
        {
            status = EncoderStatus::CodecInitFailed;
        }

        return status;
    }

    EncoderStatus EncodeAOMImage(
        aom_codec_iface_t* iface,
        const AvifEncoderOptions& encodeOptions,
        ProgressContext* progressContext,
        const aom_image_t* frame,
        CompressedAV1OutputAlloc outputAllocator,
        void** outputImage)
    {
        aom_codec_enc_cfg_t aom_cfg;

        if (aom_codec_enc_config_default(iface, &aom_cfg, encodeOptions.usage) != AOM_CODEC_OK)
        {
            return EncoderStatus::CodecInitFailed;
        }

        aom_cfg.g_limit = 1;
        aom_cfg.g_w = frame->d_w;
        aom_cfg.g_h = frame->d_h;
        aom_cfg.g_timebase.num = 1;
        aom_cfg.g_timebase.den = 24;
        aom_cfg.rc_end_usage = AOM_Q;
        aom_cfg.g_threads = encodeOptions.threadCount;
        aom_cfg.g_usage = encodeOptions.usage;
        aom_cfg.monochrome = frame->monochrome;
        // Setting g_lag_in_frames to 0 is required when using the all intra encoding mode.
        aom_cfg.g_lag_in_frames = 0;

        if (encodeOptions.lossless)
        {
            // Set both quantizer values to 0 for lossless encoding.
            // AOM uses a quality range where 0 is the highest and 63 is the lowest.
            aom_cfg.rc_min_quantizer = aom_cfg.rc_max_quantizer = 0;
        }

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

        aom_cfg.g_pass = AOM_RC_ONE_PASS;

        EncoderStatus error = DoOnePass(iface, &aom_cfg, encodeOptions, progressContext, frame,
                                        outputAllocator, outputImage);
        return error;
    }

    EncoderStatus CompressAOMImage(
        const aom_image* image,
        AvifEncoderOptions::ImageType imageType,
        const EncoderOptions* encodeOptions,
        ProgressContext* progressContext,
        CompressedAV1OutputAlloc outputAllocator,
        void** compressedImage)
    {
        if (!outputAllocator)
        {
            return EncoderStatus::NullParameter;
        }

        if (compressedImage)
        {
            *compressedImage = nullptr;
        }
        else
        {
            return EncoderStatus::NullParameter;
        }

        AvifEncoderOptions options(encodeOptions, imageType);

        aom_codec_iface_t* iface = aom_codec_av1_cx();

        if (!progressContext->progressCallback(++progressContext->progressDone, progressContext->progressTotal))
        {
            return EncoderStatus::UserCancelled;
        }

        return EncodeAOMImage(iface, options, progressContext, image, outputAllocator, compressedImage);
    }
}

EncoderStatus CompressAOMColorImage(
    const aom_image* color,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    CompressedAV1OutputAlloc outputAllocator,
    void** compressedColorImage)
{
    return CompressAOMImage(
        color,
        AvifEncoderOptions::ImageType::Color,
        encodeOptions,
        progressContext,
        outputAllocator,
        compressedColorImage);
}

EncoderStatus CompressAOMAlphaImage(
    const aom_image* alpha,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    CompressedAV1OutputAlloc outputAllocator,
    void** compressedAlphaImage)
{
    return CompressAOMImage(
        alpha,
        AvifEncoderOptions::ImageType::Alpha,
        encodeOptions,
        progressContext,
        outputAllocator,
        compressedAlphaImage);
}
