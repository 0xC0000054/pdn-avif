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
#include "TargetVer.h"

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

    // This must be kept in sync with CompressionMode.cs
    enum class CompressionMode
    {
        Fast,
        Normal,
        Slow
    };

    // This must be kept in sync with YUVChromaSubsampling.cs
    enum class YUVChromaSubsampling
    {
        Subsampling420,
        Subsampling422,
        Subsampling444
    };

    enum class EncoderStatus
    {
        Ok,
        NullParameter,
        OutOfMemory,
        UnknownYUVFormat,
        CodecInitFailed,
        EncodeFailed,
        UserCancelled
    };

    enum class DecoderStatus
    {
        Ok,
        NullParameter,
        OutOfMemory,
        CodecInitFailed,
        DecodeFailed,
        AlphaSizeMismatch,
        ColorSizeMismatch
    };

    // This must be kept in sync with EncoderOptions.cs
    struct EncoderOptions
    {
        int32_t quality;
        CompressionMode compressionMode;
        YUVChromaSubsampling yuvFormat;
    };

    enum class ColorInformationFormat
    {
        IccProfile = 0,
        Nclx
    };

    struct NclxColorData
    {
        uint16_t colorPrimaries;
        uint16_t transferCharacteristics;
        uint16_t matrixCoefficients;
        bool fullRange;
    };

    struct ColorConversionInfo
    {
        uint8_t* iccProfile;
        size_t iccProfileSize;

        NclxColorData nclxColorData;

        ColorInformationFormat format;
    };

    struct DecodeInfo
    {
        uint32_t expectedWidth;
        uint32_t expectedHeight;
    };

    struct BitmapData
    {
        uint8_t* scan0;
        uint32_t width;
        uint32_t height;
        uint32_t stride;
    };

    struct ColorBgra
    {
        uint8_t b;
        uint8_t g;
        uint8_t r;
        uint8_t a;
    };

    typedef bool(__stdcall* ProgressProc)(uint32_t done, uint32_t total);

    struct ProgressContext
    {
        ProgressProc progressCallback;
        uint32_t progressDone;
        uint32_t progressTotal;
    };

    __declspec(dllexport) DecoderStatus __stdcall DecompressImage(
        const uint8_t* compressedColorImage,
        size_t compressedColorImageSize,
        const uint8_t* compressedAlphaImage,
        size_t compressedAlphaImageSize,
        const ColorConversionInfo* colorInfo,
        const DecodeInfo* decodeInfo,
        BitmapData* outputImage);

    __declspec(dllexport) EncoderStatus __stdcall CompressImage(
        const BitmapData* bitmap,
        const EncoderOptions* encodeOptions,
        ProgressContext* progressContext,
        const ColorConversionInfo* colorInfo,
        void** compressedColorImage,
        size_t* compressedColorImageSize,
        void** compressedAlphaImage,
        size_t* compressedAlphaImageSize);

    __declspec(dllexport) bool __stdcall FreeImageData(void* imageData);

#ifdef __cplusplus
}
#endif // __cplusplus
