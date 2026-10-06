// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_ARM || TARGET_ARM64
using static RyuJitSharp.Globals;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genCodeForIndir(GenTreeIndir tree)
    {
        assert(tree.Oper is GT_IND);

#if FEATURE_SIMD
        if (tree.Type is TYP_SIMD12)
        {
            genLoadIndTypeSimd12(tree);
            return;
        }
#endif

        var type = tree.Type;
        var ins = ins_Load(type);
        var targetReg = tree.RegNum;
        var attr = type.EmitActualSize;

        genConsumeAddress(tree.Addr);

        var emitBarrier = false;
        if (tree.IsVolatile)
        {
            ins = genGetVolatileLdStIns(ins, targetReg, tree, ref emitBarrier);
        }

#if TARGET_ARM64
        if (varTypeUsesMaskReg(type))
        {
            attr = EA_SCALABLE;
        }
#endif

        Emitter.emitInsLoadStoreOp(ins, attr, targetReg, tree);

        if (emitBarrier)
        {
            instGen_MemoryBarrier(BARRIER_LOAD_ONLY);
        }

        genProduceReg(tree);
    }

    private instruction genGetVolatileLdStIns(
        instruction currentIns, regNumber targetReg, GenTreeIndir indir, ref bool needsBarrier)
    {
        assert(indir.IsVolatile);

        if (!genIsValidIntReg(targetReg))
        {
            needsBarrier = true;
            return currentIns;
        }

        assert(!varTypeIsFloating(indir.Type));
        assert(!varTypeIsSimd(indir.Type));

#if TARGET_ARM64
        needsBarrier = false;

        if (indir.IsUnaligned && (currentIns != INS_ldrb) && (currentIns != INS_strb))
        {
            needsBarrier = true;
            return currentIns;
        }

        var addr = indir.Addr;
        var addrIsInReg = addr.IsUsedFromReg;
        var shouldUseRcpc2 = !addrIsInReg && (addr.Oper is GT_LEA) && !indir.HasIndex &&
            (indir.Scale == 1) && Emitter.emitIns_valid_imm_for_unscaled_ldst_offset(indir.Offset) &&
            _compiler.compOpportunisticallyDependsOn(InstructionSet_Rcpc2);

        if (shouldUseRcpc2)
        {
            assert(!addrIsInReg);
            switch (currentIns)
            {
                case INS_ldrb:
                {
                    return INS_ldapurb;
                }

                case INS_ldrh:
                {
                    return INS_ldapurh;
                }

                case INS_ldr:
                {
                    return INS_ldapur;
                }

                case INS_strb:
                {
                    return INS_stlurb;
                }

                case INS_strh:
                {
                    return INS_stlurh;
                }

                case INS_str:
                {
                    return INS_stlur;
                }

                default:
                {
                    needsBarrier = true;
                    return currentIns;
                }
            }
        }

        if (!addrIsInReg)
        {
            needsBarrier = true;
            return currentIns;
        }

        var hasRcpc1 = _compiler.compOpportunisticallyDependsOn(InstructionSet_Rcpc);
        switch (currentIns)
        {
            case INS_ldrb:
            {
                return hasRcpc1 ? INS_ldaprb : INS_ldarb;
            }

            case INS_ldrh:
            {
                return hasRcpc1 ? INS_ldaprh : INS_ldarh;
            }

            case INS_ldr:
            {
                return hasRcpc1 ? INS_ldapr : INS_ldar;
            }

            case INS_strb:
            {
                return INS_stlrb;
            }

            case INS_strh:
            {
                return INS_stlrh;
            }

            case INS_str:
            {
                return INS_stlr;
            }

            default:
            {
                needsBarrier = true;
                return currentIns;
            }
        }
#else
        needsBarrier = true;
        return currentIns;
#endif
    }
}
#endif
