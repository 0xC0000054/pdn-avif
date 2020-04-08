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
    internal sealed class DecodedBGRAImage : IDisposable
    {
        private SafeDecodedImage data;

        public DecodedBGRAImage(SafeDecodedImage data, DecodedImageInfo info)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            this.data = data;
            this.Width = info.width;
            this.Height = info.height;
            this.Stride = info.stride.ToUInt64();
        }

        public SafeDecodedImage Data
        {
            get
            {
                VerifyNotDisposed();

                return this.data;
            }
        }

        public uint Width { get; }

        public uint Height { get; }

        public ulong Stride { get; }

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
