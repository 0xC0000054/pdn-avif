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

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal sealed class UriItemEntryInfoBox
        : ItemInfoEntryBox
    {
        private readonly BoxString uri;

        public UriItemEntryInfoBox(in EndianBinaryReaderSegment reader, ItemInfoEntryBox header)
            : base(header)
        {
            this.uri = reader.ReadBoxString();
        }

        public UriItemEntryInfoBox(uint itemId, ushort itemProtectionIndex, string name, string uri)
            : base(itemId, itemProtectionIndex, ItemInfoEntryTypes.Uri, name)
        {
            if (uri is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(uri));
            }

            this.uri = new BoxString(uri);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"ItemId: { this.ItemId }, Name: \"{ this.Name }\", Uri: \"{ this.uri }\"";

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            this.uri.Write(writer);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + this.uri.GetSize();
        }
    }
}
