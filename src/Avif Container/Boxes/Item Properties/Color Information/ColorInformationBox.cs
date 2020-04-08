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
    internal class ColorInformationBox
        : ItemProperty
    {
        public ColorInformationBox(EndianBinaryReader reader, Box header)
            : base(header)
        {
            this.ColorType = reader.ReadFourCC();
        }

        protected ColorInformationBox(ColorInformationBox header)
            : base(header)
        {
            this.ColorType = header.ColorType;
        }

        protected ColorInformationBox(FourCC colorType)
            : base(BoxTypes.ColorInformation)
        {
            this.ColorType = colorType;
        }

        public FourCC ColorType { get; }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.ColorType);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + FourCC.SizeOf;
        }
    }
}
