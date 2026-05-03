// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Current local ref count state</summary>
    public RefCountState lvaRefCountState;

    /// <summary>true if we cannot add new tracked variables; otherwise, false</summary>
    public bool lvaTrackedFixed;

    /// <summary>total number of locals, which includes function arguments, special arguments, IL local variables, and JIT temporary variables</summary>
    public uint lvaCount;

    /// <summary>variable descriptor table</summary>
    public LclVarDsc[] lvaTable = [];

    public AbiPassingInformation? lvaParameterPassingInfo;

    public uint lvaParameterStackSize;

    /// <summary>actual # of locals being tracked</summary>
    public uint lvaTrackedCount;

    /// <summary>min # of size_t's sufficient to hold a bit for all the locals being tracked</summary>
    public uint lvaTrackedCountInSizeTUnits;

#if DEBUG
    /// <summary>set of tracked variables</summary>
    public VARSET_TP lvaTrackedVars;
#endif

#if !TARGET_64BIT
    /// <summary>set of long (64-bit) variables</summary>
    public VARSET_TP lvaLongVars;
#endif
    /// <summary>set of floating-point (32-bit and 64-bit) or SIMD variables</summary>
    public VARSET_TP lvaFloatVars;

#if FEATURE_MASKED_HW_INTRINSICS
    /// <summary>set of mask variables</summary>
    public VARSET_TP lvaMaskVars;
#endif

    /// <summary>VarSets are relative to a specific set of tracked var indices. If that changes, this changes.</summary>
    /// <remarks>VarSets from different epochs cannot be meaningfully combined.</remarks>
    public uint lvaCurEpoch;

    /// <summary>reverse map of tracked number to var number</summary>
    public uint[]? lvaTrackedToVarNum;

#if DEBUG && DOUBLE_ALIGN
    /// <summary># of procs compiled a with double-aligned stack</summary>
    public static uint s_lvaDoubleAlignedProcsCount;
#endif

    public bool lvaEnregEHVars;

    public bool lvaEnregMultiRegVars;

    public uint lvaVarargsHandleArg = BAD_VAR_NUM;

#if TARGET_X86
    /// <summary>Pointer (computed based on incoming varargs handle) to the start of the stack arguments</summary>
    public uint lvaVarargsBaseOfStkArgs = BAD_VAR_NUM;
#endif

#if TARGET_WASM
    /// <summary>lcl var index of Wasm stack pointer arg</summary>
    public uint lvaWasmSpArg = BAD_VAR_NUM;
#endif

    /// <summary>variable representing the InlinedCallFrame</summary>
    public uint lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;

    /// <summary>variable representing the reverse PInvoke frame</summary>
    public uint lvaReversePInvokeFrameVar = BAD_VAR_NUM;

    /// <summary>boolean variable introduced into in synchronized methods that tracks whether the lock has been taken</summary>
    public uint lvaMonAcquired = BAD_VAR_NUM;

    /// <summary>ExecutionContext local for async methods</summary>
    public uint lvaAsyncExecutionContextVar = BAD_VAR_NUM;

    /// <summary>SynchronizationContext local for async methods</summary>
    public uint lvaAsyncSynchronizationContextVar = BAD_VAR_NUM;
    /// <summary>The lclNum of arg0. Normally this will be info.compThisArg.</summary>
    /// <remarks>However, if there is a "ldarga 0" or "starg 0" in the IL, we will redirect all "ldarg(a) 0" and "starg 0" to this temp.</remarks>
    public uint lvaArg0Var = BAD_VAR_NUM;

    /// <summary>The temp to spill the non-VOID return expression in case there are multiple BBJ_RETURN blocks in the inlinee or if the inlinee has GC ref locals.</summary>
    public uint lvaInlineeReturnSpillTemp = BAD_VAR_NUM;

    /// <summary>True if the temp was freshly created for the inlinee return</summary>
    public bool lvaInlineeReturnSpillTempFreshlyCreated;

    /// <summary>Local number of argument passed as WellKnownArg::InstParam to next call</summary>
    public uint lvaNextCallGenericContext = BAD_VAR_NUM;

    /// <summary>Local number of argument passed as WellKnownArg::AsyncContinuation to next call</summary>
    public uint lvaNextCallAsyncContinuation = BAD_VAR_NUM;

#if FEATURE_FIXED_OUT_ARGS
    /// <summary>Var that represents outgoing argument space</summary>
    public uint lvaOutgoingArgSpaceVar = BAD_VAR_NUM;

    /// <summary>Size of fixed outgoing argument space</summary>
    public PhasedVar<uint> lvaOutgoingArgSpaceSize;
#endif

    /// <summary>Variable representing the return address.</summary>
    /// <remarks>The helper-based tailcall mechanism passes the address of the return address to a runtime helper where it is used to detect tail-call chains.</remarks>
    public uint lvaRetAddrVar = BAD_VAR_NUM;

#if SWIFT_SUPPORT
    public uint lvaSwiftSelfArg = BAD_VAR_NUM;

    public uint lvaSwiftIndirectResultArg = BAD_VAR_NUM;

    public uint lvaSwiftErrorArg = BAD_VAR_NUM;

    public uint lvaSwiftErrorLocal;
#endif

    /// <summary>Variable representing async continuation argument passed.</summary>
    public uint lvaAsyncContinuationArg = BAD_VAR_NUM;

#if DEBUG && TARGET_XARCH
    /// <summary>Stores SP to confirm it is not corrupted on return.</summary>
    public uint lvaReturnSpCheck = BAD_VAR_NUM;
#endif

#if DEBUG && TARGET_X86
    /// <summary>Stores SP to confirm it is not corrupted after every call.</summary>
    public uint lvaCallSpCheck = BAD_VAR_NUM;
#endif

    public bool lvaGenericsContextInUse;

    public int lvaCachedGenericContextArgOffs;

#if JIT32_GCENCODER
    /// <summary>variable which stores the value of ESP after the last alloca/localloc</summary>
    public uint lvaLocAllocSPvar = BAD_VAR_NUM;
#endif

    /// <summary>Variable with arguments for new MD array helper</summary>
    public uint lvaNewObjArrayArgs = BAD_VAR_NUM;

#if DEBUG
    public static unsafe fgWalkPreFn lvaStressLclFldCB;
#endif

    /// <summary>LclVar number</summary>
    public uint lvaGSSecurityCookie = BAD_VAR_NUM;

#if TARGET_ARM64
    /// <summary>LclVar number</summary>
    public uint lvaFfrRegister = BAD_VAR_NUM;
#endif

    /// <summary>Variable representing the secret stub argument</summary>
    public uint lvaStubArgumentVar = BAD_VAR_NUM;

#if FEATURE_SIMD
    /// <summary>This is a temp lclVar allocated on the stack as TYP_SIMD.</summary>
    /// <remarks>
    ///   <para>It is used to implement intrinsics that require indexed access to the individual fields of the vector, which is not well supported by the hardware.</para>
    ///   <para>It is allocated when/if such situations are encountered during Lowering.</para> 
    /// </remarks>
    public uint lvaSIMDInitTempVarNum = BAD_VAR_NUM;
#endif

    /// <summary>The highest frame layout state that we've completed.</summary>
    /// <remarks>During frame layout calculations, this is the level we are currently computing.</remarks>
    public FrameLayoutState lvaDoneFrameLayout;

    /// <summary>return true if there is no place in the code that writes to arg0</summary>
    public bool lvaIsOriginalThisReadOnly => lvaArg0Var == info.compThisArg;

    public ref LclVarDsc lvaGetDesc(uint lclNum)
    {
        assert(lclNum < lvaCount);
        return ref lvaTable[lclNum];
    }

    public uint lvaGrabTemp(bool shortLifetime, string reason)
    {
        // TODO: Port Compiler.lvaGrabTemp
        return 0;
    }

    public void lvaInitTypeRef()
    {
        // TODO: Port Compiler.lvaInitTypeRef
    }
}
