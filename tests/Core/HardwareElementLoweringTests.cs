// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
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
internal static unsafe class HardwareElementLoweringTests
{
    [TestCase(TYP_BYTE, TYP_SIMD16, 3, 3, true)]
    [TestCase(TYP_SHORT, TYP_SIMD32, 9, 1, true)]
    [TestCase(TYP_LONG, TYP_SIMD64, 5, 1, false)]
    public static void RegisterExtractionPreservesSignednessAndWideLaneSelection(
        var_types baseType, var_types vectorType, int indexValue, int laneIndex, bool signedCast)
    {
        WithCompiler(compiler => {
            foreach (var isa in new[] { InstructionSet_X86Base, InstructionSet_AVX,
                InstructionSet_AVX2, InstructionSet_AVX512 })
            {
                compiler.opts.compSupportsISA.AddInstructionSet(isa);
                compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
            }
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.lvaTable[1].Type = vectorType;
            var left = new GenTreeLclVar(vectorType, 1);
            var right = new GenTreeLclVar(vectorType, 1);
            var addId = vectorType switch {
                TYP_SIMD16 => NI_X86Base_Add,
                TYP_SIMD32 => NI_AVX2_Add,
                _ => NI_AVX512_Add,
            };
            var vector = compiler.gtNewSimdHWIntrinsicNode(vectorType, addId, baseType, (byte)vectorType.Size, left, right);
            var index = compiler.gtNewIconNode(TYP_INT, indexValue);
            var node = new GenTreeHWIntrinsic(baseType.ActualType, NI_Vector_GetElement, baseType,
                (byte)vectorType.Size, vector, index);
            var user = new GenTreeUnOp(GT_RETURN, baseType.ActualType, node);
            var block = NewBlock(left, right, vector, index, node, user);

            Assert.That(LowerGetElement(NewLowering(compiler, block), node), Is.SameAs(user));

            var extracted = user.Op1;
            if (signedCast)
            {
                Assert.That(extracted.Oper, Is.EqualTo(GT_CAST));
                Assert.That(extracted.AsCast().CastType, Is.EqualTo(baseType));
                extracted = extracted.AsCast().Op1;
            }
            Assert.That(extracted, Is.SameAs(node));
            Assert.That(node.HWIntrinsicId, Is.EqualTo(baseType is TYP_LONG ? NI_X86Base_X64_Extract : NI_X86Base_Extract));
            Assert.That(node.SimdSize, Is.EqualTo(16));
            Assert.That(node.GetOp(2).AsIntCon().IconValue, Is.EqualTo((nint)laneIndex));
            if (vectorType is TYP_SIMD16)
            {
                Assert.That(node.GetOp(1), Is.SameAs(vector));
            }
            else
            {
                var part = node.GetOp(1).AsHWIntrinsic();
                Assert.That(part.HWIntrinsicId, Is.EqualTo(vectorType is TYP_SIMD32
                    ? NI_AVX2_ExtractVector128 : NI_AVX512_ExtractVector128));
                Assert.That(part.GetOp(1), Is.SameAs(vector));
                Assert.That(part.GetOp(2).AsIntCon().IconValue, Is.EqualTo((nint)(vectorType is TYP_SIMD32 ? 1 : 2)));
            }
            Assert.That(index.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(1, 4)]
    [TestCase(-1, 12)]
    [TestCase(257, 4)]
    public static void MemoryExtractionUsesNativeImmediateTruncation(int value, int offset)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var vector = compiler.gtNewIndir(TYP_SIMD16, address, GTF_IND_NONFAULTING | GTF_IND_UNALIGNED);
            var index = compiler.gtNewIconNode(TYP_INT, value);
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_UINT, 16, vector, index);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, node) { IsUnusedValue = true };
            var block = NewBlock(address, vector, index, node, user);
            var flags = vector.Flags & GTF_IND_FLAGS;

            var next = LowerGetElement(NewLowering(compiler, block), node);

            var load = user.Op1.AsIndir();
            Assert.That(next, Is.SameAs(load));
            Assert.That(load.Type, Is.EqualTo(TYP_INT));
            Assert.That(load.Flags & GTF_IND_FLAGS, Is.EqualTo(flags));
            Assert.That(load.Addr.AsAddrMode().BaseAddress, Is.SameAs(address));
            Assert.That(load.Addr.AsAddrMode().Offset, Is.EqualTo(offset));
            Assert.That(load.Addr.AsAddrMode().Index, Is.Null);
            Assert.That(vector.Next, Is.Null);
            Assert.That(index.Next, Is.Null);
            Assert.That(node.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(TYP_INT, true)]
    [TestCase(TYP_I_IMPL, false)]
    public static void DynamicMemoryIndexIsZeroExtendedOnlyWhenNeeded(var_types type, bool needsCast)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[2].Type = type;
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var vector = compiler.gtNewIndir(TYP_SIMD16, address, GTF_IND_NONFAULTING);
            var index = compiler.gtNewLclvNode(type, 2);
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_INT, 16, vector, index);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, node) { IsUnusedValue = true };
            var block = NewBlock(address, vector, index, node, user);

            _ = LowerGetElement(NewLowering(compiler, block), node);

            var mode = user.Op1.AsIndir().Addr.AsAddrMode();
            var normalizedIndex = mode.Index;
            assert(normalizedIndex is not null);
            Assert.That(mode.Scale, Is.EqualTo(4));
            Assert.That(normalizedIndex.Type, Is.EqualTo(TYP_I_IMPL));
            if (needsCast)
            {
                var cast = normalizedIndex.AsCast();
                Assert.That(cast.Op1, Is.SameAs(index));
                Assert.That((cast.Flags & GTF_UNSIGNED) != 0, Is.True);
                Assert.That(index.Next, Is.SameAs(cast));
            }
            else
            {
                Assert.That(mode.Index, Is.SameAs(index));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StackLocalExtractionIncludesTheOriginalFieldOffset(bool field)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[1].Type = TYP_SIMD32;
            compiler.lvaTable[1].lvDoNotEnregister = true;
            GenTree vector = field
                ? compiler.gtNewLclFldNode(TYP_SIMD16, 1, 8)
                : compiler.gtNewLclvNode(TYP_SIMD32, 1);
            var index = compiler.gtNewIconNode(TYP_INT, 1);
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_INT, field ? (byte)16 : (byte)32, vector, index);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, node) { IsUnusedValue = true };
            var block = NewBlock(vector, index, node, user);

            Assert.That(LowerGetElement(NewLowering(compiler, block), node), Is.SameAs(user));

            var load = user.Op1.AsLclFld();
            Assert.That(load.LclNum, Is.EqualTo(1));
            Assert.That(load.LclOffs, Is.EqualTo(field ? 12 : 4));
            Assert.That(block.FirstNode, Is.SameAs(load));
            Assert.That(vector.Next, Is.Null);
            Assert.That(index.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(8, true)]
    [TestCase(int.MaxValue - 16, false)]
    public static void AddressOffsetFoldingRespectsTheNativeOverflowBoundary(int offset, bool folds)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var originalMode = new GenTreeAddrMode(TYP_I_IMPL, address, null, 0, offset);
            var vector = compiler.gtNewIndir(TYP_SIMD16, originalMode, GTF_IND_NONFAULTING);
            var index = compiler.gtNewIconNode(TYP_INT, 1);
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_INT, 16, vector, index);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, node) { IsUnusedValue = true };
            var block = NewBlock(address, originalMode, vector, index, node, user);

            _ = LowerGetElement(NewLowering(compiler, block), node);

            var mode = user.Op1.AsIndir().Addr.AsAddrMode();
            Assert.That(mode.BaseAddress, Is.SameAs(address));
            Assert.That(mode.Offset, Is.EqualTo(folds ? offset + 4 : offset));
            Assert.That(mode.Index, folds ? Is.Null : Is.SameAs(index));
            Assert.That(mode.Scale, Is.EqualTo(folds ? 0 : 4));
            Assert.That(originalMode.Next, Is.Null);
        });
    }

    [Test]
    public static void MovingAFaultingLoadRetainsExceptionOrder()
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var vector = compiler.gtNewIndir(TYP_SIMD16, address);
            var dividend = compiler.gtNewIconNode(TYP_INT, 42);
            var divisor = compiler.gtNewIconNode(TYP_INT, 0);
            var division = new GenTreeOp(GT_DIV, TYP_INT, dividend, divisor) {
                Flags = GTF_EXCEPT,
                IsUnusedValue = true,
            };
            var index = compiler.gtNewIconNode(TYP_INT, 1);
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_INT, 16, vector, index);
            var user = new GenTreeUnOp(GT_NEG, TYP_INT, node) { IsUnusedValue = true };
            var block = NewBlock(address, vector, dividend, divisor, division, index, node, user);

            _ = LowerGetElement(NewLowering(compiler, block), node);

            var nullcheck = dividend.Prev;
            assert(nullcheck is not null);
            Assert.That(nullcheck.Oper, Is.EqualTo(GT_NULLCHECK));
            Assert.That(nullcheck.AsIndir().Addr.AsLclVar().LclNum, Is.Zero);
            var load = user.Op1.AsIndir();
            Assert.That((load.Flags & GTF_IND_NONFAULTING) != 0, Is.True);
            Assert.That((load.Flags & GTF_EXCEPT) != 0, Is.False);
            Assert.That(division.Next, Is.SameAs(load.Addr));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerHWIntrinsicGetElement")]
    private static extern GenTree? LowerGetElement(Lowering lowering, GenTreeHWIntrinsic node);

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
        compiler.lvaTable = [new() { Type = TYP_I_IMPL }, new() { Type = TYP_SIMD16 }, new() { Type = TYP_INT }];
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
