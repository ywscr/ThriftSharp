﻿// Copyright (c) 2014-15 Solal Pirelli
// This code is licensed under the MIT License (see Licence.txt for details).

using System.Collections.Generic;
using System.Reflection;

namespace ThriftSharp.Internals
{
    /// <summary>
    /// Thrift field.
    /// </summary>
    internal sealed class ThriftField
    {
        /// <summary>
        /// Gets the field's ID.
        /// </summary>
        public readonly short Id;

        /// <summary>
        /// Gets the field's name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets the field's type.
        /// </summary>
        public readonly ThriftType WireType;

        /// <summary>
        /// Gets a value indicating whether the field is required.
        /// </summary>
        public readonly bool IsRequired;

        /// <summary>
        /// Gets the field's default value, if any.
        /// </summary>
        public readonly object DefaultValue;

        /// <summary>
        /// Gets the converter associated with the field, if any.
        /// </summary>
        public readonly object Converter;

        /// <summary>
        /// Gets the property associated with the field, if any.
        /// </summary>
        public readonly PropertyInfo BackingProperty;


        public ThriftField( short id, string name, bool isRequired, object defaultValue, object converter, PropertyInfo backingProperty )
        {
            Id = id;
            Name = name;
            WireType = ThriftType.Get( backingProperty.PropertyType, converter );
            IsRequired = isRequired;
            DefaultValue = defaultValue;
            Converter = converter;
            BackingProperty = backingProperty;
        }
    }

    /// <summary>
    /// Thrift struct.
    /// </summary>
    internal sealed class ThriftStruct
    {
        /// <summary>
        /// Gets the struct's header.
        /// </summary>
        public readonly ThriftStructHeader Header;

        /// <summary>
        /// Gets the struct's fields.
        /// </summary>
        public readonly IReadOnlyList<ThriftField> Fields;

        /// <summary>
        /// Gets the struct's underlying TypeInfo.
        /// </summary>
        public readonly TypeInfo TypeInfo;


        /// <summary>
        /// Initializes a new instance of the ThriftStructHeader class with the specified values.
        /// </summary>
        public ThriftStruct( ThriftStructHeader header, IReadOnlyList<ThriftField> fields, TypeInfo typeInfo )
        {
            Header = header;
            Fields = fields;
            TypeInfo = typeInfo;
        }
    }

    /// <summary>
    /// Thrift method "throws" clause.
    /// </summary>
    internal sealed class ThriftThrowsClause
    {
        /// <summary>
        /// Gets the clause's ID.
        /// </summary>
        public readonly short Id;

        /// <summary>
        /// Gets the clause's name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets the field's type.
        /// </summary>
        public readonly ThriftType Type;


        /// <summary>
        /// Initializes a new instance of the ThriftThrowsClause class with the specified values.
        /// </summary>
        public ThriftThrowsClause( short id, string name, TypeInfo typeInfo )
        {
            Id = id;
            Name = name;
            Type = ThriftType.Get( typeInfo.AsType(), null );
        }
    }

    /// <summary>
    /// Thrift method parameter.
    /// </summary>
    internal sealed class ThriftParameter
    {
        /// <summary>
        /// Gets the parameter's ID.
        /// </summary>
        public readonly short Id;

        /// <summary>
        /// Gets the parameter's name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets the field's type.
        /// </summary>
        public readonly ThriftType WireType;

        /// <summary>
        /// Gets the parameter's underlying TypeInfo.
        /// </summary>
        public readonly TypeInfo UnderlyingTypeInfo;

        /// <summary>
        /// Gets the parameter's converter, if any.
        /// </summary>
        public readonly object Converter;


        /// <summary>
        /// Initializes a new instance of the ThriftMethodParameter class with the specified values.
        /// </summary>
        public ThriftParameter( short id, string name, TypeInfo typeInfo, object converter )
        {
            Id = id;
            Name = name;
            WireType = ThriftType.Get( typeInfo.AsType(), converter );
            UnderlyingTypeInfo = typeInfo;
            Converter = converter;
        }
    }

    /// <summary>
    /// Thrift method return value.
    /// </summary>
    internal sealed class ThriftReturnValue
    {
        /// <summary>
        /// Gets the field's type.
        /// </summary>
        public readonly ThriftType WireType;

        /// <summary>
        /// Gets the parameter's underlying TypeInfo.
        /// </summary>
        public readonly TypeInfo UnderlyingTypeInfo;

        /// <summary>
        /// Gets the parameter's converter, if any.
        /// </summary>
        public readonly object Converter;


        /// <summary>
        /// Initializes a new instance of the ThriftMethodReturnValue class with the specified values.
        /// </summary>
        public ThriftReturnValue( TypeInfo typeInfo, object converter )
        {
            WireType = ThriftType.Get( typeInfo.AsType(), converter );
            UnderlyingTypeInfo = typeInfo;
            Converter = converter;
        }
    }

    /// <summary>
    /// Thrift method.
    /// </summary>
    internal sealed class ThriftMethod
    {
        /// <summary>
        /// Gets the method's name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets a value indicating whether the method is one-way.
        /// </summary>
        /// <remarks>
        /// If it is, no reply should be read.
        /// </remarks>
        public readonly bool IsOneWay;

        /// <summary>
        /// Gets the method's return value.
        /// </summary>
        public readonly ThriftReturnValue ReturnValue;

        /// <summary>
        /// Gets the method's "throws" clauses.
        /// </summary>
        public readonly IReadOnlyList<ThriftThrowsClause> Exceptions;

        /// <summary>
        /// Gets the method's parameters.
        /// </summary>
        public readonly IReadOnlyList<ThriftParameter> Parameters;


        /// <summary>
        /// Initializes a new instance of the ThriftMethod class with the specified values.
        /// </summary>
        public ThriftMethod( string name, bool isOneWay,
                             ThriftReturnValue returnValue,
                             IReadOnlyList<ThriftThrowsClause> exceptions,
                             IReadOnlyList<ThriftParameter> parameters )
        {
            Name = name;
            IsOneWay = isOneWay;
            ReturnValue = returnValue;
            Exceptions = exceptions;
            Parameters = parameters;
        }
    }

    /// <summary>
    /// Thrift service.
    /// </summary>
    internal sealed class ThriftService
    {
        /// <summary>
        /// Gets the service's name.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Gets the service's Thrift methods, mapped by their .NET name.
        /// </summary>
        public readonly IReadOnlyDictionary<string, ThriftMethod> Methods;


        /// <summary>
        /// Initializes a new instance of the ThriftService class with the specified values.
        /// </summary>
        public ThriftService( string name, IReadOnlyDictionary<string, ThriftMethod> methods )
        {
            Name = name;
            Methods = methods;
        }
    }
}