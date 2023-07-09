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

using AvifFileType.Interop;
using PaintDotNet;
using PaintDotNet.AppModel;
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
                                              IArrayPoolService arrayPool,
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

            using (CompressedAV1DataAllocator allocator = new CompressedAV1DataAllocator(1, arrayPool))
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
                                              IArrayPoolService arrayPool,
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

            using (CompressedAV1DataAllocator allocator = new CompressedAV1DataAllocator(1, arrayPool))
            {
                IntPtr colorImage;

                CompressedAV1OutputAlloc outputAllocDelegate = new CompressedAV1OutputAlloc(allocator.Allocate);
                EncoderStatus status = EncoderStatus.Ok;
                NativeCICPColorData nativeCICPColorData = colorInfo.ToNative();
                NativeEncoderOptions nativeEncoderOptions = options.ToNative();

                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    status = AvifNative_64.CompressColorImage(bitmapData,
                                                              nativeEncoderOptions,
                                                              ref progressContext,
                                                              nativeCICPColorData,
                                                              outputAllocDelegate,
                                                              out colorImage);
                }
                else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    status = AvifNative_ARM64.CompressColorImage(bitmapData,
                                                                 nativeEncoderOptions,
                                                                 ref progressContext,
                                                                 nativeCICPColorData,
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

        public static void DecompressColor(AvifItemData colorImage,
                                           CICPColorData? colorConversionInfo,
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

            unsafe
            {
                colorImage.UseBufferPointer((ptr, length) =>
                {
                    BitmapData bitmapData = new BitmapData
                    {
                        scan0 = fullSurface.Scan0.Pointer,
                        width = (uint)fullSurface.Width,
                        height = (uint)fullSurface.Height,
                        stride = (uint)fullSurface.Stride
                    };
                    UIntPtr colorImageSize = new UIntPtr(length);
                    DecoderStatus status = DecoderStatus.Ok;
                    NativeDecodeInfo nativeDecodeInfo = decodeInfo.ToNative();

                    if (colorConversionInfo.HasValue)
                    {
                        NativeCICPColorData colorData = colorConversionInfo.Value.ToNative();

                        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                        {
                            status = AvifNative_64.DecompressColorImage(ptr,
                                                                        colorImageSize,
                                                                        colorData,
                                                                        ref nativeDecodeInfo,
                                                                        bitmapData);
                        }
                        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                        {
                            status = AvifNative_ARM64.DecompressColorImage(ptr,
                                                                           colorImageSize,
                                                                           colorData,
                                                                           ref nativeDecodeInfo,
                                                                           bitmapData);
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
                            status = AvifNative_64.DecompressColorImage(ptr,
                                                                        colorImageSize,
                                                                        IntPtr.Zero,
                                                                        ref nativeDecodeInfo,
                                                                        bitmapData);
                        }
                        else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                        {
                            status = AvifNative_ARM64.DecompressColorImage(ptr,
                                                                           colorImageSize,
                                                                           IntPtr.Zero,
                                                                           ref nativeDecodeInfo,
                                                                           bitmapData);
                        }
                        else
                        {
                            throw new PlatformNotSupportedException();
                        }
                    }

                    if (status == DecoderStatus.Ok)
                    {
                        decodeInfo.CopyFromNative(nativeDecodeInfo);
                    }
                    else
                    {
                        HandleError(status);
                    }
                });
            }
        }

        public static void DecompressAlpha(AvifItemData alphaImage,
                                           DecodeInfo decodeInfo,
                                           Surface fullSurface)
        {
            if (alphaImage is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(alphaImage));
            }

            if (decodeInfo is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(decodeInfo));
            }

            if (fullSurface is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(fullSurface));
            }

            unsafe
            {
                alphaImage.UseBufferPointer((ptr, length) =>
                {
                    BitmapData bitmapData = new BitmapData
                    {
                        scan0 = fullSurface.Scan0.Pointer,
                        width = (uint)fullSurface.Width,
                        height = (uint)fullSurface.Height,
                        stride = (uint)fullSurface.Stride
                    };

                    UIntPtr alphaImageSize = new UIntPtr(length);
                    DecoderStatus status = DecoderStatus.Ok;
                    NativeDecodeInfo nativeDecodeInfo = decodeInfo.ToNative();

                    if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    {
                        status = AvifNative_64.DecompressAlphaImage(ptr,
                                                                    alphaImageSize,
                                                                    ref nativeDecodeInfo,
                                                                    bitmapData);
                    }
                    else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    {
                        status = AvifNative_ARM64.DecompressAlphaImage(ptr,
                                                                        alphaImageSize,
                                                                        ref nativeDecodeInfo,
                                                                        bitmapData);
                    }
                    else
                    {
                        throw new PlatformNotSupportedException();
                    }

                    if (status == DecoderStatus.Ok)
                    {
                        decodeInfo.CopyFromNative(nativeDecodeInfo);
                    }
                    else
                    {
                        HandleError(status);
                    }
                });
            }
        }

        public static bool MemoryBlocksAreEqual(IntPtr buffer1, IntPtr buffer2, ulong length)
        {
            bool result;

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                result = AvifNative_64.MemoryBlocksAreEqual(buffer1, buffer2, new UIntPtr(length));
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                result = AvifNative_ARM64.MemoryBlocksAreEqual(buffer1, buffer2, new UIntPtr(length));
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            return result;
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

        private static void HandleError(EncoderStatus status, ExceptionDispatchInfo exceptionDispatchInfo)
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
                case DecoderStatus.AlphaSizeMismatch:
                    throw new FormatException("The alpha image does not match the expected size.");
                case DecoderStatus.ColorSizeMismatch:
                    throw new FormatException("The color image does not match the expected size.");
                case DecoderStatus.TileNclxProfileMismatch:
                    throw new FormatException("The color image tiles must use an identical color profile.");
                case DecoderStatus.UnsupportedBitDepth:
                    throw new FormatException("The image has an unsupported bit depth.");
                case DecoderStatus.UnknownYUVFormat:
                    throw new FormatException("The YUV format is not supported by the decoder.");
                case DecoderStatus.TileFormatMismatch:
                    throw new FormatException("The color image tiles must use the same YUV format and bit depth.");
                default:
                    throw new FormatException("An unknown error occurred when decoding the image.");
            }
        }
    }
}
