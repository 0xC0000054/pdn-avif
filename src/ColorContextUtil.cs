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
        public static IColorContext? CreateFromCICPColorInfo(CICPColorData colorData, IImagingFactory imagingFactory)
        {
            IColorContext? colorContext = null;

            if (colorData.colorPrimaries == CICPColorPrimaries.BT709)
            {
                switch (colorData.transferCharacteristics)
                {
                    case CICPTransferCharacteristics.Linear:
                        colorContext = imagingFactory.CreateColorContext(KnownColorSpace.ScRgb);
                        break;
                    case CICPTransferCharacteristics.Srgb:
                        colorContext = imagingFactory.CreateColorContext(KnownColorSpace.Srgb);
                        break;
                }
            }
            else if (colorData.colorPrimaries == CICPColorPrimaries.Smpte432)
            {
                // DisplayP3 uses SMPTE EG 432-1 primaries with the sRGB transfer curve.
                if (colorData.transferCharacteristics == CICPTransferCharacteristics.Srgb)
                {
                    colorContext = imagingFactory.CreateColorContext(KnownColorSpace.DisplayP3);
                }
            }

            return colorContext;
        }
    }
}
