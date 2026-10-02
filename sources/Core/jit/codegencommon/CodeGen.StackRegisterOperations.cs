// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if !TARGET_WASM
#if !FEATURE_FIXED_OUT_ARGS
using System.Runtime.CompilerServices;
#endif

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genSinglePop()
    {
        SubtractStackLevel(REGSIZE_BYTES);
    }

    public regMaskTP genPushRegs(regMaskTP regs, ref regMaskTP byrefRegs, ref regMaskTP noRefRegs)
    {
        byrefRegs = RBM_NONE;
        noRefRegs = RBM_NONE;

        if (regs == RBM_NONE)
        {
            return RBM_NONE;
        }

#if FEATURE_FIXED_OUT_ARGS
        NYI("Don't call genPushRegs with real regs!");

        return RBM_NONE;
#else
        noway_assert(TYP_REF.StSz == TYP_I_IMPL.StSz, "genTypeStSz(TYP_REF) == genTypeStSz(TYP_I_IMPL)");
        noway_assert(TYP_BYREF.StSz == TYP_I_IMPL.StSz, "genTypeStSz(TYP_BYREF) == genTypeStSz(TYP_I_IMPL)");

        var pushedRegs = regs;
        for (var regIndex = (int)REG_INT_FIRST; regIndex <= (int)REG_INT_LAST; regIndex++)
        {
            var reg = (regNumber)regIndex;
            var regMask = genRegMask(reg);

            if ((regMask & pushedRegs) == RBM_NONE)
            {
                continue;
            }

            var_types type;
            if ((regMask & _gcInfo.gcRegGCrefSetCur) != RBM_NONE)
            {
                type = TYP_REF;
            }
            else if ((regMask & _gcInfo.gcRegByrefSetCur) != RBM_NONE)
            {
                byrefRegs |= regMask;
                type = TYP_BYREF;
            }
            else if (!Unsafe.IsNullRef(ref noRefRegs))
            {
                noRefRegs |= regMask;
                type = TYP_I_IMPL;
            }
            else
            {
                continue;
            }

            inst_RV(INS_push, reg, type);
            genSinglePush();
            _gcInfo.gcMarkRegSetNpt(regMask);
        }

        return pushedRegs;
#endif
    }

    public void genPopRegs(regMaskTP regs, regMaskTP byrefRegs, regMaskTP noRefRegs)
    {
        if (regs == RBM_NONE)
        {
            return;
        }

#if FEATURE_FIXED_OUT_ARGS
        NYI("Don't call genPopRegs with real regs!");
#else
        noway_assert((regs & byrefRegs) == byrefRegs);
        noway_assert((regs & noRefRegs) == noRefRegs);
        noway_assert((regs & (_gcInfo.gcRegGCrefSetCur | _gcInfo.gcRegByrefSetCur)) == RBM_NONE,
            "(regs & (gcInfo.gcRegGCrefSetCur | gcInfo.gcRegByrefSetCur)) == RBM_NONE");

        noway_assert(TYP_REF.StSz == TYP_INT.StSz, "genTypeStSz(TYP_REF) == genTypeStSz(TYP_INT)");
        noway_assert(TYP_BYREF.StSz == TYP_INT.StSz, "genTypeStSz(TYP_BYREF) == genTypeStSz(TYP_INT)");

        var popedRegs = regs;

        // A signed cursor preserves the native sentinel below REG_INT_FIRST; managed regNumber is a byte.
        for (var regIndex = (int)REG_INT_LAST; regIndex >= (int)REG_INT_FIRST; regIndex--)
        {
            var reg = (regNumber)regIndex;
            var regMask = genRegMask(reg);

            if ((regMask & popedRegs) == RBM_NONE)
            {
                continue;
            }

            var_types type;
            if ((regMask & byrefRegs) != RBM_NONE)
            {
                type = TYP_BYREF;
            }
            else if ((regMask & noRefRegs) != RBM_NONE)
            {
                type = TYP_INT;
            }
            else
            {
                type = TYP_REF;
            }

            inst_RV(INS_pop, reg, type);
            genSinglePop();

            if (type != TYP_INT)
            {
                _gcInfo.gcMarkRegPtrVal(reg, type);
            }
        }
#endif
    }
}
#endif
