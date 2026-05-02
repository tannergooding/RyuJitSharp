// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

global using static RyuJitSharp.RefCountState;

namespace RyuJitSharp;

public enum RefCountState
{
    /// <summary>not valid to get/set ref counts</summary>
    RCS_INVALID,

    /// <summary>early counts for struct promotion and struct passing</summary>
    RCS_EARLY,

    /// <summary>normal ref counts (from lvaMarkRefs onward)</summary>
    RCS_NORMAL, 
}
