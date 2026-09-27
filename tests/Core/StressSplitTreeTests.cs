// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class StressSplitTreeTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SplitTreesRemoveCommas")]
    private static extern void RemoveCommas(Compiler compiler);

    [Test]
    public static void DisabledStressPreservesTheGraph()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = NewBlock(compiler);
            var comma = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
            var stmt = Append(compiler, block, comma);

            Assert.That(compiler.StressSplitTree(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(stmt.RootNode, Is.SameAs(comma));
            Assert.That(block.Statements.Count(), Is.EqualTo(1));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void CommaRemovalPreservesEffectsAndRestartsAtIntroducedStatements(bool nested, bool sideEffects)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            var block = NewBlock(compiler);
            GenTree first = sideEffects
                ? compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 7))
                : compiler.gtNewIconNode(TYP_INT, 7);
            var result = compiler.gtNewIconNode(TYP_INT, 9);
            GenTree root = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, first, result);

            if (nested)
            {
                result = compiler.gtNewIconNode(TYP_INT, 11);
                root = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, root, result);
            }

            var stmt = Append(compiler, block, root);
            RemoveCommas(compiler);

            Assert.That(stmt.RootNode, Is.SameAs(result));
            GenTree[] expected = sideEffects ? [first, result] : [result];
            Assert.That(block.Statements.Select(statement => statement.RootNode).ToArray(),
                Is.EqualTo(expected));

            foreach (var statement in block.Statements)
            {
                foreach (var tree in statement.TreeList)
                {
                    Assert.That(tree.Oper, Is.Not.EqualTo(GT_COMMA));
                }
            }
        });
    }

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStressModeNames")]
    private static extern ref byte* StressNames(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitStressSplitTreeLimit")]
    private static extern ref int SplitLimit(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compMethodHashPrivate")]
    private static extern ref int MethodHash(ref Compiler.Info info);

    [Test]
    public static void CommaStressDispatchesTheCompleteTransformation()
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            compiler.compAllowStress = true;
            compiler.info.compMethodName = nameof(CommaStressDispatchesTheCompleteTransformation);
            var block = NewBlock(compiler);
            var result = compiler.gtNewIconNode(TYP_INT, 2);
            var stmt = Append(compiler, block, compiler.gtNewBinaryNode(GT_COMMA, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), result));

            fixed (byte* names = "STRESS_SPLIT_TREES_REMOVE_COMMAS\0"u8)
            {
                StressNames(ref JitConfig) = names;
                Assert.That(compiler.StressSplitTree(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            }

            Assert.That(stmt.RootNode, Is.SameAs(result));
        });
    }

    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    [TestCase(1, true)]
    public static void RandomStressHonorsLimitsAndTakesPrecedence(int limit, bool bothModes)
    {
        SsaLivenessTests.WithCompiler(1, compiler => {
            compiler.compAllowStress = true;
            compiler.info.compMethodName = nameof(RandomStressHonorsLimitsAndTakesPrecedence);
            MethodHash(ref compiler.info) = 0x077cc4d4 ^ 5;
            SplitLimit(ref JitConfig) = limit;
            var block = NewBlock(compiler);
            var left = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var root = compiler.gtNewBinaryNode(GT_SUB, TYP_INT, left, call);
            var stmt = Append(compiler, block, root);
            var comma = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT,
                compiler.gtNewIconNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 2));
            var untouched = Append(compiler, block, comma);
            var namesBytes = bothModes
                ? "STRESS_SPLIT_TREES_RANDOMLY STRESS_SPLIT_TREES_REMOVE_COMMAS\0"u8
                : "STRESS_SPLIT_TREES_RANDOMLY\0"u8;

            fixed (byte* names = namesBytes)
            {
                StressNames(ref JitConfig) = names;
                Assert.That(compiler.StressSplitTree(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            }

            Assert.That(compiler.lvaCount, Is.EqualTo(1 + limit));
            Assert.That(block.Statements.Count(), Is.EqualTo(2 + limit));
            Assert.That(untouched.RootNode, Is.SameAs(comma));
            Assert.That(root.Op2, Is.SameAs(call));

            if (limit == 0)
            {
                Assert.That(root.Op1, Is.SameAs(left));
            }
            else
            {
                var spill = block.FirstStmt ?? throw new System.InvalidOperationException();
                Assert.That(spill.RootNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(spill.RootNode.AsLclVarCommon().Data, Is.SameAs(left));
                Assert.That(spill.NextStmt, Is.SameAs(stmt));
                Assert.That(root.Op1.AsLclVarCommon().LclNum, Is.EqualTo(1));
            }
        });
    }
#endif

    private static BasicBlock NewBlock(Compiler compiler)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        return block;
    }

    private static Statement Append(Compiler compiler, BasicBlock block, GenTree root)
    {
        var stmt = compiler.fgNewStmtFromTree(root);
        compiler.fgInsertStmtAtEnd(block, stmt);
        return stmt;
    }
}
