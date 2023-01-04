////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    [DebuggerTypeProxy(typeof(ItemLocationBoxDebugView))]
    internal sealed class ItemLocationBox
        : FullBox
    {
        private readonly List<ItemLocationEntry> items;

        public ItemLocationBox(in EndianBinaryReaderSegment reader, Box header)
            : base(reader, header)
        {
            if (this.Version < 0 || this.Version > 2)
            {
                ExceptionUtil.ThrowFormatException($"ItemLocationBox version must be 0, 1 or 2, actual value: { this.Version }");
            }

            byte offsetSizeAndLengthSize = reader.ReadByte();
            byte baseOffsetSizeAndIndexSize = reader.ReadByte();

            this.OffsetSize = (byte)(offsetSizeAndLengthSize >> 4);
            this.LengthSize = (byte)(offsetSizeAndLengthSize & 0x0f);
            this.BaseOffsetSize = (byte)(baseOffsetSizeAndIndexSize >> 4);
            if (this.Version == 1 || this.Version == 2)
            {
                this.IndexSize = (byte)(baseOffsetSizeAndIndexSize & 0x0f);
            }
            else
            {
                this.IndexSize = 0;
            }

            ValidateSizeFieldRange(this.OffsetSize, nameof(this.OffsetSize));
            ValidateSizeFieldRange(this.LengthSize, nameof(this.LengthSize));
            ValidateSizeFieldRange(this.BaseOffsetSize, nameof(this.BaseOffsetSize));
            ValidateSizeFieldRange(this.IndexSize, nameof(this.IndexSize));

            uint itemCount;
            switch (this.Version)
            {
                case 0:
                case 1:
                    itemCount = reader.ReadUInt16();
                    break;
                case 2:
                    itemCount = reader.ReadUInt32();
                    if (itemCount > int.MaxValue)
                    {
                        ExceptionUtil.ThrowFormatException($"Cannot read a container box with more than { int.MaxValue } items.");
                    }
                    break;
                default:
                    throw new FormatException($"ItemLocationBox version must be 0, 1 or 2, actual value: { this.Version }");
            }

            this.items = new List<ItemLocationEntry>((int)itemCount);

            for (uint i = 0; i < itemCount; i++)
            {
                this.items.Add(new ItemLocationEntry(reader, this));
            }
        }

        public ItemLocationBox(bool use64BitOffsets, int itemCount, ItemDataBox itemDataBox)
            : base(CalculateBoxVersion(itemCount, itemDataBox), 0, BoxTypes.ItemLocation)
        {
            if (use64BitOffsets)
            {
                this.OffsetSize = sizeof(ulong);
                this.LengthSize = sizeof(ulong);
            }
            else
            {
                this.OffsetSize = sizeof(uint);
                this.LengthSize = sizeof(uint);
            }
            // The base offset and index size fields are not used.
            this.BaseOffsetSize = 0;
            this.IndexSize = 0;
            this.items = new List<ItemLocationEntry>(itemCount);
        }

        public byte OffsetSize { get; }

        public byte LengthSize { get; }

        public byte BaseOffsetSize { get; }

        public byte IndexSize { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => "Count = " + this.items.Count.ToString();

        public void Add(ItemLocationEntry item)
        {
            if (item is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(item));
            }

            this.items.Add(item);
        }

        public ItemLocationEntry TryFindItem(uint itemId)
        {
            for (int i = 0; i < this.items.Count; i++)
            {
                ItemLocationEntry entry = this.items[i];

                if (entry.ItemId == itemId)
                {
                    return entry;
                }
            }

            return null;
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            byte offsetSizeAndLengthSize = (byte)((this.OffsetSize << 4) | (this.LengthSize & 0x0f));
            byte baseOffsetSizeAndIndexSize = (byte)((this.BaseOffsetSize << 4) | (this.IndexSize & 0x0f));

            writer.Write(offsetSizeAndLengthSize);
            writer.Write(baseOffsetSizeAndIndexSize);

            if (this.Version < 2)
            {
                writer.Write((ushort)this.items.Count);
            }
            else
            {
                writer.Write((uint)this.items.Count);
            }

            for (int i = 0; i < this.items.Count; i++)
            {
                this.items[i].Write(writer, this);
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize()
                   + sizeof(byte) // Offset size and length size
                   + sizeof(byte) // Base offset size and reserved
                   + (this.Version < 2 ? (ulong)sizeof(ushort) : sizeof(uint)) // Item count
                   + ((ulong)this.items.Count * (ulong)ItemLocationEntry.GetSize(this));
        }

        private static byte CalculateBoxVersion(int itemCount, ItemDataBox itemDataBox)
        {
            if (itemCount > ushort.MaxValue)
            {
                return 2;
            }
            else
            {
                return (byte)(itemDataBox != null ? 1 : 0);
            }
        }

        private static void ValidateSizeFieldRange(byte value, string name)
        {
            if (value != 0 && value != 4 && value != 8)
            {
                ExceptionUtil.ThrowInvalidOperationException($"{ name } must be 0, 4 or 8, actual value: { value }");
            }
        }

        private sealed class ItemLocationBoxDebugView
        {
            private readonly ItemLocationBox itemLocationBox;

            public ItemLocationBoxDebugView(ItemLocationBox itemLocationBox)
            {
                this.itemLocationBox = itemLocationBox;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public ItemLocationEntry[] Items => this.itemLocationBox.items.ToArray();
        }
    }
}
