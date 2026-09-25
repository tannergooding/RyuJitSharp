// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.rpdArgType_t;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterCallGCOutputTests
{
    private delegate void EmitterTest(Compiler compiler, Emitter emitter, byte* buffer);

    [Test]
    public static void PartialMapUsesCompactMasksAndRecordsEvenEmptyCalls()
    {
        WithEmitter((_, emitter, buffer) =>
        {
            emitter.emitSimpleStkUsed = true;
            GCrefRegs(emitter) = SRBM_RCX;
            ByrefRegs(emitter) = SRBM_RDX;

            emitter.emitStackPush(buffer + 1, GCT_GCREF);
            emitter.emitStackPush(buffer + 2, GCT_BYREF);
            emitter.emitStackPushN(buffer + 3, 2);
            emitter.emitStackPop(buffer + 4, false, 0);
            emitter.emitStackKillArgs(buffer + 5, 2, 5);

            Assert.That(emitter.emitCurStackLvl, Is.EqualTo(12));
            Assert.That(unchecked((uint)emitter.u1.emitSimpleStkMask), Is.EqualTo(4u));
            Assert.That(unchecked((uint)emitter.u1.emitSimpleByrefStkMask), Is.Zero);
            emitter.emitRecordGCcall(buffer + 12, 5);

            var first = CallList(ref emitter.GCInfo);
            Assert.That(CallField<nint>(first, "cdBlock"), Is.EqualTo((nint)0));
            Assert.That(CallField<uint>(first, "cdOffs"), Is.EqualTo(12u));
            Assert.That(CallField<ushort>(first, "cdCallInstrSize"), Is.EqualTo((ushort)5));
            Assert.That(CallField<regMask>(first, "cdGCrefRegs"), Is.EqualTo(SRBM_RCX));
            Assert.That(CallField<regMask>(first, "cdByrefRegs"), Is.EqualTo(SRBM_RDX));
            Assert.That(CallField<ushort>(first, "cdArgCnt"), Is.EqualTo((ushort)0));
            Assert.That(CallField<uint>(first, "cdArgMask"), Is.EqualTo(4u));
            Assert.That(CallField<uint>(first, "cdByrefArgMask"), Is.EqualTo(0u));
            Assert.That(CallOptionalField(first, "cdArgTable"), Is.Null);

            emitter.emitStackPop(buffer + 14, false, 0, 3);
            emitter.emitRecordGCcall(buffer + 16, 5);
            var second = CallOptionalField(first, "cdNext")
                ?? throw new AssertionException("Missing empty call-site descriptor.");
            Assert.That(CallField<uint>(second, "cdOffs"), Is.EqualTo(16u));
            Assert.That(CallField<uint>(second, "cdArgMask"), Is.EqualTo(0u));
            Assert.That(CallOptionalField(second, "cdNext"), Is.Null);
        });
    }

    [TestCase(GCT_GCREF)]
    [TestCase(GCT_BYREF)]
    public static void CompactMaskRetainsTheHighestPendingArgumentBit(GCInfo.GCtype type)
    {
        WithEmitter((_, emitter, buffer) =>
        {
            emitter.emitSimpleStkUsed = true;
            emitter.emitStackPush(buffer + 1, type);
            emitter.emitStackPushN(buffer + 2, 31);
            emitter.emitRecordGCcall(buffer + 8, 5);

            var call = CallList(ref emitter.GCInfo);
            Assert.That(CallField<uint>(call, "cdArgMask"), Is.EqualTo(1u << 31));
            Assert.That(CallField<uint>(call, "cdByrefArgMask"), Is.EqualTo(type == GCT_BYREF ? 1u << 31 : 0u));
            Assert.That(emitter.emitCurStackLvl, Is.EqualTo(128));

            emitter.emitStackPop(buffer + 9, false, 0);
            Assert.That(unchecked((uint)emitter.u1.emitSimpleStkMask), Is.EqualTo(1u << 30));
            Assert.That(unchecked((uint)emitter.u1.emitSimpleByrefStkMask), Is.EqualTo(type == GCT_BYREF ? 1u << 30 : 0u));
            emitter.emitStackKillArgs(buffer + 10, 31, 5);
            Assert.That(unchecked((uint)emitter.u1.emitSimpleStkMask), Is.EqualTo(0u));
        });
    }

    [Test]
    public static void LargePartialMapReportsPendingArgumentsFromStackTop()
    {
        WithEmitter((_, emitter, buffer) =>
        {
            var tracking = stackalloc byte[64];
            emitter.emitSimpleStkUsed = false;
            emitter.emitFullArgInfo = false;
            emitter.emitMaxStackDepth = 64;
            emitter.u2.emitArgTrackTab = tracking;
            emitter.u2.emitArgTrackTop = tracking;
            emitter.u2.emitGcArgTrackCnt = 0;

            emitter.emitStackPushN(buffer + 1, 33);
            for (var i = 0; i < 20; i++)
            {
                emitter.emitStackPush(buffer + 2, (i & 1) == 0 ? GCT_GCREF : GCT_BYREF);
            }
            Assert.That(emitter.emitCurStackLvl, Is.EqualTo(212));
            Assert.That(emitter.u2.emitGcArgTrackCnt, Is.EqualTo((ushort)20));

            emitter.emitRecordGCcall(buffer + 14, 5);
            var call = CallList(ref emitter.GCInfo);
            Assert.That(CallField<uint>(call, "cdOffs"), Is.EqualTo(14u));
            Assert.That(CallField<ushort>(call, "cdArgCnt"), Is.EqualTo((ushort)20));
            var expected = new uint[20];
            for (var i = 0; i < expected.Length; i++)
            {
                expected[i] = (uint)(i * 8) | ((i & 1) == 0 ? 1u : 0u);
            }
            Assert.That(CallField<uint[]>(call, "cdArgTable"), Is.EqualTo(expected));

            emitter.emitStackKillArgs(buffer + 20, 20, 5);
            Assert.That(emitter.emitCurStackLvl, Is.EqualTo(212));
            Assert.That(emitter.u2.emitGcArgTrackCnt, Is.EqualTo((ushort)0));
            Assert.That((nuint)emitter.u2.emitArgTrackTop, Is.EqualTo((nuint)(tracking + 53)));
            Assert.That(tracking[33], Is.EqualTo((byte)GCT_NONE));
            Assert.That(tracking[52], Is.EqualTo((byte)GCT_NONE));
            Assert.That(RegPtrList(ref emitter.GCInfo), Is.Null);

            emitter.emitRecordGCcall(buffer + 24, 5);
            var empty = CallOptionalField(call, "cdNext")
                ?? throw new AssertionException("Missing large-stack call without live arguments.");
            Assert.That(CallField<ushort>(empty, "cdArgCnt"), Is.EqualTo((ushort)0));
            Assert.That(CallOptionalField(empty, "cdArgTable"), Is.Null);
        });
    }

    [Test]
    public static void FullMapPushKillAndPopPreserveRecordOrderingAndPendingCount()
    {
        WithEmitter((_, emitter, buffer) =>
        {
            var tracking = stackalloc byte[16];
            emitter.emitSimpleStkUsed = false;
            emitter.emitFullGCinfo = true;
            emitter.emitFullArgInfo = true;
            emitter.emitMaxStackDepth = 16;
            emitter.u2.emitArgTrackTab = tracking;
            emitter.u2.emitArgTrackTop = tracking;
            emitter.u2.emitGcArgTrackCnt = 0;
            GCrefRegs(emitter) = SRBM_RCX;
            ByrefRegs(emitter) = SRBM_RDX;

            emitter.emitStackPush(buffer + 1, GCT_NONE);
            emitter.emitStackPush(buffer + 2, GCT_GCREF);
            emitter.emitStackPush(buffer + 3, GCT_BYREF);
            emitter.emitStackKillArgs(buffer + 6, 2, 5);
            Assert.That(emitter.emitCurStackLvl, Is.EqualTo(12));
            Assert.That(emitter.u2.emitGcArgTrackCnt, Is.EqualTo((ushort)3));
            Assert.That(tracking[1], Is.EqualTo((byte)GCT_NONE));
            Assert.That(tracking[2], Is.EqualTo((byte)GCT_NONE));
            emitter.emitStackPop(buffer + 9, false, 0, 3);

            Assert.That(emitter.emitCurStackLvl, Is.Zero);
            Assert.That(emitter.u2.emitGcArgTrackCnt, Is.EqualTo((ushort)0));
            var record = RegPtrList(ref emitter.GCInfo)
                ?? throw new AssertionException("Missing argument push records.");
            Assert.That(record.rpdGCtypeGet(), Is.EqualTo(GCT_NONE));
            Assert.That(record.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)0));
            record = record.rpdNext ?? throw new AssertionException("Missing GC-ref push.");
            Assert.That(record.rpdGCtypeGet(), Is.EqualTo(GCT_GCREF));
            Assert.That(record.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)1));
            record = record.rpdNext ?? throw new AssertionException("Missing byref push.");
            Assert.That(record.rpdGCtypeGet(), Is.EqualTo(GCT_BYREF));
            Assert.That(record.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)2));
            record = record.rpdNext ?? throw new AssertionException("Missing argument kill.");
            Assert.That(record.rpdArgTypeGet(), Is.EqualTo(rpdARG_KILL));
            Assert.That(record.rpdOffs, Is.EqualTo(6u));
            Assert.That(record.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)2));
            record = record.rpdNext ?? throw new AssertionException("Missing call record.");
            Assert.That(record.rpdArgTypeGet(), Is.EqualTo(rpdARG_POP));
            Assert.That(record.rpdCall, Is.True);
            Assert.That(record.rpdCallInstrSize, Is.EqualTo((byte)5));
            Assert.That(record.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)0));
            Assert.That(record.rpdCallData.rpdCallGCrefRegs, Is.EqualTo((uint)SRBM_RCX));
            Assert.That(record.rpdCallData.rpdCallByrefRegs, Is.EqualTo((uint)SRBM_RDX));
            record = record.rpdNext ?? throw new AssertionException("Missing final argument pop.");
            Assert.That(record.rpdArgTypeGet(), Is.EqualTo(rpdARG_POP));
            Assert.That(record.rpdCall, Is.True);
            Assert.That(record.rpdCallInstrSize, Is.EqualTo((byte)0));
            Assert.That(record.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)3));
            Assert.That(record.rpdNext, Is.Null);
        }, fullPtrMap: true);
    }

    [Test]
    public static void FullMapWithFramePointerTracksOnlyPointerArguments()
    {
        WithEmitter((_, emitter, buffer) =>
        {
            var tracking = stackalloc byte[16];
            emitter.emitSimpleStkUsed = false;
            emitter.emitFullGCinfo = true;
            emitter.emitFullArgInfo = false;
            emitter.emitHasFramePtr = true;
            emitter.emitMaxStackDepth = 16;
            emitter.u2.emitArgTrackTab = tracking;
            emitter.u2.emitArgTrackTop = tracking;

            emitter.emitStackPush(buffer + 1, GCT_NONE);
            emitter.emitStackPush(buffer + 2, GCT_GCREF);
            emitter.emitStackPush(buffer + 3, GCT_BYREF);
            Assert.That(emitter.u2.emitGcArgTrackCnt, Is.EqualTo((ushort)2));
            emitter.emitStackKillArgs(buffer + 6, 2, 5);

            Assert.That(emitter.u2.emitGcArgTrackCnt, Is.EqualTo((ushort)0));
            var first = RegPtrList(ref emitter.GCInfo)
                ?? throw new AssertionException("Missing pointer-argument push.");
            Assert.That(first.rpdGCtypeGet(), Is.EqualTo(GCT_GCREF));
            Assert.That(first.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)1));
            var second = first.rpdNext ?? throw new AssertionException("Missing byref-argument push.");
            Assert.That(second.rpdGCtypeGet(), Is.EqualTo(GCT_BYREF));
            Assert.That(second.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)2));
            var kill = second.rpdNext ?? throw new AssertionException("Missing pointer-argument kill.");
            Assert.That(kill.rpdArgTypeGet(), Is.EqualTo(rpdARG_KILL));
            Assert.That(kill.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)2));
            var call = kill.rpdNext ?? throw new AssertionException("Missing call record after argument kill.");
            Assert.That(call.rpdArgTypeGet(), Is.EqualTo(rpdARG_POP));
            Assert.That(call.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)0));
            Assert.That(call.rpdNext, Is.Null);
        }, fullPtrMap: true);
    }

    [Test]
    public static void FullMapRecordsZeroArgumentCalls()
    {
        WithEmitter((_, emitter, buffer) =>
        {
            var tracking = stackalloc byte[16];
            emitter.emitSimpleStkUsed = false;
            emitter.emitFullGCinfo = true;
            emitter.emitMaxStackDepth = 16;
            emitter.u2.emitArgTrackTab = tracking;
            emitter.u2.emitArgTrackTop = tracking;
            emitter.emitStackPop(buffer + 7, true, 5, 0);

            var call = RegPtrList(ref emitter.GCInfo)
                ?? throw new AssertionException("Missing zero-argument call record.");
            Assert.That(call.rpdOffs, Is.EqualTo(7u));
            Assert.That(call.rpdCall, Is.True);
            Assert.That(call.rpdCallInstrSize, Is.EqualTo((byte)5));
            Assert.That(call.rpdCallData.rpdPtrArg, Is.EqualTo((ushort)0));
        }, fullPtrMap: true);
    }

    private static void WithEmitter(EmitterTest test, bool fullPtrMap = false)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            codeGen.IsFullPtrRegMapRequired = fullPtrMap;
            var emitter = codeGen.Emitter;
            var buffer = stackalloc byte[64];
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 64;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            test(compiler, emitter, buffer);
        });
    }

    private static object CallList(ref GCInfo info)
    {
        var field = typeof(GCInfo).GetField("gcCallDescList", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing GC call-list field.");
        return field.GetValue(info) ?? throw new AssertionException("Missing GC call descriptor.");
    }

    private static T CallField<T>(object descriptor, string name)
    {
        var field = CallDescriptorField(name);
        return field.GetValue(descriptor) is T value
            ? value
            : throw new AssertionException($"Invalid GC call descriptor field {name}.");
    }

    private static object? CallOptionalField(object descriptor, string name)
    {
        return CallDescriptorField(name).GetValue(descriptor);
    }

    private static FieldInfo CallDescriptorField(string name)
    {
        var type = typeof(GCInfo).GetNestedType("CallDsc", BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing GC call descriptor type.");
        return type.GetField(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new AssertionException($"Missing GC call descriptor field {name}.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegPtrList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask GCrefRegs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ByrefRegs(Emitter emitter);
}
