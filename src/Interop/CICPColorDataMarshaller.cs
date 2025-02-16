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
using System.Runtime.InteropServices.Marshalling;

namespace AvifFileType.Interop
{
    [CustomMarshaller(typeof(CICPColorData), MarshalMode.Default, typeof(CICPColorDataMarshaller))]
    internal static class CICPColorDataMarshaller
    {
        public struct NativeCICPColorData
        {
            public CICPColorPrimaries colorPrimaries;
            public CICPTransferCharacteristics transferCharacteristics;
            public CICPMatrixCoefficients matrixCoefficients;
            public byte fullRange;

            public NativeCICPColorData(CICPColorData managed)
            {
                this.colorPrimaries = managed.colorPrimaries;
                this.transferCharacteristics = managed.transferCharacteristics;
                this.matrixCoefficients = managed.matrixCoefficients;
                this.fullRange = managed.fullRange.ToByte();
            }
        }

        public static NativeCICPColorData ConvertToUnmanaged(CICPColorData managed)
        {
            return new NativeCICPColorData(managed);
        }

        public static CICPColorData ConvertToManaged(NativeCICPColorData unmanaged)
        {
            return new CICPColorData(unmanaged.colorPrimaries,
                                     unmanaged.transferCharacteristics,
                                     unmanaged.matrixCoefficients,
                                     unmanaged.fullRange != 0);
        }
    }
}
