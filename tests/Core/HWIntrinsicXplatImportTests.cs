// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && (TARGET_XARCH || TARGET_ARM64)
[NonParallelizable]
internal static unsafe class HWIntrinsicXplatImportTests
{
#if TARGET_XARCH
    private static int s_assertionCount;

    [TestCase((byte)32, false)]
#if DEBUG
    [TestCase((byte)64, true)]
#endif
    public static void VectorT128ExpansionKeepsDestinationIdentityAndNativeWidthAssertion(
        byte simdSize, bool expectsAssertion)
    {
        WithImporter(compiler => {
#if DEBUG
            compiler.info.compFullName = nameof(VectorT128ExpansionKeepsDestinationIdentityAndNativeWidthAssertion);
#endif
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_VectorT128);
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX2);
            var operand = new GenTreeLclVar(TYP_SIMD16, 0);
            compiler.impPushOnStack(operand, new typeInfo(TYP_SIMD16));
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 1;
            s_assertionCount = 0;

            var result = Import(compiler, NI_Vector_AsVector256, default, default, in sig,
                default, TYP_INT, TYP_SIMD32, simdSize, false);
            var assertionCount = s_assertionCount;

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            var node = result switch {
                GenTreeHWIntrinsic hardware => hardware,
                _ => throw new InvalidOperationException("Expected a Vector<T> expansion intrinsic."),
            };
            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_Vector_ToVector256));
            Assert.That(node.SimdSize, Is.EqualTo(16));
            Assert.That(node.Type, Is.EqualTo(TYP_SIMD32));
            Assert.That(node.GetOp(1), Is.SameAs(operand));
            Assert.That(compiler.impStackHeight, Is.Zero);
            Assert.That(assertionCount, Is.EqualTo(expectsAssertion ? 1 : 0));
        });
    }

    [TestCase(TYP_SHORT, TYP_BYTE)]
    [TestCase(TYP_INT, TYP_SHORT)]
    public static void SignedSaturatingNarrowUsesDestinationLanesAndPreservesOperandOrder(
        var_types baseType, var_types destinationType)
    {
        WithImporter(compiler => {
#if DEBUG
            compiler.info.compFullName = nameof(SignedSaturatingNarrowUsesDestinationLanesAndPreservesOperandOrder);
#endif
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;
            var first = new GenTreeLclVar(TYP_SIMD16, 0);
            var second = new GenTreeLclVar(TYP_SIMD16, 1);
            compiler.impPushOnStack(first, new typeInfo(TYP_SIMD16));
            compiler.impPushOnStack(second, new typeInfo(TYP_SIMD16));

            var result = Import(compiler, NI_Vector_NarrowWithSaturation, default, default, in sig,
                default, baseType, TYP_SIMD16, 16, false);
            var node = result switch {
                GenTreeHWIntrinsic hardware => hardware,
                _ => throw new InvalidOperationException("Expected a signed saturating narrowing intrinsic."),
            };

            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_X86Base_PackSignedSaturate));
            Assert.That(node.SimdBaseType, Is.EqualTo(destinationType));
            Assert.That(node.SimdSize, Is.EqualTo(16));
            Assert.That(node.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(node.GetOp(1), Is.SameAs(first));
            Assert.That(node.GetOp(2), Is.SameAs(second));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_Vector_LoadAligned, false, true)]
    [TestCase(NI_Vector_LoadAlignedNonTemporal, false, true)]
    [TestCase(NI_Vector_LoadUnsafe, false, true)]
    [TestCase(NI_Vector_LoadUnsafe, true, true)]
    [TestCase(NI_Vector_StoreAligned, false, true)]
    [TestCase(NI_Vector_StoreAlignedNonTemporal, false, true)]
    [TestCase(NI_Vector_StoreUnsafe, false, true)]
    [TestCase(NI_Vector_StoreUnsafe, true, true)]
    [TestCase(NI_Vector_LoadAligned, false, false)]
    [TestCase(NI_Vector_StoreAligned, false, false)]
    public static void MemoryImportsStripOnlyByrefAddressCasts(
        NamedIntrinsic intrinsic, bool withOffset, bool fromByref)
    {
        WithImporter(compiler => {
#if DEBUG
            compiler.info.compFullName = nameof(MemoryImportsStripOnlyByrefAddressCasts);
#endif
            var isStore = intrinsic is NI_Vector_StoreAligned or NI_Vector_StoreAlignedNonTemporal or NI_Vector_StoreUnsafe;
            var operandType = fromByref ? TYP_BYREF : TYP_INT;
            compiler.lvaTable[1].Type = operandType;
            var original = new GenTreeLclVar(operandType, 1);
            var cast = new GenTreeCast(TYP_I_IMPL, original, false, TYP_I_IMPL);
            if (isStore)
            {
                compiler.impPushOnStack(new GenTreeLclVar(TYP_SIMD16, 0), new typeInfo(TYP_SIMD16));
            }
            compiler.impPushOnStack(cast, new typeInfo(TYP_I_IMPL));
            if (withOffset)
            {
                compiler.impPushOnStack(compiler.gtNewIconNode(TYP_I_IMPL, 3), new typeInfo(TYP_I_IMPL));
            }
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = (ushort)((isStore ? 2 : 1) + (withOffset ? 1 : 0));
            s_assertionCount = 0;

            var result = Import(compiler, intrinsic, default, default, in sig, default,
                TYP_INT, isStore ? TYP_VOID : TYP_SIMD16, 16, false);

            Assert.That(result, Is.Not.Null);
            var address = result switch {
                GenTreeHWIntrinsic hardware => hardware.GetOp(1),
                GenTreeStoreInd store => store.Addr,
                GenTreeIndir load => load.Addr,
                _ => throw new InvalidOperationException("Unexpected vector memory import."),
            };
            if (withOffset)
            {
                Assert.That(address.Oper, Is.EqualTo(genTreeOps.GT_ADD));
                Assert.That(address.AsOp().Op2.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)4));
                address = address.AsOp().Op1;
            }
            Assert.That(address, Is.SameAs(fromByref ? original : (GenTree)cast));
            Assert.That(compiler.impStackHeight, Is.Zero);
            Assert.That(s_assertionCount, Is.Zero);
        });
    }

    [TestCase(NI_Vector_ConvertToDouble, TYP_LONG)]
    [TestCase(NI_Vector_ConvertToUInt32, TYP_FLOAT)]
    [TestCase(NI_Vector_FusedMultiplyAdd, TYP_FLOAT)]
    public static void UnavailableIsaDoesNotConsumeOperands(NamedIntrinsic intrinsic, var_types baseType)
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = intrinsic == NI_Vector_FusedMultiplyAdd ? (ushort)3 : (ushort)1;

            for (var index = 0; index < sig.numArgs; index++)
            {
                compiler.impPushOnStack(new GenTreeLclVar(TYP_SIMD16, index),
                    new typeInfo(TYP_SIMD16));
            }

            var result = Import(compiler, intrinsic, default, default, in sig, default,
                baseType, TYP_SIMD16, 16, false);

            Assert.That(result, Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(sig.numArgs));
        });
    }

    [Test]
    public static void AvxOnlyWidthCheckFallsBackBeforePoppingOperands()
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 1;
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX);
            compiler.lvaTable[0].Type = TYP_SIMD32;
            compiler.impPushOnStack(new GenTreeLclVar(TYP_SIMD32, 0), new typeInfo(TYP_SIMD32));

            var result = Import(compiler, NI_Vector_IsFinite, default, default, in sig, default,
                TYP_FLOAT, TYP_SIMD32, 32, false);

            Assert.That(result, Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(1));
        });
    }

    [Test]
    public static void FloatingEvenIntegerAndScalarShiftPreserveTheStack()
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 1;
            compiler.impPushOnStack(new GenTreeLclVar(TYP_SIMD16, 0), new typeInfo(TYP_SIMD16));
            Assert.That(Import(compiler, NI_Vector_IsEvenInteger, default, default, in sig,
                default, TYP_FLOAT, TYP_SIMD16, 16, false), Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(1));

            sig.numArgs = 2;
            compiler.lvaTable[1].Type = TYP_INT;
            compiler.impPushOnStack(new GenTreeLclVar(TYP_INT, 1), new typeInfo(TYP_INT));
            Assert.That(Import(compiler, NI_Vector_ShiftLeft, default, default, in sig,
                default, TYP_INT, TYP_SIMD16, 16, false), Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(2));
        });
    }
#endif

    [Test]
    public static void BinaryOperandsPreserveSourceOrder()
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;
            var first = new GenTreeLclVar(TYP_SIMD16, 0);
            var second = new GenTreeLclVar(TYP_SIMD16, 1);
            compiler.impPushOnStack(first, new typeInfo(TYP_SIMD16));
            compiler.impPushOnStack(second, new typeInfo(TYP_SIMD16));

            var result = Import(compiler, NI_Vector_op_BitwiseAnd, default, default, in sig,
                default, TYP_INT, TYP_SIMD16, 16, false);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Oper, Is.EqualTo(genTreeOps.GT_HWINTRINSIC));
            Assert.That(result.AsHWIntrinsic().GetOp(1), Is.SameAs(first));
            Assert.That(result.AsHWIntrinsic().GetOp(2), Is.SameAs(second));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase((byte)16)]
#if TARGET_XARCH
    [TestCase((byte)32)]
#elif TARGET_ARM64
    [TestCase((byte)8)]
#endif
    public static void PerLaneShiftUsesNativeWidthAndPreservesOperandOrder(byte size)
    {
        WithImporter(compiler => {
#if DEBUG
            compiler.info.compFullName = nameof(PerLaneShiftUsesNativeWidthAndPreservesOperandOrder);
#endif
#if TARGET_XARCH
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX2);
            var expected = NI_AVX2_ShiftLeftLogicalVariable;
#elif TARGET_ARM64
            var expected = NI_AdvSimd_ShiftLogical;
#endif
#if TARGET_ARM64
            var type = size == 8 ? TYP_SIMD8 : TYP_SIMD16;
#else
            var type = size == 8 ? TYP_SIMD8 : size == 16 ? TYP_SIMD16 : TYP_SIMD32;
#endif
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[1].Type = type;
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;
            var first = new GenTreeLclVar(type, 0);
            var counts = new GenTreeLclVar(type, 1);
            compiler.impPushOnStack(first, new typeInfo(type));
            compiler.impPushOnStack(counts, new typeInfo(type));

            var result = Import(compiler, NI_Vector_ShiftLeft, default, default, in sig,
                default, TYP_INT, type, size, false);
            var node = result switch {
                GenTreeHWIntrinsic hardware => hardware,
                _ => throw new InvalidOperationException("Expected a per-lane shift intrinsic."),
            };

            Assert.That(node.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(node.SimdSize, Is.EqualTo(size));
            Assert.That(node.Type, Is.EqualTo(type));
            Assert.That(node.GetOp(1), Is.SameAs(first));
            Assert.That(node.GetOp(2), Is.SameAs(counts));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void DeferredGeometricSequenceRetainsMethodAndOperandOrder()
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;
            compiler.lvaTable[0].Type = TYP_INT;
            compiler.lvaTable[1].Type = TYP_INT;
            var first = new GenTreeLclVar(TYP_INT, 0);
            var multiplier = new GenTreeLclVar(TYP_INT, 1);
            compiler.impPushOnStack(first, new typeInfo(TYP_INT));
            compiler.impPushOnStack(multiplier, new typeInfo(TYP_INT));
            var method = (CORINFO_METHOD_STRUCT_*)1;

            var result = Import(compiler, NI_Vector_CreateGeometricSequence,
                default, method, in sig, default, TYP_INT, TYP_SIMD16, 16, false);

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            var intrinsic = result!.AsHWIntrinsic();
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(NI_Vector_CreateGeometricSequence));
            Assert.That(intrinsic.GetOp(1), Is.SameAs(first));
            Assert.That(intrinsic.GetOp(2), Is.SameAs(multiplier));
            Assert.That(intrinsic.MethodHandle == method, Is.True);
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_Vector_get_NaN, TYP_FLOAT, 0xFFC00000UL)]
    [TestCase(NI_Vector_get_NegativeInfinity, TYP_FLOAT, 0xFF800000UL)]
    [TestCase(NI_Vector_get_NegativeZero, TYP_FLOAT, 0x80000000UL)]
    [TestCase(NI_Vector_get_NaN, TYP_DOUBLE, 0xFFF8000000000000UL)]
    [TestCase(NI_Vector_get_PositiveInfinity, TYP_DOUBLE, 0x7FF0000000000000UL)]
    public static void VectorConstantsKeepNativeFloatingPointBits(
        NamedIntrinsic intrinsic, var_types baseType, ulong expected)
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            var result = Import(compiler, intrinsic, default, default, in sig, default,
                baseType, TYP_SIMD16, 16, false);

            Assert.That(result, Is.TypeOf<GenTreeVecCon>());
            var vector = (GenTreeVecCon)result!;
            Assert.That(baseType == TYP_FLOAT ? vector.SimdVal.u32[0] : vector.SimdVal.u64[0],
                Is.EqualTo(expected));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

#if TARGET_ARM64
    [TestCase(false)]
    [TestCase(true)]
    public static void LongGeometricSequenceWithConstantMultiplierDefersOrPreservesTheStack(bool minOpts)
    {
        WithImporter(compiler => {
#if DEBUG
            compiler.info.compFullName = nameof(LongGeometricSequenceWithConstantMultiplierDefersOrPreservesTheStack);
#endif
            compiler.opts.SetMinOpts(minOpts);
            compiler.lvaTable[0].Type = TYP_LONG;
            var initial = new GenTreeLclVar(TYP_LONG, 0);
            var multiplier = compiler.gtNewLconNode(2);
            compiler.impPushOnStack(initial, new typeInfo(TYP_LONG));
            compiler.impPushOnStack(multiplier, new typeInfo(TYP_LONG));
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;
            var method = (CORINFO_METHOD_STRUCT_*)1;

            var result = Import(compiler, NI_Vector_CreateGeometricSequence, default, method, in sig,
                default, TYP_LONG, TYP_SIMD16, 16, false);

            if (minOpts)
            {
                Assert.That(result, Is.Null);
                Assert.That(compiler.impStackHeight, Is.EqualTo(2));
                Assert.That(compiler.impStackTop(1).val, Is.SameAs(initial));
                Assert.That(compiler.impStackTop(0).val, Is.SameAs(multiplier));
            }
            else
            {
                var node = result switch {
                    GenTreeHWIntrinsic hardware => hardware,
                    _ => throw new InvalidOperationException("Expected a deferred geometric sequence."),
                };

                Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_Vector_CreateGeometricSequence));
                Assert.That(node.MethodHandle == method, Is.True);
                Assert.That(node.GetOp(1), Is.SameAs(initial));
                Assert.That(node.GetOp(2), Is.SameAs(multiplier));
                Assert.That(compiler.impStackHeight, Is.Zero);
            }
        });
    }

    [TestCase(TYP_LONG, (byte)8, false)]
    [TestCase(TYP_LONG, (byte)8, true)]
    [TestCase(TYP_LONG, (byte)16, false)]
    [TestCase(TYP_LONG, (byte)16, true)]
    [TestCase(TYP_ULONG, (byte)8, false)]
    [TestCase(TYP_ULONG, (byte)8, true)]
    [TestCase(TYP_ULONG, (byte)16, false)]
    [TestCase(TYP_ULONG, (byte)16, true)]
    public static void LongMultiplicationEvaluatesEachOperandOnceAndPropagatesSpillEffects(
        var_types baseType, byte size, bool scalarSecond)
    {
        WithImporter(compiler => {
            var type = size == 8 ? TYP_SIMD8 : TYP_SIMD16;
            var first = new GenTreeIndir(genTreeOps.GT_IND, type,
                compiler.gtNewIconNode(TYP_I_IMPL, 0x1234)) {
                Flags = GenTreeFlags.GTF_GLOB_REF | GenTreeFlags.GTF_EXCEPT,
            };
            var second = new GenTreeIndir(genTreeOps.GT_IND, scalarSecond ? TYP_LONG : type,
                compiler.gtNewIconNode(TYP_I_IMPL, 0x5678)) {
                Flags = GenTreeFlags.GTF_GLOB_REF | GenTreeFlags.GTF_EXCEPT,
            };

            var result = compiler.gtNewSimdBinOpNode(genTreeOps.GT_MUL, type, first, second, baseType, size);
            var loads = new List<GenTree>();
            var stores = 0;
            CheckEffects(result);

            Assert.That(loads, Is.EqualTo(new GenTree[] { first, second }));
            Assert.That(stores, Is.EqualTo(size == 16 ? 2 : 0));
            Assert.That(compiler.lvaCount, Is.EqualTo(size == 16 ? 5 : 3));

            void CheckEffects(GenTree node)
            {
                if (node.Oper == genTreeOps.GT_IND)
                {
                    loads.Add(node);
                }
                else if (node.Oper == genTreeOps.GT_STORE_LCL_VAR)
                {
                    stores++;
                }

                foreach (var operand in node.Operands)
                {
                    var effects = operand.Flags & GenTreeFlags.GTF_ALL_EFFECT;
                    Assert.That(node.Flags & effects, Is.EqualTo(effects), node.Oper.ToString());
                    CheckEffects(operand);
                }
            }
        });
    }

    [TestCase(NI_Vector_AddSaturate, TYP_INT, (byte)16, NI_AdvSimd_AddSaturate)]
    [TestCase(NI_Vector_AddSaturate, TYP_LONG, (byte)8, NI_AdvSimd_AddSaturateScalar)]
    [TestCase(NI_Vector_SubtractSaturate, TYP_LONG, (byte)8, NI_AdvSimd_SubtractSaturateScalar)]
    public static void IntegerSaturationUsesAdvSimdAndPreservesOperandOrder(
        NamedIntrinsic operation, var_types baseType, byte size, NamedIntrinsic expected)
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;
            var type = size == 8 ? TYP_SIMD8 : TYP_SIMD16;
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[1].Type = type;
            var first = new GenTreeLclVar(type, 0);
            var second = new GenTreeLclVar(type, 1);
            compiler.impPushOnStack(first, new typeInfo(type));
            compiler.impPushOnStack(second, new typeInfo(type));

            var result = Import(compiler, operation, default, default, in sig, default,
                baseType, type, size, false);

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            var node = result!.AsHWIntrinsic();
            Assert.That(node.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(node.GetOp(1), Is.SameAs(first));
            Assert.That(node.GetOp(2), Is.SameAs(second));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void LongCreateSequenceFallsBackBeforePoppingVariableIncrement()
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;
            compiler.lvaTable[0].Type = TYP_LONG;
            compiler.lvaTable[1].Type = TYP_LONG;
            compiler.impPushOnStack(new GenTreeLclVar(TYP_LONG, 0), new typeInfo(TYP_LONG));
            compiler.impPushOnStack(new GenTreeLclVar(TYP_LONG, 1), new typeInfo(TYP_LONG));

            var result = Import(compiler, NI_Vector_CreateSequence, default, default, in sig, default,
                TYP_LONG, TYP_SIMD16, 16, false);

            Assert.That(result, Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(2));
        });
    }

    [TestCase(-1)]
    [TestCase(4)]
    public static void OutOfRangeWithElementFallsBackBeforePopping(int lane)
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 3;
            compiler.impPushOnStack(new GenTreeLclVar(TYP_SIMD16, 0), new typeInfo(TYP_SIMD16));
            compiler.impPushOnStack(compiler.gtNewIconNode(TYP_INT, lane), new typeInfo(TYP_INT));
            compiler.lvaTable[2].Type = TYP_INT;
            compiler.impPushOnStack(new GenTreeLclVar(TYP_INT, 2), new typeInfo(TYP_INT));

            var result = Import(compiler, NI_Vector_WithElement, default, default, in sig, default,
                TYP_INT, TYP_SIMD16, 16, false);

            Assert.That(result, Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(3));
        });
    }

    [Test]
    public static void VariableWithElementDefersExpansionWithMethodAndOperandOrder()
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 3;
            compiler.lvaTable[1].Type = TYP_INT;
            compiler.lvaTable[2].Type = TYP_INT;
            var vector = new GenTreeLclVar(TYP_SIMD16, 0);
            var index = new GenTreeLclVar(TYP_INT, 1);
            var value = new GenTreeLclVar(TYP_INT, 2);
            compiler.impPushOnStack(vector, new typeInfo(TYP_SIMD16));
            compiler.impPushOnStack(index, new typeInfo(TYP_INT));
            compiler.impPushOnStack(value, new typeInfo(TYP_INT));
            var method = (CORINFO_METHOD_STRUCT_*)1;

            var result = Import(compiler, NI_Vector_WithElement, default, method, in sig, default,
                TYP_INT, TYP_SIMD16, 16, false);

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            var node = result!.AsHWIntrinsic();
            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_Vector_WithElement));
            Assert.That(node.GetOp(1), Is.SameAs(vector));
            Assert.That(node.GetOp(2), Is.SameAs(index));
            Assert.That(node.GetOp(3), Is.SameAs(value));
            Assert.That(node.MethodHandle == method, Is.True);
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }
#endif

    private static void WithImporter(Action<Compiler> test)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        vtable.Base.notifyInstructionSetUsage =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte, byte>)&NotifyInstructionSetUsage;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.info.compMaxStack = 4;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.stackState.esStack = new StackEntry[4];
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_SIMD16 },
            new LclVarDsc { Type = TYP_SIMD16 },
            new LclVarDsc { Type = TYP_SIMD16 },
        ];
        compiler.lvaCount = 3;
        JitTls.Compiler = compiler;
#if DEBUG
        JitTls.LogEnv.Compiler = compiler;
#endif
        try
        {
            test(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
#if TARGET_XARCH
        s_assertionCount++;
#endif
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte NotifyInstructionSetUsage(ICorJitInfo* self,
        CORINFO_InstructionSet isa, byte supported, byte preserveNegativeDependency) => supported;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impXplatIntrinsic")]
    private static extern GenTree? Import(Compiler compiler, NamedIntrinsic intrinsic,
        CORINFO_CLASS_STRUCT_* clsHnd, CORINFO_METHOD_STRUCT_* method,
        in CORINFO_SIG_INFO sig, in CORINFO_CONST_LOOKUP entryPoint,
        var_types simdBaseType, var_types retType, byte simdSize, bool mustExpand);
}
#endif
