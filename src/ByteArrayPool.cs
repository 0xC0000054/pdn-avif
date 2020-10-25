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

using System.Buffers;

namespace AvifFileType
{
    internal sealed class ByteArrayPool : IByteArrayPool
    {
        private readonly ArrayPool<byte> arrayPool;

        public ByteArrayPool()
        {
            // We reduce the max array length to 131,072 bytes from the default of 1 MB,
            // and reduce the max number of pooled arrays to 25 from the default of 50.
            //
            // In practice only one pooled array should be in use at a time as loading and saving is
            // not multi-threaded, and any buffer over 85,000 bytes will be allocated in unmanaged memory.
            this.arrayPool = ArrayPool<byte>.Create(131072, 25);
        }

        public byte[] Rent(int minimumSize)
        {
            return this.arrayPool.Rent(minimumSize);
        }

        public void Return(byte[] buffer)
        {
            this.arrayPool.Return(buffer);
        }
    }
}
