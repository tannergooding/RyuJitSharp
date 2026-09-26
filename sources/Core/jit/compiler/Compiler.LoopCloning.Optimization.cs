// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Compiler
{
#if DEBUG
    private void optDebugLogLoopCloning(BasicBlock block, Statement insertBefore)
    {
        if (JitConfig.JitDebugLogLoopCloning == 0)
        {
            return;
        }
        var call = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_DEBUG_LOG_LOOP_CLONING);
        var stmt = fgNewStmtFromTree(call);
        fgInsertStmtBefore(block, insertBefore, stmt);
        _ = fgMorphBlockStmt(block, stmt, message: "Debug log loop cloning");
    }
#endif

    private void optPerformStaticOptimizations(FlowGraphNaturalLoop loop,
        LoopCloneContext context, bool dynamicPath)
    {
        var options = context.GetLoopOptInfo(loop.Index)!;
        foreach (var option in options)
        {
            switch (option)
            {
                case LcJaggedArrayOptInfo array:
                {
                    var block = array.ArrIndex.UseBlock
                        ?? throw new FatalJitException("Array candidate has no use block.");
                    compCurBB = block;
                    for (var dim = 0; dim <= array.Dim; dim++)
                    {
                        var comma = array.ArrIndex.BndsChks[dim];
#if DEBUG
                        if (verbose)
                        {
                            jitprintf($"Remove bounds check [{comma.AsOp().Op1.TreeId:D6}] " +
                                $"for {FMT_STMT(array.Stmt.Id)}, dim {dim}, ");
                            array.ArrIndex.Print();
                            jitprintf(", bounds check nodes: ");
                            array.ArrIndex.PrintBoundsCheckNodes();
                            jitprintf("\n");
                        }
#endif
                        if (comma.AsOp().Op1.Oper is GT_BOUNDS_CHECK)
                        {
                            _ = optRemoveRangeCheck(comma.AsOp().Op1.AsBoundsChk(), comma, array.Stmt);
                        }
                        else
                        {
                            JITDUMP("  Bounds check already removed\n");
                            assert(comma.AsOp().Op1.Oper is GT_NOP);
                        }
                    }
#if DEBUG
                    if (dynamicPath)
                    {
                        optDebugLogLoopCloning(block, array.Stmt);
                    }
#endif
                    break;
                }
                case LcSpanOptInfo span:
                {
                    var block = span.SpanIndex.UseBlock
                        ?? throw new FatalJitException("Span candidate has no use block.");
                    var comma = span.SpanIndex.BndsChk
                        ?? throw new FatalJitException("Span candidate has no bounds check.");
                    compCurBB = block;
#if DEBUG
                    if (verbose)
                    {
                        jitprintf($"Remove bounds check [{comma.AsOp().Op1.TreeId:D6}] " +
                            $"for {FMT_STMT(span.Stmt.Id)}, ");
                        span.SpanIndex.Print();
                        jitprintf(", bounds check nodes: ");
                        span.SpanIndex.PrintBoundsCheckNode();
                        jitprintf("\n");
                    }
#endif
                    if (comma.AsOp().Op1.Oper is GT_BOUNDS_CHECK)
                    {
                        _ = optRemoveRangeCheck(comma.AsOp().Op1.AsBoundsChk(), comma, span.Stmt);
                    }
                    else
                    {
                        JITDUMP("  Bounds check already removed\n");
                        assert(comma.AsOp().Op1.Oper is GT_NOP);
                    }
#if DEBUG
                    if (dynamicPath)
                    {
                        optDebugLogLoopCloning(block, span.Stmt);
                    }
#endif
                    break;
                }
                case LcMdArrayOptInfo:
                {
                    break;
                }
                case LcTypeTestOptInfo type:
                {
                    JITDUMP("Updating flags on GDV guard inside hot loop. Before:\n");
#if DEBUG
                    if (verbose)
                    {
                        gtDispStmt(type.Stmt);
                    }
#endif
                    optMarkLoopCloningGuard(type.Stmt, type.MethodTableIndir);
                    JITDUMP("After:\n");
#if DEBUG
                    if (verbose)
                    {
                        gtDispStmt(type.Stmt);
                    }
#endif
                    break;
                }
                case LcMethodAddrTestOptInfo method:
                {
                    JITDUMP("Updating flags on GDV guard inside hot loop. Before:\n");
#if DEBUG
                    if (verbose)
                    {
                        gtDispStmt(method.Stmt);
                    }
#endif
                    optMarkLoopCloningGuard(method.Stmt, method.DelegateAddressIndir);
                    JITDUMP("After:\n");
#if DEBUG
                    if (verbose)
                    {
                        gtDispStmt(method.Stmt);
                    }
#endif
                    break;
                }
                default:
                {
                    throw new FatalJitException("Unknown loop-cloning optimization.");
                }
            }
        }
    }

    private void optMarkLoopCloningGuard(Statement stmt, GenTreeIndir indir)
    {
        indir.Flags |= GTF_IND_NONFAULTING;
        indir.HasOrderingSideEffect = true;
        indir.Flags &= ~GTF_EXCEPT;
        assert(fgNodeThreading is NodeThreading.None);
        gtUpdateStmtSideEffects(stmt);
    }
}
