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

// Ignore Spelling: Bgra Rgba

using PaintDotNet.Imaging;

namespace AvifFileType
{
    internal sealed class AvifReaderImageFormat
    {
        public static readonly AvifReaderImageFormat Bgra32 = new(PixelFormats.Bgra32, HDRFormat.None);
        public static readonly AvifReaderImageFormat Rgba64 = new(PixelFormats.Rgba64, HDRFormat.None);
        public static readonly AvifReaderImageFormat Rgba128FloatPQ = new(PixelFormats.Rgba128Float, HDRFormat.PQ);

        private AvifReaderImageFormat(PixelFormat pixelFormat, HDRFormat HDRFormat)
        {
            this.WICPixelFormat = pixelFormat;
            this.HDRFormat = HDRFormat;
        }

        public PixelFormat WICPixelFormat { get; }

        public HDRFormat HDRFormat { get; }
    }
}
