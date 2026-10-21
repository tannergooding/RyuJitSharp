// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;
using static RyuJitSharp.Emitter.insFormat;

namespace RyuJitSharp;

public partial class Emitter
{
    public insFormat emitMapFmtAtoM(insFormat fmt)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Memory operand format mapping requires xarch.");
            return default!;
        }

        assert((fmt >= IF_XARCH_ARD) && (fmt <= IF_XARCH_RWR_RRD_ARD_RRD));
        assert((IF_XARCH_RWR_RRD_ARD_RRD - IF_XARCH_ARD) == (IF_XARCH_RWR_RRD_MRD_RRD - IF_XARCH_MRD));
        assert(IF_XARCH_MRD < IF_XARCH_ARD);

        const uint delta = IF_XARCH_ARD - IF_XARCH_MRD;
        assert((IF_XARCH_ARD - delta) == IF_XARCH_MRD);
        assert((IF_XARCH_ARD_CNS - delta) == IF_XARCH_MRD_CNS);
        assert((IF_XARCH_ARD_RRD - delta) == IF_XARCH_MRD_RRD);
        assert((IF_XARCH_RRD_ARD - delta) == IF_XARCH_RRD_MRD);
        assert((IF_XARCH_RRD_ARD_CNS - delta) == IF_XARCH_RRD_MRD_CNS);
        assert((IF_XARCH_RRD_ARD_RRD - delta) == IF_XARCH_RRD_MRD_RRD);
        assert((IF_XARCH_RRD_RRD_ARD - delta) == IF_XARCH_RRD_RRD_MRD);
        assert((IF_XARCH_RWR_RRD_ARD_RRD - delta) == IF_XARCH_RWR_RRD_MRD_RRD);

        return fmt - delta;
    }

    public void emitHandleMemOp(GenTreeIndir indir, instrDesc id, insFormat fmt, instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Memory operand descriptor initialization requires xarch.");
            return;
        }

        assert(fmt != IF_NONE);
        assert(_compiler is not null);
        var memBase = indir.Base;

        if ((memBase is not null) && memBase.Oper.IsCnsIntOrI && memBase.IsContained)
        {
            var constant = memBase.AsIntConCommon();
            assert(constant.FitsInAddrBase(_compiler));

            // Relocatable addresses must be handles, zero, or constants admitted
            // by the target's address-base containment contract.
            assert(!_compiler.opts.compReloc || memBase.IsIconHandle() || memBase.IsIntegralConst(0) ||
                constant.FitsInAddrBase(_compiler));

            if (constant.AddrNeedsReloc(_compiler))
            {
                id.idSetIsDspReloc();
            }

            id.idAddr().iiaAddrMode.amBaseReg = REG_NA;
            id.idAddr().iiaAddrMode.amIndxReg = REG_NA;
            id.idAddr().iiaAddrMode.amScale = (uint)opSize.OPSZ1;
            id.idInsFmt(emitMapFmtForIns(fmt, ins));

            // The constructor must already have allocated and stored the absolute address.
            assert(emitGetInsAmdAny(id) == constant.IconValue);
        }
        else
        {
            var amBaseReg = REG_NA;
            if (memBase is not null)
            {
                assert(!memBase.IsContained);
                amBaseReg = memBase.RegNum;
                assert(amBaseReg != REG_NA);
            }

            var amIndxReg = REG_NA;
            if (indir.HasIndex)
            {
                var index = indir.Index;
                assert(!index.IsContained);
                amIndxReg = index.RegNum;
                assert(amIndxReg != REG_NA);
            }

            assert((amBaseReg != REG_NA) || (amIndxReg != REG_NA) || (indir.Offset != 0));
            id.idAddr().iiaAddrMode.amBaseReg = amBaseReg;
            id.idAddr().iiaAddrMode.amIndxReg = amIndxReg;
            id.idAddr().iiaAddrMode.amScale = (uint)emitEncodeScale(indir.Scale);
            id.idInsFmt(emitMapFmtForIns(fmt, ins));

            // Changing the address registers must not disturb the constructor's displacement.
            assert(emitGetInsAmdAny(id) == indir.Offset);
        }

        if (Configuration.DEBUG && (memBase is not null) && memBase.IsIconHandle() && memBase.IsContained)
        {
            var debugInfo = id.idDebugOnlyInfo();
            assert(debugInfo is not null);
            debugInfo.idFlags = memBase.Flags;
            debugInfo.idMemCookie = memBase.AsIntCon().TargetHandle;
        }
    }

    public insFormat emitMapFmtForIns(insFormat fmt, instruction ins)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Instruction memory format mapping requires xarch.");
            return default!;
        }

        switch (ins)
        {
            case INS_rol_N or INS_ror_N or INS_rcl_N or INS_rcr_N or INS_shl_N or INS_shr_N or INS_sar_N:
            {
                switch (fmt)
                {
                    case IF_XARCH_RRW_CNS:
                    {
                        return IF_XARCH_RRW_SHF;
                    }

                    case IF_XARCH_MRW_CNS:
                    {
                        return IF_XARCH_MRW_SHF;
                    }

                    case IF_XARCH_SRW_CNS:
                    {
                        return IF_XARCH_SRW_SHF;
                    }

                    case IF_XARCH_ARW_CNS:
                    {
                        return IF_XARCH_ARW_SHF;
                    }

                    default:
                    {
                        unreached();
                        break;
                    }
                }
                break;
            }

            default:
            {
                // MOV writes its destination rather than reading and writing it.
                if (IsMovInstruction(ins) && (fmt == IF_XARCH_RRW_ARD))
                {
                    return IF_XARCH_RWR_ARD;
                }
                break;
            }
        }

        return fmt;
    }

    private static opSize emitEncodeScale(nuint scale)
    {
        if (!Configuration.TARGET_XARCH)
        {
            FatalJitException.ThrowFatalJitException(CORJIT_SKIPPED, "Address scaling is only supported on xarch.");
            return default!;
        }

        assert(scale is 1 or 2 or 4 or 8);
        return (opSize)BitOperations.Log2(unchecked((uint)scale));
    }
}
