// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class HardwareScalarLoweringTests
{
    [TestCase(TYP_UBYTE, TYP_UBYTE)]
    [TestCase(TYP_UINT, TYP_INT)]
    [TestCase(TYP_ULONG, TYP_LONG)]
    [TestCase(TYP_FLOAT, TYP_FLOAT)]
    public static void ToScalarExposesAProperlyTypedMemoryLoad(var_types baseType, var_types expectedType)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var vector = compiler.gtNewIndir(TYP_SIMD16, address,
                GTF_IND_NONFAULTING | GTF_IND_UNALIGNED);
            var scalar = compiler.gtNewSimdHWIntrinsicNode(baseType.ActualType, NI_Vector_ToScalar, baseType, 16, vector);
            scalar._vnPair.SetBoth(123);
            var user = new GenTreeUnOp(GT_NEG, baseType.ActualType, scalar) { IsUnusedValue = true };
            var block = NewBlock(address, vector, scalar, user);
            var flags = vector.Flags & GTF_IND_FLAGS;

            Assert.That(LowerToScalar(NewLowering(compiler, block), scalar), Is.SameAs(user));

            var load = user.Op1.AsIndir();
            Assert.That(load.Type, Is.EqualTo(expectedType));
            Assert.That(load.Addr, Is.SameAs(address));
            Assert.That(load.Flags & GTF_IND_FLAGS, Is.EqualTo(flags));
            Assert.That(load._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(address.Next, Is.SameAs(load));
            Assert.That(load.Next, Is.SameAs(user));
            Assert.That(vector.Next, Is.Null);
            Assert.That(scalar.Next, Is.Null);
#if DEBUG
            Assert.That(load.TreeId, Is.Not.EqualTo(scalar.TreeId));
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ToScalarLoadsTheFirstElementFromStackLocals(bool field)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[1].Type = field ? TYP_SIMD32 : TYP_SIMD16;
            compiler.lvaTable[1].lvDoNotEnregister = true;
            var offset = field ? (ushort)8 : (ushort)0;
            GenTree vector = field
                ? compiler.gtNewLclFldNode(TYP_SIMD16, 1, offset)
                : compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var scalar = compiler.gtNewSimdHWIntrinsicNode(TYP_FLOAT, NI_Vector_ToScalar, TYP_FLOAT, 16, vector);
            var user = new GenTreeUnOp(GT_NEG, TYP_FLOAT, scalar) { IsUnusedValue = true };
            var block = NewBlock(vector, scalar, user);

            Assert.That(LowerToScalar(NewLowering(compiler, block), scalar), Is.SameAs(user));

            var load = user.Op1.AsLclFld();
            Assert.That(load.Type, Is.EqualTo(TYP_FLOAT));
            Assert.That(load.LclNum, Is.EqualTo(1));
            Assert.That(load.LclOffs, Is.EqualTo(offset));
            Assert.That(block.FirstNode, Is.SameAs(load));
            Assert.That(load.Next, Is.SameAs(user));
            Assert.That(vector.Next, Is.Null);
            Assert.That(scalar.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void ComputedVectorKeepsItsExtractionNode()
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var right = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var vector = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_X86Base_Add, TYP_INT, 16, left, right);
            var scalar = compiler.gtNewSimdHWIntrinsicNode(TYP_INT, NI_Vector_ToScalar, TYP_INT, 16, vector);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, scalar) { IsUnusedValue = true };
            var block = NewBlock(left, right, vector, scalar, user);

            Assert.That(LowerToScalar(NewLowering(compiler, block), scalar), Is.SameAs(user));

            Assert.That(user.Op1, Is.SameAs(scalar));
            Assert.That(scalar.GetOp(1), Is.SameAs(vector));
            Assert.That(vector.IsContained, Is.False);
        });
    }

    private static BasicBlock NewBlock(params ReadOnlySpan<GenTree> nodes)
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicToScalar")]
    private static extern GenTree? LowerToScalar(Lowering lowering, GenTreeHWIntrinsic node);

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
        compiler.lvaTable = [new() { Type = TYP_I_IMPL }, new() { Type = TYP_SIMD16 }];
        compiler.lvaCount = compiler.lvaTable.Length;
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
