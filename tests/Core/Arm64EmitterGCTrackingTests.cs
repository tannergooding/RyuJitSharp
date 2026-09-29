// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64EmitterGCTrackingTests
{
    [Test]
    public static void SimpleStackTrackingPreservesPointerAndByrefBits()
    {
        WithEmitter((emitter, buffer) =>
        {
            emitter.emitSimpleStkUsed = true;
            emitter.emitStackPush(buffer + 1, GCT_GCREF);
            emitter.emitStackPush(buffer + 2, GCT_BYREF);
            emitter.emitStackPushN(buffer + 3, 2);

            Assert.That(emitter.emitCurStackLvl, Is.EqualTo(16));
            Assert.That(unchecked((uint)emitter.u1.emitSimpleStkMask), Is.EqualTo(12u));
            Assert.That(unchecked((uint)emitter.u1.emitSimpleByrefStkMask), Is.EqualTo(4u));

            emitter.emitStackKillArgs(buffer + 4, 3, 0);
            Assert.That(unchecked((uint)emitter.u1.emitSimpleStkMask), Is.EqualTo(8u));
            Assert.That(unchecked((uint)emitter.u1.emitSimpleByrefStkMask), Is.Zero);
            emitter.emitStackPop(buffer + 5, false, 0, 2);

            Assert.That(emitter.emitCurStackLvl, Is.EqualTo(8));
            Assert.That(unchecked((uint)emitter.u1.emitSimpleStkMask), Is.EqualTo(2u));
        });
    }

    [Test]
    public static void FullMapTracksPushKillAndPopDescriptors()
    {
        WithEmitter((emitter, buffer) =>
        {
            var tracking = stackalloc byte[8];
            emitter.emitSimpleStkUsed = false;
            emitter.emitFullGCinfo = true;
            emitter.emitFullArgInfo = true;
            emitter.emitMaxStackDepth = 8;
            emitter.u2.emitArgTrackTab = tracking;
            emitter.u2.emitArgTrackTop = tracking;

            emitter.emitStackPushN(buffer + 1, 2);
            emitter.emitStackPush(buffer + 3, GCT_GCREF);
            Assert.That(emitter.emitCurStackLvl, Is.EqualTo(12));
            Assert.That(emitter.u2.emitGcArgTrackCnt, Is.EqualTo((ushort)3));

            emitter.emitStackKillArgs(buffer + 4, 1, 4);
            Assert.That(emitter.u2.emitGcArgTrackCnt, Is.EqualTo((ushort)3));
            emitter.emitStackPop(buffer + 5, false, 0, 3);

            Assert.That(emitter.emitCurStackLvl, Is.Zero);
            Assert.That((nint)emitter.u2.emitArgTrackTop, Is.EqualTo((nint)tracking));
            Assert.That(emitter.u2.emitGcArgTrackCnt, Is.Zero);

            var record = RegPtrList(ref emitter.GCInfo)
                ?? throw new AssertionException("Missing first argument push.");
            for (var i = 0; i < 3; i++)
            {
                Assert.That(record.rpdArgType, Is.EqualTo(GCInfo.rpdArgType_t.rpdARG_PUSH));
                Assert.That(record.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)i));
                record = record.rpdNext ?? throw new AssertionException("Missing argument transition.");
            }

            Assert.That(record.rpdArgType, Is.EqualTo(GCInfo.rpdArgType_t.rpdARG_KILL));
            Assert.That(record.rpdOffs, Is.EqualTo(4u));
            record = record.rpdNext ?? throw new AssertionException("Missing call transition.");
            Assert.That(record.rpdArgType, Is.EqualTo(GCInfo.rpdArgType_t.rpdARG_POP));
            Assert.That(record.rpdCall, Is.True);
            Assert.That(record.rpdCallInstrSize, Is.EqualTo((byte)4));
            record = record.rpdNext ?? throw new AssertionException("Missing final pop.");
            Assert.That(record.rpdArgType, Is.EqualTo(GCInfo.rpdArgType_t.rpdARG_POP));
            Assert.That(record.rpdCall, Is.True);
            Assert.That(record.rpdCallInstrSize, Is.Zero);
            Assert.That(record.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)3));
            Assert.That(record.rpdNext, Is.Null);
        });
    }

    [Test]
    public static void RegisterMaskDeathsRecordGcrefsBeforeByrefs()
    {
        WithEmitter((emitter, buffer) =>
        {
            emitter.emitFullGCinfo = true;
            SyncThisRegister(emitter) = REG_NA;
            GCrefRegisters(emitter) = SRBM_R1;
            ByrefRegisters(emitter) = SRBM_R2;

            KillRegisters(emitter, new regMaskTP(SRBM_R1 | SRBM_R2), buffer + 9);

            Assert.That(GCrefRegisters(emitter), Is.EqualTo(SRBM_NONE));
            Assert.That(ByrefRegisters(emitter), Is.EqualTo(SRBM_NONE));
            var first = RegPtrList(ref emitter.GCInfo)
                ?? throw new AssertionException("Missing GC-ref death.");
            var second = first.rpdNext ?? throw new AssertionException("Missing byref death.");
            Assert.That(first.rpdOffs, Is.EqualTo(9u));
            Assert.That(first.rpdGCtype, Is.EqualTo(GCT_GCREF));
            Assert.That(first.rpdCompiler.rpdDel, Is.EqualTo(SRBM_R1));
            Assert.That(second.rpdOffs, Is.EqualTo(9u));
            Assert.That(second.rpdGCtype, Is.EqualTo(GCT_BYREF));
            Assert.That(second.rpdCompiler.rpdDel, Is.EqualTo(SRBM_R2));
            Assert.That(second.rpdNext, Is.Null);
        });
    }

    [Test]
    public static void PartialCallRecordsCodeOffsetAndRegisterMasks()
    {
        WithEmitter((emitter, buffer) =>
        {
            emitter.emitSimpleStkUsed = true;
            GCrefRegisters(emitter) = SRBM_R1;
            ByrefRegisters(emitter) = SRBM_R2;

            emitter.emitRecordGCcall(buffer + 12, 4);

            var record = typeof(GCInfo).GetField("gcCallDescList", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(emitter.GCInfo) ?? throw new AssertionException("Missing partial call descriptor.");
            Assert.That(ReadCallField<uint>(record, "cdOffs"), Is.EqualTo(12u));
            Assert.That(ReadCallField<ushort>(record, "cdCallInstrSize"), Is.EqualTo((ushort)4));
            Assert.That(ReadCallField<regMask>(record, "cdGCrefRegs"), Is.EqualTo(SRBM_R1));
            Assert.That(ReadCallField<regMask>(record, "cdByrefRegs"), Is.EqualTo(SRBM_R2));
            Assert.That(ReadCallField<ushort>(record, "cdArgCnt"), Is.Zero);
        });
    }

    [Test]
    public static void StackLifetimeRecordsUnsignedOffsetAndExclusiveEnd()
    {
        WithEmitter((emitter, buffer) =>
        {
            FrameMinimum(emitter) = -16;
            FrameCount(emitter) = 1;
            LiveTable(emitter) = new GCInfo.varPtrDsc?[1];

            LiveSet(emitter, -16, GCT_BYREF, buffer + 4, -1);
            var record = VarPtrList(ref emitter.GCInfo)
                ?? throw new AssertionException("Missing live stack pointer.");
            Assert.That(record.vpdBegOfs, Is.EqualTo(4u));
            Assert.That(record.vpdVarNum, Is.EqualTo(unchecked((uint)-16) | 1u));
            Assert.That(LiveTable(emitter)[0], Is.SameAs(record));

            DeadSet(emitter, -16, buffer + 12, -1);
            Assert.That(record.vpdEndOfs, Is.EqualTo(12u));
            Assert.That(LiveTable(emitter)[0], Is.Null);
        });
    }

    [Test]
    public static void RegisterKindChangeRecordsDeathBeforeBirth()
    {
        WithEmitter((emitter, buffer) =>
        {
            emitter.emitFullGCinfo = true;
            SyncThisRegister(emitter) = REG_NA;
            ByrefRegisters(emitter) = SRBM_R1;

            UpdateRegisters(emitter, GCT_GCREF, new regMaskTP(SRBM_R1), buffer + 8);

            Assert.That(GCrefRegisters(emitter), Is.EqualTo(SRBM_R1));
            Assert.That(ByrefRegisters(emitter), Is.EqualTo(SRBM_NONE));
            var first = RegPtrList(ref emitter.GCInfo)
                ?? throw new AssertionException("Missing register death.");
            var second = first.rpdNext ?? throw new AssertionException("Missing register birth.");
            Assert.That(first.rpdOffs, Is.EqualTo(8u));
            Assert.That(first.rpdGCtype, Is.EqualTo(GCT_BYREF));
            Assert.That(first.rpdCompiler.rpdDel, Is.EqualTo(SRBM_R1));
            Assert.That(second.rpdOffs, Is.EqualTo(8u));
            Assert.That(second.rpdGCtype, Is.EqualTo(GCT_GCREF));
            Assert.That(second.rpdCompiler.rpdAdd, Is.EqualTo(SRBM_R1));
            Assert.That(second.rpdNext, Is.Null);
        });
    }

    private delegate void EmitterTest(TestEmitter emitter, byte* buffer);

    private static void WithEmitter(EmitterTest test)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        var codeGen = new CodeGen(compiler) { IsFullPtrRegMapRequired = true };
        compiler.codeGen = codeGen;
        var emitter = new TestEmitter(codeGen);
        emitter.emitBegCG(compiler, default);
        emitter.Init();
        emitter.emitBegFN(false
#if DEBUG
            , true
#endif
            );
        var buffer = stackalloc byte[64];
        emitter.emitCodeBlock = buffer;
        emitter.emitTotalHotCodeSize = 64;
#if DEBUG
        emitter.emitIssuing = true;
#endif
        test(emitter, buffer);
    }

    private static T ReadCallField<T>(object descriptor, string name)
    {
        var type = typeof(GCInfo).GetNestedType("CallDsc", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing GC call descriptor type.");
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new AssertionException($"Missing call descriptor field {name}.");
        return field.GetValue(descriptor) is T value
            ? value
            : throw new AssertionException($"Invalid call descriptor field {name}.");
    }

    private sealed class TestEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGCvarLiveSet")]
    private static extern void LiveSet(Emitter emitter, int offset, GCInfo.GCtype type, byte* address, nint displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGCvarDeadSet")]
    private static extern void DeadSet(Emitter emitter, int offset, byte* address, nint displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitUpdateLiveGCregs")]
    private static extern void UpdateRegisters(Emitter emitter, GCInfo.GCtype type, regMaskTP registers, byte* address);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGCregDeadUpdMask")]
    private static extern void KillRegisters(Emitter emitter, regMaskTP registers, byte* address);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcVarPtrList")]
    private static extern ref GCInfo.varPtrDsc? VarPtrList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegPtrList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsMin")]
    private static extern ref int FrameMinimum(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsCnt")]
    private static extern ref int FrameCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameLiveTab")]
    private static extern ref GCInfo.varPtrDsc?[] LiveTable(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask GCrefRegisters(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ByrefRegisters(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitSyncThisObjReg")]
    private static extern ref regNumber SyncThisRegister(Emitter emitter);
}
#endif
