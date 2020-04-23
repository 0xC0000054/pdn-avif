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
    internal readonly struct Rational
        : IEquatable<Rational>
    {
        public Rational(EndianBinaryReader reader)
        {
            this.Numerator = reader.ReadInt32();
            this.Denominator = reader.ReadUInt32();
        }

        public int Numerator { get; }

        public uint Denominator { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"Numerator: {this.Numerator}, Denominator: {this.Denominator}";
            }
        }

        public override bool Equals(object obj)
        {
            return obj is Rational other && Equals(other);
        }

        public bool Equals(Rational other)
        {
            return this.Numerator == other.Numerator && this.Denominator == other.Denominator;
        }

        public override int GetHashCode()
        {
            int hashCode = -671859081;

            unchecked
            {
                hashCode = (hashCode * -1521134295) + this.Numerator.GetHashCode();
                hashCode = (hashCode * -1521134295) + this.Denominator.GetHashCode();
            }

            return hashCode;
        }

        public double ToDouble()
        {
            if (this.Denominator == 0)
            {
                ExceptionUtil.ThrowInvalidOperationException("The Rational denominator is zero.");
            }

            return (double)this.Numerator / this.Denominator;
        }

        public int ToInt32()
        {
            return (int)Math.Round(ToDouble());
        }

        public static bool operator ==(Rational left, Rational right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Rational left, Rational right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return this.Numerator.ToString() + "/" + this.Denominator.ToString();
        }
    }
}
