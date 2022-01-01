////////////////////////////////////////////////////////////////////////
//
// This file is part of pdn-avif, a FileType plugin for Paint.NET
// that loads and saves AVIF images.
//
// Copyright (c) 2020, 2021, 2022 Nicholas Hayes
//
// This file is licensed under the MIT License.
// See LICENSE.txt for complete licensing and attribution information.
//
////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace AvifFileType.AvifContainer
{
    [DebuggerDisplay("{DebuggerDisplay, nq}")]
    internal sealed class SequenceLevel
        : StronglyTypedEnumeration<SequenceLevel, byte>, IEquatable<SequenceLevel>
    {
        // These values are from the Annex A.3 table: https://aomediacodec.github.io/av1-spec/av1-spec.pdf

        public static readonly SequenceLevel TwoPointZero = new SequenceLevel(0, "2.0");
        public static readonly SequenceLevel TwoPointOne = new SequenceLevel(1, "2.1");
        public static readonly SequenceLevel TwoPointTwo = new SequenceLevel(2, "2.2");
        public static readonly SequenceLevel TwoPointThree = new SequenceLevel(3, "2.3");

        public static readonly SequenceLevel ThreePointZero = new SequenceLevel(4, "3.0");
        public static readonly SequenceLevel ThreePointOne = new SequenceLevel(5, "3.1");
        public static readonly SequenceLevel ThreePointTwo = new SequenceLevel(6, "3.2");
        public static readonly SequenceLevel ThreePointThree = new SequenceLevel(7, "3.3");

        public static readonly SequenceLevel FourPointZero = new SequenceLevel(8, "4.0");
        public static readonly SequenceLevel FourPointOne = new SequenceLevel(9, "4.1");
        public static readonly SequenceLevel FourPointTwo = new SequenceLevel(10, "4.2");
        public static readonly SequenceLevel FourPointThree = new SequenceLevel(11, "4.3");

        public static readonly SequenceLevel FivePointZero = new SequenceLevel(12, "5.0");
        public static readonly SequenceLevel FivePointOne = new SequenceLevel(13, "5.1");
        public static readonly SequenceLevel FivePointTwo = new SequenceLevel(14, "5.2");
        public static readonly SequenceLevel FivePointThree = new SequenceLevel(15, "5.3");

        public static readonly SequenceLevel SixPointZero = new SequenceLevel(16, "6.0");
        public static readonly SequenceLevel SixPointOne = new SequenceLevel(17, "6.1");
        public static readonly SequenceLevel SixPointTwo = new SequenceLevel(18, "6.2");
        public static readonly SequenceLevel SixPointThree = new SequenceLevel(19, "6.3");

        public static readonly SequenceLevel SevenPointZero = new SequenceLevel(20, "7.0");
        public static readonly SequenceLevel SevenPointOne = new SequenceLevel(21, "7.1");
        public static readonly SequenceLevel SevenPointTwo = new SequenceLevel(22, "7.2");
        public static readonly SequenceLevel SevenPointThree = new SequenceLevel(23, "7.3");

        public static readonly SequenceLevel MaximumParameters = new SequenceLevel(31, "Maximum Parameters");

        private SequenceLevel(byte value, string name) : base(value, name)
        {
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"{ this.Name } ({ this.Value })";

        /// <summary>
        /// Creates a <see cref="SequenceLevel" /> from the packed sequence profile and level.
        /// </summary>
        /// <param name="seqProfileAndSeqLevelIdx0">The packed sequence profile and level.</param>
        /// <returns>A <see cref="SequenceLevel" /> created from the packed sequence profile and level.</returns>
        public static SequenceLevel FromPackedByte(byte seqProfileAndSeqLevelIdx0)
        {
            byte value = (byte)(seqProfileAndSeqLevelIdx0 & 0x1f);

            if (SequenceLevelMap.Instance.TryGetValue(value, out SequenceLevel level))
            {
                return level;
            }
            else
            {
                // From AV1 specification, https://aomediacodec.github.io/av1-spec/av1-spec.pdf:
                // "The level uses a X.Y format. X is equal to 2 + (seq_level_idx >> 2). Y is given by (seq_level_idx & 3)."
                int majorVersion = 2 + (value >> 2);
                int minorVersion = value & 3;

                string name = majorVersion.ToString(CultureInfo.InvariantCulture) + "." + minorVersion.ToString(CultureInfo.InvariantCulture);

                return new SequenceLevel(value, name);
            }
        }

        public override bool Equals(object obj)
        {
            return obj is SequenceLevel sequenceLevel && Equals(sequenceLevel);
        }

        public bool Equals(SequenceLevel other)
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

        public static bool operator ==(SequenceLevel left, SequenceLevel right)
        {
            return EqualityComparer<SequenceLevel>.Default.Equals(left, right);
        }

        public static bool operator !=(SequenceLevel left, SequenceLevel right)
        {
            return !(left == right);
        }

        private static class SequenceLevelMap
        {
            public static IReadOnlyDictionary<byte, SequenceLevel> Instance { get; } = CreateSequenceLevelMap();

            private static IReadOnlyDictionary<byte, SequenceLevel> CreateSequenceLevelMap()
            {
                FieldInfo[] fieldInfos = typeof(SequenceLevel).GetFields(BindingFlags.Public | BindingFlags.Static);

                Dictionary<byte, SequenceLevel> map = new Dictionary<byte, SequenceLevel>(fieldInfos.Length);

                for (int i = 0; i < fieldInfos.Length; i++)
                {
                    FieldInfo fieldInfo = fieldInfos[i];
                    if (fieldInfo.FieldType == typeof(SequenceLevel))
                    {
                        SequenceLevel item = (SequenceLevel)fieldInfo.GetValue(null);

                        map.Add(item.Value, item);
                    }
                }

                return map;
            }
        }
    }
}
