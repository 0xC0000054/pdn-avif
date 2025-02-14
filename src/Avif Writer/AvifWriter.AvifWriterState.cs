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

using AvifFileType.AvifContainer;
using AvifFileType.Interop;
using CommunityToolkit.HighPerformance.Buffers;
using System;
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
            private readonly Dictionary<int, int> duplicateAlphaTiles;
            private readonly Dictionary<int, int> duplicateColorTiles;

            public AvifWriterState(IReadOnlyList<CompressedAV1Image> colorImages,
                                   IReadOnlyList<CompressedAV1Image>? alphaImages,
                                   HomogeneousTileInfo homogeneousTiles,
                                   bool premultipliedAlpha,
                                   ImageGridMetadata? imageGridMetadata,
                                   AvifMetadata metadata)
            {
                if (colorImages is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(colorImages));
                }

                if (metadata is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(metadata));
                }

                this.ImageGrid = imageGridMetadata;
                this.items = new List<AvifWriterItem>(GetItemCount(colorImages, alphaImages, metadata));
                this.duplicateAlphaTiles = new Dictionary<int, int>(homogeneousTiles.DuplicateAlphaTileMap);
                this.duplicateColorTiles = new Dictionary<int, int>(homogeneousTiles.DuplicateColorTileMap);
                DeduplicateColorTiles(colorImages, homogeneousTiles);

                if (alphaImages != null)
                {
                    DeduplicateAlphaTiles(alphaImages, homogeneousTiles);
                }

                Initialize(colorImages, alphaImages, premultipliedAlpha, imageGridMetadata, metadata);
            }

            public uint AlphaItemId { get; private set; }

            public ImageGridMetadata? ImageGrid { get; }

            public ItemDataBox? ItemDataBox { get; private set; }

            public IReadOnlyList<AvifWriterItem> Items => this.items;

            public IReadOnlyList<int>? MediaDataBoxAlphaItemIndexes { get; private set; }

            public IReadOnlyList<int>? MediaDataBoxColorItemIndexes { get; private set; }

            public ulong MediaDataBoxContentSize { get; private set; }

            public IReadOnlyList<int>? MediaDataBoxMetadataItemIndexes { get; private set; }

            public uint PrimaryItemId { get; private set; }

            private static ItemDataBox CreateItemDataBox(ImageGridMetadata imageGridMetadata)
            {
                ImageGridDescriptor imageGridDescriptor = new ImageGridDescriptor(imageGridMetadata);

                byte[] dataBoxBuffer = new byte[imageGridDescriptor.GetSize()];

                MemoryStream? stream = null;
                try
                {
                    stream = new MemoryStream(dataBoxBuffer);

                    using (BigEndianBinaryWriter writer = new BigEndianBinaryWriter(stream, leaveOpen: false))
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

            private void DeduplicateAlphaTiles(IReadOnlyList<CompressedAV1Image> alphaImages,
                                               HomogeneousTileInfo homogeneousTiles)
            {
                if (alphaImages.Count == 1 || alphaImages.Count == homogeneousTiles.HomogeneousAlphaTiles.Count)
                {
                    return;
                }

                using (MemoryOwner<int> duplicateTileOwner = GetDuplicateTileSearchSpace(alphaImages,
                                                                                                    homogeneousTiles.HomogeneousAlphaTiles))
                {
                    ReadOnlySpan<int> duplicateTileSearchSpace = duplicateTileOwner.Span;

                    for (int i = 0; i < duplicateTileSearchSpace.Length; i++)
                    {
                        int firstTileIndex = duplicateTileSearchSpace[i];

                        if (this.duplicateAlphaTiles.ContainsKey(firstTileIndex))
                        {
                            continue;
                        }

                        CompressedAV1Data firstImageData = alphaImages[firstTileIndex].Data;

                        for (int j = i + 1; j < duplicateTileSearchSpace.Length; j++)
                        {
                            int secondTileIndex = duplicateTileSearchSpace[j];

                            if (this.duplicateAlphaTiles.ContainsKey(secondTileIndex))
                            {
                                continue;
                            }

                            CompressedAV1Data secondImageData = alphaImages[secondTileIndex].Data;

                            if (firstImageData.Equals(secondImageData))
                            {
                                this.duplicateAlphaTiles.Add(secondTileIndex, firstTileIndex);
                            }
                        }
                    }
                }
            }

            private void DeduplicateColorTiles(IReadOnlyList<CompressedAV1Image> colorImages,
                                               HomogeneousTileInfo homogeneousTiles)
            {
                if (colorImages.Count == 1 || colorImages.Count == homogeneousTiles.HomogeneousColorTiles.Count)
                {
                    return;
                }

                using (MemoryOwner<int> owner = GetDuplicateTileSearchSpace(colorImages, homogeneousTiles.HomogeneousColorTiles))
                {
                    ReadOnlySpan<int> duplicateTileSearchSpace = owner.Span;

                    for (int i = 0; i < duplicateTileSearchSpace.Length; i++)
                    {
                        int firstTileIndex = duplicateTileSearchSpace[i];

                        if (this.duplicateColorTiles.ContainsKey(firstTileIndex))
                        {
                            continue;
                        }

                        CompressedAV1Data firstImageData = colorImages[firstTileIndex].Data;

                        for (int j = i + 1; j < duplicateTileSearchSpace.Length; j++)
                        {
                            int secondTileIndex = duplicateTileSearchSpace[j];

                            if (this.duplicateColorTiles.ContainsKey(secondTileIndex))
                            {
                                continue;
                            }

                            CompressedAV1Data secondImageData = colorImages[secondTileIndex].Data;

                            if (firstImageData.Equals(secondImageData))
                            {
                                this.duplicateColorTiles.Add(secondTileIndex, firstTileIndex);
                            }
                        }
                    }
                }
            }

            private static MemoryOwner<int> GetDuplicateTileSearchSpace(IReadOnlyList<CompressedAV1Image> images,
                                                                        HashSet<int> homogeneousTiles)
            {
                MemoryOwner<int> buffer = MemoryOwner<int>.Allocate(images.Count - homogeneousTiles.Count);

                Span<int> searchSpace = buffer.Span;
                int index = 0;

                for (int i = 0; i < images.Count; i++)
                {
                    if (!homogeneousTiles.Contains(i))
                    {
                        searchSpace[index] = i;
                        index++;
                    }
                }

                return buffer;
            }

            private void Initialize(IReadOnlyList<CompressedAV1Image> colorImages,
                                    IReadOnlyList<CompressedAV1Image>? alphaImages,
                                    bool premultipliedAlpha,
                                    ImageGridMetadata? imageGridMetadata,
                                    AvifMetadata metadata)
            {
                ImageStateInfo result;

                if (imageGridMetadata != null)
                {
                    result = InitializeFromImageGrid(colorImages, alphaImages, premultipliedAlpha, imageGridMetadata);
                    this.ItemDataBox = CreateItemDataBox(imageGridMetadata);
                }
                else
                {
                    result = InitializeFromSingleImage(colorImages[0], alphaImages?[0], premultipliedAlpha);
                    this.ItemDataBox = null;
                }

                uint itemId = result.NextId;
                ulong mediaDataBoxContentSize = result.MediaDataBoxContentSize;

                List<int> mediaDataBoxMetadataItemIndexes = new List<int>(2);

                ReadOnlyMemory<byte> exif = metadata.Exif;
                if (exif.Length > 0)
                {
                    AvifWriterItem exifItem = AvifWriterItem.CreateFromExif(itemId, exif);
                    itemId++;
                    exifItem.ItemReferences.Add(new ItemReferenceEntryBox(exifItem.Id, ReferenceTypes.ContentDescription, this.PrimaryItemId));

                    mediaDataBoxMetadataItemIndexes.Add(this.items.Count);
                    this.items.Add(exifItem);
                    mediaDataBoxContentSize += (ulong)exifItem.ContentBytes.Length;
                }

                ReadOnlyMemory<byte> xmp = metadata.Xmp;
                if (xmp.Length > 0)
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
                                                           IReadOnlyList<CompressedAV1Image>? alphaImages,
                                                           bool premultipliedAlpha,
                                                           ImageGridMetadata imageGridMetadata)
            {
                ulong mediaDataBoxContentSize = 0;
                uint itemId = FirstItemId;

                List<uint> colorImageIds = new List<uint>(colorImages.Count);
                List<uint>? alphaImageIds = alphaImages != null ? new List<uint>(alphaImages.Count) : null;

                List<int> mediaDataBoxColorItemIndexes = new List<int>(colorImages.Count);
                List<int> mediaBoxAlphaItemIndexes = new List<int>(alphaImages != null ? alphaImages.Count : 0);

                for (int i = 0; i < colorImages.Count; i++)
                {
                    int duplicateImageIndex;
                    int duplicateColorImageIndex = -1;

                    if (this.duplicateColorTiles.TryGetValue(i, out duplicateImageIndex))
                    {
                        duplicateColorImageIndex = mediaDataBoxColorItemIndexes[duplicateImageIndex];
                    }

                    CompressedAV1Image color = colorImages[i];
                    AvifWriterItem colorItem = AvifWriterItem.CreateFromImage(itemId, null, color, false, duplicateColorImageIndex);
                    itemId++;
                    colorImageIds.Add(colorItem.Id);

                    mediaDataBoxColorItemIndexes.Add(this.items.Count);
                    this.items.Add(colorItem);

                    if (duplicateColorImageIndex == -1)
                    {
                        mediaDataBoxContentSize += color.Data.ByteLength;
                    }

                    if (alphaImages != null)
                    {
                        int duplicateAlphaImageIndex = -1;

                        if (this.duplicateAlphaTiles.TryGetValue(i, out duplicateImageIndex))
                        {
                            duplicateAlphaImageIndex = mediaBoxAlphaItemIndexes[duplicateImageIndex];
                        }

                        CompressedAV1Image alpha = alphaImages[i];
                        AvifWriterItem alphaItem = AvifWriterItem.CreateFromImage(itemId, null, alpha, true, duplicateAlphaImageIndex);
                        itemId++;
                        alphaItem.ItemReferences.Add(new ItemReferenceEntryBox(alphaItem.Id, ReferenceTypes.AuxiliaryImage, colorItem.Id));

                        if (premultipliedAlpha)
                        {
                            alphaItem.ItemReferences.Add(new ItemReferenceEntryBox(colorItem.Id,
                                                                                   ReferenceTypes.PremultipliedAlphaImage,
                                                                                   alphaItem.Id));
                        }

                        alphaImageIds!.Add(alphaItem.Id);
                        mediaBoxAlphaItemIndexes.Add(this.items.Count);
                        this.items.Add(alphaItem);

                        if (duplicateAlphaImageIndex == -1)
                        {
                            mediaDataBoxContentSize += alpha.Data.ByteLength;
                        }
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
                    alphaGridItem.ItemReferences.Add(new ItemReferenceEntryBox(alphaGridItem.Id, ReferenceTypes.DerivedImage, alphaImageIds!));

                    if (premultipliedAlpha)
                    {
                        alphaGridItem.ItemReferences.Add(new ItemReferenceEntryBox(colorGridItem.Id,
                                                                                   ReferenceTypes.PremultipliedAlphaImage,
                                                                                   alphaGridItem.Id));
                    }

                    this.AlphaItemId = alphaGridItem.Id;
                    this.items.Add(alphaGridItem);
                }

                this.MediaDataBoxAlphaItemIndexes = mediaBoxAlphaItemIndexes;
                this.MediaDataBoxColorItemIndexes = mediaDataBoxColorItemIndexes;

                return new ImageStateInfo(mediaDataBoxContentSize, itemId);
            }

            private ImageStateInfo InitializeFromSingleImage(CompressedAV1Image color, CompressedAV1Image? alpha, bool premultipliedAlpha)
            {
                ulong mediaDataBoxContentSize = color.Data.ByteLength;
                uint itemId = FirstItemId;

                List<int> mediaDataBoxColorItemIndexes = new List<int>(1);
                List<int> mediaBoxAlphaItemIndexes = new List<int>(1);

                AvifWriterItem colorItem = AvifWriterItem.CreateFromImage(itemId, "Color", color, false, duplicateImageIndex: -1);
                itemId++;
                this.PrimaryItemId = colorItem.Id;
                mediaDataBoxColorItemIndexes.Add(this.items.Count);
                this.items.Add(colorItem);

                if (alpha != null)
                {
                    AvifWriterItem alphaItem = AvifWriterItem.CreateFromImage(itemId, "Alpha", alpha, true, duplicateImageIndex: -1);
                    itemId++;
                    alphaItem.ItemReferences.Add(new ItemReferenceEntryBox(alphaItem.Id, ReferenceTypes.AuxiliaryImage, this.PrimaryItemId));
                    if (premultipliedAlpha)
                    {
                        alphaItem.ItemReferences.Add(new ItemReferenceEntryBox(this.PrimaryItemId,
                                                                               ReferenceTypes.PremultipliedAlphaImage,
                                                                               alphaItem.Id));
                    }

                    this.AlphaItemId = alphaItem.Id;

                    mediaBoxAlphaItemIndexes.Add(this.items.Count);
                    this.items.Add(alphaItem);
                    mediaDataBoxContentSize += alpha.Data.ByteLength;
                }

                this.MediaDataBoxAlphaItemIndexes = mediaBoxAlphaItemIndexes;
                this.MediaDataBoxColorItemIndexes = mediaDataBoxColorItemIndexes;

                return new ImageStateInfo(mediaDataBoxContentSize, itemId);
            }

            private static int GetItemCount(IReadOnlyList<CompressedAV1Image> colorImages, IReadOnlyList<CompressedAV1Image>? alphaImages, AvifMetadata metadata)
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

                if (metadata.Exif.Length > 0)
                {
                    count++;
                }

                if (metadata.Xmp.Length > 0)
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
