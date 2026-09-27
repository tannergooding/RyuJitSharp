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
#if TARGET_AMD64 || TARGET_ARM64
        var codeGen = _compiler.codeGen;
        assert(codeGen is not null);

        var frameType = FT_NOT_SET;
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
                assert(!codeGen.IsFramePointerRequired);
                assert(!codeGen.IsFrameRequired);
                codeGen.IsFramePointerUsed = false;
                break;
            }
            case FT_EBP_FRAME:
            {
                codeGen.IsFramePointerUsed = true;
                break;
            }
            default:
            {
                throw new FatalJitException("LSRA frame type was not selected.");
            }
        }

        _compiler.rpFrameType = frameType;
        var removeMask = frameType is FT_EBP_FRAME ? SRBM_FPBASE : SRBM_NONE;
#if TARGET_ARM64
        if (_compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT))
        {
            codeGen.RegSet.rsMaskResvd |= new regMaskTP(SRBM_OPT_RSVD);
            assert(REG_OPT_RSVD != REG_FP);
            JITDUMP($"  Reserved REG_OPT_RSVD ({REG_OPT_RSVD.Name}) due to large frame\n");
            removeMask |= SRBM_OPT_RSVD;
        }

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
#if TARGET_XARCH
            _lowGprRegs = _availableIntRegs & SRBM_LOWINT;
#endif
        }
#else
        NYI("LSRA frame selection outside AMD64/ARM64");
        fatal(CORJIT_IMPLLIMITATION);
        throw new FatalJitException("LSRA frame selection outside AMD64/ARM64.");
#endif
    }
}
