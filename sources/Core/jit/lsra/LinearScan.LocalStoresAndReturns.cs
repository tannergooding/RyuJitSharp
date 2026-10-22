// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void buildStoreLocDef(
        GenTreeLclVarCommon store, ref LclVarDsc local, RefPosition? sourceUse, int index)
    {
        assert(local.lvTracked);
        var interval = getIntervalForLocalVar(local._varIndex);
        if (!store.IsLastUse(index))
        {
            VarSetOps.AddElemD(_compiler, _currentLiveVariables, local._varIndex);
        }

        if (sourceUse is not null)
        {
            var sourceInterval = sourceUse.getInterval();
            if (sourceInterval.relatedInterval is null)
            {
                if (!sourceInterval.isLocalVar)
                {
                    sourceInterval.assignRelatedInterval(interval);
                }
                else
                {
                    var sourceTree = sourceUse.treeNode;
                    if (sourceTree is null)
                    {
                        FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, FatalJitErrorCode.LocalSourceUseRequiresItsTree);
                        return;
                    }

                    if ((sourceTree.Flags & GTF_VAR_DEATH) != 0)
                    {
                        sourceInterval.assignRelatedInterval(interval);
                    }
                }
            }
            else if (!sourceInterval.isLocalVar)
            {
                sourceInterval.assignRelatedInterval(interval);
            }
        }

        var registerType = local.GetRegisterType();
        var candidates = allRegs(registerType);
        if (Configuration.TARGET_X86 && varTypeIsByte(registerType))
        {
            candidates = _availableIntRegs & ~RBM_NON_BYTE_REGS.IntRegSet;
        }
        var definition = newRefPosition(interval, _referenceBuildLocation + 1,
            RefType.RefTypeDef, store, candidates, checked((uint)index));
        if (interval.isWriteThru)
        {
            definition.regOptional = true;
        }

        if (Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE &&
            LsraConfiguration.VarTypeNeedsPartialCalleeSave(interval.registerType))
        {
            interval.isPartiallySpilled = false;
        }
    }

    private int buildMultiRegStoreLoc(GenTreeLclVar store)
    {
        var source = store.Op1;
        var destinationCount = store.GetFieldCount(_compiler);
        var sourceCount = (int)destinationCount;
        ref var local = ref _compiler.lvaGetDesc(store.LclNum);

        assert(_compiler.lvaEnregMultiRegVars);
        assert(store.Oper is GT_STORE_LCL_VAR);
        var multiRegSource = source.IsMultiRegNode;
        if (multiRegSource)
        {
            assert(source.GetMultiRegCount(_compiler) == sourceCount);
        }
        else if (varTypeIsEnregisterable(source.Type))
        {
            var use = buildUse(source);
            setDelayFree(use);
            sourceCount = 1;
        }
        else
        {
            assert((source.Oper is GT_LCL_VAR) && source.IsContained && (source.Type is TYP_STRUCT));
            sourceCount = 0;
        }

        for (var index = 0; index < destinationCount; index++)
        {
            ref var field = ref _compiler.lvaGetDesc(local.lvFieldLclStart + index);
            RefPosition? singleUse = null;
            if (multiRegSource)
            {
                var sourceCandidates = SRBM_NONE;
                if (Configuration.TARGET_X86 && varTypeIsByte(field.Type))
                {
                    sourceCandidates = _availableIntRegs & ~RBM_NON_BYTE_REGS.IntRegSet;
                }
                singleUse = buildUse(source, sourceCandidates, index);
            }

            assert(field.lvLRACandidate);
            buildStoreLocDef(store, ref field, singleUse, index);

            if (multiRegSource && (index < destinationCount - 1))
            {
                _referenceBuildLocation += 2;
            }
        }

        return sourceCount;
    }

    private int buildStoreLoc(GenTreeLclVarCommon store)
    {
        var source = store.Op1;
        ref var local = ref _compiler.lvaGetDesc(store.LclNum);
        if (store.IsMultiRegLclVar)
        {
            return buildMultiRegStoreLoc(store.AsLclVar());
        }

        if (Configuration.FEATURE_SIMD &&
            varTypeIsSimd(store.Type) && !source.IsVectorZero && (store.Type is TYP_SIMD12))
        {
            if (Configuration.TARGET_ARM64)
            {
                _ = buildInternalIntRegisterDefForNode(store, _availableIntRegs);
            }
            else
            {
                _ = buildInternalFloatRegisterDefForNode(store, _availableFloatRegs);
            }
        }

        int sourceCount;
        RefPosition? singleUse = null;
        if (source.IsMultiRegNode)
        {
            assert(store.Oper is GT_STORE_LCL_VAR);
            sourceCount = source.GetMultiRegCount(_compiler);
            for (var index = 0; index < sourceCount; index++)
            {
                _ = buildUse(source, SRBM_NONE, index);
            }
        }
        else if (source.IsContained && (source.Oper is GT_BITCAST))
        {
            var bitcastSource = source.AsUnOp().Op1;
            var registerType = regType(bitcastSource.Type);
            singleUse = buildUse(bitcastSource, allRegs(registerType));
            assert(regType(singleUse.getInterval().registerType) == registerType);
            sourceCount = 1;
        }
        else if (!Configuration.TARGET_64BIT && varTypeIsLong(source.Type))
        {
            assert(source.Oper is GT_LONG);
            assert(source.IsContained && !source.AsOp().Op1.IsContained && !source.AsOp().Op2.IsContained);
            sourceCount = buildBinaryUses(source.AsOp());
            assert(sourceCount == 2);
        }
        else if (source.IsContained)
        {
            sourceCount = 0;
        }
        else
        {
            var sourceCandidates = SRBM_NONE;
            if (Configuration.TARGET_X86 && varTypeIsByte(local.GetRegisterType(store)))
            {
                sourceCandidates = _availableIntRegs & ~RBM_NON_BYTE_REGS.IntRegSet;
            }
            singleUse = buildUse(source, sourceCandidates);
            sourceCount = 1;
        }

        if (Configuration.TARGET_ARM &&
            (store.Oper is GT_STORE_LCL_FLD) && store.AsLclFld().IsOffsetMisaligned)
        {
            _ = buildInternalIntRegisterDefForNode(store, _availableIntRegs);
            _ = buildInternalIntRegisterDefForNode(store, _availableIntRegs);
            if (store.Type is TYP_DOUBLE)
            {
                _ = buildInternalIntRegisterDefForNode(store, _availableIntRegs);
            }
        }

        if (Configuration.FEATURE_SIMD || Configuration.TARGET_ARM)
        {
            buildInternalRegisterUses();
        }

        if (local.lvLRACandidate)
        {
            buildStoreLocDef(store, ref local, singleUse, 0);
        }

        return sourceCount;
    }

    private int buildReturn(GenTree tree)
    {
        var value = tree.Oper is GT_SWIFT_ERROR_RET
            ? tree.AsOp().Op2
            : tree.AsUnOp().Op1;

        if (!Configuration.TARGET_64BIT && tree.Type is TYP_LONG)
        {
            assert(value.Oper is GT_LONG && value.IsContained);
            var pair = value.AsOp();
            if (Configuration.TARGET_X86)
            {
                _ = buildUse(pair.Op1, SRBM_X86_EAX);
                _ = buildUse(pair.Op2, SRBM_X86_EDX);
            }
            else if (Configuration.TARGET_ARM)
            {
                _ = buildUse(pair.Op1, genSingleTypeRegMask(TargetRuntimeMetadata.Register0));
                _ = buildUse(pair.Op2, genSingleTypeRegMask(REG_ARM32_R1));
            }
            else if (Configuration.TARGET_WASM)
            {
                _ = buildUse(pair.Op1, SRBM_NONE);
                _ = buildUse(pair.Op2, SRBM_NONE);
            }
            else
            {
                FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.Failure32BitLongReturnRegisterContractIsNotAvailableOnThisTarget
    );
                return default!;
            }
            return 2;
        }

        if ((tree.Type is not TYP_VOID) && !value.IsContained)
        {
            if (Configuration.FEATURE_MULTIREG_RET && Configuration.TARGET_ARM64 &&
                varTypeIsSimd(tree.Type) && !value.IsMultiRegLclVar)
            {
                var returnRegister = _compiler.compRetTypeDesc.GetAbiReturnReg(0, _compiler.info.compCallConv);
                _ = buildUse(value, genSingleTypeRegMask(returnRegister));
                return 1;
            }

            if (Configuration.FEATURE_MULTIREG_RET && varTypeIsStruct(tree.Type))
            {
                if ((value.Oper is GT_LCL_VAR) && !value.IsMultiRegLclVar)
                {
                    _ = buildUse(value);
                }
                else
                {
                    assert(value.IsMultiRegCall || (value.IsMultiRegLclVar && _compiler.lvaEnregMultiRegVars));
                    var returnDescriptor = _compiler.compRetTypeDesc;
                    var sourceCount = (int)returnDescriptor.ReturnRegCount;
                    assert(value.GetMultiRegCount(_compiler) == sourceCount);
                    var mismatchedTypes = false;

                    if (value.IsMultiRegLclVar)
                    {
                        for (var index = 0; index < sourceCount; index++)
                        {
                            var sourceType = regType(value.AsLclVar().GetFieldTypeByIndex(_compiler, index));
                            var destinationType = regType(returnDescriptor.GetReturnRegType(checked((byte)index)));
                            if (sourceType != destinationType)
                            {
                                mismatchedTypes = true;
                                var destinationMask = genSingleTypeRegMask(returnDescriptor.GetAbiReturnReg(
                                    checked((byte)index), _compiler.info.compCallConv));

                                if (varTypeUsesIntReg(destinationType))
                                {
                                    _ = buildInternalIntRegisterDefForNode(tree, destinationMask);
                                }
                                else if (Configuration.FEATURE_SIMD && Configuration.TARGET_XARCH &&
                                    varTypeUsesMaskReg(destinationType))
                                {
                                    _ = buildInternalMaskRegisterDefForNode(tree, destinationMask);
                                }
                                else
                                {
                                    assert(varTypeUsesFloatReg(destinationType));
                                    _ = buildInternalFloatRegisterDefForNode(tree, destinationMask);
                                }
                            }
                        }
                    }

                    for (var index = 0; index < sourceCount; index++)
                    {
                        var matches = !mismatchedTypes ||
                            (regType(value.AsLclVar().GetFieldTypeByIndex(_compiler, index)) ==
                                regType(returnDescriptor.GetReturnRegType(checked((byte)index))));
                        var candidates = matches
                            ? genSingleTypeRegMask(returnDescriptor.GetAbiReturnReg(
                                checked((byte)index), _compiler.info.compCallConv))
                            : SRBM_NONE;
                        _ = buildUse(value, candidates, index);
                    }

                    if (mismatchedTypes)
                    {
                        buildInternalRegisterUses();
                    }

                    return sourceCount;
                }
            }
            else
            {
                SingleTypeRegSet candidates;
                if (Configuration.TARGET_X86)
                {
                    candidates = tree.Type switch {
                        TYP_FLOAT or TYP_DOUBLE => SRBM_NONE,
                        TYP_LONG => SRBM_X86_EAX | SRBM_X86_EDX,
                        _ => SRBM_X86_EAX,
                    };
                }
                else if (Configuration.TARGET_ARM)
                {
                    candidates = tree.Type switch {
                        TYP_FLOAT or TYP_DOUBLE => genSingleTypeRegMask(REG_ARM32_F0),
                        TYP_LONG => genSingleTypeRegMask(TargetRuntimeMetadata.Register0) | genSingleTypeRegMask(REG_ARM32_R1),
                        _ => genSingleTypeRegMask(TargetRuntimeMetadata.Register0),
                    };
                }
                else if (Configuration.TARGET_LOONGARCH64)
                {
                    candidates = tree.Type switch {
                        TYP_FLOAT or TYP_DOUBLE => genSingleTypeRegMask(REG_LOONGARCH64_F0),
                        _ => genSingleTypeRegMask(REG_LOONGARCH64_A0),
                    };
                }
                else if (Configuration.TARGET_RISCV64)
                {
                    candidates = tree.Type switch {
                        TYP_FLOAT or TYP_DOUBLE => genSingleTypeRegMask(REG_RISCV64_FA0),
                        _ => genSingleTypeRegMask(REG_RISCV64_A0),
                    };
                }
                else if (Configuration.TARGET_WASM)
                {
                    candidates = SRBM_NONE;
                }
                else if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)
                {
                    candidates = tree.Type switch {
                        TYP_FLOAT => TargetRuntimeMetadata.FloatingReturnMask,
                        TYP_DOUBLE => TargetRuntimeMetadata.DoubleReturnMask,
                        TYP_LONG => TargetRuntimeMetadata.LongReturnMask,
                        _ => TargetRuntimeMetadata.IntegerReturnMask,
                    };
                }
                else
                {
                    FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ReturnRegisterContractIsNotAvailableOnThisTarget
    );
                    return default!;
                }
                _ = buildUse(value, candidates);
                return 1;
            }
        }
        else if ((tree.Type is not TYP_VOID) && (value.Oper is GT_FIELD_LIST))
        {
            var returnDescriptor = _compiler.compRetTypeDesc;
            var registerIndex = 0;
            foreach (var field in value.AsFieldList().Uses)
            {
                var register = returnDescriptor.GetAbiReturnReg(
                    checked((byte)registerIndex), _compiler.info.compCallConv);
                _ = buildUse(field.Node, genSingleTypeRegMask(register));
                registerIndex++;
            }

            return registerIndex;
        }
        else
        {
            var returnDescriptor = _compiler.compRetTypeDesc;
            var killedRegisters = new regMaskTP(SRBM_NONE);
            for (byte index = 0; index < returnDescriptor.ReturnRegCount; index++)
            {
                var register = returnDescriptor.GetAbiReturnReg(index, _compiler.info.compCallConv);
                killedRegisters |= regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
            }

            _ = buildKillPositionsForNode(tree, _referenceBuildLocation + 1, killedRegisters);
        }

        return 0;
    }
}
