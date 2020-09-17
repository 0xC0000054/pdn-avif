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
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(ItemReferenceBoxDebugView))]
    internal sealed class ItemReferenceBox
        : FullBox
    {
        private readonly List<ItemReferenceEntryBox> itemReferences;

        public ItemReferenceBox(in EndianBinaryReaderSegment reader, Box header)
            : base(reader, header)
        {
            if (this.Version != 0 && this.Version != 1)
            {
                ExceptionUtil.ThrowFormatException($"ItemReferenceBox version must be 0 or 1, actual value { this.Version }");
            }

            this.itemReferences = new List<ItemReferenceEntryBox>();

            while (reader.Position < reader.EndOffset)
            {
                Box entry = new Box(reader);

                this.itemReferences.Add(new ItemReferenceEntryBox(reader.CreateChildSegment(entry), entry, this));
            }
        }

        public ItemReferenceBox()
            : base(0, 0, BoxTypes.ItemReference)
        {
            this.itemReferences = new List<ItemReferenceEntryBox>();
        }

        public int Count => this.itemReferences.Count;

        public IReadOnlyList<IItemReferenceEntry> ItemReferences => this.itemReferences;

        public void Add(ItemReferenceEntryBox reference)
        {
            if (reference is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(reference));
            }

            reference.SetParent(this);
            this.itemReferences.Add(reference);
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            for (int i = 0; i < this.itemReferences.Count; i++)
            {
                this.itemReferences[i].Write(writer);
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            ulong size = base.GetTotalBoxSize();

            for (int i = 0; i < this.itemReferences.Count; i++)
            {
                size += this.itemReferences[i].GetSize();
            }

            return size;
        }

        private sealed class ItemReferenceBoxDebugView
        {
            private readonly ItemReferenceBox itemReferenceBox;

            public ItemReferenceBoxDebugView(ItemReferenceBox itemReferenceBox)
            {
                this.itemReferenceBox = itemReferenceBox;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public ItemReferenceEntryBox[] Items => this.itemReferenceBox.itemReferences.ToArray();
        }
    }
}
