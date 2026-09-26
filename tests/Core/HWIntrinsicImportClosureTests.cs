// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && TARGET_XARCH
[NonParallelizable]
internal static unsafe class HWIntrinsicImportClosureTests
{
    private static int s_selectedArg;

    [TestCase(NI_X86Base_Pause, 0u)]
    [TestCase(NI_X86Base_LoadVector128, 16u)]
    [TestCase(NI_AVX_LoadVector256, 32u)]
    [TestCase(NI_AVX512_LoadVector512, 64u)]
    public static void FixedWidthMetadataDoesNotInspectSignature(NamedIntrinsic intrinsic, uint expectedSize)
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            Assert.That(HWIntrinsicInfo.lookupSimdSize(compiler, intrinsic, in sig), Is.EqualTo(expectedSize));
        });
    }

    [TestCase(NI_Vector_Create, 0, 16)]
    [TestCase(NI_Vector_AsInt32, 1, 16)]
    [TestCase(NI_AVX_MaskStore, 2, 32)]
    public static void VariableWidthMetadataUsesTheNativeSignatureSource(
        NamedIntrinsic intrinsic, int selectedArg, int expectedSize)
    {
        WithImporter(compiler => {
            var className = expectedSize == 32 ? "Vector256`1\0"u8 : "Vector128`1\0"u8;
            fixed (byte* name = className)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                var classInfo = new ClassInfo {
                    Name = name,
                    Namespace = ns,
                    Size = expectedSize,
                };
                CORINFO_SIG_INFO sig = default;
                sig.retType = CORINFO_TYPE_VALUECLASS;
                sig.retTypeSigClass = (CORINFO_CLASS_STRUCT_*)&classInfo;
                sig.args = (CORINFO_ARG_LIST_STRUCT_*)1;
                compiler.info.compMatchedVM = true;
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX);
                s_selectedArg = 0;

                Assert.That(HWIntrinsicInfo.lookupSimdSize(compiler, intrinsic, in sig),
                    Is.EqualTo((uint)expectedSize));
                Assert.That(s_selectedArg, Is.EqualTo(selectedArg));
            }
        });
    }

    [TestCase(NI_Vector_Create, true)]
    [TestCase(NI_Vector_ConvertToInt64, false)]
    [TestCase(NI_X86Base_LoadVector128, false)]
    public static void AvxOnlyCompatibilityUsesPinnedMetadata(NamedIntrinsic intrinsic, bool expected)
    {
        Assert.That(HWIntrinsicInfo.AvxOnlyCompatible(intrinsic), Is.EqualTo(expected));
    }

    [Test]
    public static void SingleOperandCreateConsumesOnlyOneValue()
    {
        WithImporter(compiler => {
            var operand = new GenTreeLclVar(TYP_INT, 0);
            compiler.impPushOnStack(operand, new typeInfo(TYP_INT));
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 1;

            var node = Create(compiler, NI_Vector_Create, in sig, TYP_INT, TYP_SIMD16, 16);

            Assert.That(node, Is.Not.Null);
            Assert.That(node!.Oper, Is.EqualTo(genTreeOps.GT_HWINTRINSIC));
            Assert.That(node.AsHWIntrinsic().GetOp(1), Is.SameAs(operand));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(TYP_UBYTE, 16)]
    [TestCase(TYP_USHORT, 8)]
    [TestCase(TYP_UINT, 4)]
    [TestCase(TYP_ULONG, 2)]
    [TestCase(TYP_FLOAT, 4)]
    [TestCase(TYP_DOUBLE, 2)]
    public static void ConstantCreatePreservesElementOrderAndBitPattern(var_types baseType, int count)
    {
        WithImporter(compiler => {
            compiler.info.compMaxStack = count;
            compiler.stackState.esStack = new StackEntry[count];
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = (ushort)count;

            for (var index = 0; index < count; index++)
            {
                GenTree value = baseType switch {
                    TYP_FLOAT => compiler.gtNewDconNode(TYP_FLOAT, index is 0 ? -0.0 : index),
                    TYP_DOUBLE => compiler.gtNewDconNode(TYP_DOUBLE, index is 0 ? -0.0 : index),
                    TYP_ULONG => compiler.gtNewLconNode(index is 0 ? -1L : index),
                    _ => compiler.gtNewIconNode(TYP_INT, index is 0 ? -1 : index),
                };
                compiler.impPushOnStack(value, new typeInfo(value.Type));
            }

            var result = Create(compiler, NI_Vector_Create, in sig, baseType, TYP_SIMD16, 16);

            Assert.That(result, Is.TypeOf<GenTreeVecCon>());
            var vector = (GenTreeVecCon)result!;
            Assert.That(compiler.impStackHeight, Is.Zero);

            switch (baseType)
            {
                case TYP_UBYTE:
                    Assert.That(vector.SimdVal.u8[0], Is.EqualTo(byte.MaxValue));
                    Assert.That(vector.SimdVal.u8[count - 1], Is.EqualTo(count - 1));
                    break;
                case TYP_USHORT:
                    Assert.That(vector.SimdVal.u16[0], Is.EqualTo(ushort.MaxValue));
                    Assert.That(vector.SimdVal.u16[count - 1], Is.EqualTo(count - 1));
                    break;
                case TYP_UINT:
                    Assert.That(vector.SimdVal.u32[0], Is.EqualTo(uint.MaxValue));
                    Assert.That(vector.SimdVal.u32[count - 1], Is.EqualTo(count - 1));
                    break;
                case TYP_ULONG:
                    Assert.That(vector.SimdVal.u64[0], Is.EqualTo(ulong.MaxValue));
                    Assert.That(vector.SimdVal.u64[count - 1], Is.EqualTo(count - 1));
                    break;
                case TYP_FLOAT:
                    Assert.That(BitConverter.SingleToInt32Bits(vector.SimdVal.f32[0]), Is.EqualTo(int.MinValue));
                    Assert.That(vector.SimdVal.f32[count - 1], Is.EqualTo(count - 1));
                    break;
                case TYP_DOUBLE:
                    Assert.That(BitConverter.DoubleToInt64Bits(vector.SimdVal.f64[0]), Is.EqualTo(long.MinValue));
                    Assert.That(vector.SimdVal.f64[count - 1], Is.EqualTo(count - 1));
                    break;
            }
        });
    }

    [Test]
    public static void VariableCreatePreservesSourceOrderAcrossThePop()
    {
        WithImporter(compiler => {
            compiler.info.compMaxStack = 4;
            compiler.stackState.esStack = new StackEntry[4];
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 4;
            var values = new GenTree[4];

            for (var index = 0; index < values.Length; index++)
            {
                values[index] = new GenTreeLclVar(TYP_INT, index);
                compiler.impPushOnStack(values[index], new typeInfo(TYP_INT));
            }

            var result = Create(compiler, NI_Vector_Create, in sig, TYP_INT, TYP_SIMD16, 16);

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            for (var index = 0; index < values.Length; index++)
            {
                Assert.That(result!.AsHWIntrinsic().GetOp(index + 1), Is.SameAs(values[index]));
            }
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_X86Base_ShiftLeftLogical, NI_X86Base_ShiftLeftLogical, TYP_INT)]
    [TestCase(NI_AVX512_RotateLeft, NI_AVX512_RotateLeftVariable, TYP_INT)]
    [TestCase(NI_AVX512_RotateLeft, NI_AVX512_RotateLeftVariable, TYP_LONG)]
    public static void NonconstantFallbackBuildsTheNativeAlternative(
        NamedIntrinsic intrinsic, NamedIntrinsic expected, var_types baseType)
    {
        WithImporter(compiler => {
            compiler.info.compMaxStack = 2;
            compiler.stackState.esStack = new StackEntry[2];
            compiler.lvaTable = [new LclVarDsc { Type = TYP_SIMD16 }, new LclVarDsc { Type = TYP_INT }];
            compiler.lvaCount = 2;
            var vector = new GenTreeLclVar(TYP_SIMD16, 0);
            var count = new GenTreeLclVar(TYP_INT, 1);
            compiler.impPushOnStack(vector, new typeInfo(TYP_SIMD16));
            compiler.impPushOnStack(count, new typeInfo(TYP_INT));

            var result = Fallback(compiler, intrinsic, TYP_SIMD16, baseType);

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            Assert.That(result!.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(result.AsHWIntrinsic().GetOp(1), Is.SameAs(vector));
            Assert.That(result.AsHWIntrinsic().GetOp(2).Oper, Is.EqualTo(genTreeOps.GT_HWINTRINSIC));
            if (baseType is TYP_LONG)
            {
                var cast = result.AsHWIntrinsic().GetOp(2).AsHWIntrinsic().GetOp(1);
                Assert.That(cast.Oper, Is.EqualTo(genTreeOps.GT_CAST));
                Assert.That(cast.Type, Is.EqualTo(TYP_LONG));
                Assert.That(cast.AsCast().IsUnsigned, Is.True);
            }
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    private static void WithImporter(Action<Compiler> test)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        vtable.Base.Base.getClassNameFromMetadata = &GetClassName;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
        vtable.Base.Base.getTypeForPrimitiveNumericClass = &GetNumericType;
        vtable.Base.Base.getArgClass = &GetArgClass;
        vtable.Base.Base.getArgNext = &GetArgNext;
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
        compiler.info.compMaxStack = 1;
        compiler.opts.jitFlags = &flags;
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.stackState.esStack = new StackEntry[1];
        compiler.lvaTable = [];
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

    private struct ClassInfo
    {
        public byte* Name;
        public byte* Namespace;
        public int Size;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetClassName(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, byte** ns)
    {
        var info = (ClassInfo*)type;
        *ns = info->Namespace;
        return info->Name;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => ((ClassInfo*)type)->Size;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index)
        => type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetNumericType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => CORINFO_TYPE_FLOAT;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetArgClass(ICorJitInfo* self, CORINFO_SIG_INFO* sig,
        CORINFO_ARG_LIST_STRUCT_* arg)
    {
        s_selectedArg = (int)arg;
        return sig->retTypeSigClass;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_ARG_LIST_STRUCT_* GetArgNext(ICorJitInfo* self, CORINFO_ARG_LIST_STRUCT_* arg)
        => arg + 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte NotifyInstructionSetUsage(ICorJitInfo* self, CORINFO_InstructionSet isa, byte supported)
        => supported;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impSimdCreate")]
    private static extern GenTree? Create(Compiler compiler, NamedIntrinsic intrinsic,
        in CORINFO_SIG_INFO sig, var_types baseType, var_types retType, byte size);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impNonConstFallback")]
    private static extern GenTreeHWIntrinsic? Fallback(Compiler compiler, NamedIntrinsic intrinsic,
        var_types simdType, var_types baseType);
}
#endif
