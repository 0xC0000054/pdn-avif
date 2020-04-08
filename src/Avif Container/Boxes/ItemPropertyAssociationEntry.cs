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
    internal readonly struct ItemPropertyAssociationEntry
    {
        public ItemPropertyAssociationEntry(bool essential, ushort propertyIndex)
        {
            this.Essential = essential;
            this.PropertyIndex = propertyIndex;
        }

        public bool Essential { get; }

        public uint PropertyIndex { get; }
    }
}
