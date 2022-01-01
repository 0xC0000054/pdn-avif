////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal sealed class CleanApertureBox
        : ItemProperty
    {
        public CleanApertureBox(in EndianBinaryReaderSegment reader, Box header)
            : base(header)
        {
            this.Width = new Rational(reader);
            this.Height = new Rational(reader);
            this.HorizontalOffset = new Rational(reader);
            this.VerticalOffset = new Rational(reader);
        }

        public CleanApertureBox(Rational width, Rational height, Rational horizontalOffset, Rational verticalOffset)
            : base(BoxTypes.CleanAperture)
        {
            this.Width = width;
            this.Height = height;
            this.HorizontalOffset = horizontalOffset;
            this.VerticalOffset = verticalOffset;
        }

        public Rational Width { get; }

        public Rational Height { get; }

        public Rational HorizontalOffset { get; }

        public Rational VerticalOffset { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"Width: {this.Width}, Height: {this.Height}, HorizontalOffset: {this.HorizontalOffset}, VerticalOffset: {this.VerticalOffset}";
            }
        }
    }
}
