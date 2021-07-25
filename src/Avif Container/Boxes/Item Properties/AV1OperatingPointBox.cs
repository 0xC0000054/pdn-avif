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

namespace AvifFileType.AvifContainer
{
    internal sealed class AV1OperatingPointBox
        : ItemProperty
    {
        private const int MaxOperatingPointIndex = 31;

        public AV1OperatingPointBox(in EndianBinaryReaderSegment reader, Box header)
           : base(header)
        {
            byte operatingPointIndex = reader.ReadByte();

            if (operatingPointIndex > MaxOperatingPointIndex)
            {
                ExceptionUtil.ThrowFormatException($"The { nameof(AV1OperatingPointBox)} box contains an unsupported value: { operatingPointIndex }.");
            }

            this.OperatingPointIndex = operatingPointIndex;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AV1OperatingPointBox"/> class.
        /// </summary>
        /// <param name="operatingPointIndex">The operating point index.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="operatingPointIndex"/> is an unsupported value.</exception>
        public AV1OperatingPointBox(byte operatingPointIndex)
            : base(BoxTypes.AV1OperatingPoint)
        {
            if (operatingPointIndex > MaxOperatingPointIndex)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(operatingPointIndex), $"Must be <= { MaxOperatingPointIndex }.");
            }

            this.OperatingPointIndex = operatingPointIndex;
        }

        public byte OperatingPointIndex { get; }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.OperatingPointIndex);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + sizeof(byte);
        }
    }
}
