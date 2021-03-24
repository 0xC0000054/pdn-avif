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
using System.Diagnostics;

namespace AvifFileType.Interop
{
    [DebuggerDisplay("Length = {ByteLength}")]
    internal abstract class CompressedAV1Data
        : IDisposable, IPinnableBuffer
    {
        private bool disposed;

        protected CompressedAV1Data(ulong size)
        {
            this.ByteLength = size;
            this.disposed = false;
        }

        public ulong ByteLength { get; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void Write(BigEndianBinaryWriter writer)
        {
            if (writer is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(writer));
            }

            VerifyNotDisposed();

            WriteBuffer(writer);
        }

        protected virtual void Dispose(bool disposing)
        {
            this.disposed = true;
        }

        protected abstract IntPtr PinBuffer();

        protected abstract void UnpinBuffer();

        protected abstract void WriteBuffer(BigEndianBinaryWriter writer);

        private void VerifyNotDisposed()
        {
            if (this.disposed)
            {
                ExceptionUtil.ThrowObjectDisposedException(GetType().Name);
            }
        }

        IntPtr IPinnableBuffer.Pin()
        {
            VerifyNotDisposed();

            return PinBuffer();
        }

        void IPinnableBuffer.Unpin()
        {
            UnpinBuffer();
        }
    }
}
