// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genSetRegToConst(regNumber targetReg, var_types targetType, GenTree tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genSetRegToConstXarch(targetReg, targetType, tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genSetRegToConstArm64(targetReg, targetType, tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genSetRegToConstArm(targetReg, targetType, tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genSetRegToConstLoongArch64(targetReg, targetType, tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genSetRegToConstRiscV64(targetReg, targetType, tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genSetRegToConst));
    }

    public void genCodeForBinary(GenTreeOp tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForBinaryXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForBinaryArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genCodeForBinaryArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForBinaryLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForBinaryRiscV64(tree);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            genCodeForBinaryWasm(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForBinary));
    }

    public void genCodeForBswap(GenTree tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForBswapXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForBswapArm64(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForBswapLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForBswapRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForBswap));
    }

    public void genCodeForCompare(GenTreeOp tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForCompareXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForCompareArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genCodeForCompareArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForCompareLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForCompareRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForCompare));
    }

    public void genCodeForDivMod(GenTreeOp tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForDivModXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForDivModArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genCodeForDivModArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForDivModLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForDivModRiscV64(tree);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            genCodeForDivModWasm(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForDivMod));
    }

    public void genCodeForIndexAddr(GenTreeIndexAddr tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForIndexAddrXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            genCodeForIndexAddrArmArch(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForIndexAddrRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForIndexAddr));
    }

    public void genCodeForIndir(GenTreeIndir tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForIndirXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            genCodeForIndirArmArch(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForIndirLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForIndirRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForIndir));
    }

    public void genCodeForJTrue(GenTreeUnOp tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForJTrueXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForJTrueArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genCodeForJTrueArm(tree);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            genCodeForJTrueWasm(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForJTrue));
    }

    public void genCodeForLclFld(GenTreeLclFld tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForLclFldXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            genCodeForLclFldArmArch(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForLclFldRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForLclFld));
    }

    public void genCodeForMulHi(GenTreeOp tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForMulHiXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForMulHiArm64(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForMulHiLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForMulHiRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForMulHi));
    }

    public void genCodeForNegNot(GenTree tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForNegNotXarch(tree.AsUnOp());
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForNegNotArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genCodeForNegNotArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForNegNotLoongArch64(tree.AsUnOp());
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForNegNotRiscV64(tree.AsUnOp());
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForNegNot));
    }

    public void genCodeForNullCheck(GenTreeIndir tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForNullCheckXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            genCodeForNullCheckArmArch(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            genCodeForNullCheckLoongRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForNullCheck));
    }

    public void genCodeForSelect(GenTreeOp tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForSelectXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForSelectArm64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForSelectRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForSelect));
    }

    public void genCodeForShift(GenTree tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForShiftXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            genCodeForShiftArmArch(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForShiftRiscV64(tree);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            genCodeForShiftWasm(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForShift));
    }

    public void genCodeForStoreInd(GenTreeStoreInd tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForStoreIndXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForStoreIndArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genCodeForStoreIndArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForStoreIndLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForStoreIndRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForStoreInd));
    }

    public void genCodeForStoreLclFld(GenTreeLclFld tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForStoreLclFldXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForStoreLclFldArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genCodeForStoreLclFldArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForStoreLclFldLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForStoreLclFldRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForStoreLclFld));
    }

    public void genCodeForStoreLclVar(GenTreeLclVar tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForStoreLclVarXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForStoreLclVarArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genCodeForStoreLclVarArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForStoreLclVarLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForStoreLclVarRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForStoreLclVar));
    }

    public void genCodeForSwap(GenTreeOp tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCodeForSwapXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCodeForSwapArm64(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCodeForSwapLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCodeForSwapRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCodeForSwap));
    }

    public void genCompareFloat(GenTreeOp tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCompareFloatXarch(tree);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            genCompareFloatWasm(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCompareFloat));
    }

    public void genCompareInt(GenTreeOp tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCompareIntXarch(tree);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            genCompareIntWasm(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCompareInt));
    }

    public void genIntrinsic(GenTreeIntrinsic tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genIntrinsicXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            genIntrinsicArmArch(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genIntrinsicRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genIntrinsic));
    }

    public void genIntToIntCast(GenTreeCast tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genIntToIntCastXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            genIntToIntCastArmArch(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genIntToIntCastRiscV64(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genIntToIntCastLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            genIntToIntCastWasm(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genIntToIntCast));
    }

    public void genJumpTable(GenTree tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genJumpTableXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genJumpTableArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genJumpTableArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genJumpTableLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genJumpTableRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genJumpTable));
    }

    public void genLeaInstruction(GenTreeAddrMode tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genLeaInstructionXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            genLeaInstructionArmArch(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genLeaInstructionRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genLeaInstruction));
    }

    public void genLoadIndTypeSimd12(GenTreeIndir tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genLoadIndTypeSimd12Xarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genLoadIndTypeSimd12Arm64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genLoadIndTypeSIMD12(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genLoadIndTypeSimd12));
    }

    public void genLoadLclTypeSimd12(GenTreeLclVarCommon tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genLoadLclTypeSimd12Xarch(tree);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            genLoadLclTypeSimd12Wasm(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genLoadLclTypeSimd12));
    }

    public void genRangeCheck(GenTree tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genRangeCheckXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            genRangeCheckArmArch(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genRangeCheckRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genRangeCheck));
    }

    public void genStoreLclTypeSimd12(GenTreeLclVarCommon tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genStoreLclTypeSimd12Xarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genStoreLclTypeSimd12Arm64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genStoreLclTypeSIMD12(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genStoreLclTypeSimd12));
    }

    public void genTableBasedSwitch(GenTree tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genTableBasedSwitchXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genTableBasedSwitchArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genTableBasedSwitchArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genTableBasedSwitchLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genTableBasedSwitchRiscV64(tree);
            return;
        }
        if (Configuration.TARGET_WASM)
        {
            genTableBasedSwitchWasm(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genTableBasedSwitch));
    }

    public void genCkfinite(GenTree tree)
    {
        if (Configuration.TARGET_XARCH)
        {
            genCkfiniteXarch(tree);
            return;
        }
        if (Configuration.TARGET_ARM64)
        {
            genCkfiniteArm64(tree);
            return;
        }
        if (Configuration.TARGET_ARM)
        {
            genCkfiniteArm(tree);
            return;
        }
        if (Configuration.TARGET_LOONGARCH64)
        {
            genCkfiniteLoongArch64(tree);
            return;
        }
        if (Configuration.TARGET_RISCV64)
        {
            genCkfiniteRiscV64(tree);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(genCkfinite));
    }

    public void inst_SETCC(GenCondition condition, var_types type, regNumber dstReg)
    {
        if (Configuration.TARGET_XARCH)
        {
            inst_SETCCXarch(condition, type, dstReg);
            return;
        }
        if (Configuration.TARGET_ARMARCH)
        {
            inst_SETCCArmArch(condition, type, dstReg);
            return;
        }

        throwUnsupportedTargetCodeGenMethod(nameof(inst_SETCC));
    }

    private static void throwUnsupportedTargetCodeGenMethod(string methodName)
    {
        throw new FatalJitException(CORJIT_SKIPPED, $"{methodName} is not implemented for the active target.");
    }
}
