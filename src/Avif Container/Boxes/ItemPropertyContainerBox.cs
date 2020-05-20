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
    internal sealed class ItemPropertyContainerBox
        : Box
    {
        private readonly List<IItemProperty> properties;

        public ItemPropertyContainerBox()
            : base(BoxTypes.ItemPropertyContainer)
        {
            this.properties = new List<IItemProperty>();
        }

        public ItemPropertyContainerBox(EndianBinaryReader reader)
            : base(reader)
        {
            if (this.Type != BoxTypes.ItemPropertyContainer)
            {
                ExceptionUtil.ThrowFormatException($"Expected an 'ipco' box, actual value: '{ this.Type }'");
            }

            this.properties = new List<IItemProperty>();

            while (reader.Position < this.End)
            {
                Box header = new Box(reader);

                this.properties.Add(ItemPropertyFactory.TryCreate(reader, header));

                reader.Position = header.End;
            }
        }

        public IReadOnlyList<IItemProperty> Properties => this.properties;

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

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            for (int i = 0; i < this.properties.Count; i++)
            {
                this.properties[i].Write(writer);
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            ulong size = base.GetTotalBoxSize();

            for (int i = 0; i < this.properties.Count; i++)
            {
                size += this.properties[i].GetSize();
            }

            return size;
        }
    }
}
