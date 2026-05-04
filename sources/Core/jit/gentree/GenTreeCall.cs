// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RyuJitSharp;

public sealed class GenTreeCall : GenTree
{
    private CallArgs _args;

#if DEBUG
    // Used to register callsites with the EE
    private unsafe CORINFO_SIG_INFO* _callSig;
#endif

    private _Anonymous1_e__Union _anonymous1;

#if FEATURE_MULTIREG_RET
    // TODO-AllArch: enable for all call nodes to unify single-reg and multi-reg returns.
    private ReturnTypeDesc _returnTypeDesc;

    // RegNum would always be the first return reg.
    // The following array holds the other reg numbers of multi-reg return.
    private regNumber _otherRegs[MAX_RET_REG_COUNT - 1];

    private MultiRegSpillFlags _spillFlags;
#endif

    // in addition to gtFlags
    private GenTreeCallFlags _callMoreFlags;

    // value from the gtCallTypes enumeration
    // TODO: Port private gtCallTypes _callType : 3;

    // exact return type
    // TODO: Port private var_types _returnType : 5;

    /// <summary>number of inline candidates for the given call</summary>
    private byte _inlineInfoCount;

    private unsafe CORINFO_CLASS_HANDLE _retClsHnd;

    private _Anonymous2_e__Union _anonymous2;

    private _Anonymous3_e__Union _anonymous3;

    // Always available for user virtual calls
    private LateDevirtualizationInfo? _lateDevirtualizationInfo;

    /// <summary>expression evaluated after args are placed which determines the control target</summary>
    /// <remarks>Applicable to any call type</remarks>
    private GenTree? _controlExpr;

    // CT_USER_FUNC or CT_HELPER
    private unsafe CORINFO_METHOD_HANDLE _callMethHnd;

#if FEATURE_READYTORUN
    /// <summary>Call target lookup info for method call from a Ready To Run module</summary>
    private CORINFO_CONST_LOOKUP _entryPoint;
#endif

#if DEBUG
    private GenTreeCallDebugFlags _callDebugFlags;

    /// <summary>For non-inline candidates, track the first observation that blocks candidacy.</summary>
    private InlineObservation _inlineObservation;

    // IL offset of the call wrt its parent method.
    private IL_OFFSET _rawILOffset;
#endif

    private InlineContext? _inlineContext;

    public GenTreeCall(var_types type)
        : base(GT_CALL, type)
    {
    }

    public ref CallArgs Args => ref _args;

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

    public bool IsExpandedEarly
    {
        get
        {
            return (_callMoreFlags & GTF_CALL_M_EXPANDED_EARLY) is not 0;
        }

        set
        {
            _callMoreFlags = (_callMoreFlags & ~GTF_CALL_M_EXPANDED_EARLY) | (value ? GTF_CALL_M_EXPANDED_EARLY : 0);
        }
    }

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
    public ref readonly ReturnTypeDesc ReturnTypeDesc => _returnTypeDesc;
#else
    public ref readonly ReturnTypeDesc ReturnTypeDesc => ref Unsafe.NullRef<ReturnTypeDesc>();
#endif

    /// <summary>Returns true if the ABI dictates that this call should get a ret buf arg.</summary>
    /// <remarks>This may be out of sync with gtArgs.HasRetBuffer during import until we actually create the ret buffer.</remarks>
    public bool ShouldHaveRetBufArg => (_callMoreFlags & GTF_CALL_M_RETBUFFARG) is not 0;

    /// <summary>get i'th return register allocated to this call node.</summary>
    /// <param name="idx">index of the return register</param>
    /// <returns>Return regNumber of i'th return register of call node.</returns>
    /// <remarks>Returns REG_NA if there is no valid return register for the given index.</remarks>
    public regNumber GetRegNumByIdx(byte idx)
    {
        assert(idx < MAX_RET_REG_COUNT);

        var result = REG_NA;

        if (idx is 0)
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

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous1_e__Union
    {
        /// <summary>Used for explicit tail prefixed calls</summary>
        [FieldOffset(0)]
        public TailCallSiteInfo? TailCallInfo;

        /// <summary>Only used for unmanaged calls, which cannot be tail-called</summary>
        [FieldOffset(0)]
        public CorInfoCallConvExtension UnmgdCallConv;

        /// <summary>Used for async calls</summary>
        [FieldOffset(0)]
        public AsyncCallInfo? AsyncInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous2_e__Union
    {
        /// <summary>GTF_CALL_VIRT_STUB - these are never inlined</summary>
        [FieldOffset(0)]
        public unsafe void* StubCallStubAddr;

        /// <summary>Used by static init helpers, represents a class they init</summary>
        [FieldOffset(0)]
        public unsafe CORINFO_CLASS_HANDLE InitClsHnd;

        /// <summary>Used by cast helpers to save corresponding IL offset</summary>
        [FieldOffset(0)]
        public IL_OFFSET CastHelperILOffset;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct _Anonymous3_e__Union
    {
        /// <summary>The serialized CALLI unmanaged call (CT_INDIRECT) cookie; reified into argument IR in morph</summary>
        [FieldOffset(0)]
        public unsafe CORINFO_CONST_LOOKUP* CallCookie;

        // Only used when inlining methods
        [FieldOffset(0)]
        public InlineCandidateInfo? InlineCandidateInfo;

        // Used when we have more than one GDV candidate
        [FieldOffset(0)]
        public List<InlineCandidateInfo>? InlineCandidateInfoList;
        
        [FieldOffset(0)]
        public HandleHistogramProfileCandidateInfo? HandleHistogramProfileCandidateInfo;

        /// <summary>Used to track type handle argument of dynamic helpers</summary>
        [FieldOffset(0)]
        public unsafe CORINFO_GENERIC_HANDLE CompileTimeHelperArgumentHandle;

        /// <summary>Used to pass direct call address between lower and codegen</summary>
        [FieldOffset(0)]
        public unsafe void* DirectCallAddress;
    }
}
