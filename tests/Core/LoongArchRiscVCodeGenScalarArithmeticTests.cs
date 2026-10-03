// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64 || TARGET_RISCV64
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
            var operand = new GenTreePhysReg(operandReg, TYP_I_IMPL) { RegNum = operandReg };
            var tree = new GenTreeUnOp(GT_INC_SATURATE, TYP_I_IMPL, operand) { RegNum = targetReg };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForIncSaturate(tree));

            Assert.That(failure?.Message, Does.Contain("instruction recording"));
        });
    }

    [Test]
    public static void HighMultiplyReachesTargetInstructionRecording()
    {
        WithCodeGen(codeGen =>
        {
#if TARGET_LOONGARCH64
            var operand1 = new GenTreePhysReg(REG_S0, TYP_I_IMPL) { RegNum = REG_S0 };
#else
            var operand1 = new GenTreePhysReg(REG_FP, TYP_I_IMPL) { RegNum = REG_FP };
#endif
            var operand2 = new GenTreePhysReg(REG_S1, TYP_I_IMPL) { RegNum = REG_S1 };
            var tree = new GenTreeOp(GT_MULHI, TYP_I_IMPL, operand1, operand2) { RegNum = REG_S2 };

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genCodeForMulHi(tree));

            Assert.That(failure?.Message, Does.Contain("instruction recording"));
        });
    }

    private static regNumber SaturatingIncrementOperandReg
    {
        get
        {
#if TARGET_LOONGARCH64
            return REG_S0;
#else
            return REG_S1;
#endif
        }
    }

    private static regNumber SaturatingIncrementTargetReg
    {
        get
        {
#if TARGET_LOONGARCH64
            return REG_S1;
#else
            return REG_S2;
#endif
        }
    }

    private static void WithCodeGen(Action<CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
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
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            action(codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
