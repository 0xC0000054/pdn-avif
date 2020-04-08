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

using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal abstract class SafeAV1Image : SafeBuffer
    {
        protected SafeAV1Image(bool ownsHandle) : base(ownsHandle)
        {
        }
    }

    internal sealed class SafeAV1ImageX86 : SafeAV1Image
    {
        private SafeAV1ImageX86() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return AvifNative_86.FreeImageData(this.handle);
        }
    }

    internal sealed class SafeAV1ImageX64 : SafeAV1Image
    {
        private SafeAV1ImageX64() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return AvifNative_64.FreeImageData(this.handle);
        }
    }
}
