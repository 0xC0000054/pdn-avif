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

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("AuxType: {AuxType, nq}")]
    internal class AuxiliaryTypePropertyBox
        : ItemPropertyFull
    {
        private readonly byte[] auxSubType;

        public AuxiliaryTypePropertyBox(in EndianBinaryReaderSegment reader, Box header)
            : base(reader, header)
        {
            this.AuxType = reader.ReadBoxString();
            long remaining = reader.EndOffset - reader.Position;

            if (remaining > 0 && remaining < int.MaxValue)
            {
                this.auxSubType = reader.ReadBytes((int)remaining);
            }
            else
            {
                this.auxSubType = [];
            }
        }

        protected AuxiliaryTypePropertyBox(string auxType)
            : base(0, 0, BoxTypes.AuxiliaryTypeProperty)
        {
            this.AuxType = new BoxString(auxType);
            this.auxSubType = [];
        }

        public IReadOnlyList<byte> AuxSubType => this.auxSubType;

        public BoxString AuxType { get; }

        public sealed override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            this.AuxType.Write(writer);
            if (this.auxSubType.Length > 0)
            {
                writer.Write(this.auxSubType);
            }
        }

        protected sealed override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize()
                   + this.AuxType.GetSize()
                   + (ulong)this.auxSubType.Length;
        }
    }
}
