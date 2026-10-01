// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe byte* emitOutputData16(byte* dst)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Data-size prefix output requires xarch.");
#elif TARGET_X86
        // Native x86 data16 output does not emit a prefix.
        return dst;
#else
        var dstRW = unchecked(dst + writeableOffset);
        *dstRW++ = 0x66;
        return unchecked(dstRW - writeableOffset);
#endif
    }

    public unsafe byte* emitOutputNOP(byte* dst, nuint nBytes)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "NOP output requires xarch.");
#else
        assert(nBytes <= 15);
        var dstRW = unchecked(dst + writeableOffset);
#if TARGET_X86
        // Native's x86 switch has no case above 15 in release builds.
        if (nBytes <= 15)
        {
            for (nuint i = 0; i < nBytes; i++)
            {
                *dstRW++ = 0x90;
            }
        }
#else
        switch (nBytes)
        {
            case 0:
            {
                break;
            }

            case 1:
            {
                *dstRW++ = 0x90;
                break;
            }

            case 2:
            {
                *dstRW++ = 0x66;
                *dstRW++ = 0x90;
                break;
            }

            case 3:
            {
                *dstRW++ = 0x0F;
                *dstRW++ = 0x1F;
                *dstRW++ = 0x00;
                break;
            }

            case 4:
            {
                *dstRW++ = 0x0F;
                *dstRW++ = 0x1F;
                *dstRW++ = 0x40;
                *dstRW++ = 0x00;
                break;
            }

            case 5:
            case 6:
            {
                if (nBytes == 6)
                {
                    *dstRW++ = 0x66;
                }
                *dstRW++ = 0x0F;
                *dstRW++ = 0x1F;
                *dstRW++ = 0x44;
                *dstRW++ = 0x00;
                *dstRW++ = 0x00;
                break;
            }

            case 7:
            {
                *dstRW++ = 0x0F;
                *dstRW++ = 0x1F;
                *dstRW++ = 0x80;
                *dstRW++ = 0x00;
                *dstRW++ = 0x00;
                *dstRW++ = 0x00;
                *dstRW++ = 0x00;
                break;
            }

            case 8:
            case 9:
            case 10:
            case 11:
            {
                for (nuint prefix = 8; prefix < nBytes; prefix++)
                {
                    *dstRW++ = 0x66;
                }
                *dstRW++ = 0x0F;
                *dstRW++ = 0x1F;
                *dstRW++ = 0x84;
                *dstRW++ = 0x00;
                *dstRW++ = 0x00;
                *dstRW++ = 0x00;
                *dstRW++ = 0x00;
                *dstRW++ = 0x00;
                break;
            }

            case 12:
            {
                return emitOutputNOP(emitOutputNOP(dst, 4), 8);
            }

            case 13:
            {
                return emitOutputNOP(emitOutputNOP(dst, 5), 8);
            }

            case 14:
            {
                return emitOutputNOP(emitOutputNOP(dst, 7), 7);
            }

            case 15:
            {
                return emitOutputNOP(emitOutputNOP(dst, 7), 8);
            }
        }
#endif
        return unchecked(dstRW - writeableOffset);
#endif
    }

#if FEATURE_LOOP_ALIGN
    public unsafe byte* emitOutputAlign(insGroup ig, instrDesc id, byte* dst)
    {
#if !TARGET_XARCH
        throw new FatalJitException(CORJIT_SKIPPED, "Loop-alignment output requires xarch.");
#else
        var alignInstr = id as instrDescAlign
            ?? throw new FatalJitException("Loop alignment requires an alignment descriptor.");
        var compiler = _compiler ?? throw new FatalJitException("Loop alignment requires an active compiler.");
#if DEBUG
        var offset = compiler.IsAot ? (nuint)emitCurCodeOffs(dst) : (nuint)dst;
        var validatePadding = !alignInstr.isPlacedAfterJmp;
#endif
        assert(codeGen.ShouldAlignLoops);
        assert(ig.endsWithAlignInstr());

        var paddingToAdd = id.idCodeSize();
#if DEBUG
        assert(!validatePadding || (paddingToAdd == 0) ||
            ((offset & (uint)(compiler.opts.compJitAlignLoopBoundary - 1)) != 0));
#endif
        assert(paddingToAdd < compiler.opts.compJitAlignLoopBoundary);

#if DEBUG
        if (validatePadding)
        {
            var loopHead = alignInstr.idaIG?.igNext
                ?? throw new FatalJitException("An alignment descriptor requires a following loop group.");
            var containingIG = alignInstr.idaIG
                ?? throw new FatalJitException("An alignment descriptor requires a containing group.");
            var loopHeadPredIG = alignInstr.idaLoopHeadPredIG
                ?? throw new FatalJitException("An alignment descriptor requires a loop-head predecessor.");
            var paddingNeeded = emitCalculatePaddingForLoopAlignment(loopHead, offset, true,
                containingIG, loopHeadPredIG);
            if (compiler.opts.compJitAlignLoopAdaptive)
            {
                assert(paddingToAdd == paddingNeeded);
            }
        }
#endif
        compiler.Metrics.LoopsAligned++;

#if DEBUG
        if (compiler.compStressCompile(Compiler.compStressArea.STRESS_EMITTER, 50)
            && alignInstr.isPlacedAfterJmp && paddingToAdd >= 1)
        {
            dst += emitOutputByte(dst, unchecked((long)insCodeMR(INS_BREAKPOINT)));
            paddingToAdd--;
        }
#endif
        return emitOutputNOP(dst, paddingToAdd);
#endif
    }
#endif

}
