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
using System.Diagnostics;

namespace AvifFileType.Interop
{
    [DebuggerDisplay("Length = {ByteLength}")]
    internal abstract class CompressedAV1Data
        : Disposable, IEquatable<CompressedAV1Data>, IPinnableBuffer
    {
        protected CompressedAV1Data(ulong size)
        {
            this.ByteLength = size;
        }

        public ulong ByteLength { get; }

        public override bool Equals(object? obj)
        {
            return obj is CompressedAV1Data other && Equals(other);
        }

        public bool Equals(CompressedAV1Data? other)
        {
            if (other is null)
            {
                return false;
            }

            if (this.ByteLength != other.ByteLength || this.IsDisposed || other.IsDisposed)
            {
                return false;
            }

            bool result;

            if (GetType() == other.GetType())
            {
                result = EqualsCore(other);
            }
            else
            {
                IntPtr firstPinnedBuffer = PinBuffer();

                try
                {
                    IntPtr secondPinnedBuffer = other.PinBuffer();

                    try
                    {
                        result = BufferUtil.BitwiseEquals(firstPinnedBuffer, secondPinnedBuffer, this.ByteLength);
                    }
                    finally
                    {
                        other.UnpinBuffer();
                    }
                }
                finally
                {
                    UnpinBuffer();
                }
            }

            return result;
        }

        public override int GetHashCode()
        {
            return this.ByteLength.GetHashCode();
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

        protected abstract bool EqualsCore(CompressedAV1Data other);

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

        public static bool operator ==(CompressedAV1Data left, CompressedAV1Data right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(CompressedAV1Data left, CompressedAV1Data right)
        {
            return !(left == right);
        }
    }
}
