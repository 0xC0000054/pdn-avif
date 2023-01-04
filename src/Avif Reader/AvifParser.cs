////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.AvifContainer;
using PaintDotNet.AppModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AvifFileType
{
    [DebuggerTypeProxy(typeof(AvifParserDebugView))]
    internal sealed class AvifParser
        : Disposable
    {
        private const ulong ManagedAvifItemDataMaxSize = 1024 * 1024;

        private FileTypeBox fileTypeBox;
        private MetaBox metaBox;
        private EndianBinaryReader reader;
        private readonly ulong fileLength;
        private readonly IArrayPoolService arrayPool;

        public AvifParser(Stream stream, bool leaveOpen, IArrayPoolService arrayPool)
        {
            if (stream is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(stream));
            }

            this.arrayPool = arrayPool;
            this.reader = new EndianBinaryReader(stream, Endianess.Big, leaveOpen, arrayPool);
            Parse();
            this.fileLength = (ulong)stream.Length;
        }

        public IEnumerable<ColorInformationBox> EnumerateColorInformationBoxes(uint itemId)
        {
            ItemPropertiesBox itemPropertiesBox = this.metaBox.ItemProperties;
            IReadOnlyList<ItemPropertyAssociationEntry> items = itemPropertiesBox.TryGetAssociatedProperties(itemId);

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    IItemProperty property = itemPropertiesBox.TryGetProperty(items[i].PropertyIndex);

                    if (property is ColorInformationBox colorInformationBox)
                    {
                        yield return colorInformationBox;
                    }
                }
            }
        }

        public uint GetAlphaItemId(uint primaryItemId)
        {
            uint alphaImageItemId = 0;

            IItemReferenceEntry entry = GetMatchingReferences(primaryItemId, ReferenceTypes.AuxiliaryImage).FirstOrDefault();

            if (entry != null && IsAlphaChannelItem(entry.FromItemId))
            {
                alphaImageItemId = entry.FromItemId;
            }

            return alphaImageItemId;
        }

        public LayerSelectorInfo GetLayerSelectorInfo(uint itemId, ulong totalItemSize)
        {
            AV1LayeredImageIndexingBox layeredImageIndexingBox = null;
            LayerSelectorBox layerSelectorBox = null;

            ItemPropertiesBox itemPropertiesBox = this.metaBox.ItemProperties;
            IReadOnlyList<ItemPropertyAssociationEntry> items = itemPropertiesBox.TryGetAssociatedProperties(itemId);

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    IItemProperty property = itemPropertiesBox.TryGetProperty(items[i].PropertyIndex);

                    if (property != null)
                    {
                        if (property.Type == BoxTypes.AV1LayeredImageIndexing)
                        {
                            layeredImageIndexingBox = (AV1LayeredImageIndexingBox)property;
                        }
                        else if (property.Type == BoxTypes.LayerSelector)
                        {
                            layerSelectorBox = (LayerSelectorBox)property;
                        }
                    }
                }
            }

            LayerSelectorInfo layerSelectorInfo = null;

            if (layerSelectorBox != null)
            {
                layerSelectorInfo = new LayerSelectorInfo(layeredImageIndexingBox,
                                                          totalItemSize,
                                                          layerSelectorBox.LayerId);
            }

            return layerSelectorInfo;
        }

        public uint GetPrimaryItemId()
        {
            return this.metaBox.PrimaryItem?.ItemId ?? 1;
        }

        public void GetTransformationProperties(uint itemId,
                                                out CleanApertureBox cleanAperture,
                                                out ImageRotateBox imageRotate,
                                                out ImageMirrorBox imageMirror)
        {
            cleanAperture = null;
            imageRotate = null;
            imageMirror = null;

            ItemPropertiesBox itemPropertiesBox = this.metaBox.ItemProperties;
            IReadOnlyList<ItemPropertyAssociationEntry> items = itemPropertiesBox.TryGetAssociatedProperties(itemId);

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    IItemProperty property = itemPropertiesBox.TryGetProperty(items[i].PropertyIndex);

                    if (property != null)
                    {
                        if (property.Type == BoxTypes.CleanAperture)
                        {
                            cleanAperture = (CleanApertureBox)property;
                        }
                        else if (property.Type == BoxTypes.ImageRotation)
                        {
                            imageRotate = (ImageRotateBox)property;
                        }
                        else if (property.Type == BoxTypes.ImageMirror)
                        {
                            imageMirror = (ImageMirrorBox)property;
                        }
                    }
                }
            }
        }

        public bool IsAlphaPremultiplied(uint primaryItemId, uint alphaItemId)
        {
            IItemReferenceEntry entry = GetMatchingReferences(alphaItemId, ReferenceTypes.PremultipliedAlphaImage).FirstOrDefault();

            return entry != null && entry.FromItemId == primaryItemId;
        }

        public AvifItemData ReadItemData(ItemLocationEntry entry, ulong? numberOfBytesToRead = null)
        {
            if (entry is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(entry));
            }

            AvifItemData data;

            if (entry.Extents.Count == 1)
            {
                long offset = CalculateExtentOffset(entry.BaseOffset, entry.ConstructionMethod, entry.Extents[0]);

                this.reader.Position = offset;

                ulong totalItemSize = numberOfBytesToRead ?? entry.TotalItemSize;

                if (totalItemSize <= ManagedAvifItemDataMaxSize)
                {
                    ManagedAvifItemData managedItemData = new ManagedAvifItemData((int)totalItemSize, this.arrayPool);

                    try
                    {
                        this.reader.ProperRead(managedItemData.GetBuffer(), 0, (int)managedItemData.Length);

                        data = managedItemData;
                        managedItemData = null;
                    }
                    finally
                    {
                        managedItemData?.Dispose();
                    }
                }
                else
                {
                    UnmanagedAvifItemData unmanagedItemData = new UnmanagedAvifItemData(totalItemSize);

                    try
                    {
                        this.reader.ProperRead(unmanagedItemData.UnmanagedBuffer, 0, totalItemSize);

                        data = unmanagedItemData;
                        unmanagedItemData = null;
                    }
                    finally
                    {
                        unmanagedItemData?.Dispose();
                    }
                }
            }
            else
            {
                data = ReadDataFromMultipleExtents(entry, numberOfBytesToRead);
            }

            return data;
        }

        public TProperty TryGetAssociatedItemProperty<TProperty>(uint itemId) where TProperty : class, IItemProperty
        {
            if (typeof(TProperty).IsAbstract)
            {
                ExceptionUtil.ThrowInvalidOperationException($"Cannot call this method with an abstract type, type: { typeof(TProperty).Name }.");
            }

            ItemPropertiesBox itemPropertiesBox = this.metaBox.ItemProperties;
            IReadOnlyList<ItemPropertyAssociationEntry> items = itemPropertiesBox.TryGetAssociatedProperties(itemId);

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    IItemProperty property = itemPropertiesBox.TryGetProperty(items[i].PropertyIndex);

                    if (property is TProperty requestedProperty)
                    {
                        return requestedProperty;
                    }
                }
            }

            return null;
        }

        public ItemLocationEntry TryGetExifLocation(uint itemId)
        {
            foreach (IItemReferenceEntry item in GetMatchingReferences(itemId, ReferenceTypes.ContentDescription))
            {
                IItemInfoEntry entry = TryGetItemInfoEntry(item.FromItemId);

                if (entry != null && entry.ItemType == ItemInfoEntryTypes.Exif)
                {
                    return TryGetItemLocation(item.FromItemId);
                }
            }

            return null;
        }

        public ImageGridInfo TryGetImageGridInfo(uint itemId)
        {
            ImageGridDescriptor gridDescriptor = TryGetImageGridDescriptor(itemId);

            if (gridDescriptor != null)
            {
                IItemReferenceEntry derivedImageProperty = GetMatchingReferences(itemId, ReferenceTypes.DerivedImage).FirstOrDefault();

                if (derivedImageProperty is null)
                {
                    ExceptionUtil.ThrowFormatException("The grid image does not have an associated derived image property.");
                }

                return new ImageGridInfo(derivedImageProperty.ToItemIds, gridDescriptor);
            }

            return null;
        }

        public IItemInfoEntry TryGetItemInfoEntry(uint itemId)
        {
            return this.metaBox.ItemInfo.TryGetEntry(itemId);
        }

        public ItemLocationEntry TryGetItemLocation(uint itemId)
        {
            return this.metaBox.ItemLocations.TryFindItem(itemId);
        }

        public ItemLocationEntry TryGetXmpLocation(uint itemId)
        {
            foreach (IItemReferenceEntry item in GetMatchingReferences(itemId, ReferenceTypes.ContentDescription))
            {
                IItemInfoEntry entry = TryGetItemInfoEntry(item.FromItemId);

                if (entry != null && entry.ItemType == ItemInfoEntryTypes.Mime)
                {
                    MimeItemInfoEntryBox mimeItemInfo = (MimeItemInfoEntryBox)entry;
                    string contentType = mimeItemInfo.ContentType.Value;

                    if (string.Equals(contentType, XmpItemInfoEntry.XmpContentType, StringComparison.Ordinal))
                    {
                        return TryGetItemLocation(item.FromItemId);
                    }
                }
            }

            return null;
        }

        public void ValidateRequiredImageProperties(uint itemId)
        {
            ItemPropertiesBox itemPropertiesBox = this.metaBox.ItemProperties;
            IReadOnlyList<ItemPropertyAssociationEntry> items = itemPropertiesBox.TryGetAssociatedProperties(itemId);

            if (items != null)
            {
                FourCC[] essentialProperties = new FourCC[]
                {
                    // The AVIF specification states that the a1op box must be marked as essential.
                    BoxTypes.AV1OperatingPoint,
                    // The HEIF specification states that the lsel box must be marked as essential.
                    BoxTypes.LayerSelector
                };

                for (int i = 0; i < items.Count; i++)
                {
                    ItemPropertyAssociationEntry entry = items[i];
                    IItemProperty property = itemPropertiesBox.TryGetProperty(entry.PropertyIndex);

                    if (entry.Essential)
                    {
                        if (property is null)
                        {
                            ExceptionUtil.ThrowFormatException($"ItemId { itemId } has essential properties that are not supported.");
                        }

                        // The AVIF specification states that the a1lx box must not marked as essential.
                        if (property.Type == BoxTypes.AV1LayeredImageIndexing)
                        {
                            ExceptionUtil.ThrowFormatException($"ItemId { itemId } has a property that is marked as essential, when it should not be.");
                        }
                    }
                    else
                    {
                        if (property is null)
                        {
                            // Skip any unsupported properties.
                            continue;
                        }

                        foreach (FourCC essentialProperty in essentialProperties)
                        {
                            if (property.Type == essentialProperty)
                            {
                                ExceptionUtil.ThrowFormatException($"ItemId { itemId } has a property that is not marked as essential, when it should be.");
                            }
                        }
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.reader != null)
                {
                    this.reader.Dispose();
                    this.reader = null;
                }
            }
        }

        private long CalculateExtentOffset(ulong baseOffset, ConstructionMethod constructionMethod, ItemLocationExtent extent)
        {
            if (extent is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(extent));
            }

            ulong offset;

            try
            {
                if (constructionMethod == ConstructionMethod.FileOffset)
                {
                    checked
                    {
                        offset = baseOffset + extent.Offset;

                        if ((offset + extent.Length) > this.fileLength)
                        {
                            throw new FormatException("The item has an invalid file offset.");
                        }
                    }
                }
                else if (constructionMethod == ConstructionMethod.IDatBoxOffset)
                {
                    ItemDataBox dataBox = this.metaBox.ItemData;

                    if (dataBox is null)
                    {
                        throw new FormatException("The file does not have an item data box.");
                    }

                    checked
                    {
                        if ((extent.Offset + extent.Length) > (ulong)dataBox.Length)
                        {
                            throw new FormatException("The item has an invalid data box offset.");
                        }

                        offset = (ulong)dataBox.Offset + extent.Offset;

                        if ((offset + extent.Length) > this.fileLength)
                        {
                            throw new FormatException("The item has an invalid file offset.");
                        }
                    }
                }
                else
                {
                    throw new FormatException($"ItemLocationEntry construction method { constructionMethod } is not supported.");
                }
            }
            catch (OverflowException ex)
            {
                throw new FormatException("Overflow when attempting to calculate the item file offset.", ex);
            }

            if (offset > long.MaxValue)
            {
                throw new FormatException($"The item file offset exceeds {long.MaxValue:F0} bytes.");
            }

            return (long)offset;
        }

        private void CheckForRequiredBoxes()
        {
            if (this.fileTypeBox is null)
            {
                ExceptionUtil.ThrowFormatException("The file does not contain a FileType box.");
            }

            if (this.metaBox is null)
            {
                ExceptionUtil.ThrowFormatException("The file does not contain a Meta box.");
            }

            if (this.metaBox.ItemInfo is null)
            {
                ExceptionUtil.ThrowFormatException("The file does not have an ItemInfo box.");
            }

            if (this.metaBox.ItemLocations is null)
            {
                ExceptionUtil.ThrowFormatException("The file does not have an ItemLocations box.");
            }

            if (this.metaBox.ItemProperties is null)
            {
                ExceptionUtil.ThrowFormatException("The file does not have an ItemProperties box.");
            }
        }

        private IEnumerable<IItemReferenceEntry> GetMatchingReferences(uint itemId, FourCC requiredReferenceType)
        {
            ItemReferenceBox itemReferenceBox = this.metaBox.ItemReferences;

            if (itemReferenceBox != null)
            {
                return itemReferenceBox.EnumerateMatchingReferences(itemId, requiredReferenceType);
            }
            else
            {
                return Enumerable.Empty<IItemReferenceEntry>();
            }
        }

        private bool IsAlphaChannelItem(uint itemId)
        {
            AuxiliaryTypePropertyBox auxiliaryTypeBox = TryGetAssociatedItemProperty<AuxiliaryTypePropertyBox>(itemId);

            if (auxiliaryTypeBox != null)
            {
                string auxType = auxiliaryTypeBox.AuxType.Value;

                if (string.Equals(auxType, AlphaChannelNames.AVIF, StringComparison.Ordinal) ||
                    string.Equals(auxType, AlphaChannelNames.HEVC, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void Parse()
        {
            while (this.reader.Position < this.reader.Length)
            {
                Box header = new Box(this.reader);

                if (header.Type == BoxTypes.FileType)
                {
                    if (this.fileTypeBox != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file contains multiple FileType boxes.");
                    }

                    EndianBinaryReaderSegment segment = this.reader.CreateSegment(header.DataStartOffset, header.DataLength);

                    this.fileTypeBox = new FileTypeBox(segment, header);
                    this.fileTypeBox.CheckForAvifCompatibility();
                }
                else if (header.Type == BoxTypes.Meta)
                {
                    if (this.metaBox != null)
                    {
                        ExceptionUtil.ThrowFormatException("The file contains multiple Meta boxes.");
                    }

                    EndianBinaryReaderSegment segment = this.reader.CreateSegment(header.DataStartOffset, header.DataLength);

                    this.metaBox = new MetaBox(segment, header);
                }
                else
                {
                    // Skip any other boxes
                    this.reader.Position = header.End;
                }
            }

            CheckForRequiredBoxes();
        }

        private AvifItemData ReadDataFromMultipleExtents(ItemLocationEntry entry, ulong? numberOfBytesToRead = null)
        {
            AvifItemData data;

            IReadOnlyList<ItemLocationExtent> extents = entry.Extents;
            ulong totalItemSize = numberOfBytesToRead ?? entry.TotalItemSize;

            if (totalItemSize <= ManagedAvifItemDataMaxSize)
            {
                ManagedAvifItemData managedItemData = new ManagedAvifItemData((int)totalItemSize, this.arrayPool);

                try
                {
                    int offset = 0;
                    int remainingBytes = (int)managedItemData.Length;
                    byte[] bytes = managedItemData.GetBuffer();

                    for (int i = 0; i < extents.Count; i++)
                    {
                        ItemLocationExtent extent = extents[i];

                        long itemOffset = CalculateExtentOffset(entry.BaseOffset, entry.ConstructionMethod, extent);

                        int length = (int)extent.Length;

                        if (length > remainingBytes)
                        {
                            throw new FormatException("The extent length is greater than the number of bytes remaining for the item.");
                        }

                        this.reader.Position = itemOffset;
                        this.reader.ProperRead(bytes, offset, length);

                        offset += length;
                        remainingBytes -= length;
                    }

                    if (remainingBytes > 0)
                    {
                        // This should never happen, the total item size is the sum of all the extent sizes.
                        throw new FormatException("The item has more data than was read from the extents.");
                    }

                    data = managedItemData;
                    managedItemData = null;
                }
                finally
                {
                    managedItemData?.Dispose();
                }
            }
            else
            {
                UnmanagedAvifItemData unmanagedItemData = new UnmanagedAvifItemData(totalItemSize);

                try
                {
                    ulong offset = 0;
                    ulong remainingBytes = totalItemSize;

                    for (int i = 0; i < extents.Count; i++)
                    {
                        ItemLocationExtent extent = extents[i];

                        long itemOffset = CalculateExtentOffset(entry.BaseOffset, entry.ConstructionMethod, extent);

                        ulong length = extent.Length;

                        if (length > remainingBytes)
                        {
                            throw new FormatException("The extent length is greater than the number of bytes remaining for the item.");
                        }

                        this.reader.Position = itemOffset;
                        this.reader.ProperRead(unmanagedItemData.UnmanagedBuffer, offset, length);

                        offset += length;
                        remainingBytes -= length;
                    }

                    if (remainingBytes > 0)
                    {
                        // This should never happen, the total item size is the sum of all the extent sizes.
                        throw new FormatException("The item has more data than was read from the extents.");
                    }

                    data = unmanagedItemData;
                    unmanagedItemData = null;
                }
                finally
                {
                    unmanagedItemData?.Dispose();
                }
            }

            return data;
        }

        private ImageGridDescriptor TryGetImageGridDescriptor(uint itemId)
        {
            IItemInfoEntry entry = TryGetItemInfoEntry(itemId);

            if (entry != null && entry.ItemType == ItemInfoEntryTypes.ImageGrid)
            {
                ItemLocationEntry locationEntry = TryGetItemLocation(itemId);

                if (locationEntry != null)
                {
                    if (locationEntry.TotalItemSize < ImageGridDescriptor.SmallDescriptorLength)
                    {
                        ExceptionUtil.ThrowFormatException("Invalid image grid descriptor length.");
                    }

                    using (AvifItemData itemData = ReadItemData(locationEntry))
                    {
                        Stream stream = null;

                        try
                        {
                            stream = itemData.GetStream();

                            using (EndianBinaryReader imageGridReader = new EndianBinaryReader(stream, this.reader.Endianess, this.arrayPool))
                            {
                                stream = null;

                                return new ImageGridDescriptor(imageGridReader, itemData.Length);
                            }
                        }
                        finally
                        {
                            stream?.Dispose();
                        }
                    }
                }
            }

            return null;
        }

        private sealed class AvifParserDebugView
        {
            private readonly AvifParser parser;

            public AvifParserDebugView(AvifParser parser)
            {
                this.parser = parser;
            }

            public FileTypeBox FileTypeBox => this.parser.fileTypeBox;

            public MetaBox MetaBox => this.parser.metaBox;
        }
    }
}
