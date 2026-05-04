// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Globals
{
    public static void DoPhase(Compiler compiler, Phases phase, Action action)
    {
        var actionPhase = new CompilerPhase(compiler, phase, action);
        actionPhase.Run();
    }

    public static void DoPhase(Compiler compiler, Phases phase, Func<PhaseStatus> action)
    {
        var actionPhase = new CompilerPhaseWithStatus(compiler, phase, action);
        actionPhase.Run();
    }
}
