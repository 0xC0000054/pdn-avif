﻿////////////////////////////////////////////////////////////////////////
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

namespace AvifFileType.ICCProfile
{
    internal enum ProfileClass : uint
    {
        Input = 0x73636E72,       // 'scnr'
        Display = 0x6D6E7472,     // 'mntr'
        Output = 0x70727472,      // 'prtr'
        Link = 0x6C696E6B,        // 'link'
        Abstract = 0x61627374,    // 'abst'
        ColorSpace = 0x73706163,  // 'spac'
        NamedColor = 0x6e6d636c   // 'nmcl'
    }
}
