// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeBlk;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm32BlockMemoryCodeGenTests
{
    [Test]
    public static void LoopBlockDispatchEmitsReverseZeroingLoop()
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            var block = new GenTreeBlk(TYP_STRUCT, Physical(REG_R1, TYP_BYREF),
                Physical(REG_R0, TYP_INT), new ClassLayout(8))
            {
                _kind = BlkOpKindLoop,
            };
            codeGen.InternalRegisters.Add(block, Mask(REG_R2));

#if DEBUG
            RecordArm32Instructions(() => codeGen.genCodeForStoreBlk(block));
#else
            codeGen.genCodeForStoreBlk(block);
#endif

            var emitted = InstructionsSince(codeGen.Emitter, 0);
#if DEBUG
            Assert.That(emitted, Is.Empty);
#else
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo([INS_str, INS_mov, INS_str, INS_sub, INS_bne]));
            Assert.That(emitted[0].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[0].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(emitted[2].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[2].idReg2(), Is.EqualTo(REG_R1));
            Assert.That(emitted[2].idReg3(), Is.EqualTo(REG_R2));
#endif
        }, captureAssertions: true);
    }

    [TestCase(1, new[] { INS_strb })]
    [TestCase(2, new[] { INS_strh })]
    [TestCase(3, new[] { INS_strh, INS_strb })]
    [TestCase(4, new[] { INS_str })]
    [TestCase(5, new[] { INS_str, INS_strb })]
    [TestCase(7, new[] { INS_str, INS_strh, INS_strb })]
    [TestCase(8, new[] { INS_str, INS_str })]
    public static void InitBlockUnrollUsesExactScalarStoreWidths(int size, instruction[] expectedInstructions)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.compCurBB = new BasicBlock(null, null);
            codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);

            var block = new GenTreeBlk(TYP_STRUCT, Physical(REG_R1, TYP_BYREF),
                Physical(REG_R0, TYP_INT), new ClassLayout((uint)size))
            {
                _kind = BlkOpKindUnroll,
            };

#if DEBUG
            RecordArm32Instructions(() => codeGen.genCodeForInitBlkUnroll(block));
#else
            codeGen.genCodeForInitBlkUnroll(block);
#endif

            var emitted = InstructionsSince(codeGen.Emitter, 0);
#if DEBUG
            Assert.That(emitted, Has.Count.EqualTo(1));
            Assert.That(emitted[0].idIns(), Is.EqualTo(expectedInstructions[0]));
            Assert.That(emitted[0].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[0].idReg2(), Is.EqualTo(REG_R1));
#else
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()),
                Is.EqualTo(expectedInstructions));
            Assert.That(emitted.TrueForAll(static descriptor => descriptor.idReg1() == REG_R0), Is.True);
            Assert.That(emitted.TrueForAll(static descriptor => descriptor.idReg2() == REG_R1), Is.True);
#endif
        }, captureAssertions: true);
    }

    private static List<Emitter.instrDesc> InstructionsSince(Emitter emitter, int initialCount)
    {
        var descriptors = new List<Emitter.instrDesc>();
        var currentGroup = emitter.emitCurIG;

        for (var group = FirstGroup(emitter); group is not null; group = group.igNext)
        {
            if (group == currentGroup)
            {
                descriptors.AddRange(
                    CurrentDescriptors(emitter) ?? throw new AssertionException("Missing descriptor buffer."));
            }
            else if (group.igInsCnt > 0)
            {
                descriptors.AddRange(
                    group.igData ?? throw new AssertionException("Missing saved descriptor buffer."));
            }
        }

        return descriptors.GetRange(initialCount, descriptors.Count - initialCount);
    }

    private static void RecordArm32Instructions(TestDelegate action)
    {
#if DEBUG
        var failure = Assert.Throws<FatalJitException>(action)
            ?? throw new AssertionException("Missing expected ARM target sanity-check skip.");
        Assert.That(failure.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
#else
        action();
#endif
    }

    private static GenTreePhysReg Physical(regNumber reg, var_types type)
    {
        return new GenTreePhysReg(reg, type) { RegNum = reg };
    }

    private static regMaskTP Mask(regNumber reg)
    {
        return regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);
}
#endif
