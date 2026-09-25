// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_AMD64
    private static int emitSizeOfInsDsc_AMD(instrDesc id)
    {
        assert(!id.idIsSmallDsc());
#if DEBUG
        var idOp = (ID_OPS)emitFmtToOps[(int)id.idInsFmt()];
        assert(idOp is ID_OPS.ID_OP_AMD or ID_OPS.ID_OP_AMD_CNS);
#endif
        if (id.idIsLargeCns())
        {
            return id.idIsLargeDsp() ? ConstantDescriptorSizes.ConstantAddressMode : ConstantDescriptorSizes.Constant;
        }
        if (id.idIsLargeDsp())
        {
            return ConstantDescriptorSizes.AddressMode;
        }

        return INSTR_DESC_SIZE;
    }

    private static int emitSizeOfInsDsc_CNS(instrDesc id)
    {
#if DEBUG
        var idOp = (ID_OPS)emitFmtToOps[(int)id.idInsFmt()];
        assert(idOp is ID_OPS.ID_OP_CNS or ID_OPS.ID_OP_SCNS);
#endif
        if (id.idIsSmallDsc())
        {
            return SMALL_IDSC_SIZE;
        }
        if (id.idIsLargeCns())
        {
            return ConstantDescriptorSizes.Constant;
        }

        return INSTR_DESC_SIZE;
    }

    private static int emitSizeOfInsDsc_NONE(instrDesc id)
    {
#if DEBUG
        assert((ID_OPS)emitFmtToOps[(int)id.idInsFmt()] == ID_OPS.ID_OP_NONE);
#endif
        if (id.idIsSmallDsc())
        {
            return SMALL_IDSC_SIZE;
        }
#if FEATURE_LOOP_ALIGN
        if (id.idIns() == INS_align)
        {
            return DescriptorSizes.Align;
        }
#endif
        return INSTR_DESC_SIZE;
    }

    private static int emitSizeOfInsDsc_SPEC(instrDesc id)
    {
        assert(!id.idIsSmallDsc());
#if DEBUG
        var idOp = (ID_OPS)emitFmtToOps[(int)id.idInsFmt()];
        assert(idOp is ID_OPS.ID_OP_CALL or ID_OPS.ID_OP_SPEC);
#endif
        if (id.idIsLargeCall())
        {
            return ((instrDescCGCA)id).NativeLogicalSize;
        }
        if (id.idIsLargeCns())
        {
            return id.idIsLargeDsp() ? ConstantDescriptorSizes.ConstantDisplacement : ConstantDescriptorSizes.Constant;
        }
        if (id.idIsLargeDsp())
        {
            return ConstantDescriptorSizes.Displacement;
        }

        return INSTR_DESC_SIZE;
    }

    private static int emitSizeOfInsDsc_DSP(instrDesc id)
    {
        assert(!id.idIsSmallDsc());
#if DEBUG
        var idOp = (ID_OPS)emitFmtToOps[(int)id.idInsFmt()];
        assert(idOp is ID_OPS.ID_OP_DSP or ID_OPS.ID_OP_DSP_CNS);
#endif
        if (id.idIsLargeCns())
        {
            return id.idIsLargeDsp() ? ConstantDescriptorSizes.ConstantDisplacement : ConstantDescriptorSizes.Constant;
        }
        if (id.idIsLargeDsp())
        {
            return ConstantDescriptorSizes.Displacement;
        }

        return INSTR_DESC_SIZE;
    }
#endif
}
