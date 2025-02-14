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

using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.IO;

namespace AvifFileType
{
    internal sealed class ManagedAvifItemData
        : AvifItemData
    {
        private readonly MemoryOwner<byte> buffer;

        public ManagedAvifItemData(int length)
            : base()
        {
            this.buffer = MemoryOwner<byte>.Allocate(length);
            this.Length = (ulong)length;
        }

        internal Span<byte> AsSpan()
        {
            VerifyNotDisposed();

            return this.buffer.Span;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.buffer.Dispose();
            }
        }

        protected override Stream GetStreamImpl()
        {
            // The buffer is cast to ReadOnlyMemory<byte> to prevent the returned stream
            // from taking ownership of the buffer, and to ensure that the caller cannot
            // modify the data.
            return ((ReadOnlyMemory<byte>)this.buffer.Memory).AsStream();
        }

        protected override unsafe void UseBufferPointerImpl(UseBufferPointerDelegate action)
        {
            fixed (byte* ptr = this.buffer.Span)
            {
                action(ptr, this.Length);
            }
        }
    }
}
