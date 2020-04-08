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

namespace AvifFileType.AvifContainer
{
    internal sealed class ItemInfoBox
        : FullBox
    {
        private readonly List<ItemInfoEntryBox> itemInfoEntries;

        public ItemInfoBox()
            : base(0, 0, BoxTypes.ItemInfo)
        {
            this.itemInfoEntries = new List<ItemInfoEntryBox>();
        }

        public ItemInfoBox(EndianBinaryReader reader, Box header)
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
                        throw new FormatException($"Cannot read a container box with more than { int.MaxValue } items.");
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

                this.itemInfoEntries.Add(new ItemInfoEntryBox(reader, entryHeader));
                reader.Position = entryHeader.End;
            }
        }

        public int Count => this.itemInfoEntries.Count;

        public IReadOnlyList<IItemInfoEntry> Entries => this.itemInfoEntries;

        public void Add(ItemInfoEntryBox itemInfo)
        {
            if (itemInfo is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(itemInfo));
            }

            this.itemInfoEntries.Add(itemInfo);
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
    }
}
