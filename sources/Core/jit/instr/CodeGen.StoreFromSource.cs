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
            if (Configuration.TARGET_XARCH && Configuration.FEATURE_SIMD && genIsValidMaskReg(srcReg))
            {
                return ins_Store(TYP_MASK, aligned);
            }
            assert(genIsValidFloatReg(srcReg));
            var dstSize = dstType.Size;
            if (dstSize == 4)
            {
                dstType = TYP_FLOAT;
            }
            else
            {
                if (Configuration.TARGET_64BIT)
                {
                    assert(dstSize == 8);
                    dstType = TYP_DOUBLE;
                }
                else
                {
                    unreached();
                    throw new FatalJitException(CORJIT_SKIPPED, "An eight-byte floating register store requires a 64-bit target.");
                }
            }

            return ins_Store(dstType, aligned);
        }

        if (varTypeUsesMaskReg(dstType))
        {
            if (!Configuration.FEATURE_MASKED_HW_INTRINSICS)
            {
                throw new FatalJitException(CORJIT_SKIPPED,
                    "Masked-register stores require masked hardware intrinsics.");
            }

            if (genIsValidMaskReg(srcReg))
            {
                return ins_Store(dstType, aligned);
            }

            assert(genIsValidIntOrFakeReg(srcReg));
            return ins_Store(dstType, aligned);
        }
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
                if (Configuration.TARGET_64BIT)
                {
                    assert(dstSize == 8);
                    dstType = TYP_LONG;
                }
                else
                {
                    unreached();
                    throw new FatalJitException(CORJIT_SKIPPED, "An eight-byte integer register store requires a 64-bit target.");
                }
            }
        }
        else
        {
            assert(genIsValidFloatReg(srcReg));
        }

        return ins_Store(dstType, aligned);
    }
}
