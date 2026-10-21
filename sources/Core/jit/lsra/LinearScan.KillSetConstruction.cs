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
        var codeGen = _compiler.codeGen;
        if (codeGen is null)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage319
            );
            return default!;
        }
        var form = codeGen.GCInfo.gcIsWriteBarrierCandidate(tree);
        if (form is GCInfo.WriteBarrierForm.WBF_NoBarrier)
        {
            return new regMaskTP(SRBM_NONE);
        }

        if (codeGen.genUseOptimizedWriteBarriers(form))
        {
            if (Configuration.TARGET_X86 && Configuration.NOGC_WRITE_BARRIERS)
            {
                return new regMaskTP(LsraGlobals.genSingleTypeRegMask(REG_X86_EDX));
            }

            FatalJitException.ThrowFatalJitException("Optimized write barriers are unsupported on this target.");
            return default!;
        }

        var helper = codeGen.genWriteBarrierHelperForWriteBarrierForm(form);
        return _compiler.compHelperCallKillSet(helper);
    }

    private static regMaskTP getKillSetForShiftRotate(GenTreeOp shiftNode)
    {
        var killMask = new regMaskTP(SRBM_NONE);
        if (Configuration.TARGET_XARCH)
        {
            assert(shiftNode.Oper.IsShiftOrRotate);
            if (!shiftNode.Op2.IsContained)
            {
                killMask = new regMaskTP(TargetRuntimeMetadata.SelectXarch(SRBM_X86_ECX, SRBM_X64_RCX));
            }
        }
        return killMask;
    }

    private regMaskTP getKillSetForMul(GenTreeOp mulNode)
    {
        if (Configuration.TARGET_XARCH)
        {
            assert(mulNode.Oper.IsMul);
            if (mulNode.Oper is not GT_MUL)
            {
                if (mulNode.IsUnsigned && _compiler.compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    return (mulNode.Op1.IsContained || mulNode.Op2.IsContained)
                        ? new regMaskTP(SRBM_NONE)
                        : TargetRuntimeMetadata.IntegerReturnRegister1Mask;
                }

                return TargetRuntimeMetadata.IntegerReturnRegisterMask |
                    TargetRuntimeMetadata.IntegerReturnRegister1Mask;
            }

            return mulNode.IsUnsigned && mulNode.HasOverflowCheckEx
                ? TargetRuntimeMetadata.IntegerReturnRegisterMask |
                    TargetRuntimeMetadata.IntegerReturnRegister1Mask
                : new regMaskTP(SRBM_NONE);
        }

        return new regMaskTP(SRBM_NONE);
    }

    private static regMaskTP getKillSetForModDiv(GenTreeOp node)
    {
        if (!Configuration.TARGET_XARCH)
        {
            return new regMaskTP(SRBM_NONE);
        }

        assert(node.Oper is GT_MOD or GT_DIV or GT_UMOD or GT_UDIV);
        return varTypeUsesIntReg(node.Type)
            ? TargetRuntimeMetadata.IntegerReturnRegisterMask |
                TargetRuntimeMetadata.IntegerReturnRegister1Mask
            : new regMaskTP(SRBM_NONE);
    }

    private regMaskTP getKillSetForCall(GenTreeCall call)
    {
        var killMask = getCalleeTrashKillMask();
        if (Configuration.TARGET_X86 && _compiler.compFloatingPointUsed)
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
        if (call.IsHelperCall())
        {
            killMask = _compiler.compHelperCallKillSet(call.HelperNum);
        }

        if (!_needToKillFloatRegisters)
        {
            assert(!_compiler.compFloatingPointUsed || !_enregisterLocalVars);
            if (Configuration.TARGET_ARM64)
            {
                killMask = removeRegisterSets(killMask, SRBM_NONE, SRBM_FLT_CALLEE_TRASH,
                    _compiler.SRBM_MSK_CALLEE_TRASH);
            }
            else if (Configuration.TARGET_WASM)
            {
                assert(killMask.IsEmpty);
            }
            else
            {
                var codeGen = _compiler.codeGen;
                if (codeGen is null)
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage320
                    );
                    return default!;
                }
                if (Configuration.TARGET_XARCH)
                {
                    killMask = removeRegisterSets(killMask, SRBM_NONE, codeGen.SRBM_FLT_CALLEE_TRASH,
                        codeGen.SRBM_MSK_CALLEE_TRASH);
                }
                else
                {
                    killMask = removeRegisterSets(killMask, SRBM_NONE, codeGen.SRBM_FLT_CALLEE_TRASH,
                        SRBM_NONE);
                }
            }
        }

        if (call.IsVirtualStub)
        {
            var virtualStubParamInfo = _compiler.virtualStubParamInfo;
            if (virtualStubParamInfo is null)
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage321
                );
                return default!;
            }
            if (Configuration.TARGET_ARM)
            {
                killMask |= virtualStubParamInfo.RegMask;
            }
            else
            {
                assert((killMask & virtualStubParamInfo.RegMask) == virtualStubParamInfo.RegMask);
            }
        }

        if (Configuration.SWIFT_SUPPORT && call.HasSwiftErrorHandling)
        {
            killMask |= new regMaskTP(TargetRuntimeMetadata.SwiftErrorMask);
        }
        return killMask;
    }

    private static regMaskTP getKillSetForBlockStore(GenTreeBlk blockNode)
    {
        assert(blockNode.Oper.IsStoreBlk);
        // Remaining block-store forms do not clobber fixed registers; helper
        // calls account for their own kills.
        return new regMaskTP(SRBM_NONE);
    }

    private static regMaskTP getKillSetForHWIntrinsic(GenTreeHWIntrinsic node)
    {
        var killMask = new regMaskTP(SRBM_NONE);
        if (Configuration.TARGET_XARCH && node.HWIntrinsicId is NI_X86Base_MaskMove)
        {
            // A fixed address use alone does not reserve implicit EDI when
            // conflicting definitions cause LSRA to reassign that address.
            killMask = new regMaskTP(LsraGlobals.genSingleTypeRegMask(
                TargetRuntimeMetadata.SelectXarch(REG_X86_EDI, REG_X64_RDI)));
        }
        return killMask;
    }

    private regMaskTP getKillSetForReturn(GenTree returnNode)
    {
        if (!_compiler.compIsProfilerHookNeeded)
        {
            return new regMaskTP(SRBM_NONE);
        }

        var killSet = _compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_LEAVE);
        if (Configuration.TARGET_ARM && returnNode.Type is TYP_VOID)
        {
            killSet |= new regMaskTP(SRBM_ARM32_R0);
        }
        return killSet;
    }

    private regMaskTP getKillSetForProfilerHook()
        => _compiler.compIsProfilerHookNeeded
            ? _compiler.compHelperCallKillSet(CORINFO_HELP_PROF_FCN_TAILCALL)
            : new regMaskTP(SRBM_NONE);

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
                killMask = getKillSetForShiftRotate(tree.AsOp());
                break;

            case GT_LSH_HI:
            case GT_RSH_LO:
            {
                if (Configuration.TARGET_X86)
                {
                    killMask = getKillSetForShiftRotate(tree.AsOp());
                }
                break;
            }

            case GT_MUL:
            case GT_MULHI:
                killMask = getKillSetForMul(tree.AsOp());
                break;

            case GT_MUL_LONG:
            {
                if (!Configuration.TARGET_64BIT || Configuration.TARGET_ARM64)
                {
                    killMask = getKillSetForMul(tree.AsOp());
                }
                break;
            }

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

            case GT_RETURN:
            case GT_SWIFT_ERROR_RET:
            {
                if (Configuration.PROFILING_SUPPORTED)
                {
                    killMask = getKillSetForReturn(tree);
                }
                break;
            }

            case GT_PROF_HOOK:
            {
                if (Configuration.PROFILING_SUPPORTED)
                {
                    killMask = getKillSetForProfilerHook();
                }
                break;
            }

            case GT_HWINTRINSIC:
                if (Configuration.FEATURE_HW_INTRINSICS)
                {
                    killMask = getKillSetForHWIntrinsic(tree.AsHWIntrinsic());
                }
                break;

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

    private regMaskTP getCalleeTrashKillMask()
    {
        if (Configuration.TARGET_ARM64)
        {
            return SRBM_CALLEE_TRASH_ARM64;
        }
        if (Configuration.TARGET_WASM)
        {
            return RBM_NONE;
        }

        var codeGen = _compiler.codeGen;
        if (codeGen is null)
        {
            FatalJitException.ThrowFatalJitException(
                CORJIT_INTERNALERROR,
                FatalJitErrorCode.DirectFailureMessage320
            );
            return default!;
        }
        return Configuration.HAS_MORE_THAN_64_REGISTERS
            ? new regMaskTP(codeGen.SRBM_INT_CALLEE_TRASH | codeGen.SRBM_FLT_CALLEE_TRASH,
                codeGen.SRBM_MSK_CALLEE_TRASH)
            : new regMaskTP(codeGen.SRBM_INT_CALLEE_TRASH | codeGen.SRBM_FLT_CALLEE_TRASH |
                codeGen.SRBM_MSK_CALLEE_TRASH);
    }

    private static regMaskTP removeRegisterSets(
        regMaskTP killMask, regMask intRegisters, regMask floatRegisters, regMask maskRegisters)
    {
        return Configuration.HAS_MORE_THAN_64_REGISTERS
            ? new regMaskTP(killMask.Lower & ~(intRegisters | floatRegisters),
                killMask.Upper & ~maskRegisters)
            : new regMaskTP(killMask.Lower & ~(intRegisters | floatRegisters | maskRegisters));
    }
}
