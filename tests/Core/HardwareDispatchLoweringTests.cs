// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_XARCH
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareDispatchLoweringTests
{
    [TestCase(NI_AVX512_Add, 2, false)]
    [TestCase(NI_AVX512_Add, 3, true)]
    [TestCase(NI_AVX512_FusedMultiplyAdd, 3, false)]
    [TestCase(NI_AVX512_FusedMultiplyAdd, 4, true)]
    [TestCase(NI_AVX512_Sqrt, 1, false)]
    [TestCase(NI_AVX512_Sqrt, 2, true)]
    [TestCase(NI_AVX512_AddScalar, 3, true)]
    [TestCase(NI_X86Base_Add, 2, false)]
    public static void EmbeddedRoundingUsesTheNativeOperandArity(NamedIntrinsic id, int count, bool rounding)
    {
        WithCompiler(compiler => {
            var operands = new GenTree[count];
            for (var index = 0; index < count; index++)
            {
                operands[index] = new GenTreeLclVar(TYP_SIMD16, 0);
            }
            if (rounding)
            {
                operands[^1] = compiler.gtNewIconNode(TYP_INT, 0);
            }

            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, id, TYP_FLOAT, 16, operands);
            Assert.That(node.IsEmbeddedRoundingEnabled, Is.EqualTo(rounding));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RoundingBypassesOrdinaryContainmentAndContainsOnlyConstants(bool constant)
    {
        WithCompiler(compiler => {
            var left = new GenTreeLclVar(TYP_SIMD16, 0);
            var right = new GenTreeLclVar(TYP_SIMD16, 1);
            GenTree rounding = constant ? compiler.gtNewIconNode(TYP_INT, 0) : new GenTreeLclVar(TYP_INT, 2);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_AVX512_Add, TYP_FLOAT, 16, left, right, rounding);
            var user = new GenTreeUnOp(GT_RETURN, TYP_SIMD16, node);
            var block = NewBlock(left, right, rounding, node, user);

            Assert.That(LowerIntrinsic(NewLowering(compiler, block), node), Is.SameAs(user));
            Assert.That(rounding.IsContained, Is.EqualTo(constant));
            Assert.That(left.IsContained, Is.False);
            Assert.That(right.IsContained, Is.False);
            Assert.That(node.Operands.Length, Is.EqualTo(3));
        });
    }

    [TestCase(TYP_FLOAT, true, true)]
    [TestCase(TYP_DOUBLE, true, true)]
    [TestCase(TYP_FLOAT, false, false)]
    [TestCase(TYP_INT, true, false)]
    public static void ScalarConstructionElisionPreservesAbiAndOperandTypes(
        var_types baseType, bool hardwareConsumer, bool elided)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[2].Type = baseType;
            var scalar = new GenTreeLclVar(baseType, 2);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_CreateScalarUnsafe, baseType, 16, scalar);
            GenTree user = hardwareConsumer
                ? compiler.gtNewSimdHWIntrinsicNode(baseType, NI_Vector_ToScalar, baseType, 16, node)
                : new GenTreeUnOp(GT_RETURN, TYP_SIMD16, node);
            var block = NewBlock(scalar, node, user);

            Assert.That(LowerIntrinsic(NewLowering(compiler, block), node), Is.SameAs(user));
            var operand = hardwareConsumer ? user.AsHWIntrinsic().GetOp(1) : user.AsUnOp().Op1;
            Assert.That(operand, Is.SameAs(elided ? scalar : node));
            Assert.That(scalar.Type, Is.EqualTo(baseType));
            Assert.That(node.Next, Is.SameAs(elided ? null : user));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void VectorWideningIsElidedExceptForGatherIndices(bool gather)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[1].Type = TYP_SIMD32;
            compiler.lvaTable[2].Type = TYP_I_IMPL;
            var input = new GenTreeLclVar(TYP_SIMD16, 0);
            var node = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD32, NI_Vector_ToVector256Unsafe, TYP_INT, 32, input);
            var address = new GenTreeLclVar(TYP_I_IMPL, 2);
            var scale = compiler.gtNewIconNode(TYP_INT, 4);
            var other = new GenTreeLclVar(TYP_SIMD32, 1);
            var user = gather
                ? compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD32, NI_AVX2_GatherVector256, TYP_FLOAT, 32, address, node, scale)
                : compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD32, NI_AVX_Add, TYP_FLOAT, 32, node, other);
            user.AuxiliaryType = gather ? TYP_INT : TYP_UNKNOWN;
            var block = gather ? NewBlock(input, node, address, scale, user) : NewBlock(input, node, other, user);
            var next = node.Next;

            Assert.That(LowerIntrinsic(NewLowering(compiler, block), node), Is.SameAs(next));
            Assert.That(user.GetOp(gather ? 2 : 1), Is.SameAs(gather ? node : input));
            Assert.That(input.Type, Is.EqualTo(TYP_SIMD16));
            Assert.That(node.Next, Is.SameAs(gather ? next : null));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NotAndFusionPreservesTheComplementedOperand(bool firstOperand)
    {
        WithCompiler(compiler => {
            var input = new GenTreeLclVar(TYP_SIMD16, 0);
            var ones = AllBitsSet();
            var complement = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_Xor, TYP_INT, 16, input, ones);
            var other = new GenTreeLclVar(TYP_SIMD16, 1);
            var user = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_And, TYP_INT, 16,
                firstOperand ? complement : other, firstOperand ? other : complement);
            var block = NewBlock(input, ones, complement, other, user);

            Assert.That(LowerIntrinsic(NewLowering(compiler, block), complement), Is.SameAs(other));
            Assert.That(user.HWIntrinsicId, Is.EqualTo(NI_X86Base_AndNot));
            Assert.That(user.GetOp(1), Is.SameAs(input));
            Assert.That(user.GetOp(2), Is.SameAs(other));
            Assert.That(input.Next, Is.SameAs(other));
            Assert.That(complement.Next, Is.Null);
            Assert.That(ones.Next, Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void DoubleNotRemovalRepairsUsesAndTheSavedContinuation(bool hasUser, bool interveningNode)
    {
        WithCompiler(compiler => {
            EnableIsa(compiler, InstructionSet_AVX512);
            var input = new GenTreeLclVar(TYP_SIMD16, 0);
            var firstOnes = AllBitsSet();
            var first = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_Xor, TYP_INT, 16, input, firstOnes);
            var secondOnes = AllBitsSet();
            var second = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_Xor, TYP_INT, 16, first, secondOnes);
            var block = NewBlock(input, firstOnes, first);
            var intervening = interveningNode ? new GenTree(GT_NOP, TYP_VOID) : null;
            if (intervening is not null)
            {
                block.InsertAtEnd(intervening);
            }
            block.InsertAtEnd(secondOnes);
            block.InsertAtEnd(second);
            var user = hasUser ? new GenTreeUnOp(GT_RETURN, TYP_SIMD16, second) : null;
            if (user is not null)
            {
                block.InsertAtEnd(user);
            }
            else
            {
                second.IsUnusedValue = true;
            }

            Assert.That(LowerIntrinsic(NewLowering(compiler, block), first), Is.SameAs(intervening ?? user));
            Assert.That(input.IsUnusedValue, Is.EqualTo(!hasUser));
            if (user is not null)
            {
                Assert.That(user.Op1, Is.SameAs(input));
            }
            Assert.That(input.Next, Is.SameAs(intervening ?? user));
            Assert.That(first.Next, Is.Null);
            Assert.That(second.Next, Is.Null);
            Assert.That(firstOnes.Next, Is.Null);
            Assert.That(secondOnes.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    private static GenTreeVecCon AllBitsSet()
    {
        var node = new GenTreeVecCon(TYP_SIMD16);
        node.SimdVal.u64[0] = ulong.MaxValue;
        node.SimdVal.u64[1] = ulong.MaxValue;

        return node;
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsic")]
    private static extern GenTree? LowerIntrinsic(Lowering lowering, GenTreeHWIntrinsic node);

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
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new() { Type = TYP_SIMD16 }, new() { Type = TYP_SIMD16 }, new() { Type = TYP_INT }];
        compiler.lvaCount = compiler.lvaTable.Length;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        EnableIsa(compiler, InstructionSet_X86Base);
        EnableIsa(compiler, InstructionSet_AVX);
        EnableIsa(compiler, InstructionSet_AVX2);
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
#endif
