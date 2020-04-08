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

#pragma once

#include <stdint.h>
#include "AvifNative.h"
#include "Memory.h"

enum class YUVFormat
{
    Invalid,
    I420,
    I422,
    I444
};

class YUVAImage
{
public:

    YUVAImage();

    ~YUVAImage();

    EncoderStatus Initialize(const BitmapData* bgraImage, YUVChromaSubsampling yuvFormat) noexcept;

    void Reset() noexcept;

    uint32_t GetWidth() const { return width; }

    uint32_t GetHeight() const { return height; }

    YUVFormat GetFormat() const { return format; }

    const uint8_t* const GetYRowPointerReadOnly(size_t y) const;

    const uint8_t* const GetURowPointerReadOnly(size_t y) const;

    const uint8_t* const GetVRowPointerReadOnly(size_t y) const;

    const uint8_t* const GetAlphaRowPointerReadOnly(size_t y) const;

    size_t GetYPlaneStride() const { return yPlaneStride; }

    size_t GetUPlaneStride() const { return uPlaneStride; }

    size_t GetVPlaneStride() const { return vPlaneStride; }

    size_t GetAlphaPlaneStride() const { return alphaPlaneStride; }

    bool HasAlphaChannel() const { return alphaPlane && alphaPlaneStride; }

    void SetYPlaneValue(size_t x, size_t y, uint8_t value);

    void SetUPlaneValue(size_t x, size_t y, uint8_t value);

    void SetVPlaneValue(size_t x, size_t y, uint8_t value);

private:

    void SetAlphaPlaneFromImage(const BitmapData* bgraImage);

    uint32_t width;
    uint32_t height;

    uint8_t* yPlane;
    uint8_t* uPlane;
    uint8_t* vPlane;
    uint8_t* alphaPlane;
    size_t yPlaneStride;
    size_t uPlaneStride;
    size_t vPlaneStride;
    size_t alphaPlaneStride;

    YUVFormat format;
};

