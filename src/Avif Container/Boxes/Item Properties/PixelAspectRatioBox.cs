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
    internal sealed class PixelAspectRatioBox
        : ItemProperty
    {
        public PixelAspectRatioBox(in EndianBinaryReaderSegment reader, Box header)
            : base(header)
        {
            this.HorizontalSpacing = reader.ReadUInt32();
            this.VerticalSpacing = reader.ReadUInt32();
        }

        public PixelAspectRatioBox(uint horizontalSpacing, uint verticalSpacing)
            : base(BoxTypes.PixelAspectRatio)
        {
            this.HorizontalSpacing = horizontalSpacing;
            this.VerticalSpacing = verticalSpacing;
        }

        public uint HorizontalSpacing { get; }

        public uint VerticalSpacing { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"HorizontalSpacing: {this.HorizontalSpacing}, VerticalSpacing: {this.VerticalSpacing}";
            }
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.HorizontalSpacing);
            writer.Write(this.VerticalSpacing);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + sizeof(uint) + sizeof(uint);
        }
    }
}
