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
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;

namespace AvifFileType
{
    internal static class StreamExtensions
    {
        [SkipLocalsInit]
        public static long TryReadUInt32BigEndian(this Stream stream)
        {
            Span<byte> bytes = stackalloc byte[sizeof(uint)];

            int bytesRead = stream.ReadAtLeast(bytes, sizeof(uint), throwOnEndOfStream: false);

            return bytesRead == sizeof(uint) ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : -1;
        }
    }
}
