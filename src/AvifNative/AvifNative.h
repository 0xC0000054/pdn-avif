////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

#pragma once

#include <stdint.h>
#include "TargetVer.h"
#include "CICPEnums.h"

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

    // This must be kept in sync with CompressionSpeed.cs
    enum class CompressionSpeed
    {
        Fast,
        Medium,
        Slow,
        VerySlow
    };

    // This must be kept in sync with YUVChromaSubsampling.cs
    enum class YUVChromaSubsampling
    {
        Subsampling420,
        Subsampling422,
        Subsampling444,
        Subsampling400,
        IdentityMatrix
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
        ColorSizeMismatch,
        TileNclxProfileMismatch,
        UnsupportedBitDepth,
        UnknownYUVFormat,
        TileFormatMismatch
    };

    // This must be kept in sync with EncoderOptions.cs
    struct EncoderOptions
    {
        int32_t quality;
        CompressionSpeed compressionSpeed;
        YUVChromaSubsampling yuvFormat;
        int32_t maxThreads;
    };

    struct CICPColorData
    {
        CICPColorPrimaries colorPrimaries;
        CICPTransferCharacteristics transferCharacteristics;
        CICPMatrixCoefficients matrixCoefficients;
        bool fullRange;
    };

    struct DecodeInfo
    {
        uint32_t expectedWidth;
        uint32_t expectedHeight;
        uint32_t tileColumnIndex;
        uint32_t tileRowIndex;
        YUVChromaSubsampling chromaSubsampling;
        uint32_t bitDepth;
        CICPColorData firstTileColorData;
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

    typedef void*(__stdcall* CompressedAV1OutputAlloc)(size_t sizeInBytes);

    __declspec(dllexport) DecoderStatus __stdcall DecompressColorImage(
        const uint8_t* compressedColorImage,
        size_t compressedColorImageSize,
        const CICPColorData* colorInfo,
        DecodeInfo* decodeInfo,
        BitmapData* outputImage);

    __declspec(dllexport) DecoderStatus __stdcall DecompressAlphaImage(
        const uint8_t* compressedAlphaImage,
        size_t compressedAlphaImageSize,
        DecodeInfo* decodeInfo,
        BitmapData* outputImage);

    __declspec(dllexport) EncoderStatus __stdcall CompressImage(
        const BitmapData* bitmap,
        const EncoderOptions* encodeOptions,
        ProgressContext* progressContext,
        const CICPColorData& colorInfo,
        CompressedAV1OutputAlloc outputAllocator,
        void** compressedColorImage,
        void** compressedAlphaImage);

#ifdef __cplusplus
}
#endif // __cplusplus
