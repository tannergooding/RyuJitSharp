// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
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

    [TestCase(TYP_INT, TYP_INT, false, GT_LCL_VAR, TYP_INT)]
    [TestCase(TYP_INT, TYP_FLOAT, false, GT_BITCAST, TYP_FLOAT)]
    [TestCase(TYP_INT, TYP_SHORT, false, GT_CAST, TYP_INT)]
    [TestCase(TYP_INT, TYP_SHORT, true, GT_LCL_FLD, TYP_SHORT)]
    public static void LocalIndirectionsHonorTypeAndOptimizationGates(
        var_types localType, var_types accessType, bool minOpts, genTreeOps expectedOper, var_types expectedType)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = localType;
            var address = compiler.gtNewLclVarAddrNode(TYP_BYREF, 0);
            var indir = compiler.gtNewIndir(accessType, address);
            var user = compiler.gtNewUnaryNode(GT_RETURN, accessType.ActualType, indir);
            var visitor = new LocalAddressVisitor(compiler);
            visitor.MorphLocalIndir(ref user.Op1Ref, 0, 0, user);
            var result = user.Op1;

            Assert.That(result.Oper, Is.EqualTo(expectedOper));
            Assert.That(result.Type, Is.EqualTo(expectedType));
            var local = result.Oper.IsAnyLocal ? result : result.AsUnOp().Op1;
            Assert.That(local.AsLclVarCommon().LclNum, Is.Zero);
            Assert.That(local.Flags, Is.EqualTo(GTF_EMPTY));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.EqualTo(expectedOper is GT_LCL_FLD));
#if DEBUG
            Assert.That(local.TreeId, Is.EqualTo(result.Oper.IsAnyLocal ? indir.TreeId : address.TreeId));

            if (expectedOper is GT_CAST)
            {
                Assert.That(result.TreeId, Is.GreaterThan(user.TreeId));
            }
            else
            {
                Assert.That(result.TreeId, Is.EqualTo(indir.TreeId));
            }
#endif
        }, minOpts);
    }

    [Test]
    public static void UnusedIndirectLocalLoadsBecomeNopsWithoutAllocatingNodes()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_INT;
            GenTree tree = compiler.gtNewIndir(TYP_INT, compiler.gtNewLclVarAddrNode(TYP_BYREF, 0));
            var original = tree;
            var visitor = new LocalAddressVisitor(compiler);
            visitor.MorphLocalIndir(ref tree, 0, 0, user: null);

            Assert.That(tree, Is.SameAs(original));
            Assert.That(tree.Oper, Is.EqualTo(GT_NOP));
            Assert.That(tree.Type, Is.EqualTo(TYP_VOID));
            Assert.That(tree.Flags & GTF_ALL_EFFECT, Is.EqualTo(GTF_EMPTY));
        });
    }

    [Test]
    public static void LocalIndirectionToPromotedFieldRefreshesTheReplacementAlias()
    {
        WithCompiler(compiler => {
            InitializePromotedPair(compiler);
            var data = compiler.gtNewIndir(TYP_INT, compiler.gtNewIconNode(TYP_I_IMPL, 16));
            GenTree tree = compiler.gtNewStoreIndNode(TYP_INT, compiler.gtNewLclVarAddrNode(TYP_BYREF, 0), data);
            var visitor = new LocalAddressVisitor(compiler);
            visitor.MorphLocalIndir(ref tree, 0, 4, user: null);

            Assert.That(tree.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(tree.AsLclVar().LclNum, Is.EqualTo(2));
            Assert.That(tree.AsLclVar().Data, Is.SameAs(data));
            Assert.That(tree.Flags, Is.EqualTo((data.Flags & GTF_ALL_EFFECT) | GTF_ASG | GTF_VAR_DEF));
        });
    }

    [Test]
    public static void PartialSmallLocalStoresForceNormalizationOnLoad()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SHORT;
            GenTree tree = compiler.gtNewStoreIndNode(TYP_BYTE, compiler.gtNewLclVarAddrNode(TYP_BYREF, 0),
                compiler.gtNewIconNode(TYP_INT, 1));
            var visitor = new LocalAddressVisitor(compiler);
            visitor.MorphLocalIndir(ref tree, 0, 1, user: null);

            Assert.That(tree.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(tree.AsLclFld().LclOffs, Is.EqualTo(1));
            Assert.That(tree.Flags & GTF_VAR_USEASG, Is.EqualTo(GTF_VAR_USEASG));
            Assert.That(compiler.lvaTable[0].IsAddressExposed, Is.True);
            Assert.That(compiler.lvaTable[0].lvNormalizeOnLoad, Is.True);
        }, minOpts: true);
    }

    [TestCase(false, 0u, NI_Vector_ToScalar)]
    [TestCase(false, 4u, NI_Vector_GetElement)]
    [TestCase(true, 4u, NI_Vector_WithElement)]
    public static void VectorLocalFieldAccessesUseElementOperations(bool store, uint offset, NamedIntrinsic expectedId)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var address = compiler.gtNewLclVarAddrNode(TYP_BYREF, 0);
            GenTree tree = store
                ? compiler.gtNewStoreIndNode(TYP_FLOAT, address, compiler.gtNewDconNode(TYP_FLOAT, 1))
                : compiler.gtNewIndir(TYP_FLOAT, address);
            var user = compiler.gtNewUnaryNode(GT_RETURN, tree.Type, tree);
            var visitor = new LocalAddressVisitor(compiler);
            visitor.MorphLocalIndir(ref user.Op1Ref, 0, offset, user);
            var result = user.Op1;
            var intrinsic = (store ? result.AsLclVar().Data : result).AsHWIntrinsic();

            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(expectedId));
            Assert.That(intrinsic.GetOp(1).AsLclVar().LclNum, Is.Zero);

            if (store)
            {
                Assert.That(result.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(result.Type, Is.EqualTo(TYP_SIMD16));
                Assert.That(result.Flags, Is.EqualTo(GTF_ASG | GTF_VAR_DEF));
            }
        });
    }

    [TestCase(false, -1)]
    [TestCase(false, 4)]
    [TestCase(true, 4)]
    public static void VectorElementFactoriesPreserveOutOfRangeChecks(bool store, int index)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var vector = compiler.gtNewLclVarNode(TYP_UNDEF, 0);
            var indexNode = compiler.gtNewIconNode(TYP_INT, index);
            var tree = store
                ? compiler.gtNewSimdWithElementNode(TYP_SIMD16, vector, indexNode,
                    compiler.gtNewDconNode(TYP_FLOAT, 1), TYP_FLOAT, 16)
                : compiler.gtNewSimdGetElementNode(TYP_FLOAT, vector, indexNode, TYP_FLOAT, 16);
            var checkedIndex = tree.AsHWIntrinsic().GetOp(2).AsOp();
            var check = checkedIndex.Op1.AsBoundsChk();

            Assert.That(checkedIndex.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(check.Index.AsIntCon().IconValue, Is.EqualTo((nint)index));
            Assert.That(check.ArrayLength.AsIntCon().IconValue, Is.EqualTo((nint)4));
            Assert.That(checkedIndex.Op2.AsIntCon().IconValue, Is.EqualTo((nint)index));
            Assert.That(tree.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EXCEPT));
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

    [TestCase(0u, GT_LCL_ADDR)]
    [TestCase(uint.MaxValue, GT_ADD)]
    public static void EscapedAddressesPreserveUnsignedOffsetsAndUpdateTheirOwners(uint offset, genTreeOps expectedOper)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_INT;
            var address = compiler.gtNewLclVarAddrNode(TYP_BYREF, 0);
            var constant = compiler.gtNewIconNode(TYP_I_IMPL, unchecked((nint)(nuint)offset));
            var original = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, address, constant);
            var stmt = compiler.gtNewStmt(original);
            var value = new LocalAddressVisitor.Value(stmt, ref stmt.RootNodeRef, owner: null);
            value.Address(0, offset);
            var visitor = new LocalAddressVisitor(compiler);
            visitor.EscapeValue(ref value, user: null);
            var result = stmt.RootNode;

            Assert.That(result, Is.Not.SameAs(original));
            Assert.That(result.Oper, Is.EqualTo(expectedOper));
            Assert.That(result.Flags, Is.EqualTo(GTF_EMPTY));
            Assert.That(compiler.lvaTable[0].IsAddressExposed, Is.True);

            if (expectedOper is GT_ADD)
            {
                Assert.That(result.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)(nuint)offset)));
            }
#if DEBUG
            Assert.That(result.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(value.IsConsumed, Is.True);
#endif
        });
    }

    [Test]
    public static void WideAccessesExposeTheParentOfPromotedFields()
    {
        WithCompiler(compiler => {
            InitializePromotedPair(compiler);
            var address = compiler.gtNewLclVarAddrNode(TYP_BYREF, 1);
            var indir = compiler.gtNewIndir(TYP_LONG, address);
            var stmt = compiler.gtNewStmt(indir);
            var value = new LocalAddressVisitor.Value(stmt, ref indir.AddrRef, indir);
            value.Address(1, 0);
            var visitor = new LocalAddressVisitor(compiler);
            visitor.ProcessIndirection(ref stmt.RootNodeRef, ref value, user: null);

            Assert.That(stmt.RootNode, Is.SameAs(indir));
            Assert.That(indir.Addr, Is.Not.SameAs(address));
            Assert.That(indir.Addr.AsLclFld().LclNum, Is.EqualTo(1));
            Assert.That(compiler.lvaTable[0].IsAddressExposed, Is.True);
            Assert.That(compiler.lvaTable[1].IsAddressExposed, Is.True);
            Assert.That(indir.Flags & GTF_GLOB_REF, Is.EqualTo(GTF_GLOB_REF));
        });
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

    [TestCase(false)]
    [TestCase(true)]
    public static void StatementTraversalRewritesIndirectLoadsAndRebuildsEffectsAndLocals(bool sequenceLocals)
    {
        WithCompiler(compiler => {
            compiler.fgNodeThreading = sequenceLocals ? NodeThreading.AllLocals : NodeThreading.None;
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[1].Type = TYP_INT;
            var indir = compiler.gtNewIndir(TYP_INT, compiler.gtNewLclVarAddrNode(TYP_BYREF, 0));
            var store = compiler.gtNewStoreLclVarNode(1, indir);
            var stmt = compiler.gtNewStmt(store);
            var visitor = new LocalAddressVisitor(compiler, sequenceLocals ? new LocalSequencer(compiler) : null);
            visitor.VisitStmt(stmt);

            Assert.That(visitor.MadeChanges, Is.True);
            Assert.That(store.Data.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(store.Data.AsLclVar().LclNum, Is.Zero);
            Assert.That(store.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EMPTY));
            Assert.That(store.Flags & GTF_ASG, Is.EqualTo(GTF_ASG));
            Assert.That(compiler.lvaTable[0].IsAddressExposed, Is.False);
            Assert.That(compiler.lvaTable[0].lvRefCnt(RCS_EARLY), Is.EqualTo(1));

            if (sequenceLocals)
            {
                Assert.That(stmt.TreeListBegin, Is.SameAs(store.Data));
                Assert.That(stmt.TreeListEnd, Is.SameAs(store));
                Assert.That(store.Data.Next, Is.SameAs(store));
                Assert.That(store.Prev, Is.SameAs(store.Data));
            }
#if DEBUG
            Assert.That(store.Data.TreeId, Is.EqualTo(indir.TreeId));
#endif
        }, minOpts: true);
    }

    [Test]
    public static void AddressDifferencesReplaceSequencedRootsWithoutExposingTheLocal()
    {
        WithCompiler(compiler => {
            compiler.fgNodeThreading = NodeThreading.AllLocals;
            compiler.lvaTable[0].Type = TYP_INT;
            var lhs = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 3);
            var rhs = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0);
            var difference = compiler.gtNewBinaryNode(GT_SUB, TYP_BYREF, lhs, rhs);
            difference._vnPair.SetBoth(42);
            var stmt = compiler.gtNewStmt(difference);
            var visitor = new LocalAddressVisitor(compiler, new LocalSequencer(compiler));
            visitor.VisitStmt(stmt);

            Assert.That(stmt.RootNode.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(stmt.RootNode.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(stmt.RootNode.AsIntCon().IconValue, Is.EqualTo((nint)3));
            Assert.That(stmt.RootNode._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(stmt.TreeListBegin, Is.Null);
            Assert.That(stmt.TreeListEnd, Is.Null);
            Assert.That(compiler.lvaTable[0].IsAddressExposed, Is.False);
#if DEBUG
            Assert.That(stmt.RootNode.TreeId, Is.EqualTo(difference.TreeId));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ImplicitByRefRetypingChangesOnlyPointerParameters(bool implicitByRef)
    {
        WithCompiler(compiler => {
            compiler.info.compArgsCount = 1;
            ref var arg = ref compiler.lvaTable[0];
            arg.Type = TYP_STRUCT;
            arg.Layout = new ClassLayout(16);
            arg.lvIsParam = true;
            arg.IsImplicitByRef = implicitByRef;
            arg.SetAddressExposed(true, AddressExposedReason.ESCAPE_ADDRESS);
            arg.lvDoNotEnregister = true;
            compiler.lvaTable[1].Type = TYP_INT;

            var status = compiler.fgRetypeImplicitByRefArgs();

            Assert.That(status, Is.EqualTo(implicitByRef ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(arg.Type, Is.EqualTo(implicitByRef ? TYP_BYREF : TYP_STRUCT));
            Assert.That(arg.IsAddressExposed, Is.EqualTo(!implicitByRef));
            Assert.That(arg.lvDoNotEnregister, Is.EqualTo(!implicitByRef));
            Assert.That(arg.IsImplicitByRef, Is.EqualTo(implicitByRef));
            Assert.That(compiler.lvaTable[1].Type, Is.EqualTo(TYP_INT));
            Assert.That(compiler.lvaCount, Is.EqualTo(3));
        }, minOpts: true);
    }

    [TestCase(false, 2, 0, false)]
    [TestCase(false, 3, 0, true)]
    [TestCase(false, 4, 2, false)]
    [TestCase(true, 9, 0, false)]
    public static void ImplicitByRefRetypingPreservesPromotionOwnership(bool dependent, int totalUses, int callUses, bool keep)
    {
        WithCompiler(compiler => {
            compiler.info.compArgsCount = 1;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            var originalTable = compiler.lvaTable;
            var layout = new ClassLayout(16);
            ref var arg = ref compiler.lvaTable[0];
            arg.Type = TYP_STRUCT;
            arg.Layout = layout;
            arg.lvIsParam = true;
            arg.IsImplicitByRef = true;
            arg.lvPromoted = true;
            arg.lvFieldLclStart = 1;
            arg.lvFieldCnt = 2;
            arg.lvContainsHoles = true;
            arg.lvDoNotEnregister = dependent;
            arg.SetAddressExposed(dependent, AddressExposedReason.ESCAPE_ADDRESS);
            arg.lvSingleDef = true;
            arg.lvSingleDefRegCandidate = true;
            arg.lvSpillAtSingleDef = true;
            arg.setLvRefCnt((ushort)totalUses, RCS_EARLY);
            arg.setLvRefCntWtd(callUses, RCS_EARLY);

            for (var i = 1; i < 3; i++)
            {
                ref var field = ref compiler.lvaTable[i];
                field.Type = TYP_LONG;
                field.lvIsStructField = true;
                field.lvParentLcl = 0;
                field.lvFldOffset = (byte)((i - 1) * 8);
                field.lvIsParam = true;
                field.lvIsRegArg = true;
                field.lvIsMultiRegArg = true;
                field.lvIsOSRLocal = true;
                field.lvIsOSRExposedLocal = true;
            }

            var block = new BasicBlock(null, null);
            compiler.fgFirstBB = block;
            var status = compiler.fgRetypeImplicitByRefArgs();

            Assert.That(status, Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.lvaTable, Is.Not.SameAs(originalTable));
            Assert.That(compiler.lvaCount, Is.EqualTo(4));
            ref var pointer = ref compiler.lvaTable[0];
            Assert.That(pointer.Type, Is.EqualTo(TYP_BYREF));
            Assert.That(pointer.lvPromoted, Is.EqualTo(keep));
            Assert.That(pointer.lvFieldLclStart, Is.EqualTo(3));
            Assert.That(pointer.lvFieldCnt, Is.Zero);
            Assert.That(pointer.IsAddressExposed, Is.False);
            Assert.That(pointer.lvDoNotEnregister, Is.False);
            ref var temp = ref compiler.lvaTable[3];
            Assert.That(temp.Layout, Is.SameAs(layout));
            Assert.That(temp.lvPromoted, Is.True);
            Assert.That(temp.lvFieldLclStart, Is.EqualTo(1));
            Assert.That(temp.lvFieldCnt, Is.EqualTo(2));
            Assert.That(temp.lvContainsHoles, Is.True);
            Assert.That(temp.IsAddressExposed, Is.EqualTo(dependent));
            Assert.That(temp.lvDoNotEnregister, Is.EqualTo(dependent));
            Assert.That(temp.lvSingleDef && temp.lvSingleDefRegCandidate && temp.lvSpillAtSingleDef, Is.True);

            for (var i = 1; i < 3; i++)
            {
                ref var field = ref compiler.lvaTable[i];
                Assert.That(field.lvParentLcl, Is.EqualTo(keep ? 3 : 0));
                Assert.That(field.lvIsParam || field.lvIsRegArg || field.lvIsMultiRegArg, Is.False);
                Assert.That(field.lvIsOSRLocal || field.lvIsOSRExposedLocal, Is.False);
            }

            if (keep)
            {
                var stmt = block.FirstStmt ?? throw new InvalidOperationException("Missing promotion initializer.");
                var store = stmt.RootNode.AsLclVarCommon();
                Assert.That(store.LclNum, Is.EqualTo(3));
                Assert.That(store.Data.Oper, Is.EqualTo(GT_BLK));
                Assert.That(store.Data.AsBlk().Layout, Is.SameAs(layout));
                Assert.That(store.Data.AsIndir().Addr.Type, Is.EqualTo(TYP_BYREF));
                Assert.That(store.Data.AsIndir().Addr.AsLclVarCommon().LclNum, Is.Zero);
                Assert.That(stmt.NextStmt, Is.Null);
            }
            else
            {
                Assert.That(block.FirstStmt, Is.Null);
            }

            compiler.fgMarkDemotedImplicitByRefArgs();
            Assert.That(pointer.lvPromoted, Is.False);
            Assert.That(pointer.lvFieldLclStart, Is.Zero);
            Assert.That(compiler.lvaTable[1].lvParentLcl, Is.EqualTo(3));
            Assert.That(compiler.lvaTable[2].lvParentLcl, Is.EqualTo(3));
        });
    }

    [TestCase(GT_LCL_VAR, GT_BLK)]
    [TestCase(GT_STORE_LCL_VAR, GT_STORE_BLK)]
    [TestCase(GT_LCL_FLD, GT_IND)]
    [TestCase(GT_STORE_LCL_FLD, GT_STOREIND)]
    [TestCase(GT_LCL_ADDR, GT_ADD)]
    public static void ImplicitByRefExpansionReplacesOwnersAndPreservesPointerIdentity(genTreeOps oper, genTreeOps expectedOper)
    {
        WithCompiler(compiler => {
            var layout = new ClassLayout(16);
            ref var arg = ref compiler.lvaTable[0];
            arg.Type = TYP_STRUCT;
            arg.Layout = layout;
            arg.lvIsParam = true;
            arg.IsImplicitByRef = true;
            compiler.lvaTable[2].Type = TYP_STRUCT;
            compiler.lvaTable[2].Layout = layout;
            var structValue = compiler.gtNewLclvNode(TYP_STRUCT, 2);
            var scalarValue = compiler.gtNewIconNode(TYP_INT, 42);
            GenTreeLclVarCommon local = oper switch {
                GT_LCL_VAR => compiler.gtNewLclvNode(TYP_STRUCT, 0),
                GT_STORE_LCL_VAR => compiler.gtNewStoreLclVarNode(0, structValue),
                GT_LCL_FLD => new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 0, 4),
                GT_STORE_LCL_FLD => new GenTreeLclFld(TYP_INT, 0, 4, scalarValue, layout: null),
                GT_LCL_ADDR => new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 4),
                _ => throw new ArgumentOutOfRangeException(nameof(oper)),
            };
            local.Flags |= GTF_VAR_DEATH | GTF_GLOB_REF;
            local._vnPair.SetBoth(42);
            local.SsaNum = 9;
            var statement = compiler.gtNewStmt(local);
            arg.Type = TYP_BYREF;
            compiler.fgGlobalMorph = true;

            statement.RootNodeRef = compiler.fgMorphExpandLocal(local) ??
                throw new InvalidOperationException("Missing implicit-byref expansion.");

            var result = statement.RootNode;
            Assert.That(result, Is.Not.SameAs(local));
            Assert.That(result.Oper, Is.EqualTo(expectedOper));
            var address = result.Oper is GT_ADD ? result : result.AsIndir().Addr;
            var pointer = address.Oper is GT_ADD ? address.AsOp().Op1 : address;
            Assert.That(pointer.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(pointer.Type, Is.EqualTo(TYP_BYREF));
            Assert.That(pointer.AsLclVarCommon().LclNum, Is.Zero);
            Assert.That(pointer.AsLclVarCommon().HasSsaName, Is.False);
            Assert.That(pointer.Flags & GTF_ALL_EFFECT, Is.EqualTo(GTF_EMPTY));
            Assert.That(pointer.Flags & GTF_VAR_DEATH, Is.EqualTo(GTF_VAR_DEATH));
            Assert.That(pointer._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
#if DEBUG
            Assert.That(pointer.TreeId, Is.EqualTo(local.TreeId));
#endif
            if (address.Oper is GT_ADD)
            {
                Assert.That(address.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)4));
            }

            if (result.Oper.IsIndir)
            {
                Assert.That(result.Flags & GTF_GLOB_REF, Is.EqualTo(GTF_GLOB_REF));
            }

            if (oper.IsLocalStore)
            {
                Assert.That(result.AsIndir().Data, Is.SameAs(local.Data));
                Assert.That(result.Flags & GTF_ASG, Is.EqualTo(GTF_ASG));
            }

            if (result.Oper.IsBlk)
            {
                Assert.That(result.AsBlk().Layout, Is.SameAs(layout));
            }
        });
    }

    [Test]
    public static void ImplicitByRefExpansionRecognizesPointersAndKeptPromotion()
    {
        WithCompiler(compiler => {
            ref var arg = ref compiler.lvaTable[0];
            arg.Type = TYP_STRUCT;
            arg.Layout = new ClassLayout(16);
            arg.lvIsParam = true;
            arg.IsImplicitByRef = true;
            var original = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            arg.Type = TYP_BYREF;
            var pointer = compiler.gtNewLclvNode(TYP_BYREF, 0);
            Assert.That(compiler.fgMorphExpandLocal(original), Is.Null);
            compiler.fgGlobalMorph = true;
            Assert.That(compiler.fgMorphExpandLocal(pointer), Is.Null);

            arg.lvPromoted = true;
            arg.lvFieldLclStart = 2;
            compiler.lvaTable[2].Type = TYP_STRUCT;
            compiler.lvaTable[2].Layout = arg.Layout;
            original.SsaNum = 9;
            Assert.That(compiler.fgMorphExpandLocal(original), Is.SameAs(original));
            Assert.That(original.LclNum, Is.EqualTo(2));
            Assert.That(original.Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(original.HasSsaName, Is.False);
        });
    }

    [Test]
    public static void ImplicitByRefFieldExpansionCombinesOffsetsWithoutClaimingParentDeath()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].IsImplicitByRef = true;
            compiler.lvaTable[1].Type = TYP_LONG;
            compiler.lvaTable[1].lvIsStructField = true;
            compiler.lvaTable[1].lvParentLcl = 0;
            compiler.lvaTable[1].lvFldOffset = 8;
            compiler.fgGlobalMorph = true;
            var field = new GenTreeLclFld(GT_LCL_FLD, TYP_INT, 1, 4) { Flags = GTF_VAR_DEATH };
            var expanded = compiler.fgMorphExpandLocal(field) ??
                throw new InvalidOperationException("Missing field expansion.");
            var address = expanded.AsIndir().Addr.AsOp();
            Assert.That(address.Oper, Is.EqualTo(GT_ADD));
            Assert.That(address.Op2.AsIntCon().IconValue, Is.EqualTo((nint)12));
            Assert.That(address.Op1.AsLclVarCommon().LclNum, Is.Zero);
            Assert.That(address.Op1.Flags & GTF_VAR_DEATH, Is.EqualTo(GTF_EMPTY));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DemotedImplicitByRefDeathRequiresEveryField(bool allFieldsDying)
    {
        WithCompiler(compiler => {
            var layout = new ClassLayout(16);
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].IsImplicitByRef = true;
            compiler.lvaTable[0].lvFieldLclStart = 2;
            compiler.lvaTable[2].Type = TYP_STRUCT;
            compiler.lvaTable[2].Layout = layout;
            compiler.lvaTable[2].lvPromoted = true;
            compiler.lvaTable[2].lvFieldCnt = 2;
            var local = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            compiler.lvaTable[0].Type = TYP_BYREF;
            local.Flags = allFieldsDying ? compiler.lvaTable[2].AllFieldDeathFlags : GTF_VAR_DEATH;
            var expanded = compiler.fgMorphExpandImplicitByRefArg(local) ??
                throw new InvalidOperationException("Missing demoted-parameter expansion.");
            Assert.That((expanded.AsIndir().Addr.Flags & GTF_VAR_DEATH) != 0, Is.EqualTo(allFieldsDying));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalStoreExpansionNormalizesOnlyUnaliasedGlobalMorphStores(bool globalMorph, bool exposed)
    {
        WithCompiler(compiler => {
            compiler.fgGlobalMorph = globalMorph;
            compiler.lvaTable[0].Type = TYP_BYTE;
            compiler.lvaTable[0].SetAddressExposed(exposed, AddressExposedReason.ESCAPE_ADDRESS);
            var value = compiler.gtNewIconNode(TYP_INT, 300);
            var store = compiler.gtNewStoreLclVarNode(0, value);
            var expanded = compiler.fgMorphExpandLocal(store);

            if (globalMorph && !exposed)
            {
                Assert.That(expanded, Is.SameAs(store));
                Assert.That(store.Type, Is.EqualTo(TYP_INT));
                Assert.That(store.Data.Oper, Is.EqualTo(GT_CAST));
                Assert.That(store.Data.AsCast().CastType, Is.EqualTo(TYP_BYTE));
                Assert.That(store.Data.AsCast().Op1, Is.SameAs(value));
                var cast = store.Data;
                Assert.That(compiler.fgMorphExpandLocal(store), Is.Null);
                Assert.That(store.Data, Is.SameAs(cast));
            }
            else
            {
                Assert.That(expanded, Is.Null);
                Assert.That(store.Data, Is.SameAs(value));
            }
        });
    }

    private static void WithCompiler(Action<Compiler> action, bool minOpts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
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
