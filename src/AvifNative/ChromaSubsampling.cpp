
////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
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

#include <stdint.h>
#include <math.h>
#include "ChromaSubsampling.h"
#include "Memory.h"
#include "YUVConversionHelpers.h"
#include <array>

namespace
{
    struct ColorRgb24Float
    {
        float r;
        float g;
        float b;
    };

    struct YUVBlock
    {
        float y;
        float u;
        float v;
    };

    enum class YuvChannel
    {
        Y,
        U,
        V
    };

    float avifRoundf(float v)
    {
        return floorf(v + 0.5f);
    }

    uint8_t yuvToUNorm(YuvChannel chan, float v)
    {
        if (chan != YuvChannel::Y)
        {
            v += 0.5f;
        }

        if (v < 0.0f)
        {
            v = 0.0f;
        }
        else if (v > 1.0f)
        {
            v = 1.0f;
        }

        return static_cast<uint8_t>(avifRoundf(v * 255.0f));
    }

    uint32_t GetUVHeight(uint32_t imageHeight, aom_img_fmt_t aomFormat)
    {
        switch (aomFormat)
        {
        case AOM_IMG_FMT_I420:
        case AOM_IMG_FMT_AOMI420:
        case AOM_IMG_FMT_I42016:
        case AOM_IMG_FMT_YV12:
        case AOM_IMG_FMT_AOMYV12:
        case AOM_IMG_FMT_YV1216:
            return imageHeight / 2;
        case AOM_IMG_FMT_I422:
        case AOM_IMG_FMT_I42216:
        case AOM_IMG_FMT_I444:
        case AOM_IMG_FMT_I44416:
            return imageHeight;
        case AOM_IMG_FMT_NONE:
        default:
            return 0;
        }
    }

    constexpr std::array<float, 256> BuildUint8ToFloatLookupTable()
    {
        std::array<float, 256> table = {};

        for (size_t i = 0; i < table.size(); ++i)
        {
            table[i] = static_cast<float>(i) / 255.0f;
        }

        return table;
    }

    void ColorToIdentity8(
        const BitmapData* bgraImage,
        uint8_t* yPlane,
        size_t yPlaneStride,
        uint8_t* uPlane,
        size_t uPlaneStride,
        uint8_t* vPlane,
        size_t vPlaneStride)
    {
        for (size_t y = 0; y < bgraImage->height; ++y)
        {
            const ColorBgra* src = reinterpret_cast<const ColorBgra*>(bgraImage->scan0 + (y * bgraImage->stride));
            uint8_t* dstY = &yPlane[y * yPlaneStride];
            uint8_t* dstU = &uPlane[y * uPlaneStride];
            uint8_t* dstV = &vPlane[y * vPlaneStride];

            for (size_t x = 0; x < bgraImage->width; ++x)
            {
                // RGB -> Identity GBR conversion
                // Formulas 41-43 from https://www.itu.int/rec/T-REC-H.273-201612-I/en

                *dstY = src->g;
                *dstU = src->b;
                *dstV = src->r;

                ++src;
                ++dstY;
                ++dstU;
                ++dstV;
            }
        }
    }

    void ColorToYUV8(
        const BitmapData* bgraImage,
        const CICPColorData& colorInfo,
        YUVChromaSubsampling yuvFormat,
        uint8_t* yPlane,
        size_t yPlaneStride,
        uint8_t* uPlane,
        size_t uPlaneStride,
        uint8_t* vPlane,
        size_t vPlaneStride)
    {
        YUVCoefficiants yuvCoefficiants;
        GetYUVCoefficiants(colorInfo, yuvCoefficiants);

        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;

        YUVBlock yuvBlock[2][2];
        ColorRgb24Float rgbPixel;

        static constexpr std::array<float, 256> uint8ToFloatTable = BuildUint8ToFloatLookupTable();

        for (size_t imageY = 0; imageY < bgraImage->height; imageY += 2)
        {
            const size_t blockHeight = (imageY + 1) < bgraImage->height ? 2 : 1;

            for (size_t imageX = 0; imageX < bgraImage->width; imageX += 2)
            {
                const size_t blockWidth = (imageX + 1) < bgraImage->width ? 2 : 1;

                // Convert an entire 2x2 block to YUV, and populate any fully sampled channels as we go
                for (size_t blockY = 0; blockY < blockHeight; ++blockY)
                {
                    for (size_t blockX = 0; blockX < blockWidth; ++blockX)
                    {
                        size_t x = imageX + blockX;
                        size_t y = imageY + blockY;

                        // Unpack RGB into normalized float

                        const ColorBgra* pixel = reinterpret_cast<const ColorBgra*>(bgraImage->scan0 + (y * bgraImage->stride) + (x * sizeof(ColorBgra)));

                        rgbPixel.r = uint8ToFloatTable[pixel->r];
                        rgbPixel.g = uint8ToFloatTable[pixel->g];
                        rgbPixel.b = uint8ToFloatTable[pixel->b];

                        // RGB -> YUV conversion
                        float Y = (kr * rgbPixel.r) + (kg * rgbPixel.g) + (kb * rgbPixel.b);
                        yuvBlock[blockX][blockY].y = Y;
                        yuvBlock[blockX][blockY].u = (rgbPixel.b - Y) / (2 * (1 - kb));
                        yuvBlock[blockX][blockY].v = (rgbPixel.r - Y) / (2 * (1 - kr));

                        yPlane[x + (y * yPlaneStride)] = yuvToUNorm(YuvChannel::Y, yuvBlock[blockX][blockY].y);

                        if (yuvFormat == YUVChromaSubsampling::Subsampling444)
                        {
                            // YUV444, full chroma
                            uPlane[x + (y * uPlaneStride)] = yuvToUNorm(YuvChannel::U, yuvBlock[blockX][blockY].u);
                            vPlane[x + (y * vPlaneStride)] = yuvToUNorm(YuvChannel::V, yuvBlock[blockX][blockY].v);
                        }
                    }
                }

                // Populate any subsampled channels with averages from the 2x2 block
                if (yuvFormat == YUVChromaSubsampling::Subsampling420)
                {
                    // YUV420, average 4 samples (2x2)

                    float sumU = 0.0f;
                    float sumV = 0.0f;
                    for (size_t bJ = 0; bJ < blockHeight; ++bJ)
                    {
                        for (size_t bI = 0; bI < blockWidth; ++bI)
                        {
                            sumU += yuvBlock[bI][bJ].u;
                            sumV += yuvBlock[bI][bJ].v;
                        }
                    }
                    float totalSamples = static_cast<float>(blockWidth * blockHeight);
                    float avgU = sumU / totalSamples;
                    float avgV = sumV / totalSamples;

                    size_t x = imageX >> 1;
                    size_t y = imageY >> 1;

                    uPlane[x + (y * uPlaneStride)] = yuvToUNorm(YuvChannel::U, avgU);
                    vPlane[x + (y * vPlaneStride)] = yuvToUNorm(YuvChannel::V, avgV);
                }
                else if (yuvFormat == YUVChromaSubsampling::Subsampling422)
                {
                    // YUV422, average 2 samples (1x2), twice

                    for (size_t blockY = 0; blockY < blockHeight; ++blockY) {
                        float sumU = 0.0f;
                        float sumV = 0.0f;
                        for (size_t blockX = 0; blockX < blockWidth; ++blockX) {
                            sumU += yuvBlock[blockX][blockY].u;
                            sumV += yuvBlock[blockX][blockY].v;
                        }
                        float totalSamples = static_cast<float>(blockWidth);
                        float avgU = sumU / totalSamples;
                        float avgV = sumV / totalSamples;

                        size_t x = imageX >> 1;
                        size_t y = imageY + blockY;

                        uPlane[x + (y * uPlaneStride)] = yuvToUNorm(YuvChannel::U, avgU);
                        vPlane[x + (y * vPlaneStride)] = yuvToUNorm(YuvChannel::V, avgV);
                    }
                }
            }
        }
    }

    void MonoToY8(
        const BitmapData* bgraImage,
        uint8_t* yPlane,
        size_t yPlaneStride)
    {
        for (uint32_t y = 0; y < bgraImage->height; ++y)
        {
            const ColorBgra* src = reinterpret_cast<const ColorBgra*>(bgraImage->scan0 + (static_cast<size_t>(y) * bgraImage->stride));
            uint8_t* dst = &yPlane[y * yPlaneStride];

            for (uint32_t x = 0; x < bgraImage->width; ++x)
            {
                *dst = src->r;

                src++;
                dst++;
            }
        }
    }

    void AlphaToY8(
        const BitmapData* bgraImage,
        uint8_t* yPlane,
        size_t yPlaneStride)
    {
        for (uint32_t y = 0; y < bgraImage->height; ++y)
        {
            const ColorBgra* src = reinterpret_cast<const ColorBgra*>(bgraImage->scan0 + (static_cast<size_t>(y) * bgraImage->stride));
            uint8_t* dst = &yPlane[y * yPlaneStride];

            for (uint32_t x = 0; x < bgraImage->width; ++x)
            {
                *dst = src->a;

                src++;
                dst++;
            }
        }
    }

    void ZeroUVPlanes(
        uint32_t uvHeight,
        uint8_t* uPlane,
        size_t uPlaneStride,
        uint8_t* vPlane,
        size_t vPlaneStride)
    {
        for (uint32_t y = 0; y < uvHeight; ++y)
        {
            // Zero out U and V
            memset(&uPlane[y * uPlaneStride], 0, uPlaneStride);
            memset(&vPlane[y * vPlaneStride], 0, vPlaneStride);
        }
    }
}


aom_image_t* ConvertColorToAOMImage(
    const BitmapData* bgraImage,
    const CICPColorData& colorInfo,
    YUVChromaSubsampling yuvFormat,
    aom_img_fmt aomFormat)
{
    aom_image_t* aomImage = aom_img_alloc(nullptr, aomFormat, bgraImage->width, bgraImage->height, 16);
    if (!aomImage)
    {
        return nullptr;
    }

    aomImage->cp = static_cast<aom_color_primaries_t>(colorInfo.colorPrimaries);
    aomImage->tc = static_cast<aom_transfer_characteristics_t>(colorInfo.transferCharacteristics);
    aomImage->mc = static_cast<aom_matrix_coefficients_t>(colorInfo.matrixCoefficients);
    aomImage->range = AOM_CR_FULL_RANGE;
    aomImage->monochrome = yuvFormat == YUVChromaSubsampling::Subsampling400;

    if (aomImage->monochrome)
    {
        MonoToY8(
            bgraImage,
            reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_Y]),
            static_cast<size_t>(aomImage->stride[AOM_PLANE_Y]));

        const uint32_t uvHeight = GetUVHeight(bgraImage->height, aomFormat);

        ZeroUVPlanes(
            uvHeight,
            reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_U]),
            static_cast<size_t>(aomImage->stride[AOM_PLANE_U]),
            reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_V]),
            static_cast<size_t>(aomImage->stride[AOM_PLANE_V]));
    }
    else
    {
        if (yuvFormat == YUVChromaSubsampling::IdentityMatrix)
        {
            // The IdentityMatrix format places the RGB values into the YUV planes
            // without any conversion.
            // This reduces the compression efficiency, but allows for fully lossless encoding.
            ColorToIdentity8(
                bgraImage,
                reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_Y]),
                static_cast<size_t>(aomImage->stride[AOM_PLANE_Y]),
                reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_U]),
                static_cast<size_t>(aomImage->stride[AOM_PLANE_U]),
                reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_V]),
                static_cast<size_t>(aomImage->stride[AOM_PLANE_V]));
        }
        else
        {
            ColorToYUV8(
                bgraImage,
                colorInfo,
                yuvFormat,
                reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_Y]),
                static_cast<size_t>(aomImage->stride[AOM_PLANE_Y]),
                reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_U]),
                static_cast<size_t>(aomImage->stride[AOM_PLANE_U]),
                reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_V]),
                static_cast<size_t>(aomImage->stride[AOM_PLANE_V]));
        }
    }

    return aomImage;
}

aom_image_t* ConvertAlphaToAOMImage(const BitmapData* bgraImage)
{
    // Chroma sub-sampling does not matter for the alpha channel
    // YUV 4:0:0 would be a better format for the alpha image than YUV 4:2:0,
    // but it appears that libaom does not currently support it.

    constexpr aom_img_fmt aomFormat = AOM_IMG_FMT_I420;

    aom_image_t* aomImage = aom_img_alloc(nullptr, aomFormat, bgraImage->width, bgraImage->height, 16);
    if (!aomImage)
    {
        return nullptr;
    }

    aomImage->cp = AOM_CICP_CP_UNSPECIFIED;
    aomImage->tc = AOM_CICP_TC_UNSPECIFIED;
    aomImage->mc = AOM_CICP_MC_UNSPECIFIED;
    aomImage->range = AOM_CR_FULL_RANGE;
    aomImage->monochrome = 1;

    AlphaToY8(
        bgraImage,
        reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_Y]),
        static_cast<size_t>(aomImage->stride[AOM_PLANE_Y]));

    const uint32_t uvHeight = GetUVHeight(bgraImage->height, aomFormat);

    ZeroUVPlanes(
        uvHeight,
        reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_U]),
        static_cast<size_t>(aomImage->stride[AOM_PLANE_U]),
        reinterpret_cast<uint8_t*>(aomImage->planes[AOM_PLANE_V]),
        static_cast<size_t>(aomImage->stride[AOM_PLANE_V]));

    return aomImage;
}
