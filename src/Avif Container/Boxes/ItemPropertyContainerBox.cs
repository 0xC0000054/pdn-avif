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

using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    [DebuggerTypeProxy(typeof(ItemPropertyContainerBoxDebugView))]
    internal sealed class ItemPropertyContainerBox
        : Box
    {
        private readonly List<IItemProperty?> properties;

        public ItemPropertyContainerBox()
            : base(BoxTypes.ItemPropertyContainer)
        {
            this.properties = new List<IItemProperty?>();
        }

        public ItemPropertyContainerBox(in EndianBinaryReaderSegment reader, Box header)
            : base(header)
        {
            if (this.Type != BoxTypes.ItemPropertyContainer)
            {
                ExceptionUtil.ThrowFormatException($"Expected an 'ipco' box, actual value: '{ this.Type }'");
            }

            this.properties = new List<IItemProperty?>();
            ItemPropertyFactory propertyFactory = new ItemPropertyFactory();

            while (reader.Position < reader.EndOffset)
            {
                Box entry = new Box(reader);

                EndianBinaryReaderSegment childSegment = reader.CreateChildSegment(entry);

                this.properties.Add(propertyFactory.TryCreate(childSegment, entry));

                reader.Position = childSegment.EndOffset;
            }
        }

        public int Count => this.properties.Count;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => "Count = " + this.properties.Count.ToString();

        public void AddProperty(IItemProperty property)
        {
            if (property is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(property));
            }

            this.properties.Add(property);
        }

        public IItemProperty? TryGetProperty(uint propertyIndex)
        {
            if (propertyIndex > 0 && propertyIndex <= (uint)this.properties.Count)
            {
                return this.properties[(int)(propertyIndex - 1)];
            }

            return null;
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            for (int i = 0; i < this.properties.Count; i++)
            {
                IItemProperty? property = this.properties[i];

                property?.Write(writer);
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            ulong size = base.GetTotalBoxSize();

            for (int i = 0; i < this.properties.Count; i++)
            {
                IItemProperty? property = this.properties[i];

                if (property != null)
                {
                    size += property.GetSize();
                }
            }

            return size;
        }

        private sealed class ItemPropertyContainerBoxDebugView
        {
            private readonly ItemPropertyContainerBox itemPropertyContainerBox;

            public ItemPropertyContainerBoxDebugView(ItemPropertyContainerBox itemPropertyContainerBox)
            {
                this.itemPropertyContainerBox = itemPropertyContainerBox;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public IItemProperty?[] Items => this.itemPropertyContainerBox.properties.ToArray();
        }
    }
}
