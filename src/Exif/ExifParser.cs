﻿////////////////////////////////////////////////////////////////////////
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

using PaintDotNet.Imaging;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace AvifFileType.Exif
{
    internal static class ExifParser
    {
        /// <summary>
        /// Parses the EXIF data into a collection of properties.
        /// </summary>
        /// <param name="exif">The EXIF data.</param>
        /// <returns>
        /// A collection containing the EXIF properties.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="exif"/> is null.
        /// </exception>
        internal static ExifValueCollection? Parse(AvifItemData exif)
        {
            if (exif is null)
            {
                throw new ArgumentNullException(nameof(exif));
            }

            ExifValueCollection? metadataEntries = null;

            StreamSegment? stream = TryParseExifMetadataHeader(exif);

            if (stream != null)
            {
                try
                {
                    Endianess? byteOrder = TryDetectTiffByteOrder(stream);

                    if (byteOrder.HasValue)
                    {
                        using (EndianBinaryReader reader = new EndianBinaryReader(stream, byteOrder.Value))
                        {
                            stream = null;

                            ushort signature = reader.ReadUInt16();

                            if (signature == TiffConstants.Signature)
                            {
                                uint ifdOffset = reader.ReadUInt32();

                                List<ParserIFDEntry> entries = ParseDirectories(reader, ifdOffset);

                                metadataEntries = new ExifValueCollection(ConvertIFDEntriesToMetadataEntries(reader, entries));
                            }
                        }
                    }
                }
                catch (EndOfStreamException)
                {
                }
                finally
                {
                    stream?.Dispose();
                }
            }

            return metadataEntries;
        }

        private static Dictionary<ExifPropertyPath, ExifValue> ConvertIFDEntriesToMetadataEntries(EndianBinaryReader reader,
                                                                                                  List<ParserIFDEntry> entries)
        {
            Dictionary<ExifPropertyPath, ExifValue> metadataEntries = new(entries.Count);
            bool swapNumberByteOrder = reader.Endianess == Endianess.Big;

            for (int i = 0; i < entries.Count; i++)
            {
                ParserIFDEntry entry = entries[i];

                byte[]? propertyData;
                if (entry.OffsetFieldContainsValue)
                {
                    propertyData = entry.GetValueBytesFromOffset();
                    if (propertyData is null)
                    {
                        continue;
                    }
                }
                else
                {
                    long bytesToRead = entry.Count * ExifValueTypeUtil.GetSizeInBytes(entry.Type);

                    // Skip any tags that are empty or larger than 2 GB.
                    if (bytesToRead == 0 || bytesToRead > int.MaxValue)
                    {
                        continue;
                    }

                    uint offset = entry.Offset;

                    if ((offset + bytesToRead) > reader.Length)
                    {
                        continue;
                    }

                    reader.Position = offset;

                    propertyData = reader.ReadBytes((int)bytesToRead);

                    if (swapNumberByteOrder)
                    {
                        // Paint.NET converts all multi-byte numbers to little-endian.
                        switch (entry.Type)
                        {
                            case ExifValueType.Short:
                            case ExifValueType.SShort:
                                propertyData = SwapShortArrayToLittleEndian(propertyData, entry.Count);
                                break;
                            case ExifValueType.Long:
                            case ExifValueType.SLong:
                            case ExifValueType.Float:
                                propertyData = SwapLongArrayToLittleEndian(propertyData, entry.Count);
                                break;
                            case ExifValueType.Rational:
                            case ExifValueType.SRational:
                                propertyData = SwapRationalArrayToLittleEndian(propertyData, entry.Count);
                                break;
                            case ExifValueType.Double:
                                propertyData = SwapDoubleArrayToLittleEndian(propertyData, entry.Count);
                                break;
                            case ExifValueType.Byte:
                            case ExifValueType.Ascii:
                            case ExifValueType.Undefined:
                            default:
                                break;
                        }
                    }
                }

                metadataEntries.TryAdd(new ExifPropertyPath(entry.Section, entry.Tag), new ExifValue(entry.Type, propertyData));
            }

            return metadataEntries;
        }

        private static List<ParserIFDEntry> ParseDirectories(EndianBinaryReader reader, uint firstIFDOffset)
        {
            List<ParserIFDEntry> items = [];

            bool foundExif = false;
            bool foundGps = false;
            bool foundInterop = false;

            Queue<MetadataOffset> ifdOffsets = new Queue<MetadataOffset>();
            ifdOffsets.Enqueue(new MetadataOffset(ExifSection.Image, firstIFDOffset));

            while (ifdOffsets.Count > 0)
            {
                MetadataOffset metadataOffset = ifdOffsets.Dequeue();

                ExifSection section = metadataOffset.Section;
                uint offset = metadataOffset.Offset;

                if (offset >= reader.Length)
                {
                    continue;
                }

                reader.Position = offset;

                ushort count = reader.ReadUInt16();
                if (count == 0)
                {
                    continue;
                }

                items.Capacity += count;

                for (int i = 0; i < count; i++)
                {
                    ParserIFDEntry entry = new ParserIFDEntry(reader, section);

                    switch (entry.Tag)
                    {
                        case TiffConstants.Tags.ExifIFD:
                            if (!foundExif)
                            {
                                foundExif = true;
                                ifdOffsets.Enqueue(new MetadataOffset(ExifSection.Photo, entry.Offset));
                            }
                            break;
                        case TiffConstants.Tags.GpsIFD:
                            if (!foundGps)
                            {
                                foundGps = true;
                                ifdOffsets.Enqueue(new MetadataOffset(ExifSection.GpsInfo, entry.Offset));
                            }
                            break;
                        case TiffConstants.Tags.InteropIFD:
                            if (!foundInterop)
                            {
                                foundInterop = true;
                                ifdOffsets.Enqueue(new MetadataOffset(ExifSection.Interop, entry.Offset));
                            }
                            break;
                        case TiffConstants.Tags.StripOffsets:
                        case TiffConstants.Tags.RowsPerStrip:
                        case TiffConstants.Tags.StripByteCounts:
                        case TiffConstants.Tags.SubIFDs:
                        case TiffConstants.Tags.ThumbnailOffset:
                        case TiffConstants.Tags.ThumbnailLength:
                            // Skip the thumbnail and/or preview images.
                            // The StripOffsets and StripByteCounts tags are used to store a preview image in some formats.
                            // The SubIFDs tag is used to store thumbnails in TIFF and for storing other data in some camera formats.
                            //
                            // Note that some cameras will also store a thumbnail as part of their private data in the EXIF MakerNote tag.
                            // The EXIF MakerNote tag is treated as an opaque blob, so those thumbnails will be preserved.
                            break;
                        default:
                            items.Add(entry);
                            break;
                    }

                    System.Diagnostics.Debug.WriteLine(entry.ToString());
                }
            }

            return items;
        }

        private static unsafe byte[] SwapDoubleArrayToLittleEndian(byte[] values, uint count)
        {
            fixed (byte* pBytes = values)
            {
                ulong* ptr = (ulong*)pBytes;
                ulong* ptrEnd = ptr + count;

                while (ptr < ptrEnd)
                {
                    *ptr = BinaryPrimitives.ReverseEndianness(*ptr);
                    ptr++;
                }
            }

            return values;
        }

        private static unsafe byte[] SwapLongArrayToLittleEndian(byte[] values, uint count)
        {
            fixed (byte* pBytes = values)
            {
                uint* ptr = (uint*)pBytes;
                uint* ptrEnd = ptr + count;

                while (ptr < ptrEnd)
                {
                    *ptr = BinaryPrimitives.ReverseEndianness(*ptr);
                    ptr++;
                }
            }

            return values;
        }

        private static unsafe byte[] SwapRationalArrayToLittleEndian(byte[] values, uint count)
        {
            // A rational value consists of two 4-byte values, a numerator and a denominator.
            long itemCount = (long)count * 2;

            fixed (byte* pBytes = values)
            {
                uint* ptr = (uint*)pBytes;
                uint* ptrEnd = ptr + itemCount;

                while (ptr < ptrEnd)
                {
                    *ptr = BinaryPrimitives.ReverseEndianness(*ptr);
                    ptr++;
                }
            }

            return values;
        }

        private static unsafe byte[] SwapShortArrayToLittleEndian(byte[] values, uint count)
        {
            fixed (byte* pBytes = values)
            {
                ushort* ptr = (ushort*)pBytes;
                ushort* ptrEnd = ptr + count;

                while (ptr < ptrEnd)
                {
                    *ptr = BinaryPrimitives.ReverseEndianness(*ptr);
                    ptr++;
                }
            }

            return values;
        }

        [SkipLocalsInit]
        private static Endianess? TryDetectTiffByteOrder(Stream stream)
        {
            Span<byte> byteOrderMarker = stackalloc byte[2];

            stream.ReadExactly(byteOrderMarker);

            if (byteOrderMarker.SequenceEqual(TiffConstants.BigEndianByteOrderMarker))
            {
                return Endianess.Big;
            }
            else if (byteOrderMarker.SequenceEqual(TiffConstants.LittleEndianByteOrderMarker))
            {
                return Endianess.Little;
            }
            else
            {
                return null;
            }
        }

        private static StreamSegment? TryParseExifMetadataHeader(AvifItemData data)
        {
            // The EXIF data block has a header consisting of a big-endian 4-byte unsigned integer
            // that indicates the number of bytes that come before the start of the TIFF header.
            // See ISO/IEC 23008-12:2017 section A.2.1.

            StreamSegment? stream = null;
            Stream? avifItemStream = null;

            try
            {
                avifItemStream = data.GetStream();

                long tiffStartOffset = avifItemStream.TryReadUInt32BigEndian();

                if (tiffStartOffset != -1)
                {
                    long origin = avifItemStream.Position + tiffStartOffset;
                    ulong length = data.Length - (ulong)tiffStartOffset - sizeof(uint);

                    if (length > 0 && length <= long.MaxValue)
                    {
                        stream = new StreamSegment(avifItemStream, origin, (long)length);
                        // The StreamSegment will take ownership of the existing stream.
                        avifItemStream = null;
                    }
                }
            }
            finally
            {
                avifItemStream?.Dispose();
            }

            return stream;
        }

        private readonly struct ParserIFDEntry
        {
#pragma warning disable IDE0032 // Use auto property
            private readonly IFDEntry entry;
            private readonly ExifSection section;
            private readonly bool offsetIsBigEndian;
#pragma warning restore IDE0032 // Use auto property

            public ParserIFDEntry(EndianBinaryReader reader, ExifSection section)
            {
                this.entry = new IFDEntry(reader);
                this.section = section;
                this.offsetIsBigEndian = reader.Endianess == Endianess.Big;
            }

            public ushort Tag => this.entry.Tag;

            public ExifValueType Type => this.entry.Type;

            public uint Count => this.entry.Count;

            public uint Offset => this.entry.Offset;

            public bool OffsetFieldContainsValue
            {
                get
                {
                    return ExifValueTypeUtil.ValueFitsInOffsetField(this.Type, this.Count);
                }
            }

#pragma warning disable IDE0032 // Use auto property
            public ExifSection Section => this.section;
#pragma warning restore IDE0032 // Use auto property

            public unsafe byte[]? GetValueBytesFromOffset()
            {
                if (!this.OffsetFieldContainsValue)
                {
                    return null;
                }

                ExifValueType type = this.entry.Type;
                uint count = this.entry.Count;
                uint offset = this.entry.Offset;

                if (count == 0)
                {
                    return [];
                }

                // Paint.NET always stores data in little-endian byte order.
                byte[] bytes;
                if (type == ExifValueType.Byte
                    || type == ExifValueType.Ascii
                    || type == (ExifValueType)6 // SByte
                    || type == ExifValueType.Undefined)
                {
                    bytes = new byte[count];

                    if (this.offsetIsBigEndian)
                    {
                        switch (count)
                        {
                            case 1:
                                bytes[0] = (byte)((offset >> 24) & 0x000000ff);
                                break;
                            case 2:
                                bytes[0] = (byte)((offset >> 24) & 0x000000ff);
                                bytes[1] = (byte)((offset >> 16) & 0x000000ff);
                                break;
                            case 3:
                                bytes[0] = (byte)((offset >> 24) & 0x000000ff);
                                bytes[1] = (byte)((offset >> 16) & 0x000000ff);
                                bytes[2] = (byte)((offset >> 8) & 0x000000ff);
                                break;
                            case 4:
                                bytes[0] = (byte)((offset >> 24) & 0x000000ff);
                                bytes[1] = (byte)((offset >> 16) & 0x000000ff);
                                bytes[2] = (byte)((offset >> 8) & 0x000000ff);
                                bytes[3] = (byte)(offset & 0x000000ff);
                                break;
                        }
                    }
                    else
                    {
                        switch (count)
                        {
                            case 1:
                                bytes[0] = (byte)(offset & 0x000000ff);
                                break;
                            case 2:
                                bytes[0] = (byte)(offset & 0x000000ff);
                                bytes[1] = (byte)((offset >> 8) & 0x000000ff);
                                break;
                            case 3:
                                bytes[0] = (byte)(offset & 0x000000ff);
                                bytes[1] = (byte)((offset >> 8) & 0x000000ff);
                                bytes[2] = (byte)((offset >> 16) & 0x000000ff);
                                break;
                            case 4:
                                bytes[0] = (byte)(offset & 0x000000ff);
                                bytes[1] = (byte)((offset >> 8) & 0x000000ff);
                                bytes[2] = (byte)((offset >> 16) & 0x000000ff);
                                bytes[3] = (byte)((offset >> 24) & 0x000000ff);
                                break;
                        }
                    }
                }
                else if (type == ExifValueType.Short || type == ExifValueType.SShort)
                {
                    int byteArrayLength = unchecked((int)count) * sizeof(ushort);
                    bytes = new byte[byteArrayLength];

                    fixed (byte* ptr = bytes)
                    {
                        ushort* ushortPtr = (ushort*)ptr;

                        if (this.offsetIsBigEndian)
                        {
                            switch (count)
                            {
                                case 1:
                                    ushortPtr[0] = (ushort)((offset >> 16) & 0x0000ffff);
                                    break;
                                case 2:
                                    ushortPtr[0] = (ushort)((offset >> 16) & 0x0000ffff);
                                    ushortPtr[1] = (ushort)(offset & 0x0000ffff);
                                    break;
                            }
                        }
                        else
                        {
                            switch (count)
                            {
                                case 1:
                                    ushortPtr[0] = (ushort)(offset & 0x0000ffff);
                                    break;
                                case 2:
                                    ushortPtr[0] = (ushort)(offset & 0x0000ffff);
                                    ushortPtr[1] = (ushort)((offset >> 16) & 0x0000ffff);
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    bytes = new byte[4];

                    fixed (byte* ptr = bytes)
                    {
                        // The offset is stored as little-endian in memory.
                        *(uint*)ptr = offset;
                    }
                }

                return bytes;
            }

            public override string ToString()
            {
                if (this.OffsetFieldContainsValue)
                {
                    return string.Format("Tag={0}, Type={1}, Count={2}, Value={3}",
                                         this.entry.Tag.ToString(CultureInfo.InvariantCulture),
                                         this.entry.Type.ToString(),
                                         this.entry.Count.ToString(CultureInfo.InvariantCulture),
                                         GetValueStringFromOffset());
                }
                else
                {
                    return string.Format("Tag={0}, Type={1}, Count={2}, Offset=0x{3}",
                                         this.entry.Tag.ToString(CultureInfo.InvariantCulture),
                                         this.entry.Type.ToString(),
                                         this.entry.Count.ToString(CultureInfo.InvariantCulture),
                                         this.entry.Offset.ToString("X", CultureInfo.InvariantCulture));
                }
            }

            private string GetValueStringFromOffset()
            {
                string valueString;

                ExifValueType type = this.entry.Type;
                uint count = this.entry.Count;
                uint offset = this.entry.Offset;

                if (count == 0)
                {
                    return string.Empty;
                }

                int typeSizeInBytes = ExifValueTypeUtil.GetSizeInBytes(type);

                if (typeSizeInBytes == 1)
                {
                    byte[] bytes = new byte[count];

                    if (this.offsetIsBigEndian)
                    {
                        switch (count)
                        {
                            case 1:
                                bytes[0] = (byte)((offset >> 24) & 0x000000ff);
                                break;
                            case 2:
                                bytes[0] = (byte)((offset >> 24) & 0x000000ff);
                                bytes[1] = (byte)((offset >> 16) & 0x000000ff);
                                break;
                            case 3:
                                bytes[0] = (byte)((offset >> 24) & 0x000000ff);
                                bytes[1] = (byte)((offset >> 16) & 0x000000ff);
                                bytes[2] = (byte)((offset >> 8) & 0x000000ff);
                                break;
                            case 4:
                                bytes[0] = (byte)((offset >> 24) & 0x000000ff);
                                bytes[1] = (byte)((offset >> 16) & 0x000000ff);
                                bytes[2] = (byte)((offset >> 8) & 0x000000ff);
                                bytes[3] = (byte)(offset & 0x000000ff);
                                break;
                        }
                    }
                    else
                    {
                        switch (count)
                        {
                            case 1:
                                bytes[0] = (byte)(offset & 0x000000ff);
                                break;
                            case 2:
                                bytes[0] = (byte)(offset & 0x000000ff);
                                bytes[1] = (byte)((offset >> 8) & 0x000000ff);
                                break;
                            case 3:
                                bytes[0] = (byte)(offset & 0x000000ff);
                                bytes[1] = (byte)((offset >> 8) & 0x000000ff);
                                bytes[2] = (byte)((offset >> 16) & 0x000000ff);
                                break;
                            case 4:
                                bytes[0] = (byte)(offset & 0x000000ff);
                                bytes[1] = (byte)((offset >> 8) & 0x000000ff);
                                bytes[2] = (byte)((offset >> 16) & 0x000000ff);
                                bytes[3] = (byte)((offset >> 24) & 0x000000ff);
                                break;
                        }
                    }

                    if (type == ExifValueType.Ascii)
                    {
                        valueString = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    }
                    else if (count == 1)
                    {
                        valueString = bytes[0].ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        StringBuilder builder = new();

                        uint lastItemIndex = count - 1;

                        for (int i = 0; i < count; i++)
                        {
                            builder.Append(bytes[i].ToString(CultureInfo.InvariantCulture));

                            if (i < lastItemIndex)
                            {
                                builder.Append(',');
                            }
                        }

                        valueString = builder.ToString();
                    }
                }
                else if (typeSizeInBytes == 2)
                {
                    ushort[] values = new ushort[count];
                    if (this.offsetIsBigEndian)
                    {
                        switch (count)
                        {
                            case 1:
                                values[0] = (ushort)((offset >> 16) & 0x0000ffff);
                                break;
                            case 2:
                                values[0] = (ushort)((offset >> 16) & 0x0000ffff);
                                values[1] = (ushort)(offset & 0x0000ffff);
                                break;
                        }
                    }
                    else
                    {
                        switch (count)
                        {
                            case 1:
                                values[0] = (ushort)(offset & 0x0000ffff);
                                break;
                            case 2:
                                values[0] = (ushort)(offset & 0x0000ffff);
                                values[1] = (ushort)((offset >> 16) & 0x0000ffff);
                                break;
                        }
                    }

                    if (count == 1)
                    {
                        switch (type)
                        {
                            case ExifValueType.SShort:
                                valueString = ((short)values[0]).ToString(CultureInfo.InvariantCulture);
                                break;
                            case ExifValueType.Short:
                            default:
                                valueString = values[0].ToString(CultureInfo.InvariantCulture);
                                break;
                        }
                    }
                    else
                    {
                        switch (type)
                        {
                            case ExifValueType.SShort:
                                valueString = ((short)values[0]).ToString(CultureInfo.InvariantCulture) + "," +
                                              ((short)values[1]).ToString(CultureInfo.InvariantCulture);
                                break;
                            case ExifValueType.Short:
                            default:
                                valueString = values[0].ToString(CultureInfo.InvariantCulture) + "," +
                                              values[1].ToString(CultureInfo.InvariantCulture);
                                break;
                        }
                    }
                }
                else
                {
                    valueString = offset.ToString(CultureInfo.InvariantCulture);
                }

                return valueString;
            }
        }

        private readonly struct MetadataOffset
        {
            public MetadataOffset(ExifSection section, uint offset)
            {
                this.Section = section;
                this.Offset = offset;
            }

            public ExifSection Section { get; }

            public uint Offset { get; }
        }
    }
}
