//-----------------------------------------------------------------------
// <copyright file="Method.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace Microsoft.ServiceFabric.WatchdogService.Models
{
    [Flags]
    public enum HttpVerb : byte
    {
        Unknown = 0x00,
        Delete = 0x01,
        Get = 0x02,
        Head = 0x04,
        Options = 0x08,
        Patch = 0x10,
        Post = 0x20,
        Put = 0x40,
        Trace = 0x80,
    }
}
