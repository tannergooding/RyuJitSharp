// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.GCInfo.GCtype;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regMask;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterRegisterOutputTests
{
    private delegate byte* Output(Emitter emitter, byte* dst, Emitter.instrDesc descriptor);

    [TestCase(INS_inc, EA_8BYTE, REG_RAX, "48FFC0")]
    [TestCase(INS_bswap, EA_8BYTE, REG_R8, "490FC8")]
    [TestCase(INS_push, EA_8BYTE, REG_R9, "4151")]
    [TestCase(INS_pop, EA_8BYTE, REG_R9, "4159")]
    [TestCase(INS_setne, EA_1BYTE, REG_RAX, "0F95C0")]
    public static void SingleRegisterInstructionsPreservePrefixesAndOpcodeWidths(
        instruction ins, emitAttr attr, regNumber reg, string hex)
    {
        WithOutput(emitter => emitter.emitIns_R(ins, attr, reg),
            (emitter, dst, id) => emitter.emitOutputR(dst, id), hex);
    }

    [TestCase(INS_mov, EA_8BYTE, REG_RAX, REG_RCX, "488BC1")]
    [TestCase(INS_xor, EA_8BYTE, REG_R8, REG_R8, "4533C0")]
    [TestCase(INS_add, EA_2BYTE, REG_RAX, REG_RCX, "6603C1")]
    public static void RegisterPairsPreserveModRmAndOperandWidth(
        instruction ins, emitAttr attr, regNumber dstReg, regNumber srcReg, string hex)
    {
        WithOutput(emitter =>
        {
            if (ins == INS_mov)
            {
                _ = emitter.emitIns_Mov(ins, attr, dstReg, srcReg, canSkip: false);
            }
            else
            {
                emitter.emitIns_R_R(ins, attr, dstReg, srcReg);
            }
        },
            (emitter, dst, id) => emitter.emitOutputRR(dst, id), hex);
    }

    [Test]
    public static void ThreeRegisterAvxUsesTheFirstSourceInVvvv()
    {
        WithOutput(emitter => emitter.emitIns_R_R_R(INS_addps, EA_16BYTE, REG_XMM1, REG_XMM2, REG_XMM3),
            (emitter, dst, id) => emitter.emitOutputRRR(dst, id), "C5E858CB", vex: true);
    }

    [Test]
    public static void HighSimdRegistersUseEvexExtensionsAndTheFullVectorLength()
    {
        WithOutput(emitter =>
        {
            emitter.UseEvexEncodings = true;
            emitter.emitIns_R_R_R(INS_addps, EA_64BYTE, REG_XMM16, REG_XMM17, REG_XMM18);
        }, (emitter, dst, id) => emitter.emitOutputRRR(dst, id), "62A1744058C2", vex: true);
    }

    [Test]
    public static void ApxNddUsesVvvvvAndTheExtendedSourceRegisterBit()
    {
        WithOutput(emitter =>
        {
            emitter.UseRex2Encodings = true;
            emitter.UsePromotedEvexEncodings = true;
            emitter.emitIns_R_R(INS_add, EA_8BYTE, REG_R16, REG_R17, INS_OPTS_EVEX_nd);
        }, (emitter, dst, id) => emitter.emitOutputRR(dst, id), "62FCFC1003C1", vex: true);
    }

    [TestCase(INS_mov, EA_8BYTE, REG_RAX, 0x1122334455667788L, "48B88877665544332211")]
    [TestCase(INS_mov, EA_8BYTE, REG_R9, 0x1122334455667788L, "49B98877665544332211")]
    [TestCase(INS_add, EA_8BYTE, REG_RCX, -1L, "4883C1FF")]
    [TestCase(INS_add, EA_8BYTE, REG_R9, -1L, "4983C1FF")]
    [TestCase(INS_add, EA_4BYTE, REG_RAX, 5L, "83C005")]
    [TestCase(INS_pslld, EA_16BYTE, REG_XMM0, 4L, "660F72F004")]
    public static void RegisterImmediatesPreserveSignExtensionAndSimdShiftByte(
        instruction ins, emitAttr attr, regNumber reg, long immediate, string hex)
    {
        WithOutput(emitter => emitter.emitIns_R_I(ins, attr, reg, (nint)immediate),
            (emitter, dst, id) => emitter.emitOutputRI(dst, id), hex);
    }

    [TestCase(INS_push, EA_8BYTE, -1L, "6AFF")]
    [TestCase(INS_push, EA_8BYTE, 128L, "6880000000")]
    [TestCase(INS_ret, EA_8BYTE, 16L, "C21000")]
    [TestCase(INS_loop, EA_1BYTE, -2L, "E2FE")]
    public static void ImmediateOnlyInstructionsRetainNativeWidths(
        instruction ins, emitAttr attr, long immediate, string hex)
    {
        WithOutput(emitter => emitter.emitIns_I(ins, attr, (nint)immediate),
            (emitter, dst, id) => emitter.emitOutputIV(dst, id), hex);
    }

    [Test]
    public static void JgeImmediateOnlyUsesShortConditionalOpcode()
    {
        WithOutput(emitter =>
        {
            emitter.emitIns_I(INS_loop, EA_1BYTE, 5);
            Last(emitter).idIns(INS_jge);
        }, (emitter, dst, id) => emitter.emitOutputIV(dst, id), "7D05");
    }

    [Test]
    public static void RegisterOutputRecordsGCRegisterBirthAndDeathAtIssuedOffsets()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, local) =>
        {
            codeGen.IsFullPtrRegMapRequired = true;
            var emitter = codeGen.Emitter;
            emitter.emitFullGCinfo = true;
            _ = emitter.emitIns_Mov(INS_mov, EA_GCREF, REG_RCX, REG_RAX, canSkip: false);
            var born = Last(emitter);
            _ = emitter.emitIns_Mov(INS_mov, EA_8BYTE, REG_RCX, REG_RDX, canSkip: false);
            var dead = Last(emitter);

            var buffer = stackalloc byte[32];
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputRR(buffer, born);
            var birthOffset = (uint)(end - buffer);
            end = emitter.emitOutputRR(end, dead);

            var record = RegPtrList(ref codeGen.GCInfo) ?? throw new AssertionException("Missing GC birth record.");
            Assert.That(record.rpdGCtype, Is.EqualTo(GCT_GCREF));
            Assert.That(record.rpdOffs, Is.EqualTo(birthOffset));
            Assert.That(record.rpdCompiler.rpdAdd, Is.EqualTo(SRBM_RCX));
            record = record.rpdNext ?? throw new AssertionException("Missing GC death record.");
            Assert.That(record.rpdGCtype, Is.EqualTo(GCT_GCREF));
            Assert.That(record.rpdOffs, Is.EqualTo((uint)(end - buffer)));
            Assert.That(record.rpdCompiler.rpdDel, Is.EqualTo(SRBM_RCX));
            Assert.That(record.rpdNext, Is.Null);
            Assert.That(GCrefRegs(emitter), Is.EqualTo(SRBM_NONE));
        });
    }

    [TestCase(false, CorInfoReloc.DIRECT)]
    [TestCase(true, CorInfoReloc.AMD64_WIN_SECREL)]
    public static void MovImmediateRecordsDirectOrSectionRelativeRelocationAtTheImmediate(
        bool sectionRelative, CorInfoReloc relocation)
    {
        WithRelocation((emitter) =>
        {
            var attr = EA_8BYTE | EA_CNS_RELOC_FLG
                | (sectionRelative ? EA_CNS_SEC_RELOC : 0);
            emitter.emitIns_R_I(INS_mov, attr, REG_RAX, 0x12345678);
        }, (emitter, buffer, id) => emitter.emitOutputRI(buffer, id), relocation, 2,
            "48B87856341200000000");
    }

    [Test]
    public static void PushImmediateRelocationStaysWideAndRelative()
    {
        WithRelocation(emitter =>
            emitter.emitIns_I(INS_push, EA_4BYTE | EA_CNS_RELOC_FLG, 0x12345678),
            (emitter, buffer, id) => emitter.emitOutputIV(buffer, id),
            CorInfoReloc.RELATIVE32, 1, "6878563412");
    }

    private static void WithRelocation(Action<Emitter> record, Output output, CorInfoReloc kind,
        int siteOffset, string hex)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_NATIVEAOT_ABI;
            compiler.opts.compReloc = true;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            compiler.info.compMatchedVM = true;
            var emitter = codeGen.Emitter;
            emitter.emitCmpHandle = &context.JitInfo;
            record(emitter);
            var id = Last(emitter);
            var buffer = stackalloc byte[24];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = output(emitter, buffer, id);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That((nuint)context.Location, Is.EqualTo((nuint)(buffer + siteOffset)));
            Assert.That((nuint)context.Target, Is.EqualTo((nuint)0x12345678));
            Assert.That(context.Kind, Is.EqualTo(kind));
            Assert.That(context.Calls, Is.EqualTo(1));
        });
    }

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

    private static void WithOutput(Action<Emitter> record, Output output, string hex, bool vex = false)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            var emitter = codeGen.Emitter;
            emitter.UseVexEncodings = vex;
            emitter.UseEvexEncodings = false;
            record(emitter);
            var id = Last(emitter);
            var buffer = stackalloc byte[32];
            new Span<byte>(buffer, 32).Fill(0xA5);
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = output(emitter, buffer, id);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
            Assert.That(buffer[end - buffer], Is.EqualTo(0xA5));
        });
    }

    private static Emitter.instrDesc Last(Emitter emitter) =>
        LastInstruction(emitter) ?? throw new AssertionException("No recorded instruction.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegPtrList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitThisGCrefRegs")]
    private static extern ref regMask GCrefRegs(Emitter emitter);
}
