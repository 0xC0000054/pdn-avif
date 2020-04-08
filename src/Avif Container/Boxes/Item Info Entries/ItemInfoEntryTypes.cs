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
    internal static class ItemInfoEntryTypes
    {
        public static readonly FourCC AV01 = new FourCC('a', 'v', '0', '1');
        public static readonly FourCC Exif = new FourCC('E', 'x', 'i', 'f');
        public static readonly FourCC ImageGrid = new FourCC('g', 'r', 'i', 'd');
        public static readonly FourCC Mime = new FourCC('m', 'i', 'm', 'e');
        public static readonly FourCC Uri = new FourCC('u', 'r', 'i', ' ');
    }
}
