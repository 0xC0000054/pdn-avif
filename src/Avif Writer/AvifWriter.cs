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

using AvifFileType.AvifContainer;
using PaintDotNet;
using System.Collections.Generic;
using System.IO;

namespace AvifFileType
{
    internal sealed partial class AvifWriter
    {
        private readonly AvifWriterState state;
        private readonly AvifMetadata metadata;
        private readonly FileTypeBox fileTypeBox;
        private readonly MetaBox metaBox;

        private readonly ProgressEventHandler progressCallback;
        private uint progressDone;
        private readonly uint progressTotal;

        public AvifWriter(CompressedAV1Image color,
                          CompressedAV1Image alpha,
                          YUVChromaSubsampling chromaSubsampling,
                          AvifMetadata metadata,
                          ProgressEventHandler progressEventHandler,
                          uint progressDone,
                          uint progressTotal)
        {
            this.state = new AvifWriterState(color, alpha, metadata);
            this.metadata = metadata;
            this.progressCallback = progressEventHandler;
            this.progressDone = progressDone;
            this.progressTotal = progressTotal;
            this.fileTypeBox = new FileTypeBox(chromaSubsampling);
            this.metaBox = new MetaBox(this.state.PrimaryItemId, this.state.TotalDataSize > uint.MaxValue);
            PopulateMetaBox();
        }

        public void WriteTo(Stream stream)
        {
            using (BigEndianBinaryWriter writer = new BigEndianBinaryWriter(stream, true))
            {
                this.fileTypeBox.Write(writer);
                this.metaBox.Write(writer);

                new MediaDataBox(this.state.TotalDataSize).Write(writer);

                IReadOnlyList<AvifWriterItem> items = this.state.Items;

                for (int i = 0; i < items.Count; i++)
                {
                    AvifWriterItem item = items[i];

                    item.ItemLocation.Extent.WriteFinalOffset(writer, (ulong)writer.Position);

                    if (item.Image != null)
                    {
                        Interop.SafeAV1Image imageBuffer = item.Image.Data;

                        ulong imageDataLength = imageBuffer.ByteLength;

                        if (imageDataLength == 0)
                        {
                            continue;
                        }

                        writer.BaseStream.Write(imageBuffer, imageDataLength);

                        this.progressDone++;
                        this.progressCallback?.Invoke(this, new ProgressEventArgs(((double)this.progressDone / this.progressTotal) * 100.0));
                    }
                    else
                    {
                        writer.Write(item.ContentBytes);
                    }
                }
            }
        }

        private void PopulateItemInfos()
        {
            IReadOnlyList<AvifWriterItem> items = this.state.Items;
            ItemInfoBox itemInfoBox = this.metaBox.ItemInfo;

            for (int i = 0; i < items.Count; i++)
            {
                AvifWriterItem item = items[i];
                if (item.ItemInfoEntry != null)
                {
                    itemInfoBox.Add(item.ItemInfoEntry);
                }
            }
        }

        private void PopulateItemLocations()
        {
            IReadOnlyList<AvifWriterItem> items = this.state.Items;
            ItemLocationBox itemLocationBox = this.metaBox.ItemLocations;

            for (int i = 0; i < items.Count; i++)
            {
                AvifWriterItem item = items[i];
                if (item.ItemLocation != null)
                {
                    itemLocationBox.Add(item.ItemLocation);
                }
            }
        }

        private void PopulateItemProperties()
        {
            IReadOnlyList<AvifWriterItem> items = this.state.Items;
            ItemPropertiesBox itemPropertiesBox = this.metaBox.ItemProperties;

            // The property association ids are 1-based
            ushort propertyAssociationIndex = 1;

            for (int i = 0; i < items.Count; i++)
            {
                AvifWriterItem item = items[i];
                if (item.Image != null)
                {
                    itemPropertiesBox.AddProperty(new ImageSpatialExtentsBox((uint)item.Image.Width, (uint)item.Image.Height));
                    itemPropertiesBox.AddPropertyAssociation(item.Id, false, propertyAssociationIndex);
                    propertyAssociationIndex++;

                    itemPropertiesBox.AddProperty(new PixelAspectRatioBox(1, 1));
                    itemPropertiesBox.AddPropertyAssociation(item.Id, false, propertyAssociationIndex);
                    propertyAssociationIndex++;

                    itemPropertiesBox.AddProperty(AV1ConfigBoxBuilder.Build(item.Image, item.IsAlphaImage));
                    itemPropertiesBox.AddPropertyAssociation(item.Id, true, propertyAssociationIndex);
                    propertyAssociationIndex++;

                    itemPropertiesBox.AddProperty(new PixelInformationBox(item.IsAlphaImage));
                    itemPropertiesBox.AddPropertyAssociation(item.Id, true, propertyAssociationIndex);
                    propertyAssociationIndex++;

                    if (item.IsAlphaImage)
                    {
                        itemPropertiesBox.AddProperty(new AlphaChannelBox());
                        itemPropertiesBox.AddPropertyAssociation(item.Id, true, propertyAssociationIndex);
                        propertyAssociationIndex++;
                    }
                    else
                    {
                        byte[] iccProfile = this.metadata.GetICCProfileBytesReadOnly();
                        if (iccProfile != null && iccProfile.Length > 0)
                        {
                            itemPropertiesBox.AddProperty(new IccProfileColorInformation(iccProfile));
                            itemPropertiesBox.AddPropertyAssociation(item.Id, true, propertyAssociationIndex);
                            propertyAssociationIndex++;
                        }
                    }
                }
            }
        }

        private void PopulateItemReferences()
        {
            IReadOnlyList<AvifWriterItem> items = this.state.Items;
            ItemReferenceBox itemReferenceBox = this.metaBox.ItemReferences;

            for (int i = 0; i < items.Count; i++)
            {
                AvifWriterItem item = items[i];
                if (item.ItemReference != null)
                {
                    itemReferenceBox.Add(item.ItemReference);
                }
            }
        }

        private void PopulateMetaBox()
        {
            PopulateItemInfos();
            PopulateItemLocations();
            PopulateItemProperties();
            PopulateItemReferences();
        }
    }
}
