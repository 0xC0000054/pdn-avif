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
    internal sealed class ItemDataBox : Box
    {
        public ItemDataBox(Box header) : base(header)
        {
        }

        public long Offset => this.BoxDataStartOffset;

        public long Length => this.BoxDataSize;
    }
}
