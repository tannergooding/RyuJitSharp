// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_TT_RV(instruction ins, emitAttr size, GenTree tree, regNumber reg)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Register-to-local stores outside AMD64 are not implemented.");
#else
#if DEBUG
        assert(reg != REG_STK);
        var isValidInReg = (tree.Flags & GTF_SPILLED) == 0;

        if (!isValidInReg && ((tree.Flags & GTF_SPILL) != 0) && (tree.Oper == GT_STORE_LCL_VAR))
        {
            isValidInReg = true;
        }

        assert(isValidInReg);
        assert(size != EA_UNKNOWN);
        assert(tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR);
#endif
        var varNum = tree.AsLclVarCommon().LclNum;
        assert((uint)varNum < (uint)_compiler.lvaCount);
        Emitter.emitIns_S_R(ins, size, reg, varNum, 0);
#endif
    }

    public instruction ins_Store(var_types dstType, bool aligned = false)
    {
#if TARGET_XARCH
        if (varTypeUsesIntReg(dstType))
        {
            return INS_mov;
        }

#if FEATURE_MASKED_HW_INTRINSICS
        if (varTypeUsesMaskReg(dstType))
        {
            return INS_kmovq_msk;
        }
#endif

        assert(varTypeUsesFloatReg(dstType));
        var dstSize = dstType.Size;

        if (dstSize == 4)
        {
            return INS_movss;
        }
        else if (dstSize == 8)
        {
            return INS_movsd_simd;
        }
        else
        {
            assert(dstSize is 12 or 16 or 32 or 64);
            // Prefer movaps/movups over movapd/movupd: they avoid a 66h prefix.
            return aligned ? INS_movaps : INS_movups;
        }
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Store instruction selection outside xarch is not implemented.");
#endif
    }
}
