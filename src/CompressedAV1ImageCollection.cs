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
using System.Collections.ObjectModel;

namespace AvifFileType
{
    internal sealed class CompressedAV1ImageCollection : Collection<CompressedAV1Image>, IDisposable
    {
        private bool disposed;

        public CompressedAV1ImageCollection(int capacity) : base(new List<CompressedAV1Image>(capacity))
        {
            this.Capacity = capacity;
            this.disposed = false;
        }

        public int Capacity { get; }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;

                IList<CompressedAV1Image> items = this.Items;

                for (int i = 0; i < items.Count; i++)
                {
                    items[i]?.Dispose();
                }
            }
        }

        protected override void ClearItems()
        {
            VerifyNotDisposed();

            IList<CompressedAV1Image> items = this.Items;

            for (int i = 0; i < items.Count; i++)
            {
                items[i]?.Dispose();
            }

            base.ClearItems();
        }

        protected override void InsertItem(int index, CompressedAV1Image item)
        {
            VerifyNotDisposed();

            IList<CompressedAV1Image> items = this.Items;

            if (index < items.Count)
            {
                items[index]?.Dispose();
            }

            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            VerifyNotDisposed();

            IList<CompressedAV1Image> items = this.Items;

            if (index < items.Count)
            {
                items[index]?.Dispose();
            }

            base.RemoveItem(index);
        }

        protected override void SetItem(int index, CompressedAV1Image item)
        {
            VerifyNotDisposed();

            IList<CompressedAV1Image> items = this.Items;

            if (index < items.Count)
            {
                items[index]?.Dispose();
            }

            base.SetItem(index, item);
        }

        private void VerifyNotDisposed()
        {
            if (this.disposed)
            {
                ExceptionUtil.ThrowObjectDisposedException(nameof(CompressedAV1ImageCollection));
            }
        }
    }
}
