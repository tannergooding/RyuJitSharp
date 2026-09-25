// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public static instruction emitJumpKindToIns(emitJumpKind jumpKind)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Jump-kind instruction mapping requires AMD64.");
#else
        assert(unchecked((uint)jumpKind) < (uint)emitJumpKindInstructions.Length);
        return emitJumpKindInstructions[(int)jumpKind];
#endif
    }

#if DEBUG
#pragma warning disable CA1802 // Keep the native debug selector non-constant so disabled tracing has no unreachable branches.
    private static readonly int INTERESTING_JUMP_NUM = -1;
#pragma warning restore CA1802
#endif

#if EMITTER_STATS
    private static uint emitTotalIGjmps;
#endif

    public void emitIns_J(instruction ins, BasicBlock dst, bool keepShort = false, bool isRemovableJmpCandidate = false)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Label jump instruction recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(_compiler is not null);
        assert(_compiler.compCurBB is not null);
        var lastInsIsCall = emitIsLastInsCall();
        uint sz;
        var id = emitNewInstrJmp();

        assert(dst.HasFlag(BBF_HAS_LABEL));
        id.idIns(ins);
        id.idInsFmt(IF_LABEL);
#if DEBUG
        if ((ins == INS_call) && (_compiler.compCurBB.Kind is BBJ_CALLFINALLY))
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idFinallyCall = true;
        }
#endif

        if (isRemovableJmpCandidate)
        {
            emitContainsRemovableJmpCandidates = true;
            id.idjIsRemovableJmpCandidate = true;
            // Removing a jump after a call may require a nop before an OS epilog.
            // The epilog check belongs to emitRemoveJumpToNextInst.
            id.idjIsAfterCallBeforeEpilog = lastInsIsCall;
        }
        else
        {
            id.idjIsRemovableJmpCandidate = false;
        }

        id.idjShort = keepShort;
        id.idjTarget = dst;
        id.idjKeepLong = _compiler.fgInDifferentRegions(_compiler.compCurBB, dst);
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
#if EMITTER_STATS
        emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
#endif

        if (ins == INS_call)
        {
            sz = CALL_INST_SIZE;
        }
        else if (ins is INS_push or INS_push_hide)
        {
            // Label pushes encode an absolute address, unlike relative branches.
            if (_compiler.opts.compReloc)
            {
                id.idSetIsDspReloc();
            }
            sz = PUSH_INST_SIZE;
        }
        else
        {
            insGroup? tgt = null;
            if (dst is not null)
            {
                sz = ins == INS_jmp ? (uint)JMP_SIZE_LARGE : (uint)JCC_SIZE_LARGE;
                tgt = emitCodeGetCookie(dst);
            }
            else
            {
                sz = JMP_SIZE_SMALL;
            }

            if (tgt is not null)
            {
#pragma warning disable CA1508 // The distance estimate requires conditional and unconditional short encodings to have equal widths.
                assert(JMP_SIZE_SMALL == JCC_SIZE_SMALL);
#pragma warning restore CA1508
                var srcOffs = unchecked((uint)emitCurCodeOffset + (uint)emitCurIGsize + JMP_SIZE_SMALL);
                var jmpDist = unchecked((int)(srcOffs - tgt.igOffs));
                assert(jmpDist > 0);
                var extra = unchecked(jmpDist + JMP_DIST_SMALL_MAX_NEG);
#if DEBUG
                var debugInfo = id.idDebugOnlyInfo();
                assert(debugInfo is not null);
                if ((debugInfo.idNum == unchecked((uint)INTERESTING_JUMP_NUM)) || (INTERESTING_JUMP_NUM == 0))
                {
                    if (INTERESTING_JUMP_NUM == 0)
                    {
                        jitprintf($"[0] Jump {debugInfo.idNum}:\n");
                    }
                    jitprintf($"[0] Jump source is at {srcOffs:X8}\n");
                    jitprintf($"[0] Label block is at {tgt.igOffs:X8}\n");
                    jitprintf($"[0] Jump  distance  - {jmpDist:X4}\n");
                    if (extra > 0)
                    {
                        jitprintf($"[0] Distance excess = {extra}  \n");
                    }
                }
#endif
                if ((extra <= 0) && !id.idjKeepLong)
                {
                    emitSetShortJump(id);
                    sz = JMP_SIZE_SMALL;
                }
            }
#if DEBUG
            else
            {
                var debugInfo = id.idDebugOnlyInfo();
                assert(debugInfo is not null);
                if ((debugInfo.idNum == unchecked((uint)INTERESTING_JUMP_NUM)) || (INTERESTING_JUMP_NUM == 0))
                {
                    if (INTERESTING_JUMP_NUM == 0)
                    {
                        jitprintf($"[0] Jump {debugInfo.idNum}:\n");
                    }
                    var srcOffs = unchecked((uint)emitCurCodeOffset + (uint)emitCurIGsize + JMP_SIZE_SMALL);
                    jitprintf($"[0] Jump source is at {emitCurIGsize:X4}/{srcOffs:X8}\n");
                    jitprintf("[0] Label block is unknown\n");
                }
            }
#endif
        }

        id.idCodeSize(sz);
        dispIns(id);
        appendToCurIG(id);

        // Native emitAdjustStackDepthPushPop is empty with AMD64's FEATURE_FIXED_OUT_ARGS.
#endif
    }

#if TARGET_AMD64
    private static void emitSetShortJump(instrDescJmp id)
    {
        if (id.idjKeepLong)
        {
            return;
        }

        id.idjShort = true;
    }

    private static insGroup? emitCodeGetCookie(BasicBlock block)
    {
        assert(block is not null);
        return block.bbEmitCookie;
    }

    private void appendToCurIG(instrDesc id)
    {
        emitCurIGsize = unchecked(emitCurIGsize + (int)id.idCodeSize());
    }
#endif
}
