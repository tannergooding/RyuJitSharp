// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;

namespace RyuJitSharp.UnitTests;

internal static unsafe class FlowGraphDfsTests
{
    [TestCase("acyclic", false)]
    [TestCase("cycle", true)]
    [TestCase("safe-cycle", false)]
    [TestCase("bypass", true)]
    [TestCase("disconnected", true)]
    public static void DetectsCyclesThatAvoidSafePoints(string shape, bool expected)
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var loop = BasicBlock.New(compiler, BBJ_ALWAYS);
            var exit = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = loop;
            loop.Next = exit;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = exit;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(entry, loop, null));
            loop.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(loop, shape == "acyclic" ? exit : loop, null));

            if (shape == "safe-cycle")
            {
                loop.SetFlags(BasicBlockFlags.BBF_GC_SAFE_POINT);
            }
            else if (shape == "bypass")
            {
                exit.SetFlags(BasicBlockFlags.BBF_GC_SAFE_POINT);
                loop.SetCond(new FlowEdge(loop, loop, null), new FlowEdge(loop, exit, null));
            }
            else if (shape == "disconnected")
            {
                entry.SetKindAndTargetEdge(BBJ_RETURN, null);
            }

            Assert.That(compiler.fgHasCycleWithoutGCSafePoint(), Is.EqualTo(expected));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(0x2B, -2, 0)]
    [TestCase(0x2B, 1, 3)]
    [TestCase(0x2B, -3, -1)]
    [TestCase(0x2B, 2, -1)]
    [TestCase(0x2B, sbyte.MinValue, -1)]
    [TestCase(0x2B, sbyte.MaxValue, -1)]
    [TestCase(0x38, -5, 0)]
    [TestCase(0x38, 1, 6)]
    [TestCase(0x38, -6, -1)]
    [TestCase(0x38, 2, -1)]
    [TestCase(0x38, int.MinValue, -1)]
    [TestCase(0x38, int.MaxValue, -1)]
    [TestCase(0xDD, -6, -1)]
    [TestCase(0xDE, -3, -1)]
    public static void BranchScanPreservesUnsignedDestinationBounds(byte opcode, int displacement, int expectedTarget)
    {
        var operandSize = (opcode is 0x2B or 0xDE) ? sizeof(sbyte) : sizeof(int);
        var il = new byte[operandSize + 3];
        il[0] = opcode;
        if (operandSize is sizeof(sbyte))
        {
            il[1] = unchecked((byte)displacement);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(il.AsSpan(1), displacement);
        }
        il[^1] = 0x2A;

        AssertJumpTargetScan(il, expectedTarget);
    }

    [TestCase(0u, 6)]
    [TestCase(1u, -1)]
    [TestCase(0x80000000u, -1)]
    [TestCase(uint.MaxValue, -1)]
    public static void SwitchScanPreservesUnsignedCountBounds(uint count, int expectedTarget)
    {
        byte[] il = [0x16, 0x45, 0, 0, 0, 0, 0x2A];
        BinaryPrimitives.WriteUInt32LittleEndian(il.AsSpan(2), count);

        AssertJumpTargetScan(il, expectedTarget);
    }

    [TestCase(-10, 0)]
    [TestCase(0, 10)]
    [TestCase(-11, -1)]
    [TestCase(1, -1)]
    [TestCase(int.MinValue, -1)]
    [TestCase(int.MaxValue, -1)]
    public static void SwitchScanPreservesUnsignedDestinationBounds(int displacement, int expectedTarget)
    {
        byte[] il = [0x16, 0x45, 1, 0, 0, 0, 0, 0, 0, 0, 0x2A];
        BinaryPrimitives.WriteInt32LittleEndian(il.AsSpan(6), displacement);

        AssertJumpTargetScan(il, expectedTarget);
    }

    private static void AssertJumpTargetScan(byte[] il, int expectedTarget)
    {
        if (expectedTarget < 0)
        {
            var error = Assert.Throws<FatalJitException>(() => ScanJumpTargets(il));
            Assert.That(error, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_BADCODE));
        }
        else
        {
            Assert.That(ScanJumpTargets(il)[expectedTarget], Is.True);
        }
    }

    private static BitArray ScanJumpTargets(byte[] il)
    {
        var compiler = CreateCompiler();
        compiler.compInlineContext = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
        compiler.compInlineContext._ilSize = il.Length;
        var methodInfo = new CORINFO_METHOD_INFO();
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
        var flags = new JitFlags();
        compiler.opts.jitFlags = &flags;
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;
#if DEBUG
        JitTls.LogEnv.Compiler = compiler;
#endif

        try
        {
            var jumpTargets = new BitArray(il.Length);
            fixed (byte* code = il)
            {
                compiler.fgFindJumpTargets(code, il.Length, jumpTargets, makeInlineObservations: false);
            }

            return jumpTargets;
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void RegularSuccessorEnumerationPreservesOrderAndAbort(int count)
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var block = BasicBlock.New(compiler, count == 3 ? BBJ_SWITCH : BBJ_EHFINALLYRET);
            compiler.fgFirstBB = block;
            var first = BasicBlock.New(compiler, BBJ_RETURN);
            var second = BasicBlock.New(compiler, BBJ_RETURN);
            var third = BasicBlock.New(compiler, BBJ_RETURN);
            var firstEdge = new FlowEdge(block, first, null);
            var secondEdge = new FlowEdge(block, second, null);
            BasicBlock[] expected = [];

            if (count == 1)
            {
                block.SetCond(firstEdge, firstEdge);
                expected = [first];
            }
            else if (count == 2)
            {
                block.SetCond(firstEdge, secondEdge);
                expected = [second, first];
            }
            else if (count == 3)
            {
                var thirdEdge = new FlowEdge(block, third, null);
                block.SwitchTargets = new BBswtDesc([firstEdge, secondEdge, thirdEdge], [0, 1, 2], hasDefault: true, dominantCase: 0);
                expected = [first, second, third];
            }

            var enumerator = new GCSafePointSuccessorEnumerator(compiler, block);
            Assert.That(enumerator.Block, Is.SameAs(block));

            foreach (var successor in expected)
            {
                Assert.That(enumerator.NextSuccessor, Is.SameAs(successor));
            }

            Assert.That(enumerator.NextSuccessor, Is.Null);
            Assert.That(enumerator.NextSuccessor, Is.Null);
            var visited = 0;
            var result = block.VisitRegularSuccs(compiler, successor => {
                Assert.That(successor, Is.SameAs(expected[0]));
                visited++;
                return BasicBlockVisit.Abort;
            });
            Assert.That(visited, Is.EqualTo(count == 0 ? 0 : 1));
            Assert.That(result, Is.EqualTo(count == 0 ? BasicBlockVisit.Continue : BasicBlockVisit.Abort));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(false, "fast")]
    [TestCase(true, "fast")]
    [TestCase(false, "helper")]
    [TestCase(true, "helper")]
    [TestCase(false, "jmp")]
    [TestCase(true, "jmp")]
    public static void TailcallPseudoSuccessorsUseTreeAndLirTerminals(bool lir, string kind)
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            compiler.compJmpOpUsed = kind == "jmp";
            compiler.compTailCallUsed = kind != "jmp";
            var block = BasicBlock.New(compiler, kind == "helper" ? BBJ_THROW : BBJ_RETURN);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            block.SetFlags(BasicBlockFlags.BBF_HAS_JMP);
            var call = new GenTreeCall(var_types.TYP_VOID) {
                _callMoreFlags = GenTreeCallFlags.GTF_CALL_M_TAILCALL |
                    (kind == "helper" ? GenTreeCallFlags.GTF_CALL_M_TAILCALL_VIA_JIT_HELPER : 0),
            };
            var terminal = kind == "jmp" ? new GenTreeVal(genTreeOps.GT_JMP, var_types.TYP_VOID, 0) : (GenTree)call;

            if (lir)
            {
                block.SetFlags(BasicBlockFlags.BBF_IS_LIR);
                block.FirstLIRNode = terminal;
                block.LastLIRNode = terminal;
            }
            else
            {
                compiler.fgInsertStmtAtEnd(block, new Statement(terminal, 1));
            }

            Assert.That(block.GetLastNode(), Is.SameAs(terminal));
            Assert.That(block.EndsWithTailCallOrJmp(compiler), Is.True);
            Assert.That(block.EndsWithTailCall(compiler, false, false, out var tail), Is.EqualTo(kind != "jmp"));
            Assert.That(tail, Is.SameAs(kind == "jmp" ? null : call));
            var enumerator = new GCSafePointSuccessorEnumerator(compiler, block);
            Assert.That(enumerator.NextSuccessor, Is.SameAs(kind == "helper" ? null : block));
            Assert.That(enumerator.NextSuccessor, Is.Null);
            Assert.That(compiler.fgHasCycleWithoutGCSafePoint(), Is.EqualTo(kind != "helper"));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PreservesSuccessorOrderAndTreeNumbering(bool useProfile)
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var entry = BasicBlock.New(compiler, BBJ_COND);
            var falseBlock = BasicBlock.New(compiler, BBJ_RETURN);
            var trueBlock = BasicBlock.New(compiler, BBJ_RETURN);
            var unreachable = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = falseBlock;
            falseBlock.Next = trueBlock;
            trueBlock.Next = unreachable;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = unreachable;

            var falseEdge = new FlowEdge(entry, falseBlock, null) { Likelihood = 0.8 };
            var trueEdge = new FlowEdge(entry, trueBlock, null) { Likelihood = 0.2 };
            entry.FalseEdge = falseEdge;
            entry.TrueEdge = trueEdge;

            var dfs = ComputeDfs(compiler, useProfile);
            var first = useProfile ? trueBlock : falseBlock;
            var second = useProfile ? falseBlock : trueBlock;

            Assert.Multiple(() => {
                Assert.That(dfs.GetPostOrder().Length, Is.EqualTo(4));
                Assert.That(dfs.PostOrderCount, Is.EqualTo(3));
                Assert.That(dfs.GetPostOrder(0), Is.SameAs(first));
                Assert.That(dfs.GetPostOrder(1), Is.SameAs(second));
                Assert.That(dfs.GetPostOrder(2), Is.SameAs(entry));
                Assert.That(entry.bbPreorderNum, Is.Zero);
                Assert.That(first.bbPreorderNum, Is.EqualTo(1));
                Assert.That(second.bbPreorderNum, Is.EqualTo(2));
                Assert.That(entry.bbPostorderNum, Is.EqualTo(2));
                Assert.That(dfs.HasCycle, Is.False);
                Assert.That(dfs.IsProfileAware, Is.EqualTo(useProfile));
                Assert.That(dfs.Contains(unreachable), Is.False);
                Assert.That(dfs.IsAncestor(entry, first), Is.True);
                Assert.That(dfs.IsAncestor(first, second), Is.False);
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void DetectsBackedgesAndPreservesBlockNumbersOnInvalidation()
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var entry = BasicBlock.New(compiler, BBJ_ALWAYS);
            var loop = BasicBlock.New(compiler, BBJ_ALWAYS);
            entry.Next = loop;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = loop;
            entry.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(entry, loop, null));
            loop.SetKindAndTargetEdge(BBJ_ALWAYS, new FlowEdge(loop, entry, null));

            var dfs = ComputeDfs(compiler, false);
            compiler._dfsTree = dfs;
            compiler.fgSsaValid = true;
            compiler.fgInvalidateDfsTree();

            Assert.Multiple(() => {
                Assert.That(dfs.HasCycle, Is.True);
                Assert.That(dfs.GetPostOrder(0), Is.SameAs(loop));
                Assert.That(dfs.GetPostOrder(1), Is.SameAs(entry));
                Assert.That(loop.bbPostorderNum, Is.Zero);
                Assert.That(dfs.IsAncestor(entry, loop), Is.True);
                Assert.That(compiler._dfsTree, Is.Null);
                Assert.That(compiler.fgSsaValid, Is.False);
                Assert.That(entry.bbPostorderNum, Is.EqualTo(1));
            });
            Assert.That(compiler.fgDfsBlocksAndRemove(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler._dfsTree, Is.Not.Null.And.Not.SameAs(dfs));
            Assert.That(compiler._dfsTree!.HasCycle, Is.True);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void VisitsFilterAndHandlerAsExceptionalSuccessors()
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var tryBlock = BasicBlock.New(compiler, BBJ_RETURN);
            var filter = BasicBlock.New(compiler, BBJ_RETURN);
            var handler = BasicBlock.New(compiler, BBJ_RETURN);
            var unreachable = BasicBlock.New(compiler, BBJ_RETURN);
            tryBlock.Next = filter;
            filter.Next = handler;
            handler.Next = unreachable;
            compiler.fgFirstBB = tryBlock;
            compiler.fgLastBB = unreachable;
            tryBlock.TryIndex = 0;
            filter.HndIndex = 0;
            handler.HndIndex = 0;

            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdTryBeg = tryBlock,
                    ebdTryLast = tryBlock,
                    ebdFilter = filter,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdHandlerType = EHHandlerType.EH_HANDLER_FILTER,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX
                }
            ];
            compiler.compHndBBtabCount = 1;

            var dfs = ComputeDfs(compiler, false);
            Assert.Multiple(() => {
                Assert.That(dfs.PostOrderCount, Is.EqualTo(3));
                Assert.That(dfs.GetPostOrder(0), Is.SameAs(filter));
                Assert.That(dfs.GetPostOrder(1), Is.SameAs(handler));
                Assert.That(dfs.GetPostOrder(2), Is.SameAs(tryBlock));
                Assert.That(dfs.Contains(unreachable), Is.False);
                Assert.That(dfs.IsAncestor(tryBlock, filter), Is.True);
                Assert.That(dfs.IsAncestor(tryBlock, handler), Is.True);
            });

            compiler._dfsTree = dfs;
            var callback = new DataFlowCallback(changeFirstPass: false, blockCount: 3);
            new DataFlow(compiler).ForwardAnalysis(ref callback);
            BasicBlock[] expectedStarts = [tryBlock, handler, filter];
            (BasicBlock, BasicBlock, BasicBlock)[] expectedHandlerMerges = [
                (handler, tryBlock, tryBlock),
                (filter, tryBlock, tryBlock)
            ];

            Assert.Multiple(() => {
                Assert.That(compiler._dfsTree, Is.SameAs(dfs));
                Assert.That(callback.Starts, Is.EqualTo(expectedStarts));
                Assert.That(callback.HandlerMerges, Is.EqualTo(expectedHandlerMerges));
                Assert.That(callback.Merges, Is.Empty);
                Assert.That(callback.Ends, Is.EqualTo(3));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void RemovesUnreachableBlocksButRetainsNonRemovableBlocks()
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var dead = BasicBlock.New(compiler, BBJ_THROW);
            var retained = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = dead;
            dead.Next = retained;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = retained;
            dead.bbRefs = 0;
            retained.bbRefs = 0;
            retained.SetFlags(BasicBlockFlags.BBF_DONT_REMOVE | BasicBlockFlags.BBF_INTERNAL);
            compiler._dfsTree = ComputeDfs(compiler, false);

            var previousDfs = compiler._dfsTree;
            compiler.fgSsaValid = true;
            Assert.That(compiler.fgDfsBlocksAndRemove(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            Assert.Multiple(() => {
                Assert.That(compiler._dfsTree, Is.Not.Null.And.Not.SameAs(previousDfs));
                Assert.That(compiler.fgSsaValid, Is.False);
                Assert.That(compiler.fgBBcount, Is.EqualTo(2));
                Assert.That(entry.Next, Is.SameAs(retained));
                Assert.That(retained.Prev, Is.SameAs(entry));
                Assert.That(retained.Kind, Is.EqualTo(BBJ_THROW));
                Assert.That(retained.HasFlag(BasicBlockFlags.BBF_REMOVED), Is.False);
                Assert.That(retained.HasFlag(BasicBlockFlags.BBF_INTERNAL), Is.False);
                Assert.That(retained.HasFlag(BasicBlockFlags.BBF_IMPORTED), Is.True);
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnreachableRemovalInvalidatesAnnotationsOnlyWhenDfsIsRecomputed(bool callFinallyPair)
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var entry = BasicBlock.New(compiler, callFinallyPair ? BBJ_THROW : BBJ_RETURN);
            var dead = BasicBlock.New(compiler, callFinallyPair ? BBJ_CALLFINALLY : BBJ_THROW);
            entry.Next = dead;
            entry.bbRefs = 1;
            dead.bbRefs = 0;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = dead;
            BasicBlock? tail = null;
            BasicBlock? continuation = null;
            BasicBlock? handler = null;

            if (callFinallyPair)
            {
                tail = BasicBlock.New(compiler, BBJ_CALLFINALLYRET);
                continuation = BasicBlock.New(compiler, BBJ_RETURN);
                handler = BasicBlock.New(compiler, BBJ_EHFINALLYRET);
                tail.bbRefs = 0;
                continuation.bbRefs = 0;
                handler.bbRefs = 0;
                dead.Next = tail;
                tail.Next = continuation;
                continuation.Next = handler;
                compiler.fgLastBB = handler;
                dead.SetKindAndTargetEdge(BBJ_CALLFINALLY,
                    compiler.fgAddRefPred(handler, dead, initializingPreds: true));
                tail.SetKindAndTargetEdge(BBJ_CALLFINALLYRET,
                    compiler.fgAddRefPred(continuation, tail, initializingPreds: true));
                var finallyReturnEdge = compiler.fgAddRefPred(tail, handler, initializingPreds: true);
                finallyReturnEdge.Likelihood = 1.0;
                handler.SetEhf(new BBJumpTable([finallyReturnEdge]));
                entry.TryIndex = 0;
                handler.HndIndex = 0;
                handler.CatchType = bbCatchType.BBCT_FINALLY;
                handler.bbRefs++;
                entry.SetFlags(BasicBlockFlags.BBF_DONT_REMOVE);
                tail.SetFlags(BasicBlockFlags.BBF_DONT_REMOVE);
                handler.SetFlags(BasicBlockFlags.BBF_DONT_REMOVE);
                compiler.compHndBBtab = [
                    new EHblkDsc {
                        ebdTryBeg = entry,
                        ebdTryLast = entry,
                        ebdHndBeg = handler,
                        ebdHndLast = handler,
                        ebdHandlerType = EHHandlerType.EH_HANDLER_FINALLY,
                        ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                        ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX
                    }
                ];
                compiler.compHndBBtabCount = 1;
            }

            compiler.fgPredsComputed = true;
            var dfs = ComputeDfs(compiler, false);
            var loops = FlowGraphNaturalLoops.Find(dfs);
            compiler._dfsTree = dfs;
            compiler._loops = loops;
            compiler.fgSsaValid = true;
            Assert.That(dfs.Contains(dead), Is.False);
            if (callFinallyPair)
            {
                Assert.That(dfs.Contains(tail ?? throw new AssertionException("Missing callfinally tail.")), Is.True);
                Assert.That(dfs.Contains(continuation ?? throw new AssertionException("Missing continuation.")), Is.True);
            }

            Assert.That(compiler.fgRemoveBlocksOutsideDfsTree(), Is.True);
            Assert.That(dead.HasFlag(BasicBlockFlags.BBF_REMOVED), Is.True);
            var newDfs = compiler._dfsTree ?? throw new AssertionException("Missing DFS tree after removal.");

            if (callFinallyPair)
            {
                if ((tail is not BasicBlock pairTail) || (continuation is not BasicBlock returnBlock) ||
                    (handler is not BasicBlock finallyHandler))
                {
                    throw new AssertionException("Missing callfinally graph blocks.");
                }

                Assert.That(newDfs, Is.Not.SameAs(dfs));
                Assert.That(compiler._loops, Is.Null);
                Assert.That(compiler.fgSsaValid, Is.False);
                Assert.That(pairTail.HasFlag(BasicBlockFlags.BBF_REMOVED), Is.True);
                Assert.That(returnBlock.HasFlag(BasicBlockFlags.BBF_REMOVED), Is.True);
                Assert.That(newDfs.PostOrderCount, Is.EqualTo(2));
                Assert.That(newDfs.GetPostOrder(0), Is.SameAs(finallyHandler));
                Assert.That(newDfs.GetPostOrder(1), Is.SameAs(entry));
                Assert.That(entry.Next, Is.SameAs(finallyHandler));
            }
            else
            {
                Assert.That(newDfs, Is.SameAs(dfs));
                Assert.That(compiler._loops, Is.SameAs(loops));
                Assert.That(compiler.fgSsaValid, Is.True);
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(0)]
    [TestCase(62)]
    [TestCase(63)]
    [TestCase(64)]
    [TestCase(72)]
    [TestCase(256)]
    public static void SwitchOperandsPreserveFullTableWidth(int caseCount)
    {
        var compiler = CreateCompiler();
#if DEBUG
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        var methodInfo = new CORINFO_METHOD_INFO();
        var flags = new JitFlags();
        compiler.info.compMethodInfo = &methodInfo;
        compiler.opts.jitFlags = &flags;
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var tableEnd = 6 + (caseCount * sizeof(int));
            var il = new byte[tableEnd + 2];
            il[0] = 0x16;
            il[1] = 0x45;
            BinaryPrimitives.WriteInt32LittleEndian(il.AsSpan(2), caseCount);
            for (var i = 0; i < caseCount; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(il.AsSpan(6 + (i * sizeof(int))), 1);
            }
            il[tableEnd] = 0x2A;
            il[tableEnd + 1] = 0x2A;
            compiler.info.compILCodeSize = il.Length;
            var jumpTargets = new BitArray(il.Length);
            jumpTargets[tableEnd] = true;
            jumpTargets[tableEnd + 1] = true;

            fixed (byte* code = il)
            {
                compiler.info.compCode = code;
                compiler.fgMakeBasicBlocks(code, il.Length, jumpTargets);
            }

            var block = compiler.fgFirstBB ?? throw new AssertionException("Missing switch block.");
            Assert.That(block.Kind, Is.EqualTo(BBJ_SWITCH));
            Assert.That(block.bbCodeOffsEnd, Is.EqualTo(tableEnd));
            Assert.That(block.SwitchTargets.Cases.Length, Is.EqualTo(caseCount + 1));
            Assert.That(block.SwitchTargets.DefaultCase.DestinationBlock.bbCodeOffs, Is.EqualTo(tableEnd));
            for (var i = 0; i < caseCount; i++)
            {
                Assert.That(block.SwitchTargets.Cases[i].DestinationBlock.bbCodeOffs, Is.EqualTo(tableEnd + 1));
            }
            Assert.That(compiler.fgBBcount, Is.EqualTo(3));
            Assert.That(compiler.fgReturnCount, Is.EqualTo(2));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(false, 3)]
    [TestCase(true, 2)]
    public static void TailPrefixAppliesOnlyToTheNextCallAndNotAsyncVersions(bool asyncVersion, int expectedBlocks)
    {
        var compiler = CreateCompiler();
        var methodInfo = new CORINFO_METHOD_INFO {
            options = asyncVersion ? CorInfoOptions.CORINFO_ASYNC_VERSION : 0
        };
        var flags = new JitFlags();
        compiler.info.compMethodInfo = &methodInfo;
        compiler.opts.jitFlags = &flags;
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            ReadOnlySpan<byte> il = [
                0xFE, 0x14,
                0x28, 0x01, 0x00, 0x00, 0x06,
                0x2A,
                0x28, 0x02, 0x00, 0x00, 0x06,
                0x2A
            ];
            compiler.info.compILCodeSize = il.Length;

            fixed (byte* code = il)
            {
                compiler.info.compCode = code;
                compiler.fgMakeBasicBlocks(code, il.Length, new BitArray(il.Length));
            }

            var lastBlock = compiler.fgLastBB ?? throw new InvalidOperationException("No basic blocks were constructed.");
            Assert.Multiple(() => {
                Assert.That(compiler.fgBBcount, Is.EqualTo(expectedBlocks));
                Assert.That(compiler.fgReturnCount, Is.EqualTo(expectedBlocks));
                Assert.That(compiler.compTailPrefixSeen, Is.EqualTo(!asyncVersion));
                Assert.That(lastBlock.bbCodeOffs, Is.EqualTo(8));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void RemovingUnreachableBlockRetargetsSurvivingEHRegionEnds()
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var entry = BasicBlock.New(compiler, BBJ_RETURN);
            var dead = BasicBlock.New(compiler, BBJ_THROW);
            var handler = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = dead;
            dead.Next = handler;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = handler;
            dead.bbRefs = 0;

            // EH removal can clear block indices before the outer region's last-block pointers are repaired.
            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdTryBeg = entry,
                    ebdTryLast = dead,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX
                }
            ];
            compiler.compHndBBtabCount = 1;

            var next = compiler.fgRemoveBlock(dead, unreachable: true);
            Assert.Multiple(() => {
                Assert.That(next, Is.SameAs(handler));
                Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(entry));
                Assert.That(compiler.compHndBBtab[0].ebdHndLast, Is.SameAs(handler));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [Test]
    public static void RekeysAndMergesAddCodeDescriptorsWhenRemovingEHEntry()
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            compiler.compHndBBtab = [
                new EHblkDsc {
                    ebdEnclosingTryIndex = 1,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX
                },
                new EHblkDsc {
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX
                }
            ];
            compiler.compHndBBtabCount = 2;

            var inner = new Compiler.AddCodeDsc {
                acdKind = SpecialCodeKind.SCK_RNGCHK_FAIL,
                acdTryIndex = 1,
                acdKeyDsg = Compiler.AcdKeyDesignator.KD_TRY
            };
            var outer = new Compiler.AddCodeDsc {
                acdKind = SpecialCodeKind.SCK_RNGCHK_FAIL,
                acdTryIndex = 2,
                acdKeyDsg = Compiler.AcdKeyDesignator.KD_TRY
            };
            var filter = new Compiler.AddCodeDsc {
                acdKind = SpecialCodeKind.SCK_RNGCHK_FAIL,
                acdHndIndex = 1,
                acdKeyDsg = Compiler.AcdKeyDesignator.KD_FLT
            };
            var map = new AddCodeDscMap {
                [new Compiler.AddCodeDscKey(inner)] = inner,
                [new Compiler.AddCodeDscKey(outer)] = outer,
                [new Compiler.AddCodeDscKey(filter)] = filter
            };
            var mapField = typeof(Compiler).GetField("fgAddCodeDscMap", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("fgAddCodeDscMap was not found.");
            mapField.SetValue(compiler, map);

            _ = InvokePrivate(compiler, "fgUpdateACDsBeforeEHTableEntryRemoval", (ushort)0);
            Assert.Multiple(() => {
                Assert.That(map.Count, Is.EqualTo(1));
                Assert.That(inner.acdTryIndex, Is.EqualTo(2));
                Assert.That(map[new Compiler.AddCodeDscKey(outer)], Is.SameAs(outer));
            });

            _ = InvokePrivate(compiler, "fgRemoveEHTableEntry", (ushort)0);
            Assert.Multiple(() => {
                Assert.That(compiler.compHndBBtabCount, Is.EqualTo(1));
                Assert.That(compiler.compHndBBtab[0].ebdEnclosingTryIndex, Is.EqualTo(EHblkDsc.NO_ENCLOSING_INDEX));
                Assert.That(outer.acdTryIndex, Is.EqualTo(1));
                Assert.That(map.Count, Is.EqualTo(1));
                Assert.That(map[new Compiler.AddCodeDscKey(outer)], Is.SameAs(outer));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(false, false, 1)]
    [TestCase(false, true, 1)]
    [TestCase(true, false, 1)]
    [TestCase(true, true, 2)]
    public static void ForwardDataFlowRepeatsOnlyChangedCyclicGraphs(bool cyclic, bool changeFirstPass, int passes)
    {
        var compiler = CreateCompiler();
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var entry = BasicBlock.New(compiler, BBJ_COND);
            var next = BasicBlock.New(compiler, cyclic ? BBJ_ALWAYS : BBJ_RETURN);
            var unreachable = BasicBlock.New(compiler, BBJ_RETURN);
            entry.Next = next;
            next.Next = unreachable;
            compiler.fgFirstBB = entry;
            compiler.fgLastBB = unreachable;

            var sharedEdge = new FlowEdge(entry, next, null);
            sharedEdge.incrementDupCount();
            sharedEdge.incrementDupCount();
            entry.SetCond(sharedEdge, sharedEdge);
            next.bbPreds = sharedEdge;

            if (cyclic)
            {
                var backedge = new FlowEdge(next, entry, null);
                backedge.incrementDupCount();
                next.SetKindAndTargetEdge(BBJ_ALWAYS, backedge);
                entry.bbPreds = backedge;
            }

            var callback = new DataFlowCallback(changeFirstPass, blockCount: 2);
            new DataFlow(compiler).ForwardAnalysis(ref callback);
            BasicBlock[] expectedStarts = passes == 1 ? [entry, next] : [entry, next, entry, next];

            Assert.Multiple(() => {
                Assert.That(compiler._dfsTree, Is.Not.Null);
                Assert.That(callback.Ends, Is.EqualTo(2 * passes));
                Assert.That(callback.Starts, Is.EqualTo(expectedStarts));
                Assert.That(callback.HandlerMerges, Is.Empty);
                Assert.That(callback.Merges.Count, Is.EqualTo((cyclic ? 2 : 1) * passes));
            });

            for (var pass = 0; pass < passes; pass++)
            {
                var index = pass * (cyclic ? 2 : 1);
                if (cyclic)
                {
                    Assert.That(callback.Merges[index++], Is.EqualTo((entry, next, 1)));
                }

                Assert.That(callback.Merges[index], Is.EqualTo((next, entry, 2)));
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct DataFlowCallback(bool changeFirstPass, int blockCount) : DataFlow.ICallback
    {
        public List<BasicBlock> Starts = [];
        public List<(BasicBlock, BasicBlock, int)> Merges = [];
        public List<(BasicBlock, BasicBlock, BasicBlock)> HandlerMerges = [];
        public int Ends;

        public readonly void StartMerge(BasicBlock block)
        {
            Starts.Add(block);
        }

        public readonly void Merge(BasicBlock block, BasicBlock predecessor, int duplicateCount)
        {
            Merges.Add((block, predecessor, duplicateCount));
        }

        public readonly void MergeHandler(BasicBlock block, BasicBlock firstTryBlock, BasicBlock lastTryBlock)
        {
            HandlerMerges.Add((block, firstTryBlock, lastTryBlock));
        }

        public bool EndMerge(BasicBlock block)
        {
            Ends++;
            return changeFirstPass && (Ends <= blockCount);
        }
    }

    private static Compiler CreateCompiler()
    {
        // Normal construction queries the EE; these checks exercise only initialized graph state.
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];

#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;

#endif
        return compiler;
    }

    private static FlowGraphDfsTree ComputeDfs(Compiler compiler, bool useProfile)
    {
        return compiler.fgComputeDfs(useProfile);
    }

    private static object? InvokePrivate(Compiler compiler, string name, params object[] args)
    {
        var method = typeof(Compiler).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{name} was not found.");
        return method.Invoke(compiler, args);
    }
}
