// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64CalleeSavedRegisterTests
{
    [TestCase(nameof(Compiler.FrameInfo.frameType))]
    [TestCase(nameof(Compiler.FrameInfo.calleeSaveSpOffset))]
    [TestCase(nameof(Compiler.FrameInfo.calleeSaveSpDelta))]
    [TestCase(nameof(Compiler.FrameInfo.offsetSpToSavedFp))]
    public static void FrameInfoFieldsDefaultToZero(string name)
    {
        var field = typeof(Compiler.FrameInfo).GetField(name) ??
            throw new AssertionException($"Missing frame field {name}.");

        Assert.That(field.FieldType, Is.EqualTo(typeof(int)));
        Assert.That(field.GetValue(default(Compiler.FrameInfo)), Is.EqualTo(0));
    }

    [Test]
    public static void FrameInfoPreservesOnlyTheFourPinnedDeclarationsInOrder()
    {
        var names = typeof(Compiler.FrameInfo).GetFields().OrderBy(field => field.MetadataToken)
            .Select(field => field.Name).ToArray();

        string[] expectedNames =
        [
            nameof(Compiler.FrameInfo.frameType),
            nameof(Compiler.FrameInfo.calleeSaveSpOffset),
            nameof(Compiler.FrameInfo.calleeSaveSpDelta),
            nameof(Compiler.FrameInfo.offsetSpToSavedFp),
        ];
        Assert.That(names, Is.EqualTo(expectedNames));
    }

    [Test]
    public static void FrameInfoCopiesPersistIndependentlyThroughTheExistingCompilerField()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var frame = new Compiler.FrameInfo
        {
            frameType = 3,
            calleeSaveSpOffset = 8,
            calleeSaveSpDelta = 48,
            offsetSpToSavedFp = 512,
        };
        compiler.compFrameInfo = frame;
        frame.calleeSaveSpOffset = 0;

        Assert.That(compiler.compFrameInfo.frameType, Is.EqualTo(3));
        Assert.That(compiler.compFrameInfo.calleeSaveSpOffset, Is.EqualTo(8));
        Assert.That(compiler.compFrameInfo.calleeSaveSpDelta, Is.EqualTo(48));
        Assert.That(compiler.compFrameInfo.offsetSpToSavedFp, Is.EqualTo(512));
        Assert.That(frame.calleeSaveSpOffset, Is.Zero);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public static void PairConstructionPreservesGapsBanksFramePairAndSaveNext(int selection)
    {
        (regNumber[] Registers, (regNumber First, regNumber Second, bool UseSaveNextPair)[] Pairs) data =
            selection switch
            {
                0 => ([], []),
                1 => ([REG_R19], [(REG_R19, REG_NA, false)]),
                2 => ([REG_R19, REG_R20, REG_R21, REG_R22],
                    [(REG_R19, REG_R20, false), (REG_R21, REG_R22, true)]),
                3 => ([REG_R19, REG_R21, REG_R22, REG_R24],
                    [(REG_R19, REG_NA, false), (REG_R21, REG_R22, false), (REG_R24, REG_NA, false)]),
                4 => ([REG_R28, REG_FP, REG_LR],
                    [(REG_R28, REG_NA, false), (REG_FP, REG_LR, false)]),
                5 => ([REG_V8, REG_V9, REG_V10, REG_V11],
                    [(REG_V8, REG_V9, false), (REG_V10, REG_V11, true)]),
                6 => ([REG_R19, REG_R20, REG_V8, REG_V9],
                    [(REG_R19, REG_R20, false), (REG_V8, REG_V9, false)]),
                7 => ([REG_R27, REG_R28, REG_FP, REG_LR],
                    [(REG_R27, REG_R28, false), (REG_FP, REG_LR, true)]),
                _ => throw new AssertionException("Unknown register-pair case."),
            };
        List<(regNumber First, regNumber Second, bool UseSaveNextPair)> stack = [];

        BuildPairs(null, Mask(data.Registers), stack);

        Assert.That(stack, Is.EqualTo(data.Pairs));
        Assert.That(CountRegisters(null, Mask(data.Registers)), Is.EqualTo(data.Registers.Length));
        Assert.That(SlotSize(null, Mask(data.Registers)), Is.EqualTo(8));
    }

#if HAS_MORE_THAN_64_REGISTERS
    [Test]
    public static void RegisterCountIncludesBothMaskBanks()
    {
        var mask = new regMaskTP(SRBM_R19 | SRBM_V8, SRBM_P8 | SRBM_P9);

        Assert.That(CountRegisters(null, mask), Is.EqualTo(4));
    }
#endif

    [TestCase(REG_R19, REG_R20, 0, 16, true)]
    [TestCase(REG_R19, REG_R20, 0, 496, true)]
    [TestCase(REG_R19, REG_R20, 0, 512, false)]
    [TestCase(REG_R19, REG_R20, 8, 16, false)]
    [TestCase(REG_R19, REG_R20, -512, 0, false)]
    [TestCase(REG_R19, REG_R20, 504, 0, false)]
    [TestCase(REG_V8, REG_V9, 0, 16, true)]
    [TestCase(REG_V8, REG_V9, 8, 512, false)]
    public static void PairRestoreUsesSignedScaledOffsetsAndNativePostIndexLimit(
        regNumber first, regNumber second, int offset, int delta, bool folded)
    {
        WithCodeGen((_, codeGen) =>
        {
            RestorePair(codeGen, first, second, REG_SPBASE, offset, delta, false, REG_IP1, null, false);
            var descriptors = Descriptors(codeGen);
            var separateAdjustment = delta != 0 && !folded;

            Assert.That(descriptors, Has.Count.EqualTo(separateAdjustment ? 2 : 1));
            var load = descriptors[0];
            Assert.That(load.idIns(), Is.EqualTo(INS_ldp));
            Assert.That(load.idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(load.idReg1(), Is.EqualTo(first));
            Assert.That(load.idReg2(), Is.EqualTo(second));
            Assert.That(load.idReg3(), Is.EqualTo(REG_ZR));
            Assert.That(load.idInsOpt(), Is.EqualTo(folded ? INS_OPTS_POST_INDEX : INS_OPTS_NONE));
            Assert.That(Emitter.emitGetInsSC(load), Is.EqualTo((nint)((folded ? delta : offset) / 8)));
            if (separateAdjustment)
            {
                AssertStackAdjustment(descriptors[1], INS_add, delta);
            }
        });
    }

    [TestCase(REG_R19, 0, 240, true)]
    [TestCase(REG_R19, 0, 256, false)]
    [TestCase(REG_R19, 8, 16, false)]
    [TestCase(REG_R19, 0, 0, false)]
    [TestCase(REG_V8, 0, 240, true)]
    public static void SingleRestoreUsesUnscaledPostIndexLimitAndSeparateAdjustment(
        regNumber reg, int offset, int delta, bool folded)
    {
        WithCodeGen((_, codeGen) =>
        {
            RestoreSingle(codeGen, reg, REG_SPBASE, offset, delta, REG_IP1, null, false);
            var descriptors = Descriptors(codeGen);
            var separateAdjustment = delta != 0 && !folded;

            Assert.That(descriptors, Has.Count.EqualTo(separateAdjustment ? 2 : 1));
            var load = descriptors[0];
            Assert.That(load.idIns(), Is.EqualTo(INS_ldr));
            Assert.That(load.idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(load.idReg1(), Is.EqualTo(reg));
            Assert.That(load.idReg2(), Is.EqualTo(REG_ZR));
            Assert.That(load.idInsOpt(), Is.EqualTo(folded ? INS_OPTS_POST_INDEX : INS_OPTS_NONE));
            if (folded)
            {
                Assert.That(Emitter.emitGetInsSC(load), Is.EqualTo((nint)delta));
            }
            if (separateAdjustment)
            {
                AssertStackAdjustment(descriptors[1], INS_add, delta);
            }
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void RestoreGroupReversesSaveOrderAndRegisterOrderOnlyUnderStress(bool reverse, bool floats)
    {
        WithCodeGen((_, codeGen) =>
        {
            codeGen.genReverseAndPairCalleeSavedRegisters = reverse;
            regNumber[] registers = floats
                ? [REG_V8, REG_V9, REG_V10, REG_V11, REG_V13]
                : [REG_R19, REG_R20, REG_R21, REG_R22, REG_R24];

            RestoreGroup(codeGen, Mask(registers), REG_SPBASE, 0, 48, false);
            var descriptors = Descriptors(codeGen);
            regNumber[] expected = reverse
                ? [registers[1], registers[3], registers[4]]
                : [registers[4], registers[2], registers[0]];
            Assert.That(descriptors.Select(id => id.idReg1()), Is.EqualTo(expected));
            instruction[] expectedInstructions = reverse
                ? [INS_ldp, INS_ldp, INS_ldr]
                : [INS_ldr, INS_ldp, INS_ldp];
            Assert.That(descriptors.Select(id => id.idIns()), Is.EqualTo(expectedInstructions));
            Assert.That(descriptors.All(id => id.idOpSize() == EA_8BYTE), Is.True);
            var pairs = descriptors.Where(id => id.idIns() == INS_ldp).ToArray();
            nint[] expectedOffsets = reverse ? [4, 2] : [3, 1];
            Assert.That(pairs.Select(Emitter.emitGetInsSC), Is.EqualTo(expectedOffsets));
            regNumber[] expectedSecondRegisters = reverse
                ? [registers[0], registers[2]]
                : [registers[3], registers[1]];
            Assert.That(pairs.Select(id => id.idReg2()), Is.EqualTo(expectedSecondRegisters));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RestoreGroupAdjustsStackOnlyWithTheFinalRestore(bool reverse)
    {
        WithCodeGen((_, codeGen) =>
        {
            codeGen.genReverseAndPairCalleeSavedRegisters = reverse;
            RestoreGroup(codeGen, Mask(REG_R19, REG_R20, REG_R21, REG_R22), REG_SPBASE, 32, 32, false);
            var descriptors = Descriptors(codeGen);

            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idInsOpt(), Is.EqualTo(INS_OPTS_NONE));
            Assert.That(Emitter.emitGetInsSC(descriptors[0]), Is.EqualTo((nint)2));
            Assert.That(descriptors[1].idInsOpt(), Is.EqualTo(INS_OPTS_POST_INDEX));
            Assert.That(Emitter.emitGetInsSC(descriptors[1]), Is.EqualTo((nint)4));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(reverse ? REG_R20 : REG_R21));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(reverse ? REG_R22 : REG_R19));
        });
    }

    [TestCase(0, -512, INS_stp, INS_OPTS_PRE_INDEX,
        "ARM64 unwind pre-indexed register-pair recording is not ported.")]
    [TestCase(0, -528, INS_sub, INS_OPTS_NONE, "Unwind recording requires Windows AMD64.")]
    [TestCase(8, -16, INS_sub, INS_OPTS_NONE, "Unwind recording requires Windows AMD64.")]
    [TestCase(504, 0, INS_stp, INS_OPTS_NONE, "ARM64 unwind register-pair recording is not ported.")]
    public static void PairSaveTerminatesAtRealUnwindBoundaryAfterNativeFirstInstruction(
        int offset, int delta, instruction expected, insOpts options, string message)
    {
        WithCodeGen((_, codeGen) =>
        {
            AssertFailure(() => SavePair(codeGen, REG_R19, REG_R20, offset, delta, false, REG_IP0, null), message);
            var descriptor = Descriptors(codeGen).Single();

            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(options));
            if (expected == INS_stp)
            {
                Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)((delta != 0 ? delta : offset) / 8)));
            }
            else
            {
                AssertStackAdjustment(descriptor, INS_sub, -delta);
            }
        });
    }

    [TestCase(0, -256, INS_str, INS_OPTS_PRE_INDEX,
        "ARM64 unwind pre-indexed register recording is not ported.")]
    [TestCase(0, -272, INS_sub, INS_OPTS_NONE, "Unwind recording requires Windows AMD64.")]
    [TestCase(8, -16, INS_sub, INS_OPTS_NONE, "Unwind recording requires Windows AMD64.")]
    [TestCase(32760, 0, INS_str, INS_OPTS_NONE, "Unwind recording requires Windows AMD64.")]
    public static void SingleSaveTerminatesAtRealUnwindBoundaryAfterNativeFirstInstruction(
        int offset, int delta, instruction expected, insOpts options, string message)
    {
        WithCodeGen((_, codeGen) =>
        {
            AssertFailure(() => SaveSingle(codeGen, REG_R19, offset, delta, REG_IP0, null), message);
            var descriptor = Descriptors(codeGen).Single();

            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(options));
            if (options == INS_OPTS_PRE_INDEX)
            {
                Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)delta));
            }
            if (expected == INS_sub)
            {
                AssertStackAdjustment(descriptor, INS_sub, -delta);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SaveGroupUsesNativeReverseStressSelectionBeforeUnwindTermination(bool reverse)
    {
        WithCodeGen((_, codeGen) =>
        {
            codeGen.genReverseAndPairCalleeSavedRegisters = reverse;
            var message = reverse
                ? "Unwind recording requires Windows AMD64."
                : "ARM64 unwind register-pair recording is not ported.";

            AssertFailure(() =>
                SaveGroup(codeGen, Mask(REG_R19, REG_R20, REG_R21, REG_R22, REG_R24), 0, 8), message);

            var descriptor = Descriptors(codeGen).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(reverse ? INS_str : INS_stp));
            Assert.That(descriptor.idReg1(), Is.EqualTo(reverse ? REG_R24 : REG_R19));
            Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SaveNextSelectionTerminatesAfterRecordingTheNextRegisterPair(bool restore)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.emitIns_R_R_R_I(restore ? INS_ldp : INS_stp, EA_8BYTE,
                REG_R19, REG_R20, REG_SPBASE, 0);
            var message = TargetOS.IsUnix && compiler.generateCFIUnwindCodes()
                ? "ARM64 unwind register-pair recording is not ported."
                : "ARM64 unwind save-next recording is not ported.";
            AssertFailure(() =>
            {
                if (restore)
                {
                    RestorePair(codeGen, REG_R21, REG_R22, REG_SPBASE, 16, 0, true, REG_IP1, null, true);
                }
                else
                {
                    SavePair(codeGen, REG_R21, REG_R22, 16, 0, true, REG_IP0, null);
                }
            }, message);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(restore ? INS_ldp : INS_stp));
            Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_R21));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_R22));
            Assert.That(Emitter.emitGetInsSC(descriptors[1]), Is.EqualTo((nint)2));
        });
    }

    [TestCase(16)]
    [TestCase(4097)]
    public static void ScratchZeroRefWriteRemainsAfterTheRealConstantInstructionDependency(int amount)
    {
        WithCodeGen((_, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            var zeroed = stackalloc bool[1];
            *zeroed = true;
            AssertFailure(() => StackAdjustment(codeGen, -amount, REG_R9, zeroed, true),
                amount == 16
                    ? "Unwind recording requires Windows AMD64."
                    : "Target prolog unwind padding is not ported.");

            Assert.That(*zeroed, Is.True);
            var descriptors = Descriptors(codeGen);
            if (amount == 16)
            {
                AssertStackAdjustment(descriptors.Single(), INS_sub, amount);
            }
            else
            {
                Assert.That(descriptors, Is.Not.Empty);
                Assert.That(descriptors.All(id => id.idReg1() == REG_R9), Is.True);
            }
        });
    }

    [TestCase(0, 0, false, false, false, INS_stp, REG_FP, -16)]
    [TestCase(496, 0, false, false, false, INS_sub, REG_ZR, 512)]
    [TestCase(32, 16, false, false, false, INS_sub, REG_ZR, 48)]
    [TestCase(512, 0, false, false, false, INS_sub, REG_ZR, 528)]
    [TestCase(512, 0, false, true, false, INS_stp, REG_R19, -16)]
    [TestCase(512, 512, false, false, false, INS_stp, REG_FP, -16)]
    [TestCase(0, 0, true, true, false, INS_stp, REG_R19, -32)]
    [TestCase(32, 16, true, false, false, INS_sub, REG_ZR, 48)]
    [TestCase(512, 0, true, false, false, INS_stp, REG_FP, -16)]
    [TestCase(0, 0, true, false, true, INS_stp, REG_FP, -16)]
    public static void PushPreservesAllFiveFrameChoicesBeforeUnwindTermination(
        int localSize, int outgoingSize, bool saveFrameAtTop, bool extraPair, bool enc,
        instruction expected, regNumber first, int immediate)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            codeGen.IsFramePointerUsed = true;
            SaveFrameAtTop(codeGen) = saveFrameAtTop;
            compiler.compLclFrameSize = localSize;
            compiler.compCalleeRegsPushed = extraPair ? 4 : 2;
            compiler.opts.compDbgEnC = enc;
            compiler.lvaOutgoingArgSpaceSize.Value = outgoingSize;
            if (extraPair)
            {
                codeGen.RegSet.rsSetRegsModified(Mask(REG_R19, REG_R20));
            }
            var zeroed = true;
            var message = expected == INS_stp
                ? "ARM64 unwind pre-indexed register-pair recording is not ported."
                : "Unwind recording requires Windows AMD64.";

            AssertFailure(() => codeGen.genPushCalleeSavedRegisters(REG_R19, ref zeroed), message);

            var descriptor = Descriptors(codeGen).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idReg1(), Is.EqualTo(first));
            Assert.That(Emitter.emitGetInsSC(descriptor),
                Is.EqualTo((nint)(expected == INS_stp ? immediate / 8 : immediate)));
            Assert.That(zeroed, Is.True);
            Assert.That(SavedMask(ref codeGen.RegSet), Is.EqualTo(extraPair
                ? Mask(REG_R19, REG_R20, REG_FP, REG_LR)
                : Mask(REG_FP, REG_LR)));
            Assert.That(compiler.compFrameInfo.frameType, Is.Zero);
            Assert.That(compiler.compFrameInfo.calleeSaveSpOffset, Is.Zero);
            Assert.That(compiler.compFrameInfo.calleeSaveSpDelta, Is.Zero);
            Assert.That(compiler.compFrameInfo.offsetSpToSavedFp, Is.Zero);
        });
    }

    [Test]
    public static void VarargsSpaceParticipatesInWholePushBeforeTheFirstUnwindBoundary()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            codeGen.IsFramePointerUsed = true;
            compiler.compCalleeRegsPushed = 2;
            compiler.info.compIsVarArgs = true;
            var zeroed = true;

            AssertFailure(() => codeGen.genPushCalleeSavedRegisters(REG_R19, ref zeroed),
                "ARM64 unwind pre-indexed register-pair recording is not ported.");

            var descriptor = Descriptors(codeGen).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(INS_stp));
            Assert.That(descriptor.idReg1(), Is.EqualTo(REG_FP));
            Assert.That(descriptor.idReg2(), Is.EqualTo(REG_LR));
            Assert.That(descriptor.idInsOpt(), Is.EqualTo(INS_OPTS_PRE_INDEX));
            Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)(-80 / 8)));
            Assert.That(zeroed, Is.True);
        });
    }

    [Test]
    public static void WholePushProbesBeforeSavingTheMaskAndDoesNotTrashTheIncomingInitRegister()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.emitBegProlog();
            codeGen.IsFramePointerUsed = true;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 4096;
            var zeroed = true;

            AssertFailure(() => codeGen.genPushCalleeSavedRegisters(REG_R19, ref zeroed),
                "Target prolog unwind padding is not ported.");

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_ldr));
            Assert.That(descriptors.Count(id => id.idIns() == INS_ldr), Is.EqualTo(1));
            Assert.That(descriptors.Where(id => id.idIns() != INS_ldr)
                .All(id => id.idReg1() == REG_SCRATCH), Is.True);
            Assert.That(descriptors.Any(id => id.idReg1() == REG_R19), Is.False);
            Assert.That(SavedMask(ref codeGen.RegSet), Is.EqualTo(RBM_NONE));
            Assert.That(zeroed, Is.True);
            Assert.That(compiler.compFrameInfo.frameType, Is.Zero);
        });
    }

    [TestCase(1, false, INS_ldp, "ARM64 unwind pre-indexed register-pair recording is not ported.")]
    [TestCase(2, false, INS_ldp, "ARM64 unwind register-pair recording is not ported.")]
    [TestCase(3, false, INS_ldp, "ARM64 unwind register-pair recording is not ported.")]
    [TestCase(4, false, INS_ldp, "ARM64 unwind pre-indexed register-pair recording is not ported.")]
    [TestCase(5, false, INS_mov, "Unwind recording requires Windows AMD64.")]
    [TestCase(1, true, INS_mov, "Unwind recording requires Windows AMD64.")]
    [TestCase(2, true, INS_sub, "Unwind recording requires Windows AMD64.")]
    [TestCase(3, true, INS_mov, "Unwind recording requires Windows AMD64.")]
    [TestCase(4, true, INS_mov, "Unwind recording requires Windows AMD64.")]
    [TestCase(5, true, INS_mov, "Unwind recording requires Windows AMD64.")]
    public static void PopPreservesFrameAndLocallocDispatchBeforeUnwindTermination(
        int frameType, bool localloc, instruction expected, string message)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var group = CurrentGroup(codeGen.Emitter) ??
                throw new AssertionException("Missing epilog instruction group.");
            group.igFlags = InsGroupFlags.Epilog;
            codeGen.IsFramePointerUsed = true;
            SaveFrameAtTop(codeGen) = frameType >= 4;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = frameType switch
            {
                2 => 16,
                3 or 5 => 512,
                _ => 0,
            };
            compiler.lvaOutgoingArgSpaceSize.Value = frameType == 2 ? 16 : 0;
            compiler.compLocallocUsed = localloc;
            compiler.compFrameInfo = new Compiler.FrameInfo
            {
                frameType = frameType,
                calleeSaveSpOffset = frameType switch
                {
                    1 => 16,
                    2 => 32,
                    _ => 0,
                },
                calleeSaveSpDelta = frameType >= 4 ? 16 : 0,
                offsetSpToSavedFp = frameType == 2 ? 16 : 0,
            };

            AssertFailure(() => codeGen.genPopCalleeSavedRegistersAndFreeLclFrame(false), message);

            var descriptor = Descriptors(codeGen).Single();
            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
            Assert.That(descriptor.idReg1(), Is.EqualTo(expected == INS_ldp ? REG_FP : REG_ZR));
            Assert.That(compiler.compFrameInfo.frameType, Is.EqualTo(frameType));
        });
    }

    [TestCase(false, REG_V8, INS_stp)]
    [TestCase(true, REG_FP, INS_ldp)]
    public static void WholeSaveRestoreHelpersKeepTheNativeRegisterBankOrder(
        bool restore, regNumber expectedFirst, instruction expected)
    {
        WithCodeGen((_, codeGen) =>
        {
            var mask = Mask(REG_R19, REG_R20, REG_V8, REG_V9, REG_FP, REG_LR);
            AssertFailure(() =>
            {
                if (restore)
                {
                    RestoreHelp(codeGen, mask, 0, 0);
                }
                else
                {
                    SaveHelp(codeGen, mask, 0, 0);
                }
            }, "ARM64 unwind register-pair recording is not ported.");
            var descriptor = Descriptors(codeGen).Single();

            Assert.That(descriptor.idIns(), Is.EqualTo(expected));
            Assert.That(descriptor.idReg1(), Is.EqualTo(expectedFirst));
            Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo(restore ? (nint)4 : 0));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EmptySaveRestoreMasksStillPerformTheRequestedStackAdjustment(bool restore)
    {
        WithCodeGen((_, codeGen) =>
        {
            AssertFailure(() =>
            {
                if (restore)
                {
                    RestoreHelp(codeGen, RBM_NONE, 0, 64);
                }
                else
                {
                    SaveHelp(codeGen, RBM_NONE, 0, -64);
                }
            }, "Unwind recording requires Windows AMD64.");

            AssertStackAdjustment(Descriptors(codeGen).Single(), restore ? INS_add : INS_sub, 64);
        });
    }

    private static regMaskTP Mask(params regNumber[] registers)
    {
        var mask = RBM_NONE;
        foreach (var reg in registers)
        {
            regMaskTP.AddRegNumInMask(ref mask, reg);
        }

        return mask;
    }

    private static void AssertStackAdjustment(Emitter.instrDesc descriptor, instruction expected, int delta)
    {
        Assert.That(descriptor.idIns(), Is.EqualTo(expected));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
        Assert.That(descriptor.idReg1(), Is.EqualTo(REG_ZR));
        Assert.That(descriptor.idReg2(), Is.EqualTo(REG_ZR));
        Assert.That(Emitter.emitGetInsSC(descriptor), Is.EqualTo((nint)delta));
    }

    private static void AssertFailure(TestDelegate action, string message)
    {
        var failure = Assert.Throws<FatalJitException>(action) ??
            throw new AssertionException("Missing genuine unwind dependency failure.");

        Assert.That(failure.Message, Is.EqualTo(message));
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
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
            codeGen.resetFramePointerUsedWritePhase();
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIG")]
    private static extern ref insGroup? CurrentGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "genSaveFpLrWithAllCalleeSavedRegisters")]
    private static extern ref bool SaveFrameAtTop(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_rsMaskCalleeSaved")]
    private static extern ref regMaskTP SavedMask(ref RegSet regSet);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genPrologSaveRegPairArm64")]
    private static extern void SavePair(CodeGen codeGen, regNumber first, regNumber second, int offset, int delta,
        bool useSaveNextPair, regNumber scratch, bool* zeroed);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genPrologSaveRegArm64")]
    private static extern void SaveSingle(CodeGen codeGen, regNumber reg, int offset, int delta,
        regNumber scratch, bool* zeroed);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRestoreRegPairArm64")]
    private static extern void RestorePair(CodeGen codeGen, regNumber first, regNumber second, regNumber baseReg,
        int offset, int delta, bool useSaveNextPair, regNumber scratch, bool* zeroed, bool reportUnwindData);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRestoreRegArm64")]
    private static extern void RestoreSingle(CodeGen codeGen, regNumber reg, regNumber baseReg,
        int offset, int delta, regNumber scratch, bool* zeroed, bool reportUnwindData);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRestoreCalleeSavedRegisterGroupArm64")]
    private static extern void RestoreGroup(CodeGen codeGen, regMaskTP mask, regNumber baseReg,
        int delta, int topOffset, bool reportUnwindData);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genSaveCalleeSavedRegisterGroupArm64")]
    private static extern void SaveGroup(CodeGen codeGen, regMaskTP mask, int delta, int offset);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genStackPointerAdjustmentArm64")]
    private static extern void StackAdjustment(CodeGen codeGen, nint delta, regNumber scratch,
        bool* zeroed, bool reportUnwindData);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genSaveCalleeSavedRegistersHelpArm64")]
    private static extern void SaveHelp(CodeGen codeGen, regMaskTP mask, int offset, int delta);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genRestoreCalleeSavedRegistersHelpArm64")]
    private static extern void RestoreHelp(CodeGen codeGen, regMaskTP mask, int offset, int delta);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "genBuildRegPairsStackArm64")]
    private static extern void BuildPairs(CodeGen? codeGen, regMaskTP mask,
        List<(regNumber First, regNumber Second, bool UseSaveNextPair)> stack);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "genCalleeSaveCountArm64")]
    private static extern int CountRegisters(CodeGen? codeGen, regMaskTP mask);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "genGetSlotSizeForRegsInMaskArm64")]
    private static extern int SlotSize(CodeGen? codeGen, regMaskTP mask);
}
#endif
