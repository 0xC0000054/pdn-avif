////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020-2025 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal sealed class ManagedCompressedAV1Data
        : CompressedAV1Data, IEquatable<ManagedCompressedAV1Data>
    {
        private readonly byte[] buffer;
        private GCHandle gcHandle;

        public ManagedCompressedAV1Data(ulong size)
            : base(size)
        {
            this.buffer = ArrayPool<byte>.Shared.Rent(checked((int)size));
        }

        ~ManagedCompressedAV1Data()
        {
            Dispose(false);
        }

        public override bool Equals(object? obj)
        {
            return obj is ManagedCompressedAV1Data other && Equals(other);
        }

        public bool Equals(ManagedCompressedAV1Data? other)
        {
            if (other is null)
            {
                return false;
            }

            if (this.ByteLength != other.ByteLength || this.IsDisposed || other.IsDisposed)
            {
                return false;
            }

            ReadOnlySpan<byte> firstBuffer = this.buffer.AsSpan(0, (int)this.ByteLength);
            ReadOnlySpan<byte> secondBuffer = other.buffer.AsSpan(0, (int)this.ByteLength);

            return firstBuffer.SequenceEqual(secondBuffer);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ArrayPool<byte>.Shared.Return(this.buffer);
            }

            if (this.gcHandle.IsAllocated)
            {
                this.gcHandle.Free();
            }
        }

        protected override bool EqualsCore(CompressedAV1Data other)
        {
            ReadOnlySpan<byte> firstBuffer = this.buffer.AsSpan(0, (int)this.ByteLength);
            ReadOnlySpan<byte> secondBuffer = ((ManagedCompressedAV1Data)other).buffer.AsSpan(0, (int)this.ByteLength);

            return firstBuffer.SequenceEqual(secondBuffer);
        }

        protected override IntPtr PinBuffer()
        {
            if (!this.gcHandle.IsAllocated)
            {
                this.gcHandle = GCHandle.Alloc(this.buffer, GCHandleType.Pinned);
            }

            return this.gcHandle.AddrOfPinnedObject();
        }

        protected override void UnpinBuffer()
        {
            if (this.gcHandle.IsAllocated)
            {
                this.gcHandle.Free();
            }
        }

        protected override void WriteBuffer(BigEndianBinaryWriter writer)
        {
            writer.Write(this.buffer, 0, (int)this.ByteLength);
        }

        public static bool operator ==(ManagedCompressedAV1Data left, ManagedCompressedAV1Data right)
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

        public static bool operator !=(ManagedCompressedAV1Data left, ManagedCompressedAV1Data right)
        {
            return !(left == right);
        }
    }
}
