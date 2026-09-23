// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
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
            var map = new Dictionary<Compiler.AddCodeDscKey, Compiler.AddCodeDsc> {
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
