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

namespace AvifFileType.AvifContainer
{
    internal sealed class MetaBox
        : FullBox
    {
        public MetaBox(EndianBinaryReader reader, Box header)
            : base(reader, header)
        {
            if (this.Version != 0)
            {
                ExceptionUtil.ThrowFormatException($"MetaBox version must be 0, actual value: { this.Version }");
            }

            while (reader.Position < this.End)
            {
                Box itemHeader = new Box(reader);

                if (itemHeader.Type == BoxTypes.Handler)
                {
                    this.Handler = new HandlerBox(reader, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.PrimaryItem)
                {
                    if (this.PrimaryItem != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file has multiple primary item boxes.");
                    }

                    this.PrimaryItem = new PrimaryItemBox(reader, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemLocation)
                {
                    this.ItemLocations = new ItemLocationBox(reader, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemInfo)
                {
                    this.ItemInfo = new ItemInfoBox(reader, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemProperties)
                {
                    this.ItemProperties = new ItemPropertiesBox(reader, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemReference)
                {
                    this.ItemReferences = new ItemReferenceBox(reader, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemData)
                {
                    this.ItemData = new ItemDataBox(header);
                }

                reader.Position = itemHeader.End;
            }
        }

        public MetaBox(ushort primaryItemId, bool use64BitFileOffsets)
            : base(0, 0, BoxTypes.Meta)
        {
            this.Handler = new HandlerBox();
            this.PrimaryItem = new PrimaryItemBox(primaryItemId);
            // The ItemData box is only used when reading from a file.
            this.ItemData = null;
            this.ItemLocations = new ItemLocationBox(use64BitFileOffsets);
            this.ItemInfo = new ItemInfoBox();
            this.ItemReferences = new ItemReferenceBox();
            this.ItemProperties = new ItemPropertiesBox();
        }

        public HandlerBox Handler { get; }

        public PrimaryItemBox PrimaryItem { get; }

        public ItemDataBox ItemData { get; }

        public ItemLocationBox ItemLocations { get; }

        public ItemInfoBox ItemInfo { get; }

        public ItemReferenceBox ItemReferences { get; }

        public ItemPropertiesBox ItemProperties { get; }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            this.Handler.Write(writer);
            this.PrimaryItem.Write(writer);
            this.ItemLocations.Write(writer);
            this.ItemInfo.Write(writer);
            if (this.ItemReferences.Count > 0)
            {
                this.ItemReferences.Write(writer);
            }
            this.ItemProperties.Write(writer);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize()
                   + this.Handler.GetSize()
                   + this.PrimaryItem.GetSize()
                   + this.ItemLocations.GetSize()
                   + this.ItemInfo.GetSize()
                   + (this.ItemReferences.Count > 0 ? this.ItemReferences.GetSize() : 0)
                   + this.ItemProperties.GetSize();
        }
    }
}
