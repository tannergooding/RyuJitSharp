// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public static emitJumpKind emitReverseJumpKind(emitJumpKind jumpKind)
    {
        if (Configuration.TARGET_WASM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ReverseJumpKindMappingIsNotSupportedForThisTarget
    );
            return default!;
        }

        var index = unchecked((uint)jumpKind);
        if (index >= (uint)Configuration.EMIT_JUMPKIND_COUNT)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.InvalidJumpKind,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? jumpKind : default)
    );
            return default!;
        }

        var result = emitReverseJumpKinds[(int)index];
        if ((jumpKind != EJ_NONE) && (result == EJ_NONE))
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.MissingReverseJumpMapping,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? jumpKind : default)
    );
            return default!;
        }

        return result;
    }

    public static instruction emitJumpKindToIns(emitJumpKind jumpKind)
    {
        var index = unchecked((uint)jumpKind);
        var jumpKindInstructions = emitJumpKindInstructions;
        if (index >= (uint)jumpKindInstructions.Length)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.InvalidJumpKind,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? jumpKind : default)
    );
            return default!;
        }

        var result = jumpKindInstructions[(int)index];
        if ((jumpKind != EJ_NONE) && (result == INS_invalid))
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.UnsupportedJumpKind,
        (Configuration.DETAILED_FATAL_ERROR_MESSAGES ? jumpKind : default)
    );
            return default!;
        }

        return result;
    }

    public static emitJumpKind emitInsToJumpKind(instruction ins)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARMJumpKindMappingIsNotSupportedForThisTarget
    );
            return default!;
        }

        for (var index = 1; index < Configuration.EMIT_JUMPKIND_COUNT; index++)
        {
            var mappedInstruction = emitJumpKindInstructions[index];
            if ((mappedInstruction != INS_invalid) && (ins == mappedInstruction))
            {
                var result = (emitJumpKind)index;
                assert(EJ_NONE < result && (int)result < Configuration.EMIT_JUMPKIND_COUNT);
                return result;
            }
        }

        unreached();
        return default;
    }

    private static class DebugJumpSelector
    {
#pragma warning disable CA1802 // Keep the native debug selector non-constant so disabled tracing has no unreachable branches.
        internal static readonly int INTERESTING_JUMP_NUM = -1;
#pragma warning restore CA1802
    }

    private static uint emitTotalIGjmps;

    public void emitIns_R_L(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        if (Configuration.TARGET_ARM)
        {
            emitIns_R_LArm32(ins, attr, dst, reg);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_LArm64(ins, attr, dst, reg);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            emitInsRLoongArchLabel(ins, attr, dst, reg);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            emitIns_R_LRiscV64(ins, attr, dst, reg);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.BasicBlockLabelAddressRecordingIsNotSupportedForThisTarget
    );
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(_compiler is not null);
        assert(ins == INS_lea);
        assert(dst.HasFlag(BBF_HAS_LABEL));
        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idReg1(reg);
        id.idInsFmt(IF_XARCH_RWR_LABEL);
        id.idOpSize(EA_SIZE(attr));
        id.idjTarget = dst;
        id.idjShort = false;
        id.idjKeepLong = true;
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
        if (Configuration.DEBUG)
        {
            assert(_compiler is not null);
            assert(_compiler.compCurBB is not null);

            if (_compiler.compCurBB.Kind == BBJ_EHCATCHRET)
            {
                var debugInfo = id.idDebugOnlyInfo();
                assert(debugInfo is not null);
                debugInfo.idCatchRet = true;
            }
        }
        if (Configuration.EMITTER_STATS)
        {
            emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
        }
        id.idSetRelocFlags(attr);
        var size = emitInsSizeAM(id, insCodeRM(ins));
        id.idCodeSize(size);
        dispIns(id);
        emitCurIGsize = unchecked(emitCurIGsize + (int)size);
    }

    public void emitIns_R_D(instruction ins, emitAttr attr, uint offs, regNumber reg)
    {
        if (Configuration.TARGET_ARM)
        {
            emitIns_R_DArm32(ins, attr, offs, reg);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            emitIns_R_DArm64(ins, attr, offs, reg);
            return;
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.DataLabelAddressRecordingIsNotSupportedForThisTarget
    );
        return;
    }

    public void emitIns_J_R(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        if (Configuration.TARGET_ARM)
        {
            emitIns_J_RArm32(ins, attr, dst, reg);
            return;
        }

        if (Configuration.TARGET_ARM64)
        {
            emitIns_J_RArm64(ins, attr, dst, reg);
            return;
        }

        FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ConditionalRegisterBranchesAreNotSupportedForThisTarget
    );
        return;
    }

    public void emitIns_J(instruction ins, BasicBlock dst, bool keepShort = false, bool isRemovableJmpCandidate = false)
    {
        if (Configuration.TARGET_ARM64)
        {
            emitIns_JArm64(ins, dst, keepShort);
            return;
        }

        if (Configuration.TARGET_ARM)
        {
            emitIns_JArm32(ins, dst, keepShort);
            return;
        }

        if (Configuration.TARGET_RISCV64)
        {
            assert(emitIsUncondJump(ins));
            emitIns_Jump(ins, dst, ins is INS_jal ? REG_RISCV64_RA : REG_RISCV64_ZERO, REG_RISCV64_ZERO);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            assert(!keepShort && !isRemovableJmpCandidate);
            emitInsJLoongArch64(ins, dst, 0);
            return;
        }

        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.LabelJumpInstructionRecordingIsNotSupportedForThisTarget
    );
            return;
        }

        if (Configuration.TARGET_AMD64)
        {
            RequireSupportedInstructionRecording();
        }

        assert(_compiler is not null);
        assert(_compiler.compCurBB is not null);
        var lastInsIsCall = Configuration.TARGET_AMD64 && emitIsLastInsCall();
        uint sz;
        var id = emitNewInstrJmp();

        assert(dst.HasFlag(BBF_HAS_LABEL));
        id.idIns(ins);
        id.idInsFmt(IF_XARCH_LABEL);
        if (Configuration.DEBUG && (ins == INS_call) && (_compiler.compCurBB.Kind is BBJ_CALLFINALLY))
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idFinallyCall = true;
        }

        if (isRemovableJmpCandidate)
        {
            emitContainsRemovableJmpCandidates = true;
            id.idjIsRemovableJmpCandidate = true;
            // Removing a jump after a call may require a nop before an OS epilog.
            // The epilog check belongs to emitRemoveJumpToNextInst.
            if (Configuration.TARGET_AMD64)
            {
                id.idjIsAfterCallBeforeEpilog = lastInsIsCall;
            }
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
        if (Configuration.EMITTER_STATS)
        {
            emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
        }

        if (ins == INS_call)
        {
            sz = unchecked((uint)CALL_INST_SIZE);
        }
        else if (ins is INS_push or INS_push_hide)
        {
            // Label pushes encode an absolute address, unlike relative branches.
            if (_compiler.opts.compReloc)
            {
                id.idSetIsDspReloc();
            }
            sz = unchecked((uint)PUSH_INST_SIZE);
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
                sz = unchecked((uint)JMP_SIZE_SMALL);
            }

            if (tgt is not null)
            {
#pragma warning disable CA1508 // The distance estimate requires conditional and unconditional short encodings to have equal widths.
                assert(JMP_SIZE_SMALL == JCC_SIZE_SMALL);
#pragma warning restore CA1508
                var srcOffs = unchecked((uint)emitCurCodeOffset + (uint)emitCurIGsize + (uint)JMP_SIZE_SMALL);
                var jmpDist = unchecked((int)(srcOffs - tgt.igOffs));
                assert(jmpDist > 0);
                var extra = unchecked(jmpDist + JMP_DIST_SMALL_MAX_NEG);
                if (Configuration.DEBUG_EMIT)
                {
                    var debugInfo = id.idDebugOnlyInfo();
                    assert(debugInfo is not null);
                    if ((debugInfo.idNum == unchecked((uint)DebugJumpSelector.INTERESTING_JUMP_NUM)) ||
                        (DebugJumpSelector.INTERESTING_JUMP_NUM == 0))
                    {
                        if (DebugJumpSelector.INTERESTING_JUMP_NUM == 0)
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
                }
                if ((extra <= 0) && !id.idjKeepLong)
                {
                    emitSetShortJump(id);
                    sz = unchecked((uint)JMP_SIZE_SMALL);
                }
            }
            else
            {
                if (Configuration.DEBUG_EMIT)
                {
                    var debugInfo = id.idDebugOnlyInfo();
                    assert(debugInfo is not null);
                    if ((debugInfo.idNum == unchecked((uint)DebugJumpSelector.INTERESTING_JUMP_NUM)) ||
                        (DebugJumpSelector.INTERESTING_JUMP_NUM == 0))
                    {
                        if (DebugJumpSelector.INTERESTING_JUMP_NUM == 0)
                        {
                            jitprintf($"[0] Jump {debugInfo.idNum}:\n");
                        }
                        var srcOffs = unchecked((uint)emitCurCodeOffset + (uint)emitCurIGsize + (uint)JMP_SIZE_SMALL);
                        jitprintf($"[0] Jump source is at {emitCurIGsize:X4}/{srcOffs:X8}\n");
                        jitprintf("[0] Label block is unknown\n");
                    }
                }
            }
        }

        id.idCodeSize(sz);
        dispIns(id);
        appendToCurIG(id);

        if (Configuration.TARGET_X86)
        {
            emitAdjustStackDepthPushPop(ins);
        }
    }

    private void emitIns_Jump(instruction ins, BasicBlock dst, regNumber reg1, regNumber reg2)
    {
        if (!Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.RISCVJumpRecordingIsNotSupportedForThisTarget
    );
            return;
        }

        var compiler = _compiler;
        if (compiler is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.RISCVJumpRecordingRequiresAnActiveCompiler
    );
            return;
        }
        var currentBlock = compiler.compCurBB;
        if (currentBlock is null)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_INTERNALERROR,
        FatalJitErrorCode.RISCVJumpRecordingRequiresACurrentBasicBlock
    );
            return;
        }
        assert(dst.HasFlag(BBF_HAS_LABEL));

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idReg1(reg1);
        id.idReg2(reg2);
        id.idjShort = false;
        id.idCodeSize(emitIsCmpJump(ins) ? 12u : 8u);
        id.idInsOpt(INS_OPTS_JUMP);
        id.idjTarget = dst;

        id.idjKeepLong = compiler.fgInDifferentRegions(currentBlock, dst);
        if (Configuration.DEBUG && compiler.opts.compLongAddress)
        {
            id.idjKeepLong = true;
        }

        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;
        if (Configuration.EMITTER_STATS)
        {
            emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
        }

        var targetIG = emitCodeGetCookie(dst);
        if (!id.idjKeepLong && (targetIG is not null))
        {
            var sourceOffset = unchecked((uint)emitCurCodeOffset + (uint)emitCurIGsize);
            var jumpDistance = unchecked((int)(sourceOffset - targetIG.igOffs));
            assert(jumpDistance >= 0);

            if (emitIsCmpJump(id))
            {
                if (-4096 <= -jumpDistance)
                {
                    emitSetShortJump(id);
                }
                else if (-1048576 <= -jumpDistance - sizeof(uint))
                {
                    emitSetMediumJump(id);
                }
            }
            else if (-1048576 <= -jumpDistance)
            {
                emitSetShortJump(id);
            }
        }

        dispIns(id);
        appendToCurIG(id);
    }

    private static void emitSetShortJumpXarch(instrDescJmp id)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.XarchShortJumpSelectionIsNotSupportedForThisTarget
    );
            return;
        }

        if (id.idjKeepLong)
        {
            return;
        }

        id.idjShort = true;
    }
}
