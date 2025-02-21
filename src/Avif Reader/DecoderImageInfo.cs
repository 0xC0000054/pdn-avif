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

using System;

namespace AvifFileType
{
    internal sealed class DecoderImageInfo : IEquatable<DecoderImageInfo>
    {
        public DecoderImageInfo()
        {
        }

        public DecoderImageInfo(uint width,
                                uint height,
                                uint bitDepth,
                                YUVChromaSubsampling chromaSubsampling,
                                CICPColorData cicpColor)
        {
            this.Width = width;
            this.Height = height;
            this.BitDepth = bitDepth;
            this.ChromaSubsampling = chromaSubsampling;
            this.CICPColor = cicpColor;
        }

        public uint Width { get; }

        public uint Height { get; }

        public uint BitDepth { get; }

        public YUVChromaSubsampling ChromaSubsampling { get; }

        public CICPColorData CICPColor { get; }

        public override bool Equals(object? obj)
        {
            return Equals(obj as DecoderImageInfo);
        }

        public bool Equals(DecoderImageInfo? other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Width, this.Height, this.BitDepth, this.ChromaSubsampling, this.CICPColor);
        }

        public static bool operator ==(DecoderImageInfo? left, DecoderImageInfo? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Width == right.Width
                && left.Height == right.Height
                && left.BitDepth == right.BitDepth
                && left.ChromaSubsampling == right.ChromaSubsampling
                && left.CICPColor == right.CICPColor;
        }

        public static bool operator !=(DecoderImageInfo? left, DecoderImageInfo? right)
        {
            return !(left == right);
        }
    }
}
