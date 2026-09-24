// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
    private regMaskTP _rsModifiedRegsMask;

#if SWIFT_SUPPORT
    private readonly regMaskTP AllCalleeSavedMask => _rsAllCalleeSavedMask;
#elif HAS_MORE_THAN_64_REGISTERS
    private readonly regMaskTP AllCalleeSavedMask
        => new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED, SRBM_MSK_CALLEE_SAVED);
#else
    private readonly regMaskTP AllCalleeSavedMask
        => new regMaskTP(SRBM_INT_CALLEE_SAVED | SRBM_FLT_CALLEE_SAVED);
#endif

#if SWIFT_SUPPORT
    private readonly regMaskTP IntCalleeSavedMask => _rsIntCalleeSavedMask;
#else
    private readonly regMaskTP IntCalleeSavedMask => new regMaskTP(SRBM_INT_CALLEE_SAVED);
#endif

    private static regMaskTP AndNot(regMaskTP left, regMaskTP right)
    {
#if HAS_MORE_THAN_64_REGISTERS
        return new regMaskTP(left.Lower & ~right.Lower, left.Upper & ~right.Upper);
#else
        return new regMaskTP(left.Lower & ~right.Lower);
#endif
    }

    public readonly regMaskTP rsGetModifiedRegsMask()
    {
#if DEBUG
        assert(_rsModifiedRegsMaskInitialized);
#endif
        return _rsModifiedRegsMask;
    }

    public readonly regMaskTP rsGetModifiedCalleeSavedRegsMask()
    {
#if DEBUG
        assert(_rsModifiedRegsMaskInitialized);
#endif
        return _rsModifiedRegsMask & AllCalleeSavedMask;
    }

    public readonly regMaskTP rsGetModifiedIntCalleeSavedRegsMask()
    {
#if DEBUG
        assert(_rsModifiedRegsMaskInitialized);
#endif
        return _rsModifiedRegsMask & IntCalleeSavedMask;
    }

#if TARGET_AMD64
    public readonly regMaskTP rsGetModifiedOsrIntCalleeSavedRegsMask()
    {
#if DEBUG
        assert(_rsModifiedRegsMaskInitialized);
#endif
        return _rsModifiedRegsMask & (IntCalleeSavedMask | RBM_EBP);
    }
#endif

    public readonly regMaskTP rsGetModifiedFltCalleeSavedRegsMask()
    {
#if DEBUG
        assert(_rsModifiedRegsMaskInitialized);
#endif
        return _rsModifiedRegsMask & new regMaskTP(SRBM_FLT_CALLEE_SAVED);
    }

    public readonly bool rsRegsModified(regMaskTP mask)
    {
#if DEBUG
        assert(_rsModifiedRegsMaskInitialized);
#endif
        return (_rsModifiedRegsMask & mask).IsNonEmpty;
    }

    public void rsClearRegsModified()
    {
        assert(Compiler.lvaDoneFrameLayout < RyuJitSharp.Compiler.FINAL_FRAME_LAYOUT);

#if DEBUG
        if (Compiler.verbose)
        {
            jitprintf("Clearing modified regs.\n");
        }
        _rsModifiedRegsMaskInitialized = true;
#endif

        _rsModifiedRegsMask = RBM_NONE;

#if SWIFT_SUPPORT
        if (Compiler.lvaSwiftErrorArg != BAD_VAR_NUM)
        {
            var swiftError = new regMaskTP(SRBM_SWIFT_ERROR);
            _rsAllCalleeSavedMask = AndNot(_rsAllCalleeSavedMask, swiftError);
            _rsIntCalleeSavedMask = AndNot(_rsIntCalleeSavedMask, swiftError);
        }
#endif
    }

    public void rsSetRegsModified(regMaskTP mask, bool suppressDump = false)
    {
        assert(mask != RBM_NONE);
#if DEBUG
        assert(_rsModifiedRegsMaskInitialized);
        assert((Compiler.lvaDoneFrameLayout < RyuJitSharp.Compiler.FINAL_FRAME_LAYOUT) ||
               _codeGen.Emitter.emitGeneratingPrologOrFuncletProlog() ||
               _codeGen.Emitter.emitGeneratingEpilogOrFuncletEpilog() ||
               ((_rsModifiedRegsMask & AllCalleeSavedMask) == ((_rsModifiedRegsMask | mask) & AllCalleeSavedMask)));

        if (Compiler.verbose && !suppressDump && (_rsModifiedRegsMask != (_rsModifiedRegsMask | mask)))
        {
            jitprintf("Marking regs modified: ");
            dspRegMask(mask);
            jitprintf(" (");
            dspRegMask(_rsModifiedRegsMask);
            jitprintf(" => ");
            dspRegMask(_rsModifiedRegsMask | mask);
            jitprintf(")\n");
        }
#endif

        _rsModifiedRegsMask |= mask;
    }

    public void rsRemoveRegsModified(regMaskTP mask)
    {
        assert(mask != RBM_NONE);
#if DEBUG
        assert(_rsModifiedRegsMaskInitialized);
#endif

        var remaining = AndNot(_rsModifiedRegsMask, mask);
#if DEBUG
        assert((Compiler.lvaDoneFrameLayout < RyuJitSharp.Compiler.FINAL_FRAME_LAYOUT) ||
               _codeGen.Emitter.emitGeneratingPrologOrFuncletProlog() ||
               _codeGen.Emitter.emitGeneratingEpilogOrFuncletEpilog() ||
               ((remaining & AllCalleeSavedMask) == (_rsModifiedRegsMask & AllCalleeSavedMask)));

        if (Compiler.verbose)
        {
            jitprintf("Removing modified regs: ");
            dspRegMask(mask);
            if (_rsModifiedRegsMask == remaining)
            {
                jitprintf(" (unchanged)");
            }
            else
            {
                jitprintf(" (");
                dspRegMask(_rsModifiedRegsMask);
                jitprintf(" => ");
                dspRegMask(remaining);
                jitprintf(")");
            }
            jitprintf("\n");
        }
#endif

        _rsModifiedRegsMask = remaining;
    }

    public void verifyRegUsed(regNumber reg)
    {
        rsSetRegsModified(regMaskTP.CreateFromRegNum(reg, (regMask)(1L << ((int)reg & 63))));
    }

    public void verifyRegistersUsed(regMaskTP regMask)
    {
        if (Compiler.opts.OptimizationDisabled || (regMask == RBM_NONE))
        {
            return;
        }

        rsSetRegsModified(regMask);
    }
}
