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
                // The debugger watch windows will truncate the string
                // if it contains embedded NULs.
                return ToString().Replace('\0', ' ');
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
            char[] chars = new char[]
            {
                (char)((value >> 24) & 0xff),
                (char)((value >> 16) & 0xff),
                (char)((value >> 8) & 0xff),
                (char)(value & 0xff)
            };

            return new string(chars);
        }
    }
}
