// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
#if TARGET_XARCH
    public static bool instrHasImplicitRegPairDest(instruction ins)
        => ins is INS_mulEAX or INS_imulEAX or INS_div or INS_idiv;
#endif

    public unsafe regNumber emitInsBinary(instruction ins, emitAttr attr, GenTree dst, GenTree src,
        regNumber targetReg = REG_NA)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Binary operand recording requires AMD64.");
#else
        RequireSupportedInstructionRecording();
        assert(_compiler is not null);
        var useNdd = UsePromotedEvexEncodings && (targetReg != REG_NA);
        if (useNdd)
        {
            assert(IsApxNddEncodableInstruction(ins));
            assert(targetReg < REG_STK);
            assert(dst.IsUsedFromReg);
            var dstReg = dst.RegNum;
            var srcReg = src.IsUsedFromReg ? src.RegNum : REG_NA;
            assert(targetReg != dstReg);
            assert(targetReg != srcReg);
        }

        GenTree? memOp = null;
        GenTree? cnsOp = null;
        GenTree? otherOp = null;

        // Only one operand can be memory, and only the source can be an immediate.
        // The apparent destination of three-operand IMUL is instead a ModRM operand.
        if (dst.IsContained || (dst.Oper.IsLclField && (dst.RegNum == REG_NA)) || dst.IsUsedFromSpillTemp)
        {
            assert(dst.IsUsedFromMemory || (dst.RegNum == REG_NA) || instrIs3opImul(ins));
            assert(!src.IsUsedFromMemory);
            assert(!useNdd);
            memOp = dst;

            if (src.IsContained)
            {
                assert(src.Oper.IsCnsIntOrI);
                cnsOp = src;
            }
            else
            {
                otherOp = src;
            }
        }
        else if (src.IsContained || src.IsUsedFromSpillTemp)
        {
            assert(!dst.IsUsedFromMemory);
            otherOp = dst;

            if ((src.Oper.IsCnsIntOrI || src.Oper.IsCnsFltOrDbl) && !src.IsUsedFromSpillTemp)
            {
                assert(!src.IsUsedFromMemory || src.Oper.IsCnsFltOrDbl);
                cnsOp = src;
            }
            else
            {
                assert(src.IsUsedFromMemory);
                memOp = src;
            }
        }

        if (memOp is not null)
        {
            TempDsc? temp = null;
            var varNum = BAD_VAR_NUM;
            var offset = uint.MaxValue;

            if (memOp.IsUsedFromSpillTemp)
            {
                assert(memOp.IsRegOptional);
                temp = codeGen.getSpillTempDsc(memOp);
                varNum = temp.tdTempNum;
                offset = 0;
                codeGen.RegSet.tmpRlsTemp(temp);
            }
            else if (memOp.Oper.IsIndir)
            {
                var indir = memOp.AsIndir();
                var memBase = indir.Op1;

                if ((memBase.Oper is GT_LCL_ADDR) && memBase.IsContained)
                {
                    varNum = memBase.AsLclFld().LclNum;
                    offset = memBase.AsLclFld().LclOffs;
                    assert(!indir.HasIndex);
                    assert(indir.Scale == 1);
                    assert(indir.Offset == 0);
                }
                else
                {
                    instrDesc id;
                    if (cnsOp is not null)
                    {
                        assert(ReferenceEquals(memOp, dst));
                        assert(ReferenceEquals(cnsOp, src));
                        assert(otherOp is null);
                        assert(src.Oper.IsCnsIntOrI);
                        assert(!useNdd);
                        id = emitNewInstrAmdCns(attr, indir.Offset, unchecked((int)src.AsIntConCommon().IconValue));
                    }
                    else
                    {
                        id = emitNewInstrAmd(attr, indir.Offset);
                        id.idIns(ins);
                        var regTree = ReferenceEquals(memOp, src) ? dst : src;
                        assert(!regTree.IsContained);
                        id.idReg1(regTree.RegNum);
                    }

                    id.idIns(ins);
                    if (useNdd)
                    {
                        assert(ReferenceEquals(memOp, src));
                        id.idReg1(targetReg);
                        id.idReg2(dst.RegNum);
                        id.idSetEvexNdContext();
                    }

                    insFormat format;
                    if (ReferenceEquals(memOp, src))
                    {
                        assert(cnsOp is null);
                        assert(ReferenceEquals(otherOp, dst));
                        if (instrHasImplicitRegPairDest(ins))
                        {
                            format = emitInsModeFormat(ins, IF_ARD);
                        }
                        else
                        {
                            var baseFormat = useNdd ? IF_RRD_RRD_ARD : IF_RRD_ARD;
                            format = emitInsModeFormat(ins, baseFormat, useNdd);
                        }
                    }
                    else
                    {
                        assert(ReferenceEquals(memOp, dst));
                        assert(!useNdd);
                        if (cnsOp is not null)
                        {
                            assert(ReferenceEquals(cnsOp, src));
                            assert(otherOp is null);
                            assert(src.Oper.IsCnsIntOrI);
                            format = emitInsModeFormat(ins, IF_ARD_CNS);
                        }
                        else
                        {
                            assert(ReferenceEquals(otherOp, src));
                            format = emitInsModeFormat(ins, IF_ARD_RRD);
                        }
                    }
                    assert(format != IF_NONE);
                    emitHandleMemOp(indir, id, format, ins);

                    uint size;
                    if (ReferenceEquals(memOp, src))
                    {
                        assert(ReferenceEquals(otherOp, dst));
                        assert(cnsOp is null);
                        size = instrHasImplicitRegPairDest(ins)
                            ? emitInsSizeAM(id, insCode(ins))
                            : emitInsSizeAM(id, insCodeRM(ins));
                    }
                    else
                    {
                        assert(ReferenceEquals(memOp, dst));
                        assert(!useNdd);
                        if (cnsOp is not null)
                        {
                            assert(ReferenceEquals(cnsOp, src));
                            assert(otherOp is null);
                            size = emitInsSizeAM(id, insCodeMI(ins), unchecked((int)src.AsIntConCommon().IconValue));
                        }
                        else
                        {
                            assert(ReferenceEquals(otherOp, src));
                            size = emitInsSizeAM(id, insCodeMR(ins));
                        }
                    }
                    assert(size != 0);
                    id.idCodeSize(size);
                    dispIns(id);
                    emitCurIGsize += (int)size;

                    return ReferenceEquals(memOp, src) ? (useNdd ? targetReg : dst.RegNum) : REG_NA;
                }
            }
            else
            {
                switch (memOp.Oper)
                {
                    case GT_LCL_FLD:
                    case GT_STORE_LCL_FLD:
                    {
                        varNum = memOp.AsLclFld().LclNum;
                        offset = memOp.AsLclFld().LclOffs;
                        break;
                    }

                    case GT_LCL_VAR:
                    {
                        assert(memOp.IsRegOptional || !_compiler.lvaGetDesc(memOp.AsLclVar().LclNum).lvIsRegCandidate);
                        varNum = memOp.AsLclVar().LclNum;
                        offset = 0;
                        break;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
            }

            // Spill temporary numbers start at -1, which also represents BAD_VAR_NUM.
            assert((varNum != BAD_VAR_NUM) || (temp is not null));
            assert(offset != uint.MaxValue);
            var stackOffset = unchecked((int)offset);

            if (ReferenceEquals(memOp, src))
            {
                assert(ReferenceEquals(otherOp, dst));
                assert(cnsOp is null);
                if (instrHasImplicitRegPairDest(ins))
                {
                    emitIns_S(ins, attr, varNum, stackOffset);
                }
                else if (useNdd)
                {
                    emitIns_R_R_S(ins, attr, targetReg, dst.RegNum, varNum, stackOffset, INS_OPTS_EVEX_nd);
                    return targetReg;
                }
                else
                {
                    emitIns_R_S(ins, attr, dst.RegNum, varNum, stackOffset);
                }
            }
            else
            {
                assert(ReferenceEquals(memOp, dst));
                assert((dst.RegNum == REG_NA) || dst.IsRegOptional);
                assert(!useNdd);
                if (cnsOp is not null)
                {
                    assert(ReferenceEquals(cnsOp, src));
                    assert(otherOp is null);
                    assert(src.Oper.IsCnsIntOrI);
                    emitIns_S_I(ins, attr, varNum, stackOffset, unchecked((int)src.AsIntConCommon().IconValue));
                }
                else
                {
                    assert(ReferenceEquals(otherOp, src));
                    assert(!src.IsContained);
                    emitIns_S_R(ins, attr, src.RegNum, varNum, stackOffset);
                }
            }
        }
        else if (cnsOp is not null)
        {
            assert(ReferenceEquals(cnsOp, src));
            assert(ReferenceEquals(otherOp, dst));
            if (src.Oper.IsCnsIntOrI)
            {
                assert(!dst.IsContained);
                var constant = src.AsIntConCommon();
                if (useNdd)
                {
                    emitIns_R_R_I(ins, attr, targetReg, dst.RegNum, unchecked((int)constant.IconValue), INS_OPTS_EVEX_nd);
                    return targetReg;
                }

                emitIns_R_I(ins, attr, dst.RegNum, constant.IconValue);
            }
            else
            {
                assert(!useNdd);
                assert(src.Oper.IsCnsFltOrDbl);
                var constant = src.AsDblCon();
                var handle = emitFltOrDblConst(constant.DconVal, constant.Type.EmitSize);
                emitIns_R_C(ins, attr, dst.RegNum, handle, 0);
            }
        }
        else
        {
            assert(otherOp is null);
            assert(!src.IsContained && !dst.IsContained);
            if (instrHasImplicitRegPairDest(ins))
            {
                emitIns_R(ins, attr, src.RegNum);
            }
            else if (useNdd)
            {
                emitIns_R_R_R(ins, attr, targetReg, dst.RegNum, src.RegNum, INS_OPTS_EVEX_nd);
                return targetReg;
            }
            else
            {
                emitIns_R_R(ins, attr, dst.RegNum, src.RegNum);
            }
        }

        return dst.RegNum;
#endif
    }
}
