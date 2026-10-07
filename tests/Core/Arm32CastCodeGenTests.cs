#if TARGET_ARM
// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.emitJumpKind;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.SpecialCodeKind;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm32CastCodeGenTests
{
    [TestCase(TYP_INT, false, TYP_FLOAT, INS_vcvt_i2f)]
    [TestCase(TYP_INT, true, TYP_FLOAT, INS_vcvt_u2f)]
    [TestCase(TYP_INT, false, TYP_DOUBLE, INS_vcvt_i2d)]
    [TestCase(TYP_INT, true, TYP_DOUBLE, INS_vcvt_u2d)]
    public static void IntToFloatCastsPreserveSignednessAndDestinationWidth(
        var_types sourceType, bool isUnsigned, var_types destinationType, instruction conversion)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = Register(REG_R1, sourceType);
            var cast = new GenTreeCast(destinationType, source, isUnsigned, destinationType)
            {
                RegNum = REG_F0,
            };
            var initialCount = InstructionCount(codeGen.Emitter);

            RecordArm32Instructions(() => codeGen.genCodeForCast(cast));

            var emitted = InstructionsSince(codeGen.Emitter, initialCount);
            Assert.That(emitted, Has.Count.EqualTo(2));
            Assert.That(emitted[0].idIns(), Is.EqualTo(INS_vmov_i2f));
            Assert.That(emitted[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(emitted[1].idIns(), Is.EqualTo(conversion));
            Assert.That(emitted[1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(emitted[1].idReg1(), Is.EqualTo(REG_F0));
            Assert.That(emitted[1].idReg2(), Is.EqualTo(REG_F0));
        }, captureAssertions: true);
    }

    [TestCase(TYP_FLOAT, TYP_INT, INS_vcvt_f2i)]
    [TestCase(TYP_FLOAT, TYP_UINT, INS_vcvt_f2u)]
    [TestCase(TYP_DOUBLE, TYP_INT, INS_vcvt_d2i)]
    [TestCase(TYP_DOUBLE, TYP_UINT, INS_vcvt_d2u)]
    public static void FloatToIntCastsUseAVfpTemporary(
        var_types sourceType, var_types destinationType, instruction conversion)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var source = Register(REG_F0, sourceType);
            var cast = new GenTreeCast(destinationType, source, fromUnsigned: false, castType: destinationType)
            {
                RegNum = REG_R0,
            };
            codeGen.InternalRegisters.Add(cast, new regMaskTP(SRBM_F2));
            var initialCount = InstructionCount(codeGen.Emitter);

            RecordArm32Instructions(() => codeGen.genCodeForCast(cast));

            var emitted = InstructionsSince(codeGen.Emitter, initialCount);
            Assert.That(emitted, Has.Count.EqualTo(2));
            Assert.That(emitted[0].idIns(), Is.EqualTo(conversion));
            Assert.That(emitted[0].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(emitted[0].idReg1(), Is.EqualTo(REG_F2));
            Assert.That(emitted[0].idReg2(), Is.EqualTo(REG_F0));
            Assert.That(emitted[1].idIns(), Is.EqualTo(INS_vmov_f2i));
            Assert.That(emitted[1].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[1].idReg2(), Is.EqualTo(REG_F2));
        }, captureAssertions: true);
    }

    [TestCase(false, TYP_INT)]
    [TestCase(true, TYP_INT)]
    [TestCase(false, TYP_UINT)]
    [TestCase(true, TYP_UINT)]
    public static void UncheckedLongToIntCastUsesTheLowWord(bool isUnsigned, var_types destinationType)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((_, codeGen) =>
        {
            var cast = LongToIntCast(isUnsigned, destinationType);
            var initialCount = InstructionCount(codeGen.Emitter);

            RecordArm32Instructions(() => codeGen.genCodeForCast(cast));

            var emitted = InstructionsSince(codeGen.Emitter, initialCount);
            Assert.That(emitted, Has.Count.EqualTo(1));
            Assert.That(emitted[0].idIns(), Is.EqualTo(INS_mov));
            Assert.That(emitted[0].idReg1(), Is.EqualTo(REG_R0));
            Assert.That(emitted[0].idReg2(), Is.EqualTo(REG_R1));
        }, captureAssertions: true);
    }

    [TestCase(false, TYP_INT, REG_R1, new instruction[] { INS_tst, INS_bmi, INS_tst, INS_bne, INS_b, INS_cmp, INS_bne, INS_mov })]
    [TestCase(true, TYP_INT, REG_R1, new instruction[] { INS_tst, INS_bmi, INS_tst, INS_bne, INS_mov })]
    [TestCase(false, TYP_UINT, REG_R2, new instruction[] { INS_tst, INS_bne, INS_mov })]
    [TestCase(true, TYP_UINT, REG_R2, new instruction[] { INS_tst, INS_bne, INS_mov })]
    public static void CheckedLongToIntCastRetainsNativeOverflowChecks(
        bool isUnsigned, var_types destinationType, regNumber expectedSource, instruction[] expected)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            ConfigureOverflowTarget(compiler, codeGen);
            var cast = LongToIntCast(isUnsigned, destinationType);
            cast.Flags |= GTF_OVERFLOW;
            var initialCount = InstructionCount(codeGen.Emitter);

            RecordArm32Instructions(() => codeGen.genCodeForCast(cast));
            var emitted = InstructionsSince(codeGen.Emitter, initialCount);
            Assert.That(emitted.ConvertAll(static descriptor => descriptor.idIns()), Is.EqualTo(expected));
        }, captureAssertions: true);
    }

    private static GenTreeCast LongToIntCast(bool isUnsigned, var_types destinationType)
    {
        var low = Register(REG_R1, TYP_INT);
        var high = Register(REG_R2, TYP_INT);
        var source = new GenTreeOp(GT_LONG, TYP_LONG, low, high);

        return new GenTreeCast(destinationType, source, isUnsigned, destinationType)
        {
            RegNum = REG_R0,
        };
    }

    private static GenTreePhysReg Register(regNumber reg, var_types type)
    {
        return new GenTreePhysReg(reg, type) { RegNum = reg };
    }

    private static void ConfigureOverflowTarget(Compiler compiler, CodeGen codeGen)
    {
        compiler.compCurBB = new BasicBlock(null, null);
        codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
        var target = new BasicBlock(null, null);
        target.SetFlags(BBF_HAS_LABEL | BBF_THROW_HELPER);
        var descriptor = compiler.fgGetExcptnTarget(SCK_OVERFLOW, compiler.compCurBB);
        descriptor.acdUsed = true;
        descriptor.acdDstBlk = target;
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

    private static int InstructionCount(Emitter emitter)
    {
        return InstructionsSince(emitter, 0).Count;
    }

    private static void RecordArm32Instructions(TestDelegate action)
    {
#if DEBUG
        try
        {
            action();
        }
        catch (FatalJitException failure) when (failure.Result == CorJitResult.CORJIT_SKIPPED)
        {
        }
#else
        action();
#endif
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstGroup(Emitter emitter);
}
#endif
