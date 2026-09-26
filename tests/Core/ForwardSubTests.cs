// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ForwardSubTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgForwardSub")]
    private static extern PhaseStatus ForwardSub(Compiler compiler);

    [Test]
    public static void SingleUseMovesOriginalSubtreeAndClearsEarlierDeath()
    {
        WithCompiler(3, compiler => {
            var block = AddBlock(compiler);
            var sourceLocal = compiler.gtNewLclvNode(TYP_INT, 1);
            var expression = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, sourceLocal,
                compiler.gtNewIconNode(TYP_INT, 7));
            var definition = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, expression));
            var earlier = compiler.gtNewLclvNode(TYP_INT, 1);
            earlier.Flags |= GTF_VAR_DEATH;
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            use.Flags |= GTF_VAR_DEATH;
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, earlier, use);
            var consumer = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(2, sum));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt, Is.SameAs(consumer));
            Assert.That(sum.Op2, Is.SameAs(expression));
            Assert.That(sourceLocal.Prev, Is.SameAs(earlier));
            Assert.That(earlier.Flags & GTF_VAR_DEATH, Is.EqualTo(GTF_EMPTY));
            Assert.That(consumer.TreeListEnd, Is.SameAs(consumer.RootNode));
            Assert.That(use.Prev, Is.Null);
            Assert.That(definition, Is.Not.SameAs(consumer));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CheapAddressCanReplaceTwoUsesWithoutSharingTreeNodes(bool useLocalBase)
    {
        WithCompiler(3, compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[2].Type = TYP_BYREF;
            if (useLocalBase)
            {
                compiler.lvaTable[1].Type = TYP_BYREF;
            }
            var block = AddBlock(compiler);
            GenTreeLclVarCommon address = useLocalBase
                ? compiler.gtNewLclvNode(TYP_BYREF, 1)
                : compiler.gtNewLclAddrNode(TYP_BYREF, 1, 0);
            address.SsaNum = 3;
            var definition = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, address));
            var first = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var last = compiler.gtNewLclvNode(TYP_BYREF, 0);
            last.Flags |= GTF_VAR_DEATH;
            var comma = compiler.gtNewCommaNode(TYP_BYREF, first, last);
            var consumer = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(2, comma));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt, Is.SameAs(consumer));
            Assert.That(comma.Op2, Is.SameAs(address));
            Assert.That(comma.Op1, Is.Not.SameAs(address));
            Assert.That(comma.Op1.Oper, Is.EqualTo(useLocalBase ? GT_LCL_VAR : GT_LCL_ADDR));
            Assert.That(comma.Op1.AsLclVarCommon().LclNum, Is.EqualTo(1));
            Assert.That(comma.Op1.AsLclVarCommon().SsaNum, Is.EqualTo(useLocalBase ? 3 : 0));
            Assert.That(address.SsaNum, Is.EqualTo(3));
            Assert.That(consumer.TreeListEnd, Is.SameAs(consumer.RootNode));
            Assert.That(definition, Is.Not.SameAs(consumer));
        });
    }

    [Test]
    public static void BacktracksAfterRemovingAnInterveningDefinition()
    {
        WithCompiler(3, compiler => {
            var block = AddBlock(compiler);
            var firstValue = compiler.gtNewIconNode(TYP_INT, 7);
            _ = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(0, firstValue));
            var secondValue = compiler.gtNewIconNode(TYP_INT, 9);
            _ = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(1, secondValue));
            var firstUse = compiler.gtNewLclvNode(TYP_INT, 0);
            firstUse.Flags |= GTF_VAR_DEATH;
            var secondUse = compiler.gtNewLclvNode(TYP_INT, 1);
            secondUse.Flags |= GTF_VAR_DEATH;
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, firstUse, secondUse);
            var consumer = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(2, sum));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt, Is.SameAs(consumer));
            Assert.That(sum.Op1, Is.SameAs(firstValue));
            Assert.That(sum.Op2, Is.SameAs(secondValue));
        });
    }

    [Test]
    public static void RejectsStoreBeforeUseThatChangesSourceLocal()
    {
        WithCompiler(3, compiler => {
            var block = AddBlock(compiler);
            _ = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 2)));
            var source = compiler.gtNewLclvNode(TYP_INT, 1);
            var definition = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, source));
            var store = compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 5));
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            use.Flags |= GTF_VAR_DEATH;
            var expression = compiler.gtNewCommaNode(TYP_INT, store, use);
            _ = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(2, expression));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(definition.NextStmt?.RootNode.AsLclVarCommon().Data, Is.SameAs(expression));
            Assert.That(expression.Op2, Is.SameAs(use));
        });
    }

    [Test]
    public static void RejectsDifferentExceptionsBeforeLastUse()
    {
        WithCompiler(4, compiler => {
            compiler.lvaTable[3].Type = TYP_BYREF;
            var block = AddBlock(compiler);
            var division = compiler.gtNewBinaryNode(GT_DIV, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 12), compiler.gtNewLclvNode(TYP_INT, 1));
            var definition = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, division));
            var load = compiler.gtNewIndir(TYP_INT, compiler.gtNewLclvNode(TYP_BYREF, 3));
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            use.Flags |= GTF_VAR_DEATH;
            var expression = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, load, use);
            _ = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(2, expression));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(definition));
            Assert.That(expression.Op2, Is.SameAs(use));
        });
    }

    [Test]
    public static void CheapAddressDoesNotCloneIntoFiveUses()
    {
        WithCompiler(3, compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[2].Type = TYP_BYREF;
            var block = AddBlock(compiler);
            var definition = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewLclAddrNode(TYP_BYREF, 1, 0)));
            GenTree expression = compiler.gtNewLclvNode(TYP_BYREF, 0);
            for (var i = 0; i < 4; i++)
            {
                var use = compiler.gtNewLclvNode(TYP_BYREF, 0);
                if (i == 3)
                {
                    use.Flags |= GTF_VAR_DEATH;
                }
                expression = compiler.gtNewCommaNode(TYP_BYREF, expression, use);
            }
            var consumer = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(2, expression));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(definition));
            Assert.That(definition.NextStmt, Is.SameAs(consumer));
        });
    }

    [TestCase(3)]
    [TestCase(4)]
    public static void CheapAddressClonesUpToFourUses(int useCount)
    {
        WithCompiler(3, compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[2].Type = TYP_BYREF;
            var block = AddBlock(compiler);
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 1, 0);
            _ = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(0, address));
            var uses = new GenTreeLclVar[useCount];
            GenTree expression = uses[0] = compiler.gtNewLclvNode(TYP_BYREF, 0);
            for (var i = 1; i < useCount; i++)
            {
                uses[i] = compiler.gtNewLclvNode(TYP_BYREF, 0);
                expression = compiler.gtNewCommaNode(TYP_BYREF, expression, uses[i]);
            }
            uses[^1].Flags |= GTF_VAR_DEATH;
            var consumer = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(2, expression));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt, Is.SameAs(consumer));
            var operand = expression;
            for (var i = useCount - 1; i > 0; i--)
            {
                Assert.That(operand.AsOp().Op2.Oper, Is.EqualTo(GT_LCL_ADDR));
                Assert.That(operand.AsOp().Op2, i == useCount - 1 ? Is.SameAs(address) : Is.Not.SameAs(address));
                operand = operand.AsOp().Op1;
            }
            Assert.That(operand.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(operand, Is.Not.SameAs(address));
        });
    }

    [Test]
    public static void RejectsNonTopLevelConditionalSubstitution()
    {
        WithCompiler(3, compiler => {
            var block = AddBlock(compiler);
            var conditional = compiler.gtNewQmarkNode(TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1),
                compiler.gtNewColonNode(TYP_INT,
                    compiler.gtNewIconNode(TYP_INT, 2),
                    compiler.gtNewIconNode(TYP_INT, 3)));
            var definition = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, conditional));
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            use.Flags |= GTF_VAR_DEATH;
            var sum = compiler.gtNewBinaryNode(GT_ADD, TYP_INT, use,
                compiler.gtNewIconNode(TYP_INT, 4));
            _ = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(2, sum));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(definition));
            Assert.That(sum.Op1, Is.SameAs(use));
        });
    }

    [Test]
    public static void RejectsIndirectCallControlExpression()
    {
        WithCompiler(3, compiler => {
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            compiler.lvaTable[1].Type = TYP_I_IMPL;
            var block = AddBlock(compiler);
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 1);
            var definition = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, address));
            var use = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            use.Flags |= GTF_VAR_DEATH;
            var call = new GenTreeCall(TYP_INT) {
                _callType = CT_INDIRECT,
                _controlExpr = use,
            };
            _ = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(2, call));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(definition));
            Assert.That(call.ControlExpr, Is.SameAs(use));
        });
    }

    [Test]
    public static void RejectsMultiUseWithIndirectCallControlExpression()
    {
        WithCompiler(3, compiler => {
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            compiler.lvaTable[1].Type = TYP_I_IMPL;
            compiler.lvaTable[2].Type = TYP_I_IMPL;
            var block = AddBlock(compiler);
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 1);
            var definition = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, address));
            var control = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var call = new GenTreeCall(TYP_I_IMPL) {
                _callType = CT_INDIRECT,
                _controlExpr = control,
            };
            var last = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            last.Flags |= GTF_VAR_DEATH;
            var expression = compiler.gtNewCommaNode(TYP_I_IMPL, call, last);
            _ = AddStatement(compiler, block, compiler.gtNewStoreLclVarNode(2, expression));

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(definition));
            Assert.That(call.ControlExpr, Is.SameAs(control));
            Assert.That(expression.Op2, Is.SameAs(last));
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void RejectsUnavailableLivenessAndAliasedOrPinnedLocals(bool pinned, bool exposed)
    {
        WithCompiler(2, compiler => {
            var block = AddBlock(compiler);
            var definition = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 9)));
            var use = compiler.gtNewLclvNode(TYP_INT, 0);
            use.Flags |= GTF_VAR_DEATH;
            _ = AddStatement(compiler, block,
                compiler.gtNewStoreLclVarNode(1, use));
            compiler.lvaTable[0].lvPinned = pinned;
            if (exposed)
            {
                compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.ESCAPE_ADDRESS);
            }
            compiler.fgDidEarlyLiveness = pinned || exposed;

            Assert.That(ForwardSub(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(block.FirstStmt, Is.SameAs(definition));
            Assert.That(compiler.lvaTable[0].Type, Is.EqualTo(TYP_INT));
        });
    }

    private static BasicBlock AddBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        return block;
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var statement = compiler.fgNewStmtFromTree(tree);
        compiler.fgInsertStmtAtEnd(block, statement);
        return statement;
    }

    private static void WithCompiler(int localCount, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compIsStatic = true;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
#if DEBUG
        compiler.info.compFullName = nameof(ForwardSubTests);
        compiler.fgSafeBasicBlockCreation = true;
#endif
        compiler.compHndBBtab = [];
        compiler.lvaArg0Var = BAD_VAR_NUM;
        compiler.lvaCount = localCount;
        compiler.lvaTable = new LclVarDsc[localCount];
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.fgNodeThreading = NodeThreading.AllLocals;
        compiler.fgDidEarlyLiveness = true;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        JitConfig = new JitConfigValues();
        JitTls.Compiler = compiler;
        try
        {
            for (var i = 0; i < localCount; i++)
            {
                compiler.lvaTable[i].Type = TYP_INT;
                compiler.lvaTable[i].setLvRefCnt(3);
                compiler.lvaTable[i].setLvRefCntWtd(3);
            }
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }
}
