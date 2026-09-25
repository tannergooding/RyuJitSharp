// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareContainmentDispatchTests
{
    [Test]
    public static void BinaryLoadContainsItsAddressRatherThanItsMask()
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var mask = new GenTreeVecCon(TYP_SIMD16);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX_MaskLoad,
                TYP_FLOAT, 16, address, mask);
            var block = NewBlock(address, mask, node);

            ContainCheckHWIntrinsic(NewLowering(compiler, block), node);

            Assert.That(address.IsContained, Is.True);
            Assert.That(node.GetOp(1), Is.SameAs(address));
        });
    }

    [Test]
    public static void UnaryBroadcastRemovesScalarWrapperWithoutDiscardingTheScalar()
    {
        WithCompiler(compiler => {
            var scalar = compiler.gtNewIconNode(TYP_INT, 42);
            var create = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_CreateScalar,
                TYP_INT, 16, scalar);
            var broadcast = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX2_BroadcastScalarToVector128,
                TYP_INT, 16, create);
            var block = NewBlock(scalar, create, broadcast);

            ContainCheckHWIntrinsic(NewLowering(compiler, block), broadcast);

            Assert.That(broadcast.GetOp(1), Is.SameAs(scalar));
            Assert.That(create.Prev, Is.Null);
            Assert.That(create.Next, Is.Null);
            Assert.That(scalar.Next, Is.SameAs(broadcast));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public static void ImmediateFallbackDoesNotContainTheVectorSource(bool constantImmediate)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[1].Type = TYP_INT;
            compiler.lvaCount = 2;
            var value = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var immediate = constantImmediate
                ? (GenTree)compiler.gtNewIconNode(TYP_INT, 0x1B)
                : compiler.gtNewLclvNode(TYP_INT, 1);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_ShuffleHigh,
                TYP_SHORT, 16, value, immediate);
            var block = NewBlock(value, immediate, node);

            ContainCheckHWIntrinsic(NewLowering(compiler, block), node);

            Assert.That(immediate.IsContained, Is.EqualTo(constantImmediate));
            Assert.That(value.IsContained || value.IsRegOptional, Is.EqualTo(constantImmediate));
        });
    }

    [Test]
    public static void TernaryLoadContainsZeroMergeAndAddress()
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var mask = new GenTreeVecCon(TYP_SIMD16);
            var zero = new GenTreeVecCon(TYP_SIMD16);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_MaskLoadMask,
                TYP_FLOAT, 16, address, mask, zero);
            var block = NewBlock(address, mask, zero, node);

            ContainCheckHWIntrinsic(NewLowering(compiler, block), node);

            Assert.That(address.IsContained, Is.True);
            Assert.That(zero.IsContained, Is.True);
        });
    }

    [TestCase(0xAA)]
    [TestCase(0x88)]
    [TestCase(0xCA)]
    public static void TernaryControlByteRetainsItsLogicalFunction(byte control)
    {
        WithCompiler(compiler => {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            var first = new GenTreeVecCon(TYP_SIMD16);
            var second = new GenTreeVecCon(TYP_SIMD16);
            var third = new GenTreeVecCon(TYP_SIMD16);
            var immediate = compiler.gtNewIconNode(TYP_INT, control);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_TernaryLogic,
                TYP_INT, 16, first, second, third, immediate);
            var block = NewBlock(first, second, third, immediate, node);

            ContainCheckHWIntrinsic(NewLowering(compiler, block), node);

            Assert.That(immediate.IsContained, Is.True);
            byte OperandMask(GenTree operand)
            {
                return ReferenceEquals(operand, first) ? (byte)0xF0 :
                    ReferenceEquals(operand, second) ? (byte)0xCC : (byte)0xAA;
            }
            var actualControl = TernaryLogicInfo.GetTernaryControlByte(
                TernaryLogicInfo.Lookup(unchecked((byte)immediate.IconValue)),
                OperandMask(node.GetOp(1)), OperandMask(node.GetOp(2)), OperandMask(node.GetOp(3)));
            Assert.That(actualControl, Is.EqualTo(control));
            if (control is 0xAA)
            {
                Assert.That(first.IsContained && second.IsContained, Is.True);
            }
            else if (control is 0x88)
            {
                Assert.That(first.IsContained, Is.True);
            }
        });
    }

    [Test]
    public static void TernaryFallbackDoesNotContainARegisterImmediate()
    {
        WithCompiler(compiler => {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            var first = new GenTreeVecCon(TYP_SIMD16);
            var second = new GenTreeVecCon(TYP_SIMD16);
            var third = new GenTreeVecCon(TYP_SIMD16);
            var control = compiler.gtNewLclvNode(TYP_INT, 0);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_TernaryLogic,
                TYP_INT, 16, first, second, third, control);
            var block = NewBlock(first, second, third, control, node);

            ContainCheckHWIntrinsic(NewLowering(compiler, block), node);

            Assert.That(control.IsContained, Is.False);
            Assert.That(first.IsContained, Is.False);
            Assert.That(second.IsContained, Is.False);
            Assert.That(third.IsContained, Is.False);
        });
    }

    [Test]
    public static void TernaryBroadcastSwapKeepsTheNewBroadcastOperand()
    {
        var previous = EnableEmbeddedBroadcast(ref Globals.JitConfig);
        EnableEmbeddedBroadcast(ref Globals.JitConfig) = 1;
        try
        {
            WithCompiler(compiler => {
                var codeGen = compiler.codeGen ?? throw new InvalidOperationException("Code generation is not initialized.");
                codeGen.Emitter.UseVexEncodings = true;
                codeGen.Emitter.UseEvexEncodings = true;
                compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
                compiler.lvaTable[0].Type = TYP_SIMD16;
                compiler.lvaTable[1].Type = TYP_SIMD16;
                compiler.lvaCount = 2;
                var first = compiler.gtNewLclvNode(TYP_SIMD16, 0);
                var constant = new GenTreeVecCon(TYP_SIMD16);
                constant.SimdVal.AsSpan<int>()[..4].Fill(7);
                var left = compiler.gtNewLclvNode(TYP_SIMD16, 1);
                var right = new GenTreeVecCon(TYP_SIMD16);
                var computed = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_Xor,
                    TYP_INT, 16, left, right);
                var immediate = compiler.gtNewIconNode(TYP_INT, 0xCA);
                var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_TernaryLogic,
                    TYP_INT, 16, first, constant, computed, immediate);
                var block = NewBlock(first, constant, left, right, computed, immediate, node);

                ContainCheckHWIntrinsic(NewLowering(compiler, block), node);

                Assert.That(node.GetOp(2), Is.SameAs(computed));
                Assert.That(node.GetOp(3) is GenTreeHWIntrinsic, Is.True);
                Assert.That(node.GetOp(3).AsHWIntrinsic().HWIntrinsicId,
                    Is.EqualTo(NI_AVX2_BroadcastScalarToVector128));
                Assert.That(constant.Next, Is.Null);
                Assert.That(constant.Prev, Is.Null);
                var logicalControl = TernaryLogicInfo.GetTernaryControlByte(
                    TernaryLogicInfo.Lookup(unchecked((byte)immediate.IconValue)),
                    0xF0, 0xAA, 0xCC);
                Assert.That(logicalControl, Is.EqualTo((byte)0xCA));
            });
        }
        finally
        {
            EnableEmbeddedBroadcast(ref Globals.JitConfig) = previous;
        }
    }

    [Test]
    public static void FusedMultiplyDoesNotContainItsRmwOperand()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            var rmw = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            rmw.Flags |= GTF_VAR_DEATH;
            var multiply = new GenTreeVecCon(TYP_SIMD16);
            multiply.SimdVal.f32[0] = 2;
            var addend = new GenTreeVecCon(TYP_SIMD16);
            addend.SimdVal.f32[0] = 3;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX2_MultiplyAdd,
                TYP_FLOAT, 16, rmw, multiply, addend);
            var block = NewBlock(rmw, multiply, addend, node);

            ContainCheckHWIntrinsic(NewLowering(compiler, block), node);

            Assert.That(rmw.IsContained, Is.False);
            Assert.That(multiply.IsContained || addend.IsContained, Is.True);
            Assert.That(node.GetOp(1), Is.SameAs(rmw));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EmbeddedBroadcastReplacesTheVectorConstantAndRetainsTheOwningUse(bool swap)
    {
        var previous = EnableEmbeddedBroadcast(ref Globals.JitConfig);
        EnableEmbeddedBroadcast(ref Globals.JitConfig) = 1;
        try
        {
            WithCompiler(compiler => {
                var codeGen = compiler.codeGen ?? throw new InvalidOperationException("Code generation is not initialized.");
                codeGen.Emitter.UseVexEncodings = true;
                codeGen.Emitter.UseEvexEncodings = true;
                compiler.lvaTable[0].Type = TYP_SIMD16;
                var value = compiler.gtNewLclvNode(TYP_SIMD16, 0);
                var constant = new GenTreeVecCon(TYP_SIMD16);
                constant.SimdVal.AsSpan<int>()[..4].Fill(7);
                var secondSource = new GenTreeVecCon(TYP_SIMD16);
                var computed = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_Xor,
                    TYP_INT, 16, value, secondSource);
                var other = swap ? (GenTree)computed : value;
                var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_Add,
                    TYP_INT, 16, swap ? constant : other, swap ? other : constant);
                var block = swap
                    ? NewBlock(value, secondSource, computed, constant, node)
                    : NewBlock(value, constant, node);

                ContainCheckHWIntrinsic(NewLowering(compiler, block), node);

                var broadcast = node.GetOp(2).AsHWIntrinsic();
                Assert.That(node.GetOp(1), Is.SameAs(other));
                Assert.That(broadcast.HWIntrinsicId, Is.EqualTo(NI_AVX2_BroadcastScalarToVector128));
                Assert.That(broadcast.IsContained, Is.True);
                Assert.That(broadcast.GetOp(1).AsIntCon().IconValue, Is.EqualTo((nint)7));
                Assert.That(broadcast.GetOp(1).IsContained, Is.True);
                Assert.That(constant.Next, Is.Null);
                Assert.That(constant.Prev, Is.Null);
                Assert.That(broadcast.Next, Is.SameAs(node));
            });
        }
        finally
        {
            EnableEmbeddedBroadcast(ref Globals.JitConfig) = previous;
        }
    }

    [Test]
    public static void PermuteSwapsSourcesAndTogglesIndicesWhenRmwUsesThirdOperand()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[1].Type = TYP_SIMD16;
            compiler.lvaCount = 2;
            var memory = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var indices = new GenTreeVecCon(TYP_SIMD16);
            var rmw = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            rmw.Flags |= GTF_VAR_DEATH;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_PermuteVar2x64x2,
                TYP_LONG, 16, memory, indices, rmw);
            var block = NewBlock(memory, indices, rmw, node);

            ContainCheckHWIntrinsic(NewLowering(compiler, block), node);

            Assert.That(node.GetOp(1), Is.SameAs(rmw));
            Assert.That(node.GetOp(3), Is.SameAs(memory));
            Assert.That(memory.IsContained || memory.IsRegOptional, Is.True);
            Assert.That(indices.SimdVal.u64[0], Is.EqualTo(2UL));
            Assert.That(indices.SimdVal.u64[1], Is.EqualTo(2UL));
        });
    }

    [Test]
    public static void BlendMaskContainsAnInvariantMaskedOperation()
    {
        var previous = EnableEmbeddedMasking(ref Globals.JitConfig);
        EnableEmbeddedMasking(ref Globals.JitConfig) = 1;
        try
        {
            WithCompiler(compiler => {
                compiler.lvaTable = [new LclVarDsc(), new LclVarDsc()];
                compiler.lvaTable[0].Type = TYP_SIMD16;
                compiler.lvaTable[1].Type = TYP_MASK;
                compiler.lvaCount = 2;
                var zero = new GenTreeVecCon(TYP_SIMD16);
                var value = compiler.gtNewLclvNode(TYP_SIMD16, 0);
                var other = new GenTreeVecCon(TYP_SIMD16);
                other.SimdVal.i32[0] = 5;
                var operation = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_Add,
                    TYP_INT, 16, value, other);
                var mask = compiler.gtNewLclvNode(TYP_MASK, 1);
                var blend = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_BlendVariableMask,
                    TYP_INT, 16, zero, operation, mask);
                var block = NewBlock(zero, value, other, operation, mask, blend);

                ContainCheckHWIntrinsic(NewLowering(compiler, block), blend);

                Assert.That(zero.IsContained, Is.True);
                Assert.That(operation.IsContained, Is.True);
                Assert.That((operation.Flags & GTF_HW_EM_OP) != 0, Is.True);
            });
        }
        finally
        {
            EnableEmbeddedMasking(ref Globals.JitConfig) = previous;
        }
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckHWIntrinsic")]
    private static extern void ContainCheckHWIntrinsic(Lowering lowering, GenTreeHWIntrinsic intrinsic);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableEmbeddedBroadcast")]
    private static extern ref int EnableEmbeddedBroadcast(ref JitConfigValues config);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableEmbeddedMasking")]
    private static extern ref int EnableEmbeddedMasking(ref JitConfigValues config);

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
#if DEBUG
        compiler.opts.compEnablePCRelAddr = true;
#endif
        compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
        compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
        compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX);
        compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_INT;
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
#endif
