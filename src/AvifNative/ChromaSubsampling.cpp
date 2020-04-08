
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

#include <stdint.h>
#include <math.h>
#include "ChromaSubsampling.h"
#include "Memory.h"
#include "YUVConversionHelpers.h"


namespace
{
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

        return  static_cast<uint8_t>(avifRoundf(v * 255.0f));
    }
}


EncoderStatus ConvertBitmapDataToYUVA(
    const BitmapData* bgraImage,
    const ColorConversionInfo* colorInfo,
    YUVChromaSubsampling yuvFormat,
    YUVAImage* yuvaImage)
{
    EncoderStatus error = yuvaImage->Initialize(bgraImage, yuvFormat);
    if (error != EncoderStatus::Ok)
    {
        return error;
    }

    YUVCoefficiants yuvCoefficiants;
    GetYUVCoefficiants(colorInfo, yuvCoefficiants);

    const float kr = yuvCoefficiants.kr;
    const float kg = yuvCoefficiants.kg;
    const float kb = yuvCoefficiants.kb;

    YUVBlock yuvBlock[2][2];
    float rgbPixel[3];

    for (size_t imageY = 0; imageY < bgraImage->height; imageY += 2)
    {
        for (size_t imageX = 0; imageX < bgraImage->width; imageX += 2)
        {
            size_t blockWidth = 2, blockHeight = 2;
            if ((imageX + 1) >= bgraImage->width)
            {
                blockWidth = 1;
            }
            if ((imageY + 1) >= bgraImage->height)
            {
                blockHeight = 1;
            }

            // Convert an entire 2x2 block to YUV, and populate any fully sampled channels as we go
            for (size_t blockY = 0; blockY < blockHeight; ++blockY)
            {
                for (size_t blockX = 0; blockX < blockWidth; ++blockX)
                {
                    size_t x = imageX + blockX;
                    size_t y = imageY + blockY;

                    // Unpack RGB into normalized float

                    const ColorBgra* pixel = reinterpret_cast<const ColorBgra*>(bgraImage->scan0 + (y * bgraImage->stride) + (x * sizeof(ColorBgra)));

                    rgbPixel[0] = static_cast<float>(pixel->r) / 255.0f;
                    rgbPixel[1] = static_cast<float>(pixel->g) / 255.0f;
                    rgbPixel[2] = static_cast<float>(pixel->b) / 255.0f;

                    // RGB -> YUV conversion
                    float Y = (kr * rgbPixel[0]) + (kg * rgbPixel[1]) + (kb * rgbPixel[2]);
                    yuvBlock[blockX][blockY].y = Y;
                    yuvBlock[blockX][blockY].u = (rgbPixel[2] - Y) / (2 * (1 - kb));
                    yuvBlock[blockX][blockY].v = (rgbPixel[0] - Y) / (2 * (1 - kr));

                    yuvaImage->SetYPlaneValue(x, y, yuvToUNorm(YuvChannel::Y, yuvBlock[blockX][blockY].y));

                    if (yuvFormat == YUVChromaSubsampling::Subsampling444)
                    {
                        // YUV444, full chroma
                        yuvaImage->SetUPlaneValue(x, y, yuvToUNorm(YuvChannel::U, yuvBlock[blockX][blockY].u));
                        yuvaImage->SetVPlaneValue(x, y, yuvToUNorm(YuvChannel::V, yuvBlock[blockX][blockY].v));
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

                yuvaImage->SetUPlaneValue(x, y, yuvToUNorm(YuvChannel::U, avgU));
                yuvaImage->SetVPlaneValue(x, y, yuvToUNorm(YuvChannel::V, avgV));

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
                    float totalSamples = (float)blockWidth;
                    float avgU = sumU / totalSamples;
                    float avgV = sumV / totalSamples;

                    size_t x = imageX >> 1;
                    size_t y = imageY + blockY;

                    yuvaImage->SetUPlaneValue(x, y, yuvToUNorm(YuvChannel::U, avgU));
                    yuvaImage->SetVPlaneValue(x, y, yuvToUNorm(YuvChannel::V, avgV));
                }
            }
        }
    }

    return EncoderStatus::Ok;
}
