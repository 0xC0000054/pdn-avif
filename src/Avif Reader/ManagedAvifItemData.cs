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

using PaintDotNet;
using PaintDotNet.AppModel;
using System.IO;

namespace AvifFileType
{
    internal sealed class ManagedAvifItemData
        : AvifItemData
    {
        private IArrayPoolBuffer<byte> bufferFromArrayPool;

        public ManagedAvifItemData(int length, IArrayPoolService pool)
            : base()
        {
            this.bufferFromArrayPool = pool.Rent<byte>(length);
            this.Length = (ulong)length;
        }

        internal byte[] GetBuffer()
        {
            VerifyNotDisposed();

            return this.bufferFromArrayPool.Array;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposableUtil.Free(ref this.bufferFromArrayPool);
            }

            base.Dispose(disposing);
        }

        protected override Stream GetStreamImpl()
        {
            return new MemoryStream(this.bufferFromArrayPool.Array, 0, (int)this.Length, writable: false);
        }

        protected override unsafe void UseBufferPointerImpl(UseBufferPointerDelegate action)
        {
            fixed (byte* ptr = this.bufferFromArrayPool.Array)
            {
                action(ptr, this.Length);
            }
        }
    }
}
