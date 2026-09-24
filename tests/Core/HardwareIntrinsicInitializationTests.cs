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
    [TestCase(NI_Vector_Create, 8)]
    [TestCase(NI_Vector_Create, 12)]
    [TestCase(NI_Vector_Create, 16)]
    [TestCase(NI_Vector_Create, 32)]
    [TestCase(NI_Vector_Create, 64)]
    [TestCase(NI_Vector_CreateScalar, 16)]
    [TestCase(NI_Vector_CreateScalarUnsafe, 16)]
    public static void CreationConstantsPreserveScalarAndUpperLanePolicy(NamedIntrinsic id, byte size)
    {
        WithCompiler(compiler => {
            var type = size switch {
                8 => TYP_SIMD8,
                12 => TYP_SIMD12,
                16 => TYP_SIMD16,
                32 => TYP_SIMD32,
                64 => TYP_SIMD64,
                _ => throw new ArgumentOutOfRangeException(nameof(size)),
            };
            var node = new GenTreeHWIntrinsic(type, id, TYP_FLOAT, size, compiler.gtNewDconNode(TYP_FLOAT, -0.0));
            var value = simd64_t.AllBitsSet;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.True);
            for (var index = 0; index < 16; index++)
            {
                var filled = (index < size / sizeof(float)) && ((index == 0) || (id != NI_Vector_CreateScalar));
                Assert.That(value.i32[index], Is.EqualTo(filled ? int.MinValue : 0));
            }
        });
    }

    [TestCase(TYP_BYTE, 0x180L, 0x80UL)]
    [TestCase(TYP_UBYTE, -1L, 0xFFUL)]
    [TestCase(TYP_SHORT, 0x18000L, 0x8000UL)]
    [TestCase(TYP_USHORT, -1L, 0xFFFFUL)]
    [TestCase(TYP_INT, 0x180000000L, 0x80000000UL)]
    [TestCase(TYP_UINT, -1L, 0xFFFFFFFFUL)]
    [TestCase(TYP_LONG, long.MinValue, 0x8000000000000000UL)]
    [TestCase(TYP_ULONG, -1L, ulong.MaxValue)]
    public static void IntegralCreationTruncatesLaneBits(var_types type, long input, ulong expected)
    {
        WithCompiler(compiler => {
            var constant = compiler.gtNewIconNode(type is TYP_LONG or TYP_ULONG ? TYP_LONG : Globals.TYP_I_IMPL, (nint)input);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, type, 16, constant);
            simd64_t value = default;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.True);
            for (var index = 0; index < 16; index++)
            {
                var shift = index % type.Size * 8;
                Assert.That(value.u8[index], Is.EqualTo((byte)(expected >> shift)));
            }
        });
    }

    [Test]
    public static void CreationConstantsRetainLaneOrderAndNaNPayloads()
    {
        WithCompiler(compiler => {
            const long nanBits = unchecked((long)0xFFF8000000000123);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_DOUBLE, 16,
                compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(nanBits)),
                compiler.gtNewDconNode(TYP_DOUBLE, -0.0));
            simd64_t value = default;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.True);
            Assert.That(value.i64[0], Is.EqualTo(nanBits));
            Assert.That(value.i64[1], Is.EqualTo(long.MinValue));
        });
    }

    [Test]
    public static void CreationConstantsRoundSinglesAndLeaveUnknownLanesZero()
    {
        WithCompiler(compiler => {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_Create, TYP_FLOAT, 16,
                compiler.gtNewDconNode(TYP_FLOAT, 16777217.0), compiler.gtNewLclvNode(TYP_FLOAT, 0),
                compiler.gtNewDconNode(TYP_FLOAT, -0.0), compiler.gtNewDconNode(TYP_FLOAT, 42.0));
            var value = simd64_t.AllBitsSet;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.False);
            Assert.That(value.f32[0], Is.EqualTo(16777216.0f));
            Assert.That(value.i32[1], Is.Zero);
            Assert.That(value.i32[2], Is.EqualTo(int.MinValue));
            Assert.That(value.f32[3], Is.EqualTo(42.0f));
            Assert.That(value.i64[2], Is.Zero);
        });
    }

    [Test]
    public static void NonCreationDoesNotOverwriteTheOutput()
    {
        WithCompiler(_ => {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_Add, TYP_INT, 16,
                new GenTreeLclVar(TYP_SIMD16, 0), new GenTreeLclVar(TYP_SIMD16, 1));
            var value = simd64_t.AllBitsSet;
            Assert.That(GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref value), Is.False);
            Assert.That(value.IsAllBitsSet, Is.True);
        });
    }

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
