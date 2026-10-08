// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_LOONGARCH64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Emitter
{
    public void emitInsLoadStoreOp(instruction ins, emitAttr attr, regNumber dataReg, GenTreeIndir indir)
    {
        var compiler = _compiler ??
            throw new FatalJitException(CORJIT_INTERNALERROR, "LoongArch64 indirect load/store recording requires an active compiler.");
        var addr = indir.Addr;

        if (addr.IsContained)
        {
            assert(addr.Oper is GT_LCL_ADDR or GT_LEA);

            var offset = 0;
            var shift = 0;
            if (addr.Oper is GT_LEA)
            {
                var addrMode = addr.AsAddrMode();
                offset = addrMode.Offset;

                if (addrMode.Scale > 0)
                {
                    assert((addrMode.Scale & (addrMode.Scale - 1)) == 0);
                    shift = System.Numerics.BitOperations.TrailingZeroCount((uint)addrMode.Scale);
                }
            }

            var memBase = indir.Base;
            var addType = (memBase is not null) && varTypeIsGC(memBase.Type) ? EA_BYREF : EA_PTRSIZE;

            if (indir.HasIndex)
            {
                var index = indir.Index ??
                    throw new FatalJitException(CORJIT_INTERNALERROR, "LoongArch64 contained address is missing its index tree.");
                var baseTree = memBase ??
                    throw new FatalJitException(CORJIT_INTERNALERROR, "LoongArch64 indexed address is missing its base tree.");
                var baseReg = baseTree.RegNum;
                var indexReg = index.RegNum;

                if (offset != 0)
                {
                    var tmpReg = codeGen.InternalRegisters.GetSingle(indir);

                    if (isValidSimm12(offset))
                    {
                        if (shift > 0)
                        {
                            emitIns_R_R_I(INS_slli_d, addType, REG_R21, indexReg, shift);
                            emitIns_R_R_R(INS_add_d, addType, tmpReg, baseReg, REG_R21);
                        }
                        else
                        {
                            emitIns_R_R_R(INS_add_d, addType, tmpReg, baseReg, indexReg);
                        }

                        assert(emitInsIsLoad(ins) || (tmpReg != dataReg));
                        emitIns_R_R_I(ins, attr, dataReg, tmpReg, offset);
                    }
                    else
                    {
                        emitIns_I_la(EA_PTRSIZE, tmpReg, offset);
                        emitIns_R_R_R(INS_add_d, addType, tmpReg, tmpReg, baseReg);

                        assert(emitInsIsLoad(ins) || (tmpReg != dataReg));
                        assert(tmpReg != indexReg);

                        emitIns_R_R_I(INS_slli_d, addType, REG_R21, indexReg, shift);
                        emitIns_R_R_R(INS_add_d, addType, tmpReg, tmpReg, REG_R21);
                        emitIns_R_R_I(ins, attr, dataReg, tmpReg, 0);
                    }
                }
                else
                {
                    switch (EA_SIZE(indir.Type.EmitSize))
                    {
                        case EA_1BYTE:
                        {
                            assert((((int)ins <= (int)INS_ld_wu) && ((int)ins >= (int)INS_ld_b)) ||
                                   (((int)ins <= (int)INS_st_d) && ((int)ins >= (int)INS_st_b)));
                            if ((int)ins <= (int)INS_ld_wu)
                            {
                                ins = varTypeIsUnsigned(indir.Type) ? INS_ldx_bu : INS_ldx_b;
                            }
                            else
                            {
                                ins = INS_stx_b;
                            }

                            break;
                        }
                        case EA_2BYTE:
                        {
                            assert((((int)ins <= (int)INS_ld_wu) && ((int)ins >= (int)INS_ld_b)) ||
                                   (((int)ins <= (int)INS_st_d) && ((int)ins >= (int)INS_st_b)));
                            if ((int)ins <= (int)INS_ld_wu)
                            {
                                ins = varTypeIsUnsigned(indir.Type) ? INS_ldx_hu : INS_ldx_h;
                            }
                            else
                            {
                                ins = INS_stx_h;
                            }

                            break;
                        }
                        case EA_4BYTE:
                        {
                            assert(((((int)ins <= (int)INS_ld_wu) && ((int)ins >= (int)INS_ld_b)) ||
                                    (((int)ins <= (int)INS_st_d) && ((int)ins >= (int)INS_st_b)) ||
                                    (ins is INS_fst_s or INS_fld_s)));
                            assert((int)INS_fst_s > (int)INS_st_d);

                            if ((int)ins <= (int)INS_ld_wu)
                            {
                                ins = varTypeIsUnsigned(indir.Type) ? INS_ldx_wu : INS_ldx_w;
                            }
                            else if (ins is INS_fld_s)
                            {
                                ins = INS_fldx_s;
                            }
                            else if (ins is INS_fst_s)
                            {
                                ins = INS_fstx_s;
                            }
                            else
                            {
                                ins = INS_stx_w;
                            }

                            break;
                        }
                        case EA_8BYTE:
                        {
                            assert(((((int)ins <= (int)INS_ld_wu) && ((int)ins >= (int)INS_ld_b)) ||
                                    (((int)ins <= (int)INS_st_d) && ((int)ins >= (int)INS_st_b)) ||
                                    (ins is INS_fst_d or INS_fld_d)));
                            assert((int)INS_fst_d > (int)INS_st_d);

                            if ((int)ins <= (int)INS_ld_wu)
                            {
                                ins = INS_ldx_d;
                            }
                            else if (ins is INS_fld_d)
                            {
                                ins = INS_fldx_d;
                            }
                            else if (ins is INS_fst_d)
                            {
                                ins = INS_fstx_d;
                            }
                            else
                            {
                                ins = INS_stx_d;
                            }

                            break;
                        }
                        default:
                        {
                            assert(false, "Unsupported LoongArch64 indirect load/store size.");
                            throw new FatalJitException(CORJIT_INTERNALERROR, "Unsupported LoongArch64 indirect load/store size.");
                        }
                    }

                    if (shift > 0)
                    {
                        emitIns_R_R_I(INS_slli_d, index.Type.EmitActualSize, REG_R21, indexReg, shift);
                        emitIns_R_R_R(ins, attr, dataReg, baseReg, REG_R21);
                    }
                    else
                    {
                        emitIns_R_R_R(ins, attr, dataReg, baseReg, indexReg);
                    }
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
            else if (isValidSimm12(offset))
            {
                var baseTree = memBase ??
                    throw new FatalJitException(CORJIT_INTERNALERROR, "LoongArch64 contained address is missing its base tree.");
                emitIns_R_R_I(ins, attr, dataReg, baseTree.RegNum, offset);
            }
            else
            {
                var baseTree = memBase ??
                    throw new FatalJitException(CORJIT_INTERNALERROR, "LoongArch64 contained address is missing its base tree.");
                var tmpReg = codeGen.InternalRegisters.GetSingle(indir);

                emitIns_I_la(EA_PTRSIZE, tmpReg, offset);
                emitIns_R_R_R(INS_add_d, addType, tmpReg, baseTree.RegNum, tmpReg);
                emitIns_R_R_I(ins, attr, dataReg, tmpReg, 0);
            }
        }
        else
        {
#if DEBUG
            if (addr.Oper is GT_LCL_ADDR)
            {
                ref var local = ref compiler.lvaGetDesc(addr.AsLclVarCommon().LclNum);
                assert(!local.lvTracked);
            }
#endif
            emitIns_R_R_I(ins, attr, dataReg, addr.RegNum, 0);
        }
    }
}
#endif
