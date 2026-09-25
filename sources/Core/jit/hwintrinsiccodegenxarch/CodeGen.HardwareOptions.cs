// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

#if FEATURE_HW_INTRINSICS && TARGET_XARCH
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void assertIsContainableHWIntrinsicOp(GenTreeHWIntrinsic parent, GenTree operand)
    {
#if DEBUG
        // Detached temporary operands cannot be checked against evaluation order.
        if (operand.Next is null)
        {
            return;
        }

        var lowering = _compiler.LoweringPhase;
        assert(lowering is not null);
        var containable = lowering.IsContainableHWIntrinsicOp(parent, operand, out var supportsRegOptional);
        assert(containable || supportsRegOptional);
#endif
    }

    internal static insOpts AddEmbRoundingMode(insOpts options, sbyte mode)
    {
        // Embedded rounding suppresses FP exceptions and ignores MXCSR.RC.
        // Only RC's two bits matter; native RS/P and reserved bits are ignored.
        assert((options & INS_OPTS_EVEX_b_MASK) == 0);
        switch (mode & 3)
        {
            case 1:
            {
                options |= INS_OPTS_EVEX_er_rd;
                break;
            }

            case 2:
            {
                options |= INS_OPTS_EVEX_er_ru;
                break;
            }

            case 3:
            {
                options |= INS_OPTS_EVEX_er_rz;
                break;
            }
        }

        return options;
    }

    internal static insOpts AddEmbMaskingMode(insOpts options, regNumber maskReg, bool mergeWithZero)
    {
        assert((options & INS_OPTS_EVEX_aaa_MASK) == 0);
        assert((options & INS_OPTS_EVEX_z_MASK) == 0);
        var mask = (uint)(maskReg - KBASE) << 2;
        assert(genIsValidMaskReg(maskReg));
        assert((mask & (uint)INS_OPTS_EVEX_aaa_MASK) == mask);
        options |= (insOpts)mask;
        if (mergeWithZero)
        {
            options |= INS_OPTS_EVEX_em_zero;
        }

        return options;
    }

    internal static uint GetImmediateMaxAndMask(instruction ins, uint simdSize, out uint mask)
    {
        assert((simdSize >= 16) && (simdSize <= 64));
        var lanes = simdSize / 16;
        mask = 0xFF;
        uint max;
        switch (ins)
        {
            case INS_pslldq or INS_psrldq:
            {
                // Byte shifts zero the entire 128-bit lane at 16 or greater.
                max = 16;
                break;
            }

            case INS_palignr:
            {
                // PALIGNR shifts a concatenation of two 128-bit lanes.
                max = 32;
                break;
            }

            case INS_pextrq or INS_pinsrq:
            {
                mask = 1;
                max = mask;
                break;
            }

            case INS_extractps or INS_pextrd or INS_pinsrd:
            {
                mask = 3;
                max = mask;
                break;
            }

            case INS_pextrw or INS_pinsrw:
            {
                mask = 7;
                max = mask;
                break;
            }

            case INS_pextrb or INS_pinsrb:
            {
                mask = 15;
                max = mask;
                break;
            }

            case INS_valignd:
            {
                mask = (simdSize / 4) - 1;
                max = mask;
                break;
            }

            case INS_valignq:
            {
                mask = (simdSize / 8) - 1;
                max = mask;
                break;
            }

            case INS_blendpd or INS_shufpd or INS_vpermilpd:
            {
                assert(lanes <= 4);
                mask = (1u << (int)(lanes * 2)) - 1;
                max = mask;
                break;
            }

            case INS_blendps or INS_vpblendd:
            {
                assert(lanes <= 2);
                mask = (1u << (int)(lanes * 4)) - 1;
                max = mask;
                break;
            }

            case INS_mpsadbw:
            {
                assert(lanes <= 2);
                mask = (1u << (int)(lanes * 3)) - 1;
                max = mask;
                break;
            }

            case INS_vextractf32x4 or INS_vextracti32x4 or INS_vextractf64x2 or INS_vextracti64x2:
            case INS_vinsertf32x4 or INS_vinserti32x4 or INS_vinsertf64x2 or INS_vinserti64x2:
            {
                assert(lanes >= 2);
                mask = lanes - 1;
                max = mask;
                break;
            }

            case INS_vshuff32x4 or INS_vshufi32x4 or INS_vshuff64x2 or INS_vshufi64x2:
            {
                assert(lanes >= 2);
                // Each destination lane selects its source with log2(lanes) bits.
                mask = (1u << (int)(lanes * BitOperations.Log2(lanes))) - 1;
                max = mask;
                break;
            }

            case INS_vextractf32x8 or INS_vextracti32x8 or INS_vextractf64x4 or INS_vextracti64x4:
            case INS_vinsertf32x8 or INS_vinserti32x8 or INS_vinsertf64x4 or INS_vinserti64x4:
            {
                assert(simdSize == 64);
                mask = 1;
                max = mask;
                break;
            }

            case INS_dppd:
            {
                // Broadcast mask [1:0] and element selection mask [5:4].
                mask = 0b00110011;
                max = mask;
                break;
            }

            case INS_pclmulqdq:
            {
                // Source qword selection uses bits 0 and 4.
                mask = 0b00010001;
                max = mask;
                break;
            }

            case INS_vperm2f128 or INS_vperm2i128:
            {
                // Source lane selectors [1:0]/[5:4] and zero controls 3/7.
                mask = 0b10111011;
                max = mask;
                break;
            }

            default:
            {
                max = 255;
                break;
            }
        }

        return max;
    }
}
#endif
