// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed class GenTreeCall : GenTree
{
    internal CallArgs _args;

#if DEBUG || TARGET_WASM
    // Used to register callsites with the EE
    internal CORINFO_SIG_INFO _callSig;
#endif

    internal _Anonymous1_e__Union _anonymous1;

    /// <summary>Used for explicit tail prefixed calls</summary>
    internal ref TailCallSiteInfo _tailCallInfo => ref _anonymous1.TailCallInfo;

    /// <summary>Used for async calls</summary>
    internal ref AsyncCallInfo _asyncInfo => ref _anonymous1.AsyncInfo;

    /// <summary>Only used for unmanaged calls, which cannot be tail-called</summary>
    internal ref CorInfoCallConvExtension _unmgdCallConv => ref _anonymous1.UnmgdCallConv;

#if FEATURE_MULTIREG_RET
    // TODO-AllArch: enable for all call nodes to unify single-reg and multi-reg returns.
    internal ReturnTypeDesc _returnTypeDesc;

    // RegNum would always be the first return reg.
    // The following array holds the other reg numbers of multi-reg return.
    private regNumber _otherRegs[MAX_RET_REG_COUNT - 1];

    private MultiRegSpillFlags _spillFlags;
#endif

    // in addition to gtFlags
    internal GenTreeCallFlags _callMoreFlags;

    internal byte _bitfield;

    // value from the gtCallTypes enumeration
    internal gtCallTypes _callType
    {
        get
        {
            return (gtCallTypes)(_bitfield & 0x07);
        }

        set
        {
            _bitfield = (byte)((_bitfield & ~0x07) | ((byte)(value) & 0x07));
        }
    }

    // exact return type
    internal var_types _returnType
    {
        get
        {
            return (var_types)((_bitfield >>> 3) & 0x1F);
        }

        set
        {
            _bitfield = (byte)((_bitfield & ~(0x1F << 3)) | ((byte)(value) & 0x1F) << 3);
        }
    }

    /// <summary>number of inline candidates for the given call</summary>
    internal byte _inlineInfoCount;

    internal unsafe CORINFO_CLASS_HANDLE _retClsHnd;

    internal unsafe void* _anonymous2;

    /// <summary>GTF_CALL_VIRT_STUB - these are never inlined</summary>
    internal unsafe void* StubCallStubAddr
    {
        get
        {
            return _anonymous2;
        }

        set
        {
            _anonymous2 = value;
        }
    }

    /// <summary>Used by static init helpers, represents a class they init</summary>
    internal unsafe CORINFO_CLASS_HANDLE _initClsHnd
    {
        get
        {
            return (CORINFO_CLASS_HANDLE)(_anonymous2);
        }

        set
        {
            _anonymous2 = value;
        }
    }

    /// <summary>Used by cast helpers to save corresponding IL offset</summary>\
    internal unsafe IL_OFFSET _castHelperILOffset
    {
        get
        {
            return unchecked((IL_OFFSET)(_anonymous2));
        }

        set
        {
            _anonymous2 = unchecked((void*)(value));
        }
    }

    internal object? _anonymous3;

    // Only used when inlining methods
    private InlineCandidateInfo? _inlineCandidateInfo
    {
        get
        {
            return _anonymous3 as InlineCandidateInfo;
        }

        set
        {
            _anonymous3 = value;
        }
    }

    // Used when we have more than one GDV candidate
    private List<InlineCandidateInfo>? _inlineCandidateInfoList
    {
        get
        {
            return _anonymous3 as List<InlineCandidateInfo>;
        }

        set
        {
            _anonymous3 = value;
        }
    }

    internal HandleHistogramProfileCandidateInfo? _handleHistogramProfileCandidateInfo
    {
        get
        {
            return _anonymous3 as HandleHistogramProfileCandidateInfo;
        }

        set
        {
            _anonymous3 = value;
        }
    }

    internal unsafe void* _anonymous4;

    /// <summary>Used to track type handle argument of dynamic helpers</summary>
    internal unsafe CORINFO_GENERIC_HANDLE _compileTimeHelperArgumentHandle
    {
        get
        {
            return (CORINFO_GENERIC_HANDLE) (_anonymous4);
        }

        set
        {
            _anonymous4 = value;
        }
    }

    /// <summary>Used to pass direct call address between lower and codegen</summary>
    private unsafe void* _directCallAddress
    {
        get
        {
            return _anonymous4;
        }

        set
        {
            _anonymous4 = value;
        }
    }

    /// <summary>The serialized CALLI unmanaged call (CT_INDIRECT) cookie; reified into argument IR in morph</summary>
    internal CORINFO_CONST_LOOKUP _callCookie;

    // Always available for user virtual calls
    internal LateDevirtualizationInfo? _lateDevirtualizationInfo;

    /// <summary>expression evaluated after args are placed which determines the control target</summary>
    /// <remarks>Applicable to any call type</remarks>
    internal GenTree? _controlExpr;

    // CT_USER_FUNC or CT_HELPER
    internal unsafe CORINFO_METHOD_HANDLE _callMethHnd;

#if FEATURE_READYTORUN
    /// <summary>Call target lookup info for method call from a Ready To Run module</summary>
    internal CORINFO_CONST_LOOKUP _entryPoint;
#endif

#if DEBUG
    internal GenTreeCallDebugFlags _callDebugFlags;

    /// <summary>For non-inline candidates, track the first observation that blocks candidacy.</summary>
    internal InlineObservation _inlineObservation;

    // IL offset of the call wrt its parent method.
    internal IL_OFFSET _rawILOffset;
#endif

    internal InlineContext? _inlineContext;

    public GenTreeCall(var_types type)
        : base(GT_CALL, type)
    {
    }

    public ref CallArgs Args => ref _args;

    public bool CallerPop => (Flags & GTF_CALL_POP_ARGS) != 0;

    /// <summary>true implies that importer has performed tail call checks and providing a hint that this can be converted to a tail call.</summary>
    public bool CanTailCall => IsTailPrefixedCall || IsImplicitTailCall;

    public GenTree? ControlExpr
    {
        get
        {
            return _controlExpr;
        }

        set
        {
            _controlExpr = value;
        }
    }

#nullable disable
    public ref GenTree ControlExprRef => ref _controlExpr;
#nullable restore

    /// <summary>whether the call node returns its value in multiple return registers.</summary>
#if FEATURE_MULTIREG_RET
    public bool HasMultiRegRetVal
    {
        get
        {
#if TARGET_LOONGARCH64 || TARGET_RISCV64
            return (_type is TYP_STRUCT) && (_returnTypeDesc.ReturnRegCount > 1);
#else
            var result = false;
            var type = _type;

            if (varTypeIsStruct(type) && !ShouldHaveRetBufArg)
            {
                // Now it is a struct that is returned in registers.
                result = _returnTypeDesc.IsMultiRegRetType;
            }
#if TARGET_32BIT
            else
            {
                result = varTypeIsLong(type);
            }
#endif

            return result;
#endif
    }
#else
    public bool HasMultiRegRetVal => false;
#endif

    /// <summary>Get the helper identifier for this call or <see cref="CORINFO_HELP_UNDEF" /> if this is not a helper call.</summary>
    public unsafe CorInfoHelpFunc HelperNum
        => IsHelperCall() ? Compiler.eeGetHelperNum(_callMethHnd) : CORINFO_HELP_UNDEF;

    public byte InlineCandidatesCount => _inlineInfoCount;

    /// <summary>Whether or not this call is to an async function.</summary>
    /// <remarks>
    ///   <para>async involves passing an async continuation as a separate argument and returning an async continuation in REG_ASYNC_CONTINUATION_RET.</para>
    ///   <para>The async continuation is usually JIT added (WellKnownArg.AsyncContinuation). This is the case for an async method calling another async method by normal means. However, the VM also creates stubs that call async methods through calli where the async continuations are passed explicitly. See CEEJitInfo.getAsyncResumptionStub and MethodDesc.EmitTaskReturningThunk for examples. In those cases the JIT does not know (and does not need to know) which arg is the async continuation.</para>
    ///   <para>The VM also uses the AsyncHelpers.AsyncCallContinuation() intrinsic in the stubs discussed above. The JIT must take care in those cases to still mark the preceding call as an async call; this is required for correct LSRA behavior and GC reporting around the returned async continuation. This is currently done in lowering; see LowerAsyncContinuation().</para>
    /// </remarks>
    public bool IsAsync => (_callMoreFlags & GTF_CALL_M_ASYNC) != 0;

    public bool IsDelegateInvoke => (_callMoreFlags & GTF_CALL_M_DELEGATE_INV) != 0;

#if DEBUG
    public bool IsDevirtualized => (_callDebugFlags & GTF_CALL_MD_DEVIRTUALIZED) != 0;
#endif

    public bool IsExpandedEarly
    {
        get
        {
            return (_callMoreFlags & GTF_CALL_M_EXPANDED_EARLY) != 0;
        }

        set
        {
            _callMoreFlags = (_callMoreFlags & ~GTF_CALL_M_EXPANDED_EARLY) | (value ? GTF_CALL_M_EXPANDED_EARLY : 0);
        }
    }

#if FEATURE_FASTTAILCALL
    public bool IsFastTailCall
    {
        get
        {
            var result = false;

            if (IsTailCall)
            {
#if TARGET_X86
                result = (_callMoreFlags & GTF_CALL_M_TAILCALL_VIA_JIT_HELPER) == 0;
#else
                result = true;
#endif
            }

            return result;
        }
    }
#else
    public bool IsFastTailCall => false;
#endif

    public bool IsFatPointerCandidate
    {
        get
        {
            return (_callMoreFlags & GTF_CALL_M_FAT_POINTER_CHECK) != 0;
        }

        set
        {
            _callMoreFlags = (_callMoreFlags & ~GTF_CALL_M_FAT_POINTER_CHECK) | (value ? GTF_CALL_M_FAT_POINTER_CHECK : 0);
        }
    }

#if DEBUG
    public bool IsGuarded
    {
        get
        {
            return (_callDebugFlags & GTF_CALL_MD_GUARDED) != 0;
        }

        set
        {
            _callDebugFlags = (_callDebugFlags & ~GTF_CALL_MD_GUARDED) | (value ? GTF_CALL_MD_GUARDED : 0);
        }
    }
#endif

    public bool IsGuardedDevirtualizationCandidate
    {
        get
        {
            return (_callMoreFlags & GTF_CALL_M_GUARDED_DEVIRT) != 0;
        }

        set
        {
            if (value)
            {
                _callMoreFlags |= GTF_CALL_M_GUARDED_DEVIRT;
            }
            else
            {
                _callMoreFlags &= ~(GTF_CALL_M_GUARDED_DEVIRT | GTF_CALL_M_GUARDED_DEVIRT_EXACT);
            }
        }
    }

    /// <summary>true if this is marked for opportunistic tail calling.</summary>
#if FEATURE_TAILCALL_OPT
    public bool IsImplicitTailCall => (_callMoreFlags & GTF_CALL_M_IMPLICIT_TAILCALL) != 0;
#else
    public bool IsImplicitTailCall => false;
#endif

    [MemberNotNullWhen(true, nameof(_inlineCandidateInfo), nameof(_inlineCandidateInfoList))]
    public bool IsInlineCandidate => (Flags & GTF_CALL_INLINE_CANDIDATE) != 0;

    public bool IsNoReturn
    {
        get
        {
            return (_callMoreFlags & GTF_CALL_M_DOES_NOT_RETURN) != 0;
        }

        set
        {
            _callMoreFlags = (_callMoreFlags & ~GTF_CALL_M_DOES_NOT_RETURN) | (value ? GTF_CALL_M_DOES_NOT_RETURN : 0);
        }
    }

    public bool IsOptimizingRetBufAsLocal => (_callMoreFlags & GTF_CALL_M_RETBUFFARG_LCLOPT) != 0;

    /// <summary>true if VM has flagged this method as CORINFO_FLG_PINVOKE.</summary>
    public bool IsPInvoke => (_callMoreFlags & GTF_CALL_M_PINVOKE) != 0;

#if FEATURE_READYTORUN
    public bool IsR2RRelativeIndir => _entryPoint.accessType is IAT_PVALUE;
#else
    public bool IsR2RRelativeIndir => false;
#endif

    public bool IsSameThis => (_callMoreFlags & GTF_CALL_M_NONVIRT_SAME_THIS) != 0;

    /// <summary>true if this call didn't have an explicit tail. prefix in the IL but was marked as an explicit tail call because of tail call stress mode.</summary>
#if DEBUG
    public bool IsStressTailCall => (_callDebugFlags & GTF_CALL_MD_STRESS_TAILCALL) != 0;
#else
    public bool IsStressTailCall => false;
#endif

    public bool IsSuppressGCTransition => (_callMoreFlags & GTF_CALL_M_SUPPRESS_GC_TRANSITION) != 0;

    /// <summary>true implies that tail call flowgraph morhphing has performed final checks and committed to making a tail call.</summary>
    public bool IsTailCall => (_callMoreFlags & GTF_CALL_M_TAILCALL) != 0;

#if FEATURE_TAILCALL_OPT
    public bool IsTailCallConvertibleToLoop => (_callMoreFlags & GTF_CALL_M_TAILCALL_TO_LOOP) != 0;
#else
    public bool IsTailCallConvertibleToLoop => false;
#endif

    /// <summary>Check whether this is a tailcall dispatched via JIT helper.</summary>
    /// <remarks>We only use this mechanism on x86 as it is faster than our other more general tailcall mechanism.</remarks>
#if TARGET_X86
    public bool IsTailCallViaJitHelper => IsTailCall && ((_callMoreFlags & GTF_CALL_M_TAILCALL_VIA_JIT_HELPER) != 0);
#else
    public bool IsTailCallViaJitHelper =>false;
#endif

    /// <summary></summary>
    /// <remarks>Note that the distinction of whether tail prefixed or an implicit tail call is maintained on a call node till fgMorphCall() after which it will be either a tail call (i.e. IsTailCall() is true) or a non-tail call.</remarks>
    public bool IsTailPrefixedCall => (_callMoreFlags & GTF_CALL_M_EXPLICIT_TAILCALL) != 0;

#if DEBUG
    public bool IsUnboxed => (_callDebugFlags & GTF_CALL_MD_UNBOXED) != 0;
#endif

    public bool IsUnmanaged => (Flags & GTF_CALL_UNMANAGED) != 0;

    public bool IsVirtual => (Flags & GTF_CALL_VIRT_KIND_MASK) is not GTF_CALL_NONVIRT;

    public bool IsVirtualStub => (Flags & GTF_CALL_VIRT_KIND_MASK) is GTF_CALL_VIRT_STUB;

    public bool IsVirtualStubRelativeIndir => (_callMoreFlags & GTF_CALL_M_VIRTSTUB_REL_INDIRECT) != 0;

    public bool IsVirtualVtable => (Flags & GTF_CALL_VIRT_KIND_MASK) is GTF_CALL_VIRT_VTABLE;

    public bool NeedsNullCheck => (Flags & GTF_CALL_NULLCHECK) != 0;

    public bool NormalizesSmallTypesOnReturn => UnmanagedCallConv is CorInfoCallConvExtension.Managed;

    /// <summary>The return type handle of the call if it is a struct; always available</summary>
    public unsafe CORINFO_CLASS_HANDLE RetClsHnd
    {
        get
        {
            return _retClsHnd;
        }

        set
        {
            _retClsHnd = value;
        }
    }

    /// <summary>get the type descriptor of return value of the call</summary>
#if FEATURE_MULTIREG_RET
    internal ref readonly ReturnTypeDesc ReturnTypeDesc => ref _returnTypeDesc;
#else
    public ref readonly ReturnTypeDesc ReturnTypeDesc => ref Unsafe.NullRef<ReturnTypeDesc>();
#endif

    /// <summary>Returns true if the ABI dictates that this call should get a ret buf arg.</summary>
    /// <remarks>This may be out of sync with gtArgs.HasRetBuffer during import until we actually create the ret buffer.</remarks>
    public bool ShouldHaveRetBufArg => (_callMoreFlags & GTF_CALL_M_RETBUFFARG) != 0;

    public InlineCandidateInfo? SingleInlineCandidateInfo
    {
        get
        {
            if (_inlineInfoCount > 1)
            {
                NO_WAY("Call has multiple inline candidates");
            }
            return _inlineCandidateInfo;
        }

        set
        {
            if (value is not null)
            {
                _inlineInfoCount = 1;
                Flags |= GTF_CALL_INLINE_CANDIDATE;
            }
            else
            {
                _inlineInfoCount = 0;
                Flags &= ~GTF_CALL_INLINE_CANDIDATE;
            }

            _inlineCandidateInfo = value;
            IsGuardedDevirtualizationCandidate = false;
        }
    }

    public CorInfoCallConvExtension UnmanagedCallConv => IsUnmanaged ? _unmgdCallConv : CorInfoCallConvExtension.Managed;

#if DEBUG
    public bool WasInlineCandidate
    {
        get
        {
            return (_callDebugFlags & GTF_CALL_MD_WAS_CANDIDATE) != 0;
        }

        set
        {
            _callDebugFlags = (_callDebugFlags & ~GTF_CALL_MD_WAS_CANDIDATE) | (value ? GTF_CALL_MD_WAS_CANDIDATE : 0);
        }
    }
#endif

    /// <summary>Record a guarded devirtualization (GDV) candidate info for this call.</summary>
    /// <param name="comp">Compiler instance</param>
    /// <param name="candidateInfo">GDV candidate info</param>
    /// <remarks>A call can't have more than MAX_GDV_TYPE_CHECKS number of candidates</remarks>
    public void AddGdvCandidateInfo(Compiler comp, InlineCandidateInfo candidateInfo)
    {
        assert((_callMoreFlags & GTF_CALL_M_GUARDED_DEVIRT_EXACT) is 0);
        assert(_inlineInfoCount < MAX_GDV_TYPE_CHECKS);
        assert(candidateInfo is not null);

        if (_inlineInfoCount is 0)
        {
            // Most calls are monomorphic, so we don't need to allocate a vector
            _inlineCandidateInfo = candidateInfo;
        }
        else if (_inlineInfoCount is 1)
        {
            // Upgrade _inlineCandidateInfo to _inlineCandidateInfoList (vector)
            assert(_inlineCandidateInfo is not null);
            _inlineCandidateInfoList = [_inlineCandidateInfo, candidateInfo];
        }
        else
        {
            assert(_inlineCandidateInfoList is not null);
            _inlineCandidateInfoList.Add(candidateInfo);
        }

        _callMoreFlags |= GTF_CALL_M_GUARDED_DEVIRT;
        _inlineInfoCount++;
    }

    public void ClearInlineInfo()
    {
        SingleInlineCandidateInfo = null!;
    }

    public void ClearOtherRegs()
    {
#if FEATURE_MULTIREG_RET
        Span<regNumber> otherRegs = _otherRegs;
        otherRegs.Fille(REG_NA);
        _spillFlags = 0;
#endif
    }

    public void CopyOtherRegs(GenTreeCall tree)
    {
#if FEATURE_MULTIREG_RET
        Span<regNumber> otherRegs = tree._otherRegs;
        otherRegs.CopyTo(_otherRegs);
        _spillFlags = tree._spillFlags;
#endif
    }

    public InlineCandidateInfo GetGdvCandidateInfo(byte index)
    {
        assert(IsInlineCandidate);
        assert(index < _inlineInfoCount);

        if (_inlineInfoCount > 1)
        {
            // In this case we should access it through gtInlineCandidateInfoList
            return _inlineCandidateInfoList[index];
        }
        return _inlineCandidateInfo;
    }

    /// <summary>get i'th return register allocated to this call node.</summary>
    /// <param name="idx">index of the return register</param>
    /// <returns>Return regNumber of i'th return register of call node.</returns>
    /// <remarks>Returns REG_NA if there is no valid return register for the given index.</remarks>
    public regNumber GetRegNumByIdx(byte idx)
    {
        assert(idx < MAX_RET_REG_COUNT);

        var result = REG_NA;

        if (idx == 0)
        {
            result = RegNum;
        }
#if FEATURE_MULTIREG_RET
        else
        {
            result = _otherRegs[idx - 1];
        }
#endif

        return result;
    }

    /// <summary>Returns true if this call has any side effects.</summary>
    /// <param name="compiler">the compiler instance</param>
    /// <param name="ignoreExceptions">when `true`, ignores exception side effects</param>
    /// <param name="ignoreCctors">when `true`, ignores class constructor side effects</param>
    /// <returns>true if this call has any side-effects; false otherwise.</returns>
    /// <remarks>
    ///   <para>All non-helpers are considered to have side-effects.</para>
    ///   <para>Only helpers that do not mutate the heap, do not run constructors, may not throw, and are either a) pure or b) non-finalizing allocation functions are considered side-effect-free.</para>
    /// </remarks>
    public bool HasSideEffects(Compiler compiler, bool ignoreExceptions = false, bool ignoreCctors = false)
    {
        // Generally all GT_CALL nodes are considered to have side-effects, but we may have extra information about helper
        // calls that can prove them side-effect-free.
        if (!IsHelperCall())
        {
            // If needed, we can annotate other special intrinsic methods as side effect free as well.
            return !IsSpecialIntrinsic(compiler, NI_System_Type_GetTypeFromHandle);
        }

        var helper = HelperNum;

        // We definitely care about the side effects if MutatesHeap is true
        if (helper.MutatesHeap)
        {
            return true;
        }

        // Unless we have been instructed to ignore cctors (CSE, for example, ignores cctors), consider them side effects.
        if (!ignoreCctors && helper.MayRunCctor)
        {
            return true;
        }

        // Consider array allocators side-effect free for constant length (if it's not negative and fits into i32)
        if (helper.IsAllocator)
        {
            var arrLen = compiler.getArrayLengthFromAllocation(this);

            // if arrLen is null it means it wasn't an array allocator
            if ((arrLen is not null) && arrLen.IsIntCnsFitsInI32)
            {
                var cns = arrLen.AsIntConCommon().IconValue;

                if ((cns >= 0) && (cns <= CORINFO_Array_MaxLength))
                {
                    return false;
                }
            }
        }

        // If we also care about exceptions then check if the helper can throw
        if (!ignoreExceptions && !helper.NoThrow)
        {
            return true;
        }

        // If this is not a Pure helper call or an allocator (that will not need to run a finalizer) then this call has side effects.
        return !helper.IsPure && (!helper.IsAllocator || ((_callMoreFlags & GTF_CALL_M_ALLOC_SIDE_EFFECTS) is not 0));
    }

    /// <summary> Determine if this GT_CALL node is a devirtualization candidate.</summary>
    /// <param name="compiler">the compiler instance so that we can call eeFindHelper</param>
    /// <returns>Returns true if this GT_CALL node is a devirtualization candidate.</returns>
    /// <remarks>A call will be unmarked from devirtualization candidate if it is devirtualized.</remarks>
    public bool IsDevirtualizationCandidate(Compiler compiler)
    {
        return IsVirtual || (IsGenericVirtual(compiler) && (JitConfig.JitEnableGenericVirtualDevirtualization is not 0));
    }

    public bool IsGenericVirtual(Compiler compiler)
    {
        var result = false;

        if (_callType is CT_INDIRECT)
        {
            assert(_controlExpr is not null);

            if (_controlExpr.Oper.IsCall)
            {
                var call = _controlExpr.AsCall();

                if (call.IsHelperCall(CORINFO_HELP_VIRTUAL_FUNC_PTR) ||
                    call.IsHelperCall(CORINFO_HELP_GVMLOOKUP_FOR_SLOT))
                {
                    result = true;
                }
#if FEATURE_READYTORUN
                else if (call.IsHelperCall(CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR))
                {
                    result = true;
                }
#endif
            }
        }

        return result;
    }

    public bool IsHelperCall() => _callType == CT_HELPER;

    public unsafe bool IsHelperCall(CORINFO_METHOD_HANDLE callMethodHandle) => IsHelperCall() && (_callMethHnd == callMethodHandle);

    public unsafe bool IsHelperCall(CorInfoHelpFunc helperFunc) => IsHelperCall(Compiler.eeFindHelper(helperFunc));

    public bool IsSpecialIntrinsic() => (_callMoreFlags & GTF_CALL_M_SPECIAL_INTRINSIC) != 0;

    /// <summary>Determine if this GT_CALL node is a specific intrinsic.</summary>
    /// <param name="compiler">the compiler instance so that we can call lookupNamedIntrinsic</param>
    /// <param name="ni">the intrinsic id</param>
    /// <returns>Returns true if this GT_CALL node is a special intrinsic call.</returns>
    public unsafe bool IsSpecialIntrinsic(Compiler compiler, NamedIntrinsic ni)
    {
        return IsSpecialIntrinsic() && (compiler.lookupNamedIntrinsic(_callMethHnd) == ni);
    }

    public void SetIsAsync(in AsyncCallInfo info)
    {
        _callMoreFlags |= GTF_CALL_M_ASYNC;
        _asyncInfo = info;
    }

    /// <summary>Remove a guarded devirtualization (GDV) candidate info by its index.</summary>
    /// <param name="comp">Compiler instance</param>
    /// <param name="index">GDV candidate to remove</param>
    /// <remarks>Index must not be greater than gtInlineInfoCount the call will be marked as "has no inline candidates" if the last candidate is removed</remarks>
    public void RemoveGdvCandidateInfo(Compiler comp, byte index)
    {
        // We change the number of candidates so it's no longer "doesn't need a fallback"
        _callMoreFlags &= ~GTF_CALL_M_GUARDED_DEVIRT_EXACT;

        assert(index < _inlineInfoCount);

        if (_inlineInfoCount is 1)
        {
            // No longer have any inline candidates
            ClearInlineInfo();
            assert(_inlineInfoCount is 0);
            return;
        }

        assert(_inlineCandidateInfoList is not null);
        _inlineCandidateInfoList.RemoveAt(index);
        _inlineInfoCount--;

        // Downgrade _inlineCandidateInfoList to _inlineCandidateInfo
        if (_inlineInfoCount is 1)
        {
            _inlineCandidateInfo = _inlineCandidateInfoList[0];
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct _Anonymous1_e__Union
    {
        [FieldOffset(0)]
        public TailCallSiteInfo TailCallInfo;

        [FieldOffset(0)]
        public AsyncCallInfo AsyncInfo;

        [FieldOffset(0)]
        public CorInfoCallConvExtension UnmgdCallConv;
    }
}
