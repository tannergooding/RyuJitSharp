// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insCFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64ArithmeticLoweringTests
{
    [TestCase(GT_EQ, false, INS_FLAGS_Z, INS_FLAGS_NONE)]
    [TestCase(GT_NE, false, INS_FLAGS_NONE, INS_FLAGS_Z)]
    [TestCase(GT_LT, false, INS_FLAGS_NC, INS_FLAGS_Z)]
    [TestCase(GT_LE, false, INS_FLAGS_NZC, INS_FLAGS_NONE)]
    [TestCase(GT_GE, false, INS_FLAGS_Z, INS_FLAGS_NC)]
    [TestCase(GT_GT, false, INS_FLAGS_NONE, INS_FLAGS_NZC)]
    [TestCase(GT_LT, true, INS_FLAGS_NONE, INS_FLAGS_C)]
    [TestCase(GT_LE, true, INS_FLAGS_Z, INS_FLAGS_C)]
    [TestCase(GT_GE, true, INS_FLAGS_C, INS_FLAGS_NONE)]
    [TestCase(GT_GT, true, INS_FLAGS_C, INS_FLAGS_Z)]
    public static void ArithmeticEntryPreservesConditionalCompareFlagsAndOwnership(
        genTreeOps comparison, bool unsigned, insCFlags trueFlags, insCFlags falseFlags)
    {
        foreach (var oper in new[] { GT_AND, GT_OR })
        {
            WithLowering((compiler, lowering, block) => {
                var first = Relop(compiler, block, GT_NE, 0);
                var second = Relop(compiler, block, comparison, 1);
                second.Flags |= unsigned ? GTF_UNSIGNED : GTF_EMPTY;
                var expectedCondition = GenCondition.FromRelop(second);
                var tree = new GenTreeOp(oper, TYP_INT, first, second) { Flags = GTF_DONT_CSE };
                var user = new GenTreeUnOp(GT_NEG, TYP_INT, tree) { IsUnusedValue = true };
                Append(block, tree, user);

                Assert.That(LowerBinaryArithmetic(lowering, tree), Is.SameAs(user));
                var setcc = user.Op1.AsCC();
                var ccmp = setcc.Prev as GenTreeCCMP ?? throw new AssertionException("CCMP missing.");
                Assert.That(setcc.Condition.Code, Is.EqualTo(expectedCondition.Code));
                Assert.That(ccmp.Condition.Code, Is.EqualTo(oper is GT_AND ? GenCondition.NE : GenCondition.EQ));
                Assert.That(ccmp.FlagsVal, Is.EqualTo(oper is GT_AND ? falseFlags : trueFlags));
                Assert.That(ccmp.Type, Is.EqualTo(TYP_VOID));
                Assert.That(ccmp.Flags & GTF_SET_FLAGS, Is.EqualTo(GTF_SET_FLAGS));
                Assert.That(ccmp.Op2.IsContained, Is.True);
                Assert.That(ccmp.Prev, Is.SameAs(first));
                Assert.That(first.Oper, Is.EqualTo(GT_CMP));
                Assert.That(tree.Next, Is.Null);
                Assert.That(second.Next, Is.Null);
                Assert.That(setcc.Flags & GTF_DONT_CSE, Is.EqualTo(GTF_DONT_CSE));
#if DEBUG
                Assert.That(setcc.TreeId, Is.EqualTo(tree.TreeId));
                Assert.That(ccmp.TreeId, Is.EqualTo(second.TreeId));
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
            });
        }
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    public static void CcmpPreservesCleanSidePreferenceAndBothDirtyFallback(int dirtySides, bool swap)
    {
        WithLowering((compiler, lowering, block) => {
            var first = Relop(compiler, block, GT_NE, 0);
            var second = Relop(compiler, block, GT_EQ, 1);
            first.Op1.IsContained = (dirtySides & 1) != 0;
            second.Op1.IsContained = (dirtySides & 2) != 0;
            var tree = new GenTreeOp(GT_AND, TYP_INT, first, second) { IsUnusedValue = true };
            block.InsertAtEnd(tree);

            Assert.That(LowerBinaryArithmetic(lowering, tree), Is.Null);
            var setcc = block.LastNode as GenTreeCC ?? throw new AssertionException("SETCC missing.");
            var ccmp = setcc.Prev as GenTreeCCMP ?? throw new AssertionException("CCMP missing.");
            Assert.That(ccmp.Op1, Is.SameAs(swap ? first.Op1 : second.Op1));
            Assert.That(ccmp.Op1.IsContained, Is.False);
            Assert.That(ccmp.Prev, Is.SameAs(swap ? second : first));
        });
    }

    [Test]
    public static void NestedCcmpMovesTheFlagsChainAndRemovesTheOldSetcc()
    {
        WithLowering((compiler, lowering, block) => {
            var first = Relop(compiler, block, GT_NE, 0);
            var second = Relop(compiler, block, GT_EQ, 1);
            var inner = new GenTreeOp(GT_AND, TYP_INT, first, second);
            block.InsertAtEnd(inner);
            var third = Relop(compiler, block, GT_GT, 0);
            var outer = new GenTreeOp(GT_OR, TYP_INT, inner, third) { IsUnusedValue = true };
            block.InsertAtEnd(outer);
            _ = LowerBinaryArithmetic(lowering, inner);
            var oldSetcc = outer.Op1;
            var firstCcmp = oldSetcc.Prev as GenTreeCCMP ?? throw new AssertionException("CCMP missing.");
            _ = LowerBinaryArithmetic(lowering, outer);

            var setcc = block.LastNode as GenTreeCC ?? throw new AssertionException("SETCC missing.");
            var lastCcmp = setcc.Prev as GenTreeCCMP ?? throw new AssertionException("CCMP missing.");
            Assert.That(lastCcmp.Prev, Is.SameAs(firstCcmp));
            Assert.That(firstCcmp.Prev, Is.SameAs(first));
            Assert.That(oldSetcc.Next, Is.Null);
            Assert.That(oldSetcc.Prev, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(GenCondition.FEQ, GT_NONE, EJ_eq, EJ_NONE)]
    [TestCase(GenCondition.FNE, GT_AND, EJ_gt, EJ_lo)]
    [TestCase(GenCondition.FEQU, GT_OR, EJ_eq, EJ_vs)]
    [TestCase(GenCondition.FNEU, GT_NONE, EJ_ne, EJ_NONE)]
    [TestCase(GenCondition.FLT, GT_NONE, EJ_lo, EJ_NONE)]
    [TestCase(GenCondition.FLTU, GT_NONE, EJ_lt, EJ_NONE)]
    public static void FloatingConditionDescriptorsPreserveNativeBranchCombinations(
        GenCondition.CodeKind condition, genTreeOps oper, emitJumpKind first, emitJumpKind second)
    {
        var descriptor = GenConditionDesc.Get(new GenCondition(condition));
        Assert.That(descriptor.Oper, Is.EqualTo(oper));
        Assert.That(descriptor.JumpKind1, Is.EqualTo(first));
        Assert.That(descriptor.JumpKind2, Is.EqualTo(second));
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void CcmpRejectsLeadingConditionsThatRequireMultipleFlagChecks(bool unordered, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var a = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var b = compiler.gtNewLclvNode(TYP_DOUBLE, 1);
            var first = new GenTreeOp(GT_NE, TYP_INT, a, b);
            first.Flags |= unordered ? GTF_RELOP_NAN_UN : GTF_EMPTY;
            Append(block, a, b, first);
            var second = Relop(compiler, block, GT_EQ, 0);
            var tree = new GenTreeOp(GT_AND, TYP_INT, first, second) { IsUnusedValue = true };
            block.InsertAtEnd(tree);
            _ = LowerBinaryArithmetic(lowering, tree);

            Assert.That(block.LastNode?.Oper, Is.EqualTo(expected ? GT_SETCC : GT_AND));
        });
    }

    [TestCase(-1L, false)]
    [TestCase(0L, true)]
    [TestCase(31L, true)]
    [TestCase(32L, false)]
    [TestCase(long.MinValue, false)]
    [TestCase(long.MaxValue, false)]
    public static void ConditionalCompareImmediateIsUnsignedFiveBits(long immediate, bool expected)
    {
        Assert.That(Emitter.emitIns_valid_imm_for_ccmp(immediate), Is.EqualTo(expected));
    }

    [TestCase(GT_AND, GT_AND_NOT)]
    [TestCase(GT_OR, GT_OR_NOT)]
    [TestCase(GT_XOR, GT_XOR_NOT)]
    public static void BitwiseNotCombinesEitherOperandOnlyWhenOptimizing(genTreeOps oper, genTreeOps expected)
    {
        foreach (var minopts in new[] { false, true })
        {
            foreach (var notFirst in new[] { false, true })
            {
                WithLowering((compiler, lowering, block) => {
                    var a = compiler.gtNewLclvNode(TYP_LONG, 0);
                    var b = compiler.gtNewLclvNode(TYP_LONG, 1);
                    var not = new GenTreeUnOp(GT_NOT, TYP_LONG, b);
                    var tree = new GenTreeOp(oper, TYP_LONG, notFirst ? not : a, notFirst ? a : not) {
                        Flags = GTF_UNSIGNED | GTF_DONT_CSE,
                        IsUnusedValue = true,
                    };
                    tree._vnPair.SetBoth(123);
                    Append(block, a, b, not, tree);
                    Assert.That(LowerBinaryArithmetic(lowering, tree), Is.Null);
                    Assert.That(tree.Oper, Is.EqualTo(minopts ? oper : expected));
                    Assert.That(tree.Flags & GTF_DONT_CSE, Is.EqualTo(GTF_DONT_CSE));
                    Assert.That(tree.Flags & GTF_UNSIGNED, Is.EqualTo(GTF_UNSIGNED));
                    Assert.That(tree._vnPair.Liberal, Is.EqualTo(minopts ? 123u : ValueNumStore.NoVN));
                    if (!minopts)
                    {
                        Assert.That(tree.Op1, Is.SameAs(a));
                        Assert.That(tree.Op2, Is.SameAs(b));
                        Assert.That(not.Next, Is.Null);
                    }
                }, minopts);
            }
        }
    }

    [TestCase(TYP_INT, 0x1FL, 3, true)]
    [TestCase(TYP_INT, 0x1FL, 27, true)]
    [TestCase(TYP_INT, 0x1FL, 28, false)]
    [TestCase(TYP_INT, 0x1FL, 32, false)]
    [TestCase(TYP_LONG, 0x1FL, -1, false)]
    [TestCase(TYP_LONG, 0x1FL, 59, true)]
    [TestCase(TYP_LONG, 0x1FL, 60, false)]
    [TestCase(TYP_LONG, 0L, 1, false)]
    [TestCase(TYP_LONG, 0x55L, 1, false)]
    [TestCase(TYP_LONG, 0xFFL, 1, false)]
    [TestCase(TYP_LONG, 0xFFFFL, 1, false)]
    [TestCase(TYP_LONG, 0xFFFFFFFFL, 1, false)]
    [TestCase(TYP_LONG, -1L, 0, true)]
    [TestCase(TYP_INT, -1L, 0, false)]
    public static void BitfieldExtractionPreservesWidthsOffsetsAndExtensionExclusions(
        var_types type, long maskValue, int offset, bool expected)
    {
        foreach (var shiftOper in new[] { GT_RSH, GT_RSZ })
        {
            WithLowering((compiler, lowering, block) => {
                var value = compiler.gtNewLclvNode(type, 0);
                var amount = compiler.gtNewIconNode(TYP_INT, offset);
                var shift = new GenTreeOp(shiftOper, type, value, amount);
                var mask = compiler.gtNewIconNode(type, (nint)maskValue);
                var tree = new GenTreeOp(GT_AND, type, shift, mask);
                var user = new GenTreeUnOp(GT_NEG, type, tree) { IsUnusedValue = true };
                Append(block, value, amount, shift, mask, tree, user);
                ContainCheckShiftRotate(lowering, shift);
                Assert.That(LowerBinaryArithmetic(lowering, tree), Is.SameAs(user));
                if (expected)
                {
                    var bfx = user.Op1 as GenTreeBfm ?? throw new AssertionException("BFX missing.");
                    Assert.That(bfx.Op1, Is.SameAs(value));
                    Assert.That(bfx.GetOffset(), Is.EqualTo((uint)offset));
                    Assert.That(bfx.GetWidth(),
                        Is.EqualTo((uint)System.Numerics.BitOperations.PopCount(unchecked((ulong)maskValue))));
                    Assert.That(tree.Next, Is.Null);
                    Assert.That(shift.Next, Is.Null);
                    Assert.That(amount.Next, Is.Null);
                    Assert.That(mask.Next, Is.Null);
                }
                else
                {
                    Assert.That(user.Op1, Is.SameAs(tree));
                }
            });
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnusedBitfieldPreservesInputEffectsAndMinopts(bool minopts)
    {
        WithLowering((compiler, lowering, block) => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var value = new GenTreeIndir(GT_IND, TYP_LONG, address) { Flags = GTF_EXCEPT | GTF_GLOB_REF };
            var amount = compiler.gtNewIconNode(TYP_INT, 2);
            var shift = new GenTreeOp(GT_RSZ, TYP_LONG, value, amount);
            var mask = compiler.gtNewIconNode(TYP_LONG, 31);
            var tree = new GenTreeOp(GT_AND, TYP_LONG, shift, mask) { IsUnusedValue = true };
            Append(block, address, value, amount, shift, mask, tree);
            ContainCheckShiftRotate(lowering, shift);
            Assert.That(LowerBinaryArithmetic(lowering, tree), Is.Null);
            if (!minopts)
            {
                var bfx = block.LastNode as GenTreeBfm ?? throw new AssertionException("BFX missing.");
                Assert.That(bfx.IsUnusedValue, Is.True);
                Assert.That(bfx.Flags & GTF_ALL_EFFECT, Is.EqualTo(value.Flags & GTF_ALL_EFFECT));
            }
            else
            {
                Assert.That(block.LastNode, Is.SameAs(tree));
            }
        }, minopts);
    }

    [TestCase(false, GTF_EMPTY, false, true)]
    [TestCase(true, GTF_EMPTY, false, false)]
    [TestCase(false, GTF_OVERFLOW, false, false)]
    [TestCase(false, GTF_SET_FLAGS, false, false)]
    [TestCase(false, GTF_EMPTY, true, false)]
    public static void WideningMultiplyGuardsAndUnusedSuccessorArePreserved(
        bool minopts, GenTreeFlags flags, bool modified, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var addValue = compiler.gtNewLclvNode(TYP_LONG, 0);
            var a = compiler.gtNewLclvNode(TYP_INT, 0);
            var b = compiler.gtNewLclvNode(TYP_INT, 1);
            var multiply = new GenTreeOp(GT_MUL_LONG, TYP_LONG, a, b);
            var tree = new GenTreeOp(GT_SUB, TYP_LONG, addValue, multiply) {
                Flags = flags,
                IsUnusedValue = true,
            };
            Append(block, addValue, a, b, multiply);
            if (modified)
            {
                var constant = compiler.gtNewIconNode(TYP_INT, 5);
                var store = compiler.gtNewStoreLclVarNode(1, constant);
                Append(block, constant, store);
            }
            block.InsertAtEnd(tree);
            var next = LowerBinaryArithmetic(lowering, tree);
            if (expected)
            {
                var result = block.LastNode as GenTreeHWIntrinsic ?? throw new AssertionException("Intrinsic missing.");
                Assert.That(next, Is.SameAs(result));
                Assert.That(result.IsUnusedValue, Is.True);
            }
            else
            {
                Assert.That(next, Is.Null);
                Assert.That(block.LastNode, Is.SameAs(tree));
            }
        }, minopts);
    }

    [TestCase(GT_ADD, false, false, true)]
    [TestCase(GT_ADD, true, true, true)]
    [TestCase(GT_SUB, false, false, true)]
    [TestCase(GT_SUB, false, true, true)]
    [TestCase(GT_SUB, true, false, false)]
    public static void WideningMultiplyFusionPreservesOrderAndSignedness(
        genTreeOps oper, bool multiplyFirst, bool unsigned, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var a = compiler.gtNewLclvNode(TYP_INT, 0);
            var b = compiler.gtNewLclvNode(TYP_INT, 1);
            var multiply = new GenTreeOp(GT_MUL_LONG, TYP_LONG, a, b);
            multiply.Flags |= unsigned ? GTF_UNSIGNED : GTF_EMPTY;
            var addValue = compiler.gtNewLclvNode(TYP_LONG, 0);
            var tree = new GenTreeOp(oper, TYP_LONG, multiplyFirst ? multiply : addValue,
                multiplyFirst ? addValue : multiply);
            var user = new GenTreeUnOp(GT_NEG, TYP_LONG, tree) { IsUnusedValue = true };
            Append(block, a, b, multiply, addValue, tree, user);
            if (oper is GT_SUB)
            {
                var next = LowerBinaryArithmetic(lowering, tree);
                Assert.That(next, Is.SameAs(expected ? user.Op1 : user));
            }
            else
            {
                Assert.That(TryLowerAddSubToMulLongOp(lowering, tree, out var next), Is.EqualTo(expected));
                Assert.That(next, Is.SameAs(user.Op1));
            }

            if (expected)
            {
                var intrinsic = user.Op1.AsHWIntrinsic();
                Assert.That(intrinsic.HWIntrinsicId,
                    Is.EqualTo(oper is GT_ADD ? NI_ArmBase_Arm64_MultiplyLongAdd : NI_ArmBase_Arm64_MultiplyLongSub));
                Assert.That(intrinsic.SimdBaseType, Is.EqualTo(unsigned ? TYP_ULONG : TYP_LONG));
                Assert.That(intrinsic.GetOp(1), Is.SameAs(a));
                Assert.That(intrinsic.GetOp(2), Is.SameAs(b));
                Assert.That(intrinsic.GetOp(3), Is.SameAs(addValue));
                Assert.That(tree.Next, Is.Null);
                Assert.That(multiply.Next, Is.Null);
            }
            else
            {
                Assert.That(user.Op1, Is.SameAs(tree));
            }
        });
    }

    private static GenTreeOp Relop(Compiler compiler, BasicBlock block, genTreeOps oper, int local)
    {
        var value = compiler.gtNewLclvNode(TYP_INT, local);
        var constant = compiler.gtNewIconNode(TYP_INT, 7);
        var relop = new GenTreeOp(oper, TYP_INT, value, constant);
        Append(block, value, constant, relop);
        return relop;
    }

    private static void Append(BasicBlock block, params GenTree[] nodes)
    {
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerBinaryArithmetic")]
    private static extern GenTree? LowerBinaryArithmetic(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckShiftRotate")]
    private static extern void ContainCheckShiftRotate(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryLowerAddSubToMulLongOp")]
    private static extern bool TryLowerAddSubToMulLongOp(Lowering lowering, GenTreeOp node, out GenTree? next);

    private static void WithLowering(Action<Compiler, Lowering, BasicBlock> action, bool minopts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.opts.compFlags = minopts ? 0 : CLFLG_REGVAR;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_LONG;
        compiler.lvaTable[1].Type = TYP_LONG;
        JitTls.Compiler = compiler;

        try
        {
            compiler.codeGen = new CodeGen(compiler);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
