// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LoweringDispatchTests
{
    [TestCase(GT_LSH, TYP_INT, 3L, 33, 6L)]
    [TestCase(GT_RSH, TYP_LONG, long.MinValue, 63, -1L)]
    [TestCase(GT_RSZ, TYP_INT, -1L, 4, 268435455L)]
    [TestCase(GT_ROL, TYP_INT, (long)int.MinValue, 1, 1L)]
    [TestCase(GT_ROR, TYP_LONG, 1L, 1, long.MinValue)]
    [TestCase(GT_AND, TYP_INT, 6L, 3, 2L)]
    [TestCase(GT_OR, TYP_INT, 6L, 3, 7L)]
    [TestCase(GT_XOR, TYP_INT, 6L, 3, 5L)]
    public static void OptimizedBinaryDispatchFoldsConstantsAndPreservesOwner(
        genTreeOps oper, var_types type, long value, int count, long expected)
    {
        WithCompiler(compiler => {
            var source = compiler.gtNewIconNode(type, unchecked((nint)value));
            var amount = compiler.gtNewIconNode(TYP_INT, count);
            var operation = new GenTreeOp(oper, type, source, amount);
            operation._vnPair.SetBoth(314);
            var owner = new GenTreeUnOp(GT_RETURN, TYP_VOID, operation);
            var block = NewBlock(source, amount, operation, owner);
#if DEBUG
            var id = operation.TreeId;
#endif

            new Lowering(compiler, new LinearScan(compiler)).LowerRange(
                block, new LIR.ReadOnlyRange(source, operation));

            Assert.That(owner.Op1.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(owner.Op1.Type, Is.EqualTo(type));
            Assert.That(owner.Op1.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)expected)));
            Assert.That(owner.Op1._vnPair.Conservative, Is.EqualTo(314u));
            Assert.That(block.FirstNode, Is.SameAs(owner.Op1));
            Assert.That(owner.Op1.Next, Is.SameAs(owner));
            Assert.That(source.Prev is null && source.Next is null, Is.True);
            Assert.That(amount.Prev is null && amount.Next is null, Is.True);
            Assert.That(operation.Prev is null && operation.Next is null, Is.True);
#if DEBUG
            Assert.That(owner.Op1.TreeId, Is.EqualTo(id));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }, minOpts: false);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AndNegativeOneDispatchPreservesOwningUseOrUnusedResult(bool unused)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var mask = compiler.gtNewIconNode(TYP_INT, -1);
            var operation = new GenTreeOp(GT_AND, TYP_INT, value, mask) { IsUnusedValue = unused };
            var successor = unused
                ? new GenTree(GT_NO_OP, TYP_VOID)
                : new GenTreeUnOp(GT_RETURN, TYP_VOID, operation);
            var block = NewBlock(value, mask, operation, successor);

            new Lowering(compiler, new LinearScan(compiler)).LowerRange(
                block, new LIR.ReadOnlyRange(value, operation));

            Assert.That(block.FirstNode, Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(successor));
            Assert.That(value.IsUnusedValue, Is.EqualTo(unused));
            Assert.That(mask.Prev is null && mask.Next is null, Is.True);
            Assert.That(operation.Prev is null && operation.Next is null, Is.True);
            if (!unused)
            {
                Assert.That(successor.AsUnOp().Op1, Is.SameAs(value));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }, minOpts: false);
    }

    [Test]
    public static void AlreadyLoweredCompareJumpPassesThroughUnchanged()
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 7);
            var jump = new GenTreeOpCC(GT_JCMP, TYP_VOID, new GenCondition(GenCondition.EQ), value, constant);
            var block = NewBlock(value, constant, jump);

            new Lowering(compiler, new LinearScan(compiler)).LowerRange(
                block, new LIR.ReadOnlyRange(value, jump));

            Assert.That(block.LastNode, Is.SameAs(jump));
            Assert.That(jump.Op1, Is.SameAs(value));
            Assert.That(jump.Op2, Is.SameAs(constant));
            Assert.That(value.Next, Is.SameAs(constant));
            Assert.That(constant.Next, Is.SameAs(jump));
            Assert.That(constant.IsContained, Is.False);
        });
    }

    [TestCase(GT_BSWAP, TYP_INT, false)]
    [TestCase(GT_BSWAP, TYP_INT, true)]
    [TestCase(GT_BSWAP16, TYP_USHORT, false)]
    [TestCase(GT_BSWAP16, TYP_USHORT, true)]
    public static void ByteSwapLoweringIsNotRepeatedByContainmentTraversal(
        genTreeOps oper, var_types loadType, bool containmentOnly)
    {
        WithCompiler(compiler => {
            compiler.opts.compSupportsISA.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX2);
            compiler.opts.compSupportsISAReported.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX2);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(CORINFO_InstructionSet.InstructionSet_AVX2);
            var address = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1234);
            var operand = new GenTreeIndir(GT_IND, loadType, address);
            var operation = new GenTreeUnOp(oper, TYP_INT, operand);
            var block = NewBlock(address, operand, operation);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            if (containmentOnly)
            {
                LoweringBlock(lowering) = block;
                ContainCheckNode(lowering, operation);
            }
            else
            {
                lowering.LowerRange(block, new LIR.ReadOnlyRange(operation, operation));
            }

            Assert.That(operand.IsContained, Is.EqualTo(!containmentOnly));
            Assert.That(operation.Op1, Is.SameAs(operand));
            Assert.That(operand.Next, Is.SameAs(operation));
        }, minOpts: false);
    }

    [TestCase(GT_LSH, false, false)]
    [TestCase(GT_LSH, true, false)]
    [TestCase(GT_LSH, true, true)]
    [TestCase(GT_ROR, false, false)]
    [TestCase(GT_ROR, true, false)]
    [TestCase(GT_ROR, true, true)]
    public static void ZeroShiftDispatchPreservesFlagSettersAndUnusedValues(
        genTreeOps oper, bool unused, bool setsFlags)
    {
        WithCompiler(compiler => {
            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var operation = new GenTreeOp(oper, TYP_INT, source, zero) {
                IsUnusedValue = unused,
            };
            if (setsFlags)
            {
                operation.Flags |= GenTreeFlags.GTF_SET_FLAGS;
            }
            var successor = unused
                ? new GenTree(GT_NO_OP, TYP_VOID)
                : new GenTreeUnOp(GT_RETURN, TYP_VOID, operation);
            var block = NewBlock(source, zero, operation, successor);

            new Lowering(compiler, new LinearScan(compiler)).LowerRange(
                block, new LIR.ReadOnlyRange(source, operation));

            Assert.That(source.IsUnusedValue, Is.EqualTo(unused && !setsFlags));
            Assert.That(source.Next, Is.SameAs(setsFlags ? zero : successor));
            if (setsFlags)
            {
                Assert.That(operation.Next, Is.SameAs(successor));
                Assert.That(operation.Flags & GenTreeFlags.GTF_SET_FLAGS, Is.Not.Zero);
            }
            else
            {
                Assert.That(operation.Prev is null && operation.Next is null, Is.True);
                Assert.That(zero.Prev is null && zero.Next is null, Is.True);
                if (!unused)
                {
                    Assert.That(successor.AsUnOp().Op1, Is.SameAs(source));
                }
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }, minOpts: false);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CastDispatchUsesConstantFoldingOnlyWhenOptimized(bool minOpts)
    {
        WithCompiler(compiler => {
            var source = compiler.gtNewIconNode(TYP_INT, -1);
            var cast = new GenTreeCast(TYP_LONG, source, false, TYP_LONG);
            var owner = new GenTreeUnOp(GT_RETURN, TYP_VOID, cast);
            var block = NewBlock(source, cast, owner);

            new Lowering(compiler, new LinearScan(compiler)).LowerRange(
                block, new LIR.ReadOnlyRange(source, cast));

            Assert.That(owner.Op1.Oper, Is.EqualTo(minOpts ? GT_CAST : GT_CNS_INT));
            Assert.That(owner.Op1.Next, Is.SameAs(owner));
            Assert.That(source.IsUnusedValue, Is.EqualTo(!minOpts));
            if (!minOpts)
            {
                Assert.That(owner.Op1.AsIntCon().IconValue, Is.EqualTo((nint)(-1)));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        }, minOpts);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void NonLocalJumpDispatchPreservesAddressAndContainment(bool memorySource, bool containmentOnly)
    {
        WithCompiler(compiler => {
            compiler.opts.compFlags = Globals.CLFLG_REGVAR;
            compiler.lvaTable[0].Type = Globals.TYP_I_IMPL;
            compiler.lvaTable[0].lvDoNotEnregister = memorySource;
            var address = compiler.gtNewLclvNode(Globals.TYP_I_IMPL, 0);
            var jump = new GenTreeUnOp(GT_NONLOCAL_JMP, TYP_VOID, address);
            var block = NewBlock(address, jump);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            if (containmentOnly)
            {
                LoweringBlock(lowering) = block;
                ContainCheckNode(lowering, jump);
            }
            else
            {
                lowering.LowerRange(block, new LIR.ReadOnlyRange(address, jump));
            }

            Assert.That(jump.Op1, Is.SameAs(address));
            Assert.That(address.IsContained, Is.EqualTo(memorySource));
            Assert.That(address.IsRegOptional, Is.EqualTo(!memorySource));
            Assert.That(address.Next, Is.SameAs(jump));
            Assert.That(block.LastNode, Is.SameAs(jump));
        });
    }

    [TestCase(GT_NEG, TYP_INT)]
    [TestCase(GT_NEG, TYP_LONG)]
    [TestCase(GT_NEG, TYP_DOUBLE)]
    [TestCase(GT_NOT, TYP_INT)]
    public static void XarchUnaryOperationsPreserveNodesAndContinue(genTreeOps oper, var_types type)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = type;
            var value = compiler.gtNewLclvNode(type, 0);
            var operation = new GenTreeUnOp(oper, type, value);
            operation._vnPair.SetBoth(123);
            var sentinel = compiler.gtNewIconNode(TYP_INT, 7);
            var block = NewBlock(value, operation, sentinel);
            var flags = operation.Flags;

            var lowering = new Lowering(compiler, new LinearScan(compiler));
            lowering.LowerRange(block, new LIR.ReadOnlyRange(value, operation));

            Assert.That(block.FirstNode, Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(operation));
            Assert.That(operation.Next, Is.SameAs(sentinel));
            Assert.That(operation.Op1, Is.SameAs(value));
            Assert.That(operation.Oper, Is.EqualTo(oper));
            Assert.That(operation.Flags, Is.EqualTo(flags));
            Assert.That(operation._vnPair.Liberal, Is.EqualTo(123u));
            Assert.That(value.IsContained || value.IsRegOptional, Is.False);
        });
    }

    [TestCase(GT_EQ)]
    [TestCase(GT_NE)]
    [TestCase(GT_LT)]
    [TestCase(GT_LE)]
    [TestCase(GT_GT)]
    [TestCase(GT_GE)]
    [TestCase(GT_TEST_EQ)]
    [TestCase(GT_TEST_NE)]
    public static void ComparisonDispatchLowersOperandsAndPreservesOwner(genTreeOps oper)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 7);
            var comparison = new GenTreeOp(oper, TYP_INT, value, constant);
            var owner = compiler.gtNewStoreLclVarNode(1, comparison);
            var block = NewBlock(value, constant, comparison, owner);

            var lowering = new Lowering(compiler, new LinearScan(compiler));
            lowering.LowerRange(block, new LIR.ReadOnlyRange(value, comparison));

            Assert.That(constant.IsContained, Is.True);
            Assert.That(owner.Data, Is.SameAs(comparison));
            Assert.That(comparison.Oper, Is.EqualTo(oper));
            Assert.That(comparison.Next, Is.SameAs(owner));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void BoundsCheckDispatchContainsImmediateOperand(bool constantIndex)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 7);
            GenTree index = constantIndex ? constant : value;
            GenTree length = constantIndex ? value : constant;
            var check = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            var block = NewBlock(index, length, check);

            var lowering = new Lowering(compiler, new LinearScan(compiler));
            lowering.LowerRange(block, new LIR.ReadOnlyRange(index, check));

            Assert.That(constant.IsContained, Is.True);
            Assert.That(value.IsContained, Is.True);
            Assert.That(value.IsRegOptional, Is.False);
            Assert.That(check.Index, Is.SameAs(index));
            Assert.That(check.ArrayLength, Is.SameAs(length));
            Assert.That(block.LastNode, Is.SameAs(check));
        });
    }

    [Test]
    public static void ArrayLengthDispatchRevisitsInsertedAddress()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_REF;
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            var metadata = new GenTreeArrLen(TYP_INT, array, Globals.OFFSETOF__CORINFO_Array__length);
            var owner = compiler.gtNewStoreLclVarNode(1, metadata);
            var block = NewBlock(array, metadata, owner);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            lowering.LowerRange(block, new LIR.ReadOnlyRange(array, metadata));

            Assert.That(owner.Data.Oper, Is.EqualTo(GT_IND));
            Assert.That(owner.Data.AsIndir().Addr.IsContained, Is.True);
            Assert.That(owner.Data.Next, Is.SameAs(owner));
            Assert.That(metadata.Prev is null && metadata.Next is null, Is.True);
        });
    }

    [Test]
    public static void ConditionalBranchDispatchUsesComparisonFlags()
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 7);
            var comparison = new GenTreeOp(GT_EQ, TYP_INT, value, constant);
            var branch = new GenTreeUnOp(GT_JTRUE, TYP_VOID, comparison);
            var block = NewBlock(value, constant, comparison, branch);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            lowering.LowerRange(block, new LIR.ReadOnlyRange(value, branch));

            var replacement = block.LastNode as GenTreeCC
                ?? throw new AssertionException("Branch dispatch did not produce JCC.");
            Assert.That(replacement.Oper, Is.EqualTo(GT_JCC));
            Assert.That(replacement.Condition.Code, Is.EqualTo(GenCondition.EQ));
            Assert.That(comparison.Oper, Is.EqualTo(GT_CMP));
            Assert.That(comparison.Next, Is.SameAs(replacement));
            Assert.That(constant.IsContained, Is.True);
        });
    }

    [Test]
    public static void SelectDispatchReplacesRangeEndAndPreservesOwner()
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewLclvNode(TYP_INT, 0);
            var right = compiler.gtNewIconNode(TYP_INT, 7);
            var comparison = new GenTreeOp(GT_EQ, TYP_INT, left, right);
            var trueValue = compiler.gtNewLclvNode(TYP_INT, 0);
            var falseValue = compiler.gtNewLclvNode(TYP_INT, 1);
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, comparison, trueValue, falseValue);
            var owner = compiler.gtNewStoreLclVarNode(1, select);
            var block = NewBlock(left, right, comparison, trueValue, falseValue, select, owner);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            lowering.LowerRange(block, new LIR.ReadOnlyRange(left, select));

            Assert.That(owner.Data.Oper, Is.EqualTo(GT_SELECTCC));
            Assert.That(owner.Data.AsOpCC().Condition.Code, Is.EqualTo(GenCondition.EQ));
            Assert.That(owner.Data.Next, Is.SameAs(owner));
            Assert.That(comparison.Oper, Is.EqualTo(GT_CMP));
            Assert.That(comparison.Next, Is.SameAs(owner.Data));
            Assert.That(trueValue.IsContained && falseValue.IsContained, Is.True);
        });
    }

    [TestCase(0L, false)]
    [TestCase(long.MinValue, true)]
    public static void IntrinsicDispatchPreservesSignedZeroContainment(long bits, bool contained)
    {
        WithCompiler(compiler => {
            var value = new GenTreeDblCon(TYP_DOUBLE, BitConverter.Int64BitsToDouble(bits));
            var intrinsic = new GenTreeIntrinsic(TYP_DOUBLE, value, NamedIntrinsic.NI_System_Math_Sqrt, null);
            var block = NewBlock(value, intrinsic);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            lowering.LowerRange(block, new LIR.ReadOnlyRange(value, intrinsic));

            Assert.That(value.IsContained, Is.EqualTo(contained));
            Assert.That(value.IsBitwiseEqual(bits), Is.True);
            Assert.That(value.Next, Is.SameAs(intrinsic));
        });
    }

    [TestCase(TYP_DOUBLE, TYP_LONG)]
    [TestCase(TYP_LONG, TYP_DOUBLE)]
    public static void ReturnDispatchInsertsRegisterClassBitcast(var_types sourceType, var_types returnType)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = sourceType;
            var value = compiler.gtNewLclvNode(sourceType, 0);
            var ret = new GenTreeUnOp(GT_RETURN, returnType, value);
            var block = NewBlock(value, ret);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            lowering.LowerRange(block, new LIR.ReadOnlyRange(value, ret));

            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_BITCAST));
            Assert.That(ret.Op1.Type, Is.EqualTo(returnType));
            Assert.That(ret.Op1.AsUnOp().Op1, Is.SameAs(value));
            Assert.That(ret.Op1.Next, Is.SameAs(ret));
            Assert.That(block.LastNode, Is.SameAs(ret));
        });
    }

    [Test]
    public static void VoidReturnDispatchPreservesTerminalNode()
    {
        WithCompiler(compiler => {
            var ret = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            var block = NewBlock(ret);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            lowering.LowerRange(block, new LIR.ReadOnlyRange(ret, ret));

            Assert.That(block.FirstNode, Is.SameAs(ret));
            Assert.That(block.LastNode, Is.SameAs(ret));
        });
    }

    [TestCase(GT_JCC, TYP_VOID)]
    [TestCase(GT_SETCC, TYP_INT)]
    public static void ExistingConditionNodesNeedNoFurtherLowering(genTreeOps oper, var_types type)
    {
        WithCompiler(compiler => {
            var node = new GenTreeCC(oper, type, new GenCondition(GenCondition.EQ));
            node._vnPair.SetBoth(123);
            var block = NewBlock(node);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            lowering.LowerRange(block, new LIR.ReadOnlyRange(node, node));

            Assert.That(block.FirstNode, Is.SameAs(node));
            Assert.That(block.LastNode, Is.SameAs(node));
            Assert.That(node.Condition.Code, Is.EqualTo(GenCondition.EQ));
            Assert.That(node._vnPair.Liberal, Is.EqualTo(123u));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void HighMultiplyDispatchAppliesNativeContainment(bool unsigned)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, 7);
            var multiply = new GenTreeOp(GT_MULHI, TYP_INT, value, constant) { IsUnsigned = unsigned };
            var block = NewBlock(value, constant, multiply);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            lowering.LowerRange(block, new LIR.ReadOnlyRange(value, multiply));

            Assert.That(multiply.Oper, Is.EqualTo(GT_MULHI));
            Assert.That(multiply.IsUnsigned, Is.EqualTo(unsigned));
            Assert.That(value.IsContained, Is.True);
            Assert.That(constant.IsContained, Is.False);
        });
    }

    [TestCase(TYP_INT, 7L, false, false)]
    [TestCase(TYP_INT, 7L, true, true)]
    [TestCase(TYP_LONG, 7L, true, true)]
    [TestCase(TYP_LONG, long.MaxValue, true, false)]
    public static void AtomicAddDiscardsOnlyUnusedResults(
        var_types type, long value, bool unused, bool contained)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[1].Type = type;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var data = compiler.gtNewIconNode(type, (nint)value);
            var add = new GenTreeOp(GT_XADD, type, address, data) { IsUnusedValue = unused };
            add._vnPair.SetBoth(123);
            var flags = add.Flags;
            var block = NewBlock(address, data, add);
            GenTreeLclVar? owner = null;
            if (!unused)
            {
                owner = compiler.gtNewStoreLclVarNode(1, add);
                block.InsertAtEnd(owner);
            }

            var lowering = new Lowering(compiler, new LinearScan(compiler));
            lowering.LowerRange(block, new LIR.ReadOnlyRange(address, add));

            Assert.That(add.Oper, Is.EqualTo(unused ? GT_LOCKADD : GT_XADD));
            Assert.That(add.Type, Is.EqualTo(unused ? TYP_VOID : type));
            Assert.That(add.IsUnusedValue, Is.False);
            Assert.That(add.Flags, Is.EqualTo(flags));
            Assert.That(add._vnPair.Liberal, Is.EqualTo(unused ? ValueNumStore.NoVN : 123u));
            Assert.That(data.Type, Is.EqualTo(type));
            Assert.That(data.IsContained, Is.EqualTo(contained));
            Assert.That(address.Next, Is.SameAs(data));
            Assert.That(data.Next, Is.SameAs(add));
            if (owner is not null)
            {
                Assert.That(owner.Data, Is.SameAs(add));
            }
        });
    }

    [TestCase(GT_DIV, 8, GT_RSH)]
    [TestCase(GT_MOD, 8, GT_SUB)]
    [TestCase(GT_UDIV, 8, GT_RSZ)]
    [TestCase(GT_UMOD, 8, GT_AND)]
    [TestCase(GT_DIV, 0, GT_DIV)]
    [TestCase(GT_UDIV, 0, GT_UDIV)]
    [TestCase(GT_DIV, int.MinValue, GT_EQ)]
    [TestCase(GT_UDIV, -3, GT_GE)]
    public static void DivisionDispatchPreservesResultOwners(genTreeOps oper, int divisor, genTreeOps expected)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var constant = compiler.gtNewIconNode(TYP_INT, divisor);
            var division = new GenTreeOp(oper, TYP_INT, value, constant);
            var owner = compiler.gtNewStoreLclVarNode(1, division);
            var block = NewBlock(value, constant, division, owner);
            var lowering = new Lowering(compiler, new LinearScan(compiler));

            lowering.LowerRange(block, new LIR.ReadOnlyRange(value, division));

            Assert.That(owner.Data.Oper, Is.EqualTo(expected));
            Assert.That(owner.Data.Next, Is.SameAs(owner));
            Assert.That(block.LastNode, Is.SameAs(owner));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AsyncContinuationMovesToItsCallWithoutRevisitingTheRange(bool separated)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[1].Type = TYP_REF;
            var call = new GenTreeCall(TYP_VOID);
            call.SetIsAsync(default);
            var block = NewBlock(call);
            var marker = new GenTree(GT_NO_OP, TYP_VOID);
            if (separated)
            {
                block.InsertAtEnd(marker);
            }
            var continuation = new GenTree(GT_ASYNC_CONTINUATION, TYP_REF);
            var owner = compiler.gtNewStoreLclVarNode(1, continuation);
            block.InsertAtEnd(continuation);
            block.InsertAtEnd(owner);

            var lowering = new Lowering(compiler, new LinearScan(compiler));
            lowering.LowerRange(block, new LIR.ReadOnlyRange(continuation, continuation));

            Assert.That(call.Next, Is.SameAs(continuation));
            Assert.That(continuation.Prev, Is.SameAs(call));
            Assert.That(continuation.Next, Is.SameAs(separated ? marker : owner));
            Assert.That(owner.Data, Is.SameAs(continuation));
            Assert.That(block.LastNode, Is.SameAs(owner));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SuspendReturnRemovesFollowingNodesAndOutgoingFlow(bool hasSuccessor)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_REF;
            var continuation = compiler.gtNewLclvNode(TYP_REF, 0);
            var value = compiler.gtNewIconNode(TYP_INT, 1);
            var suspend = new GenTreeUnOp(GT_RETURN_SUSPEND, TYP_VOID, continuation);
            var other = compiler.gtNewIconNode(TYP_INT, 2);
            var sum = new GenTreeOp(GT_ADD, TYP_INT, value, other);
            var block = NewBlock(continuation, value, suspend, other, sum);
            compiler.compCurBB = block;
            compiler.fgPredsComputed = true;
            compiler.fgPgoConsistent = true;
#if DEBUG
            compiler.fgSafeFlowEdgeCreation = true;
#endif
            var target = NewBlock();
            var exit = NewBlock();
            exit.SetKindAndTargetEdge(BBKinds.BBJ_RETURN, null);
            if (hasSuccessor)
            {
                block.setBBProfileWeight(40);
                target.setBBProfileWeight(100);
                var outgoing = compiler.fgAddRefPred(target, block);
                outgoing.Likelihood = 1;
                block.SetKindAndTargetEdge(BBKinds.BBJ_ALWAYS, outgoing);
                var targetOutgoing = compiler.fgAddRefPred(exit, target);
                targetOutgoing.Likelihood = 1;
                target.SetKindAndTargetEdge(BBKinds.BBJ_ALWAYS, targetOutgoing);
            }
            else
            {
                block.SetKindAndTargetEdge(BBKinds.BBJ_RETURN, null);
            }

            var lowering = new Lowering(compiler, new LinearScan(compiler));
            lowering.LowerRange(block, new LIR.ReadOnlyRange(suspend, sum));

            Assert.That(block.Kind, Is.EqualTo(BBKinds.BBJ_RETURN));
            Assert.That(block.LastNode, Is.SameAs(suspend));
            Assert.That(suspend.Next, Is.Null);
            Assert.That(value.IsUnusedValue, Is.True);
            Assert.That(continuation.IsUnusedValue, Is.False);
            Assert.That(sum.Prev, Is.Null);
            Assert.That(other.Next, Is.Null);
            Assert.That(compiler.fgPgoConsistent, Is.EqualTo(!hasSuccessor));
            if (hasSuccessor)
            {
                Assert.That(target.bbWeight, Is.EqualTo(60));
                Assert.That(target.bbPreds, Is.Null);
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckNode")]
    private static extern void ContainCheckNode(Lowering lowering, GenTree node);

    private static BasicBlock NewBlock(params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
        return block;
    }

    private static void WithCompiler(Action<Compiler> action, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_INT;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
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
