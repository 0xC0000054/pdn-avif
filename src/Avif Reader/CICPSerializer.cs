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

using AvifFileType.AvifContainer;
using AvifFileType.Interop;
using System;
using System.Globalization;

namespace AvifFileType
{
    internal static class CICPSerializer
    {
        private const string ColorPrimariesPropertyName = "ColorPrimaries";
        private const string TransferCharacteristicsPropertyName = "TransferCharacteristics";
        private const string MatrixCoefficientsPropertyName = "MatrixCoefficients";

        public static CICPColorData? TryDeserialize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            // "Nclx" was the old item signature.
            if (!value.StartsWith("<CICP", StringComparison.Ordinal) &&
                !value.StartsWith("<Nclx", StringComparison.Ordinal))
            {
                return null;
            }

            ushort colorPrimaries = GetPropertyValue(value, ColorPrimariesPropertyName);
            ushort transferCharacteristics = GetPropertyValue(value, TransferCharacteristicsPropertyName);
            ushort matrixCoefficients = GetPropertyValue(value, MatrixCoefficientsPropertyName);
            // We always use the full RGB/YUV color range.
            const bool fullRange = true;

            return new CICPColorData
            {
                colorPrimaries = (CICPColorPrimaries)colorPrimaries,
                transferCharacteristics = (CICPTransferCharacteristics)transferCharacteristics,
                matrixCoefficients = (CICPMatrixCoefficients)matrixCoefficients,
                fullRange = fullRange
            };
        }

        public static string TrySerialize(CICPColorData cicpColor)
        {
            // The identity matrix coefficient is never serialized.
            if (cicpColor.matrixCoefficients == CICPMatrixCoefficients.Identity)
            {
                return null;
            }

            if (cicpColor.colorPrimaries == CICPColorPrimaries.Unspecified ||
                cicpColor.transferCharacteristics == CICPTransferCharacteristics.Unspecified ||
                cicpColor.matrixCoefficients == CICPMatrixCoefficients.Unspecified)
            {
                return null;
            }

            ushort colorPrimaries = (ushort)cicpColor.colorPrimaries;
            ushort transferCharacteristics = (ushort)cicpColor.transferCharacteristics;
            ushort matrixCoefficients = (ushort)cicpColor.matrixCoefficients;

            return string.Format(CultureInfo.InvariantCulture,
                                 "<CICP {0}=\"{1}\" {2}=\"{3}\" {4}=\"{5}\"/>",
                                 ColorPrimariesPropertyName,
                                 colorPrimaries.ToString(CultureInfo.InvariantCulture),
                                 TransferCharacteristicsPropertyName,
                                 transferCharacteristics.ToString(CultureInfo.InvariantCulture),
                                 MatrixCoefficientsPropertyName,
                                 matrixCoefficients.ToString(CultureInfo.InvariantCulture));
        }

        private static ushort GetPropertyValue(string haystack, string propertyName)
        {
            string needle = propertyName + "=\"";

            int valueStartIndex = haystack.IndexOf(needle, StringComparison.Ordinal) + needle.Length;
            int valueEndIndex = haystack.IndexOf('"', valueStartIndex);

            string propertyValue = haystack.Substring(valueStartIndex, valueEndIndex - valueStartIndex);

            return ushort.Parse(propertyValue, CultureInfo.InvariantCulture);
        }
    }
}
