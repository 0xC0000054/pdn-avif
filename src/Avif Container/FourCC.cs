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
using System.Diagnostics;
using System.Text;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal readonly struct FourCC
        : IEquatable<FourCC>
    {
        public const int SizeOf = sizeof(uint);

        public FourCC(char first, char second, char third, char fourth)
        {
            this.Value = (uint)((first << 24) | (second << 16) | (third << 8) | fourth);
        }

        public FourCC(uint value)
        {
            this.Value = value;
        }

        public uint Value { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return ToString();
            }
        }

        public override bool Equals(object obj)
        {
            return obj is FourCC other && Equals(other);
        }

        public bool Equals(FourCC other)
        {
            return this.Value == other.Value;
        }

        public override int GetHashCode()
        {
            return unchecked(-1584136870 + this.Value.GetHashCode());
        }

        public static bool operator ==(FourCC left, FourCC right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(FourCC left, FourCC right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            uint value = this.Value;

            StringBuilder builder = new StringBuilder(20);
            builder.Append('\'');

            for (int i = 3; i >= 0; i--)
            {
                uint c = (value >> (i * 8)) & 0xff;

                // Ignore any bytes that are not printable ASCII characters
                // because they can not be displayed in the debugger watch windows.

                if (c >= 0x20 && c <= 0x7e)
                {
                    builder.Append((char)c);
                }
            }
            builder.Append('\'');
            builder.Append(" (0x").Append(value.ToString("X8")).Append(')');

            return builder.ToString();
        }
    }
}
