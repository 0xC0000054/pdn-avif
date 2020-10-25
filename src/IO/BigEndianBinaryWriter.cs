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

using AvifFileType.AvifContainer;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AvifFileType
{
    internal sealed class BigEndianBinaryWriter
        : IDisposable
    {
        private Stream stream;
        private readonly bool leaveOpen;
        private readonly byte[] buffer;
        private readonly IByteArrayPool arrayPool;

        public BigEndianBinaryWriter(Stream stream, bool leaveOpen, IByteArrayPool arrayPool)
        {
            if (stream is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(stream));
            }

            this.stream = stream;
            this.leaveOpen = leaveOpen;
            this.buffer = new byte[sizeof(ulong)];
            this.arrayPool = arrayPool;
        }

        public long Position
        {
            get
            {
                VerifyNotDisposed();
                return this.stream.Position;
            }
            set
            {
                VerifyNotDisposed();
                this.stream.Position = value;
            }
        }

        public void Dispose()
        {
            if (this.stream != null)
            {
                if (!this.leaveOpen)
                {
                    this.stream.Dispose();
                }

                this.stream = null;
            }
        }

        public void Write(byte value)
        {
            VerifyNotDisposed();

            this.stream.WriteByte(value);
        }

        public void Write(short value)
        {
            Write((ushort)value);
        }

        public void Write(ushort value)
        {
            VerifyNotDisposed();

            this.buffer[0] = (byte)((value >> 8) & 0xff);
            this.buffer[1] = (byte)(value & 0xff);

            this.stream.Write(this.buffer, 0, 2);
        }

        public void Write(int value)
        {
            Write((uint)value);
        }

        public void Write(uint value)
        {
            VerifyNotDisposed();

            this.buffer[0] = (byte)((value >> 24) & 0xff);
            this.buffer[1] = (byte)((value >> 16) & 0xff);
            this.buffer[2] = (byte)((value >> 8) & 0xff);
            this.buffer[3] = (byte)(value & 0xff);

            this.stream.Write(this.buffer, 0, 4);
        }

        public void Write(long value)
        {
            Write((ulong)value);
        }

        public void Write(ulong value)
        {
            VerifyNotDisposed();

            this.buffer[0] = (byte)((value >> 56) & 0xff);
            this.buffer[1] = (byte)((value >> 48) & 0xff);
            this.buffer[2] = (byte)((value >> 40) & 0xff);
            this.buffer[3] = (byte)((value >> 32) & 0xff);
            this.buffer[4] = (byte)((value >> 24) & 0xff);
            this.buffer[5] = (byte)((value >> 16) & 0xff);
            this.buffer[6] = (byte)((value >> 8) & 0xff);
            this.buffer[7] = (byte)(value & 0xff);

            this.stream.Write(this.buffer, 0, 8);
        }

        public void Write(FourCC fourCC)
        {
            Write(fourCC.Value);
        }

        public void Write(byte[] bytes)
        {
            if (bytes is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(bytes));
            }

            Write(bytes, 0, bytes.Length);
        }

        public void Write(byte[] bytes, int offset, int count)
        {
            VerifyNotDisposed();

            this.stream.Write(bytes, offset, count);
        }

        public unsafe void Write(SafeBuffer buffer)
        {
            if (buffer is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(buffer));
            }

            VerifyNotDisposed();

            ulong length = buffer.ByteLength;

            if (length == 0)
            {
                return;
            }

            // The largest multiple of 4096 that is under the large object heap limit.
            const int MaxBufferSize = 81920;

            int bufferSize = (int)Math.Min(length, MaxBufferSize);
            byte[] writeBuffer = this.arrayPool.Rent(bufferSize);

            byte* readPtr = null;
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                buffer.AcquirePointer(ref readPtr);

                fixed (byte* writePtr = writeBuffer)
                {
                    ulong totalBytesRead = 0;

                    while (totalBytesRead < length)
                    {
                        ulong bytesRead = Math.Min(length - totalBytesRead, MaxBufferSize);

                        Buffer.MemoryCopy(readPtr + totalBytesRead, writePtr, bytesRead, bytesRead);

                        this.stream.Write(writeBuffer, 0, (int)bytesRead);

                        totalBytesRead += bytesRead;
                    }
                }
            }
            finally
            {
                if (readPtr != null)
                {
                    buffer.ReleasePointer();
                }
            }

            this.arrayPool.Return(writeBuffer);
        }

        private void VerifyNotDisposed()
        {
            if (this.stream is null)
            {
                ExceptionUtil.ThrowObjectDisposedException(nameof(BigEndianBinaryWriter));
            }
        }
    }
}
