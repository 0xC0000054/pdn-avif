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

using PaintDotNet.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace AvifFileType
{
    internal static class ObjectRefExtensions
    {
        [return: NotNullIfNotNull(nameof(newObjectRef))]
        internal static T? ReplaceRef<T, U>(this T? objectRef, U? newObjectRef)
            where T : class, IObjectRef
            where U : class, T
        {
            objectRef?.Dispose();
            return newObjectRef;
        }
    }
}
