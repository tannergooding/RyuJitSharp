// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.Emitter.insSize;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    private void emitIns_J_RArm32(instruction ins, emitAttr attr, BasicBlock dst, regNumber reg)
    {
        if (!Configuration.TARGET_ARM)
        {
            FatalJitException.ThrowFatalJitException(
        CORJIT_SKIPPED,
        FatalJitErrorCode.ARM32ConditionalRegisterBranchesAreNotAvailableForThisTarget
    );
            return;
        }

        assert(dst.HasFlag(BBF_HAS_LABEL));

        Emitter.insFormat format;
        switch (ins)
        {
            case INS_cbz:
            case INS_cbnz:
            {
                format = IF_ARM32_T1_I;
                break;
            }

            default:
            {
                unreached();
                return;
            }
        }

        assert(format == IF_ARM32_T1_I);
        assert(isLowRegister(reg));

        var id = emitNewInstrJmp();
        id.idIns(ins);
        id.idInsFmt(IF_ARM32_T1_I);
        id.idInsSize(emitInsSize(IF_ARM32_T1_I));
        id.idReg1(reg);
        id.idjShort = true;
        id.idjTarget = dst;
        id.idjKeepLong = false;
        id.idjIG = emitCurIG;
        id.idjOffs = unchecked((uint)emitCurIGsize);
        id.idjNext = emitCurIGjmpList;
        emitCurIGjmpList = id;

        if (Configuration.EMITTER_STATS)
        {
            emitTotalIGjmps = unchecked(emitTotalIGjmps + 1);
        }

        dispIns(id);
        appendToCurIG(id);
    }
}
