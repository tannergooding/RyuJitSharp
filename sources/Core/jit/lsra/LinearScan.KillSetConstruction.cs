// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private regMaskTP getKillSetForStoreInd(GenTreeStoreInd tree)
    {
        assert(tree.Oper is GT_STOREIND);
        var codeGen = _compiler.codeGen
            ?? throw new FatalJitException("Write-barrier kill-set construction requires initialized CodeGen.");
        var form = codeGen.GCInfo.gcIsWriteBarrierCandidate(tree);
        if (form is GCInfo.WriteBarrierForm.WBF_NoBarrier)
        {
            return new regMaskTP(SRBM_NONE);
        }

        if (codeGen.genUseOptimizedWriteBarriers(form))
        {
#if TARGET_X86 && NOGC_WRITE_BARRIERS
            return new regMaskTP(LsraGlobals.genSingleTypeRegMask(REG_EDX));
#else
            throw new FatalJitException("Optimized write barriers are unsupported on this target.");
#endif
        }

        var helper = codeGen.genWriteBarrierHelperForWriteBarrierForm(form);
        return _compiler.compHelperCallKillSet(helper);
    }

    private static regMaskTP getKillSetForShiftRotate(GenTreeOp shiftNode)
    {
        var killMask = new regMaskTP(SRBM_NONE);
#if TARGET_XARCH
        assert(shiftNode.Oper.IsShiftOrRotate);
        if (!shiftNode.Op2.IsContained)
        {
            killMask = new regMaskTP(SRBM_RCX);
        }
#endif
        return killMask;
    }

#if TARGET_AMD64
    private regMaskTP getKillSetForMul(GenTreeOp mulNode)
    {
        assert(mulNode.Oper is GT_MUL or GT_MULHI);
        if (mulNode.Oper is not GT_MUL)
        {
            if (mulNode.IsUnsigned && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
            {
                return (mulNode.Op1.IsContained || mulNode.Op2.IsContained)
                    ? new regMaskTP(SRBM_NONE)
                    : new regMaskTP(SRBM_RDX);
            }

            return new regMaskTP(SRBM_RAX | SRBM_RDX);
        }

        return mulNode.IsUnsigned && mulNode.HasOverflowCheckEx
            ? new regMaskTP(SRBM_RAX | SRBM_RDX)
            : new regMaskTP(SRBM_NONE);
    }

    private static regMaskTP getKillSetForModDiv(GenTreeOp node)
    {
        assert(node.Oper is GT_MOD or GT_DIV or GT_UMOD or GT_UDIV);
        return varTypeUsesIntReg(node.Type)
            ? new regMaskTP(SRBM_RAX | SRBM_RDX)
            : new regMaskTP(SRBM_NONE);
    }
#endif

    private regMaskTP getKillSetForCall(GenTreeCall call)
    {
        var killMask = getCalleeTrashKillMask();
#if TARGET_X86
        if (_compiler.compFloatingPointUsed)
        {
            if (call.Type is TYP_DOUBLE)
            {
                _needDoubleTmpForFPCall = true;
            }
            else if (call.Type is TYP_FLOAT)
            {
                _needFloatTmpForFPCall = true;
            }
        }
#endif
        if (call.IsHelperCall())
        {
            killMask = _compiler.compHelperCallKillSet(call.HelperNum);
        }

        if (!_needToKillFloatRegisters)
        {
            assert(!_compiler.compFloatingPointUsed || !_enregisterLocalVars);
#if TARGET_ARM64
            killMask = removeRegisterSets(killMask, SRBM_NONE, SRBM_FLT_CALLEE_TRASH,
                SRBM_MSK_CALLEE_TRASH);
#else
            var codeGen = _compiler.codeGen
                ?? throw new FatalJitException("Call kill-set construction requires initialized CodeGen.");
#if TARGET_XARCH
            killMask = removeRegisterSets(killMask, SRBM_NONE, codeGen.SRBM_FLT_CALLEE_TRASH,
                codeGen.SRBM_MSK_CALLEE_TRASH);
#else
            killMask = removeRegisterSets(killMask, SRBM_NONE, codeGen.SRBM_FLT_CALLEE_TRASH,
                SRBM_NONE);
#endif
#endif
        }

#if TARGET_ARM
        if (call.IsVirtualStub)
        {
            var virtualStubParamInfo = _compiler.virtualStubParamInfo
                ?? throw new FatalJitException("Virtual-stub calls require initialized register metadata.");
            killMask |= virtualStubParamInfo.RegMask;
        }
#else
        if (call.IsVirtualStub)
        {
            var virtualStubParamInfo = _compiler.virtualStubParamInfo
                ?? throw new FatalJitException("Virtual-stub calls require initialized register metadata.");
            assert((killMask & virtualStubParamInfo.RegMask) == virtualStubParamInfo.RegMask);
        }
#endif

#if SWIFT_SUPPORT
        if (call.HasSwiftErrorHandling)
        {
            killMask |= new regMaskTP(SRBM_SWIFT_ERROR);
        }
#endif
        return killMask;
    }

#if TARGET_AMD64 || TARGET_ARM64
    private static regMaskTP getKillSetForBlockStore(GenTreeBlk blockNode)
    {
        assert(blockNode.Oper.IsStoreBlk);
        return new regMaskTP(SRBM_NONE);
    }

    private static regMaskTP getKillSetForHWIntrinsic(GenTreeHWIntrinsic node)
    {
        var killMask = new regMaskTP(SRBM_NONE);
#if TARGET_XARCH
        if (node.HWIntrinsicId is NI_X86Base_MaskMove)
        {
            killMask = new regMaskTP(LsraGlobals.genSingleTypeRegMask(REG_EDI));
        }
#endif
        return killMask;
    }

    private regMaskTP getKillSetForReturn(GenTree returnNode)
    {
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return new regMaskTP(SRBM_NONE);
        }

        return _compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_LEAVE);
    }

    private regMaskTP getKillSetForProfilerHook()
        => _compiler.compIsProfilerHookNeeded
            ? _compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_TAILCALL)
            : new regMaskTP(SRBM_NONE);
#endif

#if DEBUG
    private regMaskTP getKillSetForNode(GenTree tree)
    {
        var killMask = new regMaskTP(SRBM_NONE);
        switch (tree.Oper)
        {
            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROL:
            case GT_ROR:
#if TARGET_X86
            case GT_LSH_HI:
            case GT_RSH_LO:
#endif
                killMask = getKillSetForShiftRotate(tree.AsOp());
                break;

            case GT_MUL:
            case GT_MULHI:
#if !TARGET_64BIT || TARGET_ARM64
            case GT_MUL_LONG:
#endif
                killMask = getKillSetForMul(tree.AsOp());
                break;

            case GT_MOD:
            case GT_DIV:
            case GT_UMOD:
            case GT_UDIV:
                killMask = getKillSetForModDiv(tree.AsOp());
                break;

            case GT_STORE_BLK:
                killMask = getKillSetForBlockStore(tree.AsBlk());
                break;

            case GT_RETURNTRAP:
                killMask = _compiler.compHelperCallKillSet(CORINFO_HELP_STOP_FOR_GC);
                break;

            case GT_CALL:
                killMask = getKillSetForCall(tree.AsCall());
                break;

            case GT_STOREIND:
                killMask = getKillSetForStoreInd(tree.AsStoreInd());
                break;

#if PROFILING_SUPPORTED
            case GT_RETURN:
            case GT_SWIFT_ERROR_RET:
                killMask = getKillSetForReturn(tree);
                break;

            case GT_PROF_HOOK:
                killMask = getKillSetForProfilerHook();
                break;
#endif

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
                killMask = getKillSetForHWIntrinsic(tree.AsHWIntrinsic());
                break;
#endif

            case GT_PATCHPOINT:
                killMask = _compiler.compHelperCallKillSet(CORINFO_HELP_PATCHPOINT);
                break;

            case GT_PATCHPOINT_FORCED:
                killMask = _compiler.compHelperCallKillSet(CORINFO_HELP_PATCHPOINT_FORCED);
                break;

            default:
                break;
        }

        return killMask;
    }
#endif

#if DEBUG && !TARGET_AMD64
    private static regMaskTP getKillSetForMul(GenTreeOp tree)
    {
#if TARGET_ARM64
        return new regMaskTP(SRBM_NONE);
#else
        throw new FatalJitException("The multiply kill-set helper is not ported for this target.");
#endif
    }

    private static regMaskTP getKillSetForModDiv(GenTreeOp tree)
    {
#if TARGET_ARM64
        return new regMaskTP(SRBM_NONE);
#else
        throw new FatalJitException("The division kill-set helper is not ported for this target.");
#endif
    }
#endif

#if DEBUG && !TARGET_AMD64 && !TARGET_ARM64
    private static regMaskTP getKillSetForBlockStore(GenTreeBlk tree)
    {
        throw new FatalJitException("The block-store kill-set helper is not ported for this target.");
    }

    private regMaskTP getKillSetForReturn(GenTree tree)
    {
        throw new FatalJitException("The profiler return kill-set helper is not ported for this target.");
    }

    private regMaskTP getKillSetForProfilerHook()
    {
        throw new FatalJitException("The profiler hook kill-set helper is not ported for this target.");
    }

#if FEATURE_HW_INTRINSICS
    private static regMaskTP getKillSetForHWIntrinsic(GenTreeHWIntrinsic tree)
    {
        throw new FatalJitException("The hardware-intrinsic kill-set helper is not ported for this target.");
    }
#endif
#endif

    private regMaskTP getCalleeTrashKillMask()
    {
#if TARGET_ARM64
        return SRBM_CALLEE_TRASH;
#else
        var codeGen = _compiler.codeGen
            ?? throw new FatalJitException("Call kill-set construction requires initialized CodeGen.");
#if HAS_MORE_THAN_64_REGISTERS
        return new regMaskTP(codeGen.SRBM_INT_CALLEE_TRASH | codeGen.SRBM_FLT_CALLEE_TRASH,
            codeGen.SRBM_MSK_CALLEE_TRASH);
#else
        return new regMaskTP(codeGen.SRBM_INT_CALLEE_TRASH | codeGen.SRBM_FLT_CALLEE_TRASH |
            codeGen.SRBM_MSK_CALLEE_TRASH);
#endif
#endif
    }

    private static regMaskTP removeRegisterSets(
        regMaskTP killMask, regMask intRegisters, regMask floatRegisters, regMask maskRegisters)
    {
#if HAS_MORE_THAN_64_REGISTERS
        return new regMaskTP(killMask.Lower & ~(intRegisters | floatRegisters),
            killMask.Upper & ~maskRegisters);
#else
        return new regMaskTP(killMask.Lower & ~(intRegisters | floatRegisters | maskRegisters));
#endif
    }
}
