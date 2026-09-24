// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class IntrinsicContainmentTests
{
    [TestCase(GT_BSWAP, TYP_INT, TYP_INT, false, true, false)]
    [TestCase(GT_BSWAP, TYP_INT, TYP_INT, true, false, false)]
    [TestCase(GT_BSWAP, TYP_INT, TYP_INT, true, true, true)]
    [TestCase(GT_BSWAP, TYP_LONG, TYP_LONG, true, true, true)]
    [TestCase(GT_BSWAP16, TYP_INT, TYP_USHORT, true, true, true)]
    [TestCase(GT_BSWAP16, TYP_INT, TYP_INT, true, true, false)]
    public static void ByteSwapContainmentRequiresOptimizationIsaAndMatchingLoadWidth(
        genTreeOps oper, var_types type, var_types loadType, bool optimized, bool supported, bool contained)
    {
        WithCompiler(compiler => {
            if (supported)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX2);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX2);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX2);
            }
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var operand = new GenTreeIndir(GT_IND, loadType, address);
            var operation = new GenTreeUnOp(oper, type, operand);
            var block = NewBlock(operand, operation);
            block.InsertBefore(operand, address);

            LowerBswapOp(NewLowering(compiler, block), operation);

            Assert.That(operand.IsContained, Is.EqualTo(contained));
            Assert.That(operand.Type, Is.EqualTo(loadType));
            Assert.That(operation.Op1, Is.SameAs(operand));
        }, minOpts: !optimized);
    }

    [Test]
    public static void MathConstantsPreserveBitsAndDistinguishPositiveZero(
        [Values(NI_System_Math_Ceiling, NI_System_Math_Floor, NI_System_Math_Truncate,
            NI_System_Math_Round, NI_System_Math_Sqrt)] NamedIntrinsic intrinsic,
        [Values(0L, long.MinValue, 0x3FF0000000000000L, 0x7FF8000000000001L)] long bits)
    {
        WithCompiler(compiler => {
            var operand = new GenTreeDblCon(TYP_DOUBLE, BitConverter.Int64BitsToDouble(bits));
            var operation = new GenTreeIntrinsic(TYP_DOUBLE, operand, intrinsic, null);
            var block = NewBlock(operand, operation);

            ContainCheckIntrinsic(NewLowering(compiler, block), operation);

            Assert.That(operand.IsContained, Is.EqualTo(bits != 0));
            Assert.That(operand.IsRegOptional, Is.EqualTo(bits == 0));
            Assert.That(operand.IsBitwiseEqual(bits), Is.True);
            Assert.That(operation.Op1, Is.SameAs(operand));
            Assert.That(operand.Next, Is.SameAs(operation));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LocalOperandUsesNativeContainmentOrRegisterOptionality(bool stackLocal)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].lvDoNotEnregister = stackLocal;
            var operand = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var operation = new GenTreeIntrinsic(TYP_DOUBLE, operand, NI_System_Math_Sqrt, null);
            var block = NewBlock(operand, operation);

            ContainCheckIntrinsic(NewLowering(compiler, block), operation);

            Assert.That(operand.IsContained, Is.EqualTo(stackLocal));
            Assert.That(operand.IsRegOptional, Is.EqualTo(!stackLocal));
        });
    }

    [Test]
    public static void OtherScalarIntrinsicsDoNotChangeOperandFlags()
    {
        WithCompiler(compiler => {
            var operand = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var operation = new GenTreeIntrinsic(TYP_DOUBLE, operand, NI_System_Math_Abs, null);
            var block = NewBlock(operand, operation);
            var flags = operand.Flags;

            ContainCheckIntrinsic(NewLowering(compiler, block), operation);

            Assert.That(operand.Flags, Is.EqualTo(flags));
        });
    }

    private static BasicBlock NewBlock(GenTree operand, GenTree operation)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        block.InsertAtEnd(operand);
        block.InsertAtEnd(operation);
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckIntrinsic")]
    private static extern void ContainCheckIntrinsic(Lowering lowering, GenTreeIntrinsic node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerBswapOp")]
    private static extern void LowerBswapOp(Lowering lowering, GenTreeUnOp node);

    private static void WithCompiler(Action<Compiler> action, bool minOpts = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_DOUBLE;
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
