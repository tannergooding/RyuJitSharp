// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CompilerParameterInitialRegistersTests
{
    [Test]
    public static void ArgumentAndMappedTargetHomesAreRestoredOnlyAfterAllocation()
    {
        CodeGenSpillVariableTests.WithCompiler(var_types.TYP_LONG, REG_X64_RAX, (compiler, codeGen, tree) =>
        {
            compiler.lvaTable = [
                new LclVarDsc
                {
                    Type = var_types.TYP_LONG, RegNum = REG_X64_RAX, ArgInitReg = REG_X64_R10,
                    lvIsParam = true, lvLRACandidate = true,
                },
                new LclVarDsc
                {
                    Type = TYP_DOUBLE, RegNum = REG_X64_XMM1, ArgInitReg = REG_X64_XMM0,
                    lvIsParamRegTarget = true, lvLRACandidate = true,
                },
                new LclVarDsc
                {
                    Type = var_types.TYP_LONG, RegNum = REG_X64_RBX, ArgInitReg = REG_X64_R11,
                    lvLRACandidate = true,
                },
                new LclVarDsc
                {
                    Type = var_types.TYP_LONG, RegNum = REG_X64_STK, ArgInitReg = REG_X64_RCX,
                    lvIsParam = true,
                },
            ];
            compiler.lvaCount = 4;
            compiler.info.compArgsCount = 1;

            compiler.lvaUpdateArgsWithInitialReg();
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_X64_RAX));
            Assert.That(compiler.lvaTable[1].RegNum, Is.EqualTo(REG_X64_XMM1));

            compiler.compRegAllocDone = true;
            compiler.lvaUpdateArgsWithInitialReg();

            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_X64_R10));
            Assert.That(compiler.lvaTable[1].RegNum, Is.EqualTo(REG_X64_XMM0));
            Assert.That(compiler.lvaTable[2].RegNum, Is.EqualTo(REG_X64_RBX));
            Assert.That(compiler.lvaTable[3].RegNum, Is.EqualTo(REG_X64_STK));
        });
    }
}
