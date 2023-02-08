////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
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
    internal static partial class AvifNative_ARM64
    {
        private const string DllName = "AvifNative_ARM64.dll";

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial EncoderStatus CompressColorImage(
            in BitmapData image,
            in EncoderOptions options,
            ref ProgressContext progressContext,
            in NativeCICPColorData colorInfo,
            [MarshalAs(UnmanagedType.FunctionPtr)] CompressedAV1OutputAlloc outputAllocator,
            out IntPtr colorImage);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial EncoderStatus CompressAlphaImage(
            in BitmapData image,
            in EncoderOptions options,
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
        [return: MarshalAs(UnmanagedType.U1)]
        internal static partial bool MemoryBlocksAreEqual(IntPtr buffer1, IntPtr buffer2, UIntPtr length);
    }
}
