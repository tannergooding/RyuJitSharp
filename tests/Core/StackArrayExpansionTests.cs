// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class StackArrayExpansionTests
{
    [TestCase(CORINFO_HELP_NEWARR_1_DIRECT)]
    [TestCase(CORINFO_HELP_NEWARR_1_VC)]
    [TestCase(CORINFO_HELP_NEWARR_1_PTR)]
    [TestCase(CORINFO_HELP_NEWARR_1_ALIGN8)]
    public static void StackArrayHelperInitializesHeaderAndReplacesOwningUse(CorInfoHelpFunc helper)
    {
        WithCompiler((compiler, block) =>
        {
            var call = NewArrayCall(compiler, helper, local: 0, methodTable: 123, length: 3,
                stackAllocated: true);
            var originalAddress = call.Args.FindWellKnownArg(WellKnownArg.StackArrayLocal)!.Node;
            var store = compiler.gtNewStoreLclVarNode(2, call);
            var statement = Append(compiler, block, store);
            compiler.MethodHasStackAllocatedArray = true;

            Assert.That(compiler.fgExpandStackArrayAllocations(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var statements = Collect(block);
            Assert.That(statements, Has.Count.EqualTo(3));
            var mtStore = statements[0].RootNode.AsIndir();
            Assert.That(mtStore.Oper, Is.EqualTo(GT_STOREIND));
            Assert.That(mtStore.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(mtStore.Addr.AsLclVarCommon().LclNum, Is.EqualTo(0));
            Assert.That(mtStore.Data.AsIntCon().IconValue, Is.EqualTo((nint)123));

            var lengthStore = statements[1].RootNode.AsIndir();
            Assert.That(lengthStore.Oper, Is.EqualTo(GT_STOREIND));
            Assert.That(lengthStore.Type, Is.EqualTo(TYP_INT));
            Assert.That(lengthStore.Data.AsIntCon().IconValue, Is.EqualTo((nint)3));
            Assert.That(lengthStore.Addr.Oper, Is.EqualTo(GT_ADD));
            var lengthAddress = lengthStore.Addr.AsOp();
            Assert.That(lengthAddress.Op1.AsLclVarCommon().LclNum, Is.EqualTo(0));
            Assert.That(lengthAddress.Op2.AsIntCon().IconValue,
                Is.EqualTo((nint)OFFSETOF__CORINFO_Array__length));

            Assert.That(statements[2], Is.SameAs(statement));
            Assert.That(store.Data.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(store.Data.AsLclVarCommon().LclNum, Is.EqualTo(0));
            Assert.That(store.Data, Is.Not.SameAs(originalAddress));
            Assert.That(compiler.fgExpandStackArrayAllocations(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [Test]
    public static void OrdinaryAndUnrecognizedCallsStayUntouched()
    {
        WithCompiler((compiler, block) =>
        {
            var noStackArg = NewArrayCall(compiler, CORINFO_HELP_NEWARR_1_PTR, 0, 11, 2,
                stackAllocated: false);
            var unsupported = NewArrayCall(compiler, CORINFO_HELP_NEWARR_1_MAYBEFROZEN, 1, 22, 4,
                stackAllocated: true);
            var userCall = compiler.gtNewCallNode(TYP_I_IMPL, gtCallTypes.CT_USER_FUNC, NO_METHOD_HANDLE);
            var ordinary = Append(compiler, block, compiler.gtNewStoreLclVarNode(2, noStackArg));
            var other = Append(compiler, block, compiler.gtNewStoreLclVarNode(2, unsupported));
            var user = Append(compiler, block, compiler.gtNewStoreLclVarNode(2, userCall));
            compiler.MethodHasStackAllocatedArray = true;

            Assert.That(compiler.fgExpandStackArrayAllocations(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(Collect(block), Has.Count.EqualTo(3));
            Assert.That(block.FirstStmt, Is.SameAs(ordinary));
            Assert.That(ordinary.NextStmt, Is.SameAs(other));
            Assert.That(other.NextStmt, Is.SameAs(user));
            Assert.That(ordinary.RootNode.AsLclVar().Data, Is.SameAs(noStackArg));
            Assert.That(other.RootNode.AsLclVar().Data, Is.SameAs(unsupported));
            Assert.That(user.RootNode.AsLclVar().Data, Is.SameAs(userCall));
        });
    }

    [Test]
    public static void PhaseGateLeavesStackArrayCallUntilMethodIsMarked()
    {
        WithCompiler((compiler, block) =>
        {
            var call = NewArrayCall(compiler, CORINFO_HELP_NEWARR_1_DIRECT, 0, 123, 3,
                stackAllocated: true);
            var statement = Append(compiler, block, compiler.gtNewStoreLclVarNode(2, call));

            Assert.That(compiler.fgExpandStackArrayAllocations(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(statement));
            Assert.That(statement.NextStmt, Is.Null);
            Assert.That(statement.RootNode.AsLclVar().Data, Is.SameAs(call));
        });
    }

    [Test]
    public static void TwoAllocationsInOneStatementPreserveHeaderOrderAndReplaceBothUses()
    {
        WithCompiler((compiler, block) =>
        {
            var first = NewArrayCall(compiler, CORINFO_HELP_NEWARR_1_DIRECT, 0, 111, 3,
                stackAllocated: true);
            var second = NewArrayCall(compiler, CORINFO_HELP_NEWARR_1_ALIGN8, 1, 222, 5,
                stackAllocated: true);
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_I_IMPL, first, second);
            sum.Flags |= GTF_CALL;
            var store = compiler.gtNewStoreLclVarNode(2, sum);
            var statement = Append(compiler, block, store);
            compiler.MethodHasStackAllocatedArray = true;

            Assert.That(compiler.fgExpandStackArrayAllocations(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var statements = Collect(block);
            Assert.That(statements, Has.Count.EqualTo(5));
            Assert.That(statements[4], Is.SameAs(statement));
            Assert.That(statements[0].RootNode.AsIndir().Data.AsIntCon().IconValue, Is.EqualTo((nint)111));
            Assert.That(statements[1].RootNode.AsIndir().Data.AsIntCon().IconValue, Is.EqualTo((nint)3));
            Assert.That(statements[2].RootNode.AsIndir().Data.AsIntCon().IconValue, Is.EqualTo((nint)222));
            Assert.That(statements[3].RootNode.AsIndir().Data.AsIntCon().IconValue, Is.EqualTo((nint)5));
            Assert.That(statements[0].RootNode.AsIndir().Addr.AsLclVarCommon().LclNum, Is.EqualTo(0));
            Assert.That(statements[2].RootNode.AsIndir().Addr.AsLclVarCommon().LclNum, Is.EqualTo(1));

            var expandedSum = store.Data.AsOp();
            Assert.That(expandedSum.Op1.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(expandedSum.Op1.AsLclVarCommon().LclNum, Is.EqualTo(0));
            Assert.That(expandedSum.Op2.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(expandedSum.Op2.AsLclVarCommon().LclNum, Is.EqualTo(1));
        });
    }

    [TestCase(false, TYP_I_IMPL)]
    [TestCase(true, TYP_BYTE)]
    [TestCase(true, TYP_INT)]
    [TestCase(true, TYP_I_IMPL)]
    [TestCase(true, TYP_LONG)]
    [TestCase(true, TYP_FLOAT)]
    [TestCase(true, TYP_DOUBLE)]
    [TestCase(true, TYP_REF)]
    public static void SplitCreatedBlockInitIsRemorphedBeforeArrayHeaderStores(bool scalar, var_types scalarType)
    {
        WithCompiler((compiler, block) =>
        {
            compiler.lvaTable[3].Type = scalarType;
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var blockInit = new GenTreeLclFld(TYP_STRUCT, scalar ? 3 : 1, 0, zero,
                new ClassLayout(scalar ? (uint)scalarType.Size : 32u))
            {
                Flags = GTF_ASG | GTF_VAR_DEF,
            };
            var call = NewArrayCall(compiler, CORINFO_HELP_NEWARR_1_DIRECT, 0, 123, 3,
                stackAllocated: true);
            var sequence = compiler.gtNewCommaNode(TYP_I_IMPL, blockInit, call);
            var store = compiler.gtNewStoreLclVarNode(2, sequence);
            var statement = Append(compiler, block, store);
            compiler.MethodHasStackAllocatedArray = true;

            Assert.That(compiler.fgExpandStackArrayAllocations(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));

            var statements = Collect(block);
            Assert.That(statements, Has.Count.EqualTo(4));
            if (scalar)
            {
                var morphed = statements[0].RootNode.AsLclVar();
                Assert.That(morphed.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(morphed.LclNum, Is.EqualTo(3));
                Assert.That(morphed.Data.Type, Is.EqualTo(scalarType.ActualType));
                if (scalarType is TYP_FLOAT or TYP_DOUBLE)
                {
                    Assert.That(morphed.Data.AsDblCon().IsPositiveZero, Is.True);
                }
                else
                {
                    Assert.That(morphed.Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
                }
#if DEBUG
                Assert.That(morphed.TreeId, Is.EqualTo(blockInit.TreeId));
                Assert.That(morphed.Data.TreeId, Is.EqualTo(zero.TreeId));
#endif
            }
            else
            {
                Assert.That(statements[0].RootNode, Is.SameAs(blockInit));
                Assert.That(compiler.lvaTable[1].lvDoNotEnregister, Is.True);
            }
            Assert.That(statements[1].RootNode.AsIndir().Data.AsIntCon().IconValue, Is.EqualTo((nint)123));
            Assert.That(statements[2].RootNode.AsIndir().Data.AsIntCon().IconValue, Is.EqualTo((nint)3));
            Assert.That(statements[3], Is.SameAs(statement));
            Assert.That(store.Data.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(store.Data.AsOp().Op1.Oper, Is.EqualTo(GT_NOP));
            Assert.That(store.Data.AsOp().Op2.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(store.Data.AsOp().Op2.AsLclVarCommon().LclNum, Is.Zero);
        });
    }

    private static GenTreeCall NewArrayCall(Compiler compiler, CorInfoHelpFunc helper, int local,
        int methodTable, int length, bool stackAllocated)
    {
        var call = compiler.gtNewHelperCallNode(TYP_I_IMPL, helper,
            compiler.gtNewIconNode(TYP_I_IMPL, methodTable), compiler.gtNewIconNode(TYP_INT, length));
        if (stackAllocated)
        {
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, local, 0);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(address)
                .WithWellKnownArg(WellKnownArg.StackArrayLocal));
            call._callMoreFlags |= GTF_CALL_M_STACK_ARRAY;
        }
        return call;
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree root)
    {
        var statement = compiler.gtNewStmt(root);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.gtSetStmtInfo(statement);
        compiler.fgSetStmtSeq(statement);
        return statement;
    }

    private static List<Statement> Collect(BasicBlock block)
    {
        var statements = new List<Statement>();
        foreach (var statement in block.Statements)
        {
            statements.Add(statement);
        }
        return statements;
    }

    private static void WithCompiler(Action<Compiler, BasicBlock> action)
    {
#if DEBUG
        ICorJitInfo jitInfo = default;
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
#if DEBUG
        compiler.info.compFullName = nameof(StackArrayExpansionTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        JitTls.Compiler = compiler;
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 4;
        compiler.lvaSetStruct(0, compiler.typGetBlkLayout(32), unsafeValueClsCheck: false);
        compiler.lvaSetStruct(1, compiler.typGetBlkLayout(32), unsafeValueClsCheck: false);
        compiler.lvaTable[2].Type = TYP_I_IMPL;
        compiler.fgNodeThreading = NodeThreading.AllTrees;
        compiler.codeGen = new CodeGen(compiler);
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        compiler.compCurBB = block;

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
