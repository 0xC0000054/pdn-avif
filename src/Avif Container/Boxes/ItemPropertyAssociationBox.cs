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

using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal sealed class ItemPropertyAssociationBox
        : FullBox
    {
        private readonly Dictionary<uint, List<ItemPropertyAssociationEntry>> entries;

        private const int MaxAssociationEntriesCount = 255;

        public ItemPropertyAssociationBox()
            : base(0, 0, BoxTypes.ItemPropertyAssociations)
        {
            this.entries = new Dictionary<uint, List<ItemPropertyAssociationEntry>>();
        }

        public ItemPropertyAssociationBox(EndianBinaryReader reader)
            : base(reader)
        {
            if (this.Type != BoxTypes.ItemPropertyAssociations)
            {
                ExceptionUtil.ThrowFormatException($"Expected an 'ipma' box, actual value: '{ this.Type }'");
            }

            uint entryCount = reader.ReadUInt32();

            if (entryCount > int.MaxValue)
            {
                ExceptionUtil.ThrowFormatException($"Cannot read a container box with more than { int.MaxValue } items.");
            }

            this.entries = new Dictionary<uint, List<ItemPropertyAssociationEntry>>((int)entryCount);

            for (uint i = 0; i < entryCount; i++)
            {
                uint itemID = this.LargeItemId ? reader.ReadUInt32() : reader.ReadUInt16();

                byte associationCount = reader.ReadByte();

                this.entries.Add(itemID, new List<ItemPropertyAssociationEntry>(associationCount));

                for (int j = 0; j < associationCount; j++)
                {
                    bool essential;
                    ushort propertyIndex;

                    if (this.LargePropertyIndex)
                    {
                        ushort temp = reader.ReadUInt16();

                        essential = (temp & 0x8000) == 0x8000;
                        propertyIndex = (ushort)(temp & 0x7fff);
                    }
                    else
                    {
                        byte temp = reader.ReadByte();

                        essential = (temp & 0x80) == 0x80;
                        propertyIndex = (byte)(temp & 0x7f);
                    }

                    Add(itemID, essential, propertyIndex);
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => "Count = " + this.entries.Count.ToString();

        private bool LargeItemId => this.Version >= 1;

        private bool LargePropertyIndex => (this.Flags & 1) == 1;

        public void Add(uint itemId, bool essential, ushort propertyIndex)
        {
            if (propertyIndex > 32767)
            {
                ExceptionUtil.ThrowInvalidOperationException("The property index must be <= 32767.");
            }

            List<ItemPropertyAssociationEntry> itemProperties;

            if (this.entries.TryGetValue(itemId, out itemProperties))
            {
                if (itemProperties.Count == MaxAssociationEntriesCount)
                {
                    ExceptionUtil.ThrowInvalidOperationException("Cannot add more than 255 properties to an entry.");
                }

                itemProperties.Add(new ItemPropertyAssociationEntry(essential, propertyIndex));
            }
            else
            {
                itemProperties = new List<ItemPropertyAssociationEntry>
                {
                    new ItemPropertyAssociationEntry(essential, propertyIndex)
                };

                this.entries.Add(itemId, itemProperties);
            }

            if (itemId > ushort.MaxValue && !this.LargeItemId)
            {
                this.Version = 1;
            }

            if (propertyIndex > 127 && !this.LargePropertyIndex)
            {
                this.Flags |= 1;
            }
        }

        public IReadOnlyList<ItemPropertyAssociationEntry> TryGetAssociatedProperties(uint toItemId)
        {
            if (this.entries.TryGetValue(toItemId, out List<ItemPropertyAssociationEntry> values))
            {
                return values;
            }

            return null;
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write((uint)this.entries.Count);

            foreach (KeyValuePair<uint, List<ItemPropertyAssociationEntry>> entry in this.entries)
            {
                if (this.LargeItemId)
                {
                    writer.Write(entry.Key);
                }
                else
                {
                    writer.Write((ushort)entry.Key);
                }

                List<ItemPropertyAssociationEntry> properties = entry.Value;

                writer.Write((byte)properties.Count);

                foreach (ItemPropertyAssociationEntry association in properties)
                {
                    int essentialValue = association.Essential ? 1 : 0;

                    if (this.LargePropertyIndex)
                    {
                        writer.Write((ushort)((essentialValue << 15) | (ushort)(association.PropertyIndex & 0x7fff)));
                    }
                    else
                    {
                        writer.Write((byte)((essentialValue << 7) | (byte)(association.PropertyIndex & 0x7f)));
                    }
                }
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            ulong size = base.GetTotalBoxSize() + sizeof(uint);

            foreach (KeyValuePair<uint, List<ItemPropertyAssociationEntry>> entry in this.entries)
            {
                if (this.LargeItemId)
                {
                    size += sizeof(uint);
                }
                else
                {
                    size += sizeof(ushort);
                }

                size += sizeof(byte); // Association count

                if (this.LargePropertyIndex)
                {
                    size += (ulong)entry.Value.Count * sizeof(ushort);
                }
                else
                {
                    size += (ulong)entry.Value.Count * sizeof(byte);
                }
            }

            return size;
        }
    }
}
