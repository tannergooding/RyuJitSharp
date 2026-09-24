// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class LinearScan
{
#if DEBUG
    // lsra.h's selection and lifetime stress fields can be combined.
    private bool doReverseCallerCallee() => (_lsraStressMask & 0x08) != 0;

    private bool extendLifetimes() => (_lsraStressMask & 0x80) != 0;

    internal Compiler ReferenceDiagnosticCompiler => _compiler;

    internal weight_t GetReferenceDiagnosticWeight(RefPosition reference) => getWeight(reference);
#else
    private static bool extendLifetimes() => false;
#endif

    private void applyCalleeSaveHeuristics(RefPosition rp)
    {
#if TARGET_AMD64
        if (_compiler.opts.compDbgEnC)
        {
            // EnC only uses RSI and RDI, so do not favor callee-save registers.
            return;
        }
#endif
        var interval = rp.getInterval();
#if DEBUG
        if (!doReverseCallerCallee())
#endif
        {
            interval.mergeRegisterPreferences(rp.registerAssignment);
        }
    }

    private static void checkConflictingDefUse(RefPosition useRP)
    {
        assert(useRP.refType is RefType.RefTypeUse);
        var interval = useRP.getInterval();
        assert(!interval.isLocalVar);

        var defRP = interval.firstRefPosition;
        assert(defRP is not null);
        assert(defRP.treeNode is not null);
        var previousAssignment = defRP.registerAssignment;
        var newAssignment = previousAssignment & useRP.registerAssignment;
        if (newAssignment != SRBM_NONE)
        {
            // Propagating a fixed register across interfering delayed uses would
            // require a physical-register reference at the definition as well.
            if (!isSingleRegister(newAssignment) || !interval.hasInterferingUses)
            {
                defRP.registerAssignment = newAssignment;
            }
        }
        else
        {
            interval.hasConflictingDefUse = true;
        }
    }

    private void associateRefPosWithInterval(RefPosition rp)
    {
        var referent = rp.referent;
        if (referent is not null)
        {
            if (rp.isIntervalRef())
            {
                var interval = rp.getInterval();
                applyCalleeSaveHeuristics(rp);

                if (interval.isLocalVar)
                {
                    if (RefTypeIsUse(rp.refType))
                    {
                        var previous = interval.recentRefPosition;
                        if ((previous is not null) && (previous.bbNum == rp.bbNum))
                        {
                            previous.lastUse = false;
                        }
                    }

                    rp.lastUse = (rp.refType is not RefType.RefTypeExpUse and
                        not RefType.RefTypeParamDef and not RefType.RefTypeZeroInit) && !extendLifetimes();
                }
                else if (rp.refType is RefType.RefTypeUse)
                {
                    checkConflictingDefUse(rp);
                    rp.lastUse = true;
                }
            }

            var previousRef = referent.recentRefPosition;
            if (previousRef is not null)
            {
                previousRef.nextRefPosition = rp;
            }
            else
            {
                referent.firstRefPosition = rp;
            }

            referent.recentRefPosition = rp;
            referent.lastRefPosition = rp;
        }
        else
        {
            assert(rp.refType is RefType.RefTypeBB or RefType.RefTypeKillGCRefs or RefType.RefTypeKill);
        }
    }

    internal RefPosition newRefPosition(regNumber reg, LsraLocation location, RefType refType,
        GenTree? treeNode, SingleTypeRegSet mask)
    {
        var newRP = newRefPositionRaw(location, treeNode, refType);
        var regRecord = getRegisterRecord(reg);
        newRP.setReg(regRecord);
        newRP.registerAssignment = mask;
        newRP.setMultiRegIdx(0);
        newRP.setRegOptional(false);

        assert((regRecord.lastRefPosition is null) || (regRecord.lastRefPosition.nodeLocation < location) ||
            (regRecord.lastRefPosition.refType != refType));
        associateRefPosWithInterval(newRP);

#if DEBUG
        if (VERBOSE)
        {
            newRP.dump(this);
        }
#endif
        return newRP;
    }

    internal RefPosition newRefPosition(Interval? interval, LsraLocation location, RefType refType,
        GenTree? treeNode, SingleTypeRegSet mask, uint multiRegIdx = 0)
    {
        if (interval is not null)
        {
            if (mask == SRBM_NONE)
            {
                mask = allRegs(interval.registerType);
            }
        }
        else
        {
            assert(refType is RefType.RefTypeBB or RefType.RefTypeKillGCRefs or RefType.RefTypeKill);
        }

#if DEBUG
        if ((interval is not null) && (regType(interval.registerType) is TYP_FLOAT))
        {
            assert(_compiler.compFloatingPointUsed || ((mask & SRBM_FLT_CALLEE_SAVED) == SRBM_NONE));
        }
#endif

        var isFixedRegister = isSingleRegister(mask);
        var insertFixedRef = false;
        if (isFixedRegister)
        {
            if (refType is RefType.RefTypeDef or RefType.RefTypeUse)
            {
                assert(interval is not null);
                insertFixedRef = (refType is RefType.RefTypeDef) || !interval.isInternal;
            }
        }

        if (insertFixedRef)
        {
            assert(interval is not null);
            var physicalReg = genRegNumFromMask(mask, interval.registerType);
            _ = newRefPosition(physicalReg, location, RefType.RefTypeFixedReg, null, mask);
            assert((allRegs(interval.registerType) & mask) != SRBM_NONE);
        }

        var newRP = newRefPositionRaw(location, treeNode, refType);
        newRP.setInterval(interval);
        newRP.isFixedRegRef = isFixedRegister;
        newRP.registerAssignment = mask;
        newRP.setMultiRegIdx(multiRegIdx);
        newRP.setRegOptional(false);

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
        newRP.skipSaveRestore = false;
        newRP.liveVarUpperSave = false;
#endif
        associateRefPosWithInterval(newRP);

        if (RefTypeIsDef(newRP.refType))
        {
            assert(interval is not null);
            interval.isSingleDef = ReferenceEquals(interval.firstRefPosition, newRP);
        }

#if DEBUG
        // addKillForRegs sets this in Release; the build-time dump needs it now.
        if (refType is RefType.RefTypeKill)
        {
            newRP.killedRegisters = new regMaskTP(mask);
        }

        if (VERBOSE)
        {
            newRP.dump(this);
        }
#endif
        return newRP;
    }
}
