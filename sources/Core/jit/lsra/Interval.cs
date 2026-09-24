// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class Interval : Referenceable
{
    public Interval(RegisterType registerType, SingleTypeRegSet registerPreferences)
        : base(registerType)
    {
        this.registerPreferences = registerPreferences;
        registerAversion = SRBM_NONE;
        physReg = REG_COUNT;

#if DEBUG
        intervalIndex = 0;
#endif
    }

    public SingleTypeRegSet registerPreferences;
    public SingleTypeRegSet registerAversion;
    public Interval? relatedInterval;
    public RegRecord? assignedReg;
    public uint varNum;
    public regNumber physReg;
    public bool isActive;
    public bool isLocalVar;
    public bool isSplit;
    public bool isSpilled;
    public bool isInternal;
    public bool isStructField;
    public bool isPromotedStruct;
    public bool hasConflictingDefUse;
    public bool hasInterferingUses;
    public bool isSpecialPutArg;
    public bool preferCalleeSave;
    public bool isConstant;

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    public bool isUpperVector;
    public bool isPartiallySpilled;

    public bool IsUpperVector() => isUpperVector;
#else
    public bool IsUpperVector() => false;
#endif

    public bool isWriteThru;
    public bool isSingleDef;

#if DEBUG
    public uint intervalIndex;
#endif

    public ref LclVarDsc getLocalVar(Compiler compiler)
    {
        assert(isLocalVar);
        return ref compiler.lvaGetDesc(checked((int)varNum));
    }

    public uint getVarIndex(Compiler compiler)
    {
        ref var varDsc = ref getLocalVar(compiler);
        assert(varDsc.lvTracked);
        return varDsc._varIndex;
    }

    public void setLocalNumber(Compiler compiler, uint lclNum, LinearScan linearScan)
    {
        ref var varDsc = ref compiler.lvaGetDesc(checked((int)lclNum));
        assert(varDsc.lvTracked);
        assert(varDsc._varIndex < compiler.lvaTrackedCount);

        var localVarIntervals = linearScan.localVarIntervals;
        assert(localVarIntervals is not null);

        localVarIntervals[varDsc._varIndex] = this;
        assert(ReferenceEquals(localVarIntervals[varDsc._varIndex], this));

        isLocalVar = true;
        varNum = lclNum;
    }

    public void assignRelatedInterval(Interval newRelatedInterval)
    {
#if DEBUG
        if (VERBOSE)
        {
            jitprintf("Assigning related ");
            newRelatedInterval.microDump();
            jitprintf(" to ");
            microDump();
            jitprintf("\n");
        }
#endif
        relatedInterval = newRelatedInterval;
    }

    public bool assignRelatedIntervalIfUnassigned(Interval newRelatedInterval)
    {
        if (relatedInterval is not null)
        {
#if DEBUG
            if (VERBOSE)
            {
                jitprintf("Interval ");
                microDump();
                jitprintf(" already has a related interval\n");
            }
#endif
            return false;
        }

        assignRelatedInterval(newRelatedInterval);
        return true;
    }

    public SingleTypeRegSet getCurrentPreferences()
    {
        if (assignedReg is null)
        {
            return registerPreferences;
        }

        return genSingleTypeRegMask(assignedReg.regNum);
    }

    public void mergeRegisterPreferences(SingleTypeRegSet preferences)
    {
        assert(registerPreferences != SRBM_NONE);
        assert(preferences != SRBM_NONE);

        preferences = genAndNot(preferences, registerAversion);
        if (preferences == SRBM_NONE)
        {
            return;
        }

        var commonPreferences = registerPreferences & preferences;
        if (commonPreferences != SRBM_NONE)
        {
            registerPreferences = commonPreferences;
            return;
        }

        if (!genMaxOneBit(preferences))
        {
            // Disjoint multi-register sets generally represent kills; do not union them.
            registerPreferences = preferences;
            return;
        }

        if (!genMaxOneBit(registerPreferences))
        {
            return;
        }

        var newPreferences = registerPreferences | preferences;
        if (preferCalleeSave)
        {
            var calleeSaveMask = LinearScan.calleeSaveRegs(registerType) & newPreferences;
            if (calleeSaveMask != SRBM_NONE)
            {
                newPreferences = calleeSaveMask;
            }
        }

        registerPreferences = newPreferences;
    }

    public void updateRegisterPreferences(SingleTypeRegSet preferences)
    {
        if ((relatedInterval is not null) && !relatedInterval.isActive)
        {
            mergeRegisterPreferences(relatedInterval.getCurrentPreferences());
        }

        mergeRegisterPreferences(preferences);
    }

#if DEBUG
    public void tinyDump()
    {
        jitprintf($"<Ivl:{intervalIndex}");
        if (isLocalVar)
        {
            jitprintf($" V{varNum:D2}");
        }
        else if (IsUpperVector())
        {
            assert(relatedInterval is not null);
            jitprintf($" (U{relatedInterval.varNum:D2})");
        }
        else if (isInternal)
        {
            jitprintf(" internal");
        }
        jitprintf("> ");
    }

    public void microDump()
    {
        if (isLocalVar)
        {
            jitprintf($"<V{varNum:D2}/L{intervalIndex}>");
            return;
        }
        else if (IsUpperVector())
        {
            assert(relatedInterval is not null);
            jitprintf($" (U{relatedInterval.varNum:D2})");
        }

        var intervalTypeChar = isInternal ? 'T' : 'I';
        jitprintf($"<{intervalTypeChar}{intervalIndex}>");
    }
#endif
}
