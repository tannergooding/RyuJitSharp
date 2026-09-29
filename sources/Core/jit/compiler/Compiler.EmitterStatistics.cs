// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

namespace RyuJitSharp;

public partial class Compiler
{
#if EMITTER_STATS
    private static void emitterStaticStats()
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Static emitter statistics are not yet ported.");
    }
#endif
}
