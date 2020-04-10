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

using AvifFileType.Properties;
using System;
using System.Globalization;
using System.Reflection;

namespace AvifFileType
{
    internal static class VersionUtil
    {
        public static void CheckForBetaExpiration()
        {
            if (!IsBetaVersion)
            {
                return;
            }

            const int BetaExpirationInDays = 90;

            DateTime buildDateUTC = DateTime.Parse(Resources.BuildDate, CultureInfo.InvariantCulture);

            DateTime expirationDateUTC = buildDateUTC.AddDays(BetaExpirationInDays);

            if (DateTime.UtcNow > expirationDateUTC)
            {
                throw new NotSupportedException(Resources.BetaVersionExpired);
            }
        }

        public static bool IsBetaVersion
        {
            get
            {
                AssemblyConfigurationAttribute attribute = typeof(VersionUtil).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>();

                if (attribute != null)
                {
                    return string.Equals(attribute.Configuration, "Beta", StringComparison.Ordinal);
                }

                return false;
            }
        }
    }
}
