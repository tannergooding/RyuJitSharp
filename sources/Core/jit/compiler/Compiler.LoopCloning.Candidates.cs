// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool optIsStackLocalInvariant(FlowGraphNaturalLoop loop, int lclNum)
    {
        if (lvaGetDesc(lclNum).IsAddressExposed)
        {
            return false;
        }

        return !loop.HasDef(lclNum);
    }

    private bool optExtractArrIndex(GenTree tree, ArrIndex result, int lhsNum, out bool topLevelIsFinal)
    {
        topLevelIsFinal = false;
        if (tree.Oper is not GT_COMMA || tree.AsOp().Op1.Oper is not GT_BOUNDS_CHECK)
        {
            return false;
        }

        var check = tree.AsOp().Op1.AsBoundsChk();
        if (check.Index.Oper is not GT_LCL_VAR ||
            check.ArrayLength.Oper is GT_LCL_VAR or GT_LCL_FLD or GT_CNS_INT ||
            check.ArrayLength.AsUnOp().Op1.Oper is not GT_LCL_VAR)
        {
            return false;
        }

        var arr = check.ArrayLength.AsUnOp().Op1.AsLclVarCommon();
        var arrLcl = arr.LclNum;
        if (lhsNum != BAD_VAR_NUM && arrLcl != lhsNum)
        {
            return false;
        }

        if (lhsNum == BAD_VAR_NUM)
        {
            result.ArrLcl = arrLcl;
            result.ArrType = arr.Type;
        }

        result.IndLcls.Add(check.Index.AsLclVarCommon().LclNum);
        result.BndsChks.Add(tree);
        result.UseBlock = compCurBB;
        result.Rank++;
        assert(check.InxType is not TYP_VOID);
        topLevelIsFinal = check.InxType is not TYP_REF;
        return true;
    }

    private bool optExtractSpanIndex(GenTree tree, SpanIndex result)
    {
        if (tree.Oper is not GT_COMMA || tree.AsOp().Op1.Oper is not GT_BOUNDS_CHECK)
        {
            return false;
        }

        var check = tree.AsOp().Op1.AsBoundsChk();
        if (check.Index.Oper is not GT_LCL_VAR || check.ArrayLength.Oper is not GT_LCL_VAR)
        {
            return false;
        }

        result.LenLcl = check.ArrayLength.AsLclVarCommon().LclNum;
        result.IndLcl = check.Index.AsLclVarCommon().LclNum;
        result.BndsChk = tree;
        result.UseBlock = compCurBB;
        return true;
    }

    private bool optReconstructArrIndexHelp(GenTree tree, ArrIndex result, int lhsNum, out bool topLevelIsFinal)
    {
        if (optExtractArrIndex(tree, result, lhsNum, out topLevelIsFinal))
        {
            return true;
        }

        if (tree.Oper is GT_COMMA)
        {
            var before = tree.AsOp().Op1;
            if (before.Oper is not GT_STORE_LCL_VAR ||
                !optReconstructArrIndexHelp(before.AsLclVar().Data!, result, lhsNum, out topLevelIsFinal))
            {
                return false;
            }

            if (topLevelIsFinal)
            {
                return false;
            }

            return optExtractArrIndex(tree.AsOp().Op2, result,
                before.AsLclVarCommon().LclNum, out topLevelIsFinal);
        }

        topLevelIsFinal = false;
        return false;
    }

    private bool optReconstructArrIndex(GenTree tree, ArrIndex result)
        => optReconstructArrIndexHelp(tree, result, BAD_VAR_NUM, out _);

    private bool optArrLenLimit(NaturalLoopIterInfo iterInfo, ArrIndex index)
    {
        assert(iterInfo.HasArrayLengthLimit);
        var limit = iterInfo.LimitBase();
        assert(limit.Oper is GT_ARR_LENGTH);
        var arrRef = limit.AsArrCommon().ArrRef;
        if (arrRef.Oper is GT_LCL_VAR)
        {
            index.ArrLcl = arrRef.AsLclVarCommon().LclNum;
            index.ArrType = arrRef.Type;
            index.Rank = 0;
            return true;
        }
        if (arrRef.Oper is GT_COMMA)
        {
            return optReconstructArrIndex(arrRef, index);
        }
        return false;
    }

    private static bool optIsHandleOrIndirOfHandle(GenTree tree, GenTreeFlags handleType)
    {
        var handle = tree.Oper is GT_IND ? tree.AsIndir().Addr : tree;
        return handle.Oper.IsCnsIntOrI && handle.AsIntCon().IsIconHandle(handleType);
    }

    private bool optCheckLoopCloningGDVTestProfitable(GenTreeOp guard, FlowGraphNaturalLoop loop)
    {
        JITDUMP("Checking whether cloning is profitable ...\n");
        var block = compCurBB ?? throw new FatalJitException("Loop-cloning visitor requires a current block.");
        if (!loop.Header.hasProfileWeight || !block.hasProfileWeight)
        {
            JITDUMP("  No; loop does not have profile data.\n");
            return false;
        }
        if (loop.Header.getBBWeight(this) < 0.5 * BB_UNITY_WEIGHT)
        {
            JITDUMP("  No; loop does not iterate often enough.\n");
            return false;
        }
        if (block.bbWeight < 0.5 * loop.Header.bbWeight)
        {
            JITDUMP("  No; guard does not execute often enough within the loop.\n");
            return false;
        }

        var hot = guard.Oper is GT_EQ ? block.TrueTarget : block.FalseTarget;
        var cold = guard.Oper is GT_EQ ? block.FalseTarget : block.TrueTarget;
        if (!hot.hasProfileWeight || !cold.hasProfileWeight)
        {
            JITDUMP("  No; guard successor blocks were not profiled.\n");
            return false;
        }
        if (hot.bbWeight == BB_ZERO_WEIGHT)
        {
            JITDUMP($"  No; guard hot successor block {FMT_BB(hot.bbNum)} is rarely run.\n");
            return false;
        }
        if (cold.bbWeight > BB_ZERO_WEIGHT)
        {
            var bias = cold.bbWeight / (hot.bbWeight + cold.bbWeight);
            if (bias > 0.05)
            {
                JITDUMP($"  No; guard not sufficiently biased: failure likelihood is " +
                    $"{FMT_WT(bias)} > {FMT_WT(0.05)}\n");
                return false;
            }
        }

        JITDUMP("  Yes\n");
        return true;
    }

    private struct LoopCloneVisitor : IGenTreeVisitor<LoopCloneVisitor>
    {
        public static bool DoPreOrder => true;
        public static bool UseExecutionOrder => true;

        private readonly Compiler _compiler;
        private readonly FlowGraphNaturalLoop _loop;
        private readonly LoopCloneContext _context;
        private readonly bool _arrayBounds;
        private readonly bool _gdvTests;
        private readonly GenTreeStack _ancestors;
        public Statement? Stmt;

        public LoopCloneVisitor(Compiler compiler, FlowGraphNaturalLoop loop, LoopCloneContext context,
            bool arrayBounds, bool gdvTests)
        {
            _compiler = compiler;
            _loop = loop;
            _context = context;
            _arrayBounds = arrayBounds;
            _gdvTests = gdvTests;
            _ancestors = [];
            Stmt = null;
        }

        public readonly fgWalkResult PreOrderVisit(ref GenTree use, GenTree? user)
        {
            if (Stmt is not Statement stmt)
            {
                throw new FatalJitException("Loop-cloning visitor requires a current statement.");
            }
            return _compiler.optCanOptimizeByLoopCloning(use, _loop, _context, stmt, _arrayBounds, _gdvTests);
        }

        public readonly fgWalkResult PostOrderVisit(ref GenTree use, GenTree? user) => WALK_CONTINUE;

        public fgWalkResult WalkTree(ref GenTree use, GenTree? user)
            => IGenTreeVisitor<LoopCloneVisitor>.WalkTree(ref this, ref use, user, _ancestors);
    }

    private unsafe fgWalkResult optCanOptimizeByLoopCloning(GenTree tree, FlowGraphNaturalLoop loop,
        LoopCloneContext context, Statement stmt, bool arrayBounds, bool gdvTests)
    {
        var block = compCurBB ?? throw new FatalJitException("Loop-cloning visitor requires a current block.");
        var arrIndex = new ArrIndex();
        if (arrayBounds && optReconstructArrIndex(tree, arrIndex))
        {
#if DEBUG
            if (verbose)
            {
                var useBlock = arrIndex.UseBlock
                    ?? throw new FatalJitException("Array candidate has no use block.");
                jitprintf($"Found ArrIndex at {FMT_BB(useBlock.bbNum)} {FMT_STMT(stmt.Id)} tree " +
                    $"[{tree.TreeId:D6}] which is equivalent to: ");
                arrIndex.Print();
                jitprintf(", bounds check nodes: ");
                arrIndex.PrintBoundsCheckNodes();
                jitprintf("\n");
            }
#endif
            if (!optIsStackLocalInvariant(loop, arrIndex.ArrLcl))
            {
                JITDUMP($"V{arrIndex.ArrLcl:D2} is not loop invariant\n");
                return WALK_SKIP_SUBTREES;
            }

            var iterInfo = context.GetLoopIterInfo(loop.Index)!;
            for (var dim = 0; dim < arrIndex.Rank; dim++)
            {
                if (arrIndex.IndLcls[dim] != iterInfo.IterVar)
                {
                    JITDUMP($"Induction V{iterInfo.IterVar:D2} is not used as index on dim {dim}\n");
                    continue;
                }
                for (var previous = 0; previous < dim; previous++)
                {
                    if (!optIsStackLocalInvariant(loop, arrIndex.IndLcls[previous]))
                    {
                        JITDUMP($"V{arrIndex.IndLcls[previous]:D2} is assigned in loop\n");
                        return WALK_SKIP_SUBTREES;
                    }
                }
#if DEBUG
                if (verbose)
                {
                    jitprintf($"Loop L{loop.Index:D2} can be cloned for ArrIndex ");
                    arrIndex.Print();
                    jitprintf($" on dim {dim}\n");
                }
#endif
                context.EnsureLoopOptInfo(loop.Index).Add(new LcJaggedArrayOptInfo(arrIndex, dim, stmt));
            }
            return WALK_SKIP_SUBTREES;
        }

        var spanIndex = new SpanIndex();
        if (arrayBounds && optExtractSpanIndex(tree, spanIndex))
        {
            if (!optIsStackLocalInvariant(loop, spanIndex.LenLcl))
            {
                JITDUMP($"Span.Length V{spanIndex.LenLcl:D2} is not loop invariant\n");
                return WALK_SKIP_SUBTREES;
            }
            if (spanIndex.IndLcl == context.GetLoopIterInfo(loop.Index)!.IterVar)
            {
                context.EnsureLoopOptInfo(loop.Index).Add(new LcSpanOptInfo(spanIndex, stmt));
            }
            else
            {
                JITDUMP($"Induction V{context.GetLoopIterInfo(loop.Index)!.IterVar:D2} is not used as index\n");
            }
            return WALK_SKIP_SUBTREES;
        }

        if (!gdvTests || tree.Oper is not GT_JTRUE)
        {
            return WALK_CONTINUE;
        }

#if DEBUG
        JITDUMP($"...GDV considering [{tree.TreeId:D6}]\n");
#endif
        assert(stmt.RootNode == tree);
        var relop = tree.AsUnOp().Op1;
        if (relop.Oper is not GT_EQ and not GT_NE)
        {
            return WALK_CONTINUE;
        }
        var left = relop.AsOp().Op1;
        var right = relop.AsOp().Op2;
        if (optIsHandleOrIndirOfHandle(left, GTF_ICON_CLASS_HDL) ||
            optIsHandleOrIndirOfHandle(left, GTF_ICON_FTN_ADDR))
        {
            (left, right) = (right, left);
        }
        if (left.Oper is not GT_IND || left.Type is not TYP_I_IMPL and not TYP_REF and not TYP_BYREF)
        {
            return WALK_CONTINUE;
        }

        var indir = left.AsIndir();
        var addr = indir.Addr;
        if (right.Oper.IsCnsIntOrI && right.AsIntCon().IsIconHandle(GTF_ICON_CLASS_HDL))
        {
            if (addr.Type is not TYP_REF || addr.Oper is not GT_LCL_VAR)
            {
                return WALK_CONTINUE;
            }
            var lclNum = addr.AsLclVarCommon().LclNum;
            JITDUMP($"... right form for type test with local V{lclNum:D2}\n");
            if (!optIsStackLocalInvariant(loop, lclNum))
            {
                JITDUMP("... but not invariant\n");
                return WALK_CONTINUE;
            }
#if DEBUG
            JITDUMP($"Loop L{loop.Index:D2} has invariant type test [{tree.TreeId:D6}] on V{lclNum:D2}\n");
#endif
            if (optCheckLoopCloningGDVTestProfitable(relop.AsOp(), loop))
            {
                assert(block.LastStmt == stmt);
                context.EnsureLoopOptInfo(loop.Index).Add(new LcTypeTestOptInfo(
                    block, stmt, indir, lclNum, (CORINFO_CLASS_HANDLE)right.AsIntCon().IconValue));
            }
        }
        else if (optIsHandleOrIndirOfHandle(right, GTF_ICON_FTN_ADDR))
        {
            nint offset = 0;
            if (addr.Oper is GT_ADD)
            {
                var op = addr.AsOp();
                if (!op.Op2.Oper.IsCnsIntOrI || op.Op2.Type is not TYP_I_IMPL || op.Op2.IsIconHandle())
                {
                    return WALK_CONTINUE;
                }
                offset = op.Op2.AsIntCon().IconValue;
                addr = op.Op1;
            }
            if (addr.Type is not TYP_REF || addr.Oper is not GT_LCL_VAR ||
                offset != (nint)eeGetEEInfo().offsetOfDelegateFirstTarget)
            {
                return WALK_CONTINUE;
            }
            var lclNum = addr.AsLclVarCommon().LclNum;
            JITDUMP($"... right form for method address test with local V{lclNum:D2}\n");
            var descriptor = lvaGetDesc(lclNum);
            if (descriptor.lvClassHnd == NO_CLASS_HANDLE)
            {
                JITDUMP("... but no class handle available for local\n");
                return WALK_CONTINUE;
            }
            if ((info.compCompHnd->getClassAttribs(descriptor.lvClassHnd) & CORINFO_FLG_DELEGATE) == 0)
            {
                JITDUMP("... but not a delegate instance\n");
                return WALK_CONTINUE;
            }
            if (!optIsStackLocalInvariant(loop, lclNum))
            {
                JITDUMP("... but not invariant\n");
                return WALK_CONTINUE;
            }
#if DEBUG
            JITDUMP($"Loop L{loop.Index:D2} has invariant method address test [{tree.TreeId:D6}] on V{lclNum:D2}\n");
#endif
            if (optCheckLoopCloningGDVTestProfitable(relop.AsOp(), loop))
            {
                var handle = right.IsIconHandle() ? right.AsIntCon() : right.AsIndir().Addr.AsIntCon();
                assert(handle.IsIconHandle(GTF_ICON_FTN_ADDR));
                assert(block.LastStmt == stmt);
                context.EnsureLoopOptInfo(loop.Index).Add(new LcMethodAddrTestOptInfo(
                    block, stmt, indir, lclNum, (void*)handle.IconValue, right != handle
#if DEBUG
                    , (CORINFO_METHOD_HANDLE)handle.TargetHandle
#endif
                ));
            }
        }
        return WALK_CONTINUE;
    }

    private bool optIdentifyLoopOptInfo(FlowGraphNaturalLoop loop, LoopCloneContext context)
    {
        var arrayBounds = (optMethodFlags & OMF_HAS_ARRAYREF) != 0 && context.GetLoopIterInfo(loop.Index) is not null;
        var gdvTests = MethodHasGuardedDevirtualization;
        if (!arrayBounds && !gdvTests)
        {
            JITDUMP($"Not checking loop L{loop.Index:D2} -- no array bounds or type tests in this method\n");
            return false;
        }
#if DEBUG
        gdvTests &= JitConfig.JitCloneLoopsWithGdvTests != 0;
#endif
        JITDUMP($"Checking loop L{loop.Index:D2} for optimization candidates" +
            $"{(arrayBounds ? " (array bounds)" : "")}{(gdvTests ? " (GDV tests)" : "")}\n");
        var visitor = new LoopCloneVisitor(this, loop, context, arrayBounds, gdvTests);
        _ = loop.VisitLoopBlocksReversePostOrder(block => {
            compCurBB = block;
            foreach (var stmt in block.Statements)
            {
                visitor.Stmt = stmt;
                _ = visitor.WalkTree(ref stmt.RootNodeRef, null);
            }
            return BasicBlockVisit.Continue;
        });
        return true;
    }

    private bool optObtainLoopCloningOpts(LoopCloneContext context)
    {
        var result = false;
        foreach (var loop in _loops!.InReversePostOrder())
        {
            JITDUMP($"Considering loop L{loop.Index:D2} to clone for optimizations.\n");
            if (loop.AnalyzeIteration(out var iterInfo, allowMissingBaseCase: true))
            {
                context.SetLoopIterInfo(loop.Index, iterInfo);
            }
            if (optIsLoopClonable(loop, context) && optIdentifyLoopOptInfo(loop, context))
            {
                result = true;
            }
            JITDUMP("------------------------------------------------------------\n");
        }
        JITDUMP("\n");
        return result;
    }

    private static bool optLoopCloningEnabled()
    {
#if DEBUG
        return JitConfig.JitCloneLoops != 0;
#else
        return true;
#endif
    }
}
