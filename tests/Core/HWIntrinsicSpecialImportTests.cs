// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && TARGET_XARCH
[NonParallelizable]
internal static unsafe class HWIntrinsicSpecialImportTests
{
    [TestCase(NI_X86Base_AndNot, TYP_INT, (byte)0)]
    [TestCase(NI_X86Base_AndNot, TYP_SIMD16, (byte)16)]
    [TestCase(NI_AVX2_AndNot, TYP_SIMD32, (byte)32)]
    public static void AndNotDecomposesWithFirstOperandNegated(
        NamedIntrinsic intrinsic, var_types resultType, byte width)
    {
        WithImporter(compiler => {
            var first = new GenTreeLclVar(resultType, 0);
            var second = new GenTreeLclVar(resultType, 1);
            Push(compiler, first, second);
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;

            var result = Import(compiler, intrinsic, in sig, TYP_INT, resultType, width);

            Assert.That(result, Is.Not.Null);
            if (width == 0)
            {
                Assert.That(result!.Oper, Is.EqualTo(genTreeOps.GT_AND));
                Assert.That(result.AsOp().Op2, Is.SameAs(second));
                Assert.That(result.AsOp().Op1.Oper, Is.EqualTo(genTreeOps.GT_NOT));
                Assert.That(result.AsOp().Op1.AsUnOp().Op1, Is.SameAs(first));
            }
            else
            {
                Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
                Assert.That(result!.AsHWIntrinsic().GetOp(2), Is.SameAs(second));
                var negation = result.AsHWIntrinsic().GetOp(1).AsHWIntrinsic();
                Assert.That(negation.GetOp(1), Is.SameAs(first));
                Assert.That(negation.GetOp(2).IsVectorAllBitsSet, Is.True);
            }
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_X86Base_Pause)]
    [TestCase(NI_X86Serialize_Serialize)]
    [TestCase(NI_X86Base_MemoryFence)]
    public static void ZeroArgumentIntrinsicsPreserveVoidSignature(NamedIntrinsic intrinsic)
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = default;
            sig.retType = CORINFO_TYPE_VOID;

            var result = Import(compiler, intrinsic, in sig, TYP_UNDEF, TYP_VOID, 0);

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            Assert.That(result!.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(intrinsic));
            Assert.That(result.Type, Is.EqualTo(TYP_VOID));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_AVX2_ZeroHighBits)]
    [TestCase(NI_AVX2_BitFieldExtract)]
    public static void ScalarEncodingReversesOperands(NamedIntrinsic intrinsic)
    {
        WithImporter(compiler => {
            var first = new GenTreeLclVar(TYP_INT, 0);
            var second = new GenTreeLclVar(TYP_INT, 1);
            Push(compiler, first, second);
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;

            var result = Import(compiler, intrinsic, in sig, TYP_INT, TYP_INT, 0);

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            Assert.That(result!.AsHWIntrinsic().GetOp(1), Is.SameAs(second));
            Assert.That(result.AsHWIntrinsic().GetOp(2), Is.SameAs(first));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void ManagedThreeArgumentBitFieldExtractDoesNotConsumeArguments()
    {
        WithImporter(compiler => {
            Push(compiler, new GenTreeLclVar(TYP_INT, 0),
                new GenTreeLclVar(TYP_INT, 1), new GenTreeLclVar(TYP_INT, 2));
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 3;

            Assert.That(Import(compiler, NI_AVX2_BitFieldExtract,
                in sig, TYP_INT, TYP_INT, 0), Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(3));
        });
    }

    [Test]
    public static void PermuteVarSwapsIndexAndSource()
    {
        WithImporter(compiler => {
            var source = new GenTreeLclVar(TYP_SIMD32, 0);
            var indices = new GenTreeLclVar(TYP_SIMD32, 1);
            Push(compiler, source, indices);
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 2;
            fixed (byte* name = "Vector256`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                var classInfo = new ClassInfo { Name = name, Namespace = ns, Size = 32 };
                sig.retTypeSigClass = (CORINFO_CLASS_STRUCT_*)&classInfo;
                var result = Import(compiler, NI_AVX2_PermuteVar8x32,
                    in sig, TYP_INT, TYP_SIMD32, 32);

                Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
                Assert.That(result!.AsHWIntrinsic().GetOp(1), Is.SameAs(indices));
                Assert.That(result.AsHWIntrinsic().GetOp(2), Is.SameAs(source));
                Assert.That(compiler.impStackHeight, Is.Zero);
            }
        });
    }

    [Test]
    public static void LoadAndStorePreserveAddressAndVectorOrder()
    {
        WithImporter(compiler => {
            var address = new GenTreeLclVar(TYP_BYREF, 0);
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 1;
            Push(compiler, address);

            var loaded = Import(compiler, NI_X86Base_LoadVector128,
                in sig, TYP_INT, TYP_SIMD16, 16);

            Assert.That(loaded!.Oper, Is.EqualTo(genTreeOps.GT_IND));
            Assert.That(loaded.AsIndir().Addr, Is.SameAs(address));
            Assert.That(compiler.impStackHeight, Is.Zero);

            var vector = new GenTreeLclVar(TYP_SIMD16, 1);
            Push(compiler, address, vector);
            sig.numArgs = 2;
            var stored = Import(compiler, NI_X86Base_Store,
                in sig, TYP_INT, TYP_VOID, 16);

            Assert.That(stored!.Oper, Is.EqualTo(genTreeOps.GT_STOREIND));
            Assert.That(stored.AsStoreInd().Addr, Is.SameAs(address));
            Assert.That(stored.AsStoreInd().Data, Is.SameAs(vector));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(NI_AVX512_CompareEqual, NI_AVX512_CompareEqualMask, false)]
    [TestCase(NI_AVX512_Classify, NI_AVX512_ClassifyMask, true)]
    public static void MaskResultsConvertBackToTheSignatureVector(
        NamedIntrinsic intrinsic, NamedIntrinsic maskedId, bool hasImmediate)
    {
        WithImporter(compiler => {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            fixed (byte* name = "Vector512`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                var classInfo = new ClassInfo { Name = name, Namespace = ns, Size = 64 };
                CORINFO_SIG_INFO sig = default;
                sig.numArgs = 2;
                sig.retTypeSigClass = (CORINFO_CLASS_STRUCT_*)&classInfo;
                var first = new GenTreeLclVar(TYP_SIMD64, 0);
                GenTree second = hasImmediate
                    ? compiler.gtNewIconNode(TYP_INT, 1)
                    : new GenTreeLclVar(TYP_SIMD64, 1);
                Push(compiler, first, second);

                var result = Import(compiler, intrinsic, in sig, TYP_INT, TYP_SIMD64, 64);

                Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
                var conversion = result!.AsHWIntrinsic();
                Assert.That(conversion.HWIntrinsicId, Is.EqualTo(NI_AVX512_ConvertMaskToVector));
                Assert.That(conversion.Type, Is.EqualTo(TYP_SIMD64));
                var mask = conversion.GetOp(1).AsHWIntrinsic();
                Assert.That(mask.HWIntrinsicId, Is.EqualTo(maskedId));
                Assert.That(mask.GetOp(1), Is.SameAs(first));
                Assert.That(mask.GetOp(2), Is.SameAs(second));
                Assert.That(compiler.compMaskConvertUsed, Is.True);
                Assert.That(compiler.impStackHeight, Is.Zero);
            }
        });
    }

    [TestCase(0x96)]
    [TestCase(0xE8)]
    public static void TernaryLogicRetainsAllThreeOperandsWhenRequired(int control)
    {
        WithImporter(compiler => {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            var first = new GenTreeLclVar(TYP_SIMD16, 0);
            var second = new GenTreeLclVar(TYP_SIMD16, 1);
            var third = new GenTreeLclVar(TYP_SIMD16, 2);
            Push(compiler, first, second, third, compiler.gtNewIconNode(TYP_INT, control));
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 4;

            var result = Import(compiler, NI_AVX512_TernaryLogic,
                in sig, TYP_INT, TYP_SIMD16, 16);

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            var node = result!.AsHWIntrinsic();
            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_AVX512_TernaryLogic));
            Assert.That(node.GetOp(1), Is.SameAs(first));
            Assert.That(node.GetOp(2), Is.SameAs(second));
            Assert.That(node.GetOp(3), Is.SameAs(third));
            Assert.That(node.GetOp(4).AsIntCon().IconValue, Is.EqualTo((nint)control));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void TernarySelectAppendsUnusedSideEffectsBeforeReturningSelectedValue()
    {
        WithImporter(compiler => {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            var effect = compiler.gtNewStoreLclVarNode(0, compiler.gtNewZeroConNode(TYP_SIMD16));
            var selected = new GenTreeLclVar(TYP_SIMD16, 2);
            Push(compiler, effect, compiler.gtNewZeroConNode(TYP_SIMD16),
                selected, compiler.gtNewIconNode(TYP_INT, 0xAA));
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 4;

            var result = Import(compiler, NI_AVX512_TernaryLogic,
                in sig, TYP_INT, TYP_SIMD16, 16);

            Assert.That(result, Is.SameAs(selected));
            var statement = ImportStatements(compiler);
            Assert.That(statement, Is.Not.Null);
            Assert.That(statement!.RootNode.AsOp().Op1, Is.SameAs(effect));
            Assert.That(statement.NextStmt, Is.Null);
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [TestCase(0x88, false)]
    [TestCase(0xEE, false)]
    [TestCase(0x66, false)]
    [TestCase(0x22, true)]
    public static void TernaryBinaryControlsNormalizeInNativeOperandOrder(int control, bool negated)
    {
        WithImporter(compiler => {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            var first = compiler.gtNewZeroConNode(TYP_SIMD16);
            var second = new GenTreeLclVar(TYP_SIMD16, 1);
            var third = new GenTreeLclVar(TYP_SIMD16, 2);
            Push(compiler, first, second, third, compiler.gtNewIconNode(TYP_INT, control));
            CORINFO_SIG_INFO sig = default;
            sig.numArgs = 4;

            var result = Import(compiler, NI_AVX512_TernaryLogic,
                in sig, TYP_INT, TYP_SIMD16, 16);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            var node = result!.AsHWIntrinsic();
            if (negated)
            {
                Assert.That(node.GetOp(1), Is.SameAs(third));
                Assert.That(node.GetOp(2).AsHWIntrinsic().GetOp(1), Is.SameAs(second));
                Assert.That(node.GetOp(2).AsHWIntrinsic().GetOp(2).IsVectorAllBitsSet, Is.True);
            }
            else
            {
                Assert.That(node.GetOp(1), Is.SameAs(second));
                Assert.That(node.GetOp(2), Is.SameAs(third));
            }
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    private static void Push(Compiler compiler, params GenTree[] nodes)
    {
        foreach (var node in nodes)
        {
            compiler.impPushOnStack(node, new typeInfo(node.Type));
        }
    }

    private static void WithImporter(Action<Compiler> test)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        vtable.Base.Base.getClassNameFromMetadata = &GetClassName;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getTypeForPrimitiveNumericClass = &GetNumericType;
        vtable.Base.Base.getTypeInstantiationArgument = &GetTypeArgument;
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
        compiler.info.compMaxStack = 8;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.stackState.esStack = new StackEntry[8];
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_SIMD32 },
            new LclVarDsc { Type = TYP_SIMD32 },
            new LclVarDsc { Type = TYP_SIMD16 },
        ];
        compiler.lvaCount = compiler.lvaTable.Length;
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
        var classInfo = (ClassInfo*)type;
        *ns = classInfo->Namespace;
        return classInfo->Name;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => ((ClassInfo*)type)->Size;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index)
        => type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetNumericType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => CORINFO_TYPE_INT;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impSpecialIntrinsic")]
    private static extern GenTree? Import(Compiler compiler, NamedIntrinsic intrinsic,
        CORINFO_CLASS_STRUCT_* clsHnd, CORINFO_METHOD_STRUCT_* method,
        in CORINFO_SIG_INFO sig, in CORINFO_CONST_LOOKUP entryPoint,
        var_types simdBaseType, var_types retType, byte simdSize, bool mustExpand);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "impStmtList")]
    private static extern ref Statement? ImportStatements(Compiler compiler);

    private static GenTree? Import(Compiler compiler, NamedIntrinsic intrinsic,
        in CORINFO_SIG_INFO sig, var_types baseType, var_types retType, byte width)
        => Import(compiler, intrinsic, default, default, in sig, default, baseType, retType, width, false);
}
#endif
