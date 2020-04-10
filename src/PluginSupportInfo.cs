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

using PaintDotNet;
using System;
using System.Reflection;

namespace AvifFileType
{
    public sealed class PluginSupportInfo : IPluginSupportInfo
    {
        public string DisplayName => "AVIF FileType";

        public string Author => "null54";

        public string Copyright
        {
            get
            {
                object[] attributes = typeof(PluginSupportInfo).Assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);

                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public Version Version => typeof(PluginSupportInfo).Assembly.GetName().Version;

        public Uri WebsiteUri => new Uri(@"https://forums.getpaint.net");
    }
}
