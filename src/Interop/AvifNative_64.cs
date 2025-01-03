﻿////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace AvifFileType.Interop
{
    [System.Security.SuppressUnmanagedCodeSecurity]
    internal static partial class AvifNative_64
    {
        private const string DllName = "AvifNative_x64.dll";

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial EncoderStatus CompressColorImage(
            in BitmapData image,
            in NativeEncoderOptions options,
            ref ProgressContext progressContext,
            in NativeCICPColorData colorInfo,
            [MarshalAs(UnmanagedType.FunctionPtr)] CompressedAV1OutputAlloc outputAllocator,
            out IntPtr colorImage);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial EncoderStatus CompressAlphaImage(
            in BitmapData image,
            in NativeEncoderOptions options,
            ref ProgressContext progressContext,
            [MarshalAs(UnmanagedType.FunctionPtr)] CompressedAV1OutputAlloc outputAllocator,
            out IntPtr alphaImage);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial DecoderStatus DecompressColorImage(
            byte* compressedColorImage,
            UIntPtr compressedColorImageSize,
            in NativeCICPColorData colorInfo,
            ref NativeDecodeInfo decodeInfo,
            in BitmapData fullImage);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial DecoderStatus DecompressColorImage(
            byte* compressedColorImage,
            UIntPtr compressedColorImageSize,
            IntPtr colorInfo_MustBeZero,
            ref NativeDecodeInfo decodeInfo,
            in BitmapData fullImage);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial DecoderStatus DecompressAlphaImage(
            byte* compressedAlphaImage,
            UIntPtr compressedAlphaImageSize,
            ref NativeDecodeInfo decodeInfo,
            in BitmapData fullImage);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        [return: MarshalUsing(typeof(NativeOwnedAsciiString))]
        internal static partial string GetAOMVersionString();
    }
}
