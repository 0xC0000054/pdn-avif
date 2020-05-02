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
using System;
using System.Globalization;

namespace AvifFileType
{
    internal static class NclxSerializer
    {
        private const string ColorPrimariesPropertyName = "ColorPrimaries";
        private const string TransferCharacteristicsPropertyName = "TransferCharacteristics";
        private const string MatrixCoefficientsPropertyName = "MatrixCoefficients";

        public static NclxColorInformation TryDeserialize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (!value.StartsWith("<Nclx", StringComparison.Ordinal))
            {
                return null;
            }

            ushort colorPrimaries = GetPropertyValue(value, ColorPrimariesPropertyName);
            ushort transferCharacteristics = GetPropertyValue(value, TransferCharacteristicsPropertyName);
            ushort matrixCoefficients = GetPropertyValue(value, MatrixCoefficientsPropertyName);
            // We always use the full RGB/YUV color range.
            const bool fullRange = true;

            return new NclxColorInformation((NclxColorPrimaries)colorPrimaries,
                                            (NclxTransferCharacteristics)transferCharacteristics,
                                            (NclxMatrixCoefficients)matrixCoefficients,
                                            fullRange);
        }

        public static string TrySerialize(NclxColorInformation nclxColor)
        {
            if (nclxColor is null)
            {
                return null;
            }

            // The identity matrix coefficient is never serialized.
            if (nclxColor.MatrixCoefficients == NclxMatrixCoefficients.Identity)
            {
                return null;
            }

            if (nclxColor.ColorPrimaries == NclxColorPrimaries.Unspecified ||
                nclxColor.TransferCharacteristics == NclxTransferCharacteristics.Unspecified ||
                nclxColor.MatrixCoefficients == NclxMatrixCoefficients.Unspecified)
            {
                return null;
            }

            ushort colorPrimaries = (ushort)nclxColor.ColorPrimaries;
            ushort transferCharacteristics = (ushort)nclxColor.TransferCharacteristics;
            ushort matrixCoefficients = (ushort)nclxColor.MatrixCoefficients;

            return string.Format(CultureInfo.InvariantCulture,
                                 "<Nclx {0}=\"{1}\" {2}=\"{3}\" {4}=\"{5}\"/>",
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
