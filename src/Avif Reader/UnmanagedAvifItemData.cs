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

using AvifFileType.Interop;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AvifFileType
{
    internal sealed class UnmanagedAvifItemData
        : AvifItemData
    {
        private readonly SafeNativeMemoryBuffer buffer;

        public UnmanagedAvifItemData(ulong length)
            : base()
        {
            this.buffer = SafeNativeMemoryBuffer.Create(length);
            this.Length = length;
        }

        public SafeBuffer UnmanagedBuffer
        {
            get
            {
                VerifyNotDisposed();

                return this.buffer;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.buffer?.Dispose();
            }
        }

        protected override Stream GetStreamImpl()
        {
            // The UnmanagedMemoryStream class does not take ownership of the SafeBuffer.
            return new UnmanagedMemoryStream(this.buffer, 0, checked((long)this.Length), FileAccess.Read);
        }

        protected override unsafe void UseBufferPointerImpl(UseBufferPointerDelegate action)
        {
            byte* ptr = null;
            RuntimeHelpers.PrepareDelegate(action);
            try
            {
                this.buffer.AcquirePointer(ref ptr);

                action(ptr, this.Length);
            }
            finally
            {
                if (ptr != null)
                {
                    this.buffer.ReleasePointer();
                }
            }
        }
    }
}
