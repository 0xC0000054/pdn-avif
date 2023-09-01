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

namespace AvifFileType.AvifContainer
{
    internal abstract class StronglyTypedEnumeration<TEnum, TValue> :
        IEquatable<StronglyTypedEnumeration<TEnum, TValue>>
        where TEnum : StronglyTypedEnumeration<TEnum, TValue>
        where TValue : struct, IEquatable<TValue>
    {
        protected StronglyTypedEnumeration(TValue value, string name)
        {
            if (name is null)
            {
                ExceptionUtil.ThrowArgumentNullException(nameof(name));
            }

            this.Value = value;
            this.Name = name;
        }

        public string Name { get; }

        public TValue Value { get; }

        public override bool Equals(object? obj)
        {
            return obj is StronglyTypedEnumeration<TEnum, TValue> other && Equals(other);
        }

        public bool Equals(StronglyTypedEnumeration<TEnum, TValue>? other)
        {
            if (other is null)
            {
                return false;
            }

            return this.Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return this.Value.GetHashCode();
        }

        public override string ToString()
        {
            return this.Name;
        }

        public static bool operator ==(StronglyTypedEnumeration<TEnum, TValue>? left, StronglyTypedEnumeration<TEnum, TValue>? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Value.Equals(right.Value);
        }

        public static bool operator !=(StronglyTypedEnumeration<TEnum, TValue>? left, StronglyTypedEnumeration<TEnum, TValue>? right)
        {
            return !(left == right);
        }
    }
}
