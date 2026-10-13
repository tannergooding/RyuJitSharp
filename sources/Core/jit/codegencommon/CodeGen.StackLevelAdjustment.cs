// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genAdjustStackLevel(BasicBlock block)
    {
        if (Configuration.TARGET_WASM)
        {
            throw new FatalJitException(CORJIT_SKIPPED, "Stack-level adjustment is not supported on Wasm.");
        }

        if (Configuration.FEATURE_FIXED_OUT_ARGS)
        {
            return;
        }

        if (Configuration.UNIX_X86_ABI && IsFramePointerUsed && _compiler.fgIsThrowHlpBlk(block))
        {
            // A throw reached while pushing arguments may have an unaligned SP even with a frame pointer.
            Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, TargetRuntimeMetadata.StackPointerBaseRegister, TargetRuntimeMetadata.FramePointerBaseRegister, unchecked(-genSPtoFPdelta));
        }

        if (!IsFramePointerUsed && _compiler.fgIsThrowHlpBlk(block))
        {
            noway_assert(block.HasFlag(BBF_HAS_LABEL));
            SetStackLevel(unchecked(_compiler.fgThrowHlpBlkStkLevel(block) * sizeof(int)));

            if (genStackLevel != 0)
            {
                if (Configuration.TARGET_X86)
                {
                    Emitter.emitMarkStackLvl(genStackLevel);
                    inst_RV_IV(INS_add, TargetRuntimeMetadata.StackPointerBaseRegister, unchecked((nint)genStackLevel), EA_PTRSIZE);
                    SetStackLevel(0);
                }
                else
                {
                    throw new FatalJitException(CORJIT_SKIPPED, "Stack-level marking is not available for this target.");
                }
            }
        }
    }
}
