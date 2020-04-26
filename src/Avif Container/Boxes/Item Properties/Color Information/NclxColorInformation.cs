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
    internal sealed class NclxColorInformation
        : ColorInformationBox
    {
        private const byte FullRangeMask = 1 << 7;

        public NclxColorInformation(EndianBinaryReader reader, ColorInformationBox header)
            : base(header)
        {
            this.ColorPrimaries = (NclxColorPrimaries)reader.ReadUInt16();
            this.TransferCharacteristics = (NclxTransferCharacteristics)reader.ReadUInt16();
            this.MatrixCoefficients = (NclxMatrixCoefficients)reader.ReadUInt16();
            this.FullRange = (reader.ReadByte() & FullRangeMask) == FullRangeMask;
        }

        public NclxColorInformation(NclxColorPrimaries colorPrimaries,
                                    NclxTransferCharacteristics transferCharacteristics,
                                    NclxMatrixCoefficients matrixCoefficients,
                                    bool fullRange)
            : base(ColorInformationBoxTypes.Nclx)
        {
            this.ColorPrimaries = colorPrimaries;
            this.TransferCharacteristics = transferCharacteristics;
            this.MatrixCoefficients = matrixCoefficients;
            this.FullRange = fullRange;
        }

        public NclxColorPrimaries ColorPrimaries { get; }

        public NclxTransferCharacteristics TransferCharacteristics { get; }

        public NclxMatrixCoefficients MatrixCoefficients { get; }

        public bool FullRange { get; }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write((ushort)this.ColorPrimaries);
            writer.Write((ushort)this.TransferCharacteristics);
            writer.Write((ushort)this.MatrixCoefficients);
            writer.Write((byte)(this.FullRange ? FullRangeMask : 0));
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize()
                   + sizeof(ushort) // Color primaries
                   + sizeof(ushort) // Transfer characteristics
                   + sizeof(ushort) // Matrix coefficients
                   + sizeof(byte); // Full range
        }
    }
}
