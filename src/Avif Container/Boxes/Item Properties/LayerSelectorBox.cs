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

namespace AvifFileType.AvifContainer
{
    internal sealed class LayerSelectorBox
        : ItemProperty
    {
        public LayerSelectorBox(in EndianBinaryReaderSegment reader, Box header)
           : base(header)
        {
            this.LayerId = reader.ReadUInt16();
        }

        public LayerSelectorBox(ushort layerId)
            : base(BoxTypes.LayerSelector)
        {
            this.LayerId = layerId;
        }

        public ushort LayerId { get; }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.LayerId);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + sizeof(ushort);
        }
    }
}
