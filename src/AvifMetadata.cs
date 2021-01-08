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

namespace AvifFileType
{
    internal sealed class AvifMetadata
    {
        private readonly byte[] exifBytes;
        private readonly byte[] iccProfileBytes;
        private readonly byte[] xmpBytes;

        public AvifMetadata(byte[] exifBytes, byte[] iccProfileBytes, byte[] xmpBytes)
        {
            this.exifBytes = exifBytes;
            this.iccProfileBytes = iccProfileBytes;
            this.xmpBytes = xmpBytes;
        }

        public byte[] GetExifBytesReadOnly()
        {
            return this.exifBytes;
        }

        public byte[] GetICCProfileBytesReadOnly()
        {
            return this.iccProfileBytes;
        }

        public byte[] GetXmpBytesReadOnly()
        {
            return this.xmpBytes;
        }
    }
}
