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

        FrameType frameType;
        if (Configuration.DOUBLE_ALIGN)
        {
            setDoubleAlign(false);
        }

        if (Configuration.DOUBLE_ALIGN && _doDoubleAlign)
        {
            frameType = FT_DOUBLE_ALIGN_FRAME;
            setDoubleAlign(true);
        }
        else
            if (codeGen.IsFramePointerRequired)
            {
                frameType = FT_EBP_FRAME;
            }
            else
            {
                if (!_compiler.rpMustCreateEBPCalled)
                {
                    _compiler.rpMustCreateEBPCalled = true;
                    if (_compiler.rpMustCreateEBPFrame(out var reason))
                    {
                        JITDUMP($"; Decided to create an EBP based frame for ETW stackwalking ({reason})\n");
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
            case FT_DOUBLE_ALIGN_FRAME:
            {
                noway_assert(!codeGen.IsFramePointerRequired);
                codeGen.IsFramePointerUsed = false;
                break;
            }
            default:
            {
                FatalJitException.ThrowFatalJitException(
                    CORJIT_INTERNALERROR,
                    FatalJitErrorCode.DirectFailureMessage316
                );
                return;
            }
        }

        var removeMask = SRBM_NONE;
        if (frameType is FT_EBP_FRAME)
        {
            if (Configuration.TARGET_AMD64 || Configuration.TARGET_ARM64)
            {
                removeMask |= SRBM_FPBASE;
            }
            else
            {
                removeMask |= getFramePointerBaseMask();
            }
        }

        _compiler.rpFrameType = frameType;
        if ((Configuration.TARGET_ARMARCH || Configuration.TARGET_RISCV64) &&
            _compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT))
        {
            var reservedRegister = Configuration.TARGET_ARM64
                ? REG_ARM64_IP1
                : getOptionalFrameReserveRegister();
            var reservedMask = genSingleTypeRegMask(reservedRegister);
            codeGen.RegSet.rsMaskResvd |= regMaskTP.CreateFromRegNum(reservedRegister, reservedMask);
            assert(reservedRegister != TargetRuntimeMetadata.FramePointerRegister);
            JITDUMP($"  Reserved REG_OPT_RSVD ({reservedRegister.Name}) due to large frame\n");
            removeMask |= reservedMask;
        }

        if (Configuration.TARGET_ARM && _compiler.compLocallocUsed)
        {
            var savedStackPointerRegister = getSavedLocallocStackPointerRegister();
            var savedStackPointerMask = genSingleTypeRegMask(savedStackPointerRegister);
            codeGen.RegSet.rsMaskResvd |= regMaskTP.CreateFromRegNum(savedStackPointerRegister, savedStackPointerMask);
            JITDUMP($"  Reserved REG_SAVED_LOCALLOC_SP ({savedStackPointerRegister.Name}) due to localloc\n");
            removeMask |= savedStackPointerMask;
        }

        if (Configuration.TARGET_ARM64 && _compiler.compUsesUnknownSizeFrame)
        {
            codeGen.RegSet.rsMaskResvd |= new regMaskTP(SRBM_UNKBASE);
            JITDUMP($"  Reserved REG_UNKBASE ({REG_UNKBASE.Name}) due to presence of UnknownSizeFrame\n");
            removeMask |= SRBM_UNKBASE;
        }
        if ((removeMask != SRBM_NONE) && ((_availableIntRegs & removeMask) != SRBM_NONE))
        {
            _availableIntRegs &= ~removeMask;
            initializeAvailableRegs();
            if (Configuration.TARGET_AMD64)
            {
                _lowGprRegs = _availableIntRegs & SRBM_LOWINT;
            }
            else if (Configuration.TARGET_X86)
            {
                _lowGprRegs = _availableIntRegs;
            }
        }
    }

    private void setDoubleAlign(bool enabled)
    {
        var codeGen = _compiler.codeGen;
        assert(codeGen is not null);
        codeGen.IsDoubleAligned = enabled;
    }

    private regMask getFramePointerBaseMask()
    {
        NYI("RBM_FPBASE on this target");
        FatalJitException.ThrowFatalJitException(CORJIT_IMPLLIMITATION, "Frame-pointer base register mask is not ported.");
        return default!;
    }

    private regNumber getOptionalFrameReserveRegister()
    {
        NYI("REG_OPT_RSVD on this target");
        FatalJitException.ThrowFatalJitException(CORJIT_IMPLLIMITATION, "Reserved frame-offset register is not ported.");
        return default!;
    }

    private regNumber getSavedLocallocStackPointerRegister()
    {
        NYI("REG_SAVED_LOCALLOC_SP on ARM");
        FatalJitException.ThrowFatalJitException(CORJIT_IMPLLIMITATION, "Saved localloc stack-pointer register is not ported.");
        return default!;
    }
}
