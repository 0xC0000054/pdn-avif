////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal sealed class ImageSpatialExtentsBox
        : ItemPropertyFull
    {
        public ImageSpatialExtentsBox(in EndianBinaryReaderSegment reader, Box header)
            : base(reader, header)
        {
            if (this.Version != 0)
            {
                ExceptionUtil.ThrowFormatException($"{ nameof(ImageSpatialExtentsBox) } version must be 0, actual value: { this.Version }.");
            }

            this.ImageWidth = reader.ReadUInt32();
            this.ImageHeight = reader.ReadUInt32();
        }

        public ImageSpatialExtentsBox(uint imageWidth, uint imageHeight)
            : base(0, 0, BoxTypes.ImageSpatialExtents)
        {
            this.ImageWidth = imageWidth;
            this.ImageHeight = imageHeight;
        }

        // Used via reflection to get the item property box type.
        private ImageSpatialExtentsBox()
            : base(0, 0, BoxTypes.ImageSpatialExtents)
        {
        }

        public uint ImageWidth { get; }

        public uint ImageHeight { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"Width: {this.ImageWidth}, Height: {this.ImageHeight}";
            }
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.ImageWidth);
            writer.Write(this.ImageHeight);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + sizeof(uint) + sizeof(uint);
        }
    }
}
