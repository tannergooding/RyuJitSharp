// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class MergedReturns
{
    // Both x86's SET_EPILOGCNT_MAX and the pinned policy for other targets are four.
    public const int ReturnCountHardLimit = 4;

    private readonly Compiler _compiler;
    private readonly BasicBlock?[] _returnBlocks = new BasicBlock?[ReturnCountHardLimit];
    private readonly long[] _returnConstants = new long[ReturnCountHardLimit];
    private readonly BasicBlock?[] _insertionPoints = new BasicBlock?[ReturnCountHardLimit];
    private PhasedVar<int> _maxReturns;
    private bool _mergingReturns;

    public MergedReturns(Compiler compiler)
    {
        _compiler = compiler;
        _compiler.fgReturnCount = 0;
    }

    public void SetMaxReturns(int value)
    {
        _maxReturns.Value = value;
        _maxReturns.MarkAsReadOnly();
    }

    /// <summary>Record a return, merging when the limit is exceeded. Non-constant rewrites are deferred to morph.</summary>
    public void Record(BasicBlock returnBlock)
    {
        var oldReturnCount = _compiler.fgReturnCount++;

        if (!_mergingReturns)
        {
            if (oldReturnCount < _maxReturns.Value)
            {
                _returnBlocks[oldReturnCount] = returnBlock;
                return;
            }

            _mergingReturns = true;

            for (int i = 0, searchLimit = 0; i < oldReturnCount; i++)
            {
                var mergedReturnBlock = Merge(_returnBlocks[i], searchLimit);

                if (_returnBlocks[searchLimit] == mergedReturnBlock)
                {
                    searchLimit++;
                }
            }
        }

        // Exclude this original block from the newly established merged-return set.
        _ = Merge(returnBlock, _compiler.fgReturnCount - 1);
    }

    public BasicBlock EagerCreate()
    {
        _mergingReturns = true;
        return Merge(null, 0);
    }

    public bool PlaceReturns()
    {
        if (!_mergingReturns)
        {
            return false;
        }

        for (var index = 0; index < _compiler.fgReturnCount; index++)
        {
            var returnBlock = _returnBlocks[index];

            if (returnBlock == _compiler.genReturnBB)
            {
                continue;
            }

            var insertionPoint = _insertionPoints[index];
            assert(returnBlock is not null);
            assert(insertionPoint is not null);

            _compiler.fgUnlinkBlock(returnBlock);
            _compiler.fgMoveBlocksAfter(returnBlock, returnBlock, insertionPoint);
            // Constant returns cannot throw. Sharing the insertion point's region
            // preserves contiguous EH extents without changing exception behavior.
            _compiler.fgExtendEHRegionAfter(insertionPoint);
        }

        return true;
    }

    private unsafe BasicBlock CreateReturnBB(int index, GenTreeIntConCommon? returnConst = null)
    {
        var newReturnBB = _compiler.fgNewBBinRegion(BBJ_RETURN);
        _compiler.fgReturnCount++;
        noway_assert(newReturnBB.IsLast);
        JITDUMP($"\n newReturnBB [{FMT_BB(newReturnBB.bbNum)}] created\n");
        GenTree returnExpr;

        if (returnConst is not null)
        {
            returnExpr = _compiler.gtNewUnaryNode(GT_RETURN, returnConst.Type, returnConst);
            _returnConstants[index] = returnConst.IntegralValue;
        }
        else if (_compiler.compMethodHasRetVal)
        {
            var retLclNum = _compiler.lvaGrabTemp(shortLifetime: true, "Single return block return value");
            _compiler.genReturnLocal = retLclNum;
            ref var retVarDsc = ref _compiler.lvaGetDesc(retLclNum);
            var retLclType = _compiler.compMethodReturnsRetBufAddr ? TYP_BYREF : _compiler.info.compRetType.ActualType;

            if (varTypeIsStruct(retLclType))
            {
                _compiler.lvaSetStruct(retLclNum, _compiler.info.compMethodInfo->args.retTypeClass, unsafeValueClsCheck: false);

                if (_compiler.compMethodReturnsMultiRegRetType)
                {
                    retVarDsc.lvIsMultiRegRet = true;
                }
            }
            else
            {
                retVarDsc.Type = retLclType;
            }

            if (varTypeIsFloating(retVarDsc.Type))
            {
                _compiler.compFloatingPointUsed = true;
            }

#if DEBUG
            // Assignments to this local are introduced after stress type conversion.
            retVarDsc.lvKeepType = true;
#endif
            var retTemp = _compiler.gtNewLclvNode(retVarDsc.Type, retLclNum);
            retTemp.Flags |= GTF_DONT_CSE;
            returnExpr = _compiler.gtNewUnaryNode(GT_RETURN, retTemp.Type, retTemp);
        }
        else
        {
            assert((_compiler.info.compRetType is TYP_VOID) || varTypeIsStruct(_compiler.info.compRetType));
            _compiler.genReturnLocal = BAD_VAR_NUM;
            returnExpr = _compiler.gtNewUnaryNode(GT_RETURN, TYP_VOID, op1: null);
        }

        _compiler.fgInsertStmtAtEnd(newReturnBB, _compiler.gtNewStmt(returnExpr));
        returnExpr.Flags |= GTF_RET_MERGED;

#if DEBUG
        if (_compiler.verbose)
        {
            jitprintf("\nmergeReturns statement tree ");
            Compiler.printTreeId(returnExpr);
            jitprintf($" added to genReturnBB {newReturnBB.dspToString()}\n");
            _compiler.gtDispTree(returnExpr);
            jitprintf("\n");
        }
#endif
        assert(index < _maxReturns.Value);
        _returnBlocks[index] = newReturnBB;
        return newReturnBB;
    }

    private BasicBlock Merge(BasicBlock? returnBlock, int searchLimit)
    {
        assert(_mergingReturns);
        BasicBlock? mergedReturnBlock = null;

        // Constant merging in debug codegen can lose sequence points.
        if ((returnBlock is not null) && (_maxReturns.Value > 1) && !_compiler.opts.compDbgCode)
        {
            var retConst = GetReturnConst(returnBlock);

            if (retConst is not null)
            {
                var constReturnBlock = FindConstReturnBlock(retConst, searchLimit, out var index);

                if (constReturnBlock is null)
                {
                    var slotsReserved = searchLimit;

                    if (_compiler.genReturnBB is null)
                    {
                        // Keep one slot available for non-constant returns.
                        slotsReserved++;
                    }

                    if (slotsReserved < _maxReturns.Value)
                    {
                        constReturnBlock = CreateReturnBB(searchLimit, retConst);
                    }
                }

                if (constReturnBlock is not null)
                {
                    mergedReturnBlock = constReturnBlock;
                    assert((_compiler.info.compFlags & CORINFO_FLG_SYNCH) == 0);
                    var newEdge = _compiler.fgAddRefPred(constReturnBlock, returnBlock);
                    returnBlock.SetKindAndTargetEdge(BBJ_ALWAYS, newEdge);
                    var lastStmt = returnBlock.LastStmt;
                    assert(lastStmt is not null);
                    assert(lastStmt.RootNode.Oper is GT_RETURN);
                    assert(lastStmt.RootNode.AsUnOp().Op1.Oper.IsIntegralConst);
                    _compiler.fgRemoveStmt(returnBlock, lastStmt);

                    // The last merged source makes all incoming branches forward
                    // and permits a fallthrough from that source.
                    _insertionPoints[index] = returnBlock;

                    if (returnBlock.hasProfileWeight)
                    {
                        var oldWeight = mergedReturnBlock.hasProfileWeight ? mergedReturnBlock.bbWeight : BB_ZERO_WEIGHT;
                        var newWeight = oldWeight + returnBlock.bbWeight;
                        JITDUMP($"merging profile weight {FMT_WT(returnBlock.bbWeight)} from {FMT_BB(returnBlock.bbNum)} to const return {FMT_BB(mergedReturnBlock.bbNum)}\n");
                        mergedReturnBlock.setBBProfileWeight(newWeight);
#if DEBUG
                        if (_compiler.verbose)
                        {
                            _compiler.fgTableDispBasicBlock(mergedReturnBlock);
                        }
#endif
                    }
                }
            }
        }

        if (mergedReturnBlock is null)
        {
            // Morph must rewrite non-constant returns after tail calls and hidden
            // return buffers have been transformed, including flow/profile updates.
            mergedReturnBlock = _compiler.genReturnBB;

            if (mergedReturnBlock is null)
            {
                assert(searchLimit < _maxReturns.Value);
                mergedReturnBlock = CreateReturnBB(searchLimit);
                _compiler.genReturnBB = mergedReturnBlock;
                mergedReturnBlock.SetFlags(BBF_DONT_REMOVE);
            }
        }

        if (returnBlock is not null)
        {
            _compiler.fgReturnCount--;
        }

        return mergedReturnBlock;
    }

    private static GenTreeIntConCommon? GetReturnConst(BasicBlock returnBlock)
    {
        var lastStmt = returnBlock.LastStmt;

        if (lastStmt is null)
        {
            return null;
        }

        var lastExpr = lastStmt.RootNode;

        if (lastExpr.Oper is not GT_RETURN)
        {
            return null;
        }

        var retExpr = lastExpr.AsUnOp().Op1;
        return ((retExpr is not null) && retExpr.Oper.IsIntegralConst) ? retExpr.AsIntConCommon() : null;
    }

    private BasicBlock? FindConstReturnBlock(GenTreeIntConCommon constExpr, int searchLimit, out int index)
    {
        var constVal = constExpr.IntegralValue;

        for (var i = 0; i < searchLimit; i++)
        {
            var returnBlock = _returnBlocks[i];

            // The general return's constants entry is not meaningful, even if zero.
            if (returnBlock == _compiler.genReturnBB)
            {
                continue;
            }

            if (_returnConstants[i] == constVal)
            {
                index = i;
                return returnBlock;
            }
        }

        index = searchLimit;
        return null;
    }
}
