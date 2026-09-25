// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterStackGCOutputTests
{
    [TestCase(GCT_GCREF, 0u)]
    [TestCase(GCT_BYREF, 1u)]
    public static void StackSlotLifetimesAreLinkedAndRetainOffsetFlags(GCInfo.GCtype type, uint flag)
    {
        WithEmitter((_, codeGen, buffer) =>
        {
            var emitter = codeGen.Emitter;
            Live(emitter, -16, int.MaxValue, type, buffer + 3);
            var first = VarPtrList(ref codeGen.GCInfo) ?? throw new AssertionException("Missing first stack lifetime.");
            Assert.That(first.vpdBegOfs, Is.EqualTo(3));
            Assert.That(first.vpdVarNum, Is.EqualTo(unchecked((uint)-16) | flag));
            Assert.That(LiveTable(emitter)[0], Is.SameAs(first));
            Assert.That(CurrentVariableSet(emitter), Is.False);

            Live(emitter, -16, int.MaxValue, type, buffer + 5);
            Assert.That(first.vpdNext, Is.Null);
            Dead(emitter, -16, buffer + 9);
            Assert.That(first.vpdEndOfs, Is.EqualTo(9));
            Assert.That(LiveTable(emitter)[0], Is.Null);
            Dead(emitter, -16, buffer + 10);
            Assert.That(first.vpdEndOfs, Is.EqualTo(9));

            Live(emitter, -8, int.MaxValue, type, buffer + 15);
            var second = first.vpdNext ?? throw new AssertionException("Missing second stack lifetime.");
            Assert.That(second.vpdBegOfs, Is.EqualTo(15));
            Assert.That(second.vpdVarNum, Is.EqualTo(unchecked((uint)-8) | flag));
            Assert.That(VarPtrLast(ref codeGen.GCInfo), Is.SameAs(second));
            Assert.That(LiveTable(emitter)[1], Is.SameAs(second));
            Dead(emitter, -8, buffer + 20);
            Assert.That(second.vpdEndOfs, Is.EqualTo(20));
        });
    }

    [TestCase(GCT_GCREF, true)]
    [TestCase(GCT_BYREF, true)]
    [TestCase(GCT_GCREF, false)]
    public static void OutgoingPointerArgumentsRecordOnlyFullGcMaps(GCInfo.GCtype type, bool fullMap)
    {
        WithEmitter((compiler, codeGen, buffer) =>
        {
            var emitter = codeGen.Emitter;
            compiler.lvaOutgoingArgSpaceVar = 7;
            emitter.emitFullGCinfo = fullMap;
            Live(emitter, 8, 7, type, buffer + 6);
            var record = RegPtrList(ref codeGen.GCInfo);
            Assert.That(VarPtrList(ref codeGen.GCInfo), Is.Null);
            if (!fullMap)
            {
                Assert.That(record, Is.Null);
                return;
            }

            record = record ?? throw new AssertionException("Missing outgoing argument record.");
            Assert.That(record.rpdOffs, Is.EqualTo(6));
            Assert.That(record.rpdGCtype, Is.EqualTo(type));
            Assert.That(record.rpdArg, Is.True);
            Assert.That(record.rpdCall, Is.False);
            Assert.That(record.rpdIsThis, Is.False);
            Assert.That(record.rpdCallData.rpdPtrArg, Is.EqualTo(8));
            Assert.That(record.rpdArgType, Is.EqualTo(GCInfo.rpdArgType_t.rpdARG_PUSH));
        });
    }

    [Test]
    public static void UntrackedAndOutsideFrameSlotsDoNotCreateLifetimes()
    {
        WithEmitter((compiler, codeGen, buffer) =>
        {
            var emitter = codeGen.Emitter;
            compiler.lvaTable[0].lvTracked = false;
            Live(emitter, -16, 0, GCT_GCREF, buffer + 1);
            Live(emitter, -24, int.MaxValue, GCT_GCREF, buffer + 2);
            Live(emitter, 0, int.MaxValue, GCT_GCREF, buffer + 3);
            Dead(emitter, -24, buffer + 4);
            Dead(emitter, 0, buffer + 5);

            Assert.That(VarPtrList(ref codeGen.GCInfo), Is.Null);
            Assert.That(RegPtrList(ref codeGen.GCInfo), Is.Null);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    public static void VariableSetUpdatesPreserveOffsetKindsAndInvalidateCachedLiveness(int flag)
    {
        WithEmitter((compiler, codeGen, buffer) =>
        {
            var emitter = codeGen.Emitter;
            var offsets = stackalloc int[1];
            offsets[0] = -16 | flag;
            TrackedOffsets(emitter) = offsets;
            TrackedCount(emitter) = 1;
            CurrentVariableSet(emitter) = false;
            var live = VarSetOps.MakeSingleton(compiler, 0);

            UpdateVariables(emitter, live, buffer + 2);
            var first = VarPtrList(ref codeGen.GCInfo) ?? throw new AssertionException("Missing tracked lifetime.");
            Assert.That(first.vpdBegOfs, Is.EqualTo(2));
            Assert.That(first.vpdVarNum, Is.EqualTo(unchecked((uint)(-16 | flag))));
            Assert.That(CurrentVariableSet(emitter), Is.True);
            UpdateVariables(emitter, live, buffer + 3);
            Assert.That(first.vpdNext, Is.Null);

            Dead(emitter, -16, buffer + 5);
            Assert.That(CurrentVariableSet(emitter), Is.False);
            UpdateVariables(emitter, live, buffer + 9);
            var second = first.vpdNext ?? throw new AssertionException("Missing restored lifetime.");
            Assert.That(first.vpdEndOfs, Is.EqualTo(5));
            Assert.That(second.vpdBegOfs, Is.EqualTo(9));
            Assert.That(second.vpdVarNum, Is.EqualTo(first.vpdVarNum));

            UpdateVariables(emitter, VarSetOps.MakeEmpty(compiler), buffer + 16);
            Assert.That(second.vpdEndOfs, Is.EqualTo(16));
            Assert.That(LiveTable(emitter)[0], Is.Null);
            Assert.That(CurrentVariableSet(emitter), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RegisterSetUpdatesOrderDeathsAndKindChangesByRegister(bool fullMap)
    {
        WithEmitter((_, codeGen, buffer) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitFullGCinfo = fullMap;
            SyncThisRegister(emitter) = REG_NA;
            ReferenceRegisters(emitter) = SRBM_RAX | SRBM_RDX;
            ByrefRegisters(emitter) = SRBM_RCX;

            UpdateRegisters(emitter, GCT_GCREF, new regMaskTP(SRBM_RCX | SRBM_R8), buffer + 9);

            Assert.That(ReferenceRegisters(emitter), Is.EqualTo(SRBM_RCX | SRBM_R8));
            Assert.That(ByrefRegisters(emitter), Is.EqualTo(SRBM_NONE));
            var record = RegPtrList(ref codeGen.GCInfo);
            if (!fullMap)
            {
                Assert.That(record, Is.Null);
                return;
            }

            (regMask add, regMask del, GCInfo.GCtype kind)[] expected =
            [
                (SRBM_NONE, SRBM_RAX, GCT_GCREF),
                (SRBM_NONE, SRBM_RCX, GCT_BYREF),
                (SRBM_RCX, SRBM_NONE, GCT_GCREF),
                (SRBM_NONE, SRBM_RDX, GCT_GCREF),
                (SRBM_R8, SRBM_NONE, GCT_GCREF),
            ];
            foreach (var (add, del, kind) in expected)
            {
                record = record ?? throw new AssertionException("Missing register transition.");
                Assert.That(record.rpdOffs, Is.EqualTo(9));
                Assert.That(record.rpdGCtype, Is.EqualTo(kind));
                Assert.That(record.rpdCompiler.rpdAdd, Is.EqualTo(add));
                Assert.That(record.rpdCompiler.rpdDel, Is.EqualTo(del));
                record = record.rpdNext;
            }
            Assert.That(record, Is.Null);
        });
    }

    private delegate void TestBody(Compiler compiler, CodeGen codeGen, byte* buffer);

    private static void WithEmitter(TestBody body)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, _) =>
        {
            codeGen.IsFullPtrRegMapRequired = true;
            var emitter = codeGen.Emitter;
            var buffer = stackalloc byte[32];
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
            FrameMinimum(emitter) = -16;
            FrameMaximum(emitter) = 0;
            FrameCount(emitter) = 2;
            LiveTable(emitter) = new GCInfo.varPtrDsc?[2];
            CurrentVariableSet(emitter) = true;
            compiler.lvaOutgoingArgSpaceVar = -1;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            body(compiler, codeGen, buffer);
        });
    }

    private static void Live(Emitter emitter, int offset, int variable, GCInfo.GCtype type, byte* address)
    {
        LiveUpdate(emitter, offset, variable, type, address
#if DEBUG
            , uint.MaxValue
#endif
            );
    }

    private static void Dead(Emitter emitter, int offset, byte* address)
    {
        DeadUpdate(emitter, offset, address
#if DEBUG
            , uint.MaxValue
#endif
            );
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGCvarLiveUpd")]
    private static extern void LiveUpdate(Emitter emitter, int offset, int variable, GCInfo.GCtype type, byte* address
#if DEBUG
        , uint actualVariable
#endif
        );

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGCvarDeadUpd")]
    private static extern void DeadUpdate(Emitter emitter, int offset, byte* address
#if DEBUG
        , uint variable
#endif
        );

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsMin")]
    private static extern ref int FrameMinimum(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsMax")]
    private static extern ref int FrameMaximum(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsCnt")]
    private static extern ref int FrameCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameLiveTab")]
    private static extern ref GCInfo.varPtrDsc?[] LiveTable(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefVset")]
    private static extern ref bool CurrentVariableSet(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcVarPtrList")]
    private static extern ref GCInfo.varPtrDsc? VarPtrList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcVarPtrLast")]
    private static extern ref GCInfo.varPtrDsc? VarPtrLast(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegPtrList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitUpdateLiveGCvars")]
    private static extern void UpdateVariables(Emitter emitter, nint[] variables, byte* address);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitUpdateLiveGCregs")]
    private static extern void UpdateRegisters(Emitter emitter, GCInfo.GCtype type, regMaskTP registers, byte* address);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsTab")]
    private static extern ref int* TrackedOffsets(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitTrkVarCnt")]
    private static extern ref int TrackedCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask ReferenceRegisters(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ByrefRegisters(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitSyncThisObjReg")]
    private static extern ref regNumber SyncThisRegister(Emitter emitter);
}
