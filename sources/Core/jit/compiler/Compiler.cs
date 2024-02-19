// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed unsafe partial class Compiler
{
    /// <summary>One-time initialization.</summary>
    public static void compStartup()
    {
        // TODO: Port compStartup
    }

    /// <summary>One time finalization code.</summary>
    public static void compShutdown()
    {
        // TODO: Port compShutdown
    }

    /// <summary>Assumes called as part of process shutdown; does any compiler-specific work associated with that.</summary>
    public static void ProcessShutdownWork(ICorStaticInfo* statInfo)
    {
    }
}
