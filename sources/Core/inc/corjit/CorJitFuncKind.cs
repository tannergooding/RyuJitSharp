// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.CorJitFuncKind;

namespace RyuJitSharp;

public enum CorJitFuncKind
{
    /// <summary>The main/root function (always id==0).</summary>
    CORJIT_FUNC_ROOT,

    /// <summary>A funclet associated with an EH handler (finally, fault, catch, filter handler).</summary>
    CORJIT_FUNC_HANDLER,

    /// <summary>A funclet associated with an EH filter.</summary>
    CORJIT_FUNC_FILTER,
}
