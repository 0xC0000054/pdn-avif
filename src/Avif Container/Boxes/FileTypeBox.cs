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
using System.Collections.Generic;

namespace AvifFileType.AvifContainer
{
    internal sealed class FileTypeBox
        : Box
    {
        private readonly FourCC majorBrand;
        private readonly uint minorVersion;
        private readonly IReadOnlyList<FourCC> compatibleBrands;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileTypeBox"/> class.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="header">The header.</param>
        /// <exception cref="FormatException">
        /// The compatible brand size must be a multiple of 4.
        /// </exception>
        public FileTypeBox(in EndianBinaryReaderSegment reader, Box header)
            : base(header)
        {
            this.majorBrand = reader.ReadFourCC();
            this.minorVersion = reader.ReadUInt32();

            long compatibleBrandSize = reader.EndOffset - reader.Position;
            if ((compatibleBrandSize & 3) != 0)
            {
                ExceptionUtil.ThrowFormatException($"The compatible brand size must be a multiple of 4, actual value: { compatibleBrandSize }");
            }

            long compatibleBrandCount = compatibleBrandSize / 4;
            List<FourCC> brands = new List<FourCC>(checked((int)compatibleBrandCount));

            for (int i = 0; i < brands.Capacity; i++)
            {
                brands.Add(reader.ReadFourCC());
            }

            this.compatibleBrands = brands;
        }

        public FileTypeBox(YUVChromaSubsampling chromaSubsampling)
            : base(BoxTypes.FileType)
        {
            this.majorBrand = AvifBrands.AVIF;
            this.minorVersion = 0;
            List<FourCC> compatibleBrands = new List<FourCC>
            {
                AvifBrands.AVIF,
                AvifBrands.MIF1,
                AvifBrands.MIAF
            };

            switch (chromaSubsampling)
            {
                case YUVChromaSubsampling.Subsampling400:
                case YUVChromaSubsampling.Subsampling420:
                    compatibleBrands.Add(AvifBrands.MA1B);
                    break;
                case YUVChromaSubsampling.Subsampling444:
                    compatibleBrands.Add(AvifBrands.MA1A);
                    break;
            }
            this.compatibleBrands = compatibleBrands;
        }

        /// <summary>
        /// Checks for AVIF format compatibility.
        /// </summary>
        /// <exception cref="FormatException">The file is not AVIF compatible.</exception>
        public void CheckForAvifCompatibility()
        {
            if (this.majorBrand != AvifBrands.AVIF)
            {
                if (this.majorBrand == AvifBrands.AVIS)
                {
                    ExceptionUtil.ThrowFormatException("Animated AVIF images are not supported.");
                }
                else
                {
                    bool isCompatible = false;
                    bool isImageSequence = false;

                    for (int i = 0; i < this.compatibleBrands.Count; i++)
                    {
                        FourCC brand = this.compatibleBrands[i];

                        if (brand == AvifBrands.AVIF)
                        {
                            isCompatible = true;
                        }
                        else if (brand == AvifBrands.AVIS)
                        {
                            isImageSequence = true;
                        }
                    }

                    if (isImageSequence)
                    {
                        ExceptionUtil.ThrowFormatException("Animated AVIF images are not supported.");
                    }
                    else if (!isCompatible)
                    {
                        ExceptionUtil.ThrowFormatException("The file is not AVIF compatible.");
                    }
                }
            }
        }

        public override void Write(BigEndianBinaryWriter writer)
        {
            base.Write(writer);

            writer.Write(this.majorBrand);
            writer.Write(this.minorVersion);
            for (int i = 0; i < this.compatibleBrands.Count; i++)
            {
                writer.Write(this.compatibleBrands[i]);
            }
        }

        protected override ulong GetTotalBoxSize()
        {
            return base.GetTotalBoxSize() + FourCC.SizeOf + sizeof(uint) + ((ulong)this.compatibleBrands.Count * FourCC.SizeOf);
        }
    }
}
