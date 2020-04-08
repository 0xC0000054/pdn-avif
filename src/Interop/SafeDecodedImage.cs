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
    internal abstract class SafeDecodedImage
        : SafeBuffer
    {
        protected SafeDecodedImage(bool ownsHandle) : base(ownsHandle)
        {
        }
    }

    internal sealed class SafeDecodedImageX86
        : SafeDecodedImage
    {
        private SafeDecodedImageX86()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return AvifNative_86.FreeImageData(this.handle);
        }
    }

    internal sealed class SafeDecodedImageX64
        : SafeDecodedImage
    {
        private SafeDecodedImageX64()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return AvifNative_64.FreeImageData(this.handle);
        }
    }
}
