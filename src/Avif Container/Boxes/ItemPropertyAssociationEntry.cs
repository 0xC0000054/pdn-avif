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

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal readonly struct ItemPropertyAssociationEntry
    {
        public ItemPropertyAssociationEntry(bool essential, ushort propertyIndex)
        {
            this.Essential = essential;
            this.PropertyIndex = propertyIndex;
        }

        public bool Essential { get; }

        public uint PropertyIndex { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"Essential: { this.Essential }, PropertyIndex: { this.PropertyIndex }";
            }
        }
    }
}
