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

namespace AvifFileType.AvifContainer
{
    internal static class ReferenceTypes
    {
        public static readonly FourCC AuxiliaryImage = new FourCC('a', 'u', 'x', 'l');
        public static readonly FourCC ContentDescription = new FourCC('c', 'd', 's', 'c');
        public static readonly FourCC DerivedImage = new FourCC('d', 'i', 'm', 'g');
        public static readonly FourCC PremultipliedAlphaImage = new FourCC('p', 'r', 'e', 'm');
    }
}
