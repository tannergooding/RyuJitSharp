// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForLclAddr(GenTreeLclFld localAddress)
    {
        if (Configuration.TARGET_XARCH)
        {
            Emitter.RequireSupportedInstructionRecording();
        }
        else if (!Configuration.TARGET_ARMARCH && !Configuration.TARGET_LOONGARCH64 && !Configuration.TARGET_RISCV64)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Native local-address generation is unavailable for this target.");
            return;
        }

        assert(localAddress.Oper is GT_LCL_ADDR);

        var targetType = localAddress.Type;
        var size = targetType.EmitSize;
        var targetReg = localAddress.RegNum;
        noway_assert((targetType is var __targetAliasValue0 && (__targetAliasValue0 is TYP_BYREF || __targetAliasValue0 == TYP_I_IMPL)));

        Emitter.emitIns_R_S(INS_lea, size, targetReg, localAddress.LclNum, localAddress.LclOffs);
        genProduceReg(localAddress);
    }
}
