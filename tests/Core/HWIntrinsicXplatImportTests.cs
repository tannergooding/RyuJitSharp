// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && TARGET_XARCH
[NonParallelizable]
internal static unsafe class HWIntrinsicXplatImportTests
{
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

    private static void WithImporter(Action<Compiler> test)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        vtable.Base.notifyInstructionSetUsage =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte>)&NotifyInstructionSetUsage;
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
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte NotifyInstructionSetUsage(ICorJitInfo* self,
        CORINFO_InstructionSet isa, byte supported) => supported;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impXplatIntrinsic")]
    private static extern GenTree? Import(Compiler compiler, NamedIntrinsic intrinsic,
        CORINFO_CLASS_STRUCT_* clsHnd, CORINFO_METHOD_STRUCT_* method,
        in CORINFO_SIG_INFO sig, in CORINFO_CONST_LOOKUP entryPoint,
        var_types simdBaseType, var_types retType, byte simdSize, bool mustExpand);
}
#endif
