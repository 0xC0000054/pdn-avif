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

using System.Runtime.InteropServices.Marshalling;

namespace AvifFileType.Interop
{
    using NativeCICPColorData = CICPColorDataMarshaller.NativeCICPColorData;

    [CustomMarshaller(typeof(DecoderImageInfo), MarshalMode.Default, typeof(DecoderImageInfoMarshaller))]
    internal static class DecoderImageInfoMarshaller
    {
        public struct NativeDecoderImageInfo
        {
            public uint width;
            public uint height;
            public uint bitDepth;
            public YUVChromaSubsampling chromaSubsampling;
            public NativeCICPColorData cicpData;
        }

        public static NativeDecoderImageInfo ConvertToUnmanaged(DecoderImageInfo managed)
        {
            return new NativeDecoderImageInfo
            {
                width = managed.Width,
                height = managed.Height,
                bitDepth = managed.BitDepth,
                chromaSubsampling = managed.ChromaSubsampling,
                cicpData = CICPColorDataMarshaller.ConvertToUnmanaged(managed.CICPColor)
            };
        }

        public static DecoderImageInfo ConvertToManaged(NativeDecoderImageInfo unmanaged)
        {
            return new DecoderImageInfo(unmanaged.width,
                                        unmanaged.height,
                                        unmanaged.bitDepth,
                                        unmanaged.chromaSubsampling,
                                        CICPColorDataMarshaller.ConvertToManaged(unmanaged.cicpData));
        }
    }
}
