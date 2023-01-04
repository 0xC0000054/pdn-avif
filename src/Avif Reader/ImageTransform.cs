////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.AvifContainer;
using PaintDotNet;
using System;
using System.Drawing;

namespace AvifFileType
{
    internal static class ImageTransform
    {
        internal static void Crop(CleanApertureBox cleanApertureBox, ref Surface surface)
        {
            if (cleanApertureBox is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(cleanApertureBox));
            }

            if (cleanApertureBox.Width.Denominator == 0 ||
                cleanApertureBox.Height.Denominator == 0 ||
                cleanApertureBox.HorizontalOffset.Denominator == 0 ||
                cleanApertureBox.VerticalOffset.Denominator == 0)
            {
                return;
            }

            int cropWidth = cleanApertureBox.Width.ToInt32();
            int cropHeight = cleanApertureBox.Height.ToInt32();

            if (cropWidth <= 0 || cropHeight <= 0)
            {
                // Invalid crop width/height.
                return;
            }

            double offsetX = cleanApertureBox.HorizontalOffset.ToDouble();
            double offsetY = cleanApertureBox.VerticalOffset.ToDouble();

            double pictureCenterX = offsetX + ((surface.Width - 1) / 2.0);
            double pictureCenterY = offsetY + ((surface.Height - 1) / 2.0);

            int cropRectX = (int)Math.Round(pictureCenterX - ((cropWidth - 1) / 2.0));
            int cropRectY = (int)Math.Round(pictureCenterY - ((cropHeight - 1) / 2.0));

            Rectangle cropRect = new Rectangle(cropRectX, cropRectY, cropWidth, cropHeight);

            // Check that the crop rectangle is within the surface bounds.
            if (cropRect.IntersectsWith(surface.Bounds))
            {
                Surface temp = new Surface(cropWidth, cropHeight);
                try
                {
                    temp.CopySurface(surface, cropRect);

                    surface.Dispose();
                    surface = temp;
                    temp = null;
                }
                finally
                {
                    temp?.Dispose();
                }
            }
        }

        internal static unsafe void FlipHorizontal(Surface surface)
        {
            int lastColumn = surface.Width - 1;
            int flipWidth = surface.Width / 2;

            for (int y = 0; y < surface.Height; y++)
            {
                for (int x = 0; x < flipWidth; x++)
                {
                    int sampleColumn = lastColumn - x;

                    ColorBgra temp = surface[x, y];
                    surface[x, y] = surface[sampleColumn, y];
                    surface[sampleColumn, y] = temp;
                }
            }
        }

        internal static unsafe void FlipVertical(Surface surface)
        {
            int lastRow = surface.Height - 1;
            int flipHeight = surface.Height / 2;

            for (int x = 0; x < surface.Width; x++)
            {
                for (int y = 0; y < flipHeight; y++)
                {
                    int sampleRow = lastRow - y;

                    ColorBgra temp = surface[x, y];
                    surface[x, y] = surface[x, sampleRow];
                    surface[x, sampleRow] = temp;
                }
            }
        }

        internal static unsafe void Rotate90CCW(ref Surface surface)
        {
            Surface temp = null;
            try
            {
                temp = new Surface(surface.Height, surface.Width);

                int lastColumn = surface.Width - 1;

                for (int y = 0; y < temp.Height; y++)
                {
                    ColorBgra* dstPtr = temp.GetRowPointerUnchecked(y);

                    for (int x = 0; x < temp.Width; x++)
                    {
                        ColorBgra pixel = surface[lastColumn - y, x];

                        dstPtr->Bgra = pixel.Bgra;
                        dstPtr++;
                    }
                }

                surface.Dispose();
                surface = temp;
                temp = null;
            }
            finally
            {
                temp?.Dispose();
            }
        }

        internal static unsafe void Rotate180(Surface surface)
        {
            int width = surface.Width;
            int height = surface.Height;

            int halfHeight = height / 2;
            int lastColumn = width - 1;

            for (int y = 0; y < halfHeight; y++)
            {
                ColorBgra* topPtr = surface.GetRowPointerUnchecked(y);
                ColorBgra* bottomPtr = surface.GetPointPointerUnchecked(lastColumn, height - y - 1);

                for (int x = 0; x < width; x++)
                {
                    ColorBgra temp = *bottomPtr;
                    *bottomPtr = *topPtr;
                    *topPtr = temp;

                    topPtr++;
                    bottomPtr--;
                }
            }

            // The middle row must be handled separately if the height is odd.
            if ((height & 1) == 1)
            {
                int halfWidth = width / 2;

                ColorBgra* leftPtr = surface.GetRowPointerUnchecked(halfHeight);
                ColorBgra* rightPtr = surface.GetPointPointerUnchecked(lastColumn, halfHeight);

                for (int x = 0; x < halfWidth; x++)
                {
                    ColorBgra temp = *rightPtr;
                    *rightPtr = *leftPtr;
                    *leftPtr = temp;

                    leftPtr++;
                    rightPtr--;
                }
            }
        }

        internal static unsafe void Rotate270CCW(ref Surface surface)
        {
            Surface temp = null;
            try
            {
                temp = new Surface(surface.Height, surface.Width);

                int lastRow = surface.Height - 1;

                for (int y = 0; y < temp.Height; y++)
                {
                    ColorBgra* dstPtr = temp.GetRowPointerUnchecked(y);

                    for (int x = 0; x < temp.Width; x++)
                    {
                        ColorBgra pixel = surface[y, lastRow - x];

                        dstPtr->Bgra = pixel.Bgra;
                        dstPtr++;
                    }
                }

                surface.Dispose();
                surface = temp;
                temp = null;
            }
            finally
            {
                temp?.Dispose();
            }
        }
    }
}
