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

        public NclxColorData nclxColorData;

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

                this.nclxColorData.colorPrimaries = nclxInfo.ColorPrimaries;
                this.nclxColorData.transferCharacteristics = nclxInfo.TransferCharacteristics;
                this.nclxColorData.matrixCoefficients = nclxInfo.MatrixCoefficients;
                this.nclxColorData.fullRange = nclxInfo.FullRange;
                this.format = ColorInformationFormat.Nclx;
            }
            else
            {
                ExceptionUtil.ThrowArgumentException($"Unknown { nameof(ColorInformationBox) } type: { colorInfoBox.ColorType }");
            }
        }
    }
}
