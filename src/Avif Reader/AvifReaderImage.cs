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

using AvifFileType.AvifContainer;
using PaintDotNet;
using PaintDotNet.Imaging;
using PaintDotNet.Rendering;
using System;

namespace AvifFileType
{
    internal sealed class AvifReaderImage
        : Disposable, IOutputImageTransform
    {
        private IBitmap image;
        private readonly IImagingFactory imagingFactory;

        public AvifReaderImage(SizeInt32 imageSize,
                               AvifReaderImageFormat format,
                               in CICPColorData colorData,
                               IImagingFactory imagingFactory)
        {
            ArgumentNullException.ThrowIfNull(format);

            this.image = imagingFactory.CreateBitmap(imageSize, format.WICPixelFormat);
            this.HDRFormat = format.HDRFormat;
            this.CICPColor = colorData;
            this.IsPremultipliedAlpha = false;
            this.imagingFactory = imagingFactory.CreateRef();
        }

        public IBitmap Image => this.image;

        public HDRFormat HDRFormat { get; }

        public CICPColorData CICPColor { get; }

        public bool IsPremultipliedAlpha { get; set; }

        public PixelFormat WICPixelFormat => this.image.PixelFormat;

        public SizeInt32 Size => this.image.Size;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.image?.Dispose();
                this.imagingFactory.Dispose();
            }
        }

        void IOutputImageTransform.Crop(CleanApertureBox cleanApertureBox)
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

            SizeInt32 bitmapSize = this.image.Size;

            int cropWidth = cleanApertureBox.Width.ToInt32();
            int cropHeight = cleanApertureBox.Height.ToInt32();

            if (cropWidth <= 0 || cropHeight <= 0)
            {
                // Invalid crop width/height.
                return;
            }

            double offsetX = cleanApertureBox.HorizontalOffset.ToDouble();
            double offsetY = cleanApertureBox.VerticalOffset.ToDouble();

            double pictureCenterX = offsetX + ((bitmapSize.Width - 1) / 2.0);
            double pictureCenterY = offsetY + ((bitmapSize.Height - 1) / 2.0);

            int cropRectX = (int)Math.Round(pictureCenterX - ((cropWidth - 1) / 2.0));
            int cropRectY = (int)Math.Round(pictureCenterY - ((cropHeight - 1) / 2.0));

            RectInt32 cropRect = new(cropRectX, cropRectY, cropWidth, cropHeight);

            // Check that the crop rectangle is within the surface bounds.
            if (cropRect.IntersectsWith(this.image.Bounds()))
            {
                IBitmap? temp = this.image.CreateWindow(cropRect);
                try
                {
                    // Calling Dispose is the correct behavior because CreateWindow
                    // increments the reference count of the original bitmap.
                    this.image.Dispose();
                    this.image = temp;
                    temp = null;
                }
                finally
                {
                    temp?.Dispose();
                }
            }
        }

        void IOutputImageTransform.Rotate90CCW()
        {
            IBitmap? temp = null;

            try
            {
                SizeInt32 size = this.image.Size;
                PixelFormat pixelFormat = this.image.PixelFormat;

                temp = this.imagingFactory.CreateBitmap(size.Height, size.Width, pixelFormat);

                using (IBitmapLock sourceLock = this.image.Lock(BitmapLockOptions.Read))
                using (IBitmapLock destLock = temp.Lock(BitmapLockOptions.Write))
                {
                    if (pixelFormat == PixelFormats.Bgra32)
                    {
                        ImageTransform.Rotate90CCW<ColorBgra32>(sourceLock, destLock);
                    }
                    else if (pixelFormat == PixelFormats.Rgba64)
                    {
                        ImageTransform.Rotate90CCW<ColorRgba64>(sourceLock, destLock);
                    }
                    else if (pixelFormat == PixelFormats.Rgba128Float)
                    {
                        ImageTransform.Rotate90CCW<ColorRgba128Float>(sourceLock, destLock);
                    }
                    else
                    {
                        ExceptionUtil.UnsupportedPixelFormat(pixelFormat);
                    }
                }

                this.image.Dispose();
                this.image = temp;
                temp = null;
            }
            finally
            {
                temp?.Dispose();
            }
        }

        void IOutputImageTransform.Rotate180()
        {
            using (IBitmapLock bitmapLock = this.image.Lock(BitmapLockOptions.ReadWrite))
            {
                PixelFormat pixelFormat = bitmapLock.PixelFormat;

                if (pixelFormat == PixelFormats.Bgra32)
                {
                    ImageTransform.Rotate180<ColorBgra32>(bitmapLock);
                }
                else if (pixelFormat == PixelFormats.Rgba64)
                {
                    ImageTransform.Rotate180<ColorRgba64>(bitmapLock);
                }
                else if (pixelFormat == PixelFormats.Rgba128Float)
                {
                    ImageTransform.Rotate180<ColorRgba128Float>(bitmapLock);
                }
                else
                {
                    ExceptionUtil.UnsupportedPixelFormat(pixelFormat);
                }
            }
        }

        void IOutputImageTransform.Rotate270CCW()
        {
            IBitmap? temp = null;

            try
            {
                SizeInt32 size = this.image.Size;
                PixelFormat pixelFormat = this.image.PixelFormat;

                temp = this.imagingFactory.CreateBitmap(size.Height, size.Width, pixelFormat);

                using (IBitmapLock sourceLock = this.image.Lock(BitmapLockOptions.Read))
                using (IBitmapLock destLock = temp.Lock(BitmapLockOptions.Write))
                {
                    if (pixelFormat == PixelFormats.Bgra32)
                    {
                        ImageTransform.Rotate270CCW<ColorBgra32>(sourceLock, destLock);
                    }
                    else if (pixelFormat == PixelFormats.Rgba64)
                    {
                        ImageTransform.Rotate270CCW<ColorRgba64>(sourceLock, destLock);
                    }
                    else if (pixelFormat == PixelFormats.Rgba128Float)
                    {
                        ImageTransform.Rotate270CCW<ColorRgba128Float>(sourceLock, destLock);
                    }
                    else
                    {
                        ExceptionUtil.UnsupportedPixelFormat(pixelFormat);
                    }
                }

                this.image.Dispose();
                this.image = temp;
                temp = null;
            }
            finally
            {
                temp?.Dispose();
            }
        }

        void IOutputImageTransform.FlipHorizontal()
        {
            using (IBitmapLock bitmapLock = this.image.Lock(BitmapLockOptions.ReadWrite))
            {
                PixelFormat pixelFormat = bitmapLock.PixelFormat;

                if (pixelFormat == PixelFormats.Bgra32)
                {
                    ImageTransform.FlipHorizontal<ColorBgra32>(bitmapLock);
                }
                else if (pixelFormat == PixelFormats.Rgba64)
                {
                    ImageTransform.FlipHorizontal<ColorRgba64>(bitmapLock);
                }
                else if (pixelFormat == PixelFormats.Rgba128Float)
                {
                    ImageTransform.FlipHorizontal<ColorRgba128Float>(bitmapLock);
                }
                else
                {
                    ExceptionUtil.UnsupportedPixelFormat(pixelFormat);
                }
            }
        }

        void IOutputImageTransform.FlipVertical()
        {
            using (IBitmapLock bitmapLock = this.image.Lock(BitmapLockOptions.ReadWrite))
            {
                PixelFormat pixelFormat = bitmapLock.PixelFormat;

                if (pixelFormat == PixelFormats.Bgra32)
                {
                    ImageTransform.FlipVertical<ColorBgra32>(bitmapLock);
                }
                else if (pixelFormat == PixelFormats.Rgba64)
                {
                    ImageTransform.FlipVertical<ColorRgba64>(bitmapLock);
                }
                else if (pixelFormat == PixelFormats.Rgba128Float)
                {
                    ImageTransform.FlipVertical<ColorRgba128Float>(bitmapLock);
                }
                else
                {
                    ExceptionUtil.UnsupportedPixelFormat(pixelFormat);
                }
            }
        }
    }
}
