// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenHardwareGenerationTests
{
    [TestCase(INS_pslldq, 16u, 16u, 255u)]
    [TestCase(INS_psrldq, 64u, 16u, 255u)]
    [TestCase(INS_palignr, 32u, 32u, 255u)]
    [TestCase(INS_pextrq, 16u, 1u, 1u)]
    [TestCase(INS_extractps, 16u, 3u, 3u)]
    [TestCase(INS_pinsrw, 16u, 7u, 7u)]
    [TestCase(INS_pinsrb, 16u, 15u, 15u)]
    [TestCase(INS_valignd, 64u, 15u, 15u)]
    [TestCase(INS_valignq, 32u, 3u, 3u)]
    [TestCase(INS_blendpd, 16u, 3u, 3u)]
    [TestCase(INS_shufpd, 32u, 15u, 15u)]
    [TestCase(INS_vpermilpd, 64u, 255u, 255u)]
    [TestCase(INS_blendps, 16u, 15u, 15u)]
    [TestCase(INS_vpblendd, 32u, 255u, 255u)]
    [TestCase(INS_mpsadbw, 32u, 63u, 63u)]
    [TestCase(INS_vextracti64x2, 64u, 3u, 3u)]
    [TestCase(INS_vinsertf32x4, 32u, 1u, 1u)]
    [TestCase(INS_vshuff32x4, 32u, 3u, 3u)]
    [TestCase(INS_vshufi64x2, 64u, 255u, 255u)]
    [TestCase(INS_vextractf32x8, 64u, 1u, 1u)]
    [TestCase(INS_vinserti64x4, 64u, 1u, 1u)]
    [TestCase(INS_dppd, 16u, 51u, 51u)]
    [TestCase(INS_pclmulqdq, 16u, 17u, 17u)]
    [TestCase(INS_vperm2f128, 32u, 187u, 187u)]
    [TestCase(INS_shufps, 16u, 255u, 255u)]
    public static void ImmediateReductionRetainsNativeLaneMasksAndSaturation(
        instruction ins, uint size, uint expectedMax, uint expectedMask)
    {
        Assert.That(CodeGen.GetImmediateMaxAndMask(ins, size, out var mask), Is.EqualTo(expectedMax));
        Assert.That(mask, Is.EqualTo(expectedMask));
    }

    [Test]
    public static void RoundingIgnoresAllBitsExceptRcAndPreservesMaskOptions()
    {
        insOpts[] rounding = [INS_OPTS_NONE, INS_OPTS_EVEX_er_rd, INS_OPTS_EVEX_er_ru, INS_OPTS_EVEX_er_rz];
        for (var mode = 0; mode <= 255; mode++)
        {
            var options = CodeGen.AddEmbMaskingMode(INS_OPTS_NONE, REG_K7, true);
            Assert.That(CodeGen.AddEmbRoundingMode(options, unchecked((sbyte)mode)),
                Is.EqualTo(options | rounding[mode & 3]));
        }
    }

    [TestCase(NI_X86Base_CompareEqual, TYP_FLOAT, 0)]
    [TestCase(NI_X86Base_CompareEqual, TYP_INT, -1)]
    [TestCase(NI_AVX_CompareGreaterThan, TYP_DOUBLE, 14)]
    [TestCase(NI_AVX512_CompareGreaterThanMask, TYP_INT, -1)]
    [TestCase(NI_AVX512_CompareGreaterThanMask, TYP_UINT, 6)]
    [TestCase(NI_AVX512_CompareLessThanMask, TYP_INT, 1)]
    [TestCase(NI_AVX512_CompareNotLessThanMask, TYP_UINT, 5)]
    [TestCase(NI_X86Base_CompareNotEqual, TYP_DOUBLE, 4)]
    [TestCase(NI_AVX512_CompareNotEqualMask, TYP_LONG, 4)]
    [TestCase(NI_X86Base_Ceiling, TYP_FLOAT, 2)]
    [TestCase(NI_AVX_Floor, TYP_DOUBLE, 1)]
    public static void ImplicitImmediatesRetainFloatingPredicatesAndIntegerOpcodeChoices(
        NamedIntrinsic intrinsic, var_types type, int expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, _) =>
        {
            EnableAvx2(compiler);
            Assert.That(HWIntrinsicInfo.lookupIval(compiler, intrinsic, type), Is.EqualTo(expected));
        });
    }

    [TestCase(NI_X86Base_ShuffleHigh, INS_pshufhw, 255, 255)]
    [TestCase(NI_X86Base_ShiftLeftLogical128BitLane, INS_pslldq, 16, 255)]
    [TestCase(NI_AES_CarrylessMultiply, INS_pclmulqdq, 17, 17)]
    public static void DynamicImmediateTablesKeepNativeIndicesAndSignedByteCases(
        NamedIntrinsic intrinsic, instruction ins, int max, int mask)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.fgFirstBB = new BasicBlock(null, null);
            compiler.fgFirstBB.SetFlags(BBF_HAS_LABEL);
            var cases = new List<sbyte>();
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genHWIntrinsicJumpTableFallback(intrinsic, ins, EA_16BYTE, REG_RAX, REG_R10, REG_R11,
                immediate =>
                {
                    cases.Add(immediate);
                    codeGen.Emitter.emitIns(INS_nop);
                });

            var table = codeGen.Emitter.emitConsDsc.dsdLast ?? throw new AssertionException("Missing immediate table.");
            Assert.That(table.dsType, Is.EqualTo(Emitter.dataSection.sectionType.blockRelative32));
            Assert.That(table.dsSize, Is.EqualTo((uint)((max + 1) * 4)));
            Assert.That(table.Blocks, Has.Length.EqualTo(max + 1));
            Assert.That(table.Blocks.Distinct().Count(), Is.EqualTo(max + 1));
            Assert.That(cases, Is.EqualTo(Enumerable.Range(0, max + 1)
                .Where(index => (index & mask) == index).Select(index => unchecked((sbyte)index))));
            var descriptors = CodeGenLocalHeapTests.AllDescriptors(firstGroup, codeGen);
            Assert.That(descriptors.Count(descriptor => descriptor.idIns() == INS_i_jmp), Is.EqualTo(1));
            Assert.That(descriptors.Count(descriptor => descriptor.idIns() == INS_jmp), Is.EqualTo(cases.Count));
            Assert.That(descriptors.Any(descriptor => descriptor.idIns() == INS_cmp), Is.EqualTo(max == 16));
            Assert.That(descriptors.Any(descriptor => descriptor.idIns() == INS_and), Is.EqualTo(mask != 255));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TableDispatchUsesTheUpperBitSourceAndFullImmediateByte(bool copyUpper)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            Enable(compiler, codeGen);
            var immediate = compiler.gtNewIconNode(TYP_INT, 255);
            immediate.IsContained = true;
            var type = copyUpper ? TYP_DOUBLE : TYP_SHORT;
            var intrinsic = copyUpper ? NI_X86Base_RoundCurrentDirectionScalar : NI_X86Base_ShuffleHigh;
            var source = new GenTreePhysReg(REG_XMM1, TYP_SIMD16) { RegNum = REG_XMM1 };
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, intrinsic, type, 16,
                copyUpper ? [source] : [source, immediate]) { RegNum = REG_XMM0 };

            codeGen.genHWIntrinsic(node);

            var descriptor = Descriptors(codeGen).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(copyUpper ? INS_roundsd : INS_pshufhw));
            Assert.That(InstructionConstant(codeGen.Emitter, descriptor), Is.EqualTo(copyUpper ? (nint)4 : -1));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_XMM1));
        });
    }

    [TestCase(REG_XMM0, 0xCA)]
    [TestCase(REG_XMM2, 0xE2)]
    [TestCase(REG_XMM3, 0xD8)]
    public static void TernaryLogicPermutesItsTruthTableWithAliasedInputs(regNumber target, int control)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            Enable(compiler, codeGen);
            var immediate = compiler.gtNewIconNode(TYP_INT, 0xCA);
            immediate.IsContained = true;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX512_TernaryLogic, TYP_INT, 16,
                new GenTreePhysReg(REG_XMM1, TYP_SIMD16) { RegNum = REG_XMM1 },
                new GenTreePhysReg(REG_XMM2, TYP_SIMD16) { RegNum = REG_XMM2 },
                new GenTreePhysReg(REG_XMM3, TYP_SIMD16) { RegNum = REG_XMM3 }, immediate) { RegNum = target };

            codeGen.genHWIntrinsic(node);

            var descriptor = Descriptors(codeGen).Last();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_vpternlogd));
            Assert.That(unchecked((byte)InstructionConstant(codeGen.Emitter, descriptor)), Is.EqualTo((byte)control));
            Assert.That(descriptor.idReg1(), Is.EqualTo(target));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EmbeddedMaskingRetargetsTheInnerOperationAndKeepsRounding(bool zeroMerge)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            Enable(compiler, codeGen);
            var rounding = compiler.gtNewIconNode(TYP_INT, 10);
            rounding.IsContained = true;
            var operation = new GenTreeHWIntrinsic(TYP_SIMD64, NI_AVX512_Add, TYP_FLOAT, 64,
                new GenTreePhysReg(REG_XMM1, TYP_SIMD64) { RegNum = REG_XMM1 },
                new GenTreePhysReg(REG_XMM2, TYP_SIMD64) { RegNum = REG_XMM2 }, rounding)
            {
                IsContained = true,
                RegNum = REG_NA,
            };
            operation.Flags |= GTF_HW_EM_OP;
            GenTree merge = zeroMerge
                ? new GenTreeVecCon(TYP_SIMD64) { IsContained = true }
                : new GenTreePhysReg(REG_XMM4, TYP_SIMD64) { RegNum = REG_XMM4 };
            var node = new GenTreeHWIntrinsic(TYP_SIMD64, NI_AVX512_BlendVariableMask, TYP_FLOAT, 64,
                merge, operation, new GenTreePhysReg(REG_K3, TYP_MASK) { RegNum = REG_K3 }) { RegNum = REG_XMM0 };

            codeGen.genHWIntrinsic(node);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(zeroMerge ? 1 : 2));
            Assert.That(operation.IsContained, Is.False);
            Assert.That(operation.RegNum, Is.EqualTo(REG_XMM0));
            Assert.That(operation.Operands.Length, Is.EqualTo(2));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_addps));
            Assert.That(descriptors[^1].idGetEvexAaaContext(), Is.EqualTo(3u));
            Assert.That(descriptors[^1].idIsEvexZContextSet(), Is.EqualTo(zeroMerge));
            Assert.That(descriptors[^1].idGetEvexbContext(), Is.EqualTo(2u));
        });
    }

    [TestCase(REG_XMM2, INS_vfmadd213ps)]
    [TestCase(REG_XMM3, INS_vfmadd231ps)]
    public static void DynamicRoundingUsesFmaAliasSelectionForEveryCase(regNumber target, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            Enable(compiler, codeGen);
            compiler.fgFirstBB = new BasicBlock(null, null);
            compiler.fgFirstBB.SetFlags(BBF_HAS_LABEL);
            var node = new GenTreeHWIntrinsic(TYP_SIMD64, NI_AVX512_FusedMultiplyAdd, TYP_FLOAT, 64,
                new GenTreePhysReg(REG_XMM1, TYP_SIMD64) { RegNum = REG_XMM1 },
                new GenTreePhysReg(REG_XMM2, TYP_SIMD64) { RegNum = REG_XMM2 },
                new GenTreePhysReg(REG_XMM3, TYP_SIMD64) { RegNum = REG_XMM3 },
                Register(compiler, TYP_INT, REG_RAX)) { RegNum = target };
            codeGen.InternalRegisters.Add(node, RBM_R10 | RBM_R11);
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genHWIntrinsic(node);

            var fma = CodeGenLocalHeapTests.AllDescriptors(firstGroup, codeGen)
                .Where(descriptor => descriptor.idIns() == expected).ToArray();
            Assert.That(fma, Has.Length.EqualTo(12));
            Assert.That(fma.Select(descriptor => descriptor.idReg1()), Is.All.EqualTo(target));
            Assert.That(fma.Select(descriptor => descriptor.idGetEvexbContext()),
                Is.EqualTo(Enumerable.Range(0, 12).Select(index => (uint)(index & 3))));
            Assert.That(node.Operands.Length, Is.EqualTo(3));
            var table = codeGen.Emitter.emitConsDsc.dsdLast ?? throw new AssertionException("Missing rounding table.");
            Assert.That(table.dsSize, Is.EqualTo(48u));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void TableDispatchKeepsLoadAndStoreAddresses(bool store)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            Enable(compiler, codeGen);
            var address = Register(compiler, TYP_BYREF, REG_RAX);
            var node = store
                ? new GenTreeHWIntrinsic(TYP_VOID, NI_X86Base_StoreAligned, TYP_FLOAT, 16,
                    address, new GenTreePhysReg(REG_XMM1, TYP_SIMD16) { RegNum = REG_XMM1 })
                : new GenTreeHWIntrinsic(TYP_SIMD16, NI_X86Base_LoadAlignedVector128, TYP_FLOAT, 16, address)
                {
                    RegNum = REG_XMM1,
                };

            codeGen.genHWIntrinsic(node);

            var descriptor = Descriptors(codeGen).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_movaps));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_XMM1));
            Assert.That(descriptor.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(store
                ? Emitter.insFormat.IF_AWR_RRD : Emitter.insFormat.IF_RWR_ARD));
        });
    }

    internal static void Enable(Compiler compiler, CodeGen codeGen)
    {
        foreach (var isa in new[] { InstructionSet_X86Base, InstructionSet_AVX, InstructionSet_AVX2,
            InstructionSet_AVX512, InstructionSet_AVX512_X64, InstructionSet_Vector128,
            InstructionSet_Vector256, InstructionSet_Vector512 })
        {
            compiler.opts.compSupportsISA.AddInstructionSet(isa);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
            compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
        }
        codeGen.Emitter.UseVexEncodings = true;
        codeGen.Emitter.UseEvexEncodings = true;
    }
}
