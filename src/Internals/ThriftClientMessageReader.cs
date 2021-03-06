﻿// Copyright (c) Solal Pirelli
// This code is licensed under the MIT License (see Licence.txt for details)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using ThriftSharp.Models;
using ThriftSharp.Protocols;
using ThriftSharp.Utilities;

namespace ThriftSharp.Internals
{
    /// <summary>
    /// Reads Thrift messages sent from a server to a client.
    /// </summary>
    internal static class ThriftClientMessageReader
    {
        private static readonly ConcurrentDictionary<ThriftMethod, object> _knownReaders = new ConcurrentDictionary<ThriftMethod, object>();


        /// <summary>
        /// Creates a reader for the specified method.
        /// </summary>
        private static LambdaExpression CreateReaderForMethod( ThriftMethod method )
        {
            var protocolParam = Expression.Parameter( typeof( IThriftProtocol ) );

            var headerVariable = Expression.Variable( typeof( ThriftMessageHeader ) );
            ParameterExpression hasReturnVariable = null;
            ParameterExpression returnVariable = null;

            if( method.ReturnValue.UnderlyingTypeInfo != TypeInfos.Void )
            {
                hasReturnVariable = Expression.Variable( typeof( bool ) );
                returnVariable = Expression.Variable( method.ReturnValue.UnderlyingTypeInfo.AsType() );
            }

            var wireFields = new List<ThriftWireField>();
            if( returnVariable != null )
            {
                wireFields.Add( ThriftWireField.ReturnValue( method.ReturnValue, returnVariable, hasReturnVariable ) );
            }
            foreach( var exception in method.Exceptions )
            {
                wireFields.Add( ThriftWireField.ThrowsClause( exception ) );
            }

            var statements = new List<Expression>
            {
                Expression.Assign(
                    headerVariable,
                    Expression.Call(
                        protocolParam,
                        Methods.IThriftProtocol_ReadMessageHeader
                    )
                ),

                Expression.IfThen(
                    // Do not use IsFalse, not supported by UWP's expression interpreter
                    Expression.Equal(
                        Expression.Call(
                            Methods.Enum_IsDefined,
                            // The second argument is absolutely crucial here.
                            // System.Type is an abstract class, implemented by an internal framework class System.RuntimeType
                            // Not specifying the argument leads to the expression's type being typeof(System.RuntimeType),
                            // which crashes on frameworks that restrict access to non-public framework types, such as Windows Phone 8.1
                            Expression.Constant( typeof( ThriftMessageType ), typeof( Type ) ),
                            Expression.Convert(
                                Expression.Field( headerVariable, Fields.ThriftMessageHeader_MessageType ),
                                typeof( object )
                            )
                        ),
                        Expression.Constant(false)
                    ),
                    Expression.Throw(
                        Expression.New(
                            Constructors.ThriftProtocolException,
                            Expression.Constant( ThriftProtocolExceptionType.InvalidMessageType )
                        )
                    )
                ),

                Expression.IfThen(
                    Expression.Equal(
                        Expression.Field( headerVariable, Fields.ThriftMessageHeader_MessageType ),
                        Expression.Constant( ThriftMessageType.Exception )
                    ),
                    Expression.Throw(
                        Expression.Call(
                            typeof( ThriftStructReader ),
                            "Read",
                            new[] { typeof( ThriftProtocolException ) },
                            protocolParam
                        )
                    )
                ),

                ThriftStructReader.CreateReaderForFields( protocolParam, wireFields ),

                Expression.Call( protocolParam, Methods.IThriftProtocol_ReadMessageEnd )
            };

            if( returnVariable != null )
            {
                statements.Add(
                    Expression.IfThen(
                        Expression.Equal(
                            hasReturnVariable,
                            Expression.Constant( false )
                        ),
                        Expression.Throw(
                            Expression.New(
                                Constructors.ThriftProtocolException,
                                Expression.Constant( ThriftProtocolExceptionType.MissingResult )
                            )
                        )
                    )
                );
            }

            if( returnVariable == null )
            {
                statements.Add( Expression.Constant( null ) );
            }
            else
            {
                statements.Add( returnVariable );
            }

            return Expression.Lambda(
                Expression.Block(
                    returnVariable?.Type ?? typeof( object ),
                    returnVariable == null ? new[] { headerVariable } : new[] { headerVariable, hasReturnVariable, returnVariable },
                    statements
                ),
                new[] { protocolParam }
            );
        }

        /// <summary>
        /// Reads a ThriftMessage returned by the specified method on the specified protocol.
        /// </summary>
        public static T Read<T>( ThriftMethod method, IThriftProtocol protocol )
        {
            if( !_knownReaders.ContainsKey( method ) )
            {
                _knownReaders.TryAdd( method, CreateReaderForMethod( method ).Compile() );
            }

            return ( (Func<IThriftProtocol, T>) _knownReaders[method] )( protocol );
        }
    }
}