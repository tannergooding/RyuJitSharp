// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.RefCountState;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LocalMorphTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ValuesKeepTheExactOwningSlotForSharedOperands(bool reversed)
    {
        WithCompiler(compiler => {
            var shared = compiler.gtNewIconNode(TYP_INT, 1);
            var tree = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, shared, shared);
            tree.Flags |= reversed ? GTF_REVERSE_OPS : GTF_EMPTY;
            var stmt = compiler.gtNewStmt(tree);
            var value = new LocalAddressVisitor.Value(stmt, ref tree.Op2Ref, tree);
            var replacement = compiler.gtNewIconNode(TYP_INT, 2);
            value.Use = replacement;

            Assert.That(tree.Op1, Is.SameAs(shared));
            Assert.That(tree.Op2, Is.SameAs(replacement));
            Assert.That(value.Node, Is.SameAs(replacement));

            var root = new LocalAddressVisitor.Value(stmt, ref stmt.RootNodeRef, owner: null) {
                Use = replacement,
            };
            Assert.That(stmt.RootNode, Is.SameAs(replacement));
            Assert.That(root.Node, Is.SameAs(replacement));

            var call = compiler.gtNewHelperCallNode(TYP_INT, CorInfoHelpFunc.CORINFO_HELP_UDIV, shared, shared);
            var callStmt = compiler.gtNewStmt(call);
            var secondArg = call.Args.GetUserArgByIndex(1);
            assert(secondArg is not null);
            var argument = new LocalAddressVisitor.Value(callStmt, ref secondArg.EarlyNodeRef, call) {
                Use = replacement,
            };

            Assert.That(call.Args.GetUserArgByIndex(0)?.Node, Is.SameAs(shared));
            Assert.That(secondArg.Node, Is.SameAs(replacement));
            Assert.That(argument.Node, Is.SameAs(replacement));
        });
    }

    [TestCase(uint.MaxValue, 0u, true)]
    [TestCase(uint.MaxValue, 1u, false)]
    [TestCase(uint.MaxValue - 1, 1u, true)]
    public static void AddressOffsetsDoNotWrap(uint offset, uint delta, bool succeeds)
    {
        WithCompiler(compiler => {
            var stmt = compiler.gtNewStmt(compiler.gtNewIconNode(TYP_I_IMPL, 0));
            var source = new LocalAddressVisitor.Value(stmt, ref stmt.RootNodeRef, owner: null);
            source.Address(2, offset);
            var result = new LocalAddressVisitor.Value(stmt, ref stmt.RootNodeRef, owner: null);

            Assert.That(result.AddOffset(ref source, delta), Is.EqualTo(succeeds));
            Assert.That(result.IsAddress, Is.EqualTo(succeeds));

            if (succeeds)
            {
                Assert.That(result.LclNum, Is.EqualTo(2));
                Assert.That(result.Offset, Is.EqualTo(offset + delta));
            }
#if DEBUG
            Assert.That(source.IsConsumed, Is.EqualTo(succeeds));
#endif
        });
    }

    [TestCase(8, 0u, 8, false)]
    [TestCase(8, 4u, 4, false)]
    [TestCase(8, 4u, 8, true)]
    [TestCase(8, uint.MaxValue, 1, true)]
    [TestCase(65536, 65534u, 1, false)]
    [TestCase(65536, 65535u, 1, true)]
    public static void WideAccessChecksTheLastByteWithoutOverflow(int localSize, uint offset, int accessSize, bool wide)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = new ClassLayout(localSize);

            Assert.That(compiler.IsWideAccess(0, offset, new ValueSize(accessSize)), Is.EqualTo(wide));
            Assert.That(compiler.IsWideAccess(0, offset, ValueSize.Unknown), Is.True);
        });
    }

    [Test]
    public static void HiddenReturnBuffersDisableEnregistrationWithoutEscapingTheLocal()
    {
        WithCompiler(compiler => {
            InitializePromotedPair(compiler);
            compiler.lvaSetHiddenBufferStructArg(0);

            foreach (var local in compiler.lvaTable)
            {
                Assert.That(local.lvDoNotEnregister, Is.True);
                Assert.That(local.IsAddressExposed, Is.False);
#if DEBUG
                Assert.That(local.IsDefinedViaAddress, Is.True);
#endif
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MatchingPromotedFieldsBecomeScalarLocalNodes(bool store)
    {
        WithCompiler(compiler => {
            InitializePromotedPair(compiler);
            var data = compiler.gtNewIconNode(TYP_INT, 7);
            GenTree tree = store
                ? new GenTreeLclFld(TYP_INT, 0, 4, data, layout: null)
                : new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, 4);
            tree.Flags |= GTF_DONT_CSE;

            if (store)
            {
                tree.Flags |= GTF_ASG | GTF_VAR_DEF | GTF_VAR_USEASG;
            }

            var original = tree;
            var visitor = new LocalAddressVisitor(compiler);
            visitor.MorphLocalField(ref tree, user: null);

            Assert.That(tree, Is.Not.SameAs(original));
            Assert.That(tree.Oper, Is.EqualTo(store ? GT_STORE_LCL_VAR : GT_LCL_VAR));
            Assert.That(tree.AsLclVar().LclNum, Is.EqualTo(2));
            Assert.That(tree.Flags & GTF_DONT_CSE, Is.EqualTo(GTF_DONT_CSE));
            Assert.That(tree.Flags & GTF_VAR_USEASG, Is.EqualTo(GTF_EMPTY));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.False);

            if (store)
            {
                Assert.That(tree.AsLclVar().Data, Is.SameAs(data));
                Assert.That(tree.Flags & (GTF_ASG | GTF_VAR_DEF), Is.EqualTo(GTF_ASG | GTF_VAR_DEF));
            }
#if DEBUG
            Assert.That(tree.TreeId, Is.EqualTo(original.TreeId));
#endif
        });
    }

    [TestCase(TYP_INT, (ushort)2)]
    [TestCase(TYP_FLOAT, (ushort)4)]
    public static void UnmatchedPromotedFieldsKeepTheAccessAndDisableEnregistration(var_types type, ushort offset)
    {
        WithCompiler(compiler => {
            InitializePromotedPair(compiler);
            GenTree tree = new GenTreeLclFld(GT_LCL_FLD, type, 0, offset);
            var original = tree;
            var visitor = new LocalAddressVisitor(compiler);
            visitor.MorphLocalField(ref tree, user: null);

            Assert.That(tree, Is.SameAs(original));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IndirectPromotedFieldsUpdateBothAddressAndAccessOwners(bool store)
    {
        WithCompiler(compiler => {
            InitializePromotedPair(compiler);
            var address = new GenTreeFieldAddr(TYP_BYREF, compiler.gtNewLclVarAddrNode(TYP_BYREF, 0), null, 4);
            var data = compiler.gtNewIconNode(TYP_INT, 7);
            GenTree tree = store ? compiler.gtNewStoreIndNode(TYP_INT, address, data) : compiler.gtNewIndir(TYP_INT, address);
            var original = tree.AsIndir();
            var visitor = new LocalAddressVisitor(compiler);

            Assert.That(visitor.MorphStructField(ref tree, user: null), Is.True);
            Assert.That(tree.Oper, Is.EqualTo(store ? GT_STORE_LCL_VAR : GT_LCL_VAR));
            Assert.That(tree.AsLclVar().LclNum, Is.EqualTo(2));
            Assert.That(original.Addr.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(original.Addr.AsLclFld().LclNum, Is.EqualTo(2));
            Assert.That(original.Addr, Is.Not.SameAs(address));

            if (store)
            {
                Assert.That(tree.AsLclVar().Data, Is.SameAs(data));
            }
#if DEBUG
            Assert.That(tree.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(original.Addr.TreeId, Is.EqualTo(address.TreeId));
#endif
        });
    }

    [Test]
    public static void DifferentlyTypedIndirectionStillUsesThePromotedFieldAddress()
    {
        WithCompiler(compiler => {
            InitializePromotedPair(compiler);
            var address = new GenTreeFieldAddr(TYP_BYREF, compiler.gtNewLclVarAddrNode(TYP_BYREF, 0), null, 4);
            GenTree tree = compiler.gtNewIndir(TYP_FLOAT, address);
            var original = tree;
            var visitor = new LocalAddressVisitor(compiler);

            Assert.That(visitor.MorphStructField(ref tree, user: null), Is.False);
            Assert.That(tree, Is.SameAs(original));
            Assert.That(tree.AsIndir().Addr.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(tree.AsIndir().Addr.AsLclFld().LclNum, Is.EqualTo(2));
        });
    }

    [Test]
    public static void VolatileIndirectionsRetainTheNativeDereferencedFieldQuirk()
    {
        WithCompiler(compiler => {
            InitializePromotedPair(compiler);
            var address = new GenTreeFieldAddr(TYP_BYREF, compiler.gtNewLclVarAddrNode(TYP_BYREF, 0), null, 4);
            GenTree tree = compiler.gtNewIndir(TYP_INT, address, GTF_IND_VOLATILE);
            var original = tree;
            var visitor = new LocalAddressVisitor(compiler);

            Assert.That(visitor.MorphStructField(ref tree, user: null), Is.False);
            Assert.That(tree, Is.SameAs(original));
            Assert.That(tree.AsIndir().Addr, Is.SameAs(address));

            address.Flags |= GTF_FLD_DEREFERENCED;
            Assert.That(visitor.MorphStructField(ref tree, user: null), Is.True);
            Assert.That(tree.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    private static void InitializePromotedPair(Compiler compiler)
    {
        compiler.lvaTable[0].Type = TYP_STRUCT;
        compiler.lvaTable[0].Layout = new ClassLayout(8);
        compiler.lvaTable[0].lvPromoted = true;
        compiler.lvaTable[0].lvFieldLclStart = 1;
        compiler.lvaTable[0].lvFieldCnt = 2;

        for (var i = 1; i < 3; i++)
        {
            compiler.lvaTable[i].Type = TYP_INT;
            compiler.lvaTable[i].lvIsStructField = true;
            compiler.lvaTable[i].lvParentLcl = 0;
            compiler.lvaTable[i].lvFldOffset = (byte)((i - 1) * sizeof(int));
        }
    }

    [Test]
    public static void EarlyReferenceCountsSaturate()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[0].setLvRefCnt(ushort.MaxValue, RCS_EARLY);
            var visitor = new LocalAddressVisitor(compiler);
            visitor.UpdateEarlyRefCount(0, node: null, user: null);

            Assert.That(compiler.lvaTable[0].lvRefCnt(RCS_EARLY), Is.EqualTo(ushort.MaxValue));
        });
    }

    [Test]
    public static void ReplacingASequencedLocalTransfersLinksAndTheAppendCursor()
    {
        WithCompiler(compiler => {
            compiler.fgNodeThreading = NodeThreading.AllLocals;
            var first = compiler.gtNewLclvNode(TYP_INT, 0);
            var source = new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 1, 0);
            source.Flags |= GTF_DONT_CSE;
            source._vnPair.SetBoth(42);
            var last = compiler.gtNewLclvNode(TYP_INT, 2);
            var left = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, first, source);
            var stmt = compiler.gtNewStmt(compiler.gtNewBinaryNode(GT_ADD, TYP_INT, left, last));
            var sequencer = new LocalSequencer(compiler);
            sequencer.Start(stmt);
            sequencer.SequenceLocal(first);
            sequencer.SequenceLocal(source);
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif
            var replacement = new GenTreeLclVar(GT_LCL_VAR, TYP_INT, 1, data: null, source, NodeThreading.AllLocals);
            left.Op2Ref = replacement;
            sequencer.ReplaceNode(source, replacement);
            sequencer.SequenceLocal(last);
            sequencer.Finish(stmt);

            Assert.That(stmt.TreeListBegin, Is.SameAs(first));
            Assert.That(first.Next, Is.SameAs(replacement));
            Assert.That(replacement.Prev, Is.SameAs(first));
            Assert.That(replacement.Next, Is.SameAs(last));
            Assert.That(last.Prev, Is.SameAs(replacement));
            Assert.That(stmt.TreeListEnd, Is.SameAs(last));
            Assert.That(source.Prev, Is.Null);
            Assert.That(source.Next, Is.Null);
            Assert.That(replacement.Flags, Is.EqualTo(GTF_DONT_CSE));
            Assert.That(replacement._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
#if DEBUG
            Assert.That(replacement.TreeId, Is.EqualTo(source.TreeId));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId));
#endif
        });
    }

    [Test]
    public static void ReplacingTheRootLocalPreservesTheTransientSentinel()
    {
        WithCompiler(compiler => {
            compiler.fgNodeThreading = NodeThreading.AllLocals;
            var source = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var stmt = compiler.gtNewStmt(source);
            var sequencer = new LocalSequencer(compiler);
            sequencer.Start(stmt);
            sequencer.SequenceLocal(source);

            var replacement = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 1, 0, data: null,
                layout: null, source, NodeThreading.AllLocals);
            stmt.RootNodeRef = replacement;
            sequencer.ReplaceNode(source, replacement);
            sequencer.Finish(stmt);

            Assert.That(stmt.TreeListBegin, Is.SameAs(replacement));
            Assert.That(stmt.TreeListEnd, Is.SameAs(replacement));
            Assert.That(replacement.Prev, Is.Null);
            Assert.That(replacement.Next, Is.Null);
            Assert.That(source.Prev, Is.Null);
            Assert.That(source.Next, Is.Null);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.lvaRefCountState = RCS_EARLY;
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
