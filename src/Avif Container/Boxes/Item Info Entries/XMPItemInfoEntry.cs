////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

namespace AvifFileType.AvifContainer
{
    internal sealed class XmpItemInfoEntry
        : MimeItemInfoEntryBox
    {
        internal const string XmpContentType = "application/rdf+xml";

        public XmpItemInfoEntry(uint itemId) : base(itemId, 0, "XMP", XmpContentType)
        {
        }
    }
}
