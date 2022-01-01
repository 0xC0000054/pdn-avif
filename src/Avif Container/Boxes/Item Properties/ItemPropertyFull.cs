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
    internal abstract class ItemPropertyFull
        : FullBox, IItemProperty
    {
        protected ItemPropertyFull(in EndianBinaryReaderSegment reader, Box header)
            : base(reader, header)
        {
        }

        protected ItemPropertyFull(byte version, uint flags, FourCC type)
            : base(version, flags, type)
        {
        }
    }
}
