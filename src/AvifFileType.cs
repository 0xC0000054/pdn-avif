////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System;
using System.Collections.Generic;
using System.IO;

namespace AvifFileType
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class AvifFileTypePlugin
        : PropertyBasedFileType
    {
        private readonly IArrayPoolService arrayPoolService;
        private readonly IAvifStringResourceManager strings;

        // Names of the properties
        private enum PropertyNames
        {
            Quality,
            CompressionSpeed,
            YUVChromaSubsampling,
            ForumLink,
            GitHubLink,
            PreserveExistingTileSize,
            PremultipliedAlpha,
            LosslessAlphaCompression
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AvifFileTypePlugin"/> class.
        /// </summary>
        /// <param name="host">The host.</param>
        public AvifFileTypePlugin(IFileTypeHost host)
             : base(
                "AV1 (AVIF)",
                new FileTypeOptions
                {
                    LoadExtensions = new string[] { ".avif" },
                    SaveExtensions = new string[] { ".avif" },
                    SupportsCancellation = true,
                    SupportsLayers = false
                })
        {
            this.arrayPoolService = host?.Services.GetService<IArrayPoolService>();
            PaintDotNet.Avif.IAvifFileTypeStrings avifFileTypeStrings = host?.Services.GetService<PaintDotNet.Avif.IAvifFileTypeStrings>();

            if (avifFileTypeStrings != null)
            {
                this.strings = new PdnLocalizedStringResourceManager(avifFileTypeStrings);
            }
            else
            {
                this.strings = new BuiltinStringResourceManager();
            }
        }

        /// <summary>
        /// Add properties to the dialog
        /// </summary>
        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            Property[] props = new Property[]
            {
                new Int32Property(PropertyNames.Quality, 85, 0, 100, false),
                new BooleanProperty(PropertyNames.LosslessAlphaCompression, true),
                StaticListChoiceProperty.CreateForEnum(PropertyNames.CompressionSpeed, CompressionSpeed.Fast),
                CreateChromaSubsampling(),
                new BooleanProperty(PropertyNames.PreserveExistingTileSize, true),
                new BooleanProperty(PropertyNames.PremultipliedAlpha, false),
                new UriProperty(PropertyNames.ForumLink, new Uri("https://forums.getpaint.net/topic/116233-avif-filetype")),
                new UriProperty(PropertyNames.GitHubLink, new Uri("https://github.com/0xC0000054/pdn-avif"))
            };

            List<PropertyCollectionRule> rules = new List<PropertyCollectionRule>
            {
                new ReadOnlyBoundToValueRule<int, Int32Property>(PropertyNames.PremultipliedAlpha,
                                                                 PropertyNames.Quality,
                                                                 100,
                                                                 false),
                new ReadOnlyBoundToValueRule<int, Int32Property>(PropertyNames.YUVChromaSubsampling,
                                                                 PropertyNames.Quality,
                                                                 100,
                                                                 false),
                new ReadOnlyBoundToValueRule<int, Int32Property>(PropertyNames.LosslessAlphaCompression,
                                                                 PropertyNames.Quality,
                                                                 100,
                                                                 false)
            };

            return new PropertyCollection(props, rules);

            static StaticListChoiceProperty CreateChromaSubsampling()
            {
                // The list is created manually because some of the YUVChromaSubsampling enumeration values
                // are used for internal signaling.

                object[] choiceValues = new object[]
                {
                    YUVChromaSubsampling.Subsampling420,
                    YUVChromaSubsampling.Subsampling422,
                    YUVChromaSubsampling.Subsampling444
                };

                int defaultChoiceIndex = Array.IndexOf(choiceValues, YUVChromaSubsampling.Subsampling422);

                return new StaticListChoiceProperty(PropertyNames.YUVChromaSubsampling, choiceValues, defaultChoiceIndex);
            }
        }

        /// <summary>
        /// Adapt properties in the dialog (DisplayName, ...)
        /// </summary>
        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultSaveConfigUI(props);

            PropertyControlInfo qualityPCI = configUI.FindControlForPropertyName(PropertyNames.Quality);
            qualityPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = this.strings.GetString("Quality_DisplayName");
            qualityPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = string.Empty;

            PropertyControlInfo losslessAlphaPCI = configUI.FindControlForPropertyName(PropertyNames.LosslessAlphaCompression);
            losslessAlphaPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            losslessAlphaPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = this.strings.GetString("LosslessAlphaCompression_Description");

            PropertyControlInfo compressionSpeedPCI = configUI.FindControlForPropertyName(PropertyNames.CompressionSpeed);
            compressionSpeedPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = this.strings.GetString("CompressionSpeed_DisplayName");
            compressionSpeedPCI.SetValueDisplayName(CompressionSpeed.Fast, this.strings.GetString("CompressionSpeed_Fast_DisplayName"));
            compressionSpeedPCI.SetValueDisplayName(CompressionSpeed.Medium, this.strings.GetString("CompressionSpeed_Medium_DisplayName"));
            compressionSpeedPCI.SetValueDisplayName(CompressionSpeed.Slow, this.strings.GetString("CompressionSpeed_Slow_DisplayName"));
            compressionSpeedPCI.SetValueDisplayName(CompressionSpeed.VerySlow, this.strings.GetString("CompressionSpeed_VerySlow_DisplayName"));

            PropertyControlInfo subsamplingPCI = configUI.FindControlForPropertyName(PropertyNames.YUVChromaSubsampling);
            subsamplingPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = this.strings.GetString("ChromaSubsampling_DisplayName");
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling420, this.strings.GetString("ChromaSubsampling_420_DisplayName"));
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling422, this.strings.GetString("ChromaSubsampling_422_DisplayName"));
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling444, this.strings.GetString("ChromaSubsampling_444_DisplayName"));

            PropertyControlInfo preserveExistingTileSizePCI = configUI.FindControlForPropertyName(PropertyNames.PreserveExistingTileSize);
            preserveExistingTileSizePCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            preserveExistingTileSizePCI.ControlProperties[ControlInfoPropertyNames.Description].Value = this.strings.GetString("PreserveExistingTileSize_Description");

            PropertyControlInfo premultipliedAlphaPCI = configUI.FindControlForPropertyName(PropertyNames.PremultipliedAlpha);
            premultipliedAlphaPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            premultipliedAlphaPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = this.strings.GetString("PremultipliedAlpha_Description");

            PropertyControlInfo forumLinkPCI = configUI.FindControlForPropertyName(PropertyNames.ForumLink);
            forumLinkPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = this.strings.GetString("ForumLink_DisplayName");
            forumLinkPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = this.strings.GetString("ForumLink_Description");

            PropertyControlInfo githubLinkPCI = configUI.FindControlForPropertyName(PropertyNames.GitHubLink);
            githubLinkPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            githubLinkPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = "GitHub"; // GitHub is a brand name that should not be localized.

            return configUI;
        }

        /// <summary>
        /// Saves a document to a stream respecting the properties
        /// </summary>
        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            int quality = token.GetProperty<Int32Property>(PropertyNames.Quality).Value;
            CompressionSpeed compressionSpeed = (CompressionSpeed)token.GetProperty(PropertyNames.CompressionSpeed).Value;
            YUVChromaSubsampling chromaSubsampling = (YUVChromaSubsampling)token.GetProperty(PropertyNames.YUVChromaSubsampling).Value;
            bool preserveExistingTileSize = token.GetProperty<BooleanProperty>(PropertyNames.PreserveExistingTileSize).Value;

            // The premultiplied alpha conversion can cause the colors to drift, so it is disabled for lossless encoding.
            bool premultipliedAlpha = token.GetProperty<BooleanProperty>(PropertyNames.PremultipliedAlpha).Value && quality < 100;
            bool losslessAlpha = token.GetProperty<BooleanProperty>(PropertyNames.LosslessAlphaCompression).Value;

            AvifFile.Save(input,
                          output,
                          quality,
                          losslessAlpha,
                          compressionSpeed,
                          chromaSubsampling,
                          preserveExistingTileSize,
                          premultipliedAlpha,
                          scratchSurface,
                          progressCallback,
                          this.arrayPoolService);
        }

        /// <summary>
        /// Creates a document from a stream
        /// </summary>
        protected override Document OnLoad(Stream input)
        {
            return AvifFile.Load(input, this.arrayPoolService);
        }
    }
}
