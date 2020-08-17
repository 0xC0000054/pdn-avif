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
using System.IO;

namespace AvifFileType.AvifContainer
{
    internal class Box
    {
        public Box(EndianBinaryReader reader)
        {
            long startOffset = reader.Position;
            uint size32 = reader.ReadUInt32();
            this.Type = reader.ReadFourCC();

            int boxHeaderSize = sizeof(uint) + FourCC.SizeOf;

            switch (size32)
            {
                case 0:
                    // The box extends to the end of the file.
                    this.Size = reader.Length - startOffset;
                    break;
                case 1:
                    // The box size is 64-bit.
                    ulong size64 = reader.ReadUInt64();
                    if (size64 > long.MaxValue)
                    {
                        throw new IOException($"The box is larger than { long.MaxValue } bytes.");
                    }

                    this.Size = (long)size64;
                    boxHeaderSize += sizeof(ulong);
                    break;
                default:
                    this.Size = size32;
                    break;
            }

            this.BoxDataStartOffset = reader.Position;
            this.BoxDataSize = this.Size - boxHeaderSize;
            try
            {
                this.End = checked(startOffset + this.Size);
            }
            catch (OverflowException ex)
            {
                throw new IOException($"The box is larger than { long.MaxValue } bytes.", ex);
            }
        }

        public Box(in EndianBinaryReaderSegment reader)
        {
            long startOffset = reader.Position;
            uint size32 = reader.ReadUInt32();
            this.Type = reader.ReadFourCC();

            int boxHeaderSize = sizeof(uint) + FourCC.SizeOf;

            switch (size32)
            {
                case 0:
                    // The box extends to the end of the file.
                    this.Size = reader.EndOffset - startOffset;
                    break;
                case 1:
                    // The box size is 64-bit.
                    ulong size64 = reader.ReadUInt64();
                    if (size64 > long.MaxValue)
                    {
                        throw new IOException($"The box is larger than { long.MaxValue } bytes.");
                    }

                    this.Size = (long)size64;
                    boxHeaderSize += sizeof(ulong);
                    break;
                default:
                    this.Size = size32;
                    break;
            }

            this.BoxDataStartOffset = reader.Position;
            this.BoxDataSize = this.Size - boxHeaderSize;
            try
            {
                this.End = checked(startOffset + this.Size);
            }
            catch (OverflowException ex)
            {
                throw new IOException($"The box is larger than { long.MaxValue } bytes.", ex);
            }
        }

        protected Box(Box header)
        {
            if (header is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(header));
            }

            this.Size = header.Size;
            this.Type = header.Type;
            this.BoxDataStartOffset = header.BoxDataStartOffset;
            this.BoxDataSize = header.BoxDataSize;
            this.End = header.End;
        }

        protected Box(FourCC type)
        {
            this.Size = -1;
            this.Type = type;
            this.BoxDataStartOffset = -1;
            this.BoxDataSize = -1;
            this.End = -1;
        }

        public long End { get; }

        public long Size { get; }

        public FourCC Type { get; }

        public long BoxDataStartOffset { get; }

        public long BoxDataSize { get; }

        public ulong GetSize()
        {
            ulong size = GetTotalBoxSize();

            if (size > uint.MaxValue)
            {
                // A box that is larger than uint.MaxValue writes
                // the 64-bit size after the type field.
                size += sizeof(ulong);
            }

            return size;
        }

        public virtual void Write(BigEndianBinaryWriter writer)
        {
            ulong totalBoxSize = GetTotalBoxSize();
            bool largeBox = totalBoxSize > uint.MaxValue;

            // A box that is larger than uint.MaxValue writes a marker value of 1 in
            // the size field and then writes the 64-bit size after the type field.
            // See part 4.2 of the ISO base media file format specification (ISO/IEC 14496-12:2015).
            writer.Write(largeBox ? 1 : (uint)totalBoxSize);
            writer.Write(this.Type);
            if (largeBox)
            {
                writer.Write(totalBoxSize);
            }
        }

        protected virtual ulong GetTotalBoxSize()
        {
            return 8;
        }
    }
}
