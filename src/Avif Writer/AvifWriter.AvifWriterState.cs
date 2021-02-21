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
using PaintDotNet.AppModel;
using System.Collections.Generic;
using System.IO;

namespace AvifFileType
{
    internal sealed partial class AvifWriter
    {
        // The item ids start at 1.
        private const uint FirstItemId = 1;

        private sealed class AvifWriterState
        {
            private readonly List<AvifWriterItem> items;

            public AvifWriterState(IReadOnlyList<CompressedAV1Image> colorImages,
                                   IReadOnlyList<CompressedAV1Image> alphaImages,
                                   ImageGridMetadata imageGridMetadata,
                                   AvifMetadata metadata,
                                   IArrayPoolService arrayPool)
            {
                if (colorImages is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(colorImages));
                }

                if (metadata is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(metadata));
                }

                if (arrayPool is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(arrayPool));
                }

                this.ImageGrid = imageGridMetadata;
                this.items = new List<AvifWriterItem>(GetItemCount(colorImages, alphaImages, metadata));
                Initialize(colorImages, alphaImages, imageGridMetadata, metadata, arrayPool);
            }

            public uint AlphaItemId { get; private set; }

            public ImageGridMetadata ImageGrid { get; }

            public ItemDataBox ItemDataBox { get; private set; }

            public IReadOnlyList<AvifWriterItem> Items => this.items;

            public IReadOnlyList<int> MediaDataBoxAlphaItemIndexes { get; private set; }

            public IReadOnlyList<int> MediaDataBoxColorItemIndexes { get; private set; }

            public ulong MediaDataBoxContentSize { get; private set; }

            public IReadOnlyList<int> MediaDataBoxMetadataItemIndexes { get; private set; }

            public uint PrimaryItemId { get; private set; }

            private static ItemDataBox CreateItemDataBox(ImageGridMetadata imageGridMetadata, IArrayPoolService arrayPool)
            {
                ImageGridDescriptor imageGridDescriptor = new ImageGridDescriptor(imageGridMetadata);

                byte[] dataBoxBuffer = new byte[imageGridDescriptor.GetSize()];

                MemoryStream stream = null;
                try
                {
                    stream = new MemoryStream(dataBoxBuffer);

                    using (BigEndianBinaryWriter writer = new BigEndianBinaryWriter(stream, leaveOpen: false, arrayPool))
                    {
                        stream = null;

                        // The ImageGridDescriptor is shared between the color and alpha image.
                        imageGridDescriptor.Write(writer);
                    }
                }
                finally
                {
                    stream?.Dispose();
                }

                return new ItemDataBox(dataBoxBuffer);
            }

            private void Initialize(IReadOnlyList<CompressedAV1Image> colorImages,
                                    IReadOnlyList<CompressedAV1Image> alphaImages,
                                    ImageGridMetadata imageGridMetadata,
                                    AvifMetadata metadata,
                                    IArrayPoolService arrayPool)
            {
                ImageStateInfo result;

                if (imageGridMetadata != null)
                {
                    result = InitializeFromImageGrid(colorImages, alphaImages, imageGridMetadata);
                    this.ItemDataBox = CreateItemDataBox(imageGridMetadata, arrayPool);
                }
                else
                {
                    result = InitializeFromSingleImage(colorImages[0], alphaImages?[0]);
                    this.ItemDataBox = null;
                }

                uint itemId = result.NextId;
                ulong mediaDataBoxContentSize = result.MediaDataBoxContentSize;

                List<int> mediaDataBoxMetadataItemIndexes = new List<int>(2);

                byte[] exif = metadata.GetExifBytesReadOnly();
                if (exif != null && exif.Length > 0)
                {
                    AvifWriterItem exifItem = AvifWriterItem.CreateFromExif(itemId, exif);
                    itemId++;
                    exifItem.ItemReferences.Add(new ItemReferenceEntryBox(exifItem.Id, ReferenceTypes.ContentDescription, this.PrimaryItemId));

                    mediaDataBoxMetadataItemIndexes.Add(this.items.Count);
                    this.items.Add(exifItem);
                    mediaDataBoxContentSize += (ulong)exifItem.ContentBytes.Length;
                }

                byte[] xmp = metadata.GetXmpBytesReadOnly();
                if (xmp != null && xmp.Length > 0)
                {
                    AvifWriterItem xmpItem = AvifWriterItem.CreateFromXmp(itemId, xmp);
                    xmpItem.ItemReferences.Add(new ItemReferenceEntryBox(xmpItem.Id, ReferenceTypes.ContentDescription, this.PrimaryItemId));

                    mediaDataBoxMetadataItemIndexes.Add(this.items.Count);
                    this.items.Add(xmpItem);
                    mediaDataBoxContentSize += (ulong)xmpItem.ContentBytes.Length;
                }

                this.MediaDataBoxContentSize = mediaDataBoxContentSize;
                this.MediaDataBoxMetadataItemIndexes = mediaDataBoxMetadataItemIndexes;
            }

            private ImageStateInfo InitializeFromImageGrid(IReadOnlyList<CompressedAV1Image> colorImages,
                                                           IReadOnlyList<CompressedAV1Image> alphaImages,
                                                           ImageGridMetadata imageGridMetadata)
            {
                ulong mediaDataBoxContentSize = 0;
                uint itemId = FirstItemId;

                List<uint> colorImageIds = new List<uint>(colorImages.Count);
                List<uint> alphaImageIds = alphaImages != null ? new List<uint>(alphaImages.Count) : null;

                List<int> mediaDataBoxColorItemIndexes = new List<int>(colorImages.Count);
                List<int> mediaBoxAlphaItemIndexes = new List<int>(alphaImages != null ? alphaImages.Count : 0);

                for (int i = 0; i < colorImages.Count; i++)
                {
                    CompressedAV1Image color = colorImages[i];
                    AvifWriterItem colorItem = AvifWriterItem.CreateFromImage(itemId, null, color, false);
                    itemId++;
                    colorImageIds.Add(colorItem.Id);
                    mediaDataBoxColorItemIndexes.Add(this.items.Count);
                    this.items.Add(colorItem);
                    mediaDataBoxContentSize += color.Data.ByteLength;

                    if (alphaImages != null)
                    {
                        CompressedAV1Image alpha = alphaImages[i];
                        AvifWriterItem alphaItem = AvifWriterItem.CreateFromImage(itemId, null, alpha, true);
                        itemId++;
                        alphaItem.ItemReferences.Add(new ItemReferenceEntryBox(alphaItem.Id, ReferenceTypes.AuxiliaryImage, colorItem.Id));
                        alphaImageIds.Add(alphaItem.Id);
                        mediaBoxAlphaItemIndexes.Add(this.items.Count);
                        this.items.Add(alphaItem);
                        mediaDataBoxContentSize += alpha.Data.ByteLength;
                    }
                }

                ulong gridDescriptorLength;

                if (imageGridMetadata.OutputHeight > ushort.MaxValue || imageGridMetadata.OutputWidth > ushort.MaxValue)
                {
                    gridDescriptorLength = ImageGridDescriptor.LargeDescriptorLength;
                }
                else
                {
                    gridDescriptorLength = ImageGridDescriptor.SmallDescriptorLength;
                }

                // The grid items do not have any data to write in the media data box.
                AvifWriterItem colorGridItem = AvifWriterItem.CreateFromImageGrid(itemId, "Color", 0, gridDescriptorLength);
                itemId++;
                colorGridItem.ItemReferences.Add(new ItemReferenceEntryBox(colorGridItem.Id, ReferenceTypes.DerivedImage, colorImageIds));

                this.PrimaryItemId = colorGridItem.Id;
                this.items.Add(colorGridItem);

                if (alphaImages != null)
                {
                    // The ImageGridDescriptor is shared between the color and alpha image.
                    AvifWriterItem alphaGridItem = AvifWriterItem.CreateFromImageGrid(itemId, "Alpha", 0, gridDescriptorLength);
                    itemId++;
                    alphaGridItem.ItemReferences.Add(new ItemReferenceEntryBox(alphaGridItem.Id, ReferenceTypes.AuxiliaryImage, colorGridItem.Id));
                    alphaGridItem.ItemReferences.Add(new ItemReferenceEntryBox(alphaGridItem.Id, ReferenceTypes.DerivedImage, alphaImageIds));

                    this.AlphaItemId = alphaGridItem.Id;
                    this.items.Add(alphaGridItem);
                }

                this.MediaDataBoxAlphaItemIndexes = mediaBoxAlphaItemIndexes;
                this.MediaDataBoxColorItemIndexes = mediaDataBoxColorItemIndexes;

                return new ImageStateInfo(mediaDataBoxContentSize, itemId);
            }

            private ImageStateInfo InitializeFromSingleImage(CompressedAV1Image color, CompressedAV1Image alpha)
            {
                ulong mediaDataBoxContentSize = color.Data.ByteLength;
                uint itemId = FirstItemId;

                List<int> mediaDataBoxColorItemIndexes = new List<int>(1);
                List<int> mediaBoxAlphaItemIndexes = new List<int>(1);

                AvifWriterItem colorItem = AvifWriterItem.CreateFromImage(itemId, "Color", color, false);
                itemId++;
                this.PrimaryItemId = colorItem.Id;
                mediaDataBoxColorItemIndexes.Add(this.items.Count);
                this.items.Add(colorItem);

                if (alpha != null)
                {
                    AvifWriterItem alphaItem = AvifWriterItem.CreateFromImage(itemId, "Alpha", alpha, true);
                    itemId++;
                    alphaItem.ItemReferences.Add(new ItemReferenceEntryBox(alphaItem.Id, ReferenceTypes.AuxiliaryImage, this.PrimaryItemId));
                    this.AlphaItemId = alphaItem.Id;

                    mediaBoxAlphaItemIndexes.Add(this.items.Count);
                    this.items.Add(alphaItem);
                    mediaDataBoxContentSize += alpha.Data.ByteLength;
                }

                this.MediaDataBoxAlphaItemIndexes = mediaBoxAlphaItemIndexes;
                this.MediaDataBoxColorItemIndexes = mediaDataBoxColorItemIndexes;

                return new ImageStateInfo(mediaDataBoxContentSize, itemId);
            }

            private static int GetItemCount(IReadOnlyList<CompressedAV1Image> colorImages, IReadOnlyList<CompressedAV1Image> alphaImages, AvifMetadata metadata)
            {
                int count;

                if (colorImages.Count == 1)
                {
                    count = 1;
                }
                else
                {
                    // Add one item for the grid image.
                    count = 1 + colorImages.Count;
                }

                if (alphaImages != null)
                {
                    // The color and alpha lists will always have the same number of images.
                    count *= 2;
                }

                byte[] exif = metadata.GetExifBytesReadOnly();
                if (exif != null && exif.Length > 0)
                {
                    count++;
                }

                byte[] xmp = metadata.GetXmpBytesReadOnly();
                if (xmp != null && xmp.Length > 0)
                {
                    count++;
                }

                return count;
            }

            private readonly struct ImageStateInfo
            {
                public ImageStateInfo(ulong mediaDataBoxContentSize, uint nextId)
                {
                    this.MediaDataBoxContentSize = mediaDataBoxContentSize;
                    this.NextId = nextId;
                }

                public ulong MediaDataBoxContentSize { get; }

                public uint NextId { get; }
            }
        }
    }
}
