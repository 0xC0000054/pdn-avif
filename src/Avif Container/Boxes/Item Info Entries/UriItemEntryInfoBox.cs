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
    internal sealed class UriItemEntryInfoBox
        : ItemInfoEntryBox
    {
        private readonly BoxString uri;

        public UriItemEntryInfoBox(EndianBinaryReader reader, ItemInfoEntryBox header)
            : base(header)
        {
            this.uri = reader.ReadBoxString(header.End);
        }

        public UriItemEntryInfoBox(ushort itemId, ushort itemProtectionIndex, string name, string uri)
            : base(itemId, itemProtectionIndex, ItemInfoEntryTypes.Uri, name)
        {
            if (uri is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(uri));
            }

            this.uri = new BoxString(uri);
        }

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
