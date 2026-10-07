// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.HWIntrinsicFlag;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if FEATURE_HW_INTRINSICS && FEATURE_SIMD && (TARGET_XARCH || TARGET_ARM64)
[NonParallelizable]
internal static unsafe class HWIntrinsicDispatchTests
{
    private static readonly CorInfoType[] s_argumentTypes = new CorInfoType[4];
    private static readonly CORINFO_CLASS_STRUCT_*[] s_argumentClasses = new CORINFO_CLASS_STRUCT_*[4];
    private static readonly System.Collections.Generic.List<int> s_argumentQueries = [];
#if TARGET_XARCH
    private static readonly int[] s_oneArgument = [0];
    private static readonly int[] s_twoArguments = [0, 1];
    private static readonly int[] s_threeArguments = [0, 1, 2];
#endif

#if TARGET_XARCH
    [Test]
    public static void NextCallRetAddrDoesNotConsumeOperandsUnlessExpansionIsRequired()
    {
        WithImporter(compiler => {
            var operand = new GenTreeLclVar(TYP_INT, 0);
            Push(compiler, operand);
            compiler.info.compHasNextCallRetAddr = true;
            s_argumentTypes[0] = CORINFO_TYPE_INT;
            CORINFO_SIG_INFO sig = new() {
                args = (CORINFO_ARG_LIST_STRUCT_*)1, numArgs = 1, retType = CORINFO_TYPE_INT
            };

            Assert.That(Import(compiler, NI_X86Base_BitScanForward, in sig), Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(1));
            Assert.That(compiler.impStackTop().val, Is.SameAs(operand));
            Assert.That(s_argumentQueries, Is.Empty);

            var result = Import(compiler, NI_X86Base_BitScanForward, in sig, mustExpand: true);
            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            Assert.That(result!.AsHWIntrinsic().GetOp(1), Is.SameAs(operand));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void ZeroAndOneOperandImportsKeepTheirSignatureShape()
    {
        WithImporter(compiler => {
            CORINFO_SIG_INFO sig = new() {
                args = (CORINFO_ARG_LIST_STRUCT_*)1, retType = CORINFO_TYPE_VOID
            };
            var pause = Import(compiler, NI_X86Base_Pause, in sig);
            Assert.That(pause, Is.TypeOf<GenTreeHWIntrinsic>());
            Assert.That(pause!.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NI_X86Base_Pause));
            Assert.That(pause.Type, Is.EqualTo(TYP_VOID));

            var value = new GenTreeLclVar(TYP_INT, 0);
            Push(compiler, value);
            sig.numArgs = 1;
            sig.retType = CORINFO_TYPE_INT;
            s_argumentTypes[0] = CORINFO_TYPE_INT;
            var scan = Import(compiler, NI_X86Base_BitScanForward, in sig);
            Assert.That(scan, Is.TypeOf<GenTreeHWIntrinsic>());
            Assert.That(scan!.AsHWIntrinsic().GetOp(1), Is.SameAs(value));
            Assert.That(compiler.impStackHeight, Is.Zero);
            Assert.That(s_argumentQueries, Is.EqualTo(s_oneArgument));
        });
    }

    [Test]
    public static void CrcUsesPreciseSecondArgumentTypeAndSignatureOrder()
    {
        WithImporter(compiler => {
            s_argumentTypes[0] = CORINFO_TYPE_UINT;
            s_argumentTypes[1] = CORINFO_TYPE_UBYTE;
            var crc = new GenTreeLclVar(TYP_INT, 0);
            var data = new GenTreeLclVar(TYP_INT, 1);
            Push(compiler, crc, data);
            CORINFO_SIG_INFO sig = new() {
                args = (CORINFO_ARG_LIST_STRUCT_*)1, numArgs = 2, retType = CORINFO_TYPE_UINT
            };

            var result = Import(compiler, NI_X86Base_Crc32, in sig);

            Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
            var node = result!.AsHWIntrinsic();
            Assert.That(node.GetOp(1), Is.SameAs(crc));
            Assert.That(node.GetOp(2), Is.SameAs(data));
            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_UBYTE));
            Assert.That(s_argumentQueries, Is.EqualTo(s_twoArguments));
            Assert.That(compiler.impStackHeight, Is.Zero);
        });
    }

    [Test]
    public static void VectorBinaryAndFourOperandSpecialImportPreserveArguments()
    {
        WithImporter(compiler => {
            fixed (byte* name = "Vector128`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                ClassInfo vector = new() { Name = name, Namespace = ns, Size = 16 };
                var vectorClass = (CORINFO_CLASS_STRUCT_*)&vector;
                CORINFO_SIG_INFO sig = new() {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1,
                    retType = CORINFO_TYPE_VALUECLASS,
                    retTypeSigClass = vectorClass,
                    numArgs = 2
                };
                s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
                s_argumentTypes[1] = CORINFO_TYPE_VALUECLASS;
                s_argumentClasses[0] = vectorClass;
                s_argumentClasses[1] = vectorClass;
                var first = new GenTreeLclVar(TYP_SIMD16, 0);
                var second = new GenTreeLclVar(TYP_SIMD16, 1);
                Push(compiler, first, second);

                var sum = Import(compiler, NI_X86Base_Add, in sig);
                Assert.That(sum, Is.TypeOf<GenTreeHWIntrinsic>());
                Assert.That(sum!.AsHWIntrinsic().GetOp(1), Is.SameAs(first));
                Assert.That(sum.AsHWIntrinsic().GetOp(2), Is.SameAs(second));
                Assert.That(s_argumentQueries, Is.EqualTo(s_twoArguments));
                Assert.That(compiler.impStackHeight, Is.Zero);

                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
                s_argumentQueries.Clear();
                s_argumentTypes[2] = CORINFO_TYPE_VALUECLASS;
                s_argumentClasses[2] = vectorClass;
                s_argumentTypes[3] = CORINFO_TYPE_INT;
                var third = new GenTreeLclVar(TYP_SIMD16, 2);
                var control = compiler.gtNewIconNode(TYP_INT, 0x96);
                Push(compiler, first, second, third, control);
                sig.numArgs = 4;
                var ternary = Import(compiler, NI_AVX512_TernaryLogic, in sig);

                Assert.That(ternary, Is.TypeOf<GenTreeHWIntrinsic>());
                var node = ternary!.AsHWIntrinsic();
                Assert.That(node.GetOp(1), Is.SameAs(first));
                Assert.That(node.GetOp(2), Is.SameAs(second));
                Assert.That(node.GetOp(3), Is.SameAs(third));
                Assert.That(node.GetOp(4), Is.SameAs(control));
                Assert.That(compiler.impStackHeight, Is.Zero);
            }
        });
    }

    [Test]
    public static void GatherImmediateRejectsInvalidScaleWithoutPopping()
    {
        WithImporter(compiler => {
            fixed (byte* name = "Vector128`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                ClassInfo vector = new() { Name = name, Namespace = ns, Size = 16 };
                CORINFO_SIG_INFO sig = new() {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1,
                    retType = CORINFO_TYPE_VALUECLASS,
                    retTypeSigClass = (CORINFO_CLASS_STRUCT_*)&vector,
                    numArgs = 3
                };
                var address = new GenTreeLclVar(TYP_BYREF, 0);
                var indices = new GenTreeLclVar(TYP_SIMD16, 1);
                var scale = compiler.gtNewIconNode(TYP_INT, 3);
                Push(compiler, address, indices, scale);

                Assert.That(Import(compiler, NI_AVX2_GatherVector128, in sig), Is.Null);
                Assert.That(compiler.impStackHeight, Is.EqualTo(3));
                Assert.That(compiler.impStackTop().val, Is.SameAs(scale));
                Assert.That(s_argumentQueries, Is.Empty);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NonconstantShiftUsesVariableCountFallbackEvenWhenExpansionIsRequired(bool mustExpand)
    {
        WithImporter(compiler => {
            fixed (byte* name = "Vector128`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                ClassInfo vector = new() { Name = name, Namespace = ns, Size = 16 };
                var vectorClass = (CORINFO_CLASS_STRUCT_*)&vector;
                CORINFO_SIG_INFO sig = new() {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1,
                    retType = CORINFO_TYPE_VALUECLASS,
                    retTypeSigClass = vectorClass,
                    numArgs = 2
                };
                var source = new GenTreeLclVar(TYP_SIMD16, 0);
                var shift = new GenTreeLclVar(TYP_INT, 1);
                Push(compiler, source, shift);

                var result = Import(compiler, NI_X86Base_ShiftLeftLogical, in sig,
                    mustExpand: mustExpand);

                Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
                var node = result!.AsHWIntrinsic();
                Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_X86Base_ShiftLeftLogical));
                Assert.That(node.GetOp(1), Is.SameAs(source));
                Assert.That(node.GetOp(2).AsHWIntrinsic().GetOp(1), Is.SameAs(shift));
                Assert.That(compiler.impStackHeight, Is.Zero);
            }
        });
    }

    [Test]
    public static void UnknownImmediateDefersOnlyWithOptimizations()
    {
        WithImporter(compiler => {
            fixed (byte* name = "Vector128`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                ClassInfo vector = new() { Name = name, Namespace = ns, Size = 16 };
                var vectorClass = (CORINFO_CLASS_STRUCT_*)&vector;
                CORINFO_SIG_INFO sig = new() {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1,
                    retType = CORINFO_TYPE_VALUECLASS,
                    retTypeSigClass = vectorClass,
                    numArgs = 3
                };
                s_argumentTypes[0] = CORINFO_TYPE_VALUECLASS;
                s_argumentTypes[1] = CORINFO_TYPE_VALUECLASS;
                s_argumentTypes[2] = CORINFO_TYPE_INT;
                s_argumentClasses[0] = vectorClass;
                s_argumentClasses[1] = vectorClass;
                s_vectorElementType = CORINFO_TYPE_FLOAT;
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
                var first = new GenTreeLclVar(TYP_SIMD16, 0);
                var second = new GenTreeLclVar(TYP_SIMD16, 1);
                var immediate = new GenTreeLclVar(TYP_INT, 2);
                Push(compiler, first, second, immediate);
                var method = (CORINFO_METHOD_STRUCT_*)0x1234;

                var result = Import(compiler, NI_AVX512_Range, in sig, method: method);
                Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
                Assert.That(result!.AsHWIntrinsic().MethodHandle == method, Is.True);
                Assert.That(result.AsHWIntrinsic().GetOp(1), Is.SameAs(first));
                Assert.That(result.AsHWIntrinsic().GetOp(2), Is.SameAs(second));
                var checkedImmediate = result.AsHWIntrinsic().GetOp(3);
                Assert.That(checkedImmediate.Oper, Is.EqualTo(genTreeOps.GT_COMMA));
                Assert.That(checkedImmediate.AsOp().Op1.Oper, Is.EqualTo(genTreeOps.GT_BOUNDS_CHECK));
                Assert.That(checkedImmediate.AsOp().Op2.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
                Assert.That(s_argumentQueries, Is.EqualTo(s_threeArguments));
                Assert.That(compiler.impStackHeight, Is.Zero);
            }
        });

        WithImporter(compiler => {
            fixed (byte* name = "Vector128`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                ClassInfo vector = new() { Name = name, Namespace = ns, Size = 16 };
                CORINFO_SIG_INFO sig = new() {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1,
                    retType = CORINFO_TYPE_VALUECLASS,
                    retTypeSigClass = (CORINFO_CLASS_STRUCT_*)&vector,
                    numArgs = 3
                };
                Push(compiler, new GenTreeLclVar(TYP_SIMD16, 0),
                    new GenTreeLclVar(TYP_SIMD16, 1), new GenTreeLclVar(TYP_INT, 2));
                Assert.That(Import(compiler, NI_AVX512_Range, in sig), Is.Null);
                Assert.That(compiler.impStackHeight, Is.EqualTo(3));
            }
        }, minOpts: true);
    }

    [Test]
    public static void PortableAndSpecialDispatchKeepTheirOwnNodeSemantics()
    {
        WithImporter(compiler => {
            fixed (byte* name = "Vector128`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                ClassInfo vector = new() { Name = name, Namespace = ns, Size = 16 };
                var vectorClass = (CORINFO_CLASS_STRUCT_*)&vector;
                CORINFO_SIG_INFO sig = new() {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1,
                    retType = CORINFO_TYPE_VALUECLASS,
                    retTypeSigClass = vectorClass,
                    numArgs = 2
                };
                var first = new GenTreeLclVar(TYP_SIMD16, 0);
                var second = new GenTreeLclVar(TYP_SIMD16, 1);
                Push(compiler, first, second);

                var result = Import(compiler, NI_Vector_op_BitwiseAnd, in sig);
                Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
                Assert.That(result!.AsHWIntrinsic().GetOp(1), Is.SameAs(first));
                Assert.That(result.AsHWIntrinsic().GetOp(2), Is.SameAs(second));
                Assert.That(compiler.impStackHeight, Is.Zero);

                sig.numArgs = 1;
                var address = new GenTreeLclVar(TYP_BYREF, 2);
                var cast = compiler.gtNewCastNode(TYP_I_IMPL, address, false, TYP_I_IMPL);
                Push(compiler, cast);
                s_argumentTypes[0] = CORINFO_TYPE_NATIVEINT;
                var load = Import(compiler, NI_X86Base_LoadAlignedVector128, in sig);
                Assert.That(load, Is.TypeOf<GenTreeHWIntrinsic>());
                Assert.That(load!.AsHWIntrinsic().GetOp(1), Is.SameAs(address));
                Assert.That(compiler.impStackHeight, Is.Zero);

                Push(compiler, address);
                var special = Import(compiler, NI_X86Base_LoadVector128, in sig);
                Assert.That(special!.Oper, Is.EqualTo(genTreeOps.GT_IND));
                Assert.That(special.AsIndir().Addr, Is.SameAs(address));
                Assert.That(compiler.impStackHeight, Is.Zero);
            }
        });
    }

    [Test]
    public static void InvalidNodeIdWithoutSpecialImportRoutesVectorAdditionToPortableImporter()
    {
        WithImporter(compiler => {
            fixed (byte* name = "Vector128`1\0"u8)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                ClassInfo vector = new() { Name = name, Namespace = ns, Size = 16 };
                CORINFO_SIG_INFO sig = new() {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1,
                    retType = CORINFO_TYPE_VALUECLASS,
                    retTypeSigClass = (CORINFO_CLASS_STRUCT_*)&vector,
                    numArgs = 2
                };
                var first = new GenTreeLclVar(TYP_SIMD16, 0);
                var second = new GenTreeLclVar(TYP_SIMD16, 1);
                Push(compiler, first, second);

                Assert.That(HWIntrinsicInfo.IsInvalidNodeId(NI_Vector_op_Addition), Is.True);
                Assert.That(HWIntrinsicInfo.lookupFlags(NI_Vector_op_Addition) & HW_Flag_SpecialImport,
                    Is.EqualTo(HW_Flag_NoFlag));
                Assert.That(IsTableDriven(null, NI_Vector_op_Addition,
                    HWIntrinsicInfo.lookupCategory(NI_Vector_op_Addition)), Is.False);

                var result = Import(compiler, NI_Vector_op_Addition, in sig);

                Assert.That(result, Is.TypeOf<GenTreeHWIntrinsic>());
                var addition = result!.AsHWIntrinsic();
                Assert.That(addition.HWIntrinsicId, Is.EqualTo(NI_X86Base_Add));
                Assert.That(addition.GetOp(1), Is.SameAs(first));
                Assert.That(addition.GetOp(2), Is.SameAs(second));
                Assert.That(s_argumentQueries, Is.Empty);
                Assert.That(compiler.impStackHeight, Is.Zero);
            }
        });
    }
#elif TARGET_ARM64
    [TestCase(NI_Sve_ConditionalExtractLastActiveElement, true, false)]
    [TestCase(NI_Sve_ConditionalExtractLastActiveElementScalar, false, false)]
    [TestCase(NI_AdvSimd_Store, false, true)]
    [TestCase(NI_AdvSimd_Add, false, false)]
    public static void ScalarInputAndTupleFlagsKeepTheirIndependentNativeBits(
        NamedIntrinsic intrinsic, bool hasScalarInputVariant, bool baseTypeFromTuple)
    {
        Assert.That(HWIntrinsicInfo.HasScalarInputVariant(intrinsic), Is.EqualTo(hasScalarInputVariant));
        Assert.That(HWIntrinsicInfo.BaseTypeFromValueTupleArg(intrinsic), Is.EqualTo(baseTypeFromTuple));
    }

    [Test]
    public static void NextCallRetAddrReturnsBeforeTargetMetadataOrOperands()
    {
        WithImporter(compiler => {
            var operand = new GenTreeLclVar(TYP_INT, 0);
            Push(compiler, operand);
            compiler.info.compHasNextCallRetAddr = true;
            CORINFO_SIG_INFO sig = new() { numArgs = 1, retType = CORINFO_TYPE_INT };

            Assert.That(Import(compiler, NI_Illegal, in sig), Is.Null);
            Assert.That(compiler.impStackHeight, Is.EqualTo(1));
            Assert.That(compiler.impStackTop().val, Is.SameAs(operand));
            Assert.That(s_argumentQueries, Is.Empty);
            Assert.That(compiler.compFloatingPointUsed, Is.False);
        });
    }

    [TestCase(NI_AdvSimd_Add)]
    [TestCase(NI_AdvSimd_Insert)]
    [TestCase(NI_Sve_ConvertToSingle)]
    public static void UnsupportedStructResultReturnsBeforeArgumentBaseTypeOrImmediates(NamedIntrinsic intrinsic)
    {
        WithImporter(compiler => {
            fixed (byte* name = "UnsupportedStruct\0"u8)
            fixed (byte* ns = "System\0"u8)
            {
                ClassInfo resultClass = new() { Name = name, Namespace = ns, Size = 16 };
                CORINFO_SIG_INFO sig = new() {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1,
                    numArgs = 1,
                    retType = CORINFO_TYPE_VALUECLASS,
                    retTypeSigClass = (CORINFO_CLASS_STRUCT_*)&resultClass,
                };
                var operand = new GenTreeLclVar(TYP_INT, 0);
                Push(compiler, operand);

                Assert.That(Import(compiler, intrinsic, in sig), Is.Null);
                Assert.That(compiler.impStackHeight, Is.EqualTo(1));
                Assert.That(compiler.impStackTop().val, Is.SameAs(operand));
                Assert.That(s_argumentQueries, Is.Empty);
                Assert.That(compiler.compFloatingPointUsed, Is.False);
            }
        });
    }

    [TestCase(8)]
    [TestCase(16)]
    public static void TupleArgumentExtractsFirstVectorBeforeTheUnportedImmediateDependency(int vectorSize)
    {
        WithImporter(compiler => {
            var vectorName = vectorSize == 8 ? "Vector64`1\0"u8 : "Vector128`1\0"u8;
            fixed (byte* tupleName = "ValueTuple`2\0"u8)
            fixed (byte* tupleNamespace = "System\0"u8)
            fixed (byte* name = vectorName)
            fixed (byte* ns = "System.Runtime.Intrinsics\0"u8)
            {
                ClassInfo vectorClass = new() { Name = name, Namespace = ns, Size = vectorSize };
                ClassInfo tupleClass = new() {
                    Name = tupleName,
                    Namespace = tupleNamespace,
                    Size = vectorSize * 2,
                    FirstFieldClass = (CORINFO_CLASS_STRUCT_*)&vectorClass,
                };
                s_argumentTypes[0] = CORINFO_TYPE_PTR;
                s_argumentTypes[1] = CORINFO_TYPE_VALUECLASS;
                s_argumentClasses[1] = (CORINFO_CLASS_STRUCT_*)&tupleClass;
                CORINFO_SIG_INFO sig = new() {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1, numArgs = 2, retType = CORINFO_TYPE_VOID
                };
                var address = new GenTreeLclVar(TYP_BYREF, 0);
                var tuple = new GenTreeLclVar(TYP_STRUCT, 1);
                Push(compiler, address, tuple);

                var exception = Assert.Throws<FatalJitException>(() =>
                    Import(compiler, NI_AdvSimd_Store, in sig));

                Assert.That(exception, Is.TypeOf<FatalJitException>()
                    .With.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
                Assert.That(s_fieldQueries, Is.EqualTo(1));
                Assert.That(s_fieldTypeQueries, Is.EqualTo(1));
                Assert.That(s_lastFieldClass == (CORINFO_CLASS_STRUCT_*)&tupleClass, Is.True);
                Assert.That(compiler.UsesSimdTypes, Is.True);
                Assert.That(compiler.impStackHeight, Is.EqualTo(2));
                Assert.That(compiler.impStackTop().val, Is.SameAs(tuple));
                Assert.That(compiler.compFloatingPointUsed, Is.False);
            }
        });
    }
#endif

    private static void WithImporter(Action<Compiler> test, bool minOpts = false)
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
#if TARGET_ARM64
        vtable.Base.Base.getClassNumInstanceFields = &GetInstanceFieldCount;
        vtable.Base.Base.getFieldInClass = &GetFirstField;
        vtable.Base.Base.getFieldType = &GetFieldType;
#endif
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
        compiler.opts.SetMinOpts(minOpts);
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.stackState.esStack = new StackEntry[4];
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_INT },
            new LclVarDsc { Type = TYP_SIMD16 },
            new LclVarDsc { Type = TYP_INT },
        ];
        compiler.lvaCount = compiler.lvaTable.Length;
        s_argumentQueries.Clear();
        Array.Fill(s_argumentTypes, CORINFO_TYPE_UNDEF);
        Array.Clear(s_argumentClasses);
        s_vectorElementType = CORINFO_TYPE_INT;
#if TARGET_ARM64
        s_fieldQueries = 0;
        s_fieldTypeQueries = 0;
        s_lastFieldClass = null;
        var previousConfig = JitConfig;
        AltJitAssertOnNyi(ref JitConfig) = 0;
#endif
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
#if TARGET_ARM64
            JitConfig = previousConfig;
#endif
        }
    }

    private static void Push(Compiler compiler, params GenTree[] operands)
    {
        foreach (var operand in operands)
        {
            compiler.impPushOnStack(operand, new typeInfo(operand.Type));
        }
    }

    private static GenTree? Import(Compiler compiler, NamedIntrinsic intrinsic,
        in CORINFO_SIG_INFO sig, bool mustExpand = false, CORINFO_METHOD_STRUCT_* method = null)
        => compiler.impHWIntrinsic(intrinsic, null, method, in sig, default, mustExpand);

#if TARGET_XARCH
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "impIsTableDrivenHWIntrinsic")]
    private static extern bool IsTableDriven(Compiler? _, NamedIntrinsic intrinsic,
        HWIntrinsicCategory category);
#endif

    private struct ClassInfo
    {
        public byte* Name;
        public byte* Namespace;
        public int Size;
#if TARGET_ARM64
        public CORINFO_CLASS_STRUCT_* FirstFieldClass;
#endif
    }

    private static CorInfoType s_vectorElementType = CORINFO_TYPE_INT;

#if TARGET_ARM64
    private static int s_fieldQueries;
    private static int s_fieldTypeQueries;
    private static CORINFO_CLASS_STRUCT_* s_lastFieldClass;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitAssertOnNYI")]
    private static extern ref int AltJitAssertOnNyi(ref JitConfigValues config);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetInstanceFieldCount(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 2;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_FIELD_STRUCT_* GetFirstField(
        ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index)
    {
        s_fieldQueries++;
        s_lastFieldClass = type;
        return (CORINFO_FIELD_STRUCT_*)&((ClassInfo*)type)->FirstFieldClass;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetFieldType(ICorJitInfo* self, CORINFO_FIELD_STRUCT_* field,
        CORINFO_CLASS_STRUCT_** type, CORINFO_CLASS_STRUCT_* owner)
    {
        s_fieldTypeQueries++;
        *type = *(CORINFO_CLASS_STRUCT_**)field;
        return CORINFO_TYPE_VALUECLASS;
    }
#endif

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte NotifyInstructionSetUsage(ICorJitInfo* self,
        CORINFO_InstructionSet isa, byte supported, byte preserveNegativeDependency) => supported;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetClassName(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, byte** ns)
    {
        var cls = (ClassInfo*)type;
        *ns = cls->Namespace;
        return cls->Name;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => ((ClassInfo*)type)->Size;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeArgument(ICorJitInfo* self,
        CORINFO_CLASS_STRUCT_* type, int index) => type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetNumericType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type)
        => s_vectorElementType;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod GetArgumentType(ICorJitInfo* self, CORINFO_SIG_INFO* sig,
        CORINFO_ARG_LIST_STRUCT_* argument, CORINFO_CLASS_STRUCT_** type)
    {
        var index = checked((int)(nuint)argument - 1);
        s_argumentQueries.Add(index);
        *type = s_argumentClasses[index];
        return (CorInfoTypeWithMod)s_argumentTypes[index];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_ARG_LIST_STRUCT_* GetNextArgument(ICorJitInfo* self,
        CORINFO_ARG_LIST_STRUCT_* argument) => (CORINFO_ARG_LIST_STRUCT_*)((nuint)argument + 1);
}
#endif
