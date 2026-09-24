// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
#if TARGET_XARCH
    public static int instKMaskBaseSize(instruction ins)
    {
        assert((uint)ins < (uint)s_kMaskBaseSizes.Length);
        return s_kMaskBaseSizes[(int)ins];
    }

    public static bool instIsEmbeddedMaskingCompatible(instruction ins)
    {
        return (ins is not INS_invalid) && (instKMaskBaseSize(ins) != 0);
    }

    public bool IsEmbeddedBroadcastEnabled(instruction ins, GenTree operand)
    {
#if FEATURE_HW_INTRINSICS
        if (!Compiler.canUseEvexEncoding() || !s_broadcastCompatible[(int)ins])
        {
            return false;
        }

        bool evexEncodable;
        if (s_evexCompatible[(int)ins])
        {
            evexEncodable = (ins < INS_vpdpwsud) || (ins > INS_vpdpbuuds) ||
                Compiler.compSupportsHWIntrinsic(InstructionSet_AVXVNNIINT_V512);
        }
        else
        {
            evexEncodable = ins switch {
                INS_aesdec or INS_aesdeclast or INS_aesenc or INS_aesenclast or INS_pclmulqdq =>
                    Compiler.compSupportsHWIntrinsic(InstructionSet_AES_V512),
                INS_vpdpbusd or INS_vpdpwssd or INS_vpdpbusds or INS_vpdpwssds =>
                    Compiler.compSupportsHWIntrinsic(InstructionSet_AVX512v3),
                INS_vpmadd52huq or INS_vpmadd52luq =>
                    Compiler.compSupportsHWIntrinsic(InstructionSet_AVX512v2),
                _ => false,
            };
        }

        return evexEncodable && operand.IsContained && operand.Oper.IsHWIntrinsic &&
            operand.AsHWIntrinsic().IsBroadcastScalar;
#else
        return false;
#endif
    }
#endif
}
