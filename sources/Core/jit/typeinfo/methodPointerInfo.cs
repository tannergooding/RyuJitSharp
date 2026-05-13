// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Captures information about a method pointer</summary>
public sealed class methodPointerInfo
{
    /// <summary>The CORINFO_RESOLVED_TOKEN from the IL, potentially with a more precise method handle from getCallInfo</summary>
    public CORINFO_RESOLVED_TOKEN _token;

    /// <summary>The constraint if this was a constrained ldftn.</summary>
    public mdToken _tokenConstraint;
}
