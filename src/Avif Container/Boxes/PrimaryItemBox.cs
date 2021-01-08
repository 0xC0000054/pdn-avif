////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("ItemId: {ItemId, nq}")]
    internal sealed class PrimaryItemBox
        : FullBox
    {
        public PrimaryItemBox(in EndianBinaryReaderSegment reader, Box header)
            : base(reader, header)
        {
            if (this.Version == 0)
            {
                this.ItemId = reader.ReadUInt16();
            }
            else
            {
                this.ItemId = reader.ReadUInt32();
            }
        }

        public PrimaryItemBox(uint itemId)
            : base((byte)(itemId > ushort.MaxValue ? 1 : 0), 0, BoxTypes.PrimaryItem)
        {
            this.ItemId = itemId;
        }

        public uint ItemId { get; }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            if (this.Version == 0)
            {
                writer.Write((ushort)this.ItemId);
            }
            else
            {
                writer.Write(this.ItemId);
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize()
                + (this.Version == 0 ? (ulong)sizeof(ushort) : sizeof(uint));
        }
    }
}
