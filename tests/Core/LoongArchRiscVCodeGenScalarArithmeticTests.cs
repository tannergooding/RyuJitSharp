// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LoongArchRiscVCodeGenScalarArithmeticTests
{
    [Test]
    public static void SaturatingIncrementReachesTargetInstructionRecording()
    {
        WithCodeGen(codeGen =>
        {
            var operandReg = SaturatingIncrementOperandReg;
            var targetReg = SaturatingIncrementTargetReg;
            var operand = new GenTreePhysReg(operandReg, Globals.TYP_I_IMPL) { RegNum = operandReg };
            var tree = new GenTreeUnOp(GT_INC_SATURATE, Globals.TYP_I_IMPL, operand) { RegNum = targetReg };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForIncSaturate(tree));

            Assert.That(failure?.Message, Does.Contain("instruction recording"));
        });
    }

    [Test]
    public static void HighMultiplyReachesTargetInstructionRecording()
    {
        WithCodeGen(codeGen =>
        {
            var operand1Reg = Configuration.TARGET_LOONGARCH64 ? Globals.REG_S0 : Globals.REG_FP;
            var operand1 = new GenTreePhysReg(operand1Reg, Globals.TYP_I_IMPL) { RegNum = operand1Reg };
            var operand2 = new GenTreePhysReg(Globals.REG_S1, Globals.TYP_I_IMPL) { RegNum = Globals.REG_S1 };
            var tree = new GenTreeOp(GT_MULHI, Globals.TYP_I_IMPL, operand1, operand2) { RegNum = Globals.REG_S2 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForMulHi(tree));

            Assert.That(failure?.Message, Does.Contain("instruction recording"));
        });
    }

    private static regNumber SaturatingIncrementOperandReg
        => Configuration.TARGET_LOONGARCH64 ? Globals.REG_S0 : Globals.REG_S1;

    private static regNumber SaturatingIncrementTargetReg
        => Configuration.TARGET_LOONGARCH64 ? Globals.REG_S1 : Globals.REG_S2;

    private static void WithCodeGen(Action<CodeGen> action)
    {
        Assume.That(Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64);

        using var tls = TestConfiguration.CreateJitTls();
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false, TestConfiguration.IsDebug);
            action(codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
