// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics.CodeAnalysis;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code",
        Justification = "Preserves the native target-dependent reserved-register assertion.")]
    private void setFrameType()
    {
        var codeGen = _compiler.codeGen;
        assert(codeGen is not null);

        var frameType = FT_NOT_SET;
#if DOUBLE_ALIGN
        setDoubleAlign(false);
        if (_doDoubleAlign)
        {
            frameType = FT_DOUBLE_ALIGN_FRAME;
            setDoubleAlign(true);
        }
        else
#endif
        if (codeGen.IsFramePointerRequired)
        {
            frameType = FT_EBP_FRAME;
        }
        else
        {
            if (!_compiler.rpMustCreateEBPCalled)
            {
                _compiler.rpMustCreateEBPCalled = true;
#if DEBUG
                if (_compiler.rpMustCreateEBPFrame(out var reason))
#else
                if (_compiler.rpMustCreateEBPFrame(out _))
#endif
                {
#if DEBUG
                    JITDUMP($"; Decided to create an EBP based frame for ETW stackwalking ({reason})\n");
#endif
                    codeGen.IsFrameRequired = true;
                }
            }

            frameType = codeGen.IsFrameRequired ? FT_EBP_FRAME : FT_ESP_FRAME;
        }

        switch (frameType)
        {
            case FT_ESP_FRAME:
            {
                noway_assert(!codeGen.IsFramePointerRequired);
                noway_assert(!codeGen.IsFrameRequired);
                codeGen.IsFramePointerUsed = false;
                break;
            }
            case FT_EBP_FRAME:
            {
                codeGen.IsFramePointerUsed = true;
                break;
            }
#if DOUBLE_ALIGN
            case FT_DOUBLE_ALIGN_FRAME:
            {
                noway_assert(!codeGen.IsFramePointerRequired);
                codeGen.IsFramePointerUsed = false;
                break;
            }
#endif
            default:
            {
                throw new FatalJitException("LSRA frame type was not selected.");
            }
        }

        var removeMask = SRBM_NONE;
        if (frameType is FT_EBP_FRAME)
        {
#if TARGET_AMD64 || TARGET_ARM64
            removeMask |= SRBM_FPBASE;
#else
            removeMask |= getFramePointerBaseMask();
#endif
        }

        _compiler.rpFrameType = frameType;
#if TARGET_ARMARCH || TARGET_RISCV64
        if (_compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT))
        {
#if TARGET_ARM64
            var reservedRegister = REG_OPT_RSVD;
#else
            var reservedRegister = getOptionalFrameReserveRegister();
#endif
            var reservedMask = genSingleTypeRegMask(reservedRegister);
            codeGen.RegSet.rsMaskResvd |= regMaskTP.CreateFromRegNum(reservedRegister, reservedMask);
            assert(reservedRegister != REG_FP);
            JITDUMP($"  Reserved REG_OPT_RSVD ({reservedRegister.Name}) due to large frame\n");
            removeMask |= reservedMask;
        }
#endif

#if TARGET_ARM
        if (_compiler.compLocallocUsed)
        {
            var savedStackPointerRegister = getSavedLocallocStackPointerRegister();
            var savedStackPointerMask = genSingleTypeRegMask(savedStackPointerRegister);
            codeGen.RegSet.rsMaskResvd |= regMaskTP.CreateFromRegNum(savedStackPointerRegister, savedStackPointerMask);
            JITDUMP($"  Reserved REG_SAVED_LOCALLOC_SP ({savedStackPointerRegister.Name}) due to localloc\n");
            removeMask |= savedStackPointerMask;
        }
#endif

#if TARGET_ARM64
        if (_compiler.compUsesUnknownSizeFrame)
        {
            codeGen.RegSet.rsMaskResvd |= new regMaskTP(SRBM_UNKBASE);
            JITDUMP($"  Reserved REG_UNKBASE ({REG_UNKBASE.Name}) due to presence of UnknownSizeFrame\n");
            removeMask |= SRBM_UNKBASE;
        }
#endif
        if ((removeMask != SRBM_NONE) && ((_availableIntRegs & removeMask) != SRBM_NONE))
        {
            _availableIntRegs &= ~removeMask;
            initializeAvailableRegs();
#if TARGET_AMD64
            _lowGprRegs = _availableIntRegs & SRBM_LOWINT;
#elif TARGET_X86
            _lowGprRegs = _availableIntRegs;
#endif
        }
    }

#if DOUBLE_ALIGN
    private void setDoubleAlign(bool enabled)
    {
        var codeGen = _compiler.codeGen;
        assert(codeGen is not null);
        codeGen.IsDoubleAligned = enabled;
    }
#endif

#if !(TARGET_AMD64 || TARGET_ARM64)
    private regMask getFramePointerBaseMask()
    {
        NYI("RBM_FPBASE on this target");
        throw new FatalJitException(CORJIT_IMPLLIMITATION, "Frame-pointer base register mask is not ported.");
    }
#endif

#if (TARGET_ARM && !TARGET_ARM64) || TARGET_RISCV64
    private regNumber getOptionalFrameReserveRegister()
    {
        NYI("REG_OPT_RSVD on this target");
        throw new FatalJitException(CORJIT_IMPLLIMITATION, "Reserved frame-offset register is not ported.");
    }
#endif

#if TARGET_ARM
    private regNumber getSavedLocallocStackPointerRegister()
    {
        NYI("REG_SAVED_LOCALLOC_SP on ARM");
        throw new FatalJitException(CORJIT_IMPLLIMITATION, "Saved localloc stack-pointer register is not ported.");
    }
#endif
}
