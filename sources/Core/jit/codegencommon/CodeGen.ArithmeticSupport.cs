// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public static bool genIsSameLocalVar(GenTree op1, GenTree op2)
    {
        var first = op1.SkipCopyOrReload;
        var second = op2.SkipCopyOrReload;
        return (first.Oper is GT_LCL_VAR) && (second.Oper is GT_LCL_VAR) &&
            (first.AsLclVar().LclNum == second.AsLclVar().LclNum);
    }

    public void genJumpToThrowHlpBlk(emitJumpKind jumpKind, SpecialCodeKind codeKind,
        BasicBlock? failBlock = null)
    {
        if (Configuration.TARGET_WASM)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Wasm throw-helper generation uses a separate target path.");
        }

        if (Configuration.TARGET_AMD64)
        {
            Emitter.RequireSupportedInstructionRecording();
        }
        var useThrowHelperBlocks = _compiler.fgUseThrowHelperBlocks();
        if (Configuration.UNIX_X86_ABI)
        {
            // Funclets must throw inline so their frames can be unwound.
            useThrowHelperBlocks = useThrowHelperBlocks && (_compiler.funCurrentFunc().funKind == FuncKind.FUNC_ROOT);
        }
        if (useThrowHelperBlocks)
        {
            assert(_compiler.compCurBB is not null);
            BasicBlock? target;
            if (failBlock is not null)
            {
                target = failBlock;
                if (Configuration.DEBUG)
                {
                    var add = _compiler.fgGetExcptnTarget(codeKind, _compiler.compCurBB);
                    assert(add.acdUsed);
                    assert(ReferenceEquals(target, add.acdDstBlk));
                    if (!Configuration.FEATURE_FIXED_OUT_ARGS)
                    {
                        assert(add.acdStkLvlInit || IsFramePointerUsed);
                    }
                }
            }
            else
            {
                var add = _compiler.fgGetExcptnTarget(codeKind, _compiler.compCurBB);
                assert(add is not null);
                assert(add.acdUsed);
                target = add.acdDstBlk;
                if (Configuration.DEBUG && !Configuration.FEATURE_FIXED_OUT_ARGS)
                {
                    assert(add.acdStkLvlInit || IsFramePointerUsed);
                }
            }

            noway_assert(target is not null);
            inst_JMP(jumpKind, target);
        }
        else
        {
            BasicBlock? target = null;
            var reverseJumpKind = RyuJitSharp.Emitter.emitReverseJumpKind(jumpKind);
            if (reverseJumpKind != jumpKind)
            {
                target = genCreateTempLabel();
                inst_JMP(reverseJumpKind, target);
            }

            genEmitHelperCall(Compiler.acdHelper(codeKind), 0, EA_UNKNOWN);
            if (target is not null)
            {
                assert(reverseJumpKind != jumpKind);
                genDefineTempLabel(target);
            }
        }
    }

    public void genCheckOverflow(GenTree tree)
    {
        if (Configuration.TARGET_WASM || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Overflow checking uses a separate target path.");
        }

        noway_assert(tree.HasOverflowCheck);
        noway_assert(!varTypeIsSmall(tree.Type));
        emitJumpKind jumpKind;
        if (Configuration.TARGET_ARM64 && (tree.Oper is GT_MUL))
        {
            jumpKind = Globals.EJ_ne;
        }
        else
        {
            var unsigned = (tree.Flags & GTF_UNSIGNED) != 0;
            if (Configuration.TARGET_XARCH)
            {
                jumpKind = unsigned ? Globals.EJ_jb : Globals.EJ_jo;
            }
            else if (Configuration.TARGET_ARMARCH)
            {
                jumpKind = unsigned ? Globals.EJ_lo : Globals.EJ_vs;
                if ((jumpKind == Globals.EJ_lo) && (tree.Oper is not GT_SUB))
                {
                    jumpKind = Globals.EJ_hs;
                }
            }
            else
            {
                throw new FatalJitException(CORJIT_SKIPPED, "Overflow checking is not implemented for this target.");
            }
        }

        genJumpToThrowHlpBlk(jumpKind, SCK_OVERFLOW);
    }

    public void inst_JMP(emitJumpKind jump, BasicBlock target, bool isRemovableJmpCandidate = false)
    {
        if (Configuration.TARGET_ARMARCH || Configuration.TARGET_RISCV64)
        {
            if (Configuration.DEBUG && !Configuration.FEATURE_FIXED_OUT_ARGS)
            {
                assert((target.bbTgtStkDepth * sizeof(int) == genStackLevel) || IsFramePointerUsed);
            }

            Emitter.emitIns_J(RyuJitSharp.Emitter.emitJumpKindToIns(jump), target);
            return;
        }

        if (Configuration.TARGET_XARCH)
        {
            inst_JMPXarch(jump, target, isRemovableJmpCandidate);
            return;
        }

        if (Configuration.TARGET_WASM)
        {
            inst_JMP_Wasm(jump, target);
            return;
        }

        if (Configuration.TARGET_LOONGARCH64)
        {
            inst_JMPLoongArch64(jump, target, isRemovableJmpCandidate);
            return;
        }

        throw new FatalJitException(CORJIT_SKIPPED, "Jump instruction generation is not implemented for this target.");
    }

    public void genEmitHelperCall(CorInfoHelpFunc helper, int argSize, emitAttr retSize)
    {
        genEmitHelperCall(helper, argSize, retSize, REG_NA);
    }

    public void genEmitHelperCall(CorInfoHelpFunc helper, int argSize, emitAttr retSize, regNumber callTargetReg)
    {
        if (Configuration.TARGET_XARCH)
        {
            genEmitHelperCallXarch(helper, argSize, retSize, callTargetReg);
        }
        else if (Configuration.TARGET_WASM)
        {
            genEmitHelperCallWasm(helper, argSize, retSize, callTargetReg);
        }
        else if (Configuration.TARGET_RISCV64)
        {
            genEmitHelperCallRiscV64(helper, argSize, retSize, callTargetReg);
        }
        else if (Configuration.TARGET_LOONGARCH64)
        {
            genEmitHelperCallLoongArch64(helper, argSize, retSize, callTargetReg);
        }
        else if (Configuration.TARGET_ARM)
        {
            genEmitHelperCallArm(helper, argSize, retSize, callTargetReg);
        }
        else if (Configuration.TARGET_ARM64)
        {
            genEmitHelperCallArm64(helper, argSize, retSize, callTargetReg);
        }
        else
        {
            throw new FatalJitException(CORJIT_SKIPPED,
                "Helper call generation is not implemented for this target.");
        }
    }
}
