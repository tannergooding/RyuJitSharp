// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

/// <summary>Specify compiler data that a phase might modify</summary>
public enum PhaseStatus : uint
{
    /// <summary>Phase did not make any changes that warrant running post-phase checks or dumping the main jit data strutures.</summary>
    MODIFIED_NOTHING,

    /// <summary>Phase made changes that warrant running post-phase checks or dumping the main jit data strutures.</summary>
    MODIFIED_EVERYTHING,
};
