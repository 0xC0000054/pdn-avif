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

namespace AvifFileType.AvifContainer
{
    internal readonly struct Rational : IEquatable<Rational>
    {
        private readonly int numerator;
        private readonly uint denominator;

        public Rational(EndianBinaryReader reader)
        {
            this.numerator = reader.ReadInt32();
            this.denominator = reader.ReadUInt32();
        }

        public override bool Equals(object obj)
        {
            return obj is Rational other && Equals(other);
        }

        public bool Equals(Rational other)
        {
            return this.numerator == other.numerator && this.denominator == other.denominator;
        }

        public override int GetHashCode()
        {
            int hashCode = -671859081;

            unchecked
            {
                hashCode = (hashCode * -1521134295) + this.numerator.GetHashCode();
                hashCode = (hashCode * -1521134295) + this.denominator.GetHashCode();
            }

            return hashCode;
        }

        public double ToDouble()
        {
            if (this.denominator == 0)
            {
                return 0.0;
            }

            return (double)this.numerator / this.denominator;
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
    }
}
