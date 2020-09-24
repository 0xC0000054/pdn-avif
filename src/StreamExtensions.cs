////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AvifFileType
{
    internal static class StreamExtensions
    {
        public static long TryReadUInt32BigEndian(this Stream stream)
        {
            int byte1 = stream.ReadByte();
            if (byte1 == -1)
            {
                return -1;
            }

            int byte2 = stream.ReadByte();
            if (byte2 == -1)
            {
                return -1;
            }

            int byte3 = stream.ReadByte();
            if (byte3 == -1)
            {
                return -1;
            }

            int byte4 = stream.ReadByte();
            if (byte4 == -1)
            {
                return -1;
            }

            return (byte1 << 24) | (byte2 << 16) | (byte3 << 8) | byte4;
        }
    }
}
