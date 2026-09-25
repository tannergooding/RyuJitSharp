// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareConstructionLoweringTests
{
    private static readonly var_types[] s_baseTypes = [
        TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT, TYP_LONG, TYP_ULONG, TYP_FLOAT, TYP_DOUBLE,
    ];

    private static IEnumerable<TestCaseData> ConstantCases()
    {
        foreach (var type in s_baseTypes)
        {
            foreach (var size in new byte[] { 16, 32, 64 })
            {
                yield return new TestCaseData(type, size);
            }
        }
    }

    [TestCaseSource(nameof(ConstantCases))]
    public static void ConstantCreateReplacesItsOwningUseWithAFreshVector(var_types baseType, byte size)
    {
        WithCompiler(compiler => {
            var simdType = Compiler.GetSimdTypeForSize(size);
            var args = new GenTree[size / baseType.Size];
            for (var i = 0; i < args.Length; i++)
            {
                args[i] = Constant(compiler, baseType, i + 1);
            }
            var create = compiler.gtNewSimdHWIntrinsicNode(simdType, NI_Vector_Create, baseType, size, args);
            create._vnPair.SetBoth(123);
            var user = new GenTreeUnOp(GT_NEG, simdType, create) { IsUnusedValue = true };
            var block = NewBlock(args);
            block.InsertAtEnd(create);
            block.InsertAtEnd(user);

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.SameAs(user));

            var result = user.Op1.AsVecCon();
            for (var i = 0; i < args.Length; i++)
            {
                AssertLane(result, baseType, i, i + 1);
                Assert.That(args[i].Next, Is.Null);
                Assert.That(args[i].Prev, Is.Null);
            }
            Assert.That(result.Type, Is.EqualTo(simdType));
            Assert.That(result._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(block.FirstNode, Is.SameAs(result));
            Assert.That(create.Next, Is.Null);
            Assert.That(create.Prev, Is.Null);
#if DEBUG
            Assert.That(result.TreeId, Is.Not.EqualTo(create.TreeId));
#endif
            CheckLir(compiler, block);
        });
    }

    [TestCase(NI_Vector_Create, false)]
    [TestCase(NI_Vector_CreateScalar, true)]
    [TestCase(NI_Vector_CreateScalarUnsafe, false)]
    public static void SingleConstantDistinguishesZeroingFromBroadcast(NamedIntrinsic intrinsic, bool zeroUpper)
    {
        WithCompiler(compiler => {
            var scalar = compiler.gtNewIconNode(TYP_INT, -3);
            var create = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, intrinsic, TYP_INT, 16, scalar);
            create.IsUnusedValue = true;
            var block = NewBlock(scalar, create);

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.Null);

            var result = (block.FirstNode ?? throw new InvalidOperationException()).AsVecCon();
            Assert.That(result.IsUnusedValue, Is.True);
            Assert.That(result.SimdVal.i32[0], Is.EqualTo(-3));
            for (var i = 1; i < 4; i++)
            {
                Assert.That(result.SimdVal.i32[i], Is.EqualTo(zeroUpper ? 0 : -3));
            }
            CheckLir(compiler, block);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void FloatingConstantsPreserveSignedZeroAndNaNPayload(bool single)
    {
        WithCompiler(compiler => {
            var type = single ? TYP_FLOAT : TYP_DOUBLE;
            var negativeZero = new GenTreeDblCon(type, -0.0);
            var nan = new GenTreeDblCon(type, single
                ? BitConverter.UInt32BitsToSingle(0x7FC12345)
                : BitConverter.UInt64BitsToDouble(0x7FF8000000012345));
            GenTree[] args = single
                ? [negativeZero, nan, new GenTreeDblCon(type, 1), new GenTreeDblCon(type, -2)]
                : [negativeZero, nan];
            var create = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_Create, type, 16, args);
            create.IsUnusedValue = true;
            var block = NewBlock(args);
            block.InsertAtEnd(create);

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.Null);

            ref var value = ref (block.FirstNode ?? throw new InvalidOperationException()).AsVecCon().SimdVal;
            if (single)
            {
                Assert.That(value.u32[0], Is.EqualTo(0x80000000U));
                Assert.That(value.u32[1], Is.EqualTo(0x7FC12345U));
            }
            else
            {
                Assert.That(value.u64[0], Is.EqualTo(0x8000000000000000UL));
                Assert.That(value.u64[1], Is.EqualTo(0x7FF8000000012345UL));
            }
            CheckLir(compiler, block);
        });
    }

    [TestCase(TYP_UBYTE, 511L, 255UL)]
    [TestCase(TYP_SHORT, 98304L, 32768UL)]
    [TestCase(TYP_UINT, -1L, 4294967295UL)]
    [TestCase(TYP_ULONG, -1L, ulong.MaxValue)]
    public static void ConstantCreatePreservesTruncatedUnsignedLaneBits(var_types type, long input, ulong expected)
    {
        WithCompiler(compiler => {
            var scalar = compiler.gtNewIconNode(type.ActualType, unchecked((nint)input));
            var create = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_Create, type, 16, scalar);
            create.IsUnusedValue = true;
            var block = NewBlock(scalar, create);

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.Null);

            ref var value = ref (block.FirstNode ?? throw new InvalidOperationException()).AsVecCon().SimdVal;
            for (var i = 0; i < 16 / type.Size; i++)
            {
                var actual = type switch {
                    TYP_UBYTE => (ulong)value.u8[i],
                    TYP_SHORT => value.u16[i],
                    TYP_UINT => value.u32[i],
                    TYP_ULONG => value.u64[i],
                    _ => throw new ArgumentOutOfRangeException(nameof(type)),
                };
                Assert.That(actual, Is.EqualTo(expected));
            }
            CheckLir(compiler, block);
        });
    }

    [TestCase(TYP_BYTE, TYP_UBYTE)]
    [TestCase(TYP_UBYTE, TYP_UBYTE)]
    [TestCase(TYP_SHORT, TYP_USHORT)]
    [TestCase(TYP_USHORT, TYP_USHORT)]
    public static void CreateScalarRetypesASafeSmallCast(var_types baseType, var_types unsignedType)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var cast = compiler.gtNewCastNode(TYP_INT, value, false, baseType);
            var create = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_CreateScalar, baseType, 16, cast);
            create.IsUnusedValue = true;
            var block = NewBlock(value, cast, create);

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.Null);

            Assert.That(create.SimdBaseType, Is.EqualTo(TYP_INT));
            Assert.That(create.GetOp(1), Is.SameAs(cast));
            Assert.That(cast.CastType, Is.EqualTo(unsignedType));
            Assert.That(cast.CastOp, Is.SameAs(value));
            CheckLir(compiler, block);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CreateScalarDoesNotRetypeAnOverflowingOrContainedLoadCast(bool containedLoad)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 1);
            var value = compiler.gtNewIndir(TYP_INT, address, GTF_IND_NONFAULTING);
            value.IsContained = containedLoad;
            var cast = compiler.gtNewCastNode(TYP_INT, value, false, TYP_SHORT);
            if (!containedLoad)
            {
                cast.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
            }
            var create = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_CreateScalar, TYP_SHORT, 16, cast);
            create.IsUnusedValue = true;
            var block = NewBlock(address, value, cast, create);

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.Null);

            Assert.That(cast.CastType, Is.EqualTo(TYP_SHORT));
            var zeroExtend = create.GetOp(1).AsCast();
            Assert.That(zeroExtend, Is.Not.SameAs(cast));
            Assert.That(zeroExtend.CastType, Is.EqualTo(TYP_USHORT));
            Assert.That(zeroExtend.CastOp, Is.SameAs(cast));
            Assert.That(cast.Next, Is.SameAs(zeroExtend));
            Assert.That(zeroExtend.Next, Is.SameAs(create));
            CheckLir(compiler, block);
        });
    }

    [TestCase(false, TYP_BYTE)]
    [TestCase(false, TYP_SHORT)]
    [TestCase(true, TYP_BYTE)]
    [TestCase(true, TYP_SHORT)]
    public static void CreateScalarRetypesSmallMemoryLoads(bool field, var_types baseType)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 1);
            GenTree load = field
                ? compiler.gtNewLclFldNode(baseType, 0, 0)
                : compiler.gtNewIndir(baseType, address, GTF_IND_NONFAULTING);
            var create = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_CreateScalar, baseType, 16, load);
            create.IsUnusedValue = true;
            var block = field ? NewBlock(load, create) : NewBlock(address, load, create);

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.Null);

            Assert.That(create.GetOp(1), Is.SameAs(load));
            Assert.That(load.Type, Is.EqualTo(varTypeToUnsigned(baseType)));
            Assert.That(create.SimdBaseType, Is.EqualTo(TYP_INT));
            CheckLir(compiler, block);
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_I_IMPL)]
    public static void WithElementNormalizesOnlyNarrowVariableIndices(var_types indexType)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[1].Type = indexType;
            var vector = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var index = compiler.gtNewLclvNode(indexType, 1);
            var scalar = compiler.gtNewLclvNode(TYP_INT, 2);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_WithElement, TYP_INT, 16, vector, index, scalar);
            node.IsUnusedValue = true;
            var block = NewBlock(vector, index, scalar, node);

            Assert.That(LowerWithElement(NewLowering(compiler, block), node), Is.Null);

            Assert.That(node.HWIntrinsicId, Is.EqualTo(NI_Vector_WithElement));
            Assert.That(node.GetOp(2).Type, Is.EqualTo(TYP_I_IMPL));
            if (indexType == TYP_I_IMPL)
            {
                Assert.That(node.GetOp(2), Is.SameAs(index));
            }
            else
            {
                var cast = node.GetOp(2).AsCast();
                Assert.That(cast.IsUnsigned, Is.True);
                Assert.That(cast.CastOp, Is.SameAs(index));
                Assert.That(index.Next, Is.SameAs(cast));
                Assert.That(cast.Next, Is.SameAs(scalar));
            }
            CheckLir(compiler, block);
        });
    }

    [TestCase(TYP_FLOAT)]
    [TestCase(TYP_DOUBLE)]
    public static void ScalarUnsafeKeepsNonconstantFloatingOperands(var_types type)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = type;
            var scalar = compiler.gtNewLclvNode(type, 0);
            var block = NewBlock(scalar);

            Assert.That(InsertScalarUnsafe(NewLowering(compiler, block), TYP_SIMD16, scalar, type, 16), Is.SameAs(scalar));
            Assert.That(block.FirstNode, Is.SameAs(scalar));
            Assert.That(block.LastNode, Is.SameAs(scalar));
        });
    }

    [TestCase(TYP_INT)]
    [TestCase(TYP_DOUBLE)]
    public static void ScalarUnsafeReplacesConstantsWithBroadcasts(var_types type)
    {
        WithCompiler(compiler => {
            var scalar = Constant(compiler, type, 7);
            var tail = new GenTree(GT_NO_OP, TYP_VOID);
            var block = NewBlock(scalar, tail);

            var result = InsertScalarUnsafe(NewLowering(compiler, block), TYP_SIMD16, scalar, type, 16);

            Assert.That(result.Oper, Is.EqualTo(GT_CNS_VEC));
            Assert.That(result.Next, Is.SameAs(tail));
            Assert.That(block.FirstNode, Is.SameAs(result));
            Assert.That(scalar.Next, Is.Null);
            Assert.That(scalar.Prev, Is.Null);
            for (var i = 0; i < 16 / type.Size; i++)
            {
                AssertLane(result.AsVecCon(), type, i, 7);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LocalMaterializationPreservesTheNativeFastPathAndExplicitTemp(bool explicitTemp)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, value) { IsUnusedValue = true };
            var block = NewBlock(value, user);
            var use = new LIR.Use(block, ref user.Op1Ref, user);
            var count = compiler.lvaCount;

            var local = Materialize(NewLowering(compiler, block), use, explicitTemp ? 2 : BAD_VAR_NUM);

            Assert.That(compiler.lvaCount, Is.EqualTo(count));
            Assert.That(user.Op1, Is.SameAs(local));
            Assert.That(local.LclNum, Is.EqualTo(explicitTemp ? 2 : 0));
            if (explicitTemp)
            {
                var store = (value.Next ?? throw new InvalidOperationException()).AsLclVar();
                Assert.That(store.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(store.LclNum, Is.EqualTo(2));
                Assert.That(store.Data, Is.SameAs(value));
                Assert.That(store.Next, Is.SameAs(local));
                Assert.That(local.Next, Is.SameAs(user));
            }
            else
            {
                Assert.That(local, Is.SameAs(value));
                Assert.That(value.Next, Is.SameAs(user));
            }
            CheckLir(compiler, block);
        });
    }

    [Test]
    public static void LastNodeUsesExecutionOrderIncludingDuplicatesAndTheRangeEnd()
    {
        WithCompiler(compiler => {
            GenTree[] nodes = [
                compiler.gtNewIconNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 1),
                compiler.gtNewIconNode(TYP_INT, 2), compiler.gtNewIconNode(TYP_INT, 3),
                compiler.gtNewIconNode(TYP_INT, 4),
            ];
            _ = NewBlock(nodes);
            for (var i = 0; i < nodes.Length; i++)
            {
                for (var j = 0; j < nodes.Length; j++)
                {
                    Assert.That(LIR.LastNode(nodes[i], nodes[j]), Is.SameAs(nodes[Math.Max(i, j)]));
                }
            }
            Assert.That(LIR.LastNode([nodes[3], nodes[0], nodes[2], nodes[3]]), Is.SameAs(nodes[3]));
            Assert.That(LIR.LastNode([nodes[4], nodes[1], nodes[0]]), Is.SameAs(nodes[4]));
            Assert.That(LIR.LastNode([nodes[2]]), Is.SameAs(nodes[2]));
        });
    }

    // These are normal executable regressions, not ignored tests. Their integration dependency
    // is the complete recursive hardware dispatcher supplied separately from this packet.
    [Category("HardwareRecursiveDispatch")]
    [TestCase(TYP_BYTE, 0, NI_X86Base_Shuffle)]
    [TestCase(TYP_SHORT, 0, NI_X86Base_Shuffle)]
    [TestCase(TYP_INT, 0, NI_X86Base_Shuffle)]
    [TestCase(TYP_FLOAT, 0, NI_X86Base_Shuffle)]
    [TestCase(TYP_FLOAT, 1, NI_AVX_Permute)]
    [TestCase(TYP_LONG, 0, NI_X86Base_MoveAndDuplicate)]
    [TestCase(TYP_DOUBLE, 2, NI_X86Base_MoveAndDuplicate)]
    [TestCase(TYP_INT, 2, NI_AVX2_BroadcastScalarToVector128)]
    public static void BroadcastSelectsTheNativeIsaAndBaseTypePath(var_types type, int isaLevel, NamedIntrinsic expected)
    {
        WithCompiler(compiler => {
            EnableIsa(compiler, InstructionSet_X86Base);
            if (isaLevel >= 1)
            {
                EnableIsa(compiler, InstructionSet_AVX);
            }
            if (isaLevel >= 2)
            {
                EnableIsa(compiler, InstructionSet_AVX2);
            }
            compiler.lvaTable[0].Type = type.ActualType;
            var scalar = compiler.gtNewLclvNode(type.ActualType, 0);
            var create = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_Create, type, 16, scalar);
            create.IsUnusedValue = true;
            var block = NewBlock(scalar, create);
#if DEBUG
            var treeId = create.TreeId;
#endif

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.Null);

            Assert.That(create.HWIntrinsicId, Is.EqualTo(expected));
#if DEBUG
            Assert.That(create.TreeId, Is.EqualTo(treeId));
#endif
            CheckLir(compiler, block);
        });
    }

    [Category("HardwareRecursiveDispatch")]
    [TestCase(TYP_INT, (byte)16)]
    [TestCase(TYP_FLOAT, (byte)16)]
    [TestCase(TYP_INT, (byte)32)]
    [TestCase(TYP_DOUBLE, (byte)64)]
    public static void MultiOperandCreatePreservesReorderedOperandOwnership(var_types type, byte size)
    {
        WithCompiler(compiler => {
            EnableIsa(compiler, InstructionSet_X86Base);
            EnableIsa(compiler, InstructionSet_X86Base_X64);
            EnableIsa(compiler, InstructionSet_AVX);
            EnableIsa(compiler, InstructionSet_AVX2);
            EnableIsa(compiler, InstructionSet_AVX512);
            var simdType = Compiler.GetSimdTypeForSize(size);
            var count = size / type.Size;
            var args = new GenTree[count];
            for (var i = 0; i < count; i++)
            {
                compiler.lvaTable[i].Type = type.ActualType;
                args[i] = compiler.gtNewLclvNode(type.ActualType, i);
            }
            var block = NewBlock();
            for (var i = count - 2; i >= 0; i--)
            {
                block.InsertAtEnd(args[i]);
            }
            block.InsertAtEnd(args[^1]);
            var create = compiler.gtNewSimdHWIntrinsicNode(simdType, NI_Vector_Create, type, size, args);
            var user = new GenTreeUnOp(GT_NEG, simdType, create) { IsUnusedValue = true };
            block.InsertAtEnd(create);
            block.InsertAtEnd(user);

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.SameAs(user));

            Assert.That(user.Op1, Is.SameAs(create));
            Assert.That(create.HWIntrinsicId, Is.Not.EqualTo(NI_Vector_Create));
            for (var i = 0; i < count; i++)
            {
                Assert.That(block.TryGetUse(args[i], out var use), Is.True);
                Assert.That(use.Def(), Is.SameAs(args[i]));
            }
            CheckLir(compiler, block);
        });
    }

    [Category("HardwareRecursiveDispatch")]
    [TestCase(TYP_FLOAT, (byte)32, false, NI_AVX_InsertVector128)]
    [TestCase(TYP_INT, (byte)32, true, NI_AVX2_BroadcastScalarToVector256)]
    [TestCase(TYP_FLOAT, (byte)64, true, NI_AVX512_BroadcastScalarToVector512)]
    public static void WideBroadcastHandlesAvxOnlyAndDirectBroadcasts(var_types type, byte size, bool avx2,
        NamedIntrinsic expected)
    {
        WithCompiler(compiler => {
            EnableIsa(compiler, InstructionSet_X86Base);
            EnableIsa(compiler, InstructionSet_AVX);
            if (avx2)
            {
                EnableIsa(compiler, InstructionSet_AVX2);
            }
            if (size == 64)
            {
                EnableIsa(compiler, InstructionSet_AVX512);
            }
            compiler.lvaTable[0].Type = type;
            var scalar = compiler.gtNewLclvNode(type, 0);
            var create = compiler.gtNewSimdHWIntrinsicNode(Compiler.GetSimdTypeForSize(size),
                NI_Vector_Create, type, size, scalar);
            create.IsUnusedValue = true;
            var block = NewBlock(scalar, create);

            Assert.That(LowerCreate(NewLowering(compiler, block), create), Is.Null);

            Assert.That(create.HWIntrinsicId, Is.EqualTo(expected));
            CheckLir(compiler, block);
        });
    }

    [Category("HardwareRecursiveDispatch")]
    [TestCase(TYP_BYTE, -1, NI_X86Base_Insert, 15)]
    [TestCase(TYP_USHORT, 258, NI_X86Base_Insert, 2)]
    [TestCase(TYP_INT, 7, NI_X86Base_Insert, 3)]
    [TestCase(TYP_ULONG, 3, NI_X86Base_X64_Insert, 1)]
    [TestCase(TYP_FLOAT, 2, NI_X86Base_Insert, 32)]
    [TestCase(TYP_DOUBLE, 0, NI_X86Base_MoveScalar, -1)]
    [TestCase(TYP_DOUBLE, 1, NI_X86Base_UnpackLow, -1)]
    public static void WithElementUsesTruncatedAndMaskedImmediates(var_types type, int indexValue,
        NamedIntrinsic expected, int immediate)
    {
        WithCompiler(compiler => {
            EnableIsa(compiler, InstructionSet_X86Base);
            EnableIsa(compiler, InstructionSet_X86Base_X64);
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[1].Type = type.ActualType;
            var vector = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var index = compiler.gtNewIconNode(TYP_INT, indexValue);
            var value = compiler.gtNewLclvNode(type.ActualType, 1);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_WithElement, type, 16, vector, index, value);
            node.IsUnusedValue = true;
            var block = NewBlock(vector, index, value, node);

            Assert.That(LowerWithElement(NewLowering(compiler, block), node), Is.Null);

            Assert.That(node.HWIntrinsicId, Is.EqualTo(expected));
            Assert.That(index.Next, Is.Null);
            Assert.That(index.Prev, Is.Null);
            if (immediate >= 0)
            {
                Assert.That(node.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)immediate));
            }
            CheckLir(compiler, block);
        });
    }

    [Category("HardwareRecursiveDispatch")]
    [TestCase((byte)32, 0)]
    [TestCase((byte)32, 6)]
    [TestCase((byte)64, 0)]
    [TestCase((byte)64, 5)]
    [TestCase((byte)64, 10)]
    [TestCase((byte)64, 15)]
    public static void WideWithElementExtractsAndReinsertsTheCorrectLane(byte size, int indexValue)
    {
        WithCompiler(compiler => {
            EnableIsa(compiler, InstructionSet_X86Base);
            EnableIsa(compiler, InstructionSet_AVX);
            EnableIsa(compiler, InstructionSet_AVX2);
            EnableIsa(compiler, InstructionSet_AVX512);
            var type = Compiler.GetSimdTypeForSize(size);
            compiler.lvaTable[0].Type = type;
            var source = compiler.gtNewLclvNode(type, 0);
            var index = compiler.gtNewIconNode(TYP_INT, indexValue);
            var value = compiler.gtNewLclvNode(TYP_INT, 2);
            var node = compiler.gtNewSimdHWIntrinsicNode(type, NI_Vector_WithElement, TYP_INT, size, source, index, value);
            var user = new GenTreeUnOp(GT_NEG, type, node) { IsUnusedValue = true };
            var block = NewBlock(source, index, value, node, user);

            Assert.That(LowerWithElement(NewLowering(compiler, block), node), Is.SameAs(user));

            Assert.That(user.Op1, Is.SameAs(node));
            Assert.That(node.GetOp(1), Is.SameAs(source));
            var lane = node.GetOp(2).AsHWIntrinsic();
            Assert.That(lane.HWIntrinsicId, Is.EqualTo(NI_X86Base_Insert));
            Assert.That(lane.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)(indexValue % 4)));
            Assert.That(node.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)(indexValue / 4)));
            Assert.That(node.HWIntrinsicId, Is.EqualTo(size == 64 ? NI_AVX512_InsertVector128 : NI_AVX2_InsertVector128));
            Assert.That(index.Next, Is.Null);
            CheckLir(compiler, block);
        });
    }

    private static GenTree Constant(Compiler compiler, var_types type, int value)
    {
        return varTypeIsFloating(type)
            ? new GenTreeDblCon(type, value)
            : compiler.gtNewIconNode(type.ActualType, value);
    }

    private static void AssertLane(GenTreeVecCon vector, var_types type, int index, int value)
    {
        var actual = type switch {
            TYP_BYTE or TYP_UBYTE => (double)vector.SimdVal.i8[index],
            TYP_SHORT or TYP_USHORT => vector.SimdVal.i16[index],
            TYP_INT or TYP_UINT => vector.SimdVal.i32[index],
            TYP_LONG or TYP_ULONG => vector.SimdVal.i64[index],
            TYP_FLOAT => vector.SimdVal.f32[index],
            TYP_DOUBLE => vector.SimdVal.f64[index],
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        Assert.That(actual, Is.EqualTo((double)value));
    }

    private static BasicBlock NewBlock(params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }

        return block;
    }

    private static void CheckLir(Compiler compiler, BasicBlock block)
    {
#if DEBUG
        Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
    }

    private static Lowering NewLowering(Compiler compiler, BasicBlock block)
    {
        var lowering = new Lowering(compiler, new LinearScan(compiler));
        LoweringBlock(lowering) = block;

        return lowering;
    }

    private static void EnableIsa(Compiler compiler, CORINFO_InstructionSet isa)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(isa);
        compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicCreate")]
    private static extern GenTree? LowerCreate(Lowering lowering, GenTreeHWIntrinsic node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicWithElement")]
    private static extern GenTree? LowerWithElement(Lowering lowering, GenTreeHWIntrinsic node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InsertNewSimdCreateScalarUnsafeNode")]
    private static extern GenTree InsertScalarUnsafe(Lowering lowering, var_types type, GenTree value, var_types baseType, byte size);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReplaceWithLclVar")]
    private static extern GenTreeLclVar Materialize(Lowering lowering, LIR.Use use, int tempNum);

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = new LclVarDsc[16];
        compiler.lvaCount = compiler.lvaTable.Length;
        for (var i = 0; i < compiler.lvaCount; i++)
        {
            compiler.lvaTable[i] = new LclVarDsc { Type = TYP_INT };
        }
        compiler.lvaTable[1].Type = TYP_I_IMPL;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
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
