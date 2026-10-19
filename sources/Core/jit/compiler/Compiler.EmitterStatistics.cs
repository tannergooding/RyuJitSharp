// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace RyuJitSharp;

public partial class Compiler
{
    [System.Diagnostics.Conditional("EMITTER_STATS")]
    private static void emitterStaticStats()
    {
        Emitter.emitStaticStats();
    }
}
