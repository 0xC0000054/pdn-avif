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
using System.Collections.ObjectModel;

namespace AvifFileType.AvifContainer
{
    internal sealed class AV1LayeredImageIndexingBox
        : ItemProperty
    {
        private const int LargeItemIdFlag = 1;
        private const int ReservedFlags = 0xfe;
        private const int LayerSizeItemCount = 3;

        private readonly bool largeItemId;
        private readonly ReadOnlyCollection<uint> layerSize;

        public AV1LayeredImageIndexingBox(in EndianBinaryReaderSegment reader, Box header)
           : base(header)
        {
            byte flags = reader.ReadByte();

            if ((flags & ReservedFlags) != 0)
            {
                ExceptionUtil.ThrowFormatException($"The { nameof(AV1LayeredImageIndexingBox)} box contains an unsupported header flag: { flags }.");
            }

            this.largeItemId = (flags & LargeItemIdFlag) == LargeItemIdFlag;

            uint[] values = new uint[LayerSizeItemCount];

            for (int i = 0; i < values.Length; i++)
            {
                if (this.largeItemId)
                {
                    values[i] = reader.ReadUInt32();
                }
                else
                {
                    values[i] = reader.ReadUInt16();
                }
            }

            this.layerSize = new ReadOnlyCollection<uint>(values);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AV1LayeredImageIndexingBox"/> class.
        /// </summary>
        /// <param name="largeItemId"><see langword="true"/> if the item id is 32-bit; otherwise, <see langword="false"/>.</param>
        /// <param name="layerSize">The list of layer sizes.</param>
        /// <exception cref="ArgumentNullException"><paramref name="layerSize"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="layerSize"/> has an invalid item count.</exception>
        public AV1LayeredImageIndexingBox(bool largeItemId, ReadOnlyCollection<uint> layerSize)
            : base(BoxTypes.AV1LayeredImageIndexing)
        {
            if (layerSize is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(layerSize));
            }

            if (layerSize.Count != LayerSizeItemCount)
            {
                ExceptionUtil.ThrowArgumentException($"The { nameof(layerSize) } collection must contain { LayerSizeItemCount } items.");
            }

            this.largeItemId = largeItemId;
            this.layerSize = layerSize;
        }

        public IReadOnlyList<uint> LayerSize => this.layerSize;

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write((byte)(this.largeItemId ? LargeItemIdFlag : 0));

            for (int i = 0; i < this.layerSize.Count; i++)
            {
                uint value = this.layerSize[i];

                if (this.largeItemId)
                {
                    writer.Write(value);
                }
                else
                {
                    writer.Write((ushort)value);
                }
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            ulong size = base.GetTotalBoxSize() + sizeof(byte);

            if (this.largeItemId)
            {
                size += 3 * sizeof(uint);
            }
            else
            {
                size += 3 * sizeof(ushort);
            }

            return size;
        }
    }
}
