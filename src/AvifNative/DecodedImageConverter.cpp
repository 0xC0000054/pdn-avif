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


#include "DecodedImageConverter.h"
#include "Memory.h"
#include "YUVConversionHelpers.h"
#include "NclxEnums.h"

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
        const DecodeInfo* decodeInfo,
        const BitmapData* bgraImage,
        uint32_t& copyWidth,
        uint32_t& copyHeight)
    {
        copyWidth = image->d_w;
        uint32_t maxWidth = image->d_w * (decodeInfo->tileColumnIndex + 1);
        if (maxWidth > bgraImage->width)
        {
            copyWidth -= (maxWidth - bgraImage->width);
        }

        copyHeight = image->d_h;
        uint32_t maxHeight = image->d_h * (decodeInfo->tileRowIndex + 1);
        if (maxHeight > bgraImage->height)
        {
            copyHeight -= (maxHeight - bgraImage->height);
        }
    }

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


    int avifLimitedToFullY(int depth, int v)
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
        }
        return v;
    }

    #undef AVIF_CLAMP

    void Identity16ToRGB8Color(
        const aom_image_t* image,
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        const uint32_t maxUVI = ((image->d_w + image->x_chroma_shift) >> image->x_chroma_shift) - 1;
        const uint32_t maxUVJ = ((image->d_h + image->y_chroma_shift) >> image->y_chroma_shift) - 1;

        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        constexpr float rgbMaxChannel = 255.0f;

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = Min(y >> image->y_chroma_shift, maxUVJ);
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);
            uint16_t* ptrU = reinterpret_cast<uint16_t*>(&image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])]);
            uint16_t* ptrV = reinterpret_cast<uint16_t*>(&image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])]);

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack Identity into unorm
                uint32_t uvI = Min(x >> image->x_chroma_shift, maxUVI);
                uint32_t unormY = ptrY[x];
                uint32_t unormU = ptrU[uvI];
                uint32_t unormV = ptrV[uvI];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    // The identity matrix uses the Y plane range for U and V.
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                    unormU = avifLimitedToFullY(image->bit_depth, unormU);
                    unormV = avifLimitedToFullY(image->bit_depth, unormV);
                }

                // Convert unorm to float
                const float Y = (float)unormY / yuvMaxChannel;
                const float Cb = ((float)unormU / yuvMaxChannel);
                const float Cr = ((float)unormV / yuvMaxChannel);

                float G = Y;
                float B = Cb;
                float R = Cr;
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

    void Identity16ToRGB8Mono(
        const aom_image_t* image,
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        constexpr float rgbMaxChannel = 255.0f;

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack Identity into unorm
                uint32_t unormY = ptrY[x];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                }

                // Convert unorm to float
                const float Y = Clamp(static_cast<float>(unormY) / yuvMaxChannel, 0.0f, 1.0f);

                const uint8_t gray = static_cast<uint8_t>(0.5f + (Y * rgbMaxChannel));

                dstPtr->g = gray;
                dstPtr->b = gray;
                dstPtr->r = gray;
                ++dstPtr;
            }
        }
    }

    void Identity8ToRGB8Color(
        const aom_image_t* image,
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        const uint32_t maxUVI = ((image->d_w + image->x_chroma_shift) >> image->x_chroma_shift) - 1;
        const uint32_t maxUVJ = ((image->d_h + image->y_chroma_shift) >> image->y_chroma_shift) - 1;

        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        constexpr float rgbMaxChannel = 255.0f;

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = Min(y >> image->y_chroma_shift, maxUVJ);
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];
            uint8_t* ptrU = &image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])];
            uint8_t* ptrV = &image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])];

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack Identity into unorm
                uint32_t uvI = Min(x >> image->x_chroma_shift, maxUVI);
                uint32_t unormY = ptrY[x];
                uint32_t unormU = ptrU[uvI];
                uint32_t unormV = ptrV[uvI];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    // The identity matrix uses the Y plane range for U and V.
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                    unormU = avifLimitedToFullY(image->bit_depth, unormU);
                    unormV = avifLimitedToFullY(image->bit_depth, unormV);
                }

                dstPtr->g = static_cast<uint8_t>(unormY);
                dstPtr->b = static_cast<uint8_t>(unormU);
                dstPtr->r = static_cast<uint8_t>(unormV);
                ++dstPtr;
            }
        }
    }

    void Identity8ToRGB8Mono(
        const aom_image_t* image,
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        constexpr float rgbMaxChannel = 255.0f;

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack Identity into unorm
                uint32_t unormY = ptrY[x];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                }

                const uint8_t gray = static_cast<uint8_t>(unormY);

                dstPtr->r = gray;
                dstPtr->g = gray;
                dstPtr->b = gray;
                ++dstPtr;
            }
        }
    }

    void YUV16ToRGB8Color(
        const aom_image_t* image,
        const YUVCoefficiants& yuvCoefficiants,
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;
        const uint32_t maxUVI = ((image->d_w + image->x_chroma_shift) >> image->x_chroma_shift) - 1;
        const uint32_t maxUVJ = ((image->d_h + image->y_chroma_shift) >> image->y_chroma_shift) - 1;

        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        float rgbMaxChannel = 255.0f;

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = Min(y >> image->y_chroma_shift, maxUVJ);
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);
            uint16_t* ptrU = reinterpret_cast<uint16_t*>(&image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])]);
            uint16_t* ptrV = reinterpret_cast<uint16_t*>(&image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])]);

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t uvI = Min(x >> image->x_chroma_shift, maxUVI);
                uint32_t unormY = ptrY[x];
                uint32_t unormU = ptrU[uvI];
                uint32_t unormV = ptrV[uvI];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                    unormU = avifLimitedToFullUV(image->bit_depth, unormU);
                    unormV = avifLimitedToFullUV(image->bit_depth, unormV);
                }

                // Convert unorm to float
                const float Y = (float)unormY / yuvMaxChannel;
                const float Cb = ((float)unormU / yuvMaxChannel) - 0.5f;
                const float Cr = ((float)unormV / yuvMaxChannel) - 0.5f;

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

    void YUV16ToRGB8Mono(
        const aom_image_t* image,
        const YUVCoefficiants& yuvCoefficiants,
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;

        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        float rgbMaxChannel = 255.0f;

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t unormY = ptrY[x];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                }

                // Convert unorm to float
                const float Y = (float)unormY / yuvMaxChannel;
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

    void YUV8ToRGB8Color(
        const aom_image_t* image,
        const YUVCoefficiants& yuvCoefficiants,
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;
        const uint32_t maxUVI = ((image->d_w + image->x_chroma_shift) >> image->x_chroma_shift) - 1;
        const uint32_t maxUVJ = ((image->d_h + image->y_chroma_shift) >> image->y_chroma_shift) - 1;

        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        float rgbMaxChannel = 255.0f;

        uint32_t uPlaneIndex = AOM_PLANE_U;
        uint32_t vPlaneIndex = AOM_PLANE_V;

        if (image->fmt & AOM_IMG_FMT_UV_FLIP)
        {
            uPlaneIndex = AOM_PLANE_V;
            vPlaneIndex = AOM_PLANE_U;
        }

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            const uint32_t uvJ = Min(y >> image->y_chroma_shift, maxUVJ);
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];
            uint8_t* ptrU = &image->planes[uPlaneIndex][(uvJ * image->stride[uPlaneIndex])];
            uint8_t* ptrV = &image->planes[vPlaneIndex][(uvJ * image->stride[vPlaneIndex])];

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t uvI = Min(x >> image->x_chroma_shift, maxUVI);
                uint32_t unormY = ptrY[x];
                uint32_t unormU = ptrU[uvI];
                uint32_t unormV = ptrV[uvI];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                    unormU = avifLimitedToFullUV(image->bit_depth, unormU);
                    unormV = avifLimitedToFullUV(image->bit_depth, unormV);
                }

                // Convert unorm to float
                const float Y = (float)unormY / yuvMaxChannel;
                const float Cb = ((float)unormU / yuvMaxChannel) - 0.5f;
                const float Cr = ((float)unormV / yuvMaxChannel) - 0.5f;

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
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        const float kr = yuvCoefficiants.kr;
        const float kg = yuvCoefficiants.kg;
        const float kb = yuvCoefficiants.kb;

        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        float rgbMaxChannel = 255.0f;

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t unormY = ptrY[x];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                }

                // Convert unorm to float
                const float Y = (float)unormY / yuvMaxChannel;
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

    void YUV16ToAlpha8(
        const aom_image_t* image,
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        float rgbMaxChannel = 255.0f;

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint16_t* ptrY = reinterpret_cast<uint16_t*>(&image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])]);

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t unormY = ptrY[x];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                }

                // Convert unorm to float
                const float Y = (float)unormY / yuvMaxChannel;

                float A = Clamp(Y, 0.0f, 1.0f);

                dstPtr->a = static_cast<uint8_t>(0.5f + (A * rgbMaxChannel));
                ++dstPtr;
            }
        }
    }

    void YUV8ToAlpha8(
        const aom_image_t* image,
        const DecodeInfo* decodeInfo,
        BitmapData* bgraImage)
    {
        float yuvMaxChannel = (float)((1 << image->bit_depth) - 1);
        float rgbMaxChannel = 255.0f;

        uint32_t copyWidth;
        uint32_t copyHeight;
        GetCopySizes(image, decodeInfo, bgraImage, copyWidth, copyHeight);

        for (uint32_t y = 0; y < copyHeight; ++y)
        {
            uint8_t* ptrY = &image->planes[AOM_PLANE_Y][(y * image->stride[AOM_PLANE_Y])];

            const size_t destX = static_cast<size_t>(decodeInfo->tileColumnIndex) * decodeInfo->expectedWidth;
            const size_t destY = static_cast<size_t>(y) + (static_cast<size_t>(decodeInfo->tileRowIndex) * decodeInfo->expectedHeight);

            ColorBgra* dstPtr = reinterpret_cast<ColorBgra*>(bgraImage->scan0 + (destY * bgraImage->stride) + (destX * sizeof(ColorBgra)));

            for (uint32_t x = 0; x < copyWidth; ++x)
            {
                // Unpack YUV into unorm
                uint32_t unormY = ptrY[x];

                // adjust for limited/full color range, if need be
                if (image->range == AOM_CR_STUDIO_RANGE)
                {
                    unormY = avifLimitedToFullY(image->bit_depth, unormY);
                }

                // Convert unorm to float
                const float Y = (float)unormY / yuvMaxChannel;

                float A = Clamp(Y, 0.0f, 1.0f);

                dstPtr->a = static_cast<uint8_t>(0.5f + (A * rgbMaxChannel));
                ++dstPtr;
            }
        }
    }
}

DecoderStatus ConvertColorImage(
    const aom_image_t* frame,
    const ColorConversionInfo* containerColorInfo,
    DecodeInfo* decodeInfo,
    BitmapData* outputImage)
{
    if (!frame || !outputImage)
    {
        return DecoderStatus::NullParameter;
    }

    const bool isFirstTile = decodeInfo->tileColumnIndex == 0 && decodeInfo->tileRowIndex == 0;

    if (isFirstTile && decodeInfo->expectedWidth == 0 && decodeInfo->expectedHeight == 0)
    {
        decodeInfo->expectedWidth = frame->d_w;
        decodeInfo->expectedHeight = frame->d_h;
    }

    ColorConversionInfo colorInfo = {};

    if (containerColorInfo)
    {
        colorInfo = *containerColorInfo;
    }
    else
    {
        colorInfo.format = ColorInformationFormat::Nclx;
        colorInfo.nclxColorData.colorPrimaries = static_cast<NclxColorPrimaries>(frame->cp);
        colorInfo.nclxColorData.transferCharacteristics = static_cast<NclxTransferCharacteristics>(frame->tc);
        colorInfo.nclxColorData.matrixCoefficients = static_cast<NclxMatrixCoefficients>(frame->mc);
        colorInfo.nclxColorData.fullRange = frame->range == aom_color_range::AOM_CR_FULL_RANGE;

        if (isFirstTile)
        {
            decodeInfo->firstTileNclxProfile.colorPrimaries = colorInfo.nclxColorData.colorPrimaries;
            decodeInfo->firstTileNclxProfile.transferCharacteristics = colorInfo.nclxColorData.transferCharacteristics;
            decodeInfo->firstTileNclxProfile.matrixCoefficients = colorInfo.nclxColorData.matrixCoefficients;
            decodeInfo->firstTileNclxProfile.fullRange = colorInfo.nclxColorData.fullRange;
            decodeInfo->usingFirstTileNclxProfile = true;
        }
        else
        {
            if (decodeInfo->usingFirstTileNclxProfile)
            {
                if (colorInfo.nclxColorData.colorPrimaries != colorInfo.nclxColorData.colorPrimaries ||
                    colorInfo.nclxColorData.transferCharacteristics != colorInfo.nclxColorData.transferCharacteristics ||
                    colorInfo.nclxColorData.matrixCoefficients != colorInfo.nclxColorData.matrixCoefficients ||
                    colorInfo.nclxColorData.fullRange != colorInfo.nclxColorData.fullRange)
                {
                    return DecoderStatus::TileNclxProfileMismatch;
                }
            }
        }
    }

    if (colorInfo.format == ColorInformationFormat::Nclx &&
        colorInfo.nclxColorData.matrixCoefficients == NclxMatrixCoefficients::Identity)
    {
        // The Identity matrix coefficient contains RGB color values.

        if (frame->bit_depth > 8)
        {
            if (frame->monochrome)
            {
                Identity16ToRGB8Mono(frame,
                    decodeInfo,
                    outputImage);
            }
            else
            {
                Identity16ToRGB8Color(frame,
                    decodeInfo,
                    outputImage);
            }
        }
        else
        {
            if (frame->monochrome)
            {
                Identity8ToRGB8Mono(frame,
                    decodeInfo,
                    outputImage);
            }
            else
            {
                Identity8ToRGB8Color(frame,
                    decodeInfo,
                    outputImage);
            }
        }
    }
    else
    {
        YUVCoefficiants yuvCoefficiants;
        GetYUVCoefficiants(&colorInfo, yuvCoefficiants);

        if (frame->bit_depth > 8)
        {
            if (frame->monochrome)
            {
                YUV16ToRGB8Mono(frame,
                    yuvCoefficiants,
                    decodeInfo,
                    outputImage);
            }
            else
            {
                YUV16ToRGB8Color(frame,
                    yuvCoefficiants,
                    decodeInfo,
                    outputImage);
            }
        }
        else
        {
            if (frame->monochrome)
            {
                YUV8ToRGB8Mono(frame,
                    yuvCoefficiants,
                    decodeInfo,
                    outputImage);
            }
            else
            {
                YUV8ToRGB8Color(frame,
                    yuvCoefficiants,
                    decodeInfo,
                    outputImage);
            }
        }
    }

    return DecoderStatus::Ok;
}

DecoderStatus ConvertAlphaImage(
    const aom_image_t* frame,
    DecodeInfo* decodeInfo,
    BitmapData* outputBGRAImageData)
{
    if (!frame || !outputBGRAImageData)
    {
        return DecoderStatus::NullParameter;
    }

    const bool isFirstTile = decodeInfo->tileColumnIndex == 0 && decodeInfo->tileRowIndex == 0;

    if (isFirstTile && decodeInfo->expectedWidth == 0 && decodeInfo->expectedHeight == 0)
    {
        decodeInfo->expectedWidth = frame->d_w;
        decodeInfo->expectedHeight = frame->d_h;
    }

    if (frame->bit_depth > 8)
    {
        YUV16ToAlpha8(frame,
                      decodeInfo,
                      outputBGRAImageData);
    }
    else
    {
        YUV8ToAlpha8(frame,
                     decodeInfo,
                     outputBGRAImageData);

    }

    return DecoderStatus::Ok;
}
