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
    [CustomMarshaller(typeof(EncoderOptions), MarshalMode.ManagedToUnmanagedIn, typeof(EncoderOptionsMarshaller))]
    internal static class EncoderOptionsMarshaller
    {
        internal readonly struct NativeEncoderOptions
        {
            public readonly int quality;
            public readonly EncoderPreset encoderPreset;
            public readonly YUVChromaSubsampling yuvFormat;
            public readonly int maxThreads;
            public readonly byte lossless;
            public readonly byte losslessAlpha;

            public NativeEncoderOptions(in EncoderOptions managed)
            {
                this.quality = managed.quality;
                this.encoderPreset = managed.encoderPreset;
                this.yuvFormat = managed.yuvFormat;
                this.maxThreads = managed.maxThreads;
                this.lossless = managed.lossless.ToByte();
                this.losslessAlpha = managed.losslessAlpha.ToByte();
            }
        }

        public static NativeEncoderOptions ConvertToUnmanaged(EncoderOptions managed)
        {
            return new NativeEncoderOptions(managed);
        }
    }
}
