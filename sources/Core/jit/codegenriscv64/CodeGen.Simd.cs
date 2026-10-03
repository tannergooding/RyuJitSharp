// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64 && FEATURE_SIMD
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public insOpts genGetSimdInsOpt(emitAttr size, var_types elementType)
    {
        NYI_RISCV64("genGetSimdInsOpt-----unimplemented/unused on RISCV64 yet----");
        return INS_OPTS_NONE;
    }

    // The native SIMDIntrinsicID type is unported; this unused NYI path never inspects its value.
    public unsafe instruction getOpForSIMDIntrinsic(int intrinsicId, var_types baseType, uint* ival = null)
    {
        NYI_RISCV64("getOpForSIMDIntrinsic-----unimplemented/unused on RISCV64 yet----");
        return INS_invalid;
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicInit(GenTree simdNode)
    {
        NYI_RISCV64("genSIMDIntrinsicInit-----unimplemented/unused on RISCV64 yet----");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicInitN(GenTree simdNode)
    {
        NYI_RISCV64("genSIMDIntrinsicInitN-----unimplemented/unused on RISCV64 yet----");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicUnOp(GenTree simdNode)
    {
        NYI_RISCV64("genSIMDIntrinsicUnOp-----unimplemented/unused on RISCV64 yet----");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicWiden(GenTree simdNode)
    {
        NYI_RISCV64("genSIMDIntrinsicWiden-----unimplemented/unused on RISCV64 yet----");
    }
}
#endif
