// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void inst_RV_RV_RV(instruction ins, regNumber reg1, regNumber reg2, regNumber reg3,
        emitAttr size, insFlags flags = INS_FLAGS_DONT_CARE)
    {
        if (Configuration.TARGET_ARM)
        {
            Emitter.emitIns_R_R_R(ins, size, reg1, reg2, reg3, flags);
            return;
        }

        if (Configuration.TARGET_XARCH || Configuration.TARGET_LOONGARCH64 || Configuration.TARGET_RISCV64)
        {
            Emitter.emitIns_R_R_R(ins, size, reg1, reg2, reg3);
            return;
        }

        NYI("inst_RV_RV_RV");
        throw new FatalJitException(CORJIT_SKIPPED, "inst_RV_RV_RV is not implemented for this target.");
    }

    public void inst_IV_handle(instruction ins, nint value)
    {
        Emitter.emitIns_I(ins, EA_PTRSIZE | EA_CNS_RELOC_FLG, value);
    }

    public void inst_set_SV_var(GenTree tree)
    {
        if (Configuration.DEBUG)
        {
            assert(tree is not null && ((tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR) ||
                ((tree.Oper is GT_LCL_ADDR) && (tree.AsLclFld().LclOffs == 0))));
            assert((uint)tree.AsLclVarCommon().LclNum < (uint)_compiler.lvaCount);

            if (tree is not GenTreeLclVar local)
            {
                throw new FatalJitException(CORJIT_INTERNALERROR, "Stack variable reference requires a local variable or store.");
            }

            Emitter.emitVarRefOffs = local.LclIlOffs;
        }
    }
}
