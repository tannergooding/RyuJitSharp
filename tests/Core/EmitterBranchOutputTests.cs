// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterBranchOutputTests
{
    [TestCase(0, "")]
    [TestCase(1, "90")]
    [TestCase(2, "6690")]
    [TestCase(3, "0F1F00")]
    [TestCase(4, "0F1F4000")]
    [TestCase(5, "0F1F440000")]
    [TestCase(6, "660F1F440000")]
    [TestCase(7, "0F1F8000000000")]
    [TestCase(8, "0F1F840000000000")]
    [TestCase(9, "660F1F840000000000")]
    [TestCase(10, "66660F1F840000000000")]
    [TestCase(11, "6666660F1F840000000000")]
    [TestCase(12, "0F1F40000F1F840000000000")]
    [TestCase(13, "0F1F4400000F1F840000000000")]
    [TestCase(14, "0F1F80000000000F1F8000000000")]
    [TestCase(15, "0F1F80000000000F1F840000000000")]
    public static void NopLengthsUseNativeInstructionSequences(int size, string hex)
    {
        WithEmitter((_, emitter) =>
        {
            var buffer = stackalloc byte[18];
            new Span<byte>(buffer, 18).Fill(0xA5);
            var end = emitter.emitOutputNOP(buffer, (nuint)size);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That(buffer[size], Is.EqualTo(0xA5));
        });
    }

    [Test]
    public static void DataSizePrefixWritesThroughWritableAlias()
    {
        WithEmitter((_, emitter) =>
        {
            var executable = stackalloc byte[2];
            var writable = stackalloc byte[2];
            executable[0] = 0xA5;
            writable[0] = 0xA5;
            emitter.writeableOffset = (nint)(writable - executable);

            var end = emitter.emitOutputData16(executable);
            Assert.That((nuint)end, Is.EqualTo((nuint)(executable + 1)));
            Assert.That(executable[0], Is.EqualTo(0xA5));
            Assert.That(writable[0], Is.EqualTo(0x66));
        });
    }

#if FEATURE_LOOP_ALIGN
    [TestCase(0u, "")]
    [TestCase(7u, "0F1F8000000000")]
    [TestCase(15u, "0F1F80000000000F1F840000000000")]
    public static void LoopAlignmentEmitsRecordedPaddingAndUpdatesMetrics(uint padding, string hex)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;
            codeGen.ShouldAlignLoops = true;
            compiler.opts.compJitAlignLoopBoundary = 16;
            var emitter = codeGen.Emitter;
            var group = emitter.emitCurIG ?? throw new AssertionException("Missing alignment group.");
            group.igFlags |= InsGroupFlags.HasAlign;
            var descriptor = View.Align(group, padding);
            var buffer = stackalloc byte[20];
            new Span<byte>(buffer, 20).Fill(0xA5);
            var initialCount = compiler.Metrics.LoopsAligned;

            var end = emitter.emitOutputAlign(group, descriptor, buffer);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That(buffer[padding], Is.EqualTo(0xA5));
            Assert.That(compiler.Metrics.LoopsAligned, Is.EqualTo(initialCount + 1));
        });
    }
#endif

    [TestCase(INS_jmp, 0, "EB03")]
    [TestCase(INS_jne, 0, "7504")]
    [TestCase(INS_jmp, 125, "E97D000000")]
    [TestCase(INS_jne, 128, "0F8580000000")]
    [TestCase(INS_call, 0, "E800000000")]
    public static void ForwardLabelsPreserveShortLongAndCallEncodings(
        instruction ins, int padding, string expected)
    {
        WithEmitter((_, emitter) =>
        {
            var label = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(ins, label);
            var descriptor = Last(emitter);
            AddNops(emitter, padding);
            var target = emitter.emitAddInlineLabel();
            View.Target(descriptor) = target;

            var buffer = stackalloc byte[256];
            new Span<byte>(buffer, 256).Fill(0xA5);
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 256;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputLJ(source, buffer, descriptor);
            var bytes = Convert.FromHexString(expected);
            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(), Is.EqualTo(bytes));
            Assert.That(buffer[bytes.Length], Is.EqualTo(0xA5));
            Assert.That(View.Offset(descriptor), Is.EqualTo(target.igOffs));
            Assert.That((nuint)View.PatchSite(descriptor),
                Is.EqualTo((nuint)(buffer + (bytes.Length == 2 ? 1 : bytes.Length - 4))));
        });
    }

    [Test]
    public static void BackwardJumpUsesNegativeShortDisplacementWithoutPatchSite()
    {
        WithEmitter((_, emitter) =>
        {
            var label = Label();
            var target = emitter.emitCurIG ?? throw new AssertionException("Missing target group.");
            label.bbEmitCookie = target;
            emitter.emitIns_Nop(3);
            var source = emitter.emitAddInlineLabel();
            emitter.emitIns_J(INS_jmp, label);
            var descriptor = Last(emitter);
            View.Target(descriptor) = target;

            var buffer = stackalloc byte[32];
            new Span<byte>(buffer, 32).Fill(0xA5);
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputLJ(source, buffer + 3, descriptor);
            Assert.That(new ReadOnlySpan<byte>(buffer + 3, (int)(end - (buffer + 3))).ToArray(),
                Is.EqualTo(Convert.FromHexString("EBFB")));
            Assert.That((nuint)View.PatchSite(descriptor), Is.EqualTo((nuint)0));
        });
    }

    [Test]
    public static void ForwardJumpRecordsAdjustedTargetOffsetAndFuturePatchSite()
    {
        WithEmitter((_, emitter) =>
        {
            var label = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_jmp, label);
            var descriptor = Last(emitter);
            AddNops(emitter, 10);
            var target = emitter.emitAddInlineLabel();
            View.Target(descriptor) = target;
            View.Adjustment(emitter) = 4;

            var buffer = stackalloc byte[32];
            new Span<byte>(buffer, 32).Fill(0xA5);
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputLJ(source, buffer, descriptor);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString("EB09")));
            Assert.That(View.Offset(descriptor), Is.EqualTo(target.igOffs - 4));
            Assert.That((nuint)View.PatchSite(descriptor), Is.EqualTo((nuint)(buffer + 1)));
        });
    }

    [Test]
    public static void LocalCallKillsBothClassesOfLiveGCRegistersAtTheCallEnd()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (_, codeGen, _) =>
        {
            codeGen.IsFullPtrRegMapRequired = true;
            var emitter = codeGen.Emitter;
            emitter.emitFullGCinfo = true;
            var label = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(INS_call, label);
            var descriptor = Last(emitter);
            View.Target(descriptor) = emitter.emitAddInlineLabel();
            SyncThisReg(emitter) = REG_NA;
            GCrefRegs(emitter) = SRBM_RCX;
            ByrefRegs(emitter) = SRBM_RDX;

            var buffer = stackalloc byte[32];
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputLJ(source, buffer, descriptor);

            Assert.That(GCrefRegs(emitter) | ByrefRegs(emitter), Is.EqualTo(SRBM_NONE));
            var gcRecord = RegPtrList(ref codeGen.GCInfo)
                ?? throw new AssertionException("Missing GC-register death record.");
            Assert.That(gcRecord.rpdGCtype, Is.EqualTo(GCT_GCREF));
            Assert.That(gcRecord.rpdOffs, Is.EqualTo((uint)(end - buffer)));
            Assert.That(gcRecord.rpdCompiler.rpdDel, Is.EqualTo(SRBM_RCX));
            var byrefRecord = gcRecord.rpdNext
                ?? throw new AssertionException("Missing byref-register death record.");
            Assert.That(byrefRecord.rpdGCtype, Is.EqualTo(GCT_BYREF));
            Assert.That(byrefRecord.rpdOffs, Is.EqualTo((uint)(end - buffer)));
            Assert.That(byrefRecord.rpdCompiler.rpdDel, Is.EqualTo(SRBM_RDX));
            Assert.That(byrefRecord.rpdNext, Is.Null);
        });
    }

    [TestCase(INS_jmp, "E900000000")]
    [TestCase(INS_push, "6800000000")]
    public static void RelocatableCrossRegionLabelUsesZeroDisplacementAndColdTargetRelocation(
        instruction ins, string expected)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.opts.compReloc = true;
            compiler.info.compMatchedVM = true;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            emitter.emitCmpHandle = &context.JitInfo;

            var label = Label();
            var source = emitter.emitCurIG ?? throw new AssertionException("Missing source group.");
            emitter.emitIns_J(ins, label);
            var descriptor = Last(emitter);
            var target = emitter.emitAddInlineLabel();
            View.Target(descriptor) = target;
            View.KeepLong(descriptor) = true;

            var hot = stackalloc byte[16];
            var cold = stackalloc byte[16];
            new Span<byte>(hot, 16).Fill(0xA5);
            emitter.emitCodeBlock = hot;
            emitter.emitColdCodeBlock = cold;
            emitter.emitTotalHotCodeSize = unchecked((int)target.igOffs);
            emitter.emitTotalColdCodeSize = 16;
            emitter.emitSetFirstColdIGCookie(target);
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputLJ(source, hot, descriptor);

            Assert.That(new ReadOnlySpan<byte>(hot, (int)(end - hot)).ToArray(),
                Is.EqualTo(Convert.FromHexString(expected)));
            Assert.That((nuint)View.PatchSite(descriptor), Is.EqualTo((nuint)(hot + 1)));
            Assert.That(View.Offset(descriptor), Is.EqualTo(target.igOffs));
            Assert.That(context.Calls, Is.EqualTo(1));
            Assert.That(context.Kind, Is.EqualTo(CorInfoReloc.RELATIVE32));
            Assert.That((nuint)context.Location, Is.EqualTo((nuint)(hot + 1)));
            Assert.That((nuint)context.Target, Is.EqualTo((nuint)cold));
        });
    }

    [Test]
    public static void RelocatableLabelLeaEncodesRipRelativeAddressWithRelocation()
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.opts.compReloc = true;
            compiler.info.compMatchedVM = true;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            emitter.emitCmpHandle = &context.JitInfo;

            var target = emitter.emitCurIG ?? throw new AssertionException("Missing target group.");
            emitter.emitIns_Nop(3);
            var source = emitter.emitAddInlineLabel();
            emitter.emitIns_R_L(INS_lea, EA_PTRSIZE | EA_DSP_RELOC_FLG, target, REG_RAX);
            var descriptor = Last(emitter);

            var buffer = stackalloc byte[32];
            new Span<byte>(buffer, 32).Fill(0xA5);
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputLJ(source, buffer + 3, descriptor);

            Assert.That(new ReadOnlySpan<byte>(buffer + 3, (int)(end - (buffer + 3))).ToArray(),
                Is.EqualTo(Convert.FromHexString("488D0500000000")));
            Assert.That((nuint)View.PatchSite(descriptor), Is.EqualTo((nuint)0));
            Assert.That(context.Calls, Is.EqualTo(1));
            Assert.That((nuint)context.Location, Is.EqualTo((nuint)(buffer + 6)));
            Assert.That((nuint)context.Target, Is.EqualTo((nuint)buffer));
            Assert.That(context.Kind, Is.EqualTo(CorInfoReloc.RELATIVE32));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LabelMovToStackPreservesLocalTargetAndDescriptorAfterRelocation(bool dispatch)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            compiler.opts.compReloc = true;
            compiler.info.compMatchedVM = true;
            compiler.lvaTable[0].lvFramePointerBased = true;
            compiler.lvaTable[0].StackOffset = -16;

            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            emitter.emitCmpHandle = &context.JitInfo;

            var target = emitter.emitCurIG ?? throw new AssertionException("Missing target group.");
            emitter.emitIns_Nop(3);
            var source = emitter.emitAddInlineLabel();
            var descriptor = View.LabelMov(source, target, 0, 0);
            var originalFormat = descriptor.idInsFmt();
            var originalLocal = View.Local(descriptor);

            var buffer = stackalloc byte[32];
            new Span<byte>(buffer, 32).Fill(0xA5);
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = buffer + 3;
            if (dispatch)
            {
                Assert.That(emitter.emitOutputInstr(source, descriptor, &end), Is.EqualTo((nuint)56));
            }
            else
            {
                end = emitter.emitOutputLJ(source, end, descriptor);
            }

            Assert.That(new ReadOnlySpan<byte>(buffer + 3, (int)(end - (buffer + 3))).ToArray(),
                Is.EqualTo(Convert.FromHexString("48C745F000000000")));
            Assert.That((uint)(end - (buffer + 3)), Is.EqualTo(descriptor.idCodeSize()));
            Assert.That(buffer[11], Is.EqualTo(0xA5));
            Assert.That(context.Calls, Is.EqualTo(1));
            Assert.That(context.Kind, Is.EqualTo(CorInfoReloc.RELATIVE32));
            Assert.That((nuint)context.Location, Is.EqualTo((nuint)(buffer + 7)));
            Assert.That((nuint)context.Target, Is.EqualTo((nuint)buffer));
            Assert.That(descriptor.idInsFmt(), Is.EqualTo(originalFormat));
            Assert.That(descriptor.idIsDspReloc(), Is.True);
            Assert.That(View.Local(descriptor).lvaVarNum(), Is.EqualTo(originalLocal.lvaVarNum()));
            Assert.That(View.Local(descriptor).lvaOffset(), Is.EqualTo(originalLocal.lvaOffset()));
            Assert.That(View.Target(descriptor), Is.SameAs(target));
            Assert.That((nuint)View.PatchSite(descriptor), Is.EqualTo((nuint)0));
        });
    }

    private static void AddNops(Emitter emitter, int count)
    {
        while (count > 0)
        {
            var length = int.Min(count, 15);
            emitter.emitIns_Nop((uint)length);
            count -= length;
        }
    }

    private static BasicBlock Label()
    {
        var block = new BasicBlock(null, null);
        block.SetFlags(BBF_HAS_LABEL);
        return block;
    }

    private static void WithEmitter(Action<Compiler, Emitter> test)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX,
            (compiler, codeGen, _) => test(compiler, codeGen.Emitter));
    }

    private static Emitter.instrDesc Last(Emitter emitter)
    {
        return LastInstruction(emitter) ?? throw new AssertionException("Missing recorded instruction.");
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitOffsAdj")]
    private static extern ref int OffsetAdjustment(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask GCrefRegs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitSyncThisObjReg")]
    private static extern ref regNumber SyncThisReg(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisByrefRegs")]
    private static extern ref regMask ByrefRegs(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegPtrList(ref GCInfo info);

    private struct RelocationContext
    {
        public ICorJitInfo JitInfo;
        public int Calls;
        public void* Location;
        public void* Target;
        public CorInfoReloc Kind;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* self, void* location, void* locationRW,
        void* target, CorInfoReloc kind, int delta)
    {
        var context = (RelocationContext*)self;
        context->Calls++;
        context->Location = location;
        context->Target = target;
        context->Kind = kind;
    }

    private abstract class View(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDesc LabelMov(insGroup source, insGroup target, int local, uint offset)
        {
            var descriptor = new instrDescLbl();
            descriptor.idIns(INS_mov);
            descriptor.idInsFmt(IF_SWR_LABEL);
            descriptor.idOpSize(EA_8BYTE);
            descriptor.idCodeSize(8);
            descriptor.idjIG = source;
            descriptor.idjTargetIG = target;
            descriptor.idjKeepLong = true;
            descriptor.idSetIsBound();
            descriptor.idSetIsDspReloc();
            descriptor.idAddr().iiaLclVar.initLclVarAddr(local, offset);
            return descriptor;
        }

        public static emitLclVarAddr Local(instrDesc descriptor) => descriptor.idAddr().iiaLclVar;

#if FEATURE_LOOP_ALIGN
        public static instrDesc Align(insGroup group, uint padding)
        {
            var descriptor = new instrDescAlign();
            descriptor.idIns(INS_align);
            descriptor.idCodeSize(padding);
            descriptor.idaIG = group;
#if DEBUG
            descriptor.isPlacedAfterJmp = true;
#endif
            return descriptor;
        }
#endif

        public static ref insGroup? Target(instrDesc descriptor) => ref ((instrDescJmp)descriptor).idjTargetIG;
        public static ref bool KeepLong(instrDesc descriptor) => ref ((instrDescJmp)descriptor).idjKeepLong;
        public static uint Offset(instrDesc descriptor) => ((instrDescJmp)descriptor).idjOffs;
        public static byte* PatchSite(instrDesc descriptor) => ((instrDescJmp)descriptor).idjAddr;
        public static ref int Adjustment(Emitter emitter) => ref OffsetAdjustment(emitter);
    }
}
