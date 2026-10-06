// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genUnknownSizeFrame()
    {
        assert(_compiler.compLocallocUsed && _compiler.compUsesUnknownSizeFrame);
#if DEBUG
        assert(_compiler.unkSizeFrame.isFinalized);
#endif
        var totalVectorCount = _compiler.unkSizeFrame.FrameSizeInVectors();

        // SVE local offsets are based at the top of the UnknownSizeFrame.
        // TODO-SVE: Centering the frame could reduce address computations because
        // the indexed immediate is signed 9-bit.
        inst_Mov(TYP_I_IMPL, REG_UNKBASE, REG_SP, canSkip: false);

        if ((totalVectorCount > 0) && (totalVectorCount <= 32))
        {
            Emitter.emitIns_R_R_I(
                INS_sve_addvl, EA_8BYTE, REG_SP, REG_SP, -(nint)totalVectorCount);
        }
        else
        {
            assert(totalVectorCount != 0);
            var rsvd = rsGetRsvdReg();

            instGen_Set_Reg_To_Imm(EA_8BYTE, rsvd, (nint)totalVectorCount);
            Emitter.emitIns_R_I(INS_sve_rdvl, EA_8BYTE, REG_SCRATCH, 1);
            Emitter.emitIns_R_R_R_R(INS_msub, EA_8BYTE, REG_SP, rsvd, REG_SCRATCH, REG_SP);
        }
    }
}
#endif
