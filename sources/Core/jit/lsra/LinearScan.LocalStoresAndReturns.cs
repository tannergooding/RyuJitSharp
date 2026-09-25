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
                if (!sourceInterval.isLocalVar ||
                    ((sourceUse.treeNode ?? throw new FatalJitException("A local source use requires its tree.")).Flags &
                        GTF_VAR_DEATH) != 0)
                {
                    sourceInterval.assignRelatedInterval(interval);
                }
            }
            else if (!sourceInterval.isLocalVar)
            {
                sourceInterval.assignRelatedInterval(interval);
            }
        }

        var candidates = allRegs(local.GetRegisterType());
        var definition = newRefPosition(interval, _referenceBuildLocation + 1,
            RefType.RefTypeDef, store, candidates, checked((uint)index));
        if (interval.isWriteThru)
        {
            definition.regOptional = true;
        }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        if (Compiler.varTypeNeedsPartialCalleeSave(interval.registerType))
        {
            interval.isPartiallySpilled = false;
        }
#endif
    }

    private int buildMultiRegStoreLoc(GenTreeLclVar store)
    {
#if TARGET_AMD64
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
                singleUse = buildUse(source, SRBM_NONE, index);
            }

            assert(field.lvLRACandidate);
            buildStoreLocDef(store, ref field, singleUse, index);

            if (multiRegSource && (index < destinationCount - 1))
            {
                _referenceBuildLocation += 2;
            }
        }

        return sourceCount;
#else
        NYI("LinearScan.buildMultiRegStoreLoc outside AMD64");
        throw new FatalJitException("LinearScan.buildMultiRegStoreLoc outside AMD64.");
#endif
    }

    private int buildStoreLoc(GenTreeLclVarCommon store)
    {
#if TARGET_AMD64
        var source = store.Op1;
        ref var local = ref _compiler.lvaGetDesc(store.LclNum);
        if (store.IsMultiRegLclVar)
        {
            return buildMultiRegStoreLoc(store.AsLclVar());
        }

#if FEATURE_SIMD
        if (varTypeIsSimd(store.Type) && !source.IsVectorZero && (store.Type is TYP_SIMD12))
        {
            _ = buildInternalFloatRegisterDefForNode(store, _availableFloatRegs);
        }
#endif

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
        else if (source.IsContained)
        {
            sourceCount = 0;
        }
        else
        {
            singleUse = buildUse(source);
            sourceCount = 1;
        }

#if FEATURE_SIMD
        buildInternalRegisterUses();
#endif

        if (local.lvLRACandidate)
        {
            buildStoreLocDef(store, ref local, singleUse, 0);
        }

        return sourceCount;
#else
        NYI("LinearScan.buildStoreLoc outside AMD64");
        throw new FatalJitException("LinearScan.buildStoreLoc outside AMD64.");
#endif
    }

    private int buildReturn(GenTree tree)
    {
#if TARGET_AMD64
        var value = tree.Oper is GT_SWIFT_ERROR_RET
            ? tree.AsOp().Op2
            : tree.AsUnOp().Op1;

        if ((tree.Type is not TYP_VOID) && !value.IsContained)
        {
#if FEATURE_MULTIREG_RET
            if (varTypeIsStruct(tree.Type))
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
#if FEATURE_SIMD && TARGET_XARCH
                                else if (varTypeUsesMaskReg(destinationType))
                                {
                                    _ = buildInternalMaskRegisterDefForNode(tree, destinationMask);
                                }
#endif
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
#endif
            {
                var candidates = tree.Type switch
                {
                    TYP_FLOAT => SRBM_FLOATRET,
                    TYP_DOUBLE => SRBM_DOUBLERET,
                    TYP_LONG => SRBM_LNGRET,
                    _ => SRBM_INTRET,
                };
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
#else
        NYI("LinearScan.buildReturn outside AMD64");
        throw new FatalJitException("LinearScan.buildReturn outside AMD64.");
#endif
    }
}
