// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64OperandContainmentTests
{
    [TestCase(GT_ADD, 4095L, true)]
    [TestCase(GT_SUB, -4096L, true)]
    [TestCase(GT_ADD, 4097L, false)]
    [TestCase(GT_EQ, 4096L, true)]
    [TestCase(GT_LT, long.MinValue, false)]
    [TestCase(GT_AND, 0x00FF00FF00FF00FFL, true)]
    [TestCase(GT_OR, 0L, false)]
    [TestCase(GT_XOR, -1L, false)]
    [TestCase(GT_TEST_EQ, 0xFFL, true)]
    [TestCase(GT_MUL, 1L, false)]
    public static void ImmediateContainmentUsesTheParentInstruction(genTreeOps oper, long value, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var source = compiler.gtNewLclvNode(TYP_LONG, 0);
            var constant = compiler.gtNewIconNode(TYP_LONG, (nint)value);
            var parent = new GenTreeOp(oper, oper.IsCompare ? TYP_INT : TYP_LONG, source, constant);
            Assert.That(lowering.IsContainableImmed(parent, constant), Is.EqualTo(expected));
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public static void AtomicImmediateDependsOnLseAvailability(bool atomics, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            if (atomics)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Atomics);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Atomics);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Atomics);
            }

            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var constant = compiler.gtNewIconNode(TYP_LONG, 4096);
            var parent = new GenTreeIndir(GT_XADD, TYP_LONG, address, constant);
            Assert.That(lowering.IsContainableImmed(parent, constant), Is.EqualTo(expected));
        });
    }

    [TestCase(GT_EQ, 0L, true)]
    [TestCase(GT_EQ, long.MinValue, false)]
    [TestCase(GT_ADD, 0L, false)]
    [TestCase(GT_EQ, 0x3FF0000000000000L, false)]
    public static void OnlyPositiveFloatingZeroCanBeAnImmediate(genTreeOps oper, long bits, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var source = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var constant = compiler.gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(bits));
            var parent = new GenTreeOp(oper, oper.IsCompare ? TYP_INT : TYP_DOUBLE, source, constant);
            Assert.That(lowering.IsContainableImmed(parent, constant), Is.EqualTo(expected));
        });
    }

    [TestCase(false, GTF_ICON_SECREL_OFFSET)]
    [TestCase(true, GTF_ICON_SECREL_OFFSET)]
    [TestCase(true, GTF_ICON_CLASS_HDL)]
    public static void RelocationsOnlyContainWindowsNativeAotSectionOffsets(bool nativeAot, GenTreeFlags handleKind)
    {
        WithLowering((compiler, lowering, block) => {
            compiler.opts.compReloc = true;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = nativeAot
                ? CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI
                : CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            var source = compiler.gtNewLclvNode(TYP_LONG, 0);
            var constant = compiler.gtNewIconNode(TYP_LONG, 8);
            constant.Flags |= handleKind;
            var parent = new GenTreeOp(GT_ADD, TYP_LONG, source, constant);

            Assert.That(constant.ImmedValNeedsReloc(compiler), Is.True);
            Assert.That(lowering.IsContainableImmed(parent, constant),
                Is.EqualTo(nativeAot && TargetOS.IsWindows && (handleKind == GTF_ICON_SECREL_OFFSET)));
        });
    }

    [TestCase(0L, true)]
    [TestCase(1L, false)]
    public static void LocalStoresOnlyContainIntegerZero(long value, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var constant = compiler.gtNewIconNode(TYP_LONG, (nint)value);
            var parent = compiler.gtNewStoreLclVarNode(0, constant);
            Assert.That(lowering.IsContainableImmed(parent, constant), Is.EqualTo(expected));
        });
    }

    [TestCase(TYP_INT, GT_EQ, 0, false, false)]
    [TestCase(TYP_INT, GT_EQ, 31, false, true)]
    [TestCase(TYP_INT, GT_EQ, 32, false, false)]
    [TestCase(TYP_LONG, GT_EQ, 32, false, true)]
    [TestCase(TYP_LONG, GT_TEST_EQ, 63, false, true)]
    [TestCase(TYP_LONG, GT_EQ, 64, false, false)]
    [TestCase(TYP_LONG, GT_EQ, -1, false, false)]
    [TestCase(TYP_LONG, GT_AND, 3, true, true)]
    [TestCase(TYP_LONG, GT_OR, 3, true, false)]
    [TestCase(TYP_LONG, GT_AND_NOT, 3, false, true)]
    public static void ShiftContainmentUsesOperandWidthAndParentFlagRules(
        var_types type, genTreeOps oper, int amount, bool setFlags, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var left = compiler.gtNewLclvNode(type, 0);
            var source = compiler.gtNewLclvNode(type, 1);
            var constant = compiler.gtNewIconNode(TYP_INT, amount);
            var shift = new GenTreeOp(GT_LSH, type, source, constant);
            var parent = new GenTreeOp(oper, oper.IsCompare ? TYP_INT : type, left, shift);
            parent.Flags |= setFlags ? GTF_SET_FLAGS : GTF_EMPTY;
            Append(block, left, source, constant, shift, parent);
            ContainCheckShiftRotate(lowering, shift);
            ContainCheckBinary(lowering, parent);

            Assert.That(constant.IsContained, Is.True);
            Assert.That(shift.IsContained, Is.EqualTo(expected));
        });
    }

    [TestCase(GT_AND, GT_ROL, 65, false, 63, true)]
    [TestCase(GT_AND, GT_ROL, 0, false, 64, true)]
    [TestCase(GT_XOR, GT_ROR, 129, false, 1, true)]
    [TestCase(GT_ADD, GT_ROL, 65, false, 63, false)]
    [TestCase(GT_AND, GT_ROR, 65, true, 1, true)]
    [TestCase(GT_OR, GT_ROL, 65, true, 63, false)]
    public static void RotateNormalizationPrecedesContainmentRejection(
        genTreeOps oper, genTreeOps rotateOper, int amount, bool setFlags, int normalized, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var left = compiler.gtNewLclvNode(TYP_LONG, 0);
            var source = compiler.gtNewLclvNode(TYP_LONG, 1);
            var constant = compiler.gtNewIconNode(TYP_INT, amount);
            var rotate = new GenTreeOp(rotateOper, TYP_LONG, source, constant);
            var parent = new GenTreeOp(oper, TYP_LONG, left, rotate);
            parent.Flags |= setFlags ? GTF_SET_FLAGS : GTF_EMPTY;
            Append(block, left, source, constant, rotate, parent);
            ContainCheckShiftRotate(lowering, rotate);
            ContainCheckBinary(lowering, parent);

            Assert.That(rotate.Oper, Is.EqualTo(GT_ROR));
            Assert.That(constant.IconValue, Is.EqualTo((nint)normalized));
            Assert.That(rotate.IsContained, Is.EqualTo(expected));
        });
    }

    [TestCase(GTF_EMPTY, GTF_EMPTY, false, true)]
    [TestCase(GTF_OVERFLOW, GTF_EMPTY, false, false)]
    [TestCase(GTF_SET_FLAGS, GTF_EMPTY, false, false)]
    [TestCase(GTF_EMPTY, GTF_OVERFLOW, false, false)]
    [TestCase(GTF_EMPTY, GTF_SET_FLAGS, false, false)]
    [TestCase(GTF_EMPTY, GTF_EMPTY, true, false)]
    public static void MultiplyContainmentPreservesOverflowFlagsAndOperandRequirements(
        GenTreeFlags parentFlags, GenTreeFlags childFlags, bool containedOperand, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var a = compiler.gtNewLclvNode(TYP_LONG, 0);
            var b = compiler.gtNewLclvNode(TYP_LONG, 1);
            var multiply = new GenTreeOp(GT_MUL, TYP_LONG, a, b);
            var c = compiler.gtNewLclvNode(TYP_LONG, 1);
            var parent = new GenTreeOp(GT_ADD, TYP_LONG, multiply, c);
            parent.Flags |= parentFlags;
            multiply.Flags |= childFlags;
            a.IsContained = containedOperand;
            Append(block, a, b, multiply, c, parent);
            ContainCheckBinary(lowering, parent);

            Assert.That(multiply.IsContained, Is.EqualTo(expected));
            Assert.That(parent.Op2, Is.SameAs(expected ? multiply : c));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MultiplyCannotMoveAcrossAnOperandRedefinition(bool modified)
    {
        WithLowering((compiler, lowering, block) => {
            var left = compiler.gtNewLclvNode(TYP_LONG, 1);
            var a = compiler.gtNewLclvNode(TYP_LONG, 0);
            var b = compiler.gtNewLclvNode(TYP_LONG, 1);
            var multiply = new GenTreeOp(GT_MUL, TYP_LONG, a, b);
            var parent = new GenTreeOp(GT_SUB, TYP_LONG, left, multiply);
            Append(block, left, a, b, multiply);
            if (modified)
            {
                var value = compiler.gtNewIconNode(TYP_LONG, 42);
                var store = compiler.gtNewStoreLclVarNode(0, value);
                Append(block, value, store);
            }
            block.InsertAtEnd(parent);
            ContainCheckBinary(lowering, parent);

            Assert.That(multiply.IsContained, Is.EqualTo(!modified));
        });
    }

    [TestCase(TYP_INT, TYP_LONG, TYP_LONG, true)]
    [TestCase(TYP_INT, TYP_INT, TYP_BYTE, true)]
    [TestCase(TYP_INT, TYP_INT, TYP_SHORT, true)]
    [TestCase(TYP_LONG, TYP_INT, TYP_INT, false)]
    public static void CompareContainmentOnlyAcceptsSupportedRegisterExtensions(
        var_types sourceType, var_types resultType, var_types castType, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var left = compiler.gtNewLclvNode(resultType, 0);
            var source = compiler.gtNewLclvNode(sourceType, 1);
            var cast = new GenTreeCast(resultType, source, false, castType);
            var parent = new GenTreeOp(GT_EQ, TYP_INT, left, cast);
            Append(block, left, source, cast, parent);
            ContainCheckBinary(lowering, parent);

            Assert.That(cast.IsContained, Is.EqualTo(expected));
        });
    }

    [TestCase(GT_ADD, true, true)]
    [TestCase(GT_CMP, true, true)]
    [TestCase(GT_EQ, true, false)]
    [TestCase(GT_EQ, false, false)]
    public static void CastContainmentPreservesMemoryExtensionPrecedence(
        genTreeOps oper, bool containedLoad, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var left = compiler.gtNewLclvNode(TYP_LONG, 1);
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var load = new GenTreeIndir(GT_IND, TYP_INT, address) { IsContained = containedLoad };
            var cast = new GenTreeCast(TYP_LONG, load, false, TYP_LONG);
            var parent = new GenTreeOp(oper, oper.IsCompare ? TYP_INT : TYP_LONG, left, cast);
            Append(block, left, address, load, cast, parent);
            ContainCheckBinary(lowering, parent);

            Assert.That(cast.IsContained, Is.EqualTo(expected));
            Assert.That(load.IsContained, Is.EqualTo(containedLoad && !expected));
        });
    }

    [TestCase(GT_EQ, true)]
    [TestCase(GT_NE, true)]
    [TestCase(GT_LT, false)]
    public static void NegatedShiftCanOnlyFoldIntoEquality(genTreeOps oper, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var left = compiler.gtNewLclvNode(TYP_LONG, 0);
            var source = compiler.gtNewLclvNode(TYP_LONG, 1);
            var amount = compiler.gtNewIconNode(TYP_INT, 3);
            var shift = new GenTreeOp(GT_RSH, TYP_LONG, source, amount);
            var negate = new GenTreeUnOp(GT_NEG, TYP_LONG, shift);
            var parent = new GenTreeOp(oper, TYP_INT, left, negate);
            Append(block, left, source, amount, shift, negate, parent);
            ContainCheckShiftRotate(lowering, shift);
            ContainCheckNeg(lowering, negate);
            ContainCheckBinary(lowering, parent);

            Assert.That(shift.IsContained, Is.True);
            Assert.That(negate.IsContained, Is.EqualTo(expected));
        });
    }

    [TestCase(GT_NEG, GT_MUL, true, true)]
    [TestCase(GT_NEG, GT_LSH, true, false)]
    [TestCase(GT_NOT, GT_LSH, true, false)]
    [TestCase(GT_NOT, GT_LSH, false, true)]
    public static void UnaryContainmentPreservesTheMinoptsMultiplyException(
        genTreeOps oper, genTreeOps childOper, bool minopts, bool expected)
    {
        WithLowering((compiler, lowering, block) => {
            var source = compiler.gtNewLclvNode(TYP_LONG, 0);
            GenTree other = childOper is GT_MUL
                ? compiler.gtNewLclvNode(TYP_LONG, 1)
                : compiler.gtNewIconNode(TYP_INT, 3);
            var child = new GenTreeOp(childOper, TYP_LONG, source, other);
            var parent = new GenTreeUnOp(oper, TYP_LONG, child);
            Append(block, source, other, child, parent);
            if (childOper is GT_LSH)
            {
                ContainCheckShiftRotate(lowering, child);
            }

            if (oper is GT_NEG)
            {
                ContainCheckNeg(lowering, parent);
            }
            else
            {
                ContainCheckNot(lowering, parent);
            }
            Assert.That(child.IsContained, Is.EqualTo(expected));
        }, minopts);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void BinaryImmediateSwapsEvenUnderMinopts(bool minopts)
    {
        WithLowering((compiler, lowering, block) => {
            var immediate = compiler.gtNewIconNode(TYP_LONG, 4096);
            var source = compiler.gtNewLclvNode(TYP_LONG, 0);
            var parent = new GenTreeOp(GT_ADD, TYP_LONG, immediate, source);
            Append(block, immediate, source, parent);
            ContainCheckBinary(lowering, parent);

            Assert.That(immediate.IsContained, Is.True);
            Assert.That(parent.Op1, Is.SameAs(source));
            Assert.That(parent.Op2, Is.SameAs(immediate));
        }, minopts);
    }

    private static void Append(BasicBlock block, params GenTree[] nodes)
    {
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckBinary")]
    private static extern void ContainCheckBinary(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckShiftRotate")]
    private static extern void ContainCheckShiftRotate(Lowering lowering, GenTreeOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckNeg")]
    private static extern void ContainCheckNeg(Lowering lowering, GenTreeUnOp node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckNot")]
    private static extern void ContainCheckNot(Lowering lowering, GenTreeUnOp node);

    private static void WithLowering(Action<Compiler, Lowering, BasicBlock> action, bool minopts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.opts.compFlags = minopts ? 0 : CLFLG_REGVAR;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_LONG;
        compiler.lvaTable[1].Type = TYP_LONG;
        JitTls.Compiler = compiler;

        try
        {
            compiler.codeGen = new CodeGen(compiler);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
