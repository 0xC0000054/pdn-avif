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

    #define AVIF_CLAMP(x, low, high) (((x) > (high)) ? (high) : (((x) < (low)) ? (low) : (x)))

    // Limited -> Full
    // Plan: subtract limited offset, then multiply by ratio of FULLSIZE/LIMITEDSIZE (rounding), then clamp.
    // RATIO = (FULLY - 0) / (MAXLIMITEDY - MINLIMITEDY)
    // -----------------------------------------
    // ( ( (v - MINLIMITEDY)                    | subtract limited offset
    //     * FULLY                              | multiply numerator of ratio
    //   ) + ((MAXLIMITEDY - MINLIMITEDY) / 2)  | add 0.5 (half of denominator) to round
    // ) / (MAXLIMITEDY - MINLIMITEDY)          | divide by denominator of ratio
    // AVIF_CLAMP(v, 0, FULLY)                  | clamp to full range
    // -----------------------------------------
    #define LIMITED_TO_FULL(MINLIMITEDY, MAXLIMITEDY, FULLY)                                                 \
        v = (((v - MINLIMITEDY) * FULLY) + ((MAXLIMITEDY - MINLIMITEDY) / 2)) / (MAXLIMITEDY - MINLIMITEDY); \
        v = AVIF_CLAMP(v, 0, FULLY)


    constexpr int avifLimitedToFullY(int depth, int v)
    {
        switch (depth) {
        case 8:
            LIMITED_TO_FULL(16, 235, 255);
            break;
        case 10:
            LIMITED_TO_FULL(64, 940, 1023);
            break;
        case 12:
            LIMITED_TO_FULL(256, 3760, 4095);
            break;
        case 16:
            LIMITED_TO_FULL(1024, 60160, 65535);
            break;
        default:
            throw unknown_bit_depth_error("The image has an unsupported bit depth, must be 8, 10, 12 or 16.");
        }
        return v;
    }

    int avifLimitedToFullUV(int depth, int v)
    {
        switch (depth) {
        case 8:
            LIMITED_TO_FULL(16, 240, 255);
            break;
        case 10:
            LIMITED_TO_FULL(64, 960, 1023);
            break;
        case 12:
            LIMITED_TO_FULL(256, 3840, 4095);
            break;
        case 16:
            LIMITED_TO_FULL(1024, 61440, 65535);
            break;
        default:
            throw unknown_bit_depth_error("The image has an unsupported bit depth, must be 8, 10, 12 or 16.");
        }
        return v;
    }

    #undef AVIF_CLAMP
    #undef LIMITED_TO_FULL

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

            float yuvMaxChannel = static_cast<float>((1 << image->bit_depth) - 1);

            for (int i = 0; i < count; ++i)
            {
                int unormY = i;
                int unormUV = i;

                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                    if (isColorImage)
                    {
                        unormUV = avifLimitedToFullUV(image->bit_depth, unormUV);
                    }
                }

                unormFloatTableY[i] = static_cast<float>(unormY) / yuvMaxChannel;

                if (isColorImage)
                {
                    if (isIdentityMatrix)
                    {
                        unormFloatTableUV[i] = unormFloatTableY[i];
                    }
                    else
                    {
                        unormFloatTableUV[i] = static_cast<float>(unormUV) / yuvMaxChannel - 0.5f;
                    }
                }
            }
        }
    };

    constexpr std::array<uint8_t, 256> BuildIdentity8LimitedToFullYLookupTable()
    {
        std::array<uint8_t, 256> table = {};

        for (size_t i = 0; i < table.size(); ++i)
        {
            table[i] = static_cast<uint8_t>(avifLimitedToFullY(8, static_cast<int>(i)));
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
        const YUVCoefficiants& yuvCoefficiants,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;

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

                float R = Y + (2 * (1 - kr)) * Cr;
                float B = Y + (2 * (1 - kb)) * Cb;
                float G = Y - ((2 * ((kr * (1 - kr) * Cr) + (kb * (1 - kb) * Cb))) / kg);

                dstPtr->r = R;
                dstPtr->g = G;
                dstPtr->b = B;
                ++dstPtr;
            }
        }
    }

    void YUV16ToRGB32Mono(
        const aom_image_t* image,
        const YUVCoefficiants& yuvCoefficiants,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;

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
                const float Y = Clamp(tables.unormFloatTableY[unormY], 0.0f, 1.0f);
                const float Cb = 0.0f;
                const float Cr = 0.0f;

                float R = Y + (2 * (1 - kr)) * Cr;
                float B = Y + (2 * (1 - kb)) * Cb;
                float G = Y - ((2 * ((kr * (1 - kr) * Cr) + (kb * (1 - kb) * Cb))) / kg);

                dstPtr->r = R;
                dstPtr->g = G;
                dstPtr->b = B;
                ++dstPtr;
            }
        }
    }

    void YUV16ToRGB16Color(
        const aom_image_t* image,
        const YUVCoefficiants& yuvCoefficiants,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;

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

                float R = Y + (2 * (1 - kr)) * Cr;
                float B = Y + (2 * (1 - kb)) * Cb;
                float G = Y - ((2 * ((kr * (1 - kr) * Cr) + (kb * (1 - kb) * Cb))) / kg);
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

    void YUV16ToRGB16Mono(
        const aom_image_t* image,
        const YUVCoefficiants& yuvCoefficiants,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;

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
                const float Cb = 0.0f;
                const float Cr = 0.0f;

                float R = Y + (2 * (1 - kr)) * Cr;
                float B = Y + (2 * (1 - kb)) * Cb;
                float G = Y - ((2 * ((kr * (1 - kr) * Cr) + (kb * (1 - kb) * Cb))) / kg);
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

    void YUV8ToRGB8Color(
        const aom_image_t* image,
        const YUVCoefficiants& yuvCoefficiants,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;

        constexpr float rgbMaxChannel = std::numeric_limits<uint8_t>::max();

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

                float R = Y + (2 * (1 - kr)) * Cr;
                float B = Y + (2 * (1 - kb)) * Cb;
                float G = Y - ((2 * ((kr * (1 - kr) * Cr) + (kb * (1 - kb) * Cb))) / kg);
                R = Clamp(R, 0.0f, 1.0f);
                G = Clamp(G, 0.0f, 1.0f);
                B = Clamp(B, 0.0f, 1.0f);

                dstPtr->r = static_cast<uint8_t>(0.5f + (R * rgbMaxChannel));
                dstPtr->g = static_cast<uint8_t>(0.5f + (G * rgbMaxChannel));
                dstPtr->b = static_cast<uint8_t>(0.5f + (B * rgbMaxChannel));
                ++dstPtr;
            }
        }
    }

    void YUV8ToRGB8Mono(
        const aom_image_t* image,
        const YUVCoefficiants& yuvCoefficiants,
        const YUVLookupTables& tables,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;

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
                const float Cb = 0.0f;
                const float Cr = 0.0f;

                float R = Y + (2 * (1 - kr)) * Cr;
                float B = Y + (2 * (1 - kb)) * Cb;
                float G = Y - ((2 * ((kr * (1 - kr) * Cr) + (kb * (1 - kb) * Cb))) / kg);
                R = Clamp(R, 0.0f, 1.0f);
                G = Clamp(G, 0.0f, 1.0f);
                B = Clamp(B, 0.0f, 1.0f);

                dstPtr->r = static_cast<uint8_t>(0.5f + (R * rgbMaxChannel));
                dstPtr->g = static_cast<uint8_t>(0.5f + (G * rgbMaxChannel));
                dstPtr->b = static_cast<uint8_t>(0.5f + (B * rgbMaxChannel));
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

            YUVCoefficiants yuvCoefficiants;
            GetYUVCoefficiants(*colorInfo, yuvCoefficiants);

            if (outputImage->format == BitmapDataPixelFormat::Bgra32)
            {
                if (frame->monochrome)
                {
                    YUV8ToRGB8Mono(frame, yuvCoefficiants, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
                else
                {
                    YUV8ToRGB8Color(frame, yuvCoefficiants, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
            }
            else if (outputImage->format == BitmapDataPixelFormat::Rgba64)
            {
                if (frame->monochrome)
                {
                    YUV16ToRGB16Mono(frame, yuvCoefficiants, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
                else
                {
                    YUV16ToRGB16Color(frame, yuvCoefficiants, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
            }
            else if (outputImage->format == BitmapDataPixelFormat::Rgba128Float)
            {
                if (frame->monochrome)
                {
                    YUV16ToRGB32Mono(frame, yuvCoefficiants, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
                }
                else
                {
                    YUV16ToRGB32Color(frame, yuvCoefficiants, *lookupTable, tileColumnIndex, tileRowIndex, outputImage);
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
