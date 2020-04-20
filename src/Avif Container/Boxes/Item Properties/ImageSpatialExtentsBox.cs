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

using System;

namespace AvifFileType.AvifContainer
{
    internal sealed class ImageSpatialExtentsBox
        : ItemPropertyFull
    {
        public ImageSpatialExtentsBox(EndianBinaryReader reader, Box header)
            : base(reader, header)
        {
            if (this.Version != 0)
            {
                throw new FormatException($"{ nameof(ImageSpatialExtentsBox) } version must be 0, actual value: { this.Version }.");
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

        public uint ImageWidth { get; }

        public uint ImageHeight { get; }

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
