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


#include "DecodedImageConverter.h"
#include "Memory.h"
#include "YUVConversionHelpers.h"
#include "CICPEnums.h"
#include <array>
#include <limits>
#include <memory>
#include <stdexcept>

namespace
{
    enum class YUVDecodingMode : uint32_t
    {
        YUVCoefficiants = 0,
        YCgCo,
        YCgCoRe,
        YCgCoRo,
    };

    struct YUVDecodeInfo
    {
        YUVCoefficiants yuvCoefficiants;
        YUVDecodingMode mode;
    };

    YUVDecodeInfo GetYUVDecodeInfo(const aom_image_t* image, const CICPColorData& colorInfo)
    {
        YUVDecodeInfo info{};

        if (image->fmt == AOM_IMG_FMT_I444 || image->fmt == AOM_IMG_FMT_I44416)
        {
            // The Identity matrix coefficient is handled
            // before this method is called.
            switch (colorInfo.matrixCoefficients)
            {
            case CICPMatrixCoefficients::YCgCo:
                info.mode = YUVDecodingMode::YCgCo;
                break;
            case CICPMatrixCoefficients::YCgCoRe:
                info.mode = YUVDecodingMode::YCgCoRe;
                break;
            case CICPMatrixCoefficients::YCgCoRo:
                info.mode = YUVDecodingMode::YCgCoRo;
                break;
            default:
                info.mode = YUVDecodingMode::YUVCoefficiants;
                break;
            }
        }
        else
        {
            info.mode = YUVDecodingMode::YUVCoefficiants;
        }

        if (info.mode == YUVDecodingMode::YUVCoefficiants)
        {
            GetYUVCoefficiants(colorInfo, info.yuvCoefficiants);
        }
        else
        {
            info.yuvCoefficiants.kr = 0.0f;
            info.yuvCoefficiants.kg = 0.0f;
            info.yuvCoefficiants.kb = 0.0f;
        }

        return info;
    }

    inline float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }
        else if (value > max)
        {
            return max;
        }

        return value;
    }

    inline uint32_t Min(uint32_t a, uint32_t b)
    {
        return a < b ? a : b;
    }

    void GetCopySizes(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        const BitmapData* outputImage,
        uint32_t& copyWidth,
        uint32_t& copyHeight)
    {
        copyWidth = image->d_w;
        uint32_t maxWidth = image->d_w * (tileColumnIndex + 1);
        if (maxWidth > outputImage->width)
        {
            copyWidth -= (maxWidth - outputImage->width);
        }

        copyHeight = image->d_h;
        uint32_t maxHeight = image->d_h * (tileRowIndex + 1);
        if (maxHeight > outputImage->height)
        {
            copyHeight -= (maxHeight - outputImage->height);
        }
    }

    class unknown_bit_depth_error : public std::runtime_error
    {
    public:
        unknown_bit_depth_error(const char* message) : std::runtime_error(message) {}

    private:

    };

    float avifRoundf(float v)
    {
        return floorf(v + 0.5f);
    }

    struct YUVLookupTables
    {
        std::unique_ptr<float[]> unormFloatTableY;
        std::unique_ptr<float[]> unormFloatTableUV;

        YUVLookupTables(const aom_image_t* image, bool isIdentityMatrix)
        {
            if (image->bit_depth != 8 &&
                image->bit_depth != 10 &&
                image->bit_depth != 12 &&
                image->bit_depth != 16)
            {
                throw unknown_bit_depth_error("The image has an unsupported bit depth, must be 8, 10, 12 or 16.");
            }

            const int count = 1 << static_cast<int>(image->bit_depth);
            const bool isColorImage = !image->monochrome;

            unormFloatTableY = std::make_unique<float[]>(count);
            if (isColorImage)
            {
                unormFloatTableUV = std::make_unique<float[]>(count);
            }

            const float yuvMaxChannel = static_cast<float>((1 << image->bit_depth) - 1);
            const uint32_t limitedRangeShift = image->bit_depth - 8;

            const float biasY = image->range == AOM_CR_STUDIO_RANGE ? (16 << limitedRangeShift) : 0.0f;
            const float biasUV = static_cast<float>(1 << (image->bit_depth - 1));
            const float rangeY = image->range == AOM_CR_STUDIO_RANGE ? (219 << limitedRangeShift) : yuvMaxChannel;
            const float rangeUV = image->range == AOM_CR_STUDIO_RANGE ? (224 << limitedRangeShift) : yuvMaxChannel;

            for (int i = 0; i < count; ++i)
            {
                const float unormY = static_cast<float>(i);
                const float unormUV = static_cast<float>(i);

                unormFloatTableY[i] = (unormY - biasY) / rangeY;

                if (isColorImage)
                {
                    if (isIdentityMatrix)
                    {
                        unormFloatTableUV[i] = unormFloatTableY[i];
                    }
                    else
                    {
                        unormFloatTableUV[i] = (unormUV - biasUV) / rangeUV;
                    }
                }
            }
        }
    };

    constexpr std::array<uint8_t, 256> BuildIdentity8LimitedToFullYLookupTable()
    {
        std::array<uint8_t, 256> table = {};

        constexpr float biasY = 16.0f;
        constexpr float rangeY = 224.0f;

        for (size_t i = 0; i < table.size(); ++i)
        {
            const float unormY = (static_cast<float>(i) - biasY) / rangeY;

            table[i] = static_cast<uint8_t>(unormY);
        }

        return table;
    }

    void Identity16ToRGB32Color(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        const YUVLookupTables& tables,
        BitmapData* outputImage)
    {
        uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = y >> image->y_chroma_shift;
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);
            uint16_t* ptrU = reinterpret_cast<uint16_t*>(&image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])]);
            uint16_t* ptrV = reinterpret_cast<uint16_t*>(&image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba128Float* dstPtr = reinterpret_cast<ColorRgba128Float*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba128Float)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack Identity into unorm
                uint32_t uvI = x >> image->x_chroma_shift;

                // Clamp the values to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);
                uint32_t unormU = Min(ptrU[uvI], yuvMaxChannel);
                uint32_t unormV = Min(ptrV[uvI], yuvMaxChannel);

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];
                const float Cb = tables.unormFloatTableUV[unormU];
                const float Cr = tables.unormFloatTableUV[unormV];

                float G = Y;
                float B = Cb;
                float R = Cr;

                dstPtr->r = R;
                dstPtr->g = G;
                dstPtr->b = B;
                ++dstPtr;
            }
        }
    }

    void Identity16ToRGB32Mono(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        const YUVLookupTables& tables,
        BitmapData* outputImage)
    {
        uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba128Float* dstPtr = reinterpret_cast<ColorRgba128Float*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba128Float)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Clamp the value to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];

                dstPtr->r = Y;
                dstPtr->g = Y;
                dstPtr->b = Y;
                ++dstPtr;
            }
        }
    }

    void Identity16ToRGB16Color(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        const YUVLookupTables& tables,
        BitmapData* outputImage)
    {
        uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;
        constexpr float rgbMaxChannel = std::numeric_limits<uint16_t>::max();

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = y >> image->y_chroma_shift;
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);
            uint16_t* ptrU = reinterpret_cast<uint16_t*>(&image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])]);
            uint16_t* ptrV = reinterpret_cast<uint16_t*>(&image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba64* dstPtr = reinterpret_cast<ColorRgba64*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba64)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack Identity into unorm
                uint32_t uvI = x >> image->x_chroma_shift;

                // Clamp the values to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);
                uint32_t unormU = Min(ptrU[uvI], yuvMaxChannel);
                uint32_t unormV = Min(ptrV[uvI], yuvMaxChannel);

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];
                const float Cb = tables.unormFloatTableUV[unormU];
                const float Cr = tables.unormFloatTableUV[unormV];

                float G = Y;
                float B = Cb;
                float R = Cr;
                R = Clamp(R, 0.0f, 1.0f);
                G = Clamp(G, 0.0f, 1.0f);
                B = Clamp(B, 0.0f, 1.0f);

                dstPtr->r = static_cast<uint16_t>(0.5f + (R * rgbMaxChannel));
                dstPtr->g = static_cast<uint16_t>(0.5f + (G * rgbMaxChannel));
                dstPtr->b = static_cast<uint16_t>(0.5f + (B * rgbMaxChannel));
                ++dstPtr;
            }
        }
    }

    void Identity16ToRGB16Mono(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        const YUVLookupTables& tables,
        BitmapData* outputImage)
    {
        uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;
        constexpr float rgbMaxChannel = std::numeric_limits<uint16_t>::max();

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba64* dstPtr = reinterpret_cast<ColorRgba64*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba64)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Clamp the value to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);

                // Convert unorm to float
                const float Y = Clamp(tables.unormFloatTableY[unormY], 0.0f, 1.0f);

                const uint16_t gray = static_cast<uint16_t>(0.5f + (Y * rgbMaxChannel));

                dstPtr->r = gray;
                dstPtr->g = gray;
                dstPtr->b = gray;
                ++dstPtr;
            }
        }
    }

    void Identity8ToRGB8Color(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        static constexpr std::array<uint8_t, 256> limitedToFullY = BuildIdentity8LimitedToFullYLookupTable();

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = y >> image->y_chroma_shift;
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];
            uint8_t* ptrU = &image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])];
            uint8_t* ptrV = &image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])];

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorBgra32* dstPtr = reinterpret_cast<ColorBgra32*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorBgra32)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack Identity into unorm
                uint32_t uvI = x >> image->x_chroma_shift;
                uint8_t unormY = ptrY[x];
                uint8_t unormU = ptrU[uvI];
                uint8_t unormV = ptrV[uvI];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    // The identity matrix uses the Y plane range for U and V.
                    unormY = limitedToFullY[unormY];
                    unormU = limitedToFullY[unormU];
                    unormV = limitedToFullY[unormV];
                }

                dstPtr->g = unormY;
                dstPtr->b = unormU;
                dstPtr->r = unormV;
                ++dstPtr;
            }
        }
    }

    void Identity8ToRGB8Mono(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        static constexpr std::array<uint8_t, 256> limitedToFullY = BuildIdentity8LimitedToFullYLookupTable();

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorBgra32* dstPtr = reinterpret_cast<ColorBgra32*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorBgra32)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack Identity into unorm
                uint8_t unormY = ptrY[x];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = limitedToFullY[unormY];
                }

                const uint8_t gray = unormY;

                dstPtr->r = gray;
                dstPtr->g = gray;
                dstPtr->b = gray;
                ++dstPtr;
            }
        }
    }

    void YUV16ToRGB32Color(
        const aom_image_t* image,
        const YUVDecodeInfo& yuvDecodeInfo,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const float kr = yuvDecodeInfo.yuvCoefficiants.kr;
        const float kg = yuvDecodeInfo.yuvCoefficiants.kg;
        const float kb = yuvDecodeInfo.yuvCoefficiants.kb;
        const YUVDecodingMode yuvDecodingMode = yuvDecodeInfo.mode;
        const uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = y >> image->y_chroma_shift;
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);
            uint16_t* ptrU = reinterpret_cast<uint16_t*>(&image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])]);
            uint16_t* ptrV = reinterpret_cast<uint16_t*>(&image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba128Float* dstPtr = reinterpret_cast<ColorRgba128Float*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba128Float)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t uvI = x >> image->x_chroma_shift;

                // Clamp the values to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);
                uint32_t unormU = Min(ptrU[uvI], yuvMaxChannel);
                uint32_t unormV = Min(ptrV[uvI], yuvMaxChannel);

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];
                const float Cb = tables.unormFloatTableUV[unormU];
                const float Cr = tables.unormFloatTableUV[unormV];


                float R = 0.0f;
                float G = 0.0f;
                float B = 0.0f;

                if (yuvDecodingMode == YUVDecodingMode::YCgCo)
                {
                    // YCgCo: Formulas 54, 55, 56, 57.
                    // https://www.itu.int/rec/T-REC-H.273-202407-I/en

                    const float t = Y - Cb;
                    G = Y + Cb;
                    B = t - Cr;
                    R = t + Cr;
                }
                else
                {
                    // YUV

                    R = Y + (2 * (1 - kr)) * Cr;
                    B = Y + (2 * (1 - kb)) * Cb;
                    G = Y - ((2 * ((kr * (1 - kr) * Cr) + (kb * (1 - kb) * Cb))) / kg);
                }

                dstPtr->r = R;
                dstPtr->g = G;
                dstPtr->b = B;
                ++dstPtr;
            }
        }
    }

    void YUV16ToRGB32Mono(
        const aom_image_t* image,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba128Float* dstPtr = reinterpret_cast<ColorRgba128Float*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba128Float)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Clamp the value to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);

                // Convert unorm to float
                const float Y = Clamp(tables.unormFloatTableY[unormY], 0.0f, 1.0f);

                dstPtr->r = Y;
                dstPtr->g = Y;
                dstPtr->b = Y;
                ++dstPtr;
            }
        }
    }

    void YUV16ToRGB16Color(
        const aom_image_t* image,
        const YUVDecodeInfo& yuvDecodeInfo,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const float kr = yuvDecodeInfo.yuvCoefficiants.kr;
        const float kg = yuvDecodeInfo.yuvCoefficiants.kg;
        const float kb = yuvDecodeInfo.yuvCoefficiants.kb;
        const YUVDecodingMode yuvDecodingMode = yuvDecodeInfo.mode;
        const uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;
        constexpr int32_t rgbMaxChannel = std::numeric_limits<uint16_t>::max();
        constexpr float rgbMaxChannelFloat = rgbMaxChannel;

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = y >> image->y_chroma_shift;
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);
            uint16_t* ptrU = reinterpret_cast<uint16_t*>(&image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])]);
            uint16_t* ptrV = reinterpret_cast<uint16_t*>(&image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba64* dstPtr = reinterpret_cast<ColorRgba64*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba64)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t uvI = x >> image->x_chroma_shift;

                // Clamp the values to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);
                uint32_t unormU = Min(ptrU[uvI], yuvMaxChannel);
                uint32_t unormV = Min(ptrV[uvI], yuvMaxChannel);

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];
                const float Cb = tables.unormFloatTableUV[unormU];
                const float Cr = tables.unormFloatTableUV[unormV];

                float R = 0.0f;
                float G = 0.0f;
                float B = 0.0f;

                if (yuvDecodingMode == YUVDecodingMode::YCgCo)
                {
                    // YCgCo: Formulas 54, 55, 56, 57.
                    // https://www.itu.int/rec/T-REC-H.273-202407-I/en

                    const float t = Y - Cb;
                    G = Y + Cb;
                    B = t - Cr;
                    R = t + Cr;
                }
                else
                {
                    // YUV

                    R = Y + (2 * (1 - kr)) * Cr;
                    B = Y + (2 * (1 - kb)) * Cb;
                    G = Y - ((2 * ((kr * (1 - kr) * Cr) + (kb * (1 - kb) * Cb))) / kg);
                }

                const float clampedR = Clamp(R, 0.0f, 1.0f);
                const float clampedG = Clamp(G, 0.0f, 1.0f);
                const float clampedB = Clamp(B, 0.0f, 1.0f);

                dstPtr->r = static_cast<uint16_t>(0.5f + (clampedR * rgbMaxChannelFloat));
                dstPtr->g = static_cast<uint16_t>(0.5f + (clampedG * rgbMaxChannelFloat));
                dstPtr->b = static_cast<uint16_t>(0.5f + (clampedB * rgbMaxChannelFloat));
                ++dstPtr;
            }
        }
    }

    void YUV16ToRGB16Mono(
        const aom_image_t* image,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;
        constexpr float rgbMaxChannel = std::numeric_limits<uint16_t>::max();

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba64* dstPtr = reinterpret_cast<ColorRgba64*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba64)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Clamp the value to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);

                // Convert unorm to float
                const float Y = Clamp(tables.unormFloatTableY[unormY], 0.0f, 1.0f);

                const uint16_t clampedGray = static_cast<uint16_t>(0.5f + (Y * rgbMaxChannel));

                dstPtr->r = clampedGray;
                dstPtr->g = clampedGray;
                dstPtr->b = clampedGray;
                ++dstPtr;
            }
        }
    }

    void YUV16ToRGB16ColorYCoCgR(
        const aom_image_t* image,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;
        constexpr int32_t rgbMaxChannel = std::numeric_limits<uint16_t>::max();
        constexpr float rgbMaxChannelFloat = rgbMaxChannel;

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = y >> image->y_chroma_shift;
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);
            uint16_t* ptrU = reinterpret_cast<uint16_t*>(&image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])]);
            uint16_t* ptrV = reinterpret_cast<uint16_t*>(&image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba64* dstPtr = reinterpret_cast<ColorRgba64*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba64)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t uvI = x >> image->x_chroma_shift;

                // Clamp the values to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);
                uint32_t unormU = Min(ptrU[uvI], yuvMaxChannel);
                uint32_t unormV = Min(ptrV[uvI], yuvMaxChannel);

                // Convert unorm to float
                const float Cb = tables.unormFloatTableUV[unormU];
                const float Cr = tables.unormFloatTableUV[unormV];

                float R = 0.0f;
                float G = 0.0f;
                float B = 0.0f;


                // YCgCoRe/YCgCoRo: Formulas 62,63,64,65 from
                // https://www.itu.int/rec/T-REC-H.273-202407-P

                const int YY = unormY;
                const int Cg = (int)avifRoundf(Cb * yuvMaxChannel);
                const int Co = (int)avifRoundf(Cr * yuvMaxChannel);
                const int t = YY - (Cg / 2);

                G = Clamp(static_cast<float>(t + Cg), 0, rgbMaxChannel);
                B = Clamp(static_cast<float>(t - (Co / 2)), 0, rgbMaxChannel);
                R = Clamp(B + Co, 0.0f, rgbMaxChannelFloat);

                G /= rgbMaxChannelFloat;
                B /= rgbMaxChannelFloat;
                R /= rgbMaxChannelFloat;

                const float clampedR = Clamp(R, 0.0f, 1.0f);
                const float clampedG = Clamp(G, 0.0f, 1.0f);
                const float clampedB = Clamp(B, 0.0f, 1.0f);

                dstPtr->r = static_cast<uint16_t>(0.5f + (clampedR * rgbMaxChannelFloat));
                dstPtr->g = static_cast<uint16_t>(0.5f + (clampedG * rgbMaxChannelFloat));
                dstPtr->b = static_cast<uint16_t>(0.5f + (clampedB * rgbMaxChannelFloat));
                ++dstPtr;
            }
        }
    }

    void YUV16ToRGB8ColorYCoCgR(
        const aom_image_t* image,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;
        constexpr int32_t rgbMaxChannel = std::numeric_limits<uint8_t>::max();
        constexpr float rgbMaxChannelFloat = rgbMaxChannel;

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = y >> image->y_chroma_shift;
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);
            uint16_t* ptrU = reinterpret_cast<uint16_t*>(&image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])]);
            uint16_t* ptrV = reinterpret_cast<uint16_t*>(&image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorBgra32* dstPtr = reinterpret_cast<ColorBgra32*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorBgra32)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t uvI = x >> image->x_chroma_shift;

                // Clamp the values to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);
                uint32_t unormU = Min(ptrU[uvI], yuvMaxChannel);
                uint32_t unormV = Min(ptrV[uvI], yuvMaxChannel);

                // Convert unorm to float
                const float Cb = tables.unormFloatTableUV[unormU];
                const float Cr = tables.unormFloatTableUV[unormV];

                float R = 0.0f;
                float G = 0.0f;
                float B = 0.0f;

                // YCgCoRe/YCgCoRo: Formulas 62,63,64,65 from
                // https://www.itu.int/rec/T-REC-H.273-202407-P

                const int YY = unormY;
                const int Cg = (int)avifRoundf(Cb * yuvMaxChannel);
                const int Co = (int)avifRoundf(Cr * yuvMaxChannel);
                const int t = YY - (Cg / 2);

                G = Clamp(static_cast<float>(t + Cg), 0, rgbMaxChannel);
                B = Clamp(static_cast<float>(t - (Co / 2)), 0, rgbMaxChannel);
                R = Clamp(B + Co, 0.0f, rgbMaxChannelFloat);

                G /= rgbMaxChannelFloat;
                B /= rgbMaxChannelFloat;
                R /= rgbMaxChannelFloat;

                const float clampedR = Clamp(R, 0.0f, 1.0f);
                const float clampedG = Clamp(G, 0.0f, 1.0f);
                const float clampedB = Clamp(B, 0.0f, 1.0f);

                dstPtr->r = static_cast<uint8_t>(0.5f + (clampedR * rgbMaxChannelFloat));
                dstPtr->g = static_cast<uint8_t>(0.5f + (clampedG * rgbMaxChannelFloat));
                dstPtr->b = static_cast<uint8_t>(0.5f + (clampedB * rgbMaxChannelFloat));
                ++dstPtr;
            }
        }
    }

    void YUV8ToRGB8Color(
        const aom_image_t* image,
        const YUVDecodeInfo& yuvDecodeInfo,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const float kr = yuvDecodeInfo.yuvCoefficiants.kr;
        const float kg = yuvDecodeInfo.yuvCoefficiants.kg;
        const float kb = yuvDecodeInfo.yuvCoefficiants.kb;
        const YUVDecodingMode yuvDecodingMode = yuvDecodeInfo.mode;
        constexpr float rgbMaxChannelFloat = std::numeric_limits<uint8_t>::max();

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = y >> image->y_chroma_shift;
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];
            uint8_t* ptrU = &image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])];
            uint8_t* ptrV = &image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])];

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorBgra32* dstPtr = reinterpret_cast<ColorBgra32*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorBgra32)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t uvI = x >> image->x_chroma_shift;
                uint8_t unormY = ptrY[x];
                uint8_t unormU = ptrU[uvI];
                uint8_t unormV = ptrV[uvI];

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];
                const float Cb = tables.unormFloatTableUV[unormU];
                const float Cr = tables.unormFloatTableUV[unormV];

                float R = 0.0f;
                float G = 0.0f;
                float B = 0.0f;

                if (yuvDecodingMode == YUVDecodingMode::YCgCo)
                {
                    // YCgCo: Formulas 54, 55, 56, 57.
                    // https://www.itu.int/rec/T-REC-H.273-202407-I/en

                    const float t = Y - Cb;
                    G = Y + Cb;
                    B = t - Cr;
                    R = t + Cr;
                }
                else
                {
                    // YUV

                    R = Y + (2 * (1 - kr)) * Cr;
                    B = Y + (2 * (1 - kb)) * Cb;
                    G = Y - ((2 * ((kr * (1 - kr) * Cr) + (kb * (1 - kb) * Cb))) / kg);
                }

                const float clampedR = Clamp(R, 0.0f, 1.0f);
                const float clampedG = Clamp(G, 0.0f, 1.0f);
                const float clampedB = Clamp(B, 0.0f, 1.0f);

                dstPtr->r = static_cast<uint8_t>(0.5f + (clampedR * rgbMaxChannelFloat));
                dstPtr->g = static_cast<uint8_t>(0.5f + (clampedG * rgbMaxChannelFloat));
                dstPtr->b = static_cast<uint8_t>(0.5f + (clampedB * rgbMaxChannelFloat));
                ++dstPtr;
            }
        }
    }

    void YUV8ToRGB8Mono(
        const aom_image_t* image,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        constexpr float rgbMaxChannel = std::numeric_limits<uint8_t>::max();

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorBgra32* dstPtr = reinterpret_cast<ColorBgra32*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorBgra32)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint8_t unormY = ptrY[x];

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];

                const uint8_t clampedGray = static_cast<uint8_t>(0.5f + (Y * rgbMaxChannel));

                dstPtr->r = clampedGray;
                dstPtr->g = clampedGray;
                dstPtr->b = clampedGray;
                ++dstPtr;
            }
        }
    }

    void YUV16ToAlpha32(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        const YUVLookupTables& tables,
        BitmapData* outputImage)
    {
        uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba128Float* dstPtr = reinterpret_cast<ColorRgba128Float*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba128Float)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Clamp the value to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];

                dstPtr->a = Clamp(Y, 0.0f, 1.0f);
                ++dstPtr;
            }
        }
    }

    void YUV16ToAlpha16(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        const YUVLookupTables& tables,
        BitmapData* outputImage)
    {
        uint32_t yuvMaxChannel = (1 << image->bit_depth) - 1;
        constexpr float rgbMaxChannel = std::numeric_limits<uint16_t>::max();

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorRgba64* dstPtr = reinterpret_cast<ColorRgba64*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorRgba64)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Clamp the value to the lookup table range
                uint32_t unormY = Min(ptrY[x], yuvMaxChannel);

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];

                float A = Clamp(Y, 0.0f, 1.0f);

                dstPtr->a = static_cast<uint16_t>(0.5f + (A * rgbMaxChannel));
                ++dstPtr;
            }
        }
    }

    void YUV8ToAlpha8(
        const aom_image_t* image,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        const YUVLookupTables& tables,
        BitmapData* outputImage)
    {
        constexpr float rgbMaxChannel = std::numeric_limits<uint8_t>::max();

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, tileColumnIndex, tileRowIndex, outputImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];

            const size_t destX = static_cast<size_t>(tileColumnIndex) * image->d_w;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(tileRowIndex) * image->d_h);

            ColorBgra32* dstPtr = reinterpret_cast<ColorBgra32*>(outputImage->scan0 + (destY * outputImage->stride) + (destX * sizeof(ColorBgra32)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint8_t unormY = ptrY[x];

                // Convert unorm to float
                const float Y = tables.unormFloatTableY[unormY];

                float A = Clamp(Y, 0.0f, 1.0f);

                dstPtr->a = static_cast<uint8_t>(0.5f + (A * rgbMaxChannel));
                ++dstPtr;
            }
        }
    }
}

DecoderStatus ConvertColorImage(
    const aom_image_t* frame,
    const CICPColorData* colorInfo,
    uint32_t tileColumnIndex,
    uint32_t tileRowIndex,
    BitmapData* outputImage)
{
    if (!frame || !colorInfo || !outputImage)
    {
        return DecoderStatus::NullParameter;
    }

    try
    {
        if (colorInfo->matrixCoefficients == CICPMatrixCoefficients::Identity)
        {
            // The Identity matrix coefficient contains RGB color values.

            if (outputImage->format == BitmapDataPixelFormat::Bgra32)
            {
                if (frame->monochrome)
                {
                    Identity8ToRGB8Mono(frame, tileColumnIndex, tileRowIndex, outputImage);
                }
                else
                {
                    Identity8ToRGB8Color(frame, tileColumnIndex, tileRowIndex, outputImage);
                }
            }
            else
            {
                std::unique_ptr<YUVLookupTables> lookupTable = std::make_unique<YUVLookupTables>(frame, true);

                if (outputImage->format == BitmapDataPixelFormat::Rgba64)
                {
                    if (frame->monochrome)
                    {
                        Identity16ToRGB16Mono(frame, tileColumnIndex, tileRowIndex, *lookupTable, outputImage);
                    }
                    else
                    {
                        Identity16ToRGB16Color(frame, tileColumnIndex, tileRowIndex, *lookupTable, outputImage);
                    }
                }
                else if (outputImage->format == BitmapDataPixelFormat::Rgba128Float)
                {
                    if (frame->monochrome)
                    {
                        Identity16ToRGB32Mono(frame, tileColumnIndex, tileRowIndex, *lookupTable, outputImage);
                    }
                    else
                    {
                        Identity16ToRGB32Color(frame, tileColumnIndex, tileRowIndex, *lookupTable, outputImage);
                    }
                }
                else
                {
                    return DecoderStatus::UnsupportedOutputPixelFormat;
                }
            }
        }
        else
        {
            std::unique_ptr<YUVLookupTables> lookupTable = std::make_unique<YUVLookupTables>(frame, false);

            const YUVDecodeInfo yuvDecodeInfo = GetYUVDecodeInfo(frame, *colorInfo);

            if (outputImage->format == BitmapDataPixelFormat::Bgra32)
            {
                if (frame->monochrome)
                {
                    YUV8ToRGB8Mono(frame, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
                else if (colorInfo->matrixCoefficients == CICPMatrixCoefficients::YCgCoRe
                     || colorInfo->matrixCoefficients == CICPMatrixCoefficients::YCgCoRo)
                {
                    YUV16ToRGB8ColorYCoCgR(frame, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
                else
                {
                    YUV8ToRGB8Color(frame, yuvDecodeInfo, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
            }
            else if (outputImage->format == BitmapDataPixelFormat::Rgba64)
            {
                if (frame->monochrome)
                {
                    YUV16ToRGB16Mono(frame, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
                else if (colorInfo->matrixCoefficients == CICPMatrixCoefficients::YCgCoRe
                    || colorInfo->matrixCoefficients == CICPMatrixCoefficients::YCgCoRo)
                {
                    YUV16ToRGB16ColorYCoCgR(frame, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
                else
                {
                    YUV16ToRGB16Color(frame, yuvDecodeInfo, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
            }
            else if (outputImage->format == BitmapDataPixelFormat::Rgba128Float)
            {
                if (frame->monochrome)
                {
                    YUV16ToRGB32Mono(frame, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
                else
                {
                    YUV16ToRGB32Color(frame, yuvDecodeInfo, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
            }
            else
            {
                return DecoderStatus::UnsupportedOutputPixelFormat;
            }
        }
    }
    catch (const std::bad_alloc&)
    {
        return DecoderStatus::OutOfMemory;
    }
    catch (const unknown_bit_depth_error&)
    {
        // The YUVLookupTables constructor throws this for unsupported image bit depths.
        return DecoderStatus::UnsupportedBitDepth;
    }

    return DecoderStatus::Ok;
}

DecoderStatus ConvertAlphaImage(
    const aom_image_t* frame,
    uint32_t tileColumnIndex,
    uint32_t tileRowIndex,
    BitmapData* outputImage)
{
    if (!frame || !outputImage)
    {
        return DecoderStatus::NullParameter;
    }

    try
    {
        std::unique_ptr<YUVLookupTables> lookupTable = std::make_unique<YUVLookupTables>(frame, false);

        if (outputImage->format == BitmapDataPixelFormat::Bgra32)
        {
            YUV8ToAlpha8(frame, tileColumnIndex, tileRowIndex, *lookupTable, outputImage);
        }
        else if (outputImage->format == BitmapDataPixelFormat::Rgba64)
        {
            YUV16ToAlpha16(frame, tileColumnIndex, tileRowIndex, *lookupTable, outputImage);
        }
        else if (outputImage->format == BitmapDataPixelFormat::Rgba128Float)
        {
            YUV16ToAlpha32(frame, tileColumnIndex, tileRowIndex, *lookupTable, outputImage);
        }
        else
        {
            return DecoderStatus::UnsupportedOutputPixelFormat;
        }
    }
    catch (const std::bad_alloc&)
    {
        return DecoderStatus::OutOfMemory;
    }
    catch (const unknown_bit_depth_error&)
    {
        // The YUVLookupTables constructor throws this for unsupported image bit depths.
        return DecoderStatus::UnsupportedBitDepth;
    }

    return DecoderStatus::Ok;
}
