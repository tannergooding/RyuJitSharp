// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    public void genSpillVar(GenTree tree)
    {
#if !TARGET_AMD64
        throw new FatalJitException(CORJIT_SKIPPED, "Local-variable spill generation outside AMD64 is not implemented.");
#else
        var varNum = tree.AsLclVarCommon().LclNum;
        ref var varDsc = ref _compiler.lvaGetDesc(varNum);
        assert(varDsc.lvIsRegCandidate);

        var needsSpill = ((tree.Flags & GTF_VAR_DEF) == 0) && varDsc.lvIsInReg;

        if (needsSpill)
        {
            // Enregistered locals are not aliasable, so normalize to their stack home type when storing.
            var lclType = varDsc.GetStackSlotHomeType();
            var size = lclType.EmitSize;

            if (!varDsc.IsAlwaysAliveInMemory)
            {
                assert(varDsc.RegNum == tree.RegNum);
#if FEATURE_SIMD
                if (lclType == TYP_SIMD12)
                {
                    Emitter.emitStoreSimd12ToLclOffset(unchecked((uint)varNum), tree.AsLclVarCommon().LclOffs,
                        tree.RegNum, null);
                }
                else
#endif
                {
                    var storeIns = ins_Store(lclType, _compiler.isSIMDTypeLocalAligned(varNum));
                    inst_TT_RV(storeIns, size, tree, tree.RegNum);
                }
            }

            assert((tree.Flags & GTF_SPILLED) == 0);
            genUpdateRegLife(in varDsc, isBorn: false, isDying: true
#if DEBUG
                , tree
#endif
                );
            _gcInfo.gcMarkRegSetNpt(genGetRegMask(in varDsc));

            if (VarSetOps.IsMember(_compiler, _gcInfo.gcTrkStkPtrLcls, varDsc._varIndex))
            {
#if DEBUG
                if (_compiler.verbose)
                {
                    var state = VarSetOps.IsMember(_compiler, _gcInfo.gcVarPtrSetCur, varDsc._varIndex)
                        ? "continuing" : "becoming";
                    jitprintf($"\t\t\t\t\t\t\tVar V{varNum:D2} {state} live\n");
                }
#endif
                VarSetOps.AddElemD(_compiler, _gcInfo.gcVarPtrSetCur, varDsc._varIndex);
            }
        }

        tree.Flags &= ~GTF_SPILL;

        if ((tree.Flags & GTF_SPILLED) == 0)
        {
            varDsc.RegNum = REG_STK;

            if (varTypeIsMultiReg(tree.Type))
            {
                varDsc.OtherReg = REG_STK;
            }
        }
        else
        {
            assert(varDsc.IsAlwaysAliveInMemory && ((tree.Flags & GTF_VAR_DEF) != 0));
        }

        if (needsSpill)
        {
            // The live-range location must observe the new stack home, not the old register.
            getVariableLiveKeeper().siUpdateVariableLiveRange(in varDsc, varNum);
        }
#endif
    }
}
