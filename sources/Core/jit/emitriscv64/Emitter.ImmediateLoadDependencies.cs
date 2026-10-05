// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public partial class Emitter
{
    public static int emitLoadImmediate(bool doEmit, emitAttr size, regNumber reg, nint immediate)
    {
        assert(!EA_IS_RELOC(size));
        assert(!doEmit || isGeneralRegister(reg));

        if (isValidSimm12(immediate))
        {
            if (doEmit)
            {
                getCurrentEmitter().emitIns_R_R_I(INS_addi, size, reg, REG_R0, immediate & 0xFFF);
            }

            return 1;
        }

        var immBits = unchecked((ulong)immediate);

        // Partition the value into a signed high32 and a low offset, then materialize
        // the offset with SLLI/ADDI chunks.
        int x;
        int y;
        if ((immBits >> 63) != 0)
        {
            y = 63 - BitOperations.LeadingZeroCount(~immBits) + 1;
        }
        else
        {
            y = 63 - BitOperations.LeadingZeroCount(immBits) + 1;
        }

        if ((immBits & 1) != 0)
        {
            x = BitOperations.TrailingZeroCount(~immBits);
        }
        else
        {
            x = BitOperations.TrailingZeroCount(immBits);
        }

        const int absMaxInsCount = 8; // Matches instrDescLoadImm::absMaxInsCount in the pinned emitter.
        const int prefMaxInsCount = 5;
        assert(prefMaxInsCount <= absMaxInsCount);

        var insCountLimit = prefMaxInsCount;
        if (JitTls.Compiler is Compiler compiler && compiler.codeGen is not null)
        {
            var emitter = compiler.GetEmitter();
            if (emitter.emitGeneratingPrologOrFuncletProlog() ||
                emitter.emitGeneratingEpilogOrFuncletEpilog())
            {
                insCountLimit = absMaxInsCount;
            }
        }

        var utilizeSRLI = false;
        var srliShiftAmount = 0;
        var originalImmediate = immediate;
        var cond1 = (y - x) > 31;
        if ((immBits >> 63) == 0 && cond1)
        {
            srliShiftAmount = BitOperations.LeadingZeroCount(immBits);
            var tempImm = immBits << srliShiftAmount;
            var m = BitOperations.LeadingZeroCount(~tempImm);
            var b = 64 - m;
            var a = BitOperations.TrailingZeroCount(tempImm);
            var cond2 = (b - a) < 32;
            var cond3 = ((y - x) - (b - a)) >= 11;
            if (cond2 || cond3)
            {
                immBits = tempImm;
                y = b;
                x = a;
                utilizeSRLI = true;
                insCountLimit -= 1;
            }
        }

        assert(y >= x);
        assert((1 <= y) && (y <= 63));
        assert((1 <= x) && (x <= 63));

        if (y < 32)
        {
            y = 31;
            x = 0;
        }
        else if ((y - x) < 31)
        {
            y = x + 31;
            y = (y > 63) ? 63 : y;
        }
        else
        {
            x = y - 31;
        }

        var high32 = unchecked((uint)(unchecked((long)immBits) >> x)) & WordMask(32);

        // Choose the smaller of the additive and subtractive low offsets.
        var offset1 = immBits & BitMask64((byte)x);
        var offset2 = unchecked(~unchecked(offset1 - 1UL)) & BitMask64((byte)x);
        var offset = offset1;
        var isSubtractMode = false;

        if ((high32 == 0x7FFFFFFF) && (y != 63))
        {
            // Incrementing this high word would change its sign; shift the split instead.
            var newX = x + 1;
            var newOffset1 = immBits & BitMask64((byte)newX);
            var newOffset2 = unchecked(~unchecked(newOffset1 - 1UL)) & BitMask64((byte)newX);
            if (newOffset2 < offset1)
            {
                x = newX;
                high32 = unchecked((uint)(unchecked((long)immBits) >> x)) & WordMask(32);
                offset2 = newOffset2;
                isSubtractMode = true;
            }
        }
        else if (offset2 < offset1)
        {
            isSubtractMode = true;
        }

        if (isSubtractMode)
        {
            offset = offset2;
            high32 = unchecked(high32 + 1) & WordMask(32);
        }

        assert(absMaxInsCount >= 2);
        Span<instruction> instructions = stackalloc instruction[absMaxInsCount];
        Span<int> values = stackalloc int[absMaxInsCount];
        var numberOfInstructions = 0;

        var upper = (high32 >> 12) & WordMask(20);
        var lower = high32 & WordMask(12);
        var lowerMsb = (lower >> 11) & 1;
        if (lowerMsb == 1)
        {
            upper = unchecked(upper + 1) & WordMask(20);
        }

        if (upper != 0)
        {
            if (doEmit)
            {
                instructions[numberOfInstructions] = INS_lui;
                values[numberOfInstructions] = ((upper >> 19) & 1) != 0
                    ? unchecked((int)(upper + 0xFFF00000u))
                    : (int)upper;
            }

            numberOfInstructions += 1;
        }

        if (lower != 0)
        {
            if (doEmit)
            {
                instructions[numberOfInstructions] = INS_addiw;
                values[numberOfInstructions] = (int)lower;
            }

            numberOfInstructions += 1;
        }

        var chunkLsbPos = (x < 11) ? 0 : (x - 11);
        var shift = (x < 11) ? x : 11;
        var chunkMask = (x < 11) ? BitMask64((byte)x) : BitMask64(11);
        while (true)
        {
            var chunk = (offset >> chunkLsbPos) & chunkMask;
            if (chunk != 0)
            {
                var leadingZerosOn11BitsChunk = 11 - (64 - BitOperations.LeadingZeroCount(chunk));
                if (leadingZerosOn11BitsChunk > 0)
                {
                    var maxAdditionalShift = (chunkLsbPos < leadingZerosOn11BitsChunk)
                        ? chunkLsbPos
                        : leadingZerosOn11BitsChunk;
                    chunkLsbPos -= maxAdditionalShift;
                    shift += maxAdditionalShift;
                    chunk = (offset >> chunkLsbPos) & chunkMask;
                }

                numberOfInstructions += 2;
                if (numberOfInstructions > insCountLimit)
                {
                    break;
                }

                if (doEmit)
                {
                    instructions[numberOfInstructions - 2] = INS_slli;
                    values[numberOfInstructions - 2] = shift;
                    instructions[numberOfInstructions - 1] = INS_addi;
                    values[numberOfInstructions - 1] = isSubtractMode ? unchecked(-(int)chunk) : (int)chunk;
                }

                shift = 0;
            }

            if (chunkLsbPos == 0)
            {
                break;
            }

            shift += (chunkLsbPos < 11) ? chunkLsbPos : 11;
            chunkMask = (chunkLsbPos < 11) ? (chunkMask >> (11 - chunkLsbPos)) : BitMask64(11);
            chunkLsbPos -= (chunkLsbPos < 11) ? chunkLsbPos : 11;
        }

        if (shift > 0)
        {
            numberOfInstructions += 1;
            if (doEmit && (numberOfInstructions <= insCountLimit))
            {
                instructions[numberOfInstructions - 1] = INS_slli;
                values[numberOfInstructions - 1] = shift;
            }
        }

        if (numberOfInstructions <= insCountLimit)
        {
            if (utilizeSRLI)
            {
                numberOfInstructions += 1;
                assert(numberOfInstructions < absMaxInsCount);
                if (doEmit)
                {
                    instructions[numberOfInstructions - 1] = INS_srli;
                    values[numberOfInstructions - 1] = srliShiftAmount;
                }
            }

            if (doEmit)
            {
                getCurrentEmitter().emitLoadImmediateSequence(
                    size,
                    reg,
                    originalImmediate,
                    instructions[..numberOfInstructions],
                    values[..numberOfInstructions]);
            }

            return numberOfInstructions;
        }

        if (EA_SIZE(size) == EA_PTRSIZE)
        {
            if (doEmit)
            {
                var emitter = getCurrentEmitter();
                assert(!emitter.emitGeneratingPrologOrFuncletProlog() &&
                       !emitter.emitGeneratingEpilogOrFuncletEpilog());

                var originalValue = unchecked((long)originalImmediate);
                Span<byte> data = stackalloc byte[sizeof(long)];
                MemoryMarshal.Write(data, in originalValue);
                var constAddr = emitter.emitDataConst(data, sizeof(long), TYP_LONG);
                unsafe
                {
                    emitter.emitIns_R_C(
                        INS_ld,
                        EA_PTRSIZE,
                        reg,
                        REG_NA,
                        Compiler.eeFindJitDataOffs(constAddr));
                }
            }

            return -1;
        }

        assert(false, "If number of instructions exceeds MAX_NUM_OF_LOAD_IMM_INS, imm must be 8 bytes");
        return 0;
    }

    private static Emitter getCurrentEmitter()
    {
        var compiler = JitTls.Compiler ??
            throw new FatalJitException(CORJIT_INTERNALERROR, "RISC-V64 emission requires an active compiler.");
        return compiler.GetEmitter();
    }

    private void emitLoadImmediateSequence(
        emitAttr size,
        regNumber reg,
        nint immediate,
        ReadOnlySpan<instruction> instructions,
        ReadOnlySpan<int> values)
    {
        throw new FatalJitException(
            CORJIT_SKIPPED,
            "RISC-V64 multi-instruction immediate descriptor recording is not ported.");
    }
}
#endif
