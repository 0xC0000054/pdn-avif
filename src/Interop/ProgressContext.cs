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

using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal delegate bool AvifProgressCallback(uint done, uint total);

    [StructLayout(LayoutKind.Sequential)]
    internal ref struct ProgressContext
    {
        public nint progressCallback;
        public uint progressDone;
        public uint progressTotal;

        public ProgressContext(AvifProgressCallback progressCallback, uint progressDone, uint progressTotal)
        {
            this.progressCallback = Marshal.GetFunctionPointerForDelegate(progressCallback);
            this.progressDone = progressDone;
            this.progressTotal = progressTotal;
        }
    }
}
