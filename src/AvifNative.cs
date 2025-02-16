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

using AvifFileType.Interop;
using PaintDotNet;
using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace AvifFileType
{
    internal static class AvifNative
    {
        public static void CompressAlphaImage(Surface surface,
                                              in EncoderOptions options,
                                              AvifProgressCallback avifProgress,
                                              ref uint progressDone,
                                              uint progressTotal,
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

            using (CompressedAV1DataAllocator allocator = new CompressedAV1DataAllocator(1))
            {
                IntPtr alphaImage;

                CompressedAV1OutputAlloc outputAllocDelegate = new CompressedAV1OutputAlloc(allocator.Allocate);
                EncoderStatus status = EncoderStatus.Ok;
                NativeEncoderOptions nativeEncoderOptions = options.ToNative();

                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    status = AvifNative_64.CompressAlphaImage(bitmapData,
                                                              nativeEncoderOptions,
                                                              ref progressContext,
                                                              outputAllocDelegate,
                                                              out alphaImage);
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    status = AvifNative_ARM64.CompressAlphaImage(bitmapData,
                                                                 nativeEncoderOptions,
                                                                 ref progressContext,
                                                                 outputAllocDelegate,
                                                                 out alphaImage);
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                GC.KeepAlive(outputAllocDelegate);

                if (status != EncoderStatus.Ok)
                {
                    HandleError(status, allocator.ExceptionInfo);
                }

                alpha = new CompressedAV1Image(allocator.GetCompressedAV1Data(alphaImage), surface.Width, surface.Height, YUVChromaSubsampling.Subsampling400);
            }

            progressDone = progressContext.progressDone;
            GC.KeepAlive(avifProgress);
        }

        public static void CompressColorImage(Surface surface,
                                              in EncoderOptions options,
                                              AvifProgressCallback avifProgress,
                                              ref uint progressDone,
                                              uint progressTotal,
                                              CICPColorData colorInfo,
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

            using (CompressedAV1DataAllocator allocator = new CompressedAV1DataAllocator(1))
            {
                IntPtr colorImage;

                CompressedAV1OutputAlloc outputAllocDelegate = new CompressedAV1OutputAlloc(allocator.Allocate);
                EncoderStatus status = EncoderStatus.Ok;
                NativeEncoderOptions nativeEncoderOptions = options.ToNative();

                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    status = AvifNative_64.CompressColorImage(bitmapData,
                                                              nativeEncoderOptions,
                                                              ref progressContext,
                                                              colorInfo,
                                                              outputAllocDelegate,
                                                              out colorImage);
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    status = AvifNative_ARM64.CompressColorImage(bitmapData,
                                                                 nativeEncoderOptions,
                                                                 ref progressContext,
                                                                 colorInfo,
                                                                 outputAllocDelegate,
                                                                 out colorImage);
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                GC.KeepAlive(outputAllocDelegate);

                if (status != EncoderStatus.Ok)
                {
                    HandleError(status, allocator.ExceptionInfo);
                }

                color = new CompressedAV1Image(allocator.GetCompressedAV1Data(colorImage), surface.Width, surface.Height, options.yuvFormat);
            }

            progressDone = progressContext.progressDone;
            GC.KeepAlive(avifProgress);
        }

        public static DecoderImage DecodeImage(AvifItemData image, CICPColorData? containerColorData, DecoderLayerInfo layerInfo)
        {
            if (image is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(image));
            }

            DecoderImage decoderImage = null!;

            unsafe
            {
                image.UseBufferPointer((ptr, length) =>
                {
                    UIntPtr colorImageSize = new UIntPtr(length);
                    DecoderStatus status = DecoderStatus.Ok;

                    SafeDecoderImageHandle? safeDecoderImageHandle;
                    DecoderImageInfo decoderImageInfo = new();

                    if (containerColorData.HasValue)
                    {
                        CICPColorData colorData = containerColorData.Value;

                        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                        {
                            status = AvifNative_64.DecodeImage(ptr,
                                                               colorImageSize,
                                                               colorData,
                                                               layerInfo,
                                                               out safeDecoderImageHandle,
                                                               ref decoderImageInfo);
                        }
                        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                        {
                            status = AvifNative_ARM64.DecodeImage(ptr,
                                                                  colorImageSize,
                                                                  colorData,
                                                                  layerInfo,
                                                                  out safeDecoderImageHandle,
                                                                  ref decoderImageInfo);
                        }
                        else
                        {
                            throw new PlatformNotSupportedException();
                        }
                    }
                    else
                    {
                        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                        {
                            status = AvifNative_64.DecodeImage(ptr,
                                                               colorImageSize,
                                                               nint.Zero,
                                                               layerInfo,
                                                               out safeDecoderImageHandle,
                                                               ref decoderImageInfo);
                        }
                        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                        {
                            status = AvifNative_ARM64.DecodeImage(ptr,
                                                                  colorImageSize,
                                                                  nint.Zero,
                                                                  layerInfo,
                                                                  out safeDecoderImageHandle,
                                                                  ref decoderImageInfo);
                        }
                        else
                        {
                            throw new PlatformNotSupportedException();
                        }
                    }

                    if (status == DecoderStatus.Ok)
                    {
                        try
                        {
                            decoderImage = new(safeDecoderImageHandle, decoderImageInfo);
                            safeDecoderImageHandle = null;
                        }
                        finally
                        {
                            safeDecoderImageHandle?.Dispose();
                        }
                    }
                    else
                    {
                        HandleError(status);
                    }
                });
            }

            return decoderImage;
        }

        public static unsafe void ReadColorImageData(DecoderImage image,
                                                     CICPColorData colorData,
                                                     uint tileColumnIndex,
                                                     uint tileRowIndex,
                                                     Surface surface)
        {
            BitmapData bitmapData = new()
            {
                scan0 = surface.Scan0.Pointer,
                stride = (uint)surface.Stride,
                width = (uint)surface.Width,
                height = (uint)surface.Height
            };

            DecoderStatus status;

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                status = AvifNative_64.ReadColorImageData(image.SafeDecoderImage,
                                                          colorData,
                                                          tileColumnIndex,
                                                          tileRowIndex,
                                                          ref bitmapData);
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                status = AvifNative_ARM64.ReadColorImageData(image.SafeDecoderImage,
                                                             colorData,
                                                             tileColumnIndex,
                                                             tileRowIndex,
                                                             ref bitmapData);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            if (status != DecoderStatus.Ok)
            {
                HandleError(status);
            }
        }

        public static unsafe void ReadAlphaImageData(DecoderImage image,
                                                     uint tileColumnIndex,
                                                     uint tileRowIndex,
                                                     Surface surface)
        {
            BitmapData bitmapData = new()
            {
                scan0 = surface.Scan0.Pointer,
                stride = (uint)surface.Stride,
                width = (uint)surface.Width,
                height = (uint)surface.Height
            };

            DecoderStatus status;

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                status = AvifNative_64.ReadAlphaImageData(image.SafeDecoderImage,
                                                          tileColumnIndex,
                                                          tileRowIndex,
                                                          ref bitmapData);
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                status = AvifNative_ARM64.ReadAlphaImageData(image.SafeDecoderImage,
                                                             tileColumnIndex,
                                                             tileRowIndex,
                                                             ref bitmapData);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            if (status != DecoderStatus.Ok)
            {
                HandleError(status);
            }
        }

        public static string GetAOMVersionString()
        {
            string result;

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                result = AvifNative_64.GetAOMVersionString();
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                result = AvifNative_ARM64.GetAOMVersionString();
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            return result;
        }

        private static void HandleError(EncoderStatus status, ExceptionDispatchInfo? exceptionDispatchInfo)
        {
            if (exceptionDispatchInfo != null)
            {
                exceptionDispatchInfo.Throw();
            }
            else
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
                        throw new FormatException("Unable to initialize the AV1 encoder.");
                    case EncoderStatus.EncodeFailed:
                        throw new FormatException("The AV1 encode failed.");
                    case EncoderStatus.UserCancelled:
                        throw new OperationCanceledException();
                    default:
                        throw new FormatException("An unknown error occurred when encoding the image.");
                }
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
                    throw new FormatException("Unable to initialize the AV1 decoder.");
                case DecoderStatus.DecodeFailed:
                    throw new FormatException("The AV1 decode failed.");
                case DecoderStatus.UnsupportedBitDepth:
                    throw new FormatException("The image has an unsupported bit depth.");
                case DecoderStatus.UnknownYUVFormat:
                    throw new FormatException("The YUV format is not supported by the decoder.");
                default:
                    throw new FormatException("An unknown error occurred when decoding the image.");
            }
        }
    }
}
