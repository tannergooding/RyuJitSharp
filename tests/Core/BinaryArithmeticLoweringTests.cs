// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insCFlags;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if TARGET_AMD64
[NonParallelizable]
internal static unsafe class BinaryArithmeticLoweringTests
{
    private static IEnumerable<TestCaseData> Conditions()
    {
        foreach (var operation in new[] { GT_AND, GT_OR })
        {
            yield return new(operation, GT_EQ, false, GenCondition.EQ, INS_FLAGS_ZF, INS_FLAGS_NONE);
            yield return new(operation, GT_NE, false, GenCondition.NE, INS_FLAGS_NONE, INS_FLAGS_ZF);
            yield return new(operation, GT_LT, false, GenCondition.SLT, INS_FLAGS_SF, INS_FLAGS_NONE);
            yield return new(operation, GT_LE, false, GenCondition.SLE, INS_FLAGS_ZF, INS_FLAGS_NONE);
            yield return new(operation, GT_GE, false, GenCondition.SGE, INS_FLAGS_NONE, INS_FLAGS_SF);
            yield return new(operation, GT_GT, false, GenCondition.SGT, INS_FLAGS_NONE, INS_FLAGS_ZF);
            yield return new(operation, GT_LT, true, GenCondition.ULT, INS_FLAGS_CF, INS_FLAGS_NONE);
            yield return new(operation, GT_LE, true, GenCondition.ULE, INS_FLAGS_ZF, INS_FLAGS_NONE);
            yield return new(operation, GT_GE, true, GenCondition.UGE, INS_FLAGS_NONE, INS_FLAGS_CF);
            yield return new(operation, GT_GT, true, GenCondition.UGT, INS_FLAGS_NONE, INS_FLAGS_ZF);
        }
    }

    [TestCaseSource(nameof(Conditions))]
    public static void ConditionalComparePreservesTruthTableIdentityAndSuccessor(genTreeOps operation,
        genTreeOps comparison, bool unsigned, GenCondition.CodeKind condition, insCFlags trueFlags, insCFlags falseFlags)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var left = Relop(compiler, block, GT_NE, 0, 0);
            var right = Relop(compiler, block, comparison, 1, 7);
            if (unsigned)
            {
                right.Flags |= GTF_UNSIGNED;
            }
            var tree = new GenTreeOp(operation, TYP_INT, left, right) { Flags = GTF_DONT_CSE };
            tree._vnPair.SetBoth(123);
            right._vnPair.SetBoth(456);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, tree) { IsUnusedValue = true };
            block.InsertAtEnd(tree);
            block.InsertAtEnd(user);

            Assert.That(TryCCMP(lowering, tree, out var next), Is.True);

            Assert.That(next, Is.SameAs(user));
            var setcc = user.Op1.AsCC();
            var ccmp = setcc.Prev as GenTreeCCMP ?? throw new AssertionException("CCMP missing.");
            Assert.That(setcc.Condition.Code, Is.EqualTo(condition));
            Assert.That(ccmp.Condition.Code, Is.EqualTo(operation == GT_AND ? GenCondition.NE : GenCondition.EQ));
            Assert.That(ccmp.FlagsVal, Is.EqualTo(operation == GT_AND ? falseFlags : trueFlags));
            Assert.That(ccmp.Type, Is.EqualTo(TYP_VOID));
            Assert.That(ccmp.Flags & GTF_SET_FLAGS, Is.EqualTo(GTF_SET_FLAGS));
            Assert.That(ccmp.Op2.IsContained, Is.True);
            Assert.That(ccmp.Op1.IsContained, Is.False);
            Assert.That(ccmp.Prev, Is.SameAs(left));
            Assert.That(left.Oper, Is.EqualTo(GT_CMP));
            Assert.That(setcc.Flags & GTF_DONT_CSE, Is.EqualTo(GTF_DONT_CSE));
            Assert.That(setcc._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(ccmp._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(tree.Next is null && right.Next is null, Is.True);
#if DEBUG
            Assert.That(setcc.TreeId, Is.EqualTo(tree.TreeId));
            Assert.That(ccmp.TreeId, Is.EqualTo(right.TreeId));
#endif
            CheckLir(compiler, block);
        });
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    public static void CcmpPrefersCleanSideButRetainsBothDirtyFallback(int dirtySides, bool swap)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var left = Relop(compiler, block, GT_NE, 0, 0);
            var right = Relop(compiler, block, GT_EQ, 1, 1);
            left.Op1.IsContained = (dirtySides & 1) != 0;
            right.Op1.IsContained = (dirtySides & 2) != 0;
            var tree = new GenTreeOp(GT_AND, TYP_INT, left, right) { IsUnusedValue = true };
            block.InsertAtEnd(tree);

            Assert.That(TryCCMP(lowering, tree, out var next), Is.True);

            Assert.That(next, Is.Null);
            var setcc = block.LastNode as GenTreeCC ?? throw new AssertionException("SETCC missing.");
            var ccmp = setcc.Prev as GenTreeCCMP ?? throw new AssertionException("CCMP missing.");
            Assert.That(ccmp.Op1, Is.SameAs(swap ? left.Op1 : right.Op1));
            Assert.That(ccmp.Op1.IsContained, Is.False);
            var leading = swap ? right : left;
            Assert.That(leading.Op1.IsContained, Is.EqualTo(swap ? (dirtySides & 2) != 0 : (dirtySides & 1) != 0));
            Assert.That(setcc.Condition.Code, Is.EqualTo(swap ? GenCondition.NE : GenCondition.EQ));
            Assert.That(ccmp.Condition.Code, Is.EqualTo(swap ? GenCondition.EQ : GenCondition.NE));
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void ExistingSetccChainIsMovedAndRemovedWithoutStaleValueEdges()
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var left = Relop(compiler, block, GT_NE, 0, 0);
            left.SetOper(GT_CMP);
            left.Type = TYP_VOID;
            left.Flags |= GTF_SET_FLAGS;
            var firstCcmp = new GenTreeCCMP(TYP_VOID, new GenCondition(GenCondition.NE),
                compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewIconNode(TYP_INT, 8), INS_FLAGS_NONE) {
                Flags = GTF_SET_FLAGS,
            };
            block.InsertBefore(left, firstCcmp.Op1);
            block.InsertBefore(left, firstCcmp.Op2);
            block.InsertAtEnd(firstCcmp);
            var oldSetcc = new GenTreeCC(GT_SETCC, TYP_INT, new GenCondition(GenCondition.UGT));
            block.InsertAtEnd(oldSetcc);
            var right = Relop(compiler, block, GT_EQ, 2, 7);
            var tree = new GenTreeOp(GT_OR, TYP_INT, oldSetcc, right);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, tree) { IsUnusedValue = true };
            block.InsertAtEnd(tree);
            block.InsertAtEnd(user);

            Assert.That(TryCCMP(lowering, tree, out var next), Is.True);

            var lastCcmp = user.Op1.Prev as GenTreeCCMP ?? throw new AssertionException("CCMP missing.");
            Assert.That(next, Is.SameAs(user));
            Assert.That(oldSetcc.Next is null && oldSetcc.Prev is null, Is.True);
            Assert.That(left.Next, Is.SameAs(firstCcmp));
            Assert.That(firstCcmp.Next, Is.SameAs(lastCcmp));
            Assert.That(lastCcmp.Condition.Code, Is.EqualTo(GenCondition.ULE));
            CheckLir(compiler, block);
        });
    }

    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    public static void FullArithmeticEntryHonorsMinoptsAndApxGates(bool minOpts, bool apx, bool enabled)
    {
        WithCompiler(minOpts, (compiler, block, lowering) => {
            if (apx)
            {
                Enable(compiler, InstructionSet_APX);
            }
            Enable(compiler, InstructionSet_AVX512);
            var saved = ApxChaining(ref Globals.JitConfig);
            ApxChaining(ref Globals.JitConfig) = enabled ? 1 : 0;
            try
            {
                var left = Relop(compiler, block, GT_NE, 0, 0);
                var right = Relop(compiler, block, GT_EQ, 1, 3);
                var tree = new GenTreeOp(GT_AND, TYP_INT, left, right);
                var user = new GenTreeUnOp(GT_NEG, TYP_INT, tree) { IsUnusedValue = true };
                block.InsertAtEnd(tree);
                block.InsertAtEnd(user);

                Assert.That(LowerBinary(lowering, tree), Is.SameAs(user));
                Assert.That(user.Op1.Oper, Is.EqualTo(!minOpts && apx && enabled ? GT_SETCC : GT_AND));
                CheckLir(compiler, block);
            }
            finally
            {
                ApxChaining(ref Globals.JitConfig) = saved;
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void TestCanLeadTheChainButCannotBecomeItsConditionalComparison(bool testSecond, bool dirty)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var first = Relop(compiler, block, testSecond ? GT_EQ : GT_TEST_NE, 0, 1);
            var second = Relop(compiler, block, testSecond ? GT_TEST_NE : GT_EQ, 1, 2);
            var normal = testSecond ? first : second;
            var test = testSecond ? second : first;
            normal.Op1.IsContained = dirty;
            var tree = new GenTreeOp(GT_AND, TYP_INT, first, second) { IsUnusedValue = true };
            block.InsertAtEnd(tree);

            Assert.That(TryCCMP(lowering, tree, out _), Is.True);

            Assert.That(test.Oper, Is.EqualTo(GT_TEST));
            var setcc = block.LastNode as GenTreeCC ?? throw new AssertionException("SETCC missing.");
            var ccmp = setcc.Prev as GenTreeCCMP ?? throw new AssertionException("CCMP missing.");
            Assert.That(ccmp.Prev, Is.SameAs(test));
            Assert.That(ccmp.Op1, Is.SameAs(normal.Op1));
            Assert.That(ccmp.Op1.IsContained, Is.False);
            Assert.That(ccmp.Condition.Code, Is.EqualTo(GenCondition.NE));
            CheckLir(compiler, block);
        });
    }

    [TestCase("minopts")]
    [TestCase("arbitrary")]
    [TestCase("floating")]
    [TestCase("multiple-flags")]
    [TestCase("interference")]
    [TestCase("two-tests")]
    public static void CcmpRejectionDoesNotModifyTheOriginalTree(string reason)
    {
        WithCompiler(reason == "minopts", (compiler, block, lowering) => {
            var left = Relop(compiler, block, GT_NE, 0, 0,
                reason is "floating" or "multiple-flags" ? TYP_DOUBLE : TYP_INT);
            var right = Relop(compiler, block, GT_EQ, 1, 4, reason == "floating" ? TYP_DOUBLE : TYP_INT);
            if (reason == "arbitrary")
            {
                left.SetOper(GT_ADD);
                right.SetOper(GT_SUB);
            }
            else if (reason == "multiple-flags")
            {
                left.SetOper(GT_EQ);
            }
            else if (reason == "interference")
            {
                var constant = compiler.gtNewIconNode(TYP_INT, 2);
                block.InsertAtEnd(constant);
                block.InsertAtEnd(compiler.gtNewStoreLclVarNode(0, constant));
            }
            else if (reason == "two-tests")
            {
                left.SetOper(GT_TEST_EQ);
                right.SetOper(GT_TEST_NE);
            }
            var tree = new GenTreeOp(GT_AND, TYP_INT, left, right) { IsUnusedValue = true };
            block.InsertAtEnd(tree);
            var before = Nodes(block).ToArray();

            Assert.That(TryCCMP(lowering, tree, out _), Is.False);
            Assert.That(Nodes(block), Is.EqualTo(before));
            Assert.That(tree.Oper, Is.EqualTo(GT_AND));
        });
    }

    [Test]
    public static void CcmpUsesEncodedFlagBitsRatherThanRflagsPositions()
    {
        Assert.That(Enum.GetUnderlyingType(typeof(insCFlags)), Is.EqualTo(typeof(uint)));
        Assert.That((uint)INS_FLAGS_NONE, Is.Zero);
        Assert.That((uint)INS_FLAGS_CF, Is.EqualTo(1u));
        Assert.That((uint)INS_FLAGS_ZF, Is.EqualTo(2u));
        Assert.That((uint)INS_FLAGS_SF, Is.EqualTo(4u));
        Assert.That((uint)INS_FLAGS_OF, Is.EqualTo(8u));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void ConditionalCompareDoesNotContainRelocatableHandles(bool relocatable, bool contained)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            compiler.opts.compReloc = relocatable;
            compiler.lvaTable[0].Type = TYP_LONG;
            var value = compiler.gtNewLclvNode(TYP_LONG, 0);
            var handle = compiler.gtNewIconNode(TYP_LONG, 16);
            handle.Flags |= GTF_ICON_FTN_ADDR;
            var compare = new GenTreeCCMP(TYP_VOID, new GenCondition(GenCondition.EQ), value, handle, INS_FLAGS_NONE);
            block.InsertAtEnd(value);
            block.InsertAtEnd(handle);
            block.InsertAtEnd(compare);

            ContainCCMP(lowering, compare);

            Assert.That(handle.IsContained, Is.EqualTo(contained));
        });
    }

    [TestCase((long)int.MinValue - 1, false)]
    [TestCase((long)int.MinValue, true)]
    [TestCase(-1L, true)]
    [TestCase(0L, true)]
    [TestCase((long)int.MaxValue, true)]
    [TestCase((long)int.MaxValue + 1, false)]
    public static void ConditionalCompareImmediateUsesSigned32BitEncoding(long immediate, bool contained)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            compiler.lvaTable[0].Type = TYP_LONG;
            var left = compiler.gtNewLclvNode(TYP_LONG, 0);
            var right = compiler.gtNewIconNode(TYP_LONG, (nint)immediate);
            var compare = new GenTreeCCMP(TYP_VOID, new GenCondition(GenCondition.NE), left, right, INS_FLAGS_NONE);
            block.InsertAtEnd(left);
            block.InsertAtEnd(right);
            block.InsertAtEnd(compare);

            ContainCCMP(lowering, compare);

            Assert.That(Emitter.emitIns_valid_imm_for_ccmp(immediate), Is.EqualTo(contained));
            Assert.That(right.IsContained, Is.EqualTo(contained));
        });
    }

    private static IEnumerable<TestCaseData> BmiCases()
    {
        foreach (var type in new[] { TYP_INT, TYP_LONG })
        {
            foreach (var shape in new[] { "reset", "extract", "andnot", "zero", "mask" })
            {
                yield return new(type, shape, false);
                yield return new(type, shape, true);
            }
        }
    }

    [TestCaseSource(nameof(BmiCases))]
    public static void BinaryArithmeticRecognizesCompleteScalarBitPatterns(var_types type, string shape, bool alternate)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            Enable(compiler, InstructionSet_AVX2);
            Enable(compiler, InstructionSet_AVX2_X64);
            compiler.lvaTable[0].Type = compiler.lvaTable[1].Type = type;
            var value = compiler.gtNewLclvNode(type, 0);
            var other = compiler.gtNewLclvNode(type, shape == "andnot" ? 1 : 0);
            block.InsertAtEnd(value);
            block.InsertAtEnd(other);
            GenTree inner;
            GenTree? index = null;
            if (shape is "reset" or "mask")
            {
                var one = compiler.gtNewIconNode(type, alternate ? 1 : -1);
                inner = new GenTreeOp(alternate ? GT_SUB : GT_ADD, type, other, one);
                block.InsertAtEnd(one);
            }
            else if (shape == "zero")
            {
                block.Remove(other);
                var one = compiler.gtNewIconNode(type, 1);
                index = compiler.gtNewLclvNode(TYP_INT, 2);
                var shift = new GenTreeOp(GT_LSH, type, one, index);
                var oneMore = compiler.gtNewIconNode(type, alternate ? 1 : -1);
                inner = new GenTreeOp(alternate ? GT_SUB : GT_ADD, type, shift, oneMore);
                block.InsertAtEnd(one);
                block.InsertAtEnd(index);
                block.InsertAtEnd(shift);
                block.InsertAtEnd(oneMore);
            }
            else
            {
                inner = new GenTreeUnOp(shape == "extract" ? GT_NEG : GT_NOT, type, other);
            }
            block.InsertAtEnd(inner);
            var swap = alternate && shape is "extract" or "andnot" or "zero";
            var root = new GenTreeOp(shape == "mask" ? GT_XOR : GT_AND, type, swap ? inner : value, swap ? value : inner);
            var user = new GenTreeUnOp(GT_NEG, type, root) { IsUnusedValue = true };
            block.InsertAtEnd(root);
            block.InsertAtEnd(user);

            Assert.That(LowerBinary(lowering, root), Is.SameAs(user));

            var replacement = user.Op1.AsHWIntrinsic();
            var wide = type == TYP_LONG;
            var expected = shape switch {
                "reset" => wide ? NI_AVX2_X64_ResetLowestSetBit : NI_AVX2_ResetLowestSetBit,
                "extract" => wide ? NI_AVX2_X64_ExtractLowestSetBit : NI_AVX2_ExtractLowestSetBit,
                "andnot" => wide ? NI_AVX2_X64_AndNot : NI_AVX2_AndNotScalar,
                "zero" => wide ? NI_AVX2_X64_ZeroHighBits : NI_AVX2_ZeroHighBits,
                "mask" => wide ? NI_AVX2_X64_GetMaskUpToLowestSetBit : NI_AVX2_GetMaskUpToLowestSetBit,
                _ => throw new InvalidOperationException(),
            };
            Assert.That(replacement.HWIntrinsicId, Is.EqualTo(expected));
            if (shape == "zero")
            {
                var mask = replacement.GetOp(1).AsOp();
                Assert.That(mask.Oper, Is.EqualTo(GT_AND));
                Assert.That(mask.Op1, Is.SameAs(index));
                Assert.That(mask.Op2.AsIntCon().IconValue, Is.EqualTo((nint)(wide ? 63 : 31)));
                Assert.That(replacement.GetOp(2), Is.SameAs(value));
                foreach (var count in new[] { -1, 0, 31, 32, 63, 64, 255 })
                {
                    var maskedCount = count & (int)mask.Op2.AsIntCon().IconValue;
                    foreach (var bits in new[] { 0UL, 1UL, 0xAAAA_5555_CCCC_3333UL, ulong.MaxValue })
                    {
                        var original = wide ? bits & unchecked((1UL << count) - 1) :
                            unchecked((uint)bits) & unchecked((1u << count) - 1);
                        var bzhi = bits & unchecked((1UL << maskedCount) - 1);
                        Assert.That(bzhi, Is.EqualTo(original));
                    }
                }
            }
            else if (shape == "andnot")
            {
                Assert.That(replacement.GetOp(1), Is.SameAs(other));
                Assert.That(replacement.GetOp(2), Is.SameAs(value));
            }
            Assert.That(root.Next is null && inner.Next is null, Is.True);
#if DEBUG
            Assert.That(replacement.TreeId, Is.Not.EqualTo(root.TreeId));
#endif
            CheckLir(compiler, block);
        });
    }

    private static IEnumerable<TestCaseData> ResetAndMaskGuards()
    {
        foreach (var xor in new[] { false, true })
        {
            foreach (var reason in new[] { "minopts", "isa", "flags", "inner-flags", "constant-flags",
                "overflow", "different-local", "unused", "address-exposed" })
            {
                yield return new(reason, xor);
            }
        }
    }

    [TestCaseSource(nameof(ResetAndMaskGuards))]
    public static void ResetAndMaskLowestBitPreserveRequiredGuards(string reason, bool xor)
    {
        WithCompiler(reason == "minopts", (compiler, block, lowering) => {
            if (reason != "isa")
            {
                Enable(compiler, InstructionSet_AVX2);
            }
            if (reason == "address-exposed")
            {
                compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.ESCAPE_ADDRESS);
            }
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var copy = compiler.gtNewLclvNode(TYP_INT, reason == "different-local" ? 1 : 0);
            var constant = compiler.gtNewIconNode(TYP_INT, -1);
            var add = new GenTreeOp(GT_ADD, TYP_INT, copy, constant);
            var operation = xor ? GT_XOR : GT_AND;
            var root = new GenTreeOp(operation, TYP_INT, value, add);
            if (reason == "flags")
            {
                root.Flags |= GTF_SET_FLAGS;
            }
            else if (reason == "inner-flags")
            {
                add.Flags |= GTF_SET_FLAGS;
            }
            else if (reason == "constant-flags")
            {
                constant.Flags |= GTF_SET_FLAGS;
            }
            else if (reason == "overflow")
            {
                add.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, root) { IsUnusedValue = true };
            foreach (var node in new GenTree[] { value, copy, constant, add, root })
            {
                block.InsertAtEnd(node);
            }
            if (reason != "unused")
            {
                block.InsertAtEnd(user);
            }
            else
            {
                root.IsUnusedValue = true;
            }

            _ = LowerBinary(lowering, root);

            Assert.That(root.Oper, Is.EqualTo(operation));
            Assert.That(Nodes(block).OfType<GenTreeHWIntrinsic>(), Is.Empty);
        });
    }

    [TestCase("commuted-add", true)]
    [TestCase("reversed-sub", false)]
    [TestCase("constant-count", false)]
    [TestCase("wrong-shift-base", false)]
    [TestCase("overflow", false)]
    [TestCase("flags", false)]
    [TestCase("mask-flags", false)]
    [TestCase("shift-flags", false)]
    [TestCase("isa", false)]
    [TestCase("unused", false)]
    public static void ZeroHighBitsPreservesPatternAndSafetyContracts(string reason, bool expected)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            if (reason != "isa")
            {
                Enable(compiler, InstructionSet_AVX2);
            }
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var one = compiler.gtNewIconNode(TYP_INT, reason == "wrong-shift-base" ? 2 : 1);
            GenTree index = reason == "constant-count" ? compiler.gtNewIconNode(TYP_INT, 9) :
                compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = new GenTreeOp(GT_LSH, TYP_INT, one, index);
            var constant = compiler.gtNewIconNode(TYP_INT, reason == "reversed-sub" ? 1 : -1);
            var commuted = reason is "commuted-add" or "reversed-sub";
            var mask = new GenTreeOp(reason == "reversed-sub" ? GT_SUB : GT_ADD, TYP_INT,
                commuted ? constant : shift, commuted ? shift : constant);
            var root = new GenTreeOp(GT_AND, TYP_INT, value, mask);
            if (reason == "overflow")
            {
                mask.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }
            else if (reason == "flags")
            {
                root.Flags |= GTF_SET_FLAGS;
            }
            else if (reason == "mask-flags")
            {
                mask.Flags |= GTF_SET_FLAGS;
            }
            else if (reason == "shift-flags")
            {
                shift.Flags |= GTF_SET_FLAGS;
            }
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, root) { IsUnusedValue = true };
            foreach (var node in new GenTree[] { value, one, index, shift, constant, mask, root })
            {
                block.InsertAtEnd(node);
            }
            if (reason != "unused")
            {
                block.InsertAtEnd(user);
            }
            else
            {
                root.IsUnusedValue = true;
            }

            _ = LowerBinary(lowering, root);

            Assert.That(Nodes(block).OfType<GenTreeHWIntrinsic>().Any(), Is.EqualTo(expected));
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void AndNotKeepsTheMemoryReadModifyWriteForm()
    {
        WithCompiler(false, (compiler, block, lowering) => {
            Enable(compiler, InstructionSet_AVX2);
            compiler.lvaTable[0].Type = TYP_LONG;
            var source = compiler.gtNewLclvNode(TYP_LONG, 0);
            var target = compiler.gtNewLclvNode(TYP_LONG, 0);
            var load = new GenTreeIndir(GT_IND, TYP_INT, source);
            var other = compiler.gtNewLclvNode(TYP_INT, 1);
            var not = new GenTreeUnOp(GT_NOT, TYP_INT, other);
            var root = new GenTreeOp(GT_AND, TYP_INT, load, not);
            var store = new GenTreeStoreInd(TYP_INT, target, root);
            foreach (var node in new GenTree[] { target, source, load, other, not, root, store })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerBinary(lowering, root), Is.SameAs(store));
            Assert.That(store.RmwStatus, Is.EqualTo(RmwStatus.STOREIND_RMW_DST_IS_OP1));
            Assert.That(store.Data, Is.SameAs(root));
            Assert.That(Nodes(block).OfType<GenTreeHWIntrinsic>(), Is.Empty);
            CheckLir(compiler, block);
        });
    }

    [TestCase("andnot", "flags", false)]
    [TestCase("andnot", "inner-flags", false)]
    [TestCase("andnot", "isa", false)]
    [TestCase("andnot", "unused", false)]
    [TestCase("extract", "flags", false)]
    [TestCase("extract", "inner-flags", false)]
    [TestCase("extract", "isa", false)]
    [TestCase("extract", "unused", false)]
    [TestCase("extract", "different-local", false)]
    [TestCase("extract", "address-exposed", true)]
    public static void UnaryBitPatternsKeepTheirNativeGuards(string shape, string reason, bool expected)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            if (reason != "isa")
            {
                Enable(compiler, InstructionSet_AVX2);
            }
            if (reason == "address-exposed")
            {
                compiler.lvaTable[0].SetAddressExposed(true, AddressExposedReason.ESCAPE_ADDRESS);
            }
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var other = compiler.gtNewLclvNode(TYP_INT, reason == "different-local" || shape == "andnot" ? 1 : 0);
            var unary = new GenTreeUnOp(shape == "andnot" ? GT_NOT : GT_NEG, TYP_INT, other);
            var root = new GenTreeOp(GT_AND, TYP_INT, value, unary);
            if (reason == "flags")
            {
                root.Flags |= GTF_SET_FLAGS;
            }
            else if (reason == "inner-flags")
            {
                unary.Flags |= GTF_SET_FLAGS;
            }
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, root) { IsUnusedValue = true };
            foreach (var node in new GenTree[] { value, other, unary, root })
            {
                block.InsertAtEnd(node);
            }
            if (reason == "unused")
            {
                root.IsUnusedValue = true;
            }
            else
            {
                block.InsertAtEnd(user);
            }

            _ = LowerBinary(lowering, root);

            Assert.That(Nodes(block).OfType<GenTreeHWIntrinsic>().Any(), Is.EqualTo(expected));
            CheckLir(compiler, block);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnchangedArithmeticStillContainsItsImmediate(bool minOpts)
    {
        WithCompiler(minOpts, (compiler, block, lowering) => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var immediate = compiler.gtNewIconNode(TYP_INT, 42);
            var root = new GenTreeOp(GT_OR, TYP_INT, value, immediate) { IsUnusedValue = true };
            block.InsertAtEnd(value);
            block.InsertAtEnd(immediate);
            block.InsertAtEnd(root);

            Assert.That(LowerBinary(lowering, root), Is.Null);
            Assert.That(root.Oper, Is.EqualTo(GT_OR));
            Assert.That(immediate.IsContained, Is.True);
            CheckLir(compiler, block);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OptimizedBitOperationHasPriorityOverScalarHardwareRecognition(bool minOpts)
    {
        WithCompiler(minOpts, (compiler, block, lowering) => {
            Enable(compiler, InstructionSet_AVX2);
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var one = compiler.gtNewIconNode(TYP_INT, 1);
            var index = compiler.gtNewLclvNode(TYP_INT, 1);
            var shift = new GenTreeOp(GT_LSH, TYP_INT, one, index);
            var not = new GenTreeUnOp(GT_NOT, TYP_INT, shift);
            var root = new GenTreeOp(GT_AND, TYP_INT, value, not) { IsUnusedValue = true };
            foreach (var node in new GenTree[] { value, one, index, shift, not, root })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerBinary(lowering, root), Is.Null);
            Assert.That(root.Oper, Is.EqualTo(minOpts ? GT_AND : GT_BIT_CLEAR));
        });
    }

    private static GenTreeOp Relop(Compiler compiler, BasicBlock block, genTreeOps oper, int local, int constant,
        var_types type = TYP_INT)
    {
        compiler.lvaTable[local].Type = type;
        var value = compiler.gtNewLclvNode(type, local);
        GenTree number = type == TYP_DOUBLE ? compiler.gtNewDconNode(type, constant) :
            compiler.gtNewIconNode(type, constant);
        var compare = new GenTreeOp(oper, TYP_INT, value, number);
        block.InsertAtEnd(value);
        block.InsertAtEnd(number);
        block.InsertAtEnd(compare);

        return compare;
    }

    private static IEnumerable<GenTree> Nodes(BasicBlock block)
    {
        for (var node = block.FirstNode; node is not null; node = node.Next)
        {
            yield return node;
        }
    }

    private static void CheckLir(Compiler compiler, BasicBlock block)
    {
#if DEBUG
        Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
    }

    private static void Enable(Compiler compiler, CORINFO_InstructionSet isa)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(isa);
        compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerBinaryArithmetic")]
    private static extern GenTree? LowerBinary(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TryLowerAndOrToCCMP")]
    private static extern bool TryCCMP(Lowering lowering, GenTreeOp node, out GenTree? next);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckConditionalCompare")]
    private static extern void ContainCCMP(Lowering lowering, GenTreeCCMP node);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableApxConditionalChaining")]
    private static extern ref int ApxChaining(ref JitConfigValues values);

    private static void WithCompiler(bool minOpts, Action<Compiler, BasicBlock, Lowering> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.lvaTable = [new() { Type = TYP_INT }, new() { Type = TYP_INT }, new() { Type = TYP_INT }];
        compiler.lvaCount = compiler.lvaTable.Length;
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
#endif
