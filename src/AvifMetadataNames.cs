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

namespace AvifFileType
{
    internal static class AvifMetadataNames
    {
        internal const string CICPMetadataName = "AvifCICPData";
        internal const string ImageGridName = "AvifImageGrid";
        // This value is no longer written, but it is retained to
        // allow the data to be read from existing PDN files.
        internal const string NclxMetadataName = "AvifNclxData";
    }
}
