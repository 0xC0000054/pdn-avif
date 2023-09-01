////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022, 2023 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal sealed class SequenceProfile
        : StronglyTypedEnumeration<SequenceProfile, byte>, IEquatable<SequenceProfile>
    {
        /// <summary>
        /// Main profile
        /// </summary>
        /// <remarks>
        /// 8 or 10 bits-per-channel, YUV 4:2:0 or 4:0:0.
        /// </remarks>
        public static readonly SequenceProfile Main = new SequenceProfile(0, "Main");

        /// <summary>
        /// High profile
        /// </summary>
        /// <remarks>
        /// 8 or 10 bits-per-channel, YUV 4:4:4
        /// </remarks>
        public static readonly SequenceProfile High = new SequenceProfile(1, "High");

        /// <summary>
        /// Professional profile
        /// </summary>
        /// <remarks>
        /// 8, 10 or 12 bits-per-channel, YUV 4:2:2
        /// </remarks>
        public static readonly SequenceProfile Professional = new SequenceProfile(2, "Professional");

        private SequenceProfile(byte value, string name)
            : base(value, name)
        {
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"{ this.Name } ({ this.Value })";

        /// <summary>
        /// Creates a <see cref="SequenceProfile" /> from the packed sequence profile and level.
        /// </summary>
        /// <param name="seqProfileAndSeqLevelIdx0">The packed sequence profile and level.</param>
        /// <returns>A <see cref="SequenceProfile" /> created from the packed sequence profile and level.</returns>
        public static SequenceProfile FromPackedByte(byte seqProfileAndSeqLevelIdx0)
        {
            byte value = (byte)((seqProfileAndSeqLevelIdx0 >> 5) & 0x07);

            if (SequenceProfileMap.Instance.TryGetValue(value, out SequenceProfile? profile))
            {
                return profile;
            }
            else
            {
                return new SequenceProfile(value, "Unknown");
            }
        }

        public override bool Equals(object? obj)
        {
            return obj is SequenceProfile sequenceProfile && Equals(sequenceProfile);
        }

        public bool Equals(SequenceProfile? other)
        {
            if (other is null)
            {
                return false;
            }

            return this.Value == other.Value;
        }

        public override int GetHashCode()
        {
            return this.Value.GetHashCode();
        }

        public static bool operator ==(SequenceProfile? left, SequenceProfile? right)
        {
            return EqualityComparer<SequenceProfile>.Default.Equals(left, right);
        }

        public static bool operator !=(SequenceProfile? left, SequenceProfile? right)
        {
            return !(left == right);
        }

        private static class SequenceProfileMap
        {
            public static IReadOnlyDictionary<byte, SequenceProfile> Instance { get; } = CreateSequenceProfileMap();

            private static IReadOnlyDictionary<byte, SequenceProfile> CreateSequenceProfileMap()
            {
                return new Dictionary<byte, SequenceProfile>
                {
                    { Main.Value, Main },
                    { High.Value, High },
                    { Professional.Value, Professional }
                };
            }
        }
    }
}
