// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if FEATURE_SIMD && TARGET_XARCH
[NonParallelizable]
internal static unsafe class SIMDImportTests
{
    [TestCase("Vector128`1", 16, CORINFO_TYPE_UINT, false, false, TYP_UINT, 16)]
    [TestCase("Vector256`1", 32, CORINFO_TYPE_FLOAT, true, false, TYP_FLOAT, 32)]
    [TestCase("Vector256`1", 32, CORINFO_TYPE_FLOAT, false, false, TYP_UNDEF, 0)]
    [TestCase("Vector512`1", 64, CORINFO_TYPE_DOUBLE, true, true, TYP_DOUBLE, 64)]
    [TestCase("Vector512`1", 64, CORINFO_TYPE_DOUBLE, true, false, TYP_UNDEF, 0)]
    [TestCase("Vector128`1", 32, CORINFO_TYPE_FLOAT, true, true, TYP_UNDEF, 0)]
    [TestCase("Vector128`1", 16, CORINFO_TYPE_NATIVEINT, true, true, TYP_I_IMPL, 16)]
    [TestCase("Vector128`1", 16, CORINFO_TYPE_CLASS, true, true, TYP_UNDEF, 0)]
    [TestCase("Vector128`2", 16, CORINFO_TYPE_FLOAT, true, true, TYP_UNDEF, 0)]
    public static void HardwareVectorRecognitionRespectsWidthElementAndAvailableISA(
        string name, int size, CorInfoType elementType, bool avx, bool avx512,
        var_types expectedType, int expectedSize)
    {
        var result = Recognize(name, "System.Runtime.Intrinsics", size, elementType, true,
            avx, avx512, true);
        Assert.Multiple(() => {
            Assert.That(result.Type, Is.EqualTo(expectedType));
            Assert.That(result.Size, Is.EqualTo(expectedSize));
            Assert.That(result.UsesSimd, Is.EqualTo(expectedType is not TYP_UNDEF));
        });
    }

    [TestCase("Vector`1", 16, true, TYP_FLOAT, 16)]
    [TestCase("Vector`1", 32, true, TYP_UNDEF, 0)]
    [TestCase("Vector128`1", 16, false, TYP_UNDEF, 0)]
    public static void RecognitionPreservesCrossTargetAndIntrinsicClassContracts(
        string name, int size, bool intrinsic, var_types expectedType, int expectedSize)
    {
        var ns = name.StartsWith("Vector`", StringComparison.Ordinal)
            ? "System.Numerics" : "System.Runtime.Intrinsics";
        var result = Recognize(name, ns, size, CORINFO_TYPE_FLOAT, intrinsic, false, false, false);
        Assert.That(result.Type, Is.EqualTo(expectedType));
        Assert.That(result.Size, Is.EqualTo(expectedSize));
        Assert.That(result.UsesSimd, Is.EqualTo(expectedType is not TYP_UNDEF));
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
#if FEATURE_MASKED_HW_INTRINSICS
    [TestCase(TYP_MASK)]
#endif
    public static void NonCallValuesPopWithoutAStore(var_types type)
    {
        WithImporter(compiler => {
            var value = new GenTreeLclVar(type, 0);
            compiler.impPushOnStack(value, new typeInfo(type));

            var result = PopSimd(compiler);

            Assert.That(result, Is.SameAs(value));
            Assert.That(compiler.impStackHeight, Is.Zero);
            Assert.That(compiler.lvaCount, Is.Zero);
            Assert.That(ImportStatements(compiler), Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void CallAndRetExprNormalizeToTempAfterPop(bool retExpr, bool returnBuffer)
    {
        WithImporter(compiler => {
            var call = new GenTreeCall(TYP_SIMD16) {
                _callType = gtCallTypes.CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)1,
            };
            if (returnBuffer)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG;
            }
            GenTree value = call;

            if (retExpr)
            {
                call.SingleInlineCandidateInfo = new InlineCandidateInfo();
                compiler.impAppendStmt(compiler.gtNewStmt(call));
                value = new GenTreeRetExpr(TYP_SIMD16, call);
            }

            compiler.impPushOnStack(value, new typeInfo(TYP_SIMD16));
            var result = PopSimd(compiler);

            Assert.That(result.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
            Assert.That(result.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(result.AsLclVar().LclNum, Is.Zero);
            Assert.That(compiler.lvaCount, Is.EqualTo(1));
            Assert.That(compiler.impStackHeight, Is.Zero);
            var first = ImportStatements(compiler);
            Assert.That(first, Is.Not.Null);
            var store = retExpr ? first?.NextStmt : first;
            var expectedStoreOper = returnBuffer
                ? (retExpr ? genTreeOps.GT_RET_EXPR : genTreeOps.GT_CALL)
                : genTreeOps.GT_STORE_LCL_VAR;
            Assert.That(store?.RootNode.Oper, Is.EqualTo(expectedStoreOper));
            Assert.That(call.Args.HasRetBuffer, Is.EqualTo(returnBuffer));
        });
    }

#if FEATURE_HW_INTRINSICS
    [TestCase(TYP_INT, TYP_UBYTE, true)]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, true)]
    [TestCase(TYP_BYREF, TYP_BYREF, true)]
    [TestCase(TYP_INT, TYP_FLOAT, false)]
    [TestCase(TYP_DOUBLE, TYP_INT, false)]
    public static void ScalarHardwareArgumentsUseNativeCoercionOrReportBadCode(
        var_types actualType, var_types signatureType, bool permitted)
    {
        WithImporter(compiler => {
            var argument = new GenTreeLclVar(actualType, 0);
            compiler.impPushOnStack(argument, new typeInfo(actualType));

            if (permitted)
            {
                Assert.That(GetHardwareArgument(compiler, signatureType, null), Is.SameAs(argument));
            }
            else
            {
                var failure = Assert.Throws<FatalJitException>(
                    () => GetHardwareArgument(compiler, signatureType, null));
                Assert.That(failure?.Result, Is.EqualTo(CORJIT_BADCODE));
            }

            Assert.That(compiler.impStackHeight, Is.Zero);
            Assert.That(ImportStatements(compiler), Is.Null);
        });
    }

    [Test]
    public static void HardwareArgumentsPopInCallerSpecifiedOrder()
    {
        WithImporter(compiler => {
            var left = new GenTreeLclVar(TYP_DOUBLE, 0);
            var right = new GenTreeLclVar(TYP_INT, 1);
            compiler.stackState.esStack = new StackEntry[2];
            compiler.info.compMaxStack = 2;
            compiler.impPushOnStack(left, new typeInfo(TYP_DOUBLE));
            compiler.impPushOnStack(right, new typeInfo(TYP_INT));

            Assert.That(GetHardwareArgument(compiler, TYP_INT, null), Is.SameAs(right));
            Assert.That(GetHardwareArgument(compiler, TYP_DOUBLE, null), Is.SameAs(left));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void StructHardwareArgumentsRecognizeClassBeforePoppingSIMD(
        bool signatureIsSimd, bool callOperand)
    {
        WithImporter(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
            vtable.Base.Base.getClassNameFromMetadata = &GetClassName;
            vtable.Base.Base.getClassSize = &GetClassSize;
            vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
            vtable.Base.Base.getTypeForPrimitiveNumericClass = &GetNumericType;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;

            fixed (byte* name = "Vector128`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                var typeInfo = new ClassInfo {
                    Name = name,
                    Namespace = ns,
                    Size = 16,
                    ElementType = CORINFO_TYPE_FLOAT,
                    Intrinsic = 1
                };
                GenTree argument = callOperand
                    ? new GenTreeCall(TYP_SIMD16) { _callType = gtCallTypes.CT_USER_FUNC }
                    : new GenTreeLclVar(TYP_SIMD16, 0);
                compiler.impPushOnStack(argument, new typeInfo(TYP_SIMD16));
                var result = GetHardwareArgument(compiler,
                    signatureIsSimd ? TYP_SIMD16 : TYP_STRUCT,
                    signatureIsSimd ? null : (CORINFO_CLASS_STRUCT_*)&typeInfo);

                Assert.That(result.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
                if (!callOperand)
                {
                    Assert.That(result, Is.SameAs(argument));
                }
                Assert.That(compiler.lvaCount, Is.EqualTo(callOperand ? 1 : 0));
                Assert.That(compiler._usesSimdTypes, Is.EqualTo(!signatureIsSimd));
                Assert.That(compiler.impStackHeight, Is.Zero);
            }
        });
    }

#if FEATURE_MASKED_HW_INTRINSICS
    [Test]
    public static void HardwareVectorSignatureCanPopAnImportedMask()
    {
        WithImporter(compiler => {
            var mask = new GenTreeLclVar(TYP_MASK, 0);
            compiler.impPushOnStack(mask, new typeInfo(TYP_MASK));

            Assert.That(GetHardwareArgument(compiler, TYP_SIMD16, null), Is.SameAs(mask));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }
#endif
#endif

    private static void WithImporter(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
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
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static (var_types Type, int Size, bool UsesSimd) Recognize(
        string name, string ns, int size, CorInfoType elementType, bool intrinsic,
        bool avx, bool avx512, bool matchedVM)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        vtable.Base.Base.getClassNameFromMetadata = &GetClassName;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
        vtable.Base.Base.getTypeForPrimitiveNumericClass = &GetNumericType;
        vtable.Base.notifyInstructionSetUsage =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, byte>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, byte, byte>)&NotifyInstructionSetUsage;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo, compMatchedVM = matchedVM };
        if (avx)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX);
        }
        if (avx512)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
        }

#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            var nameBytes = Encoding.UTF8.GetBytes(name + '\0');
            var namespaceBytes = Encoding.UTF8.GetBytes(ns + '\0');
            fixed (byte* namePointer = nameBytes)
            fixed (byte* namespacePointer = namespaceBytes)
            {
                var typeInfo = new ClassInfo {
                    Name = namePointer,
                    Namespace = namespacePointer,
                    Size = size,
                    ElementType = elementType,
                    Intrinsic = intrinsic ? (byte)1 : (byte)0
                };
                var type = compiler.getBaseTypeAndSizeOfSimdType((CORINFO_CLASS_STRUCT_*)&typeInfo, out var actualSize);
                return (type, actualSize, compiler._usesSimdTypes);
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct ClassInfo
    {
        public byte* Name;
        public byte* Namespace;
        public int Size;
        public CorInfoType ElementType;
        public byte Intrinsic;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
        => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => ((ClassInfo*)type)->Intrinsic;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetClassName(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, byte** ns)
    {
        var info = (ClassInfo*)type;
        *ns = info->Namespace;
        return info->Name;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => ((ClassInfo*)type)->Size;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index)
        => type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetNumericType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => ((ClassInfo*)type)->ElementType;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte NotifyInstructionSetUsage(ICorJitInfo* self, CORINFO_InstructionSet isa, byte supported)
        => supported;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impSIMDPopStack")]
    private static extern GenTree PopSimd(Compiler compiler);

#if FEATURE_HW_INTRINSICS
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getArgForHWIntrinsic")]
    private static extern GenTree GetHardwareArgument(
        Compiler compiler, var_types argumentType, CORINFO_CLASS_STRUCT_* argumentClass);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "impStmtList")]
    private static extern ref Statement? ImportStatements(Compiler compiler);
}
#endif
