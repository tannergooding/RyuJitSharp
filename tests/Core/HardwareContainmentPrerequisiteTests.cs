// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareContainmentPrerequisiteTests
{
    [TestCase(8u, false, true)]
    [TestCase(16u, true, false)]
    public static void LocalAddressesRespectAccessExtent(uint accessSize, bool addressExposed, bool contained)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].SetAddressExposed(addressExposed, AddressExposedReason.ESCAPE_ADDRESS);
            var address = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0);
            var intrinsic = Load(address);
            var block = NewBlock(address, intrinsic);

            ContainCheckHWIntrinsicAddr(NewLowering(compiler, block), intrinsic, address, accessSize);

            Assert.That(address.IsContained, Is.EqualTo(contained));
            Assert.That(intrinsic.GetOp(1), Is.SameAs(address));
        });
    }

    [Test]
    public static void AbsoluteAddressesAreContainedWithoutChangingTheirOwner()
    {
        WithCompiler(compiler => {
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
#endif
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var intrinsic = Load(address);
            var block = NewBlock(address, intrinsic);

            ContainCheckHWIntrinsicAddr(NewLowering(compiler, block), intrinsic, address, 16);

            Assert.That(address.IsContained, Is.True);
            Assert.That(intrinsic.GetOp(1), Is.SameAs(address));
        });
    }

    [Test]
    public static void AddressModeReplacementRetainsTheIntrinsicOperand()
    {
        WithCompiler(compiler => {
            var local = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var offset = compiler.gtNewIconNode(TYP_I_IMPL, 16);
            var address = new GenTreeOp(GT_ADD, TYP_I_IMPL, local, offset);
            var intrinsic = Load(address);
            var block = NewBlock(local, offset, address, intrinsic);

            ContainCheckHWIntrinsicAddr(NewLowering(compiler, block), intrinsic, address, 16);

            var mode = intrinsic.GetOp(1);
            Assert.That(mode.Oper, Is.EqualTo(GT_LEA));
            Assert.That(mode.IsContained, Is.True);
            Assert.That(mode.Next, Is.SameAs(intrinsic));
            Assert.That(address.Next, Is.Null);
            Assert.That(address.Prev, Is.Null);
#if DEBUG
            Assert.That(mode.TreeId, Is.EqualTo(address.TreeId));
#endif
        });
    }

    [TestCase(NI_Vector_CreateScalar)]
    [TestCase(NI_Vector_CreateScalarUnsafe)]
    public static void CreateScalarRemovalPreservesTheUnderlyingScalar(NamedIntrinsic create)
    {
        WithCompiler(compiler => {
            var scalar = compiler.gtNewIconNode(TYP_INT, 42);
            scalar.IsContained = true;
            var createScalar = new GenTreeHWIntrinsic(TYP_SIMD16, create, TYP_INT, 16, scalar);
            var parent = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_ToScalar, TYP_INT, 16, createScalar);
            var block = NewBlock(scalar, createScalar, parent);

            ContainHWIntrinsicOperand(NewLowering(compiler, block), parent, createScalar);

            Assert.That(parent.GetOp(1), Is.SameAs(scalar));
            Assert.That(scalar.IsContained, Is.True);
            Assert.That(createScalar.Prev, Is.Null);
            Assert.That(createScalar.Next, Is.Null);
            Assert.That(scalar.Next, Is.SameAs(parent));
        });
    }

    private static GenTreeHWIntrinsic Load(GenTree address)
        => new(TYP_SIMD16, NI_X86Base_LoadScalarVector128, TYP_FLOAT, 16, address);

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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckHWIntrinsicAddr")]
    private static extern void ContainCheckHWIntrinsicAddr(Lowering lowering, GenTreeHWIntrinsic intrinsic,
        GenTree address, uint accessSize);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainHWIntrinsicOperand")]
    private static extern void ContainHWIntrinsicOperand(Lowering lowering, GenTreeHWIntrinsic intrinsic,
        GenTree operand);

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
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_LONG;
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
