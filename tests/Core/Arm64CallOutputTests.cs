// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class Arm64CallOutputTests
{
    private delegate void OutputTest(Compiler compiler, Emitter emitter, byte* buffer);

    [TestCase(false, EA_PTRSIZE)]
    [TestCase(false, EA_GCREF)]
    [TestCase(false, EA_BYREF)]
    [TestCase(true, EA_PTRSIZE)]
    [TestCase(true, EA_GCREF)]
    [TestCase(true, EA_BYREF)]
    public static void NoGcCallsStillWriteOneInstructionAndUpdateReturnMasks(bool large, emitAttr returnSize)
    {
        WithEmitter((compiler, emitter, buffer) =>
        {
            var refs = new regMaskTP(SRBM_R19 | SRBM_R28);
            var byrefs = large ? new regMaskTP(SRBM_R20) : RBM_NONE;
            var id = Call(emitter, VarSetOps.MakeEmpty(compiler), refs, byrefs, returnSize);
            id.idSetIsNoGC(true);
            GCrefRegs(emitter) = SRBM_R21;
            ByrefRegs(emitter) = SRBM_R22;
            ThisVariables(emitter) = VarSetOps.MakeSingleton(compiler, 0);
            VariablesSet(emitter) = true;

            var size = Output(emitter, new insGroup(), buffer + 8, id, 0x94000000u);

            Assert.That(size, Is.EqualTo(4u));
            Assert.That(Unsafe.ReadUnaligned<uint>(buffer + 8), Is.EqualTo(0x94000000u));
            Assert.That(buffer[7], Is.EqualTo(0xA5));
            Assert.That(buffer[12], Is.EqualTo(0xA5));
            Assert.That(id.idIsLargeCall(), Is.EqualTo(large));
            Assert.That(VarSetOps.IsEmpty(compiler, ThisVariables(emitter)), Is.True);
            Assert.That(GCrefRegs(emitter),
                Is.EqualTo(SRBM_R19 | SRBM_R28 | (returnSize == EA_GCREF ? SRBM_R0 : SRBM_NONE)));
            Assert.That(ByrefRegs(emitter),
                Is.EqualTo((large ? SRBM_R20 : SRBM_NONE) | (returnSize == EA_BYREF ? SRBM_R0 : SRBM_NONE)));
            Assert.That(CallList(ref emitter.GCInfo), Is.Null);
        });
    }

    [TestCase(-4, 0x94000000u)]
    [TestCase(0, 0xD63F0040u)]
    [TestCase(8, 0xD63F0060u)]
    public static void EncodedDirectAndIndirectWordsUseWritableAliasWithoutMovingTheCallerCursor(int alias, uint code)
    {
        WithEmitter((compiler, emitter, buffer) =>
        {
            emitter.writeableOffset = alias;
            var id = Call(emitter, VarSetOps.MakeEmpty(compiler), RBM_NONE, RBM_NONE, EA_PTRSIZE);
            id.idSetIsNoGC(true);
            var cursor = buffer + 12;

            var size = Output(emitter, new insGroup(), cursor, id, code);

            Assert.That(size, Is.EqualTo(4u));
            Assert.That((nuint)cursor, Is.EqualTo((nuint)(buffer + 12)));
            var expectedOffset = 12 + alias;
            Assert.That(Unsafe.ReadUnaligned<uint>(buffer + expectedOffset), Is.EqualTo(code));
            for (var index = 0; index < 64; index++)
            {
                if ((index < 12 + alias) || (index >= 16 + alias))
                {
                    Assert.That(buffer[index], Is.EqualTo(0xA5));
                }
            }
        });
    }

    [TestCase(EA_GCREF, EA_BYREF, false)]
    [TestCase(EA_BYREF, EA_GCREF, false)]
    [TestCase(EA_PTRSIZE, EA_GCREF, true)]
    public static void FatCallsPreserveSecondAndAsyncReturnMasks(
        emitAttr firstReturn, emitAttr secondReturn, bool asyncReturn)
    {
        WithEmitter((compiler, emitter, buffer) =>
        {
            var vars = VarSetOps.MakeSingleton(compiler, 0);
            var id = Call(emitter, vars, new regMaskTP(SRBM_R19), new regMaskTP(SRBM_R20),
                firstReturn, secondReturn, asyncReturn);
            id.idSetIsNoGC(true);

            var size = Output(emitter, new insGroup(), buffer + 8, id, 0xD63F0040u);

            var expectedRefs = SRBM_R19 |
                (firstReturn == EA_GCREF ? SRBM_R0 : SRBM_NONE) |
                (secondReturn == EA_GCREF ? SRBM_R1 : SRBM_NONE) |
                (asyncReturn ? REG_ASYNC_CONTINUATION_RET.SingleTypeMask : SRBM_NONE);
            var expectedByrefs = SRBM_R20 |
                (firstReturn == EA_BYREF ? SRBM_R0 : SRBM_NONE) |
                (secondReturn == EA_BYREF ? SRBM_R1 : SRBM_NONE);
            Assert.That(size, Is.EqualTo(4u));
            Assert.That(id.idIsLargeCall(), Is.True);
            Assert.That(GCrefRegs(emitter), Is.EqualTo(expectedRefs));
            Assert.That(ByrefRegs(emitter), Is.EqualTo(expectedByrefs));
            Assert.That(VarSetOps.Equal(compiler, ThisVariables(emitter), vars), Is.True);
            Assert.That(CallList(ref emitter.GCInfo), Is.Null);
        });
    }

    [TestCase(EA_GCREF)]
    [TestCase(EA_BYREF)]
    public static void StackVariablesDieAtCallStartAndFullMapReturnsAreBornAtInstructionEnd(emitAttr returnSize)
    {
        WithEmitter((compiler, emitter, buffer) =>
        {
            emitter.emitFullGCinfo = true;
            var offsets = stackalloc int[1];
            offsets[0] = 0;
            FrameOffsets(emitter) = offsets;
            TrackedVariables(emitter) = 1;
            FrameMaximum(emitter) = 8;
            FrameCount(emitter) = 1;
            var live = new GCInfo.varPtrDsc
            {
                vpdBegOfs = 0,
                vpdEndOfs = 0xFACEDEAD,
                vpdVarNum = 0,
            };
            FrameLive(emitter) = [live];
            emitter.GCInfo.gcVarPtrList = live;
            emitter.GCInfo.gcVarPtrLast = live;
            ThisVariables(emitter) = VarSetOps.MakeSingleton(compiler, 0);
            VariablesSet(emitter) = true;
            var id = Call(emitter, VarSetOps.MakeEmpty(compiler), RBM_NONE, RBM_NONE, returnSize);
            id.idSetIsNoGC(true);

            var size = Output(emitter, new insGroup(), buffer + 8, id, 0x94000000u);

            Assert.That(size, Is.EqualTo(4u));
            Assert.That(live.vpdEndOfs, Is.EqualTo(8u));
            var frames = FrameLive(emitter) ?? throw new AssertionException("Missing tracked GC frame.");
            Assert.That(frames[0], Is.Null);
            var birth = RegPtrList(ref emitter.GCInfo) ?? throw new AssertionException("Missing return-register birth.");
            Assert.That(birth.rpdOffs, Is.EqualTo(12u));
            Assert.That(birth.rpdGCtypeGet(), Is.EqualTo(returnSize == EA_GCREF ? GCT_GCREF : GCT_BYREF));
            Assert.That(birth.rpdCompiler.rpdAdd, Is.EqualTo(SRBM_R0));
            Assert.That(birth.rpdCompiler.rpdDel, Is.EqualTo(SRBM_NONE));
            Assert.That(birth.rpdArg, Is.False);
            Assert.That(birth.rpdCall, Is.False);
            Assert.That(birth.rpdNext, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GcCallsKeepTheExistingStackTrackingBoundaryAfterBytesAndRegisterUpdates(bool large)
    {
        WithEmitter((compiler, emitter, buffer) =>
        {
            var id = Call(emitter, VarSetOps.MakeEmpty(compiler), new regMaskTP(SRBM_R19),
                large ? new regMaskTP(SRBM_R20) : RBM_NONE, EA_GCREF);
#if EMIT_TRACK_STACK_DEPTH
            var size = Output(emitter, new insGroup(), buffer + 8, id, 0x94000000u);
            Assert.That(size, Is.EqualTo(4u));
            var call = CallList(ref emitter.GCInfo) ?? throw new AssertionException("Missing partial-map call.");
            Assert.That(CallField<uint>(call, "cdOffs"), Is.EqualTo(12u));
            Assert.That(CallField<ushort>(call, "cdCallInstrSize"), Is.EqualTo((ushort)4));
            Assert.That(CallField<regMask>(call, "cdGCrefRegs"), Is.EqualTo(SRBM_R19 | SRBM_R0));
            Assert.That(CallField<regMask>(call, "cdByrefRegs"), Is.EqualTo(large ? SRBM_R20 : SRBM_NONE));
#else
            var address = (nint)(buffer + 8);
            var failure = Assert.Throws<FatalJitException>(() =>
                Output(emitter, new insGroup(), (byte*)address, id, 0x94000000u)) ??
                throw new AssertionException("Missing stack-tracking dependency failure.");
            Assert.That(failure.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("Stack-depth tracking is disabled for this target."));
            Assert.That(CallList(ref emitter.GCInfo), Is.Null);
#endif
            Assert.That(Unsafe.ReadUnaligned<uint>(buffer + 8), Is.EqualTo(0x94000000u));
            Assert.That(GCrefRegs(emitter), Is.EqualTo(SRBM_R19 | SRBM_R0));
            Assert.That(ByrefRegs(emitter), Is.EqualTo(large ? SRBM_R20 : SRBM_NONE));
        });
    }

    [Test]
    public static void FullMapGcCallsRecordTheCallAfterTheReturnRegisterBirth()
    {
        WithEmitter((compiler, emitter, buffer) =>
        {
            emitter.emitFullGCinfo = true;
            emitter.emitSimpleStkUsed = false;
#if EMIT_TRACK_STACK_DEPTH
            var tracking = stackalloc byte[1];
            emitter.emitMaxStackDepth = 1;
            emitter.u2.emitArgTrackTab = tracking;
            emitter.u2.emitArgTrackTop = tracking;
            emitter.u2.emitGcArgTrackCnt = 0;
#endif
            var id = Call(emitter, VarSetOps.MakeEmpty(compiler), RBM_NONE, RBM_NONE, EA_GCREF);
#if EMIT_TRACK_STACK_DEPTH
            var size = Output(emitter, new insGroup(), buffer + 8, id, 0x94000000u);
            Assert.That(size, Is.EqualTo(4u));
#else
            var address = (nint)(buffer + 8);
            var failure = Assert.Throws<FatalJitException>(() =>
                Output(emitter, new insGroup(), (byte*)address, id, 0x94000000u)) ??
                throw new AssertionException("Missing stack-tracking dependency failure.");
            Assert.That(failure.Result, Is.EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(failure.Message, Is.EqualTo("Stack-depth tracking is disabled for this target."));
#endif
            Assert.That(Unsafe.ReadUnaligned<uint>(buffer + 8), Is.EqualTo(0x94000000u));
            Assert.That(GCrefRegs(emitter), Is.EqualTo(SRBM_R0));
            Assert.That(ByrefRegs(emitter), Is.EqualTo(SRBM_NONE));
            Assert.That(CallList(ref emitter.GCInfo), Is.Null);
            var birth = RegPtrList(ref emitter.GCInfo) ?? throw new AssertionException("Missing return-register birth.");
            Assert.That(birth.rpdOffs, Is.EqualTo(12u));
            Assert.That(birth.rpdGCtypeGet(), Is.EqualTo(GCT_GCREF));
            Assert.That(birth.rpdCompiler.rpdAdd, Is.EqualTo(SRBM_R0));
            Assert.That(birth.rpdCall, Is.False);
            Assert.That(birth.rpdArg, Is.False);
#if EMIT_TRACK_STACK_DEPTH
            var call = birth.rpdNext ?? throw new AssertionException("Missing full-map call.");
            Assert.That(call.rpdOffs, Is.EqualTo(12u));
            Assert.That(call.rpdCall, Is.True);
            Assert.That(call.rpdArg, Is.True);
            Assert.That(call.rpdArgTypeGet(), Is.EqualTo(GCInfo.rpdArgType_t.rpdARG_POP));
            Assert.That(call.rpdCallInstrSize, Is.EqualTo((byte)4));
            Assert.That(call.rpdCallData.rpdCallGCrefRegs, Is.EqualTo((uint)SRBM_R0));
            Assert.That(call.rpdCallData.rpdCallByrefRegs, Is.Zero);
            Assert.That(call.rpdCallData.rpdPtrArg, Is.Zero);
            Assert.That(call.rpdNext, Is.Null);
#else
            Assert.That(birth.rpdNext, Is.Null);
#endif
        });
    }

    private static Emitter.instrDesc Call(Emitter emitter, ReadOnlySpan<nint> variables,
        regMaskTP refs, regMaskTP byrefs, emitAttr firstReturn,
        emitAttr secondReturn = EA_PTRSIZE, bool asyncReturn = false)
    {
        return AllocateCall(emitter, 0, variables, refs, byrefs, firstReturn,
#if MULTIREG_HAS_SECOND_GC_RET
            secondReturn,
#endif
            asyncReturn);
    }

    private static void WithEmitter(OutputTest action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compIsStatic = true;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler) { IsFullPtrRegMapRequired = true };
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            var emitter = codeGen.Emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            SyncThisObjectReg(emitter) = REG_NA;
            emitter.emitSimpleStkUsed = true;
            var buffer = stackalloc byte[64];
            new Span<byte>(buffer, 64).Fill(0xA5);
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 64;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            action(compiler, emitter, buffer);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static object? CallList(ref GCInfo info)
    {
        var field = typeof(GCInfo).GetField("gcCallDescList", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new AssertionException("Missing GC call list.");

        return field.GetValue(info);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "The untrimmed test inspects fixed public fields on the private GCInfo call descriptor.")]
    private static T CallField<T>(object call, string name)
    {
        var field = call.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public) ??
            throw new AssertionException($"Missing call field {name}.");

        return field.GetValue(call) is T value ? value : throw new AssertionException($"Invalid call field {name}.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitOutputCallArm64")]
    private static extern uint Output(Emitter emitter, insGroup group, byte* dst, Emitter.instrDesc id, uint code);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrCallDir")]
    private static extern Emitter.instrDesc AllocateCall(Emitter emitter, int arguments, ReadOnlySpan<nint> variables,
        regMaskTP refs, regMaskTP byrefs, emitAttr firstReturn,
#if MULTIREG_HAS_SECOND_GC_RET
        emitAttr secondReturn,
#endif
        bool asyncReturn);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask GCrefRegs(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ByrefRegs(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitSyncThisObjReg")]
    private static extern ref regNumber SyncThisObjectReg(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefVars")]
    private static extern ref nint[] ThisVariables(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefVset")]
    private static extern ref bool VariablesSet(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsTab")]
    private static extern ref int* FrameOffsets(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitTrkVarCnt")]
    private static extern ref int TrackedVariables(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsMax")]
    private static extern ref int FrameMaximum(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsCnt")]
    private static extern ref int FrameCount(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameLiveTab")]
    private static extern ref GCInfo.varPtrDsc?[]? FrameLive(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegPtrList(ref GCInfo info);
}
#endif
