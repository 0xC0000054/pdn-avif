////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

namespace AvifFileType.AvifContainer
{
    internal static class AvifBrands
    {
        public static readonly FourCC AVIF = new FourCC('a', 'v', 'i', 'f');
        public static readonly FourCC AVIS = new FourCC('a', 'v', 'i', 's');

        public static readonly FourCC MA1A = new FourCC('M', 'A', '1', 'A');
        public static readonly FourCC MA1B = new FourCC('M', 'A', '1', 'B');
        public static readonly FourCC MIAF = new FourCC('m', 'i', 'a', 'f');
        public static readonly FourCC MIF1 = new FourCC('m', 'i', 'f', '1');

    }
}
