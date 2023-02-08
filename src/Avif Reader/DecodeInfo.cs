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

using AvifFileType.Interop;

namespace AvifFileType
{
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
        public bool allLayers;
        public byte operatingPoint;

        public void CopyFromNative(in NativeDecodeInfo nativeDecodeInfo)
        {
            this.expectedWidth = nativeDecodeInfo.expectedWidth;
            this.expectedHeight = nativeDecodeInfo.expectedHeight;
            this.tileColumnIndex = nativeDecodeInfo.tileColumnIndex;
            this.tileRowIndex = nativeDecodeInfo.tileRowIndex;
            this.chromaSubsampling = nativeDecodeInfo.chromaSubsampling;
            this.bitDepth = nativeDecodeInfo.bitDepth;
            this.firstTileColorData = new CICPColorData(nativeDecodeInfo.firstTileColorData);
            this.spatialLayerId = nativeDecodeInfo.spatialLayerId;
            this.allLayers = nativeDecodeInfo.allLayers != 0;
            this.operatingPoint = nativeDecodeInfo.operatingPoint;
        }

        public NativeDecodeInfo ToNative()
        {
            return new NativeDecodeInfo(this);
        }
    }
}
