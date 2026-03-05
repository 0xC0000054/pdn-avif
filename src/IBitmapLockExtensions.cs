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

using PaintDotNet;
using PaintDotNet.Imaging;
using System;

namespace AvifFileType
{
    internal static class IBitmapLockExtensions
    {
        public static unsafe RegionPtr<T> AsRegionPtr<T>(this IBitmapLock bitmapLock) where T : unmanaged, INaturalPixelInfo
        {
            if (default(T).PixelFormat != bitmapLock.PixelFormat)
            {
                throw new ArgumentException($"The pixel format of the bitmap lock must be {default(T).PixelFormat}.", nameof(bitmapLock));
            }

            return new((T*)bitmapLock.Buffer, bitmapLock.Size, bitmapLock.BufferStride);
        }
    }
}
