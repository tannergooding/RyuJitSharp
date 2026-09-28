// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64HardwareContainmentTests
{
    private static readonly bool[] s_booleanValues = [false, true];

    [TestCase(NI_AdvSimd_ShiftLeftLogical, 2, 2)]
    [TestCase(NI_AdvSimd_ShiftRightLogicalAdd, 3, 3)]
    [TestCase(NI_AdvSimd_MultiplyBySelectedScalar, 3, 3)]
    [TestCase(NI_AdvSimd_Arm64_FusedMultiplyAddBySelectedScalar, 4, 4)]
    [TestCase(NI_AdvSimd_Insert, 3, 2)]
    [TestCase(NI_AdvSimd_ExtractVector128, 3, 3)]
    [TestCase(NI_Sve_CreateTrueMaskInt32, 1, 1)]
    [TestCase(NI_Sve_Count32BitElements, 1, 1)]
    [TestCase(NI_Sve_FusedMultiplyAddBySelectedScalar, 4, 4)]
    [TestCase(NI_Sha3_XorRotateRight, 3, 3)]
    public static void ImmediateFamiliesContainOnlyTheirConstantOperand(NamedIntrinsic id, int count, int position)
    {
        WithLowering((compiler, lowering, _) => {
            foreach (var constant in s_booleanValues)
            {
                var operands = new GenTree[count];
                for (var index = 0; index < count; index++)
                {
                    operands[index] = index + 1 == position
                        ? constant ? compiler.gtNewIconNode(TYP_INT, 1) : compiler.gtNewLclvNode(TYP_INT, 0)
                        : compiler.gtNewLclvNode(TYP_SIMD16, 1);
                }

                var type = id is NI_Sve_CreateTrueMaskInt32 ? TYP_MASK
                    : id is NI_Sve_Count32BitElements ? TYP_LONG : TYP_SIMD16;
                var node = new GenTreeHWIntrinsic(type, id, TYP_INT, 16, operands);
                ContainCheckNode(lowering, node);
                for (var index = 0; index < count; index++)
                {
                    Assert.That(operands[index].IsContained, Is.EqualTo(constant && (index + 1 == position)));
                }
            }
        });
    }

    [TestCase(NI_Sve_SaturatingIncrementBy32BitElementCount, 3, 2)]
    [TestCase(NI_Sve_MultiplyAddRotateComplexBySelectedScalar, 5, 4)]
    [TestCase(NI_Sve2_DotProductRotateComplexBySelectedIndex, 5, 4)]
    public static void PairedImmediatesAreContainedOnlyTogether(NamedIntrinsic id, int count, int firstPosition)
    {
        WithLowering((compiler, lowering, _) => {
            for (var constants = 0; constants < 4; constants++)
            {
                var operands = new GenTree[count];
                for (var index = 0; index < count; index++)
                {
                    var immediate = index + 1 >= firstPosition;
                    operands[index] = immediate
                        ? (constants & (1 << (index + 1 - firstPosition))) != 0
                            ? compiler.gtNewIconNode(TYP_INT, 1) : compiler.gtNewLclvNode(TYP_INT, 0)
                        : compiler.gtNewLclvNode(TYP_SIMD16, 1);
                }

                var node = new GenTreeHWIntrinsic(TYP_SIMD16, id, TYP_INT, 16, operands);
                ContainCheckNode(lowering, node);
                for (var index = 0; index < count; index++)
                {
                    Assert.That(operands[index].IsContained, Is.EqualTo((constants == 3) && (index + 1 >= firstPosition)));
                }
            }
        });
    }

    [Test]
    public static void InsertSelectedScalarContainsBothIndices()
    {
        WithLowering((compiler, lowering, _) => {
            var firstIndex = compiler.gtNewIconNode(TYP_INT, 0);
            var secondIndex = compiler.gtNewIconNode(TYP_INT, 1);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_Arm64_InsertSelectedScalar, TYP_INT, 16,
                compiler.gtNewLclvNode(TYP_SIMD16, 1), firstIndex, compiler.gtNewLclvNode(TYP_SIMD16, 1), secondIndex);
            ContainCheckNode(lowering, node);
            Assert.That(firstIndex.IsContained, Is.True);
            Assert.That(secondIndex.IsContained, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GatherPrefetchSelectsImmediateByAddressShape(bool vectorAddress)
    {
        WithLowering((compiler, lowering, _) => {
            foreach (var constant in s_booleanValues)
            {
                var mask = new GenTreeMskCon(default);
                var address = compiler.gtNewLclvNode(vectorAddress ? TYP_SIMD16 : TYP_BYREF, 1);
                GenTree immediate = constant ? compiler.gtNewIconNode(TYP_INT, 0) : compiler.gtNewLclvNode(TYP_INT, 0);
                GenTree[] operands = vectorAddress
                    ? [mask, address, immediate]
                    : [mask, address, compiler.gtNewLclvNode(TYP_SIMD16, 1), immediate];
                var node = new GenTreeHWIntrinsic(TYP_VOID, NI_Sve_GatherPrefetch32Bit, TYP_INT, 16, operands);
                ContainCheckNode(lowering, node);
                Assert.That(immediate.IsContained, Is.EqualTo(constant));
                Assert.That(address.IsContained, Is.False);
            }
        });
    }

    [TestCase(NI_AdvSimd_CompareEqual, TYP_INT, true)]
    [TestCase(NI_AdvSimd_CompareEqual, TYP_UINT, true)]
    [TestCase(NI_AdvSimd_CompareGreaterThan, TYP_INT, false)]
    [TestCase(NI_AdvSimd_CompareGreaterThan, TYP_UINT, false)]
    [TestCase(NI_AdvSimd_CompareLessThanOrEqual, TYP_INT, false)]
    [TestCase(NI_AdvSimd_CompareLessThanOrEqual, TYP_UINT, false)]
    [TestCase(NI_AdvSimd_Arm64_CompareGreaterThanScalar, TYP_DOUBLE, false)]
    public static void ZeroComparisonsPreserveSignednessAndOperandOrder(NamedIntrinsic id, var_types baseType, bool equality)
    {
        WithLowering((compiler, lowering, _) => {
            foreach (var zeroOnLeft in s_booleanValues)
            {
                var zero = new GenTreeVecCon(TYP_SIMD16);
                var value = compiler.gtNewLclvNode(TYP_SIMD16, 1);
                var node = new GenTreeHWIntrinsic(TYP_SIMD16, id, baseType, 16,
                    zeroOnLeft ? zero : value, zeroOnLeft ? value : zero);
                ContainCheckNode(lowering, node);
                Assert.That(zero.IsContained, Is.EqualTo(equality || (!zeroOnLeft && !varTypeIsUnsigned(baseType))));
                Assert.That(node.GetOp(equality || !zeroOnLeft ? 2 : 1), Is.SameAs(zero));
                Assert.That(value.IsContained, Is.False);
            }
        });
    }

    [TestCase(TYP_INT, 0x12FFFFL, true)]
    [TestCase(TYP_INT, 0x12345678L, false)]
    [TestCase(TYP_SHORT, 0x1234L, false)]
    [TestCase(TYP_BYTE, 0xFFL, true)]
    [TestCase(TYP_LONG, -1L, true)]
    [TestCase(TYP_LONG, 1L, false)]
    public static void IntegerDuplicatesUseLaneWidth(var_types baseType, long value, bool expected)
    {
        WithLowering((compiler, lowering, _) => {
            var operand = compiler.gtNewIconNode(baseType.ActualType, (nint)value);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_DuplicateToVector128, baseType, 16, operand);
            ContainCheckNode(lowering, node);
            Assert.That(operand.IsContained, Is.EqualTo(expected));
        });
    }

    [TestCase(1.0, true)]
    [TestCase(0.0, false)]
    [TestCase(-0.0, false)]
    [TestCase(1.1, false)]
    [TestCase(double.NaN, false)]
    public static void FloatingScalarCreationUsesExactFmovConstants(double value, bool expected)
    {
        WithLowering((_, lowering, _) => {
            var operand = new GenTreeDblCon(TYP_DOUBLE, value);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_CreateScalarUnsafe, TYP_DOUBLE, 16, operand);
            ContainCheckNode(lowering, node);
            Assert.That(operand.IsContained, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void GetElementContainsOnlyTheRegisterIndex()
    {
        WithLowering((compiler, lowering, block) => {
            var first = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var second = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var vector = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_Add, TYP_INT, 16, first, second);
            var index = compiler.gtNewIconNode(TYP_INT, 2);
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_INT, 16, vector, index);
            Append(block, first, second, vector, index, node);
            ContainCheckNode(lowering, node);
            Assert.That(index.IsContained, Is.True);
            Assert.That(vector.IsContained, Is.False);
        });
    }

    [TestCase(NI_Sve_Add, false, true)]
    [TestCase(NI_Sve_Add, true, true)]
    [TestCase(NI_Sve2_AddPairwise, false, false)]
    [TestCase(NI_Sve2_AddPairwise, true, true)]
    [TestCase(NI_AdvSimd_Add, false, false)]
    public static void ConditionalSelectPreservesEmbeddedAndZeroMergePolicies(
        NamedIntrinsic id, bool trueMask, bool containZero)
    {
        WithLowering((compiler, lowering, block) => {
            simdmask_t bits = default;
            bits.u64[0] = trueMask ? 0x1111UL : 0;
            var mask = new GenTreeMskCon(bits);
            var first = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var second = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var operation = new GenTreeHWIntrinsic(TYP_SIMD16, id, TYP_INT, 16, first, second);
            var zero = new GenTreeVecCon(TYP_SIMD16);
            var select = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_ConditionalSelect, TYP_INT, 16, mask, operation, zero);
            Append(block, mask, first, second, operation, zero, select);
            ContainCheckNode(lowering, select);
            var embedded = id is not NI_AdvSimd_Add;
            Assert.That(operation.IsContained, Is.EqualTo(embedded));
            Assert.That((operation.Flags & GTF_HW_EM_OP) != 0, Is.EqualTo(embedded));
            Assert.That(mask.IsContained, Is.EqualTo(!trueMask));
            Assert.That(zero.IsContained, Is.EqualTo(containZero));

            ContainCheckNode(lowering, select);
            Assert.That(operation.IsContained, Is.EqualTo(embedded));
            Assert.That((operation.Flags & GTF_HW_EM_OP) != 0, Is.EqualTo(embedded));
            Assert.That(zero.IsContained, Is.EqualTo(containZero));
        });
    }

    [TestCase(TYP_INT, true)]
    [TestCase(TYP_LONG, true)]
    [TestCase(TYP_SHORT, false)]
    public static void EmbeddedConversionsAcceptBaseOrAuxiliaryMaskWidth(var_types maskType, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var mask = new GenTreeMskCon(default);
            var value = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var operation = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_ConvertToDouble, TYP_INT, 16, value) {
                AuxiliaryType = TYP_DOUBLE,
            };
            var zero = new GenTreeVecCon(TYP_SIMD16);
            var select = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_ConditionalSelect, maskType, 16, mask, operation, zero);
            Append(block, mask, value, operation, zero, select);
            ContainCheckNode(lowering, select);
            Assert.That(operation.IsContained, Is.EqualTo(expected));
            Assert.That(zero.IsContained, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void EmbeddedShiftContainsItsImmediate()
    {
        WithLowering((compiler, lowering, block) => {
            var mask = new GenTreeMskCon(default);
            var value = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var shift = compiler.gtNewIconNode(TYP_INT, 2);
            var operation = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_ShiftRightArithmeticForDivide, TYP_INT, 16, value, shift);
            var zero = new GenTreeVecCon(TYP_SIMD16);
            var select = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_ConditionalSelect, TYP_INT, 16, mask, operation, zero);
            Append(block, mask, value, shift, operation, zero, select);
            ContainCheckNode(lowering, select);
            Assert.That(operation.IsContained, Is.True);
            Assert.That(shift.IsContained, Is.True);
            Assert.That(zero.IsContained, Is.True);
        });
    }

    [TestCase(1, 2, true, false)]
    [TestCase(4097, 2, false, true)]
    [TestCase(4097, 4097, false, false)]
    public static void BoundsChecksPreferTheIndexImmediate(int indexValue, int lengthValue, bool indexContained, bool lengthContained)
    {
        WithLowering((compiler, lowering, _) => {
            var index = compiler.gtNewIconNode(TYP_INT, indexValue);
            var length = compiler.gtNewIconNode(TYP_INT, lengthValue);
            var check = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL);
            ContainCheckBoundsChk(lowering, check);
            Assert.That(index.IsContained, Is.EqualTo(indexContained));
            Assert.That(length.IsContained, Is.EqualTo(lengthContained));
            Assert.That(index.IsRegOptional || length.IsRegOptional, Is.False);
        });
    }

    [Test]
    public static void BoundsChecksDoNotContainMemoryOrMakeRegistersOptional()
    {
        WithLowering((compiler, lowering, block) => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var length = compiler.gtNewIndir(TYP_INT, address);
            var index = compiler.gtNewLclvNode(TYP_INT, 0);
            var check = new GenTreeBoundsChk(index, length, SCK_RNGCHK_FAIL);
            Append(block, address, length, index, check);
            ContainCheckBoundsChk(lowering, check);
            Assert.That(length.IsContained || length.IsRegOptional, Is.False);
            Assert.That(index.IsContained || index.IsRegOptional, Is.False);
        });
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckNode")]
    private static extern void ContainCheckNode(Lowering lowering, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckBoundsChk")]
    private static extern void ContainCheckBoundsChk(Lowering lowering, GenTreeBoundsChk node);

    private static void WithLowering(Action<Compiler, Lowering, BasicBlock> action)
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
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_SIMD16;
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
