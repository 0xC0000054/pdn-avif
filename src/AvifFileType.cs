﻿////////////////////////////////////////////////////////////////////////
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

using PaintDotNet;
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
        private readonly IAvifStringResourceManager strings;

        // Names of the properties
        private enum PropertyNames
        {
            Quality,
            EncoderPreset,
            YUVChromaSubsampling,
            ForumLink,
            GitHubLink,
            PreserveExistingTileSize,
            PremultipliedAlpha,
            LosslessAlphaCompression,
            Lossless,
            PluginVersion,
            AOMVersion
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
            PaintDotNet.Avif.IAvifFileTypeStrings? avifFileTypeStrings = host?.Services.GetService<PaintDotNet.Avif.IAvifFileTypeStrings>();

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
            Property[] props =
            [
                new Int32Property(PropertyNames.Quality, 85, 0, 100, false),
                new BooleanProperty(PropertyNames.Lossless, false),
                new BooleanProperty(PropertyNames.LosslessAlphaCompression, true),
                StaticListChoiceProperty.CreateForEnum(PropertyNames.EncoderPreset, EncoderPreset.Fast),
                CreateChromaSubsampling(),
                new BooleanProperty(PropertyNames.PreserveExistingTileSize, true),
                new BooleanProperty(PropertyNames.PremultipliedAlpha, false),
                new UriProperty(PropertyNames.ForumLink, new Uri("https://forums.getpaint.net/topic/116233-avif-filetype")),
                new UriProperty(PropertyNames.GitHubLink, new Uri("https://github.com/0xC0000054/pdn-avif")),
                new StringProperty(PropertyNames.PluginVersion),
                new StringProperty(PropertyNames.AOMVersion),
            ];

            List<PropertyCollectionRule> rules =
            [
                new ReadOnlyBoundToBooleanRule(PropertyNames.Quality, PropertyNames.Lossless, false),
                new ReadOnlyBoundToBooleanRule(PropertyNames.LosslessAlphaCompression, PropertyNames.Lossless, false),
                new ReadOnlyBoundToBooleanRule(PropertyNames.YUVChromaSubsampling, PropertyNames.Lossless, false),
                new ReadOnlyBoundToBooleanRule(PropertyNames.PremultipliedAlpha, PropertyNames.Lossless, false)
            ];

            return new PropertyCollection(props, rules);

            static StaticListChoiceProperty CreateChromaSubsampling()
            {
                // The list is created manually because some of the YUVChromaSubsampling enumeration values
                // are used for internal signaling.

                object[] choiceValues =
                [
                    YUVChromaSubsampling.Subsampling420,
                    YUVChromaSubsampling.Subsampling422,
                    YUVChromaSubsampling.Subsampling444
                ];

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

            PropertyControlInfo qualityPCI = configUI.FindControlForPropertyName(PropertyNames.Quality)!;
            qualityPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = this.strings.GetString("Quality_DisplayName");
            qualityPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = string.Empty;

            PropertyControlInfo losslessPCI = configUI.FindControlForPropertyName(PropertyNames.Lossless)!;
            losslessPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            losslessPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.strings.GetString("Lossless_Description");

            PropertyControlInfo losslessAlphaPCI = configUI.FindControlForPropertyName(PropertyNames.LosslessAlphaCompression)!;
            losslessAlphaPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            losslessAlphaPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.strings.GetString("LosslessAlphaCompression_Description");

            PropertyControlInfo encoderPresetPCI = configUI.FindControlForPropertyName(PropertyNames.EncoderPreset)!;
            encoderPresetPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = this.strings.GetString("EncoderPreset_DisplayName");
            encoderPresetPCI.SetValueDisplayName(EncoderPreset.Fast, this.strings.GetString("EncoderPreset_Fast_DisplayName")!);
            encoderPresetPCI.SetValueDisplayName(EncoderPreset.Medium, this.strings.GetString("EncoderPreset_Medium_DisplayName")!);
            encoderPresetPCI.SetValueDisplayName(EncoderPreset.Slow, this.strings.GetString("EncoderPreset_Slow_DisplayName")!);
            encoderPresetPCI.SetValueDisplayName(EncoderPreset.VerySlow, this.strings.GetString("EncoderPreset_VerySlow_DisplayName")!);

            PropertyControlInfo subsamplingPCI = configUI.FindControlForPropertyName(PropertyNames.YUVChromaSubsampling)!;
            subsamplingPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = this.strings.GetString("ChromaSubsampling_DisplayName");
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling420, this.strings.GetString("ChromaSubsampling_420_DisplayName")!);
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling422, this.strings.GetString("ChromaSubsampling_422_DisplayName")!);
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling444, this.strings.GetString("ChromaSubsampling_444_DisplayName")!);

            PropertyControlInfo preserveExistingTileSizePCI = configUI.FindControlForPropertyName(PropertyNames.PreserveExistingTileSize)!;
            preserveExistingTileSizePCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            preserveExistingTileSizePCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.strings.GetString("PreserveExistingTileSize_Description");

            PropertyControlInfo premultipliedAlphaPCI = configUI.FindControlForPropertyName(PropertyNames.PremultipliedAlpha)!;
            premultipliedAlphaPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            premultipliedAlphaPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.strings.GetString("PremultipliedAlpha_Description");

            PropertyControlInfo forumLinkPCI = configUI.FindControlForPropertyName(PropertyNames.ForumLink)!;
            forumLinkPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = this.strings.GetString("ForumLink_DisplayName");
            forumLinkPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.strings.GetString("ForumLink_Description");

            PropertyControlInfo githubLinkPCI = configUI.FindControlForPropertyName(PropertyNames.GitHubLink)!;
            githubLinkPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            githubLinkPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = "GitHub"; // GitHub is a brand name that should not be localized.

            PropertyControlInfo pluginVersionPCI = configUI.FindControlForPropertyName(PropertyNames.PluginVersion)!;
            pluginVersionPCI.ControlType.Value = PropertyControlType.Label;
            pluginVersionPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            pluginVersionPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = "AvifFileType v" + VersionInfo.PluginVersion;

            PropertyControlInfo aomVersionPCI = configUI.FindControlForPropertyName(PropertyNames.AOMVersion)!;
            aomVersionPCI.ControlType.Value = PropertyControlType.Label;
            aomVersionPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
            aomVersionPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = "AOM v" + VersionInfo.AOMVersion;

            return configUI;
        }

        /// <summary>
        /// Saves a document to a stream respecting the properties
        /// </summary>
        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            int quality = token.GetProperty<Int32Property>(PropertyNames.Quality)!.Value;
            bool lossless = token.GetProperty<BooleanProperty>(PropertyNames.Lossless)!.Value;
            bool losslessAlpha = token.GetProperty<BooleanProperty>(PropertyNames.LosslessAlphaCompression)!.Value;
            EncoderPreset encoderPreset = (EncoderPreset)token.GetProperty(PropertyNames.EncoderPreset)!.Value!;
            YUVChromaSubsampling chromaSubsampling = (YUVChromaSubsampling)token.GetProperty(PropertyNames.YUVChromaSubsampling)!.Value!;
            bool preserveExistingTileSize = token.GetProperty<BooleanProperty>(PropertyNames.PreserveExistingTileSize)!.Value;
            bool premultipliedAlpha = token.GetProperty<BooleanProperty>(PropertyNames.PremultipliedAlpha)!.Value;

            AvifSave.Save(input,
                          output,
                          quality,
                          lossless,
                          losslessAlpha,
                          encoderPreset,
                          chromaSubsampling,
                          preserveExistingTileSize,
                          premultipliedAlpha,
                          scratchSurface,
                          progressCallback);
        }

        /// <summary>
        /// Creates a document from a stream
        /// </summary>
        protected override Document OnLoad(Stream input)
        {
            return AvifLoad.Load(input);
        }
    }
}
