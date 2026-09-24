// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal sealed class RefInfoList
{
    private RefInfoListNode? _head;
    private RefInfoListNode? _tail;

    internal RefInfoListNode? First => _head;

    public void Append(RefInfoListNode node)
    {
        assert(node.next is null);
        if (_tail is null)
        {
            assert(_head is null);
            _head = node;
        }
        else
        {
            _tail.next = node;
        }

        _tail = node;
    }

    public RefInfoListNode RemoveListNode(GenTree treeNode, uint multiRegIndex)
    {
        RefInfoListNode? previous = null;
        for (var node = _head; node is not null; node = node.next)
        {
            if (ReferenceEquals(node.treeNode, treeNode) &&
                (node.refPosition?.getMultiRegIdx() == multiRegIndex))
            {
                var next = node.next;
                if (previous is null)
                {
                    _head = next;
                }
                else
                {
                    previous.next = next;
                }

                if (next is null)
                {
                    _tail = previous;
                }

                node.next = null;
                return node;
            }

            previous = node;
        }

        assert(false, "Definition list does not contain the requested node and register index.");
        throw new FatalJitException("LSRA could not match a use with its definition.");
    }
}

public sealed partial class LinearScan
{
    private readonly RefInfoList _definitionList = new();
    private RefPosition? _targetPreferredUse2;
    private RefPosition? _targetPreferredUse3;
    private LsraLocation _referenceBuildLocation;
    private VARSET_TP _currentLiveVariables = [];
    private regMaskTP _placedArgumentRegisters;
    private readonly PlacedArgumentLocal[] _placedArgumentLocals = new PlacedArgumentLocal[(int)REG_COUNT];
    private int _placedArgumentLocalCount;
    private bool _needToKillFloatRegisters;

    private struct PlacedArgumentLocal
    {
        public uint VarIndex;
        public regNumber Register;
    }

    private Interval getIntervalForLocalVar(uint varIndex)
    {
        assert(varIndex < _compiler.lvaTrackedCount);
        var localIntervals = localVarIntervals
            ?? throw new FatalJitException("Tracked LSRA locals require interval mappings.");
        var interval = localIntervals[checked((int)varIndex)]
            ?? throw new FatalJitException($"Tracked local {varIndex} has no LSRA interval.");
        return interval;
    }

    private Interval getIntervalForLocalVarNode(GenTreeLclVarCommon tree)
    {
        ref var local = ref _compiler.lvaGetDesc(tree.LclNum);
        assert(local.lvTracked);
        return getIntervalForLocalVar(local._varIndex);
    }

    private bool isCandidateLocalRef(GenTree tree)
    {
        if (!tree.Oper.IsLocal)
        {
            return false;
        }

        return _compiler.lvaGetDesc(tree.AsLclVarCommon().LclNum).lvLRACandidate;
    }

    private var_types getDefType(GenTree tree)
    {
        var type = tree.Type;
        if (type is TYP_STRUCT)
        {
            assert(tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR);
            type = _compiler.lvaGetDesc(tree.AsLclVar().LclNum).GetRegisterType(tree.AsLclVarCommon());
        }

        assert(type is not TYP_UNDEF and not TYP_STRUCT);
        if (type is TYP_UNDEF or TYP_STRUCT)
        {
            throw new FatalJitException($"LSRA cannot define a register for {type}.");
        }

        return type;
    }

    private var_types getRegisterTypeByIndex(GenTree tree, int multiRegIndex)
    {
        var index = checked((byte)multiRegIndex);
        if (tree.IsMultiRegCall)
        {
            return tree.AsCall().ReturnTypeDesc.GetReturnRegType(index);
        }

#if !TARGET_64BIT
        if (tree.Oper.IsMultiRegOp)
        {
            return tree.AsMultiRegOp().GetRegType(index);
        }
#endif

        if (tree.Oper.IsHWIntrinsic)
        {
            assert(tree.Type is TYP_STRUCT);
#if TARGET_ARM64
            var simdSize = tree.AsHWIntrinsic().SimdSize;
            if (simdSize is 16)
            {
                return TYP_SIMD16;
            }

            assert(simdSize is 8);
            if (simdSize is 8)
            {
                return TYP_SIMD8;
            }

            throw new FatalJitException($"ARM64 multi-register HW intrinsic has unsupported SIMD size {simdSize}.");
#elif TARGET_XARCH
            return tree.AsHWIntrinsic().GetOp(1).Type;
#else
            throw new FatalJitException("Multi-register HW intrinsic register typing is not implemented for this target.");
#endif
        }

        if (tree.Oper.IsScalarLocal)
        {
            if (tree.Type is TYP_LONG)
            {
                return TYP_INT;
            }

            assert(tree.Type is TYP_STRUCT);
            assert((tree.Flags & GTF_VAR_MULTIREG) != 0);
            assert(false, "GetRegTypeByIndex is not valid for a multi-register struct local.");
            throw new FatalJitException("GetRegTypeByIndex cannot determine a multi-register local field type.");
        }

        assert(false, "Multi-register definition has no per-register type implementation.");
        throw new FatalJitException("LSRA cannot determine a multi-register definition type.");
    }

    private void setTgtPref(Interval interval, RefPosition? targetPreferredUse)
    {
#if !TARGET_ARM
        if (targetPreferredUse is null)
        {
            return;
        }

        var useInterval = targetPreferredUse.getInterval();
        if (!useInterval.isLocalVar || (targetPreferredUse.treeNode is null) ||
            ((targetPreferredUse.treeNode.Flags & GTF_VAR_DEATH) != 0))
        {
            useInterval.assignRelatedIntervalIfUnassigned(interval);
        }
#endif
    }

    private RefPosition buildDef(GenTree tree, SingleTypeRegSet destinationCandidates, int multiRegIndex = 0)
    {
        assert(!tree.IsContained);
        if (destinationCandidates != SRBM_NONE)
        {
            assert((tree.RegNum is REG_NA) ||
                (destinationCandidates == genSingleTypeRegMask(tree.GetRegByIndex(checked((byte)multiRegIndex)))));
        }

        var type = tree.IsMultiRegNode ? getRegisterTypeByIndex(tree, multiRegIndex) : getDefType(tree);
        if (!varTypeUsesIntReg(type))
        {
            _compiler.compFloatingPointUsed = true;
            _needToKillFloatRegisters = true;
        }

        var interval = newInterval(type);

        if (tree.RegNum is not REG_NA)
        {
            if (!tree.IsMultiRegNode || (multiRegIndex == 0))
            {
                assert((destinationCandidates == SRBM_NONE) ||
                    (destinationCandidates == genSingleTypeRegMask(tree.RegNum)));
                destinationCandidates = genSingleTypeRegMask(tree.RegNum);
            }
            else
            {
                assert(isSingleRegister(destinationCandidates));
            }
        }

#if TARGET_X86
        else if (varTypeIsByte(type))
        {
            if (destinationCandidates == SRBM_NONE)
            {
                destinationCandidates = _availableIntRegs;
            }

            destinationCandidates &= ~RBM_NON_BYTE_REGS.GetIntRegSet();
            assert(destinationCandidates != SRBM_NONE);
        }
#endif

        if (_pendingDelayFree)
        {
            interval.hasInterferingUses = true;
        }

        var definition = newRefPosition(interval, _referenceBuildLocation + 1, RefType.RefTypeDef, tree,
            destinationCandidates, checked((uint)multiRegIndex));
        if (tree.IsUnusedValue)
        {
            definition.isLocalDefUse = true;
            definition.lastUse = true;
        }
        else
        {
            _definitionList.Append(_listNodePool.GetNode(definition, tree));
        }

        setTgtPref(interval, _targetPreferredUse);
        setTgtPref(interval, _targetPreferredUse2);
        setTgtPref(interval, _targetPreferredUse3);

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        assert(!interval.isPartiallySpilled);
#endif
        return definition;
    }

    private RefPosition buildUse(GenTree operand, SingleTypeRegSet candidates = SRBM_NONE, int multiRegIndex = 0)
    {
        assert(!operand.IsContained);
        var regOptional = operand.IsRegOptional;
        var refPositionTree = (GenTree?)operand;
        Interval interval;

        if (isCandidateLocalRef(operand))
        {
            interval = getIntervalForLocalVarNode(operand.AsLclVarCommon());
            if ((operand.Flags & GTF_VAR_DEATH) != 0)
            {
                var varIndex = interval.getVarIndex(_compiler);
                VarSetOps.RemoveElemD(_compiler, _currentLiveVariables, checked((int)varIndex));
                updatePreferencesOfDyingLocal(interval);
            }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
            buildUpperVectorRestoreRefPosition(interval, _referenceBuildLocation, operand, true,
                checked((uint)multiRegIndex));
#endif
        }
        else if (operand.IsMultiRegLclVar)
        {
            assert(_compiler.lvaEnregMultiRegVars);
            ref var local = ref _compiler.lvaGetDesc(operand.AsLclVar().LclNum);
            ref var fieldLocal = ref _compiler.lvaGetDesc(checked(local.lvFieldLclStart + multiRegIndex));
            interval = getIntervalForLocalVar(fieldLocal._varIndex);
            if (operand.AsLclVar().IsLastUse(multiRegIndex))
            {
                VarSetOps.RemoveElemD(_compiler, _currentLiveVariables, fieldLocal._varIndex);
            }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
            buildUpperVectorRestoreRefPosition(interval, _referenceBuildLocation, operand, true,
                checked((uint)multiRegIndex));
#endif
        }
        else
        {
            var info = _definitionList.RemoveListNode(operand, checked((uint)multiRegIndex));
            var definition = info.refPosition
                ?? throw new FatalJitException("LSRA definition list entry has no reference position.");
            assert(definition.getMultiRegIdx() == multiRegIndex);
            interval = definition.getInterval();
            _listNodePool.ReturnNode(info);
            refPositionTree = null;
        }

        var use = newRefPosition(interval, _referenceBuildLocation, RefType.RefTypeUse, refPositionTree, candidates,
            checked((uint)multiRegIndex));
        use.setRegOptional(regOptional);
        return use;
    }

    private void setDelayFree(RefPosition use)
    {
        use.delayRegFree = true;
        _pendingDelayFree = true;
    }

    private void updatePreferencesOfDyingLocal(Interval interval)
    {
        assert(!VarSetOps.IsMember(_compiler, _currentLiveVariables, checked((int)interval.getVarIndex(_compiler))));
        if (_placedArgumentRegisters.IsEmpty || interval.isWriteThru)
        {
            return;
        }

        var unpreferredRegisters = _placedArgumentRegisters;
        var varIndex = interval.getVarIndex(_compiler);
        for (var index = 0; index < _placedArgumentLocalCount; index++)
        {
            var placedLocal = _placedArgumentLocals[index];
            if (placedLocal.VarIndex == varIndex)
            {
                unpreferredRegisters = removeRegister(unpreferredRegisters, placedLocal.Register);
            }
        }

        if (unpreferredRegisters.IsEmpty)
        {
            return;
        }

#if DEBUG
        if (VERBOSE)
        {
            jitprintf($"Last use of V{interval.varNum:D2} between PUTARG and CALL. Removing occupied arg regs from preferences: ");
            _compiler.dumpRegMask(unpreferredRegisters);
            jitprintf("\n");
        }
#endif

        var unpreferredSet = getRegSetForType(unpreferredRegisters, interval.registerType);
        interval.registerAversion |= unpreferredSet;
        var newPreferences = allRegs(interval.registerType) & ~unpreferredSet;
        interval.updateRegisterPreferences(newPreferences);
    }

    private static regMaskTP removeRegister(regMaskTP registers, regNumber register)
    {
        var mask = regMaskTP.CreateFromRegNum(register, genSingleTypeRegMask(register));
#if HAS_MORE_THAN_64_REGISTERS
        return new regMaskTP(registers.Lower & ~mask.Lower, registers.Upper & ~mask.Upper);
#else
        return new regMaskTP(registers.Lower & ~mask.Lower);
#endif
    }

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    private Interval getUpperVectorInterval(uint varIndex)
    {
        foreach (var interval in intervals)
        {
            if (interval.isLocalVar)
            {
                continue;
            }

            if (!interval.IsUpperVector())
            {
                assert(false, "Every non-local interval in the upper-vector lookup must be an upper-vector interval.");
                throw new FatalJitException("LSRA upper-vector lookup encountered a non-upper-vector interval.");
            }

            var relatedInterval = interval.relatedInterval
                ?? throw new FatalJitException("Upper-vector intervals require a related local interval.");
            if (relatedInterval.getVarIndex(_compiler) == varIndex)
            {
                return interval;
            }
        }

        throw new FatalJitException($"No upper-vector interval exists for tracked local {varIndex}.");
    }

    private void buildUpperVectorRestoreRefPosition(
        Interval localInterval, LsraLocation location, GenTree? node, bool isUse, uint multiRegIndex)
    {
        if (!localInterval.isPartiallySpilled)
        {
            return;
        }

        localInterval.isPartiallySpilled = false;
        var varIndex = localInterval.getVarIndex(_compiler);
        var upperInterval = getUpperVectorInterval(varIndex);
        var savePosition = upperInterval.recentRefPosition
            ?? throw new FatalJitException("A partially spilled vector requires a preceding upper-vector save.");
        if (!isUse && !savePosition.liveVarUpperSave)
        {
            return;
        }

        var restorePosition = newRefPosition(upperInterval, location, RefType.RefTypeUpperVectorRestore,
            node, SRBM_NONE);
        restorePosition.setMultiRegIdx(multiRegIndex);
        if (isUse)
        {
            savePosition.skipSaveRestore = false;
            savePosition.liveVarUpperSave = true;
        }
        else
        {
            restorePosition.skipSaveRestore = savePosition.skipSaveRestore;
            restorePosition.liveVarUpperSave = savePosition.liveVarUpperSave;
        }

#if TARGET_XARCH
        restorePosition.regOptional = true;
#endif
    }
#endif

#if DEBUG
    private void dumpDefList()
    {
        if (!VERBOSE)
        {
            return;
        }

        jitprintf("DefList: { ");
        var first = true;
        for (var node = _definitionList.First; node is not null; node = node.next)
        {
            var treeNode = node.treeNode
                ?? throw new FatalJitException("A definition-list entry must retain its defining node.");
            jitprintf($"{(first ? "" : "; ")}N{treeNode._seqNum:D3}.t{treeNode.TreeId}. {treeNode.Oper.Name}");
            first = false;
        }

        jitprintf(" }\n");
    }
#endif
}
