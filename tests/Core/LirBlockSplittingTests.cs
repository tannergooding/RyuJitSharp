// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LirBlockSplittingTests
{
    [TestCase(1)]
    [TestCase(4)]
    public static void RemovingDefaultPreservesAllRemainingCaseReferences(int count)
    {
        WithCompiler(compiler => {
            var source = NewBlock(compiler, BBJ_SWITCH);
            var target = NewBlock(compiler, BBJ_RETURN);
            var edge = compiler.fgAddRefPred(target, source);
            var descriptor = new BBswtDesc([edge], new int[count], hasDefault: true);
            descriptor.Cases.Fill(edge);

            descriptor.RemoveDefaultCase();

            Assert.That(descriptor.HasDefaultCase, Is.False);
            Assert.That(descriptor.Cases.Length, Is.EqualTo(count - 1));
            foreach (var remaining in descriptor.Cases)
            {
                Assert.That(remaining, Is.SameAs(edge));
            }
            Assert.That(descriptor.Succs[0], Is.SameAs(edge));
        });
    }

    [TestCase(true, true, 12)]
    [TestCase(false, true, 18)]
    [TestCase(false, false, 30)]
    public static void SplitMovesOnlyTheSuffixAndUsesRootIlOffsets(bool precedingMarker, bool followingMarker, int offset)
    {
        WithCompiler(compiler => {
            var source = NewBlock(compiler, BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = source;
            source.bbCodeOffs = 2;
            source.bbCodeOffsEnd = 30;
            if (precedingMarker)
            {
                source.InsertAtEnd(NewOffset(12));
            }
            var splitAfter = new GenTree(GT_NO_OP, TYP_VOID);
            source.InsertAtEnd(splitAfter);
            var suffix = followingMarker ? NewOffset(18) : new GenTree(GT_NO_OP, TYP_VOID);
            var last = new GenTree(GT_NO_OP, TYP_VOID);
            source.InsertAtEnd(suffix);
            source.InsertAtEnd(last);

            var bottom = compiler.fgSplitBlockAfterNode(source, splitAfter);

            Assert.That(source.LastNode, Is.SameAs(splitAfter));
            Assert.That(splitAfter.Next, Is.Null);
            Assert.That(bottom.FirstNode, Is.SameAs(suffix));
            Assert.That(suffix.Prev, Is.Null);
            Assert.That(suffix.Next, Is.SameAs(last));
            Assert.That(bottom.LastNode, Is.SameAs(last));
            Assert.That(last.Next, Is.Null);
            Assert.That(source.bbCodeOffs, Is.EqualTo(2));
            Assert.That(source.bbCodeOffsEnd, Is.EqualTo(offset));
            Assert.That(bottom.bbCodeOffs, Is.EqualTo(offset));
            Assert.That(bottom.bbCodeOffsEnd, Is.EqualTo(30));
            Assert.That(source.Target, Is.SameAs(bottom));
            Assert.That(bottom.Kind, Is.EqualTo(BBJ_RETURN));
            Assert.That(compiler.fgLastBB, Is.SameAs(bottom));
        });
    }

    [Test]
    public static void SplitAtLastNodeTransfersSwitchEdgesProfileAndEnclosingEhEnd()
    {
        WithCompiler(compiler => {
            var source = NewBlock(compiler, BBJ_SWITCH);
            var handler = NewBlock(compiler, BBJ_EHFAULTRET);
            var target = NewBlock(compiler, BBJ_RETURN);
            source.Next = handler;
            handler.Next = target;
            compiler.fgFirstBB = source;
            compiler.fgLastBB = target;
            source.TryIndex = 0;
            compiler.compHndBBtab = [
                new EHblkDsc { ebdTryBeg = source, ebdTryLast = source, ebdHndBeg = handler, ebdHndLast = handler },
            ];
            compiler.compHndBBtabCount = 1;
            var edge = compiler.fgAddRefPred(target, source);
            _ = compiler.fgAddRefPred(target, source);
            edge.Likelihood = 1;
            var descriptor = new BBswtDesc([edge], [0, 0], hasDefault: true);
            descriptor.Cases.Fill(edge);
            source.SwitchTargets = descriptor;
            source.setBBProfileWeight(42);
            source.SetFlags(BBF_GC_SAFE_POINT);
            source.bbCodeOffs = 7;
            source.bbCodeOffsEnd = BAD_IL_OFFSET;
            var last = new GenTree(GT_NO_OP, TYP_VOID);
            source.InsertAtEnd(last);

            var bottom = compiler.fgSplitBlockAfterNode(source, last);

            Assert.That(bottom.IsEmpty && bottom.IsLIR, Is.True);
            Assert.That(bottom.SwitchTargets, Is.SameAs(descriptor));
            Assert.That(edge.SourceBlock, Is.SameAs(bottom));
            Assert.That(edge.DupCount, Is.EqualTo(2));
            Assert.That(target.bbRefs, Is.EqualTo(2));
            Assert.That(edge.Likelihood, Is.EqualTo(1));
            Assert.That(bottom.bbWeight, Is.EqualTo(42));
            Assert.That(bottom.hasProfileWeight, Is.True);
            Assert.That(bottom.HasFlag(BBF_GC_SAFE_POINT), Is.False);
            Assert.That(source.HasFlag(BBF_GC_SAFE_POINT), Is.True);
            Assert.That(bottom.TryIndex, Is.EqualTo(source.TryIndex));
            Assert.That(compiler.compHndBBtab[0].ebdTryBeg, Is.SameAs(source));
            Assert.That(compiler.compHndBBtab[0].ebdTryLast, Is.SameAs(bottom));
            Assert.That(bottom.Next, Is.SameAs(handler));
            Assert.That(handler.Prev, Is.SameAs(bottom));
            Assert.That(source.TargetEdge.Likelihood, Is.EqualTo(1));
            Assert.That(source.bbCodeOffsEnd, Is.EqualTo(7));
            Assert.That(bottom.bbCodeOffs, Is.EqualTo(7));
            Assert.That(bottom.bbCodeOffsEnd, Is.EqualTo(BAD_IL_OFFSET));
        });
    }

    [Test]
    public static void EmptySplitKeepsNativeUnknownIlBounds()
    {
        WithCompiler(compiler => {
            var source = NewBlock(compiler, BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = source;
            source.bbCodeOffs = 5;
            source.bbCodeOffsEnd = 10;

            var bottom = compiler.fgSplitBlockAfterNode(source, null);

            Assert.That(source.IsEmpty && bottom.IsEmpty, Is.True);
            Assert.That(source.bbCodeOffsEnd, Is.EqualTo(10));
            Assert.That(bottom.bbCodeOffs, Is.EqualTo(BAD_IL_OFFSET));
            Assert.That(bottom.bbCodeOffsEnd, Is.EqualTo(BAD_IL_OFFSET));
        });
    }

    private static GenTreeILOffset NewOffset(int rootOffset)
    {
        var strategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        var root = new InlineContext(strategy) { _ilSize = 31 };
        var inlinee = new InlineContext(strategy) { _parent = root, _location = new ILLocation(rootOffset, 0), _ilSize = 101 };
#if DEBUG
        root._ilInstsSet = new BitArray(31, true);
        inlinee._ilInstsSet = new BitArray(101, true);
#endif
        return new GenTreeILOffset(new DebugInfo(inlinee, new ILLocation(100, 0)));
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.bbRefs = 0;

        return block;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compHndBBtab = [];
        compiler.fgPredsComputed = true;
        compiler.compRationalIRForm = true;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
        compiler.fgSafeFlowEdgeCreation = true;
#endif
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
