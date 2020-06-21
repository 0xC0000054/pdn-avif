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

namespace AvifFileType.AvifContainer
{
    internal abstract class StronglyTypedEnumeration<T> where T : struct
    {
        protected StronglyTypedEnumeration(T value, string name)
        {
            this.Value = value;
            this.Name = name;
        }

        public string Name { get; }

        public T Value { get; }

        public abstract override bool Equals(object obj);

        public abstract override int GetHashCode();

        public override string ToString()
        {
            return this.Name;
        }
    }
}
