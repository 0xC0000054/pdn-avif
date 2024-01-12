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

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("Mirror: {MirrorDirection, nq}")]
    internal sealed class ImageMirrorBox
        : ItemProperty
    {
        private const byte ReservedBitsMask = 0xfe;

        public ImageMirrorBox(in EndianBinaryReaderSegment reader, Box header)
            : base(header)
        {
            byte value = reader.ReadByte();

            if ((value & ReservedBitsMask) != 0)
            {
                ExceptionUtil.ThrowFormatException($"Unknown ImageMirror value: { value }");
            }

            this.MirrorDirection = (ImageMirrorDirection)value;
        }

        public ImageMirrorBox(ImageMirrorDirection direction)
            : base(BoxTypes.ImageMirror)
        {
            if (direction < ImageMirrorDirection.Horizontal || direction > ImageMirrorDirection.Vertical)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(direction));
            }

            this.MirrorDirection = direction;
        }

        internal ImageMirrorDirection MirrorDirection { get; }
    }
}
