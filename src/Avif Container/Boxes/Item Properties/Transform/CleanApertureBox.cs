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

namespace AvifFileType.AvifContainer
{
    internal sealed class CleanApertureBox
        : ItemProperty
    {
        public CleanApertureBox(EndianBinaryReader reader, Box header)
            : base(header)
        {
            this.Width = new Rational(reader);
            this.Height = new Rational(reader);
            this.HorizontalOffset = new Rational(reader);
            this.VerticalOffset = new Rational(reader);
        }

        public CleanApertureBox(Rational width, Rational heigth, Rational horizontalOffset, Rational verticalOffset)
            : base(BoxTypes.CleanAperture)
        {
            this.Width = width;
            this.Height = heigth;
            this.HorizontalOffset = horizontalOffset;
            this.VerticalOffset = verticalOffset;
        }

        public Rational Width { get; }

        public Rational Height { get; }

        public Rational HorizontalOffset { get; }

        public Rational VerticalOffset { get; }
    }
}
