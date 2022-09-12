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
using System.Diagnostics;

namespace AvifFileType.Interop
{
    [DebuggerDisplay("Length = {ByteLength}")]
    internal abstract class CompressedAV1Data
        : Disposable, IPinnableBuffer
    {
        protected CompressedAV1Data(ulong size)
        {
            this.ByteLength = size;
        }

        public ulong ByteLength { get; }

        public void Write(BigEndianBinaryWriter writer)
        {
            if (writer is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(writer));
            }

            VerifyNotDisposed();

            WriteBuffer(writer);
        }

        protected abstract IntPtr PinBuffer();

        protected abstract void UnpinBuffer();

        protected abstract void WriteBuffer(BigEndianBinaryWriter writer);

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
