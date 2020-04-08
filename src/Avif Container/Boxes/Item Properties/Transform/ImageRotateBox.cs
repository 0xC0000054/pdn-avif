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

namespace AvifFileType.AvifContainer
{
    internal sealed class ImageRotateBox
        : ItemProperty
    {
        private const byte ReservedBitsMask = 0xfc;

        public ImageRotateBox(ImageRotation rotation)
            : base(BoxTypes.ImageRotation)
        {
            if (rotation < ImageRotation.RotateNone || rotation > ImageRotation.Rotate270CCW)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(rotation));
            }

            this.Rotation = rotation;
        }

        public ImageRotateBox(EndianBinaryReader reader, Box header)
            : base(header)
        {
            byte value = reader.ReadByte();

            if ((value & ReservedBitsMask) != 0)
            {
                ExceptionUtil.ThrowFormatException($"Unknown ImageRotate value: { value }");
            }

            this.Rotation = (ImageRotation)value;
        }

        public ImageRotation Rotation { get; }
    }
}
