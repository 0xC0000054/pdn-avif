////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020-2025 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace AvifFileType.AvifContainer
{
    internal interface IItemReferenceEntry
    {
        FourCC Type { get; }

        uint FromItemId { get; }

        IReadOnlyList<uint> ToItemIds { get; }
    }
}
