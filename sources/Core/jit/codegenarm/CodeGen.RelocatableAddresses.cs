// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public unsafe void genMov32RelocatableDisplacement(BasicBlock block, regNumber reg)
    {
        Emitter.emitIns_R_L(INS_movw, EA_4BYTE_DSP_RELOC, block, reg);
        Emitter.emitIns_R_L(INS_movt, EA_4BYTE_DSP_RELOC, block, reg);

        if (_compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_RELATIVE_CODE_RELOCS))
        {
            Emitter.emitIns_R_R_R(INS_add, EA_4BYTE_DSP_RELOC, reg, reg, REG_PC);
        }
    }
}
#endif
