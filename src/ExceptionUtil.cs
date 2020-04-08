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

namespace AvifFileType
{
    internal static class ExceptionUtil
    {
        internal static void ThrowArgumentException(string message)
        {
            throw new ArgumentException(message);
        }

        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        public static void ThrowArgumentOutOfRangeException(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }

        public static void ThrowFormatException(string message)
        {
            throw new FormatException(message);
        }

        public static void ThrowInvalidOperationException(string message)
        {
            throw new InvalidOperationException(message);
        }

        internal static void ThrowObjectDisposedException(string objectName)
        {
            throw new ObjectDisposedException(objectName);
        }
    }
}
