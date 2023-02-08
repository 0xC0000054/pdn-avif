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

using System;

namespace AvifFileType.Interop
{
    internal readonly ref struct NativeDecodeInfo
    {
        public readonly uint expectedWidth;
        public readonly uint expectedHeight;
        public readonly uint tileColumnIndex;
        public readonly uint tileRowIndex;
        public readonly YUVChromaSubsampling chromaSubsampling;
        public readonly uint bitDepth;
        public readonly NativeCICPColorData firstTileColorData;
        public readonly ushort spatialLayerId;
        public readonly byte allLayers;
        public readonly byte operatingPoint;

        internal NativeDecodeInfo(DecodeInfo managed)
        {
            ArgumentNullException.ThrowIfNull(managed);

            this.expectedWidth = managed.expectedWidth;
            this.expectedHeight = managed.expectedHeight;
            this.tileColumnIndex = managed.tileColumnIndex;
            this.tileRowIndex = managed.tileRowIndex;
            this.chromaSubsampling = managed.chromaSubsampling;
            this.bitDepth = managed.bitDepth;
            this.firstTileColorData = managed.firstTileColorData.ToNative();
            this.spatialLayerId = managed.spatialLayerId;
            this.allLayers = (byte)(managed.allLayers ? 1 : 0);
            this.operatingPoint = managed.operatingPoint;
        }
    }
}
