// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.SveMaskPattern;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64HardwareLoweringTests
{
    [SetUp]
    public static void RequireArm64Target()
    {
        Assume.That(Configuration.TARGET_ARM64, "This fixture requires the ARM64 target.");
    }

    [TestCase(TYP_BYTE, SveMaskPatternAll, 0xFFFFUL)]
    [TestCase(TYP_SHORT, SveMaskPatternVectorCount3, 0x15UL)]
    [TestCase(TYP_INT, SveMaskPatternVectorCount8, 0UL)]
    [TestCase(TYP_LONG, SveMaskPatternLargestMultipleOf3, 0UL)]
    public static void MaskPatternsRespectElementWidthsAndUnavailableLanes(
        var_types baseType, SveMaskPattern pattern, ulong expected)
    {
        simdmask_t mask = default;
        Assert.That(EvaluateSimdPatternToMask<simd16_t>(baseType, ref mask, pattern), Is.True);
        Assert.That(mask.u64[0], Is.EqualTo(expected));
    }

    [TestCase(TYP_BYTE, SveMaskPatternAll, 16)]
    [TestCase(TYP_SHORT, SveMaskPatternVectorCount3, 3)]
    [TestCase(TYP_INT, SveMaskPatternVectorCount8, 4)]
    [TestCase(TYP_LONG, SveMaskPatternLargestMultipleOf3, 0)]
    public static void VectorPatternsRespectElementWidthsAndUnavailableLanes(
        var_types baseType, SveMaskPattern pattern, int expectedSetLanes)
    {
        Assume.That(Configuration.FEATURE_MASKED_HW_INTRINSICS, "Masked hardware intrinsics are unavailable.");

        simd16_t vector = default;
        var result = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref vector, 1));

        Assert.That(EvaluateSimdPatternToVector(baseType, ref vector, pattern), Is.True);

        var initializedBytes = expectedSetLanes * baseType.Size;
        Assert.That(result[..initializedBytes].ToArray(),
            Is.All.EqualTo(byte.MaxValue));
        Assert.That(result[initializedBytes..].ToArray(),
            Is.All.EqualTo((byte)0));
    }

    [Test]
    public static void UnsupportedVectorPatternLeavesResultUnchanged()
    {
        Assume.That(Configuration.FEATURE_MASKED_HW_INTRINSICS, "Masked hardware intrinsics are unavailable.");

        simd16_t vector = default;
        var result = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref vector, 1));
        result.Fill(0xA5);
        var original = result.ToArray();

        Assert.That(EvaluateSimdPatternToVector(TYP_BYTE, ref vector, SveMaskPatternNone), Is.False);
        Assert.That(result.ToArray(), Is.EqualTo(original));
    }

    [TestCase(0, NI_Vector_Create, NI_AdvSimd_DuplicateToVector128)]
    [TestCase(1, NI_Vector_CreateScalar, NI_AdvSimd_Insert)]
    public static void VectorCreateUsesNativeBroadcastOrZeroingInsert(
        int scalar, NamedIntrinsic id, NamedIntrinsic result)
    {
        WithLowering((compiler, lowering, block) => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var create = new GenTreeHWIntrinsic(TYP_SIMD16, id, TYP_INT, 16, value) {
                IsUnusedValue = true,
            };
            Append(block, value, create);

            _ = LowerNode(lowering, create);

            Assert.That(create.HWIntrinsicId, Is.EqualTo(result));
            if (scalar != 0)
            {
                Assert.That(create.GetOp(2).IsIntegralConst(0), Is.True);
                Assert.That(create.GetOp(1).IsVectorZero, Is.True);
            }
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void PartiallyConstantVectorCreateSeedsAdvSimdInsertsFromVectorConstant()
    {
        WithLowering((compiler, lowering, block) => {
            compiler.lvaTable[1].Type = TYP_INT;
            var first = compiler.gtNewIconNode(TYP_INT, 1);
            var second = compiler.gtNewIconNode(TYP_INT, 2);
            var third = compiler.gtNewLclvNode(TYP_INT, 0);
            var fourth = compiler.gtNewLclvNode(TYP_INT, 1);
            var create = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_INT, 16,
                first, second, third, fourth);
            var user = new GenTreeUnOp(GT_NEG, TYP_SIMD16, create) { IsUnusedValue = true };
            Append(block, first, second, third, fourth, create, user);

            Assert.That(LowerNode(lowering, create), Is.SameAs(user));

            GenTreeVecCon? constantVector = null;
            var insertCount = 0;
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                if (node is GenTreeVecCon vector)
                {
                    constantVector = vector;
                }
                else if ((node is GenTreeHWIntrinsic intrinsicNode) &&
                    (intrinsicNode.HWIntrinsicId is NI_AdvSimd_Insert))
                {
                    insertCount++;
                }
            }

            Assert.That(constantVector, Is.Not.Null);
            Assert.That(constantVector!.SimdVal.i32[0], Is.EqualTo(1));
            Assert.That(constantVector.SimdVal.i32[1], Is.EqualTo(2));
            Assert.That(constantVector.SimdVal.i32[2], Is.Zero);
            Assert.That(constantVector.SimdVal.i32[3], Is.Zero);
            Assert.That(insertCount, Is.EqualTo(2));
            Assert.That(user.Op1, Is.TypeOf<GenTreeHWIntrinsic>());
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void PartiallyConstantFloatVectorCreatePreservesNegativeZeroAndZeroLanes()
    {
        WithLowering((compiler, lowering, block) => {
            var first = new GenTreeDblCon(TYP_FLOAT, -0.0);
            var second = new GenTreeDblCon(TYP_FLOAT, 1.0);
            var third = compiler.gtNewLclvNode(TYP_FLOAT, 0);
            var fourth = new GenTreeDblCon(TYP_FLOAT, 0.0);
            var create = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_FLOAT, 16,
                first, second, third, fourth);
            var user = new GenTreeUnOp(GT_NEG, TYP_SIMD16, create) { IsUnusedValue = true };
            Append(block, first, second, third, fourth, create, user);

            Assert.That(LowerNode(lowering, create), Is.SameAs(user));

            GenTreeVecCon? constantVector = null;
            var insertCount = 0;
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                if (node is GenTreeVecCon vector)
                {
                    constantVector = vector;
                }
                else if ((node is GenTreeHWIntrinsic intrinsicNode) &&
                    (intrinsicNode.HWIntrinsicId is NI_AdvSimd_Insert))
                {
                    insertCount++;
                }
            }

            Assert.That(constantVector, Is.Not.Null);
            Assert.That(constantVector!.SimdVal.u32[0], Is.EqualTo(0x80000000U));
            Assert.That(constantVector.SimdVal.u32[1], Is.EqualTo(0x3F800000U));
            Assert.That(constantVector.SimdVal.u32[2], Is.Zero);
            Assert.That(constantVector.SimdVal.u32[3], Is.Zero);
            Assert.That(insertCount, Is.EqualTo(1));
            Assert.That(user.Op1, Is.TypeOf<GenTreeHWIntrinsic>());
            Assert.That(block.TryGetUse(third, out var use), Is.True);
            Assert.That(use.Def(), Is.SameAs(third));
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void NonEncodableMaskBecomesVectorAndPredicateConversion()
    {
        WithLowering((compiler, lowering, block) => {
            simdmask_t bits = default;
            bits.u64[0] = 0x09;
            var mask = new GenTreeMskCon(bits) { IsUnusedValue = true };
            block.InsertAtEnd(mask);

            var next = LowerNode(lowering, mask);

            Assert.That(mask.Next, Is.Null);
            Assert.That(block.FirstNode, Is.TypeOf<GenTreeVecCon>());
            Assert.That(next, Is.SameAs(block.FirstNode!.Next));
            Assert.That(block.LastNode, Is.TypeOf<GenTreeHWIntrinsic>());
            var conversion = block.LastNode!.AsHWIntrinsic();
            Assert.That(conversion.HWIntrinsicId, Is.EqualTo(NI_Sve_ConvertVectorToMask));
            Assert.That(conversion.GetOp(1).AsHWIntrinsic().HWIntrinsicId,
                Is.EqualTo(NI_Sve_ConversionTrueMask));
            Assert.That(conversion.IsUnusedValue, Is.True);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SvePredicateBitwiseClearLowersMaskAndZeroOperands(bool useZeroOperand)
    {
        Assume.That(Configuration.FEATURE_MASKED_HW_INTRINSICS, "Masked hardware intrinsics are unavailable.");

        WithLowering((compiler, lowering, block) => {
            var first = useZeroOperand
                ? (GenTree)new GenTreeVecCon(TYP_SIMD16)
                : compiler.gtNewSimdTrueMaskNode(TYP_INT);
            var secondMask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, false);
            var secondVector = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, secondMask, TYP_INT, 16);
            var not = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_Not, TYP_INT, 16, secondVector);
            var and = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_And, TYP_INT, 16, first, not);
            var user = new GenTreeUnOp(GT_NEG, TYP_SIMD16, and) { IsUnusedValue = true };
            Append(block, first, secondMask, secondVector, not, and, user);

            _ = LowerNode(lowering, and);

            Assert.That(and.HWIntrinsicId, Is.EqualTo(NI_Sve_BitwiseClear_Predicates));
            Assert.That(and.Type, Is.EqualTo(TYP_MASK));
            Assert.That(varTypeIsMask(and.GetOp(1).Type), Is.True);
            Assert.That(and.GetOp(2), Is.SameAs(secondMask));
            var loweredMask = and.GetOp(1).AsMskCon();
            Assert.That(useZeroOperand ? loweredMask.IsZero : loweredMask.IsTrue(TYP_INT), Is.True);
            var convertToVector = user.Op1.AsHWIntrinsic();
            Assert.That(convertToVector.HWIntrinsicId, Is.EqualTo(NI_Sve_ConvertMaskToVector));
            Assert.That(convertToVector.GetOp(1), Is.SameAs(and));
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void SvePredicateBitwiseClearKeepsSameWidthMaskResult()
    {
        Assume.That(Configuration.FEATURE_MASKED_HW_INTRINSICS, "Masked hardware intrinsics are unavailable.");

        WithLowering((compiler, lowering, block) => {
            var firstMask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, true);
            var secondMask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, false);
            var first = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, firstMask, TYP_INT, 16);
            var second = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, secondMask, TYP_INT, 16);
            var not = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_Not, TYP_INT, 16, second);
            var and = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_And, TYP_INT, 16, first, not);
            var trueMask = compiler.gtNewSimdTrueMaskNode(TYP_INT);
            var toMask = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_ConvertVectorToMask, TYP_INT, 16, trueMask, and)
            {
                IsUnusedValue = true,
            };
            Append(block, firstMask, secondMask, first, second, not, and, trueMask, toMask);

            _ = LowerNode(lowering, and);

            Assert.That(and.HWIntrinsicId, Is.EqualTo(NI_Sve_BitwiseClear_Predicates));
            Assert.That(and.Type, Is.EqualTo(TYP_MASK));
            Assert.That(toMask.GetOp(2), Is.SameAs(and));
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void SvePredicateBitwiseClearPreservesVectorWhenMaskElementWidthsDiffer()
    {
        Assume.That(Configuration.FEATURE_MASKED_HW_INTRINSICS, "Masked hardware intrinsics are unavailable.");

        WithLowering((compiler, lowering, block) => {
            var firstMask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, true);
            var secondMask = compiler.gtNewMskConNode(TYP_MASK, TYP_BYTE, false);
            var first = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, firstMask, TYP_INT, 16);
            var second = compiler.gtNewSimdCvtMaskToVectorNode(TYP_SIMD16, secondMask, TYP_BYTE, 16);
            var not = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_Not, TYP_BYTE, 16, second);
            var and = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_And, TYP_INT, 16, first, not)
            {
                IsUnusedValue = true,
            };
            Append(block, firstMask, secondMask, first, second, not, and);

            _ = LowerNode(lowering, and);

            Assert.That(and.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_BitwiseClear));
            Assert.That(and.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(and.GetOp(1), Is.SameAs(first));
            Assert.That(and.GetOp(2), Is.SameAs(second));
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void SveVectorToMaskOfMaskLowersToItsPredicateOperand()
    {
        Assume.That(Configuration.FEATURE_MASKED_HW_INTRINSICS, "Masked hardware intrinsics are unavailable.");

        WithLowering((compiler, lowering, block) => {
            var trueMask = compiler.gtNewSimdTrueMaskNode(TYP_INT);
            var mask = compiler.gtNewMskConNode(TYP_MASK, TYP_INT, false);
            var conversion = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_ConvertVectorToMask, TYP_INT, 16,
                trueMask, mask)
            {
                IsUnusedValue = true,
            };
            Append(block, trueMask, mask, conversion);

            _ = LowerNode(lowering, conversion);

            Assert.That(conversion.Next, Is.Null);
            Assert.That(block.LastNode, Is.SameAs(mask));
            Assert.That(mask.IsUnusedValue, Is.True);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void EmbeddedMaskOperationWrapsItsLiveResult()
    {
        WithLowering((compiler, lowering, block) => {
            var first = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var operation = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_Abs, TYP_INT, 16, first);
            var user = new GenTreeUnOp(GT_NEG, TYP_SIMD16, operation) { IsUnusedValue = true };
            Append(block, first, operation, user);

            _ = LowerNode(lowering, operation);

            var select = user.Op1.AsHWIntrinsic();
            Assert.That(select.HWIntrinsicId, Is.EqualTo(NI_Sve_ConditionalSelect));
            Assert.That(select.GetOp(2), Is.SameAs(operation));
            Assert.That(select.GetOp(1).IsTrueMask(TYP_INT), Is.True);
            Assert.That(select.GetOp(3).IsVectorZero, Is.True);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void TrueMaskSelectPreservesItsLiveValue()
    {
        WithLowering((compiler, lowering, block) => {
            var mask = compiler.gtNewSimdTrueMaskNode(TYP_INT);
            var value = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var zero = new GenTreeVecCon(TYP_SIMD16);
            var select = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Sve_ConditionalSelect, TYP_INT, 16,
                mask, value, zero);
            var user = new GenTreeUnOp(GT_NEG, TYP_SIMD16, select) { IsUnusedValue = true };
            Append(block, mask, value, zero, select, user);

            Assert.That(LowerNode(lowering, select), Is.SameAs(user));
            Assert.That(user.Op1, Is.SameAs(value));
            Assert.That(zero.IsUnusedValue, Is.True);
            Assert.That(select.Next, Is.Null);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void VectorEqualityAgainstZeroUsesPairwiseReductionAndFlags()
    {
        WithLowering((compiler, lowering, block) => {
            var value = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var zero = new GenTreeVecCon(TYP_SIMD16);
            var equality = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_op_Equality, TYP_INT, 16, value, zero);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, equality) { IsUnusedValue = true };
            Append(block, value, zero, equality, user);

            _ = LowerNode(lowering, equality);

            Assert.That(equality.Next, Is.Null);
            Assert.That(user.Op1.Oper, Is.EqualTo(GT_SETCC));
            Assert.That(zero.Next, Is.Null);
            Assert.That(block.FirstNode, Is.InstanceOf<GenTreeLclVarCommon>());
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void IntegerDotProductReducesVector64WithPairwiseAdd()
    {
        WithLowering((compiler, lowering, block) => {
            var first = new GenTreeVecCon(TYP_SIMD8);
            var second = new GenTreeVecCon(TYP_SIMD8);
            var dot = new GenTreeHWIntrinsic(TYP_SIMD8, NI_Vector_Dot, TYP_INT, 8, first, second);
            var user = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_ToScalar, TYP_INT, 8, dot) {
                IsUnusedValue = true,
            };
            Append(block, first, second, dot, user);

            _ = LowerNode(lowering, dot);

            Assert.That(dot.Next, Is.Null);
            Assert.That(user.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_AdvSimd_AddPairwise));
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void ScalarFmaFoldsNegationIntoIntrinsicId()
    {
        WithLowering((compiler, lowering, block) => {
            var scalar = new GenTreeDblCon(TYP_FLOAT, 1.0);
            var negation = new GenTreeUnOp(GT_NEG, TYP_FLOAT, scalar);
            var duplicate = new GenTreeHWIntrinsic(TYP_SIMD8, NI_Vector_CreateScalarUnsafe,
                TYP_FLOAT, 8, negation);
            var second = new GenTreeVecCon(TYP_SIMD8);
            var third = new GenTreeVecCon(TYP_SIMD8);
            var fma = new GenTreeHWIntrinsic(TYP_SIMD8, NI_AdvSimd_FusedMultiplyAddScalar,
                TYP_FLOAT, 8, duplicate, second, third) { IsUnusedValue = true };
            Append(block, scalar, negation, duplicate, second, third, fma);

            _ = LowerNode(lowering, duplicate);
            _ = LowerNode(lowering, fma);

            Assert.That(fma.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_FusedMultiplySubtractNegatedScalar));
            Assert.That(duplicate.GetOp(1), Is.SameAs(scalar));
            Assert.That(negation.Next, Is.Null);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void FirstFaultRegisterStoreAndReadShareTheImplicitLocal()
    {
        WithLowering((compiler, lowering, block) => {
            var mask = new GenTreeMskCon(default);
            var set = new GenTreeHWIntrinsic(TYP_VOID, NI_Sve_SetFfr, TYP_BYTE, 16, mask);
            var get = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_GetFfrByte, TYP_BYTE, 16);
            var user = new GenTreeUnOp(GT_NEG, TYP_MASK, get) { IsUnusedValue = true };
            Append(block, mask, set, get, user);

            _ = LowerNode(lowering, set);
            _ = LowerNode(lowering, get);

            var local = compiler.lvaFfrRegister;
            Assert.That(local, Is.Not.EqualTo(BAD_VAR_NUM));
            Assert.That(compiler.lvaGetDesc(local).Type, Is.EqualTo(TYP_MASK));
            Assert.That(set.Next!.Oper, Is.EqualTo(GT_PHYSREG));
            Assert.That(set.Next.Next!.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(set.Next.Next.AsLclVarCommon().LclNum, Is.EqualTo(local));
            Assert.That(user.Op1.AsLclVar().LclNum, Is.EqualTo(local));
            Assert.That(get.Next, Is.Null);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [Test]
    public static void AtomicAddKeepsItsResultAndContainsItsImmediate()
    {
        WithLowering((compiler, lowering, block) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            var value = compiler.gtNewIconNode(TYP_INT, 1);
            var atomic = new GenTreeIndir(GT_XADD, TYP_INT, address, value) { IsUnusedValue = true };
            Append(block, address, value, atomic);

            Assert.That(LowerNode(lowering, atomic), Is.Null);
            Assert.That(atomic.Oper, Is.EqualTo(GT_XADD));
            Assert.That(atomic.IsUnusedValue, Is.True);
            Assert.That(value.IsContained, Is.True);
            if (TestConfiguration.IsDebug)
            {
                Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            }
        });
    }

    [TestCase(GT_BSWAP)]
    [TestCase(GT_BSWAP16)]
    public static void ByteSwapNodesRemainUnchangedOnArm64(genTreeOps operation)
    {
        WithLowering((compiler, lowering, block) =>
        {
            var operand = compiler.gtNewLclvNode(TYP_INT, 0);
            var node = compiler.gtNewUnaryNode(operation, TYP_INT, operand);
            Append(block, operand, node);

            Assert.That(LowerNode(lowering, node), Is.Null);
            Assert.That(node.Oper, Is.EqualTo(operation));
            Assert.That(node.AsUnOp().Op1, Is.SameAs(operand));
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);

    private static void WithLowering(Action<Compiler, Lowering, BasicBlock> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.SetMinOpts(false);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_SIMD16;
        compiler.lvaFfrRegister = BAD_VAR_NUM;
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
