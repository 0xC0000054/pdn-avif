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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    [DebuggerTypeProxy(typeof(ItemInfoBoxDebugView))]
    internal sealed class ItemInfoBox
        : FullBox
    {
        private readonly List<ItemInfoEntryBox> itemInfoEntries;

        public ItemInfoBox(int itemCount)
            : base((byte)(itemCount > ushort.MaxValue ? 1 : 0), 0, BoxTypes.ItemInfo)
        {
            this.itemInfoEntries = new List<ItemInfoEntryBox>(itemCount);
        }

        public ItemInfoBox(in EndianBinaryReaderSegment reader, Box header)
            : base(reader, header)
        {
            uint itemCount;
            switch (this.Version)
            {
                case 0:
                    itemCount = reader.ReadUInt16();
                    break;
                case 1:
                    itemCount = reader.ReadUInt32();
                    if (itemCount > int.MaxValue)
                    {
                        ExceptionUtil.ThrowFormatException($"Cannot read a container box with more than { int.MaxValue } items.");
                    }
                    break;
                default:
                    throw new FormatException("ItemInfoBox version must be 0 or 1, actual value: " + this.Version.ToString());
            }

            this.itemInfoEntries = new List<ItemInfoEntryBox>((int)itemCount);

            for (uint i = 0; i < itemCount; i++)
            {
                Box entryHeader = new Box(reader);

                if (entryHeader.Type != BoxTypes.ItemInfoEntry)
                {
                    ExceptionUtil.ThrowFormatException($"Expected an 'infe' box, actual value: '{ entryHeader.Type }'");
                }

                EndianBinaryReaderSegment entrySegment = reader.CreateChildSegment(entryHeader);

                this.itemInfoEntries.Add(ItemInfoEntryFactory.Create(entrySegment, entryHeader));
                reader.Position = entrySegment.EndOffset;
            }
        }

        public int Count => this.itemInfoEntries.Count;

        public IReadOnlyList<IItemInfoEntry> Entries => this.itemInfoEntries;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => "Count = " + this.itemInfoEntries.Count.ToString();

        public void Add(ItemInfoEntryBox itemInfo)
        {
            if (itemInfo is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(itemInfo));
            }

            this.itemInfoEntries.Add(itemInfo);
        }

        public IItemInfoEntry? TryGetEntry(uint itemId)
        {
            for (int i = 0; i < this.itemInfoEntries.Count; i++)
            {
                IItemInfoEntry item = this.itemInfoEntries[i];

                if (item.ItemId == itemId)
                {
                    return item;
                }
            }

            return null;
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            if (this.Version == 0)
            {
                writer.Write((ushort)this.itemInfoEntries.Count);
            }
            else
            {
                writer.Write((uint)this.itemInfoEntries.Count);
            }

            for (int i = 0; i < this.itemInfoEntries.Count; i++)
            {
                this.itemInfoEntries[i].Write(writer);
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            ulong size = base.GetTotalBoxSize();

            if (this.Version == 0)
            {
                size += sizeof(ushort);
            }
            else
            {
                size += sizeof(uint);
            }

            for (int i = 0; i < this.itemInfoEntries.Count; i++)
            {
                size += this.itemInfoEntries[i].GetSize();
            }

            return size;
        }

        private sealed class ItemInfoBoxDebugView
        {
            private readonly ItemInfoBox itemInfoBox;

            public ItemInfoBoxDebugView(ItemInfoBox itemInfoBox)
            {
                this.itemInfoBox = itemInfoBox;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public ItemInfoEntryBox[] Items => this.itemInfoBox.itemInfoEntries.ToArray();
        }
    }
}
