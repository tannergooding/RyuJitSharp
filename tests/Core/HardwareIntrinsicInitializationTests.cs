// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareIntrinsicInitializationTests
{
    [TestCase(NI_X86Base_Add, 2, GTF_EMPTY, false)]
    [TestCase(NI_X86Base_LoadAlignedVector128, 1, GTF_GLOB_REF | GTF_EXCEPT, false)]
    [TestCase(NI_X86Base_StoreAligned, 2, GTF_ASG | GTF_GLOB_REF | GTF_EXCEPT, true)]
    [TestCase(NI_X86Base_LoadFence, 0, GTF_ASG | GTF_GLOB_REF, true)]
    [TestCase(NI_X86Base_MemoryFence, 0, GTF_ASG | GTF_GLOB_REF, true)]
    [TestCase(NI_X86Base_StoreFence, 0, GTF_ASG | GTF_GLOB_REF, true)]
    [TestCase(NI_X86Serialize_Serialize, 0, GTF_ASG | GTF_GLOB_REF, true)]
    [TestCase(NI_X86Base_Pause, 0, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_X86Base_Prefetch0, 1, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_X86Base_Prefetch1, 1, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_X86Base_Prefetch2, 1, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_X86Base_PrefetchNonTemporal, 1, GTF_CALL | GTF_GLOB_REF, false)]
    [TestCase(NI_Vector_op_Division, 2, GTF_EXCEPT, false)]
    public static void ConstructorSetsNativeIdentityAndEffects(NamedIntrinsic id, int argCount, GenTreeFlags effects, bool storeOrBarrier)
    {
        WithCompiler(_ => {
            var operands = new GenTree[argCount];
            for (var index = 0; index < operands.Length; index++)
            {
                var address = (id is NI_X86Base_LoadAlignedVector128 or NI_X86Base_StoreAligned or
                    NI_X86Base_Prefetch0 or NI_X86Base_Prefetch1 or NI_X86Base_Prefetch2 or NI_X86Base_PrefetchNonTemporal) && index == 0;
                operands[index] = new GenTreeLclVar(address ? TYP_BYREF : TYP_SIMD16, index);
            }
            var vectorResult = id is NI_X86Base_Add or NI_X86Base_LoadAlignedVector128 or NI_Vector_op_Division;
            var vectorSize = (vectorResult || id is NI_X86Base_StoreAligned) ? (byte)16 : (byte)0;
            var intrinsic = new GenTreeHWIntrinsic(vectorResult ? TYP_SIMD16 : TYP_VOID, id, TYP_INT, vectorSize, operands);
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(id));
            Assert.That(intrinsic.Flags & GTF_ALL_EFFECT, Is.EqualTo(effects));
            Assert.That(intrinsic.IsMemoryStoreOrBarrier, Is.EqualTo(storeOrBarrier));
            Assert.That(intrinsic.RequiresCallFlag(), Is.EqualTo((effects & GTF_CALL) != 0));
        });
    }

    [TestCase(TYP_BYTE, TYP_INT)]
    [TestCase(TYP_UBYTE, TYP_UINT)]
    [TestCase(TYP_SHORT, TYP_INT)]
    [TestCase(TYP_USHORT, TYP_UINT)]
    [TestCase(TYP_INT, TYP_INT)]
    [TestCase(TYP_FLOAT, TYP_FLOAT)]
    public static void LoadConstructionNormalizesOnlySmallBaseTypes(var_types input, var_types expected)
    {
        WithCompiler(_ => {
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_LoadAlignedVector128, input, 16,
                new GenTreeLclVar(TYP_BYREF, 0));
            Assert.That(intrinsic.SimdBaseType, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void VariableArityAndInheritedEffectsArePreserved()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_INT, gtCallTypes.CT_USER_FUNC, null);
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_INT, 16, call);
            Assert.That(HWIntrinsicInfo.lookupNumArgs(NI_Vector_Create), Is.EqualTo(-1));
            Assert.That(HWIntrinsicInfo.lookupNumArgs(NI_X86Base_Add), Is.EqualTo(2));
            Assert.That(intrinsic.Flags & GTF_ALL_EFFECT, Is.EqualTo(call.Flags & GTF_ALL_EFFECT));
            Assert.That(intrinsic.RequiresCallFlag(), Is.False);
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(NI_Vector_Create));
            Assert.That(HWIntrinsicInfo.IsInvalidNodeId(NI_Vector_op_Addition), Is.True);
            Assert.That(HWIntrinsicInfo.IsInvalidNodeId(NI_X86Base_Add), Is.False);
        });
    }

    [Test]
    public static void ChangingIdentityNormalizesTypesWithoutReinitializingEffects()
    {
        WithCompiler(_ => {
            var intrinsic = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_CreateScalarUnsafe, TYP_UBYTE, 16,
                new GenTreeLclVar(TYP_INT, 0));
            var effects = intrinsic.Flags & GTF_ALL_EFFECT;
            intrinsic.SetOp(1, new GenTreeLclVar(TYP_BYREF, 1));
            intrinsic.SetHWIntrinsicId(NI_X86Base_LoadAlignedVector128);
            Assert.That(intrinsic.HWIntrinsicId, Is.EqualTo(NI_X86Base_LoadAlignedVector128));
            Assert.That(intrinsic.SimdBaseType, Is.EqualTo(TYP_UINT));
            Assert.That(intrinsic.Flags & GTF_ALL_EFFECT, Is.EqualTo(effects));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
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
}
