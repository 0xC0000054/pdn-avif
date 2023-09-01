////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvifFileType
{
    internal sealed class StreamSegment : Stream
    {
        private Stream stream;
        private bool isOpen;
        private readonly long origin;
        private readonly long length;
        private readonly bool leaveOpen;

        public StreamSegment(Stream stream, long origin, long length, bool leaveOpen = false)
        {
            if (stream is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(stream));
            }

            if (!stream.CanRead || !stream.CanSeek)
            {
                ExceptionUtil.ThrowArgumentException("The stream must support reading and seeking.");
            }

            if (origin < 0)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(origin), "Must be >= 0.");
            }

            if (length <= 0)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(origin), "Must be > 0.");
            }

            if (checked(origin + length) > stream.Length)
            {
                ExceptionUtil.ThrowArgumentException("Invalid stream origin and length.");
            }

            this.stream = stream;
            this.origin = this.stream.Position = origin;
            this.length = origin + length;
            this.leaveOpen = leaveOpen;
            this.isOpen = true;
        }

        public override bool CanRead => this.isOpen;

        public override bool CanSeek => this.isOpen;

        public override bool CanTimeout => this.isOpen && this.stream.CanTimeout;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                VerifyNotDisposed();

                return this.length - this.origin;
            }
        }

        public override long Position
        {
            get
            {
                VerifyNotDisposed();

                return this.stream.Position - this.origin;
            }
            set
            {
                VerifyNotDisposed();

                long current = this.Position;

                if (value != current)
                {
                    long newPosition = unchecked(this.origin + value);

                    if (newPosition < this.origin)
                    {
                        ExceptionUtil.ThrowArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture,
                                                                                     "The value is less than the segment origin, value: 0x{0:X} origin: 0x{1:X}",
                                                                                     newPosition,
                                                                                     this.origin));
                    }

                    if (newPosition > this.length)
                    {
                        ExceptionUtil.ThrowArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture,
                                                                                     "The value is greater than the segment length, value: 0x{0:X} length: 0x{1:X}",
                                                                                     newPosition,
                                                                                     this.length));
                    }

                    this.stream.Position = newPosition;
                }
            }
        }

        public override int ReadTimeout
        {
            get
            {
                VerifyNotDisposed();

                return this.stream.ReadTimeout;
            }
            set
            {
                VerifyNotDisposed();

                this.stream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                VerifyNotDisposed();

                return this.stream.WriteTimeout;
            }
            set
            {
                VerifyNotDisposed();

                this.stream.WriteTimeout = value;
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            VerifyNotDisposed();

            if (count < 0)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(count), "Must be >= 0.");
            }

            if ((this.Position + count) > this.Length)
            {
                count = (int)(this.Length - this.Position);
            }

            return this.stream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            VerifyNotDisposed();
            throw new NotSupportedException("The StreamSegment is read-only.");
        }

        public override void Close() => this.stream?.Close();

        public override void CopyTo(Stream destination, int bufferSize)
        {
            VerifyNotDisposed();

            this.stream.CopyTo(destination, bufferSize);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            VerifyNotDisposed();

            return this.stream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override ValueTask DisposeAsync() => this.stream?.DisposeAsync() ?? ValueTask.CompletedTask;

        public override int EndRead(IAsyncResult asyncResult)
        {
            VerifyNotDisposed();

            return this.stream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            VerifyNotDisposed();

            this.stream.EndWrite(asyncResult);
        }

        public override void Flush()
        {
            // No-op because the stream is read-only.
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            VerifyNotDisposed();

            // No-op because the stream is read-only.
            return cancellationToken.IsCancellationRequested ? Task.FromCanceled(cancellationToken) : Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            VerifyNotDisposed();

            if (count < 0)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(count), "Must be >= 0.");
            }

            if ((this.Position + count) > this.Length)
            {
                count = (int)(this.Length - this.Position);
            }

            return this.stream.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            VerifyNotDisposed();

            int count = buffer.Length;

            if ((this.Position + count) > this.Length)
            {
                count = (int)(this.Length - this.Position);
            }

            return this.stream.Read(buffer.Slice(0, count));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            VerifyNotDisposed();

            if (count < 0)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(count), "Must be >= 0.");
            }

            if ((this.Position + count) > this.Length)
            {
                count = (int)(this.Length - this.Position);
            }

            return this.stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            VerifyNotDisposed();

            int count = buffer.Length;

            if ((this.Position + count) > this.Length)
            {
                count = (int)(this.Length - this.Position);
            }

            return this.stream.ReadAsync(buffer.Slice(0, count), cancellationToken);
        }

        public override int ReadByte()
        {
            VerifyNotDisposed();

            if ((this.Position + sizeof(byte)) > this.Length)
            {
                return -1;
            }

            return this.stream.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            VerifyNotDisposed();

            long tempPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    tempPosition = unchecked(this.origin + offset);
                    break;
                case SeekOrigin.Current:
                    tempPosition = unchecked(this.Position + offset);
                    break;
                case SeekOrigin.End:
                    tempPosition = unchecked(this.length + offset);
                    break;
                default:
                    throw new ArgumentException("Unknown SeekOrigin value.");
            }

            if (tempPosition < this.origin || tempPosition > this.length)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(offset), "The offset is not within the stream segment.");
            }

            return this.stream.Seek(tempPosition, origin);
        }

        public override void SetLength(long value)
        {
            VerifyNotDisposed();
            StreamSegmentReadOnly();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            VerifyNotDisposed();
            StreamSegmentReadOnly();
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            VerifyNotDisposed();
            StreamSegmentReadOnly();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            VerifyNotDisposed();
            throw new NotSupportedException("The StreamSegment is read-only.");
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            VerifyNotDisposed();
            throw new NotSupportedException("The StreamSegment is read-only.");
        }

        public override void WriteByte(byte value)
        {
            VerifyNotDisposed();
            StreamSegmentReadOnly();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.isOpen = false;

                if (!this.leaveOpen)
                {
                    this.stream.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private static void StreamSegmentReadOnly()
        {
            throw new NotSupportedException("The StreamSegment is read-only.");
        }

        private void VerifyNotDisposed()
        {
            if (!this.isOpen)
            {
                ExceptionUtil.ThrowObjectDisposedException(nameof(StreamSegment));
            }
        }
    }
}
