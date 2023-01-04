////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    internal sealed class ItemPropertyFactory
    {
        private bool transformPropertySeen;

        internal ItemPropertyFactory()
        {
            this.transformPropertySeen = false;
        }

        internal IItemProperty TryCreate(in EndianBinaryReaderSegment reader, Box header)
        {
            IItemProperty property;

            if (header.Type == BoxTypes.ImageSpatialExtents)
            {
                property = new ImageSpatialExtentsBox(reader, header);
            }
            else if (header.Type == BoxTypes.PixelAspectRatio)
            {
                property = new PixelAspectRatioBox(reader, header);
            }
            else if (header.Type == BoxTypes.AV1Config)
            {
                property = new AV1ConfigBox(reader, header);
            }
            else if (header.Type == BoxTypes.AuxiliaryTypeProperty)
            {
                property = new AuxiliaryTypePropertyBox(reader, header);
            }
            else if (header.Type == BoxTypes.ColorInformation)
            {
                ColorInformationBox colorInformation = new ColorInformationBox(reader, header);

                if (colorInformation.ColorType == ColorInformationBoxTypes.IccProfile ||
                    colorInformation.ColorType == ColorInformationBoxTypes.RestrictedIccProfile)
                {
                    property = new IccProfileColorInformation(reader, colorInformation);
                }
                else if (colorInformation.ColorType == ColorInformationBoxTypes.Nclx)
                {
                    property = new NclxColorInformation(reader, colorInformation);
                }
                else
                {
                    // Ignore any unknown ColorInformationBox types.
                    Debug.WriteLine($"Unsupported ColorInformationBox type: { colorInformation.ColorType }.");
                    property = null;
                }
            }
            else if (header.Type == BoxTypes.PixelInformation)
            {
                property = new PixelInformationBox(reader, header);
            }
            else if (header.Type == BoxTypes.CleanAperture)
            {
                property = new CleanApertureBox(reader, header);
                this.transformPropertySeen = true;
            }
            else if (header.Type == BoxTypes.ImageMirror)
            {
                property = new ImageMirrorBox(reader, header);
                this.transformPropertySeen = true;
            }
            else if (header.Type == BoxTypes.ImageRotation)
            {
                property = new ImageRotateBox(reader, header);
                this.transformPropertySeen = true;
            }
            else if (header.Type == BoxTypes.AV1LayeredImageIndexing)
            {
                property = new AV1LayeredImageIndexingBox(reader, header);
            }
            else if (header.Type == BoxTypes.AV1OperatingPoint)
            {
                property = new AV1OperatingPointBox(reader, header);
            }
            else if (header.Type == BoxTypes.LayerSelector)
            {
                if (this.transformPropertySeen)
                {
                    ExceptionUtil.ThrowFormatException("The layer selector property must come before the transform properties.");
                }

                property = new LayerSelectorBox(reader, header);
            }
            else
            {
                Debug.WriteLine($"Unsupported property type: { header.Type }.");
                property = null;
            }

            return property;
        }
    }
}
