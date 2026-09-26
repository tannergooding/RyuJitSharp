// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private void insertMove(BasicBlock block, GenTree? insertionPoint, uint localNumber,
        regNumber fromReg, regNumber toReg)
    {
        ref var local = ref _compiler.lvaGetDesc(checked((int)localNumber));
        assert(IsRegCandidate(in local));
        assert((fromReg != REG_STK || toReg != REG_STK) && fromReg != toReg);
        local.RegNum = REG_STK;

        var type = local.Type;
#if FEATURE_SIMD
        if ((type is TYP_SIMD12) && _compiler.lvaMapSimd12ToSimd16(checked((int)localNumber)))
        {
            type = TYP_SIMD16;
        }
#endif
        var source = _compiler.gtNewLclvNode(type, checked((int)localNumber));
#if DEBUG
        source._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
#endif
        GenTree destination = source;
        if (fromReg == REG_STK)
        {
            source.Flags |= GTF_SPILLED;
            source.RegNum = toReg;
        }
        else if (toReg == REG_STK)
        {
            source.Flags |= GTF_SPILL;
            source.RegNum = fromReg;
        }
        else
        {
            // Register locals are normalized before a register-to-register copy.
            var registerType = local.GetRegisterType();
            source.ChangeType(registerType);
            destination = new GenTreeCopyOrReload(GT_COPY, registerType, source);
            destination.Flags &= ~GTF_VAR_DEATH;
            source.RegNum = fromReg;
            destination.RegNum = toReg;
#if DEBUG
            destination._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
#endif
        }
        destination.IsUnusedValue = true;

        var treeRange = LIR.SeqTree(_compiler, destination);
#if DEBUG
        if (VERBOSE)
        {
            _compiler.gtDispRange(treeRange);
        }
#endif
        if (insertionPoint is not null)
        {
            block.InsertBefore(insertionPoint, treeRange);
        }
        else if (block.Kind is BBJ_COND or BBJ_SWITCH)
        {
            var branch = block.LastNode ?? throw new FatalJitException("Branch resolution requires a terminator.");
            assert(branch.Oper.IsConditionalJump || branch.Oper is GT_SWITCH_TABLE or GT_SWITCH);
            block.InsertBefore(branch, treeRange);
        }
        else
        {
            assert(block.LastNode is null ||
                (!block.LastNode.Oper.IsConditionalJump &&
                 block.LastNode.Oper is not GT_SWITCH_TABLE and not GT_SWITCH and not GT_RETURN and not GT_RETFILT and not GT_SWIFT_ERROR_RET));
            block.InsertAtEnd(treeRange);
        }
    }

    private void insertSwap(BasicBlock block, GenTree? insertionPoint, uint firstLocal,
        regNumber firstReg, uint secondLocal, regNumber secondReg)
    {
#if DEBUG
        if (VERBOSE)
        {
            jitprintf($"   {FMT_BB(block.bbNum)} {(insertionPoint is null ? "bottom" : "top")}: swap V{firstLocal:D2} in {firstReg.Name} with V{secondLocal:D2} in {secondReg.Name}\n");
        }
#endif
        assert(firstReg is not REG_STK and not REG_NA && secondReg is not REG_STK and not REG_NA);
        ref var firstDesc = ref _compiler.lvaGetDesc(checked((int)firstLocal));
        ref var secondDesc = ref _compiler.lvaGetDesc(checked((int)secondLocal));
        var first = _compiler.gtNewLclvNode(firstDesc.Type, checked((int)firstLocal));
        first.RegNum = firstReg;
        var second = _compiler.gtNewLclvNode(secondDesc.Type, checked((int)secondLocal));
        second.RegNum = secondReg;
        var swap = new GenTreeOp(GT_SWAP, TYP_VOID, first, second) { RegNum = REG_NA };
#if DEBUG
        first._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
        second._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
        swap._debugFlags |= GTF_DEBUG_NODE_LSRA_ADDED;
#endif
        var range = LIR.SeqTree(_compiler, swap);
        if (insertionPoint is not null)
        {
            block.InsertBefore(insertionPoint, range);
        }
        else if (block.Kind is BBJ_COND or BBJ_SWITCH)
        {
            var branch = block.LastNode ?? throw new FatalJitException("Swap resolution requires a terminator.");
            assert(branch.Oper.IsConditionalJump || branch.Oper is GT_SWITCH_TABLE or GT_SWITCH);
            block.InsertBefore(branch, range);
        }
        else
        {
            assert(block.Kind is BBJ_ALWAYS);
            block.InsertAtEnd(range);
        }
    }

    private void addResolution(BasicBlock block, GenTree? insertionPoint, Interval interval,
        regNumber toReg, regNumber fromReg, BasicBlock fromBlock, BasicBlock? toBlock, string reason)
    {
#if DEBUG
        var insertion = insertionPoint is null ? "bottom" : "top";
        if (insertionPoint is null)
        {
            assert((uint)block.bbNum > _bbNumMaxBeforeResolution || fromReg == REG_STK ||
                interval.isWriteThru || !_blockInfo![block.bbNum].hasEHBoundaryOut);
        }
        else
        {
            assert((uint)block.bbNum > _bbNumMaxBeforeResolution || toReg == REG_STK ||
                interval.isWriteThru || !_blockInfo![block.bbNum].hasEHBoundaryIn);
        }
        JITDUMP($"   {FMT_BB(block.bbNum)} {insertion}");
        if (toBlock is not null)
        {
            JITDUMP($" ({FMT_BB(fromBlock.bbNum)}->{FMT_BB(toBlock.bbNum)})");
        }
        JITDUMP($": move V{interval.varNum:D2} from {fromReg.Name} to {toReg.Name} ({reason})\n");
#endif
        noway_assert(!block.isBBCallFinallyPairTail);
        insertMove(block, insertionPoint, interval.varNum, fromReg, toReg);
        assert((fromReg == REG_STK || toReg == REG_STK)
            ? interval.isSpilled : interval.isSpilled || interval.isSplit);
#if TRACK_LSRA_STATS
        updateLsraStat(LsraStat.STAT_RESOLUTION_MOV, checked((uint)block.bbNum));
#endif
    }
}
