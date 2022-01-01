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

namespace AvifFileType.AvifContainer
{
    internal abstract class ItemProperty
        : Box, IItemProperty
    {
        protected ItemProperty(Box header)
            : base(header)
        {
        }

        protected ItemProperty(FourCC type)
            : base(type)
        {
        }
    }
}
