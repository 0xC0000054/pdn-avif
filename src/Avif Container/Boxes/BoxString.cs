////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{Value}")]
    internal sealed class BoxString
        : IEquatable<BoxString>
    {
        public static readonly BoxString Empty = new BoxString(string.Empty);

        public BoxString(string value)
        {
            if (value is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(value));
            }

            this.Value = value;
        }

        public string Value { get; }

        public override bool Equals(object? obj)
        {
            return obj is BoxString other && Equals(other);
        }

        public bool Equals(BoxString? other)
        {
            if (other is null)
            {
                return false;
            }

            return string.Equals(this.Value, other.Value, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return unchecked(-1937169414 + this.Value.GetHashCode());
        }

        public ulong GetSize()
        {
            return (ulong)Encoding.UTF8.GetByteCount(this.Value) + 1;
        }

        public override string ToString()
        {
            return this.Value;
        }

        public void Write(BigEndianBinaryWriter writer)
        {
            if (string.IsNullOrEmpty(this.Value))
            {
                // For empty strings we only need to write the null-terminator.
                writer.Write((byte)0);
            }
            else
            {
                WriteString(writer, this.Value);
            }

            [SkipLocalsInit]
            static void WriteString(BigEndianBinaryWriter writer, string value)
            {
                const int MaxStackBufferSize = 256;

                Span<byte> buffer = stackalloc byte[MaxStackBufferSize];
                byte[]? arrayFromPool = null;

                try
                {
                    int stringLengthWithTerminator = checked(Encoding.UTF8.GetByteCount(value) + 1);

                    if (stringLengthWithTerminator > MaxStackBufferSize)
                    {
                        arrayFromPool = ArrayPool<byte>.Shared.Rent(stringLengthWithTerminator);
                        buffer = arrayFromPool;
                    }

                    int bytesWritten = Encoding.UTF8.GetBytes(value, buffer);

                    buffer[bytesWritten] = 0;

                    writer.Write(buffer.Slice(0, stringLengthWithTerminator));
                }
                finally
                {
                    if (arrayFromPool != null)
                    {
                        ArrayPool<byte>.Shared.Return(arrayFromPool);
                    }
                }
            }
        }

        public static bool operator ==(BoxString? left, BoxString? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(BoxString? left, BoxString? right)
        {
            return !(left == right);
        }

        public static implicit operator BoxString?(string? value)
        {
            return value is null ? null : new BoxString(value);
        }

        public static implicit operator string?(BoxString? value)
        {
            return value?.Value;
        }
    }
}
