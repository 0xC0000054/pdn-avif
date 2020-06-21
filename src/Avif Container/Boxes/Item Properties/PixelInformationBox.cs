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
using System.ComponentModel;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal sealed class PixelInformationBox
        : ItemPropertyFull
    {

        public PixelInformationBox(EndianBinaryReader reader, Box header)
            : base(reader, header)
        {
            if (this.Version != 0)
            {
                ExceptionUtil.ThrowFormatException($"{ nameof(PixelInformationBox) } version must be 0, actual value: { this.Version }.");
            }

            byte channelCount = reader.ReadByte();

            List<byte> bitDepths = new List<byte>(channelCount);
            for (int i = 0; i < channelCount; i++)
            {
                bitDepths.Add(reader.ReadByte());
            }

            this.ChannelBitDepths = bitDepths;
        }

        public PixelInformationBox(YUVChromaSubsampling chromaSubsampling)
            : base(0, 0, BoxTypes.PixelInformation)
        {
            switch (chromaSubsampling)
            {
                case YUVChromaSubsampling.Subsampling400:
                    this.ChannelBitDepths = new byte[1] { 8 };
                    break;
                case YUVChromaSubsampling.Subsampling420:
                case YUVChromaSubsampling.Subsampling422:
                case YUVChromaSubsampling.Subsampling444:
                case YUVChromaSubsampling.IdentityMatrix:
                    this.ChannelBitDepths = new byte[3] { 8, 8, 8 };
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(chromaSubsampling), (int)chromaSubsampling, typeof(YUVChromaSubsampling));
            }
        }

        public IReadOnlyList<byte> ChannelBitDepths { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                return $"Channel count: {this.ChannelBitDepths.Count}";
            }
        }

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
