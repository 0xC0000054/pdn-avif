﻿////////////////////////////////////////////////////////////////////////
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
    internal unsafe struct BitmapData
    {
        public void* scan0;
        public uint width;
        public uint height;
        public uint stride;
        public BitmapDataPixelFormat pixelFormat;
    }
}
