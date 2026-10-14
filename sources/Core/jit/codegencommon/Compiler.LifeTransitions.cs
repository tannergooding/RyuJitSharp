// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public void compUpdateLife(nint[] newLife, bool forCodeGen)
    {
        if (!VarSetOps.Equal(this, compCurLife, newLife))
        {
            compChangeLife(newLife, forCodeGen);
        }
        else if (Configuration.DEBUG && verbose)
        {
            jitprintf($"Liveness not changing: {VarSetOps.ToString(this, compCurLife)} ");
            dumpConvertedVarSet(this, compCurLife);
            jitprintf("\n");
        }
    }

    private void compChangeLife(nint[] newLife, bool forCodeGen)
    {
        if (Configuration.DEBUG && verbose)
        {
            jitprintf($"Change life {VarSetOps.ToString(this, compCurLife)} ");
            dumpConvertedVarSet(this, compCurLife);
            jitprintf($" -> {VarSetOps.ToString(this, newLife)} ");
            dumpConvertedVarSet(this, newLife);
            jitprintf("\n");
        }
        noway_assert(!VarSetOps.Equal(this, compCurLife, newLife));

        if (!forCodeGen)
        {
            VarSetOps.Assign(this, ref compCurLife, newLife);
            return;
        }

        var deadSet = VarSetOps.Diff(this, compCurLife, newLife);
        var bornSet = VarSetOps.Diff(this, newLife, compCurLife);
        noway_assert(!VarSetOps.IsEmptyUnion(this, deadSet, bornSet));
        noway_assert(VarSetOps.IsEmptyIntersection(this, deadSet, bornSet));
        VarSetOps.Assign(this, ref compCurLife, newLife);

        assert(codeGen is not null);
        assert(lvaTrackedToVarNum is not null);

        // A dying local can share its register with a newly live local.
        _ = VarSetOps.VisitBits(this, deadSet, deadVarIndex =>
        {
            var varNum = lvaTrackedIndexToLclNum(deadVarIndex);
            ref var varDsc = ref lvaGetDesc(varNum);
#if EMIT_GENERATE_GCINFO
            var isGCRef = varDsc.Type == TYP_REF;
            var isByRef = varDsc.Type == TYP_BYREF;
            var isInReg = varDsc.lvIsInReg;
            var isInMemory = !isInReg || varDsc.IsAlwaysAliveInMemory;
            if (Configuration.HAS_FIXED_REGISTER_SET && isInReg)
            {
                var regMask = codeGen.genGetRegMask(in varDsc);
                if (isGCRef)
                {
                    codeGen.GCInfo.gcRegGCrefSetCur &= ~regMask;
                }
                else if (isByRef)
                {
                    codeGen.GCInfo.gcRegByrefSetCur &= ~regMask;
                }

                codeGen.genUpdateRegLife(in varDsc, isBorn: false, isDying: true
#if DEBUG
                    , null
#endif
                    );
            }
            if (isInMemory && (isGCRef || isByRef))
            {
                VarSetOps.RemoveElemD(this, codeGen.GCInfo.gcVarPtrSetCur, deadVarIndex);
                if (Configuration.DEBUG && verbose)
                {
                    jitprintf($"\t\t\t\t\t\t\tV{varNum:D2} becoming dead\n");
                }
            }
#endif
            codeGen.getVariableLiveKeeper().siEndVariableLiveRange(varNum);

            return true;
        });

        _ = VarSetOps.VisitBits(this, bornSet, bornVarIndex =>
        {
            var varNum = lvaTrackedIndexToLclNum(bornVarIndex);
            ref var varDsc = ref lvaGetDesc(varNum);
#if EMIT_GENERATE_GCINFO
            var isGCRef = varDsc.Type == TYP_REF;
            var isByRef = varDsc.Type == TYP_BYREF;

            if (varDsc.lvIsInReg)
            {
                if (Configuration.HAS_FIXED_REGISTER_SET)
                {
                    // EH and spill-at-single-definition locals retain their stack home while enregistered.
                    if (!varDsc.IsAlwaysAliveInMemory)
                    {
                        if (Configuration.DEBUG && verbose &&
                            VarSetOps.IsMember(this, codeGen.GCInfo.gcVarPtrSetCur, bornVarIndex))
                        {
                            jitprintf($"\t\t\t\t\t\t\tRemoving V{varNum:D2} from gcVarPtrSetCur\n");
                        }
                        VarSetOps.RemoveElemD(this, codeGen.GCInfo.gcVarPtrSetCur, bornVarIndex);
                    }

                    codeGen.genUpdateRegLife(in varDsc, isBorn: true, isDying: false
#if DEBUG
                        , null
#endif
                        );
                    var regMask = codeGen.genGetRegMask(in varDsc);
                    if (isGCRef)
                    {
                        codeGen.GCInfo.gcRegGCrefSetCur |= regMask;
                    }
                    else if (isByRef)
                    {
                        codeGen.GCInfo.gcRegByrefSetCur |= regMask;
                    }
                }
            }
            else if (lvaIsGCTracked(in varDsc))
            {
                VarSetOps.AddElemD(this, codeGen.GCInfo.gcVarPtrSetCur, bornVarIndex);
                if (Configuration.DEBUG && verbose)
                {
                    jitprintf($"\t\t\t\t\t\t\tV{varNum:D2} becoming live\n");
                }
            }
#endif
            codeGen.getVariableLiveKeeper().siStartVariableLiveRange(in varDsc, varNum);

            return true;
        });
    }
}
