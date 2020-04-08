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

namespace AvifFileType.AvifContainer
{
    internal class MimeItemInfoEntryBox
        : ItemInfoEntryBox
    {
        public MimeItemInfoEntryBox(EndianBinaryReader reader, ItemInfoEntryBox header)
            : base(header)
        {
            this.ContentType = reader.ReadBoxString(header.End);
            if (reader.Position < header.End)
            {
                this.ContentEncoding = reader.ReadBoxString(header.End);
            }
        }

        public MimeItemInfoEntryBox(ushort itemId, ushort itemProtectionIndex, string name, string contentType)
            : this(itemId, itemProtectionIndex, name, contentType, null)
        {
        }

        public MimeItemInfoEntryBox(ushort itemId, ushort itemProtectionIndex, string name, string contentType, string contentEncoding)
            : base(itemId, itemProtectionIndex, ItemInfoEntryTypes.Mime, name)
        {
            if (contentType is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(contentType));
            }

            this.ContentType = new BoxString(contentType);
            this.ContentEncoding = contentEncoding is null ? null : new BoxString(contentEncoding);
        }

        internal BoxString ContentType { get; }

        internal BoxString ContentEncoding { get; }

        public sealed override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            this.ContentType.Write(writer);
            this.ContentEncoding?.Write(writer);
        }

        protected sealed override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + this.ContentType.GetSize() + this.ContentEncoding?.GetSize() ?? 0;
        }
    }
}
