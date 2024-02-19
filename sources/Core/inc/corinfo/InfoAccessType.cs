// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.InfoAccessType;

namespace RyuJitSharp;

/// <summary>Can a value be accessed directly from JITed code.</summary>
public enum InfoAccessType
{
    /// <summary>The info value is directly available.</summary>
    IAT_VALUE,

    /// <summary>The value needs to be accessed via an indirection.</summary>
    IAT_PVALUE,

    /// <summary>The value needs to be accessed via a double indirection.</summary>
    IAT_PPVALUE,

    /// <summary>The value needs to be accessed via a relative indirection.</summary>
    IAT_RELPVALUE
}
