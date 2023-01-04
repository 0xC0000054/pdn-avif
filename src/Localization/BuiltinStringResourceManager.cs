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

using AvifFileType.Properties;

namespace AvifFileType
{
    internal sealed class BuiltinStringResourceManager
        : IAvifStringResourceManager
    {
        public string GetString(string name)
        {
            return Resources.ResourceManager.GetString(name);
        }
    }
}
