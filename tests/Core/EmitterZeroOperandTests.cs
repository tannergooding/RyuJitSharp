// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class EmitterZeroOperandTests
{
    [TestCase(INS_nop, 1u)]
    [TestCase(INS_int3, 1u)]
    [TestCase(INS_pause, 2u)]
    [TestCase(INS_vzeroupper, 3u)]
    [TestCase(INS_lfence, 3u)]
    [TestCase(INS_movsq, 2u)]
    [TestCase(INS_r_movsq, 3u)]
    [TestCase(INS_serialize, 3u)]
    public static void ZeroOperandOpcodesRecordTheirNativeSizes(instruction ins, uint expectedSize)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (_, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitIns(ins);
            var id = LastInstruction(emitter);
            assert(id is not null);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_NONE));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(id.idCodeSize(), Is.EqualTo(expectedSize));
            Assert.That(CurrentSize(emitter), Is.EqualTo(expectedSize));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        });
    }

    [TestCase(INS_cdq, EA_2BYTE, 2u)]
    [TestCase(INS_cdq, EA_4BYTE, 1u)]
    [TestCase(INS_cdq, EA_8BYTE, 2u)]
    [TestCase(INS_cwde, EA_2BYTE, 2u)]
    [TestCase(INS_cwde, EA_4BYTE, 1u)]
    [TestCase(INS_cwde, EA_8BYTE, 2u)]
    public static void SignExtensionRecordingIncludesOperandAndRexPrefixes(instruction ins, emitAttr attr, uint size)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (_, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitIns(ins, attr);
            var id = LastInstruction(emitter);
            assert(id is not null);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_NONE));
            Assert.That(id.idOpSize(), Is.EqualTo(attr));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(CurrentSize(emitter), Is.EqualTo(size));
        });
    }

    [TestCase(0u)]
    [TestCase(1u)]
    [TestCase(9u)]
    [TestCase(15u)]
    public static void SizedNopsPreserveTheRequestedLogicalSize(uint size)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (_, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitIns_Nop(size);
            var id = LastInstruction(emitter);
            assert(id is not null);
            Assert.That(id.idIns(), Is.EqualTo(INS_nop));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(CurrentSize(emitter), Is.EqualTo(size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void InstructionDisplayRecordsZeroOperandInstructions(int entrypoint)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.opts.dspCode = true;
            var emitter = codeGen.Emitter;
            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
            {
                switch (entrypoint)
                {
                    case 0:
                    {
                        emitter.emitIns(INS_nop);
                        break;
                    }

                    case 1:
                    {
                        emitter.emitIns(INS_cdq, EA_8BYTE);
                        break;
                    }

                    case 2:
                    {
                        emitter.emitIns_Nop(3);
                        break;
                    }
                }
            });

            Assert.That(CurrentSize(emitter), Is.EqualTo(entrypoint switch { 0 => 1u, 1 => 2u, _ => 3u }));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            Assert.That(LastInstruction(emitter), Is.Not.Null);
            Assert.That(diagnostic, Does.Contain(entrypoint == 1 ? "cqo" : "nop"));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);
}
