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
using System.Diagnostics.CodeAnalysis;

namespace AvifFileType
{
    internal struct CICPColorData : IEquatable<CICPColorData>
    {
        public CICPColorPrimaries colorPrimaries;
        public CICPTransferCharacteristics transferCharacteristics;
        public CICPMatrixCoefficients matrixCoefficients;
        public bool fullRange;

        public CICPColorData(CICPColorPrimaries colorPrimaries,
                             CICPTransferCharacteristics transferCharacteristics,
                             CICPMatrixCoefficients matrixCoefficients,
                             bool fullRange)
        {
            this.colorPrimaries = colorPrimaries;
            this.transferCharacteristics = transferCharacteristics;
            this.matrixCoefficients = matrixCoefficients;
            this.fullRange = fullRange;
        }

        public override readonly bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is CICPColorData other && Equals(other);
        }

        public readonly bool Equals(CICPColorData other)
        {
            return this.colorPrimaries == other.colorPrimaries
                && this.transferCharacteristics == other.transferCharacteristics
                && this.matrixCoefficients == other.matrixCoefficients
                && this.fullRange == other.fullRange;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(this.colorPrimaries,
                                    this.transferCharacteristics,
                                    this.matrixCoefficients,
                                    this.fullRange);
        }

        public static bool operator ==(CICPColorData left, CICPColorData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CICPColorData left, CICPColorData right)
        {
            return !left.Equals(right);
        }
    }
}
