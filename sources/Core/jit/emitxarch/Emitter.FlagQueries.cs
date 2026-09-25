// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Emitter
{
    public bool IsRedundantCmp(emitAttr size, regNumber reg1, regNumber reg2)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Redundant comparison queries outside AMD64 are not implemented.");
#else
        if (!genIsValidIntReg(reg1) || !genIsValidIntReg(reg2))
        {
            return false;
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        var result = false;
        emitPeepholeIterateLastInstrs(id =>
        {
            var ins = id.idIns();
            if (ins == INS_cmp)
            {
                if (id.idInsFmt() != insFormat.IF_RRD_RRD)
                {
                    return emitPeepholeResult.PEEPHOLE_ABORT;
                }

                if ((id.idReg1() == reg1) && (id.idReg2() == reg2))
                {
                    result = size == id.idOpSize();
                }

                return emitPeepholeResult.PEEPHOLE_ABORT;
            }

            if (emitDoesInsModifyFlags(ins))
            {
                return emitPeepholeResult.PEEPHOLE_ABORT;
            }

            if (emitIsInstrWritingToReg(id, reg1) || emitIsInstrWritingToReg(id, reg2))
            {
                return emitPeepholeResult.PEEPHOLE_ABORT;
            }

            return emitPeepholeResult.PEEPHOLE_CONTINUE;
        });

        return result;
#endif
    }

    public bool AreFlagsSetToZeroCmp(regNumber reg, emitAttr opSize, GenCondition cond)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Zero-comparison flag queries outside AMD64 are not implemented.");
#else
        assert(reg != REG_NA);
        assert(_compiler is not null);
        if (!_compiler.opts.OptimizationEnabled)
        {
            return false;
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        var id = emitLastIns;
        assert(id is not null);
        var lastIns = id.idIns();
        if (!id.idIsReg1Write() || (id.idReg1() != reg))
        {
            return false;
        }

        if (id.idHasMemWrite() || id.idIsReg2Write())
        {
            return false;
        }

        assert(!id.idIsReg3Write());
        assert(!id.idIsReg4Write());

        // Logical operations reset OF/CF and write SF/ZF/PF like a test against zero.
        if (DoesResetOverflowAndCarryFlags(lastIns) && DoesWriteSignFlag(lastIns) &&
            DoesWriteZeroFlagForResult(lastIns) && DoesWriteParityFlag(lastIns))
        {
            return id.idOpSize() == opSize;
        }

        if (cond.Code is GenCondition.CodeKind.NE or GenCondition.CodeKind.EQ)
        {
            if (DoesWriteZeroFlagForResult(lastIns) && IsFlagsAlwaysModified(id))
            {
                return id.idOpSize() == opSize;
            }
        }

        return false;
#endif
    }

    public bool AreFlagsSetForSignJumpOpt(regNumber reg, emitAttr opSize, GenCondition cond)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Sign-jump flag queries outside AMD64 are not implemented.");
#else
        assert(reg != REG_NA);
        assert(_compiler is not null);
        if (!_compiler.opts.OptimizationEnabled)
        {
            return false;
        }

        if (!emitCanPeepholeLastIns())
        {
            return false;
        }

        var id = emitLastIns;
        assert(id is not null);
        var lastIns = id.idIns();
        if (!id.idIsReg1Write() || (id.idReg1() != reg))
        {
            return false;
        }

        if (id.idHasMemWrite() || id.idIsReg2Write())
        {
            return false;
        }

        // Signed GE/LT can use the result's sign alone instead of comparing it to zero.
        if (cond.Code is GenCondition.CodeKind.SGE or GenCondition.CodeKind.SLT)
        {
            if (DoesWriteSignFlag(lastIns) && IsFlagsAlwaysModified(id))
            {
                return id.idOpSize() == opSize;
            }
        }

        return false;
#endif
    }

#if TARGET_AMD64
    public static bool DoesWriteParityFlag(instruction ins)
    {
        var flags = CodeGen.instInfo[(int)ins];

        return (flags & Writes_PF) != 0;
    }

    public static bool DoesWriteSignFlag(instruction ins)
    {
        var flags = CodeGen.instInfo[(int)ins];

        return (flags & Writes_SF) != 0;
    }

    public static bool DoesResetOverflowAndCarryFlags(instruction ins)
    {
        var flags = CodeGen.instInfo[(int)ins];

        return (flags & (Resets_OF | Resets_CF)) == (Resets_OF | Resets_CF);
    }

    public static bool IsFlagsAlwaysModified(instrDesc id)
    {
        var ins = id.idIns();
        var fmt = id.idInsFmt();
        if (fmt == insFormat.IF_RRW_SHF)
        {
            if (id.idIsLargeCns())
            {
                return true;
            }
            else if (id.idSmallCns() == 0)
            {
                // Immediate zero counts leave flags unchanged.
                return ins is not (INS_rcl_N or INS_rcr_N or INS_rol_N or INS_ror_N or
                    INS_shl_N or INS_shr_N or INS_sar_N);
            }
        }
        else if (fmt == insFormat.IF_RRW)
        {
            // A register count may be zero, so flags cannot be reused conservatively.
            return ins is not (INS_rcl or INS_rcr or INS_rol or INS_ror or
                INS_shl or INS_shr or INS_sar);
        }

        return true;
    }

    public static bool emitDoesInsModifyFlags(instruction ins)
    {
        return (CodeGen.instInfo[(int)ins] &
            (Resets_OF | Resets_SF | Resets_AF | Resets_PF | Resets_CF |
             Undefined_OF | Undefined_SF | Undefined_AF | Undefined_PF | Undefined_CF | Undefined_ZF |
             Writes_OF | Writes_SF | Writes_AF | Writes_PF | Writes_CF | Writes_ZF |
             Restore_SF_ZF_AF_PF_CF)) != 0;
    }
#endif
}
