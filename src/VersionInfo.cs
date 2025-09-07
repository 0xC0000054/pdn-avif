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

using System;
using System.Diagnostics;

namespace AvifFileType
{
    internal static class VersionInfo
    {
        private static readonly Lazy<string> aomVersion = new(GetAOMVersion);
        private static readonly Lazy<string> pluginVersion = new(GetPluginVersion);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string AOMVersion => aomVersion.Value;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string PluginVersion => pluginVersion.Value;

        private static string GetAOMVersion()
        {
            string version = AvifNative.GetAOMVersionString();

            // Remove the v prefix (if present) because the plugin adds its own.
            return version.StartsWith('v') ? version.Substring(1) : version;
        }

        private static string GetPluginVersion()
            => typeof(VersionInfo).Assembly.GetName().Version!.ToString();
    }
}
