// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
#if FEATURE_CFI_SUPPORT
using System.Linq;
#endif
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static class Arm64UnwindRecordingTests
{
    [TestCase(REG_R0)]
    [TestCase(REG_LR)]
    [TestCase(REG_V31)]
    public static void ReturnNeedsNoAdditionalUnwindCode(regNumber reg)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));

        Assert.DoesNotThrow(() => compiler.unwindReturn(reg));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public static void NativeRecordingUsesDescriptorOwnedUnwindInfoAndCapturesLocation(int operation)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            Assert.That(compiler.generateCFIUnwindCodes(), Is.False);
            ref var func = ref compiler.compFuncInfos[0];
            func.uwi = new UnwindInfo();
            var emitter = codeGen.Emitter;
            CurrentSize(emitter) = 0;
            emitter.emitBegProlog();
            compiler.unwindBegProlog();
            var unwindInfo = func.GetUnwindInfo();
            var initialLocation = unwindInfo.GetCurrentEmitterLocation()
                ?? throw new AssertionException("Unwind prolog location was not captured.");

            CurrentSize(emitter) = 4;
            switch (operation)
            {
                case 0:
                {
                    compiler.unwindAllocStack(512);
                    break;
                }

                case 1:
                {
                    compiler.unwindSetFrameReg(REG_FP, 8);
                    break;
                }

                case 2:
                {
                    compiler.unwindSaveRegPair(REG_FP, REG_LR, 504);
                    break;
                }

                case 3:
                {
                    compiler.unwindSaveRegPairPreindexed(REG_FP, REG_LR, -512);
                    break;
                }

                case 4:
                {
                    compiler.unwindSaveReg(REG_V15, 504);
                    break;
                }

                case 5:
                {
                    compiler.unwindSaveRegPreindexed(REG_LR, -256);
                    break;
                }

                case 6:
                {
                    compiler.unwindSaveNext();
                    break;
                }

                case 7:
                {
                    compiler.unwindNop();
                    break;
                }

                default:
                {
                    throw new AssertionException("Unknown unwind operation.");
                }
            }

            Assert.That(func.GetUnwindInfo(), Is.SameAs(unwindInfo));
            var currentLocation = unwindInfo.GetCurrentEmitterLocation()
                ?? throw new AssertionException("Unwind recording did not capture the emitter location.");
            Assert.That(initialLocation.IsCurrentLocation(emitter), Is.False);
            Assert.That(currentLocation.IsCurrentLocation(emitter), Is.True);
            compiler.unwindEndProlog();
        });
    }

#if FEATURE_CFI_SUPPORT
    [Test]
    public static void DwarfMappingIncludesEveryIntegerAndVectorRegister()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        for (var index = 0; index < 31; index++)
        {
            Assert.That(compiler.mapRegNumToDwarfReg((regNumber)((int)REG_R0 + index)), Is.EqualTo(index));
        }

        Assert.That(compiler.mapRegNumToDwarfReg(REG_SP), Is.EqualTo(31));
        for (var index = 0; index < 32; index++)
        {
            Assert.That(compiler.mapRegNumToDwarfReg((regNumber)((int)REG_V0 + index)), Is.EqualTo(64 + index));
        }
    }

    [TestCase(false, REG_R19, REG_R20, 0)]
    [TestCase(false, REG_FP, REG_LR, 504)]
    [TestCase(false, REG_V14, REG_V15, 504)]
    [TestCase(true, REG_R19, REG_R20, -256)]
    [TestCase(true, REG_FP, REG_LR, -512)]
    [TestCase(true, REG_V14, REG_V15, -8)]
    public static void CfiPairSavesPreserveAdjustmentAndRegisterRecordOrder(
        bool preindexed, regNumber first, regNumber second, int offset)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.funCurrentFunc().cfiCodes = [];
            codeGen.Emitter.emitBegProlog();
            codeGen.Emitter.emitIns(INS_nop);
            Assert.That(compiler.generateCFIUnwindCodes(), Is.True);

            if (preindexed)
            {
                compiler.unwindSaveRegPairPreindexed(first, second, offset);
            }
            else
            {
                compiler.unwindSaveRegPair(first, second, offset);
            }

            (byte, byte, short, int)[] expected = preindexed
                ? [(4, 0, -1, -offset), (4, 2, compiler.mapRegNumToDwarfReg(first), 0),
                    (4, 2, compiler.mapRegNumToDwarfReg(second), 8)]
                : [(4, 2, compiler.mapRegNumToDwarfReg(first), offset),
                    (4, 2, compiler.mapRegNumToDwarfReg(second), offset + 8)];
            Assert.That(compiler.funCurrentFunc().cfiCodes?.Select(
                code => (code.CodeOffset, code.CfiOpCode, code.DwarfReg, code.Offset)), Is.EqualTo(expected));
        });
    }

    [TestCase(false, REG_R19, 0)]
    [TestCase(false, REG_LR, 504)]
    [TestCase(false, REG_V15, 504)]
    [TestCase(true, REG_R19, -256)]
    [TestCase(true, REG_V8, -8)]
    public static void CfiSingleSavesPreserveAdjustmentAndRelativeOffset(
        bool preindexed, regNumber reg, int offset)
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.funCurrentFunc().cfiCodes = [];
            codeGen.Emitter.emitBegProlog();
            codeGen.Emitter.emitIns(INS_nop);

            if (preindexed)
            {
                compiler.unwindSaveRegPreindexed(reg, offset);
            }
            else
            {
                compiler.unwindSaveReg(reg, offset);
            }

            (byte, byte, short, int)[] expected = preindexed
                ? [(4, 0, -1, -offset), (4, 2, compiler.mapRegNumToDwarfReg(reg), 0)]
                : [(4, 2, compiler.mapRegNumToDwarfReg(reg), offset)];
            Assert.That(compiler.funCurrentFunc().cfiCodes?.Select(
                code => (code.CodeOffset, code.CfiOpCode, code.DwarfReg, code.Offset)), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void CfiEpilogSkipsArm64SaveStackAndFrameRecords()
    {
        Arm64CalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.funCurrentFunc().cfiCodes = [];
            var group = CurrentGroup(codeGen.Emitter) ?? throw new AssertionException("Missing instruction group.");
            group.igFlags = InsGroupFlags.Epilog;

            compiler.unwindAllocStack(16);
            compiler.unwindSetFrameReg(REG_FP, 0);
            compiler.unwindSaveRegPair(REG_FP, REG_LR, 0);
            compiler.unwindSaveRegPairPreindexed(REG_R19, REG_R20, -256);
            compiler.unwindSaveReg(REG_V8, 0);
            compiler.unwindSaveRegPreindexed(REG_LR, -256);

            Assert.That(compiler.funCurrentFunc().cfiCodes, Is.Empty);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIG")]
    private static extern ref insGroup? CurrentGroup(Emitter emitter);
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);
}
#endif
