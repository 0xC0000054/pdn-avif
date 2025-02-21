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

using AvifFileType.Interop;
using System;

namespace AvifFileType
{
    internal sealed class DecoderImage : Disposable
    {
        public DecoderImage(
            SafeDecoderImageHandle handle,
            DecoderImageInfo info)
        {
            this.SafeDecoderImage = handle ?? throw new ArgumentNullException(nameof(handle));
            this.Info = info ?? throw new ArgumentNullException(nameof(info));
        }

        public DecoderImageInfo Info { get; }

        public uint Width => this.Info.Width;

        public uint Height => this.Info.Height;

        public uint BitDepth => this.Info.BitDepth;

        public YUVChromaSubsampling ChromaSubsampling => this.Info.ChromaSubsampling;

        public CICPColorData CICPColor => this.Info.CICPColor;

        public SafeDecoderImageHandle SafeDecoderImage { get; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.SafeDecoderImage.Dispose();
            }
        }
    }
}
