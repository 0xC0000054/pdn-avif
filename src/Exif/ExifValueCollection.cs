﻿////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020-2025 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using PaintDotNet.Imaging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AvifFileType.Exif
{
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(ExifValueCollectionDebugView))]
    internal sealed class ExifValueCollection
        : IEnumerable<KeyValuePair<ExifPropertyPath, ExifValue>>
    {
        private readonly Dictionary<ExifPropertyPath, ExifValue> exifMetadata;

        public ExifValueCollection(Dictionary<ExifPropertyPath, ExifValue> items)
        {
            this.exifMetadata = items ?? throw new ArgumentNullException(nameof(items));
        }

        public int Count => this.exifMetadata.Count;

        public void Remove(ExifPropertyPath key)
        {
            this.exifMetadata.Remove(key);
        }

        public IEnumerator<KeyValuePair<ExifPropertyPath, ExifValue>> GetEnumerator()
        {
            return this.exifMetadata.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.exifMetadata.GetEnumerator();
        }

        private sealed class ExifValueCollectionDebugView
        {
            private readonly ExifValueCollection collection;

            public ExifValueCollectionDebugView(ExifValueCollection collection)
            {
                this.collection = collection ?? throw new ArgumentNullException(nameof(collection));
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public KeyValuePair<ExifPropertyPath, ExifValue>[] Items
            {
                get
                {
                    return this.collection.exifMetadata.ToArray();
                }
            }
        }
    }
}
