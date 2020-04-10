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
using PaintDotNet;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System.IO;

namespace AvifFileType
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    internal sealed class AvifFileTypePlugin : PropertyBasedFileType
    {
        // Names of the properties
        private enum PropertyNames
        {
            Quality,
            CompressionMode,
            YUVChromaSubsampling
        }

        /// <summary>
        /// Constructs a ExamplePropertyBasedFileType instance
        /// </summary>
        internal AvifFileTypePlugin()
            : base(
                "AVIF",
                new FileTypeOptions
                {
                    LoadExtensions = new string[] { ".avif" },
                    SaveExtensions = new string[] { ".avif" },
                    SupportsCancellation = true,
                    SupportsLayers = false
                })
        {
            VersionUtil.CheckForBetaExpiration();
        }

        /// <summary>
        /// Determines if the document was saved without altering the pixel values.
        ///
        /// Any settings that change the pixel values should return 'false'.
        ///
        /// Because Paint.NET prompts the user to flatten the image, flattening should not be
        /// considered.
        /// For example, a 32-bit PNG will return 'true' even if the document has multiple layers.
        /// </summary>
        protected override bool IsReflexive(PropertyBasedSaveConfigToken token)
        {
            // NOTE: The YUV444 conversion should be lossless with the default settings,
            // but if the image has a color profile its values will be used for the YUV conversion.

            return false;
        }

        /// <summary>
        /// Add properties to the dialog
        /// </summary>
        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            Property[] props = new Property[]
            {
                new Int32Property(PropertyNames.Quality, 85, 0, 100, false),
                StaticListChoiceProperty.CreateForEnum(PropertyNames.CompressionMode, CompressionMode.Normal),
                StaticListChoiceProperty.CreateForEnum(PropertyNames.YUVChromaSubsampling, YUVChromaSubsampling.Subsampling422)
            };

            return new PropertyCollection(props);
        }

        /// <summary>
        /// Adapt properties in the dialog (DisplayName, ...)
        /// </summary>
        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultSaveConfigUI(props);

            PropertyControlInfo qualityPCI = configUI.FindControlForPropertyName(PropertyNames.Quality);
            qualityPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            qualityPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = Resources.Quality_DisplayName;

            PropertyControlInfo compressionModePCI = configUI.FindControlForPropertyName(PropertyNames.CompressionMode);
            compressionModePCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = Resources.CompressionMode_DisplayName;
            compressionModePCI.SetValueDisplayName(CompressionMode.Fast, Resources.CompressionMode_Fast_DisplayName);
            compressionModePCI.SetValueDisplayName(CompressionMode.Normal, Resources.CompressionMode_Normal_DisplayName);
            compressionModePCI.SetValueDisplayName(CompressionMode.Slow, Resources.CompressionMode_Slow_DisplayName);

            PropertyControlInfo subsamplingPCI = configUI.FindControlForPropertyName(PropertyNames.YUVChromaSubsampling);
            subsamplingPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = Resources.ChromaSubsampling_DisplayName;
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling420, Resources.ChromaSubsampling_420_DisplayName);
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling422, Resources.ChromaSubsampling_422_DisplayName);
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling444, Resources.ChromaSubsampling_444_DisplayName);

            return configUI;
        }

        /// <summary>
        /// Saves a document to a stream respecting the properties
        /// </summary>
        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            int quality = token.GetProperty<Int32Property>(PropertyNames.Quality).Value;
            CompressionMode compressionMode = (CompressionMode)token.GetProperty(PropertyNames.CompressionMode).Value;
            YUVChromaSubsampling chromaSubsampling = (YUVChromaSubsampling)token.GetProperty(PropertyNames.YUVChromaSubsampling).Value;

            AvifFile.Save(input, output, quality, compressionMode, chromaSubsampling, scratchSurface, progressCallback);
        }

        /// <summary>
        /// Creates a document from a stream
        /// </summary>
        protected override Document OnLoad(Stream input)
        {
            return AvifFile.Load(input);
        }
    }
}
