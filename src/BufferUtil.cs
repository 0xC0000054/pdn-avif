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
using System.Runtime.Intrinsics;

namespace AvifFileType
{
    internal static class BufferUtil
    {
        /// <summary>
        /// Determines whether all of the masked values are equal to the specified value.
        /// </summary>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="mask">The mask to apply before comparing the values.</param>
        /// <param name="compare">The value to use for the comparison.</param>
        /// <param name="elementCount">The number of elements to compare.</param>
        /// <returns>
        /// <see langword="true"/> if all the masked values in <paramref name="buffer"/> are equal to <paramref name="compare"/>;
        /// otherwise <see langword="false".
        /// </returns>
        internal static unsafe bool BitwiseAllEqualToMasked(uint* buffer, uint mask, uint compare, int elementCount)
        {
            uint* pBuffer = buffer;
            int count = elementCount;

            if (Vector256.IsHardwareAccelerated)
            {
                if (count >= 8)
                {
                    Vector256<uint> allBitsSet = Vector256<uint>.AllBitsSet;
                    Vector256<uint> mask2 = Vector256.Create(mask);
                    Vector256<uint> vecCompare = Vector256.Create(compare);

                    while (count >= 32)
                    {
                        Vector256<uint> src1 = Vector256.Load(pBuffer);
                        Vector256<uint> src2 = Vector256.Load(pBuffer + 8);
                        Vector256<uint> src3 = Vector256.Load(pBuffer + 16);
                        Vector256<uint> src4 = Vector256.Load(pBuffer + 24);

                        Vector256<uint> srcMasked1 = src1 & mask2;
                        Vector256<uint> srcMasked2 = src2 & mask2;
                        Vector256<uint> srcMasked3 = src3 & mask2;
                        Vector256<uint> srcMasked4 = src4 & mask2;

                        Vector256<uint> srcEqual1 = Vector256.Equals(srcMasked1, vecCompare);
                        Vector256<uint> srcEqual2 = Vector256.Equals(srcMasked2, vecCompare);
                        Vector256<uint> srcEqual3 = Vector256.Equals(srcMasked3, vecCompare);
                        Vector256<uint> srcEqual4 = Vector256.Equals(srcMasked4, vecCompare);

                        Vector256<uint> result12 = Vector256.BitwiseAnd(srcEqual1, srcEqual2);
                        Vector256<uint> result34 = Vector256.BitwiseAnd(srcEqual3, srcEqual4);
                        Vector256<uint> result1234 = Vector256.BitwiseAnd(result12, result34);

                        if (!result1234.Equals(allBitsSet))
                        {
                            return false;
                        }

                        pBuffer += 32;
                        count -= 32;
                    }

                    while (count >= 8)
                    {
                        Vector256<uint> src = Vector256.Load(pBuffer);

                        Vector256<uint> srcMasked = src & mask2;

                        if (!srcMasked.Equals(vecCompare))
                        {
                            return false;
                        }

                        pBuffer += 8;
                        count -= 8;
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                if (count >= 4)
                {
                    Vector128<uint> allBitsSet = Vector128<uint>.AllBitsSet;
                    Vector128<uint> mask2 = Vector128.Create(mask);
                    Vector128<uint> vecCompare = Vector128.Create(compare);

                    while (count >= 16)
                    {
                        Vector128<uint> src1 = Vector128.Load(pBuffer);
                        Vector128<uint> src2 = Vector128.Load(pBuffer + 4);
                        Vector128<uint> src3 = Vector128.Load(pBuffer + 8);
                        Vector128<uint> src4 = Vector128.Load(pBuffer + 12);

                        Vector128<uint> srcMasked1 = src1 & mask2;
                        Vector128<uint> srcMasked2 = src2 & mask2;
                        Vector128<uint> srcMasked3 = src3 & mask2;
                        Vector128<uint> srcMasked4 = src4 & mask2;

                        Vector128<uint> srcEqual1 = Vector128.Equals(srcMasked1, vecCompare);
                        Vector128<uint> srcEqual2 = Vector128.Equals(srcMasked2, vecCompare);
                        Vector128<uint> srcEqual3 = Vector128.Equals(srcMasked3, vecCompare);
                        Vector128<uint> srcEqual4 = Vector128.Equals(srcMasked4, vecCompare);

                        Vector128<uint> result12 = Vector128.BitwiseAnd(srcEqual1, srcEqual2);
                        Vector128<uint> result34 = Vector128.BitwiseAnd(srcEqual3, srcEqual4);
                        Vector128<uint> result1234 = Vector128.BitwiseAnd(result12, result34);

                        if (!result1234.Equals(allBitsSet))
                        {
                            return false;
                        }

                        pBuffer += 16;
                        count -= 16;
                    }

                    while (count >= 4)
                    {
                        Vector128<uint> src = Vector128.Load(pBuffer);

                        Vector128<uint> srcMasked = src & mask2;

                        if (!srcMasked.Equals(vecCompare))
                        {
                            return false;
                        }

                        pBuffer += 4;
                        count -= 4;
                    }
                }
            }

            while (count > 0)
            {
                if ((*pBuffer & mask) != compare)
                {
                    return false;
                }

                pBuffer++;
                count--;
            }

            return true;
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
