////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

namespace AvifFileType.AvifContainer
{
    internal sealed class ItemDataBox : Box
    {
        private readonly byte[] dataToWrite;

        public ItemDataBox(Box header) : base(header)
        {
        }

        public ItemDataBox(byte[] dataToWrite)
            : base(BoxTypes.ItemData)
        {
            if (dataToWrite is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(dataToWrite));
            }

            this.dataToWrite = dataToWrite;
        }

        public long Offset => this.DataStartOffset;

        public long Length => this.DataLength;

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.dataToWrite, 0, this.dataToWrite.Length);
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + (ulong)this.dataToWrite.Length;
        }
    }
}
