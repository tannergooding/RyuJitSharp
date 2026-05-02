// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.BBKinds;

namespace RyuJitSharp;

public enum BBKinds : byte
{
    /// <summary>block ends with 'endfinally' (for finally)</summary>
    BBJ_EHFINALLYRET,

    /// <summary>block ends with 'endfinally' (IL alias for 'endfault') (for fault)</summary>
    BBJ_EHFAULTRET,

    /// <summary>block ends with 'endfilter'</summary>
    BBJ_EHFILTERRET,

    /// <summary>block ends with a leave out of a catch</summary>
    BBJ_EHCATCHRET,

    /// <summary>block ends with 'throw'</summary>
    BBJ_THROW,

    /// <summary>block ends with 'ret'</summary>
    BBJ_RETURN,

    /// <summary>block always jumps to the target</summary>
    BBJ_ALWAYS,

    /// <summary>block always jumps to the target, maybe out of guarded region. Only used until importing.</summary>
    BBJ_LEAVE,

    /// <summary>block always calls the target finally</summary>
    BBJ_CALLFINALLY,

    /// <summary>block targets the return from finally, aka "finally continuation". Always paired with BBJ_CALLFINALLY.</summary>
    BBJ_CALLFINALLYRET,

    /// <summary>block conditionally jumps to the target</summary>
    BBJ_COND,

    /// <summary>block ends with a switch statement</summary>
    BBJ_SWITCH,

    BBJ_COUNT,
}
