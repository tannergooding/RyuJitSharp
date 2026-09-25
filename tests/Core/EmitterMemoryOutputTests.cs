// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterMemoryOutputTests
{
    [TestCase(INS_addps, EA_64BYTE, EA_4BYTE, EA_64BYTE)]
    [TestCase(INS_cvtps2pd, EA_64BYTE, EA_4BYTE, EA_32BYTE)]
    public static void EmbeddedBroadcastChangesMemorySizeUnlessIgnored(
        instruction ins, emitAttr size, emitAttr broadcastSize, emitAttr fullSize)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            var emitter = codeGen.Emitter;
            if (ins == INS_addps)
            {
                emitter.emitIns_R_R_S(ins, size, REG_XMM0, REG_XMM1, 0, 0, INS_OPTS_EVEX_eb);
            }
            else
            {
                emitter.emitIns_R_S(ins, size, REG_XMM0, 0, 0, INS_OPTS_EVEX_eb);
            }
            var id = Last(emitter);

            Assert.That(emitter.emitGetMemOpSize(id, false), Is.EqualTo(broadcastSize));
            Assert.That(emitter.emitGetMemOpSize(id, true), Is.EqualTo(fullSize));
        });
    }

    [TestCase(INS_mov, EA_8BYTE, EA_8BYTE)]
    [TestCase(INS_movddup, EA_16BYTE, EA_8BYTE)]
    [TestCase(INS_movddup, EA_32BYTE, EA_32BYTE)]
    [TestCase(INS_movhps, EA_16BYTE, EA_8BYTE)]
    [TestCase(INS_pmovsxbw, EA_16BYTE, EA_8BYTE)]
    [TestCase(INS_pmovsxbd, EA_32BYTE, EA_8BYTE)]
    [TestCase(INS_pmovsxbq, EA_64BYTE, EA_8BYTE)]
    public static void FixedAndFractionalTuplesUseTheirActualMemoryWidth(
        instruction ins, emitAttr size, emitAttr expected)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            var emitter = codeGen.Emitter;
            emitter.emitIns_R_S(ins, size, ins == INS_mov ? REG_RAX : REG_XMM0, 0, 0);
            var id = Last(emitter);

            Assert.That(emitter.emitGetMemOpSize(id, false), Is.EqualTo(expected));
            Assert.That(emitter.emitGetMemOpSize(id, true), Is.EqualTo(expected));
        });
    }

    [TestCase(INS_pslld, false, EA_16BYTE)]
    [TestCase(INS_pslld, true, EA_64BYTE)]
    [TestCase(INS_psllw, false, EA_16BYTE)]
    [TestCase(INS_psllw, true, EA_64BYTE)]
    public static void MixedTuplesSelectMemoryWidthFromImmediatePresence(
        instruction ins, bool hasImmediate, emitAttr expected)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            var emitter = codeGen.Emitter;
            if (hasImmediate)
            {
                emitter.emitIns_R_S_I(ins, EA_64BYTE, REG_XMM0, 0, 0, 1);
            }
            else
            {
                emitter.emitIns_R_R_S(ins, EA_64BYTE, REG_XMM0, REG_XMM1, 0, 0);
            }
            var id = Last(emitter);

            Assert.That(emitter.emitGetMemOpSize(id, false), Is.EqualTo(expected));
            Assert.That(emitter.emitGetMemOpSize(id, true), Is.EqualTo(expected));
        });
    }

    [TestCase(true, -16, 0, "488B45F0")]
    [TestCase(true, -144, 0, "488B8570FFFFFF")]
    [TestCase(false, 0, 0, "488B0424")]
    [TestCase(false, 16, 0, "488B442410")]
    [TestCase(false, 0x1234, 0, "488B842434120000")]
    public static void StackLoadsEncodeFpAndSpAddressingAndDisplacementWidths(
        bool fpBased, int frameOffset, int operandOffset, string hex)
    {
        WithStack((compiler, emitter) =>
        {
            compiler.lvaTable[0].lvFramePointerBased = fpBased;
            compiler.lvaTable[0].StackOffset = frameOffset;
            emitter.emitIns_R_S(INS_mov, EA_8BYTE, REG_RAX, 0, operandOffset);
        }, (emitter, dst, id) => emitter.emitOutputSV(dst, id, (ulong)insCodeRM(INS_mov), null), hex);
    }

    [TestCase(5, "488345F005")]
    [TestCase(128, "488145F080000000")]
    public static void StackImmediatesUseNativeSignedByteOrWideForm(int value, string hex)
    {
        WithStack((_, emitter) => emitter.emitIns_S_I(INS_add, EA_8BYTE, 0, 0, value),
            (emitter, dst, id) =>
            {
                var constant = new Emitter.CnsVal { cnsVal = value };
                return emitter.emitOutputSV(dst, id, (ulong)insCodeMI(INS_add), &constant);
            }, hex);
    }

    [Test]
    public static void LegacySimdStackLoadPreservesOpcodeAndMemoryModRm()
    {
        WithStack((_, emitter) =>
        {
            emitter.UseVexEncodings = false;
            emitter.UseEvexEncodings = false;
            emitter.emitIns_R_S(INS_movaps, EA_16BYTE, REG_XMM1, 0, 0);
        },
            (emitter, dst, id) =>
            {
                var code = (ulong)insCodeRM(INS_movaps);
                var regcode = EncodeReg345(emitter, id, REG_XMM1, EA_16BYTE, &code);
                return emitter.emitOutputSV(dst, id, code | ((ulong)regcode << 8), null);
            }, "0F284DF0");
    }

    [TestCase(TYP_LONG, false, "488B45E8")]
    [TestCase(TYP_REF, true, "488945E8")]
    public static void SpillTemporaryUsesItsAssignedFrameOffset(
        var_types type, bool gcWrite, string hex)
    {
        CodeGenSpillVariableTests.WithCompiler(type, REG_RAX, (compiler, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            codeGen.RegSet.tmpInit();
            codeGen.RegSet.tmpBeginPreAllocateTemps();
            codeGen.RegSet.tmpPreAllocateTemps(type, 1);
            var temp = codeGen.RegSet.tmpGetTemp(type);
            temp.tdTempOffs = -32;
            try
            {
                if (gcWrite)
                {
                    emitter.emitFullGCinfo = true;
                    GcFrameMin(emitter) = -24;
                    GcFrameMax(emitter) = -16;
                    GcFrameCount(emitter) = 1;
                    GcFrameLive(emitter) = new GCInfo.varPtrDsc?[1];
                    emitter.emitIns_S_R(INS_mov, EA_GCREF, REG_RAX, temp.tdTempNum, 8);
                }
                else
                {
                    emitter.emitIns_R_S(INS_mov, EA_8BYTE, REG_RAX, temp.tdTempNum, 8);
                }
                var id = Last(emitter);
                var buffer = stackalloc byte[32];
#if DEBUG
                emitter.emitIssuing = true;
#endif
                var code = gcWrite ? insCodeMR(INS_mov) : insCodeRM(INS_mov);
                var end = emitter.emitOutputSV(buffer, id, (ulong)code, null);
                Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                    Is.EqualTo(Convert.FromHexString(hex)));
                Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
                if (gcWrite)
                {
                    Assert.That(codeGen.GCInfo.gcVarPtrList, Is.Null);
                    var frameLive = GcFrameLive(emitter)
                        ?? throw new AssertionException("The tracked GC frame was not initialized.");
                    Assert.That(frameLive[0], Is.Null);
                }
            }
            finally
            {
                codeGen.RegSet.tmpRlsTemp(temp);
            }
        });
    }

    [Test]
    public static void ApxNddStackLoadKeepsEvexAndFrameDisplacement()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            emitter.UseRex2Encodings = true;
            emitter.UsePromotedEvexEncodings = true;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            emitter.emitIns_R_R_S(INS_add, EA_8BYTE, REG_R16, REG_R17, 0, 0, INS_OPTS_EVEX_nd);
            var id = Last(emitter);

            var code = Prefix(emitter, id, (ulong)insCodeRM(INS_add), EA_8BYTE);
            code = EncodeReg3456(emitter, id, REG_R16, EA_8BYTE, code);
            var regcode = EncodeReg345(emitter, id, REG_R17, EA_8BYTE, &code);
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputSV(buffer, id, code | ((ulong)regcode << 8), null);
            var encoded = new ReadOnlySpan<byte>(buffer, (int)(end - buffer));

            Assert.That(encoded[0], Is.EqualTo(0x62));
            Assert.That(encoded[^2], Is.EqualTo(0x4D));
            Assert.That(encoded[^1], Is.EqualTo(0xF0));
            Assert.That(encoded.Length, Is.EqualTo(id.idCodeSize()));
        });
    }

    [Test]
    public static void EvexStackDisplacementUsesItsTupleScale()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.lvaTable[0].StackOffset = -128;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            var emitter = codeGen.Emitter;
            emitter.UseVexEncodings = true;
            emitter.UseEvexEncodings = true;
            emitter.emitIns_R_R_S(INS_addps, EA_64BYTE, REG_XMM16, REG_XMM17, 0, 0);
            var id = Last(emitter);

            var code = Prefix(emitter, id, (ulong)insCodeRM(INS_addps), EA_64BYTE);
            code = EncodeReg3456(emitter, id, REG_XMM17, EA_64BYTE, code);
            var regcode = EncodeReg345(emitter, id, REG_XMM16, EA_64BYTE, &code);
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputSV(buffer, id, code | ((ulong)regcode << 8), null);
            var encoded = new ReadOnlySpan<byte>(buffer, (int)(end - buffer));

            Assert.That(encoded[0], Is.EqualTo(0x62));
            Assert.That(encoded[^1], Is.EqualTo(0xFE));
            Assert.That(encoded.Length, Is.EqualTo(id.idCodeSize()));
        });
    }

    [Test]
    public static void GcStackWriteRecordsTheStackSlotAtTheIssuedOffset()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_REF, REG_RAX, (compiler, codeGen, _) =>
        {
            var emitter = codeGen.Emitter;
            emitter.emitFullGCinfo = true;
            GcFrameMin(emitter) = -16;
            GcFrameMax(emitter) = 0;
            GcFrameCount(emitter) = 2;
            GcFrameLive(emitter) = new GCInfo.varPtrDsc?[2];
            emitter.emitIns_S_R(INS_mov, EA_GCREF, REG_RAX, 0, 0);
            var id = Last(emitter);
            var buffer = stackalloc byte[32];
            emitter.emitCodeBlock = buffer;
            emitter.emitTotalHotCodeSize = 32;
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var code = (ulong)insCodeMR(INS_mov);
            var regcode = EncodeReg345(emitter, id, REG_RAX, EA_8BYTE, &code);
            var end = emitter.emitOutputSV(buffer, id, code | ((ulong)regcode << 8), null);
            var descriptor = codeGen.GCInfo.gcVarPtrList
                ?? throw new AssertionException("The stack write did not record a GC slot.");
            var live = GcFrameLive(emitter)
                ?? throw new AssertionException("The tracked GC frame was not initialized.");

            Assert.That(descriptor.vpdBegOfs, Is.EqualTo((uint)(end - buffer)));
            Assert.That(descriptor.vpdVarNum, Is.EqualTo(unchecked((uint)-16)));
            Assert.That(live[0], Is.SameAs(descriptor));
        });
    }

    [Test]
    public static void ConstantDataOffsetSearchPreservesChunkBoundaryIdentity()
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var chunks = stackalloc AllocMemChunk[2];
            var offsets = stackalloc int[2];
            var first = stackalloc byte[32];
            var second = stackalloc byte[32];
            chunks[0] = new AllocMemChunk { size = 32, block = first };
            chunks[1] = new AllocMemChunk { size = 32, block = second };
            offsets[0] = 0;
            offsets[1] = 32;
            emitter.emitConsDsc.dsdOffs = 64;
            emitter.emitDataChunks = chunks;
            emitter.emitDataChunkOffsets = offsets;
            emitter.emitNumDataChunks = 2;

            Assert.That((nuint)emitter.emitDataOffsetToPtr(0), Is.EqualTo((nuint)first));
            Assert.That((nuint)emitter.emitDataOffsetToPtr(31), Is.EqualTo((nuint)(first + 31)));
            Assert.That((nuint)emitter.emitDataOffsetToPtr(32), Is.EqualTo((nuint)second));
            Assert.That((nuint)emitter.emitDataOffsetToPtr(63), Is.EqualTo((nuint)(second + 31)));
        });
    }

    [TestCase(false, "64488B0510000000")]
    [TestCase(true, "65488B052510000000")]
    public static void SegmentStaticLoadsPreserveFsGsPrefixesAndDisplacements(bool gs, string hex)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            var emitter = codeGen.Emitter;
            emitter.emitIns_R_C(INS_mov, EA_8BYTE, REG_RAX,
                gs ? FLD_GLOBAL_GS : FLD_GLOBAL_FS, 16);
            var id = Last(emitter);
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputCV(buffer, id, (ulong)insCodeRM(INS_mov) | 0x0500, null);
            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
        });
    }

    [TestCase(32, 8, 8)]
    [TestCase(40, -8, 0)]
    public static void DataChunkStaticLoadUsesRipRelocationToTheAllocatedChunk(
        int dataOffset, int displacement, int targetOffset)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            compiler.info.compMatchedVM = true;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            var emitter = codeGen.Emitter;
            emitter.emitCmpHandle = &context.JitInfo;
            var chunks = stackalloc AllocMemChunk[2];
            var offsets = stackalloc int[2];
            var first = stackalloc byte[32];
            var second = stackalloc byte[32];
            chunks[0] = new AllocMemChunk { size = 32, block = first };
            chunks[1] = new AllocMemChunk { size = 32, block = second };
            offsets[0] = 0;
            offsets[1] = 32;
            emitter.emitConsDsc.dsdOffs = 64;
            emitter.emitDataChunks = chunks;
            emitter.emitDataChunkOffsets = offsets;
            emitter.emitNumDataChunks = 2;
            emitter.emitIns_R_C(INS_mov, EA_8BYTE, REG_RAX,
                Compiler.eeFindJitDataOffs((uint)dataOffset), displacement);
            var id = Last(emitter);
            Assert.That(id.idIsLargeDsp(), Is.True);
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputCV(buffer, id, (ulong)insCodeRM(INS_mov) | 0x0500, null);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString("488B0500000000")));
            Assert.That((nuint)context.Location, Is.EqualTo((nuint)(buffer + 3)));
            Assert.That((nuint)context.Target, Is.EqualTo((nuint)(second + targetOffset)));
            Assert.That(context.Kind, Is.EqualTo(CorInfoReloc.RELATIVE32));
            Assert.That(context.Delta, Is.Zero);
            Assert.That(context.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public static void StaticSimdImmediateRelocationAccountsForTheFollowingByte()
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            compiler.info.compMatchedVM = true;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            var emitter = codeGen.Emitter;
            emitter.emitCmpHandle = &context.JitInfo;
            var chunks = stackalloc AllocMemChunk[1];
            var offsets = stackalloc int[1];
            var data = stackalloc byte[64];
            chunks[0] = new AllocMemChunk { size = 64, block = data };
            offsets[0] = 0;
            emitter.emitConsDsc.dsdOffs = 64;
            emitter.emitDataChunks = chunks;
            emitter.emitDataChunkOffsets = offsets;
            emitter.emitNumDataChunks = 1;
            emitter.emitIns_R_C_I(INS_pshufd, EA_16BYTE, REG_XMM0, Compiler.eeFindJitDataOffs(0), 8, 7);
            var id = Last(emitter);
            var constant = new Emitter.CnsVal { cnsVal = 7 };
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = emitter.emitOutputCV(buffer, id, (ulong)insCodeRM(INS_pshufd) | 0x0500, &constant);

            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer))[^1], Is.EqualTo(7));
            Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
            Assert.That((nuint)context.Location, Is.EqualTo((nuint)(end - 5)));
            Assert.That((nuint)context.Target, Is.EqualTo((nuint)(data + 8)));
            Assert.That(context.Kind, Is.EqualTo(CorInfoReloc.RELATIVE32));
            Assert.That(context.Delta, Is.EqualTo(-1));
            Assert.That(context.Calls, Is.EqualTo(1));
        });
    }

    private delegate byte* StackOutput(Emitter emitter, byte* dst, Emitter.instrDesc id);

    private static void WithStack(Action<Compiler, Emitter> record, StackOutput output, string hex)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_CORECLR_ABI;
            var emitter = codeGen.Emitter;
            record(compiler, emitter);
            var id = Last(emitter);
            var buffer = stackalloc byte[32];
#if DEBUG
            emitter.emitIssuing = true;
#endif
            var end = output(emitter, buffer, id);
            Assert.That(new ReadOnlySpan<byte>(buffer, (int)(end - buffer)).ToArray(),
                Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That((uint)(end - buffer), Is.EqualTo(id.idCodeSize()));
        });
    }

    private static Emitter.instrDesc Last(Emitter emitter) =>
        LastInstruction(emitter) ?? throw new AssertionException("No recorded memory instruction.");

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "AddX86PrefixIfNeeded")]
    private static extern ulong Prefix(Emitter emitter, Emitter.instrDesc id, ulong code, emitAttr size);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insEncodeReg345")]
    private static extern uint EncodeReg345(Emitter emitter, Emitter.instrDesc id, regNumber reg,
        emitAttr size, ulong* code);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insEncodeReg3456")]
    private static extern ulong EncodeReg3456(Emitter emitter, Emitter.instrDesc id, regNumber reg,
        emitAttr size, ulong code);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsMin")]
    private static extern ref int GcFrameMin(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsMax")]
    private static extern ref int GcFrameMax(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameOffsCnt")]
    private static extern ref int GcFrameCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitGCrFrameLiveTab")]
    private static extern ref GCInfo.varPtrDsc?[]? GcFrameLive(Emitter emitter);

    private struct RelocationContext
    {
        public ICorJitInfo JitInfo;
        public void* Location;
        public void* Target;
        public CorInfoReloc Kind;
        public int Delta;
        public int Calls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* info, void* location, void* locationRW,
        void* target, CorInfoReloc kind, int delta)
    {
        var context = (RelocationContext*)info;
        context->Location = location;
        context->Target = target;
        context->Kind = kind;
        context->Delta = delta;
        context->Calls++;
    }
}
