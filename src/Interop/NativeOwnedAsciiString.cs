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

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace AvifFileType.Interop
{
    /// <summary>
    /// A custom marshaller for ASCII strings that are owned by native code.
    /// It converts the data to a managed string without freeing or modifying the native memory.
    /// </summary>
    [CustomMarshaller(typeof(string), MarshalMode.ManagedToUnmanagedOut, typeof(NativeOwnedAsciiString))]
    internal static class NativeOwnedAsciiString
    {
        public static string? ConvertToManaged(nint unmanaged)
        {
            return Marshal.PtrToStringAnsi(unmanaged);
        }
    }
}
