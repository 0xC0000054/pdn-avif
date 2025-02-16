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

#pragma once

#include <stdint.h>
#include "TargetVer.h"
#include "CICPEnums.h"

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

    // This must be kept in sync with EncoderPreset.cs
    enum class EncoderPreset
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
        UnsupportedBitDepth,
        UnknownYUVFormat,
    };

    // This must be kept in sync with EncoderOptions.cs and NativeEncoderOptions.cs
    struct EncoderOptions
    {
        int32_t quality;
        EncoderPreset encoderPreset;
        YUVChromaSubsampling yuvFormat;
        int32_t maxThreads;
        bool lossless;
        bool losslessAlpha;
    };

    struct CICPColorData
    {
        CICPColorPrimaries colorPrimaries;
        CICPTransferCharacteristics transferCharacteristics;
        CICPMatrixCoefficients matrixCoefficients;
        bool fullRange;
    };

    struct BitmapData
    {
        uint8_t* scan0;
        uint32_t width;
        uint32_t height;
        uint32_t stride;
    };

    struct ColorBgra32
    {
        uint8_t b;
        uint8_t g;
        uint8_t r;
        uint8_t a;
    };

    struct DecoderLayerInfo
    {
        uint16_t spatialLayerId; // Only valid if allLayers is true.
        bool allLayers;
        uint8_t operatingPoint;
    };

    typedef void* DecoderImageHandle;

    struct DecoderImageInfo
    {
        uint32_t width;
        uint32_t height;
        uint32_t bitDepth;
        YUVChromaSubsampling chromaSubsampling;
        CICPColorData cicpData;
    };

    typedef bool(__stdcall* ProgressProc)(uint32_t done, uint32_t total);

    struct ProgressContext
    {
        ProgressProc progressCallback;
        uint32_t progressDone;
        uint32_t progressTotal;
    };

    typedef void*(__stdcall* CompressedAV1OutputAlloc)(size_t sizeInBytes);

    __declspec(dllexport) DecoderStatus __stdcall DecodeImage(
        const uint8_t* compressedImage,
        size_t compressedImageSize,
        const CICPColorData* containerColorInfo,
        const DecoderLayerInfo* layerInfo,
        DecoderImageHandle** imageHandle,
        DecoderImageInfo* imageInfo);

    __declspec(dllexport) void FreeDecoderImageHandle(DecoderImageHandle* imageHandle);

    __declspec(dllexport) DecoderStatus __stdcall ReadColorImageData(
        const DecoderImageHandle* imageHandle,
        const CICPColorData* colorInfo,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage);

    __declspec(dllexport) DecoderStatus __stdcall ReadAlphaImageData(
        const DecoderImageHandle* imageHandle,
        uint32_t tileColumnIndex,
        uint32_t tileRowIndex,
        BitmapData* outputImage);

    __declspec(dllexport) EncoderStatus __stdcall CompressColorImage(
        const BitmapData* bitmap,
        const EncoderOptions* encodeOptions,
        ProgressContext* progressContext,
        const CICPColorData& colorInfo,
        CompressedAV1OutputAlloc outputAllocator,
        void** compressedColorImage);

    __declspec(dllexport) EncoderStatus __stdcall CompressAlphaImage(
        const BitmapData* bitmap,
        const EncoderOptions* encodeOptions,
        ProgressContext* progressContext,
        CompressedAV1OutputAlloc outputAllocator,
        void** compressedAlphaImage);

    // Gets a pointer to the AOM version string.
    // The memory is owned by AOM, and must not be modified or freed.
    __declspec(dllexport) const char* const __stdcall GetAOMVersionString();

#ifdef __cplusplus
}
#endif // __cplusplus
