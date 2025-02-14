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

using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    [DebuggerTypeProxy(typeof(ItemPropertiesBoxDebugView))]
    internal sealed class ItemPropertiesBox
        : Box
    {
        private readonly ItemPropertyContainerBox itemPropertyContainer;
        private readonly ItemPropertyAssociationBox itemPropertyAssociation;

        public ItemPropertiesBox(in EndianBinaryReaderSegment reader, Box header)
            : base(header)
        {
            Box propertyContainerHeader = new Box(reader);
            this.itemPropertyContainer = new ItemPropertyContainerBox(reader.CreateChildSegment(propertyContainerHeader),  propertyContainerHeader);

            Box propertyAssociationHeader = new Box(reader);
            this.itemPropertyAssociation = new ItemPropertyAssociationBox(reader.CreateChildSegment(propertyAssociationHeader), propertyAssociationHeader);
        }

        public ItemPropertiesBox()
            : base(BoxTypes.ItemProperties)
        {
            this.itemPropertyContainer = new ItemPropertyContainerBox();
            this.itemPropertyAssociation = new ItemPropertyAssociationBox();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"Property count = { this.itemPropertyContainer.Count}, Association count = { this.itemPropertyAssociation.Count }";
            }
        }

        public void AddProperty(IItemProperty property)
        {
            this.itemPropertyContainer.AddProperty(property);
        }

        public void AddPropertyAssociation(uint itemId, bool essential, ushort propertyIndex)
        {
            this.itemPropertyAssociation.Add(itemId, essential, propertyIndex);
        }

        public IReadOnlyList<ItemPropertyAssociationEntry>? TryGetAssociatedProperties(uint itemId)
        {
            return this.itemPropertyAssociation.TryGetAssociatedProperties(itemId);
        }

        public IItemProperty? TryGetProperty(uint propertyIndex)
        {
            return this.itemPropertyContainer.TryGetProperty(propertyIndex);
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            this.itemPropertyContainer.Write(writer);
            this.itemPropertyAssociation.Write(writer);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize()
                   + this.itemPropertyContainer.GetSize()
                   + this.itemPropertyAssociation.GetSize();
        }

        private sealed class ItemPropertiesBoxDebugView
        {
            private readonly ItemPropertiesBox itemPropertiesBox;

            public ItemPropertiesBoxDebugView(ItemPropertiesBox itemPropertiesBox)
            {
                this.itemPropertiesBox = itemPropertiesBox;
            }

            public ItemPropertyContainerBox ItemPropertyContainer => this.itemPropertiesBox.itemPropertyContainer;

            public ItemPropertyAssociationBox ItemPropertyAssociation => this.itemPropertiesBox.itemPropertyAssociation;
        }
    }
}
