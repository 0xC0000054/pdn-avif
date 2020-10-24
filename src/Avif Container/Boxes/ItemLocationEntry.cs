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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal sealed class ItemLocationEntry
    {
        private readonly List<ItemLocationExtent> extents;

        public ItemLocationEntry(in EndianBinaryReaderSegment reader, ItemLocationBox parent)
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
                    throw new FormatException($"{ nameof(ItemLocationBox) } version must be 0, 1 or 2, actual value: { parent.Version }.");
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
            if (extentCount == 0)
            {
                throw new FormatException("The ItemLocationEntry has zero extents.");
            }

            this.extents = new List<ItemLocationExtent>(extentCount);
            ulong totalItemSize = 0;

            for (int i = 0; i < this.extents.Capacity; i++)
            {
                ItemLocationExtent extent = new ItemLocationExtent(reader, parent, extentCount);

                this.extents.Add(extent);

                totalItemSize = checked(totalItemSize + extent.Length);
            }

            this.TotalItemSize = totalItemSize;
        }

        public ItemLocationEntry(uint itemId, ulong itemLength)
        {
            this.ItemId = itemId;
            this.DataReferenceIndex = 0;
            this.ConstructionMethod = ConstructionMethod.FileOffset;
            this.BaseOffset = 0;
            this.extents = new List<ItemLocationExtent>(1)
            {
                new ItemLocationExtent(itemLength)
            };
            this.TotalItemSize = itemLength;
        }

        public ItemLocationEntry(uint itemId, ulong itemDataBoxOffset, ulong itemLength)
        {
            this.ItemId = itemId;
            this.DataReferenceIndex = 0;
            this.ConstructionMethod = ConstructionMethod.IDatBoxOffset;
            this.BaseOffset = 0;
            this.extents = new List<ItemLocationExtent>(1)
            {
                new ItemLocationExtent(itemDataBoxOffset, itemLength)
            };
            this.TotalItemSize = itemLength;
        }

        public uint ItemId { get; }

        public ConstructionMethod ConstructionMethod { get; }

        public ushort DataReferenceIndex { get; }

        public ulong BaseOffset { get; }

        public IReadOnlyList<ItemLocationExtent> Extents => this.extents;

        public ulong TotalItemSize { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                int extentCount = this.extents.Count;
                ulong length = this.TotalItemSize;

                return $"ItemId: { this.ItemId }, ConstructionMethod: { this.ConstructionMethod }, Extent count: { extentCount }, Length: { length }";
            }
        }

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
                writer.Write((byte)this.ConstructionMethod);
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

            // The format specification allows an ItemLocationEntry to have multiple ItemLocationExtent values, but we only use one.
            writer.Write((ushort)1);
            this.extents[0].Write(writer, parent);
        }
    }
}
