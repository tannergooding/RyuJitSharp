// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class CodeGenPrologScopeTests
{
    [TestCase(REG_RCX)]
    [TestCase(REG_XMM0)]
    [TestCase(REG_NA)]
    public static void ParameterScopesUseIncomingAbiLocationsAndCloseAtThePrologEnd(regNumber reg)
    {
        CodeGenPrologTests.WithRoot((compiler, codeGen) =>
        {
            compiler.lvaCount = 2;
            compiler.info.compArgsCount = 1;
            compiler.info.compLocalsCount = 2;
            compiler.lvaTable = [
                new() { Type = TYP_LONG, lvIsParam = true, lvOnFrame = true, StackOffset = 16, RegNum = REG_STK },
                new() { Type = TYP_INT, lvOnFrame = true, StackOffset = -16, RegNum = REG_STK },
            ];
            compiler.lvaParameterPassingInfo = [AbiPassingInformation.FromSegment(compiler, false,
                reg == REG_NA ? AbiPassingSegment.OnStack(0, 0, 8) : AbiPassingSegment.InRegister(reg, 0, 8))];
            compiler.info.compVarScopes = [
                new() { vsdVarNum = 0, vsdLVnum = 0, vsdLifeBeg = 0, vsdLifeEnd = 10 },
                new() { vsdVarNum = 1, vsdLVnum = 1, vsdLifeBeg = 0, vsdLifeEnd = 10 },
            ];
            compiler.info.compVarScopesCount = 2;
            compiler.compInitScopeLists();
            codeGen.initializeVariableLiveKeeper();

            codeGen.psiBegProlog();
            codeGen.instGen(INS_nop);
            codeGen.psiEndProlog();

            var range = codeGen.getVariableLiveKeeper().getLiveRangesForVarForProlog(0)[0];
            if (reg == REG_NA)
            {
                var offset = compiler.lvaToCallerSPRelativeOffset(16, false) + 8;
                Assert.That(range.m_VarLocation.vlIsOnStack(REG_RSP, offset), Is.True);
            }
            else if (reg == REG_XMM0)
            {
                Assert.That(range.m_VarLocation.vlType, Is.EqualTo(ICorDebugInfo.VarLocType.VLT_REG_FP));
                Assert.That(range.m_VarLocation.vlReg.vlrReg, Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(reg)));
            }
            else
            {
                Assert.That(range.m_VarLocation.vlIsInReg(reg), Is.True);
            }
            Assert.That(range.m_StartEmitLocation.GetInsNum(), Is.Zero);
            Assert.That(range.m_EndEmitLocation.GetInsNum(), Is.EqualTo(1));
            Assert.That(codeGen.getVariableLiveKeeper().getLiveRangesForVarForProlog(1), Is.Empty);
        });
    }
}
