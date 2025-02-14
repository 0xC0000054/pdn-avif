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

using AvifFileType.AvifContainer;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace AvifFileType
{
    [DebuggerTypeProxy(typeof(EndianBinaryReaderSegmentDebugView))]
    internal readonly struct EndianBinaryReaderSegment
    {
        private readonly EndianBinaryReader reader;
        private readonly long startOffset;
#pragma warning disable IDE0032 // Use auto property
        private readonly long endOffset;
#pragma warning restore IDE0032 // Use auto property

        public EndianBinaryReaderSegment(EndianBinaryReader reader, long startOffset, long length)
        {
            if (reader is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(reader));
            }

            this.reader = reader;
            this.startOffset = startOffset;
            this.endOffset = checked(startOffset + length);
        }

        /// <summary>
        /// Gets the end offset.
        /// </summary>
        /// <value>
        /// The end offset.
        /// </value>
        public long EndOffset => this.endOffset;

        /// <summary>
        /// Gets or sets the position.
        /// </summary>
        /// <value>
        /// The position.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The value is less that the segment start offset.
        /// -or-
        /// The value is greater than the segment end offset.
        /// </exception>
        public long Position
        {
            get => this.reader.Position;
            set
            {
                long current = this.Position;

                if (value != current)
                {
                    if (value < this.startOffset)
                    {
                        ExceptionUtil.ThrowArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture,
                                                                                     "The value is less than the segment start offset, value: 0x{0:X} startOffset: 0x{1:X}",
                                                                                     value,
                                                                                     this.startOffset));
                    }

                    if (value > this.endOffset)
                    {
                        ExceptionUtil.ThrowArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture,
                                                                                     "The value is greater than the segment end offset, value: 0x{0:X} endOffset: 0x{1:X}",
                                                                                     value,
                                                                                     this.endOffset));
                    }

                    this.reader.Position = value;
                }
            }
        }

        /// <summary>
        /// Creates the child segment.
        /// </summary>
        /// <param name="header">The header.</param>
        /// <returns>The created child segment.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="header"/> is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// The Box specified by <paramref name="header"/> is not located within this instance.
        /// </exception>
        public EndianBinaryReaderSegment CreateChildSegment(Box header)
        {
            if (header is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(header));
            }

            long startOffset = header.DataStartOffset;
            long length = header.DataLength;

            try
            {
                if (startOffset < this.startOffset || checked(startOffset + length) > this.endOffset)
                {
                    ExceptionUtil.ThrowInvalidOperationException($"The Box data is not located in this { nameof(EndianBinaryReaderSegment) }.");
                }
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException($"The Box data is not located in this { nameof(EndianBinaryReaderSegment) }.", ex);
            }

            return new EndianBinaryReaderSegment(this.reader, startOffset, length);
        }

        /// <summary>
        /// Reads a sequence of bytes from the stream, starting from a specified point in the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The starting offset in the array.</param>
        /// <param name="count">The count.</param>
        /// <returns>The number of bytes read from the stream.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int Read(byte[] bytes, int offset, int count)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            return Read(new Span<byte>(bytes, offset, count));
        }

        /// <summary>
        /// Reads a sequence of bytes from the stream.
        /// </summary>
        /// <param name="buffer">The destination buffer.</param>
        /// <returns>The number of bytes read from the stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int Read(Span<byte> buffer)
        {
            CheckReadBounds(buffer.Length);

            return this.reader.Read(buffer);
        }

        /// <summary>
        /// Reads a null-terminated UTF-8 string from the stream.
        /// </summary>
        /// <param name="endOffset">The offset that marks the end of the null-terminator search area.</param>
        /// <returns>The string.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="IOException">The string is longer than <see cref="int.MaxValue"/>.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public BoxString ReadBoxString()
        {
            return this.reader.ReadBoxString(this.endOffset);
        }

        /// <summary>
        /// Reads the next byte from the current stream.
        /// </summary>
        /// <returns>The next byte read from the current stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public byte ReadByte()
        {
            CheckReadBounds(sizeof(byte));

            return this.reader.ReadByte();
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="count">The number of bytes to read..</param>
        /// <returns>An array containing the specified bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</except
        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(count));
            }

            CheckReadBounds(count);

            return this.reader.ReadBytes(count);
        }

        /// <summary>
        /// Reads a 8-byte floating point value.
        /// </summary>
        /// <returns>The 8-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public double ReadDouble()
        {
            CheckReadBounds(sizeof(double));

            return this.reader.ReadDouble();
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream, starting from a specified point in the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The starting offset in the array.</param>
        /// <param name="count">The count.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe void ReadExactly(byte[] bytes, int offset, int count)
        {
            if (bytes is null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            ReadExactly(new Span<byte>(bytes, offset, count));
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe void ReadExactly(Span<byte> buffer)
        {
            CheckReadBounds(buffer.Length);

            this.reader.ReadExactly(buffer);
        }

        /// <summary>
        /// Reads a four-character code from the stream.
        /// </summary>
        /// <returns>The four-character code.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public FourCC ReadFourCC()
        {
            CheckReadBounds(FourCC.SizeOf);

            return this.reader.ReadFourCC();
        }

        /// <summary>
        /// Reads a 2-byte signed integer.
        /// </summary>
        /// <returns>The 2-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        /// <summary>
        /// Reads a 4-byte signed integer.
        /// </summary>
        /// <returns>The 4-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        /// <summary>
        /// Reads a 8-byte signed integer.
        /// </summary>
        /// <returns>The 8-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long ReadInt64()
        {
            return (long)ReadUInt64();
        }

        /// <summary>
        /// Reads a 4-byte floating point value.
        /// </summary>
        /// <returns>The 4-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public float ReadSingle()
        {
            CheckReadBounds(sizeof(float));

            return this.reader.ReadSingle();
        }

        /// <summary>
        /// Reads a 2-byte unsigned integer.
        /// </summary>
        /// <returns>The 2-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ushort ReadUInt16()
        {
            CheckReadBounds(sizeof(ushort));

            return this.reader.ReadUInt16();
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer.
        /// </summary>
        /// <returns>The 4-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public uint ReadUInt32()
        {
            CheckReadBounds(sizeof(uint));

            return this.reader.ReadUInt32();
        }

        /// <summary>
        /// Reads a 8-byte unsigned integer.
        /// </summary>
        /// <returns>The 8-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ulong ReadUInt64()
        {
            CheckReadBounds(sizeof(ulong));

            return this.reader.ReadUInt64();
        }

        /// <summary>
        /// Ensures that the requested number of bytes is available in the <see cref="EndianBinaryReaderSegment" />.
        /// </summary>
        /// <param name="count">The requested number of bytes.</param>
        /// <exception cref="FormatException">The requested number of bytes is greater than the segment length.</exception>
        private void CheckReadBounds(int count)
        {
            if ((this.Position + count) > this.endOffset)
            {
                ExceptionUtil.ThrowFormatException(string.Format(CultureInfo.InvariantCulture,
                                                                 "The requested number of bytes is greater than the segment length, count: {0} currentOffset: 0x{1:X}, endOffset: 0x{2:X}",
                                                                 count,
                                                                 this.Position,
                                                                 this.endOffset));
            }
        }

        private sealed class EndianBinaryReaderSegmentDebugView
        {
            private readonly EndianBinaryReaderSegment segment;

            public EndianBinaryReaderSegmentDebugView(EndianBinaryReaderSegment segment)
            {
                this.segment = segment;
            }

            public long StartOffset => this.segment.startOffset;

            public long EndOffset => this.segment.endOffset;

            public long Position => this.segment.Position;
        }
    }
}
