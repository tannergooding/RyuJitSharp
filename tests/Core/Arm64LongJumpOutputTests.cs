// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64LongJumpOutputTests
{
    private delegate void OutputTest(Compiler compiler, Emitter emitter, byte* code, CallbackContext* callbacks);

    [TestCase(INS_b, IF_BI_0A, EA_4BYTE, 0, 0x14000008u)]
    [TestCase(INS_bl_local, IF_BI_0A, EA_4BYTE, 0, 0x94000008u)]
    [TestCase(INS_beq, IF_BI_0B, EA_4BYTE, 0, 0x54000100u)]
    [TestCase(INS_bne, IF_BI_0B, EA_4BYTE, 0, 0x54000101u)]
    [TestCase(INS_cbz, IF_BI_1A, EA_4BYTE, 0, 0x34000103u)]
    [TestCase(INS_cbnz, IF_BI_1A, EA_8BYTE, 0, 0xB5000103u)]
    [TestCase(INS_tbz, IF_BI_1B, EA_4BYTE, 0, 0x36000103u)]
    [TestCase(INS_tbz, IF_BI_1B, EA_4BYTE, 31, 0x36F80103u)]
    [TestCase(INS_tbnz, IF_BI_1B, EA_8BYTE, 32, 0xB7000103u)]
    [TestCase(INS_tbnz, IF_BI_1B, EA_8BYTE, 63, 0xB7F80103u)]
    public static void ShortBranchesPreserveNativeFormatsAndForwardPatching(
        instruction ins, Emitter.insFormat format, emitAttr size, int bit, uint expected)
    {
        WithEmitter((_, emitter, code, callbacks) =>
        {
            var id = View.Jump(ins, format, size, shortJump: true, target: 48, immediate: bit);
            var end = Output(emitter, null, code + 16, id);

            Assert.That((nuint)end, Is.EqualTo((nuint)(code + 20)));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16), Is.EqualTo(expected));
            View.CheckPatch(id, code + 16, 48);
            Assert.That(ForwardJumps(emitter), Is.True);
            Assert.That(callbacks->Count, Is.Zero);
            Assert.That(code[15], Is.EqualTo(0xA5));
            Assert.That(code[20], Is.EqualTo(0xA5));
        });
    }

    [TestCase(INS_beq, 0x54000041u)]
    [TestCase(INS_bne, 0x54000040u)]
    [TestCase(INS_bhs, 0x54000043u)]
    [TestCase(INS_blo, 0x54000042u)]
    [TestCase(INS_bmi, 0x54000045u)]
    [TestCase(INS_bpl, 0x54000044u)]
    [TestCase(INS_bvs, 0x54000047u)]
    [TestCase(INS_bvc, 0x54000046u)]
    [TestCase(INS_bhi, 0x54000049u)]
    [TestCase(INS_bls, 0x54000048u)]
    [TestCase(INS_bge, 0x5400004Bu)]
    [TestCase(INS_blt, 0x5400004Au)]
    [TestCase(INS_bgt, 0x5400004Du)]
    [TestCase(INS_ble, 0x5400004Cu)]
    [TestCase(INS_cbz, 0xB5000043u)]
    [TestCase(INS_cbnz, 0xB4000043u)]
    [TestCase(INS_tbz, 0xB7F80043u)]
    [TestCase(INS_tbnz, 0xB6F80043u)]
    public static void LongConditionalsInvertAroundBAndRebaseTheSecondWord(instruction ins, uint inverse)
    {
        WithEmitter((_, emitter, code, callbacks) =>
        {
            var id = View.Jump(ins, IF_LARGEJMP, EA_8BYTE, shortJump: false, target: 48, immediate: 63);
            var end = Output(emitter, new insGroup(), code + 16, id);

            Assert.That((nuint)end, Is.EqualTo((nuint)(code + 24)));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16), Is.EqualTo(inverse));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 20), Is.EqualTo(0x14000007u));
            View.CheckPatch(id, code + 16, 48);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_LARGEJMP));
            Assert.That(callbacks->Count, Is.Zero);
            Assert.That(code[24], Is.EqualTo(0xA5));
        });
    }

    [TestCase(true, 0u, 0x17FFFFFCu)]
    [TestCase(false, 0u, 0x17FFFFFCu)]
    [TestCase(true, 16u, 0x14000000u)]
    [TestCase(false, 16u, 0x14000000u)]
    public static void BackwardUnconditionalBranchesDoNotRequestForwardPatching(
        bool shortJump, uint target, uint expected)
    {
        WithEmitter((_, emitter, code, _) =>
        {
            OffsetAdjustment(emitter) = 4;
            var id = View.Jump(INS_b, IF_BI_0A, EA_4BYTE, shortJump, target);
            var end = Output(emitter, null, code + 16, id);

            Assert.That((nuint)end, Is.EqualTo((nuint)(code + 20)));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16), Is.EqualTo(expected));
            View.CheckPatch(id, null, 0);
            Assert.That(ForwardJumps(emitter), Is.False);
        });
    }

    [TestCase(4, 44u, 0x14000007u)]
    [TestCase(-4, 52u, 0x14000009u)]
    public static void SameSectionForwardOffsetsUseUnsignedAdjustment(int adjustment, uint target, uint expected)
    {
        WithEmitter((_, emitter, code, _) =>
        {
            OffsetAdjustment(emitter) = adjustment;
            var id = View.Jump(INS_b, IF_BI_0A, EA_4BYTE, shortJump: false, target: 48);

            _ = Output(emitter, null, code + 16, id);

            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16), Is.EqualTo(expected));
            View.CheckPatch(id, code + 16, target);
        });
    }

    [Test]
    public static void HotColdBranchesUseActualAddressesOrRelocationsWithoutOffsetAdjustment(
        [Values(false, true)] bool conditional, [Values(false, true)] bool relocatable,
        [Values(false, true)] bool matched)
    {
        WithEmitter((compiler, emitter, code, callbacks) =>
        {
            compiler.opts.compReloc = relocatable;
            compiler.info.compMatchedVM = matched;
            emitter.emitTotalHotCodeSize = 32;
            emitter.emitTotalColdCodeSize = 32;
            emitter.emitColdCodeBlock = code + 256;
            OffsetAdjustment(emitter) = 4;
            emitter.writeableOffset = 64;
            var id = View.Jump(conditional ? INS_beq : INS_b, conditional ? IF_LARGEJMP : IF_BI_0A,
                EA_4BYTE, shortJump: false, target: 40, keepLong: true);
            var end = Output(emitter, null, code + 16, id);

            var branchOffset = conditional ? 20 : 16;
            var word = relocatable ? 0x14000000u : conditional ? 0x1400003Du : 0x1400003Eu;
            Assert.That((nuint)end, Is.EqualTo((nuint)(code + branchOffset + 4)));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + branchOffset + 64), Is.EqualTo(word));
            if (conditional)
            {
                Assert.That(Unsafe.ReadUnaligned<uint>(code + 80), Is.EqualTo(0x54000041u));
            }
            View.CheckPatch(id, code + 16, 40);
            Assert.That(callbacks->Count, Is.EqualTo(relocatable && matched ? 1 : 0));
            if (relocatable && matched)
            {
                AssertRelocation(callbacks->First, code + branchOffset, code + branchOffset + 64,
                    code + 264, CorInfoReloc.ARM64_BRANCH26, word);
            }
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16), Is.EqualTo(0xA5A5A5A5u));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AddressLabelsPreservePcAndPageRelativeEncoding(bool shortAddress)
    {
        WithEmitter((_, emitter, code, callbacks) =>
        {
            var id = View.Jump(INS_adr, shortAddress ? IF_DI_1E : IF_LARGEADR, EA_8BYTE,
                shortAddress, target: 48);
            var end = Output(emitter, null, code + 16, id);

            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16),
                Is.EqualTo(shortAddress ? 0x10000103u : 0x90000003u));
            if (!shortAddress)
            {
                Assert.That(Unsafe.ReadUnaligned<uint>(code + 20), Is.EqualTo(0x9100C063u));
            }
            Assert.That((nuint)end, Is.EqualTo((nuint)(code + (shortAddress ? 20 : 24))));
            View.CheckPatch(id, code + 16, 48);
            Assert.That(callbacks->Count, Is.Zero);
        });
    }

    [TestCase(EA_4BYTE, REG_R3, REG_NA, 0xB0000003u, 0xB9402063u, 0u, 8)]
    [TestCase(EA_8BYTE, REG_R3, REG_NA, 0xB0000003u, 0xF9401063u, 0u, 8)]
    [TestCase(EA_4BYTE, REG_V2, REG_R9, 0xB0000009u, 0xB9402129u, 0x1E270122u, 12)]
    [TestCase(EA_8BYTE, REG_V2, REG_R9, 0xB0000009u, 0xF9401129u, 0x9E670122u, 12)]
    [TestCase(EA_16BYTE, REG_V2, REG_R9, 0xB0000009u, 0x91008129u, 0x4C407122u, 12)]
    public static void LongDataLoadsPreserveScalarAndVectorInstructionSequences(
        emitAttr size, regNumber destination, regNumber scratch, uint first, uint second, uint third, int length)
    {
        WithEmitter((_, emitter, code, callbacks) =>
        {
            var offsets = stackalloc int[1];
            var chunks = stackalloc AllocMemChunk[1];
            offsets[0] = 0;
            chunks[0] = new AllocMemChunk { block = code + 4096 + 32, size = 64 };
            SetDataChunks(emitter, offsets, chunks, 1, 64);
            var id = View.Data(INS_ldr, IF_LARGELDC, size, destination, scratch, shortAddress: false);

            var end = Output(emitter, null, code + 16, id);

            Assert.That((nuint)end, Is.EqualTo((nuint)(code + 16 + length)));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16), Is.EqualTo(first));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 20), Is.EqualTo(second));
            if (length == 12)
            {
                Assert.That(Unsafe.ReadUnaligned<uint>(code + 24), Is.EqualTo(third));
            }
            Assert.That(callbacks->Count, Is.Zero);
            Assert.That(ForwardJumps(emitter), Is.False);
            Assert.That(code[16 + length], Is.EqualTo(0xA5));
        });
    }

    [TestCase(REG_R3, EA_4BYTE, 0x18000103u)]
    [TestCase(REG_R3, EA_8BYTE, 0x58000103u)]
    [TestCase(REG_V2, EA_4BYTE, 0x1C000102u)]
    [TestCase(REG_V2, EA_8BYTE, 0x5C000102u)]
    [TestCase(REG_V2, EA_16BYTE, 0x9C000102u)]
    public static void ShortLiteralLoadsUseTheSelectedDataChunkAndEncodedRegister(
        regNumber destination, emitAttr size, uint expected)
    {
        WithEmitter((_, emitter, code, _) =>
        {
            var offsets = stackalloc int[2];
            var chunks = stackalloc AllocMemChunk[2];
            offsets[0] = 0;
            offsets[1] = 16;
            chunks[0] = new AllocMemChunk { block = code + 96, size = 16 };
            chunks[1] = new AllocMemChunk { block = code + 40, size = 32 };
            SetDataChunks(emitter, offsets, chunks, 2, 48);
            var id = View.Data(INS_ldr, IF_LS_1A, size, destination, REG_NA,
                shortAddress: true, dataOffset: 16, immediate: 8);

            var end = Output(emitter, null, code + 16, id);

            Assert.That((nuint)end, Is.EqualTo((nuint)(code + 20)));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16), Is.EqualTo(expected));
            View.CheckPatch(id, null, 0);
            Assert.That(ForwardJumps(emitter), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DataAddressRelocationsFollowTheirEmittedWordsInOrder(bool matched)
    {
        WithEmitter((compiler, emitter, code, callbacks) =>
        {
            compiler.info.compMatchedVM = matched;
            emitter.writeableOffset = 64;
            var offsets = stackalloc int[1];
            var chunks = stackalloc AllocMemChunk[1];
            offsets[0] = 0;
            chunks[0] = new AllocMemChunk { block = code + 4096 + 32, size = 64 };
            SetDataChunks(emitter, offsets, chunks, 1, 64);
            var id = View.Data(INS_adr, IF_LARGEADR, EA_8BYTE, REG_R3, REG_NA,
                shortAddress: false, relocatable: true);

            var end = Output(emitter, null, code + 16, id);

            Assert.That((nuint)end, Is.EqualTo((nuint)(code + 24)));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 80), Is.EqualTo(0x90000003u));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 84), Is.EqualTo(0x91000063u));
            Assert.That(callbacks->Count, Is.EqualTo(matched ? 2 : 0));
            if (matched)
            {
                AssertRelocation(callbacks->First, code + 16, code + 80, code + 4096 + 32,
                    CorInfoReloc.ARM64_PAGEBASE_REL21, 0x90000003u);
                AssertRelocation(callbacks->Second, code + 20, code + 84, code + 4096 + 32,
                    CorInfoReloc.ARM64_PAGEOFFSET_12A, 0x91000063u);
            }
            View.CheckPatch(id, null, 0);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NonRelocatableDataAddressesUseRealPcOrPageDisplacements(bool shortAddress)
    {
        WithEmitter((_, emitter, code, callbacks) =>
        {
            var offsets = stackalloc int[1];
            var chunks = stackalloc AllocMemChunk[1];
            offsets[0] = 0;
            chunks[0] = new AllocMemChunk { block = shortAddress ? code + 48 : code + 4096 + 32, size = 32 };
            SetDataChunks(emitter, offsets, chunks, 1, 32);
            var id = View.Data(INS_adr, shortAddress ? IF_DI_1E : IF_LARGEADR, EA_8BYTE,
                REG_R3, REG_NA, shortAddress);

            var end = Output(emitter, null, code + 16, id);

            Assert.That((nuint)end, Is.EqualTo((nuint)(code + (shortAddress ? 20 : 24))));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16),
                Is.EqualTo(shortAddress ? 0x10000103u : 0xB0000003u));
            if (!shortAddress)
            {
                Assert.That(Unsafe.ReadUnaligned<uint>(code + 20), Is.EqualTo(0x91008063u));
            }
            Assert.That(callbacks->Count, Is.Zero);
            View.CheckPatch(id, null, 0);
        });
    }

    [TestCase(INS_b, IF_BI_0A, -134217728, 0x16000000u)]
    [TestCase(INS_b, IF_BI_0A, 134217724, 0x15FFFFFFu)]
    [TestCase(INS_beq, IF_BI_0B, -1048576, 0x54800000u)]
    [TestCase(INS_beq, IF_BI_0B, 1048572, 0x547FFFE0u)]
    [TestCase(INS_tbz, IF_BI_1B, -32768, 0x36040003u)]
    [TestCase(INS_tbz, IF_BI_1B, 32764, 0x3603FFE3u)]
    public static void ShortBranchSupportEncodesBothSignedRangeEndpoints(
        instruction ins, Emitter.insFormat format, int displacement, uint expected)
    {
        WithEmitter((_, emitter, code, _) =>
        {
            var id = View.Jump(ins, format, EA_4BYTE, shortJump: true, target: 0);

            var end = View.ShortBranch(emitter, code + 16, ins, format, displacement, id);

            Assert.That((nuint)end, Is.EqualTo((nuint)(code + 20)));
            Assert.That(Unsafe.ReadUnaligned<uint>(code + 16), Is.EqualTo(expected));
        });
    }

    [Test]
    public static void PageDeltaPreservesUnsignedSubtractionBeforeSignedConversion()
    {
        Assert.That(PageDelta(null, 0x4000, 0x1000), Is.EqualTo((nint)3));
        Assert.That(PageDelta(null, 0x1000, 0x4000), Is.EqualTo((nint)(-3)));
        Assert.That(PageDelta(null, 0x1FFF, 0x1000), Is.EqualTo((nint)0));
        Assert.That(PageDelta(null, 0, nuint.MaxValue), Is.EqualTo(unchecked(-(nint)(nuint.MaxValue >> 12))));
    }

    private static void SetDataChunks(Emitter emitter, int* offsets, AllocMemChunk* chunks, int count, uint size)
    {
        emitter.emitDataChunks = chunks;
        emitter.emitDataChunkOffsets = offsets;
        emitter.emitNumDataChunks = count;
        emitter.emitConsDsc.dsdOffs = size;
    }

    private static void WithEmitter(OutputTest action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.recordRelocation = &RecordRelocation;
        var callbacks = new CallbackContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
#if DEBUG
        using var tls = new JitTls(&callbacks.JitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        JitTls.Compiler = compiler;
        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
#if LATE_DISASM
            codeGen.Disassembler.disInit(compiler);
#endif
            var emitter = codeGen.Emitter;
            emitter.emitBegCG(compiler, default);
            emitter.Init();
            var allocation = stackalloc byte[16384];
            new Span<byte>(allocation, 16384).Fill(0xA5);
            var code = (byte*)(((nuint)allocation + 4095) & ~(nuint)4095);
            emitter.emitCodeBlock = code;
            emitter.emitTotalHotCodeSize = 128;
            emitter.emitCmpHandle = &callbacks.JitInfo;
            action(compiler, emitter, code, &callbacks);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static void AssertRelocation(Relocation entry, byte* location, byte* writable, byte* target,
        CorInfoReloc kind, uint word)
    {
        Assert.That((nuint)entry.Location, Is.EqualTo((nuint)location));
        Assert.That((nuint)entry.Writable, Is.EqualTo((nuint)writable));
        Assert.That((nuint)entry.Target, Is.EqualTo((nuint)target));
        Assert.That(entry.Kind, Is.EqualTo(kind));
        Assert.That(entry.Word, Is.EqualTo(word));
        Assert.That(entry.Delta, Is.Zero);
    }

    private abstract class View(CodeGen codeGen) : Emitter(codeGen)
    {
        public static instrDescJmp Jump(instruction ins, insFormat format, emitAttr size, bool shortJump,
            uint target, int immediate = 0, bool keepLong = false)
        {
            var id = Create(ins, format, size, REG_R3, REG_NA, immediate);
            id.idjShort = shortJump;
            id.idjKeepLong = keepLong;
            id.idjTargetIG = new insGroup { igOffs = target };

            return id;
        }

        public static instrDescJmp Data(instruction ins, insFormat format, emitAttr size,
            regNumber destination, regNumber scratch, bool shortAddress, int dataOffset = 0,
            int immediate = 0, bool relocatable = false)
        {
            var id = Create(ins, format, size, destination, scratch, immediate);
            id.idjShort = shortAddress;
            id.idAddr().iiaFieldHnd = Compiler.eeFindJitDataOffs(unchecked((uint)dataOffset));
            if (relocatable)
            {
                id.idSetIsDspReloc();
            }

            return id;
        }

        private static instrDescJmp Create(instruction ins, insFormat format, emitAttr size,
            regNumber destination, regNumber scratch, int immediate)
        {
            var id = new instrDescJmp();
            id.idIns(ins);
            id.idInsFmt(format);
            id.idOpSize(size);
            id.idReg1(destination);
            if (scratch != REG_NA)
            {
                id.idReg2(scratch);
            }
            id.idSmallCns(immediate);
#if DEBUG
            id.idDebugOnlyInfo(new instrDescDebugInfo());
#endif

            return id;
        }

        public static void CheckPatch(instrDesc descriptor, byte* address, uint offset)
        {
            var id = (instrDescJmp)descriptor;
            Assert.That((nuint)id.idjAddr, Is.EqualTo((nuint)address));
            Assert.That(id.idjOffs, Is.EqualTo(offset));
        }

        public static byte* ShortBranch(Emitter emitter, byte* dst, instruction ins, insFormat format,
            nint displacement, instrDesc id)
        {
            return OutputShortBranch(emitter, dst, ins, format, displacement, (instrDescJmp)id);
        }

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitOutputShortBranchArm64Core")]
        private static extern byte* OutputShortBranch(
            Emitter emitter, byte* dst, instruction ins, insFormat format, nint displacement, instrDescJmp id);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitOutputLJ")]
    private static extern byte* Output(Emitter emitter, insGroup? group, byte* dst, Emitter.instrDesc id);
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "computeRelPageAddrArm64")]
    private static extern nint PageDelta(Emitter? emitter, nuint target, nuint source);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitOffsAdj")]
    private static extern ref int OffsetAdjustment(Emitter emitter);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitFwdJumps")]
    private static extern ref bool ForwardJumps(Emitter emitter);

    private struct Relocation
    {
        public void* Location;
        public void* Writable;
        public void* Target;
        public CorInfoReloc Kind;
        public uint Word;
        public int Delta;
    }

    private struct CallbackContext
    {
        public ICorJitInfo JitInfo;
        public int Count;
        public Relocation First;
        public Relocation Second;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* info, void* location, void* writable, void* target,
        CorInfoReloc kind, int delta)
    {
        var context = (CallbackContext*)info;
        var entry = new Relocation
        {
            Location = location,
            Writable = writable,
            Target = target,
            Kind = kind,
            Word = Unsafe.ReadUnaligned<uint>(writable),
            Delta = delta,
        };
        if (context->Count == 0)
        {
            context->First = entry;
        }
        else
        {
            context->Second = entry;
        }
        context->Count++;
    }
}
#endif
