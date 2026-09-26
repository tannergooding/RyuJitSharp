// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Compiler
{
    private sealed class LoopHoistContext
    {
        private VNSet? _hoistedInCurLoop;

        public VNSet CurLoopVnInvariantCache { get; } = [];

        public int LoopVarInOutCount;
        public int LoopVarCount;
        public int HoistedExprCount;
        public int LoopVarInOutFPCount;
        public int LoopVarFPCount;
        public int HoistedFPExprCount;
#if FEATURE_MASKED_HW_INTRINSICS
        public int LoopVarInOutMskCount;
        public int LoopVarMskCount;
        public int HoistedMskExprCount;
#endif

        public VNSet GetHoistedInCurLoop() => _hoistedInCurLoop ??= [];

        public void ResetHoistedInCurLoop()
        {
            _hoistedInCurLoop = null;
            JITDUMP("Resetting m_pHoistedInCurLoop\n");
        }
    }

    private void optComputeInterestingVarSets()
    {
        lvaFloatVars = VarSetOps.MakeEmpty(this);
#if !TARGET_64BIT
        lvaLongVars = VarSetOps.MakeEmpty(this);
#endif
#if FEATURE_MASKED_HW_INTRINSICS
        lvaMaskVars = VarSetOps.MakeEmpty(this);
#endif
        for (var i = 0; i < lvaCount; i++)
        {
            ref var descriptor = ref lvaGetDesc(i);
            if (!descriptor.lvTracked)
            {
                continue;
            }

            if (varTypeUsesFloatReg(descriptor.Type))
            {
                VarSetOps.AddElemD(this, lvaFloatVars, descriptor._varIndex);
            }
#if !TARGET_64BIT
            else if (varTypeIsLong(descriptor.Type))
            {
                VarSetOps.AddElemD(this, lvaLongVars, descriptor._varIndex);
            }
#endif
#if FEATURE_MASKED_HW_INTRINSICS
            else if (varTypeUsesMaskReg(descriptor.Type))
            {
                VarSetOps.AddElemD(this, lvaMaskVars, descriptor._varIndex);
            }
#endif
        }
    }

    private void optCopyLoopMemoryDependence(GenTree fromTree, GenTree toTree)
    {
        assert(fromTree.Oper == toTree.Oper);
        var map = NodeToLoopMemoryBlockMap;
        if (map.TryGetValue(fromTree, out var block))
        {
            map.Add(toTree, block);
        }

        var fromOperands = fromTree.Operands.GetEnumerator();
        var toOperands = toTree.Operands.GetEnumerator();
        while (fromOperands.MoveNext())
        {
            if (!toOperands.MoveNext())
            {
                throw new FatalJitException("Cloned hoist expression has fewer operands than its source.");
            }
            optCopyLoopMemoryDependence(fromOperands.Current, toOperands.Current);
        }
        assert(!toOperands.MoveNext());
    }

    private void optPerformHoistExpr(GenTree origExpr, BasicBlock exprBb, FlowGraphNaturalLoop loop)
    {
        assert(loop.EntryEdges.Length == 1);
        var preheader = loop.EntryEdge(0).SourceBlock;
#if DEBUG
        if (verbose)
        {
            jitprintf($"\nHoisting a copy of ");
            printTreeId(origExpr);
            jitprintf($" ${origExpr._vnPair.Liberal:x} from {FMT_BB(exprBb.bbNum)} into PreHeader " +
                $"{FMT_BB(preheader.bbNum)} for loop L{loop.Index:D2} (head: {FMT_BB(loop.Header.bbNum)}):\n");
            gtDispTree(origExpr);
            jitprintf("\n");
        }
#endif
        var hoistExpr = gtCloneExpr(origExpr)
            ?? throw new FatalJitException("Loop hoisting requires a clone of its invariant expression.");
        hoistExpr.ClearRegNum();
        optCopyLoopMemoryDependence(origExpr, hoistExpr);
        hoistExpr.Flags |= GTF_MAKE_CSE;
        assert(hoistExpr != origExpr);

        var hoist = gtUnusedValNode(hoistExpr);
        optRecordSsaUses(hoist, preheader);
        preheader.CopyFlags(exprBb, BBF_COPY_PROPAGATE);
        fgInsertStmtAtEnd(preheader, fgNewStmtFromTree(hoist));
#if DEBUG
        if (verbose)
        {
            jitprintf($"This hoisted copy placed in PreHeader ({FMT_BB(preheader.bbNum)}):\n");
            gtDispTree(hoist);
            jitprintf("\n");
        }

        if (_nodeTestData is not null && _nodeTestData.TryGetValue(origExpr, out var annotation) &&
            annotation._tl is TL_LoopHoist)
        {
            var depth = loop.GetDepth();
            if (annotation._num == -1)
            {
                jitprintf("Node ");
                printTreeId(origExpr);
                jitprintf(" was declared 'do not hoist', but is being hoisted.\n");
                throw new FatalJitException("A forbidden test expression was hoisted.");
            }
            if (annotation._num != depth)
            {
                jitprintf("Node ");
                printTreeId(origExpr);
                jitprintf($" was declared as hoistable from loop at nesting depth {annotation._num}; " +
                    $"actually hoisted from loop at depth {depth}.\n");
                throw new FatalJitException("Loop hoist violated a node test annotation.");
            }

            _nodeTestData.Remove(origExpr);
            annotation._tl = TL_CSE_Def;
            annotation._num = _loopHoistCSEClass++;
            _nodeTestData.Add(hoistExpr, annotation);
        }
#endif
#if LOOP_HOIST_STATS
        if (!_curLoopHasHoistedExpression)
        {
            _loopsWithHoistedExpressions++;
            _curLoopHasHoistedExpression = true;
        }
        _totalHoistedExpressions++;
#endif
    }
}
