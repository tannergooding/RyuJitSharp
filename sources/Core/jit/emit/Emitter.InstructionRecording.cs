// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    private unsafe void dispIns(instrDesc id)
    {
#if TARGET_LOONGARCH64
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction display is not used on LoongArch64.");
#else
#if DEBUG
#if TARGET_AMD64
        RequireSupportedInstructionRecording();
#endif
        emitInsSanityCheck(id);
        assert(_compiler is not null);
        if (_compiler.opts.dspCode)
        {
            emitDispIns(id, true, false, false);
        }

#if EMIT_TRACK_STACK_DEPTH
        assert(unchecked((int)emitCurStackLvl) >= 0);
#endif
        var debugInfo = id.idDebugOnlyInfo();
        assert(debugInfo is not null);
        assert(debugInfo.idSize == (nuint)emitSizeOfInsDsc(id));
#endif
#if EMITTER_STATS
        emitIFcounts[(int)id.idInsFmt()] = unchecked(emitIFcounts[(int)id.idInsFmt()] + 1);
#endif
#endif
    }

    private void appendToCurIG(instrDesc id)
    {
#if TARGET_ARMARCH
        if (id.idIns() == INS_dmb)
        {
            emitLastMemBarrier = id;
        }
        else if (emitInsIsLoadOrStore(id.idIns()))
        {
            emitLastMemBarrier = null;
        }
#endif
        emitCurIGsize = unchecked(emitCurIGsize + (int)id.idCodeSize());
    }

#if EMITTER_STATS
    private static readonly uint[] emitIFcounts = new uint[(int)insFormat.IF_COUNT];
#endif

#if DEBUG && !TARGET_AMD64
    private static void emitInsSanityCheck(instrDesc id)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction sanity checking outside AMD64 is not ported.");
    }
#endif

#if !TARGET_XARCH
    public unsafe void emitDispIns(instrDesc id, bool isNew, bool doffs, bool asmfm,
        uint offset = 0, byte* code = null, nuint size = 0, insGroup? ig = null)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "Instruction display outside xarch is not ported.");
    }
#endif

#if TARGET_ARM
    private bool emitInsIsLoadOrStore(instruction ins)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "ARM32 instruction memory classification is not ported.");
    }
#endif
}
