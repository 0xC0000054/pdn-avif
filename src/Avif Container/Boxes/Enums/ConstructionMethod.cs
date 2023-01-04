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
    /// <summary>
    /// Describes how <see cref="ItemLocationEntry"/> offsets are calculated.
    /// </summary>
    internal enum ConstructionMethod
    {
        /// <summary>
        /// The offset is from the start of the file.
        /// </summary>
        FileOffset = 0,

        /// <summary>
        /// The offset is from the start of the <see cref="ItemDataBox"/>.
        /// </summary>
        IDatBoxOffset = 1,

        /// <summary>
        /// The offset is an index into the extent array.
        /// </summary>
        ItemOffset = 2
    }
}
