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
    internal sealed class ImageGridDescriptor
    {
        private const int CommonFieldLength = sizeof(byte) * 4;
        internal const int LargeDescriptorLength = CommonFieldLength + sizeof(uint) + sizeof(uint);
        internal const int SmallDescriptorLength = CommonFieldLength + sizeof(ushort) + sizeof(ushort);

        private const byte RequiredVersion = 0;

        public ImageGridDescriptor(EndianBinaryReader reader, ulong length)
        {
            byte version = reader.ReadByte();
            if (version != RequiredVersion)
            {
                ExceptionUtil.ThrowFormatException($"Unknown { nameof(ImageGridDescriptor) } version: { version }.");
            }

            byte flags = reader.ReadByte();
            bool largeOutputFields = (flags & 1) == 1;

            this.RowsMinusOne = reader.ReadByte();
            this.ColumnsMinusOne = reader.ReadByte();
            if (largeOutputFields)
            {
                if (length < LargeDescriptorLength)
                {
                    ExceptionUtil.ThrowFormatException("Invalid image grid descriptor length.");
                }

                this.OutputWidth = reader.ReadUInt32();
                this.OutputHeight = reader.ReadUInt32();
            }
            else
            {
                this.OutputWidth = reader.ReadUInt16();
                this.OutputHeight = reader.ReadUInt16();
            }
        }

        public ImageGridDescriptor(ImageGridMetadata imageGridMetadata)
        {
            this.RowsMinusOne = (byte)(imageGridMetadata.TileRowCount - 1);
            this.ColumnsMinusOne = (byte)(imageGridMetadata.TileColumnCount - 1);
            this.OutputWidth = imageGridMetadata.OutputWidth;
            this.OutputHeight = imageGridMetadata.OutputHeight;
        }

        public byte RowsMinusOne { get; }

        public byte ColumnsMinusOne { get; }

        public uint OutputWidth { get; }

        public uint OutputHeight { get; }

        public void Write(BigEndianBinaryWriter writer)
        {
            bool largeOutputFields = this.OutputWidth > ushort.MaxValue || this.OutputHeight > ushort.MaxValue;

            writer.Write(RequiredVersion);
            writer.Write((byte)(largeOutputFields ? 1 : 0));
            writer.Write(this.RowsMinusOne);
            writer.Write(this.ColumnsMinusOne);

            if (largeOutputFields)
            {
                writer.Write(this.OutputWidth);
                writer.Write(this.OutputHeight);
            }
            else
            {
                writer.Write((ushort)this.OutputWidth);
                writer.Write((ushort)this.OutputHeight);
            }
        }
    }
}
