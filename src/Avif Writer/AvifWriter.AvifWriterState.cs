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
using System.Collections.Generic;

namespace AvifFileType
{
    internal sealed partial class AvifWriter
    {
        private sealed class AvifWriterState
        {
            private readonly List<AvifWriterItem> items;

            public AvifWriterState(CompressedAV1Image color, CompressedAV1Image alpha, AvifMetadata metadata)
            {
                if (color is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(color));
                }

                if (metadata is null)
                {
                    ExceptionUtil.ThrowArgumentNullException(nameof(metadata));
                }

                this.items = new List<AvifWriterItem>();
                // The item ids start at 1.
                this.PrimaryItemId = 1;
                Initialize(color, alpha, metadata);
            }

            public IReadOnlyList<AvifWriterItem> Items => this.items;

            public ushort PrimaryItemId { get; }

            public ulong TotalDataSize { get; private set; }

            private void Initialize(CompressedAV1Image color, CompressedAV1Image alpha, AvifMetadata metadata)
            {
                ulong totalDataSize = color.Data.ByteLength;

                ushort itemId = this.PrimaryItemId;

                AvifWriterItem colorItem = AvifWriterItem.CreateFromImage(itemId, "Color", color, false);
                this.items.Add(colorItem);

                if (alpha != null)
                {
                    itemId++;
                    AvifWriterItem alphaItem = AvifWriterItem.CreateFromImage(itemId, "Alpha", alpha, true);
                    alphaItem.ItemReference = new ItemReferenceEntryBox(alphaItem.Id, ReferenceTypes.AuxiliaryImage, this.PrimaryItemId);

                    this.items.Add(alphaItem);
                    totalDataSize += alpha.Data.ByteLength;
                }

                byte[] exif = metadata.GetExifBytesReadOnly();
                if (exif != null && exif.Length > 0)
                {
                    itemId++;
                    AvifWriterItem exifItem = AvifWriterItem.CreateFromExif(itemId, exif);
                    exifItem.ItemReference = new ItemReferenceEntryBox(exifItem.Id, ReferenceTypes.ContentDescription, this.PrimaryItemId);

                    this.items.Add(exifItem);
                    totalDataSize += (ulong)exifItem.ContentBytes.Length;
                }

                byte[] xmp = metadata.GetXmpBytesReadOnly();
                if (xmp != null && xmp.Length > 0)
                {
                    itemId++;
                    AvifWriterItem xmpItem = AvifWriterItem.CreateFromExif(itemId, xmp);
                    xmpItem.ItemReference = new ItemReferenceEntryBox(xmpItem.Id, ReferenceTypes.ContentDescription, this.PrimaryItemId);

                    this.items.Add(xmpItem);
                    totalDataSize += (ulong)xmpItem.ContentBytes.Length;
                }

                this.TotalDataSize = totalDataSize;
            }
        }
    }
}
