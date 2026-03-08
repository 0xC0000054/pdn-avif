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

using PaintDotNet.FileTypes;

namespace AvifFileType
{
    public sealed class AvifFileTypeFactory
        : IFileTypeFactory
    {
        public IFileType[] CreateFileTypes(IFileTypeHost host)
        {
            return [new AvifFileTypePlugin(host)];
        }
    }
}
