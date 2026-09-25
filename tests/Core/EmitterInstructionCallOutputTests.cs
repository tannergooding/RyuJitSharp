// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.GCInfo.rpdArgType_t;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterInstructionCallOutputTests
{
    private delegate void CallTest(Compiler compiler, Emitter emitter, Emitter.instrDesc id,
        byte* buffer, CallbackContext* callbacks);

    [TestCase(EmitCallType.EC_FUNC_TOKEN, false, "E800000000", CorInfoReloc.RELATIVE32)]
    [TestCase(EmitCallType.EC_FUNC_TOKEN, true, "E800000000", CorInfoReloc.RELATIVE32)]
    [TestCase(EmitCallType.EC_FUNC_TOKEN_INDIR, false, "FF1500000000", CorInfoReloc.RELATIVE32)]
    [TestCase(EmitCallType.EC_FUNC_TOKEN_INDIR, true, "FF1500000000", CorInfoReloc.RELATIVE32)]
    [TestCase(EmitCallType.EC_INDIR_R, false, "FFD0", CorInfoReloc.NONE)]
    [TestCase(EmitCallType.EC_INDIR_R, true, "FFD0", CorInfoReloc.NONE)]
    [TestCase(EmitCallType.EC_INDIR_ARD, false, "FF10", CorInfoReloc.NONE)]
    [TestCase(EmitCallType.EC_INDIR_ARD, true, "FF10", CorInfoReloc.NONE)]
    public static void RecordedCallsDispatchBytesRelocationsAndPartialGcMaps(
        EmitCallType type, bool large, string hex, CorInfoReloc relocation)
    {
        WithCall(type, large, EA_PTRSIZE, false, false, (_, emitter, id, buffer, callbacks) =>
        {
            Assert.That(id.idIsLargeCall(), Is.EqualTo(large));
            Assert.That(id.idIsNoGC(), Is.False);
            Assert.That(id.idCodeSize(), Is.EqualTo((uint)(hex.Length / 2)));

            var end = buffer + 8;
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing call group.");
            var size = emitter.emitOutputInstr(group, id, &end);

            Assert.That(size, Is.EqualTo((nuint)id.NativeLogicalSize));
            Assert.That(new ReadOnlySpan<byte>(buffer + 8, hex.Length / 2).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That((nuint)end, Is.EqualTo((nuint)(buffer + 8 + (hex.Length / 2))));
            Assert.That(buffer[8 + (hex.Length / 2)], Is.EqualTo(0xA5));
            AssertCallSite(callbacks, 8, (nint)0x2000);

            var expectedRelocations = relocation == CorInfoReloc.NONE ? 0 : 1;
            Assert.That(callbacks->Relocations, Is.EqualTo(expectedRelocations));
            if (expectedRelocations != 0)
            {
                Assert.That(callbacks->Relocation, Is.EqualTo(relocation));
                Assert.That((nuint)callbacks->Location, Is.EqualTo((nuint)(end - 4)));
                Assert.That((nuint)callbacks->Target, Is.EqualTo((nuint)0x1234));
            }
            Assert.That(callbacks->Assertions, Is.Zero);

            var record = CallList(ref emitter.GCInfo);
            Assert.That(CallField<uint>(record, "cdOffs"), Is.EqualTo((uint)(8 + (hex.Length / 2))));
            Assert.That(CallField<ushort>(record, "cdCallInstrSize"), Is.EqualTo((ushort)(hex.Length / 2)));
            Assert.That(CallField<regMask>(record, "cdGCrefRegs"), Is.EqualTo(SRBM_RBX));
            Assert.That(CallField<regMask>(record, "cdByrefRegs"), Is.EqualTo(large ? SRBM_R12 : SRBM_NONE));
            Assert.That(CallOptionalField(record, "cdNext"), Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NoGcCallsStillPublishCallSitesButNotGcCallRecords(bool large)
    {
        WithCall(EmitCallType.EC_FUNC_TOKEN, large, EA_PTRSIZE, false, true,
            (_, emitter, id, buffer, callbacks) =>
            {
                Assert.That(id.idIsNoGC(), Is.True);
                var end = buffer + 8;
                var group = emitter.emitCurIG ?? throw new AssertionException("Missing call group.");
                var size = emitter.emitOutputInstr(group, id, &end);

                Assert.That(size, Is.EqualTo((nuint)id.NativeLogicalSize));
                Assert.That(new ReadOnlySpan<byte>(buffer + 8, 5).ToArray(),
                    Is.EqualTo(Convert.FromHexString("E800000000")));
                Assert.That((nuint)end, Is.EqualTo((nuint)(buffer + 13)));
                AssertCallSite(callbacks, 8, (nint)0x2000);
                Assert.That(callbacks->Relocations, Is.EqualTo(1));
                Assert.That(CallOptionalList(ref emitter.GCInfo), Is.Null);
                Assert.That(callbacks->Assertions, Is.Zero);
            });
    }

    [TestCase(EA_GCREF, false)]
    [TestCase(EA_GCREF, true)]
    [TestCase(EA_BYREF, false)]
    [TestCase(EA_BYREF, true)]
    public static void FullMapsEndStackVariableAtCallStartAndBirthReturnRegistersAtCallEnd(
        emitAttr retSize, bool asyncReturn)
    {
        WithCall(EmitCallType.EC_FUNC_TOKEN, true, retSize, asyncReturn, false,
            (compiler, emitter, id, buffer, callbacks) =>
            {
                var tracking = stackalloc byte[16];
                emitter.emitFullGCinfo = true;
                emitter.emitFullArgInfo = true;
                emitter.emitSimpleStkUsed = false;
                emitter.emitMaxStackDepth = 16;
                emitter.u2.emitArgTrackTab = tracking;
                emitter.u2.emitArgTrackTop = tracking;
                emitter.u2.emitGcArgTrackCnt = 0;

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
                GCrefRegs(emitter) = SRBM_RBX;
                ByrefRegs(emitter) = SRBM_R12;
                Assert.That(id.idIsLargeCall(), Is.True);

                var end = buffer + 8;
                var group = emitter.emitCurIG ?? throw new AssertionException("Missing call group.");
                var size = emitter.emitOutputInstr(group, id, &end);

                Assert.That(size, Is.EqualTo((nuint)id.NativeLogicalSize));
                Assert.That((nuint)end, Is.EqualTo((nuint)(buffer + 13)));
                Assert.That(live.vpdEndOfs, Is.EqualTo(8u));
                var frameLive = FrameLive(emitter)
                    ?? throw new AssertionException("Missing tracked GC frame.");
                Assert.That(frameLive[0], Is.Null);
                Assert.That(VarSetOps.IsEmpty(compiler, ThisVariables(emitter)), Is.True);
                AssertCallSite(callbacks, 8, (nint)0x2000);

                var first = RegPtrList(ref emitter.GCInfo)
                    ?? throw new AssertionException("Missing full-map return register.");
                if (asyncReturn && retSize == EA_BYREF)
                {
                    AssertRegisterBirth(first, GCT_GCREF, SRBM_RCX, 13);
                    first = first.rpdNext ?? throw new AssertionException("Missing return-register birth.");
                }
                AssertRegisterBirth(first, retSize == EA_GCREF ? GCT_GCREF : GCT_BYREF, SRBM_RAX, 13);
                if (asyncReturn && retSize == EA_GCREF)
                {
                    first = first.rpdNext ?? throw new AssertionException("Missing async return-register birth.");
                    AssertRegisterBirth(first, GCT_GCREF, SRBM_RCX, 13);
                }
                var call = first.rpdNext ?? throw new AssertionException("Missing full-map call record.");
                Assert.That(call.rpdOffs, Is.EqualTo(13u));
                Assert.That(call.rpdCall, Is.True);
                Assert.That(call.rpdArgTypeGet(), Is.EqualTo(rpdARG_POP));
                Assert.That(call.rpdCallInstrSize, Is.EqualTo((byte)5));
                Assert.That(call.rpdNext, Is.Null);
                Assert.That(GCrefRegs(emitter),
                    Is.EqualTo(SRBM_RBX | (retSize == EA_GCREF ? SRBM_RAX : SRBM_NONE) |
                        (asyncReturn ? SRBM_RCX : SRBM_NONE)));
                Assert.That(ByrefRegs(emitter),
                    Is.EqualTo(SRBM_R12 | (retSize == EA_BYREF ? SRBM_RAX : SRBM_NONE)));
                Assert.That(CallOptionalList(ref emitter.GCInfo), Is.Null);
                Assert.That(callbacks->Assertions, Is.Zero);
            }, fullPtrMap: true);
    }

    private static void WithCall(EmitCallType type, bool large, emitAttr retSize, bool asyncReturn,
        bool noSafePoint, CallTest test, bool fullPtrMap = false)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            compiler.opts.compReloc = true;
            compiler.info.compMatchedVM = true;
            codeGen.IsFullPtrRegMapRequired = fullPtrMap;
            var emitter = codeGen.Emitter;
            emitter.UseVexEncodings = false;
            emitter.UseEvexEncodings = false;
            SyncThisRegister(emitter) = REG_NA;
            var parameters = new EmitCallParams
            {
                callType = type,
                methHnd = (CORINFO_METHOD_STRUCT_*)0x2000,
                addr = type is EmitCallType.EC_FUNC_TOKEN or EmitCallType.EC_FUNC_TOKEN_INDIR
                    ? (void*)0x1234 : null,
                ireg = type is EmitCallType.EC_INDIR_R or EmitCallType.EC_INDIR_ARD ? REG_RAX : REG_NA,
                ptrVars = VarSetOps.MakeEmpty(compiler),
                gcrefRegs = new(SRBM_RBX),
                byrefRegs = new(large ? SRBM_R12 : SRBM_NONE),
                retSize = retSize,
                hasAsyncRet = asyncReturn,
                noSafePoint = noSafePoint,
#if DEBUG
                sigInfo = new StrongBox<CORINFO_SIG_INFO>(),
#endif
            };
            emitter.emitIns_Call(parameters);
            var id = LastInstruction(emitter) ?? throw new AssertionException("Missing recorded call.");
            var buffer = stackalloc byte[64];
            new Span<byte>(buffer, 64).Fill(0xA5);
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 64;
            emitter.writeableOffset = 0;
            emitter.emitSimpleStkUsed = true;
            emitter.emitCurStackLvl = 0;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            GCrefRegs(emitter) = SRBM_RBX;
            ByrefRegs(emitter) = large ? SRBM_R12 : SRBM_NONE;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordCallSite = &RecordCallSite;
            vtable.recordRelocation = &RecordRelocation;
            vtable.doAssert = &RecordAssertion;
            var callbacks = new CallbackContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            };
            emitter.emitCmpHandle = &callbacks.JitInfo;
            test(compiler, emitter, id, buffer, &callbacks);
        });
    }

    private static void AssertCallSite(CallbackContext* callbacks, int offset, nint methodHandle)
    {
#if DEBUG
        Assert.That(callbacks->CallSites, Is.EqualTo(1));
        Assert.That(callbacks->CallOffset, Is.EqualTo(offset));
        Assert.That(callbacks->CallMethod, Is.EqualTo(methodHandle));
        Assert.That(callbacks->HadSignature, Is.True);
#else
        Assert.That(callbacks->CallSites, Is.Zero);
#endif
    }

    private static void AssertRegisterBirth(GCInfo.regPtrDsc record, GCInfo.GCtype type,
        regMask reg, uint offset)
    {
        Assert.That(record.rpdOffs, Is.EqualTo(offset));
        Assert.That(record.rpdGCtypeGet(), Is.EqualTo(type));
        Assert.That(record.rpdCall, Is.False);
        Assert.That(record.rpdArg, Is.False);
        Assert.That(record.rpdCompiler.rpdAdd, Is.EqualTo(reg));
        Assert.That(record.rpdCompiler.rpdDel, Is.EqualTo(SRBM_NONE));
    }

    private static object? CallOptionalList(ref GCInfo info)
    {
        var field = typeof(GCInfo).GetField("gcCallDescList", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new AssertionException("Missing GC call-list field.");
        return field.GetValue(info);
    }

    private static object CallList(ref GCInfo info)
        => CallOptionalList(ref info) ?? throw new AssertionException("Missing GC call descriptor.");

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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitSyncThisObjReg")]
    private static extern ref regNumber SyncThisRegister(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask GCrefRegs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ByrefRegs(Emitter emitter);

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

    private struct CallbackContext
    {
        public ICorJitInfo JitInfo;
        public int CallSites;
        public int CallOffset;
        public nint CallMethod;
        public bool HadSignature;
        public int Relocations;
        public void* Location;
        public void* Target;
        public CorInfoReloc Relocation;
        public int Assertions;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordCallSite(ICorJitInfo* info, int offset,
        CORINFO_SIG_INFO* signature, CORINFO_METHOD_STRUCT_* method)
    {
        var context = (CallbackContext*)info;
        context->CallSites++;
        context->CallOffset = offset;
        context->CallMethod = (nint)method;
        context->HadSignature = signature != null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* info, void* location, void* locationRW,
        void* target, CorInfoReloc relocation, int delta)
    {
        var context = (CallbackContext*)info;
        context->Relocations++;
        context->Location = location;
        context->Target = target;
        context->Relocation = relocation;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* info, byte* file, int line, byte* expression)
    {
        ((CallbackContext*)info)->Assertions++;
        return 0;
    }
}
