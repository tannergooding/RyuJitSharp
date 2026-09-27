// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
//
// Based on the RyuJIT compiler from dotnet/runtime, flowgraph.cpp.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class GCPollInsertionTests
{
    private static int s_trapKind;
    private static int s_trapQueries;

    [TestCase(1)]
    [TestCase(2)]
    public static void InlinePollPreservesReturnAndBuildsNativeTrapLoad(int trapKind)
    {
        WithCompiler((compiler, block) => {
            s_trapKind = trapKind;
            var suppressed = Append(compiler, block, UnmanagedCall(compiler, true));
            var ret = Append(compiler, block, compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 7)));
            block.setBBProfileWeight(100);

            Assert.That(compiler.fgInsertGCPolls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(s_trapQueries, Is.EqualTo(1));
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(block.FirstStmt, Is.SameAs(suppressed));
            var poll = block.FalseTarget;
            var bottom = block.TrueTarget;
            Assert.That(block.Next, Is.SameAs(poll));
            Assert.That(poll.Next, Is.SameAs(bottom));
            Assert.That(poll.Target, Is.SameAs(bottom));
            Assert.That(bottom.FirstStmt, Is.SameAs(ret));
            Assert.That(bottom.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(block.TrueEdge.Likelihood, Is.EqualTo(1));
            Assert.That(block.FalseEdge.Likelihood, Is.Zero);
            Assert.That(poll.bbWeight, Is.Zero);
            Assert.That(bottom.bbWeight, Is.EqualTo(100));
            Assert.That(poll.FirstStmt!.RootNode.AsCall().HelperNum, Is.EqualTo(CORINFO_HELP_POLL_GC));
            Assert.That(bottom.HasFlag(BBF_GC_SAFE_POINT), Is.True);
            Assert.That(BasicBlock.sameEHRegion(block, bottom), Is.True);
            Assert.That(compiler.compCurBB, Is.SameAs(bottom));
            var check = block.LastStmt!.RootNode.AsUnOp().Op1.AsOp();
            Assert.That(check.Oper, Is.EqualTo(GT_EQ));
            Assert.That(check.Flags & (GTF_RELOP_JMP_USED | GTF_DONT_CSE),
                Is.EqualTo(GTF_RELOP_JMP_USED | GTF_DONT_CSE));
            var load = check.Op1.AsIndir();
            Assert.That(load.Type, Is.EqualTo(TYP_INT));
            Assert.That(load.Flags & GTF_IND_NONFAULTING, Is.Not.EqualTo(GTF_EMPTY));
            Assert.That(load.Addr.Oper, Is.EqualTo(trapKind == 1 ? GT_CNS_INT : GT_IND));
            Assert.That(check.Op2.AsIntCon().IconValue, Is.EqualTo((nint)0));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void CallPollKeepsGraphAndFollowingSequencePoint(int mode)
    {
        WithCompiler((compiler, block) => {
            s_trapKind = mode == 0 ? 0 : 1;
            _ = Append(compiler, block, UnmanagedCall(compiler, true));
            var ret = Append(compiler, block, compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 7)));
            ret.SetDebugInfo(new DebugInfo(null, new ILLocation(23, 0)));
            block.SetFlags(BBF_HAS_SUPPRESSGC_CALL);
            if (mode == 2)
            {
                compiler.genReturnBB = block;
            }
            if (mode == 3)
            {
                block.SetFlags(BBF_COLD);
            }

            Assert.That(compiler.fgInsertGCPolls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Blocks.Count(), Is.EqualTo(1));
            Assert.That(block.LastStmt, Is.SameAs(ret));
            var poll = ret.PrevStmt!;
            Assert.That(poll.RootNode.AsCall().HelperNum, Is.EqualTo(CORINFO_HELP_POLL_GC));
            Assert.That(poll.DebugInfo.Location, Is.EqualTo(ret.DebugInfo.Location));
            Assert.That(block.HasFlag(BBF_GC_SAFE_POINT), Is.True);
            Assert.That(s_trapQueries, Is.EqualTo(1));
        }, minOpts: mode == 1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TailCallRequirementPlacesCallPollBeforeArgumentSetup(bool minOpts)
    {
        WithCompiler((compiler, block) => {
            block.SetFlags(BBF_NEEDS_GCPOLL);
            compiler.genReturnBB = block;
            var setup = Append(compiler, block, compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 17)));
            var ret = Append(compiler, block, compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 0)));

            Assert.That(compiler.fgInsertGCPolls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt!.RootNode.AsCall().HelperNum, Is.EqualTo(CORINFO_HELP_POLL_GC));
            Assert.That(block.FirstStmt.NextStmt, Is.SameAs(setup));
            Assert.That(block.LastStmt, Is.SameAs(ret));
        }, minOpts);
    }

    [TestCase(false, false, false, false)]
    [TestCase(false, true, false, false)]
    [TestCase(false, true, true, true)]
    [TestCase(true, false, false, true)]
    [TestCase(true, true, false, true)]
    public static void SelectionUsesFlagsOnlyInMinOpts(bool suppressed, bool staleFlag, bool minOpts, bool expands)
    {
        WithCompiler((compiler, block) => {
            if (suppressed)
            {
                _ = Append(compiler, block, UnmanagedCall(compiler, true));
            }
            if (staleFlag)
            {
                block.SetFlags(BBF_HAS_SUPPRESSGC_CALL);
            }
            _ = Append(compiler, block, compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 0)));
            Assert.That(compiler.fgInsertGCPolls(), Is.EqualTo(expands
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_trapQueries, Is.EqualTo(expands ? 1 : 0));
        }, minOpts);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RegularUnmanagedCallsProvideAnImplicitPoll(bool regularFirst)
    {
        WithCompiler((compiler, block) => {
            _ = Append(compiler, block, UnmanagedCall(compiler, !regularFirst));
            _ = Append(compiler, block, UnmanagedCall(compiler, regularFirst));
            _ = Append(compiler, block, compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 0)));
            Assert.That(compiler.fgInsertGCPolls(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_trapQueries, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ExplicitPollMarkerRequiresTheMethodFlag(bool methodFlag)
    {
        WithCompiler((compiler, block) => {
            compiler.optMethodFlags = methodFlag ? OMF_NEEDS_GCPOLLS : 0;
            var marker = new GenTree(GT_GCPOLL, TYP_VOID) { Flags = GTF_CALL | GTF_GLOB_REF };
            _ = Append(compiler, block, marker);
            _ = Append(compiler, block, compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 0)));
            Assert.That(compiler.fgInsertGCPolls(), Is.EqualTo(methodFlag
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(s_trapQueries, Is.EqualTo(methodFlag ? 1 : 0));
        });
    }

    [TestCase(BBJ_ALWAYS, false)]
    [TestCase(BBJ_ALWAYS, true)]
    [TestCase(BBJ_CALLFINALLY, false)]
    [TestCase(BBJ_CALLFINALLY, true)]
    public static void UnconditionalPollPreservesTargetAndRetlessFlags(BBKinds kind, bool minOpts)
    {
        WithCompiler((compiler, block) => {
            var successor = compiler.fgNewBBafter(BBJ_RETURN, block, true);
            var edge = compiler.fgAddRefPred(successor, block);
            block.SetKindAndTargetEdge(kind, edge);
            block.SetFlags(BBF_HAS_SUPPRESSGC_CALL);
            if (kind is BBJ_CALLFINALLY)
            {
                block.SetFlags(BBF_RETLESS_CALL);
            }
            var suppressed = Append(compiler, block, UnmanagedCall(compiler, true));

            Assert.That(compiler.fgInsertGCPolls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt, Is.SameAs(suppressed));
            if (minOpts)
            {
                Assert.That(block.Kind, Is.EqualTo(kind));
                Assert.That(block.TargetEdge, Is.SameAs(edge));
                Assert.That(block.LastStmt!.RootNode.AsCall().HelperNum, Is.EqualTo(CORINFO_HELP_POLL_GC));
            }
            else
            {
                var bottom = block.TrueTarget;
                Assert.That(bottom.Kind, Is.EqualTo(kind));
                Assert.That(bottom.TargetEdge, Is.SameAs(edge));
                Assert.That(edge.SourceBlock, Is.SameAs(bottom));
                Assert.That(bottom.FirstStmt, Is.Null);
                Assert.That(bottom.HasFlag(BBF_RETLESS_CALL), Is.EqualTo(kind == BBJ_CALLFINALLY));
                Assert.That(block.HasFlag(BBF_RETLESS_CALL), Is.False);
            }
        }, minOpts);
    }

    [TestCase(BBJ_SWITCH)]
    [TestCase(BBJ_EHFILTERRET)]
    [TestCase(BBJ_EHCATCHRET)]
    public static void SwitchAndEHExitsUseCallPollsWithoutMovingEdges(BBKinds kind)
    {
        WithCompiler((compiler, block) => {
            var successor = compiler.fgNewBBafter(BBJ_RETURN, block, true);
            var edge = compiler.fgAddRefPred(successor, block);
            if (kind is BBJ_SWITCH)
            {
                block.SetKindAndTargetEdge(kind, null);
                block.SwitchTargets = new BBswtDesc([edge], [0], hasDefault: true);
            }
            else
            {
                block.SetKindAndTargetEdge(kind, edge);
            }

            _ = Append(compiler, block, UnmanagedCall(compiler, true));
            Statement? terminator = null;
            if (kind is not BBJ_EHCATCHRET)
            {
                terminator = Append(compiler, block, compiler.gtNewUnaryNode(
                    kind == BBJ_SWITCH ? GT_SWITCH : GT_RETFILT, TYP_VOID, compiler.gtNewIconNode(TYP_INT, 0)));
            }

            Assert.That(compiler.fgInsertGCPolls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.Blocks.Count(), Is.EqualTo(2));
            Assert.That(block.Kind, Is.EqualTo(kind));
            Assert.That(edge.SourceBlock, Is.SameAs(block));
            var poll = terminator is null ? block.LastStmt! : terminator.PrevStmt!;
            Assert.That(poll.RootNode.AsCall().HelperNum, Is.EqualTo(CORINFO_HELP_POLL_GC));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InlinePollTransfersConditionalEdgesIncludingDuplicateTargets(bool sameTarget)
    {
        WithCompiler((compiler, block) => {
            var first = compiler.fgNewBBafter(BBJ_RETURN, block, true);
            var second = sameTarget ? first : compiler.fgNewBBafter(BBJ_RETURN, first, true);
            _ = Append(compiler, first, compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 1)));
            if (!sameTarget)
            {
                _ = Append(compiler, second, compiler.gtNewUnaryNode(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 2)));
            }
            _ = Append(compiler, block, UnmanagedCall(compiler, true));
            var condition = Append(compiler, block, compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID,
                compiler.gtNewBinaryNode(GT_EQ, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 0))));
            block.SetCond(compiler.fgAddRefPred(first, block), compiler.fgAddRefPred(second, block));
            var trueEdge = block.TrueEdge;
            var falseEdge = block.FalseEdge;
            trueEdge.Likelihood = sameTarget ? 1 : 0.7;
            falseEdge.Likelihood = sameTarget ? 1 : 0.3;

            Assert.That(compiler.fgInsertGCPolls(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var bottom = block.TrueTarget;
            Assert.That(bottom.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(bottom.LastStmt, Is.SameAs(condition));
            Assert.That(bottom.TrueEdge, Is.SameAs(trueEdge));
            Assert.That(bottom.FalseEdge, Is.SameAs(falseEdge));
            Assert.That(trueEdge.SourceBlock, Is.SameAs(bottom));
            Assert.That(falseEdge.SourceBlock, Is.SameAs(bottom));
            Assert.That(trueEdge.Likelihood, Is.EqualTo(sameTarget ? 1 : 0.7));
            Assert.That(trueEdge.DupCount, Is.EqualTo(sameTarget ? 2 : 1));
        });
    }

    private static GenTreeCall UnmanagedCall(Compiler compiler, bool suppressed)
    {
        var call = compiler.gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_DBG_IS_JUST_MY_CODE);
        call.Flags |= GTF_CALL_UNMANAGED;
        if (suppressed)
        {
            call._callMoreFlags |= GTF_CALL_M_SUPPRESS_GC_TRANSITION;
        }
        return call;
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var stmt = compiler.gtNewStmt(tree);
        compiler.fgInsertStmtAtEnd(block, stmt);
        compiler.gtSetStmtInfo(stmt);
        compiler.fgSetStmtSeq(stmt);
        return stmt;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int* GetTrap(ICorJitInfo* self, void** indirect)
    {
        s_trapQueries++;
        *indirect = s_trapKind == 2 ? (void*)0x2000 : null;
        return s_trapKind == 1 ? (int*)0x1000 : null;
    }

    private static void WithCompiler(Action<Compiler, BasicBlock> action, bool minOpts = false)
    {
        s_trapKind = 1;
        s_trapQueries = 0;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.getAddrOfCaptureThreadGlobal = &GetTrap;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.compHndBBtab = [];
        compiler.info.compCompHnd = &jitInfo;
        compiler.info.compRetType = TYP_INT;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.eeInfoInitialized = true;
        compiler.virtualStubParamInfo = new Compiler.VirtualStubParamInfo();
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.codeGen = new CodeGen(compiler);
        compiler.optMethodFlags = OMF_NEEDS_GCPOLLS;
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        block.RemoveFlags(BBF_INTERNAL);
        block.SetFlags(BBF_IMPORTED);
        block.bbRefs = 1;
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        compiler.fgPredsComputed = true;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
