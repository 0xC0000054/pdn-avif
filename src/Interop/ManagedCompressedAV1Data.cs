////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PaintDotNet.AppModel;
using System;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal sealed class ManagedCompressedAV1Data
        : CompressedAV1Data, IEquatable<ManagedCompressedAV1Data>
    {
        private IArrayPoolBuffer<byte> buffer;
        private GCHandle gcHandle;

        public ManagedCompressedAV1Data(ulong size, IArrayPoolService arrayPool)
            : base(size)
        {
            this.buffer = arrayPool.Rent<byte>(checked((int)size));
        }

        ~ManagedCompressedAV1Data()
        {
            Dispose(false);
        }

        public override bool Equals(object obj)
        {
            return obj is ManagedCompressedAV1Data other && Equals(other);
        }

        public bool Equals(ManagedCompressedAV1Data other)
        {
            if (other is null)
            {
                return false;
            }

            if (this.ByteLength != other.ByteLength || this.IsDisposed || other.IsDisposed)
            {
                return false;
            }

            IArrayPoolBuffer<byte> firstBuffer = this.buffer;
            IArrayPoolBuffer<byte> secondBuffer = other.buffer;

            return firstBuffer.AsSpan().SequenceEqual(secondBuffer.AsSpan());
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposableUtil.Free(ref this.buffer);
            }

            if (this.gcHandle.IsAllocated)
            {
                this.gcHandle.Free();
            }
        }

        protected override bool EqualsCore(CompressedAV1Data other)
        {
            IArrayPoolBuffer<byte> firstBuffer = this.buffer;
            IArrayPoolBuffer<byte> secondBuffer = ((ManagedCompressedAV1Data)other).buffer;

            return firstBuffer.AsSpan().SequenceEqual(secondBuffer.AsSpan());
        }

        protected override IntPtr PinBuffer()
        {
            if (!this.gcHandle.IsAllocated)
            {
                this.gcHandle = GCHandle.Alloc(this.buffer.Array, GCHandleType.Pinned);
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
            writer.Write(this.buffer.Array, 0, this.buffer.RequestedLength);
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
