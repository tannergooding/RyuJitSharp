// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_StoreFromSrc(regNumber srcReg, var_types dstType, bool aligned = false)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Source-register store selection outside AMD64 is not implemented.");
#else
        assert(srcReg != REG_NA);
        if (varTypeUsesIntReg(dstType))
        {
            if (genIsValidIntOrFakeReg(srcReg))
            {
                return ins_Store(dstType, aligned);
            }
#if FEATURE_SIMD
            if (genIsValidMaskReg(srcReg))
            {
                return ins_Store(TYP_MASK, aligned);
            }
#endif
            assert(genIsValidFloatReg(srcReg));
            var dstSize = dstType.Size;
            if (dstSize == 4)
            {
                dstType = TYP_FLOAT;
            }
            else
            {
                assert(dstSize == 8);
                dstType = TYP_DOUBLE;
            }

            return ins_Store(dstType, aligned);
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(dstType))
        {
            if (genIsValidMaskReg(srcReg))
            {
                return ins_Store(dstType, aligned);
            }

            assert(genIsValidIntOrFakeReg(srcReg));
            return ins_Store(dstType, aligned);
        }
#endif
        assert(varTypeUsesFloatReg(dstType));
        if (genIsValidIntOrFakeReg(srcReg))
        {
            var dstSize = dstType.Size;
            if (dstSize == 4)
            {
                dstType = TYP_INT;
            }
            else
            {
                assert(dstSize == 8);
                dstType = TYP_LONG;
            }
        }
        else
        {
            assert(genIsValidFloatReg(srcReg));
        }

        return ins_Store(dstType, aligned);
#endif
    }
}
