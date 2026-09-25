// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Reflection;
using NUnit.Framework;
#if DEBUG
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;
#endif

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FlowGraphUpdateChecksTests
{
    [Test]
    public static void UpdateCheckerExistsOnlyInDebug()
    {
        var method = typeof(Compiler).GetMethod("fgDebugCheckUpdate", BindingFlags.Instance | BindingFlags.Public);
#if DEBUG
        Assert.That(method, Is.Not.Null);
#else
        Assert.That(method, Is.Null);
#endif
    }

#if DEBUG
    [Test]
    public static void EmptyGraphHasNothingToCheck()
    {
        WithCompiler(false, compiler => {
            compiler.fgDebugCheckUpdate();
            Assert.That(s_assertions, Is.Empty);
        });
    }

    [TestCase("unreachable", "Unreachable block not removed!")]
    [TestCase("unimported", "Non IMPORTED block not removed!")]
    [TestCase("conditional", "Unnecessary jump to the next block!")]
    [TestCase("compactable", "Found un-compacted blocks!")]
    public static void OptimizedDiagnosticsRetainRecoverableFailureAndMessage(string fault, string message)
    {
        WithCompiler(false, compiler => {
            var block = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, block);

            switch (fault)
            {
                case "unreachable":
                {
                    block.bbRefs = 0;
                    break;
                }

                case "unimported":
                {
                    block.RemoveFlags(BBF_IMPORTED);
                    break;
                }

                case "conditional":
                {
                    var target = NewBlock(compiler, BBJ_RETURN);
                    Link(compiler, block, target);
                    target.bbRefs = 0;
                    var trueEdge = compiler.fgAddRefPred(target, block);
                    var falseEdge = compiler.fgAddRefPred(target, block);
                    block.SetCond(trueEdge, falseEdge);
                    break;
                }

                case "compactable":
                {
                    var target = NewBlock(compiler, BBJ_RETURN);
                    Link(compiler, block, target);
                    target.bbRefs = 0;
                    block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, block));
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown diagnostic case.");
                }
            }

            var flags = block.FlagsRaw;
            var count = compiler.fgBBcount;
            var exception = Assert.Throws<FatalJitException>(compiler.fgDebugCheckUpdate);

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result))
                .EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
            Assert.That(s_assertions, Is.EqualTo((string[])[message]));
            Assert.That(block.FlagsRaw, Is.EqualTo(flags));
            Assert.That(compiler.fgBBcount, Is.EqualTo(count));
        });
    }

    [Test]
    public static void MinOptsRetainsAssertionOrderAndContinuesAcrossBlocks()
    {
        WithCompiler(true, compiler => {
            var first = NewBlock(compiler, BBJ_THROW);
            var second = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, first, second);
            first.bbRefs = 0;
            first.RemoveFlags(BBF_IMPORTED);
            second.bbRefs = 0;

            compiler.fgDebugCheckUpdate();

            Assert.That(s_assertions, Is.EqualTo((string[])[
                "Unreachable block not removed!",
                "Empty block not removed!",
                "Non IMPORTED block not removed!",
                "Unreachable block not removed!",
            ]));
            Assert.That(compiler.fgFirstBB, Is.SameAs(first));
            Assert.That(first.Next, Is.SameAs(second));
            Assert.That(compiler.fgBBcount, Is.EqualTo(2));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ProtectedBlocksBypassUnreachableAndEmptyChecksButNotImportChecks(bool imported)
    {
        WithCompiler(true, compiler => {
            var block = NewBlock(compiler, BBJ_THROW);
            Link(compiler, block);
            block.bbRefs = 0;
            block.SetFlags(BBF_DONT_REMOVE);
            if (!imported)
            {
                block.RemoveFlags(BBF_IMPORTED);
            }

            compiler.fgDebugCheckUpdate();

            string[] expected = imported ? [] : ["Non IMPORTED block not removed!"];
            Assert.That(s_assertions, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void InternalBlocksNeedNotBeImported()
    {
        WithCompiler(false, compiler => {
            var block = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, block);
            block.RemoveFlags(BBF_IMPORTED);
            block.SetFlags(BBF_INTERNAL);

            compiler.fgDebugCheckUpdate();

            Assert.That(s_assertions, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ConditionalWithDistinctEdgesPassesRegardlessOfWhichEdgeTargetsNext(bool trueTargetsNext)
    {
        WithCompiler(false, compiler => {
            var block = NewBlock(compiler, BBJ_COND);
            var next = NewBlock(compiler, BBJ_RETURN);
            var other = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, block, next, other);
            next.bbRefs = 0;
            other.bbRefs = 0;
            var nextEdge = compiler.fgAddRefPred(next, block);
            var otherEdge = compiler.fgAddRefPred(other, block);
            block.SetCond(trueTargetsNext ? nextEdge : otherEdge, trueTargetsNext ? otherEdge : nextEdge);

            compiler.fgDebugCheckUpdate();

            Assert.That(s_assertions, Is.Empty);
            Assert.That(block.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(block.TrueEdge, Is.SameAs(trueTargetsNext ? nextEdge : otherEdge));
            Assert.That(block.FalseEdge, Is.SameAs(trueTargetsNext ? otherEdge : nextEdge));
        });
    }

    [Test]
    public static void ReferencedEmptyBlocksRetainNativeJumpKindAllowances(
        [Values(BBJ_EHFINALLYRET, BBJ_EHFAULTRET, BBJ_EHFILTERRET, BBJ_RETURN,
            BBJ_EHCATCHRET, BBJ_THROW, BBJ_SWITCH, BBJ_CALLFINALLYRET)] BBKinds kind,
        [Values(false, true)] bool lir)
    {
        WithCompiler(false, compiler => {
            var block = NewBlock(compiler, kind);
            Link(compiler, block);
            if (lir)
            {
                block.MakeLir(null, null);
                block.InsertAtEnd(new GenTreeILOffset(default));
            }
            else
            {
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(new GenTree(GT_NOP, TYP_VOID)));
            }

            compiler.fgDebugCheckUpdate();

            Assert.That(s_assertions, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NonemptyUnreachableBlocksDoNotEmitTheEmptyBlockDiagnostic(bool lir)
    {
        WithCompiler(true, compiler => {
            var block = NewBlock(compiler, BBJ_THROW);
            Link(compiler, block);
            block.bbRefs = 0;
            var node = new GenTree(GT_NO_OP, TYP_VOID);
            if (lir)
            {
                block.MakeLir(node, node);
            }
            else
            {
                compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(node));
            }

            compiler.fgDebugCheckUpdate();

            Assert.That(s_assertions, Is.EqualTo((string[])["Unreachable block not removed!"]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CompactableBlocksAreCheckedEvenWhenProtected(bool protectedBlock)
    {
        WithCompiler(true, compiler => {
            var block = NewBlock(compiler, BBJ_ALWAYS);
            var target = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, block, target);
            target.bbRefs = 0;
            block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, block));
            if (protectedBlock)
            {
                block.SetFlags(BBF_DONT_REMOVE);
            }

            compiler.fgDebugCheckUpdate();

            Assert.That(s_assertions, Is.EqualTo((string[])["Found un-compacted blocks!"]));
        });
    }

    [TestCase("self")]
    [TestCase("keep-always")]
    [TestCase("protected-target")]
    [TestCase("different-eh")]
    public static void NoncompactableUnconditionalBlocksPass(string reason)
    {
        WithCompiler(false, compiler => {
            var block = NewBlock(compiler, BBJ_ALWAYS);
            var target = NewBlock(compiler, BBJ_RETURN);
            Link(compiler, block, target);
            target.bbRefs = 0;
            block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(target, block));

            switch (reason)
            {
                case "self":
                {
                    compiler.fgRemoveRefPred(block.TargetEdge);
                    block.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(block, block));
                    target.SetFlags(BBF_DONT_REMOVE);
                    break;
                }

                case "keep-always":
                {
                    block.SetFlags(BBF_KEEP_BBJ_ALWAYS);
                    break;
                }

                case "protected-target":
                {
                    target.SetFlags(BBF_DONT_REMOVE);
                    break;
                }

                case "different-eh":
                {
                    target.TryIndex = 0;
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown compaction case.");
                }
            }

            compiler.fgDebugCheckUpdate();

            Assert.That(s_assertions, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CallFinallyAcceptsItsPairedTailOrRetlessFlag(bool retless)
    {
        WithCompiler(false, compiler => {
            var block = NewBlock(compiler, BBJ_CALLFINALLY);
            var tail = NewBlock(compiler, retless ? BBJ_RETURN : BBJ_CALLFINALLYRET);
            Link(compiler, block, tail);
            if (retless)
            {
                block.SetKindAndTargetEdge(BBJ_CALLFINALLY, compiler.fgAddRefPred(tail, block));
                block.SetFlags(BBF_RETLESS_CALL);
            }

            compiler.fgDebugCheckUpdate();

            Assert.That(s_assertions, Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CallFinallyReportsTheExistingPairPredicateAssertions(bool nonemptyTail)
    {
        WithCompiler(false, compiler => {
            var block = NewBlock(compiler, BBJ_CALLFINALLY);
            var tail = NewBlock(compiler, nonemptyTail ? BBJ_CALLFINALLYRET : BBJ_RETURN);
            Link(compiler, block, tail);
            if (nonemptyTail)
            {
                compiler.fgInsertStmtAtEnd(tail, compiler.gtNewStmt(new GenTree(GT_NO_OP, TYP_VOID)));
            }

            compiler.fgDebugCheckUpdate();

            Assert.That(s_assertions, Is.EqualTo((string[])[
                nonemptyTail ? "Next.isEmpty()" : "Next.Kind is BBJ_CALLFINALLYRET",
            ]));
        });
    }

    private static readonly List<string> s_assertions = [];

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.SetFlags(BBF_IMPORTED);

        return block;
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        for (var i = 0; i < blocks.Length - 1; i++)
        {
            blocks[i].Next = blocks[i + 1];
        }
    }

    private static void WithCompiler(bool minopts, Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        JitTls.Compiler = compiler;
        var previousEnableNoway = EnableNoway(ref JitConfig);
        EnableNoway(ref JitConfig) = 1;
        s_assertions.Clear();

        try
        {
            action(compiler);
        }
        finally
        {
            EnableNoway(ref JitConfig) = previousEnableNoway;
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "");

        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEnableNoWayAssert")]
    private static extern ref int EnableNoway(ref JitConfigValues config);
#endif
}
