////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023, 2024 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using AvifFileType.AvifContainer;
using AvifFileType.Interop;

namespace AvifFileType
{
    internal struct CICPColorData
    {
        public CICPColorPrimaries colorPrimaries;
        public CICPTransferCharacteristics transferCharacteristics;
        public CICPMatrixCoefficients matrixCoefficients;
        public bool fullRange;

        public CICPColorData(NativeCICPColorData native)
        {
            this.colorPrimaries = native.colorPrimaries;
            this.transferCharacteristics = native.transferCharacteristics;
            this.matrixCoefficients = native.matrixCoefficients;
            this.fullRange = native.fullRange != 0;
        }

        public NativeCICPColorData ToNative()
        {
            return new NativeCICPColorData(this);
        }
    }
}
