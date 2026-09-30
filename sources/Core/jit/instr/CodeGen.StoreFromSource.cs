// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_StoreFromSrc(regNumber srcReg, var_types dstType, bool aligned = false)
    {
        assert(srcReg != REG_NA);
        if (varTypeUsesIntReg(dstType))
        {
            if (genIsValidIntOrFakeReg(srcReg))
            {
                return ins_Store(dstType, aligned);
            }
#if TARGET_XARCH && FEATURE_SIMD
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
#if TARGET_64BIT
                assert(dstSize == 8);
                dstType = TYP_DOUBLE;
#else
                unreached();
                throw new FatalJitException(CORJIT_SKIPPED, "An eight-byte floating register store requires a 64-bit target.");
#endif
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
#if TARGET_64BIT
                assert(dstSize == 8);
                dstType = TYP_LONG;
#else
                unreached();
                throw new FatalJitException(CORJIT_SKIPPED, "An eight-byte integer register store requires a 64-bit target.");
#endif
            }
        }
        else
        {
            assert(genIsValidFloatReg(srcReg));
        }

        return ins_Store(dstType, aligned);
    }
}
