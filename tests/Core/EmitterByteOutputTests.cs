// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.instruction;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterByteOutputTests
{
    [TestCase(1, -1L, "FF", 16)]
    [TestCase(2, -2L, "FEFF", 16)]
    [TestCase(2, 0x12345678L, "7856", 16)]
    [TestCase(4, 0xFEDCBA98L, "98BADCFE", 16)]
    [TestCase(8, 0x123456789ABCDEFL, "EFCDAB8967452301", 16)]
    [TestCase(8, -1L, "FFFFFFFFFFFFFFFF", 16)]
    [TestCase(1, -1L, "FF", -16)]
    [TestCase(2, -2L, "FEFF", -16)]
    [TestCase(4, 0xFEDCBA98L, "98BADCFE", -16)]
    [TestCase(8, -1L, "FFFFFFFFFFFFFFFF", -16)]
    public static void IntegerOutputUsesUnalignedWritableAliasAndNativeTruncation(int width, long value, string hex, int aliasOffset)
    {
        CodeGenBinaryTests.WithCodeGen((_, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var buffer = stackalloc byte[40];
            new Span<byte>(buffer, 40).Fill(0xA5);
            emitter.writeableOffset = aliasOffset;
            var dst = buffer + (aliasOffset < 0 ? 19 : 3);
            var writableIndex = (int)(dst - buffer) + aliasOffset;
            var count = width switch
            {
                1 => emitter.emitOutputByte(dst, value),
                2 => emitter.emitOutputWord(dst, value),
                4 => emitter.emitOutputLong(dst, value),
                _ => emitter.emitOutputSizeT(dst, value),
            };

            Assert.That(count, Is.EqualTo(width));
            Assert.That(new ReadOnlySpan<byte>(buffer + writableIndex, count).ToArray(), Is.EqualTo(Convert.FromHexString(hex)));
            for (var i = 0; i < 40; i++)
            {
                if ((i < writableIndex) || (i >= writableIndex + count))
                {
                    Assert.That(buffer[i], Is.EqualTo(0xA5));
                }
            }
        });
    }

    [TestCase(INS_mov, 0x000000000000C08BUL, "", 0xC08BUL)]
    [TestCase(INS_mov, 0x000000480000C08BUL, "48", 0xC08BUL)]
    [TestCase(INS_mov, 0x000000480066C00FUL, "66", 0x48C00FUL)]
    [TestCase(INS_mov, 0x000000480F66C038UL, "66", 0x0F48C038UL)]
    [TestCase(INS_mov, 0x0000D5100000C08BUL, "D510", 0xC08BUL)]
    [TestCase(INS_mov, 0x0000D5800000800FUL, "D580", 0x80UL)]
    [TestCase(INS_mov, 0x0000D580000FC08BUL, "D580", 0xC08BUL)]
    [TestCase(INS_mov, 0x0000D5080066C08BUL, "66D508", 0xC08BUL)]
    [TestCase(INS_mov, 0x0000D5800F66C0B8UL, "66D580", 0xC0B8UL)]
    [TestCase(INS_addps, 0x00C4E078000FC058UL, "C5F8", 0xC058UL)]
    [TestCase(INS_addps, 0x00C46078000FC058UL, "C578", 0xC058UL)]
    [TestCase(INS_addps, 0x00C4C078000FC058UL, "C4C178", 0xC058UL)]
    [TestCase(INS_addps, 0x00C4E0F8000FC058UL, "C4E1F8", 0xC058UL)]
    [TestCase(INS_addpd, 0x00C4E0780F66C058UL, "C5F9", 0xC058UL)]
    [TestCase(INS_addss, 0x00C4E0780FF3C058UL, "C5FA", 0xC058UL)]
    [TestCase(INS_addsd, 0x00C4E0780FF2C058UL, "C5FB", 0xC058UL)]
    [TestCase(INS_paddd, 0x00C4E0780F66C038UL, "C4E279", 0xC000UL)]
    [TestCase(INS_paddd, 0x00C4E0780F66C03AUL, "C4E379", 0xC000UL)]
    [TestCase(INS_mulx, 0x00C4E0780F66C038UL, "C4E27B", 0xC000UL)]
    [TestCase(INS_sarx, 0x00C4E0780F66C038UL, "C4E27A", 0xC000UL)]
    [TestCase(INS_shlx, 0x00C4E0780F66C038UL, "C4E279", 0xC000UL)]
    [TestCase(INS_andn, 0x00C4E0780F66C038UL, "C4E278", 0xC000UL)]
    [TestCase(INS_addps, 0x62F07C08000FC058UL, "62F17C08", 0xC058UL)]
    [TestCase(INS_paddd, 0x62F07C480F66C0FEUL, "62F17D48", 0xC0FEUL)]
    [TestCase(INS_addss, 0x62F07C080FF3C058UL, "62F17E08", 0xC058UL)]
    [TestCase(INS_addsd, 0x62F07C080FF2C058UL, "62F17F08", 0xC058UL)]
    [TestCase(INS_paddd, 0x62F07C480F66C038UL, "62F27D48", 0xC000UL)]
    [TestCase(INS_paddd, 0x62F07C480F66C03AUL, "62F37D48", 0xC000UL)]
    [TestCase(INS_mulx, 0x62F07C080F66C038UL, "62F27F08", 0xC000UL)]
    [TestCase(INS_sarx, 0x62F07C080F66C038UL, "62F27E08", 0xC000UL)]
    [TestCase(INS_shlx, 0x62F07C080F66C038UL, "62F27D08", 0xC000UL)]
    [TestCase(INS_andn, 0x62F07C080F66C038UL, "62F27C08", 0xC000UL)]
    [TestCase(INS_add, 0x62F47C08000FC001UL, "62F47C08", 0xC001UL)]
    [TestCase(INS_add, 0x62F47C080000C001UL, "62F47C08", 0xC001UL)]
    [TestCase(INS_add, 0x62F07C080004C001UL, "62F47C08", 0xC001UL)]
    [TestCase(INS_addps, 0x62F07C080005C058UL, "62F57C08", 0xC058UL)]
    [TestCase(INS_addps, 0x62F07C080006C058UL, "62F67C08", 0xC058UL)]
    [TestCase(INS_addps, 0x62F07C080500C058UL, "62F57C08", 0xC058UL)]
    public static void PrefixOutputPreservesNativeMapAndOpcodeLayout(instruction ins, ulong bits, string hex, ulong expectedCode)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            emitter.UseVexEncodings = true;
            emitter.UseEvexEncodings = true;
            emitter.UsePromotedEvexEncodings = true;
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX10v1);
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX10v2);
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_APX);
            var buffer = stackalloc byte[20];
            new Span<byte>(buffer, 20).Fill(0xA5);
            emitter.writeableOffset = 8;

            var code = bits;
            var count = emitter.emitOutputRexOrSimdPrefixIfNeeded(ins, buffer + 1, ref code);

            Assert.That(new ReadOnlySpan<byte>(buffer + 9, (int)count).ToArray(), Is.EqualTo(Convert.FromHexString(hex)));
            Assert.That(code, Is.EqualTo(expectedCode));
            Assert.That(buffer[1], Is.EqualTo(0xA5));
            Assert.That(buffer[9 + count], Is.EqualTo(0xA5));
        });
    }

    [Test]
    public static void RelocationUsesExecutableAndWritableLocationsAndHonorsMatchedVm(
        [Values(false, true)] bool matched, [Values(-16, 16)] int aliasOffset)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var context = new RelocationContext { JitInfo = new ICorJitInfo { lpVtbl = &vtable } };
            compiler.info.compMatchedVM = matched;
            var emitter = codeGen.Emitter;
            emitter.emitCmpHandle = &context.JitInfo;
            emitter.writeableOffset = aliasOffset;
            var buffer = stackalloc byte[32];
            var location = buffer + (aliasOffset < 0 ? 16 : 0);
#if LATE_DISASM
            codeGen.Disassembler.disInit(compiler);
            compiler.opts.doLateDisasm = true;
#endif

            emitter.emitRecordRelocation(location, buffer + 24, CorInfoReloc.RELATIVE32, -4);

            Assert.That(context.Calls, Is.EqualTo(matched ? 1 : 0));
#if LATE_DISASM
            var relocations = Relocations(ref codeGen.Disassembler) ?? throw new AssertionException("Missing relocation map.");
            Assert.That(relocations[(nuint)location], Is.EqualTo((nuint)(buffer + 24)));
#endif
            if (matched)
            {
                Assert.That((nuint)context.Location, Is.EqualTo((nuint)location));
                Assert.That((nuint)context.WritableLocation, Is.EqualTo((nuint)(buffer + (aliasOffset < 0 ? 0 : 16))));
                Assert.That((nuint)context.Target, Is.EqualTo((nuint)(buffer + 24)));
                Assert.That(context.Type, Is.EqualTo(CorInfoReloc.RELATIVE32));
                Assert.That(context.Delta, Is.EqualTo(-4));
            }
        });
    }

    private struct RelocationContext
    {
        public ICorJitInfo JitInfo;
        public int Calls;
        public void* Location;
        public void* WritableLocation;
        public void* Target;
        public CorInfoReloc Type;
        public int Delta;
    }

#if LATE_DISASM
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_relocationMap")]
    private static extern ref Dictionary<nuint, nuint>? Relocations(ref Disassembler disassembler);
#endif

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* self, void* location, void* locationRW, void* target, CorInfoReloc type, int delta)
    {
        var context = (RelocationContext*)self;
        context->Calls++;
        context->Location = location;
        context->WritableLocation = locationRW;
        context->Target = target;
        context->Type = type;
        context->Delta = delta;
    }
}
