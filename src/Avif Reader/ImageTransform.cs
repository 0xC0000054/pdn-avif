////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using PaintDotNet;

namespace AvifFileType
{
    internal static class ImageTransform
    {
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
                    ColorBgra* dstPtr = temp.GetRowAddressUnchecked(y);

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
            int lastColumn = surface.Width - 1;
            int lastRow = surface.Height - 1;

            for (int y = 0; y < surface.Height; y++)
            {
                for (int x = 0; x < surface.Width; x++)
                {
                    surface[x, y] = surface[lastColumn - x, lastRow - y];
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
                    ColorBgra* dstPtr = temp.GetRowAddressUnchecked(y);

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
