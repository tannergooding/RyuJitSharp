// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

[Flags]
public enum PhaseChecks
{
    CHECK_NONE = 0,

    /// <summary>ir flags, etc</summary>
    CHECK_IR = 1 << 0,

    /// <summary>tree node uniqueness</summary>
    CHECK_UNIQUE = 1 << 1,

    /// <summary>flow graph integrity</summary>
    CHECK_FG = 1 << 2,

    /// <summary>eh table integrity</summary>
    CHECK_EH = 1 << 3,

    /// <summary>loop integrity/canonicalization</summary>
    CHECK_LOOPS = 1 << 4,

    /// <summary>profile data likelihood integrity</summary>
    CHECK_LIKELIHOODS = 1 << 5,

    /// <summary>profile data full integrity</summary>
    CHECK_PROFILE = 1 << 6,

    /// <summary>blocks with profile-derived weights have BBF_PROF_WEIGHT flag set</summary>
    CHECK_PROFILE_FLAGS = 1 << 7,

    /// <summary>check linked list of locals</summary>
    CHECK_LINKED_LOCALS = 1 << 8,

    /// <summary>flow graph has an init block</summary>
    CHECK_FG_INIT_BLOCK = 1 << 9,
}
