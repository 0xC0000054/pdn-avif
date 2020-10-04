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
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate IntPtr CompressedAV1OutputAlloc(UIntPtr sizeInBytes);

    internal sealed class CompressedAV1DataAllocator
        : IDisposable
    {
        private List<CompressedDataState> compressedData;

        public CompressedAV1DataAllocator(int capacity)
        {
            this.compressedData = new List<CompressedDataState>(capacity);
        }

        public ExceptionDispatchInfo ExceptionInfo { get; private set; }

        public IntPtr Allocate(UIntPtr sizeInBytes)
        {
            IntPtr nativePointer;
            ulong size = sizeInBytes.ToUInt64();

            try
            {
                CompressedDataState state = new CompressedDataState(size);
                this.compressedData.Add(state);

                nativePointer = state.NativePointer;
            }
            catch (Exception ex)
            {
                this.ExceptionInfo = ExceptionDispatchInfo.Capture(ex);

                return IntPtr.Zero;
            }

            return nativePointer;
        }

        public void Dispose()
        {
            if (this.compressedData != null)
            {
                for (int i = 0; i < this.compressedData.Count; i++)
                {
                    CompressedDataState state = this.compressedData[i];

                    if (state.OwnsDataBuffer)
                    {
                        state.Dispose();
                    }
                }

                this.compressedData = null;
            }
        }

        public CompressedAV1Data GetCompressedAV1Data(IntPtr nativePointer)
        {
            VerifyNotDisposed();

            CompressedAV1Data data = null;

            for (int i = 0; i < this.compressedData.Count; i++)
            {
                CompressedDataState state = this.compressedData[i];

                if (state.NativePointer == nativePointer)
                {
                    data = state.GetData();
                }
            }

            if (data is null)
            {
                ExceptionUtil.ThrowInvalidOperationException("The native data was not allocated by this class.");
            }

            return data;
        }

        private void VerifyNotDisposed()
        {
            if (this.compressedData is null)
            {
                ExceptionUtil.ThrowObjectDisposedException(nameof(CompressedAV1DataAllocator));
            }
        }

        private sealed class CompressedDataState
        {
            // 81920 is the largest multiple of 4096 that is under the large object heap limit (around 85,000 bytes).
            // It is used as the managed buffer size cutoff to avoid having to allocate both an unmanaged buffer and
            // a temporary managed buffer for as many images as possible.
            //
            // The BigEndianBinaryWriter will use a temporary managed buffer that is this size when reading data
            // from the unmanaged buffer.
            private const ulong ManagedCompressedAV1DataMaxSize = 81920;

            private readonly CompressedAV1Data data;
            private bool isPinned;

            public CompressedDataState(ulong size)
            {
                if (size <= ManagedCompressedAV1DataMaxSize)
                {
                    this.data = new ManagedCompressedAV1Data(size);
                }
                else
                {
                    this.data = new UnmanagedCompressedAV1Data(size);
                }
                this.OwnsDataBuffer = true;
                this.NativePointer = ((IPinnableBuffer)this.data).Pin();
                this.isPinned = true;
            }

            public IntPtr NativePointer { get; }

            public bool OwnsDataBuffer { get; private set; }

            public void Dispose()
            {
                if (this.OwnsDataBuffer)
                {
                    UnpinBuffer();
                    this.data.Dispose();
                }
            }

            public CompressedAV1Data GetData()
            {
                UnpinBuffer();
                this.OwnsDataBuffer = false;

                return this.data;
            }

            private void UnpinBuffer()
            {
                if (this.isPinned)
                {
                    ((IPinnableBuffer)this.data).Unpin();

                    this.isPinned = false;
                }
            }
        }
    }
}
