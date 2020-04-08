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
    internal sealed class PixelAspectRatioBox
        : ItemProperty
    {
        private readonly uint horizontalSpacing;
        private readonly uint verticalSpacing;

        public PixelAspectRatioBox(EndianBinaryReader reader, Box header)
            : base(header)
        {
            this.horizontalSpacing = reader.ReadUInt32();
            this.verticalSpacing = reader.ReadUInt32();
        }

        public PixelAspectRatioBox(uint horizontalSpacing, uint verticalSpacing)
            : base(BoxTypes.PixelAspectRatio)
        {
            this.horizontalSpacing = horizontalSpacing;
            this.verticalSpacing = verticalSpacing;
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.horizontalSpacing);
            writer.Write(this.verticalSpacing);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + sizeof(uint) + sizeof(uint);
        }
    }
}
