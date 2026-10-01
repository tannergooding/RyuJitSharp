// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitIns_R_PATTERN(instruction ins, emitAttr attr, regNumber reg1, insOpts opt,
        insSvePattern pattern = insSvePattern.SVE_PATTERN_ALL)
    {
        var fmt = IF_NONE;
        switch (ins)
        {
            case INS_sve_ptrue:
            case INS_sve_ptrues:
            {
                assert(isPredicateRegister(reg1));
                assert(isScalableVectorSize(attr));
                assert(insOptsScalableStandard(opt));
                fmt = IF_SVE_DE_1A;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        assert(fmt != IF_NONE);

        var id = emitNewInstr(attr);
        id.idIns(ins);
        id.idInsFmt(fmt);
        id.idReg1(reg1);
        id.idInsOpt(opt);
        id.idSvePattern(pattern);

        dispIns(id);
        appendToCurIG(id);
    }
}
#endif
