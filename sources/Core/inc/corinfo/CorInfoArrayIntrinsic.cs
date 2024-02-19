// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>These are used to detect array methods as NamedIntrinsic in JIT importer, which otherwise don't have a name.</summary>
public enum CorInfoArrayIntrinsic
{
    GET = 0,

    SET = 1,

    ADDRESS = 2,

    ILLEGAL
}
