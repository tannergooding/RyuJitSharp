// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_ShortJ(instruction ins, BasicBlock dst)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Forced short jumps require AMD64.");
#else
        assert(emitCurIG is not null);
        assert(emitGeneratingPrologOrFuncletProlog() || emitGeneratingEpilogOrFuncletEpilog());
        // Prolog unwind offsets are fixed while recording, so binding cannot resize these jumps.
        emitIns_J(ins, dst, keepShort: true);
#endif
    }
}
