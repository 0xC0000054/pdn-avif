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
using System;

namespace AvifFileType
{
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
