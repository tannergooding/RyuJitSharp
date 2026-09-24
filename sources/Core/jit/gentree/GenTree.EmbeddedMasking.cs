// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class GenTree
{
#if FEATURE_HW_INTRINSICS
    public bool IsEmbeddedMaskingCompatible()
    {
        if (!Oper.IsHWIntrinsic)
        {
            return false;
        }

        var node = AsHWIntrinsic();
        var intrinsic = node.HWIntrinsicId;
#if TARGET_XARCH
        var baseType = node.SimdBaseType;
        if ((baseType is TYP_UNKNOWN) ||
            !HWIntrinsicInfo.genIsTableDrivenHWIntrinsic(intrinsic, HWIntrinsicInfo.lookupCategory(intrinsic)) ||
            node.IsMemoryLoadOrStore)
        {
            return false;
        }
        return CodeGen.instIsEmbeddedMaskingCompatible(HWIntrinsicInfo.lookupIns(intrinsic, baseType, null));
#elif TARGET_ARM64
        var flags = HWIntrinsicInfo.lookupFlags(intrinsic);
        return (flags & (HW_Flag_EmbeddedMaskedOperation | HW_Flag_OptionalEmbeddedMaskedOperation)) != 0;
#else
        return false;
#endif
    }

    public bool IsRmwHWIntrinsic(Compiler compiler)
    {
        assert(Oper.IsHWIntrinsic);
        var node = AsHWIntrinsic();
        var intrinsic = node.HWIntrinsicId;
#if TARGET_XARCH
        if (!compiler.canUseVexEncoding())
        {
            return HWIntrinsicInfo.HasRMWSemantics(intrinsic);
        }
        if (HWIntrinsicInfo.IsRmwIntrinsic(intrinsic))
        {
            return true;
        }
        switch (intrinsic)
        {
            case NI_AVX512_BlendVariableMask:
            {
                if ((node.GetOp(2).Flags & GTF_HW_EM_OP) == 0)
                {
                    return false;
                }
                goto case NI_AVX512_CompressMask;
            }

            case NI_AVX512_CompressMask:
            case NI_AVX512_ExpandMask:
            {
                var first = node.GetOp(1);
                if (first.IsContained)
                {
                    assert(first.IsVectorZero);
                    return false;
                }
                return true;
            }

            case NI_AVX512_Fixup:
            case NI_AVX512_FixupScalar:
            {
                var table = node.GetOp(3);
                if ((node.GetOp(4).Oper is not GT_CNS_INT and not GT_CNS_LNG) || !table.Oper.IsCnsVec)
                {
                    return true;
                }

                var count = intrinsic is NI_AVX512_FixupScalar ? 1 : node.SimdSize / sizeof(uint);
                var stride = node.SimdBaseType is TYP_FLOAT ? 1 : 2;
                for (var index = 0; index < count; index += stride)
                {
                    var value = table.AsVecCon().SimdVal.u32[index];
                    if (((value & 0x0000000F) == 0) || ((value & 0x000000F0) == 0) ||
                        ((value & 0x00000F00) == 0) || ((value & 0x0000F000) == 0) ||
                        ((value & 0x000F0000) == 0) || ((value & 0x00F00000) == 0) ||
                        ((value & 0x0F000000) == 0) || ((value & 0xF0000000) == 0))
                    {
                        return true;
                    }
                }
                return false;
            }

            case NI_AVX512_TernaryLogic:
            {
                var immediate = node.GetOp(4);
                if (immediate.Oper is not GT_CNS_INT and not GT_CNS_LNG)
                {
                    return true;
                }

                var control = unchecked((byte)immediate.AsIntConCommon().IntegralValue);
                // A, B, and C select truth-table bits 4, 2, and 1. Each input is used
                // precisely when flipping its bit changes at least one output.
                return (((control ^ (control >> 4)) & 0x0F) != 0) &&
                    (((control ^ (control >> 2)) & 0x33) != 0) &&
                    (((control ^ (control >> 1)) & 0x55) != 0);
            }

            default:
            {
                return false;
            }
        }
#elif TARGET_ARM64
        return HWIntrinsicInfo.HasRMWSemantics(intrinsic);
#else
        return false;
#endif
    }

#if TARGET_XARCH
    public bool IsEmbeddedMaskingCompatible(Compiler compiler, int targetMaskSize, ref var_types targetBaseType)
    {
        return IsEmbeddedMaskingCompatible(compiler, targetMaskSize, ref targetBaseType, out _);
    }

    public bool IsEmbeddedMaskingCompatible(Compiler compiler, int targetMaskSize, ref var_types targetBaseType,
        out int broadcastOperandIndex)
    {
        broadcastOperandIndex = 0;
        if (!IsEmbeddedMaskingCompatible() || !compiler.opts.Tier0OptimizationEnabled ||
            (JitConfig.EnableEmbeddedMasking == 0) || NodeOrContainedOperandsMayThrow(compiler) ||
            IsRmwHWIntrinsic(compiler))
        {
            return false;
        }

        var node = AsHWIntrinsic();
        var intrinsic = node.HWIntrinsicId;
        var baseType = node.SimdBaseType;
        var ins = HWIntrinsicInfo.lookupIns(intrinsic, baseType, compiler);
        var maskBaseSize = CodeGen.instKMaskBaseSize(ins);
        var laneCount = Type.Size / 16;
        var targetMaskBaseSize = targetMaskSize / laneCount;
        targetBaseType = TYP_UNDEF;

        if (maskBaseSize != targetMaskBaseSize)
        {
            var supportsTwoOrFour = false;
            var broadcastIndex = 0;
            switch (ins)
            {
                case INS_andpd:
                case INS_andps:
                case INS_andnpd:
                case INS_andnps:
                case INS_orpd:
                case INS_orps:
                case INS_pandd:
                case INS_pandnd:
                case INS_pord:
                case INS_pxord:
                case INS_vpandq:
                case INS_vpandnq:
                case INS_vporq:
                case INS_vpxorq:
                case INS_vshuff32x4:
                case INS_vshuff64x2:
                case INS_vshufi32x4:
                case INS_vshufi64x2:
                case INS_xorpd:
                case INS_xorps:
                {
                    broadcastIndex = 2;
                    break;
                }

                case INS_vpternlogd:
                case INS_vpternlogq:
                {
                    broadcastIndex = 3;
                    break;
                }

                case INS_vbroadcastf32x4:
                case INS_vbroadcastf32x8:
                case INS_vbroadcastf64x2:
                case INS_vbroadcastf64x4:
                case INS_vbroadcasti32x4:
                case INS_vbroadcasti32x8:
                case INS_vbroadcasti64x2:
                case INS_vbroadcasti64x4:
                case INS_vextractf32x4:
                case INS_vextractf32x8:
                case INS_vextractf64x2:
                case INS_vextractf64x4:
                case INS_vextracti32x4:
                case INS_vextracti32x8:
                case INS_vextracti64x2:
                case INS_vextracti64x4:
                case INS_vinsertf32x4:
                case INS_vinsertf32x8:
                case INS_vinsertf64x2:
                case INS_vinsertf64x4:
                case INS_vinserti32x4:
                case INS_vinserti32x8:
                case INS_vinserti64x2:
                case INS_vinserti64x4:
                {
                    assert(maskBaseSize is 2 or 4);
                    supportsTwoOrFour = true;
                    break;
                }
            }

            if (broadcastIndex != 0)
            {
                assert(maskBaseSize is 2 or 4);
                var codeGen = compiler.codeGen;
                assert(codeGen is not null);
                if (!codeGen.IsEmbeddedBroadcastEnabled(ins, node.GetOp(broadcastIndex)))
                {
                    supportsTwoOrFour = true;
                }
                else if (maskBaseSize == 4)
                {
                    supportsTwoOrFour = true;
                    broadcastOperandIndex = broadcastIndex;
                }
            }

            if (supportsTwoOrFour)
            {
                if (broadcastOperandIndex != 0)
                {
                    var broadcast = node.GetOp(broadcastOperandIndex).AsHWIntrinsic();
                    // Widening a memory broadcast changes its access; only constants may be duplicated.
                    if (broadcast.IsMemoryLoad() || !broadcast.GetOp(1).Oper.IsConst)
                    {
                        return false;
                    }
                }

                if (targetMaskBaseSize == 2)
                {
                    targetBaseType = varTypeIsFloating(baseType) ? TYP_DOUBLE :
                        varTypeIsSigned(baseType) ? TYP_LONG : TYP_ULONG;
                }
                else if (targetMaskBaseSize == 4)
                {
                    targetBaseType = varTypeIsFloating(baseType) ? TYP_FLOAT :
                        varTypeIsSigned(baseType) ? TYP_INT : TYP_UINT;
                }
            }
        }

        if (targetBaseType is not TYP_UNDEF)
        {
            var targetInstruction = HWIntrinsicInfo.lookupIns(intrinsic, targetBaseType, compiler);
            assert(ins != targetInstruction);
            maskBaseSize = CodeGen.instKMaskBaseSize(targetInstruction);
        }

        var maskSize = maskBaseSize * laneCount;
        assert(maskSize != 0);
        return maskSize == targetMaskSize;
    }
#endif
#endif
}
