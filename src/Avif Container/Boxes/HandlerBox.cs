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

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
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

        public HandlerBox(in EndianBinaryReaderSegment reader, Box header) : base(reader, header)
        {
            if (this.Version != 0)
            {
                ExceptionUtil.ThrowFormatException($"HandlerBox version must be 0, actual value: { this.Version }");
            }

            this.preDefined = reader.ReadUInt32();
            this.handlerType = reader.ReadFourCC();

            if (this.handlerType != HandlerTypePICT)
            {
                ExceptionUtil.ThrowFormatException($"The handler type must be 'pict', actual value: { this.handlerType }");
            }

            this.reserved1 = reader.ReadUInt32();
            this.reserved2 = reader.ReadUInt32();
            this.reserved3 = reader.ReadUInt32();
            this.name = reader.ReadBoxString();
        }

        public HandlerBox()
            : base(0, 0, BoxTypes.Handler)
        {
            this.preDefined = 0;
            this.handlerType = HandlerTypePICT;
            this.reserved1 = 0;
            this.reserved2 = 0;
            this.reserved3 = 0;
            this.name = BoxString.Empty;
        }

        public string? Name => this.name?.Value;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"Name: \"{ this.name.Value }\"";
            }
        }

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
