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

using System;
using System.Collections.Generic;
using System.Linq;

namespace AvifFileType.AvifContainer
{
    internal static class ItemPropertyTypeMap
    {
        private static readonly IReadOnlyDictionary<Type, FourCC> propertyTypeMap = CreatePropertyTypeMap();

        public static FourCC GetPropertyType<TProperty>() where TProperty : class, IItemProperty
        {
            return propertyTypeMap[typeof(TProperty)];
        }

        private static IReadOnlyDictionary<Type, FourCC> CreatePropertyTypeMap()
        {
            Dictionary<Type, FourCC> map = new Dictionary<Type, FourCC>();

            Type itemPropertyType = typeof(ItemProperty);
            Type itemPropertyFullType = typeof(ItemPropertyFull);

            Type[] assemblyTypes = typeof(ItemProperty).Assembly.GetTypes();
            IEnumerable<Type> propertyTypes = from type in assemblyTypes
                                              where !type.IsAbstract && (type.IsSubclassOf(itemPropertyType) || type.IsSubclassOf(itemPropertyFullType))
                                              select type;

            foreach (Type derivedType in propertyTypes)
            {
                try
                {
                    IItemProperty property = (IItemProperty)Activator.CreateInstance(derivedType, nonPublic: true);

                    map.Add(derivedType, property.Type);
                }
                catch (MissingMethodException ex)
                {
                    throw new InvalidOperationException($"Unable to create an instance of { derivedType.Name }.", ex);
                }
            }

            return map;
        }
    }
}
