////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.AvifContainer;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType
{
    internal sealed partial class AvifWriter
    {
        [DebuggerDisplay("{DebuggerDisplay, nq}")]
        private sealed class AvifWriterItem
        {
            private AvifWriterItem(uint id,
                                   string? name,
                                   CompressedAV1Image image,
                                   bool isAlphaImage,
                                   int duplicateImageIndex)
            {
                if (image is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(image));
                }

                this.Id = id;
                this.Name = name ?? string.Empty;
                this.Image = image;
                this.IsAlphaImage = isAlphaImage;
                this.DuplicateImageIndex = duplicateImageIndex;
                this.ContentBytes = ReadOnlyMemory<byte>.Empty;
                this.ItemInfoEntry = new AV01ItemInfoEntryBox(id, name);
                this.ItemLocation = new ItemLocationEntry(id, image.Data.ByteLength);
                this.ItemReferences = [];
            }

            private AvifWriterItem(uint id, string name, ReadOnlyMemory<byte> contentBytes, ItemInfoEntryBox itemInfo)
            {
                if (itemInfo is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(itemInfo));
                }

                this.Id = id;
                this.Name = name;
                this.Image = null;
                this.IsAlphaImage = false;
                this.DuplicateImageIndex = -1;
                this.ContentBytes = contentBytes;
                this.ItemInfoEntry = itemInfo;
                this.ItemLocation = new ItemLocationEntry(id, (ulong)contentBytes.Length);
                this.ItemReferences = [];
            }

            private AvifWriterItem(uint id, string? name, ulong dataBoxOffset, ulong length)
            {
                if (name is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(name));
                }

                this.Id = id;
                this.Name = name;
                this.Image = null;
                this.IsAlphaImage = false;
                this.DuplicateImageIndex = -1;
                this.ContentBytes = ReadOnlyMemory<byte>.Empty;
                this.ItemInfoEntry = new ImageGridItemInfoEntryBox(id, name);
                this.ItemLocation = new ItemLocationEntry(id, dataBoxOffset, length);
                this.ItemReferences = [];
            }

            public uint Id { get; }

            public string Name { get; }

            public CompressedAV1Image? Image { get; }

            public bool IsAlphaImage { get; }

            public int DuplicateImageIndex { get; }

            public ReadOnlyMemory<byte> ContentBytes { get; }

            public ItemInfoEntryBox ItemInfoEntry { get; }

            public ItemLocationEntry ItemLocation { get; }

            public List<ItemReferenceEntryBox> ItemReferences { get; }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string DebuggerDisplay
            {
                get
                {
                    if (string.IsNullOrEmpty(this.Name))
                    {
                        return this.Id.ToString();
                    }
                    else
                    {
                        return $"{ this.Id }, { this.Name }";
                    }
                }
            }

            public static AvifWriterItem CreateFromImage(uint itemId,
                                                         string? name,
                                                         CompressedAV1Image image,
                                                         bool isAlphaImage,
                                                         int duplicateImageIndex)
            {
                return new AvifWriterItem(itemId, name, image, isAlphaImage, duplicateImageIndex);
            }

            public static AvifWriterItem CreateFromImageGrid(uint itemId, string? name, ulong dataBoxOffset, ulong length)
            {
                return new AvifWriterItem(itemId, name, dataBoxOffset, length);
            }

            public static AvifWriterItem CreateFromExif(uint itemId, ReadOnlyMemory<byte> exif)
            {
                // The EXIF data block has a header consisting of a big-endian 4-byte unsigned integer
                // that indicates the number of bytes that come before the start of the TIFF header.
                // See ISO/IEC 23008-12:2017 section A.2.1.
                //
                // The EXIF blob that this plug-in creates will always have the TIFF header
                // at offset 0 (i.e. immediately after the offset value).

                Memory<byte> contentBytes = GC.AllocateUninitializedArray<byte>(checked(sizeof(uint) + exif.Length));
                BinaryPrimitives.WriteUInt32BigEndian(contentBytes.Span, 0);
                exif.CopyTo(contentBytes.Slice(4));

                ExifItemInfoEntry exifItemInfo = new ExifItemInfoEntry(itemId);

                return new AvifWriterItem(itemId, "Exif", contentBytes, exifItemInfo);
            }

            public static AvifWriterItem CreateFromXmp(uint itemId, ReadOnlyMemory<byte> xmp)
            {
                XmpItemInfoEntry xmpItemInfo = new XmpItemInfoEntry(itemId);

                return new AvifWriterItem(itemId, "XMP", xmp, xmpItemInfo);
            }
        }
    }
}
