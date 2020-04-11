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

using AvifFileType.Interop;
using PaintDotNet;
using System;

namespace AvifFileType
{
    internal static class AvifNative
    {
        public static void CompressWithTransparency(Surface surface,
                                                    EncoderOptions options,
                                                    AvifProgressCallback avifProgress,
                                                    ref uint progressDone,
                                                    uint progressTotal,
                                                    ColorConversionInfo colorInfo,
                                                    out CompressedAV1Image color,
                                                    out CompressedAV1Image alpha)
        {
            BitmapData bitmapData = new BitmapData
            {
                scan0 = surface.Scan0.Pointer,
                width = (uint)surface.Width,
                height = (uint)surface.Height,
                stride = (uint)surface.Stride
            };

            ProgressContext progressContext = new ProgressContext(avifProgress, progressDone, progressTotal);

            UIntPtr colorImageSize;
            UIntPtr alphaImageSize;

            if (IntPtr.Size == 8)
            {
                SafeAV1ImageX64 colorImage;
                SafeAV1ImageX64 alphaImage;

                EncoderStatus status = AvifNative_64.CompressImage(ref bitmapData,
                                                                   options,
                                                                   progressContext,
                                                                   colorInfo,
                                                                   out colorImage,
                                                                   out colorImageSize,
                                                                   out alphaImage,
                                                                   out alphaImageSize);

                if (status != EncoderStatus.Ok)
                {
                    colorImage?.Dispose();
                    alphaImage?.Dispose();
                    HandleError(status);
                }

                colorImage.Initialize(colorImageSize.ToUInt64());
                alphaImage.Initialize(alphaImageSize.ToUInt64());

                color = new CompressedAV1Image(colorImage, surface.Width, surface.Height, options.yuvFormat);
                alpha = new CompressedAV1Image(alphaImage, surface.Width, surface.Height, YUVChromaSubsampling.Subsampling420);
            }
            else
            {
                SafeAV1ImageX86 colorImage;
                SafeAV1ImageX86 alphaImage;

                EncoderStatus status = AvifNative_86.CompressImage(ref bitmapData,
                                                                   options,
                                                                   progressContext,
                                                                   colorInfo,
                                                                   out colorImage,
                                                                   out colorImageSize,
                                                                   out alphaImage,
                                                                   out alphaImageSize);

                if (status != EncoderStatus.Ok)
                {
                    colorImage?.Dispose();
                    alphaImage?.Dispose();
                    HandleError(status);
                }

                colorImage.Initialize(colorImageSize.ToUInt64());
                alphaImage.Initialize(alphaImageSize.ToUInt64());

                color = new CompressedAV1Image(colorImage, surface.Width, surface.Height, options.yuvFormat);
                alpha = new CompressedAV1Image(alphaImage, surface.Width, surface.Height, YUVChromaSubsampling.Subsampling420);
            }

            progressDone = progressContext.progressDone;
            GC.KeepAlive(avifProgress);
        }

        public static void CompressWithoutTransparency(Surface surface,
                                                       EncoderOptions options,
                                                       AvifProgressCallback avifProgress,
                                                       ref uint progressDone,
                                                       uint progressTotal,
                                                       ColorConversionInfo colorInfo,
                                                       out CompressedAV1Image color)
        {
            BitmapData bitmapData = new BitmapData
            {
                scan0 = surface.Scan0.Pointer,
                width = (uint)surface.Width,
                height = (uint)surface.Height,
                stride = (uint)surface.Stride
            };

            ProgressContext progressContext = new ProgressContext(avifProgress, progressDone, progressTotal);

            UIntPtr colorImageSize;

            if (IntPtr.Size == 8)
            {
                SafeAV1ImageX64 colorImage;

                EncoderStatus status = AvifNative_64.CompressImage(ref bitmapData,
                                                                   options,
                                                                   progressContext,
                                                                   colorInfo,
                                                                   out colorImage,
                                                                   out colorImageSize,
                                                                   IntPtr.Zero,
                                                                   IntPtr.Zero);

                if (status != EncoderStatus.Ok)
                {
                    colorImage?.Dispose();
                    HandleError(status);
                }

                colorImage.Initialize(colorImageSize.ToUInt64());

                color = new CompressedAV1Image(colorImage, surface.Width, surface.Height, options.yuvFormat);
            }
            else
            {
                SafeAV1ImageX86 colorImage;

                EncoderStatus status = AvifNative_86.CompressImage(ref bitmapData,
                                                                   options,
                                                                   progressContext,
                                                                   colorInfo,
                                                                   out colorImage,
                                                                   out colorImageSize,
                                                                   IntPtr.Zero,
                                                                   IntPtr.Zero);

                if (status != EncoderStatus.Ok)
                {
                    colorImage?.Dispose();
                    HandleError(status);
                }

                colorImage.Initialize(colorImageSize.ToUInt64());

                color = new CompressedAV1Image(colorImage, surface.Width, surface.Height, options.yuvFormat);
            }

            progressDone = progressContext.progressDone;
            GC.KeepAlive(avifProgress);
        }

        public static void Decompress(SafeProcessHeapBuffer colorImage,
                                      SafeProcessHeapBuffer alphaImage,
                                      ColorConversionInfo colorConversionInfo,
                                      DecodeInfo decodeInfo,
                                      Surface fullSurface)
        {
            if (colorImage is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(colorImage));
            }

            if (decodeInfo is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(decodeInfo));
            }

            if (fullSurface is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(fullSurface));
            }

            if (alphaImage != null)
            {
                DecompressWithTransparency(colorImage, alphaImage, colorConversionInfo, decodeInfo, fullSurface);
            }
            else
            {
                DecompressWithoutTransparency(colorImage, colorConversionInfo, decodeInfo, fullSurface);
            }
        }

        private static void DecompressWithTransparency(SafeProcessHeapBuffer colorImage,
                                                   SafeProcessHeapBuffer alphaImage,
                                                   ColorConversionInfo colorConversionInfo,
                                                   DecodeInfo decodeInfo,
                                                   Surface surface)
        {
            BitmapData bitmapData = new BitmapData
            {
                scan0 = surface.Scan0.Pointer,
                width = (uint)surface.Width,
                height = (uint)surface.Height,
                stride = (uint)surface.Stride
            };

            UIntPtr colorImageSize = new UIntPtr(colorImage.ByteLength);
            UIntPtr alphaImageSize = new UIntPtr(alphaImage.ByteLength);

            if (IntPtr.Size == 8)
            {
                DecoderStatus status = AvifNative_64.DecompressImage(colorImage,
                                                                     colorImageSize,
                                                                     alphaImage,
                                                                     alphaImageSize,
                                                                     colorConversionInfo,
                                                                     decodeInfo,
                                                                     ref bitmapData);
                if (status != DecoderStatus.Ok)
                {
                    HandleError(status);
                }
            }
            else
            {
                DecoderStatus status = AvifNative_86.DecompressImage(colorImage,
                                                                     colorImageSize,
                                                                     alphaImage,
                                                                     alphaImageSize,
                                                                     colorConversionInfo,
                                                                     decodeInfo,
                                                                     ref bitmapData);
                if (status != DecoderStatus.Ok)
                {
                    HandleError(status);
                }
            }
        }

        private static void DecompressWithoutTransparency(SafeProcessHeapBuffer colorImage,
                                                          ColorConversionInfo colorConversionInfo,
                                                          DecodeInfo decodeInfo,
                                                          Surface surface)
        {
            BitmapData bitmapData = new BitmapData
            {
                scan0 = surface.Scan0.Pointer,
                width = (uint)surface.Width,
                height = (uint)surface.Height,
                stride = (uint)surface.Stride
            };
            UIntPtr colorImageSize = new UIntPtr(colorImage.ByteLength);

            if (IntPtr.Size == 8)
            {
                DecoderStatus status = AvifNative_64.DecompressImage(colorImage,
                                                                     colorImageSize,
                                                                     IntPtr.Zero,
                                                                     UIntPtr.Zero,
                                                                     colorConversionInfo,
                                                                     decodeInfo,
                                                                     ref bitmapData);
                if (status != DecoderStatus.Ok)
                {
                    HandleError(status);
                }
            }
            else
            {
                DecoderStatus status = AvifNative_86.DecompressImage(colorImage,
                                                                     colorImageSize,
                                                                     IntPtr.Zero,
                                                                     UIntPtr.Zero,
                                                                     colorConversionInfo,
                                                                     decodeInfo,
                                                                     ref bitmapData);
                if (status != DecoderStatus.Ok)
                {
                    HandleError(status);
                }
            }
        }

        private static void HandleError(EncoderStatus status)
        {
            switch (status)
            {
                case EncoderStatus.Ok:
                    break;
                case EncoderStatus.NullParameter:
                    throw new FormatException("A required encoder parameter was null.");
                case EncoderStatus.OutOfMemory:
                    throw new OutOfMemoryException();
                case EncoderStatus.UnknownYUVFormat:
                    throw new FormatException("The YUV format is not supported by the encoder.");
                case EncoderStatus.CodecInitFailed:
                    throw new FormatException("Unable to initialize AV1 encoder.");
                case EncoderStatus.EncodeFailed:
                    throw new FormatException("The AV1 encode failed.");
                case EncoderStatus.UserCancelled:
                    throw new OperationCanceledException();
                default:
                    throw new FormatException("An unknown error occurred when encoding the image.");
            }
        }

        private static void HandleError(DecoderStatus status)
        {
            switch (status)
            {
                case DecoderStatus.Ok:
                    break;
                case DecoderStatus.NullParameter:
                    throw new FormatException("A required decoder parameter was null.");
                case DecoderStatus.OutOfMemory:
                    throw new OutOfMemoryException();
                case DecoderStatus.CodecInitFailed:
                    throw new FormatException("Unable to initialize AV1 encoder.");
                case DecoderStatus.DecodeFailed:
                    throw new FormatException("The AV1 decode failed.");
                case DecoderStatus.AlphaSizeMismatch:
                    throw new FormatException("The alpha image size does not match the color image.");
                default:
                    throw new FormatException("An unknown error occurred when decoding the image.");
            }
        }
    }
}
