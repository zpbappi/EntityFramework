// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Reflection;
using System.Linq;

// ReSharper disable once CheckNamespace

namespace System
{
    [DebuggerStepThrough]
    internal static class SharedTypeExtensions
    {
        private static readonly Type[] _primitivePropertyTypes =
            {
                typeof(bool),
                typeof(byte[]),
                typeof(char),
                typeof(DateTime),
                typeof(DateTimeOffset),
                typeof(decimal),
                typeof(double),
                typeof(float),
                typeof(Guid),
                typeof(string),
                typeof(TimeSpan)
            };

        public static Type UnwrapNullableType(this Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        public static bool IsNullableType(this Type type)
        {
            var typeInfo = type.GetTypeInfo();

            return !typeInfo.IsValueType
                   || (typeInfo.IsGenericType
                       && typeInfo.GetGenericTypeDefinition() == typeof(Nullable<>));
        }

        public static Type MakeNullable(this Type type)
        {
            if (type.IsNullableType())
            {
                return type;
            }

            return typeof(Nullable<>).MakeGenericType(type);
        }

        public static bool IsInteger(this Type type)
        {
            type = type.UnwrapNullableType();

            return type == typeof(int)
                   || type == typeof(long)
                   || type == typeof(short)
                   || type == typeof(byte)
                   || type == typeof(uint)
                   || type == typeof(ulong)
                   || type == typeof(ushort)
                   || type == typeof(sbyte);
        }
        
        public static bool IsPrimitiveType(this Type propertyType)
        {
            propertyType = propertyType.UnwrapNullableType();

            return propertyType.IsInteger()
                   || _primitivePropertyTypes.Contains(propertyType)
                   || propertyType.GetTypeInfo().IsEnum;
        }
    }
}
