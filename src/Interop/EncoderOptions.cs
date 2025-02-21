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
    }
}
