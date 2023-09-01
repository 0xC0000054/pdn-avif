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
    internal sealed class ItemDataBox : Box
    {
        private readonly byte[] data;

        public ItemDataBox(Box header) : base(header)
        {
            this.data = Array.Empty<byte>();
        }

        public ItemDataBox(byte[] dataToWrite)
            : base(BoxTypes.ItemData)
        {
            if (dataToWrite is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(dataToWrite));
            }

            this.data = dataToWrite;
        }

        public long Offset => this.DataStartOffset;

        public long Length => this.DataLength;

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.data, 0, this.data.Length);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + (ulong)this.data.Length;
        }
    }
}
