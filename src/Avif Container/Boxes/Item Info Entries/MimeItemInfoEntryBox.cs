////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal class MimeItemInfoEntryBox
        : ItemInfoEntryBox
    {
        public MimeItemInfoEntryBox(in EndianBinaryReaderSegment reader, ItemInfoEntryBox header)
            : base(header)
        {
            this.ContentType = reader.ReadBoxString();
            if (reader.Position < reader.EndOffset)
            {
                this.ContentEncoding = reader.ReadBoxString();
            }
        }

        public MimeItemInfoEntryBox(uint itemId,
                                    bool hiddenItem,
                                    ushort itemProtectionIndex,
                                    string name,
                                    string contentType)
            : this(itemId, hiddenItem, itemProtectionIndex, name, contentType, null)
        {
        }

        public MimeItemInfoEntryBox(uint itemId,
                                    bool hiddenItem,
                                    ushort itemProtectionIndex,
                                    string name,
                                    string contentType,
                                    string contentEncoding)
            : base(itemId, hiddenItem, itemProtectionIndex, ItemInfoEntryTypes.Mime, name)
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

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                if (this.ContentEncoding is null)
                {
                    return $"ItemId: { this.ItemId }, Name: \"{ this.Name }\", ContentType: \"{ this.ContentType }\"";
                }
                else
                {
                    return $"ItemId: { this.ItemId }, Name: \"{ this.Name }\", ContentType: \"{ this.ContentType }\", Encoding: \"{ this.ContentEncoding }\"";
                }
            }
        }

        public sealed override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            this.ContentType.Write(writer);
            this.ContentEncoding?.Write(writer);
        }

        protected sealed override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + this.ContentType.GetSize() + (this.ContentEncoding?.GetSize() ?? 0);
        }
    }
}
