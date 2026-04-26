// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial struct JitConfigValues
{
    [Flags]
    private enum MethodNameFlags
    {
        None = 0,
        ContainsAssemblyName = 1 << 0,
        ContainsClassName = 1 << 1,
        ClassNameContainsInstantiation = 1 << 2,
        MethodNameContainsInstantiation = 1 << 3,
        ContainsSignature = 1 << 4
    }
}
