﻿using System.Linq;
using ThriftSharp.Internals;
using ThriftSharp.Protocols;

namespace ThriftSharp
{
    /// <summary>
    /// Dynamically generates proxies to Thrift services from their definitions.
    /// </summary>
    public static class ThriftProxy
    {
        /// <summary>
        /// Creates a proxy for the specified interface type using the specified protocol.
        /// </summary>
        /// <typeparam name="T">The interface type.</typeparam>
        /// <param name="protocol">The protocol.</param>
        /// <returns>A dynamically generated proxy to the Thrift service.</returns>
        public static T Create<T>( IThriftProtocol protocol )
        {
            var service = ThriftAttributesParser.ParseService( typeof( T ) );
            return TypeCreator.CreateImplementation<T>( m => args => Thrift.SendMessage( protocol, GetMethod( service, m.Name ), args ) );
        }

        /// <summary>
        /// Gets the method of the specified Thrift service with the specified name.
        /// </summary>
        private static ThriftMethod GetMethod( ThriftService service, string name )
        {
            return service.Methods.FirstOrDefault( m => m.UnderlyingName == name );
        }
    }
}
