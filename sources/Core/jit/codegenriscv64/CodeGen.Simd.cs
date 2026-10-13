// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.CorJitResult;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genSIMDIntrinsic(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsic-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD intrinsic generation is not ported.");
    }

    public void genSIMDIntrinsicUpperSave(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicUpperSave-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD upper-lane saving is not ported.");
    }

    public void genSIMDIntrinsicUpperRestore(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicUpperRestore-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD upper-lane restoration is not ported.");
    }

    public void genStoreIndTypeSIMD12(GenTree treeNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genStoreIndTypeSIMD12-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD12 indirect stores are not ported.");
    }

    public void genLoadIndTypeSIMD12(GenTree treeNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genLoadIndTypeSIMD12-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD12 indirect loads are not ported.");
    }

    public void genStoreLclTypeSIMD12(GenTree treeNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genStoreLclTypeSIMD12-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD12 local stores are not ported.");
    }

    public insOpts genGetSimdInsOptRiscV64(emitAttr size, var_types elementType)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genGetSimdInsOpt-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD instruction options are not ported.");
    }

    // The native SIMDIntrinsicID type is unported; this unused NYI path never inspects its value.
    public unsafe instruction getOpForSIMDIntrinsic(int intrinsicId, var_types baseType, uint* ival = null)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: getOpForSIMDIntrinsic-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD instruction selection is not ported.");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicInit(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicInit-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD initialization is not ported.");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicInitN(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicInitN-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD multi-operand initialization is not ported.");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicUnOp(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicUnOp-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 unary SIMD intrinsic generation is not ported.");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicWiden(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicWiden-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD widening is not ported.");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicNarrow(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicNarrow-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD narrowing is not ported.");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicBinOp(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicBinOp-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 binary SIMD intrinsic generation is not ported.");
    }

    // The upstream implementation is an unused NYI path and does not inspect the unported SIMD node.
    public void genSIMDIntrinsicGetItem(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicGetItem-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD get-item intrinsic generation is not ported.");
    }

    // The upstream implementation is an unused NYI path and does not inspect the unported SIMD node.
    public void genSIMDIntrinsicSetItem(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicSetItem-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD set-item intrinsic generation is not ported.");
    }

    // GenTreeSIMD is not ported, and this unused helper terminates before inspecting its node.
    public void genSIMDIntrinsicRelOp(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicRelOp-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 SIMD comparison is not ported.");
    }

    public void genSIMDIntrinsicDotProduct(GenTree simdNode)
    {
        if (Configuration.TARGET_RISCV64)
        {
            NYIRAW("NYI_RISCV64: genSIMDIntrinsicDotProduct-----unimplemented/unused on RISCV64 yet----");
        }
        throw new FatalJitException(CORJIT_SKIPPED, "RISC-V64 dot-product SIMD intrinsic generation is not ported.");
    }
}
