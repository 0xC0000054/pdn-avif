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
    internal sealed class HandlerBox
        : FullBox
    {
        private readonly uint preDefined;
        private readonly FourCC handlerType;
        private readonly uint reserved1;
        private readonly uint reserved2;
        private readonly uint reserved3;
        private readonly BoxString name;

        private static readonly FourCC HandlerTypePICT = new FourCC('p', 'i', 'c', 't');

        public HandlerBox(EndianBinaryReader reader, Box header) : base(reader, header)
        {
            if (this.Version != 0)
            {
                throw new FormatException($"HandlerBox version must be 0, actual value: { this.Version }");
            }

            this.preDefined = reader.ReadUInt32();
            this.handlerType = reader.ReadFourCC();

            if (this.handlerType != HandlerTypePICT)
            {
                throw new FormatException($"The handler type must be 'pict', actual value: { this.handlerType }");
            }

            this.reserved1 = reader.ReadUInt32();
            this.reserved2 = reader.ReadUInt32();
            this.reserved3 = reader.ReadUInt32();
            this.name = reader.ReadBoxString(header.End);
        }

        public HandlerBox()
            : base(0, 0, BoxTypes.Handler)
        {
            this.preDefined = 0;
            this.handlerType = HandlerTypePICT;
            this.reserved1 = 0;
            this.reserved2 = 0;
            this.reserved2 = 0;
            this.name = new BoxString("PDNavif");
        }

        public string Name => this.name?.Value;

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.preDefined);
            writer.Write(this.handlerType);
            writer.Write(this.reserved1);
            writer.Write(this.reserved2);
            writer.Write(this.reserved3);
            this.name.Write(writer);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize()
                   + sizeof(uint) // Predefined
                   + FourCC.SizeOf // Handler type
                   + (sizeof(uint) * 3) // Reserved fields
                   + this.name.GetSize();
        }
    }
}
