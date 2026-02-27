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

using PaintDotNet;
using PaintDotNet.ComponentModel;
using PaintDotNet.FileTypes;
using PaintDotNet.Imaging;
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System;
using System.Collections.Generic;

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
                host,
                "AV1 (AVIF)",
                FileTypeOptions.Create() with
                {
                    LoadExtensions = [".avif"],
                    SaveExtensions = [".avif"],
                    SupportsSavingLayers = false,
                    IsSavingConfigurable = true,
                    SupportsCancellationExceptions = true,
                })
        {
            PaintDotNet.FileTypes.Avif.IAvifFileTypeStrings? avifFileTypeStrings = host?.Services.GetService<PaintDotNet.FileTypes.Avif.IAvifFileTypeStrings>();

            if (avifFileTypeStrings != null)
            {
                this.strings = new PdnLocalizedStringResourceManager(avifFileTypeStrings);
            }
            else
            {
                this.strings = new BuiltinStringResourceManager();
            }
        }

        protected override PropertyBasedFileTypeSaver OnCreatePropertyBasedSaver()
        {
            return new Saver(this);
        }

        protected override PropertyBasedFileTypeLoader OnCreatePropertyBasedLoader()
        {
            return new Loader(this);
        }

        private sealed class Saver : PropertyBasedFileTypeSaver
        {
            private readonly AvifFileTypePlugin fileType;

            public Saver(AvifFileTypePlugin fileType)
                : base(fileType)
            {
                this.fileType = fileType;
            }

            /// <summary>
            /// Add properties to the dialog
            /// </summary>
            protected override PropertyCollection OnCreateDefaultSaveProperties()
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
            protected override ControlInfo OnCreateSaveOptionsUI(PropertyCollection props)
            {
                ControlInfo configUI = CreateDefaultSaveOptionsUI(props);

                PropertyControlInfo qualityPCI = configUI.FindControlForPropertyName(PropertyNames.Quality)!;
                qualityPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = this.fileType.strings.GetString("Quality_DisplayName");
                qualityPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = string.Empty;

                PropertyControlInfo losslessPCI = configUI.FindControlForPropertyName(PropertyNames.Lossless)!;
                losslessPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
                losslessPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.fileType.strings.GetString("Lossless_Description");

                PropertyControlInfo losslessAlphaPCI = configUI.FindControlForPropertyName(PropertyNames.LosslessAlphaCompression)!;
                losslessAlphaPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
                losslessAlphaPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.fileType.strings.GetString("LosslessAlphaCompression_Description");

                PropertyControlInfo encoderPresetPCI = configUI.FindControlForPropertyName(PropertyNames.EncoderPreset)!;
                encoderPresetPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = this.fileType.strings.GetString("EncoderPreset_DisplayName");
                encoderPresetPCI.SetValueDisplayName(EncoderPreset.Fast, this.fileType.strings.GetString("EncoderPreset_Fast_DisplayName")!);
                encoderPresetPCI.SetValueDisplayName(EncoderPreset.Medium, this.fileType.strings.GetString("EncoderPreset_Medium_DisplayName")!);
                encoderPresetPCI.SetValueDisplayName(EncoderPreset.Slow, this.fileType.strings.GetString("EncoderPreset_Slow_DisplayName")!);
                encoderPresetPCI.SetValueDisplayName(EncoderPreset.VerySlow, this.fileType.strings.GetString("EncoderPreset_VerySlow_DisplayName")!);

                PropertyControlInfo subsamplingPCI = configUI.FindControlForPropertyName(PropertyNames.YUVChromaSubsampling)!;
                subsamplingPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = this.fileType.strings.GetString("ChromaSubsampling_DisplayName");
                subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling420, this.fileType.strings.GetString("ChromaSubsampling_420_DisplayName")!);
                subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling422, this.fileType.strings.GetString("ChromaSubsampling_422_DisplayName")!);
                subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling444, this.fileType.strings.GetString("ChromaSubsampling_444_DisplayName")!);

                PropertyControlInfo preserveExistingTileSizePCI = configUI.FindControlForPropertyName(PropertyNames.PreserveExistingTileSize)!;
                preserveExistingTileSizePCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
                preserveExistingTileSizePCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.fileType.strings.GetString("PreserveExistingTileSize_Description");

                PropertyControlInfo premultipliedAlphaPCI = configUI.FindControlForPropertyName(PropertyNames.PremultipliedAlpha)!;
                premultipliedAlphaPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = string.Empty;
                premultipliedAlphaPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.fileType.strings.GetString("PremultipliedAlpha_Description");

                PropertyControlInfo forumLinkPCI = configUI.FindControlForPropertyName(PropertyNames.ForumLink)!;
                forumLinkPCI.ControlProperties[ControlInfoPropertyNames.DisplayName]!.Value = this.fileType.strings.GetString("ForumLink_DisplayName");
                forumLinkPCI.ControlProperties[ControlInfoPropertyNames.Description]!.Value = this.fileType.strings.GetString("ForumLink_Description");

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
            protected override void OnSave(IPropertyBasedFileTypeSaveContext context)
            {
                IPropertyBasedFileTypeSaveOptions options = context.Options;
                int quality = options.GetProperty<Int32Property>(PropertyNames.Quality)!.Value;
                bool lossless = options.GetProperty<BooleanProperty>(PropertyNames.Lossless)!.Value;
                bool losslessAlpha = options.GetProperty<BooleanProperty>(PropertyNames.LosslessAlphaCompression)!.Value;
                EncoderPreset encoderPreset = (EncoderPreset)options.GetProperty(PropertyNames.EncoderPreset)!.Value!;
                YUVChromaSubsampling chromaSubsampling = (YUVChromaSubsampling)options.GetProperty(PropertyNames.YUVChromaSubsampling)!.Value!;
                bool preserveExistingTileSize = options.GetProperty<BooleanProperty>(PropertyNames.PreserveExistingTileSize)!.Value;
                bool premultipliedAlpha = options.GetProperty<BooleanProperty>(PropertyNames.PremultipliedAlpha)!.Value;

                AvifSave.Save(context.Document,
                              context.Output,
                              quality,
                              lossless,
                              losslessAlpha,
                              encoderPreset,
                              chromaSubsampling,
                              preserveExistingTileSize,
                              premultipliedAlpha,
                              context.ProgressCallback);
            }
        }

        private sealed class Loader : PropertyBasedFileTypeLoader
        {
            public Loader(AvifFileTypePlugin fileType)
                : base(fileType)
            {
            }

            protected override IFileTypeDocument OnLoad(IPropertyBasedFileTypeLoadContext context)
            {
                IImagingFactory imagingFactory = this.Services.GetService<IImagingFactory>() ?? throw new ArgumentNullException(nameof(IImagingFactory));
                return AvifLoad.Load(context.Factory, context.Input, imagingFactory);
            }
        }
    }
}
