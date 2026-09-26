// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class HardwareBaseGenerationTests
{
    [TestCase(TYP_FLOAT, false, INS_insertps)]
    [TestCase(TYP_DOUBLE, false, INS_movq)]
    [TestCase(TYP_FLOAT, true, INS_movaps)]
    [TestCase(TYP_DOUBLE, true, INS_movaps)]
    public static void FloatingScalarCreationZerosUpperLanesOnlyForTheSafeForm(
        var_types type, bool unsafeForm, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = new GenTreeDblCon(type, 1.0) { RegNum = REG_XMM1 };
            var node = new GenTreeHWIntrinsic(TYP_SIMD16,
                unsafeForm ? NI_Vector_CreateScalarUnsafe : NI_Vector_CreateScalar, type, 16, source)
            {
                RegNum = REG_XMM0,
            };

            codeGen.genBaseIntrinsic(node, INS_OPTS_NONE);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM0));
            if (expected == INS_insertps)
            {
                Assert.That(id.idReg3(), Is.EqualTo(REG_XMM1));
                Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)14));
            }
            else
            {
                Assert.That(id.idReg2(), Is.EqualTo(REG_XMM1));
            }
        });
    }

    [Test]
    public static void Int64ScalarMemoryCreationUsesMovq()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_LONG;
            var source = compiler.gtNewLclvNode(TYP_LONG, 0);
            source.IsContained = true;
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_CreateScalar, TYP_LONG, 16, source)
            {
                RegNum = REG_XMM0,
            };

            codeGen.genBaseIntrinsic(node, INS_OPTS_NONE);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_movq));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
        });
    }

    [TestCase(TYP_FLOAT, 1, INS_movshdup)]
    [TestCase(TYP_FLOAT, 2, INS_unpckhps)]
    [TestCase(TYP_FLOAT, 3, INS_shufps)]
    [TestCase(TYP_DOUBLE, 1, INS_unpckhpd)]
    public static void FloatingElementConstantsPreserveNativeShuffleSelection(
        var_types type, int lane, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var index = compiler.gtNewIconNode(TYP_I_IMPL, lane);
            index.IsContained = true;
            var node = new GenTreeHWIntrinsic(type, NI_Vector_GetElement, type, 16,
                Vector(TYP_SIMD16, REG_XMM1), index) { RegNum = REG_XMM0 };

            codeGen.genBaseIntrinsic(node, INS_OPTS_NONE);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM1));
            if (expected == INS_shufps)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)(-1)));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void VariableElementAccessUsesTheCheckedIndexAndSimdStackTemp(bool replace)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaSimdInitTempVarNum = 0;
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[0].StackOffset = -32;
            var index = new GenTreePhysReg(REG_RCX) { RegNum = REG_RCX };
            var vector = Vector(TYP_SIMD16, REG_XMM1);
            var node = replace
                ? new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_WithElement, TYP_INT, 16,
                    vector, index, Register(compiler, TYP_INT, REG_RDX)) { RegNum = REG_XMM0 }
                : new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_INT, 16,
                    vector, index) { RegNum = REG_RAX };

            codeGen.genBaseIntrinsic(node, INS_OPTS_NONE);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(replace ? 3 : 2));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_XMM1));
            Assert.That(ids[0].idOpSize(), Is.EqualTo(EA_16BYTE));
            var address = ids[1].idAddr().iiaAddrMode;
            Assert.That(address.amBaseReg, Is.EqualTo(REG_RBP));
            Assert.That(address.amIndxReg, Is.EqualTo(REG_RCX));
            Assert.That(address.amDisp, Is.EqualTo(-32));
            Assert.That(ids[1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(ids[1].idReg1(), Is.EqualTo(replace ? REG_RDX : REG_RAX));
            if (replace)
            {
                Assert.That(ids[2].idReg1(), Is.EqualTo(REG_XMM0));
                Assert.That(ids[2].idOpSize(), Is.EqualTo(EA_16BYTE));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SmallSignedToScalarExtendsOnlyAfterAnIntegerVectorRegisterMove(bool memory)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            GenTree source = memory ? compiler.gtNewLclvNode(TYP_SIMD16, 0) : Vector(TYP_SIMD16, REG_XMM1);
            source.IsContained = memory;
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_ToScalar, TYP_BYTE, 16, source)
            {
                RegNum = REG_RAX,
            };

            codeGen.genBaseIntrinsic(node, INS_OPTS_NONE);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo(memory
                ? new[] { INS_movsx } : [INS_movd32, INS_movsx]));
            Assert.That(ids[^1].idOpSize(), Is.EqualTo(EA_1BYTE));
            Assert.That(ids[^1].idReg1(), Is.EqualTo(REG_RAX));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ContainedVectorFieldsRetainFieldAndElementOffsets(bool variableIndex)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.lvaTable[0].Type = TYP_SIMD16;
            compiler.lvaTable[0].StackOffset = -32;
            var source = new GenTreeLclFld(GT_LCL_FLD, TYP_SIMD16, 0, 4) { IsContained = true };
            GenTree index = variableIndex
                ? new GenTreePhysReg(REG_RCX) { RegNum = REG_RCX }
                : compiler.gtNewIconNode(TYP_I_IMPL, 3);
            index.IsContained = !variableIndex;
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_SHORT, 16, source, index)
            {
                RegNum = REG_RAX,
            };

            codeGen.genBaseIntrinsic(node, INS_OPTS_NONE);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_movsx));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_2BYTE));
            var address = id.idAddr().iiaAddrMode;
            Assert.That(address.amBaseReg, Is.EqualTo(REG_RBP));
            Assert.That(address.amIndxReg, Is.EqualTo(variableIndex ? REG_RCX : REG_NA));
            Assert.That(address.amDisp, Is.EqualTo(variableIndex ? -28 : -22));
        });
    }

    [TestCase(NI_Vector_ToVector256, TYP_SIMD32, 16, 1, EA_16BYTE)]
    [TestCase(NI_Vector_ToVector256Unsafe, TYP_SIMD32, 16, 0, EA_32BYTE)]
    [TestCase(NI_Vector_ToVector512, TYP_SIMD64, 32, 1, EA_32BYTE)]
    [TestCase(NI_Vector_ToVector512Unsafe, TYP_SIMD64, 32, 0, EA_64BYTE)]
    public static void WideningRetainsRequiredSameRegisterZeroExtension(
        NamedIntrinsic intrinsic, var_types type, byte sourceSize, int count, emitAttr expectedSize)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            EnableAvx2(compiler);
            var node = new GenTreeHWIntrinsic(type, intrinsic, TYP_FLOAT, sourceSize,
                Vector(Compiler.GetSimdTypeForSize(sourceSize), REG_XMM1)) { RegNum = REG_XMM1 };

            codeGen.genBaseIntrinsic(node, INS_OPTS_NONE);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(count));
            if (count != 0)
            {
                Assert.That(ids[0].idIns(), Is.EqualTo(INS_movaps));
                Assert.That(ids[0].idOpSize(), Is.EqualTo(expectedSize));
            }
        });
    }

    [TestCase(NI_X86Base_Pause, INS_pause)]
    [TestCase(NI_X86Base_StoreFence, INS_sfence)]
    [TestCase(NI_X86Base_LoadFence, INS_lfence)]
    [TestCase(NI_X86Base_MemoryFence, INS_mfence)]
    public static void ZeroOperandIntrinsicsRecordTheirNativeInstruction(NamedIntrinsic intrinsic, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_VOID, intrinsic, TYP_UNKNOWN, 0) { RegNum = REG_NA };

            codeGen.genX86BaseIntrinsic(node, INS_OPTS_NONE);

            Assert.That(Descriptors(codeGen).Single().idIns(), Is.EqualTo(expected));
        });
    }

    [TestCase(NI_X86Base_BitScanForward, TYP_INT, INS_bsf)]
    [TestCase(NI_X86Base_X64_BitScanReverse, TYP_LONG, INS_bsr)]
    public static void BitScanUsesTheScalarResultWidth(NamedIntrinsic intrinsic, var_types type, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(type, intrinsic, type, 0,
                Register(compiler, type, REG_RCX)) { RegNum = REG_RAX };

            codeGen.genX86BaseIntrinsic(node, INS_OPTS_NONE);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idOpSize(), Is.EqualTo(type.EmitSize));
            Assert.That(id.idReg2(), Is.EqualTo(REG_RCX));
        });
    }

    [TestCase(TYP_LONG, INS_imulEAX)]
    [TestCase(TYP_ULONG, INS_mulEAX)]
    public static void BigMulReusesASecondOperandAlreadyInRax(var_types type, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_STRUCT, NI_X86Base_X64_BigMul, type, 0,
                Register(compiler, type, REG_RCX), Register(compiler, type, REG_RAX)) { RegNum = REG_RAX };
            node.SetRegNumByIdx(REG_RDX, 1);

            codeGen.genX86BaseIntrinsic(node, INS_OPTS_NONE);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_8BYTE));
        });
    }

    [TestCase(TYP_LONG, INS_idiv)]
    [TestCase(TYP_ULONG, INS_div)]
    public static void DivRemCopiesTheLowAndHighDividendInNativeOrder(var_types type, instruction expected)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_STRUCT, NI_X86Base_X64_DivRem, type, 0,
                Register(compiler, type, REG_R8), Register(compiler, type, REG_R9),
                Register(compiler, type, REG_RCX)) { RegNum = REG_RAX };
            node.SetRegNumByIdx(REG_RDX, 1);

            codeGen.genX86BaseIntrinsic(node, INS_OPTS_NONE);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo([INS_mov, INS_mov, expected]));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(ids[0].idReg2(), Is.EqualTo(REG_R8));
            Assert.That(ids[1].idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(ids[1].idReg2(), Is.EqualTo(REG_R9));
            Assert.That(ids[2].idReg1(), Is.EqualTo(REG_RCX));
        });
    }

    [TestCase(TYP_UBYTE, EA_1BYTE)]
    [TestCase(TYP_USHORT, EA_2BYTE)]
    public static void Crc32RetainsTheSecondArgumentsNarrowWidth(var_types baseType, emitAttr attr)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_X86Base_Crc32, baseType, 0,
                Register(compiler, TYP_INT, REG_R8), Register(compiler, TYP_INT, REG_RCX)) { RegNum = REG_RAX };

            codeGen.genX86BaseIntrinsic(node, INS_OPTS_NONE);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo([INS_mov, INS_crc32]));
            Assert.That(ids[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(ids[1].idOpSize(), Is.EqualTo(attr));
            Assert.That(ids[1].idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(ids[1].idReg2(), Is.EqualTo(REG_RCX));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void Crc32SelectsApxForAnExtendedDestinationOrSource(bool extendedSource)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.UseRex2Encodings = true;
            var target = extendedSource ? REG_RAX : REG_R16;
            var source = extendedSource ? REG_R16 : REG_RCX;
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_X86Base_Crc32, TYP_UINT, 0,
                Register(compiler, TYP_INT, REG_R8), Register(compiler, TYP_INT, source)) { RegNum = target };

            codeGen.genX86BaseIntrinsic(node, INS_OPTS_NONE);

            var id = Descriptors(codeGen)[^1];
            Assert.That(id.idIns(), Is.EqualTo(INS_crc32_apx));
            Assert.That(id.idReg1(), Is.EqualTo(target));
            Assert.That(id.idReg2(), Is.EqualTo(source));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void PopCountBreaksOnlyFalseDestinationDependencies(bool alias)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_LONG, NI_X86Base_X64_PopCount, TYP_LONG, 0,
                Register(compiler, TYP_LONG, alias ? REG_RAX : REG_RCX)) { RegNum = REG_RAX };

            codeGen.genX86BaseIntrinsic(node, INS_OPTS_NONE);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo(alias
                ? new[] { INS_popcnt } : [INS_xor, INS_popcnt]));
            if (!alias)
            {
                Assert.That(ids[0].idOpSize(), Is.EqualTo(EA_4BYTE));
                Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RAX));
                Assert.That(ids[0].idReg2(), Is.EqualTo(REG_RAX));
            }
        });
    }

    [TestCase(0)]
    [TestCase(127)]
    [TestCase(255)]
    public static void ExtractImmediatesPreserveTheNativeSignedByte(int value)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var immediate = compiler.gtNewIconNode(TYP_INT, value);
            immediate.IsContained = true;
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_X86Base_Extract, TYP_UBYTE, 16,
                Vector(TYP_SIMD16, REG_XMM1), immediate) { RegNum = REG_RAX };

            codeGen.genX86BaseIntrinsic(node, INS_OPTS_NONE);

            var id = Descriptors(codeGen).Single();
            Assert.That(id.idIns(), Is.EqualTo(INS_pextrb));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM1));
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)unchecked((sbyte)value)));
        });
    }

    [TestCase(TYP_INT, 0, 16)]
    [TestCase(TYP_UINT, 0, 16)]
    [TestCase(TYP_INT, 1, 16)]
    [TestCase(TYP_UINT, 1, 16)]
    [TestCase(TYP_INT, 2, 32)]
    [TestCase(TYP_UINT, 2, 32)]
    public static void VectorIntegerDivisionRetainsIsaSpecificWidthsAndExceptionChecks(
        var_types baseType, int isa, byte size)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            codeGen.Emitter.UseVexEncodings = isa != 0;
            codeGen.Emitter.UseEvexEncodings = isa == 2;
            if (isa != 0)
            {
                EnableAvx2(compiler);
            }
            if (isa == 2)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            }
            var type = Compiler.GetSimdTypeForSize(size);
            AllFloat(compiler) = SRBM_ALLFLOAT_INIT;
            codeGen.CopyRegisterInfo();
            var node = new GenTreeHWIntrinsic(type, NI_Vector_op_Division, baseType, size,
                Vector(type, REG_XMM4), Vector(type, REG_XMM5)) { RegNum = REG_XMM6 };
            codeGen.InternalRegisters.Add(node, isa == 2 ? RBM_XMM1 | RBM_XMM2 : RBM_XMM0 | RBM_XMM1 | RBM_XMM2);
            var firstGroup = codeGen.Emitter.emitCurIG;

            codeGen.genBaseIntrinsic(node, INS_OPTS_NONE);

            var ids = CodeGenLocalHeapTests.AllDescriptors(firstGroup, codeGen);
            Assert.That(ids.Count(id => id.idIns() == INS_call), Is.EqualTo(baseType == TYP_INT ? 2 : 1));
            var divide = ids.Single(id => id.idIns() == INS_divpd);
            Assert.That(divide.idOpSize(), Is.EqualTo(isa == 2 ? EA_64BYTE : isa == 1 ? EA_32BYTE : EA_16BYTE));
            var conversion = baseType == TYP_UINT && isa == 2 ? INS_vcvttpd2udq : INS_cvttpd2dq;
            Assert.That(ids.Count(id => id.idIns() == conversion), Is.EqualTo(1));
            Assert.That(ids.FindIndex(id => id.idIns() == INS_call),
                Is.LessThan(ids.FindIndex(id => id.idIns() == INS_divpd)));
            if (baseType == TYP_UINT && isa != 2)
            {
                Assert.That(ids.Any(id => id.idIns() == (isa == 1 ? INS_vblendvps : INS_blendvps)), Is.True);
            }
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask AllFloat(Compiler compiler);

    [TestCase(2u, 6)]
    [TestCase(4u, 4)]
    public static void MaskClearingUsesByteLeftThenRightShifts(uint count, int shift)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.ClearUnusedMaskBits(REG_K3, count);

            var ids = Descriptors(codeGen);
            Assert.That(ids.Select(id => id.idIns()), Is.EqualTo([INS_kshiftlb, INS_kshiftrb]));
            foreach (var id in ids)
            {
                Assert.That(id.idOpSize(), Is.EqualTo(EA_8BYTE));
                Assert.That(id.idReg1(), Is.EqualTo(REG_K3));
                Assert.That(id.idReg2(), Is.EqualTo(REG_K3));
                Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)shift));
            }
        });
    }

#if DEBUG
    [Test]
    public static void DspCodeRecordsMaskClearingShifts()
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.dspCode = true;

            var diagnostic = InstructionRecordingTestSupport.Capture(
                () => codeGen.ClearUnusedMaskBits(REG_K3, 2));
            Assert.That(Descriptors(codeGen), Is.Not.Empty);
            Assert.That(diagnostic, Is.Not.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DspCodeRecordsBaseHardwareInstructions(bool x86)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var node = x86
                ? new GenTreeHWIntrinsic(TYP_LONG, NI_X86Base_X64_PopCount, TYP_LONG, 0,
                    Register(compiler, TYP_LONG, REG_RCX)) { RegNum = REG_RAX }
                : new GenTreeHWIntrinsic(TYP_SIMD16, NI_Vector_CreateScalar, TYP_FLOAT, 16,
                    new GenTreeDblCon(TYP_FLOAT, 1.0) { RegNum = REG_XMM1 }) { RegNum = REG_XMM0 };
            compiler.opts.dspCode = true;

            void Generate()
            {
                if (x86)
                {
                    codeGen.genX86BaseIntrinsic(node, INS_OPTS_NONE);
                }
                else
                {
                    codeGen.genBaseIntrinsic(node, INS_OPTS_NONE);
                }
            }

            var diagnostic = InstructionRecordingTestSupport.Capture(Generate);
            Assert.That(Descriptors(codeGen), Is.Not.Empty);
            Assert.That(diagnostic, Is.Not.Empty);
        });
    }
#endif

    private static GenTreeVecCon Vector(var_types type, regNumber reg)
    {
        return new GenTreeVecCon(type) { RegNum = reg };
    }
}
