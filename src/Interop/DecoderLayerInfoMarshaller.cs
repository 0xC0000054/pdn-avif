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

using System.Runtime.InteropServices.Marshalling;

namespace AvifFileType.Interop
{
    [CustomMarshaller(typeof(DecoderLayerInfo), MarshalMode.ManagedToUnmanagedIn, typeof(DecoderLayerInfoMarshaller))]
    internal static class DecoderLayerInfoMarshaller
    {
        internal readonly struct NativeDecoderFrameInfo
        {
            public readonly ushort spatialLayerId;
            public readonly byte allLayers;
            public readonly byte operatingPoint;

            public NativeDecoderFrameInfo(DecoderLayerInfo info)
            {
                this.spatialLayerId = info.spatialLayerId;
                this.allLayers = info.allLayers.ToByte();
                this.operatingPoint = info.operatingPoint;
            }
        }

        public static NativeDecoderFrameInfo ConvertToUnmanaged(DecoderLayerInfo managed)
        {
            return new NativeDecoderFrameInfo(managed);
        }
    }
}
