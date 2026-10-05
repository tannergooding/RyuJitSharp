// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM || TARGET_X86
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LongDecompositionTests
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicMethods)]
    private static readonly Type s_decomposerType = typeof(Lowering).GetNestedType("DecomposeLongs",
        BindingFlags.NonPublic) ?? throw new AssertionException("Long decomposition is unavailable.");

    private static readonly Action<Compiler, Lowering, LIR.Range> s_decompose =
        (s_decomposerType.GetMethod("DecomposeRange", BindingFlags.Static | BindingFlags.Public)
            ?? throw new AssertionException("DecomposeRange is unavailable."))
        .CreateDelegate<Action<Compiler, Lowering, LIR.Range>>();

    [TestCase(0L)]
    [TestCase(-1L)]
    [TestCase(long.MinValue)]
    [TestCase(long.MaxValue)]
    [TestCase(0x1234567887654321L)]
    public static void ConstantsPreserveBothHalvesAndTheLowNodesLogicalIdentity(long value)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var constant = compiler.gtNewLconNode(value);
#if DEBUG
            var originalId = constant.TreeId;
#endif
            constant.IsUnusedValue = true;
            block.InsertAtEnd(constant);

            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            Assert.That(result.Op1.AsIntCon().IconValue, Is.EqualTo((nint)unchecked((int)value)));
            Assert.That(result.Op2.AsIntCon().IconValue, Is.EqualTo((nint)unchecked((int)(value >> 32))));
            Assert.That(result.IsUnusedValue, Is.True);
            Assert.That(result.Op1.IsUnusedValue, Is.False);
            Assert.That(result.Op2.IsUnusedValue, Is.False);
#if DEBUG
            Assert.That(result.Op1.TreeId, Is.EqualTo(originalId));
#endif
            Assert.That(block.ToArray(), Is.EqualTo([result.Op1, result.Op2, result]));
            CheckLir(compiler, block);
        });
    }

    [TestCase(GT_ADD, false)]
    [TestCase(GT_ADD, true)]
    [TestCase(GT_SUB, false)]
    [TestCase(GT_SUB, true)]
    public static void CheckedArithmeticMovesOverflowToTheHighHalf(genTreeOps oper, bool unsigned)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var first = compiler.gtNewLconNode(0x00000001FFFFFFFFL);
            var second = compiler.gtNewLconNode(1);
            var arithmetic = new GenTreeOp(oper, TYP_LONG, first, second) {
                Flags = GTF_OVERFLOW | GTF_EXCEPT | (unsigned ? GTF_UNSIGNED : 0),
                IsUnusedValue = true,
            };
            block.InsertAtEnd(first);
            block.InsertAtEnd(second);
            block.InsertAtEnd(arithmetic);

            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            Assert.That(result.Op1, Is.SameAs(arithmetic));
            Assert.That(arithmetic.Type, Is.EqualTo(TYP_INT));
            Assert.That(arithmetic.Oper, Is.EqualTo(oper is GT_ADD ? GT_ADD_LO : GT_SUB_LO));
            Assert.That(arithmetic.Flags & (GTF_OVERFLOW | GTF_EXCEPT | GTF_SET_FLAGS), Is.EqualTo(GTF_SET_FLAGS));
            Assert.That(result.Op2.Oper, Is.EqualTo(oper is GT_ADD ? GT_ADD_HI : GT_SUB_HI));
            Assert.That(result.Op2.Flags & (GTF_OVERFLOW | GTF_EXCEPT), Is.EqualTo(GTF_OVERFLOW | GTF_EXCEPT));
            Assert.That(result.Op2.AsUnOp().IsUnsigned, Is.EqualTo(unsigned));
            Assert.That(arithmetic.Next, Is.SameAs(result.Op2));
            CheckLir(compiler, block);
        });
    }

    [TestCase(GT_AND)]
    [TestCase(GT_OR)]
    [TestCase(GT_XOR)]
    [TestCase(GT_NOT)]
    public static void BitwiseOperationsSplitWithoutCarryFlags(genTreeOps oper)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var first = compiler.gtNewLconNode(-81985529216486896L);
            var second = compiler.gtNewLconNode(0x1234567887654321L);
            GenTree operation = oper is GT_NOT
                ? compiler.gtNewUnaryNode(oper, TYP_LONG, first)
                : compiler.gtNewBinaryNode(oper, TYP_LONG, first, second);
            operation.IsUnusedValue = true;
            block.InsertAtEnd(first);
            if (oper is not GT_NOT)
            {
                block.InsertAtEnd(second);
            }

            block.InsertAtEnd(operation);
            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            Assert.That(result.Op1.Oper, Is.EqualTo(oper));
            Assert.That(result.Op2.Oper, Is.EqualTo(oper));
            Assert.That((result.Op1.Flags | result.Op2.Flags) & GTF_SET_FLAGS, Is.EqualTo(GTF_EMPTY));
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void NegationRetainsTheTargetSpecificCarrySequence()
    {
        WithLowering((compiler, block, lowering) =>
        {
            var value = compiler.gtNewLconNode(0x1234567887654321L);
            var negation = compiler.gtNewUnaryNode(GT_NEG, TYP_LONG, value);
            negation.IsUnusedValue = true;
            block.InsertAtEnd(value);
            block.InsertAtEnd(negation);

            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            Assert.That(result.Op1, Is.SameAs(negation));
            Assert.That(negation.Flags & GTF_SET_FLAGS, Is.EqualTo(GTF_SET_FLAGS));
#if TARGET_ARM
            var high = result.Op2.AsOp();
            Assert.That(high.Oper, Is.EqualTo(GT_SUB_HI));
            Assert.That(high.Op1.IsIntegralConst(0), Is.True);
            Assert.That(high.Op1.Next, Is.SameAs(negation));
            Assert.That(negation.Next, Is.SameAs(high));
#else
            Assert.That(result.Op2.Oper, Is.EqualTo(GT_NEG));
            var high = result.Op2.AsUnOp().Op1.AsOp();
            Assert.That(high.Oper, Is.EqualTo(GT_ADD_HI));
            Assert.That(high.Op2.IsIntegralConst(0), Is.True);
            Assert.That(negation.Next, Is.SameAs(high.Op2));
            Assert.That(high.Next, Is.SameAs(result.Op2));
#endif
            CheckLir(compiler, block);
        });
    }

    [Test, Combinatorial]
    public static void ConstantShiftsPreserveAllBitsAcrossHalfAndModuloBoundaries(
        [Values(GT_LSH, GT_RSH, GT_RSZ)] genTreeOps oper,
        [Values(0, 1, 31, 32, 33, 63, 64, -1)] int count)
    {
        WithLowering((compiler, block, lowering) =>
        {
            const long Value = -81985529216486896L;
            var value = compiler.gtNewLconNode(Value);
            var amount = compiler.gtNewIconNode(TYP_INT, count);
            var shift = compiler.gtNewBinaryNode(oper, TYP_LONG, value, amount);
            shift.IsUnusedValue = true;
            block.InsertAtEnd(value);
            block.InsertAtEnd(amount);
            block.InsertAtEnd(shift);

            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            var expected = oper switch {
                GT_LSH => unchecked((ulong)(Value << count)),
                GT_RSH => unchecked((ulong)(Value >> count)),
                GT_RSZ => unchecked((ulong)Value) >> count,
                _ => throw new AssertionException("Unexpected test shift."),
            };
            Assert.That(EvaluateShiftRange(block, result), Is.EqualTo(expected));
            Assert.That(block.Any(node => node.Oper is GT_CALL), Is.False);
            CheckLir(compiler, block);
        });
    }

    [Test, Combinatorial]
    public static void ConstantRotatesPreserveHalfSwapsAndOperandOrdering(
        [Values(GT_ROL, GT_ROR)] genTreeOps oper,
        [Values(1, 31, 32, 33, 63)] int count)
    {
        WithLowering((compiler, block, lowering) =>
        {
            const long Value = -81985529216486896L;
            var value = compiler.gtNewLconNode(Value);
            var amount = compiler.gtNewIconNode(TYP_INT, count);
            var rotate = compiler.gtNewBinaryNode(oper, TYP_LONG, value, amount);
            rotate.IsUnusedValue = true;
            block.InsertAtEnd(value);
            block.InsertAtEnd(amount);
            block.InsertAtEnd(rotate);

            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            var bits = unchecked((ulong)Value);
            var expected = oper is GT_ROL
                ? (bits << count) | (bits >> (64 - count))
                : (bits >> count) | (bits << (64 - count));
            Assert.That(EvaluateShiftRange(block, result), Is.EqualTo(expected));
            CheckLir(compiler, block);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnpromotedLocalReadsBecomeFieldsWithoutChangingTheOwningUse(bool unused)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var read = compiler.gtNewLclvNode(TYP_LONG, 0);
            read.Flags |= GTF_VAR_DEATH;
            read.SsaNum = 7;
            read.IsUnusedValue = unused;
            var store = new GenTreeLclVar(TYP_LONG, 0, read) { Flags = GTF_VAR_DEF };
            block.InsertAtEnd(read);
            if (!unused)
            {
                block.InsertAtEnd(store);
            }

            s_decompose(compiler, lowering, block);

            var result = RequireLong(unused ? block.LastNode : store.Op1);
            Assert.That(result.Op1.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(result.Op1.AsLclFld().LclOffs, Is.Zero);
            Assert.That(result.Op1.AsLclFld().SsaNum, Is.EqualTo(7));
            Assert.That(result.Op1.Flags & GTF_VAR_DEATH, Is.EqualTo(GTF_VAR_DEATH));
            Assert.That(result.Op2.AsLclFld().LclOffs, Is.EqualTo(4));
            Assert.That(compiler.lvaGetDesc(0).lvDoNotEnregister, Is.True);
            if (!unused)
            {
                Assert.That(store.Type, Is.EqualTo(TYP_LONG));
            }

            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void PromotionReacquiresTheDescriptorAfterLocalTableGrowth()
    {
        WithLowering((compiler, block, lowering) =>
        {
            compiler.opts.compFlags |= CLFLG_REGVAR;
            compiler.lvaGetDesc(0).setLvRefCnt(1);
            var oldTable = compiler.lvaTable;
            Prepare(compiler, lowering);

            Assert.That(compiler.lvaTable, Is.Not.SameAs(oldTable));
            ref var parent = ref compiler.lvaGetDesc(0);
            Assert.That(parent.lvPromoted, Is.True);
            Assert.That(parent.lvFieldCnt, Is.EqualTo(2));
            Assert.That(parent.lvFieldLclStart, Is.EqualTo(1));
            Assert.That(parent.lvContainsHoles, Is.False);
            Assert.That(compiler.lvaCount, Is.EqualTo(3));
            for (var index = 0; index < 2; index++)
            {
                ref var field = ref compiler.lvaGetDesc(parent.lvFieldLclStart + index);
                Assert.That(field.Type, Is.EqualTo(TYP_INT));
                Assert.That(field.lvIsStructField, Is.True);
                Assert.That(field.lvParentLcl, Is.Zero);
                Assert.That(field.lvFldOffset, Is.EqualTo(index * 4));
                Assert.That(field.lvFldOrdinal, Is.EqualTo(index));
                Assert.That(field.lvIsTemp, Is.False);
            }

            var constant = compiler.gtNewLconNode(0x1234567887654321L);
            var store = compiler.gtNewStoreLclVarNode(0, constant);
            block.InsertAtEnd(constant);
            block.InsertAtEnd(store);
            s_decompose(compiler, lowering, block);

            Assert.That(store.LclNum, Is.EqualTo(1));
            Assert.That(store.Type, Is.EqualTo(TYP_INT));
            var highStore = (block.LastNode ?? throw new AssertionException("Missing high store.")).AsLclVar();
            Assert.That(highStore.LclNum, Is.EqualTo(2));
            Assert.That(highStore.Flags, Is.EqualTo(GTF_VAR_DEF));
            Assert.That(block.Any(node => node.Oper is GT_LONG), Is.False);
            CheckLir(compiler, block);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void IntegerWideningKeepsZeroAndSignExtensionDistinct(bool unsigned)
    {
        WithLowering((compiler, block, lowering) =>
        {
            compiler.lvaGetDesc(0).Type = TYP_INT;
            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            var cast = new GenTreeCast(TYP_LONG, source, unsigned, TYP_LONG) { IsUnusedValue = true };
            block.InsertAtEnd(source);
            block.InsertAtEnd(cast);

            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            if (unsigned)
            {
                Assert.That(result.Op1, Is.SameAs(source));
                Assert.That(result.Op2.IsIntegralConst(0), Is.True);
            }
            else
            {
                Assert.That(result.Op1.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(result.Op2.Oper, Is.EqualTo(GT_RSH));
                Assert.That(result.Op2.AsOp().Op2.IsIntegralConst(31), Is.True);
                Assert.That(result.Op2.AsOp().Op1.AsLclVar().LclNum, Is.EqualTo(result.Op1.AsLclVar().LclNum));
            }

            Assert.That(block.Any(node => node == cast), Is.False);
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void CheckedSignedToUnsignedWideningRetainsTheLowOverflowCheck()
    {
        WithLowering((compiler, block, lowering) =>
        {
            compiler.lvaGetDesc(0).Type = TYP_INT;
            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            var cast = new GenTreeCast(TYP_LONG, source, false, TYP_ULONG) {
                Flags = GTF_OVERFLOW | GTF_EXCEPT,
                IsUnusedValue = true,
            };
            block.InsertAtEnd(source);
            block.InsertAtEnd(cast);
            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            Assert.That(result.Op1, Is.SameAs(cast));
            Assert.That(cast.CastType, Is.EqualTo(TYP_UINT));
            Assert.That(cast.Type, Is.EqualTo(TYP_INT));
            Assert.That(cast.HasOverflowCheck, Is.True);
            Assert.That(result.Op2.IsIntegralConst(0), Is.True);
            CheckLir(compiler, block);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CheckedLongSignednessChangesCheckOnlyTheHighHalf(bool unsigned)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var source = compiler.gtNewLconNode(long.MaxValue);
            var cast = new GenTreeCast(TYP_LONG, source, unsigned, unsigned ? TYP_LONG : TYP_ULONG) {
                IsUnusedValue = true,
            };
            cast.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            block.InsertAtEnd(source);
            block.InsertAtEnd(cast);
            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            Assert.That(result.Op2, Is.SameAs(cast));
            Assert.That(cast.CastType, Is.EqualTo(TYP_UINT));
            Assert.That(cast.IsUnsigned, Is.False);
            Assert.That(cast.HasOverflowCheck, Is.True);
            Assert.That(cast.CastOp.IsIntegralConst(int.MaxValue), Is.True);
            CheckLir(compiler, block);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LongMultiplyConsumesTheSkippedCastAndMarksItsStoreMultiRegister(bool constantSecond)
    {
        WithLowering((compiler, block, lowering) =>
        {
            compiler.lvaGetDesc(0).Type = TYP_INT;
            var first = compiler.gtNewLclvNode(TYP_INT, 0);
            var firstCast = new GenTreeCast(TYP_LONG, first, false, TYP_LONG);
            GenTree second = constantSecond
                ? compiler.gtNewLconNode(-7)
                : new GenTreeCast(TYP_LONG, compiler.gtNewLclvNode(TYP_INT, 0), false, TYP_LONG);
            var multiply = compiler.gtNewBinaryNode(GT_MUL, TYP_LONG, firstCast, second);
            multiply.Flags |= GTF_MUL_64RSLT;
#if DEBUG
            var multiplyTreeId = multiply.TreeId;
#endif
            var destination = compiler.lvaGrabTemp(false, "long multiply destination");
            compiler.lvaGetDesc(destination).Type = TYP_LONG;
            var store = compiler.gtNewStoreLclVarNode(destination, multiply);
            block.InsertAtEnd(first);
            block.InsertAtEnd(firstCast);
            if (second is GenTreeCast secondCast)
            {
                block.InsertAtEnd(secondCast.CastOp);
            }

            block.InsertAtEnd(second);
            block.InsertAtEnd(multiply);
            block.InsertAtEnd(store);
            s_decompose(compiler, lowering, block);

            var multiRegMultiply = store.Op1.AsOp();
            Assert.That(multiRegMultiply, Is.InstanceOf<GenTreeMultiRegOp>());
            Assert.That(multiRegMultiply.Oper, Is.EqualTo(GT_MUL_LONG));
            Assert.That(multiRegMultiply.Type, Is.EqualTo(TYP_LONG));
            Assert.That(multiRegMultiply.Op1, Is.SameAs(first));
            Assert.That(multiRegMultiply.Op2.Type, Is.EqualTo(TYP_INT));
            Assert.That(multiRegMultiply.Is64RsltMul, Is.True);
            Assert.That(store.Op1, Is.SameAs(multiRegMultiply));
#if DEBUG
            Assert.That(multiRegMultiply.TreeId, Is.EqualTo(multiplyTreeId));
#endif
            Assert.That(compiler.lvaGetDesc(destination).IsMultiRegDest, Is.True);
            Assert.That(block.Any(node => node == firstCast), Is.False);
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void FieldListLongArgumentsSplitIntoAdjacentIntFields()
    {
        WithLowering((compiler, block, lowering) =>
        {
            var value = compiler.gtNewLconNode(0x1234567887654321L);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, value, 8, TYP_LONG);
            block.InsertAtEnd(value);
            block.InsertAtEnd(fields);
            s_decompose(compiler, lowering, block);

            var uses = fields.Uses.ToArray();
            Assert.That(uses, Has.Length.EqualTo(2));
            Assert.That(uses[0].Offset, Is.EqualTo(8));
            Assert.That(uses[1].Offset, Is.EqualTo(12));
            Assert.That(uses.All(use => use.Type is TYP_INT), Is.True);
            Assert.That(uses[0].Node.IsIntegralConst(unchecked((int)0x87654321)), Is.True);
            Assert.That(uses[1].Node.IsIntegralConst(0x12345678), Is.True);
            Assert.That(block.Any(node => node.Oper is GT_LONG), Is.False);
        });
    }

    [Test]
    public static void IndirectionLoadsReuseOneAddressAndPreserveTheHighLoadFlags()
    {
        WithLowering((compiler, block, lowering) =>
        {
            compiler.lvaGetDesc(0).Type = TYP_BYREF;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var load = new GenTreeIndir(GT_IND, TYP_LONG, address) {
                Flags = GTF_GLOB_REF | GTF_EXCEPT | GTF_IND_VOLATILE | GTF_IND_UNALIGNED,
                IsUnusedValue = true,
            };
            block.InsertAtEnd(address);
            block.InsertAtEnd(load);
            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            Assert.That(result.Op1, Is.SameAs(load));
            Assert.That(load.Type, Is.EqualTo(TYP_INT));
            var high = result.Op2.AsIndir();
            Assert.That(high.Flags & (GTF_GLOB_REF | GTF_EXCEPT | GTF_IND_FLAGS),
                Is.EqualTo(load.Flags & (GTF_GLOB_REF | GTF_EXCEPT | GTF_IND_FLAGS)));
            var highAddress = high.Addr.AsAddrMode();
            Assert.That(highAddress.Offset, Is.EqualTo(4));
            Assert.That(highAddress.BaseAddress?.AsLclVar().LclNum, Is.EqualTo(load.Addr.AsLclVar().LclNum));
            Assert.That(block.Count(node => node.Oper is GT_STORE_LCL_VAR), Is.EqualTo(1));
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void IndirectionStoresKeepLowThenHighOrderAndNativeFlagMasking()
    {
        WithLowering((compiler, block, lowering) =>
        {
            compiler.lvaGetDesc(0).Type = TYP_BYREF;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var value = compiler.gtNewLconNode(0x1234567887654321L);
            var store = new GenTreeStoreInd(TYP_LONG, address, value) {
                Flags = GTF_ASG | GTF_GLOB_REF | GTF_EXCEPT | GTF_IND_VOLATILE | GTF_IND_UNALIGNED,
            };
            block.InsertAtEnd(address);
            block.InsertAtEnd(value);
            block.InsertAtEnd(store);
            s_decompose(compiler, lowering, block);

            var stores = block.Where(node => node.Oper is GT_STOREIND).ToArray();
            Assert.That(stores, Has.Length.EqualTo(2));
            Assert.That(stores[0], Is.SameAs(store));
            Assert.That(store.Data.IsIntegralConst(unchecked((int)0x87654321)), Is.True);
            var high = stores[1].AsStoreInd();
            Assert.That(high.Data.IsIntegralConst(0x12345678), Is.True);
            Assert.That(high.Type, Is.EqualTo(TYP_INT));
            Assert.That(high.Flags, Is.EqualTo(store.Flags & (GTF_ALL_EFFECT | GTF_LIVENESS_MASK)));
            Assert.That(high.Addr.AsAddrMode().Offset, Is.EqualTo(4));
            Assert.That(store.Next, Is.SameAs(high.Data));
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void SelectHighHalfConsumesTheLowSelectFlags()
    {
        WithLowering((compiler, block, lowering) =>
        {
            var condition = compiler.gtNewIconNode(TYP_INT, 1);
            var first = compiler.gtNewLconNode(0x1234567887654321L);
            var second = compiler.gtNewLconNode(-1);
            var select = new GenTreeConditional(GT_SELECT, TYP_LONG, condition, first, second) {
                IsUnusedValue = true,
            };
            block.InsertAtEnd(condition);
            block.InsertAtEnd(first);
            block.InsertAtEnd(second);
            block.InsertAtEnd(select);
            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            Assert.That(result.Op1, Is.SameAs(select));
            Assert.That(select.Cond, Is.SameAs(condition));
            Assert.That(select.Type, Is.EqualTo(TYP_INT));
            Assert.That(select.Flags & GTF_SET_FLAGS, Is.EqualTo(GTF_SET_FLAGS));
            var high = result.Op2.AsOpCC();
            Assert.That(high.Oper, Is.EqualTo(GT_SELECTCC));
            Assert.That(high.Condition.Code, Is.EqualTo(GenCondition.NE));
            Assert.That(select.Next, Is.SameAs(high));
            Assert.That(select.Op1.IsIntegralConst(unchecked((int)0x87654321)), Is.True);
            Assert.That(high.Op1.IsIntegralConst(0x12345678), Is.True);
            CheckLir(compiler, block);
        });
    }

    [TestCase(TYP_INT, false)]
    [TestCase(TYP_BYTE, false)]
    [TestCase(TYP_INT, true)]
    public static void TruncationDropsTheHighHalfOnlyForUncheckedCasts(var_types destinationType, bool checkedCast)
    {
        WithLowering((compiler, block, lowering) =>
        {
            var value = compiler.gtNewLconNode(0x1234567887654321L);
            var cast = new GenTreeCast(TYP_INT, value, false, destinationType) { IsUnusedValue = true };
            if (checkedCast)
            {
                cast.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }

            block.InsertAtEnd(value);
            block.InsertAtEnd(cast);
            s_decompose(compiler, lowering, block);

            if (checkedCast)
            {
                Assert.That(cast.CastOp.Oper, Is.EqualTo(GT_LONG));
                Assert.That(block.LastNode, Is.SameAs(cast));
            }
            else if (destinationType is TYP_BYTE)
            {
                Assert.That(cast.CastOp.IsIntegralConst(unchecked((int)0x87654321)), Is.True);
                Assert.That(block.LastNode, Is.SameAs(cast));
            }
            else
            {
                Assert.That(block.LastNode?.IsIntegralConst(unchecked((int)0x87654321)), Is.True);
                Assert.That(block.LastNode?.IsUnusedValue, Is.True);
                Assert.That(block.Any(node => node == cast), Is.False);
            }

            Assert.That(block.Count(node => node.Oper is GT_CNS_INT), Is.EqualTo(checkedCast ? 2 : 1));
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void UnsignedModuloKeepsTheFullDividendAndZeroesTheHighResult()
    {
        WithLowering((compiler, block, lowering) =>
        {
            var dividend = compiler.gtNewLconNode(long.MaxValue);
            var divisor = compiler.gtNewLconNode(0x3fffffff);
            var remainder = compiler.gtNewBinaryNode(GT_UMOD, TYP_LONG, dividend, divisor);
            remainder.IsUnusedValue = true;
            block.InsertAtEnd(dividend);
            block.InsertAtEnd(divisor);
            block.InsertAtEnd(remainder);
            s_decompose(compiler, lowering, block);

            var result = RequireLong(block.LastNode);
            Assert.That(result.Op1, Is.SameAs(remainder));
            Assert.That(remainder.Type, Is.EqualTo(TYP_INT));
            Assert.That(remainder.Op1.Oper, Is.EqualTo(GT_LONG));
            Assert.That(remainder.Op2.IsIntegralConst(0x3fffffff), Is.True);
            Assert.That(result.Op2.IsIntegralConst(0), Is.True);
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void ParameterPromotionRetainsTheTargetPolicy()
    {
        WithLowering((compiler, _, lowering) =>
        {
            compiler.opts.compFlags |= CLFLG_REGVAR;
            compiler.lvaGetDesc(0).setLvRefCnt(1);
            compiler.lvaGetDesc(0).lvIsParam = true;
            compiler.lvaGetDesc(0).lvIsRegArg = true;
            Prepare(compiler, lowering);
#if TARGET_ARM
            Assert.That(compiler.lvaGetDesc(0).lvPromoted, Is.True);
            Assert.That(compiler.lvaGetDesc(0).lvDoNotEnregister, Is.True);
            for (var index = 1; index < 3; index++)
            {
                ref var field = ref compiler.lvaGetDesc(index);
                Assert.That(field.lvIsParam, Is.True);
                Assert.That(field.lvIsRegArg, Is.True);
                Assert.That(field.lvDoNotEnregister, Is.True);
            }
#else
            Assert.That(compiler.lvaGetDesc(0).lvPromoted, Is.False);
            Assert.That(compiler.lvaCount, Is.EqualTo(1));
#endif
        });
    }

    private static GenTreeOp RequireLong(GenTree? node)
    {
        Assert.That(node, Is.Not.Null);
        Assert.That(node?.Oper, Is.EqualTo(GT_LONG));
        return (GenTreeOp)(node ?? throw new AssertionException("Missing decomposed result."));
    }

    private static ulong EvaluateShiftRange(LIR.Range range, GenTree result)
    {
        var values = new Dictionary<GenTree, ulong>();
        var locals = new Dictionary<int, ulong>();
        foreach (var node in range)
        {
            ulong value;
            switch (node.Oper)
            {
                case GT_CNS_INT:
                {
                    value = unchecked((uint)node.AsIntCon().IconValue);
                    break;
                }

                case GT_STORE_LCL_VAR:
                {
                    locals[node.AsLclVar().LclNum] = values[node.AsUnOp().Op1];
                    continue;
                }

                case GT_LCL_VAR:
                {
                    value = locals[node.AsLclVar().LclNum];
                    break;
                }

                case GT_LONG:
                {
                    var op = node.AsOp();
                    value = (uint)values[op.Op1] | ((ulong)(uint)values[op.Op2] << 32);
                    break;
                }

                case GT_LSH:
                case GT_RSH:
                case GT_RSZ:
                case GT_LSH_HI:
                case GT_RSH_LO:
                {
                    var op = node.AsOp();
                    var operand = values[op.Op1];
                    var count = (int)values[op.Op2];
                    value = node.Oper switch {
                        GT_LSH => unchecked((uint)operand << count),
                        GT_RSH => unchecked((uint)((int)operand >> count)),
                        GT_RSZ => unchecked((uint)operand) >> count,
                        GT_LSH_HI => unchecked((uint)((operand << count) >> 32)),
                        GT_RSH_LO => unchecked((uint)(operand >> count)),
                        _ => throw new AssertionException("Unexpected shift node."),
                    };
                    break;
                }

                default:
                {
                    throw new AssertionException($"Unexpected node in shift evaluation: {node.Oper}.");
                }
            }

            values.Add(node, value);
        }

        return values[result];
    }

    private static void Prepare(Compiler compiler, Lowering lowering)
    {
        var decomposer = Activator.CreateInstance(s_decomposerType, compiler, lowering)
            ?? throw new AssertionException("Could not construct the decomposer.");
        var prepare = s_decomposerType.GetMethod("PrepareForDecomposition")
            ?? throw new AssertionException("PrepareForDecomposition is unavailable.");
        _ = prepare.Invoke(decomposer, null);
    }

    private static void CheckLir(Compiler compiler, LIR.Range range)
    {
#if DEBUG
        Assert.That(range.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    private static void WithLowering(Action<Compiler, BasicBlock, Lowering> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_LONG }];
        compiler.lvaCount = 1;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            compiler.compCurBB = block;
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
