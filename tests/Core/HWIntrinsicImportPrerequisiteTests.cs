// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.CorInfoTypeWithMod;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.HWIntrinsicCategory;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
[NonParallelizable]
internal static unsafe class HWIntrinsicImportPrerequisiteTests
{
    private static readonly CorInfoType[] s_signatureTypes =
        [CORINFO_TYPE_UINT, CORINFO_TYPE_NATIVEUINT, CORINFO_TYPE_UBYTE, CORINFO_TYPE_CLASS];
    private static readonly List<nuint> s_signatureQueries = [];
    private static int s_signatureNextCalls;

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public static void SignatureReaderPreservesArgumentOrderHandlesAndPreciseTypes(int argumentCount)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArgType = &GetSignatureArgumentType;
        vtable.Base.Base.getArgNext = &GetNextSignatureArgument;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        var signature = new CORINFO_SIG_INFO {
            numArgs = checked((ushort)argumentCount),
            args = (CORINFO_ARG_LIST_STRUCT_*)1
        };
        s_signatureQueries.Clear();
        s_signatureNextCalls = 0;

        Compiler.HWIntrinsicSignatureReader reader = default;
        reader.Read(&jitInfo, &signature);

        CorInfoType[] actualTypes =
            [reader.op1JitType, reader.op2JitType, reader.op3JitType, reader.op4JitType];
        nuint[] actualHandles =
            [(nuint)reader.op1ClsHnd, (nuint)reader.op2ClsHnd,
             (nuint)reader.op3ClsHnd, (nuint)reader.op4ClsHnd];
        var_types[] coarseTypes =
            [reader.GetOp1Type(), reader.GetOp2Type(), reader.GetOp3Type(), reader.GetOp4Type()];
        var_types[] preciseTypes =
            [reader.GetOp1TypeAsPrecise(), reader.GetOp2TypeAsPrecise(),
             reader.GetOp3TypeAsPrecise(), reader.GetOp4TypeAsPrecise()];

        for (var i = 0; i < 4; i++)
        {
            var expectedType = i < argumentCount ? s_signatureTypes[i] : CORINFO_TYPE_UNDEF;
            Assert.That(actualTypes[i], Is.EqualTo(expectedType));
            Assert.That(actualHandles[i], Is.EqualTo(i < argumentCount ? (nuint)(0x800 + i) : 0));
            Assert.That(coarseTypes[i], Is.EqualTo(expectedType.VarType));
            Assert.That(preciseTypes[i], Is.EqualTo(expectedType.PreciseVarType));
        }

        Assert.That(s_signatureQueries, Is.EqualTo(GetExpectedArgumentQueries(argumentCount)));
        Assert.That(s_signatureNextCalls, Is.EqualTo(Math.Max(0, argumentCount - 1)));
    }

    private static nuint[] GetExpectedArgumentQueries(int count)
    {
        var result = new nuint[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = (nuint)(i + 1);
        }
        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod GetSignatureArgumentType(
        ICorJitInfo* self, CORINFO_SIG_INFO* signature,
        CORINFO_ARG_LIST_STRUCT_* argument, CORINFO_CLASS_STRUCT_** type)
    {
        var index = (nuint)argument;
        s_signatureQueries.Add(index);
        *type = (CORINFO_CLASS_STRUCT_*)(0x800 + index - 1);
        var result = (CorInfoTypeWithMod)s_signatureTypes[(int)index - 1];
        return index == 1 ? result | CORINFO_TYPE_MOD_PINNED : result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_ARG_LIST_STRUCT_* GetNextSignatureArgument(
        ICorJitInfo* self, CORINFO_ARG_LIST_STRUCT_* argument)
    {
        s_signatureNextCalls++;
        return (CORINFO_ARG_LIST_STRUCT_*)((nuint)argument + 1);
    }

    [TestCase(NI_X86Base_Add, false, true)]
    [TestCase(NI_X86Base_Add, true, false)]
    [TestCase(NI_AVX_Compare, false, false)]
    [TestCase(NI_Vector_Create, false, false)]
    public static void TableDrivenEligibilityHonorsCategoryAndSpecialImport(
        NamedIntrinsic intrinsic, bool forceSpecialCategory, bool expected)
    {
        var category = forceSpecialCategory
            ? HW_Category_Special
            : HWIntrinsicInfo.lookupCategory(intrinsic);
        Assert.That(IsTableDriven(null, intrinsic, category), Is.EqualTo(expected));
    }

    [TestCase(NI_X86Base_Add, TYP_UNDEF, false)]
    [TestCase(NI_X86Base_Add, TYP_INT, true)]
    [TestCase(NI_X86Base_Add, TYP_ULONG, true)]
    [TestCase(NI_X86Base_Add, TYP_FLOAT, true)]
    [TestCase(NI_Vector_Create, TYP_SIMD16, false)]
    public static void SupportedBaseTypeRequiresAnArithmeticElement(
        NamedIntrinsic intrinsic, var_types baseType, bool expected)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        Assert.That(IsSupportedBaseType(null, intrinsic, baseType), Is.EqualTo(expected));
    }

    [TestCase(NI_X86Base_Extract, TYP_INT, 1, true)]
    [TestCase(NI_X86Base_ShiftLeftLogical, TYP_INT, 1, true)]
    [TestCase(NI_X86Base_ShiftLeftLogical, TYP_FLOAT, 1, false)]
    [TestCase(NI_X86Base_Add, TYP_INT, 1, false)]
    [TestCase(NI_X86Base_Extract, TYP_INT, 0, false)]
    public static void ImmediateDiscoveryOnlyReadsEligibleStackTop(
        NamedIntrinsic intrinsic, var_types operandType, int argumentCount, bool expected)
    {
        WithCompiler(compiler => {
            GenTree top = new GenTreeLclVar(operandType, 0);
            compiler.impPushOnStack(top, new typeInfo(operandType));
            GenTree? first = null;
            GenTree? second = compiler.gtNewIconNode(TYP_INT, 42);
            var originalSecond = second;
            var signature = new CORINFO_SIG_INFO { numArgs = checked((ushort)argumentCount) };

            GetImmediateOperands(compiler, intrinsic, in signature, ref first, ref second);

            Assert.That(first, Is.EqualTo(expected ? top : null));
            Assert.That(second, Is.SameAs(originalSecond));
            Assert.That(compiler.impStackHeight, Is.EqualTo(1));
            Assert.That(compiler.impStackTop().val, Is.SameAs(top));
        });
    }

    [TestCase(NI_X86Base_Extract, true)]
    [TestCase(NI_AVX2_GatherVector128, true)]
    [TestCase(NI_AVX_Compare, false)]
    public static void ImmediateRangeChecksPreserveFullRangeAndGatherExceptions(
        NamedIntrinsic intrinsic, bool unchanged)
    {
        WithCompiler(compiler => {
            var operand = new GenTreeLclVar(TYP_INT, 0);
            var result = AddRangeCheckIfNeeded(compiler, intrinsic, operand, 0, 31);

            if (unchanged)
            {
                Assert.That(result, Is.SameAs(operand));
            }
            else
            {
                Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(result.AsOp().Op1.Oper, Is.EqualTo(GT_BOUNDS_CHECK));
                Assert.That(result.Flags & GTF_EXCEPT, Is.EqualTo(GTF_EXCEPT));
            }
        });
    }

    [Test]
    public static void ConstantImmediateDoesNotAcquireARuntimeRangeCheck()
    {
        WithCompiler(compiler => {
            var constant = compiler.gtNewIconNode(TYP_INT, 32);
            var result = AddRangeCheckIfNeeded(compiler, NI_AVX_Compare, constant, 0, 31);

            Assert.That(result, Is.SameAs(constant));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.info.compMaxStack = 1;
        compiler.stackState.esStack = new StackEntry[1];
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "impIsTableDrivenHWIntrinsic")]
    private static extern bool IsTableDriven(
        Compiler? _, NamedIntrinsic intrinsic, HWIntrinsicCategory category);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "isSupportedBaseType")]
    private static extern bool IsSupportedBaseType(
        Compiler? _, NamedIntrinsic intrinsic, var_types baseType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "getHWIntrinsicImmOps")]
    private static extern void GetImmediateOperands(
        Compiler compiler, NamedIntrinsic intrinsic, in CORINFO_SIG_INFO signature,
        ref GenTree? first, ref GenTree? second);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "addRangeCheckIfNeeded")]
    private static extern GenTree AddRangeCheckIfNeeded(
        Compiler compiler, NamedIntrinsic intrinsic, GenTree operand, int lower, int upper);
}
#endif
