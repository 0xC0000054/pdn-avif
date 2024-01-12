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

using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerTypeProxy(typeof(NclxColorInformationDebugView))]
    internal sealed class NclxColorInformation
        : ColorInformationBox
    {
        private const byte FullRangeMask = 1 << 7;

        public NclxColorInformation(in EndianBinaryReaderSegment reader, ColorInformationBox header)
            : base(header)
        {
            this.ColorPrimaries = (CICPColorPrimaries)reader.ReadUInt16();
            this.TransferCharacteristics = (CICPTransferCharacteristics)reader.ReadUInt16();
            this.MatrixCoefficients = (CICPMatrixCoefficients)reader.ReadUInt16();
            this.FullRange = (reader.ReadByte() & FullRangeMask) == FullRangeMask;
        }

        public NclxColorInformation(CICPColorPrimaries colorPrimaries,
                                    CICPTransferCharacteristics transferCharacteristics,
                                    CICPMatrixCoefficients matrixCoefficients,
                                    bool fullRange)
            : base(ColorInformationBoxTypes.Nclx)
        {
            this.ColorPrimaries = colorPrimaries;
            this.TransferCharacteristics = transferCharacteristics;
            this.MatrixCoefficients = matrixCoefficients;
            this.FullRange = fullRange;
        }

        public CICPColorPrimaries ColorPrimaries { get; }

        public CICPTransferCharacteristics TransferCharacteristics { get; }

        public CICPMatrixCoefficients MatrixCoefficients { get; }

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

        private sealed class NclxColorInformationDebugView
        {
            private readonly NclxColorInformation nclxColorInformation;

            public NclxColorInformationDebugView(NclxColorInformation nclxColorInformation)
            {
                this.nclxColorInformation = nclxColorInformation;
            }

            public CICPColorPrimaries ColorPrimaries => this.nclxColorInformation.ColorPrimaries;

            public CICPTransferCharacteristics TransferCharacteristics => this.nclxColorInformation.TransferCharacteristics;

            public CICPMatrixCoefficients MatrixCoefficients => this.nclxColorInformation.MatrixCoefficients;

            public bool FullRange => this.nclxColorInformation.FullRange;
        }
    }
}
