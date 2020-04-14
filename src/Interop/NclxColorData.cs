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
    internal struct NclxColorData
    {
        public ushort colorPrimaries;
        public ushort transferCharacteristics;
        public ushort matrixCoefficients;
        [MarshalAs(UnmanagedType.U1)]
        public bool fullRange;
    }
}
