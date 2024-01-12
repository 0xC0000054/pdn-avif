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

using System;

namespace AvifFileType
{
    internal sealed class AvifMetadata
    {
        private readonly ReadOnlyMemory<byte> exifBytes;
        private readonly ReadOnlyMemory<byte> iccProfileBytes;
        private readonly ReadOnlyMemory<byte> xmpBytes;

        public AvifMetadata(ReadOnlyMemory<byte> exifBytes,
                            ReadOnlyMemory<byte> iccProfileBytes,
                            ReadOnlyMemory<byte> xmpBytes)
        {
            this.exifBytes = exifBytes;
            this.iccProfileBytes = iccProfileBytes;
            this.xmpBytes = xmpBytes;
        }

        public ReadOnlyMemory<byte> Exif => this.exifBytes;

        public ReadOnlyMemory<byte> IccProfile => this.iccProfileBytes;

        public ReadOnlyMemory<byte> Xmp => this.xmpBytes;
    }
}
