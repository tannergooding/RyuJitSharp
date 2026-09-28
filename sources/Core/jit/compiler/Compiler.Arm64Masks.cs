// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
namespace RyuJitSharp;

public partial class Compiler
{
    public int getFFRegisterVarNum()
    {
        if (lvaFfrRegister == BAD_VAR_NUM)
        {
            lvaFfrRegister = lvaGrabTempWithImplicitUse(false, "Save the FFR value.");
            lvaTable[lvaFfrRegister].Type = TYP_MASK;
        }

        return lvaFfrRegister;
    }

    public GenTreeMskCon gtNewSimdTrueMaskNode(var_types simdBaseType)
    {
#if DEBUG
        if (JitConfig.JitUseScalableVectorT != 0)
        {
            throw new FatalJitException(CORJIT_IMPLLIMITATION, "ARM64 scalable true masks require scalable representation.");
        }
#endif

        var mask = new GenTreeMskCon(default);
        var found = EvaluateSimdPatternToMask<simd16_t>(simdBaseType, ref mask.SimdMaskVal, SveMaskPattern.SveMaskPatternAll);
        assert(found);
        return mask;
    }
}
#endif
