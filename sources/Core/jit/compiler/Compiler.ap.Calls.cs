// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp1Kind;

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe bool optAssertionVNIsSubtype(ValueNum objectVN, ValueNum castToVN, ASSERT_TP? assertions, int budget = 10)
    {
        if ((budget <= 0) || (objectVN == ValueNumStore.NoVN))
        {
            return false;
        }

        assert(vnStore is not null);
        if (!vnStore.IsVNTypeHandle(castToVN, out var castTo))
        {
            return false;
        }

        assert(castTo != NO_CLASS_HANDLE);
        var castFrom = vnStore.GetObjectType(objectVN, out _, out _);
        if ((castFrom != NO_CLASS_HANDLE) && (info.compCompHnd->compareTypesForCast(castFrom, castTo) is TypeCompareState.Must))
        {
            return true;
        }

        if (!BitVecOps.MaybeUninit(assertions))
        {
            assert(apTraits is not null);
            var result = false;
            _ = BitVecOps.VisitBits(apTraits, assertions, bitIndex => {
                var assertion = optGetAssertion(GetAssertionIndex((ushort)bitIndex));
                if (!assertion.KindIs(OAK_EQUAL) || !assertion.Op1.KindIs(O1K_SUBTYPE, O1K_EXACT_TYPE) ||
                    (assertion.Op1.VN != objectVN))
                {
                    return true;
                }

                if (vnStore.IsVNTypeHandle(assertion.Op2.VN, out var source) &&
                    (info.compCompHnd->compareTypesForCast(source, castTo) is TypeCompareState.Must))
                {
                    result = true;
                    return false;
                }

                return true;
            });
            if (result)
            {
                return true;
            }
        }

        return optVisitReachingAssertions(objectVN, (reachingVN, reachingAssertions) =>
            optAssertionVNIsSubtype(reachingVN, castToVN, reachingAssertions, budget - 1) ? AssertVisit.Continue : AssertVisit.Abort)
            is AssertVisit.Continue;
    }

    public GenTree? optAssertionProp_Call(ASSERT_TP? assertions, GenTreeCall call, Statement? statement)
    {
        if (optNonNullAssertionProp_Call(assertions, call) is not null)
        {
            return optAssertionProp_Update(call, call, statement);
        }

        if (!optLocalAssertionProp && call.IsHelperCall())
        {
            var helper = call.HelperNum;
            if (helper is CORINFO_HELP_ISINSTANCEOFINTERFACE or CORINFO_HELP_ISINSTANCEOFARRAY or
                CORINFO_HELP_ISINSTANCEOFCLASS or CORINFO_HELP_ISINSTANCEOFANY or CORINFO_HELP_CHKCASTINTERFACE or
                CORINFO_HELP_CHKCASTARRAY or CORINFO_HELP_CHKCASTCLASS or CORINFO_HELP_CHKCASTANY or CORINFO_HELP_CHKCASTCLASS_SPECIAL)
            {
                var castArgument = call.Args.GetUserArgByIndex(0);
                var objectArgument = call.Args.GetUserArgByIndex(1);
                assert((castArgument is not null) && (objectArgument is not null));
                var castTree = castArgument.Node;
                var objectTree = objectArgument.Node;
                assert((castTree is not null) && (objectTree is not null));
                var objectVN = optConservativeNormalVN(objectTree);
                var castVN = optConservativeNormalVN(castTree);
                if (optAssertionVNIsSubtype(objectVN, castVN, assertions))
                {
#if DEBUG
                    assert(compCurBB is not null);
                    JITDUMP($"\nDid VN based subtype prop in {FMT_BB(compCurBB.bbNum)}:\n");
#endif
                    DISPTREE(call);
                    // Spill an effectful object before extracting argument effects
                    // so returning the object cannot evaluate it a second time.
                    objectTree = fgMakeMultiUse(ref objectArgument.NodeRef);
                    objectTree = gtWrapWithSideEffects(objectTree, call, GTF_SIDE_EFFECT, ignoreRoot: true);
                    return optAssertionProp_Update(objectTree, call, statement);
                }

                if (((call._callMoreFlags & GTF_CALL_M_CAST_CAN_BE_EXPANDED) != 0) &&
                    optAssertionIsNonNull(objectTree, assertions))
                {
                    call._callMoreFlags |= GTF_CALL_M_CAST_OBJ_NONNULL;
                    return optAssertionProp_Update(call, call, statement);
                }
            }
        }

        return null;
    }
}
