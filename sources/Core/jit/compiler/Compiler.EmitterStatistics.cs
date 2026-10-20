// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Diagnostics;

namespace RyuJitSharp;

public partial class Compiler
{
    private static void emitterStaticStats()
    {
        Debug.Assert(Configuration.EMITTER_STATS);
        Emitter.emitStaticStats();
    }
}
