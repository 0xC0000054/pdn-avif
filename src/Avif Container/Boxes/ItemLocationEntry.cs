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
using System.Globalization;

namespace AvifFileType.AvifContainer
{
    internal sealed class ItemLocationEntry
    {
        public ItemLocationEntry(EndianBinaryReader reader, ItemLocationBox parent)
        {
            switch (parent.Version)
            {
                case 0:
                case 1:
                    this.ItemId = reader.ReadUInt16();
                    break;
                case 2:
                    this.ItemId = reader.ReadUInt32();
                    break;
                default:
                    throw new FormatException("ItemLocationBox version must be 0, 1 or 2, actual value: " + parent.Version.ToString());
            }

            if (parent.Version == 1 || parent.Version == 2)
            {
                // Skip the first reserved byte
                reader.Position++;
                this.ConstructionMethod = (ConstructionMethod)(reader.ReadByte() & 0x0f);
                if (this.ConstructionMethod != ConstructionMethod.FileOffset && this.ConstructionMethod != ConstructionMethod.IDatBoxOffset)
                {
                    throw new FormatException($"ItemLocationEntry construction method { this.ConstructionMethod } is not supported.");
                }
            }

            this.DataReferenceIndex = reader.ReadUInt16();
            switch (parent.BaseOffsetSize)
            {
                case 0:
                    this.BaseOffset = 0;
                    break;
                case 4:
                    this.BaseOffset = reader.ReadUInt32();
                    break;
                case 8:
                    this.BaseOffset = reader.ReadUInt64();
                    break;
                default:
                    throw new InvalidOperationException($"BaseOffsetSize must be 0, 4 or 8, actual value: { parent.BaseOffsetSize.ToString(CultureInfo.InvariantCulture) }");
            }

            ushort extentCount = reader.ReadUInt16();
            if (extentCount != 1)
            {
                throw new FormatException("ItemLocation entries with more than one extent are not supported.");
            }

            this.Extent = new ItemLocationExtent(reader, parent, extentCount);
        }

        public ItemLocationEntry(ushort itemId, ulong itemLength)
        {
            this.ItemId = itemId;
            this.DataReferenceIndex = 0;
            this.ConstructionMethod = ConstructionMethod.FileOffset;
            this.BaseOffset = 0;
            this.Extent = new ItemLocationExtent(itemLength);
        }

        public uint ItemId { get; }

        public ConstructionMethod ConstructionMethod { get; }

        public ushort DataReferenceIndex { get; }

        public ulong BaseOffset { get; }

        public ItemLocationExtent Extent { get; }

        public static ulong GetSize(ItemLocationBox parent)
        {
            ulong size = parent.Version < 2 ? (ulong)sizeof(ushort) : sizeof(uint); // Item id
            if (parent.Version == 1 || parent.Version == 2)
            {
                size += sizeof(ushort); // Construction method
            }
            size += sizeof(ushort); // Data reference index
            size += parent.BaseOffsetSize;// Base offset
            size += sizeof(ushort);// Extent count
            size += (ulong)ItemLocationExtent.GetSize(parent);

            return size;
        }

        public void Write(BigEndianBinaryWriter writer, ItemLocationBox parent)
        {
            if (parent.Version < 2)
            {
                writer.Write((ushort)this.ItemId);
            }
            else
            {
                writer.Write(this.ItemId);
            }

            if (parent.Version == 1 || parent.Version == 2)
            {
                // Write the reserved byte and the construction method.
                writer.Write((byte)0);
                writer.Write((byte)ConstructionMethod.FileOffset);
            }

            writer.Write(this.DataReferenceIndex);
            switch (parent.BaseOffsetSize)
            {
                case 0:
                    break;
                case 4:
                    writer.Write((uint)this.BaseOffset);
                    break;
                case 8:
                    writer.Write(this.BaseOffset);
                    break;
                default:
                    throw new InvalidOperationException($"BaseOffsetSize must be 0, 4 or 8, actual value: { parent.BaseOffsetSize.ToString(CultureInfo.InvariantCulture) }");
            }

            // The format specification allows an ItemLocationEntry to have multiple ItemLocationExtent values, but we only need one.
            writer.Write((ushort)1);
            this.Extent.Write(writer, parent);
        }
    }
}
