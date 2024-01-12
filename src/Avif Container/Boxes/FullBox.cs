////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

namespace AvifFileType.AvifContainer
{
    internal class FullBox
        : Box
    {
        public FullBox(in EndianBinaryReaderSegment reader, Box header)
            : base(header)
        {
            uint versionAndFlags = reader.ReadUInt32();

            this.Version = (byte)((versionAndFlags >> 24) & 0xff);
            this.Flags = versionAndFlags & 0x00ffffff;
        }

        protected FullBox(FullBox header)
            : base(header)
        {
            this.Version = header.Version;
            this.Flags = header.Flags;
        }

        protected FullBox(byte version, uint flags, FourCC type)
            : base(type)
        {
            this.Version = version;
            this.Flags = flags;
        }

        public byte Version { get; protected set; }

        public uint Flags { get; protected set; }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            uint versionAndFlags = ((uint)this.Version << 24) | (this.Flags & 0x00ffffff);

            writer.Write(versionAndFlags);
        }

        protected override ulong GetTotalBoxSize()
        {
            // The version and flags are written as a packed UInt32 value.
            return base.GetTotalBoxSize() + sizeof(uint);
        }
    }
}
