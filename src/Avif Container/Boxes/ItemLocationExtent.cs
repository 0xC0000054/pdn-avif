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

using System;
using System.Globalization;

namespace AvifFileType.AvifContainer
{
    internal sealed class ItemLocationExtent
    {
        private long offsetWritePosition;
        private byte offsetSize;

        public ItemLocationExtent(in EndianBinaryReaderSegment reader, ItemLocationBox parent, ushort extentCount)
        {
            if (extentCount > 1 && (parent.Version == 1 || parent.Version == 2))
            {
                switch (parent.IndexSize)
                {
                    case 0:
                        this.Index = 0;
                        break;
                    case 4:
                        this.Index = reader.ReadUInt32();
                        break;
                    case 8:
                        this.Index = reader.ReadUInt64();
                        break;
                    default:
                        throw new InvalidOperationException($"IndexSize must be 0, 4 or 8, actual value: { parent.IndexSize.ToString(CultureInfo.InvariantCulture) }");
                }
            }
            else
            {
                this.Index = 0;
            }

            switch (parent.OffsetSize)
            {
                case 0:
                    break;
                case 4:
                    this.Offset = reader.ReadUInt32();
                    break;
                case 8:
                    this.Offset = reader.ReadUInt64();
                    break;
                default:
                    throw new InvalidOperationException($"OffsetSize must be 0, 4 or 8, actual value: { parent.OffsetSize.ToString(CultureInfo.InvariantCulture) }");
            }

            switch (parent.LengthSize)
            {
                case 0:
                    break;
                case 4:
                    this.Length = reader.ReadUInt32();
                    break;
                case 8:
                    this.Length = reader.ReadUInt64();
                    break;
                default:
                    throw new InvalidOperationException($"LengthSize must be 0, 4 or 8, actual value: { parent.LengthSize.ToString(CultureInfo.InvariantCulture) }");
            }
        }

        public ItemLocationExtent(ulong length)
        {
            this.Index = 0;
            // Zero is written as a placeholder, the real offset value will be updated later.
            this.Offset = 0;
            this.Length = length;
            this.offsetWritePosition = -1;
        }

        public ItemLocationExtent(ulong offset, ulong length)
        {
            this.Index = 0;
            this.Offset = offset;
            this.Length = length;
            this.offsetWritePosition = -1;
        }

        public ulong Index { get; }

        public ulong Offset { get; private set; }

        public ulong Length { get; }

        public static int GetSize(ItemLocationBox parent)
        {
            return parent.IndexSize + parent.OffsetSize + parent.LengthSize;
        }

        public void WriteFinalOffset(BigEndianBinaryWriter writer, ulong finalOffset)
        {
            if (this.offsetWritePosition == -1)
            {
                ExceptionUtil.ThrowInvalidOperationException("The item locations must have been written before calling this method.");
            }

            if (this.offsetSize != 0)
            {
                long oldPosition = writer.Position;
                writer.Position = this.offsetWritePosition;

                switch (this.offsetSize)
                {
                    case 4:
                        writer.Write((uint)finalOffset);
                        break;
                    case 8:
                        writer.Write(finalOffset);
                        break;
                    default:
                        throw new InvalidOperationException($"{ nameof(this.offsetSize) } must be 4 or 8, actual value: { this.offsetSize.ToString(CultureInfo.InvariantCulture) }");
                }

                writer.Position = oldPosition;

                this.Offset = finalOffset;
            }
        }

        public void Write(BigEndianBinaryWriter writer, ItemLocationBox parent)
        {
            if (this.offsetWritePosition == -1)
            {
                this.offsetWritePosition = writer.Position;
                this.offsetSize = parent.OffsetSize;
            }

            if (parent.Version == 1 || parent.Version == 2)
            {
                switch (parent.IndexSize)
                {
                    case 0:
                        break;
                    case 4:
                        writer.Write((uint)this.Index);
                        break;
                    case 8:
                        writer.Write(this.Index);
                        break;
                    default:
                        throw new InvalidOperationException($"IndexSize must be 0, 4 or 8, actual value: { parent.IndexSize.ToString(CultureInfo.InvariantCulture) }");
                }
            }

            switch (parent.OffsetSize)
            {
                case 0:
                    break;
                case 4:
                    writer.Write((uint)this.Offset);
                    break;
                case 8:
                    writer.Write(this.Offset);
                    break;
                default:
                    throw new InvalidOperationException($"OffsetSize must be 0, 4 or 8, actual value: { parent.OffsetSize.ToString(CultureInfo.InvariantCulture) }");
            }

            switch (parent.LengthSize)
            {
                case 0:
                    break;
                case 4:
                    writer.Write((uint)this.Length);
                    break;
                case 8:
                    writer.Write(this.Length);
                    break;
                default:
                    throw new InvalidOperationException($"LengthSize must be 0, 4 or 8, actual value: { parent.LengthSize.ToString(CultureInfo.InvariantCulture) }");
            }
        }
    }
}
