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
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal sealed class SafeProcessHeapBuffer
        : SafeBuffer
    {
        private static readonly IntPtr ProcessHeap = UnsafeNativeMethods.GetProcessHeap();

        private SafeProcessHeapBuffer() : base(true)
        {
        }

        public static SafeProcessHeapBuffer Create(ulong length)
        {
            try
            {
                SafeProcessHeapBuffer buffer = UnsafeNativeMethods.HeapAlloc(ProcessHeap, 0, (UIntPtr)length);

                if (buffer.IsInvalid)
                {
                    throw new OutOfMemoryException();
                }

                buffer.Initialize(length);

                return buffer;
            }
            catch (OverflowException ex)
            {
                throw new OutOfMemoryException($"Overflow when attempting to allocate {length:F0} bytes.", ex);
            }
        }

        protected override bool ReleaseHandle()
        {
            return UnsafeNativeMethods.HeapFree(ProcessHeap, 0, this.handle);
        }

        [System.Security.SuppressUnmanagedCodeSecurity]
        private static class UnsafeNativeMethods
        {
            [DllImport("kernel32.dll", ExactSpelling = true)]
            internal static extern IntPtr GetProcessHeap();

            [DllImport("kernel32.dll", ExactSpelling = true)]
            internal static extern SafeProcessHeapBuffer HeapAlloc(IntPtr hHeap, uint dwFlags, UIntPtr dwSize);

            [DllImport("kernel32.dll", ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);
        }
    }
}
