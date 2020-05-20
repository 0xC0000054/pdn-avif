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
        private readonly FileTypeBox fileTypeBox;
        private readonly MetaBox metaBox;
        private readonly ColorInformationBox colorInformationBox;
        private readonly bool colorImageIsGrayscale;

        private readonly ProgressEventHandler progressCallback;
        private uint progressDone;
        private readonly uint progressTotal;

        public AvifWriter(CompressedAV1Image color,
                          CompressedAV1Image alpha,
                          AvifMetadata metadata,
                          ColorInformationBox colorInformationBox,
                          ProgressEventHandler progressEventHandler,
                          uint progressDone,
                          uint progressTotal)
        {
            this.state = new AvifWriterState(color, alpha, metadata);
            this.colorImageIsGrayscale = color.Format == YUVChromaSubsampling.Subsampling400;
            this.colorInformationBox = colorInformationBox;
            this.progressCallback = progressEventHandler;
            this.progressDone = progressDone;
            this.progressTotal = progressTotal;
            this.fileTypeBox = new FileTypeBox(color.Format);
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

                        writer.BaseStream.Write(imageBuffer, imageBuffer.ByteLength);

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

            // Cache the property association index values for the ImageSpatialExtentsBox
            // and PixelAspectRatioBox.
            // These boxes can be shared between the color and alpha images, which provides
            // a small reduction in file size.
            //
            // Gray-scale images can also share the AV1ConfigBox and PixelInformationBox
            // between the color and alpha images.
            // This works because the color and alpha images are the same size and YUV format.
            ushort imageSpatialExtentsAssociationIndex = 0;
            ushort pixelAspectRatioAssociationIndex = 0;
            ushort av1ConfigAssociationIndex = 0;
            ushort pixelInformationAssociationIndex = 0;

            for (int i = 0; i < items.Count; i++)
            {
                AvifWriterItem item = items[i];
                if (item.Image != null)
                {
                    if (imageSpatialExtentsAssociationIndex == 0)
                    {
                        itemPropertiesBox.AddProperty(new ImageSpatialExtentsBox((uint)item.Image.Width, (uint)item.Image.Height));
                        imageSpatialExtentsAssociationIndex = propertyAssociationIndex;
                        propertyAssociationIndex++;
                    }

                    itemPropertiesBox.AddPropertyAssociation(item.Id, false, imageSpatialExtentsAssociationIndex);

                    if (pixelAspectRatioAssociationIndex == 0)
                    {
                        itemPropertiesBox.AddProperty(new PixelAspectRatioBox(1, 1));
                        pixelAspectRatioAssociationIndex = propertyAssociationIndex;
                        propertyAssociationIndex++;
                    }

                    itemPropertiesBox.AddPropertyAssociation(item.Id, false, pixelAspectRatioAssociationIndex);

                    if (!this.colorImageIsGrayscale || av1ConfigAssociationIndex == 0)
                    {
                        itemPropertiesBox.AddProperty(AV1ConfigBoxBuilder.Build(item.Image));
                        av1ConfigAssociationIndex = propertyAssociationIndex;
                        propertyAssociationIndex++;
                    }

                    itemPropertiesBox.AddPropertyAssociation(item.Id, true, av1ConfigAssociationIndex);

                    if (!this.colorImageIsGrayscale || pixelInformationAssociationIndex == 0)
                    {
                        itemPropertiesBox.AddProperty(new PixelInformationBox(item.Image.Format));
                        pixelInformationAssociationIndex = propertyAssociationIndex;
                        propertyAssociationIndex++;
                    }

                    itemPropertiesBox.AddPropertyAssociation(item.Id, true, pixelInformationAssociationIndex);

                    if (item.IsAlphaImage)
                    {
                        itemPropertiesBox.AddProperty(new AlphaChannelBox());
                        itemPropertiesBox.AddPropertyAssociation(item.Id, true, propertyAssociationIndex);
                        propertyAssociationIndex++;
                    }
                    else
                    {
                        if (this.colorInformationBox != null)
                        {
                            itemPropertiesBox.AddProperty(this.colorInformationBox);
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
