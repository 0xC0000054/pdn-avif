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

using System;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal static class AvifNative_64
    {
        private const string DllName = "AvifNative_x64.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static unsafe extern EncoderStatus CompressImage(
            [In] ref BitmapData image,
            EncoderOptions options,
            [In, Out] ProgressContext progressContext,
            [In] ref CICPColorData colorInfo,
            out SafeAV1ImageX64 colorImage,
            out UIntPtr colorImageSize,
            out SafeAV1ImageX64 alphaImage,
            out UIntPtr alphaImageSize);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        internal static unsafe extern EncoderStatus CompressImage(
            [In] ref BitmapData image,
            EncoderOptions options,
            [In, Out] ProgressContext progressContext,
            [In] ref CICPColorData colorInfo,
            out SafeAV1ImageX64 colorImage,
            out UIntPtr colorImageSize,
            IntPtr alphaImage_MustBeZero,
            IntPtr alphaImageSize_MustBeZero);

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
        internal static extern bool FreeImageData(IntPtr handle);
    }
}
