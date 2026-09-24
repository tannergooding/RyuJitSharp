// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class FlagSupportTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void UnsignedAssignmentPreservesOtherFlags(bool initial, bool value)
    {
        WithCompiler(compiler => {
            var operand = compiler.gtNewLclvNode(TYP_INT, 0);
            var node = new GenTreeOp(GT_MULHI, TYP_INT, operand, compiler.gtNewIconNode(TYP_INT, 7));
            var flags = GenTreeFlags.GTF_DONT_CSE | GenTreeFlags.GTF_ORDER_SIDEEFF;
            node.Flags = flags | (initial ? GenTreeFlags.GTF_UNSIGNED : GenTreeFlags.GTF_EMPTY);

            node.IsUnsigned = value;

            Assert.That(node.IsUnsigned, Is.EqualTo(value));
            Assert.That(node.Flags, Is.EqualTo(flags | (value ? GenTreeFlags.GTF_UNSIGNED : GenTreeFlags.GTF_EMPTY)));
        });
    }

    [TestCase(GT_AND, true)]
    [TestCase(GT_OR, true)]
    [TestCase(GT_XOR, true)]
    [TestCase(GT_ADD, true)]
    [TestCase(GT_SUB, true)]
    [TestCase(GT_NEG, true)]
    [TestCase(GT_NOT, false)]
    [TestCase(GT_MUL, false)]
    public static void ScalarZeroFlagSupportDoesNotImplyFullCompareFlags(genTreeOps oper, bool expected)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            GenTree node = oper is GT_NEG or GT_NOT
                ? new GenTreeUnOp(oper, TYP_INT, value)
                : new GenTreeOp(oper, TYP_INT, value, compiler.gtNewIconNode(TYP_INT, 3));
            var flags = node.Flags;

            Assert.That(node.SupportsSettingZeroFlag(), Is.EqualTo(expected));
            Assert.That(node.SupportsSettingFlagsAsCompareToZero(), Is.False);
            Assert.That(node.Flags, Is.EqualTo(flags));
        });
    }

    [TestCase(GT_LSH, 0, false)]
    [TestCase(GT_LSH, 1, true)]
    [TestCase(GT_RSH, 0, false)]
    [TestCase(GT_RSH, 7, true)]
    [TestCase(GT_RSZ, 0, false)]
    [TestCase(GT_RSZ, 1, true)]
    [TestCase(GT_RSZ, null, false)]
    [TestCase(GT_ROL, 1, false)]
    [TestCase(GT_ROR, 1, false)]
    public static void ShiftsNeedKnownNonzeroCountsButRotatesNeverSetZeroFlag(
        genTreeOps oper, int? count, bool expected)
    {
        WithCompiler(compiler => {
            var value = compiler.gtNewLclvNode(TYP_INT, 0);
            GenTree amount = count.HasValue
                ? compiler.gtNewIconNode(TYP_INT, count.Value)
                : compiler.gtNewLclvNode(TYP_INT, 1);
            var node = new GenTreeOp(oper, TYP_INT, value, amount);

            Assert.That(node.SupportsSettingZeroFlag(), Is.EqualTo(expected));
            Assert.That(node.SupportsSettingFlagsAsCompareToZero(), Is.False);
            Assert.That(node.Op2, Is.SameAs(amount));
        });
    }

    [TestCase(NI_X86Base_BitScanForward, TYP_INT, INS_bsf, false)]
    [TestCase(NI_X86Base_BitScanReverse, TYP_INT, INS_bsr, false)]
    [TestCase(NI_X86Base_X64_BitScanForward, TYP_LONG, INS_bsf, false)]
    [TestCase(NI_X86Base_PopCount, TYP_UINT, INS_popcnt, true)]
    [TestCase(NI_X86Base_X64_PopCount, TYP_ULONG, INS_popcnt, true)]
    public static void ScalarIntrinsicsUseResultTypeAndHonorSourceBasedBitScanFlags(
        NamedIntrinsic id, var_types type, instruction expectedInstruction, bool supportsZero)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = type.ActualType;
            var value = compiler.gtNewLclvNode(type.ActualType, 0);
            var node = compiler.gtNewScalarHWIntrinsicNode(type, id, value);

            Assert.That(node.SimdBaseType, Is.EqualTo(TYP_UNKNOWN));
            Assert.That(HWIntrinsicInfo.lookupIns(node, null), Is.EqualTo(expectedInstruction));
            Assert.That(node.SupportsSettingZeroFlag(), Is.EqualTo(supportsZero));
            Assert.That(node.SupportsSettingFlagsAsCompareToZero(), Is.False);
        });
    }

    [TestCase(NI_X86Base_Add, TYP_SIMD16, TYP_INT, INS_paddd, false)]
    [TestCase(NI_X86Base_Add, TYP_SIMD16, TYP_FLOAT, INS_addps, false)]
    [TestCase(NI_X86Base_CompareScalarOrderedEqual, TYP_INT, TYP_DOUBLE, INS_comisd, true)]
    [TestCase(NI_AVX512_CompareEqualMask, TYP_MASK, TYP_FLOAT, INS_vcmpps, false)]
    [TestCase(NI_AVX512_AndMask, TYP_MASK, TYP_INT, INS_invalid, false)]
    public static void NonScalarCategoriesUseElementTypeEvenWithScalarOrMaskResults(
        NamedIntrinsic id, var_types resultType, var_types baseType, instruction expectedInstruction, bool supportsZero)
    {
        WithCompiler(compiler => {
            var operandType = id is NI_AVX512_AndMask ? TYP_MASK : TYP_SIMD16;
            compiler.lvaTable[0].Type = operandType;
            compiler.lvaTable[1].Type = operandType;
            var first = compiler.gtNewLclvNode(operandType, 0);
            var second = compiler.gtNewLclvNode(operandType, 1);
            var node = new GenTreeHWIntrinsic(resultType, id, baseType, 16, first, second);
            var flags = node.Flags;

            Assert.That(HWIntrinsicInfo.lookupIns(node, null), Is.EqualTo(expectedInstruction));
            Assert.That(node.SupportsSettingZeroFlag(), Is.EqualTo(supportsZero));
            Assert.That(node.SupportsSettingFlagsAsCompareToZero(), Is.False);
            Assert.That(node.Type, Is.EqualTo(resultType));
            Assert.That(node.SimdBaseType, Is.EqualTo(baseType));
            Assert.That(node.Flags, Is.EqualTo(flags));
        });
    }

    [TestCase(false, INS_movdqa32)]
    [TestCase(true, INS_vmovdqa64)]
    public static void NodeLookupForwardsCompilerEncodingSupport(bool evexEnabled, instruction expectedInstruction)
    {
        WithCompiler(compiler => {
            if (evexEnabled)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
            }
            compiler.lvaTable[0].Type = TYP_BYREF;
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_LoadAlignedVector128, TYP_LONG, 16, address);

            Assert.That(HWIntrinsicInfo.lookupIns(node, compiler), Is.EqualTo(expectedInstruction));
            Assert.That(HWIntrinsicInfo.lookupIns(node, null), Is.EqualTo(INS_movdqa32));
            Assert.That(node.SupportsSettingZeroFlag(), Is.False);
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_INT;
        compiler.lvaTable[1].Type = TYP_INT;
        JitTls.Compiler = compiler;
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
