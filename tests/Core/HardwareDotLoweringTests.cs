// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
[NonParallelizable]
internal static unsafe class HardwareDotLoweringTests
{
    [TestCase(TYP_FLOAT, (byte)16, false)]
    [TestCase(TYP_FLOAT, (byte)16, true)]
    [TestCase(TYP_DOUBLE, (byte)16, false)]
    [TestCase(TYP_DOUBLE, (byte)16, true)]
    [TestCase(TYP_FLOAT, (byte)32, false)]
    [TestCase(TYP_FLOAT, (byte)32, true)]
    [TestCase(TYP_DOUBLE, (byte)32, false)]
    [TestCase(TYP_DOUBLE, (byte)32, true)]
    public static void UnusedAvxDotKeepsItsOriginalNodes(var_types baseType, byte size, bool minOpts)
    {
        WithCompiler(minOpts, compiler => {
            EnableIsa(compiler, InstructionSet_AVX);
            var simdType = Compiler.GetSimdTypeForSize(size);
            compiler.lvaTable[0].Type = simdType;
            compiler.lvaTable[1].Type = simdType;
            var left = compiler.gtNewLclvNode(simdType, 0);
            var right = compiler.gtNewLclvNode(simdType, 1);
            var dot = compiler.gtNewSimdHWIntrinsicNode(simdType, NI_Vector_Dot, baseType, size, left, right);
            dot.IsUnusedValue = true;
            var tail = new GenTree(GT_NO_OP, TYP_VOID);
            var block = NewBlock(left, right, dot, tail);
            var count = compiler.lvaCount;

            Assert.That(LowerDot(NewLowering(compiler, block), dot), Is.SameAs(tail));

            Assert.That(compiler.lvaCount, Is.EqualTo(count));
            GenTree[] expectedNodes = [left, right, dot, tail];
            Assert.That(Nodes(block), Is.EqualTo(expectedNodes));
            Assert.That(dot.HWIntrinsicId, Is.EqualTo(NI_Vector_Dot));
            Assert.That(dot.GetOp(1), Is.SameAs(left));
            Assert.That(dot.GetOp(2), Is.SameAs(right));
            CheckLir(compiler, block);
        });
    }

    [Category("HardwareBinarySelectorContract")]
    [TestCase(TYP_FLOAT, TYP_FLOAT, (byte)16, NI_X86Base_Multiply)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE, (byte)32, NI_AVX_Multiply)]
    [TestCase(TYP_FLOAT, TYP_SIMD32, (byte)16, NI_X86Base_Multiply)]
    [TestCase(TYP_DOUBLE, TYP_SIMD16, (byte)32, NI_AVX_Multiply)]
    public static void DotMultiplySelectorConsumesScalarAndReinterpretedOperandsAtVectorWidth(
        var_types baseType, var_types operandType, byte size, NamedIntrinsic expected)
    {
        WithCompiler(true, compiler => {
            EnableIsa(compiler, InstructionSet_AVX);
            compiler.lvaTable[0].Type = operandType;
            compiler.lvaTable[1].Type = operandType;
            var left = compiler.gtNewLclvNode(operandType, 0);
            var right = compiler.gtNewLclvNode(operandType, 1);

            var multiply = compiler.GetHWIntrinsicIdForBinOp(GT_MUL, left, right, baseType, size, isScalar: false);

            Assert.That(multiply, Is.EqualTo(expected));
        });
    }

    [Category("HardwareRecursiveDispatch")]
    [TestCase(TYP_FLOAT, (byte)16, false, false)]
    [TestCase(TYP_FLOAT, (byte)32, false, false)]
    [TestCase(TYP_DOUBLE, (byte)16, false, false)]
    [TestCase(TYP_DOUBLE, (byte)32, false, false)]
    [TestCase(TYP_FLOAT, (byte)16, true, false)]
    [TestCase(TYP_DOUBLE, (byte)32, true, false)]
    [TestCase(TYP_FLOAT, (byte)16, true, true)]
    [TestCase(TYP_DOUBLE, (byte)32, true, true)]
    public static void AvxDotBuildsTheNativeOrderedReduction(var_types baseType, byte size, bool scalarOperands, bool scalarResult)
    {
        WithCompiler(true, compiler => {
            EnableIsa(compiler, InstructionSet_AVX);
            var simdType = Compiler.GetSimdTypeForSize(size);
            var operandType = scalarOperands ? baseType : simdType;
            var resultType = scalarResult ? baseType : simdType;
            compiler.lvaTable[0].Type = operandType;
            compiler.lvaTable[1].Type = operandType;
            var left = compiler.gtNewLclvNode(operandType, 0);
            var right = compiler.gtNewLclvNode(operandType, 1);
            var dot = compiler.gtNewSimdHWIntrinsicNode(resultType, NI_Vector_Dot, baseType, size, left, right);
            var user = new GenTreeUnOp(GT_NEG, resultType, dot) { IsUnusedValue = true };
            var block = NewBlock(left, right, dot, user);
            var lowering = NewLowering(compiler, block);

            var next = scalarResult ? LowerInner(lowering, dot) : LowerDot(lowering, dot);

            Assert.That(next, Is.SameAs(user));
            var intrinsics = Nodes(block).OfType<GenTreeHWIntrinsic>().ToArray();
            var multiply = intrinsics.Single(node => node.GetOperForHWIntrinsicId(out _) == GT_MUL);
            Assert.That(multiply.GetOperForHWIntrinsicId(out var isScalar), Is.EqualTo(GT_MUL));
            Assert.That(isScalar, Is.False);
            Assert.That(multiply.Type, Is.EqualTo(simdType));
            Assert.That(multiply.GetOp(1), Is.SameAs(left));
            Assert.That(multiply.GetOp(2), Is.SameAs(right));
            Assert.That(intrinsics.Any(node => node.HWIntrinsicId is NI_Vector_Create or NI_Vector_CreateScalarUnsafe), Is.False);

            var permutes = intrinsics.Where(node => node.HWIntrinsicId == NI_AVX_Permute).ToArray();
            var additions = intrinsics.Where(node => node.GetOperForHWIntrinsicId(out _) == GT_ADD).ToArray();
            var lanePasses = baseType == TYP_FLOAT ? 2 : 1;
            Assert.That(permutes.Length, Is.EqualTo(lanePasses));
            Assert.That(additions.Length, Is.EqualTo(lanePasses + (size == 32 ? 1 : 0)));
            Assert.That(permutes[0].GetOp(2).AsIntCon().IconValue,
                Is.EqualTo((nint)(baseType == TYP_FLOAT ? 0xB1 : size == 32 ? 0x5 : 0x1)));
            Assert.That(additions[0].GetOp(1), Is.SameAs(permutes[0]));
            AssertSameLocal(additions[0].GetOp(2), permutes[0].GetOp(1));
            if (baseType == TYP_FLOAT)
            {
                Assert.That(permutes[1].GetOp(2).AsIntCon().IconValue, Is.EqualTo((nint)0x4E));
                // SIMD32 materializes this intermediate before the cross-lane pass, without
                // lowering it. SIMD16 lowers the final add and containment commutes its local.
                var permuteOperand = size == 16 ? 1 : 2;
                Assert.That(additions[1].GetOp(permuteOperand), Is.SameAs(permutes[1]));
                AssertSameLocal(additions[1].GetOp(3 - permuteOperand), permutes[1].GetOp(1));
                Assert.That(StoredValue(block, permutes[1].GetOp(1)), Is.SameAs(additions[0]));
            }
            Assert.That(StoredValue(block, permutes[0].GetOp(1)), Is.SameAs(multiply));

            if (size == 32)
            {
                var crossLane = intrinsics.Single(node => node.HWIntrinsicId == NI_AVX_Permute2x128);
                Assert.That(crossLane.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)1));
                AssertSameLocal(crossLane.GetOp(1), crossLane.GetOp(2));
                Assert.That(additions[^1].GetOp(1), Is.SameAs(crossLane));
                AssertSameLocal(additions[^1].GetOp(2), crossLane.GetOp(1));
                Assert.That(StoredValue(block, crossLane.GetOp(1)), Is.SameAs(additions[lanePasses - 1]));
            }

            if (scalarResult)
            {
                var extraction = user.Op1.AsHWIntrinsic();
                Assert.That(extraction.HWIntrinsicId, Is.EqualTo(NI_Vector_ToScalar));
                Assert.That(extraction.GetOp(1), Is.SameAs(additions[^1]));
            }
            else
            {
                Assert.That(user.Op1, Is.SameAs(additions[^1]));
            }
            Assert.That(dot.Next, Is.Null);
            Assert.That(dot.Prev, Is.Null);
#if DEBUG
            Assert.That(user.Op1.TreeId, Is.Not.EqualTo(dot.TreeId));
#endif
            CheckLir(compiler, block);
        });
    }

    [Category("HardwareRecursiveDispatch")]
    [TestCase(TYP_FLOAT, 0xFF, false)]
    [TestCase(TYP_DOUBLE, 0x33, false)]
    [TestCase(TYP_FLOAT, 0xFF, true)]
    [TestCase(TYP_DOUBLE, 0x33, true)]
    public static void NonAvxFloatingDotRetainsIdentityAndBroadcastsAllLanes(var_types baseType, int mask, bool unused)
    {
        WithCompiler(false, compiler => {
            EnableIsa(compiler, InstructionSet_X86Base);
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[1].Type = TYP_SIMD16;
            var left = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var right = compiler.gtNewLclvNode(TYP_SIMD16, 1);
            var dot = compiler.gtNewSimdHWIntrinsicNode(TYP_SIMD16, NI_Vector_Dot, baseType, 16, left, right);
            dot.IsUnusedValue = unused;
            var user = new GenTreeUnOp(GT_NEG, TYP_SIMD16, dot) { IsUnusedValue = true };
            var block = NewBlock(left, right, dot);
            if (!unused)
            {
                block.InsertAtEnd(user);
            }
#if DEBUG
            var treeId = dot.TreeId;
#endif

            Assert.That(LowerDot(NewLowering(compiler, block), dot), Is.SameAs(unused ? null : user));

            Assert.That(dot.HWIntrinsicId, Is.EqualTo(NI_X86Base_DotProduct));
            Assert.That(dot.GetOp(1), Is.SameAs(left));
            Assert.That(dot.GetOp(2), Is.SameAs(right));
            Assert.That(dot.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)mask));
            Assert.That(dot.Prev, Is.SameAs(dot.GetOp(3)));
            Assert.That(dot.IsUnusedValue, Is.EqualTo(unused));
#if DEBUG
            Assert.That(dot.TreeId, Is.EqualTo(treeId));
#endif
            CheckLir(compiler, block);
        });
    }

    [Category("HardwareRecursiveDispatch")]
    [TestCase(TYP_SHORT, (byte)16, false)]
    [TestCase(TYP_USHORT, (byte)16, false)]
    [TestCase(TYP_INT, (byte)16, false)]
    [TestCase(TYP_UINT, (byte)16, true)]
    [TestCase(TYP_SHORT, (byte)32, false)]
    [TestCase(TYP_USHORT, (byte)32, false)]
    [TestCase(TYP_INT, (byte)32, false)]
    [TestCase(TYP_UINT, (byte)32, true)]
    public static void IntegralDotUsesPairwiseHorizontalAddsThenCombines128BitLanes(var_types baseType, byte size, bool unused)
    {
        WithCompiler(true, compiler => {
            EnableIsa(compiler, InstructionSet_X86Base);
            EnableIsa(compiler, InstructionSet_AVX);
            EnableIsa(compiler, InstructionSet_AVX2);
            var simdType = Compiler.GetSimdTypeForSize(size);
            compiler.lvaTable[0].Type = simdType;
            compiler.lvaTable[1].Type = simdType;
            var left = compiler.gtNewLclvNode(simdType, 0);
            var right = compiler.gtNewLclvNode(simdType, 1);
            var dot = compiler.gtNewSimdHWIntrinsicNode(simdType, NI_Vector_Dot, baseType, size, left, right);
            dot.IsUnusedValue = unused;
            var user = new GenTreeUnOp(GT_NEG, simdType, dot) { IsUnusedValue = true };
            var block = NewBlock(left, right, dot);
            if (!unused)
            {
                block.InsertAtEnd(user);
            }

            Assert.That(LowerDot(NewLowering(compiler, block), dot), Is.SameAs(unused ? null : user));

            var intrinsics = Nodes(block).OfType<GenTreeHWIntrinsic>().ToArray();
            var horizontalId = size == 32 ? NI_AVX2_HorizontalAdd : NI_X86Base_HorizontalAdd;
            var horizontalAdds = intrinsics.Where(node => node.HWIntrinsicId == horizontalId).ToArray();
            Assert.That(horizontalAdds.Length, Is.EqualTo(baseType.Size == 2 ? 3 : 2));
            foreach (var add in horizontalAdds)
            {
                AssertSameLocal(add.GetOp(1), add.GetOp(2));
            }
            for (var i = 1; i < horizontalAdds.Length; i++)
            {
                Assert.That(StoredValue(block, horizontalAdds[i].GetOp(1)), Is.SameAs(horizontalAdds[i - 1]));
            }

            GenTree result = horizontalAdds[^1];
            if (size == 32)
            {
                var crossLane = intrinsics.Single(node => node.HWIntrinsicId == NI_AVX2_Permute2x128);
                Assert.That(crossLane.GetOp(3).AsIntCon().IconValue, Is.EqualTo((nint)1));
                AssertSameLocal(crossLane.GetOp(1), crossLane.GetOp(2));
                Assert.That(StoredValue(block, crossLane.GetOp(1)), Is.SameAs(horizontalAdds[^1]));
                var add = intrinsics.Single(node => node.GetOperForHWIntrinsicId(out _) == GT_ADD);
                Assert.That(add.GetOp(1), Is.SameAs(crossLane));
                AssertSameLocal(add.GetOp(2), crossLane.GetOp(1));
                result = add;
            }
            if (unused)
            {
                Assert.That(result.IsUnusedValue, Is.True);
                Assert.That(result, Is.SameAs(block.LastNode));
            }
            else
            {
                Assert.That(user.Op1, Is.SameAs(result));
            }
            Assert.That(dot.Next, Is.Null);
            Assert.That(dot.Prev, Is.Null);
            CheckLir(compiler, block);
        });
    }

    private static void AssertSameLocal(GenTree left, GenTree right)
    {
        Assert.That(left.Oper, Is.EqualTo(GT_LCL_VAR));
        Assert.That(right.Oper, Is.EqualTo(GT_LCL_VAR));
        Assert.That(left, Is.Not.SameAs(right));
        Assert.That(left.AsLclVar().LclNum, Is.EqualTo(right.AsLclVar().LclNum));
    }

    private static GenTree StoredValue(BasicBlock block, GenTree local)
    {
        var number = local.AsLclVar().LclNum;
        return Nodes(block).OfType<GenTreeLclVar>().Single(node => node.Oper == GT_STORE_LCL_VAR && node.LclNum == number).Data;
    }

    private static IEnumerable<GenTree> Nodes(BasicBlock block)
    {
        for (var node = block.FirstNode; node is not null; node = node.Next)
        {
            yield return node;
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicDot")]
    private static extern GenTree? LowerDot(Lowering lowering, GenTreeHWIntrinsic node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicDotInnerMulSum")]
    private static extern GenTree? LowerInner(Lowering lowering, GenTreeHWIntrinsic node);

    private static void WithCompiler(bool minOpts, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaRefCountState = RefCountState.RCS_NORMAL;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.lvaTable = [new() { Type = TYP_SIMD16 }, new() { Type = TYP_SIMD16 }];
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
#endif
