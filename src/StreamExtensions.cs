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
        // The largest multiple of 4096 that is under the large object heap limit.
        private const int MaxBufferSize = 81920;

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

        public static unsafe ulong ProperRead(this Stream stream, SafeBuffer buffer, ulong length)
        {
            if (buffer is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(buffer));
            }

            if (length == 0)
            {
                return 0;
            }

            int bufferSize = (int)Math.Min(length, MaxBufferSize);
            byte[] readBuffer = new byte[bufferSize];

            ulong totalBytesRead = 0;

            byte* writePtr = null;
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                buffer.AcquirePointer(ref writePtr);

                fixed (byte* readPtr = readBuffer)
                {
                    while (totalBytesRead < length)
                    {
                        int bytesRead = stream.Read(readBuffer, 0, (int)Math.Min(length - totalBytesRead, MaxBufferSize));

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        Buffer.MemoryCopy(readPtr, writePtr + totalBytesRead, bytesRead, bytesRead);

                        totalBytesRead += (ulong)bytesRead;
                    }
                }
            }
            finally
            {
                if (writePtr != null)
                {
                    buffer.ReleasePointer();
                }
            }

            return totalBytesRead;
        }

        public static unsafe void Write(this Stream stream, SafeBuffer buffer, ulong length)
        {
            if (buffer is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(buffer));
            }

            if (length == 0)
            {
                return;
            }

            int bufferSize = (int)Math.Min(length, MaxBufferSize);
            byte[] writeBuffer = new byte[bufferSize];

            byte* readPtr = null;
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                buffer.AcquirePointer(ref readPtr);

                fixed (byte* writePtr = writeBuffer)
                {
                    ulong totalBytesRead = 0;

                    while (totalBytesRead < length)
                    {
                        ulong bytesRead = Math.Min(length - totalBytesRead, MaxBufferSize);

                        Buffer.MemoryCopy(readPtr + totalBytesRead, writePtr, bytesRead, bytesRead);

                        stream.Write(writeBuffer, 0, (int)bytesRead);

                        totalBytesRead += bytesRead;
                    }
                }
            }
            finally
            {
                if (readPtr != null)
                {
                    buffer.ReleasePointer();
                }
            }
        }
    }
}
