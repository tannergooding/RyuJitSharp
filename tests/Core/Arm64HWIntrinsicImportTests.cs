// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64HWIntrinsicImportTests
{
    private static readonly int[] s_argumentQueriesOne = [1];
    private static readonly int[] s_argumentQueriesOneThenZero = [1, 0];
    private static readonly int[] s_argumentQueriesZeroThenOne = [0, 1];
    private static readonly int[] s_argumentQueriesTwoThenOneThenZero = [2, 1, 0];
    private static readonly int[] s_argumentQueriesFourThroughZero = [4, 3, 2, 1, 0];

    [TestCase(NI_Sve_Compute8BitAddresses, 0)]
    [TestCase(NI_Sve_Compute16BitAddresses, 1)]
    [TestCase(NI_Sve_Compute32BitAddresses, 2)]
    [TestCase(NI_Sve_Compute64BitAddresses, 3)]
    public static void AddressScaleImmediatesPreserveTheFourElementWidths(NamedIntrinsic intrinsic, int scale)
    {
        Assert.That(HWIntrinsicInfo.lookupIval(intrinsic), Is.EqualTo(scale));
    }

    [Test]
    public static void UnknownImplicitImmediateTerminatesRatherThanInventingAScale()
    {
        var error = Assert.Throws<FatalJitException>(() => HWIntrinsicInfo.lookupIval(NI_AdvSimd_Add));
        Assert.That(error?.Result, Is.EqualTo(CorJitResult.CORJIT_RECOVERABLEERROR));
    }

    [TestCase(NI_AdvSimd_Insert, 3, 1, -1)]
    [TestCase(NI_AdvSimd_Arm64_InsertSelectedScalar, 4, 2, 0)]
    [TestCase(NI_Sve_SaturatingIncrementBy16BitElementCountScalar, 3, 1, 0)]
    [TestCase(NI_Sve_MultiplyAddRotateComplexBySelectedScalar, 5, 0, 1)]
    [TestCase(NI_AdvSimd_ShiftRightLogical, 2, 0, -1)]
    public static void ImmediateDiscoveryUsesTopRelativePositionsWithoutConsumingArguments(
        NamedIntrinsic intrinsic, int count, int firstPosition, int secondPosition)
    {
        WithImporter(compiler =>
        {
            var values = new GenTree[count];
            for (var index = 0; index < count; index++)
            {
                values[index] = compiler.gtNewIconNode(TYP_INT, index + 1);
            }
            Push(compiler, values);
            GenTree? first = new GenTreeLclVar(TYP_INT, 2);
            GenTree? second = new GenTreeLclVar(TYP_INT, 3);
            var unchangedSecond = second;
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = checked((ushort)count);

            ImmediateOperands(compiler, intrinsic, in sig, ref first, ref second);

            Assert.That(first, Is.SameAs(values[count - firstPosition - 1]));
            Assert.That(second, Is.SameAs(secondPosition < 0 ? unchangedSecond : values[count - secondPosition - 1]));
            Assert.That(compiler.impStackHeight, Is.EqualTo(count));
        });
    }

    [Test]
    public static void NonImmediateDiscoveryPreservesBothOutputSlotsAndTheStack()
    {
        WithImporter(compiler =>
        {
            var value = new GenTreeLclVar(TYP_SIMD16, 0);
            Push(compiler, value);
            GenTree? first = value;
            GenTree? second = new GenTreeLclVar(TYP_INT, 1);
            var initialSecond = second;
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 1;

            ImmediateOperands(compiler, NI_AdvSimd_Add, in sig, ref first, ref second);

            Assert.That(first, Is.SameAs(value));
            Assert.That(second, Is.SameAs(initialSecond));
            Assert.That(compiler.impStackHeight, Is.EqualTo(1));
        });
    }

    [TestCase(NI_AdvSimd_ShiftLeftLogical, NI_AdvSimd_ShiftLogical, false, TYP_SIMD8, TYP_UINT)]
    [TestCase(NI_AdvSimd_ShiftLeftLogicalScalar, NI_AdvSimd_ShiftLogicalScalar, false, TYP_SIMD8, TYP_ULONG)]
    [TestCase(NI_AdvSimd_ShiftRightLogical, NI_AdvSimd_ShiftLogical, true, TYP_SIMD16, TYP_UINT)]
    [TestCase(NI_AdvSimd_ShiftRightLogicalScalar, NI_AdvSimd_ShiftLogicalScalar, true, TYP_SIMD8, TYP_ULONG)]
    [TestCase(NI_AdvSimd_ShiftRightArithmetic, NI_AdvSimd_ShiftArithmetic, true, TYP_SIMD16, TYP_INT)]
    [TestCase(NI_AdvSimd_ShiftRightArithmeticScalar, NI_AdvSimd_ShiftArithmeticScalar, true, TYP_SIMD8, TYP_LONG)]
    public static void NonconstantShiftCountsKeepDirectionScalarDispatchAndOperandIdentity(
        NamedIntrinsic intrinsic, NamedIntrinsic fallback, bool negate, var_types vectorType, var_types baseType)
    {
        WithImporter(compiler =>
        {
            var vector = new GenTreeLclVar(vectorType, 0);
            var count = new GenTreeLclVar(TYP_INT, 1);
            compiler.lvaTable[0].Type = vectorType;
            compiler.lvaTable[1].Type = TYP_INT;
            Push(compiler, vector, count);

            var node = Fallback(compiler, intrinsic, vectorType, baseType) ??
                throw new AssertionException("Missing genuine shift fallback.");

            Assert.That(node.HWIntrinsicId, Is.EqualTo(fallback));
            Assert.That(node.Type, Is.EqualTo(vectorType));
            Assert.That(node.SimdBaseType, Is.EqualTo(baseType));
            Assert.That(node.SimdSize, Is.EqualTo(vectorType.Size));
            Assert.That(node.GetOp(1), Is.SameAs(vector));
            var broadcast = node.GetOp(2).AsHWIntrinsic();
            Assert.That(broadcast.HWIntrinsicId, Is.EqualTo(NI_Vector_Create));
            var shiftCount = broadcast.GetOp(1);
            if (negate)
            {
                Assert.That(shiftCount.Oper, Is.EqualTo(GT_NEG));
                Assert.That(shiftCount.AsUnOp().Op1, Is.SameAs(count));
            }
            else
            {
                Assert.That(shiftCount, Is.SameAs(count));
            }
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void UnsupportedFallbackLeavesTheArgumentStackUntouched()
    {
        WithImporter(compiler =>
        {
            Push(compiler, new GenTreeLclVar(TYP_SIMD16, 0), new GenTreeLclVar(TYP_INT, 1));
            Assert.That(Fallback(compiler, NI_AdvSimd_Add, TYP_SIMD16, TYP_INT), Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(2));
        });
    }

    [TestCase(NI_AdvSimd_BitwiseClear, TYP_SIMD8, GT_AND)]
    [TestCase(NI_AdvSimd_BitwiseClear, TYP_SIMD16, GT_AND)]
    [TestCase(NI_Sve_BitwiseClear, TYP_SIMD16, GT_AND)]
    [TestCase(NI_AdvSimd_OrNot, TYP_SIMD8, GT_OR)]
    [TestCase(NI_AdvSimd_OrNot, TYP_SIMD16, GT_OR)]
    public static void BinaryComplementImportNegatesTheSecondOperandBeforeLowering(
        NamedIntrinsic intrinsic, var_types vectorType, genTreeOps operation)
    {
        WithImporter(compiler =>
        {
            var first = new GenTreeLclVar(vectorType, 0);
            var second = new GenTreeLclVar(vectorType, 1);
            compiler.lvaTable[0].Type = vectorType;
            compiler.lvaTable[1].Type = vectorType;
            Push(compiler, first, second);
            var sig = Signature(2);
            var node = RequireIntrinsic(Import(compiler, intrinsic, in sig, TYP_INT, vectorType,
                checked((byte)vectorType.Size)));

            Assert.That(node.GetOp(1), Is.SameAs(first));
            var complement = node.GetOp(2).AsHWIntrinsic();
            Assert.That(complement.GetOp(1), Is.SameAs(second));
            Assert.That(complement.HWIntrinsicId, Is.EqualTo(NI_AdvSimd_Not));
            Assert.That(node.HWIntrinsicId, Is.EqualTo(operation == GT_AND ? NI_AdvSimd_And : NI_AdvSimd_Or));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void YieldImportsVoidWithoutTouchingArguments()
    {
        WithImporter(compiler =>
        {
            var sig = Signature(0);
            sig.retType = CORINFO_TYPE_VOID;
            var node = RequireIntrinsic(Import(compiler, NI_ArmBase_Yield, in sig, TYP_UNDEF, TYP_VOID, 0));

            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_ArmBase_Yield));
            Assert.That(node.Type, Is.EqualTo(TYP_VOID));
            Assert.That(node.Operands.Length, Is.Zero);
        });
    }

    [TestCase(NI_AdvSimd_LoadVector64, TYP_SIMD8)]
    [TestCase(NI_AdvSimd_LoadVector128, TYP_SIMD16)]
    public static void VectorLoadsRemoveOnlyTheCastHidingAByref(NamedIntrinsic intrinsic, var_types vectorType)
    {
        WithImporter(compiler =>
        {
            var address = new GenTreeLclVar(TYP_BYREF, 2);
            var cast = new GenTreeCast(TYP_I_IMPL, address, false, TYP_I_IMPL);
            Push(compiler, cast);
            var sig = Signature(1);
            var node = Import(compiler, intrinsic, in sig, TYP_INT, vectorType, checked((byte)vectorType.Size)) ??
                throw new AssertionException("Missing vector load.");

            Assert.That(node.Oper, Is.EqualTo(GT_IND));
            Assert.That(node.AsIndir().Addr, Is.SameAs(address));
            Assert.That(node.Type, Is.EqualTo(vectorType));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void StoreRecomputesVector128WidthEvenForTheAdvSimdOverload()
    {
        WithImporter(compiler =>
        {
            s_argumentTypes[0] = CORINFO_TYPE_NATIVEINT;
            s_argumentTypes[1] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[1] = s_vectorClass;
            var address = new GenTreeLclVar(TYP_BYREF, 2);
            var cast = new GenTreeCast(TYP_I_IMPL, address, false, TYP_I_IMPL);
            var vector = new GenTreeLclVar(TYP_SIMD16, 0);
            Push(compiler, cast, vector);
            var sig = Signature(2);
            var node = Import(compiler, NI_AdvSimd_Store, in sig, TYP_INT, TYP_VOID, 8) ??
                throw new AssertionException("Missing vector store.");

            Assert.That(node.Oper, Is.EqualTo(GT_STOREIND));
            Assert.That(node.AsStoreInd().Addr, Is.SameAs(address));
            Assert.That(node.AsStoreInd().Data, Is.SameAs(vector));
            Assert.That(s_argumentQueries, Is.EqualTo(s_argumentQueriesOne));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_AdvSimd_StoreSelectedScalar, 3, 0)]
    [TestCase(NI_AdvSimd_Arm64_StoreSelectedScalar, 3, 0)]
    [TestCase(NI_AdvSimd_LoadAndInsertScalarVector64x2, 3, 1)]
    [TestCase(NI_AdvSimd_LoadAndInsertScalarVector64x3, 3, 1)]
    [TestCase(NI_AdvSimd_Arm64_LoadAndInsertScalarVector128x4, 3, 1)]
    public static void NonconstantFieldListFormsDeclineBeforeEeQueriesAndBeforePopping(
        NamedIntrinsic intrinsic, int count, int immediatePosition)
    {
        WithImporter(compiler =>
        {
            var operands = new GenTree[count];
            for (var index = 0; index < count; index++)
            {
                operands[index] = new GenTreeLclVar(TYP_INT, index);
            }
            operands[count - immediatePosition - 1] = new GenTreeLclVar(TYP_INT, 1);
            if (immediatePosition == 0)
            {
                operands[1] = new GenTreeLclVar(TYP_STRUCT, 0);
            }
            Push(compiler, operands);
            var sig = Signature(count);

            Assert.That(Import(compiler, intrinsic, in sig, TYP_INT, TYP_VOID, 8, mustExpand: false), Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(count));
            Assert.That(s_argumentQueries, Is.Empty);
            Assert.That(compiler.info.compNeedsConsecutiveRegisters, Is.False);
        });
    }

    [TestCase(NI_Sve_CreateFalseMaskByte, TYP_UBYTE)]
    [TestCase(NI_Sve_CreateFalseMaskDouble, TYP_DOUBLE)]
    [TestCase(NI_Sve_CreateFalseMaskInt64, TYP_LONG)]
    public static void FalseMasksRemainConstantsOfTheirReturnType(NamedIntrinsic intrinsic, var_types baseType)
    {
        WithImporter(compiler =>
        {
            var sig = Signature(0);
            var node = Import(compiler, intrinsic, in sig, baseType, TYP_MASK, 16) ??
                throw new AssertionException("Missing false predicate.");
            Assert.That(node, Is.TypeOf<GenTreeMskCon>());
            Assert.That(node.AsMskCon().IsZero, Is.True);
            Assert.That(node.Type, Is.EqualTo(TYP_MASK));
        });
    }

    [TestCase(TYP_BYTE)]
    [TestCase(TYP_INT)]
    [TestCase(TYP_DOUBLE)]
    public static void ScalableFalseMasksKeepTheirElementTypeAndRepeatedEncoding(var_types baseType)
    {
        WithImporter(compiler =>
        {
            var sig = Signature(0);
            var intrinsic = baseType switch
            {
                TYP_BYTE => NI_Sve_CreateFalseMaskSByte,
                TYP_INT => NI_Sve_CreateFalseMaskInt32,
                TYP_DOUBLE => NI_Sve_CreateFalseMaskDouble,
                _ => throw new AssertionException("Unexpected scalable mask fixture type."),
            };
            var node = Import(compiler, intrinsic, in sig, baseType, TYP_SIMD, 0) ??
                throw new AssertionException("Missing scalable false vector.");

            Assert.That(node.Type, Is.EqualTo(TYP_SIMD));
            Assert.That(node.AsVecCon().SimdScalableVal.BaseType, Is.EqualTo(baseType));
            Assert.That(node.AsVecCon().SimdScalableVal.Kind, Is.EqualTo(SimdScalableKind.SimdScalableRepeated));
            Assert.That(node.AsVecCon().SimdScalableVal.Index.u64[0], Is.Zero);
        });
    }

#if !DEBUG
    [TestCase(0)]
    [TestCase(31)]
    public static void ReleaseScalableTrueMaskBranchKeepsTheNativeRepeatedConstant(int pattern)
    {
        WithImporter(compiler =>
        {
            Push(compiler, compiler.gtNewIconNode(TYP_INT, pattern));
            var sig = Signature(1);
            var node = Import(compiler, NI_Sve_CreateTrueMaskInt32, in sig, TYP_INT, TYP_SIMD, 0) ??
                throw new AssertionException("Missing scalable true vector.");

            Assert.That(node.Type, Is.EqualTo(TYP_SIMD));
            Assert.That(node.AsVecCon().SimdScalableVal.BaseType, Is.EqualTo(TYP_INT));
            Assert.That(node.AsVecCon().SimdScalableVal.Kind, Is.EqualTo(SimdScalableKind.SimdScalableRepeated));
            Assert.That(node.AsVecCon().SimdScalableVal.Index.u64[0], Is.EqualTo(0xffffffffUL));
        });
    }
#endif

    [TestCase(TYP_UBYTE, 31, 0xffffUL)]
    [TestCase(TYP_INT, 31, 0x1111UL)]
    [TestCase(TYP_LONG, 31, 0x101UL)]
    [TestCase(TYP_INT, 2, 0x11UL)]
    [TestCase(TYP_INT, 9, 0UL)]
    public static void TruePatternsPreserveElementSpacingAndOversizedAllFalseBehavior(
        var_types baseType, int pattern, ulong expected)
    {
        WithImporter(compiler =>
        {
            Push(compiler, compiler.gtNewIconNode(TYP_INT, pattern));
            var sig = Signature(1);
            var intrinsic = baseType switch
            {
                TYP_UBYTE => NI_Sve_CreateTrueMaskByte,
                TYP_LONG => NI_Sve_CreateTrueMaskInt64,
                TYP_INT => NI_Sve_CreateTrueMaskInt32,
                _ => throw new AssertionException("Unexpected fixed mask fixture type."),
            };
            var node = Import(compiler, intrinsic, in sig, baseType, TYP_MASK, 16) ??
                throw new AssertionException("Missing true predicate.");

            Assert.That(node, Is.TypeOf<GenTreeMskCon>());
            Assert.That(node.AsMskCon().SimdMaskVal.u64[0], Is.EqualTo(expected));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void UnknownTrueMaskPatternRemainsAnIntrinsicInsteadOfAnInventedConstant()
    {
        WithImporter(compiler =>
        {
            var pattern = compiler.gtNewIconNode(TYP_INT, 14);
            Push(compiler, pattern);
            var sig = Signature(1);
            var node = RequireIntrinsic(Import(compiler, NI_Sve_CreateTrueMaskInt32, in sig, TYP_INT, TYP_MASK, 16));

            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_Sve_CreateTrueMaskInt32));
            Assert.That(node.GetOp(1), Is.SameAs(pattern));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(2u, 8)]
    [TestCase(3u, 8)]
    [TestCase(4u, 16)]
    public static void TableRowsPreserveLocalIdentityOffsetsAndSimdWidth(uint count, int width)
    {
        WithImporter(compiler =>
        {
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT,
                Layout = compiler.typGetBlkLayout(checked(count * (uint)width)),
            };
            var fields = TableFields(compiler, new GenTreeLclVar(TYP_STRUCT, 0), count);
            var index = 0;
            for (var use = fields.Uses.Head; use is not null; use = use.Next)
            {
                Assert.That(use.Offset, Is.EqualTo(index * width));
                Assert.That(use.Type, Is.EqualTo(Compiler.GetSimdTypeForSize(width)));
                Assert.That(use.Node.AsLclFld().LclNum, Is.Zero);
                Assert.That(use.Node.AsLclFld().LclOffs, Is.EqualTo(index * width));
                index++;
            }
            Assert.That(index, Is.EqualTo(count));
            Assert.That(fields.IsContained, Is.True);
        });
    }

    [TestCase(2u)]
    [TestCase(3u)]
    [TestCase(4u)]
    public static void ParameterFieldsUseEachFieldClassButKeepTheTupleStorageStride(uint count)
    {
        WithImporter(compiler =>
        {
            var tuple = (ClassInfo*)s_tupleClass;
            tuple->FieldCount = checked((int)count);
            tuple->Fields[0] = s_smallVectorClass;
            tuple->Fields[1] = s_vectorClass;
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT,
                Layout = compiler.typGetBlkLayout(count * 16),
            };

            var fields = ParameterFields(compiler, new GenTreeLclVar(TYP_STRUCT, 0), count,
                (CORINFO_CLASS_STRUCT_*)s_tupleClass);

            var index = 0;
            for (var use = fields.Uses.Head; use is not null; use = use.Next)
            {
                Assert.That(use.Type, Is.EqualTo(index == 0 ? TYP_SIMD8 : TYP_SIMD16));
                Assert.That(use.Offset, Is.EqualTo(index * 16));
                Assert.That(use.Node.AsLclFld().LclOffs, Is.EqualTo(index * 16));
                Assert.That(use.Node.AsLclFld().LclNum, Is.Zero);
                index++;
            }
            Assert.That(index, Is.EqualTo(count));
            Assert.That(s_fieldQueries, Is.EqualTo(count));
            Assert.That(s_fieldTypeQueries, Is.EqualTo(count));
        });
    }

    [TestCase(NI_AdvSimd_VectorTableLookup, TYP_SIMD8, false)]
    [TestCase(NI_AdvSimd_VectorTableLookup, TYP_SIMD8, true)]
    [TestCase(NI_AdvSimd_Arm64_VectorTableLookup, TYP_SIMD16, false)]
    [TestCase(NI_AdvSimd_Arm64_VectorTableLookup, TYP_SIMD16, true)]
    public static void TableLookupConvertsOnlyTupleOperandsAndKeepsEeQueryOrder(
        NamedIntrinsic intrinsic, var_types vectorType, bool tuple)
    {
        WithImporter(compiler =>
        {
            var vectorClass = vectorType == TYP_SIMD8 ? s_smallVectorClass : s_vectorClass;
            ((ClassInfo*)vectorClass)->ElementType = CORINFO_TYPE_UBYTE;
            s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[0] = tuple ? s_tupleClass : vectorClass;
            s_argumentTypes[1] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[1] = vectorClass;
            compiler.lvaTable[0] = tuple
                ? new LclVarDsc { Type = TYP_STRUCT, Layout = compiler.typGetBlkLayout((uint)(vectorType.Size * 2)) }
                : new LclVarDsc { Type = vectorType };
            compiler.lvaTable[1].Type = vectorType;
            var table = new GenTreeLclVar(tuple ? TYP_STRUCT : vectorType, 0);
            var indices = new GenTreeLclVar(vectorType, 1);
            Push(compiler, table, indices);
            var sig = Signature(2);

            var node = RequireIntrinsic(Import(compiler, intrinsic, in sig, TYP_UBYTE,
                vectorType, checked((byte)vectorType.Size)));

            Assert.That(node.HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(node.GetOp(2), Is.SameAs(indices));
            Assert.That(node.GetOp(1), tuple ? Is.TypeOf<GenTreeFieldList>() : Is.SameAs(table));
            Assert.That(compiler.info.compNeedsConsecutiveRegisters, Is.EqualTo(tuple));
            Assert.That(s_argumentQueries, Is.EqualTo(s_argumentQueriesOneThenZero));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(2u, NI_Sve_StoreAndZipx2)]
    [TestCase(3u, NI_Sve_StoreAndZipx3)]
    [TestCase(4u, NI_Sve_StoreAndZipx4)]
    public static void SveStoreZipSelectsTheWholeTupleArityAndPreservesOperandOrder(
        uint fields, NamedIntrinsic expected)
    {
        WithImporter(compiler =>
        {
            ((ClassInfo*)s_tupleClass)->FieldCount = checked((int)fields);
            compiler.lvaTable[0] = new LclVarDsc
            {
                Type = TYP_STRUCT,
                Layout = compiler.typGetBlkLayout(fields * 16),
            };
            s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[0] = s_vectorClass;
            s_argumentTypes[1] = CORINFO_TYPE_BYREF;
            s_argumentTypes[2] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[2] = s_tupleClass;
            var mask = new GenTreeMskCon(default);
            var address = new GenTreeLclVar(TYP_BYREF, 2);
            var tuple = new GenTreeLclVar(TYP_STRUCT, 0);
            Push(compiler, mask, address, tuple);
            var sig = Signature(3);
            var node = RequireIntrinsic(Import(compiler, NI_Sve_StoreAndZip, in sig, TYP_INT, TYP_VOID, 16));

            Assert.That(node.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(node.GetOp(1), Is.SameAs(mask));
            Assert.That(node.GetOp(2), Is.SameAs(address));
            Assert.That(node.GetOp(3), Is.TypeOf<GenTreeFieldList>());
            Assert.That(compiler.info.compNeedsConsecutiveRegisters, Is.True);
            Assert.That(s_argumentQueries, Is.EqualTo(s_argumentQueriesTwoThenOneThenZero));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(CORINFO_TYPE_BYTE, TYP_BYTE)]
    [TestCase(CORINFO_TYPE_UBYTE, TYP_UBYTE)]
    [TestCase(CORINFO_TYPE_SHORT, TYP_SHORT)]
    [TestCase(CORINFO_TYPE_USHORT, TYP_USHORT)]
    public static void NarrowingStoreUsesThePointerChildTypeWithoutLosingSignedness(
        CorInfoType elementType, var_types auxiliaryType)
    {
        WithImporter(compiler =>
        {
            ((ClassInfo*)s_pointerClass)->ChildType = elementType;
            s_argumentTypes[1] = CORINFO_TYPE_PTR;
            s_argumentClasses[1] = s_pointerClass;
            var mask = new GenTreeMskCon(default);
            var address = new GenTreeLclVar(TYP_I_IMPL, 2);
            var vector = new GenTreeLclVar(TYP_SIMD16, 0);
            Push(compiler, mask, address, vector);
            var sig = Signature(3);
            var node = RequireIntrinsic(Import(compiler, NI_Sve_StoreNarrowing, in sig, TYP_INT, TYP_VOID, 16));

            Assert.That(node.AuxiliaryType, Is.EqualTo(auxiliaryType));
            Assert.That(node.GetOp(1), Is.SameAs(mask));
            Assert.That(node.GetOp(2), Is.SameAs(address));
            Assert.That(node.GetOp(3), Is.SameAs(vector));
            Assert.That(s_argumentQueries, Is.EqualTo(s_argumentQueriesOne));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_Sve_SaturatingDecrementBy16BitElementCountScalar)]
    [TestCase(NI_Sve_SaturatingIncrementBy16BitElementCountScalar)]
    [TestCase(NI_Sve_SaturatingDecrementBy32BitElementCountScalar)]
    [TestCase(NI_Sve_SaturatingIncrementBy64BitElementCountScalar)]
    public static void ScalarElementCountsRetainBothDifferentImmediateDomains(NamedIntrinsic intrinsic)
    {
        WithImporter(compiler =>
        {
            s_argumentTypes[0] = CORINFO_TYPE_LONG;
            s_argumentTypes[1] = CORINFO_TYPE_INT;
            s_argumentTypes[2] = CORINFO_TYPE_INT;
            var value = new GenTreeLclVar(TYP_LONG, 0);
            var scale = compiler.gtNewIconNode(TYP_INT, 16);
            var pattern = compiler.gtNewIconNode(TYP_INT, 31);
            Push(compiler, value, scale, pattern);
            var sig = Signature(3);
            var node = RequireIntrinsic(Import(compiler, intrinsic, in sig, TYP_LONG, TYP_LONG, 16));

            Assert.That(node.HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(node.Type, Is.EqualTo(TYP_LONG));
            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_LONG));
            Assert.That(node.GetOp(1), Is.SameAs(value));
            Assert.That(node.GetOp(2), Is.SameAs(scale));
            Assert.That(node.GetOp(3), Is.SameAs(pattern));
            Assert.That(s_argumentQueries, Is.EqualTo(s_argumentQueriesTwoThenOneThenZero));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_Sve_ExtractAfterLastActiveElementScalar, 2)]
    [TestCase(NI_Sve_ExtractLastActiveElementScalar, 2)]
    [TestCase(NI_Sve_ConditionalExtractAfterLastActiveElementScalar, 3)]
    [TestCase(NI_Sve_ConditionalExtractLastActiveElementScalar, 3)]
    public static void ScalarExtractionPreservesAllArgumentsAndItsBaseType(
        NamedIntrinsic intrinsic, int count)
    {
        WithImporter(compiler =>
        {
            s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[0] = s_vectorClass;
            s_argumentTypes[count - 1] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[count - 1] = s_vectorClass;
            GenTree[] operands = count == 2
                ? [new GenTreeMskCon(default), new GenTreeLclVar(TYP_SIMD16, 0)]
                : [new GenTreeMskCon(default), new GenTreeLclVar(TYP_INT, 3), new GenTreeLclVar(TYP_SIMD16, 0)];
            if (count == 3)
            {
                s_argumentTypes[1] = CORINFO_TYPE_INT;
            }
            Push(compiler, operands);
            var sig = Signature(count);
            var node = RequireIntrinsic(Import(compiler, intrinsic, in sig, TYP_INT, TYP_INT, 16));

            Assert.That(node.Type, Is.EqualTo(TYP_INT));
            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_INT));
            for (var index = 0; index < count; index++)
            {
                Assert.That(node.GetOp(index + 1), Is.SameAs(operands[index]));
                Assert.That(s_argumentQueries[index], Is.EqualTo(count - index - 1));
            }
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_Sve_MultiplyAddRotateComplexBySelectedScalar, TYP_FLOAT)]
    [TestCase(NI_Sve2_MultiplyAddRotateComplexBySelectedScalar, TYP_SHORT)]
    [TestCase(NI_Sve2_MultiplyAddRoundedDoublingSaturateHighRotateComplexBySelectedScalar, TYP_INT)]
    [TestCase(NI_Sve2_DotProductRotateComplexBySelectedIndex, TYP_BYTE)]
    public static void ComplexSelectedScalarKeepsFiveOperandsAndDistinctIndexAndRotation(
        NamedIntrinsic intrinsic, var_types baseType)
    {
        WithImporter(compiler =>
        {
            ((ClassInfo*)s_vectorClass)->ElementType = baseType switch
            {
                TYP_FLOAT => CORINFO_TYPE_FLOAT,
                TYP_SHORT => CORINFO_TYPE_SHORT,
                TYP_INT => CORINFO_TYPE_INT,
                TYP_BYTE => CORINFO_TYPE_BYTE,
                _ => throw new AssertionException("Unexpected complex intrinsic fixture type."),
            };

            for (var index = 0; index < 3; index++)
            {
                s_argumentTypes[index] = CORINFO_TYPE_VALUECLASS;
                s_argumentClasses[index] = s_vectorClass;
            }
            s_argumentTypes[3] = CORINFO_TYPE_INT;
            s_argumentTypes[4] = CORINFO_TYPE_INT;
            GenTree[] operands =
            [
                new GenTreeLclVar(TYP_SIMD16, 0),
                new GenTreeLclVar(TYP_SIMD16, 1),
                new GenTreeLclVar(TYP_SIMD16, 0),
                compiler.gtNewIconNode(TYP_INT, 1),
                compiler.gtNewIconNode(TYP_INT, 3),
            ];
            Push(compiler, operands);
            var sig = Signature(5);
            var node = RequireIntrinsic(Import(compiler, intrinsic, in sig, baseType, TYP_SIMD16, 16));

            Assert.That(node.HWIntrinsicId, Is.EqualTo(intrinsic));
            for (var index = 0; index < operands.Length; index++)
            {
                Assert.That(node.GetOp(index + 1), Is.SameAs(operands[index]));
            }
            Assert.That(s_argumentQueries, Is.EqualTo(s_argumentQueriesFourThroughZero));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_Sve2_AddWideningEven)]
    [TestCase(NI_Sve2_AddWideningOdd)]
    [TestCase(NI_Sve2_SubtractWideningEven)]
    [TestCase(NI_Sve2_SubtractWideningOdd)]
    public static void WideningKeepsReturnElementTypeAndSourceAuxiliaryTypeDistinct(NamedIntrinsic intrinsic)
    {
        WithImporter(compiler =>
        {
            s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[0] = s_unsignedVectorClass;
            s_argumentTypes[1] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[1] = s_vectorClass;
            var first = new GenTreeLclVar(TYP_SIMD16, 0);
            var second = new GenTreeLclVar(TYP_SIMD16, 1);
            Push(compiler, first, second);
            var sig = Signature(2);
            var node = RequireIntrinsic(Import(compiler, intrinsic, in sig, TYP_LONG, TYP_SIMD16, 16));

            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_LONG));
            Assert.That(node.AuxiliaryType, Is.EqualTo(TYP_UINT));
            Assert.That(node.GetOp(1), Is.SameAs(first));
            Assert.That(node.GetOp(2), Is.SameAs(second));
            Assert.That(s_argumentQueries, Is.EqualTo(s_argumentQueriesOneThenZero));
        });
    }

    [Test]
    public static void SaturatingAddTakesItsAuxiliaryTypeFromTheSecondArgument()
    {
        WithImporter(compiler =>
        {
            s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[0] = s_vectorClass;
            s_argumentTypes[1] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[1] = s_unsignedVectorClass;
            var first = new GenTreeLclVar(TYP_SIMD16, 0);
            var second = new GenTreeLclVar(TYP_SIMD16, 1);
            Push(compiler, first, second);
            var sig = Signature(2);
            var node = RequireIntrinsic(Import(compiler, NI_Sve2_AddSaturate, in sig, TYP_INT, TYP_SIMD16, 16));

            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_INT));
            Assert.That(node.AuxiliaryType, Is.EqualTo(TYP_UINT));
            Assert.That(node.GetOp(1), Is.SameAs(first));
            Assert.That(node.GetOp(2), Is.SameAs(second));
            Assert.That(s_argumentQueries, Is.EqualTo(s_argumentQueriesZeroThenOne));
        });
    }

    [TestCase(NI_Sve2_VectorTableLookup)]
    [TestCase(NI_Sve_SaturatingDecrementByActiveElementCount)]
    [TestCase(NI_Sve_SaturatingIncrementByActiveElementCount)]
    public static void SveMetadataQueriesKeepTheNativeAuxiliaryTypeSource(NamedIntrinsic intrinsic)
    {
        WithImporter(compiler =>
        {
            s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[0] = s_vectorClass;
            s_argumentTypes[1] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[1] = s_unsignedVectorClass;
            var first = new GenTreeLclVar(TYP_SIMD16, 0);
            GenTree second = intrinsic == NI_Sve2_VectorTableLookup
                ? new GenTreeLclVar(TYP_SIMD16, 1)
                : new GenTreeMskCon(default);
            Push(compiler, first, second);
            var sig = Signature(2);
            var node = RequireIntrinsic(Import(compiler, intrinsic, in sig, TYP_INT, TYP_SIMD16, 16));

            Assert.That(node.GetOp(1), Is.SameAs(first));
            Assert.That(node.GetOp(2), Is.SameAs(second));
            Assert.That(node.AuxiliaryType, Is.EqualTo(intrinsic == NI_Sve2_VectorTableLookup ? TYP_UINT : TYP_INT));
            Assert.That(s_argumentQueries, Is.EqualTo(intrinsic == NI_Sve2_VectorTableLookup
                ? s_argumentQueriesZeroThenOne : s_argumentQueriesOneThenZero));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_Sve_SaturatingDecrementByActiveElementCount)]
    [TestCase(NI_Sve_SaturatingIncrementByActiveElementCount)]
    public static void ActiveElementCountsConvertVectorPredicatesThroughTheRealMaskConstructor(
        NamedIntrinsic intrinsic)
    {
        WithImporter(compiler =>
        {
            s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[0] = s_vectorClass;
            s_argumentTypes[1] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[1] = s_vectorClass;
            var first = new GenTreeLclVar(TYP_SIMD16, 0);
            var predicate = new GenTreeLclVar(TYP_SIMD16, 1);
            Push(compiler, first, predicate);
            var sig = Signature(2);
            var node = RequireIntrinsic(Import(compiler, intrinsic, in sig, TYP_INT, TYP_SIMD16, 16));

            Assert.That(node.GetOp(1), Is.SameAs(first));
            var conversion = node.GetOp(2).AsHWIntrinsic();
            Assert.That(conversion.HWIntrinsicId, Is.EqualTo(NI_Sve_ConvertVectorToMask));
            Assert.That(conversion.Type, Is.EqualTo(TYP_MASK));
            Assert.That(conversion.GetOp(1).AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_Sve_ConversionTrueMask));
            Assert.That(conversion.GetOp(2), Is.SameAs(predicate));
            Assert.That(node.AuxiliaryType, Is.EqualTo(TYP_INT));
            Assert.That(s_argumentQueries, Is.EqualTo(s_argumentQueriesOneThenZero));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_Sve_Load2xVectorAndUnzip)]
    [TestCase(NI_Sve_Load3xVectorAndUnzip)]
    [TestCase(NI_Sve_Load4xVectorAndUnzip)]
    public static void MaskedTupleLoadsKeepThePredicateAndByrefAndRequestConsecutiveRegisters(
        NamedIntrinsic intrinsic)
    {
        WithImporter(compiler =>
        {
            var predicate = new GenTreeMskCon(default);
            var address = new GenTreeLclVar(TYP_BYREF, 2);
            var cast = new GenTreeCast(TYP_I_IMPL, address, false, TYP_I_IMPL);
            Push(compiler, predicate, cast);
            var sig = Signature(2);
            var node = RequireIntrinsic(Import(compiler, intrinsic, in sig, TYP_INT, TYP_STRUCT, 16));

            Assert.That(node.HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(node.GetOp(1), Is.SameAs(predicate));
            Assert.That(node.GetOp(2), Is.SameAs(address));
            Assert.That(node.Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(compiler.info.compNeedsConsecutiveRegisters, Is.True);
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(3)]
    [TestCase(4)]
    public static void GatherPrefetchKeepsItsOverloadArgumentOrderAndAddressVectorType(int count)
    {
        WithImporter(compiler =>
        {
            s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[0] = s_vectorClass;
            s_argumentTypes[count - 2] = CORINFO_TYPE_VALUECLASS;
            s_argumentClasses[count - 2] = s_unsignedVectorClass;
            s_argumentTypes[count - 1] = CORINFO_TYPE_INT;
            var mask = new GenTreeMskCon(default);
            var addresses = new GenTreeLclVar(TYP_SIMD16, 1);
            var hint = compiler.gtNewIconNode(TYP_INT, 15);
            GenTree[] operands;
            if (count == 3)
            {
                operands = [mask, addresses, hint];
            }
            else
            {
                s_argumentTypes[1] = CORINFO_TYPE_NATIVEINT;
                operands = [mask, new GenTreeLclVar(TYP_I_IMPL, 2), addresses, hint];
            }
            Push(compiler, operands);
            var sig = Signature(count);
            var node = RequireIntrinsic(Import(compiler, NI_Sve_GatherPrefetch32Bit, in sig, TYP_INT, TYP_VOID, 16));

            Assert.That(node.AuxiliaryType, Is.EqualTo(TYP_UINT));
            for (var index = 0; index < count; index++)
            {
                Assert.That(node.GetOp(index + 1), Is.SameAs(operands[index]));
                Assert.That(s_argumentQueries[index], Is.EqualTo(count - index - 1));
            }
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(TYP_BYTE, 0xffUL)]
    [TestCase(TYP_UBYTE, 0xffUL)]
    [TestCase(TYP_SHORT, 0xffffUL)]
    [TestCase(TYP_USHORT, 0xffffUL)]
    [TestCase(TYP_INT, 0xffffffffUL)]
    [TestCase(TYP_FLOAT, 0xffffffffUL)]
    [TestCase(TYP_LONG, ulong.MaxValue)]
    [TestCase(TYP_DOUBLE, ulong.MaxValue)]
    public static void ElementAllBitsSetMasksAreUnsignedBitPatterns(var_types type, ulong expected)
    {
        Assert.That(ElementBits(null, type), Is.EqualTo(expected));
    }

    [Test]
    public static void UnhandledSpecialIntrinsicLeavesTheArgumentStackUntouched()
    {
        WithImporter(compiler =>
        {
            Push(compiler, new GenTreeLclVar(TYP_SIMD16, 0), new GenTreeLclVar(TYP_SIMD16, 1));
            var sig = Signature(2);
            Assert.That(Import(compiler, NI_AdvSimd_Add, in sig, TYP_INT, TYP_SIMD16, 16), Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(2));
        });
    }

    private static GenTreeHWIntrinsic RequireIntrinsic(GenTree? node)
    {
        return node as GenTreeHWIntrinsic ?? throw new AssertionException("Missing hardware-intrinsic node.");
    }

    private static GenTree? Import(Compiler compiler, NamedIntrinsic intrinsic, in CORINFO_SIG_INFO sig,
        var_types baseType, var_types resultType, byte size, bool mustExpand = true)
    {
        return ImportCore(compiler, intrinsic, null, null, in sig, default, baseType, resultType, size, mustExpand);
    }

    private static CORINFO_SIG_INFO Signature(int count)
    {
        return new CORINFO_SIG_INFO { numArgs = checked((ushort)count), args = (CORINFO_ARG_LIST_STRUCT_*)1 };
    }

    private static void Push(Compiler compiler, params GenTree[] nodes)
    {
        foreach (var node in nodes)
        {
            compiler.impPushOnStack(node, new typeInfo(node.Type));
        }
    }

    private static readonly List<int> s_argumentQueries = [];
    private static readonly List<string> s_assertions = [];
    private static readonly CorInfoType[] s_argumentTypes = new CorInfoType[8];
    private static readonly nuint[] s_argumentClasses = new nuint[8];
    private static nuint s_vectorClass;
    private static nuint s_unsignedVectorClass;
    private static nuint s_smallVectorClass;
    private static nuint s_tupleClass;
    private static nuint s_pointerClass;
    private static int s_fieldQueries;
    private static int s_fieldTypeQueries;

    private static void WithImporter(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        vtable.Base.Base.getClassNameFromMetadata = &GetClassName;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getTypeForPrimitiveNumericClass = &GetNumericType;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
        vtable.Base.Base.getArgType = &GetArgumentType;
        vtable.Base.Base.getArgNext = &GetNextArgument;
        vtable.Base.Base.getArgClass = &GetArgumentClass;
        vtable.Base.Base.getChildType = &GetChildType;
        vtable.Base.Base.getClassNumInstanceFields = &GetInstanceFieldCount;
        vtable.Base.Base.getFieldInClass = &GetField;
        vtable.Base.Base.getFieldType = &GetFieldType;
        vtable.Base.notifyInstructionSetUsage =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte, byte>)&NotifyIsa;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.info.compMaxStack = 8;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.stackState.esStack = new StackEntry[8];
        compiler.lvaTable =
        [
            new LclVarDsc { Type = TYP_SIMD16 },
            new LclVarDsc { Type = TYP_SIMD16 },
            new LclVarDsc { Type = TYP_BYREF },
            new LclVarDsc { Type = TYP_INT },
        ];
        compiler.lvaCount = compiler.lvaTable.Length;
        s_argumentQueries.Clear();
        s_assertions.Clear();
        s_fieldQueries = 0;
        s_fieldTypeQueries = 0;
        Array.Fill(s_argumentTypes, CORINFO_TYPE_UNDEF);
        Array.Clear(s_argumentClasses);
        JitTls.Compiler = compiler;
#if DEBUG
        JitTls.LogEnv.Compiler = compiler;
#endif
        try
        {
            fixed (byte* name = "Vector128`1\0"u8)
            fixed (byte* smallName = "Vector64`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                ClassInfo vector = new() { Name = name, Namespace = ns, Size = 16, ElementType = CORINFO_TYPE_INT };
                ClassInfo unsignedVector = new() { Name = name, Namespace = ns, Size = 16, ElementType = CORINFO_TYPE_UINT };
                ClassInfo smallVector = new() { Name = smallName, Namespace = ns, Size = 8, ElementType = CORINFO_TYPE_INT };
                var fields = stackalloc nuint[4];
                for (var index = 0; index < 4; index++)
                {
                    fields[index] = unchecked((nuint)(&vector));
                }
                ClassInfo tuple = new() { FieldCount = 2, Fields = fields };
                ClassInfo pointer = new() { ChildType = CORINFO_TYPE_SHORT };
                s_vectorClass = unchecked((nuint)(&vector));
                s_unsignedVectorClass = unchecked((nuint)(&unsignedVector));
                s_smallVectorClass = unchecked((nuint)(&smallVector));
                s_tupleClass = unchecked((nuint)(&tuple));
                s_pointerClass = unchecked((nuint)(&pointer));
                action(compiler);
                Assert.That(s_assertions, Is.Empty);
            }
        }
        finally
        {
            JitTls.Compiler = previous;
            s_vectorClass = 0;
            s_unsignedVectorClass = 0;
            s_smallVectorClass = 0;
            s_tupleClass = 0;
            s_pointerClass = 0;
        }
    }

    private struct ClassInfo
    {
        public byte* Name;
        public byte* Namespace;
        public int Size;
        public CorInfoType ElementType;
        public int FieldCount;
        public nuint* Fields;
        public CorInfoType ChildType;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "");
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte NotifyIsa(ICorJitInfo* self, CORINFO_InstructionSet isa, byte supported, byte preserve) => supported;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetClassName(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, byte** ns)
    {
        var value = (ClassInfo*)type;
        *ns = value->Namespace;
        return value->Name;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => ((ClassInfo*)type)->Size;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index) => type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetNumericType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => ((ClassInfo*)type)->ElementType;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetInstanceFieldCount(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => ((ClassInfo*)type)->FieldCount;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_FIELD_STRUCT_* GetField(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index)
    {
        s_fieldQueries++;
        return (CORINFO_FIELD_STRUCT_*)&((ClassInfo*)type)->Fields[index];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetFieldType(ICorJitInfo* self, CORINFO_FIELD_STRUCT_* field,
        CORINFO_CLASS_STRUCT_** type, CORINFO_CLASS_STRUCT_* owner)
    {
        s_fieldTypeQueries++;
        *type = (CORINFO_CLASS_STRUCT_*)*(nuint*)field;
        return CORINFO_TYPE_VALUECLASS;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetArgumentClass(ICorJitInfo* self, CORINFO_SIG_INFO* sig,
        CORINFO_ARG_LIST_STRUCT_* argument)
    {
        return (CORINFO_CLASS_STRUCT_*)s_argumentClasses[checked((int)(nuint)argument - 1)];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetChildType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, CORINFO_CLASS_STRUCT_** child)
    {
        *child = null;
        return ((ClassInfo*)type)->ChildType;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod GetArgumentType(ICorJitInfo* self, CORINFO_SIG_INFO* sig,
        CORINFO_ARG_LIST_STRUCT_* argument, CORINFO_CLASS_STRUCT_** type)
    {
        var index = checked((int)(nuint)argument - 1);
        s_argumentQueries.Add(index);
        *type = (CORINFO_CLASS_STRUCT_*)s_argumentClasses[index];
        return (CorInfoTypeWithMod)s_argumentTypes[index];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_ARG_LIST_STRUCT_* GetNextArgument(ICorJitInfo* self, CORINFO_ARG_LIST_STRUCT_* argument)
        => (CORINFO_ARG_LIST_STRUCT_*)((nuint)argument + 1);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getHWIntrinsicImmOpsArm64Core")]
    private static extern void ImmediateOperands(Compiler compiler, NamedIntrinsic intrinsic, in CORINFO_SIG_INFO sig,
        ref GenTree? first, ref GenTree? second);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impNonConstFallbackArm64Core")]
    private static extern GenTreeHWIntrinsic? Fallback(Compiler compiler,
        NamedIntrinsic intrinsic, var_types vectorType, var_types baseType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impSpecialIntrinsicArm64Core")]
    private static extern GenTree? ImportCore(Compiler compiler, NamedIntrinsic intrinsic,
        CORINFO_CLASS_STRUCT_* cls, CORINFO_METHOD_STRUCT_* method, in CORINFO_SIG_INFO sig,
        in CORINFO_CONST_LOOKUP entryPoint, var_types baseType, var_types resultType, byte size, bool mustExpand);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "gtConvertTableOpToFieldListArm64")]
    private static extern GenTreeFieldList TableFields(Compiler compiler, GenTree op, uint fields);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "gtConvertParamOpToFieldListArm64")]
    private static extern GenTreeFieldList ParameterFields(Compiler compiler, GenTree op, uint fields,
        CORINFO_CLASS_STRUCT_* cls);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "simdAllBitsSetForElementTypeArm64")]
    private static extern ulong ElementBits(Compiler? compiler, var_types type);
}
#endif
