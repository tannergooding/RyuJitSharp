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
    private void optDebugLogLoopCloning(BasicBlock block, Statement insertBefore)
    {
        if (!Configuration.DEBUG || (JitConfig.JitDebugLogLoopCloning == 0))
        {
            return;
        }
        var call = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_DEBUG_LOG_LOOP_CLONING);
        var stmt = fgNewStmtFromTree(call);
        fgInsertStmtBefore(block, insertBefore, stmt);
        _ = fgMorphBlockStmt(block, stmt, message: "Debug log loop cloning");
    }

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
                    var block = array.ArrIndex.UseBlock;
                    if (block is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage144
                        );
                        return;
                    }
                    compCurBB = block;
                    for (var dim = 0; dim <= array.Dim; dim++)
                    {
                        var comma = array.ArrIndex.BndsChks[dim];
                        if (Configuration.DEBUG && verbose)
                        {
                            jitprintf($"Remove bounds check [{comma.AsOp().Op1.TreeId:D6}] " +
                                $"for {FMT_STMT(array.Stmt.Id)}, dim {dim}, ");
                            array.ArrIndex.Print();
                            jitprintf(", bounds check nodes: ");
                            array.ArrIndex.PrintBoundsCheckNodes();
                            jitprintf("\n");
                        }
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
                    if (Configuration.DEBUG && dynamicPath)
                    {
                        optDebugLogLoopCloning(block, array.Stmt);
                    }
                    break;
                }
                case LcSpanOptInfo span:
                {
                    var block = span.SpanIndex.UseBlock;
                    if (block is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage148
                        );
                        return;
                    }
                    var comma = span.SpanIndex.BndsChk;
                    if (comma is null)
                    {
                        FatalJitException.ThrowFatalJitException(
                            CORJIT_INTERNALERROR,
                            FatalJitErrorCode.DirectFailureMessage149
                        );
                        return;
                    }
                    compCurBB = block;
                    if (Configuration.DEBUG && verbose)
                    {
                        jitprintf($"Remove bounds check [{comma.AsOp().Op1.TreeId:D6}] " +
                            $"for {FMT_STMT(span.Stmt.Id)}, ");
                        span.SpanIndex.Print();
                        jitprintf(", bounds check nodes: ");
                        span.SpanIndex.PrintBoundsCheckNode();
                        jitprintf("\n");
                    }
                    if (comma.AsOp().Op1.Oper is GT_BOUNDS_CHECK)
                    {
                        _ = optRemoveRangeCheck(comma.AsOp().Op1.AsBoundsChk(), comma, span.Stmt);
                    }
                    else
                    {
                        JITDUMP("  Bounds check already removed\n");
                        assert(comma.AsOp().Op1.Oper is GT_NOP);
                    }
                    if (Configuration.DEBUG && dynamicPath)
                    {
                        optDebugLogLoopCloning(block, span.Stmt);
                    }
                    break;
                }
                case LcMdArrayOptInfo:
                {
                    break;
                }
                case LcTypeTestOptInfo type:
                {
                    JITDUMP("Updating flags on GDV guard inside hot loop. Before:\n");
                    if (Configuration.DEBUG && verbose)
                    {
                        gtDispStmt(type.Stmt);
                    }
                    optMarkLoopCloningGuard(type.Stmt, type.MethodTableIndir);
                    JITDUMP("After:\n");
                    if (Configuration.DEBUG && verbose)
                    {
                        gtDispStmt(type.Stmt);
                    }
                    break;
                }
                case LcMethodAddrTestOptInfo method:
                {
                    JITDUMP("Updating flags on GDV guard inside hot loop. Before:\n");
                    if (Configuration.DEBUG && verbose)
                    {
                        gtDispStmt(method.Stmt);
                    }
                    optMarkLoopCloningGuard(method.Stmt, method.DelegateAddressIndir);
                    JITDUMP("After:\n");
                    if (Configuration.DEBUG && verbose)
                    {
                        gtDispStmt(method.Stmt);
                    }
                    break;
                }
                default:
                {
                    FatalJitException.ThrowFatalJitException(
                        CORJIT_INTERNALERROR,
                        FatalJitErrorCode.DirectFailureMessage150
                    );
                    return;
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
