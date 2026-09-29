// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_ARM
    internal nint emitGetInsSC(instrDesc id)
#else
    internal static nint emitGetInsSC(instrDesc id)
#endif
    {
#if TARGET_ARM
        // The outer ARM-only guard is intentional upstream; ARM64 stores scaled
        // immediates for some formats and does not enter this frame-address path.
        if (id.idIsLclVar())
        {
            assert(_compiler is not null);
            var varNum = id.idAddr().iiaLclVar.lvaVarNum();
            var offs = unchecked((int)id.idAddr().iiaLclVar.lvaOffset());
#if TARGET_ARM
            var adr = _compiler.lvaFrameAddress(varNum, id.idIsLclFPBase(), out regNumber baseReg, offs,
                CodeGen.instIsFP(id.idIns()));
            var dsp = unchecked(adr + offs);
            if ((id.idIns() == INS_sub) || (id.idIns() == INS_subw))
            {
                dsp = unchecked(-dsp);
            }
#elif TARGET_ARM64
            // This branch is unreachable under the pinned outer ARM guard.
            var adr = _compiler.lvaFrameAddress(varNum, out bool fpBased);
            var dsp = unchecked(adr + offs);
            if (id.idIns() == INS_sub)
            {
                dsp = unchecked(-dsp);
            }
#endif
            return dsp;
        }
        else
#endif
        {
            if (id.idIsLargeCns())
            {
                return ((instrDescCns)id).idcCnsVal;
            }
            else
            {
                return id.idSmallCns();
            }
        }
    }
}
