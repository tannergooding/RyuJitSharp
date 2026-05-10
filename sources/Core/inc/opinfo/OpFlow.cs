// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.OpFlow;

namespace RyuJitSharp;

public enum OpFlow
{
    /// <summary>not a real instruction</summary>
    FLOW_META,

    /// <summary>a call instruction</summary>
    FLOW_CALL,

    /// <summary>unconditional branch, does not fall through</summary>
    FLOW_BRANCH,

    /// <summary>may fall through</summary>
    FLOW_COND_BRANCH,

    FLOW_PHI,

    FLOW_THROW,

    FLOW_BREAK,

    FLOW_RETURN,

    /// <summary>flows into next instruction (none of the above)</summary>
    FLOW_NEXT,
}
