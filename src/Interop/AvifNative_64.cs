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
            [MarshalUsing(typeof(CICPColorDataMarshaller))] in CICPColorData colorInfo,
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
        internal static unsafe partial DecoderStatus DecodeImage(
            byte* compressedColorImage,
            UIntPtr compressedColorImageSize,
            [MarshalUsing(typeof(CICPColorDataMarshaller))] in CICPColorData colorData,
            [MarshalUsing(typeof(DecoderLayerInfoMarshaller))] in DecoderLayerInfo layerInfo,
            out SafeDecoderImageHandle handle,
            [MarshalUsing(typeof(DecoderImageInfoMarshaller))] ref DecoderImageInfo info);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial DecoderStatus DecodeImage(
            byte* compressedColorImage,
            UIntPtr compressedColorImageSize,
            IntPtr colorData_MustBeZero,
            [MarshalUsing(typeof(DecoderLayerInfoMarshaller))] in DecoderLayerInfo frameInfo,
            out SafeDecoderImageHandle handle,
            [MarshalUsing(typeof(DecoderImageInfoMarshaller))] ref DecoderImageInfo info);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial void FreeDecoderImageHandle(IntPtr handle);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial DecoderStatus ReadColorImageData(
            SafeDecoderImageHandle handle,
            [MarshalUsing(typeof(CICPColorDataMarshaller))] in CICPColorData colorData,
            uint tileColumnIndex,
            uint tileRowIndex,
            ref BitmapData bitmapData);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        internal static unsafe partial DecoderStatus ReadAlphaImageData(
            SafeDecoderImageHandle handle,
            uint tileColumnIndex,
            uint tileRowIndex,
            ref BitmapData bitmapData);

        [LibraryImport(DllName)]
        [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
        [return: MarshalUsing(typeof(NativeOwnedAsciiString))]
        internal static partial string GetAOMVersionString();
    }
}
