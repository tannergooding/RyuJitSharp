// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct RegSet
{
    private regMaskTP _rsModifiedRegsMask;

    private readonly regMaskTP AllCalleeSavedMask
    {
        get
        {
            if (Configuration.SWIFT_SUPPORT)
            {
                return _rsAllCalleeSavedMask;
            }

            if (Configuration.HAS_MORE_THAN_64_REGISTERS)
            {
                return new regMaskTP(
                    TargetRuntimeMetadata.IntegerCalleeSavedMask | TargetRuntimeMetadata.FloatingCalleeSavedMask,
                    TargetRuntimeMetadata.MaskCalleeSavedMask);
            }

            return new regMaskTP(TargetRuntimeMetadata.IntegerCalleeSavedMask | TargetRuntimeMetadata.FloatingCalleeSavedMask);
        }
    }

    private readonly regMaskTP IntCalleeSavedMask
    {
        get
        {
            return Configuration.SWIFT_SUPPORT
                ? _rsIntCalleeSavedMask
                : new regMaskTP(TargetRuntimeMetadata.IntegerCalleeSavedMask);
        }
    }

    private static regMaskTP AndNot(regMaskTP left, regMaskTP right)
    {
        if (Configuration.HAS_MORE_THAN_64_REGISTERS)
        {
            return new regMaskTP(left.Lower & ~right.Lower, left.Upper & ~right.Upper);
        }

        return new regMaskTP(left.Lower & ~right.Lower);
    }

    public readonly regMaskTP rsGetModifiedRegsMask()
    {
        if (Configuration.DEBUG)
        {
            assert(_rsModifiedRegsMaskInitialized);
        }
        return _rsModifiedRegsMask;
    }

    public readonly regMaskTP rsGetModifiedCalleeSavedRegsMask()
    {
        if (Configuration.DEBUG)
        {
            assert(_rsModifiedRegsMaskInitialized);
        }
        return _rsModifiedRegsMask & AllCalleeSavedMask;
    }

    public readonly regMaskTP rsGetModifiedIntCalleeSavedRegsMask()
    {
        if (Configuration.DEBUG)
        {
            assert(_rsModifiedRegsMaskInitialized);
        }
        return _rsModifiedRegsMask & IntCalleeSavedMask;
    }

    public readonly regMaskTP rsGetModifiedOsrIntCalleeSavedRegsMask()
    {
        if (!Configuration.TARGET_AMD64)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "OSR integer callee-saved masks require AMD64.");
        }

        if (Configuration.DEBUG)
        {
            assert(_rsModifiedRegsMaskInitialized);
        }
        return _rsModifiedRegsMask & (IntCalleeSavedMask | RBM_EBP);
    }

    public readonly regMaskTP rsGetModifiedFltCalleeSavedRegsMask()
    {
        if (Configuration.DEBUG)
        {
            assert(_rsModifiedRegsMaskInitialized);
        }
        return _rsModifiedRegsMask & new regMaskTP(TargetRuntimeMetadata.FloatingCalleeSavedMask);
    }

    public readonly bool rsRegsModified(regMaskTP mask)
    {
        if (Configuration.DEBUG)
        {
            assert(_rsModifiedRegsMaskInitialized);
        }
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

        if (Configuration.SWIFT_SUPPORT && (Compiler.lvaSwiftErrorArg != BAD_VAR_NUM))
        {
            var swiftError = new regMaskTP(TargetRuntimeMetadata.SwiftErrorMask);
            _rsAllCalleeSavedMask = AndNot(_rsAllCalleeSavedMask, swiftError);
            _rsIntCalleeSavedMask = AndNot(_rsIntCalleeSavedMask, swiftError);
        }
    }

    public void rsSetRegsModified(regMaskTP mask, bool suppressDump = false)
    {
        assert(mask != RBM_NONE);
        if (Configuration.DEBUG)
        {
            assert(_rsModifiedRegsMaskInitialized);
        }
        if (Configuration.DEBUG)
        {
            assert((Compiler.lvaDoneFrameLayout < RyuJitSharp.Compiler.FINAL_FRAME_LAYOUT) ||
                   _codeGen.Emitter.emitGeneratingPrologOrFuncletProlog() ||
                   _codeGen.Emitter.emitGeneratingEpilogOrFuncletEpilog() ||
                   ((_rsModifiedRegsMask & AllCalleeSavedMask) == ((_rsModifiedRegsMask | mask) & AllCalleeSavedMask)));
        }

#if DEBUG
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
        if (Configuration.DEBUG)
        {
            assert(_rsModifiedRegsMaskInitialized);
        }

        var remaining = AndNot(_rsModifiedRegsMask, mask);
        if (Configuration.DEBUG)
        {
            assert((Compiler.lvaDoneFrameLayout < RyuJitSharp.Compiler.FINAL_FRAME_LAYOUT) ||
                   _codeGen.Emitter.emitGeneratingPrologOrFuncletProlog() ||
                   _codeGen.Emitter.emitGeneratingEpilogOrFuncletEpilog() ||
                   ((remaining & AllCalleeSavedMask) == (_rsModifiedRegsMask & AllCalleeSavedMask)));
        }

#if DEBUG
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
        if (!Configuration.TARGET_WASM)
        {
            rsSetRegsModified(regMaskTP.CreateFromRegNum(reg, (regMask)(1L << ((int)reg & 63))));
        }
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
