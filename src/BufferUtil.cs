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

using System;

namespace AvifFileType
{
    internal static class BufferUtil
    {
        /// <summary>
        /// Compares two memory blocks for equality up to the specified length.
        /// </summary>
        /// <param name="a">A pointer to the start of the first memory block.</param>
        /// <param name="b">A pointer to the start of the second memory block.</param>
        /// <param name="length">The number of bytes to compare.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="a"/> equals <paramref name="b"/> up to a length of <paramref name="length"/>;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Because this method operates on pointers, it cannot do any bounds checking.
        /// </remarks>
        internal static unsafe bool BitwiseEquals(nint a, nint b, ulong length)
        {
            return BitwiseEquals((byte*)a, (byte*)b, length);
        }

        /// <summary>
        /// Compares two memory blocks for equality up to the specified length.
        /// </summary>
        /// <param name="a">A pointer to the start of the first memory block.</param>
        /// <param name="b">A pointer to the start of the second memory block.</param>
        /// <param name="length">The number of bytes to compare.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="a"/> equals <paramref name="b"/> up to a length of <paramref name="length"/>;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Because this method operates on pointers, it cannot do any bounds checking.
        /// </remarks>
        internal static unsafe bool BitwiseEquals(byte* a, byte* b, ulong length)
        {
            if (length <= int.MaxValue)
            {
                ReadOnlySpan<byte> buffer1 = new(a, (int)length);
                ReadOnlySpan<byte> buffer2 = new(b, (int)length);

                return buffer1.SequenceEqual(buffer2);
            }

            byte* pa = a;
            byte* pb = b;
            ulong remaining = length;

            while (remaining > 0)
            {
                ulong compareLength = Math.Min(remaining, int.MaxValue);

                ReadOnlySpan<byte> buffer1 = new(pa, (int)compareLength);
                ReadOnlySpan<byte> buffer2 = new(pb, (int)compareLength);

                if (!buffer1.SequenceEqual(buffer2))
                {
                    return false;
                }

                pa += compareLength;
                pb += compareLength;
                remaining -= compareLength;
            }

            return true;
        }
    }
}
