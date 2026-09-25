// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public instruction ins_Load(var_types srcType, bool aligned = false)
    {
#if TARGET_XARCH
        if (varTypeUsesIntReg(srcType))
        {
            if (!varTypeIsSmall(srcType))
            {
                return INS_mov;
            }
            else if (varTypeIsUnsigned(srcType))
            {
                return INS_movzx;
            }
            else
            {
                return INS_movsx;
            }
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(srcType))
        {
            return INS_kmovq_msk;
        }
#endif

        assert(varTypeUsesFloatReg(srcType));
        var srcSize = srcType.Size;

        if (srcSize == 4)
        {
            return INS_movss;
        }
        else if (srcSize == 8)
        {
            return INS_movsd_simd;
        }
        else
        {
            assert(srcSize is 12 or 16 or 32 or 64);
            // Prefer movaps/movups over movapd/movupd: they avoid a 66h prefix.
            return aligned ? INS_movaps : INS_movups;
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Load instruction selection outside xarch is not implemented.");
#endif
    }
}
