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

using PaintDotNet.Imaging;
using System.Collections.Generic;

namespace AvifFileType.Exif
{
    internal static class MetadataHelpers
    {
        internal static byte[] EncodeLong(uint value)
        {
            return new byte[]
            {
                (byte)(value & 0xff),
                (byte)(value >> 8),
                (byte)(value >> 16),
                (byte)(value >> 24)
            };
        }

        internal static byte[] EncodeShort(ushort value)
        {
            return new byte[]
            {
                (byte)(value & 0xff),
                (byte)(value >> 8)
            };
        }

        internal static bool TryDecodeShort(ExifValue entry, out ushort value)
        {
            if (entry is null
                || entry.Type != ExifValueType.Short
                || entry.Data is null
                || entry.Data.Count != 2)
            {
                value = 0;
                return false;
            }

            IReadOnlyList<byte> data = entry.Data;

            value = (ushort)(data[0] | (data[1] << 8));

            return true;
        }
    }
}
