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

namespace AvifFileType.Interop
{
    internal readonly ref struct NativeEncoderOptions
    {
        public readonly int quality;
        public readonly EncoderPreset encoderPreset;
        public readonly YUVChromaSubsampling yuvFormat;
        public readonly int maxThreads;
        public readonly byte lossless;
        public readonly byte losslessAlpha;

        public NativeEncoderOptions(int quality,
                                    EncoderPreset encoderPreset,
                                    YUVChromaSubsampling yuvFormat,
                                    int maxThreads,
                                    bool lossless,
                                    bool losslessAlpha)
        {
            this.quality = quality;
            this.encoderPreset = encoderPreset;
            this.yuvFormat = yuvFormat;
            this.maxThreads = maxThreads;
            this.lossless = lossless.ToByte();
            this.losslessAlpha = losslessAlpha.ToByte();
        }
    }
}
