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
    internal static class ColorInformationBoxTypes
    {
        public static readonly FourCC IccProfile = new FourCC('p', 'r', 'o', 'f');
        public static readonly FourCC Nclx = new FourCC('n', 'c', 'l', 'x');
        public static readonly FourCC RestrictedIccProfile = new FourCC('r', 'i', 'c', 'c');
    }
}
