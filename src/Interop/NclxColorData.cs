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

using AvifFileType.AvifContainer;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal struct NclxColorData
    {
        public NclxColorPrimaries colorPrimaries;
        public NclxTransferCharacteristics transferCharacteristics;
        public NclxMatrixCoefficients matrixCoefficients;
        [MarshalAs(UnmanagedType.U1)]
        public bool fullRange;
    }
}
