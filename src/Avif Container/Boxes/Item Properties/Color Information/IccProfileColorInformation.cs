////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;

namespace AvifFileType.AvifContainer
{
    internal sealed class IccProfileColorInformation
        : ColorInformationBox
    {
        private readonly ReadOnlyMemory<byte> iccProfile;

        public IccProfileColorInformation(in EndianBinaryReaderSegment reader, ColorInformationBox header)
            : base(header)
        {
            long profileLength = reader.EndOffset - reader.Position;

            if (profileLength > int.MaxValue)
            {
                throw new NotSupportedException("The ICC color profile is larger than 2GB.");
            }

            this.iccProfile = reader.ReadBytes((int)profileLength);
        }

        public IccProfileColorInformation(ReadOnlyMemory<byte> iccProfile)
            : base(ColorInformationBoxTypes.IccProfile)
        {
            this.iccProfile = iccProfile;
        }

        public ReadOnlyMemory<byte> ProfileData => this.iccProfile;

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.iccProfile.Span);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + (ulong)this.iccProfile.Length;
        }
    }
}
