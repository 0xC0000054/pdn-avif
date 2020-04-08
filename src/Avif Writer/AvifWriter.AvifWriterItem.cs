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
using System;
using System.Diagnostics;

namespace AvifFileType
{
    internal sealed partial class AvifWriter
    {
        [DebuggerDisplay("{DebuggerDisplay, nq}")]
        private sealed class AvifWriterItem
        {
            private AvifWriterItem(ushort id, string name, CompressedAV1Image image, bool isAlphaImage)
            {
                if (image is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(image));
                }

                if (name is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(name));
                }

                this.Id = id;
                this.Name = name;
                this.Image = image;
                this.IsAlphaImage = isAlphaImage;
                this.ContentBytes = null;
                this.ItemInfoEntry = new AV01ItemInfoEntryBox(id, name);
                this.ItemLocation = new ItemLocationEntry(id, image.Data.ByteLength);
            }

            private AvifWriterItem(ushort id, string name, byte[] contentBytes, ItemInfoEntryBox itemInfo)
            {
                if (contentBytes is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(contentBytes));
                }

                if (name is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(name));
                }

                if (itemInfo is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(itemInfo));
                }

                this.Id = id;
                this.Name = name;
                this.Image = null;
                this.IsAlphaImage = false;
                this.ContentBytes = contentBytes;
                this.ItemInfoEntry = itemInfo;
                this.ItemLocation = new ItemLocationEntry(id, (ulong)contentBytes.Length);
            }

            public ushort Id { get; }

            public string Name { get; }

            public CompressedAV1Image Image { get; }

            public bool IsAlphaImage { get; }

            public byte[] ContentBytes { get; }

            public ItemInfoEntryBox ItemInfoEntry { get; }

            public ItemLocationEntry ItemLocation { get; }

            public ItemReferenceEntryBox ItemReference { get; set; }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string DebuggerDisplay => $"{ this.Id }, { this.Name }";

            public static AvifWriterItem CreateFromImage(ushort itemId, string name, CompressedAV1Image image, bool isAlphaImage)
            {
                return new AvifWriterItem(itemId, name, image, isAlphaImage);
            }

            public static AvifWriterItem CreateFromExif(ushort itemId, byte[] exif)
            {
                // The AVIF format includes the offset to the start of the TIFF header
                // before the EXIF data.
                // The EXIF blob that this plug-in creates will always have the TIFF header
                // at offset 0, so we only need to copy the EXIF data to a new byte array.

                byte[] contentBytes = new byte[sizeof(uint) + exif.Length];
                Buffer.BlockCopy(exif, 0, contentBytes, 4, exif.Length);

                ExifItemInfoEntry exifItemInfo = new ExifItemInfoEntry(itemId);

                return new AvifWriterItem(itemId, "Exif", contentBytes, exifItemInfo);
            }

            public static AvifWriterItem CreateFromXmp(ushort itemId, byte[] xmp)
            {
                XmpItemInfoEntry xmpItemInfo = new XmpItemInfoEntry(itemId);

                return new AvifWriterItem(itemId, "XMP", xmp, xmpItemInfo);
            }
        }
    }
}
