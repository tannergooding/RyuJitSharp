// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterAddressOutputTests
{
    [TestCase(REG_RAX, REG_NA, 0u, 0L, "488B00")]
    [TestCase(REG_RSP, REG_NA, 0u, 0L, "488B0424")]
    [TestCase(REG_RBP, REG_NA, 0u, 0L, "488B4500")]
    [TestCase(REG_R12, REG_NA, 0u, 0L, "498B0424")]
    [TestCase(REG_R13, REG_NA, 0u, 0L, "498B4500")]
    [TestCase(REG_RAX, REG_NA, 0u, -128L, "488B4080")]
    [TestCase(REG_RAX, REG_NA, 0u, 127L, "488B407F")]
    [TestCase(REG_RAX, REG_NA, 0u, -129L, "488B807FFFFFFF")]
    [TestCase(REG_RAX, REG_NA, 0u, 128L, "488B8080000000")]
    [TestCase(REG_RSP, REG_NA, 0u, 128L, "488B842480000000")]
    [TestCase(REG_RBP, REG_NA, 0u, 128L, "488B8580000000")]
    [TestCase(REG_RAX, REG_RCX, 0u, 0L, "488B0408")]
    [TestCase(REG_RAX, REG_RCX, 1u, 127L, "488B44487F")]
    [TestCase(REG_RAX, REG_RCX, 2u, 128L, "488B848880000000")]
    [TestCase(REG_RAX, REG_RCX, 3u, -128L, "488B44C880")]
    [TestCase(REG_R12, REG_R9, 3u, 8L, "4B8B44CC08")]
    [TestCase(REG_RAX, REG_RCX, 0u, 128L, "488B840880000000")]
    [TestCase(REG_RBP, REG_RCX, 1u, 0L, "488B444D00")]
    [TestCase(REG_NA, REG_RCX, 2u, 0x1234L, "488B048D34120000")]
    public static void BaseIndexAndDisplacementBytesMatchRecordedSize(regNumber baseReg, regNumber index,
        uint scale, long displacement, string hex)
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, INS_mov, EA_8BYTE, baseReg, index, scale, (nint)displacement);
            Verify(emitter, id, insCodeRM(INS_mov), null, hex);
        });
    }

    [TestCase(EA_1BYTE, "8A00")]
    [TestCase(EA_2BYTE, "668B00")]
    [TestCase(EA_4BYTE, "8B00")]
    [TestCase(EA_8BYTE, "488B00")]
    public static void OperandWidthRetainsLegacyAndRexPrefixes(emitAttr attr, string hex)
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, INS_mov, attr, REG_RAX, REG_NA, 0, 0);
            Verify(emitter, id, insCodeRM(INS_mov), null, hex);
        });
    }

    [TestCase(REG_R20, REG_NA, 0u, "D5188B0424")]
    [TestCase(REG_R21, REG_NA, 0u, "D5188B4500")]
    [TestCase(REG_RAX, REG_R20, 0u, "D5288B0420")]
    [TestCase(REG_R20, REG_R21, 2u, "D5388B04AC")]
    public static void ApxAddressesRetainHighBaseAndIndexBits(regNumber baseReg, regNumber index, uint scale, string hex)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseRex2Encodings = true;
            var id = Address(emitter, INS_mov, EA_8BYTE, baseReg, index, scale, 0);
            Verify(emitter, id, insCodeRM(INS_mov), null, hex);
        });
    }

    [TestCase(INS_movups, EA_16BYTE, false, REG_RAX, 16L, "0F104010")]
    [TestCase(INS_movups, EA_16BYTE, true, REG_RAX, 16L, "C5F8104010")]
    [TestCase(INS_pmulld, EA_16BYTE, false, REG_RAX, 16L, "660F38404010")]
    [TestCase(INS_pmulld, EA_16BYTE, true, REG_RAX, 16L, "C4E279404010")]
    [TestCase(INS_movups, EA_64BYTE, true, REG_RAX, 64L, "62F17C48104001")]
    [TestCase(INS_movups, EA_64BYTE, true, REG_RAX, -64L, "62F17C481040FF")]
    [TestCase(INS_movups, EA_64BYTE, true, REG_RAX, 8192L, "62F17C48108000200000")]
    [TestCase(INS_movups, EA_64BYTE, true, REG_RSP, 64L, "62F17C4810442401")]
    public static void SimdAddressOutputPreservesEscapeMapsAndCompressedDisplacements(instruction ins, emitAttr attr,
        bool vex, regNumber baseReg, long displacement, string hex)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseVexEncodings = vex;
            emitter.UseEvexEncodings = attr == EA_64BYTE;
            var id = Address(emitter, ins, attr, baseReg, REG_NA, 0, (nint)displacement);
            id.idReg1(REG_XMM0);
            Verify(emitter, id, insCodeRM(ins), null, hex);
        });
    }

    [TestCase(EA_4BYTE, 127, "83407F7F")]
    [TestCase(EA_4BYTE, 128, "81407F80000000")]
    [TestCase(EA_8BYTE, -1, "4883407FFF")]
    [TestCase(EA_2BYTE, 128, "6681407F8000")]
    public static void MemoryImmediateUsesNativeSignedByteAndDwordSelection(emitAttr attr, int immediate, string hex)
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, INS_add, attr, REG_RAX, REG_NA, 0, 127);
            id.idInsFmt(IF_ARW_CNS);
            var constant = new Emitter.CnsVal { cnsVal = immediate };
            Verify(emitter, id, insCodeMI(INS_add), &constant, hex);
        });
    }

    [TestCase(INS_call, REG_RAX, false, "FF10")]
    [TestCase(INS_call, REG_R9, false, "41FF11")]
    [TestCase(INS_call, REG_R9, true, "41FFD1")]
    [TestCase(INS_tail_i_jmp, REG_RAX, false, "48FF20")]
    [TestCase(INS_tail_i_jmp, REG_R9, true, "49FFE1")]
    public static void IndirectCallsAndTailJumpsRetainUnwindRecognitionPrefix(instruction ins, regNumber reg, bool throughRegister, string hex)
    {
        WithEmitter((_, emitter) =>
        {
            var id = Address(emitter, ins, EA_8BYTE, reg, REG_NA, 0, 0);
            id.idInsFmt(IF_ARD);
            id.idSetIsCall();
            if (throughRegister)
            {
                id.idSetIsCallRegPtr();
            }
            var code = (ulong)insCodeMR(ins);
            if (ins == INS_tail_i_jmp)
            {
                // emitIns_Call includes REX.W when predicting Windows tail-jump sizes.
                code |= 0x4800000000UL;
            }
            Verify(emitter, id, code, null, hex);
        });
    }

    [TestCase(1, -1)]
    [TestCase(128, -4)]
    public static void RipRelativeRelocationIncludesFollowingImmediateWidth(int immediate, int delta)
    {
        WithEmitter((compiler, emitter) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            emitter.emitCmpHandle = &context.JitInfo;
            compiler.opts.compReloc = true;
            compiler.info.compMatchedVM = true;
            var id = Address(emitter, INS_add, EA_4BYTE | EA_DSP_RELOC_FLG, REG_NA, REG_NA, 0, 0x12345678);
            id.idInsFmt(IF_ARW_CNS);
            var constant = new Emitter.CnsVal { cnsVal = immediate };
            var buffer = stackalloc byte[32];
            var predicted = emitter.emitInsSizeAM(id, insCodeMI(INS_add), immediate);
            var end = emitter.emitOutputAM(buffer, id, insCodeMI(INS_add), &constant);

            Assert.That((uint)(end - buffer), Is.EqualTo(predicted));
            Assert.That(context.Calls, Is.EqualTo(1));
            Assert.That(context.Delta, Is.EqualTo(delta));
            Assert.That(context.Kind, Is.EqualTo(CorInfoReloc.RELATIVE32));
            Assert.That((nuint)context.Location, Is.EqualTo((nuint)(buffer + 2)));
            Assert.That((nuint)context.Target, Is.EqualTo((nuint)0x12345678));
            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(immediate == 1 ? "83050000000001" : "81050000000080000000")));
        });
    }

    [Test]
    public static void PointerLoadAndOverwriteRecordRegisterLifetimesAtInstructionEnds()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (_, codeGen, _) =>
        {
            codeGen.IsFullPtrRegMapRequired = true;
            var emitter = codeGen.Emitter;
            emitter.emitFullGCinfo = true;
            SyncThisRegister(emitter) = REG_NA;
            var born = Address(emitter, INS_mov, EA_GCREF, REG_RAX, REG_NA, 0, 0);
            var dead = Address(emitter, INS_mov, EA_8BYTE, REG_RAX, REG_NA, 0, 0);
            var buffer = stackalloc byte[32];
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputAM(buffer, born, insCodeRM(INS_mov), null);
            var birthOffset = (uint)(end - buffer);
            end = emitter.emitOutputAM(end, dead, insCodeRM(INS_mov), null);

            var record = RegPtrList(ref codeGen.GCInfo) ?? throw new AssertionException("Missing register birth.");
            Assert.That(record.rpdGCtype, Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(record.rpdOffs, Is.EqualTo(birthOffset));
            record = record.rpdNext ?? throw new AssertionException("Missing register death.");
            Assert.That(record.rpdOffs, Is.EqualTo((uint)(end - buffer)));
            Assert.That(record.rpdNext, Is.Null);
        });
    }

    private static Emitter.instrDesc Address(Emitter emitter, instruction ins, emitAttr attr,
        regNumber baseReg, regNumber index, uint scale, nint displacement)
    {
        var id = NewAddress(emitter, attr, displacement);
        id.idIns(ins);
        id.idInsFmt(IF_RWR_ARD);
        id.idReg1(REG_RAX);
        id.idAddr().iiaAddrMode.amBaseReg = baseReg;
        id.idAddr().iiaAddrMode.amIndxReg = index;
        id.idAddr().iiaAddrMode.amScale = scale;

        return id;
    }

    private static void Verify(Emitter emitter, Emitter.instrDesc id, ulong code, Emitter.CnsVal* constant, string hex)
    {
        var predicted = constant == null
            ? emitter.emitInsSizeAM(id, code)
            : emitter.emitInsSizeAM(id, code, checked((int)constant->cnsVal));
        var buffer = stackalloc byte[48];
        new Span<byte>(buffer, 48).Fill(0xA5);
        emitter.writeableOffset = 16;
        var end = emitter.emitOutputAM(buffer, id, code, constant);
        var count = (int)(end - buffer);

        Assert.That(new ReadOnlySpan<byte>(buffer + 16, count).ToArray(), Is.EqualTo(Convert.FromHexString(hex)));
        Assert.That((uint)count, Is.EqualTo(predicted));
        Assert.That(buffer[0], Is.EqualTo(0xA5));
        Assert.That(buffer[16 + count], Is.EqualTo(0xA5));
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            var emitter = codeGen.Emitter;
            emitter.UseVexEncodings = false;
            emitter.UseEvexEncodings = false;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            action(compiler, emitter);
        });
    }

    private struct RelocationContext
    {
        public ICorJitInfo JitInfo;
        public int Calls;
        public int Delta;
        public void* Location;
        public void* Target;
        public CorInfoReloc Kind;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* self, void* location, void* locationRW, void* target, CorInfoReloc kind, int delta)
    {
        var context = (RelocationContext*)self;
        context->Calls++;
        context->Location = location;
        context->Target = target;
        context->Delta = delta;
        context->Kind = kind;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNewInstrAmd")]
    private static extern Emitter.instrDesc NewAddress(Emitter emitter, emitAttr attr, nint displacement);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "gcRegPtrList")]
    private static extern ref GCInfo.regPtrDsc? RegPtrList(ref GCInfo info);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitSyncThisObjReg")]
    private static extern ref regNumber SyncThisRegister(Emitter emitter);
}
