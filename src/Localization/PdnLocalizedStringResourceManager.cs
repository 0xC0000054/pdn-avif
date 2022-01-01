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

using PaintDotNet.Avif;
using System;
using System.Linq;
using System.Collections.Generic;
using AvifFileType.Properties;

namespace AvifFileType
{
    internal sealed class PdnLocalizedStringResourceManager
        : IAvifStringResourceManager
    {
        private readonly IAvifFileTypeStrings strings;
        private static readonly IReadOnlyDictionary<string, AvifFileTypeStringNames> pdnLocalizedStringMap;

        static PdnLocalizedStringResourceManager()
        {
            // Use a dictionary to map the resource name to its enumeration value.
            // This avoids repeated calls to Enum.TryParse.
            // Adapted from https://stackoverflow.com/a/13677446
            pdnLocalizedStringMap = Enum.GetValues<AvifFileTypeStringNames>()
                                        .ToDictionary(kv => kv.ToString(), kv => kv, StringComparer.OrdinalIgnoreCase);
        }

        public PdnLocalizedStringResourceManager(IAvifFileTypeStrings strings)
        {
            this.strings = strings;
        }

        public string GetString(string name)
        {
            if (pdnLocalizedStringMap.TryGetValue(name, out AvifFileTypeStringNames value))
            {
                return this.strings?.TryGetString(value) ?? Resources.ResourceManager.GetString(name);
            }
            else
            {
                return Resources.ResourceManager.GetString(name);
            }
        }
    }
}
