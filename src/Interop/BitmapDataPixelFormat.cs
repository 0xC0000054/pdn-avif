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
    // This must be kept in sync with AvifNative.h
    internal enum BitmapDataPixelFormat : int
    {
        Bgra32 = 0,
        Rgba64,
        Rgba128Float
    }
}
