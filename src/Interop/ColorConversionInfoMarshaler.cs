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
using System;
using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    internal sealed class ColorConversionInfoMarshaler
        : ICustomMarshaler
    {
        private struct NativeNclxColorData
        {
            public NclxColorPrimaries colorPrimaries;
            public NclxTransferCharacteristics transferCharacteristics;
            public NclxMatrixCoefficients matrixCoefficients;
            public byte fullRange;
        }

        private struct NativeColorConversionInfo
        {
            public IntPtr iccProfile;
            public UIntPtr iccProfileSize;

            public NativeNclxColorData nclxColorData;

            public ColorInformationFormat format;
        }

        private static readonly int NativeColorConversionInfoSize = Marshal.SizeOf(typeof(NativeColorConversionInfo));
        private static readonly ColorConversionInfoMarshaler instance = new ColorConversionInfoMarshaler();

        private ColorConversionInfoMarshaler()
        {
        }

#pragma warning disable IDE0060 // Remove unused parameter
        public static ICustomMarshaler GetInstance(string cookie)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return instance;
        }

        public void CleanUpManagedData(object ManagedObj)
        {
        }

        public void CleanUpNativeData(IntPtr pNativeData)
        {
            if (pNativeData == IntPtr.Zero)
            {
                return;
            }

            unsafe
            {
                NativeColorConversionInfo* nativeColorInfo = (NativeColorConversionInfo*)pNativeData;

                if (nativeColorInfo->format == ColorInformationFormat.IccProfile)
                {
                    if (nativeColorInfo->iccProfile != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(nativeColorInfo->iccProfile);
                    }
                }

                Marshal.FreeHGlobal(pNativeData);
            }
        }

        public int GetNativeDataSize()
        {
            return NativeColorConversionInfoSize;
        }

        public IntPtr MarshalManagedToNative(object ManagedObj)
        {
            if (ManagedObj is null)
            {
                return IntPtr.Zero;
            }

            ColorConversionInfo info = (ColorConversionInfo)ManagedObj;

            if (info.format != ColorInformationFormat.IccProfile && info.format != ColorInformationFormat.Nclx)
            {
                throw new MarshalDirectiveException($"{ info.format } is not a supported color format.");
            }

            IntPtr nativeStructure = Marshal.AllocHGlobal(NativeColorConversionInfoSize);

            unsafe
            {
                NativeColorConversionInfo* nativeColorInfo = (NativeColorConversionInfo*)nativeStructure;

                nativeColorInfo->format = info.format;

                if (info.format == ColorInformationFormat.IccProfile)
                {
                    if (info.iccProfile != null && info.iccProfile.Length > 0)
                    {
                        nativeColorInfo->iccProfile = Marshal.AllocHGlobal(info.iccProfile.Length);
                        Marshal.Copy(info.iccProfile, 0, nativeColorInfo->iccProfile, info.iccProfile.Length);
                        nativeColorInfo->iccProfileSize = new UIntPtr((uint)info.iccProfile.Length);
                    }
                    else
                    {
                        nativeColorInfo->iccProfile = IntPtr.Zero;
                        nativeColorInfo->iccProfileSize = UIntPtr.Zero;
                    }
                    nativeColorInfo->nclxColorData.colorPrimaries = 0;
                    nativeColorInfo->nclxColorData.transferCharacteristics = 0;
                    nativeColorInfo->nclxColorData.matrixCoefficients = 0;
                    nativeColorInfo->nclxColorData.fullRange = 0;
                }
                else if (info.format == ColorInformationFormat.Nclx)
                {
                    nativeColorInfo->iccProfile = IntPtr.Zero;
                    nativeColorInfo->iccProfileSize = UIntPtr.Zero;
                    nativeColorInfo->nclxColorData.colorPrimaries = info.nclxColorData.colorPrimaries;
                    nativeColorInfo->nclxColorData.transferCharacteristics = info.nclxColorData.transferCharacteristics;
                    nativeColorInfo->nclxColorData.matrixCoefficients = info.nclxColorData.matrixCoefficients;
                    if (info.nclxColorData.fullRange)
                    {
                        nativeColorInfo->nclxColorData.fullRange = 1;
                    }
                    else
                    {
                        nativeColorInfo->nclxColorData.fullRange = 0;
                    }
                }
            }

            return nativeStructure;
        }

        public object MarshalNativeToManaged(IntPtr pNativeData)
        {
            return null;
        }
    }
}
