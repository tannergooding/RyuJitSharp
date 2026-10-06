// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class Arm32BlockLabelAddressTests
{
    [TestCase(INS_adr, IF_T2_M1, false, true)]
    [TestCase(INS_movt, IF_T2_N1, true, false)]
    [TestCase(INS_movw, IF_T2_N1, true, false)]
    public static void LabelAddressesRecordTheirNativeFormatsAndTargetMetadata(
        instruction ins, Emitter.insFormat expectedFormat, bool expectedKeepLong, bool hasPcRegister)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var emitter = codeGen.Emitter;
            var target = Label();
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");

            var descriptor = RecordAddress(emitter, ins, EA_4BYTE, target, REG_R4);

            Assert.That(descriptor.idIns(), Is.EqualTo(ins));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_R4));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(expectedFormat));
            Assert.That(descriptor.idInsSize(), Is.EqualTo(ISZ_32BIT));
            Assert.That(descriptor.idCodeSize(), Is.EqualTo(4u));
            Assert.That(descriptor.idjTarget, Is.SameAs(target));
            Assert.That(descriptor.idjTargetIG, Is.Null);
            Assert.That(descriptor.idjIG, Is.SameAs(group));
            Assert.That(descriptor.idjOffs, Is.Zero);
            Assert.That(descriptor.idjShort, Is.False);
            Assert.That(descriptor.idjKeepLong, Is.EqualTo(expectedKeepLong));
            Assert.That(descriptor.idjNext, Is.Null);
            Assert.That(PendingJump(emitter), Is.SameAs(descriptor));
            if (hasPcRegister)
            {
                Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(Emitter.DescriptorSizes.Label));
                Assert.That(descriptor.idReg2(), Is.EqualTo(REG_PC));
            }
#if !DEBUG
            Assert.That(GroupSize(emitter), Is.EqualTo(4));
#endif
        });
    }

    [TestCase(INS_adr, false)]
    [TestCase(INS_adr, true)]
    [TestCase(INS_movt, false)]
    [TestCase(INS_movt, true)]
    [TestCase(INS_movw, false)]
    [TestCase(INS_movw, true)]
    public static void LabelAddressRelocationFlagsFollowCompilerRelocationPolicy(
        instruction ins, bool compReloc)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            compiler.opts.compReloc = compReloc;
            var descriptor = RecordAddress(
                codeGen.Emitter, ins, EA_4BYTE | EA_DSP_RELOC_FLG, Label(), REG_R4);

            Assert.That(descriptor.idIsDspReloc(), Is.EqualTo(compReloc));
        });
    }

    [Test]
    public static void AdrAcrossTheHotColdBoundaryRemainsLong()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            var target = Label();
            target.SetFlags(BBF_COLD);
            compiler.fgFirstColdBlock = target;

            var descriptor = RecordAddress(codeGen.Emitter, INS_adr, EA_4BYTE, target, REG_R4);

            Assert.That(descriptor.idjKeepLong, Is.True);
        });
    }

#if DEBUG
    [Test]
    [TestCase(INS_adr)]
    [TestCase(INS_movt)]
    [TestCase(INS_movw)]
    public static void CatchReturnLabelAddressSetsItsDebugMarker(instruction ins)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = compiler.compCurBB = new BasicBlock(null, null);
            var target = Label();
            source.SetKindAndTargetEdge(BBJ_EHCATCHRET, new FlowEdge(source, target, null));

            var descriptor = RecordAddress(codeGen.Emitter, ins, EA_4BYTE, target, REG_R4);
            var debugInfo = descriptor.idDebugOnlyInfo()
                ?? throw new AssertionException("Missing address descriptor debug information.");

            Assert.That(debugInfo.idCatchRet, Is.True);
        });
    }
#endif

    private static Emitter.instrDescJmp RecordAddress(
        Emitter emitter, instruction ins, emitAttr attr, BasicBlock target, regNumber reg)
    {
#if DEBUG
        var failure = Assert.Throws<FatalJitException>(() => emitter.emitIns_R_L(ins, attr, target, reg));
        Assert.That(failure?.Message, Is.EqualTo("Instruction sanity checking outside AMD64 is not ported."));
#else
        emitter.emitIns_R_L(ins, attr, target, reg);
#endif
        return LastInstruction(emitter) as Emitter.instrDescJmp
            ?? throw new AssertionException("No label-address descriptor was recorded.");
    }

    private static BasicBlock Label()
    {
        var block = new BasicBlock(null, null);
        block.SetFlags(BBF_HAS_LABEL);

        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int GroupSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGjmpList")]
    private static extern ref Emitter.instrDescJmp? PendingJump(Emitter emitter);
}
#endif
