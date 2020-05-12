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
    internal enum ColorInformationFormat
    {
        IccProfile = 0,
        Nclx
    }

    [StructLayout(LayoutKind.Sequential)]
    internal sealed class ColorConversionInfo
    {
        public byte[] iccProfile;

        public CICPColorData cicpColorData;

        public ColorInformationFormat format;

        public ColorConversionInfo(byte[] iccProfile)
        {
            if (iccProfile is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(iccProfile));
            }

            this.iccProfile = iccProfile;
            this.format = ColorInformationFormat.IccProfile;
        }

        public ColorConversionInfo(CICPColorPrimaries colorPrimaries,
                                   CICPTransferCharacteristics transferCharacteristics,
                                   CICPMatrixCoefficients matrixCoefficients,
                                   bool fullRange)
        {
            this.cicpColorData.colorPrimaries = colorPrimaries;
            this.cicpColorData.transferCharacteristics = transferCharacteristics;
            this.cicpColorData.matrixCoefficients = matrixCoefficients;
            this.cicpColorData.fullRange = fullRange;
            this.format = ColorInformationFormat.Nclx;
        }

        public ColorConversionInfo(ColorInformationBox colorInfoBox)
        {
            if (colorInfoBox is null)
            {
                ExceptionUtil.ThrowArgumentException(nameof(colorInfoBox));
            }

            if (colorInfoBox.ColorType == ColorInformationBoxTypes.IccProfile ||
                colorInfoBox.ColorType == ColorInformationBoxTypes.RestrictedIccProfile)
            {
                IccProfileColorInformation iccProfileInfo = (IccProfileColorInformation)colorInfoBox;

                this.iccProfile = iccProfileInfo.GetProfileBytes();
                this.format = ColorInformationFormat.IccProfile;
            }
            else if (colorInfoBox.ColorType == ColorInformationBoxTypes.Nclx)
            {
                NclxColorInformation nclxInfo = (NclxColorInformation)colorInfoBox;

                this.cicpColorData.colorPrimaries = nclxInfo.ColorPrimaries;
                this.cicpColorData.transferCharacteristics = nclxInfo.TransferCharacteristics;
                this.cicpColorData.matrixCoefficients = nclxInfo.MatrixCoefficients;
                this.cicpColorData.fullRange = nclxInfo.FullRange;
                this.format = ColorInformationFormat.Nclx;
            }
            else
            {
                ExceptionUtil.ThrowArgumentException($"Unknown { nameof(ColorInformationBox) } type: { colorInfoBox.ColorType }");
            }
        }
    }
}
