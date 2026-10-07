// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.GenTreeBlk;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class RiscVCodeGenPortTests
{
    private const string RiscVRecorderDebugBoundary = "Instruction sanity checking outside AMD64 is not ported.";

#if DEBUG
    [TestCase(TYP_INT, -2048, RiscVRecorderDebugBoundary, true)]
    [TestCase(TYP_INT, 2047, RiscVRecorderDebugBoundary, true)]
    [TestCase(TYP_BYREF, 42, RiscVRecorderDebugBoundary, true)]
    [TestCase(TYP_INT, -2049, "Instruction sanity checking outside AMD64 is not ported.", true)]
    [TestCase(TYP_LONG, 2048, "Instruction sanity checking outside AMD64 is not ported.", true)]
#else
    [TestCase(TYP_INT, -2048, null, true)]
    [TestCase(TYP_INT, 2047, null, true)]
    [TestCase(TYP_BYREF, 42, null, true)]
    [TestCase(TYP_INT, -2049, null, true)]
    [TestCase(TYP_LONG, 2048, null, true)]
#endif
    public static void IntegerConstantNodeDispatchPreservesRiscVImmediateRecordingBoundary(
        var_types type,
        long immediate,
        string? expectedBoundary,
        bool recordsDescriptor)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var constant = compiler.gtNewIconNode(type, unchecked((nint)immediate));
            constant.RegNum = REG_A0;

            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            FatalJitException? failure = null;
            try
            {
                codeGen.genCodeForTreeNode(constant);
            }
            catch (FatalJitException exception)
            {
                failure = exception;
            }

            if (expectedBoundary is null)
            {
                Assert.That(failure, Is.Null);
            }
            else
            {
                Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
                Assert.That(failure?.Message, Does.Contain(expectedBoundary));
            }

            Assert.That(instructionBuffer.Count,
                Is.EqualTo(initialInstructionCount + (recordsDescriptor ? 1 : 0)));
            if (!recordsDescriptor)
            {
                Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
                return;
            }

            var descriptor = instructionBuffer[^1];
            var instructionCount = Emitter.emitLoadImmediate(
                false, descriptor.idOpSize(), REG_NA, unchecked((nint)immediate));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_A0));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo((uint)(instructionCount * 4)));
            Assert.That(descriptor.idIns(), Is.Not.EqualTo(INS_invalid));
            Assert.That(CurrentInstructionGroupSize(emitter),
                Is.EqualTo(initialGroupSize + (failure is null ? instructionCount * 4 : 0)));
        });
    }

    [TestCase(INS_add, EA_8BYTE, REG_A0, REG_A1, REG_A2, INS_none, 4)]
    [TestCase(INS_add, EA_8BYTE, REG_A0, REG_R0, REG_A1, INS_c_mv, 2)]
    [TestCase(INS_add, EA_8BYTE, REG_A0, REG_A0, REG_A1, INS_c_add, 2)]
    [TestCase(INS_and, EA_8BYTE, REG_A0, REG_A0, REG_A1, INS_c_and, 2)]
    [TestCase(INS_fadd_s, EA_4BYTE, REG_FA0, REG_FA1, REG_FA2, INS_none, 4)]
    [TestCase(INS_sc_w, EA_4BYTE, REG_A0, REG_A1, REG_A2, INS_none, 4)]
    [TestCase(INS_lr_w, EA_4BYTE, REG_A0, REG_A1, REG_R0, INS_none, 4)]
    public static void ThreeRegisterInstructionRecordsItsDescriptor(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        regNumber reg3,
        instruction compressedIns,
        int codeSize)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            FatalJitException? failure = null;
            try
            {
                emitter.emitIns_R_R_R(ins, attr, reg1, reg2, reg3);
            }
            catch (FatalJitException exception)
            {
                failure = exception;
            }

#if DEBUG
            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Instruction sanity checking outside AMD64 is not ported."));
#else
            Assert.That(failure, Is.Null);
#endif

            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount + 1));
            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(reg2));
            Assert.That(descriptor.idReg3(), Is.EqualTo(reg3));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo((uint)codeSize));
            Assert.That(descriptor.idAddr().iiaInstrEncode,
                Is.EqualTo(ExpectedThreeRegisterEncoding(ins, reg1, reg2, reg3, compressedIns)));
            Assert.That(CurrentInstructionGroupSize(emitter),
                Is.EqualTo(initialGroupSize + (failure is null ? (int)codeSize : 0)));
        });
    }

    [TestCase(INS_add, 0x00000033u)]
    [TestCase(INS_fadd_s, 0x00000053u)]
    [TestCase(INS_sc_w, 0x1800202Fu)]
    [TestCase(INS_c_mv, 0x00008002u)]
    public static void InstructionCodeMatchesTheRiscVOpcodeTable(instruction ins, uint expectedCode)
    {
        Assert.That(Emitter.emitInsCode(ins), Is.EqualTo(expectedCode));
    }

    [TestCase(INS_addi, EA_8BYTE, REG_A0, REG_A1, 42, 0x02A58513u)]
    [TestCase(INS_addi, EA_8BYTE, REG_A0, REG_A1, -2048, 0x80058513u)]
    [TestCase(INS_addi, EA_8BYTE, REG_A0, REG_A1, 2047, 0x7FF58513u)]
    [TestCase(INS_addi, EA_8BYTE, REG_A0, REG_A1, -42, 0xFD658513u)]
    [TestCase(INS_addiw, EA_4BYTE, REG_A0, REG_A1, 42, 0x02A5851Bu)]
    [TestCase(INS_lw, EA_4BYTE, REG_A0, REG_A1, 42, 0x02A5A503u)]
    [TestCase(INS_flw, EA_4BYTE, REG_FA0, REG_A1, 42, 0x02A5A507u)]
    [TestCase(INS_jalr, EA_8BYTE, REG_A0, REG_A1, 42, 0x02A58567u)]
    [TestCase(INS_sw, EA_4BYTE, REG_A0, REG_A1, 42, 0x02A5A523u)]
    [TestCase(INS_sw, EA_4BYTE, REG_A0, REG_A1, -2048, 0x80A5A023u)]
    [TestCase(INS_sw, EA_4BYTE, REG_A0, REG_A1, 2047, 0x7EA5AFA3u)]
    [TestCase(INS_fsw, EA_4BYTE, REG_FA0, REG_A1, 42, 0x02A5A527u)]
    [TestCase(INS_csrrw, EA_8BYTE, REG_A0, REG_A1, 0xC00, 0xC0059573u)]
    [TestCase(INS_csrrw, EA_8BYTE, REG_A0, REG_A1, 0xFFF, 0xFFF59573u)]
    public static void TwoRegisterImmediateInstructionRecordsItsDescriptor(
        instruction ins,
        emitAttr attr,
        regNumber reg1,
        regNumber reg2,
        int immediate,
        uint expectedCode)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_R_I(ins, attr, reg1, reg2, (nint)immediate));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg1));
            Assert.That(descriptor.idReg2(), Is.EqualTo(reg2));
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(immediate));
            Assert.That(descriptor.idAddr().iiaInstrEncode, Is.EqualTo(expectedCode));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
        });
    }

    [TestCase(INS_csrrwi, REG_A0, 0, 0, 0x00005573u)]
    [TestCase(INS_csrrwi, REG_R0, 31, 0xFFF, 0xFFFFD073u)]
    [TestCase(INS_csrrsi, REG_A0, 31, 0xFFF, 0xFFFFE573u)]
    [TestCase(INS_csrrsi, REG_R0, 0, 0, 0x00006073u)]
    [TestCase(INS_csrrci, REG_A0, 0, 0xFFF, 0xFFF07573u)]
    [TestCase(INS_csrrci, REG_R0, 31, 0, 0x000FF073u)]
    public static void RegisterTwoImmediateInstructionRecordsItsDescriptor(
        instruction ins,
        regNumber reg,
        int imm1,
        int imm2,
        uint expectedCode)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_I_I(ins, EA_8BYTE, reg, imm1, imm2));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);

            var descriptor = instructionBuffer[^1];
            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reg));
            Assert.That(descriptor.idAddr().iiaInstrEncode, Is.EqualTo(expectedCode));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
        });
    }

    [Test]
    public static void UnsupportedRegisterTwoImmediateInstructionTerminatesWithoutRecording()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_I_I(INS_add, EA_8BYTE, REG_A0, 1, 0xC00));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
        });
    }

    [Test]
    public static void UnsupportedThreeRegisterImmediateInstructionTerminatesWithoutRecording()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_R_R_I(
                    INS_add, EA_8BYTE, REG_A0, REG_A1, REG_A2, 1));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Three-register-immediate instruction recording requires xarch."));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
        });
    }

    [Test]
    public static void UnsupportedFourRegisterInstructionTerminatesWithoutRecording()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var failure = CaptureFatalJitException(
                () => emitter.emitIns_R_R_R_R(
                    INS_add, EA_8BYTE, REG_A0, REG_A1, REG_A2, REG_A3));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Four-register instruction recording requires xarch."));
            Assert.That(instructionBuffer.Count, Is.EqualTo(initialInstructionCount));
            Assert.That(CurrentInstructionGroupSize(emitter), Is.EqualTo(initialGroupSize));
        });
    }

    private static uint ExpectedThreeRegisterEncoding(
        instruction ins,
        regNumber reg1,
        regNumber reg2,
        regNumber reg3,
        instruction compressedIns)
    {
        var code = Emitter.emitInsCode(compressedIns is INS_none ? ins : compressedIns);
        if (compressedIns is INS_c_mv or INS_c_add)
        {
            code |= (unchecked((uint)reg3) << 2) | (unchecked((uint)reg1) << 7);
        }
        else if (compressedIns is not INS_none)
        {
            code |= (Emitter.tryGetRvcRegisterNumber(reg3) << 2) |
                    (Emitter.tryGetRvcRegisterNumber(reg1) << 7);
        }
        else
        {
            code |= (unchecked((uint)reg1) & 0x1Fu) << 7;
            code |= (unchecked((uint)reg2) & 0x1Fu) << 15;
            code |= (unchecked((uint)reg3) & 0x1Fu) << 20;
            if (ins is INS_fadd_s or INS_fsub_s or INS_fmul_s or INS_fdiv_s or
                INS_fadd_d or INS_fsub_d or INS_fmul_d or INS_fdiv_d)
            {
                code |= 0x7u << 12;
            }
            else if (ins is INS_sc_w or INS_sc_d)
            {
                code |= 0b10u << 25;
            }
            else if (ins is INS_lr_w or INS_lr_d or INS_amoswap_w or INS_amoswap_d or
                     INS_amoadd_w or INS_amoadd_d or INS_amoxor_w or INS_amoxor_d or
                     INS_amoand_w or INS_amoand_d or INS_amoor_w or INS_amoor_d or
                     INS_amomin_w or INS_amomin_d or INS_amomax_w or INS_amomax_d or
                     INS_amominu_w or INS_amominu_d or INS_amomaxu_w or INS_amomaxu_d)
            {
                code |= 0b11u << 25;
            }
        }

        return code;
    }

    [Test]
    public static void RelocatableImmediateUsesRiscVRelocationRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(
                () => codeGen.instGen_Set_Reg_To_Imm(EA_HANDLE_CNS_RELOC, REG_A0, 42));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 relocated-address load recording is not ported."));
        });
    }

    [TestCase(TYP_FLOAT, 0UL, "Target two-register instruction recording is not implemented.")]
    [TestCase(TYP_DOUBLE, 0UL, "Target two-register instruction recording is not implemented.")]
#if DEBUG
    [TestCase(TYP_FLOAT, 0x7FFUL, RiscVRecorderDebugBoundary)]
    [TestCase(TYP_DOUBLE, 0x7FFUL, RiscVRecorderDebugBoundary)]
#else
    [TestCase(TYP_FLOAT, 0x7FFUL, "Target two-register instruction recording is not implemented.")]
    [TestCase(TYP_DOUBLE, 0x7FFUL, "Target two-register instruction recording is not implemented.")]
#endif
    [TestCase(TYP_FLOAT, 0x12345000UL, "Target register-immediate recording is not implemented.")]
    [TestCase(TYP_DOUBLE, 0x12345000UL, "Target register-immediate recording is not implemented.")]
    [TestCase(TYP_FLOAT, 0x80000000UL, "Target register-immediate recording is not implemented.")]
    public static void InlineFloatingConstantNodeDispatchPreservesRiscVInstructionRecordingBoundary(
        var_types type,
        ulong bits,
        string? expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var value = type is TYP_FLOAT
                ? BitConverter.UInt32BitsToSingle(unchecked((uint)bits))
                : BitConverter.UInt64BitsToDouble(bits);
            var constant = compiler.gtNewDconNode(type, value);
            constant.RegNum = REG_FA0;
            if (bits != 0)
            {
                codeGen.InternalRegisters.Add(
                    constant, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));
            }

            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(constant));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [TestCase(TYP_FLOAT, 0x7F800001UL)]
    [TestCase(TYP_FLOAT, 0x12345001UL)]
    [TestCase(TYP_DOUBLE, 0x8000000000000000UL)]
    [TestCase(TYP_DOUBLE, 0x3FF0000000000001UL)]
    public static void PooledFloatingConstantNodeDispatchPreservesRiscVEmbeddedDataRecordingBoundary(
        var_types type,
        ulong bits)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var value = type is TYP_FLOAT
                ? BitConverter.UInt32BitsToSingle(unchecked((uint)bits))
                : BitConverter.UInt64BitsToDouble(bits);
            var constant = compiler.gtNewDconNode(type, value);
            constant.RegNum = REG_FA0;

            var failure = Assert.Throws<FatalJitException>(
                () => codeGen.genCodeForTreeNode(constant));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 embedded-data instruction recording is not ported."));
            Assert.That(codeGen.Emitter.emitConsDsc.dsdList, Is.Not.Null);
        });
    }

    [TestCase(TYP_BYREF)]
    [TestCase(TYP_I_IMPL)]
    public static void LocalAddressPreservesTheStackInstructionRecordingBoundary(var_types type)
    {
        WithCodeGen((_, codeGen) =>
        {
            var localAddress = new GenTreeLclFld(GT_LCL_ADDR, type, 0, 24)
            {
                RegNum = REG_A0,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForLclAddr(localAddress));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Target local-stack instruction recording is not implemented."));
        });
    }

    [TestCase(TYP_INT, TYP_LONG, "Target two-register instruction recording is not implemented.")]
    [TestCase(TYP_LONG, TYP_INT, "Target two-register instruction recording is not implemented.")]
    [TestCase(TYP_LONG, TYP_LONG, "Target conditional-branch recording is not implemented.")]
    public static void RangeCheckNodeDispatchPreservesRiscVInstructionRecordingBoundary(
        var_types indexType,
        var_types lengthType,
        string expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;

            var index = new GenTreePhysReg(REG_A0, indexType) { RegNum = REG_A0 };
            var length = new GenTreePhysReg(REG_A1, lengthType) { RegNum = REG_A1 };
            var boundsCheck = new GenTreeBoundsChk(index, length, SpecialCodeKind.SCK_RNGCHK_FAIL);
            if ((indexType is TYP_INT) || (lengthType is TYP_INT))
            {
                codeGen.InternalRegisters.Add(
                    boundsCheck, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));
            }

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(boundsCheck));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message, Does.Contain(expectedBoundary));
        });
    }

    [Test]
    public static void NullCheckNodeDispatchPreservesRiscVIndirectLoadRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var address = new GenTreePhysReg(REG_A0, TYP_I_IMPL) { RegNum = REG_A0 };
            var nullCheck = new GenTreeIndir(GT_NULLCHECK, TYP_INT, address);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForTreeNode(nullCheck));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 indirect load/store instruction recording is not ported."));
        });
    }

#if DEBUG
    [TestCase(8, RiscVRecorderDebugBoundary)]
    [TestCase(0x12345, RiscVRecorderDebugBoundary)]
#else
    [TestCase(8, null)]
    [TestCase(0x12345, null)]
#endif
    public static void LeaNodeDispatchRecordsRiscVImmediateInstruction(int offset, string? expectedBoundary)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var baseAddress = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var lea = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, offset) { RegNum = REG_A1 };
            if (!Emitter.isValidSimm12(offset))
            {
                codeGen.InternalRegisters.Add(
                    lea, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));
            }

            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(lea));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void LeaNodeDispatchSkipsAZeroOffsetWhenTheBaseIsTheTarget()
    {
        WithCodeGen((_, codeGen) =>
        {
            var baseAddress = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var lea = new GenTreeAddrMode(TYP_BYREF, baseAddress, null, 0, 0) { RegNum = REG_A0 };

            Assert.DoesNotThrow(() => codeGen.genCodeForTreeNode(lea));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GSSecurityCookieInitializationSkipsWhenNotNeeded(bool initiallyZeroed)
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.compNeedsGSSecurityCookie = false;
            var zeroed = initiallyZeroed;

            codeGen.genSetGSSecurityCookie(REG_A0, ref zeroed);

            Assert.That(codeGen.Emitter.emitCurIG?.igInsCnt, Is.Zero);
            Assert.That(zeroed, Is.EqualTo(initiallyZeroed));
        });
    }

    [Test]
    public static void GSSecurityCookieInitializationSkipsWhenOsrFrameAlreadyHasCookie()
    {
        WithProlog((compiler, codeGen) =>
        {
            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 64);
            patchpoint->SecurityCookieOffset = -24;
            compiler.info.compPatchpointInfo = patchpoint;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            compiler.compNeedsGSSecurityCookie = true;
            var zeroed = true;

            codeGen.genSetGSSecurityCookie(REG_A0, ref zeroed);

            Assert.That(codeGen.Emitter.emitCurIG?.igInsCnt, Is.Zero);
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void GSSecurityCookieInitializationRecordsImmediateInstruction()
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.compNeedsGSSecurityCookie = true;
            compiler.gsGlobalSecurityCookieVal = 1;
            var zeroed = true;

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = "Target local-stack store recording is not implemented.";
#endif
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var failure = CaptureFatalJitException(
                () => codeGen.genSetGSSecurityCookie(REG_A0, ref zeroed));

            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void GSSecurityCookieInitializationPreservesRelocatedAddressBoundary()
    {
        WithProlog((compiler, codeGen) =>
        {
            compiler.compNeedsGSSecurityCookie = true;
            compiler.gsGlobalSecurityCookieAddr = (nint*)0x12345678;
            var zeroed = true;

            var failure = Assert.Throws<FatalJitException>(() =>
                codeGen.genSetGSSecurityCookie(REG_A0, ref zeroed));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 relocated-address load recording is not ported."));
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void SelectNodeDispatchPreservesRiscVThreeRegisterRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zicond);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zicond);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zicond);

            var condition = new GenTreePhysReg(REG_A0, TYP_INT) { RegNum = REG_A0 };
            var trueValue = new GenTreePhysReg(REG_A1, TYP_INT) { RegNum = REG_A1 };
            var falseValue = new GenTreePhysReg(REG_A2, TYP_INT) { RegNum = REG_A2 };
            var select = new GenTreeConditional(GT_SELECT, TYP_INT, condition, trueValue, falseValue)
            {
                RegNum = REG_A3,
            };
            codeGen.InternalRegisters.Add(select, regMaskTP.CreateFromRegNum(REG_A4, REG_A4.SingleTypeMask));

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(select));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

#if DEBUG
    [TestCase(GT_LSH, TYP_INT, true, false, RiscVRecorderDebugBoundary)]
    [TestCase(GT_RSH, TYP_LONG, false, false, RiscVRecorderDebugBoundary)]
    [TestCase(GT_ROR, TYP_INT, false, false, RiscVRecorderDebugBoundary)]
    [TestCase(GT_ROR, TYP_LONG, false, true, RiscVRecorderDebugBoundary)]
    [TestCase(GT_ROL, TYP_INT, true, true, RiscVRecorderDebugBoundary)]
#else
    [TestCase(GT_LSH, TYP_INT, true, false, null)]
    [TestCase(GT_RSH, TYP_LONG, false, false, null)]
    [TestCase(GT_ROR, TYP_INT, false, false, null)]
    [TestCase(GT_ROR, TYP_LONG, false, true, null)]
    [TestCase(GT_ROL, TYP_INT, true, true, null)]
#endif
    public static void ShiftNodeDispatchRecordsRiscVImmediateInstruction(
        genTreeOps oper,
        var_types type,
        bool useImmediate,
        bool useZbb,
        string? expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            if (useZbb)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zbb);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zbb);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zbb);
            }

            var value = new GenTreePhysReg(REG_A0, type) { RegNum = REG_A0 };
            GenTree shiftBy;
            if (useImmediate)
            {
                shiftBy = compiler.gtNewIconNode(TYP_INT, 5);
                shiftBy.IsContained = true;
            }
            else
            {
                shiftBy = new GenTreePhysReg(REG_A1, TYP_INT) { RegNum = REG_A1 };
            }

            var shift = new GenTreeOp(oper, type, value, shiftBy) { RegNum = REG_A2 };
            if ((oper is GT_ROR or GT_ROL) && !useZbb)
            {
                codeGen.InternalRegisters.Add(
                    shift, regMaskTP.CreateFromRegNum(REG_A3, REG_A3.SingleTypeMask));
            }

            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(shift));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [TestCase(GT_SH1ADD)]
    [TestCase(GT_SH2ADD)]
    [TestCase(GT_SH3ADD)]
    [TestCase(GT_SH1ADD_UW)]
    [TestCase(GT_SH2ADD_UW)]
    [TestCase(GT_SH3ADD_UW)]
    public static void ShxaddNodeDispatchPreservesRiscVInstructionRecordingBoundary(genTreeOps oper)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);

            var left = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var right = new GenTreePhysReg(REG_A1, TYP_LONG) { RegNum = REG_A1 };
            var tree = new GenTreeOp(oper, TYP_LONG, left, right) { RegNum = REG_A2 };

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void AddUwNodeDispatchPreservesRiscVInstructionRecordingBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);

            var left = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var right = new GenTreePhysReg(REG_A1, TYP_LONG) { RegNum = REG_A1 };
            var tree = new GenTreeOp(GT_ADD_UW, TYP_LONG, left, right) { RegNum = REG_A2 };

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void SlliUwNodeDispatchRecordsRiscVImmediateInstruction()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);

            var value = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var shiftBy = compiler.gtNewIconNode(TYP_INT, 7);
            shiftBy.IsContained = true;

            var tree = new GenTreeOp(GT_SLLI_UW, TYP_LONG, value, shiftBy) { RegNum = REG_A2 };

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForTreeNode(tree));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [TestCase(8)]
    [TestCase(16)]
    public static void CopyBlockUnrollRecordsRiscVImmediateInstructions(int size)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var destination = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var sourceAddress = new GenTreePhysReg(REG_A1, TYP_BYREF) { RegNum = REG_A1 };
            var source = new GenTreeIndir(GT_IND, TYP_STRUCT, sourceAddress) { IsContained = true };
            var block = new GenTreeBlk(TYP_STRUCT, destination, source, new ClassLayout((uint)size))
            {
                _kind = BlkOpKindUnroll,
            };
            codeGen.InternalRegisters.Add(
                block, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForCpBlkUnroll(block));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void StoreBlkInitLoopDispatchRecordsRiscVInitialStore()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var destination = new GenTreePhysReg(REG_A0, TYP_BYREF) { RegNum = REG_A0 };
            var block = new GenTreeBlk(
                TYP_STRUCT,
                destination,
                new GenTreeIntCon(TYP_INT, 0),
                new ClassLayout((uint)TARGET_POINTER_SIZE))
            {
                _kind = BlkOpKindLoop,
            };

#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(() => codeGen.genCodeForStoreBlk(block));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

#if DEBUG
    [TestCase(false, RiscVRecorderDebugBoundary)]
    [TestCase(true, RiscVRecorderDebugBoundary)]
#else
    [TestCase(false, null)]
    [TestCase(true, null)]
#endif
    public static void IntegerCastDispatchRecordsRiscVExtensionInstruction(
        bool useZba,
        string? expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            if (useZba)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_Zba);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Zba);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Zba);
            }

            var source = new GenTreePhysReg(REG_A0, TYP_INT) { RegNum = REG_A0 };
            var cast = new GenTreeCast(TYP_LONG, source, fromUnsigned: true, castType: TYP_ULONG)
            {
                RegNum = REG_A1,
            };

            var failure = CaptureFatalJitException(() => codeGen.genCodeForCast(cast));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void IntegerCastOverflowDispatchPreservesRiscVTwoRegisterRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_A0, TYP_INT) { RegNum = REG_A0 };
            var cast = new GenTreeCast(TYP_LONG, source, fromUnsigned: false, castType: TYP_ULONG)
            {
                RegNum = REG_A1,
                Flags = GTF_OVERFLOW,
            };
            codeGen.InternalRegisters.Add(
                cast, regMaskTP.CreateFromRegNum(REG_A2, REG_A2.SingleTypeMask));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForCast(cast));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Target two-register instruction recording is not implemented."));
        });
    }

    [Test]
    public static void IntegerCastCopyDispatchPreservesRiscVRegisterRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_A0, TYP_LONG) { RegNum = REG_A0 };
            var cast = new GenTreeCast(TYP_LONG, source, fromUnsigned: false, castType: TYP_LONG)
            {
                RegNum = REG_A1,
            };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForCast(cast));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("Target two-register instruction recording is not implemented."));
        });
    }

    [TestCase(TYP_FLOAT, TYP_DOUBLE, "Target two-register instruction recording is not implemented.")]
    [TestCase(TYP_DOUBLE, TYP_FLOAT, "Target two-register instruction recording is not implemented.")]
#if DEBUG
    [TestCase(TYP_FLOAT, TYP_FLOAT, RiscVRecorderDebugBoundary)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE, RiscVRecorderDebugBoundary)]
#else
    [TestCase(TYP_FLOAT, TYP_FLOAT, null)]
    [TestCase(TYP_DOUBLE, TYP_DOUBLE, null)]
#endif
    public static void FloatToFloatCastDispatchPreservesRiscVRecordingBoundary(
        var_types sourceType,
        var_types destinationType,
        string? expectedBoundary)
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);

            var source = new GenTreePhysReg(REG_FA0, sourceType) { RegNum = REG_FA0 };
            var cast = new GenTreeCast(destinationType, source, fromUnsigned: false, castType: destinationType)
            {
                RegNum = REG_FA1,
            };

            var failure = CaptureFatalJitException(() => codeGen.genCodeForCast(cast));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void FloatToFloatCastWithSameRegisterDoesNotRecordRiscVInstruction()
    {
        WithCodeGen((_, codeGen) =>
        {
            var source = new GenTreePhysReg(REG_FA0, TYP_FLOAT) { RegNum = REG_FA0 };
            var cast = new GenTreeCast(TYP_FLOAT, source, fromUnsigned: false, castType: TYP_FLOAT)
            {
                RegNum = REG_FA0,
            };

            Assert.DoesNotThrow(() => codeGen.genCodeForCast(cast));
        });
    }

    [Test]
    public static void CatchReturnDispatchPreservesRiscVBlockLabelRecordingBoundary()
    {
        WithCodeGen((_, codeGen) =>
        {
            var block = new BasicBlock(null, null);
            var target = new BasicBlock(null, null);
            block.SetKindAndTargetEdge(BBKinds.BBJ_EHCATCHRET, new FlowEdge(block, target, null));

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genEmitEndBlock(block));

            Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
            Assert.That(failure?.Message,
                Does.Contain("RISC-V64 block-relative address recording is not ported."));
        });
    }

    [Test]
    public static void CalleeSavedRestoreRecordsRiscVImmediateInstruction()
    {
        WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            var restoreMask = regMaskTP.CreateFromRegNum(REG_S1, REG_S1.SingleTypeMask);
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            var failure = CaptureFatalJitException(
                () => RestoreCalleeSavedRegisters(codeGen, restoreMask, REG_FP, 16, reportUnwindData: false));
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [Test]
    public static void OSRPrologRecordsRiscVImmediateInstruction()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            var initialInstructionCount = instructionBuffer.Count;
            var initialGroupSize = CurrentInstructionGroupSize(emitter);
            compiler.compFuncInfos = [new FuncInfoDsc { funKind = FuncKind.FUNC_ROOT, uwi = new UnwindInfo() }];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_OSR);
            codeGen.Emitter.emitCurIG = codeGen.Emitter.emitGetFirstPrologIG();
            compiler.funCurrentFunc().GetUnwindInfo().InitUnwindInfo(compiler, null, null);

            var storage = stackalloc byte[PatchpointInfo.ComputeSize(1)];
            var patchpoint = (PatchpointInfo*)storage;
            patchpoint->Initialize(1, 96);
            patchpoint->CalleeSaveRegisters = (long)(SRBM_S1 | SRBM_FP | SRBM_RA);
            compiler.info.compPatchpointInfo = patchpoint;

            compiler.unwindBegProlog();

            var failure = CaptureFatalJitException(
                () => codeGen.genOSRHandleTier0CalleeSavedRegistersAndFrame());
#if DEBUG
            const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
            const string? expectedBoundary = null;
#endif
            AssertRiscVInstructionBoundary(
                codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
        });
    }

    [TestCase(NI_PRIMITIVE_SaturateToInt8)]
    [TestCase(NI_PRIMITIVE_SaturateToInt16)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt8)]
    [TestCase(NI_PRIMITIVE_SaturateToUInt16)]
    public static void SaturationIntrinsicDispatchPreservesRiscVRegisterRecordingBoundary(
        NamedIntrinsic intrinsic)
    {
        WithCodeGen((compiler, codeGen) => AssertIntrinsicBoundary(
            compiler, codeGen, intrinsic, TYP_INT, hasSecondOperand: false,
            expectedBoundary: "Target two-register instruction recording is not implemented."));
    }

#if DEBUG
    [TestCase(NI_System_Math_Abs, TYP_FLOAT, RiscVRecorderDebugBoundary)]
    [TestCase(NI_System_Math_Abs, TYP_DOUBLE, RiscVRecorderDebugBoundary)]
#else
    [TestCase(NI_System_Math_Abs, TYP_FLOAT, null)]
    [TestCase(NI_System_Math_Abs, TYP_DOUBLE, null)]
#endif
    [TestCase(NI_System_Math_Sqrt, TYP_FLOAT,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_System_Math_Sqrt, TYP_DOUBLE,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, TYP_INT,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_LeadingZeroCount, TYP_LONG,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, TYP_INT,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_TrailingZeroCount, TYP_LONG,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_PopCount, TYP_INT,
        "Target two-register instruction recording is not implemented.")]
    [TestCase(NI_PRIMITIVE_PopCount, TYP_LONG,
        "Target two-register instruction recording is not implemented.")]
    public static void UnaryIntrinsicDispatchPreservesRiscVRegisterRecordingBoundary(
        NamedIntrinsic intrinsic,
        var_types type,
        string? expectedBoundary)
    {
        WithCodeGen((compiler, codeGen) => AssertIntrinsicBoundary(
            compiler, codeGen, intrinsic, type, hasSecondOperand: false,
            expectedBoundary: expectedBoundary));
    }

    [TestCase(NI_System_Math_MinNative, TYP_FLOAT)]
    [TestCase(NI_System_Math_MinNative, TYP_DOUBLE)]
    [TestCase(NI_System_Math_MaxNative, TYP_FLOAT)]
    [TestCase(NI_System_Math_MaxNative, TYP_DOUBLE)]
    [TestCase(NI_System_Math_Min, TYP_INT)]
    [TestCase(NI_System_Math_MinUnsigned, TYP_INT)]
    [TestCase(NI_System_Math_Max, TYP_INT)]
    [TestCase(NI_System_Math_MaxUnsigned, TYP_INT)]
    public static void BinaryIntrinsicDispatchPreservesRiscVThreeRegisterRecordingBoundary(
        NamedIntrinsic intrinsic,
        var_types type)
    {
#if DEBUG
        const string? expectedBoundary = RiscVRecorderDebugBoundary;
#else
        const string? expectedBoundary = null;
#endif
        WithCodeGen((compiler, codeGen) => AssertIntrinsicBoundary(
            compiler, codeGen, intrinsic, type, hasSecondOperand: true,
            expectedBoundary: expectedBoundary));
    }

    private static void AssertIntrinsicBoundary(
        Compiler compiler,
        CodeGen codeGen,
        NamedIntrinsic intrinsic,
        var_types type,
        bool hasSecondOperand,
        string? expectedBoundary)
    {
        var isFloating = type is TYP_FLOAT or TYP_DOUBLE;
        var sourceRegister = isFloating ? REG_FT0 : REG_A0;
        var secondSourceRegister = isFloating ? REG_FT1 : REG_A1;
        var targetRegister = isFloating ? REG_FT2 : REG_A2;
        var source = new GenTreePhysReg(sourceRegister, type) { RegNum = sourceRegister };
        GenTreeIntrinsic tree;
        if (hasSecondOperand)
        {
            var secondSource = new GenTreePhysReg(secondSourceRegister, type) { RegNum = secondSourceRegister };
            tree = new GenTreeIntrinsic(type, source, secondSource, intrinsic, null);
        }
        else
        {
            tree = new GenTreeIntrinsic(type, source, intrinsic, null);
        }

        tree.RegNum = targetRegister;

        if (intrinsic is NI_PRIMITIVE_SaturateToInt8 or NI_PRIMITIVE_SaturateToInt16
            or NI_PRIMITIVE_SaturateToUInt8 or NI_PRIMITIVE_SaturateToUInt16)
        {
            codeGen.InternalRegisters.Add(tree,
                regMaskTP.CreateFromRegNum(REG_A3, REG_A3.SingleTypeMask));
        }

        var emitter = codeGen.Emitter;
        var instructionBuffer = CurrentInstructionBuffer(emitter)
            ?? throw new AssertionException("Missing current instruction buffer.");
        var initialInstructionCount = instructionBuffer.Count;
        var initialGroupSize = CurrentInstructionGroupSize(emitter);
        var failure = CaptureFatalJitException(() => codeGen.genIntrinsic(tree));
        AssertRiscVInstructionBoundary(
            codeGen, failure, expectedBoundary, initialInstructionCount, initialGroupSize);
    }

    private static FatalJitException? CaptureFatalJitException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (FatalJitException exception)
        {
            return exception;
        }
    }

    private static void AssertRiscVInstructionBoundary(
        CodeGen codeGen,
        FatalJitException? failure,
        string? expectedBoundary,
        int initialInstructionCount,
        int initialGroupSize)
    {
        if (expectedBoundary is null or RiscVRecorderDebugBoundary)
        {
            var emitter = codeGen.Emitter;
            var instructionBuffer = CurrentInstructionBuffer(emitter)
                ?? throw new AssertionException("Missing current instruction buffer.");
            Assert.That(instructionBuffer.Count, Is.GreaterThan(initialInstructionCount));

            if (expectedBoundary is null)
            {
                Assert.That(failure, Is.Null);
                Assert.That(CurrentInstructionGroupSize(emitter), Is.GreaterThan(initialGroupSize));
            }
            else
            {
                Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
                Assert.That(failure?.Message, Does.Contain(expectedBoundary));
            }

            return;
        }

        Assert.That(failure?.Result, Is.EqualTo(CORJIT_SKIPPED));
        Assert.That(failure?.Message, Does.Contain(expectedBoundary));
    }

    internal static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );

            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static void WithProlog(Action<Compiler, CodeGen> action)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var group = codeGen.Emitter.emitCurIG
                ?? throw new AssertionException("Missing instruction group.");
            group.igFlags |= InsGroupFlags.Prolog;
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentInstructionBuffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentInstructionGroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRestoreCalleeSavedRegistersHelp")]
    private static extern void RestoreCalleeSavedRegisters(CodeGen codeGen, regMaskTP mask, regNumber baseReg,
        int offset, bool reportUnwindData);
}
#endif
