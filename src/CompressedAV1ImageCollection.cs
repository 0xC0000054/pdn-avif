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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType
{
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(CompressedAV1ImageCollectionDebugView))]
    internal sealed class CompressedAV1ImageCollection : IList<CompressedAV1Image>, IReadOnlyList<CompressedAV1Image>, IDisposable
    {
        private readonly List<CompressedAV1Image> items;
        private bool disposed;
        private int version;

        public CompressedAV1ImageCollection(int capacity)
        {
            this.items = new List<CompressedAV1Image>(capacity);
            this.disposed = false;
        }

        public CompressedAV1Image this[int index]
        {
            get
            {
                VerifyNotDisposed();

                return this.items[index];
            }
            set
            {
                VerifyNotDisposed();

                if ((uint)index < (uint)this.items.Count)
                {
                    this.items[index]?.Dispose();
                }

                this.items[index] = value;
                this.version++;
            }
        }

        public int Capacity => this.items.Capacity;

        public int Count => this.items.Count;

        public bool IsReadOnly => false;

        public void Add(CompressedAV1Image item)
        {
            VerifyNotDisposed();

            this.items.Add(item);
            this.version++;
        }

        public void Clear()
        {
            for (int i = 0; i < this.items.Count; i++)
            {
                this.items[i]?.Dispose();
            }

            this.items.Clear();
            this.version++;
        }

        public bool Contains(CompressedAV1Image item)
        {
            return this.items.Contains(item);
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.disposed = true;

                for (int i = 0; i < this.items.Count; i++)
                {
                    this.items[i]?.Dispose();
                }
                this.version++;
            }
        }

        public Enumerator GetEnumerator()
        {
            VerifyNotDisposed();

            return new Enumerator(this);
        }

        public int IndexOf(CompressedAV1Image item)
        {
            return this.items.IndexOf(item);
        }

        public void Insert(int index, CompressedAV1Image item)
        {
            if ((uint)index > (uint)this.items.Count)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(index));
            }

            VerifyNotDisposed();

            if (index < this.items.Count)
            {
                this.items[index]?.Dispose();
            }
            this.items.Insert(index, item);
            this.version++;
        }

        public bool Remove(CompressedAV1Image item)
        {
            int index = this.items.IndexOf(item);

            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }

            return false;
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)this.items.Count)
            {
                ExceptionUtil.ThrowArgumentOutOfRangeException(nameof(index));
            }

            this.items[index]?.Dispose();
            this.items.RemoveAt(index);
            this.version++;
        }

        void ICollection<CompressedAV1Image>.CopyTo(CompressedAV1Image[] array, int arrayIndex)
        {
            throw new NotSupportedException("The items are disposable resources owned by this collection.");
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator<CompressedAV1Image> IEnumerable<CompressedAV1Image>.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void VerifyNotDisposed()
        {
            if (this.disposed)
            {
                ExceptionUtil.ThrowObjectDisposedException(nameof(CompressedAV1ImageCollection));
            }
        }

        public struct Enumerator : IEnumerator<CompressedAV1Image>
        {
            private CompressedAV1ImageCollection items;
            private int index;
            private readonly int version;

            public Enumerator(CompressedAV1ImageCollection items) : this()
            {
                this.items = items;
                this.index = 0;
                this.version = items.version;
                this.Current = default;
            }

            public CompressedAV1Image Current { get; private set; }

            object IEnumerator.Current
            {
                get
                {
                    if (this.index == 0 || this.index >= this.items.Count)
                    {
                        ExceptionUtil.ThrowInvalidOperationException("The enumeration has not started or has reached the end of the collection.");
                    }

                    return this.Current;
                }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                CompressedAV1ImageCollection localItems = this.items;

                if (localItems.version == this.version && (uint)this.index < (uint)localItems.Count)
                {
                    this.Current = localItems[this.index];
                    this.index++;

                    return true;
                }
                else
                {
                    if (localItems.version != this.version)
                    {
                        ExceptionUtil.ThrowInvalidOperationException("The collection was modified while enumerating.");
                    }

                    this.index = localItems.Count;
                    this.Current = default;

                    return false;
                }
            }

            public void Reset()
            {
                this.index = 0;
                this.Current = default;
            }
        }

        private sealed class CompressedAV1ImageCollectionDebugView
        {
            private readonly CompressedAV1ImageCollection collection;

            public CompressedAV1ImageCollectionDebugView(CompressedAV1ImageCollection collection)
            {
                this.collection = collection;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public CompressedAV1Image[] Items => this.collection.items.ToArray();
        }
    }
}
