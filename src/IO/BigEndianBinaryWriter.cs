﻿////////////////////////////////////////////////////////////////////////
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

using AvifFileType.AvifContainer;
using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AvifFileType
{
    internal sealed class BigEndianBinaryWriter
        : Disposable
    {
        private readonly Stream stream;
        private readonly bool leaveOpen;

        public BigEndianBinaryWriter(Stream stream, bool leaveOpen)
        {
            if (stream is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(stream));
            }

            this.stream = stream;
            this.leaveOpen = leaveOpen;
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

        public void Write(byte value)
        {
            VerifyNotDisposed();

            this.stream.WriteByte(value);
        }

        public void Write(short value)
        {
            Write((ushort)value);
        }

        [SkipLocalsInit]
        public void Write(ushort value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[2];

            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);

            this.stream.Write(buffer);
        }

        public void Write(int value)
        {
            Write((uint)value);
        }

        [SkipLocalsInit]
        public void Write(uint value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[4];

            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);

            this.stream.Write(buffer);
        }

        public void Write(long value)
        {
            Write((ulong)value);
        }

        [SkipLocalsInit]
        public void Write(ulong value)
        {
            VerifyNotDisposed();

            Span<byte> buffer = stackalloc byte[8];

            BinaryPrimitives.WriteUInt64BigEndian(buffer, value);

            this.stream.Write(buffer);
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

        public void Write(ReadOnlySpan<byte> buffer)
        {
            VerifyNotDisposed();

            this.stream.Write(buffer);
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

            const int MaxBufferSize = 1024 * 1024;
            int bufferSize = (int)Math.Min(length, MaxBufferSize);

            using (SpanOwner<byte> poolBuffer = SpanOwner<byte>.Allocate(bufferSize))
            {
                Span<byte> writeBuffer = poolBuffer.Span;

                byte* readPtr = null;
                try
                {
                    buffer.AcquirePointer(ref readPtr);

                    ulong totalBytesRead = 0;

                    while (totalBytesRead < length)
                    {
                        int bytesRead = (int)Math.Min(length - totalBytesRead, MaxBufferSize);

                        new ReadOnlySpan<byte>(readPtr + totalBytesRead, bytesRead).CopyTo(writeBuffer);

                        this.stream.Write(writeBuffer.Slice(0, bytesRead));

                        totalBytesRead += (ulong)bytesRead;
                    }
                }
                finally
                {
                    if (readPtr != null)
                    {
                        buffer.ReleasePointer();
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!this.leaveOpen)
                {
                    this.stream.Dispose();
                }
            }
        }
    }
}
