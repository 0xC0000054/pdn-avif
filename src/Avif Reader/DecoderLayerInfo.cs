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

namespace AvifFileType
{
    internal readonly struct DecoderLayerInfo
    {
        public readonly ushort spatialLayerId;
        public readonly bool allLayers;
        public readonly byte operatingPoint;

        public DecoderLayerInfo(ushort spatialLayerId, bool allLayers, byte operatingPoint)
        {
            this.spatialLayerId = spatialLayerId;
            this.allLayers = allLayers;
            this.operatingPoint = operatingPoint;
        }
    }
}
