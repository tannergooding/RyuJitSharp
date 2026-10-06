// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && (DEBUG || LATE_DISASM)
using NUnit.Framework;
using static RyuJitSharp.Emitter.PerfScoreMemoryAccessKind;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class Arm64EmitterExecutionMemoryTests
{
    [TestCase(INS_add, IF_DR_3A, REG_NA, REG_NA, None, false)]
    [TestCase(INS_ldr, IF_LS_1A, REG_NA, REG_NA, Read, true)]
    [TestCase(INS_str, IF_LS_2A, REG_ZR, REG_NA, Write, true)]
    [TestCase(INS_str, IF_LS_2A, REG_R0, REG_NA, Write, false)]
    [TestCase(INS_ldr, IF_LS_3B, REG_NA, REG_FP, Read, true)]
    [TestCase(INS_ldr, IF_LS_3B, REG_NA, REG_R0, Read, false)]
    [TestCase(INS_ldadd, IF_LS_2A, REG_ZR, REG_NA, ReadWrite, true)]
    [TestCase(INS_ldr, IF_SVE_HX_3A_B, REG_ZR, REG_ZR, Read, false)]
    public static void ClassifiesMemoryAccessAndLocality(instruction ins, Emitter.insFormat format,
        regNumber reg2, regNumber reg3, Emitter.PerfScoreMemoryAccessKind expectedKind, bool expectedLocal)
    {
        var id = View.Basic(ins, format, reg2, reg3);

        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            codeGen.Emitter.getMemoryOperation(id, out var memoryAccessKind, out var isLocalAccess);

            Assert.That(memoryAccessKind, Is.EqualTo(expectedKind));
            Assert.That(isLocalAccess, Is.EqualTo(expectedLocal));
        });
    }

    private abstract class View(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Basic(instruction ins, Emitter.insFormat format, regNumber reg2, regNumber reg3)
        {
            var id = new instrDescCns();
            id.idIns(ins);
            id.idInsFmt(format);
            id.idReg2(reg2);
            id.idReg3(reg3);
            return id;
        }
    }
}
#endif
