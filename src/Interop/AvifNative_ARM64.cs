﻿////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    [System.Security.SuppressUnmanagedCodeSecurity]
    internal static class AvifNative_ARM64
    {
        private const string DllName = "AvifNative_ARM64.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern unsafe EncoderStatus CompressColorImage(
            [In] ref BitmapData image,
            EncoderOptions options,
            [In, Out] ProgressContext progressContext,
            [In] ref CICPColorData colorInfo,
            [MarshalAs(UnmanagedType.FunctionPtr)] CompressedAV1OutputAlloc outputAllocator,
            out IntPtr colorImage);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern unsafe EncoderStatus CompressAlphaImage(
            [In] ref BitmapData image,
            EncoderOptions options,
            [In, Out] ProgressContext progressContext,
            [MarshalAs(UnmanagedType.FunctionPtr)] CompressedAV1OutputAlloc outputAllocator,
            out IntPtr alphaImage);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern unsafe DecoderStatus DecompressColorImage(
            byte* compressedColorImage,
            UIntPtr compressedColorImageSize,
            [In] ref CICPColorData colorInfo,
            [In, Out] DecodeInfo decodeInfo,
            [In] ref BitmapData fullImage);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern unsafe DecoderStatus DecompressColorImage(
            byte* compressedColorImage,
            UIntPtr compressedColorImageSize,
            IntPtr colorInfo_MustBeZero,
            [In, Out] DecodeInfo decodeInfo,
            [In] ref BitmapData fullImage);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static extern unsafe DecoderStatus DecompressAlphaImage(
            byte* compressedAlphaImage,
            UIntPtr compressedAlphaImageSize,
            [In, Out] DecodeInfo decodeInfo,
            [In] ref BitmapData fullImage);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.U1)]
        internal static extern bool MemoryBlocksAreEqual(IntPtr buffer1, IntPtr buffer2, UIntPtr length);
    }
}