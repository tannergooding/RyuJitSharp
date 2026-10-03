// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if SWIFT_SUPPORT
namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void lvaClassifyParameterAbi(ref SwiftABIClassifier classifier)
    {
        lvaParameterPassingInfo = info.compArgsCount is 0 ? [] : new AbiPassingInformation[info.compArgsCount];

        for (var i = 0; i < info.compArgsCount; i++)
        {
            ref var dsc = ref lvaGetDesc(i);
            var structLayout = varTypeIsStruct(dsc.Type) ? dsc.Layout : null;
            var wellKnownArg = WellKnownArg.None;

            if (i == info.compRetBuffArg)
            {
                wellKnownArg = WellKnownArg.RetBuffer;
            }
            else if (i == lvaSecretStubArg)
            {
                wellKnownArg = WellKnownArg.SecretStubParam;
            }
            else if (i == lvaSwiftSelfArg)
            {
                wellKnownArg = WellKnownArg.SwiftSelf;
            }
            else if (i == lvaSwiftIndirectResultArg)
            {
                wellKnownArg = WellKnownArg.RetBuffer;
            }
            else if (i == lvaSwiftErrorArg)
            {
                wellKnownArg = WellKnownArg.SwiftError;
            }

            var abiInfo = classifier.Classify(this, dsc.Type, structLayout, wellKnownArg);
            lvaParameterPassingInfo[i] = abiInfo;

#if DEBUG
            JITDUMP($"Parameter V{i:D2} ABI info: ");

            if (verbose)
            {
                abiInfo.Dump();
            }
#endif

#if FEATURE_IMPLICIT_BYREFS
            dsc.IsImplicitByRef = abiInfo.IsPassedByReference;
#endif

            var numRegisters = 0;

            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (segment.IsPassedInRegister)
                {
                    numRegisters++;
                }
            }

            dsc.lvIsRegArg = numRegisters > 0;
            dsc.lvIsMultiRegArg = numRegisters > 1;

#if DEBUG
            // Extra query to facilitate wasm replay of native collections.
            // TODO-WASM: delete once we can get a wasm collection.
            if ((JitConfig.EnableExtraSuperPmiQueries != 0) && IsReadyToRun && (structLayout is not null))
            {
                var clsHnd = structLayout.ClassHandle;

                if (clsHnd != NO_CLASS_HANDLE)
                {
                    eeRunExtraSuperPmiQueries(() => info.compCompHnd->getWasmLowering(clsHnd));
                }
            }
#endif
        }

        lvaParameterStackSize = classifier.StackSize;

#if TARGET_ARM
        assert(codeGen is not null);
        ref var armRegSet = ref codeGen.RegSet;

        // The ARM32 profiling enter helper does not preserve argument registers.
        if (compIsProfilerHookNeeded)
        {
            armRegSet.rsMaskPreSpillRegArg |= new regMaskTP(SRBM_ARG_REGS);
        }

        var doubleAlignMask = SRBM_NONE;

        for (var i = 0; i < info.compArgsCount; i++)
        {
            ref readonly var abiInfo = ref lvaGetParameterAbiInfo(i);
            ref var varDsc = ref lvaGetDesc(i);
#if CONFIGURABLE_ARM_ABI
            var useSoftFP = opts.compUseSoftFP;
#else
            var useSoftFP = Options.compUseSoftFP;
#endif
            var preSpill = useSoftFP && varTypeIsFloating(varDsc.Type);
            preSpill |= varDsc.Type is TYP_STRUCT;

            if (!preSpill)
            {
                continue;
            }

            var regs = SRBM_NONE;

            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (segment.IsPassedInRegister && genIsValidIntReg(segment.Register))
                {
                    regs |= segment.RegisterMask;
                }
            }

            armRegSet.rsMaskPreSpillRegArg |= new regMaskTP(regs);

            if (varDsc.lvStructDoubleAlign || (varDsc.Type is TYP_DOUBLE))
            {
                doubleAlignMask |= regs;
            }
        }

        if (doubleAlignMask != SRBM_NONE)
        {
            assert(SRBM_ARG_REGS == (regMask)0xF);
            assert((doubleAlignMask & SRBM_ARG_REGS) == doubleAlignMask);

            if ((doubleAlignMask != SRBM_NONE) && (doubleAlignMask != SRBM_ARG_REGS))
            {
                // Double-aligned arguments start at r0 or r2. A 12-byte struct can
                // occupy r0-r2, so prespilling r2 without r3 must be padded to preserve alignment.
                assert((doubleAlignMask == (regMask)0b0011) || (doubleAlignMask == (regMask)0b1100) ||
                       (doubleAlignMask == (regMask)0b0111));

                var startsAtR0 = (doubleAlignMask & (regMask)1) is not 0;
                var r2XorR3 = ((armRegSet.rsMaskPreSpillRegArg & new regMaskTP(SRBM_R2)) == RBM_NONE) !=
                              ((armRegSet.rsMaskPreSpillRegArg & new regMaskTP(SRBM_R3)) == RBM_NONE);

                if (startsAtR0 && r2XorR3)
                {
                    armRegSet.rsMaskPreSpillAlign =
                        (~armRegSet.rsMaskPreSpillRegArg & ~new regMaskTP(doubleAlignMask)) & new regMaskTP(SRBM_ARG_REGS);
                }
            }
        }
#endif
    }
}
#endif
