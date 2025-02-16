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

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal sealed class SafeDecoderImageHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeDecoderImageHandle() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                AvifNative_64.FreeDecoderImageHandle(this.handle);
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                AvifNative_ARM64.FreeDecoderImageHandle(this.handle);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            return true;
        }
    }
}
