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

using System;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal class ItemInfoEntryBox
        : FullBox, IItemInfoEntry
    {
        private const uint HiddenItemFlag = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemInfoEntryBox"/> class.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="header">The header.</param>
        /// <exception cref="FormatException">ItemInfoEntryBox version must be 2 or 3.</exception>
        public ItemInfoEntryBox(in EndianBinaryReaderSegment reader, Box header)
            : base(reader, header)
        {
            if (this.Version == 2)
            {
                this.ItemId = reader.ReadUInt16();
            }
            else if (this.Version == 3)
            {
                this.ItemId = reader.ReadUInt32();
            }
            else
            {
                ExceptionUtil.ThrowFormatException($"{ nameof(ItemInfoEntryBox) } version must be 2 or 3, actual value: { this.Version }.");
            }
            this.ItemProtectionIndex = reader.ReadUInt16();
            this.ItemType = reader.ReadFourCC();
            this.Name = reader.ReadBoxString();
        }

        protected ItemInfoEntryBox(ItemInfoEntryBox header)
            : base(header)
        {
            this.ItemId = header.ItemId;
            this.ItemProtectionIndex = header.ItemProtectionIndex;
            this.ItemType = header.ItemType;
            this.Name = header.Name;
        }

        protected ItemInfoEntryBox(uint itemId, ushort itemProtectionIndex, FourCC itemType, string? name)
            : this(itemId, hiddenItem: false, itemProtectionIndex, itemType, name)
        {
        }

        protected ItemInfoEntryBox(uint itemId, bool hiddenItem, ushort itemProtectionIndex, FourCC itemType, string? name)
            : base((byte)(itemId > ushort.MaxValue ? 3 : 2), hiddenItem ? HiddenItemFlag : 0, BoxTypes.ItemInfoEntry)
        {
            this.ItemId = itemId;
            this.ItemProtectionIndex = itemProtectionIndex;
            this.ItemType = itemType;
            this.Name = string.IsNullOrWhiteSpace(name) ? BoxString.Empty : new BoxString(name);
        }

        public bool IsHidden => (this.Flags & HiddenItemFlag) == HiddenItemFlag;

        public uint ItemId { get; }

        public ushort ItemProtectionIndex { get; }

        public FourCC ItemType { get; }

        public BoxString Name { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(this.Name.Value))
                {
                    return $"ItemId: { this.ItemId }, Type: { this.ItemType }";
                }
                else
                {
                    return $"ItemId: { this.ItemId }, Type: { this.ItemType }, Name: \"{ this.Name }\"";
                }
            }
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            if (this.Version <= 2)
            {
                writer.Write((ushort)this.ItemId);
            }
            else
            {
                writer.Write(this.ItemId);
            }

            writer.Write(this.ItemProtectionIndex);
            writer.Write(this.ItemType);
            this.Name.Write(writer);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize()
                   + (this.Version <= 2 ? (ulong)sizeof(ushort) : sizeof(uint)) // Item id
                   + sizeof(ushort) // Item protection index
                   + FourCC.SizeOf // Item type
                   + this.Name.GetSize(); // Item name
        }
    }
}
