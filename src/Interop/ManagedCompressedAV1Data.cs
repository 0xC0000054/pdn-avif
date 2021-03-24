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

using System;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal sealed class ManagedCompressedAV1Data
        : CompressedAV1Data
    {
        private readonly byte[] buffer;
        private GCHandle gcHandle;

        public ManagedCompressedAV1Data(ulong size)
            : base(size)
        {
            this.buffer = new byte[size];
        }

        ~ManagedCompressedAV1Data()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (this.gcHandle.IsAllocated)
            {
                this.gcHandle.Free();
            }

            base.Dispose(disposing);
        }

        protected override IntPtr PinBuffer()
        {
            if (!this.gcHandle.IsAllocated)
            {
                this.gcHandle = GCHandle.Alloc(this.buffer, GCHandleType.Pinned);
            }

            return this.gcHandle.AddrOfPinnedObject();
        }

        protected override void UnpinBuffer()
        {
            if (this.gcHandle.IsAllocated)
            {
                this.gcHandle.Free();
            }
        }

        protected override void WriteBuffer(BigEndianBinaryWriter writer)
        {
            writer.Write(this.buffer, 0, this.buffer.Length);
        }
    }
}
