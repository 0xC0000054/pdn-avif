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
using System.Collections.Generic;

namespace AvifFileType.AvifContainer
{
    internal sealed class PixelInformationBox : FullBox, IItemProperty
    {

        public PixelInformationBox(EndianBinaryReader reader, Box header)
            : base(reader, header)
        {
            if (this.Version != 0)
            {
                throw new FormatException("ImageSpatialExtentsBox version must be 0, actual value: " + this.Version.ToString());
            }

            byte channelCount = reader.ReadByte();

            List<byte> bitDepths = new List<byte>(channelCount);
            for (int i = 0; i < channelCount; i++)
            {
                bitDepths.Add(reader.ReadByte());
            }

            this.ChannelBitDepths = bitDepths;
        }

        public PixelInformationBox(bool monochromeImage)
            : base(0, 0, BoxTypes.PixelInformation)
        {
            this.ChannelBitDepths = monochromeImage ? new byte[1] { 8 } : new byte[3] { 8, 8, 8 };
        }
        public IReadOnlyList<byte> ChannelBitDepths { get; }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write((byte)this.ChannelBitDepths.Count);

            for (int i = 0; i < this.ChannelBitDepths.Count; i++)
            {
                writer.Write(this.ChannelBitDepths[i]);
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + sizeof(byte) + ((ulong)this.ChannelBitDepths.Count * sizeof(byte));
        }
    }
}
