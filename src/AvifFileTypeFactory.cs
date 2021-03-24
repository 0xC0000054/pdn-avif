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

using PaintDotNet;

namespace AvifFileType
{
    public sealed class AvifFileTypeFactory
        : IFileTypeFactory2
    {
        public FileType[] GetFileTypeInstances(IFileTypeHost host)
        {
            return new[] { new AvifFileTypePlugin(host) };
        }
    }
}
