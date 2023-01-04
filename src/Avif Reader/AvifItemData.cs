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

using System.IO;

namespace AvifFileType
{
    internal unsafe delegate void UseBufferPointerDelegate(byte* ptr, ulong length);

    internal abstract class AvifItemData
        : Disposable
    {
        protected AvifItemData()
        {
        }

        public ulong Length { get; init; }

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

        protected abstract Stream GetStreamImpl();

        protected abstract unsafe void UseBufferPointerImpl(UseBufferPointerDelegate action);
    }
}
