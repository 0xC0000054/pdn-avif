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

        public AvifWriter(IReadOnlyList<CompressedAV1Image> colorImages,
                          IReadOnlyList<CompressedAV1Image> alphaImages,
                          AvifMetadata metadata,
                          ImageGridMetadata imageGridMetadata,
                          YUVChromaSubsampling chromaSubsampling,
                          ColorInformationBox colorInformationBox,
                          ProgressEventHandler progressEventHandler,
                          uint progressDone,
                          uint progressTotal)
        {
            this.state = new AvifWriterState(colorImages, alphaImages, imageGridMetadata, metadata);
            this.colorImageIsGrayscale = chromaSubsampling == YUVChromaSubsampling.Subsampling400;
            this.colorInformationBox = colorInformationBox;
            this.progressCallback = progressEventHandler;
            this.progressDone = progressDone;
            this.progressTotal = progressTotal;
            this.fileTypeBox = new FileTypeBox(chromaSubsampling);
            this.metaBox = new MetaBox(this.state.PrimaryItemId, this.state.TotalDataSize > uint.MaxValue, this.state.ItemDataBox);
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

                    if (item.Image is null && item.ContentBytes is null)
                    {
                        continue;
                    }

                    // We only ever write items with a single extent.
                    item.ItemLocation.Extents[0].WriteFinalOffset(writer, (ulong)writer.Position);

                    if (item.Image != null)
                    {
                        writer.BaseStream.Write(item.Image.Data);

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
            ushort colorAv1ConfigAssociationIndex = 0;
            ushort colorPixelInformationAssociationIndex = 0;
            ushort alphaAv1ConfigAssociationIndex = 0;
            ushort alphaPixelInformationAssociationIndex = 0;
            ushort alphaChannelAssociationIndex = 0;

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

                    if (colorAv1ConfigAssociationIndex == 0 || item.IsAlphaImage && alphaAv1ConfigAssociationIndex == 0)
                    {
                        itemPropertiesBox.AddProperty(AV1ConfigBoxBuilder.Build(item.Image));
                        if (this.colorImageIsGrayscale)
                        {
                            colorAv1ConfigAssociationIndex = alphaAv1ConfigAssociationIndex = propertyAssociationIndex;
                        }
                        else
                        {
                            if (item.IsAlphaImage)
                            {
                                alphaAv1ConfigAssociationIndex = propertyAssociationIndex;
                            }
                            else
                            {
                                colorAv1ConfigAssociationIndex = propertyAssociationIndex;
                            }
                        }
                        propertyAssociationIndex++;
                    }


                    if (colorPixelInformationAssociationIndex == 0 || item.IsAlphaImage && alphaPixelInformationAssociationIndex == 0)
                    {
                        itemPropertiesBox.AddProperty(new PixelInformationBox(item.Image.Format));
                        if (this.colorImageIsGrayscale)
                        {
                            colorPixelInformationAssociationIndex = alphaPixelInformationAssociationIndex = propertyAssociationIndex;
                        }
                        else
                        {
                            if (item.IsAlphaImage)
                            {
                                alphaPixelInformationAssociationIndex = propertyAssociationIndex;
                            }
                            else
                            {
                                colorPixelInformationAssociationIndex = propertyAssociationIndex;
                            }
                        }
                        propertyAssociationIndex++;
                    }


                    if (item.IsAlphaImage)
                    {
                        itemPropertiesBox.AddPropertyAssociation(item.Id, true, alphaAv1ConfigAssociationIndex);
                        itemPropertiesBox.AddPropertyAssociation(item.Id, true, alphaPixelInformationAssociationIndex);

                        if (alphaChannelAssociationIndex == 0)
                        {
                            itemPropertiesBox.AddProperty(new AlphaChannelBox());
                            alphaChannelAssociationIndex = propertyAssociationIndex;
                            propertyAssociationIndex++;
                        }

                        itemPropertiesBox.AddPropertyAssociation(item.Id, true, alphaChannelAssociationIndex);
                    }
                    else
                    {
                        itemPropertiesBox.AddPropertyAssociation(item.Id, true, colorAv1ConfigAssociationIndex);
                        itemPropertiesBox.AddPropertyAssociation(item.Id, true, colorPixelInformationAssociationIndex);
                    }
                }
            }

            if (this.state.ImageGrid != null)
            {
                itemPropertiesBox.AddProperty(new ImageSpatialExtentsBox(this.state.ImageGrid.OutputWidth, this.state.ImageGrid.OutputHeight));
                ushort gridImageSpatialExtentsAssociationIndex = propertyAssociationIndex;
                propertyAssociationIndex++;

                itemPropertiesBox.AddPropertyAssociation(this.state.PrimaryItemId, false, gridImageSpatialExtentsAssociationIndex);
                if (this.state.AlphaItemId != 0)
                {
                    itemPropertiesBox.AddPropertyAssociation(this.state.AlphaItemId, false, gridImageSpatialExtentsAssociationIndex);
                    itemPropertiesBox.AddPropertyAssociation(this.state.AlphaItemId, true, alphaChannelAssociationIndex);
                }
            }

            if (this.colorInformationBox != null)
            {
                itemPropertiesBox.AddProperty(this.colorInformationBox);
                itemPropertiesBox.AddPropertyAssociation(this.state.PrimaryItemId, true, propertyAssociationIndex);
            }
        }

        private void PopulateItemReferences()
        {
            IReadOnlyList<AvifWriterItem> items = this.state.Items;
            ItemReferenceBox itemReferenceBox = this.metaBox.ItemReferences;

            for (int i = 0; i < items.Count; i++)
            {
                AvifWriterItem item = items[i];
                if (item.ItemReferences.Count > 0)
                {
                    itemReferenceBox.Add(item.ItemReferences);
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
