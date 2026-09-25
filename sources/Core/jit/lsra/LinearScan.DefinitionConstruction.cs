// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private readonly RefPosition?[] _internalDefinitions = new RefPosition?[MaxInternalRegisters];
    private int _internalDefinitionCount;
    private bool _setInternalRegistersDelayFree;
    private VARSET_TP _fpCalleeSaveCandidateVars = [];
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    private VARSET_TP _largeVectorVars = [];
    private VARSET_TP _largeVectorCalleeSaveCandidateVars = [];
#endif

    private RefPosition defineNewInternalTemp(GenTree tree, RegisterType registerType, SingleTypeRegSet candidates)
    {
        var interval = newInterval(registerType);
        interval.isInternal = true;
        var definition = newRefPosition(interval, _referenceBuildLocation, RefType.RefTypeDef, tree, candidates);
        assert(_internalDefinitionCount < MaxInternalRegisters);
        _internalDefinitions[_internalDefinitionCount++] = definition;
        return definition;
    }

    private RefPosition buildInternalIntRegisterDefForNode(GenTree tree, SingleTypeRegSet candidates)
    {
        assert((candidates & ~_availableIntRegs) == SRBM_NONE);
        return defineNewInternalTemp(tree, TYP_INT, candidates);
    }

    private RefPosition buildInternalFloatRegisterDefForNode(GenTree tree, SingleTypeRegSet candidates)
    {
        assert((candidates & ~_availableFloatRegs) == SRBM_NONE);
        return defineNewInternalTemp(tree, TYP_FLOAT, candidates);
    }

#if FEATURE_SIMD && TARGET_XARCH
    private RefPosition buildInternalMaskRegisterDefForNode(GenTree tree, SingleTypeRegSet candidates)
    {
        assert((candidates & ~_availableMaskRegs) == SRBM_NONE);
        return defineNewInternalTemp(tree, TYP_MASK, candidates);
    }
#endif

    private void buildInternalRegisterUses()
    {
        assert(_internalDefinitionCount <= MaxInternalRegisters);
        for (var index = 0; index < _internalDefinitionCount; index++)
        {
            var definition = _internalDefinitions[index]
                ?? throw new FatalJitException("LSRA internal definition state contains an empty entry.");
            var use = newRefPosition(definition.getInterval(), _referenceBuildLocation, RefType.RefTypeUse,
                definition.treeNode, definition.registerAssignment);
            if (_setInternalRegistersDelayFree)
            {
                use.delayRegFree = true;
                _pendingDelayFree = true;
            }
        }
    }

    private void buildCallDefs(GenTree tree, int destinationCount, regMaskTP destinationCandidates)
    {
        var call = tree.AsCall();
        var returnTypeDesc = call.ReturnTypeDesc;
        assert(destinationCount > 0);
        assert(countRegisterMaskBits(destinationCandidates) == destinationCount);
        assert(tree.IsMultiRegCall);

        for (var index = 0; index < destinationCount; index++)
        {
            var register = returnTypeDesc.GetAbiReturnReg(checked((byte)index), call.UnmanagedCallConv);
            var registerMask = genSingleTypeRegMask(register);
            assert(destinationCandidates.IsSet(register));
            destinationCandidates = removeRegister(destinationCandidates, register);
            _ = buildDef(tree, registerMask, index);
        }
    }

    private void buildKills(GenTree tree, regMaskTP killMask)
    {
#if DEBUG && TARGET_AMD64
        assert(killMask == getKillSetForNode(tree));
#endif
        var killLocation = _referenceBuildLocation + 1;
        _ = buildKillPositionsForNode(tree, killLocation, killMask);

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        var fpCalleeKillSet = _rbmFltCalleeTrash & killMask.Lower;
        if (fpCalleeKillSet != SRBM_NONE)
        {
            buildUpperVectorSaveRefPositions(tree, killLocation, fpCalleeKillSet);
        }
#endif
    }

    private bool buildKillPositionsForNode(GenTree tree, LsraLocation location, regMaskTP killMask)
    {
        var insertedKills = false;
        if (killMask.IsNonEmpty)
        {
            _ = addKillForRegs(killMask, location);
            if (_enregisterLocalVars)
            {
                for (var varIndex = 0; varIndex < _compiler.lvaTrackedCount; varIndex++)
                {
                    if (!VarSetOps.IsMember(_compiler, _currentLiveVariables, varIndex))
                    {
                        continue;
                    }

                    var interval = getIntervalForLocalVar(checked((uint)varIndex));
                    ref var local = ref _compiler.lvaGetDesc(checked((int)interval.varNum));
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
                    if (Compiler.varTypeNeedsPartialCalleeSave(local.GetRegisterType()))
                    {
                        if (!VarSetOps.IsMember(_compiler, _largeVectorCalleeSaveCandidateVars, varIndex))
                        {
                            continue;
                        }
                    }
                    else
#endif
                    if (varTypeIsFloating(local.Type) &&
                        !VarSetOps.IsMember(_compiler, _fpCalleeSaveCandidateVars, varIndex))
                    {
                        continue;
                    }

                    updateIntervalPreferencesForKill(interval, killMask);
                }
            }

            for (var node = _definitionList.First; node is not null; node = node.next)
            {
                var definition = node.refPosition
                    ?? throw new FatalJitException("An LSRA definition-list entry must retain its reference.");
                var interval = definition.getInterval();
                if (!interval.isLocalVar)
                {
                    updateIntervalPreferencesForKill(interval, killMask);
                }
            }

            insertedKills = true;
        }

        if (_compiler.killGCRefs(tree))
        {
            _ = newRefPosition(null, location, RefType.RefTypeKillGCRefs, tree,
                _availableIntRegs & ~SRBM_ARG_REGS);
            insertedKills = true;
        }

        return insertedKills;
    }

    private void updateIntervalPreferencesForKill(Interval interval, regMaskTP killMask)
    {
#if HAS_MORE_THAN_64_REGISTERS
        var fullCalleeTrash = new regMaskTP(
            _rbmIntCalleeTrash | _rbmFltCalleeTrash, _rbmMskCalleeTrash);
#else
        var fullCalleeTrash = new regMaskTP(_rbmIntCalleeTrash | _rbmFltCalleeTrash | _rbmMskCalleeTrash);
#endif
        var isCallKill = (killMask.Lower == _rbmIntCalleeTrash) || (killMask == fullCalleeTrash);
        var registersKilled = getRegSetForType(killMask, regType(interval.registerType));

        if (isCallKill)
        {
            interval.preferCalleeSave = true;
        }

        if (!interval.isWriteThru || !isCallKill)
        {
            var newPreferences = allRegs(interval.registerType) & ~registersKilled;
            if (newPreferences != SRBM_NONE)
            {
                if (!interval.isWriteThru)
                {
                    interval.registerAversion |= registersKilled;
                }

                interval.updateRegisterPreferences(newPreferences);
            }
        }
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    private void buildUpperVectorSaveRefPositions(GenTree? tree, LsraLocation location, SingleTypeRegSet fpCalleeKillSet)
    {
        if ((tree is not null) && tree.Oper.IsCall &&
            (tree.AsCall().IsNoReturn || Compiler.fgIsThrow(tree)))
        {
            return;
        }

        if (_enregisterLocalVars && !VarSetOps.IsEmpty(_compiler, _largeVectorVars))
        {
            assert((fpCalleeKillSet & _rbmFltCalleeTrash) != SRBM_NONE);
            assert((fpCalleeKillSet & SRBM_FLT_CALLEE_SAVED) == SRBM_NONE);
            var block = _compiler.compCurBB
                ?? throw new FatalJitException("Upper-vector saves require the current basic block.");
            var liveDefinitions = VarSetOps.MakeEmpty(_compiler);
            VarSetOps.UnionD(_compiler, liveDefinitions, block.bbLiveIn);
            VarSetOps.UnionD(_compiler, liveDefinitions, block.bbVarDef);
            var blockAlwaysReturns = block.Kind is BBJ_THROW or BBJ_EHFINALLYRET or BBJ_EHFAULTRET or
                BBJ_EHFILTERRET or BBJ_EHCATCHRET;

            for (var varIndex = 0; varIndex < _compiler.lvaTrackedCount; varIndex++)
            {
                if (!VarSetOps.IsMember(_compiler, _largeVectorVars, varIndex) ||
                    !VarSetOps.IsMember(_compiler, liveDefinitions, varIndex))
                {
                    continue;
                }

                var localInterval = getIntervalForLocalVar(checked((uint)varIndex));
                if (localInterval.isPartiallySpilled)
                {
                    continue;
                }

                var upperInterval = getUpperVectorInterval(checked((uint)varIndex));
                var savePosition = newRefPosition(upperInterval, location, RefType.RefTypeUpperVectorSave,
                    tree, SRBM_FLT_CALLEE_SAVED);
                localInterval.isPartiallySpilled = true;
                savePosition.skipSaveRestore = blockAlwaysReturns;
                savePosition.liveVarUpperSave = VarSetOps.IsMember(_compiler, _currentLiveVariables, varIndex);
#if TARGET_XARCH
                savePosition.regOptional = true;
#endif
            }
        }

        for (var node = _definitionList.First; node is not null; node = node.next)
        {
            var treeNode = node.treeNode
                ?? throw new FatalJitException("An LSRA definition-list entry must retain its defining node.");
            var registerType = getUpperVectorTemporaryType(treeNode);
            if (!Compiler.varTypeNeedsPartialCalleeSave(registerType))
            {
                continue;
            }

            var definition = node.refPosition
                ?? throw new FatalJitException("An LSRA definition-list entry must retain its reference.");
            var interval = definition.getInterval();
            if (interval.recentRefPosition?.refType is not RefType.RefTypeUpperVectorSave)
            {
                _ = newRefPosition(interval, location, RefType.RefTypeUpperVectorSave,
                    tree, SRBM_FLT_CALLEE_SAVED);
            }
        }
    }

    private unsafe var_types getUpperVectorTemporaryType(GenTree tree)
    {
        if (tree.Type is not TYP_STRUCT)
        {
            return tree.Type;
        }

        if (tree.Oper.IsLocal)
        {
            return _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum).GetRegisterType(tree.AsLclVarCommon());
        }

        assert(tree.Oper.IsCall);
        if (!tree.Oper.IsCall)
        {
            throw new FatalJitException("LSRA struct temporaries must be local variables or calls.");
        }

        var call = tree.AsCall();
        var registerType = _compiler.GetReturnTypeForStruct(
            call.RetClsHnd, call.UnmanagedCallConv, out var returnKind);
        if (returnKind is Compiler.SPK_ByValueAsHfa)
        {
            registerType = _compiler.GetHfaType(call.RetClsHnd);
        }
#if TARGET_ARM64
        else if (returnKind is Compiler.SPK_ByValue)
        {
            registerType = TYP_LONG;
        }
#endif
        assert((registerType is not TYP_STRUCT) && (registerType is not TYP_UNDEF));
        return registerType;
    }
#endif

    private void buildCallDefsWithKills(
        GenTree tree, int destinationCount, regMaskTP destinationCandidates, regMaskTP killMask)
    {
        assert(destinationCount > 0);
        assert(destinationCandidates.IsNonEmpty);
        buildKills(tree, killMask);
        buildCallDefs(tree, destinationCount, destinationCandidates);
    }

#if TARGET_ARMARCH || TARGET_RISCV64 || TARGET_LOONGARCH64
    private void buildDefWithKills(GenTree tree, SingleTypeRegSet destinationCandidates, regMaskTP killMask)
    {
        assert(!tree.AsCall().HasMultiRegRetVal);
        assert(genMaxOneBit(destinationCandidates));
        buildKills(tree, killMask);
        _ = buildDef(tree, destinationCandidates);
    }
#else
    private void buildDefWithKills(
        GenTree tree, int destinationCount, SingleTypeRegSet destinationCandidates, regMaskTP killMask)
    {
        buildKills(tree, killMask);
#if TARGET_64BIT
        assert(destinationCount == 1);
        _ = buildDef(tree, destinationCandidates);
#else
        if (destinationCount == 1)
        {
            _ = buildDef(tree, destinationCandidates);
        }
        else
        {
            assert(destinationCount == 2);
            buildDefs(tree, 2, destinationCandidates);
        }
#endif
    }
#endif

#if !TARGET_64BIT
    private void buildDefs(GenTree tree, int destinationCount, SingleTypeRegSet destinationCandidates)
    {
        assert(destinationCount > 0);
        if ((destinationCandidates == SRBM_NONE) ||
            (BitOperations.PopCount(unchecked((uint)destinationCandidates)) != destinationCount))
        {
            for (var index = 0; index < destinationCount; index++)
            {
                _ = buildDef(tree, destinationCandidates, index);
            }

            return;
        }

        for (var index = 0; index < destinationCount; index++)
        {
            var bits = unchecked((uint)destinationCandidates);
            var lowestBit = bits & unchecked(0u - bits);
            var candidate = (SingleTypeRegSet)lowestBit;
            _ = buildDef(tree, candidate, index);
            destinationCandidates &= ~candidate;
        }
    }
#endif

    private static int countRegisterMaskBits(regMaskTP registers)
    {
        var count = BitOperations.PopCount(unchecked((ulong)registers.Lower));
#if HAS_MORE_THAN_64_REGISTERS
        count += BitOperations.PopCount(unchecked((ulong)registers.Upper));
#endif
        return count;
    }
}
