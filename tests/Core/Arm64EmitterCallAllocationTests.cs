// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.GCInfo.GCtype;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64EmitterCallAllocationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void CallsCaptureCurrentGcStateBeforeTheUnportedRecorder(bool jump)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var codeGen = new CodeGen(compiler);
        codeGen.GCInfo.gcVarPtrSetCur = [0];
        codeGen.GCInfo.gcRegGCrefSetCur = new regMaskTP(SRBM_R19);
        codeGen.GCInfo.gcRegByrefSetCur = new regMaskTP(SRBM_R20);
        var parameters = new EmitCallParams { argSize = 16, isJump = jump };

        Assert.That(() => codeGen.genEmitCallWithCurrentGC(ref parameters), Throws.TypeOf<FatalJitException>());

        Assert.That(parameters.ptrVars, Is.SameAs(codeGen.GCInfo.gcVarPtrSetCur));
        Assert.That(parameters.gcrefRegs, Is.EqualTo(new regMaskTP(SRBM_R19)));
        Assert.That(parameters.byrefRegs, Is.EqualTo(new regMaskTP(SRBM_R20)));
        Assert.That(parameters.argSize, Is.EqualTo((nint)16));
        Assert.That(parameters.isJump, Is.EqualTo(jump));
    }

    [TestCase(-65, false)]
    [TestCase(-64, true)]
    [TestCase(63, true)]
    [TestCase(64, false)]
    public static void SmallConstantsUseSevenBitsWithoutBackwardNavigation(int value, bool fits)
    {
        Assert.That(Emitter.instrDesc.fitsInSmallCns(value), Is.EqualTo(fits));
        if (fits)
        {
            var descriptor = CallEmitter.Basic();
            descriptor.idSmallCns(value);
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(value));
        }
    }

    [TestCase(0, false)]
    [TestCase(63, false)]
    [TestCase(64, true)]
    [TestCase(-1, true)]
    public static void ArgumentCountSelectsTheNativeDescriptor(int count, bool large)
    {
        var descriptor = Direct(CreateEmitter(), count, [0], default, default, EA_UNKNOWN, EA_UNKNOWN, false);
        Assert.That(descriptor.idIsLargeCall(), Is.EqualTo(large));
        Assert.That(descriptor.idOpSize(), Is.EqualTo(EA_PTRSIZE));
        if (large)
        {
            Assert.That(CallEmitter.ArgumentCount(descriptor), Is.EqualTo(unchecked((uint)count)));
            Assert.That(descriptor.NativeLogicalSize, Is.EqualTo(80));
        }
        else
        {
            Assert.That(descriptor.idSmallCns(), Is.EqualTo(count));
        }
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    public static void LiveScratchByrefAndAsyncStateRequireLargeCalls(bool scratch, bool byref, bool asyncReturn)
    {
        var descriptor = Direct(CreateEmitter(), 0, [0],
            new regMaskTP(scratch ? SRBM_R0 : SRBM_NONE),
            new regMaskTP(byref ? SRBM_R19 : SRBM_NONE), EA_BYREF, EA_UNKNOWN, asyncReturn);
        Assert.That(descriptor.idIsLargeCall(), Is.EqualTo(scratch || byref || asyncReturn));
        Assert.That(descriptor.idGCref(), Is.EqualTo(GCT_BYREF));
        if (descriptor.idIsLargeCall())
        {
            Assert.That(CallEmitter.HasAsyncReturn(descriptor), Is.EqualTo(asyncReturn));
        }
    }

    [TestCase(EA_8BYTE, false, GCT_NONE)]
    [TestCase(EA_GCREF, true, GCT_GCREF)]
    [TestCase(EA_BYREF, true, GCT_BYREF)]
    public static void SecondReturnRegisterCarriesItsGcType(emitAttr second, bool large, GCInfo.GCtype gcType)
    {
        var descriptor = Direct(CreateEmitter(), 0, [0], default, default, EA_UNKNOWN, second, false);
        Assert.That(descriptor.idIsLargeCall(), Is.EqualTo(large));
        if (large)
        {
            Assert.That(CallEmitter.SecondGcType(descriptor), Is.EqualTo(gcType));
        }
    }

    [Test]
    public static void CalleeSavedGcRegistersRoundTripThroughBothFiveBitFields()
    {
        var mask = SRBM_R19 | SRBM_R20 | SRBM_R21 | SRBM_R22 | SRBM_R23 |
            SRBM_R24 | SRBM_R25 | SRBM_R26 | SRBM_R27 | SRBM_R28;
        var descriptor = Direct(CreateEmitter(), 0, [0], new regMaskTP(mask), default, EA_UNKNOWN, EA_UNKNOWN, false);
        Assert.That(descriptor.idIsLargeCall(), Is.False);
        Assert.That((uint)descriptor.idReg1(), Is.EqualTo(31u));
        Assert.That((uint)descriptor.idReg2(), Is.EqualTo(31u));
        Assert.That(DecodeRegisters(null, descriptor), Is.EqualTo((uint)mask));
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void Arm64DisplacementsDoNotParticipateInTheSmallCallDecision(bool forceLarge)
    {
        var displacement = nint.MaxValue;
        var descriptor = Indirect(CreateEmitter(), 0, displacement, [0], default, default,
            EA_UNKNOWN, EA_UNKNOWN, forceLarge);
        Assert.That(descriptor.idIsLargeCall(), Is.EqualTo(forceLarge));
        if (forceLarge)
        {
            Assert.That(CallEmitter.Displacement(descriptor), Is.EqualTo(displacement));
        }
    }

#if EMITTER_STATS
    [Test]
    public static void EachLargeCallIncrementsTheAllocationCounter()
    {
        var before = AllocationCount(null);
        try
        {
            var emitter = CreateEmitter();
            _ = Direct(emitter, 64, [0], default, default, EA_UNKNOWN, EA_UNKNOWN, false);
            _ = Indirect(emitter, 64, 0, [0], default, default, EA_UNKNOWN, EA_UNKNOWN, false);
            _ = Direct(emitter, 0, [0], default, default, EA_UNKNOWN, EA_UNKNOWN, false);
            Assert.That(AllocationCount(null), Is.EqualTo(unchecked(before + 2)));
        }
        finally
        {
            AllocationCount(null) = before;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "emitTotalIDescCGCACnt")]
    private static extern ref uint AllocationCount(Emitter? emitter);
#endif

    private static CallEmitter CreateEmitter()
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var emitter = new CallEmitter(new CodeGen(compiler));
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );
        return emitter;
    }

    private sealed class CallEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        internal static instrDesc Basic()
        {
            var descriptor = new instrDescBasic();
            descriptor.idIns(INS_bl);
            descriptor.idInsFmt(IF_BI_0C);
            return descriptor;
        }

        internal static uint ArgumentCount(instrDesc descriptor) => ((instrDescCGCA)descriptor).idcArgCnt;
        internal static nint Displacement(instrDesc descriptor) => ((instrDescCGCA)descriptor).idcDisp;
        internal static bool HasAsyncReturn(instrDesc descriptor) => ((instrDescCGCA)descriptor).hasAsyncContinuationRet();
        internal static GCInfo.GCtype SecondGcType(instrDesc descriptor) => ((instrDescCGCA)descriptor).idSecondGCref();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrCallDir")]
    private static extern Emitter.instrDesc Direct(Emitter emitter, int count, ReadOnlySpan<nint> variables,
        regMaskTP refs, regMaskTP byrefs, emitAttr result, emitAttr second, bool asyncReturn);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrCallInd")]
    private static extern Emitter.instrDesc Indirect(Emitter emitter, int count, nint displacement,
        ReadOnlySpan<nint> variables, regMaskTP refs, regMaskTP byrefs, emitAttr result, emitAttr second, bool asyncReturn);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "emitDecodeCallGCregs")]
    private static extern uint DecodeRegisters(Emitter? emitter, Emitter.instrDesc descriptor);
}
#endif
