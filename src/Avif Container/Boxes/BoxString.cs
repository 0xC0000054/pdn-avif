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

using System;
using System.Diagnostics;
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

        public override bool Equals(object obj)
        {
            return obj is BoxString other && Equals(other);
        }

        public bool Equals(BoxString other)
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
            writer.Write(Encoding.UTF8.GetBytes(this.Value));
            writer.Write((byte)0);
        }

        public static bool operator ==(BoxString left, BoxString right)
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

        public static bool operator !=(BoxString left, BoxString right)
        {
            return !(left == right);
        }

        public static implicit operator BoxString(string value)
        {
            return value is null ? null : new BoxString(value);
        }

        public static implicit operator string(BoxString value)
        {
            return value?.Value;
        }
    }
}
