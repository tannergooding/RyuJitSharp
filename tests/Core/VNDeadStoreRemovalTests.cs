// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
#if DEBUG
using System.Reflection;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.CorInfoGCType;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class VNDeadStoreRemovalTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void RedundantStorePreservesEffectsIdentityAndThreading(bool threaded, bool nested)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            var descriptor = compiler.lvaTable[0];
            descriptor.lvInSsa = true;
            compiler.lvaTable[0] = descriptor;
            if (threaded)
            {
                compiler.fgNodeThreading = NodeThreading.AllTrees;
            }

            var value = store.VNForIntCon(7);
            AddDefinition(compiler, 0, block, null, value);
            var first = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7));
            first.Data._vnPair.SetBoth(value);
            AddDefinition(compiler, 0, block, first, value);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(first));

            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            call.Flags |= GTF_EXCEPT;
            call._vnPair.SetBoth(value);
            var dead = compiler.gtNewStoreLclVarNode(0, call);
            var deadNumber = AddDefinition(compiler, 0, block, dead, value);
            GenTree root = nested
                ? compiler.gtNewBinaryNode(GT_COMMA, TYP_VOID, dead, compiler.gtNewNothingNode())
                : dead;
            var statement = compiler.gtNewStmt(root);
            compiler.fgInsertStmtAtEnd(block, statement);
            if (threaded)
            {
                compiler.fgSetStmtSeq(statement);
            }

            Assert.That(compiler.optVNBasedDeadStoreRemoval(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            var replacement = nested ? statement.RootNode.AsOp().Op1 : statement.RootNode;
            Assert.That(replacement.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(replacement.AsOp().Op1, Is.SameAs(call));
            Assert.That(replacement.AsOp().Op2.Oper, Is.EqualTo(GT_NOP));
            Assert.That((replacement.Flags & GTF_ASG) == 0, Is.True);
            Assert.That((replacement.Flags & (GTF_CALL | GTF_EXCEPT)) == (GTF_CALL | GTF_EXCEPT), Is.True);
            Assert.That((statement.RootNode.Flags & GTF_CALL) != 0, Is.True);
            Assert.That((statement.RootNode.Flags & GTF_ASG) == 0, Is.True);
#if DEBUG
            Assert.That(replacement.TreeId, Is.EqualTo(dead.TreeId));
#endif
            Assert.That(compiler.lvaTable[0].GetPerSsaData(deadNumber).DefNode, Is.Null);
            Assert.That(compiler.lvaTable[0].GetPerSsaData(deadNumber)._vnPair.Conservative, Is.EqualTo(value));
            if (threaded)
            {
                Assert.That(statement.TreeListBegin, Is.SameAs(call));
                Assert.That(call.Next, Is.SameAs(replacement.AsOp().Op2));
                Assert.That(replacement.AsOp().Op2.Next, Is.SameAs(replacement));
                Assert.That(statement.RootNode.Next, Is.Null);
            }
        });
    }

    [Test]
    public static void FirstPrimitiveDefinitionIsNotRemoved()
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            compiler.lvaTable[0].lvInSsa = true;
            var value = store.VNForIntCon(7);
            _ = AddDefinition(compiler, 0, block, null, value);
            var candidate = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7));
            candidate.Data._vnPair.SetBoth(value);
            _ = AddDefinition(compiler, 0, block, candidate, value);
            var statement = compiler.gtNewStmt(candidate);
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.optVNBasedDeadStoreRemoval(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(statement.RootNode, Is.SameAs(candidate));
        });
    }

    [TestCase("NotInSsa")]
    [TestCase("SingleDef")]
    [TestCase("OtherBlock")]
    [TestCase("ExplicitInit")]
    [TestCase("Composite")]
    [TestCase("DifferentValue")]
    public static void NativeGuardsLeaveStoreAndItsDefinitionIntact(string guard)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            compiler.lvaTable[0].lvInSsa = guard != "NotInSsa";
            var oldValue = store.VNForIntCon(7);
            var previousBlock = guard == "OtherBlock" ? new BasicBlock(null, null) : block;
            _ = AddDefinition(compiler, 0, previousBlock, null, oldValue);
            if (guard != "SingleDef")
            {
                var first = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7));
                first.Data._vnPair.SetBoth(oldValue);
                _ = AddDefinition(compiler, 0, previousBlock, first, oldValue);
                compiler.fgInsertStmtAtEnd(previousBlock, compiler.gtNewStmt(first));
            }

            var value = compiler.gtNewIconNode(TYP_INT, guard == "DifferentValue" ? 8 : 7);
            value._vnPair.SetBoth(store.VNForIntCon(guard == "DifferentValue" ? 8 : 7));
            var candidate = compiler.gtNewStoreLclVarNode(guard == "Composite" ? 1 : 0, value);
            if (guard == "ExplicitInit")
            {
                candidate.Flags |= GTF_VAR_EXPLICIT_INIT;
            }
            var number = guard == "SingleDef" ? -1 : AddDefinition(compiler, 0, block, candidate, oldValue);
            var statement = compiler.gtNewStmt(candidate);
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.optVNBasedDeadStoreRemoval(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(statement.RootNode, Is.SameAs(candidate));
            if (number >= 0)
            {
                Assert.That(compiler.lvaTable[0].GetPerSsaData(number).DefNode, Is.SameAs(candidate));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FirstStructZeroInitUsesWholeObjectValueNumber(bool isAsync)
    {
        WithCompiler((compiler, store) =>
        {
            if (isAsync)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_ASYNC);
            }

            var layout = compiler.typGetBlkLayout(8);
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaTable[0].lvInSsa = true;
            var block = new BasicBlock(null, null);
            var value = store.VNForZeroObj(layout);
            _ = AddDefinition(compiler, 0, block, null, value);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            zero._vnPair.SetBoth(store.VNForIntCon(0));
            var candidate = compiler.gtNewStoreLclVarNode(0, zero);
            var number = AddDefinition(compiler, 0, block, candidate, value);
            var statement = compiler.gtNewStmt(candidate);
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.optVNBasedDeadStoreRemoval(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(statement.RootNode.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(statement.RootNode.AsOp().Op1, Is.SameAs(zero));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(number).DefNode, Is.Null);
        });
    }

    [TestCase(0, 0x5678, false)]
    [TestCase(0, 0x5678, true)]
    [TestCase(2, 0x1234, false)]
    [TestCase(2, 0x1234, true)]
    public static void PartialStoreUsesItsPreviousUseDefAndExactOffset(int offset, int matchingValue, bool redundant)
    {
        WithCompiler((compiler, store) =>
        {
            var block = new BasicBlock(null, null);
            compiler.lvaTable[0].lvInSsa = true;
            var wholeValue = store.VNForIntCon(0x12345678);
            var oldNumber = AddDefinition(compiler, 0, block, null, wholeValue);
            var partialValue = store.VNForLoad(ValueNumKind.VNK_Conservative, wholeValue,
                compiler.lvaLclValueSize(0), TYP_SHORT, offset, ValueSize.FromJitType(TYP_SHORT));
            var otherValue = matchingValue == 0x5678 ? 0x1234 : 0x5678;
            var value = compiler.gtNewIconNode(TYP_SHORT, redundant ? matchingValue : otherValue);
            value._vnPair.SetBoth(redundant ? partialValue : store.VNForIntCon(otherValue));
            var candidate = compiler.gtNewStoreLclFldNode(TYP_SHORT, 0, checked((ushort)offset), value);
            candidate.Flags |= GTF_VAR_USEASG;
            var number = AddDefinition(compiler, 0, block, candidate, wholeValue);
            compiler.lvaTable[0].GetPerSsaData(number).UseDefSsaNum = oldNumber;
            var statement = compiler.gtNewStmt(candidate);
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.optVNBasedDeadStoreRemoval(), Is.EqualTo(redundant
                ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
            Assert.That(statement.RootNode.Oper, Is.EqualTo(redundant ? GT_COMMA : GT_STORE_LCL_FLD));
            Assert.That(compiler.lvaTable[0].GetPerSsaData(number).DefNode,
                redundant ? Is.Null : Is.SameAs(candidate));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AsyncByrefLocalIsNotEliminated(bool structWithByref)
    {
        WithCompiler((compiler, store) =>
        {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_ASYNC);
            if (structWithByref)
            {
                var layout = new ClassLayout(8) { GCPtrCount = 1, _gcPtrs = [TYPE_GC_BYREF] };
                compiler.lvaTable[0].Type = TYP_STRUCT;
                compiler.lvaTable[0].Layout = layout;
            }
            else
            {
                compiler.lvaTable[0].Type = TYP_BYREF;
            }

            compiler.lvaTable[0].lvInSsa = true;
            var block = new BasicBlock(null, null);
            var data = compiler.gtNewIconNode(TYP_INT, 0);
            var value = store.VNForIntCon(0);
            data._vnPair.SetBoth(value);
            _ = AddDefinition(compiler, 0, block, null, value);
            var candidate = compiler.gtNewStoreLclVarNode(0, data);
            _ = AddDefinition(compiler, 0, block, candidate, value);
            var statement = compiler.gtNewStmt(candidate);
            compiler.fgInsertStmtAtEnd(block, statement);

            Assert.That(compiler.optVNBasedDeadStoreRemoval(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(statement.RootNode, Is.SameAs(candidate));
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void DebugMethodRangeControlsRemoval(bool enabled)
    {
        WithCompiler((compiler, store) =>
        {
            var field = typeof(Compiler).GetField("s_jitEnableVNBasedDeadStoreRemovalRange",
                BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException();
            var original = field.GetValue(null);
            var hash = unchecked((uint)compiler.info.compMethodHash());
            var selection = enabled ? hash : unchecked(hash + 1);
            var bytes = Encoding.ASCII.GetBytes($"{selection:x8}\0");
            var range = default(ConfigMethodRange);
            fixed (byte* pointer = bytes)
            {
                range.EnsureInit(pointer);
            }

            var block = new BasicBlock(null, null);
            compiler.lvaTable[0].lvInSsa = true;
            var value = store.VNForIntCon(7);
            _ = AddDefinition(compiler, 0, block, null, value);
            var first = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7));
            _ = AddDefinition(compiler, 0, block, first, value);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(first));
            var data = compiler.gtNewIconNode(TYP_INT, 7);
            data._vnPair.SetBoth(value);
            var candidate = compiler.gtNewStoreLclVarNode(0, data);
            _ = AddDefinition(compiler, 0, block, candidate, value);
            var statement = compiler.gtNewStmt(candidate);
            compiler.fgInsertStmtAtEnd(block, statement);

            try
            {
                field.SetValue(null, range);
                Assert.That(compiler.optVNBasedDeadStoreRemoval(), Is.EqualTo(enabled
                    ? PhaseStatus.MODIFIED_EVERYTHING : PhaseStatus.MODIFIED_NOTHING));
                Assert.That(statement.RootNode.Oper, Is.EqualTo(enabled ? GT_COMMA : GT_STORE_LCL_VAR));
            }
            finally
            {
                field.SetValue(null, original);
            }
        });
    }
#endif

    private static int AddDefinition(Compiler compiler, int local, BasicBlock block,
        GenTreeLclVarCommon? definition, int value)
    {
        ref var descriptor = ref compiler.lvaTable[local];
        var number = descriptor.lvPerSsaData.AllocSsaNum();
        ref var data = ref descriptor.GetPerSsaData(number);
        data = definition is null ? new LclSsaVarDsc(block) : new LclSsaVarDsc(block, definition);
        data._vnPair.SetBoth(value);
        definition?.SsaNum = number;
        return number;
    }

    private static void WithCompiler(Action<Compiler, ValueNumStore> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 2;
#if DEBUG
        compiler.info.compFullName = nameof(VNDeadStoreRemovalTests);
#endif
        JitTls.Compiler = compiler;
        try
        {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            action(compiler, store);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
