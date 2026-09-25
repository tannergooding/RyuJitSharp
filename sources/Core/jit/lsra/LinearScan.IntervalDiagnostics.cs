// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Text;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    internal enum LsraTupleDumpMode
    {
        LSRA_DUMP_PRE,
        LSRA_DUMP_REFPOS,
        LSRA_DUMP_POST,
    }

    private static string lsraGetOperandStringPre(GenTree tree)
    {
        var lastUse = tree.Oper.IsScalarLocal && ((tree.Flags & GTF_VAR_DEATH) != 0) ? "*" : "";
        return $"t{tree.TreeId}{lastUse}";
    }

    private string lsraGetOperandString(GenTree tree, LsraTupleDumpMode mode)
    {
        if (mode is LsraTupleDumpMode.LSRA_DUMP_PRE or LsraTupleDumpMode.LSRA_DUMP_REFPOS)
        {
            return lsraGetOperandStringPre(tree);
        }

        if (mode is not LsraTupleDumpMode.LSRA_DUMP_POST)
        {
            jitprintf("ERROR: INVALID TUPLE DUMP MODE\n");
            return "";
        }

        var lastUse = tree.Oper.IsScalarLocal && ((tree.Flags & GTF_VAR_DEATH) != 0) ? "*" : "";
        if (!tree.HasReg(_compiler))
        {
            return $"STK{lastUse}";
        }

        var result = new StringBuilder().Append(tree.RegNum.Name).Append(lastUse);
        if (tree.IsMultiRegNode)
        {
            var count = tree.GetMultiRegCount(_compiler);
            for (byte index = 1; index < count; index++)
            {
                _ = result.Append(',').Append(tree.GetRegByIndex(index).Name).Append(lastUse);
            }
        }

        return result.ToString();
    }

    private void lsraDispNode(GenTree tree, LsraTupleDumpMode mode, bool hasDestination)
    {
        var spillChar = ' ';
        if (mode is LsraTupleDumpMode.LSRA_DUMP_POST)
        {
            if ((tree.Flags & GTF_SPILL) != 0)
            {
                spillChar = 'S';
            }

            if (!hasDestination && tree.HasReg(_compiler))
            {
                spillChar = spillChar is 'S' ? '$' : '*';
                hasDestination = true;
            }
        }

        jitprintf($"{spillChar} N{unchecked((uint)tree._seqNum):D3}. ");

        var localNumber = -1;
        if (tree.Oper.IsLocal)
        {
            localNumber = tree.AsLclVarCommon().LclNum;
            if (_compiler.lvaGetDesc(localNumber).lvLRACandidate)
            {
                hasDestination = false;
            }
        }

        if (hasDestination)
        {
            if (mode is LsraTupleDumpMode.LSRA_DUMP_POST && ((tree.Flags & GTF_SPILLED) != 0))
            {
                assert(tree.HasReg(_compiler));
            }

            jitprintf($"{lsraGetOperandString(tree, mode),-15} =");
        }
        else
        {
            jitprintf("                 ");
        }

        if (localNumber >= 0)
        {
            if (_compiler.lvaGetDesc(localNumber).lvLRACandidate)
            {
                if (mode is LsraTupleDumpMode.LSRA_DUMP_REFPOS)
                {
                    var varIndex = _compiler.lvaGetDesc(localNumber)._varIndex;
                    jitprintf($"  V{localNumber:D2}(L{getIntervalForLocalVar(varIndex).intervalIndex})");
                }
                else
                {
                    jitprintf($"  V{localNumber:D2}({lsraGetOperandString(tree, mode)})");
                    if (mode is LsraTupleDumpMode.LSRA_DUMP_POST && ((tree.Flags & GTF_SPILLED) != 0))
                    {
                        jitprintf("R");
                    }
                }
            }
            else
            {
                jitprintf($"  V{localNumber:D2} MEM");
            }
        }
        else
        {
            _compiler.gtDispNodeName(tree);
            if (tree.Oper.IsLeaf)
            {
                var indentStack = new IndentStack(_compiler);
                _compiler.gtDispLeaf(tree, ref indentStack);
            }
        }
    }

    private void dumpOperandDefs(GenTree operand, ref bool first, LsraTupleDumpMode mode)
    {
        var destinationCount = computeOperandDstCount(operand);
        if (destinationCount != 0)
        {
            if (!first)
            {
                jitprintf(",");
            }

            jitprintf(lsraGetOperandString(operand, mode));
            first = false;
        }
        else if (operand.IsContained)
        {
            foreach (var child in operand.Operands)
            {
                dumpOperandDefs(child, ref first, mode);
            }
        }
    }

    private void dumpVarToRegMap(regNumber[]? map)
    {
        var printed = false;
        for (var index = 0; index < _compiler.lvaTrackedCount; index++)
        {
            var trackedToLocal = _compiler.lvaTrackedToVarNum
                ?? throw new FatalJitException("Tracked locals require an index-to-local mapping.");
            if (map is null)
            {
                throw new FatalJitException("Tracked locals require a variable-to-register map.");
            }

            var register = map[index];
            if (register is not REG_STK)
            {
                jitprintf($"V{trackedToLocal[index]:D2}={register.Name} ");
                printed = true;
            }
        }

        if (!printed)
        {
            jitprintf("none");
        }

        jitprintf("\n");
    }

    private void dumpInVarToRegMap(BasicBlock block)
    {
        jitprintf($"Var=Reg beg of {FMT_BB(block.bbNum)}: ");
        dumpVarToRegMap(getInVarToRegMap((uint)block.bbNum));
    }

    private void dumpOutVarToRegMap(BasicBlock block)
    {
        jitprintf($"Var=Reg end of {FMT_BB(block.bbNum)}: ");
        dumpVarToRegMap(getOutVarToRegMap((uint)block.bbNum));
    }

    private regNumber getIncomingParameterRegister(int localNumber)
    {
        ref var local = ref _compiler.lvaGetDesc(localNumber);
        if (local.lvIsParamRegTarget)
        {
            var mappings = _compiler._paramRegLocalMappings;
            if (mappings is not null)
            {
                foreach (var mapping in mappings)
                {
                    if ((mapping.LclNum == localNumber) && (mapping.Offset == 0))
                    {
                        return mapping.RegisterSegment.Register;
                    }
                }
            }

            throw new FatalJitException($"No incoming register mapping exists for V{localNumber:D2}.");
        }

        if (local.lvIsRegArg && !local.lvIsStructField)
        {
            return _compiler.lvaGetParameterAbiInfo(localNumber).Segments[0].Register;
        }

        return REG_STK;
    }

    private void dumpIncomingParameters(LsraTupleDumpMode mode, ref int referenceIndex)
    {
        jitprintf("Incoming Parameters: ");
        while (referenceIndex < refPositions.Count)
        {
            var reference = refPositions[referenceIndex];
            if (reference.refType is RefType.RefTypeBB)
            {
                break;
            }

            var interval = reference.getInterval();
            assert(interval.isLocalVar);
            jitprintf($" V{interval.varNum:D2}");

            if (mode is LsraTupleDumpMode.LSRA_DUMP_POST)
            {
                var register = reference.registerAssignment == SRBM_NONE
                    ? REG_STK
                    : reference.assignedReg();
                ref var local = ref _compiler.lvaGetDesc(checked((int)interval.varNum));
                var incomingRegister = getIncomingParameterRegister(checked((int)interval.varNum));
                assert((register == local.RegNum) || !local.lvRegister);

                jitprintf("(");
                if (register != incomingRegister)
                {
                    jitprintf($"{incomingRegister.Name}=>");
                }

                jitprintf($"{register.Name})");
            }

            referenceIndex++;
        }

        jitprintf("\n");
    }

    private void dumpBlockBoundaryRefPositions(BasicBlock block, ref int referenceIndex)
    {
        var printedBlockHeader = false;
        while (referenceIndex < refPositions.Count)
        {
            var reference = refPositions[referenceIndex];
            var interval = reference.isIntervalRef() ? reference.getInterval() : null;
            switch (reference.refType)
            {
                case RefType.RefTypeExpUse:
                {
                    assert(interval is not null && interval.isLocalVar);
                    jitprintf($"  Exposed use of V{interval.varNum:D2} at #{unchecked((int)reference.rpNum)}\n");
                    break;
                }

                case RefType.RefTypeDummyDef:
                {
                    assert(interval is not null && interval.isLocalVar);
                    jitprintf($"  Dummy def of V{interval.varNum:D2} at #{unchecked((int)reference.rpNum)}\n");
                    break;
                }

                case RefType.RefTypeBB:
                {
                    if (printedBlockHeader)
                    {
                        return;
                    }

                    block.dspBlockHeader();
                    printedBlockHeader = true;
                    jitprintf("=====\n");
                    break;
                }

                default:
                {
                    return;
                }
            }

            referenceIndex++;
        }
    }

    private void dumpNodeRefPositions(GenTree tree, ref int referenceIndex)
    {
        var killPrinted = false;
        RefPosition? lastFixedRegister = null;
        var sequenceNumber = unchecked((uint)tree._seqNum);
        while (referenceIndex < refPositions.Count)
        {
            var reference = refPositions[referenceIndex];
            if ((reference.nodeLocation != sequenceNumber) &&
                (reference.nodeLocation != unchecked(sequenceNumber + 1)))
            {
                break;
            }

            var interval = reference.isIntervalRef() ? reference.getInterval() : null;
            switch (reference.refType)
            {
                case RefType.RefTypeUse:
                {
                    if (reference.IsPhysRegRef())
                    {
                        jitprintf($"\n                               Use:R{(int)reference.getReg().regNum}(#{unchecked((int)reference.rpNum)})");
                    }
                    else
                    {
                        assert(interval is not null);
                        jitprintf("\n                               Use:");
                        interval.microDump();
                        jitprintf($"(#{unchecked((int)reference.rpNum)})");
                        if (reference.isFixedRegRef && !interval.isInternal)
                        {
                            assert(genMaxOneBit(reference.registerAssignment));
                            assert(lastFixedRegister is not null);
                            if (lastFixedRegister is null)
                            {
                                throw new FatalJitException("A fixed use requires its preceding register reference.");
                            }

                            jitprintf($" Fixed:{reference.assignedReg().Name}(#{unchecked((int)lastFixedRegister.rpNum)})");
                            lastFixedRegister = null;
                        }

                        if (reference.isLocalDefUse)
                        {
                            jitprintf(" LocalDefUse");
                        }
                        if (reference.lastUse)
                        {
                            jitprintf(" *");
                        }
                    }
                    break;
                }

                case RefType.RefTypeDef:
                {
                    assert(interval is not null);
                    jitprintf("\n        Def:");
                    interval.microDump();
                    jitprintf($"(#{unchecked((int)reference.rpNum)})");
                    if (reference.isFixedRegRef)
                    {
                        assert(genMaxOneBit(reference.registerAssignment));
                        jitprintf($" {reference.assignedReg().Name}");
                    }
                    if (reference.isLocalDefUse)
                    {
                        jitprintf(" LocalDefUse");
                    }
                    if (reference.lastUse)
                    {
                        jitprintf(" *");
                    }
                    if (interval.relatedInterval is not null)
                    {
                        jitprintf(" Pref:");
                        interval.relatedInterval.microDump();
                    }
                    break;
                }

                case RefType.RefTypeKill:
                {
                    if (!killPrinted)
                    {
                        jitprintf("\n        Kill: ");
                        killPrinted = true;
                    }
                    _compiler.dumpRegMask(reference.getKilledRegisters());
                    jitprintf(" ");
                    break;
                }

                case RefType.RefTypeFixedReg:
                {
                    lastFixedRegister = reference;
                    break;
                }

                default:
                {
                    return;
                }
            }

            referenceIndex++;
        }
    }

    private void tupleStyleDumpPre() => tupleStyleDump(LsraTupleDumpMode.LSRA_DUMP_PRE);

    private void tupleStyleDump(LsraTupleDumpMode mode)
    {
        switch (mode)
        {
            case LsraTupleDumpMode.LSRA_DUMP_PRE:
            {
                jitprintf("TUPLE STYLE DUMP BEFORE LSRA\n");
                break;
            }

            case LsraTupleDumpMode.LSRA_DUMP_REFPOS:
            {
                jitprintf("TUPLE STYLE DUMP WITH REF POSITIONS\n");
                break;
            }

            case LsraTupleDumpMode.LSRA_DUMP_POST:
            {
                jitprintf("TUPLE STYLE DUMP WITH REGISTER ASSIGNMENTS\n");
                break;
            }

            default:
            {
                jitprintf("ERROR: INVALID TUPLE DUMP MODE\n");
                return;
            }
        }

        var referenceIndex = 0;
        if (mode is not LsraTupleDumpMode.LSRA_DUMP_PRE)
        {
            dumpIncomingParameters(mode, ref referenceIndex);
        }

        for (var block = startBlockSequence(); block is not null; block = moveToNextBlock())
        {
            if (mode is LsraTupleDumpMode.LSRA_DUMP_REFPOS)
            {
                dumpBlockBoundaryRefPositions(block, ref referenceIndex);
            }
            else
            {
                block.dspBlockHeader();
                jitprintf("=====\n");
            }

            if (_enregisterLocalVars && (mode is LsraTupleDumpMode.LSRA_DUMP_POST) &&
                (block != _compiler.fgFirstBB) && ((uint)block.bbNum <= _bbNumMaxBeforeResolution))
            {
                var blockInfo = _blockInfo
                    ?? throw new FatalJitException("Variable-location predecessors require LSRA block information.");
                jitprintf($"Predecessor for variable locations: {FMT_BB(checked((int)blockInfo[block.bbNum].predBBNum))}\n");
                dumpInVarToRegMap(block);
            }

            if ((uint)block.bbNum > _bbNumMaxBeforeResolution)
            {
                var splitBlocks = _splitBBNumToTargetBBNumMap
                    ?? throw new FatalJitException("Resolution blocks require a split-edge mapping.");
                if (!splitBlocks.TryGetValue((uint)block.bbNum, out var split))
                {
                    throw new FatalJitException($"No split-edge mapping exists for BB{block.bbNum:D2}.");
                }

                assert(split.toBBNum <= _bbNumMaxBeforeResolution);
                assert(split.fromBBNum <= _bbNumMaxBeforeResolution);
                jitprintf($"New block introduced for resolution from {FMT_BB(checked((int)split.fromBBNum))} to {FMT_BB(checked((int)split.toBBNum))}\n");
            }

            foreach (var tree in block)
            {
                var produced = tree.IsValue ? computeOperandDstCount(tree) : 0;
                var consumed = computeAvailableSrcCount(tree);

                lsraDispNode(tree, mode,
                    (produced != 0) && (mode is not LsraTupleDumpMode.LSRA_DUMP_REFPOS));

                if (mode is not LsraTupleDumpMode.LSRA_DUMP_REFPOS)
                {
                    if (consumed > 0)
                    {
                        jitprintf("; ");
                        var first = true;
                        foreach (var operand in tree.Operands)
                        {
                            dumpOperandDefs(operand, ref first, mode);
                        }
                    }
                }
                else
                {
                    dumpNodeRefPositions(tree, ref referenceIndex);
                }

                jitprintf("\n");
            }

            if (_enregisterLocalVars && (mode is LsraTupleDumpMode.LSRA_DUMP_POST))
            {
                dumpOutVarToRegMap(block);
            }

            jitprintf("\n");
        }

        jitprintf("\n\n");
    }
}
#endif
