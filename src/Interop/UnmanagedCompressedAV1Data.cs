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

namespace AvifFileType.Interop
{
    internal sealed class UnmanagedCompressedAV1Data
        : CompressedAV1Data, IEquatable<UnmanagedCompressedAV1Data>
    {
        private SafeNativeMemoryBuffer buffer;

        public UnmanagedCompressedAV1Data(ulong size)
            : base(size)
        {
            this.buffer = SafeNativeMemoryBuffer.Create(size);
        }

        public override bool Equals(object obj)
        {
            return obj is UnmanagedCompressedAV1Data other && Equals(other);
        }

        public bool Equals(UnmanagedCompressedAV1Data other)
        {
            if (other is null)
            {
                return false;
            }

            if (this.ByteLength != other.ByteLength || this.IsDisposed || other.IsDisposed)
            {
                return false;
            }

            IntPtr firstBuffer = this.buffer.DangerousGetHandle();
            IntPtr secondBuffer = other.buffer.DangerousGetHandle();

            return AvifNative.MemoryBlocksAreEqual(firstBuffer, secondBuffer, this.ByteLength);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.buffer != null)
                {
                    this.buffer.Dispose();
                    this.buffer = null;
                }
            }
        }

        protected override bool EqualsCore(CompressedAV1Data other)
        {
            IntPtr firstBuffer = this.buffer.DangerousGetHandle();
            IntPtr secondBuffer = ((UnmanagedCompressedAV1Data)other).buffer.DangerousGetHandle();

            return AvifNative.MemoryBlocksAreEqual(firstBuffer, secondBuffer, this.ByteLength);
        }

        protected override IntPtr PinBuffer()
        {
            return this.buffer.DangerousGetHandle();
        }

        protected override void UnpinBuffer()
        {
        }

        protected override void WriteBuffer(BigEndianBinaryWriter writer)
        {
            writer.Write(this.buffer);
        }

        public static bool operator ==(UnmanagedCompressedAV1Data left, UnmanagedCompressedAV1Data right)
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

        public static bool operator !=(UnmanagedCompressedAV1Data left, UnmanagedCompressedAV1Data right)
        {
            return !(left == right);
        }
    }
}
