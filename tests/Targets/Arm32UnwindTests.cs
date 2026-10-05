// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm32UnwindTests
{
#if DEBUG
    [TestCase(0x7F, 2u)]
    [TestCase(0x80, 4u)]
    [TestCase(0xBF, 4u)]
    [TestCase(0xC0, 2u)]
    [TestCase(0xD7, 2u)]
    [TestCase(0xD8, 4u)]
    [TestCase(0xE8, 4u)]
    [TestCase(0xEC, 2u)]
    [TestCase(0xEF, 4u)]
    [TestCase(0xF5, 4u)]
    [TestCase(0xF7, 2u)]
    [TestCase(0xF8, 2u)]
    [TestCase(0xF9, 4u)]
    [TestCase(0xFA, 4u)]
    [TestCase(0xFB, 2u)]
    [TestCase(0xFC, 4u)]
    public static void DiagnosticInstructionSizesMatchThumbEncoding(byte header, uint expectedSize)
    {
        var codes = CreateCodes(header);
        fixed (byte* pointer = codes)
        {
            var source = new DiagnosticCodes(pointer);
            Assert.That(source.GetCodeSizeFromUnwindCodes(true), Is.EqualTo(expectedSize));
        }
    }

    [TestCase(0xFD, true, 0u)]
    [TestCase(0xFD, false, 2u)]
    [TestCase(0xFE, true, 0u)]
    [TestCase(0xFE, false, 4u)]
    [TestCase(0xFF, false, 0u)]
    public static void EpilogEndCodesCountTheirThumbPadding(byte endCode, bool isProlog, uint expectedSize)
    {
        byte[] codes = [endCode];
        fixed (byte* pointer = codes)
        {
            var source = new DiagnosticCodes(pointer);
            Assert.That(source.GetCodeSizeFromUnwindCodes(isProlog), Is.EqualTo(expectedSize));
        }
    }
#endif

    [Test]
    public static void IntegerPushMaskRecordsExtendedThumbInstruction()
    {
        WithProlog((compiler, emitter) =>
        {
            compiler.unwindPushMaskInt(new regMaskTP(SRBM_R8));

            var location = compiler.funCurrentFunc().GetUnwindInfo().GetCurrentEmitterLocation();
            Assert.That(location?.IsCurrentLocation(emitter), Is.True);
        });
    }

    [Test]
    public static void FloatingPushAndIntegerPopMasksRecordUnwindCodes()
    {
        WithProlog((compiler, emitter) =>
        {
            compiler.unwindPushMaskFloat(new regMaskTP(SRBM_F16 | SRBM_F17));

            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            Assert.That(unwindInfo.GetCurrentEmitterLocation()?.IsCurrentLocation(emitter), Is.True);

            compiler.unwindPopMaskInt(new regMaskTP(SRBM_PC));
            Assert.That(unwindInfo.GetCurrentEmitterLocation()?.IsCurrentLocation(emitter), Is.True);
        });
    }

    [Test]
    public static void StackAllocationAndFrameRegisterRecordingUseNativeWidths()
    {
        WithProlog((compiler, emitter) =>
        {
            compiler.unwindAllocStack(512);
            var unwindInfo = compiler.funCurrentFunc().GetUnwindInfo();
            Assert.That(unwindInfo.GetCurrentEmitterLocation()?.IsCurrentLocation(emitter), Is.True);

            compiler.unwindSetFrameReg(REG_R11, 0);
            Assert.That(unwindInfo.GetCurrentEmitterLocation()?.IsCurrentLocation(emitter), Is.True);
        });
    }

#if FEATURE_CFI_SUPPORT
    [Test]
    public static void CfiFloatMaskUsesArmDwarfRegisterNumbers()
    {
        WithProlog((compiler, _) =>
        {
            Assert.That(compiler.generateCFIUnwindCodes(), Is.True);
            var func = compiler.funCurrentFunc();
            compiler.unwindPushMaskFloat(new regMaskTP(SRBM_F30 | SRBM_F31));

            var codes = func.cfiCodes
                ?? throw new AssertionException("CFI unwind codes were not initialized.");
            Assert.That(codes, Has.Count.EqualTo(2));
            Assert.That(codes[0].CfiOpCode, Is.EqualTo(0));
            Assert.That(codes[0].Offset, Is.EqualTo(8));
            Assert.That(codes[1].CfiOpCode, Is.EqualTo(2));
            Assert.That(codes[1].DwarfReg, Is.EqualTo(271));
        }, nativeAot: true);
    }
#endif

#if DEBUG
    private static byte[] CreateCodes(byte header)
    {
        var codeSize = unchecked((int)UnwindInfo.GetUnwindCodeSize(header));
        var codes = new byte[codeSize + 1];
        codes[0] = header;
        codes[^1] = 0xFF;
        return codes;
    }

    private sealed class DiagnosticCodes(byte* codes) : UnwindCodesBase
    {
        public override byte* GetCodes()
        {
            return codes;
        }
    }
#endif

    private static void WithProlog(Action<Compiler, Emitter> action, bool nativeAot = false)
    {
        ArmCalleeSavedRegisterTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.eeInfo.targetAbi = nativeAot
                ? CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI
                : CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;

            var unwindInfo = new UnwindInfo();
            compiler.compFuncInfos =
            [
                new FuncInfoDsc
                {
                    funKind = FuncKind.FUNC_ROOT,
                    uwi = unwindInfo,
                },
            ];
            compiler.compFuncInfoCount = 1;
            compiler.fgFuncletsCreated = true;
            var emitter = codeGen.Emitter;
            emitter.emitBegProlog();
            compiler.unwindBegProlog();
            ArmCalleeSavedRegisterTests.WithUnwindSizeCheckSkipped(
                unwindInfo,
                () => action(compiler, emitter));
        });
    }
}
#endif
