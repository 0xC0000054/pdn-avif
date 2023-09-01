////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

namespace AvifFileType.AvifContainer
{
    internal sealed class AV01ItemInfoEntryBox
        : ItemInfoEntryBox
    {
        public AV01ItemInfoEntryBox(uint itemId, string? name)
            : base(itemId, 0, ItemInfoEntryTypes.AV01, name)
        {
        }
    }
}
