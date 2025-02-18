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
using PaintDotNet.Rendering;
using System;
using System.Runtime.CompilerServices;

namespace AvifFileType
{
    internal static class AlphaChannelUtil
    {
        public static void SetToOpaque(IBitmap image)
        {
            using (IBitmapLock bitmapLock = image.Lock(BitmapLockOptions.ReadWrite))
            {
                PixelFormat pixelFormat = bitmapLock.PixelFormat;

                if (pixelFormat == PixelFormats.Bgra32)
                {
                    SetToOpaque(bitmapLock.AsRegionPtr<ColorBgra32>());
                }
                else if (pixelFormat == PixelFormats.Rgba64)
                {
                    SetToOpaque(bitmapLock.AsRegionPtr<ColorRgba64>());
                }
                else if (pixelFormat == PixelFormats.Rgba128Float)
                {
                    SetToOpaque(bitmapLock.AsRegionPtr<ColorRgba128Float>());
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported WIC PixelFormat: {pixelFormat.GetName()}");
                }
            }
        }

        private static void SetToOpaque(RegionPtr<ColorBgra32> region)
        {
            PixelKernels.SetAlphaChannel(region, ColorAlpha8.Opaque);
        }

        private static void SetToOpaque(RegionPtr<ColorRgba64> region)
        {
            foreach (RegionRowPtr<ColorRgba64> row in region.Rows)
            {
                ref ColorRgba64 pixel = ref row[0];

                for (int x = 0; x < region.Width; x++)
                {
                    pixel.A = 65535;
                    pixel = ref Unsafe.Add(ref pixel, 1);
                }
            }
        }

        private static void SetToOpaque(RegionPtr<ColorRgba128Float> region)
        {
            foreach (RegionRowPtr<ColorRgba128Float> row in region.Rows)
            {
                ref ColorRgba128Float pixel = ref row[0];

                for (int x = 0; x < region.Width; x++)
                {
                    pixel.A = 1.0f;
                    pixel = ref Unsafe.Add(ref pixel, 1);
                }
            }
        }
    }
}
