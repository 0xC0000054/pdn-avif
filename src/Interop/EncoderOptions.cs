////////////////////////////////////////////////////////////////////////
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

using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct EncoderOptions
    {
        public int quality;
        public EncoderPreset encoderPreset;
        public YUVChromaSubsampling yuvFormat;
        public int maxThreads;
        public bool lossless;
        public bool losslessAlpha;

        public readonly NativeEncoderOptions ToNative()
        {
            return new NativeEncoderOptions(this.quality,
                                            this.encoderPreset,
                                            this.yuvFormat,
                                            this.maxThreads,
                                            this.lossless,
                                            this.losslessAlpha);
        }
    }
}
