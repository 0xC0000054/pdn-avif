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
using PaintDotNet.IndirectUI;
using PaintDotNet.PropertySystem;
using System;
using System.IO;

namespace AvifFileType
{
    [PluginSupportInfo(typeof(PluginSupportInfo))]
    public sealed class AvifFileTypePlugin
        : PropertyBasedFileType
    {
        private readonly int? maxEncoderThreadsOverride;
        private readonly IAvifStringResourceManager strings;

        // Names of the properties
        private enum PropertyNames
        {
            Quality,
            CompressionSpeed,
            YUVChromaSubsampling,
            ForumLink,
            GitHubLink
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AvifFileTypePlugin"/> class.
        /// </summary>
        public AvifFileTypePlugin()
            : this(null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AvifFileTypePlugin"/> class.
        /// </summary>
        /// <param name="host">The host.</param>
        public AvifFileTypePlugin(IFileTypeHost host)
            : this(host, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AvifFileTypePlugin"/> class.
        /// </summary>
        /// <param name="maxEncoderThreads">The maximum number of encoder threads.</param>
        public AvifFileTypePlugin(int maxEncoderThreads)
            : this(null, maxEncoderThreads)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AvifFileTypePlugin"/> class.
        /// </summary>
        /// <param name="host">The host.</param>
        /// <param name="maxEncoderThreads">The maximum number of encoder threads.</param>
        public AvifFileTypePlugin(IFileTypeHost host, int? maxEncoderThreads)
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
#if PDN_BUNDLED
            PaintDotNet.Avif.IAvifFileTypeStrings avifFileTypeStrings = host?.Services.GetService<PaintDotNet.Avif.IAvifFileTypeStrings>();

            if (avifFileTypeStrings != null)
            {
                this.strings = new PdnLocalizedStringResourceManager(avifFileTypeStrings);
            }
            else
#endif
            {
                this.strings = new BuiltinStringResourceManager();
            }
            this.maxEncoderThreadsOverride = maxEncoderThreads;
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
            int quality = token.GetProperty<Int32Property>(PropertyNames.Quality).Value;

            return quality == 100;
        }

        /// <summary>
        /// Add properties to the dialog
        /// </summary>
        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            Property[] props = new Property[]
            {
                new Int32Property(PropertyNames.Quality, 85, 0, 100, false),
                StaticListChoiceProperty.CreateForEnum(PropertyNames.CompressionSpeed, CompressionSpeed.Fast),
                CreateChromaSubsampling(),
                new UriProperty(PropertyNames.ForumLink, new Uri("https://forums.getpaint.net/topic/116233-avif-filetype")),
                new UriProperty(PropertyNames.GitHubLink, new Uri("https://github.com/0xC0000054/pdn-avif"))
            };

            return new PropertyCollection(props);

            StaticListChoiceProperty CreateChromaSubsampling()
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
            qualityPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = string.Empty;
            qualityPCI.ControlProperties[ControlInfoPropertyNames.Description].Value = this.strings.GetString("Quality_DisplayName");

            PropertyControlInfo compressionModePCI = configUI.FindControlForPropertyName(PropertyNames.CompressionSpeed);
            compressionModePCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = this.strings.GetString("CompressionSpeed_DisplayName");
            compressionModePCI.SetValueDisplayName(CompressionSpeed.Fast, this.strings.GetString("CompressionSpeed_Fast_DisplayName"));
            compressionModePCI.SetValueDisplayName(CompressionSpeed.Medium, this.strings.GetString("CompressionSpeed_Medium_DisplayName"));
            compressionModePCI.SetValueDisplayName(CompressionSpeed.Slow, this.strings.GetString("CompressionSpeed_Slow_DisplayName"));

            PropertyControlInfo subsamplingPCI = configUI.FindControlForPropertyName(PropertyNames.YUVChromaSubsampling);
            subsamplingPCI.ControlProperties[ControlInfoPropertyNames.DisplayName].Value = this.strings.GetString("ChromaSubsampling_DisplayName");
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling420, this.strings.GetString("ChromaSubsampling_420_DisplayName"));
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling422, this.strings.GetString("ChromaSubsampling_422_DisplayName"));
            subsamplingPCI.SetValueDisplayName(YUVChromaSubsampling.Subsampling444, this.strings.GetString("ChromaSubsampling_444_DisplayName"));

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

            AvifFile.Save(input, output, quality, compressionSpeed, chromaSubsampling, this.maxEncoderThreadsOverride, scratchSurface, progressCallback);
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
