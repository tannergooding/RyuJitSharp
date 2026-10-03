// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genLclHeap(GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 local-heap generation is not ported.");
    }

    private void genSetRegToConst(regNumber targetReg, var_types targetType, GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 constant materialization is not ported.");
    }

    private void genCodeForShift(GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 shift generation is not ported.");
    }

    private void genCodeForLclAddr(GenTreeLclFld tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 local-address generation is not ported.");
    }

    private void genCodeForLclFld(GenTreeLclFld tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 local-field load generation is not ported.");
    }

    private void genLeaInstruction(GenTreeAddrMode tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 address-mode generation is not ported.");
    }

    private void genCodeForIndexAddr(GenTreeIndexAddr tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 index-address generation is not ported.");
    }

    private void genFloatToFloatCast(GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 floating-point cast generation is not ported.");
    }

    private void genIntToIntCast(GenTreeCast tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 integer cast generation is not ported.");
    }

    private void genIntrinsic(GenTreeIntrinsic tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 intrinsic generation is not ported.");
    }

#if FEATURE_HW_INTRINSICS
    private void genHWIntrinsic(GenTreeHWIntrinsic tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 hardware-intrinsic generation is not ported.");
    }
#endif

    private void genCodeForNullCheck(GenTreeIndir tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 null-check generation is not ported.");
    }

    private void genRangeCheck(GenTree tree)
    {
        throw new FatalJitException(CORJIT_SKIPPED, "LoongArch64 range-check generation is not ported.");
    }

}
#endif
