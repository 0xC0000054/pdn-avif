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

#include "AvifNative.h"
#include "Memory.h"
#include "ChromaSubsampling.h"
#include "AV1Decoder.h"
#include "AV1Encoder.h"
#include <memory>


DecoderStatus __stdcall DecompressImage(
    const uint8_t* compressedColorImage,
    size_t compressedColorImageSize,
    const uint8_t* compressedAlphaImage,
    size_t compressedAlphaImageSize,
    const ColorConversionInfo* colorInfo,
    const DecodeInfo* decodeInfo,
    BitmapData* outputImage)
{
    return DecompressAV1Image(
        compressedColorImage,
        compressedColorImageSize,
        compressedAlphaImage,
        compressedAlphaImageSize,
        colorInfo,
        decodeInfo,
        outputImage);
}

EncoderStatus __stdcall CompressImage(
    const BitmapData* image,
    const EncoderOptions* encodeOptions,
    ProgressContext* progressContext,
    const ColorConversionInfo* colorInfo,
    void** compressedColorImage,
    size_t* compressedColorImageSize,
    void** compressedAlphaImage,
    size_t* compressedAlphaImageSize)
{
    if (!image || !encodeOptions || !progressContext || !compressedColorImage || !compressedColorImageSize ||
        image->hasTransparency && (!compressedAlphaImage || !compressedAlphaImageSize))
    {
        return EncoderStatus::NullParameter;
    }

    if (!progressContext->progressCallback(++progressContext->progressDone, progressContext->progressTotal))
    {
        return EncoderStatus::UserCancelled;
    }

    std::unique_ptr<YUVAImage> yuvaImage(new(std::nothrow) YUVAImage);
    if (!yuvaImage)
    {
        return EncoderStatus::OutOfMemory;
    }

    EncoderStatus error = ConvertBitmapDataToYUVA(image, colorInfo, encodeOptions->yuvFormat, yuvaImage.get());

    if (error == EncoderStatus::Ok)
    {
        error = CompressYUVAImage(yuvaImage.get(),
                                 encodeOptions,
                                 progressContext,
                                 compressedColorImage,
                                 compressedColorImageSize,
                                 compressedAlphaImage,
                                 compressedAlphaImageSize);
    }

    return error;
}

bool __stdcall FreeImageData(void* imageData)
{
    if (imageData)
    {
        AvifMemory::Free(imageData);
    }

    return true;
}
