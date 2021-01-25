////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.AvifContainer;
using PaintDotNet;
using PaintDotNet.AppModel;
using System.Collections.Generic;
using System.IO;

namespace AvifFileType
{
    internal sealed partial class AvifWriter
    {
        private readonly AvifWriterState state;
        private readonly FileTypeBox fileTypeBox;
        private readonly MetaBox metaBox;
        private readonly IReadOnlyList<ColorInformationBox> colorInformationBoxes;
        private readonly bool colorImageIsGrayscale;
        private readonly IArrayPoolService arrayPool;

        private readonly ProgressEventHandler progressCallback;
        private uint progressDone;
        private readonly uint progressTotal;

        public AvifWriter(IReadOnlyList<CompressedAV1Image> colorImages,
                          IReadOnlyList<CompressedAV1Image> alphaImages,
                          AvifMetadata metadata,
                          ImageGridMetadata imageGridMetadata,
                          YUVChromaSubsampling chromaSubsampling,
                          IReadOnlyList<ColorInformationBox> colorInformationBoxes,
                          ProgressEventHandler progressEventHandler,
                          uint progressDone,
                          uint progressTotal,
                          IArrayPoolService arrayPool)
        {
            this.state = new AvifWriterState(colorImages, alphaImages, imageGridMetadata, metadata, arrayPool);
            this.arrayPool = arrayPool;
            this.colorImageIsGrayscale = chromaSubsampling == YUVChromaSubsampling.Subsampling400;
            this.colorInformationBoxes = colorInformationBoxes ?? System.Array.Empty<ColorInformationBox>();
            this.progressCallback = progressEventHandler;
            this.progressDone = progressDone;
            this.progressTotal = progressTotal;
            this.fileTypeBox = new FileTypeBox(chromaSubsampling);
            this.metaBox = new MetaBox(this.state.PrimaryItemId,
                                       this.state.Items.Count,
                                       this.state.MediaDataBoxContentSize > uint.MaxValue,
                                       this.state.ItemDataBox);
            PopulateMetaBox();
        }

        public void WriteTo(Stream stream)
        {
            using (BigEndianBinaryWriter writer = new BigEndianBinaryWriter(stream, true, this.arrayPool))
            {
                this.fileTypeBox.Write(writer);
                this.metaBox.Write(writer);

                new MediaDataBox(this.state.MediaDataBoxContentSize).Write(writer);

                // The media data box items are written in the following order:
                // 1. EXIF and/or XMP meta data
                // 2. Alpha images (if present)
                // 3. Color images
                //
                // The meta data is written first to improve efficiency for readers that want to use it
                // without reading the image data.
                // The alpha image data is written before the color image data to improve the user experience
                // for web browsers and other applications that may display an AVIF image as it is being
                // streamed over a network.
                // See the following link for a discussion on alpha image data being written before color
                // image data: https://github.com/AOMediaCodec/libavif/issues/287

                WriteMediaDataBoxItems(writer, this.state.MediaDataBoxMetadataItemIndexes);
                if (this.state.AlphaItemId != 0)
                {
                    WriteMediaDataBoxItems(writer, this.state.MediaDataBoxAlphaItemIndexes);
                }
                WriteMediaDataBoxItems(writer, this.state.MediaDataBoxColorItemIndexes);
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

            if (this.colorInformationBoxes.Count > 0)
            {
                for (int i = 0; i < this.colorInformationBoxes.Count; i++)
                {
                    itemPropertiesBox.AddProperty(this.colorInformationBoxes[i]);
                    itemPropertiesBox.AddPropertyAssociation(this.state.PrimaryItemId, true, propertyAssociationIndex);
                    propertyAssociationIndex++;
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

        private void WriteMediaDataBoxItems(BigEndianBinaryWriter writer, IReadOnlyList<int> itemIndexes)
        {
            IReadOnlyList<AvifWriterItem> items = this.state.Items;

            for (int i = 0; i < itemIndexes.Count; i++)
            {
                int index = itemIndexes[i];
                AvifWriterItem item = items[index];

                if (item.Image is null && item.ContentBytes is null)
                {
                    continue;
                }

                // We only ever write items with a single extent.
                item.ItemLocation.Extents[0].WriteFinalOffset(writer, (ulong)writer.Position);

                if (item.Image != null)
                {
                    item.Image.Data.Write(writer);

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
}
