// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ComparisonContainmentTests
{
    [TestCase(GT_LT, false, true)]
    [TestCase(GT_GT, false, false)]
    [TestCase(GT_LT, true, false)]
    [TestCase(GT_GT, true, true)]
    public static void FloatingConditionsSelectTheEncodableMemoryOperandWithoutSwappingLir(
        genTreeOps oper, bool unordered, bool firstContained)
    {
        WithCompiler(compiler => {
            var address1 = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var first = compiler.gtNewIndir(TYP_DOUBLE, address1);
            var address2 = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var second = compiler.gtNewIndir(TYP_DOUBLE, address2);
            var comparison = new GenTreeOp(oper, TYP_INT, first, second);
            if (unordered)
            {
                comparison.Flags |= GTF_RELOP_NAN_UN;
            }
            comparison._vnPair.SetBoth(123);
            var flags = comparison.Flags;
            var block = NewBlock(address1, first, address2, second, comparison);

            ContainCheckCompare(NewLowering(compiler, block), comparison);

            Assert.That(first.IsContained, Is.EqualTo(firstContained));
            Assert.That(second.IsContained, Is.EqualTo(!firstContained));
            Assert.That(first.IsRegOptional || second.IsRegOptional, Is.False);
            Assert.That(comparison.Op1, Is.SameAs(first));
            Assert.That(comparison.Op2, Is.SameAs(second));
            Assert.That(comparison.Oper, Is.EqualTo(oper));
            Assert.That(comparison.Flags, Is.EqualTo(flags));
            Assert.That(comparison._vnPair.Liberal, Is.EqualTo(123u));
            Assert.That(first.Next, Is.SameAs(address2));
            Assert.That(second.Next, Is.SameAs(comparison));
        });
    }

    [TestCase(0L, false)]
    [TestCase(long.MinValue, true)]
    [TestCase(0x3FF0000000000000L, true)]
    [TestCase(0x7FF8000000000042L, true)]
    public static void FloatingConstantsPreserveSignedZeroAndNanPayloads(long bits, bool contained)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[2].Type = TYP_DOUBLE;
            var first = compiler.gtNewLclvNode(TYP_DOUBLE, 2);
            var second = compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(bits));
            var comparison = new GenTreeOp(GT_GT, TYP_INT, first, second);
            var block = NewBlock(first, second, comparison);

            ContainCheckCompare(NewLowering(compiler, block), comparison);

            Assert.That(second.IsContained, Is.EqualTo(contained));
            Assert.That(second.IsRegOptional, Is.EqualTo(!contained));
            Assert.That(first.IsContained || first.IsRegOptional, Is.False);
            Assert.That(second.IsBitwiseEqual(bits), Is.True);
        });
    }

    [TestCase(TYP_INT, TYP_INT, true)]
    [TestCase(TYP_SHORT, TYP_INT, false)]
    [TestCase(TYP_INT, TYP_UINT, false)]
    public static void ImmediateComparisonRequiresMatchingOperandTypesBeforeContainingMemory(
        var_types memoryType, var_types constantType, bool memoryContained)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var first = compiler.gtNewIndir(memoryType, address);
            var second = compiler.gtNewIconNode(constantType, 17);
            var comparison = new GenTreeOp(GT_EQ, TYP_INT, first, second) {
                Flags = first.Flags | GTF_UNSIGNED,
            };
            first._vnPair.SetBoth(111);
            second._vnPair.SetBoth(222);
            comparison._vnPair.SetBoth(333);
            var flags = comparison.Flags;
            var block = NewBlock(address, first, second, comparison);

            ContainCheckCompare(NewLowering(compiler, block), comparison);

            Assert.That(second.IsContained, Is.True);
            Assert.That(first.IsContained, Is.EqualTo(memoryContained));
            Assert.That(first.IsRegOptional, Is.False);
            Assert.That(first.Type, Is.EqualTo(memoryType));
            Assert.That(second.Type, Is.EqualTo(constantType));
            Assert.That(comparison.Type, Is.EqualTo(TYP_INT));
            Assert.That(comparison.Flags, Is.EqualTo(flags));
            Assert.That(first._vnPair.Liberal, Is.EqualTo(111u));
            Assert.That(second._vnPair.Liberal, Is.EqualTo(222u));
            Assert.That(comparison._vnPair.Liberal, Is.EqualTo(333u));
            Assert.That(comparison.Op1, Is.SameAs(first));
            Assert.That(comparison.Op2, Is.SameAs(second));
        });
    }

    [TestCase(2147483647L, false, true)]
    [TestCase(2147483648L, false, false)]
    [TestCase(-2147483648L, false, true)]
    [TestCase(-2147483649L, false, false)]
    [TestCase(17L, true, false)]
    public static void IntegerImmediateContainmentHonorsEncodingAndRelocationLimits(long value, bool reloc, bool contained)
    {
        WithCompiler(compiler => {
            compiler.opts.compReloc = reloc;
            compiler.lvaTable[2].Type = TYP_LONG;
            var first = compiler.gtNewLclvNode(TYP_LONG, 2);
            var second = compiler.gtNewIconNode(TYP_LONG, (nint)value);
            if (reloc)
            {
                second.Flags |= GTF_ICON_CONST_PTR;
            }
            var comparison = new GenTreeOp(GT_EQ, TYP_INT, first, second);
            var block = NewBlock(first, second, comparison);

            ContainCheckCompare(NewLowering(compiler, block), comparison);

            Assert.That(second.IsContained, Is.EqualTo(contained));
            Assert.That(second.IsRegOptional, Is.False);
            Assert.That(first.IsRegOptional, Is.True);
            Assert.That(second.IconValue, Is.EqualTo((nint)value));
        });
    }

    [TestCase(GT_EQ, true)]
    [TestCase(GT_CMP, true)]
    [TestCase(GT_TEST, true)]
    [TestCase(GT_EQ, false)]
    public static void IntegerComparisonsPreferTheSecondMemoryOperandAndOtherwiseUseTheFirst(
        genTreeOps oper, bool secondIsMemory)
    {
        WithCompiler(compiler => {
            var address1 = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var first = compiler.gtNewIndir(TYP_INT, address1);
            var address2 = compiler.gtNewLclvNode(TYP_BYREF, 1);
            GenTree second = secondIsMemory
                ? compiler.gtNewIndir(TYP_INT, address2)
                : compiler.gtNewLclvNode(TYP_INT, 2);
            var comparison = new GenTreeOp(oper, oper.IsCompare ? TYP_INT : TYP_VOID, first, second);
            var block = secondIsMemory
                ? NewBlock(address1, first, address2, second, comparison)
                : NewBlock(address1, first, second, comparison);

            ContainCheckCompare(NewLowering(compiler, block), comparison);

            Assert.That(first.IsContained, Is.EqualTo(!secondIsMemory));
            Assert.That(second.IsContained, Is.EqualTo(secondIsMemory));
            Assert.That(first.IsRegOptional || second.IsRegOptional, Is.False);
            Assert.That(comparison.Op1, Is.SameAs(first));
            Assert.That(comparison.Op2, Is.SameAs(second));
            Assert.That(comparison.Oper, Is.EqualTo(oper));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void InterveningMemoryWritesPreventContainmentButPreserveLegalSpillChoices(
        bool floating, bool interveningStore)
    {
        WithCompiler(compiler => {
            var type = floating ? TYP_DOUBLE : TYP_INT;
            compiler.lvaTable[2].Type = type;
            var first = compiler.gtNewLclvNode(type, 2);
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var second = compiler.gtNewIndir(type, address);
            var comparison = new GenTreeOp(GT_GT, TYP_INT, first, second);
            var block = NewBlock(first, address, second);
            if (interveningStore)
            {
                var storeAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
                GenTree value = floating
                    ? compiler.gtNewDconNode(TYP_DOUBLE, 1)
                    : compiler.gtNewIconNode(TYP_INT, 1);
                var store = compiler.gtNewStoreIndNode(type, storeAddress, value);
                block.InsertAtEnd(storeAddress);
                block.InsertAtEnd(value);
                block.InsertAtEnd(store);
            }
            block.InsertAtEnd(comparison);

            ContainCheckCompare(NewLowering(compiler, block), comparison);

            Assert.That(second.IsContained, Is.EqualTo(!interveningStore));
            Assert.That(first.IsContained, Is.False);
            Assert.That(first.IsRegOptional, Is.EqualTo(interveningStore && !floating));
            Assert.That(second.IsRegOptional, Is.EqualTo(interveningStore && floating));
        });
    }

    [Test]
    public static void AnUnsafeAddressExposedFloatingLocalIsNotRegOptional()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[2].Type = TYP_DOUBLE;
            compiler.lvaSetVarAddrExposed(2, AddressExposedReason.ESCAPE_ADDRESS);
            var first = compiler.gtNewDconNode(TYP_DOUBLE, 2);
            var second = compiler.gtNewLclvNode(TYP_DOUBLE, 2);
            var replacement = compiler.gtNewDconNode(TYP_DOUBLE, 3);
            var store = compiler.gtNewStoreLclVarNode(2, replacement);
            var comparison = new GenTreeOp(GT_GT, TYP_INT, first, second);
            var block = NewBlock(first, second, replacement, store, comparison);

            ContainCheckCompare(NewLowering(compiler, block), comparison);

            Assert.That(second.IsContained || second.IsRegOptional, Is.False);
            Assert.That(first.IsContained || first.IsRegOptional, Is.False);
            Assert.That(comparison.Op2, Is.SameAs(second));
        });
    }

    [Test]
    public static void AnUncontainedFirstIntegerConstantDoesNotBecomeRegOptional()
    {
        WithCompiler(compiler => {
            compiler.lvaTable[2].Type = TYP_LONG;
            var first = compiler.gtNewIconNode(TYP_LONG, unchecked((nint)(1L << 40)));
            var second = compiler.gtNewLclvNode(TYP_LONG, 2);
            var comparison = new GenTreeOp(GT_LT, TYP_INT, first, second);
            var block = NewBlock(first, second, comparison);

            ContainCheckCompare(NewLowering(compiler, block), comparison);

            Assert.That(first.IsContained || first.IsRegOptional, Is.False);
            Assert.That(second.IsContained, Is.False);
            Assert.That(second.IsRegOptional, Is.True);
        });
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckCompare")]
    private static extern void ContainCheckCompare(Lowering lowering, GenTreeOp comparison);

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
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_BYREF;
        compiler.lvaTable[1].Type = TYP_BYREF;
        compiler.lvaTable[2].Type = TYP_INT;
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
