// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_X86 && (DEBUG || LATE_DISASM)
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.PerfScoreMemoryAccessKind;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class EmitterExecutionCostX86Tests
{
    [TestCase(INS_fld, IF_SRD, 0.5f, Read)]
    [TestCase(INS_fstp, IF_SWR, 1.0f, Write)]
    public static void X87LoadAndStorePreserveNativeThroughputAndLatency(
        instruction ins, Emitter.insFormat format, float throughput, Emitter.PerfScoreMemoryAccessKind accessKind)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var emitter = new CodeGen(compiler).Emitter;
        emitter.emitBegCG(compiler, default);

        var id = View.Basic(ins, format);
        var result = emitter.getInsExecutionCharacteristics(id);

        Assert.That(result.insThroughput, Is.EqualTo(throughput));
        Assert.That(result.insLatency, Is.EqualTo(3.0f));
        Assert.That(result.insMemoryAccessKind, Is.EqualTo(accessKind));
    }

    private abstract class View(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Basic(instruction ins, insFormat format)
        {
            var id = new instrDescBasic();
            id.idIns(ins);
            id.idInsFmt(format);
            id.idOpSize(EA_8BYTE);
            return id;
        }
    }
}
#endif
