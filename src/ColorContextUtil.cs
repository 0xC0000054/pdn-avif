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
using PaintDotNet.Direct2D1;
using PaintDotNet.Imaging;

namespace AvifFileType
{
    internal static class ColorContextUtil
    {
        private enum IccProfileType
        {
            Unspecified = 0,
            Srgb,
            ScRgb,
        }

        public static IColorContext? CreateFromCicpColorInfo(CICPColorData? colorData, IImagingFactory imagingFactory)
        {
            IColorContext? colorContext = null;

            if (colorData.HasValue)
            {
                IccProfileType type = GetIccProfileType(colorData.Value);

                switch (type)
                {
                    case IccProfileType.Srgb:
                        colorContext = imagingFactory.CreateColorContext(KnownColorSpace.Srgb);
                        break;
                    case IccProfileType.ScRgb:
                        colorContext = imagingFactory.CreateColorContext(KnownColorSpace.ScRgb);
                        break;
                }
            }

            return colorContext;
        }

        private static IccProfileType GetIccProfileType(CICPColorData cicp)
        {
            IccProfileType type = IccProfileType.Unspecified;

            if (cicp.colorPrimaries == CICPColorPrimaries.BT709)
            {
                switch (cicp.transferCharacteristics)
                {
                    case CICPTransferCharacteristics.Linear:
                        type = IccProfileType.ScRgb;
                        break;
                    case CICPTransferCharacteristics.Srgb:
                        type = IccProfileType.Srgb;
                        break;
                }
            }
            
            return type;
        }
    }
}
