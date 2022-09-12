////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.IO;

namespace AvifFileType
{
    internal unsafe delegate void UseBufferPointerDelegate(byte* ptr, ulong length);

    internal abstract class AvifItemData
        : IDisposable
    {
        private bool disposed;

        protected AvifItemData()
        {
            this.disposed = false;
        }

        public ulong Length { get; protected set; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public Stream GetStream()
        {
            VerifyNotDisposed();

            return GetStreamImpl();
        }

        public unsafe void UseBufferPointer(UseBufferPointerDelegate action)
        {
            if (action is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(action));
            }

            VerifyNotDisposed();

            UseBufferPointerImpl(action);
        }

        protected virtual void Dispose(bool disposing)
        {
            this.disposed = true;
        }

        protected abstract Stream GetStreamImpl();

        protected abstract unsafe void UseBufferPointerImpl(UseBufferPointerDelegate action);

        protected void VerifyNotDisposed()
        {
            if (this.disposed)
            {
                ExceptionUtil.ThrowObjectDisposedException(GetType().Name);
            }
        }
    }
}
