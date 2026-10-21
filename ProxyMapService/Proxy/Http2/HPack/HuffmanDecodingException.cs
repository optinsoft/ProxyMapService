// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Runtime.Serialization;

namespace ProxyMapService.Proxy.Http2.HPack
{
    // TODO: Should this be public?
    [Serializable]
    internal sealed class HuffmanDecodingException : Exception, ISerializable
    {
        public HuffmanDecodingException()
        {
        }

        public HuffmanDecodingException(string message)
            : base(message)
        {
        }
    }
}
