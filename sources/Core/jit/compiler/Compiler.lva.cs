// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Current local ref count state</summary>
    public RefCountState lvaRefCountState;

    /// <summary>true if we cannot add new tracked variables; otherwise, false</summary>
    public bool lvaTrackedFixed;

    /// <summary>total number of locals, which includes function arguments, special arguments, IL local variables, and JIT temporary variables</summary>
    public int lvaCount;

    /// <summary>variable descriptor table</summary>
    public LclVarDsc[] lvaTable = [];

    public AbiPassingInformation[] lvaParameterPassingInfo = [];

    public int lvaParameterStackSize;

    /// <summary>actual # of locals being tracked</summary>
    public int lvaTrackedCount;

    /// <summary>min # of size_t's sufficient to hold a bit for all the locals being tracked</summary>
    public int lvaTrackedCountInSizeTUnits;

#if DEBUG
    /// <summary>set of tracked variables</summary>
    public VARSET_TP lvaTrackedVars = [];
#endif

#if TARGET_32BIT
    /// <summary>set of long (64-bit) variables</summary>
    public VARSET_TP lvaLongVars = [];
#endif

    /// <summary>set of floating-point (32-bit and 64-bit) or simd variables</summary>
    public VARSET_TP lvaFloatVars = [];

#if FEATURE_MASKED_HW_INTRINSICS
    /// <summary>set of mask variables</summary>
    public VARSET_TP lvaMaskVars = [];
#endif

    /// <summary>VarSets are relative to a specific set of tracked var indices. If that changes, this changes.</summary>
    /// <remarks>VarSets from different epochs cannot be meaningfully combined.</remarks>
    public int lvaCurEpoch;

    /// <summary>reverse map of tracked number to var number</summary>
    public int[]? lvaTrackedToVarNum;

#if DEBUG && DOUBLE_ALIGN
    /// <summary># of procs compiled a with double-aligned stack</summary>
    public static int s_lvaDoubleAlignedProcsCount;
#endif

    public bool lvaEnregEHVars;

    public bool lvaEnregMultiRegVars;

    public int lvaVarargsHandleArg = BAD_VAR_NUM;

#if TARGET_X86
    /// <summary>Pointer (computed based on incoming varargs handle) to the start of the stack arguments</summary>
    public int lvaVarargsBaseOfStkArgs = BAD_VAR_NUM;
#endif

#if TARGET_WASM
    /// <summary>lcl var index of Wasm stack pointer arg</summary>
    public int lvaWasmSpArg = BAD_VAR_NUM;

    /// <summary>Wasm virtual IP slot</summary>
    public int lvaWasmVirtualIP = BAD_VAR_NUM;

    /// <summary>Wasm function index slot</summary>
    public int lvaWasmFunctionIndex = BAD_VAR_NUM;

    /// <summary>Wasm catch resumption IP slot</summary>
    public int lvaWasmResumeIP = BAD_VAR_NUM;
#endif

    /// <summary>variable representing the InlinedCallFrame</summary>
    public int lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;

    /// <summary>variable representing the reverse PInvoke frame</summary>
    public int lvaReversePInvokeFrameVar = BAD_VAR_NUM;

    /// <summary>boolean variable introduced into in synchronized methods that tracks whether the lock has been taken</summary>
    public int lvaMonAcquired = BAD_VAR_NUM;

    /// <summary>Thread local for async methods</summary>
    public int lvaAsyncThreadObjectVar = BAD_VAR_NUM;

    /// <summary>ExecutionContext local for async methods</summary>
    public int lvaAsyncExecutionContextVar = BAD_VAR_NUM;

    /// <summary>SynchronizationContext local for async methods</summary>
    public int lvaAsyncSynchronizationContextVar = BAD_VAR_NUM;
    /// <summary>The lclNum of arg0. Normally this will be info.compThisArg.</summary>
    /// <remarks>However, if there is a "ldarga 0" or "starg 0" in the IL, we will redirect all "ldarg(a) 0" and "starg 0" to this temp.</remarks>
    public int lvaArg0Var = BAD_VAR_NUM;

    /// <summary>The temp to spill the non-VOID return expression in case there are multiple BBJ_RETURN blocks in the inlinee or if the inlinee has GC ref locals.</summary>
    public int lvaInlineeReturnSpillTemp = BAD_VAR_NUM;

    /// <summary>True if the temp was freshly created for the inlinee return</summary>
    public bool lvaInlineeReturnSpillTempFreshlyCreated;

    /// <summary>Local number of argument passed as WellKnownArg.InstParam to next call</summary>
    public int lvaNextCallGenericContext = BAD_VAR_NUM;

    /// <summary>Local number of argument passed as WellKnownArg.AsyncContinuation to next call</summary>
    public int lvaNextCallAsyncContinuation = BAD_VAR_NUM;

#if FEATURE_FIXED_OUT_ARGS
    /// <summary>Var that represents outgoing argument space</summary>
    public int lvaOutgoingArgSpaceVar = BAD_VAR_NUM;

    /// <summary>Size of fixed outgoing argument space</summary>
    public PhasedVar<int> lvaOutgoingArgSpaceSize;
#endif

    /// <summary>Variable representing the return address.</summary>
    /// <remarks>The helper-based tailcall mechanism passes the address of the return address to a runtime helper where it is used to detect tail-call chains.</remarks>
    public int lvaRetAddrVar = BAD_VAR_NUM;

#if SWIFT_SUPPORT
    public int lvaSwiftSelfArg = BAD_VAR_NUM;

    public int lvaSwiftIndirectResultArg = BAD_VAR_NUM;

    public int lvaSwiftErrorArg = BAD_VAR_NUM;

    public int lvaSwiftErrorLocal;
#endif

    /// <summary>Variable representing async continuation argument passed.</summary>
    public int lvaAsyncContinuationArg = BAD_VAR_NUM;

#if DEBUG && TARGET_XARCH
    /// <summary>Stores SP to confirm it is not corrupted on return.</summary>
    public int lvaReturnSpCheck = BAD_VAR_NUM;
#endif

#if DEBUG && TARGET_X86
    /// <summary>Stores SP to confirm it is not corrupted after every call.</summary>
    public int lvaCallSpCheck = BAD_VAR_NUM;
#endif

    public bool lvaGenericsContextInUse;

    public int lvaCachedGenericContextArgOffs;

#if JIT32_GCENCODER
    /// <summary>variable which stores the value of ESP after the last alloca/localloc</summary>
    public int lvaLocAllocSPvar = BAD_VAR_NUM;
#endif

    /// <summary>Variable with arguments for new MD array helper</summary>
    public int lvaNewObjArrayArgs = BAD_VAR_NUM;

#if DEBUG
    // TODO: Port Compiler.lvaStressLclFldCB
    // public static unsafe fgWalkPreFn lvaStressLclFldCB;
#endif

    /// <summary>LclVar number</summary>
    public int lvaGSSecurityCookie = BAD_VAR_NUM;

#if TARGET_ARM64
    /// <summary>LclVar number</summary>
    public int lvaFfrRegister = BAD_VAR_NUM;
#endif

    /// <summary>Variable representing the secret stub argument</summary>
    public int lvaStubArgumentVar = BAD_VAR_NUM;

#if FEATURE_SIMD
    /// <summary>This is a temp lclVar allocated on the stack as TYP_Simd.</summary>
    /// <remarks>
    ///   <para>It is used to implement intrinsics that require indexed access to the individual fields of the vector, which is not well supported by the hardware.</para>
    ///   <para>It is allocated when/if such situations are encountered during Lowering.</para> 
    /// </remarks>
    public int lvaSimdInitTempVarNum = BAD_VAR_NUM;
#endif

    /// <summary>The highest frame layout state that we've completed.</summary>
    /// <remarks>During frame layout calculations, this is the level we are currently computing.</remarks>
    public FrameLayoutState lvaDoneFrameLayout;

    /// <summary>return true if there is no place in the code that writes to arg0</summary>
    public bool lvaIsOriginalThisReadOnly => lvaArg0Var == info.compThisArg;

    public bool lvaLocalVarRefCounted => lvaRefCountState == RCS_NORMAL;

    /// <summary>Return an upper bound estimate for the size of the compiler spill temps</summary>
    public int lvaMaxSpillTempSize
    {
        get
        {
            assert(codeGen is not null);
            return codeGen.RegSet.HasComputedTmpSize ? codeGen.RegSet.tmpTotalSize : MAX_SPILL_TEMP_SIZE;
        }
    }

    /// <summary>true if the LclVar was introduced by the CSE phase of the compiler</summary>
    /// <param name="lclNum"></param>
    /// <returns></returns>
    public bool lclNumIsTrueCSE(int lclNum)
    {
        return (optCSEcount > 0) && (lclNum >= optCSEstart) && (lclNum < (optCSEstart + optCSEcount));
    }

    /// <summary>true if the LclVar should be treated like a CSE with regards to constant prop.</summary>
    /// <param name="lclNum"></param>
    /// <returns></returns>
    public bool lclNumIsCSE(int lclNum) => lvaGetDesc(lclNum).lvIsCSE;

    public void lvaAllocOutgoingArgSpaceVar()
    {
#if FEATURE_FIXED_OUT_ARGS
        // Setup the outgoing argument region, in case we end up using it later
        if (lvaOutgoingArgSpaceVar == BAD_VAR_NUM)
        {
            lvaOutgoingArgSpaceVar = lvaGrabTempWithImplicitUse(shortLifetime: false, "OutgoingArgSpace");
            lvaSetStruct(lvaOutgoingArgSpaceVar, typGetBlkLayout(0), unsafeValueClsCheck: false);
            lvaSetVarAddrExposed(lvaOutgoingArgSpaceVar, (AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY));
        }

        noway_assert(lvaOutgoingArgSpaceVar >= info.compLocalsCount && lvaOutgoingArgSpaceVar < lvaCount);
#endif
    }

    public void lvaClassifyParameterAbi()
    {
        var cInfo = new ClassifierInfo {
            CallConv = info.compCallConv,
            IsVarArgs = info.compIsVarArgs,
            HasThis = info.compThisArg != BAD_VAR_NUM,
            HasRetBuff = info.compRetBuffArg != BAD_VAR_NUM,
        };

#if SWIFT_SUPPORT
        if (info.compCallConv == CorInfoCallConvExtension.Swift)
        {
            SwiftABIClassifier classifier(cInfo);
            lvaClassifyParameterABI(classifier);
        }
        else
#endif
        {
            var classifier = new PlatformClassifier(cInfo);
            lvaClassifyParameterAbi(ref classifier);
        }

#if DEBUG
        for (var lclNum = 0; lclNum < info.compArgsCount; lclNum++)
        {
            ref readonly var abiInfo = ref lvaGetParameterAbiInfo(lclNum);

            if (lvaIsImplicitByRefLocal(lclNum))
            {
                assert((abiInfo.NumSegments is 1) && (abiInfo.Segments[0].Size == TARGET_POINTER_SIZE));
            }
            else
            {
                var segments = abiInfo.Segments;

                for (var i = 0; i < segments.Length; i++)
                {
                    ref readonly var segment = ref segments[i];
                    assert(segment.Size > 0);
                    assert(segment.Offset + segment.Size <= lvaLclExactSize(lclNum));

                    if (i > 0)
                    {
                        assert(segment.Offset > segments[i - 1].Offset);
                    }

                    for (var j = 0; j < segments.Length; j++)
                    {
                        if (i == j)
                        {
                            continue;
                        }

                        ref readonly var otherSegment = ref segments[j];
                        assert((segment.Offset + segment.Size <= otherSegment.Offset) ||
                               (segment.Offset >= otherSegment.Offset + otherSegment.Size));
                    }
                }
            }
        }
#endif
    }

    public unsafe void lvaClassifyParameterAbi(ref PlatformClassifier classifier)
    {
        lvaParameterPassingInfo = (info.compArgsCount is 0) ? [] : new AbiPassingInformation[info.compArgsCount];

        for (var i = 0; i < info.compArgsCount; i++)
        {
            ref var dsc = ref lvaGetDesc(i);
            var structLayout = varTypeIsStruct(dsc.Type) ? dsc.Layout : null;

            var wellKnownArg = WellKnownArg.None;

            if (i == info.compRetBuffArg)
            {
                wellKnownArg = WellKnownArg.RetBuffer;
            }
#if SWIFT_SUPPORT
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
#endif

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
            if ((JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] != 0) && IsReadyToRun && (structLayout is not null))
            {
                var clsHnd = structLayout.ClassHandle;

                if (clsHnd != NO_CLASS_HANDLE)
                {
                    info.compCompHnd->getWasmLowering(clsHnd);
                }
            }
#endif
        }

        lvaParameterStackSize = classifier.StackSize;

#if TARGET_ARM
        // Prespill all argument regs on to stack in case of Arm when under profiler.
        // We do this as the arm32 CORINFO_HELP_FCN_ENTER helper does not preserve
        // these registers, and is called very early.
        if (compIsProfilerHookNeeded)
        {
            codeGen.RegSet.rsMaskPreSpillRegArg |= RBM_ARG_REGS;
        }

        var doubleAlignMask = RBM_NONE;

        // Also prespill struct parameters.
        for (var i = 0; i < info.compArgsCount; i++)
        {
            ref readonly var abiInfo  = ref lvaGetParameterABIInfo(i);
            ref var varDsc   = ref lvaGetDesc(i);
            var preSpill = opts.compUseSoftFP && varTypeIsFloating(varDsc);
            preSpill |= varDsc.TypeIs(TYP_STRUCT);

            if (!preSpill)
            {
                continue;
            }

            var regs = RBM_NONE;

            foreach (ref readonly var segment in abiInfo.Segments)
            {
                if (segment.IsPassedInRegister && genIsValidIntReg(segment.Register))
                {
                    regs |= segment.RegisterMask;
                }
            }

            codeGen.RegSet.rsMaskPreSpillRegArg |= regs;

            if (varDsc.lvStructDoubleAlign || (varDsc.Type is TYP_DOUBLE))
            {
                doubleAlignMask |= regs;
            }
        }

        if (doubleAlignMask != RBM_NONE)
        {
            assert(RBM_ARG_REGS is 0xF);
            assert((doubleAlignMask & RBM_ARG_REGS) == doubleAlignMask);

            if ((doubleAlignMask != RBM_NONE) && (doubleAlignMask != RBM_ARG_REGS))
            {
                // 'double aligned types' can begin only at r0 or r2 and we always expect at least two registers to be used
                // Note that in rare cases, we can have double-aligned structs of 12 bytes (if specified explicitly with
                // attributes)
                assert((doubleAlignMask is 0b0011) || (doubleAlignMask is 0b1100) ||
                       (doubleAlignMask is 0b0111) /* || 0b1111 is if'ed out */);

                // Now if doubleAlignMask is xyz1 i.e., the struct starts in r0, and we prespill r2 or r3
                // but not both, then the stack would be misaligned for r0. So spill both
                // r2 and r3.
                //
                // ; +0 --- caller SP double aligned ----
                // ; -4 r2    r3
                // ; -8 r1    r1
                // ; -c r0    r0   <-- misaligned.
                // ; callee saved regs
                var startsAtR0 = (doubleAlignMask & 1) is 1;
                var r2XorR3    = ((codeGen.RegSet.rsMaskPreSpillRegArg & RBM_R2) is 0) !=
                                 ((codeGen.RegSet.rsMaskPreSpillRegArg & RBM_R3) is 0);
                if (startsAtR0 && r2XorR3)
                {
                    codeGen.RegSet.rsMaskPreSpillAlign = (~codeGen.RegSet.rsMaskPreSpillRegArg & ~doubleAlignMask) & RBM_ARG_REGS;
                }
            }
        }
#endif
    }

#if DEBUG
    public unsafe void lvaDumpEntry(int lclNum, FrameLayoutState curState, int refCntWtdWidth)
    {
        ref var varDsc = ref lvaGetDesc(lclNum);
        var type = varDsc.Type;

        if (curState == INITIAL_FRAME_LAYOUT)
        {
            jitprintf(";  ");
            gtDispLclVar(lclNum);

            jitprintf($" {type.Name,7} ");
            gtDispLclVarStructType(lclNum);
        }
        else
        {
            if (varDsc.lvRefCnt() is 0)
            {
                // Print this with a special indicator that the variable is unused. Even though the
                // variable itself is unused, it might be a struct that is promoted, so seeing it
                // can be useful when looking at the promoted struct fields. It's also weird to see
                // missing var numbers if these aren't printed.
                jitprintf(";* ");
            }
#if FEATURE_FIXED_OUT_ARGS
            // Since lvaOutgoingArgSpaceSize is a PhasedVar we can't read it for Dumping until
            // after we set it to something.
            else if ((lclNum == lvaOutgoingArgSpaceVar) && lvaOutgoingArgSpaceSize.HasFinalValue && (lvaOutgoingArgSpaceSize.Value is 0))
            {
                // Similar to above; print this anyway.
                jitprintf(";# ");
            }
#endif
            else
            {
                jitprintf(";  ");
            }

            gtDispLclVar(lclNum);

            jitprintf($"[V{lclNum:D2}");
            if (varDsc.lvTracked)
            {
                jitprintf($",T{varDsc._varIndex:D2}]");
            }
            else
            {
                jitprintf("    ]");
            }

            var refCntWtd = refCntWtd2str(varDsc.lvRefCntWtd(lvaRefCountState), /* padForDecimalPlaces */ true);
            jitprintf($" ({varDsc.lvRefCnt(lvaRefCountState):D3},{new string(' ', int.Max(0, refCntWtdWidth - refCntWtd.Length))}{refCntWtd}");

            jitprintf($" {type.Name,7} ");

            if (type.Size is 0)
            {
                jitprintf($"({lvaLclStackHomeSize(lclNum):D2}) ");
            }
            else
            {
                jitprintf(" ->  ");
            }

            // The register or stack location field is 11 characters wide.
            if ((varDsc.lvRefCnt(lvaRefCountState) is 0) && !varDsc.lvImplicitlyReferenced)
            {
                jitprintf("zero-ref   ");
            }
            else if (varDsc.lvRegister)
            {
                // It's always a register, and always in the same register.
                lvaDumpRegLocation(lclNum);
            }
            else if (!varDsc.lvOnFrame)
            {
                jitprintf("registers  ");
            }
            else
            {
                // For RyuJIT backend, it might be in a register part of the time, but it will definitely have a stack home
                // location. Otherwise, it's always on the stack.
                if (lvaDoneFrameLayout != NO_FRAME_LAYOUT)
                {
                    lvaDumpFrameLocation(lclNum, "zero-ref   ".Length);
                }
            }
        }

        if (varDsc.lvDoNotEnregister)
        {
            jitprintf(" do-not-enreg[");

            if (varDsc.IsAddressExposed)
            {
                jitprintf("X");
            }

            if (varDsc.IsDefinedViaAddress)
            {
                jitprintf("DA");
            }

            if (varTypeIsStruct(varDsc.Type))
            {
                jitprintf("S");
            }

            if (varDsc.DoNotEnregisterReason == DoNotEnregisterReason.VMNeedsStackAddr)
            {
                jitprintf("V");
            }

            if (lvaEnregEHVars && varDsc.lvTracked && varDsc.IsLiveInOutOfHandler)
            {
                jitprintf($"{(char)(varDsc.lvSingleDefDisqualifyReason)}");
            }

            if (varDsc.DoNotEnregisterReason == DoNotEnregisterReason.LocalField)
            {
                jitprintf("F");
            }

            if (varDsc.DoNotEnregisterReason == DoNotEnregisterReason.BlockOp)
            {
                jitprintf("B");
            }

            if (varDsc.lvIsMultiRegArg)
            {
                jitprintf("A");
            }

            if (varDsc.lvIsMultiRegRet)
            {
                jitprintf("R");
            }

            if (varDsc.lvIsMultiRegDest)
            {
                jitprintf("M");
            }

#if JIT32_GCENCODER
            if (varDsc.lvPinned)
            {
                jitprintf("P");
            }
#endif

            jitprintf("]");
        }

        if (varDsc.lvIsMultiRegArg)
        {
            jitprintf(" multireg-arg");
        }

        if (varDsc.lvIsMultiRegRet)
        {
            jitprintf(" multireg-ret");
        }

        if (varDsc.lvIsMultiRegDest)
        {
            jitprintf(" multireg-dest");
        }

        if (varDsc.lvMustInit)
        {
            jitprintf(" must-init");
        }

        if (varDsc.IsAddressExposed)
        {
            jitprintf(" addr-exposed");
        }

        if (varDsc.IsDefinedViaAddress)
        {
            jitprintf(" defined-via-address");
        }

        if (varDsc.lvHasLdAddrOp)
        {
            jitprintf(" ld-addr-op");
        }

        if (lvaIsOriginalThisArg(lclNum))
        {
            jitprintf(" this");
        }

        if (varDsc.lvPinned)
        {
            jitprintf(" pinned");
        }

        if (varDsc.lvClassHnd != NO_CLASS_HANDLE)
        {
            jitprintf(" class-hnd");
        }

        if (varDsc.lvClassIsExact)
        {
            jitprintf(" exact");
        }

        if (varDsc.lvTracked && varDsc.IsLiveInOutOfHandler)
        {
            jitprintf(" EH-live");
        }

        if (varDsc.lvSpillAtSingleDef)
        {
            jitprintf(" spill-single-def");
        }
        else if (varDsc.lvSingleDefRegCandidate)
        {
            jitprintf(" single-def");
        }

        if (lvaIsOSRLocal(lclNum) && varDsc.lvOnFrame)
        {
            jitprintf(" tier0-frame");
        }

        if (varDsc.lvIsHoist)
        {
            jitprintf(" hoist");
        }

        if (varDsc.lvIsMultiDefCSE)
        {
            jitprintf(" multi-def");
        }

#if !TARGET_64BIT
        if (varDsc.lvStructDoubleAlign)
        {
            jitprintf(" double-align");
        }
#endif

        if (compGSReorderStackLayout && !varDsc.lvRegister)
        {
            if (varDsc.lvIsPtr)
            {
                jitprintf(" ptr");
            }

            if (varDsc.lvIsUnsafeBuffer)
            {
                jitprintf(" unsafe-buffer");
            }
        }

        if (varDsc.lvReason is not null)
        {
            jitprintf($" \"{varDsc.lvReason}\"");
        }

        if (varDsc.lvIsStructField)
        {
            ref var parentVarDsc = ref lvaGetDesc(varDsc.lvParentLcl);
            var promotionType = lvaGetPromotionType(parentVarDsc);

            switch (promotionType)
            {
                case PROMOTION_TYPE_NONE:
                {
                    jitprintf(" P-NONE");
                    break;
                }

                case PROMOTION_TYPE_DEPENDENT:
                {
                    jitprintf(" P-DEP");
                    break;
                }

                case PROMOTION_TYPE_INDEPENDENT:
                {
                    jitprintf(" P-INDEP");
                    break;
                }
            }
        }

        if (varDsc.lvClassHnd != NO_CLASS_HANDLE)
        {
            jitprintf($" <{eeGetClassName(varDsc.lvClassHnd)}>");
        }
        else if (varTypeIsStruct(varDsc.Type))
        {
            var layout = varDsc.Layout;

            if (layout is not null)
            {
                jitprintf($" <{layout.ClassName}>");
            }
        }

        jitprintf("\n");
    }

    /// <summary>Dump the register a local is in right now. It is only the current location, since the location changes and it is updated throughout code generation based on LSRA register assignments.</summary>
    /// <param name="lclNum"></param>

    public void lvaDumpRegLocation(int lclNum)
    {
        ref var varDsc = ref lvaGetDesc(lclNum);

#if TARGET_ARM
        if (varDsc.Type is TYP_DOUBLE)
        {
            // The assigned registers are `lvRegNum:RegNext(lvRegNum)`
            jitprintf($"{varDsc.RegNum.Name,3}:{REG_NEXT(varDsc.RegNum),-3}    ");
        }
        else
#endif
        {
            jitprintf($"{varDsc.RegNum.Name,3}        ");
        }
    }

    /// <summary>Dump the frame location assigned to a local. It's the home location, even though the variable doesn't always live in its home location.</summary>
    /// <param name="lclNum"></param>
    /// <param name="minLength"></param>
    public void lvaDumpFrameLocation(int lclNum, int minLength)
    {
        var message = "";

#if TARGET_ARM64
        if (lvaIsUnknownSizeLocal(lclNum))
        {
            ref var varDsc = ref lvaGetDesc(lclNum);
            var offset = unkSizeFrame.GetAddressingOffset(varDsc);
            jitprintf($"[{REG_UNKBASE.Name,2}{(offset < 0 ? "-" : "+")}0x{(offset < 0 ? -offset : offset):X2}*{((varDsc.Type is TYP_MASK) ? "PL" : "VL")}] ");
        }
        else
#endif
        {
#if TARGET_ARM
            var offset = lvaFrameAddress(lclNum, compLocallocUsed, out var baseReg, 0, isFloatUsage: false);
#else
            assert(codeGen is not null);
            var offset = lvaFrameAddress(lclNum, out var EBPbased);
            var baseReg = EBPbased ? codeGen.GetFramePointerReg(ROOT_FUNC_IDX) : codeGen.GetStackPointerReg(ROOT_FUNC_IDX);
#endif

            message = $"[{baseReg.Name,2}{(offset < 0 ? "-" : "+")}0x{(offset < 0 ? -offset : offset):X2}] ";
        }

        if (message.Length is not 0)
        {
            jitprintf($"{new string(' ', int.Max(0, minLength - message.Length))}{message}");
        }
    }

#if TARGET_ARM
    /// <summary>Determine the stack frame offset of the given variable, and how to generate an address to that stack frame.</summary>
    /// <param name="varNum">The variable to inquire about. Positive for user variables or arguments, negative for spill-temporaries.</param>
    /// <param name="mustBeFPBased">True if the base register must be FP. After FINAL_FRAME_LAYOUT, if false, it also requires SP base register.</param>
    /// <param name="baseReg">Set to the base register to use.</param>
    /// <param name="addrModeOffset">The mode offset within the variable that we need to address. For example, for a large struct local, and a struct field reference, this will be the offset of the field. Thus, for V02 + 0x28, if V02 itself is at offset SP + 0x10 then addrModeOffset is what gets added beyond that, here 0x28.</param>
    /// <param name="isFloatUsage">True if the instruction being generated is a floating point instruction. This requires using floating-point offset restrictions. Note that a variable can be non-float, e.g., struct, but accessed as a float local field.</param>
    /// <returns>Returns the variable offset from the given base register.</returns>
    public int lvaFrameAddress(int varNum, bool mustBeFPBased, out regNumber baseReg, int addrModeOffset, bool isFloatUsage)
#elif TARGET_ARM64
    /// <summary>Determine the stack frame offset of the given variable, and how to generate an address to that stack frame.</summary>
    /// <param name="varNum">The variable to inquire about. Positive for user variables or arguments, negative for spill-temporaries.</param>
    /// <param name="fpBased">Set to true if the variable is addressed off of FP, false if it's addressed off of SP.</param>
    /// <param name="suppressFPtoSPRewrite"></param>
    /// <returns>Returns the variable offset from the given base register.</returns>
    public int lvaFrameAddress(int varNum, out bool fpBased, bool suppressFPtoSPRewrite = false)
#else
    /// <summary>Determine the stack frame offset of the given variable, and how to generate an address to that stack frame.</summary>
    /// <param name="varNum">The variable to inquire about. Positive for user variables or arguments, negative for spill-temporaries.</param>
    /// <param name="fpBased">Set to true if the variable is addressed off of FP, false if it's addressed off of SP.</param>
    /// <returns>Returns the variable offset from the given base register.</returns>
    public int lvaFrameAddress(int varNum, out bool fpBased)
#endif
    {
        assert(lvaDoneFrameLayout != NO_FRAME_LAYOUT);

        int varOffset;

#if TARGET_ARM
        var fConservative = false;
#endif

        if (varNum >= 0)
        {
            assert(!lvaIsUnknownSizeLocal(varNum));
            ref var varDsc = ref lvaGetDesc(varNum);

#if TARGET_ARM && PROFILING_SUPPORTED
            var isPrespilledArg = varDsc.lvIsParam && compIsProfilerHookNeeded && lvaIsPreSpilled(varNum, codeGen.RegSet.rsMaskPreSpillRegs(false));
#elif TARGET_ARM 
            var isPrespilledArg = false;
#endif

            // If we have finished with register allocation, and this isn't a stack-based local, check that this has a valid stack location.
            if ((lvaDoneFrameLayout > REGALLOC_FRAME_LAYOUT) && !varDsc.lvOnFrame)
            {
#if !TARGET_AMD64
                // For other targets, a stack parameter that is enregistered or prespilled for profiling on ARM will have a stack location.
                assert((varDsc.lvIsParam && !varDsc.lvIsRegArg) || isPrespilledArg);
#elif !UNIX_AMD64_ABI
                // On amd64, every param has a stack location, except on Unix-like systems.
                assert(varDsc.lvIsParam);
#endif
            }

            fpBased = varDsc.lvFramePointerBased;

#if DEBUG
#if FEATURE_FIXED_OUT_ARGS
            if (varNum == lvaOutgoingArgSpaceVar)
            {
                assert(!fpBased);
            }
            else
#endif
            {
#if DOUBLE_ALIGN
                assert(fpBased == (IsFramePointerUsed || (genDoubleAlign && varDsc.lvIsParam && !varDsc.lvIsRegArg)));
#elif TARGET_X86
                assert(fpBased == IsFramePointerUsed);
#endif
            }
#endif

            varOffset = varDsc.StackOffset;
        }
        else
        {
            // Its a spill-temp
            fpBased = IsFramePointerUsed;

            if (lvaDoneFrameLayout is FINAL_FRAME_LAYOUT)
            {
                assert(codeGen is not null);
                var tempDsc = codeGen.RegSet.tmpGetNum(varNum);

                assert(!varTypeHasUnknownSize(tempDsc.tdTempType));
                varOffset = tempDsc.tdTempOffs;
            }
            else
            {
                // This value is an estimate until we calculate the
                // offset after the final frame layout
                // ---------------------------------------------------
                //   :                         :
                //   +-------------------------+ base --+
                //   | LR, ++N for ARM         |        |   frameBaseOffset (= N)
                //   +-------------------------+        |
                //   | R11, ++N for ARM        | <---FP |
                //   +-------------------------+      --+
                //   | compCalleeRegsPushed - N|        |   lclFrameOffset
                //   +-------------------------+      --+
                //   | lclVars                 |        |
                //   +-------------------------+        |
                //   | tmp[MAX_SPILL_TEMP]     |        |
                //   | tmp[1]                  |        |
                //   | tmp[0]                  |        |   compLclFrameSize
                //   +-------------------------+        |
                //   | outgoingArgSpaceSize    |        |
                //   +-------------------------+      --+
                //   |                         | <---SP
                //   :                         :
                // ---------------------------------------------------

#if TARGET_ARM
                fConservative = true;
#endif

                if (!fpBased)
                {
                    // Worst case stack based offset.
#if FEATURE_FIXED_OUT_ARGS
                    var outGoingArgSpaceSize = lvaOutgoingArgSpaceSize.Value;
#else
                    var outGoingArgSpaceSize = 0;
#endif
                    varOffset = outGoingArgSpaceSize + int.Max(-varNum * TARGET_POINTER_SIZE, lvaMaxSpillTempSize);
                }
                else
                {
                    // Worst case FP based offset.
                    assert(codeGen is not null);

#if TARGET_ARM
                    varOffset = codeGen.genCallerSPtoInitialSPdelta - codeGen.genCallerSPtoFPdelta;
#else
                    varOffset = -codeGen.genTotalFrameSize;
#endif
                }
            }
        }

#if TARGET_ARM
        if (fpBased)
        {
            if (mustBeFPBased)
            {
                baseReg = REG_FPBASE;
            }
            else
            {
                // Change the Frame Pointer (R11)-based addressing to the SP-based addressing when possible because it generates smaller code on ARM. See frame picture above for the math.

                // If it is the final frame layout phase, we don't have a choice, we should stick
                // to either FP based or SP based that we decided in the earlier phase. Because
                // we have already selected the instruction. MinOpts will always reserve R10, so
                // for MinOpts always use SP-based offsets, using R10 as necessary, for simplicity.

                var spVarOffset = fConservative ? compLclFrameSize : varOffset + codeGen.genSPtoFPdelta;
                var actualSPOffset = spVarOffset + addrModeOffset;
                var actualFPOffset = varOffset + addrModeOffset;
                var encodingLimitUpper = isFloatUsage ? 0x3FC : 0xFFF;
                var encodingLimitLower = isFloatUsage ? -0x3FC : -0xFF;

                if (opts.MinOpts || (actualSPOffset <= encodingLimitUpper))
                {
                    // Use SP-based encoding. During encoding, we'll pick the best encoding for the actual offset we have.
                    varOffset = spVarOffset;
                    baseReg = compLocallocUsed ? REG_SAVED_LOCALLOC_SP : REG_SPBASE;
                }
                else if ((encodingLimitLower <= actualFPOffset) && (actualFPOffset <= encodingLimitUpper))
                {
                    // Use Frame Pointer (R11)-based encoding.
                    baseReg = REG_FPBASE;
                }
                else
                {
                    // Otherwise, use SP-based encoding. This is either (1) a small positive offset using a single movw,
                    // (2) a large offset using movw/movt. In either case, we must have already reserved
                    // the "reserved register", which will get used during encoding.

                    varOffset = spVarOffset;
                    baseReg = compLocallocUsed ? REG_SAVED_LOCALLOC_SP : REG_SPBASE;
                }
            }
        }
        else
        {
            baseReg = REG_SPBASE;
        }

#elif TARGET_ARM64
        if (fpBased && !suppressFPtoSPRewrite && !codeGen.IsFramePointerRequired && (varOffset < 0) && !opts.IsOSR && (lvaDoneFrameLayout == FINAL_FRAME_LAYOUT) && codeGen.IsSaveFpLrWithAllCalleeSavedRegisters)
        {
            var spVarOffset = varOffset + codeGen.genSPtoFPdelta;
            JITDUMP($"lvaFrameAddress optimization for V{varNum:D2}: [FP-{-varOffset}] -> [SP+{spVarOffset}]\n");

            fpBased   = false;
            varOffset = spVarOffset;
        }
#endif

        return varOffset;
    }

    /// <summary>Return true if the local is a field local of a promoted struct of type PROMOTION_TYPE_DEPENDENT; otherwise, false.</summary>
    /// <param name="varDsc"></param>
    /// <returns></returns>
    public bool lvaIsFieldOfDependentlyPromotedStruct(in LclVarDsc varDsc)
    {
        if (!varDsc.lvIsStructField)
        {
            return false;
        }

        var promotionType = lvaGetParentPromotionType(varDsc);

        if (promotionType is PROMOTION_TYPE_DEPENDENT)
        {
            return true;
        }

        assert(promotionType is PROMOTION_TYPE_INDEPENDENT);
        return false;
    }

    /// <summary>Determine whether this var should be reported as tracked for GC purposes.</summary>
    /// <param name="varDsc">the LclVarDsc for the var in question.</param>
    /// <returns>Returns true if the variable should be reported as tracked in the GC info.</returns>
    /// <remarks>
    ///   <para>This never returns true for struct variables, even if they are tracked. This is because struct variables are never tracked as a whole for GC purposes. It is up to the caller to ensure that the fields of struct variables are correctly tracked.</para>
    ///   <para>We never GC-track fields of dependently promoted structs, even though they may be tracked for optimization purposes.</para>
    /// </remarks>
    public bool lvaIsGCTracked(in LclVarDsc varDsc)
    {
        if (varDsc.lvTracked && (varDsc.Type is TYP_REF or TYP_BYREF))
        {
            // Stack parameters are always untracked w.r.t. GC reportings
            var isStackParam = varDsc.lvIsParam && !varDsc.lvIsRegArg;
            return !isStackParam && !lvaIsFieldOfDependentlyPromotedStruct(varDsc);
        }
        else
        {
            return false;
        }
    }

    /// <summary>Does the local have an unknown size at compile-time?</summary>
    /// <param name="varNum"></param>
    /// <returns>True if the local does not have an exact size, else false.</returns>
#if TARGET_ARM64
    public bool lvaIsUnknownSizeLocal(int varNum) => !lvaLclValueSize(varNum).IsExact;
#else
    public bool lvaIsUnknownSizeLocal(int varNum) => false;
#endif

    public void lvaTableDump(FrameLayoutState curState = NO_FRAME_LAYOUT)
    {
        if (curState == NO_FRAME_LAYOUT)
        {
            curState = lvaDoneFrameLayout;

            if (curState == NO_FRAME_LAYOUT)
            {
                // Still no layout? Could be a bug, but just display the initial layout
                curState = INITIAL_FRAME_LAYOUT;
            }
        }

        if (curState == INITIAL_FRAME_LAYOUT)
        {
            jitprintf("; Initial");
        }
        else if (curState == PRE_REGALLOC_FRAME_LAYOUT)
        {
            jitprintf("; Pre-RegAlloc");
        }
        else if (curState == REGALLOC_FRAME_LAYOUT)
        {
            jitprintf("; RegAlloc");
        }
        else if (curState == TENTATIVE_FRAME_LAYOUT)
        {
            jitprintf("; Tentative");
        }
        else if (curState == FINAL_FRAME_LAYOUT)
        {
            jitprintf("; Final");
        }
        else
        {
            jitprintf("UNKNOWN FrameLayoutState!");
            unreached();
        }

        jitprintf(" local variable assignments\n");
        jitprintf(";\n");

        // Figure out some sizes, to help line things up

        // Use 6 as the minimum width
        var refCntWtdWidth = 6;

        // don't need this info for INITIAL_FRAME_LAYOUT
        if (curState != INITIAL_FRAME_LAYOUT)
        {
            for (var lclNum = 0; lclNum < lvaCount; lclNum++)
            {
                ref var varDsc = ref lvaGetDesc(lclNum);

                var width = refCntWtd2str(varDsc.lvRefCntWtd(lvaRefCountState), padForDecimalPlaces: true).Length;

                if (width > refCntWtdWidth)
                {
                    refCntWtdWidth = width;
                }
            }
        }

        // Do the actual output

        for (var lclNum = 0; lclNum < lvaCount; lclNum++)
        {
            lvaDumpEntry(lclNum, curState, refCntWtdWidth);
        }

        //-------------------------------------------------------------------------
        // Display the code-gen temps

        assert(codeGen is not null);

#if DEBUG
        assert(codeGen.RegSet.tmpGetAllFree());
#endif

        for (var tempDsc = codeGen.RegSet.tmpListBeg(); tempDsc is not null; tempDsc = codeGen.RegSet.tmpListNxt(tempDsc))
        {
            jitprintf($";  TEMP_{-tempDsc.tdTempNum:D2} {new string(' ', 26 + refCntWtdWidth)}{tempDsc.tdTempType.Name,7}  -> ");
            var offset = tempDsc.tdTempOffs;
            jitprintf($" [{(IsFramePointerUsed ? STR_FPBASE : STR_SPBASE),2}{(offset < 0 ? "-" : "+")}0x{(offset < 0 ? -offset : offset):X2}]\n");
        }
        
        if (curState >= TENTATIVE_FRAME_LAYOUT)
        {
            jitprintf(";\n");
            jitprintf($"; Lcl frame size = {compLclFrameSize}\n");
        }
    }
#endif

    public ref LclVarDsc lvaGetDesc(int lclNum)
    {
        assert((lclNum >= 0) && (lclNum < lvaCount));
        return ref lvaTable[lclNum];
    }

    public int lvaGetLclNum(in LclVarDsc varDsc)
    {
        assert(Unsafe.IsAddressGreaterThanOrEqualTo(in varDsc, in lvaTable[0]) && Unsafe.IsAddressLessThan(in varDsc, in lvaTable[lvaCount]));
        var varNum = (int)(Unsafe.ByteOffset(in lvaTable[0], in varDsc) / Unsafe.SizeOf<LclVarDsc>());

        assert(Unsafe.AreSame(in varDsc, in lvaTable[varNum]));
        return varNum;
    }

    public ref readonly AbiPassingInformation lvaGetParameterAbiInfo(int lclNum)
    {
        assert(lclNum < info.compArgsCount);
        return ref lvaParameterPassingInfo[lclNum];
    }

    public lvaPromotionType lvaGetParentPromotionType(in LclVarDsc varDsc)
    {
        assert(varDsc.lvIsStructField);
        var promotionType = lvaGetPromotionType(varDsc.lvParentLcl);

        assert(promotionType is not PROMOTION_TYPE_NONE);
        return promotionType;
    }
    public lvaPromotionType lvaGetParentPromotionType(int varNum)
        => lvaGetParentPromotionType(lvaGetDesc(varNum));

    public lvaPromotionType lvaGetPromotionType(in LclVarDsc varDsc)
    {
#if DEBUG
        // TODO-Review: Sometimes we get called on ARM with HFA struct variables that have been promoted,
        // where the struct itself is no longer used because all access is via its member fields.
        // When that happens, the struct is marked as unused and its type has been changed to
        // TYP_INT (to keep the GC tracking code from looking at it).
        // See Compiler::raAssignVars() for details. For example:
        //      N002 (  4,  3) [00EA067C] -------------               return    struct $346
        //      N001 (  3,  2) [00EA0628] -------------                  lclVar    struct(U) V03 loc2
        //                                                                        float  V03.f1 (offs=0x00) -> V12 tmp7
        //                                                                        f8 (last use) (last use) $345
        // Here, the "struct(U)" shows that the "V03 loc2" variable is unused. Not shown is that V03
        // is now TYP_INT in the local variable table. It's not really unused, because it's in the tree.
        assert(!varDsc.lvPromoted || varTypeIsPromotable(varDsc.Type) || varDsc.lvUnusedStruct);
#endif

        if (!varDsc.lvPromoted)
        {
            // no struct promotion for this LclVar
            return PROMOTION_TYPE_NONE;
        }
        else if (varDsc.lvDoNotEnregister)
        {
            // The struct is not enregistered
            return PROMOTION_TYPE_DEPENDENT;
        }
        return PROMOTION_TYPE_INDEPENDENT;
    }

    public lvaPromotionType lvaGetPromotionType(int varNum)
        => lvaGetPromotionType(lvaGetDesc(varNum));

    public int lvaGrabTemp(bool shortLifetime, string reason)
    {
        if (compIsForInlining)
        {
            // Grab the temp using Inliner's Compiler instance.
            var inlinerCompiler = impInlineInfo.InlinerCompiler; // The Compiler instance for the caller (i.e. the inliner)

            if (inlinerCompiler.lvaHaveManyLocals())
            {
                // Don't create more LclVar with inlining
                compInlineResult.NoteFatal(InlineObservation.CALLSITE_TOO_MANY_LOCALS);
            }

            var tmpNum = inlinerCompiler.lvaGrabTemp(shortLifetime, reason);
            lvaTable = inlinerCompiler.lvaTable;
            lvaCount = inlinerCompiler.lvaCount;
            return tmpNum;
        }

        // You cannot allocate more space after frame layout!
        noway_assert(lvaDoneFrameLayout < TENTATIVE_FRAME_LAYOUT);

        /* Check if the lvaTable has to be grown */
        if ((lvaCount + 1) > lvaTable.Length)
        {
            var newLvaTableCnt = lvaCount + (lvaCount / 2) + 1;

            // Check for overflow
            if (newLvaTableCnt <= lvaCount)
            {
                IMPL_LIMITATION("too many locals");
            }

            var newLvaTable = new LclVarDsc[newLvaTableCnt];

            lvaTable.AsSpan().CopyTo(newLvaTable);

            for (var i = lvaCount; i < newLvaTable.Length; i++)
            {
                newLvaTable[i] = new LclVarDsc(); // call the constructor.
            }

#if DEBUG
            // Fill the old table with junks. So to detect the un-intended use.
            lvaTable.AsSpan().Clear();
#endif

            lvaTable = newLvaTable;
        }

        var tempNum = lvaCount;
        lvaCount++;

        ref var lvaDsc = ref lvaTable[tempNum];

        // Initialize lvType, lvIsTemp and lvOnFrame
        lvaDsc.Type = TYP_UNDEF;
        lvaDsc.lvIsTemp = shortLifetime;
        lvaDsc.lvOnFrame = true;

        // If we've started normal ref counting, bump the ref count of this
        // local, as we no longer do any incremental counting, and we presume
        // this new local will be referenced.
        if (lvaLocalVarRefCounted)
        {
            if (opts.OptimizationDisabled)
            {
                lvaDsc.lvImplicitlyReferenced = true;
            }
            else
            {
                lvaDsc.setLvRefCnt(1);
                lvaDsc.setLvRefCntWtd(BB_UNITY_WEIGHT);
            }
        }

#if DEBUG
        lvaDsc.lvReason = reason;

        if (verbose)
        {
            jitprintf($"\nlvaGrabTemp returning {tempNum} (");
            gtDispLclVar(tempNum, false);
            jitprintf($"){(shortLifetime ? "" : " (a long lifetime temp)")} called for {reason}.\n");
        }
#endif

        return tempNum;
    }

    public int lvaGrabTemps(int cnt, string reason)
    {
        if (compIsForInlining)
        {
            var inlinerCompiler = impInlineInfo.InlinerCompiler;

            // Grab the temps using Inliner's Compiler instance.
            var tmpNum = inlinerCompiler.lvaGrabTemps(cnt, reason);

            lvaTable = inlinerCompiler.lvaTable;
            lvaCount = inlinerCompiler.lvaCount;

            return tmpNum;
        }

    #if DEBUG
        if (verbose)
        {
            jitprintf($"\nlvaGrabTemps({cnt}) returning {lvaCount}..{lvaCount + cnt - 1} (long lifetime temps) called for {reason}");
        }
    #endif

        // Could handle this...
        assert(!lvaLocalVarRefCounted);

    // You cannot allocate more space after frame layout!
    noway_assert(lvaDoneFrameLayout < Compiler.TENTATIVE_FRAME_LAYOUT);

    // Check if the lvaTable has to be grown
    if ((lvaCount + cnt) > lvaTable.Length)
    {
        var newLvaTableCnt = lvaCount + int.Max(lvaCount / 2 + 1, cnt);

        // Check for overflow
        if (newLvaTableCnt <= lvaCount)
        {
            IMPL_LIMITATION("too many locals");
        }

        var newLvaTable = new LclVarDsc[newLvaTableCnt];
        lvaTable.AsSpan(0, lvaCount).CopyTo(newLvaTable);

        for (var i = lvaCount; i < newLvaTableCnt; i++)
        {
            newLvaTable[i] = new LclVarDsc(); // call the constructor.
        }

    #if DEBUG
        // Fill the old table with junks. So to detect the un-intended use.
        lvaTable.AsSpan(0, lvaCount).Clear();
    #endif

        lvaTable = newLvaTable;
    }

    var tempNum = lvaCount;

        while (cnt-- != 0)
        {
            ref var lvaDsc = ref lvaTable[tempNum];

            lvaDsc.Type = TYP_UNDEF;
            lvaDsc.lvIsTemp = false;
            lvaDsc.lvOnFrame = true;

            lvaCount++;
        }

        return tempNum;
    }

    /// <summary>Allocate a temporary variable which is implicitly used by code-gen</summary>
    /// <param name="shortLifetime"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    /// <remarks>There will be no explicit references to the temp, and so it needs to be forced to be kept alive, and not be optimized away.</remarks>
    public int lvaGrabTempWithImplicitUse(bool shortLifetime, string reason)
    {
        if (compIsForInlining)
        {
            var inlinerCompiler = impInlineInfo.InlinerCompiler;

            // Grab the temp using Inliner's Compiler instance.
            var tmpNum = inlinerCompiler.lvaGrabTempWithImplicitUse(shortLifetime, reason);

            lvaTable = inlinerCompiler.lvaTable;
            lvaCount = inlinerCompiler.lvaCount;

            return tmpNum;
        }

        var lclNum = lvaGrabTemp(shortLifetime, reason);
        ref var varDsc = ref lvaGetDesc(lclNum);

        // Note the implicit use
        varDsc.lvImplicitlyReferenced = true;

        return lclNum;
    }

    public bool lvaHaveManyLocals(float percent = 1.0f)
    {
        assert((percent >= 0.0) && (percent <= 1.0));
        return (lvaCount >= (JitConfig[ConfigInteger.JitMaxLocalsToTrack] * percent));
    }

    public unsafe void lvaInitArgs(bool hasRetBuffArg)
    {
#if TARGET_ARM && PROFILING_SUPPORTED
        // Prespill all argument regs on to stack in case of Arm when under profiler.
        // We do this as the arm32 CORINFO_HELP_FCN_ENTER helper does not preserve
        // these registers, and is called very early.
        if (compIsProfilerHookNeeded)
        {
            codeGen.RegSet.rsMaskPreSpillRegArg |= RBM_ARG_REGS;
        }
#endif

        //----------------------------------------------------------------------

        var varNum = 0;

#if TARGET_WASM
        if (!opts.IsReversePInvoke)
        {
            // Wasm stack pointer is first arg
            lvaInitWasmStackPtrArg(&varNum);
        }
#endif

        // Is there a "this" pointer ?
        lvaInitThisPtr(ref varNum);

        var numUserArgsToSkip = 0;
        var numUserArgs = info.compMethodInfo->args.numArgs;

#if !TARGET_ARM
        if (TargetOS.IsWindows && callConvIsInstanceMethodCallConv(info.compCallConv))
        {
            // If we are a native instance method, handle the first user arg
            // (the unmanaged this parameter) and then handle the hidden
            // return buffer parameter.

            assert(numUserArgs >= 1);
            lvaInitUserArgs(ref varNum, 0, 1);

            numUserArgsToSkip++;
            numUserArgs--;

            if (hasRetBuffArg)
            {
                lvaInitRetBuffArg(ref varNum, useFixedRetBufReg: false);
            }
        }
        else
#endif
        {
            if (hasRetBuffArg)
            {
                // If we have a hidden return-buffer parameter, that comes here
                lvaInitRetBuffArg(ref varNum, useFixedRetBufReg: true);
            }
        }

        //======================================================================

#if USER_ARGS_COME_LAST
        // @GENERICS: final instantiation-info argument for shared generic methods
        // and shared generic struct instance methods
        lvaInitGenericsCtxt(ref varNum);

        lvaInitAsyncContinuation(ref varNum);

        //* If the method is varargs, process the varargs cookie
        lvaInitVarArgsHandle(ref varNum);
#endif

        //-------------------------------------------------------------------------
        // Now walk the function signature for the explicit user arguments
        //-------------------------------------------------------------------------
        lvaInitUserArgs(ref varNum, numUserArgsToSkip, numUserArgs);

#if !USER_ARGS_COME_LAST
        lvaInitAsyncContinuation(ref varNum);

        //@GENERICS: final instantiation-info argument for shared generic methods
        // and shared generic struct instance methods
        lvaInitGenericsCtxt(ref varNum);

        // If the method is varargs, process the varargs cookie
        lvaInitVarArgsHandle(ref varNum);
#endif

#if TARGET_WASM
        if (!opts.IsReversePInvoke)
        {
            // Wasm portable entry point is the very last arg
            lvaInitWasmPortableEntryPtr(&varNum);
        }
#endif

        //----------------------------------------------------------------------

        // We have set info.compArgsCount in compCompile()
        noway_assert(varNum == info.compArgsCount);

        // Now we have parameters created in the right order. Figure out how they're passed.
        lvaClassifyParameterAbi();

        // The total argument size must be aligned.
        noway_assert((lvaParameterStackSize % TARGET_POINTER_SIZE) is 0);

#if TARGET_X86
        // We can not pass more than 2^16 dwords as arguments as the "ret"
        // instruction can only pop 2^16 arguments. Could be handled correctly
        // but it will be very difficult for fully interruptible code

        if (lvaParameterStackSize != unchecked((ushort)(lvaParameterStackSize)))
        {
            IMPL_LIMITATION("Too many arguments for the \"ret\" instruction to pop");
        }
#endif
    }

    /// <summary>Initialize the async continuation parameter.</summary>
    /// <param name="curVarNum">The current local variable number for parameters</param>
    public void lvaInitAsyncContinuation(ref int curVarNum)
    {
        if (!compIsAsync)
        {
            return;
        }

        lvaAsyncContinuationArg = curVarNum;

        ref var varDsc = ref lvaGetDesc(curVarNum);
        varDsc.Type = TYP_REF;
        varDsc.lvIsParam = true;

        // The final home for this incoming register might be our local stack frame
        varDsc.lvOnFrame = true;

#if DEBUG
        varDsc.lvReason = "Async continuation arg";
#endif

        curVarNum++;
    }

    public unsafe void lvaInitGenericsCtxt(ref int curVarNum)
    {
        // @GENERICS: final instantiation-info argument for shared generic methods
        // and shared generic struct instance methods
        if ((info.compMethodInfo->args.callConv & CORINFO_CALLCONV_PARAMTYPE) is 0)
        {
            return;
        }

        info.compTypeCtxtArg = curVarNum;

        ref var varDsc = ref lvaGetDesc(curVarNum);
        varDsc.lvIsParam = true;
        varDsc.Type = TYP_I_IMPL;
        varDsc.lvOnFrame = true; // The final home for this incoming register might be our local stack frame

        curVarNum++;
    }

    public void lvaInitRetBuffArg(ref int curVarNum, bool useFixedRetBufReg)
    {
        info.compRetBuffArg = curVarNum;

        ref var varDsc = ref lvaGetDesc(curVarNum);
        varDsc.Type = TYP_I_IMPL;
        varDsc.lvIsParam = true;
        varDsc.lvIsRegArg = false;
        varDsc.lvOnFrame = true; // The final home for this incoming register might be our local stack frame

        curVarNum++;
    }

    public unsafe void lvaInitThisPtr(ref int curVarNum)
    {
        if (info.compIsStatic)
        {
            return;
        }

        ref var varDsc = ref lvaGetDesc(curVarNum);
        varDsc.lvIsParam = true;
        varDsc.lvIsPtr = true;

        lvaArg0Var = curVarNum;
        info.compThisArg = lvaArg0Var;

#if TARGET_WASM
        noway_assert(info.compThisArg is 1);
#else
        noway_assert(info.compThisArg is 0);
#endif

        if (eeIsValueClass(info.compClassHnd))
        {
            varDsc.Type = TYP_BYREF;
        }
        else
        {
            varDsc.Type = TYP_REF;
            lvaSetClass(curVarNum, info.compClassHnd);
        }

        // The final home for this incoming register might be our local stack frame
        varDsc.lvOnFrame = true;

        curVarNum++;
    }

    public unsafe void lvaInitTypeRef()
    {
        // x86 args look something like this:
        //  [this ptr] [hidden return buffer] [declared arguments]* [generic context] [async continuation] [var arg cookie]
        // 
        // x64 is closer to the native ABI:
        //  [this ptr] [hidden return buffer] [generic context] [async continuation] [var arg cookie] [declared arguments]*
        //  (Note: prior to .NET Framework 4.5.1 for Windows 8.1 (but not .NET Framework 4.5.1 "downlevel"),
        //  the "hidden return buffer" came before the "this ptr". Now, the "this ptr" comes first. This
        //  is different from the C++ order, where the "hidden return buffer" always comes first.)
        // 
        // ARM and ARM64 are the same as the current x64 convention:
        //  [this ptr] [hidden return buffer] [generic context] [async continuation] [var arg cookie] [declared arguments]*
        // 
        // Key difference:
        //     The var arg cookie, generic context and async continuations are swapped with respect to the user arguments

        // Set compArgsCount and compLocalsCount
        ref var methodArgs = ref info.compMethodInfo->args;
        info.compArgsCount = methodArgs.numArgs;

        // Is there a 'this' pointer

        if (!info.compIsStatic)
        {
            info.compArgsCount++;
        }
        else
        {
            info.compThisArg = BAD_VAR_NUM;
        }

        info.compILargsCount = info.compArgsCount;

        // Initialize "compRetNativeType" (along with "compRetTypeDesc"):
        //
        //  1. For structs returned via a return buffer, or in multiple registers, make it TYP_STRUCT.
        //  2. For structs returned in a single register, make it the corresponding primitive type.
        //  3. For primitives, leave it as-is. Note this makes it "incorrect" for soft-FP conventions.
        //
        Unsafe.SkipInit(out ReturnTypeDesc retTypeDesc);
        retTypeDesc.InitializeReturnType(this, info.compRetType, methodArgs.retTypeClass, info.compCallConv);

        compRetTypeDesc = retTypeDesc;
        var returnRegCount = retTypeDesc.ReturnRegCount;
        var hasRetBuffArg = false;

        if (returnRegCount > 1)
        {
            info.compRetNativeType = varTypeIsMultiReg(info.compRetType) ? info.compRetType : TYP_STRUCT;
        }
        else if (returnRegCount is 1)
        {
            info.compRetNativeType = retTypeDesc.GetReturnRegType(0);
        }
        else
        {
            hasRetBuffArg = info.compRetType != TYP_VOID;
            info.compRetNativeType = hasRetBuffArg ? TYP_STRUCT : TYP_VOID;
        }

#if DEBUG
        if (verbose)
        {
            var retClass = methodArgs.retTypeClass;
            jitprintf($"{returnRegCount} return registers for return type {info.compRetType.Name} {(varTypeIsStruct(info.compRetType) ? eeGetClassName(retClass) : "")}\n");

            for (byte i = 0; i < returnRegCount; i++)
            {
                var offset = compRetTypeDesc.GetReturnFieldOffset(i);
                var size = compRetTypeDesc.GetReturnRegType(i).Size;
                jitprintf($"  [{offset:D2}..{(offset + size):D2}) reg {compRetTypeDesc.GetAbiReturnReg(i, info.compCallConv)}\n");
            }
        }
#endif

        // Do we have a RetBuffArg?
        if (hasRetBuffArg)
        {
            info.compArgsCount++;
        }
        else
        {
            info.compRetBuffArg = BAD_VAR_NUM;
        }

#if DEBUG && SWIFT_SUPPORT
        if (verbose && (info.compCallConv == CorInfoCallConvExtension.Swift) && varTypeIsStruct(info.compRetType))
        {
            var retTypeHnd = methodArgs.retTypeClass;
            var lowering = GetSwiftLowering(retTypeHnd);

            if (lowering->byReference)
            {
                jitprintf($"Swift compilation returns {typGetObjLayout(retTypeHnd).ClassName} by reference\n");
            }
            else
            {
                jitprintf($"Swift compilation returns {typGetObjLayout(retTypeHnd).ClassName} as {lowering->numLoweredElements} primitive(s) in registers\n");

                for (var i = 0; i < lowering->numLoweredElements; i++)
                {
                    jitprintf($"    [{i}] @ +{lowering->offsets[i]:D2}: {lowering->loweredElements[i].PreciseVarType.Name}\n");
                }
            }
        }
#endif

        // There is a 'hidden' cookie pushed last when the calling convention is varargs

        if (info.compIsVarArgs)
        {
            info.compArgsCount++;
        }

        // Is there an extra parameter used to pass instantiation info to
        // shared generic methods and shared generic struct instance methods?
        if ((methodArgs.callConv & CORINFO_CALLCONV_PARAMTYPE) != 0)
        {
            info.compArgsCount++;
        }
        else
        {
            info.compTypeCtxtArg = BAD_VAR_NUM;
        }

        if (compIsAsync)
        {
            info.compArgsCount++;
        }

#if TARGET_WASM
        if (!opts.IsReversePInvoke)
        {
            // Managed Wasm ABI passes stack pointer as first arg...
            info.compArgsCount += 1;

            if (opts.jitFlags->IsSet(JitFlags.JIT_FLAG_PORTABLE_ENTRY_POINTS))
            {
                // ... and portable entry point as last arg
                info.compArgsCount += 1;
            }
        }
#endif

        ref var methodLocals = ref info.compMethodInfo->locals;
        var localsNumArgs = methodLocals.numArgs;

        lvaCount = info.compArgsCount + localsNumArgs;
        info.compLocalsCount = lvaCount;

        info.compILlocalsCount = info.compILargsCount + localsNumArgs;

        // Now allocate the variable descriptor table

        if (compIsForInlining)
        {
            var inlinerCompiler = impInlineInfo.InlinerCompiler;

            lvaTable = inlinerCompiler.lvaTable;
            lvaCount = inlinerCompiler.lvaCount;

            // No more stuff needs to be done.
            return;
        }

        lvaTable = new LclVarDsc[int.Min(16, lvaCount * 2)];

        for (var i = 0; i < lvaTable.Length; i++)
        {
            lvaTable[i] = new LclVarDsc();
        }

        //-------------------------------------------------------------------------
        // Count the arguments and initialize the respective lvaTable[] entries
        //
        // First the arguments
        //-------------------------------------------------------------------------

        lvaInitArgs(hasRetBuffArg);

        //-------------------------------------------------------------------------
        // Then the local variables
        //-------------------------------------------------------------------------

        var varNum = info.compArgsCount;
        var localsSig = methodLocals.args;

        for (var i = 0; i < localsNumArgs; i++, varNum++, localsSig = info.compCompHnd->getArgNext(localsSig))
        {
            ref var varDsc = ref lvaGetDesc(varNum);

            CORINFO_CLASS_HANDLE typeHnd;
            var corInfoTypeWithMod = info.compCompHnd->getArgType(&info.compMethodInfo->locals, localsSig, &typeHnd);
            var corInfoType = strip(corInfoTypeWithMod);

            lvaInitVarDsc(ref varDsc, varNum, corInfoType, typeHnd, localsSig, in methodLocals);

            if ((corInfoTypeWithMod & CORINFO_TYPE_MOD_PINNED) is not 0)
            {
                if ((corInfoType == CORINFO_TYPE_CLASS) || (corInfoType == CORINFO_TYPE_BYREF))
                {
                    JITDUMP($"Setting lvPinned for V{varNum:D2}\n");
                    varDsc.lvPinned = true;

                    if (opts.IsOSR)
                    {
                        // OSR method may not see any references to the pinned local,
                        // but must still report it in GC info.
                        varDsc.lvImplicitlyReferenced = true;
                    }
                }
                else
                {
                    JITDUMP($"Ignoring pin for non-GC type V{varNum:D2}\n");
                }
            }

            varDsc.lvOnFrame = true; // The final home for this local variable might be our local stack frame

            if (corInfoType == CORINFO_TYPE_CLASS)
            {
                var clsHnd = info.compCompHnd->getArgClass(&info.compMethodInfo->locals, localsSig);
                lvaSetClass(varNum, clsHnd);
            }
        }

        // If there already exist unsafe buffers, don't mark more structs as unsafe
        // as that will cause them to be placed along with the real unsafe buffers,
        // unnecessarily exposing them to overruns. This can affect GS tests which
        // intentionally do buffer-overruns.
        //
        // GS checks require the stack to be re-ordered, which can't be done with EnC
        if (!NeedsGSSecurityCookie && !opts.compDbgEnC && compStressCompile(STRESS_UNSAFE_BUFFER_CHECKS, 25))
        {
            NeedsGSSecurityCookie = true;
            var nowHasCookie = NeedsGSSecurityCookie;

            if (nowHasCookie)
            {
                JITDUMP("Marking some struct locals as unsafe to stress GS checks\n");
                for (uint i = 0; i < lvaCount; i++)
                {
                    ref var lvaDsc = ref lvaTable[i];

                    if ((lvaDsc.Type == TYP_STRUCT) && compStressCompile(STRESS_GENERIC_VARN, 60))
                    {
                        lvaDsc.lvIsUnsafeBuffer = true;
                    }
                }
            }
        }

        // If this is an OSR method, mark all the OSR locals.
        //
        // Do this before we add the GS Cookie Dummy or Outgoing args to the locals
        // so we don't have to do special checks to exclude them.
        //
        if (opts.IsOSR)
        {
            for (var lclNum = 0; lclNum < lvaCount; lclNum++)
            {
                ref var varDsc = ref lvaGetDesc(lclNum);
                varDsc.lvIsOSRLocal = true;

                if (info.compPatchpointInfo->IsExposed(lclNum))
                {
                    JITDUMP($"-- V{lclNum:D2} is OSR exposed\n");
                    varDsc.lvIsOSRExposedLocal = true;

                    // Ensure that ref counts for exposed OSR locals take into account
                    // that some of the refs might be in the Tier0 parts of the method
                    // that get trimmed away.
                    varDsc.lvImplicitlyReferenced = true;
                }
            }
        }

        if (NeedsGSSecurityCookie)
        {
            // Ensure that there will be at least one stack variable since
            // we require that the GSCookie does not have a 0 stack offset.
            var dummy = lvaGrabTempWithImplicitUse(shortLifetime: false, ("GSCookie dummy"));

            ref var gsCookieDummy = ref lvaGetDesc(dummy);
            gsCookieDummy.Type = TYP_INT;
            gsCookieDummy.lvIsTemp = true; // It is not alive at all, set the flag to prevent zero-init.

            lvaSetVarDoNotEnregister(dummy, (DoNotEnregisterReason.VMNeedsStackAddr));
        }

        // Allocate the lvaOutgoingArgSpaceVar now because we can run into problems in the
        // emitter when the varNum is greater that 32767 (see emitLclVarAddr.initLclVarAddr)
        lvaAllocOutgoingArgSpaceVar();

#if TARGET_WASM
        lvaAllocWasmStackPtr();
#endif

#if DEBUG
        if (verbose)
        {
            lvaTableDump(INITIAL_FRAME_LAYOUT);
        }
#endif
    }

    /// <summary>Initialize local var descriptions for incoming user arguments</summary>
    /// <param name="curVarNum">the current local</param>
    /// <param name="skipArgs">the number of user args to skip processing.</param>
    /// <param name="takeArgs">the number of user args to process (after skipping skipArgs number of args)</param>
    public unsafe void lvaInitUserArgs(ref int curVarNum, int skipArgs, int takeArgs)
    {
        //-------------------------------------------------------------------------
        // Walk the function signature for the explicit arguments
        //-------------------------------------------------------------------------

        ref var methodArgs = ref info.compMethodInfo->args;
        var argLst = methodArgs.args;

        var argSigLen = methodArgs.numArgs;

        // We will process at most takeArgs arguments from the signature after skipping skipArgs arguments
        var numUserArgs = long.Min(takeArgs, (argSigLen - skipArgs));

        // If there are no user args or less than skipArgs args, return here since there's no work to do.
        if (numUserArgs <= 0)
        {
            return;
        }

        // Skip skipArgs arguments from the signature.
        for (var i = 0; i < skipArgs; i++)
        {
            argLst = info.compCompHnd->getArgNext(argLst);
        }

        // Process each user arg.
        for (var i = 0; i < numUserArgs; i++)
        {
            ref var varDsc = ref lvaGetDesc(curVarNum);

            CORINFO_CLASS_HANDLE typeHnd;
            var corInfoType = info.compCompHnd->getArgType(&info.compMethodInfo->args, argLst, &typeHnd);
            varDsc.lvIsParam = true;

#if TARGET_X86 && FEATURE_IJW
            if ((corInfoType & CORINFO_TYPE_MOD_COPY_WITH_HELPER) is not 0)
            {
                var typeWithoutMod = strip(corInfoType);

                if (typeWithoutMod is CORINFO_TYPE_VALUECLASS or  CORINFO_TYPE_PTR or CORINFO_TYPE_BYREF)
                {
                    JITDUMP($"Marking user arg{i:D2} as requiring special copy semantics\n");
                    recordArgRequiresSpecialCopy(i);
                }
            }
#endif

            lvaInitVarDsc(ref varDsc, curVarNum, strip(corInfoType), typeHnd, argLst, in methodArgs);

            if (strip(corInfoType) == CORINFO_TYPE_CLASS)
            {
                var clsHnd = info.compCompHnd->getArgClass(&info.compMethodInfo->args, argLst);
                lvaSetClass(curVarNum, clsHnd);
            }

            // The final home for this incoming parameter might be our local stack frame.
            varDsc.lvOnFrame = true;

#if SWIFT_SUPPORT
            if (info.compCallConv == CorInfoCallConvExtension.Swift)
            {
                if (varTypeIsSimd(varDsc.Type))
                {
                    IMPL_LIMITATION("simd types are currently unsupported in Swift reverse pinvokes");
                }

                if (lvaInitSpecialSwiftParam(argLst, curVarNum, strip(corInfoType), typeHnd))
                {
                    continue;
                }

                if (varDsc.Type is TYP_STRUCT)
                {
                    // Struct parameters are lowered to separate primitives in the
                    // Swift calling convention. We cannot handle these patterns
                    // efficiently, so we always DNER them and home them to stack
                    // in the prolog.
                    lvaSetVarDoNotEnregister(curVarNum, (DoNotEnregisterReason.IsStructArg));
                }
            }
#endif

#if CONFIGURABLE_ARM_ABI
            var compUseSoftFP = opts.compUseSoftFP;
#else
            var compUseSoftFP = Options.compUseSoftFP;
#endif

            if (info.compIsVarArgs || (compUseSoftFP && varTypeIsFloating(varDsc.Type)))
            {
#if !TARGET_X86
                // TODO-CQ: We shouldn't have to go as far as to declare these
                // address-exposed -- DoNotEnregister should suffice.
                lvaSetVarAddrExposed(curVarNum, (AddressExposedReason.TOO_CONSERVATIVE));
#endif
            }

            curVarNum++;
            argLst = info.compCompHnd->getArgNext(argLst);
        }
    }

    public void lvaInitVarArgsHandle(ref int curVarNum)
    {
        if (!info.compIsVarArgs)
        {
            return;
        }

        lvaVarargsHandleArg = curVarNum;

        ref var varDsc = ref lvaGetDesc(curVarNum);
        varDsc.Type = TYP_I_IMPL;
        varDsc.lvIsParam = true;
        varDsc.lvOnFrame = true; // The final home for this incoming register might be our local stack frame
        varDsc.lvHasLdAddrOp = true;

#if TARGET_X86
        // Codegen will need it for x86 scope info.
        varDsc.lvImplicitlyReferenced = true;
#endif

        lvaSetVarDoNotEnregister(lvaVarargsHandleArg, (DoNotEnregisterReason.VMNeedsStackAddr));

#if TARGET_X86
        // Allocate a temp to point at the beginning of the args
        lvaVarargsBaseOfStkArgs = lvaGrabTemp(shortLifetime: false, "Varargs BaseOfStkArgs");
        lvaTable[lvaVarargsBaseOfStkArgs].lvType = TYP_I_IMPL;
#endif // TARGET_X86

        curVarNum++;
    }

    public unsafe void lvaInitVarDsc(ref LclVarDsc varDsc, int varNum, CorInfoType corInfoType, CORINFO_CLASS_HANDLE typeHnd, CORINFO_ARG_LIST_HANDLE varList, in CORINFO_SIG_INFO varSig)
    {
        noway_assert(Unsafe.AreSame(in varDsc, in lvaGetDesc(varNum)));

        switch (corInfoType)
        {
            // Mark types that looks like a pointer for doing shadow-copying of
            // parameters if we have an unsafe buffer.
            // Note that this does not handle structs with pointer fields. Instead,
            // we rely on using the assign-groups/equivalence-groups in
            // gsFindVulnerableParams() to determine if a buffer-struct contains a
            // pointer. We could do better by having the EE determine this for us.
            // Note that we want to keep buffers without pointers at lower memory
            // addresses than buffers with pointers.
            case CORINFO_TYPE_PTR:
            case CORINFO_TYPE_BYREF:
            case CORINFO_TYPE_CLASS:
            {
                varDsc.lvIsPtr = true;
                break;
            }

            default:
            {
                break;
            }
        }

        var type = corInfoType.VarType;

        if (varTypeIsFloating(type))
        {
            compFloatingPointUsed = true;
        }

        // Set the lvType (before this point it is TYP_UNDEF).
        if ((varTypeIsStruct(type)))
        {
            lvaSetStruct(varNum, typeHnd, typeHnd != NO_CLASS_HANDLE);
        }
        else
        {
            varDsc.Type = type;
        }

#if DEBUG
        if (varDsc.lvValueSize.IsExact)
        {
            varDsc.StackOffset = BAD_STK_OFFS;
        }
        else
        {
            varDsc.UnknownSizeFrameIndex = BAD_STK_OFFS;
        }
#endif
    }

    /// <summary>Is the local an "implicit byref" parameter?</summary>
    /// <param name="lclNum">The local in question</param>
    /// <returns>Whether "lclNum" refers to an implicit byref.</returns>
    /// <remarks>
    ///   <para>We term structs passed via pointers to shadow copies "implicit byrefs".</para>
    ///   <para>They are used on Windows x64 for structs 3, 5, 6, 7, > 8 bytes in size, and on ARM64/LoongArch64 for structs larger than 16 bytes.</para>
    ///   <para>They are "byrefs" because the VM sometimes uses memory allocated on the GC heap for the shadow copies.</para>
    /// </remarks>
    public bool lvaIsImplicitByRefLocal(int lclNum)
    {
#if FEATURE_IMPLICIT_BYREFS
        ref var varDsc = ref lvaGetDesc(lclNum);

        if (varDsc.IsImplicitByRef)
        {
            assert(varDsc.lvIsParam);
            assert(varTypeIsStruct(varDsc.Type) || (varDsc.Type is TYP_BYREF));
            return true;
        }
#endif
        return false;
    }

    /// <summary>Return the exact width of local variable "varNum" -- the number of bytes you'd need to copy in order to overwrite the value.</summary>
    /// <param name="varNum"></param>
    /// <returns></returns>
    public int lvaLclExactSize(int varNum)
    {
        assert(varNum < lvaCount);
        return lvaGetDesc(varNum).lvExactSize;
    }

    /// <summary>returns size of stack home of a local variable, in bytes</summary>
    /// <param name="varNum">variable to query</param>
    /// <returns>Number of bytes needed on the frame for such a local.</returns>
    public int lvaLclStackHomeSize(int varNum)
    {
        assert(varNum < lvaCount);

        ref var varDsc = ref lvaGetDesc(varNum);
        var varType = varDsc.Type;

        if (!varTypeIsStruct(varType))
        {
#if TARGET_64BIT
            // We only need this Quirk for TARGET_64BIT
            if (varDsc.lvQuirkToLong)
            {
                noway_assert(varDsc.IsAddressExposed);
                return TYP_LONG.StSz * sizeof(int); // return 8  (2 * 4)
            }
#endif

            return varType.StSz * sizeof(int);
        }

        if (varDsc.lvIsParam && !varDsc.lvIsStructField)
        {
            // If this parameter was passed on the stack then we often reuse that
            // space for its home. Take into account that this space might actually
            // not be pointer-sized for some cases (macos-arm64 ABI currently).
            ref readonly var abiInfo = ref lvaGetParameterAbiInfo(varNum);

            if (abiInfo.HasExactlyOneStackSegment)
            {
                return abiInfo.Segments[0].StackSize;
            }

            // There are other cases where the caller has allocated space for the
            // parameter, like windows-x64 with shadow space for register
            // parameters, but in those cases this rounding is fine.
            return roundUp(varDsc.lvExactSize, TARGET_POINTER_SIZE);
        }

#if FEATURE_SIMD && !TARGET_64BIT
        // For 32-bit architectures, we make local variable simd12 types 16 bytes instead of just 12. We can't do
        // this for arguments, which must be passed according the defined ABI. We don't want to do this for
        // dependently promoted struct fields, but we don't know that here. See lvaMapSimd12ToSimd16().
        // (Note that for 64-bits, we are already rounding up to 16.)
        if (varDsc.Type is TYP_SIMD12)
        {
            return 16;
        }
#endif

        return roundUp(varDsc.lvExactSize, TARGET_POINTER_SIZE);
    }

    // TODO: Port Compiler.lvaMarkLocalVars
    public PhaseStatus lvaMarkLocalVars()
    {
        lvaRefCountState = RCS_NORMAL;
        return PhaseStatus.MODIFIED_NOTHING;
    }

    /// <summary>set class information for a local var.</summary>
    /// <param name="varNum">number of the variable</param>
    /// <param name="clsHnd">class handle to use in set or update</param>
    /// <param name="isExact">true if class is known exactly</param>
    /// <remarks>varNum must not already have a ref class handle.</remarks>
    public unsafe void lvaSetClass(int varNum, CORINFO_CLASS_HANDLE clsHnd, bool isExact = false)
    {
        noway_assert(varNum < lvaCount);

        if ((clsHnd != NO_CLASS_HANDLE) && !isExact && (JitConfig[ConfigInteger.JitEnableExactDevirtualization] != 0))
        {
            CORINFO_CLASS_HANDLE exactClass;
            if (info.compCompHnd->getExactClasses(clsHnd, 1, &exactClass) == 1)
            {
                isExact = true;
                clsHnd = exactClass;
            }
        }

        // Else we should have a type handle.
        assert(clsHnd is not null);

        ref var varDsc = ref lvaGetDesc(varNum);
        assert(varDsc.Type == TYP_REF);

        // We should not have any ref type information for this var.
        assert(varDsc.lvClassHnd == NO_CLASS_HANDLE);
        assert(!varDsc.lvClassIsExact);

        JITDUMP($"\nlvaSetClass: setting class for V{varNum:D2} to ({dspPtr(clsHnd):X}) {eeGetClassName(clsHnd)} {(isExact ? " [exact]" : "")}\n");

        varDsc.lvClassHnd = clsHnd;
        varDsc.lvClassIsExact = isExact;
    }

    /// <summary>set class information for a local var from a tree or stack type</summary>
    /// <param name="varNum">number of the variable. Must be a single def local</param>
    /// <param name="tree">tree establishing the variable's value</param>
    /// <param name="stackHandle">handle for the type from the evaluation stack</param>
    /// <remarks>
    ///   <para>If there is no stack type, then the class is set to object.</para>
    ///   <para>Since not all tree kinds can track ref types, the stack type is used as a fallback.</para>
    ///   <para>Preferentially uses the tree's type, when available.</para>
    /// </remarks>
    public unsafe void lvaSetClass(int varNum, GenTree tree, CORINFO_CLASS_HANDLE stackHandle = null)
    {
        var clsHnd = gtGetClassHandle(tree, out var isExact, out _);

        if (clsHnd is not null)
        {
            lvaSetClass(varNum, clsHnd, isExact);
        }
        else if (stackHandle is not null)
        {
            lvaSetClass(varNum, stackHandle);
        }
        else
        {
            lvaSetClass(varNum, impObjectClass);
        }
    }

    /// <summary>Set the type of a local to a struct, given a layout.</summary>
    /// <param name="varNum">The local</param>
    /// <param name="layout">The layout</param>
    /// <param name="unsafeValueClsCheck">Whether to check if we should potentially emit a GS cookie due to this local.</param>
    public unsafe void lvaSetStruct(int varNum, ClassLayout layout, bool unsafeValueClsCheck)
    {
        ref var varDsc = ref lvaGetDesc(varNum);

        // Set the type and associated info if we haven't already set it.
        if (varDsc.Type == TYP_UNDEF)
        {
            varDsc.Type = TYP_STRUCT;
        }

        if (varDsc.Layout is null)
        {
            varDsc.Layout = layout;

            if (layout.IsValueClass)
            {
                varDsc.Type = layout.Type;
            }
        }
        else
        {
            assert(ClassLayout.AreCompatible(varDsc.Layout, layout));

            // Inlining could replace a canon struct type with an exact one.
            varDsc.Layout = layout;

            assert(layout.IsCustomLayout || (layout.Size != 0));
        }

        if (!layout.IsCustomLayout)
        {
#if !TARGET_64BIT


#if TARGET_X86
            var fDoubleAlignHint = true;
#else
            var fDoubleAlignHint = false;
#endif

            if (info.compCompHnd->getClassAlignmentRequirement(layout.ClassHandle, fDoubleAlignHint) == 8)
            {
#if DEBUG
                if (verbose)
                {
                    jitprintf($"Marking struct in V{varNum:D2} with double align flag\n");
                }
#endif
                varDsc.lvStructDoubleAlign = 1;
            }
#endif

            varDsc.IsSpan = isSpanClass(layout.ClassHandle);

            // Check whether this local is an unsafe value type and requires GS cookie protection.
            if (unsafeValueClsCheck)
            {
                var classAttribs = info.compCompHnd->getClassAttribs(layout.ClassHandle);

                if ((classAttribs & CORINFO_FLG_UNSAFE_VALUECLASS) != 0)
                {
                    NeedsGSSecurityCookie = true;
                    varDsc.lvIsUnsafeBuffer = true;
                }
            }

#if DEBUG
            if (JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] != 0)
            {
                makeExtraStructQueries(layout.ClassHandle, 2);
            }
#endif
        }
    }

    /// <summary>Set the type of a local to a struct, given its type handle.</summary>
    /// <param name="varNum">The local</param>
    /// <param name="typeHnd">The type handle</param>
    /// <param name="unsafeValueClsCheck">Whether to check if we should potentially emit a GS cookie due to this local.</param>
    public unsafe void lvaSetStruct(int varNum, CORINFO_CLASS_HANDLE typeHnd, bool unsafeValueClsCheck)
        => lvaSetStruct(varNum, typGetObjLayout(typeHnd), unsafeValueClsCheck);

    /// <summary>Set the local var "varNum" as address-exposed.</summary>
    /// <param name="varNum"></param>
    /// <param name="reason"></param>
    /// <remarks>If this is a promoted struct, label it's fields the same way.</remarks>
    public void lvaSetVarAddrExposed(int varNum, AddressExposedReason reason)
    {
        ref var varDsc = ref lvaGetDesc(varNum);
        assert(!varDsc.lvIsStructField);

        varDsc.SetAddressExposed(true, reason);

        if (varDsc.lvPromoted)
        {
            noway_assert(varTypeIsStruct(varDsc.Type));

            for (var i = varDsc.lvFieldLclStart; i < varDsc.lvFieldLclStart + varDsc.lvFieldCnt; ++i)
            {
                noway_assert(lvaTable[i].lvIsStructField);
                lvaTable[i].SetAddressExposed(true, AddressExposedReason.PARENT_EXPOSED);
                lvaSetVarDoNotEnregister(i, DoNotEnregisterReason.AddrExposed);
            }
        }

        lvaSetVarDoNotEnregister(varNum, DoNotEnregisterReason.AddrExposed);
    }

    /// <summary>Record that the local var "varNum" should not be enregistered (for one of several reasons.)</summary>
    /// <param name="varNum"></param>
    /// <param name="reason"></param>
    public void lvaSetVarDoNotEnregister(int varNum, DoNotEnregisterReason reason)
    {
        ref var varDsc = ref lvaGetDesc(varNum);

        var wasAlreadyMarkedDoNotEnreg = varDsc.lvDoNotEnregister;
        varDsc.lvDoNotEnregister = true;

#if DEBUG
        if (!wasAlreadyMarkedDoNotEnreg)
        {
            varDsc.DoNotEnregisterReason = reason;
        }

        if (verbose)
        {
            jitprintf($"\nLocal V{varNum:D2} should not be enregistered because: ");
        }

        switch (reason)
        {
            case DoNotEnregisterReason.AddrExposed:
            {
                JITDUMP("it is address exposed\n");
                assert(varDsc.IsAddressExposed);
                break;
            }

            case DoNotEnregisterReason.HiddenBufferStructArg:
            {
                JITDUMP("it is hidden buffer struct arg\n");
                break;
            }

            case DoNotEnregisterReason.DontEnregStructs:
            {
                JITDUMP("struct enregistration is disabled\n");
                assert(varTypeIsStruct(varDsc.Type));
                break;
            }

            case DoNotEnregisterReason.NotRegSizeStruct:
            {
                JITDUMP("struct size does not match reg size\n");
                assert(varTypeIsStruct(varDsc.Type));
                break;
            }

            case DoNotEnregisterReason.LocalField:
            {
                JITDUMP("was accessed as a local field\n");
                break;
            }

            case DoNotEnregisterReason.WasmGCVisibility:
            {
                JITDUMP("Wasm GC needs to see it\n");
                break;
            }

            case DoNotEnregisterReason.VMNeedsStackAddr:
            {
                JITDUMP("VM needs stack addr\n");
                break;
            }

            case DoNotEnregisterReason.LiveInOutOfHandler:
            {
                JITDUMP("live in/out of a handler\n");
                break;
            }

            case DoNotEnregisterReason.BlockOp:
            {
                JITDUMP("written/read in a block op\n");
                break;
            }

            case DoNotEnregisterReason.IsStructArg:
            {
                if (varTypeIsStruct(varDsc.Type))
                {
                    JITDUMP("it is a struct arg\n");
                }
                else
                {
                    JITDUMP("it is reinterpreted as a struct arg\n");
                }
                break;
            }

            case DoNotEnregisterReason.DepField:
            {
                JITDUMP("field of a dependently promoted struct\n");
                assert(varDsc.lvIsStructField && (lvaGetParentPromotionType(varNum) != PROMOTION_TYPE_INDEPENDENT));
                break;
            }

            case DoNotEnregisterReason.NoRegVars:
            {
                JITDUMP("opts.compFlags & CLFLG_REGVAR is not set\n");
                assert(!compEnregLocals);
                break;
            }

#if !TARGET_64BIT
            case DoNotEnregisterReason.LongParamField:
            {
                JITDUMP("it is a decomposed field of a long parameter\n");
                break;
            }
#endif

            case DoNotEnregisterReason.PinningRef:
            {
                JITDUMP("pinning ref\n");
                assert(varDsc.lvPinned);
                break;
            }

            case DoNotEnregisterReason.LclAddrNode:
            {
                JITDUMP("LclAddrVar/Fld takes the address of this node\n");
                break;
            }

            case DoNotEnregisterReason.CastTakesAddr:
            {
                JITDUMP("cast takes addr\n");
                break;
            }

            case DoNotEnregisterReason.StoreBlkSrc:
            {
                JITDUMP("the local is used as store block src\n");
                break;
            }

            case DoNotEnregisterReason.SwizzleArg:
            {
                JITDUMP("SwizzleArg\n");
                break;
            }

            case DoNotEnregisterReason.BlockOpRet:
            {
                JITDUMP("return uses a block op\n");
                break;
            }

            case DoNotEnregisterReason.ReturnSpCheck:
            {
                JITDUMP("Used for SP check on return\n");
                break;
            }

            case DoNotEnregisterReason.CallSpCheck:
            {
                JITDUMP("Used for SP check on call\n");
                break;
            }

            case DoNotEnregisterReason.simdUserForcesDep:
            {
                JITDUMP("Promoted struct used by a simd/HWI node\n");
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif

        if (varDsc.lvPromoted && !wasAlreadyMarkedDoNotEnreg)
        {
            for (var i = 0; i < varDsc.lvFieldCnt; i++)
            {
                var fieldLclNum = varDsc.lvFieldLclStart + i;
                lvaSetVarDoNotEnregister(fieldLclNum, DoNotEnregisterReason.DepField);
            }
        }
    }

#if DEBUG
    /// <summary>update class information for a local var.</summary>
    /// <param name="varNum">number of the variable</param>
    /// <param name="clsHnd">class handle to use in set or update</param>
    /// <param name="isExact">true if class is known exactly</param>
    /// <param name="singleDefOnly">true if we should only update single-def locals</param>
    /// <remarks>
    ///   <para>This method models the type update rule for a store.</para>
    ///   <para>Updates currently should only happen for single-def user args or locals, when we are processing the expression actually being used to initialize the local (or inlined arg). The update will change the local from the declared type to the type of the initial value.</para>
    ///   <para>These updates should always *improve* what we know about the type, that is making an inexact type exact, or changing a type to some subtype. However the jit lacks precise type information for shared code, so ensuring this is so is currently not possible.</para>
    /// </remarks>
    public unsafe void lvaUpdateClass(int varNum, CORINFO_CLASS_HANDLE clsHnd, bool isExact = false, bool singleDefOnly = true)
    {
        assert(varNum < lvaCount);

        // Else we should have a class handle to consider
        assert(clsHnd is not null);

        ref var varDsc = ref lvaGetDesc(varNum);
        assert(varDsc.Type == TYP_REF);

        // We should already have a class
        assert(varDsc.lvClassHnd != NO_CLASS_HANDLE);

        // We should only be updating classes for single-def locals if requested
        if (singleDefOnly && !varDsc.lvSingleDef)
        {
            NO_WAY("Updating class for multi-def local");
        }
        else
        {
            // Now see if we should update.
            //
            // New information may not always be "better" so do some
            // simple analysis to decide if the update is worthwhile.
            var isNewClass = clsHnd != varDsc.lvClassHnd;
            var shouldUpdate = false;

            // Are we attempting to update the class? Only check this when we have
            // an new type and the existing class is inexact... we should not be
            // updating exact classes.
            if (!varDsc.lvClassIsExact && isNewClass)
            {
                shouldUpdate = info.compCompHnd->isMoreSpecificType(varDsc.lvClassHnd, clsHnd);
            }
            else if (isExact && !varDsc.lvClassIsExact && !isNewClass)
            {
                // Else are we attempting to update exactness?
                shouldUpdate = true;
            }

#if DEBUG
            if (isNewClass || (isExact != varDsc.lvClassIsExact))
            {
                JITDUMP($"\nlvaUpdateClass:{(shouldUpdate ? "" : " NOT")} Updating class for V{varNum:D2}");
                JITDUMP($" from ({dspPtr(varDsc.lvClassHnd)}) {eeGetClassName(varDsc.lvClassHnd)}{(varDsc.lvClassIsExact ? " [exact]" : "")}");
                JITDUMP($" to ({dspPtr(clsHnd)}) {eeGetClassName(clsHnd)}{(isExact ? " [exact]" : "")}\n");
            }
#endif

            if (shouldUpdate)
            {
                varDsc.lvClassHnd = clsHnd;
                varDsc.lvClassIsExact = isExact;

#if DEBUG
                // Note we've modified the type...
                varDsc.lvClassInfoUpdated = true;
#endif
            }
        }
    }

    /// <summary>Update class information for a local var from a tree or stack type</summary>
    /// <param name="varNum">number of the variable. Must be a single def local</param>
    /// <param name="tree">tree establishing the variable's value</param>
    /// <param name="stackHandle">handle for the type from the evaluation stack</param>
    /// <remarks>Preferentially uses the tree's type, when available. Since not all tree kinds can track ref types, the stack type is used as a fallback.</remarks>
    public unsafe void lvaUpdateClass(int varNum, GenTree tree, CORINFO_CLASS_HANDLE stackHandle = null)
    {
        var clsHnd = gtGetClassHandle(tree, out var isExact, out _);

        if (clsHnd is not null)
        {
            lvaUpdateClass(varNum, clsHnd, isExact);
        }
        else if (stackHandle is not null)
        {
            lvaUpdateClass(varNum, stackHandle);
        }
    }
#endif

    public unsafe bool lvaIsOriginalThisArg(int varNum)
    {
        assert(varNum < lvaCount);

        var isOriginalThisArg = (varNum == info.compThisArg) && !info.compIsStatic;

#if DEBUG
        if (isOriginalThisArg)
        {
            ref var varDsc = ref lvaGetDesc(varNum);
            // Should never write to or take the address of the original 'this' arg

#if !JIT32_GCENCODER
            // With the general encoder/decoder, when the original 'this' arg is needed as a generics context param, we
            // copy to a new local, and mark the original as DoNotEnregister, to
            // ensure that it is stack-allocated.  It should not be the case that the original one can be modified -- it
            // should not be written to, or address-exposed.
            assert(!varDsc.lvHasILStoreOp && (!varDsc.IsAddressExposed ||
                                               ((info.compMethodInfo->options & CORINFO_GENERICS_CTXT_FROM_THIS) != 0)));
#else
            assert(!varDsc.lvHasILStoreOp && !varDsc.IsAddressExposed);
#endif
        }
#endif

        return isOriginalThisArg;
    }

    /// <summary>check if this local var is one that requires special treatment for OSR compilations.</summary>
    /// <param name="varNum">variable of interest</param>
    /// <returns>true if this is an OSR compile and this local requires special treatment; otherwise false if not an OSR compile, or not an interesting local for OSR</returns>
    public bool lvaIsOSRLocal(int varNum)
    {
        ref var varDsc = ref lvaGetDesc(varNum);

#if DEBUG
        if (opts.IsOSR)
        {
            if (varDsc.lvIsOSRLocal)
            {
                // Sanity check for promoted fields of OSR locals.
                //
                if ((varNum >= info.compLocalsCount) && (varNum != lvaMonAcquired) && (varNum != lvaAsyncThreadObjectVar) &&
                    (varNum != lvaAsyncExecutionContextVar) && (varNum != lvaAsyncSynchronizationContextVar))
                {
                    assert(varDsc.lvIsStructField);
                    assert(varDsc.lvParentLcl < info.compLocalsCount);
                }
            }
        }
        else
        {
            assert(!varDsc.lvIsOSRLocal);
        }
#endif

        return varDsc.lvIsOSRLocal;
    }

#if DEBUG
    public void lvaStressLclFld()
    {
        // TODO: Port Compiler.lvaStressLclFld
    }

    /// <summary>Query the information for the given struct handle.</summary>
    /// <param name="structHandle">The handle for the struct type we're querying.</param>
    /// <param name="level">How many more levels to recurse.</param>
    public unsafe void makeExtraStructQueries(CORINFO_CLASS_HANDLE structHandle, int level)
    {
        if (level <= 0)
        {
            return;
        }

        assert(structHandle != NO_CLASS_HANDLE);
        _ = typGetObjLayout(structHandle);

        var typeFlags = info.compCompHnd->getClassAttribs(structHandle);

        var fieldCnt = info.compCompHnd->getClassNumInstanceFields(structHandle);
        impNormStructType(structHandle);

#if TARGET_ARMARCH
        GetHfaType(structHandle);
#endif

        QueryLayout(this, structHandle);

        // Bypass fetching instance fields of ref classes for now,
        // as it requires traversing the class hierarchy.
        //
        if ((typeFlags & CORINFO_FLG_VALUECLASS) == 0)
        {
            return;
        }

        // In R2R we cannot query arbitrary information about struct fields, so
        // skip it there. Note that the getTypeLayout call above is enough to cover
        // us for promotion at least.
        if (!IsAot)
        {
            for (var i = 0; i < fieldCnt; i++)
            {
                var fieldHandle = info.compCompHnd->getFieldInClass(structHandle, i);
                var fldOffset = info.compCompHnd->getFieldOffset(fieldHandle);
                var fieldClassHandle = NO_CLASS_HANDLE;
                var fieldCorType = info.compCompHnd->getFieldType(fieldHandle, &fieldClassHandle);
                var fieldVarType = fieldCorType.VarType;

                if (fieldClassHandle != NO_CLASS_HANDLE)
                {
                    if (varTypeIsStruct(fieldVarType))
                    {
                        makeExtraStructQueries(fieldClassHandle, level - 1);
                    }
                }
            }
        }

        // In a lambda since this requires a lot of stack and this function is recursive.
        static void QueryLayout(Compiler compiler, CORINFO_CLASS_HANDLE structHandle)
        {
            const int NumNodes = 256;
            var nodes = stackalloc CORINFO_TYPE_LAYOUT_NODE[NumNodes];

            var numNodes = (nint)(NumNodes);
            _ = compiler.info.compCompHnd->getTypeLayout(structHandle, nodes, &numNodes);
        }
    }
#endif

#if FEATURE_SIMD
    /// <summary>Set the flag that indicates that the lclVar referenced by this tree is used in a simd intrinsic.</summary>
    /// <param name="tree"></param>
    public void setLclRelatedToSimdIntrinsic(GenTreeLclVarCommon tree)
    {
        ref var lclVarDsc = ref lvaGetDesc(tree.LclNum);
        lclVarDsc.lvUsedInSimdIntrinsic = true;
    }

    /// <summary>Determine if the tree has a local var that needs to be set as used by a simd intrinsic, and if so, set that local var appropriately.</summary>
    /// <param name="op">The tree, to be an operand of a new simd-related node, to check.</param>
    public void SetOpLclRelatedToSimdIntrinsic(GenTree op)
    {
        if (op.Oper.IsScalarLocal)
        {
            setLclRelatedToSimdIntrinsic(op.AsLclVarCommon());
        }
    }
#endif
}
