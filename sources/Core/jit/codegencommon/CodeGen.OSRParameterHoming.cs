// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genEnregisterOSRArgsAndLocals(regNumber initReg, ref bool initRegZeroed)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "OSR argument and local enregistration requires Windows AMD64.");
#else
        assert(_compiler.opts.IsOSR);
        var patchpoint = _compiler.info.compPatchpointInfo;
        assert(patchpoint->NumberOfLocals == _compiler.info.compLocalsCount);

        var originalFrameSize = patchpoint->TotalFrameSize;
        var patchpointInfoLen = patchpoint->NumberOfLocals;
        var firstBlock = _compiler.fgFirstBB;
        noway_assert(firstBlock is not null);
        for (var varNum = 0; varNum < _compiler.lvaCount; varNum++)
        {
            if (!_compiler.lvaIsOSRLocal(varNum))
            {
                continue;
            }

            ref var local = ref _compiler.lvaGetDesc(varNum);
            if (!local.lvIsInReg)
            {
                JITDUMP($"---OSR--- V{varNum:D2} in memory\n");
                continue;
            }
            if (!VarSetOps.IsMember(_compiler, firstBlock.bbLiveIn, local._varIndex))
            {
                JITDUMP($"---OSR--- V{varNum:D2} (reg) not live at entry\n");
                continue;
            }

            var fieldOffset = 0;
            var localNumber = varNum;
            if (local.lvIsStructField)
            {
                localNumber = local.lvParentLcl;
                assert(localNumber < patchpointInfoLen);
                fieldOffset = local.lvFldOffset;
                JITDUMP($"---OSR--- V{varNum:D2} is promoted field of V{localNumber:D2} at offset {fieldOffset}\n");
            }

            var localType = local.GetStackSlotHomeType();
            var size = localType.EmitActualSize;
            var tier0Offset = _compiler.lvaOSRLocalTier0FrameOffset(localNumber) + fieldOffset;

            // Tier0's FP-relative slot is reached via the original frame size,
            // plus either the OSR saved FP or this frame's SP-to-FP delta.
            var offset = originalFrameSize + tier0Offset;
            if (IsFramePointerUsed)
            {
                offset += TARGET_POINTER_SIZE;
            }
            else
            {
                offset += genSPtoFPdelta;
            }

#if DEBUG
            if (_verbose)
            {
                jitprintf($"---OSR--- V{varNum:D2} (reg) old rbp offset {tier0Offset} old frame " +
                    $"{originalFrameSize} this frame sp-fp {genSPtoFPdelta} new offset {offset} (0x{offset:x2})\n");
            }
#endif
            Emitter.emitIns_R_AR(ins_Load(localType), size, local.RegNum, genFramePointerReg(), offset);
        }
#endif
    }

    public void genHomeStackPartOfSplitParameter(regNumber initReg, ref bool initRegStillZeroed)
    {
#if !TARGET_AMD64 || !WINDOWS_AMD64_ABI
        throw new FatalJitException(CORJIT_SKIPPED, "Split-parameter stack homing requires Windows AMD64.");
#else
        // The native Windows AMD64 branch is empty: register homes already
        // form contiguous space with incoming stack arguments.
#endif
    }
}
