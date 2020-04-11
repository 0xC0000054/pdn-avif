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

#include "YUVAImage.h"

YUVAImage::YUVAImage()
    : width(0), height(0), yPlane(nullptr), uPlane(nullptr), vPlane(nullptr), alphaPlane(nullptr),
    yPlaneStride(0), uPlaneStride(0), vPlaneStride(0), alphaPlaneStride(0), format(YUVFormat::Invalid)
{
}

YUVAImage::~YUVAImage()
{
    Reset();
}

EncoderStatus YUVAImage::Initialize(const BitmapData* bgraImage,
                                    bool includeTransparency,
                                    YUVChromaSubsampling yuvFormat) noexcept
{
    Reset();

    width = bgraImage->width;
    height = bgraImage->height;

    uint32_t uvWidth;
    uint32_t uvHeight;

    switch (yuvFormat)
    {
    case YUVChromaSubsampling::Subsampling420:
        uvWidth = (bgraImage->width + 1) / 2;
        uvHeight = (bgraImage->height + 1) / 2;
        format = YUVFormat::I420;
        break;
    case YUVChromaSubsampling::Subsampling422:
        uvWidth = (bgraImage->width + 1) / 2;
        uvHeight = bgraImage->height;
        format = YUVFormat::I422;
        break;
    case YUVChromaSubsampling::Subsampling444:
        uvWidth = bgraImage->width;
        uvHeight = bgraImage->height;
        format = YUVFormat::I444;
        break;
    default:
        return EncoderStatus::UnknownYUVFormat;
    }

    yPlaneStride = bgraImage->width;

    uint64_t yPlaneSize = static_cast<uint64_t>(yPlaneStride) * static_cast<uint64_t>(bgraImage->height);

    if (yPlaneSize > std::numeric_limits<size_t>::max())
    {
        Reset();
        return EncoderStatus::OutOfMemory;
    }

    yPlane = static_cast<uint8_t*>(AvifMemory::Allocate(static_cast<size_t>(yPlaneSize)));

    if (!yPlane)
    {
        Reset();
        return EncoderStatus::OutOfMemory;
    }

    size_t uvStride = uvWidth;
    uint64_t uvPlaneSize = static_cast<uint64_t>(uvStride)* static_cast<uint64_t>(uvHeight);

    if (uvPlaneSize > std::numeric_limits<size_t>::max())
    {
        Reset();
        return EncoderStatus::OutOfMemory;
    }

    uPlaneStride = uvStride;
    uPlane = static_cast<uint8_t*>(AvifMemory::Allocate(static_cast<size_t>(uvPlaneSize)));

    if (!uPlane)
    {
        Reset();
        return EncoderStatus::OutOfMemory;
    }

    vPlaneStride = uvStride;
    vPlane = static_cast<uint8_t*>(AvifMemory::Allocate(static_cast<size_t>(uvPlaneSize)));

    if (!vPlane)
    {
        Reset();
        return EncoderStatus::OutOfMemory;
    }

    if (includeTransparency)
    {
        // The size_t overflow check for bgraImage->width * bgraImage->height was already performed
        // above when allocating the Y plane.
        alphaPlaneStride = bgraImage->width;
        alphaPlane = static_cast<uint8_t*>(AvifMemory::Allocate(alphaPlaneStride * bgraImage->height));

        if (!alphaPlane)
        {
            Reset();
            return EncoderStatus::OutOfMemory;
        }

        SetAlphaPlaneFromImage(bgraImage);
    }

    return EncoderStatus::Ok;
}

void YUVAImage::Reset() noexcept
{
    if (yPlane)
    {
        AvifMemory::Free(yPlane);
        yPlane = nullptr;
    }

    if (uPlane)
    {
        AvifMemory::Free(uPlane);
        uPlane = nullptr;
    }

    if (vPlane)
    {
        AvifMemory::Free(vPlane);
        vPlane = nullptr;
    }

    if (alphaPlane)
    {
        AvifMemory::Free(alphaPlane);
        alphaPlane = nullptr;
    }

    width = 0;
    height = 0;
    yPlaneStride = 0;
    uPlaneStride = 0;
    vPlaneStride = 0;
    alphaPlaneStride = 0;
    format = YUVFormat::Invalid;
}

const uint8_t* const YUVAImage::GetYRowPointerReadOnly(size_t y) const
{
    if (yPlane && yPlaneStride)
    {
        return &yPlane[y * yPlaneStride];
    }

    return nullptr;
}

const uint8_t* const YUVAImage::GetURowPointerReadOnly(size_t y) const
{
    if (uPlane && uPlaneStride)
    {
        return &uPlane[y * uPlaneStride];
    }

    return nullptr;
}

const uint8_t* const YUVAImage::GetVRowPointerReadOnly(size_t y) const
{
    if (vPlane && vPlaneStride)
    {
        return &vPlane[y * vPlaneStride];
    }

    return nullptr;
}

const uint8_t* const YUVAImage::GetAlphaRowPointerReadOnly(size_t y) const
{
    if (alphaPlane && alphaPlaneStride)
    {
        return &alphaPlane[y * alphaPlaneStride];
    }

    return nullptr;
}

void YUVAImage::SetYPlaneValue(size_t x, size_t y, uint8_t value)
{
    if (yPlane && yPlaneStride)
    {
        yPlane[x + (y * yPlaneStride)] = value;
    }
}

void YUVAImage::SetUPlaneValue(size_t x, size_t y, uint8_t value)
{
    if (uPlane && uPlaneStride)
    {
        uPlane[x + (y * uPlaneStride)] = value;
    }
}

void YUVAImage::SetVPlaneValue(size_t x, size_t y, uint8_t value)
{
    if (vPlane && vPlaneStride)
    {
        vPlane[x + (y * vPlaneStride)] = value;
    }
}

void YUVAImage::SetAlphaPlaneFromImage(const BitmapData* bgraImage)
{
    if (alphaPlane && alphaPlaneStride && width == bgraImage->width && height == bgraImage->height)
    {
        for (size_t y = 0; y < bgraImage->height; ++y)
        {
            const ColorBgra* srcRow = reinterpret_cast<const ColorBgra*>(bgraImage->scan0 + (y * bgraImage->stride));
            uint8_t* dstRow = alphaPlane + (y * alphaPlaneStride);

            for (size_t x = 0; x < bgraImage->width; ++x)
            {
                *dstRow = srcRow->a;

                ++srcRow;
                ++dstRow;
            }
        }
    }
}
