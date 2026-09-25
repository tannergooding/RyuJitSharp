// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class IndirectStoreCoalescingTests
{
    [TestCase(0x1000, 0x1001, true, false, true, 0x2211)]
    [TestCase(0x1001, 0x1000, true, false, true, 0x1122)]
    [TestCase(0x1000, 0x1001, false, false, false, 0)]
    [TestCase(0x1000, 0x1001, true, true, false, 0)]
    public static void ByteStoresRespectModeOrderAndVolatility(int firstAddress, int secondAddress,
        bool optimized, bool volatileFirst, bool merged, int expected)
    {
        WithCompiler(optimized, compiler => {
            var firstAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, firstAddress);
            var firstValue = compiler.gtNewIconNode(var_types.TYP_BYTE, 0x11);
            var first = new GenTreeStoreInd(var_types.TYP_BYTE, firstAddr, firstValue);
            if (volatileFirst)
            {
                first.Flags |= GenTreeFlags.GTF_IND_VOLATILE;
            }
            var secondAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, secondAddress);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_BYTE, 0x22);
            var second = new GenTreeStoreInd(var_types.TYP_BYTE, secondAddr, secondValue);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstAddr, firstValue, first, secondAddr, secondValue, second, next);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, first);
            Assert.That(LowerNode(lowering, second), Is.SameAs(next));
            if (merged)
            {
                Assert.That(block.FirstNode, Is.SameAs(secondAddr));
                Assert.That(first.Next, Is.Null);
                Assert.That(second.Type, Is.EqualTo(var_types.TYP_USHORT));
                Assert.That(second.Addr.AsIntCon().IconValue, Is.EqualTo((nint)Math.Min(firstAddress, secondAddress)));
                Assert.That(second.Data.AsIntCon().IconValue, Is.EqualTo((nint)expected));
                Assert.That((second.Flags & GenTreeFlags.GTF_IND_ALLOW_NON_ATOMIC) != 0, Is.True);
            }
            else
            {
                Assert.That(first.Next, Is.SameAs(secondAddr));
                Assert.That(second.Type, Is.EqualTo(var_types.TYP_BYTE));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(0x1000, 0x1004, true)]
    [TestCase(0x1004, 0x1008, false)]
    public static void WideAbsoluteStoresRequireAlignedCombinedAddress(int firstAddress, int secondAddress,
        bool merged)
    {
        WithCompiler(optimized: true, compiler => {
            var firstAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, firstAddress);
            var firstValue = compiler.gtNewIconNode(var_types.TYP_INT, 0x11223344);
            var first = new GenTreeStoreInd(var_types.TYP_INT, firstAddr, firstValue);
            var secondAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, secondAddress);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_INT, 0x55667788);
            var second = new GenTreeStoreInd(var_types.TYP_INT, secondAddr, secondValue);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstAddr, firstValue, first, secondAddr, secondValue, second, next);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, first);
            _ = LowerNode(lowering, second);
            Assert.That(first.Next is null, Is.EqualTo(merged));
            Assert.That(second.Type, Is.EqualTo(merged ? var_types.TYP_LONG : var_types.TYP_INT));
            if (merged)
            {
                Assert.That(second.Data.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)0x5566778811223344L)));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void FloatingConstantsCoalesceByTheirRawBits()
    {
        WithCompiler(optimized: true, compiler => {
            var firstAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1000);
            var firstValue = new GenTreeDblCon(var_types.TYP_FLOAT, -0.0);
            var first = new GenTreeStoreInd(var_types.TYP_FLOAT, firstAddr, firstValue);
            var secondAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1004);
            var secondValue = new GenTreeDblCon(var_types.TYP_FLOAT, 1.0);
            var second = new GenTreeStoreInd(var_types.TYP_FLOAT, secondAddr, secondValue);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstAddr, firstValue, first, secondAddr, secondValue, second, next);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, first);
            _ = LowerNode(lowering, second);
            Assert.That(first.Next, Is.Null);
            Assert.That(second.Type, Is.EqualTo(var_types.TYP_LONG));
            Assert.That(second.Data.AsIntCon().IconValue, Is.EqualTo(unchecked((nint)0x3F80000080000000L)));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public static void ByrefAddressRequiresPermissionAndMatchingBase(bool allowNonAtomic, bool differentBase,
        bool merged)
    {
        WithCompiler(optimized: true, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_BYREF;
            compiler.lvaTable[1].Type = var_types.TYP_BYREF;
            compiler.lvaCount = 2;
            var firstBase = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var firstAddr = new GenTreeAddrMode(var_types.TYP_BYREF, firstBase, null, 1, 0);
            var firstValue = compiler.gtNewIconNode(var_types.TYP_INT, 0x11223344);
            var first = new GenTreeStoreInd(var_types.TYP_INT, firstAddr, firstValue);
            var secondBase = compiler.gtNewLclvNode(var_types.TYP_BYREF, differentBase ? 1 : 0);
            var secondAddr = new GenTreeAddrMode(var_types.TYP_BYREF, secondBase, null, 1, 4);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_INT, 0x55667788);
            var second = new GenTreeStoreInd(var_types.TYP_INT, secondAddr, secondValue);
            if (allowNonAtomic)
            {
                first.Flags |= GenTreeFlags.GTF_IND_ALLOW_NON_ATOMIC;
                second.Flags |= GenTreeFlags.GTF_IND_ALLOW_NON_ATOMIC;
            }
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstBase, firstAddr, firstValue, first, secondBase, secondAddr, secondValue, second, next);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, first);
            _ = LowerNode(lowering, second);
            Assert.That(first.Next is null, Is.EqualTo(merged));
            Assert.That(second.Type, Is.EqualTo(merged ? var_types.TYP_LONG : var_types.TYP_INT));
            Assert.That(secondAddr.Offset, Is.EqualTo(merged ? 0 : 4));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void ReturnBufferAddressIsPrivateEvenWhenUnaligned()
    {
        WithCompiler(optimized: true, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_BYREF;
            compiler.info.compRetBuffArg = 0;
            var firstBase = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var firstAddr = new GenTreeAddrMode(var_types.TYP_BYREF, firstBase, null, 1, 1);
            var firstValue = compiler.gtNewIconNode(var_types.TYP_INT, 0x11223344);
            var first = new GenTreeStoreInd(var_types.TYP_INT, firstAddr, firstValue);
            var secondBase = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var secondAddr = new GenTreeAddrMode(var_types.TYP_BYREF, secondBase, null, 1, 5);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_INT, 0x55667788);
            var second = new GenTreeStoreInd(var_types.TYP_INT, secondAddr, secondValue);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstBase, firstAddr, firstValue, first, secondBase, secondAddr, secondValue, second, next);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, first);
            _ = LowerNode(lowering, second);
            Assert.That(first.Next, Is.Null);
            Assert.That(second.Type, Is.EqualTo(var_types.TYP_LONG));
            Assert.That(secondAddr.Offset, Is.EqualTo(1));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void CompleteOverwriteMayRemoveNonconstantPreviousStore()
    {
        WithCompiler(optimized: true, compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_INT;
            var firstAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1000);
            var firstValue = compiler.gtNewLclvNode(var_types.TYP_INT, 0);
            var first = new GenTreeStoreInd(var_types.TYP_INT, firstAddr, firstValue);
            var secondAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1000);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_INT, 0x55667788);
            var second = new GenTreeStoreInd(var_types.TYP_INT, secondAddr, secondValue);
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstAddr, firstValue, first, secondAddr, secondValue, second, next);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, first);
            _ = LowerNode(lowering, second);
            Assert.That(block.FirstNode, Is.SameAs(secondAddr));
            Assert.That(second.Type, Is.EqualTo(var_types.TYP_INT));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void AdjacentLongStoresNeedNonAtomicPermissionForVectorWidening()
    {
        WithCompiler(optimized: true, compiler => {
            var firstAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1000);
            var firstValue = compiler.gtNewIconNode(var_types.TYP_LONG, unchecked((nint)0x1122334455667788L));
            var first = new GenTreeStoreInd(var_types.TYP_LONG, firstAddr, firstValue) {
                Flags = GenTreeFlags.GTF_IND_ALLOW_NON_ATOMIC,
            };
            var secondAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1008);
            var secondValue = compiler.gtNewIconNode(var_types.TYP_LONG, unchecked((nint)0x0102030405060708L));
            var second = new GenTreeStoreInd(var_types.TYP_LONG, secondAddr, secondValue) {
                Flags = GenTreeFlags.GTF_IND_ALLOW_NON_ATOMIC,
            };
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstAddr, firstValue, first, secondAddr, secondValue, second, next);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, first);
            _ = LowerNode(lowering, second);
            Assert.That(block.FirstNode, Is.SameAs(secondAddr));
            Assert.That(second.Type, Is.EqualTo(var_types.TYP_SIMD16));
            Assert.That(second.Data.AsVecCon().SimdVal.u64[0], Is.EqualTo(0x1122334455667788UL));
            Assert.That(second.Data.AsVecCon().SimdVal.u64[1], Is.EqualTo(0x0102030405060708UL));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void MatchingVectorsReuseEarlierValueWhenWideningIsUnavailable()
    {
        WithCompiler(optimized: true, compiler => {
            var firstAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1000);
            var firstValue = compiler.gtNewVconNode(var_types.TYP_SIMD16);
            firstValue.SimdVal.u64[0] = 0x1122334455667788;
            var first = new GenTreeStoreInd(var_types.TYP_SIMD16, firstAddr, firstValue) {
                Flags = GenTreeFlags.GTF_IND_ALLOW_NON_ATOMIC,
            };
            var secondAddr = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 0x1010);
            var secondValue = compiler.gtNewVconNode(var_types.TYP_SIMD16);
            secondValue.SimdVal = firstValue.SimdVal;
            var second = new GenTreeStoreInd(var_types.TYP_SIMD16, secondAddr, secondValue) {
                Flags = GenTreeFlags.GTF_IND_ALLOW_NON_ATOMIC,
            };
            var next = new GenTreeUnOp(genTreeOps.GT_RETURN, var_types.TYP_VOID, null);
            var block = NewBlock(firstAddr, firstValue, first, secondAddr, secondValue, second, next);
            var lowering = NewLowering(compiler, block);

            _ = LowerNode(lowering, first);
            _ = LowerNode(lowering, second);
            Assert.That(first.Data.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
            Assert.That(second.Data.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
            Assert.That(second.Data.AsLclVar().LclNum, Is.EqualTo(first.Data.AsLclVar().LclNum));
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    private static Lowering NewLowering(Compiler compiler, BasicBlock block)
    {
        var lowering = new Lowering(compiler, new LinearScan(compiler));
        LoweringBlock(lowering) = block;
        return lowering;
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);

    private static void WithCompiler(bool optimized, Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(!optimized);
        compiler.info.compRetBuffArg = Globals.BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[4];
        compiler.lvaCount = 1;
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
