////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Runtime.InteropServices;

namespace AvifFileType.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class DecodeInfo
    {
        public uint expectedWidth;
        public uint expectedHeight;
        public uint tileColumnIndex;
        public uint tileRowIndex;
        public YUVChromaSubsampling chromaSubsampling;
        public uint bitDepth;
        public CICPColorData firstTileColorData;
        public ushort spatialLayerId;
        [MarshalAs(UnmanagedType.U1)]
        public bool allLayers;
        public byte operatingPoint;
    }
}
