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
        public MetaBox(in EndianBinaryReaderSegment reader, Box header)
            : base(reader, header)
        {
            if (this.Version != 0)
            {
                ExceptionUtil.ThrowFormatException($"MetaBox version must be 0, actual value: { this.Version }");
            }

            while (reader.Position < reader.EndOffset)
            {
                Box itemHeader = new Box(reader);

                EndianBinaryReaderSegment childSegment = reader.CreateChildSegment(itemHeader);

                if (itemHeader.Type == BoxTypes.Handler)
                {
                    if (this.Handler != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file has multiple handler boxes.");
                    }

                    this.Handler = new HandlerBox(childSegment, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.PrimaryItem)
                {
                    if (this.PrimaryItem != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file has multiple primary item boxes.");
                    }

                    this.PrimaryItem = new PrimaryItemBox(childSegment, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemLocation)
                {
                    if (this.ItemLocations != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file has multiple item location boxes.");
                    }

                    this.ItemLocations = new ItemLocationBox(childSegment, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemInfo)
                {
                    if (this.ItemInfo != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file has multiple item info boxes.");
                    }

                    this.ItemInfo = new ItemInfoBox(childSegment, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemProperties)
                {
                    if (this.ItemProperties != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file has multiple item properties boxes.");
                    }

                    this.ItemProperties = new ItemPropertiesBox(childSegment, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemReference)
                {
                    if (this.ItemReferences != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file has multiple item reference boxes.");
                    }

                    this.ItemReferences = new ItemReferenceBox(childSegment, itemHeader);
                }
                else if (itemHeader.Type == BoxTypes.ItemData)
                {
                    if (this.ItemData != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file has multiple item data boxes.");
                    }

                    this.ItemData = new ItemDataBox(itemHeader);
                }

                reader.Position = childSegment.EndOffset;
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
