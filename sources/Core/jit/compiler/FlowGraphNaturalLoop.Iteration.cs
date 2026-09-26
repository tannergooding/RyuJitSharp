// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;
using static RyuJitSharp.Compiler;

namespace RyuJitSharp;

public sealed partial class FlowGraphNaturalLoop
{
    private struct DefVisitor : IGenTreeVisitor<DefVisitor>, ILocalDefVisitor
    {
        public static bool DoPreOrder => true;

        private readonly Compiler _compiler;
        private readonly Func<int, GenTreeLclVarCommon, bool> _visit;
        private readonly GenTreeStack _ancestors;

        public DefVisitor(Compiler compiler, Func<int, GenTreeLclVarCommon, bool> visit)
        {
            _compiler = compiler;
            _visit = visit;
            _ancestors = [];
        }

        public fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if ((use.Flags & GTF_ASG) == 0)
            {
                return WALK_SKIP_SUBTREES;
            }
            return use.VisitLogicalLocalDefs(_compiler, ref this) is GenTree.VisitResult.Abort
                ? WALK_ABORT : WALK_CONTINUE;
        }

        public readonly GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
            => _visit(def.LclNum, def.DefNode) ? GenTree.VisitResult.Continue : GenTree.VisitResult.Abort;

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<DefVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private bool VisitDefs(Func<int, GenTreeLclVarCommon, bool> visit)
    {
        var visitor = new DefVisitor(_dfsTree.GetCompiler(), visit);
        return VisitLoopBlocks(block => {
            foreach (var stmt in block.Statements)
            {
                var tree = stmt.RootNode;
                if (visitor.WalkTree(ref tree, null) is WALK_ABORT)
                {
                    return BasicBlockVisit.Abort;
                }
            }
            return BasicBlockVisit.Continue;
        }) is BasicBlockVisit.Continue;
    }

    private GenTreeLclVarCommon? FindDef(int localNumber)
    {
        var compiler = _dfsTree.GetCompiler();
        assert(!compiler.lvaGetDesc(localNumber).lvPromoted);
        GenTreeLclVarCommon? result = null;
        _ = VisitDefs((number, node) => {
            if (number == localNumber)
            {
                result = node;
                return false;
            }
            return true;
        });
        return result;
    }

    public bool AnalyzeIteration(out NaturalLoopIterInfo info, bool allowMissingBaseCase = false)
    {
        info = new NaturalLoopIterInfo();
        JITDUMP($"Analyzing iteration for L{_index:D2} with header {FMT_BB(_header.bbNum)}\n");
        var compiler = _dfsTree.GetCompiler();
        assert(_entryEdges.Count == 1);
        var preheader = _entryEdges[0].SourceBlock;
        JITDUMP($"  Preheader = {FMT_BB(preheader.bbNum)}\n");
        GenTree? test = null;

        info.IterVar = BAD_VAR_NUM;
        info.NeedsZeroTripGuard = false;
        foreach (var exitEdge in _exitEdges)
        {
            var cond = exitEdge.SourceBlock;
            JITDUMP($"  Checking exiting block {FMT_BB(cond.bbNum)}\n");
            if (cond.Kind is not BBJ_COND)
            {
                JITDUMP("    Not a BBJ_COND\n");
                continue;
            }
            if (!compiler.optExtractTestIncr(cond, out test, out var iterTree))
            {
                JITDUMP("    Could not extract an IV\n");
                continue;
            }

            assert(iterTree is not null && test is not null);
            var iterVar = compiler.optIsLoopIncrTree(iterTree);
            assert(iterVar != BAD_VAR_NUM);
            ref var iterVarDsc = ref compiler.lvaGetDesc(iterVar);
            if (iterVarDsc.lvIsStructField)
            {
                JITDUMP($"    iterVar V{iterVar:D2} is a promoted field\n");
                continue;
            }
            if (iterVarDsc.IsAddressExposed)
            {
                JITDUMP($"    iterVar V{iterVar:D2} is address exposed\n");
                continue;
            }
            if (!MatchLimit(iterVar, test, info))
            {
                continue;
            }
            if (!VisitDefs((number, node) => {
                if ((number == iterVar) && (node != iterTree))
                {
#if DEBUG
                    JITDUMP($"    Loop has extraneous def [{node.TreeId:D6}]\n");
#endif
                    return false;
                }
                return true;
            }))
            {
                continue;
            }

            info.TestBlock = cond;
            info.IterVar = iterVar;
            info.IterTree = iterTree;
            info.ExitedOnTrue = exitEdge.DestinationBlock == cond.TrueTarget;
            break;
        }

        if (info.IterVar == BAD_VAR_NUM)
        {
            JITDUMP("  Could not find any IV\n");
            return false;
        }

        if (FindConstInit(preheader, info))
        {
#if DEBUG
            JITDUMP($"  Init = [{info.InitTree?.TreeId:D6}], test = [{test?.TreeId:D6}], incr = [{info.IterTree?.TreeId:D6}]\n");
#endif
        }
        else
        {
#if DEBUG
            JITDUMP($"  Init = <none>, test = [{test?.TreeId:D6}], incr = [{info.IterTree?.TreeId:D6}]\n");
#endif
        }

        if (!CheckLoopConditionBaseCase(preheader, info))
        {
            if (allowMissingBaseCase &&
                (info.HasConstLimit || info.HasInvariantLocalLimit || info.HasArrayLengthLimit))
            {
                JITDUMP("  Loop condition may not be true on the first iteration; deferring to caller (NeedsZeroTripGuard)\n");
                info.NeedsZeroTripGuard = true;
            }
            else
            {
                JITDUMP("  Loop condition may not be true on the first iteration\n");
                return false;
            }
        }

#if DEBUG
        if (compiler.verbose)
        {
            jitprintf($"  IterVar = V{info.IterVar:D2}\n");
            if (info.HasConstInit)
            {
                jitprintf($"  Const init with value {info.ConstInitValue} (at [{info.InitTree?.TreeId:D6}])\n");
            }
            jitprintf($"  Test is [{info.TestTree?.TreeId:D6}] (");
            if (info.HasConstLimit)
            {
                jitprintf("const limit ");
            }
            if (info.HasSimdLimit)
            {
                jitprintf("simd limit ");
            }
            if (info.HasInvariantLocalLimit)
            {
                jitprintf("invariant local limit ");
            }
            if (info.HasArrayLengthLimit)
            {
                jitprintf("array length limit ");
            }
            if (info.LimitOffset != 0)
            {
                jitprintf($"offset {info.LimitOffset} ");
            }
            jitprintf(")\n");
        }
#endif
        return true;
    }

    private bool MatchLimit(int iterVar, GenTree test, NaturalLoopIterInfo info)
    {
        info.HasConstLimit = false;
        info.HasSimdLimit = false;
        info.HasArrayLengthLimit = false;
        info.HasInvariantLocalLimit = false;
        info.LimitOffset = 0;
        info.LimitVar = BAD_VAR_NUM;

        var compiler = _dfsTree.GetCompiler();
        var relop = test.Oper is GT_JTRUE ? test.AsUnOp().Op1 : test.AsLclVarCommon().Data;
        noway_assert(relop.Oper.IsCompare);
        var operands = relop.AsOp();
        GenTree iterOp;
        GenTree limitOp;

        if (operands.Op1.Oper.IsScalarLocal && operands.Op1.AsLclVarCommon().LclNum == iterVar)
        {
            iterOp = operands.Op1;
            limitOp = operands.Op2;
        }
        else if (operands.Op2.Oper.IsScalarLocal && operands.Op2.AsLclVarCommon().LclNum == iterVar)
        {
            iterOp = operands.Op2;
            limitOp = operands.Op1;
        }
        else
        {
            return false;
        }

        if (iterOp.Type is not TYP_INT)
        {
            return false;
        }

        var peeledOffset = 0;
        GenTree? peeledBase = null;
        if (limitOp.Oper is GT_ADD or GT_SUB && limitOp.Type is TYP_INT)
        {
            var lop1 = limitOp.AsOp().Op1;
            var lop2 = limitOp.AsOp().Op2;
            if (lop2.Oper.IsCnsIntOrI && lop2.Type is TYP_INT && !lop1.Oper.IsCnsIntOrI)
            {
                var constant = lop2.AsIntCon().IconValue;
                if (constant >= int.MinValue && constant <= int.MaxValue)
                {
                    var signedConstant = (int)constant;
                    if (limitOp.Oper is GT_SUB)
                    {
                        if (signedConstant != int.MinValue)
                        {
                            peeledOffset = -signedConstant;
                            peeledBase = lop1;
                        }
                    }
                    else
                    {
                        peeledOffset = signedConstant;
                        peeledBase = lop1;
                    }
                }
            }
        }

        if (peeledBase is not null && peeledOffset != 0)
        {
            limitOp = peeledBase;
            info.LimitOffset = peeledOffset;
        }

        if (limitOp.Oper.IsCnsIntOrI)
        {
            info.HasConstLimit = true;
            info.HasSimdLimit = (limitOp.Flags & GTF_ICON_SIMD_COUNT) != 0;
        }
        else if (limitOp.Oper is GT_LCL_VAR)
        {
            var local = limitOp.AsLclVarCommon();
            if (compiler.lvaGetDesc(local.LclNum).IsAddressExposed)
            {
                JITDUMP($"    Limit var V{local.LclNum:D2} is address exposed\n");
                return false;
            }
            var definition = FindDef(local.LclNum);
            if (definition is not null)
            {
#if DEBUG
                JITDUMP($"    Limit var V{local.LclNum:D2} modified by [{definition.TreeId:D6}]\n");
#endif
                return false;
            }
            info.HasInvariantLocalLimit = true;
            info.LimitVar = local.LclNum;
        }
        else if (limitOp.Oper is GT_ARR_LENGTH)
        {
            var array = limitOp.AsArrLen().ArrRef;
            if (array.Oper is not GT_LCL_VAR)
            {
#if DEBUG
                JITDUMP($"    Array limit tree [{limitOp.TreeId:D6}] not analyzable\n");
#endif
                return false;
            }
            var local = array.AsLclVarCommon();
            if (compiler.lvaGetDesc(local.LclNum).IsAddressExposed)
            {
                JITDUMP($"    Array base local V{local.LclNum:D2} is address exposed\n");
                return false;
            }
            var definition = FindDef(local.LclNum);
            if (definition is not null)
            {
#if DEBUG
                JITDUMP($"    Array limit var V{local.LclNum:D2} modified by [{definition.TreeId:D6}]\n");
#endif
                return false;
            }
            info.HasArrayLengthLimit = true;
            info.LimitVar = local.LclNum;
        }
        else
        {
#if DEBUG
            JITDUMP($"    Loop limit tree [{limitOp.TreeId:D6}] not analyzable\n");
#endif
            return false;
        }

        assert(info.HasConstLimit || info.HasInvariantLocalLimit || info.HasArrayLengthLimit);
        info.TestTree = relop;
        return true;
    }
}
