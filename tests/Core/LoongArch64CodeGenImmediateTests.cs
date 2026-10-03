// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class LoongArch64CodeGenImmediateTests
{
    [TestCase(-2049, "LoongArch64 address constant recording is not ported.")]
    [TestCase(-2048, "Target two-register-immediate instruction recording is not implemented.")]
    [TestCase(2047, "Target two-register-immediate instruction recording is not implemented.")]
    [TestCase(2048, "LoongArch64 address constant recording is not ported.")]
    public static void ImmediateRangeSelectsNativeRecordingPath(long immediate, string expectedMessage)
    {
        WithCodeGen(codeGen =>
        {
            var failure = Assert.Throws<FatalJitException>(() =>
                _ = InstructionWithConstant(codeGen, INS_addi_d, EA_PTRSIZE, REG_R1, REG_R2,
                    unchecked((nint)immediate), REG_R3));

            Assert.That(failure?.Message, Is.EqualTo(expectedMessage));
        });
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

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genInstrWithConstant")]
    private static extern bool InstructionWithConstant(CodeGen codeGen, instruction ins, emitAttr attr,
        regNumber reg1, regNumber reg2, nint immediate, regNumber tmpReg, bool inUnwindRegion = false);
}
#endif
