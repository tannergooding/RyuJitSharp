// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BitwiseLoweringTests
{
    [TestCase(GT_OR, GT_BIT_SET, false, false, TYP_INT)]
    [TestCase(GT_XOR, GT_BIT_INVERT, true, false, TYP_LONG)]
    [TestCase(GT_AND, GT_BIT_CLEAR, false, true, TYP_INT)]
    public static void SingleBitRewritesKeepIdentityOwnerAndEvaluationOrder(genTreeOps oper,
        genTreeOps expected, bool leftMask, bool negated, var_types type)
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[1].Type = TYP_INT;
            var value = compiler.gtNewLclvNode(type, 0);
            var one = compiler.gtNewIconNode(type, 1);
            var index = compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = new GenTreeOp(GT_LSH, type, one, index);
            GenTree mask = negated ? new GenTreeUnOp(GT_NOT, type, shift) : shift;
            var binary = new GenTreeOp(oper, type, leftMask ? mask : value, leftMask ? value : mask);
            binary.Flags |= GTF_DONT_CSE;
            binary._vnPair.SetBoth(47);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, binary);
            if (!leftMask)
            {
                block.InsertAtEnd(value);
            }
            block.InsertAtEnd(one);
            block.InsertAtEnd(index);
            block.InsertAtEnd(shift);
            if (negated)
            {
                block.InsertAtEnd(mask);
            }
            if (leftMask)
            {
                block.InsertAtEnd(value);
            }
            block.InsertAtEnd(binary);
            block.InsertAtEnd(user);
#if DEBUG
            var id = binary.TreeId;
#endif
            Assert.That(TryLowerBitwiseOpToBitOp(lowering, binary), Is.SameAs(binary));
            Assert.That(binary.Oper, Is.EqualTo(expected));
            Assert.That(binary.Op1, Is.SameAs(value));
            Assert.That(binary.Op2, Is.SameAs(index));
            Assert.That(user.Op1, Is.SameAs(binary));
            Assert.That(binary._vnPair.Conservative, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(binary.Flags & GTF_DONT_CSE, Is.EqualTo(GTF_DONT_CSE));
            Assert.That(one.Next, Is.Null);
            Assert.That(shift.Next, Is.Null);
            if (negated)
            {
                Assert.That(mask.Next, Is.Null);
            }
            Assert.That(value.Next, Is.SameAs(index).Or.SameAs(binary));
            Assert.That(index.Next, Is.SameAs(binary).Or.SameAs(value));
#if DEBUG
            Assert.That(binary.TreeId, Is.EqualTo(id));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void NegatedSingleBitRetainsFlagSettingNot()
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[1].Type = TYP_INT;
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var index = compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = new GenTreeOp(GT_LSH, TYP_INT, one, index);
            var not = new GenTreeUnOp(GT_NOT, TYP_INT, shift) {
                Flags = GTF_SET_FLAGS,
            };
            var and = new GenTreeOp(GT_AND, TYP_INT, value, not);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, and);
            foreach (var node in new GenTree[] { value, one, index, shift, not, and, user })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(TryLowerBitwiseOpToBitOp(lowering, and), Is.Null);
            Assert.That(and.Oper, Is.EqualTo(GT_AND));
            Assert.That(and.Op2, Is.SameAs(not));
            Assert.That(not.Next, Is.SameAs(and));
        });
    }

    [Test]
    public static void WrongPolarityDoesNotRemoveOperand()
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[1].Type = TYP_INT;
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var index = compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = new GenTreeOp(GT_LSH, TYP_INT, one, index);
            var not = new GenTreeUnOp(GT_NOT, TYP_INT, shift);
            var binary = new GenTreeOp(GT_OR, TYP_INT, value, not);
            foreach (var node in new GenTree[] { value, one, index, shift, not, binary })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(TryLowerBitwiseOpToBitOp(lowering, binary), Is.Null);
            Assert.That(binary.Op2, Is.SameAs(not));
            Assert.That(shift.Next, Is.SameAs(not));
        });
    }

    [TestCase(true, false, false, false)]
    [TestCase(false, true, false, false)]
    [TestCase(false, false, true, false)]
    [TestCase(false, false, false, true)]
    public static void SingleBitRejectsConstantsWidthAndFlagDependencies(bool constantIndex,
        bool wrongWidth, bool shiftSetsFlags, bool bitwiseSetsFlags)
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[1].Type = TYP_INT;
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var one = compiler.gtNewIconNode(wrongWidth ? TYP_LONG : TYP_INT, 1);
            GenTree index = constantIndex ? compiler.gtNewIconNode(TYP_INT, 3) : compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = new GenTreeOp(GT_LSH, wrongWidth ? TYP_LONG : TYP_INT, one, index);
            var binary = new GenTreeOp(GT_OR, TYP_INT, value, shift);
            if (shiftSetsFlags)
            {
                shift.Flags |= GTF_SET_FLAGS;
            }
            if (bitwiseSetsFlags)
            {
                binary.Flags |= GTF_SET_FLAGS;
            }
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, binary);
            foreach (var node in new GenTree[] { value, one, index, shift, binary, user })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(TryLowerBitwiseOpToBitOp(lowering, binary), Is.Null);
            Assert.That(binary.Oper, Is.EqualTo(GT_OR));
            Assert.That(binary.Op2, Is.SameAs(shift));
            Assert.That(one.Next, Is.SameAs(index));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AndNegativeOneTransfersUseOrMarksRootUnused(bool root)
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.lvaTable[0].Type = TYP_LONG;
            var value = compiler.gtNewLclvNode(TYP_LONG, 0);
            var minusOne = compiler.gtNewIconNode(TYP_LONG, -1);
            var and = new GenTreeOp(GT_AND, TYP_LONG, value, minusOne);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, and);
            block.InsertAtEnd(value);
            block.InsertAtEnd(minusOne);
            block.InsertAtEnd(and);
            if (!root)
            {
                block.InsertAtEnd(user);
            }

            Assert.That(TryLowerAndNegativeOne(lowering, and, out var successor), Is.True);
            Assert.That(successor, Is.SameAs(root ? null : user));
            Assert.That(value.IsUnusedValue, Is.EqualTo(root));
            if (!root)
            {
                Assert.That(user.Op1, Is.SameAs(value));
            }
            Assert.That(value.Next, Is.SameAs(root ? null : user));
            Assert.That(minusOne.Next, Is.Null);
            Assert.That(and.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void AndNegativeOneRetainsFlagSettingOrContainedNodes(bool setsFlags, bool contained)
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.lvaTable[0].Type = TYP_INT;
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var minusOne = compiler.gtNewIconNode(TYP_INT, -1);
            var and = new GenTreeOp(GT_AND, TYP_INT, value, minusOne);
            var user = new GenTreeUnOp(GT_RETURN, TYP_VOID, and);
            if (setsFlags)
            {
                and.Flags |= GTF_SET_FLAGS;
            }
            and.IsContained = contained;
            foreach (var node in new GenTree[] { value, minusOne, and, user })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(TryLowerAndNegativeOne(lowering, and, out var successor), Is.False);
            Assert.That(successor, Is.Null);
            Assert.That(user.Op1, Is.SameAs(and));
            Assert.That(minusOne.Next, Is.SameAs(and));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryLowerBitwiseOpToBitOp")]
    private static extern GenTreeOp? TryLowerBitwiseOpToBitOp(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryLowerAndNegativeOne")]
    private static extern bool TryLowerAndNegativeOne(Lowering lowering, GenTreeOp node, out GenTree? nextNode);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    private static void WithCompiler(Action<Compiler, BasicBlock, Lowering> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 2;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, block, lowering);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
