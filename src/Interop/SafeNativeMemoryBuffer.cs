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

using System;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal sealed class SafeNativeMemoryBuffer
        : SafeBuffer
    {
        public SafeNativeMemoryBuffer() : base(true)
        {
        }

        public static unsafe SafeNativeMemoryBuffer Create(ulong length)
        {
            SafeNativeMemoryBuffer buffer = new SafeNativeMemoryBuffer();

            try
            {
                // The documentation for NativeMemory.Alloc states that the memory block pointer cannot be passed
                // to NativeMemory.Free if the length is zero, so we use a length of 1 in that case.
                if (length == 0)
                {
                    length = 1;
                }

                buffer.SetHandle((IntPtr)NativeMemory.Alloc((nuint)length));
                buffer.Initialize(length);
            }
            catch (OverflowException ex)
            {
                throw new OutOfMemoryException($"Overflow when attempting to allocate {length:F0} bytes.", ex);
            }

            return buffer;
        }

        protected override bool ReleaseHandle()
        {
            unsafe
            {
                NativeMemory.Free(this.handle.ToPointer());
            }
            return true;
        }
    }
}
