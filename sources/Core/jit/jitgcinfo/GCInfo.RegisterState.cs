// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct GCInfo
{
    public regMaskTP gcRegGCrefSetCur;
    public regMaskTP gcRegByrefSetCur;
    public VARSET_TP gcTrkStkPtrLcls = [];
    public VARSET_TP gcVarPtrSetCur = [];

    public void gcResetForBB()
    {
        gcRegGCrefSetCur = RBM_NONE;
        gcRegByrefSetCur = RBM_NONE;
        VarSetOps.AssignNoCopy(Compiler, ref gcVarPtrSetCur, VarSetOps.MakeEmpty(Compiler));
    }

    public void gcMarkRegSetGCref(regMaskTP mask
#if DEBUG
        , bool forceOutput = false
#endif
    )
    {
        assert((gcRegByrefSetCur & mask).IsEmpty);
        var byrefSet = gcRegByrefSetCur & ~mask;
        var gcrefSet = gcRegGCrefSetCur | mask;
#if DEBUG
        gcDspGCrefSetChanges(gcrefSet, forceOutput);
        gcDspByrefSetChanges(byrefSet);
#endif
        gcRegByrefSetCur = byrefSet;
        gcRegGCrefSetCur = gcrefSet;
    }

    public void gcMarkRegSetByref(regMaskTP mask
#if DEBUG
        , bool forceOutput = false
#endif
    )
    {
        var byrefSet = gcRegByrefSetCur | mask;
        var gcrefSet = gcRegGCrefSetCur & ~mask;
#if DEBUG
        gcDspGCrefSetChanges(gcrefSet);
        gcDspByrefSetChanges(byrefSet, forceOutput);
#endif
        gcRegByrefSetCur = byrefSet;
        gcRegGCrefSetCur = gcrefSet;
    }

    public void gcMarkRegSetNpt(regMaskTP mask
#if DEBUG
        , bool forceOutput = false
#endif
    )
    {
#if HAS_FIXED_REGISTER_SET
        // Live register variables retain their pointer classification.
        var byrefSet = gcRegByrefSetCur & ~(mask & ~RegSet.GetMaskVars());
        var gcrefSet = gcRegGCrefSetCur & ~(mask & ~RegSet.GetMaskVars());
#if DEBUG
        gcDspGCrefSetChanges(gcrefSet, forceOutput);
        gcDspByrefSetChanges(byrefSet, forceOutput);
#endif
        gcRegByrefSetCur = byrefSet;
        gcRegGCrefSetCur = gcrefSet;
#else
        throw new FatalJitException(CORJIT_SKIPPED, "Non-pointer GC register marking without a fixed register set is not implemented.");
#endif
    }

    public void gcMarkRegPtrVal(regNumber reg, var_types type)
    {
#if EMIT_GENERATE_GCINFO && HAS_FIXED_REGISTER_SET
        var mask = regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);
        switch (type)
        {
            case TYP_REF:
            {
                gcMarkRegSetGCref(mask);
                break;
            }

            case TYP_BYREF:
            {
                gcMarkRegSetByref(mask);
                break;
            }

            default:
            {
                gcMarkRegSetNpt(mask);
                break;
            }
        }
#endif
    }

#if DEBUG
    public readonly void gcDspGCrefSetChanges(regMaskTP newSet, bool forceOutput = false)
    {
        if (Compiler.verbose && (forceOutput || (gcRegGCrefSetCur != newSet)))
        {
            jitprintf("\t\t\t\t\t\t\tGC regs: ");
            if (gcRegGCrefSetCur == newSet)
            {
                jitprintf("(unchanged) ");
            }
            else
            {
                printRegMaskInt(gcRegGCrefSetCur);
                _codeGen.Emitter.emitDispRegSet(gcRegGCrefSetCur);
                jitprintf(" => ");
            }
            printRegMaskInt(newSet);
            _codeGen.Emitter.emitDispRegSet(newSet);
            jitprintf("\n");
        }
    }

    public readonly void gcDspByrefSetChanges(regMaskTP newSet, bool forceOutput = false)
    {
        if (Compiler.verbose && (forceOutput || (gcRegByrefSetCur != newSet)))
        {
            jitprintf("\t\t\t\t\t\t\tByref regs: ");
            if (gcRegByrefSetCur == newSet)
            {
                jitprintf("(unchanged) ");
            }
            else
            {
                printRegMaskInt(gcRegByrefSetCur);
                _codeGen.Emitter.emitDispRegSet(gcRegByrefSetCur);
                jitprintf(" => ");
            }
            printRegMaskInt(newSet);
            _codeGen.Emitter.emitDispRegSet(newSet);
            jitprintf("\n");
        }
    }
#endif
}
