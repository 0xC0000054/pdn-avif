////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.Interop;
using System;
using System.Diagnostics;

namespace AvifFileType
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal sealed class CompressedAV1Image
        : IDisposable
    {
        private CompressedAV1Data data;

        public CompressedAV1Image(CompressedAV1Data data, int width, int height, YUVChromaSubsampling format)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            this.data = data;
            this.Width = width;
            this.Height = height;
            this.Format = format;
        }

        public CompressedAV1Data Data
        {
            get
            {
                VerifyNotDisposed();

                return this.data;
            }
        }

        public int Width { get; }

        public int Height { get; }

        public YUVChromaSubsampling Format { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                string yuvFormat;
                switch (this.Format)
                {
                    case YUVChromaSubsampling.Subsampling420:
                        yuvFormat = "YUV 4:2:0";
                        break;
                    case YUVChromaSubsampling.Subsampling422:
                        yuvFormat = "YUV 4:2:2";
                        break;
                    case YUVChromaSubsampling.Subsampling444:
                        yuvFormat = "YUV 4:4:4";
                        break;
                    case YUVChromaSubsampling.Subsampling400:
                        yuvFormat = "YUV 4:0:0";
                        break;
                    case YUVChromaSubsampling.IdentityMatrix:
                        yuvFormat = "Identity matrix";
                        break;
                    default:
                        yuvFormat = "Unknown";
                        break;
                }
                string dataLength = this.data != null ? $"{ this.data.ByteLength } bytes" : "Disposed";

                return $"Width: { this.Width }, Height: { this.Height }, Format: { yuvFormat }, Data: { dataLength }";
            }
        }

        public void Dispose()
        {
            if (this.data != null)
            {
                this.data.Dispose();
                this.data = null;
            }
        }

        private void VerifyNotDisposed()
        {
            if (this.data is null)
            {
                ExceptionUtil.ThrowObjectDisposedException(nameof(CompressedAV1Image));
            }
        }
    }
}
