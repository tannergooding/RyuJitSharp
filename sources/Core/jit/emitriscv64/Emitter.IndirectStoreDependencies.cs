// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public partial class Emitter
{
    public unsafe void emitInsLoadStoreOp(instruction ins, emitAttr attr, regNumber dataReg, GenTreeIndir indir)
    {
        var compiler = _compiler ??
            throw new FatalJitException(CORJIT_INTERNALERROR, "Indirect load/store recording requires an active compiler.");
        var addr = indir.Addr;

        if (addr.IsContained)
        {
            assert(addr.OperIs(GT_LCL_ADDR) || addr.OperIs(GT_LEA) || addr.OperIs(GT_CNS_INT));
            assert(!addr.OperIs(GT_LEA) ||
                   (!addr.AsAddrMode().HasIndex && (addr.AsAddrMode().Scale <= 1)));

            var offset = indir.Offset;
            var memBase = indir.Base;

            if (addr.Oper is GT_LCL_ADDR)
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
            else if (addr.Oper is GT_CNS_INT)
            {
                assert(memBase == addr);
                assert(offset == addr.AsIntCon().IconValue);
                if (!addr.AsIntCon().AddrNeedsReloc(compiler) && isValidSimm12(offset))
                {
                    emitIns_R_R_I(ins, attr, dataReg, REG_ZERO, offset);
                }
                else
                {
                    var needTemp = (indir.Oper is GT_STOREIND or GT_NULLCHECK) ||
                                   varTypeIsFloating(indir.Type);
                    if (addr.AsIntCon().FitsInAddrBase(compiler) &&
                        addr.AsIntCon().AddrNeedsReloc(compiler))
                    {
                        var addrReg = needTemp
                            ? codeGen.InternalRegisters.GetSingle(indir)
                            : dataReg;
                        attr = EA_SET_FLG(attr, EA_DSP_RELOC_FLG);
                        emitIns_R_AI(ins, attr, dataReg, addrReg, offset);
                    }
                    else
                    {
                        var lo12 = unchecked((offset << (64 - 12)) >> (64 - 12));
                        offset = unchecked(offset - lo12);

                        var addrReg = REG_ZERO;
                        if (offset != 0)
                        {
                            addrReg = needTemp
                                ? codeGen.InternalRegisters.GetSingle(indir)
                                : dataReg;
                            _ = emitLoadImmediate(true, EA_PTRSIZE, addrReg, offset);
                        }

                        emitIns_R_R_I(ins, attr, dataReg, addrReg, lo12);
                    }
                }
            }
            else if (isValidSimm12(offset))
            {
                var baseReg = memBase?.RegNum ??
                    throw new FatalJitException(CORJIT_INTERNALERROR, "Contained RISC-V address mode is missing its base register.");
                emitIns_R_R_I(ins, attr, dataReg, baseReg, offset);
            }
            else
            {
                var lo12 = unchecked((offset << (64 - 12)) >> (64 - 12));
                offset = unchecked(offset - lo12);

                var tmpReg = codeGen.InternalRegisters.GetSingle(indir);
                _ = emitLoadImmediate(true, EA_PTRSIZE, tmpReg, offset);

                var baseTree = memBase ??
                    throw new FatalJitException(CORJIT_INTERNALERROR, "Contained RISC-V address mode is missing its base tree.");
                var addType = varTypeIsGC(baseTree.Type) ? EA_BYREF : EA_PTRSIZE;
                emitIns_R_R_R(INS_add, addType, tmpReg, baseTree.RegNum, tmpReg);
                emitIns_R_R_I(ins, attr, dataReg, tmpReg, lo12);
            }
        }
        else
        {
#if DEBUG
            if (addr.Oper is GT_LCL_ADDR)
            {
                // This path does not update GC local lifetimes, so an uncontained local address must be untracked.
                ref var local = ref compiler.lvaGetDesc(addr.AsLclVarCommon().LclNum);
                assert(!local.lvTracked);
            }
#endif
            emitIns_R_R_I(ins, attr, dataReg, addr.RegNum, 0);
        }
    }
}
#endif
