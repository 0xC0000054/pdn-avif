////////////////////////////////////////////////////////////////////////
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

using System.Collections.Generic;

namespace AvifFileType.Exif
{
    internal static class ReadOnlyListExtensions
    {
        internal static T[] AsArrayOrToArray<T>(this IReadOnlyList<T> items)
        {
            if (items is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(items));
            }

            return items is T[] asArray ? asArray : [.. items];
        }
    }
}
