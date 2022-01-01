////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace AvifFileType.AvifContainer
{
    internal sealed class LayerSelectorInfo
    {
        public LayerSelectorInfo(AV1LayeredImageIndexingBox layeredImageIndexingBox,
                                 ulong totalItemSize,
                                 ushort spatialLayerId)
        {
            this.LayerDataSize = TryGetLayerDataSize(layeredImageIndexingBox, totalItemSize, spatialLayerId);
            this.SpatialLayerId = spatialLayerId;
        }

        public ulong? LayerDataSize { get; }

        public ushort SpatialLayerId { get; }

        private static List<ulong> GetLayerSizes(AV1LayeredImageIndexingBox layeredImageIndexingBox, ulong totalItemSize)
        {
            List<ulong> layerSizes = new List<ulong>(4);

            ulong remainingBytes = totalItemSize;

            foreach (uint size in layeredImageIndexingBox.LayerSize)
            {
                if (size > 0)
                {
                    if (size >= remainingBytes)
                    {
                        ExceptionUtil.ThrowFormatException($"The a1lx layer index does not fit in the remaining bytes.");
                    }

                    layerSizes.Add(size);
                    remainingBytes -= size;
                }
                else
                {
                    layerSizes.Add(remainingBytes);
                    remainingBytes = 0;
                    break;
                }
            }

            if (remainingBytes > 0)
            {
                layerSizes.Add(remainingBytes);
            }

            return layerSizes;
        }

        private static ulong? TryGetLayerDataSize(AV1LayeredImageIndexingBox layeredImageIndexingBox,
                                                  ulong totalItemSize,
                                                  uint layerId)
        {
            if (layeredImageIndexingBox is null)
            {
                return null;
            }

            List<ulong> layerSizes = GetLayerSizes(layeredImageIndexingBox, totalItemSize);

            ulong? requestedLayerSize = null;

            if (layerSizes.Count > 0)
            {
                if (layerId >= layerSizes.Count)
                {
                    ExceptionUtil.ThrowFormatException("The lsel box requests a layer that is not in the al1x box.");
                }

                ulong layerDataSize = 0;

                for (int i = 0; i <= layerId; i++)
                {
                    layerDataSize += layerSizes[i];
                }

                requestedLayerSize = layerDataSize;
            }

            return requestedLayerSize;
        }
    }
}
