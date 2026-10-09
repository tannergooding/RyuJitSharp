// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64 && FEATURE_LOOP_ALIGN && (DEBUG || LATE_DISASM)
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static class Arm64EmitterAlignmentCostTests
{
    [Test]
    public static void EmptyAlignmentDoesNotRequirePlacementMetadata()
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            var id = View.Alignment(INS_OPTS_NONE, false);
            var result = codeGen.Emitter.getInsExecutionCharacteristics(id);

            Assert.That(result.insThroughput, Is.Zero);
            Assert.That(result.insLatency, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EmittedAlignmentUsesPlacementMetadata(bool placedAfterJump)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((_, codeGen) =>
        {
            var id = View.Alignment(INS_OPTS_LSL, placedAfterJump);
            var result = codeGen.Emitter.getInsExecutionCharacteristics(id);
            Assert.That(result.insThroughput, Is.EqualTo(placedAfterJump ? 0.0f : 0.5f));
            Assert.That(result.insLatency, Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RecordedAlignmentPreservesPlacementForScoring(bool placedAfterJump)
    {
        Arm64CodeGenLocalVariableTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compJitAlignLoopBoundary = 16;
            var id = View.RecordAlignment(codeGen.Emitter, placedAfterJump);

            Assert.That(View.IsPlacedAfterJump(id), Is.EqualTo(placedAfterJump));
            var result = codeGen.Emitter.getInsExecutionCharacteristics(id);
            Assert.That(result.insThroughput, Is.EqualTo(placedAfterJump ? 0.0f : 0.5f));
            Assert.That(result.insLatency, Is.Zero);
        });
    }

    private abstract class View(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc Alignment(insOpts opt, bool placedAfterJump)
        {
            var id = new instrDescAlign();
            id.idIns(INS_align);
            id.idInsFmt(IF_SN_0A);
            id.idInsOpt(opt);
#if DEBUG || LATE_DISASM
            id.isPlacedAfterJmp = placedAfterJump;
#endif
            return id;
        }

        public static instrDesc RecordAlignment(Emitter emitter, bool placedAfterJump)
        {
            emitter.emitLoopAlignment(placedAfterJump);
            return CurrentAlignment(emitter)
                ?? throw new AssertionException("Missing recorded alignment instruction.");
        }

        public static bool IsPlacedAfterJump(instrDesc id)
        {
            return ((instrDescAlign)id).isPlacedAfterJmp;
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGAlignList")]
        private static extern ref instrDescAlign? CurrentAlignment(Emitter emitter);
    }
}
#endif
