// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

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

    public AbiPassingInformation? lvaParameterPassingInfo;

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

    /// <summary>set of floating-point (32-bit and 64-bit) or SIMD variables</summary>
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
    public static unsafe fgWalkPreFn lvaStressLclFldCB;
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
    /// <summary>This is a temp lclVar allocated on the stack as TYP_SIMD.</summary>
    /// <remarks>
    ///   <para>It is used to implement intrinsics that require indexed access to the individual fields of the vector, which is not well supported by the hardware.</para>
    ///   <para>It is allocated when/if such situations are encountered during Lowering.</para> 
    /// </remarks>
    public int lvaSIMDInitTempVarNum = BAD_VAR_NUM;
#endif

    /// <summary>The highest frame layout state that we've completed.</summary>
    /// <remarks>During frame layout calculations, this is the level we are currently computing.</remarks>
    public FrameLayoutState lvaDoneFrameLayout;

    /// <summary>return true if there is no place in the code that writes to arg0</summary>
    public bool lvaIsOriginalThisReadOnly => lvaArg0Var == info.compThisArg;

    public bool lvaLocalVarRefCounted => lvaRefCountState == RCS_NORMAL;

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
        // TODO: Port Compiler.lvaGrabTemp
        return 0;
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

    public void lvaInitTypeRef()
    {
        // TODO: Port Compiler.lvaInitTypeRef
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

        JITDUMP($"\nlvaSetClass: setting class for V{varNum:D2} to ({dspPtr(clsHnd)}) {eeGetClassName(clsHnd)} {(isExact ? " [exact]" : "")}\n");

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

            case DoNotEnregisterReason.VMNeedsStackAddr:
            {
                JITDUMP("VM needs stack addr\n");
                break;
            }

            case DoNotEnregisterReason.LiveInOutOfHandler:
            {
                JITDUMP("live in/out of a handler\n");
                varDsc.lvLiveInOutOfHndlr = true;
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

#if JIT32_GCENCODER
            case DoNotEnregisterReason.PinningRef:
            {
                JITDUMP("pinning ref\n");
                assert(varDsc->lvPinned);
                break;
            }
#endif

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

            case DoNotEnregisterReason.SimdUserForcesDep:
            {
                JITDUMP("Promoted struct used by a SIMD/HWI node\n");
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif
    }

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
                if ((varNum >= info.compLocalsCount) && (varNum != lvaMonAcquired) &&
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
            var numNodes = (nint)(256);
            var nodes = stackalloc CORINFO_TYPE_LAYOUT_NODE[(int)(numNodes)];
            _ = compiler.info.compCompHnd->getTypeLayout(structHandle, nodes, &numNodes);
        }
    }
#endif
}
