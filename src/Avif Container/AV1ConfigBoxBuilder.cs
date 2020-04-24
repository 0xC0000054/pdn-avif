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
using System.ComponentModel;

namespace AvifFileType.AvifContainer
{
    internal static class AV1ConfigBoxBuilder
    {
        /// <summary>
        /// Builds the <see cref="AV1ConfigBox"/> for the specified image.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <returns></returns>
        public static AV1ConfigBox Build(CompressedAV1Image image)
        {
            if (image is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(image));
            }

            bool chromaSubsamplingX;
            bool chromaSubsamplingY;

            switch (image.Format)
            {
                case YUVChromaSubsampling.Subsampling400:
                case YUVChromaSubsampling.Subsampling420:
                    chromaSubsamplingX = true;
                    chromaSubsamplingY = true;
                    break;
                case YUVChromaSubsampling.Subsampling422:
                    chromaSubsamplingX = false;
                    chromaSubsamplingY = true;
                    break;
                case YUVChromaSubsampling.Subsampling444:
                    chromaSubsamplingX = false;
                    chromaSubsamplingY = false;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown { nameof(YUVChromaSubsampling) } value: { image.Format }");
            }

            return new AV1ConfigBox()
            {
                SeqProfile = GetSeqProfile(image.Format),
                SeqLevelIdx0 = GetSeqLevelIdx0(image),
                SeqTier0 = false,
                HighBitDepth = false,
                TwelveBit = false,
                Monochrome = image.Format == YUVChromaSubsampling.Subsampling400,
                ChromaSubsamplingX = chromaSubsamplingX,
                ChromaSubsamplingY = chromaSubsamplingY,
                ChromaSamplePosition = 0
            };
        }

        private static byte GetSeqProfile(YUVChromaSubsampling format)
        {
            switch (format)
            {
                case YUVChromaSubsampling.Subsampling400:
                case YUVChromaSubsampling.Subsampling420:
                    return SequenceProfiles.Profile0;
                case YUVChromaSubsampling.Subsampling422:
                    return SequenceProfiles.Profile2;
                case YUVChromaSubsampling.Subsampling444:
                    return SequenceProfiles.Profile1;
                default:
                    throw new InvalidEnumArgumentException(nameof(format), (int)format, typeof(YUVChromaSubsampling));
            }
        }

        private static byte GetSeqLevelIdx0(CompressedAV1Image image)
        {
            int width = image.Width;
            int height = image.Height;
            long imageSize = (long)width * height;

            // These values are from the Annex A.3 table: https://aomediacodec.github.io/av1-spec/av1-spec.pdf
            if (imageSize <= 147456 && width <= 2048 && height <= 1152)
            {
                return 0; // 2.0
            }
            else if (imageSize <= 278784 && width <= 2816 && height <= 1584)
            {
                return 1; // 2.1
            }
            else if (imageSize <= 665856 && width <= 4352 && height <= 2448)
            {
                return 4; // 3.0
            }
            else if (imageSize <= 1065024 && width <= 5504 && height <= 3096)
            {
                return 5; // 3.1
            }
            else if (imageSize <= 2359296 && width <= 6144 && height <= 3456)
            {
                // 4.0 and 4.1 support the same image sizes.
                return 9; // 4.1
            }
            else if (imageSize <= 8912896 && width <= 8192 && height <= 4352)
            {
                // 5.0-5.3 support the same image sizes.
                // The AV1 specification states that 5.1 is the baseline profile
                // https://aomediacodec.github.io/av1-avif/#baseline-profile
                return 13; // 5.1
            }
            else
            {
                // If the image is larger than the defined profile values,
                // return the "Maximum parameters" value.
                return 31;
            }
        }

        private static class SequenceProfiles
        {
            /// <summary>
            /// Profile 0 - Main
            /// </summary>
            /// <remarks>
            /// 8 or 10 bits-per-channel, YUV 4:2:0 or 4:0:0.
            /// </remarks>
            public const byte Profile0 = 0;

            /// <summary>
            /// Profile 1 - High
            /// </summary>
            /// <remarks>
            /// 8 or 10 bits-per-channel, YUV 4:4:4
            /// </remarks>
            public const byte Profile1 = 1;

            /// <summary>
            /// Profile 2 - Professional
            /// </summary>
            /// <remarks>
            /// 8, 10 or 12 bits-per-channel, YUV 4:2:2
            /// </remarks>
            public const byte Profile2 = 2;
        }
    }
}
