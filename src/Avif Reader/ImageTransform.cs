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
using System.Runtime.CompilerServices;

namespace AvifFileType
{
    internal static class ImageTransform
    {
        internal static void FlipHorizontal<T>(IBitmapLock bitmapLock) where T: unmanaged, INaturalPixelInfo
        {
            RegionPtr<T> region = bitmapLock.AsRegionPtr<T>();

            int lastColumn = region.Width - 1;
            int flipWidth = region.Width / 2;

            for (int y = 0; y < region.Height; y++)
            {
                for (int x = 0; x < flipWidth; x++)
                {
                    int sampleColumn = lastColumn - x;

                    (region[sampleColumn, y], region[x, y]) = (region[x, y], region[sampleColumn, y]);
                }
            }
        }

        internal static void FlipVertical<T>(IBitmapLock bitmapLock) where T : unmanaged, INaturalPixelInfo
        {
            RegionPtr<T> region = bitmapLock.AsRegionPtr<T>();

            int lastRow = region.Height - 1;
            int flipHeight = region.Height / 2;

            for (int x = 0; x < region.Width; x++)
            {
                for (int y = 0; y < flipHeight; y++)
                {
                    int sampleRow = lastRow - y;

                    (region[x, sampleRow], region[x, y]) = (region[x, y], region[x, sampleRow]);
                }
            }
        }

        internal static void Rotate90CCW<T>(IBitmapLock sourceLock, IBitmapLock destLock) where T : unmanaged, INaturalPixelInfo
        {
            RegionPtr<T> source = sourceLock.AsRegionPtr<T>();
            RegionPtr<T> dest = destLock.AsRegionPtr<T>();

            int lastColumn = source.Width - 1;

            for (int y = 0; y < dest.Height; y++)
            {
                RegionRowPtr<T> row = dest.Rows[y];

                for (int x = 0; x < dest.Width; x++)
                {
                    row[x] = source[lastColumn - y, x];
                }
            }
        }

        internal static void Rotate180<T>(IBitmapLock bitmapLock) where T : unmanaged, INaturalPixelInfo
        {
            RegionPtr<T> region = bitmapLock.AsRegionPtr<T>();

            int width = region.Width;
            int height = region.Height;

            int halfHeight = height / 2;
            int lastColumn = width - 1;

            for (int y = 0; y < halfHeight; y++)
            {
                ref T topRef = ref region[0, y];
                ref T bottomRef = ref region[lastColumn, height - y - 1];

                for (int x = 0; x < width; x++)
                {
                    (topRef, bottomRef) = (bottomRef, topRef);

                    topRef = ref Unsafe.Add(ref topRef, 1);
                    bottomRef = ref Unsafe.Subtract(ref bottomRef, 1);
                }
            }

            // The middle row must be handled separately if the height is odd.
            if ((height & 1) == 1)
            {
                int halfWidth = width / 2;

                ref T leftRef = ref region[0, halfHeight];
                ref T rightRef = ref region[lastColumn, halfHeight];

                for (int x = 0; x < halfWidth; x++)
                {
                    (leftRef, rightRef) = (rightRef, leftRef);

                    leftRef = ref Unsafe.Add(ref leftRef, 1);
                    rightRef = ref Unsafe.Subtract(ref rightRef, 1);
                }
            }
        }

        internal static void Rotate270CCW<T>(IBitmapLock sourceLock, IBitmapLock destLock) where T : unmanaged, INaturalPixelInfo
        {
            RegionPtr<T> source = sourceLock.AsRegionPtr<T>();
            RegionPtr<T> dest = destLock.AsRegionPtr<T>();

            int lastRow = source.Height - 1;

            for (int y = 0; y < dest.Height; y++)
            {
                RegionRowPtr<T> row = dest.Rows[y];

                for (int x = 0; x < dest.Width; x++)
                {
                    row[x] = source[y, lastRow - x];
                }
            }
        }
    }
}
