////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;

namespace AvifFileType.Interop
{
    internal sealed class UnmanagedCompressedAV1Data
        : CompressedAV1Data
    {
        private SafeNativeMemoryBuffer buffer;

        public UnmanagedCompressedAV1Data(ulong size)
            : base(size)
        {
            this.buffer = SafeNativeMemoryBuffer.Create(size);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.buffer != null)
                {
                    this.buffer.Dispose();
                    this.buffer = null;
                }
            }

            base.Dispose(disposing);
        }

        protected override IntPtr PinBuffer()
        {
            return this.buffer.DangerousGetHandle();
        }

        protected override void UnpinBuffer()
        {
        }

        protected override void WriteBuffer(BigEndianBinaryWriter writer)
        {
            writer.Write(this.buffer);
        }
    }
}
