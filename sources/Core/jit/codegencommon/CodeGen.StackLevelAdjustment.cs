// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genAdjustStackLevel(BasicBlock block)
    {
#if !FEATURE_FIXED_OUT_ARGS && !TARGET_WASM
#if UNIX_X86_ABI
        if (IsFramePointerUsed && _compiler.fgIsThrowHlpBlk(block))
        {
            // A throw reached while pushing arguments may have an unaligned SP even with a frame pointer.
            Emitter.emitIns_R_AR(INS_lea, EA_PTRSIZE, REG_SPBASE, REG_FPBASE, unchecked(-genSPtoFPdelta));
        }
#endif

        if (!IsFramePointerUsed && _compiler.fgIsThrowHlpBlk(block))
        {
            noway_assert(block.HasFlag(BBF_HAS_LABEL));
            SetStackLevel(unchecked(_compiler.fgThrowHlpBlkStkLevel(block) * sizeof(int)));

            if (genStackLevel != 0)
            {
#if TARGET_X86
                Emitter.emitMarkStackLvl(genStackLevel);
                inst_RV_IV(INS_add, REG_SPBASE, unchecked((nint)genStackLevel), EA_PTRSIZE);
                SetStackLevel(0);
#else
                NYI("Need emitMarkStackLvl()");
#endif
            }
        }
#endif
    }
}
