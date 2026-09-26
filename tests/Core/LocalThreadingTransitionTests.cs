// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class LocalThreadingTransitionTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void RetiringLocalListsAllowsImplicitByrefReplacementAtEveryPosition(int position)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            compiler.fgNodeThreading = NodeThreading.AllLocals;
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].IsImplicitByRef = true;
            GenTreeLclFld[] locals = [
                new(GT_LCL_FLD, TYP_LONG, 0, 0),
                new(GT_LCL_FLD, TYP_LONG, 0, 8),
                new(GT_LCL_FLD, TYP_LONG, 0, 16),
            ];
            locals[position].Flags |= GTF_VAR_DEATH;
            var sum = new GenTreeOp(GT_ADD, TYP_LONG,
                new GenTreeOp(GT_ADD, TYP_LONG, locals[0], locals[1]), locals[2]);
            var statement = new Statement(sum, 0);
            var block = BasicBlock.New(compiler, BBJ_RETURN);
            compiler.fgFirstBB = compiler.fgLastBB = block;
            compiler.fgInsertStmtAtEnd(block, statement);
            compiler.fgSequenceLocals(statement);
            Assert.That(statement.TreeListBegin, Is.SameAs(locals[0]));
            Assert.That(locals[1].Prev, Is.SameAs(locals[0]));
            Assert.That(locals[1].Next, Is.SameAs(locals[2]));

            EndLocalTreeLists(compiler);

            Assert.That(compiler.fgNodeThreading, Is.EqualTo(NodeThreading.None));
            Assert.That(statement.TreeListBegin, Is.Null);
            Assert.That(statement.TreeListEnd, Is.Null);
            Assert.That(statement.RootNode, Is.SameAs(sum));
            foreach (var local in locals)
            {
                Assert.That(local.Prev, Is.Null);
                Assert.That(local.Next, Is.Null);
            }

            var result = compiler.fgMorphExpandImplicitByRefArg(locals[position]) ??
                throw new InvalidOperationException("Missing implicit-byref expansion.");
            var address = result.AsIndir().Addr;
            var pointer = address.Oper is GT_ADD ? address.AsOp().Op1 : address;
            Assert.That(pointer.Type, Is.EqualTo(TYP_BYREF));
            Assert.That(pointer.Flags & GTF_VAR_DEATH, Is.Not.Zero);
#if DEBUG
            Assert.That(pointer.TreeId, Is.EqualTo(locals[position].TreeId));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RetiringListsClearsEveryStatementAndBlock(bool withLocals)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            compiler.fgNodeThreading = withLocals ? NodeThreading.AllLocals : NodeThreading.None;
            var statements = new Statement[4];
            for (var i = 0; i < 2; i++)
            {
                var block = BasicBlock.New(compiler, BBJ_RETURN);
                if (compiler.fgLastBB is BasicBlock previous)
                {
                    previous.Next = block;
                }
                else
                {
                    compiler.fgFirstBB = block;
                }
                compiler.fgLastBB = block;

                for (var j = 0; j < 2; j++)
                {
                    var local = compiler.gtNewLclvNode(TYP_INT, 0);
                    var statement = new Statement(local, 0);
                    statements[(2 * i) + j] = statement;
                    compiler.fgInsertStmtAtEnd(block, statement);
                    if (withLocals)
                    {
                        compiler.fgSequenceLocals(statement);
                    }
                }
            }

            EndLocalTreeLists(compiler);

            Assert.That(compiler.fgNodeThreading, Is.EqualTo(NodeThreading.None));
            foreach (var statement in statements)
            {
                Assert.That(statement.TreeListBegin, Is.Null);
                Assert.That(statement.TreeListEnd, Is.Null);
                Assert.That(statement.RootNode.Oper, Is.EqualTo(GT_LCL_VAR));
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgEndLocalTreeLists")]
    private static extern void EndLocalTreeLists(Compiler compiler);
}
