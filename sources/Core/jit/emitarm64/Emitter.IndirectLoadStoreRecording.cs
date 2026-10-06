// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM64
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.regNumber;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsLoadStoreOp(instruction ins, emitAttr attr, regNumber dataReg, GenTreeIndir indir)
    {
        var addr = indir.Addr;

        if (addr.IsContained)
        {
            assert((addr.Oper is GT_LCL_ADDR or GT_LEA) ||
                (addr.Oper.IsCnsIntOrI && addr.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL)));

            var offset = 0;
            var lsl = 0;

            if (addr.Oper is GT_LEA)
            {
                var addrMode = addr.AsAddrMode();
                offset = addrMode.Offset;
                if (addrMode.Scale > 0)
                {
                    assert((addrMode.Scale & (addrMode.Scale - 1)) == 0);
                    lsl = System.Numerics.BitOperations.Log2(addrMode.Scale);
                }
            }

            var memBase = indir.Base;
            if (indir.HasIndex)
            {
                var index = indir.Index;
                assert(memBase is not null);

                if (offset != 0)
                {
                    var tmpReg = codeGen.InternalRegisters.GetSingle(indir);
                    var addType = varTypeIsGC(memBase.Type) ? EA_BYREF : EA_PTRSIZE;

                    if (emitIns_valid_imm_for_add(offset, EA_8BYTE))
                    {
                        if (lsl > 0)
                        {
                            emitIns_R_R_R_I(
                                INS_add, addType, tmpReg, memBase.RegNum, index.RegNum, lsl, INS_OPTS_LSL);
                        }
                        else
                        {
                            emitIns_R_R_R(INS_add, addType, tmpReg, memBase.RegNum, index.RegNum);
                        }

                        noway_assert(emitInsIsLoad(ins) || (tmpReg != dataReg));
                        emitIns_R_R_I(ins, attr, dataReg, tmpReg, offset);
                    }
                    else
                    {
                        codeGen.instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, offset);
                        emitIns_R_R_R(INS_add, addType, tmpReg, memBase.RegNum, tmpReg);

                        noway_assert(emitInsIsLoad(ins) || (tmpReg != dataReg));
                        noway_assert(tmpReg != index.RegNum);
                        emitIns_R_R_R_I(
                            ins, attr, dataReg, tmpReg, index.RegNum, lsl, INS_OPTS_LSL);
                    }
                }
                else if (lsl > 0)
                {
                    emitIns_R_R_R_Ext(
                        ins, attr, dataReg, memBase.RegNum, index.RegNum, INS_OPTS_LSL, lsl);
                }
                else if ((index.Oper is GT_BFIZ or GT_CAST) && index.IsContained)
                {
                    var cast = index.Oper is GT_BFIZ ? index.AsOp().Op1.AsCast() : index.AsCast();
                    var shift = index.Oper is GT_BFIZ
                        ? unchecked((int)index.AsOp().Op2.AsIntConCommon().IconValue)
                        : 0;

                    assert(cast.IsContained);
                    emitIns_R_R_R_Ext(
                        ins,
                        attr,
                        dataReg,
                        memBase.RegNum,
                        cast.CastOp.RegNum,
                        cast.IsUnsigned ? INS_OPTS_UXTW : INS_OPTS_SXTW,
                        shift);
                }
                else
                {
                    emitIns_R_R_R(ins, attr, dataReg, memBase.RegNum, index.RegNum);
                }
            }
            else if (addr.Oper is GT_LCL_ADDR)
            {
                var local = addr.AsLclVarCommon();
                if (emitInsIsStore(ins))
                {
                    emitIns_S_R(ins, attr, dataReg, local.LclNum, local.LclOffs);
                }
                else
                {
                    emitIns_R_S(ins, attr, dataReg, local.LclNum, local.LclOffs);
                }
            }
            else if (addr.Oper.IsCnsIntOrI && addr.AsIntCon().IsIconHandle(GTF_ICON_TLS_HDL))
            {
                emitIns_R_R_I(ins, attr, dataReg, REG_R18, addr.AsIntCon().IconValue);
            }
            else if (emitIns_valid_imm_for_ldst_offset(indir.Offset, indir.Type.EmitSize))
            {
                assert(memBase is not null);
                emitIns_R_R_I(ins, attr, dataReg, memBase.RegNum, indir.Offset);
            }
            else
            {
                assert(memBase is not null);
                var tmpReg = codeGen.InternalRegisters.GetSingle(indir);
                codeGen.instGen_Set_Reg_To_Imm(EA_PTRSIZE, tmpReg, indir.Offset);
                emitIns_R_R_R(ins, attr, dataReg, memBase.RegNum, tmpReg);
            }
        }
        else
        {
#if DEBUG
            if (addr.Oper is GT_LCL_ADDR)
            {
                assert(_compiler is not null);
                ref var varDsc = ref _compiler.lvaGetDesc(addr.AsLclVarCommon().LclNum);
                assert(!varDsc.lvTracked);
            }
#endif
            emitIns_R_R(ins, attr, dataReg, addr.RegNum);
        }
    }
}
#endif
