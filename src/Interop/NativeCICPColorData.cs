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

using AvifFileType.AvifContainer;

namespace AvifFileType.Interop
{
    internal readonly ref struct NativeCICPColorData
    {
        public readonly CICPColorPrimaries colorPrimaries;
        public readonly CICPTransferCharacteristics transferCharacteristics;
        public readonly CICPMatrixCoefficients matrixCoefficients;
        public readonly byte fullRange;

        public NativeCICPColorData(CICPColorData managed)
        {
            this.colorPrimaries = managed.colorPrimaries;
            this.transferCharacteristics = managed.transferCharacteristics;
            this.matrixCoefficients = managed.matrixCoefficients;
            this.fullRange = managed.fullRange.ToByte();
        }
    }
}
