// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using static RyuJitSharp.CorInfoHelpFuncExtensions.Flags;

namespace RyuJitSharp;

public static class CorInfoHelpFuncExtensions
{
    private static ReadOnlySpan<Flags> s_flags => [
        MutatesHeap,                                // CORINFO_HELP_UNDEF
        IsPure,                                     // CORINFO_HELP_DIV
        IsPure,                                     // CORINFO_HELP_MOD
        IsPure,                                     // CORINFO_HELP_UDIV
        IsPure,                                     // CORINFO_HELP_UMOD
        IsPure | IsNoGC,                            // CORINFO_HELP_LLSH
        IsPure | IsNoGC,                            // CORINFO_HELP_LRSH
        IsPure | IsNoGC,                            // CORINFO_HELP_LRSZ
        IsPure,                                     // CORINFO_HELP_LMUL
        IsPure,                                     // CORINFO_HELP_LMUL_OVF
        IsPure,                                     // CORINFO_HELP_ULMUL_OVF
        IsPure,                                     // CORINFO_HELP_LDIV
        IsPure,                                     // CORINFO_HELP_LMOD
        IsPure,                                     // CORINFO_HELP_ULDIV
        IsPure,                                     // CORINFO_HELP_ULMOD
        IsPure,                                     // CORINFO_HELP_LNG2FLT
        IsPure,                                     // CORINFO_HELP_LNG2DBL
        IsPure,                                     // CORINFO_HELP_ULNG2FLT
        IsPure,                                     // CORINFO_HELP_ULNG2DBL
        IsPure,                                     // CORINFO_HELP_DBL2INT_OVF
        IsPure,                                     // CORINFO_HELP_DBL2LNG
        IsPure,                                     // CORINFO_HELP_DBL2LNG_OVF
        IsPure,                                     // CORINFO_HELP_DBL2UINT_OVF
        IsPure,                                     // CORINFO_HELP_DBL2ULNG
        IsPure,                                     // CORINFO_HELP_DBL2ULNG_OVF
        IsPure,                                     // CORINFO_HELP_FLTREM
        IsPure,                                     // CORINFO_HELP_DBLREM
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWFAST
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWFAST_MAYBEFROZEN
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWSFAST
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWSFAST_FINALIZE
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWSFAST_ALIGN8
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWSFAST_ALIGN8_VC
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEW_MDARR
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEW_MDARR_RARE
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWARR_1_DIRECT
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWARR_1_MAYBEFROZEN
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWARR_1_PTR
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWARR_1_VC
        IsAllocator | NonNullReturn,                // CORINFO_HELP_NEWARR_1_ALIGN8
        MutatesHeap | MayRunCctor,                  // CORINFO_HELP_INITCLASS
        MutatesHeap | MayRunCctor,                  // CORINFO_HELP_INITINSTCLASS
        IsPure,                                     // CORINFO_HELP_ISINSTANCEOFINTERFACE
        IsPure,                                     // CORINFO_HELP_ISINSTANCEOFARRAY
        IsPure,                                     // CORINFO_HELP_ISINSTANCEOFCLASS
        IsPure,                                     // CORINFO_HELP_ISINSTANCEOFANY
        IsPure,                                     // CORINFO_HELP_CHKCASTINTERFACE
        IsPure,                                     // CORINFO_HELP_CHKCASTARRAY
        IsPure,                                     // CORINFO_HELP_CHKCASTCLASS
        IsPure,                                     // CORINFO_HELP_CHKCASTANY
        IsPure,                                     // CORINFO_HELP_CHKCASTCLASS_SPECIAL
        MutatesHeap,                                // CORINFO_HELP_ISINSTANCEOF_EXCEPTION
        IsAllocator | NonNullReturn,                // CORINFO_HELP_BOX
        IsAllocator,                                // CORINFO_HELP_BOX_NULLABLE
        IsPure | IsNoEscape,                        // CORINFO_HELP_UNBOX
        IsPure,                                     // CORINFO_HELP_UNBOX_TYPETEST
        MutatesHeap,                                // CORINFO_HELP_UNBOX_NULLABLE
#if !WINDOWS_AMD64_ABI && !TARGET_WASM
        IsPure,                                     // CORINFO_HELP_GETREFANY
#else
        None,                                       // CORINFO_HELP_GETREFANY
#endif
        MutatesHeap,                                // CORINFO_HELP_ARRADDR_ST
        IsPure,                                     // CORINFO_HELP_LDELEMA_REF
        AlwaysThrow,                                // CORINFO_HELP_THROW
        AlwaysThrow,                                // CORINFO_HELP_RETHROW
        MutatesHeap,                                // CORINFO_HELP_THROWEXACT
        MutatesHeap,                                // CORINFO_HELP_USER_BREAKPOINT
        AlwaysThrow,                                // CORINFO_HELP_RNGCHKFAIL
        AlwaysThrow,                                // CORINFO_HELP_OVERFLOW
        AlwaysThrow,                                // CORINFO_HELP_THROWDIVZERO
        AlwaysThrow,                                // CORINFO_HELP_THROWNULLREF
        AlwaysThrow,                                // CORINFO_HELP_VERIFICATION
        AlwaysThrow | IsNoGC,                       // CORINFO_HELP_FAIL_FAST
        AlwaysThrow,                                // CORINFO_HELP_METHOD_ACCESS_EXCEPTION
        AlwaysThrow,                                // CORINFO_HELP_FIELD_ACCESS_EXCEPTION
        AlwaysThrow,                                // CORINFO_HELP_CLASS_ACCESS_EXCEPTION
        None,                                       // CORINFO_HELP_MON_ENTER
        None,                                       // CORINFO_HELP_MON_EXIT
        IsPure,                                     // CORINFO_HELP_GETCLASSFROMMETHODPARAM
        IsPure,                                     // CORINFO_HELP_GETSYNCFROMCLASSHANDLE
        MutatesHeap,                                // CORINFO_HELP_STOP_FOR_GC
        None,                                       // CORINFO_HELP_POLL_GC
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_CHECK_OBJ
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_ASSIGN_REF
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_CHECKED_ASSIGN_REF
        MutatesHeap,                                // CORINFO_HELP_BULK_WRITEBARRIER
        MutatesHeap,                                // CORINFO_HELP_GETFIELDADDR
        MutatesHeap,                                // CORINFO_HELP_GETSTATICFIELDADDR
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GETSTATICFIELDADDR_TLS
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GET_GCSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GET_NONGCSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GETDYNAMIC_GCSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GETDYNAMIC_NONGCSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GETPINNED_GCSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GETPINNED_NONGCSTATIC_BASE
        IsPure | NonNullReturn | IsNoGC,            // CORINFO_HELP_GET_GCSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn | IsNoGC,            // CORINFO_HELP_GET_NONGCSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETDYNAMIC_GCSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETDYNAMIC_NONGCSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETPINNED_GCSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETPINNED_NONGCSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GET_GCTHREADSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GET_NONGCTHREADSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE
        IsPure | NonNullReturn,                     // CORINFO_HELP_GET_GCTHREADSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn,                     // CORINFO_HELP_GET_NONGCTHREADSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2
        IsPure | NonNullReturn,                     // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2_NOJITOPT
        MutatesHeap,                                // CORINFO_HELP_GETDIRECTONTHREADLOCALDATA_NONGCTHREADSTATIC_BASE
        None,                                       // CORINFO_HELP_DBG_IS_JUST_MY_CODE
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_PROF_FCN_ENTER
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_PROF_FCN_LEAVE
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_PROF_FCN_TAILCALL
        MutatesHeap,                                // CORINFO_HELP_PINVOKE_CALLI
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_TAILCALL
        IsPure,                                     // CORINFO_HELP_GETCURRENTMANAGEDTHREADID
        IsNoGC,                                     // CORINFO_HELP_INIT_PINVOKE_FRAME
        IsNoEscape,                                 // CORINFO_HELP_MEMSET
        IsNoEscape,                                 // CORINFO_HELP_MEMZERO
        IsNoEscape,                                 // CORINFO_HELP_MEMCPY
        IsNoEscape,                                 // CORINFO_HELP_NATIVE_MEMSET
        IsPure | NonNullReturn,                     // CORINFO_HELP_RUNTIMEHANDLE_METHOD
        IsPure | NonNullReturn,                     // CORINFO_HELP_RUNTIMEHANDLE_CLASS
        IsPure,                                     // CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE
        IsPure,                                     // CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL
        MutatesHeap,                                // CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD
        MutatesHeap,                                // CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD
        MutatesHeap,                                // CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE
        MutatesHeap,                                // CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL
        IsPure,                                     // CORINFO_HELP_VIRTUAL_FUNC_PTR
        IsAllocator | NonNullReturn,                // CORINFO_HELP_READYTORUN_NEW
        IsAllocator | NonNullReturn,                // CORINFO_HELP_READYTORUN_NEWARR_1
        IsPure,                                     // CORINFO_HELP_READYTORUN_ISINSTANCEOF
        IsPure,                                     // CORINFO_HELP_READYTORUN_CHKCAST
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_READYTORUN_GCSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_READYTORUN_THREADSTATIC_BASE
        IsPure | NonNullReturn,                     // CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_READYTORUN_NONGCTHREADSTATIC_BASE
        IsPure,                                     // CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR
        IsPure | NonNullReturn,                     // CORINFO_HELP_READYTORUN_GENERIC_HANDLE
        MutatesHeap,                                // CORINFO_HELP_READYTORUN_DELEGATE_CTOR
        IsPure | NonNullReturn | MayRunCctor,       // CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE
        MutatesHeap,                                // CORINFO_HELP_EE_PERSONALITY_ROUTINE
        MutatesHeap,                                // CORINFO_HELP_EE_PERSONALITY_ROUTINE_FILTER_FUNCLET
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_ASSIGN_REF_EAX
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_ASSIGN_REF_EBX
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_ASSIGN_REF_ECX
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_ASSIGN_REF_ESI
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_ASSIGN_REF_EDI
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_ASSIGN_REF_EBP
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_CHECKED_ASSIGN_REF_EAX
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_CHECKED_ASSIGN_REF_EBX
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_CHECKED_ASSIGN_REF_ECX
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_CHECKED_ASSIGN_REF_ESI
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_CHECKED_ASSIGN_REF_EDI
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_CHECKED_ASSIGN_REF_EBP
        IsPure,                                     // CORINFO_HELP_LOOP_CLONE_CHOICE_ADDR
        MutatesHeap,                                // CORINFO_HELP_DEBUG_LOG_LOOP_CLONING
        AlwaysThrow,                                // CORINFO_HELP_THROW_ARGUMENTEXCEPTION
        AlwaysThrow,                                // CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION
        AlwaysThrow,                                // CORINFO_HELP_THROW_NOT_IMPLEMENTED
        AlwaysThrow,                                // CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED
        AlwaysThrow,                                // CORINFO_HELP_THROW_TYPE_NOT_SUPPORTED
        MutatesHeap,                                // CORINFO_HELP_THROW_AMBIGUOUS_RESOLUTION_EXCEPTION
        MutatesHeap,                                // CORINFO_HELP_THROW_ENTRYPOINT_NOT_FOUND_EXCEPTION
        None,                                       // CORINFO_HELP_JIT_PINVOKE_BEGIN
        None,                                       // CORINFO_HELP_JIT_PINVOKE_END
        IsNoGC,                                     // CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER
        IsNoGC,                                     // CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS
        None,                                       // CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT
        MutatesHeap,                                // CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT_TRACK_TRANSITIONS
        IsPure,                                     // CORINFO_HELP_GVMLOOKUP_FOR_SLOT
        MutatesHeap,                                // CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_STACK_PROBE
        MutatesHeap,                                // CORINFO_HELP_PATCHPOINT
        MutatesHeap,                                // CORINFO_HELP_PATCHPOINT_FORCED
        MutatesHeap,                                // CORINFO_HELP_CLASSPROFILE32
        MutatesHeap,                                // CORINFO_HELP_CLASSPROFILE64
        MutatesHeap,                                // CORINFO_HELP_DELEGATEPROFILE32
        MutatesHeap,                                // CORINFO_HELP_DELEGATEPROFILE64
        MutatesHeap,                                // CORINFO_HELP_VTABLEPROFILE32
        MutatesHeap,                                // CORINFO_HELP_VTABLEPROFILE64
        MutatesHeap,                                // CORINFO_HELP_COUNTPROFILE32
        MutatesHeap,                                // CORINFO_HELP_COUNTPROFILE64
        MutatesHeap,                                // CORINFO_HELP_VALUEPROFILE32
        MutatesHeap,                                // CORINFO_HELP_VALUEPROFILE64
        MutatesHeap | IsNoGC,                       // CORINFO_HELP_VALIDATE_INDIRECT_CALL
        MutatesHeap,                                // CORINFO_HELP_DISPATCH_INDIRECT_CALL
        IsAllocator | MutatesHeap,                  // CORINFO_HELP_ALLOC_CONTINUATION
        IsAllocator | MutatesHeap,                  // CORINFO_HELP_ALLOC_CONTINUATION_METHOD
        IsAllocator | MutatesHeap,                  // CORINFO_HELP_ALLOC_CONTINUATION_CLASS
    ];

    private static ReadOnlySpan<ExceptionSetFlags> s_thrownExceptions => [
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_UNDEF
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_DIV
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_MOD
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_UDIV
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_UMOD
        ExceptionSetFlags.None,                     // CORINFO_HELP_LLSH
        ExceptionSetFlags.None,                     // CORINFO_HELP_LRSH
        ExceptionSetFlags.None,                     // CORINFO_HELP_LRSZ
        ExceptionSetFlags.None,                     // CORINFO_HELP_LMUL
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_LMUL_OVF
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ULMUL_OVF
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_LDIV
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_LMOD
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ULDIV
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ULMOD
        ExceptionSetFlags.None,                     // CORINFO_HELP_LNG2FLT
        ExceptionSetFlags.None,                     // CORINFO_HELP_LNG2DBL
        ExceptionSetFlags.None,                     // CORINFO_HELP_ULNG2FLT
        ExceptionSetFlags.None,                     // CORINFO_HELP_ULNG2DBL
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_DBL2INT_OVF
        ExceptionSetFlags.None,                     // CORINFO_HELP_DBL2LNG
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_DBL2LNG_OVF
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_DBL2UINT_OVF
        ExceptionSetFlags.None,                     // CORINFO_HELP_DBL2ULNG
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_DBL2ULNG_OVF
        ExceptionSetFlags.None,                     // CORINFO_HELP_FLTREM
        ExceptionSetFlags.None,                     // CORINFO_HELP_DBLREM
        ExceptionSetFlags.None,                     // CORINFO_HELP_NEWFAST
        ExceptionSetFlags.None,                     // CORINFO_HELP_NEWFAST_MAYBEFROZEN
        ExceptionSetFlags.None,                     // CORINFO_HELP_NEWSFAST
        ExceptionSetFlags.None,                     // CORINFO_HELP_NEWSFAST_FINALIZE
        ExceptionSetFlags.None,                     // CORINFO_HELP_NEWSFAST_ALIGN8
        ExceptionSetFlags.None,                     // CORINFO_HELP_NEWSFAST_ALIGN8_VC
        ExceptionSetFlags.None,                     // CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_NEW_MDARR
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_NEW_MDARR_RARE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_NEWARR_1_DIRECT
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_NEWARR_1_MAYBEFROZEN
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_NEWARR_1_PTR
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_NEWARR_1_VC
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_NEWARR_1_ALIGN8
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_INITCLASS
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_INITINSTCLASS
        ExceptionSetFlags.None,                     // CORINFO_HELP_ISINSTANCEOFINTERFACE
        ExceptionSetFlags.None,                     // CORINFO_HELP_ISINSTANCEOFARRAY
        ExceptionSetFlags.None,                     // CORINFO_HELP_ISINSTANCEOFCLASS
        ExceptionSetFlags.None,                     // CORINFO_HELP_ISINSTANCEOFANY
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHKCASTINTERFACE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHKCASTARRAY
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHKCASTCLASS
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHKCASTANY
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHKCASTCLASS_SPECIAL
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ISINSTANCEOF_EXCEPTION
        ExceptionSetFlags.None,                     // CORINFO_HELP_BOX
        ExceptionSetFlags.None,                     // CORINFO_HELP_BOX_NULLABLE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_UNBOX
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_UNBOX_TYPETEST
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_UNBOX_NULLABLE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETREFANY
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ARRADDR_ST
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_LDELEMA_REF
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROW
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_RETHROW
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROWEXACT
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_USER_BREAKPOINT
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_RNGCHKFAIL
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_OVERFLOW
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROWDIVZERO
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROWNULLREF
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_VERIFICATION
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_FAIL_FAST
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_METHOD_ACCESS_EXCEPTION
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_FIELD_ACCESS_EXCEPTION
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CLASS_ACCESS_EXCEPTION
        ExceptionSetFlags.None,                     // CORINFO_HELP_MON_ENTER
        ExceptionSetFlags.None,                     // CORINFO_HELP_MON_EXIT
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETCLASSFROMMETHODPARAM
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETSYNCFROMCLASSHANDLE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_STOP_FOR_GC
        ExceptionSetFlags.None,                     // CORINFO_HELP_POLL_GC
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHECK_OBJ
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ASSIGN_REF
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHECKED_ASSIGN_REF
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_BULK_WRITEBARRIER
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETFIELDADDR
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETSTATICFIELDADDR
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETSTATICFIELDADDR_TLS
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GET_GCSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GET_NONGCSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETDYNAMIC_GCSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETDYNAMIC_NONGCSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETPINNED_GCSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETPINNED_NONGCSTATIC_BASE
        ExceptionSetFlags.None,                     // CORINFO_HELP_GET_GCSTATIC_BASE_NOCTOR
        ExceptionSetFlags.None,                     // CORINFO_HELP_GET_NONGCSTATIC_BASE_NOCTOR
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETDYNAMIC_GCSTATIC_BASE_NOCTOR
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETDYNAMIC_NONGCSTATIC_BASE_NOCTOR
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETPINNED_GCSTATIC_BASE_NOCTOR
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETPINNED_NONGCSTATIC_BASE_NOCTOR
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GET_GCTHREADSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GET_NONGCTHREADSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE
        ExceptionSetFlags.None,                     // CORINFO_HELP_GET_GCTHREADSTATIC_BASE_NOCTOR
        ExceptionSetFlags.None,                     // CORINFO_HELP_GET_NONGCTHREADSTATIC_BASE_NOCTOR
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2_NOJITOPT
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_GETDIRECTONTHREADLOCALDATA_NONGCTHREADSTATIC_BASE
        ExceptionSetFlags.None,                     // CORINFO_HELP_DBG_IS_JUST_MY_CODE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_PROF_FCN_ENTER
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_PROF_FCN_LEAVE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_PROF_FCN_TAILCALL
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_PINVOKE_CALLI
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_TAILCALL
        ExceptionSetFlags.None,                     // CORINFO_HELP_GETCURRENTMANAGEDTHREADID
        ExceptionSetFlags.None,                     // CORINFO_HELP_INIT_PINVOKE_FRAME
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_MEMSET
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_MEMZERO
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_MEMCPY
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_NATIVE_MEMSET
        ExceptionSetFlags.None,                     // CORINFO_HELP_RUNTIMEHANDLE_METHOD
        ExceptionSetFlags.None,                     // CORINFO_HELP_RUNTIMEHANDLE_CLASS
        ExceptionSetFlags.None,                     // CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE
        ExceptionSetFlags.None,                     // CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE_MAYBENULL
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL
        ExceptionSetFlags.NullReferenceException,   // CORINFO_HELP_VIRTUAL_FUNC_PTR
        ExceptionSetFlags.None,                     // CORINFO_HELP_READYTORUN_NEW
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_READYTORUN_NEWARR_1
        ExceptionSetFlags.None,                     // CORINFO_HELP_READYTORUN_ISINSTANCEOF
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_READYTORUN_CHKCAST
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_READYTORUN_GCSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_READYTORUN_THREADSTATIC_BASE
        ExceptionSetFlags.None,                     // CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_READYTORUN_NONGCTHREADSTATIC_BASE
        ExceptionSetFlags.NullReferenceException,   // CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR
        ExceptionSetFlags.None,                     // CORINFO_HELP_READYTORUN_GENERIC_HANDLE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_READYTORUN_DELEGATE_CTOR
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_EE_PERSONALITY_ROUTINE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_EE_PERSONALITY_ROUTINE_FILTER_FUNCLET
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ASSIGN_REF_EAX
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ASSIGN_REF_EBX
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ASSIGN_REF_ECX
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ASSIGN_REF_ESI
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ASSIGN_REF_EDI
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ASSIGN_REF_EBP
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHECKED_ASSIGN_REF_EAX
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHECKED_ASSIGN_REF_EBX
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHECKED_ASSIGN_REF_ECX
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHECKED_ASSIGN_REF_ESI
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHECKED_ASSIGN_REF_EDI
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CHECKED_ASSIGN_REF_EBP
        ExceptionSetFlags.None,                     // CORINFO_HELP_LOOP_CLONE_CHOICE_ADDR
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_DEBUG_LOG_LOOP_CLONING
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROW_ARGUMENTEXCEPTION
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROW_NOT_IMPLEMENTED
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROW_TYPE_NOT_SUPPORTED
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROW_AMBIGUOUS_RESOLUTION_EXCEPTION
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_THROW_ENTRYPOINT_NOT_FOUND_EXCEPTION
        ExceptionSetFlags.None,                     // CORINFO_HELP_JIT_PINVOKE_BEGIN
        ExceptionSetFlags.None,                     // CORINFO_HELP_JIT_PINVOKE_END
        ExceptionSetFlags.None,                     // CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER
        ExceptionSetFlags.None,                     // CORINFO_HELP_JIT_REVERSE_PINVOKE_ENTER_TRACK_TRANSITIONS
        ExceptionSetFlags.None,                     // CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_JIT_REVERSE_PINVOKE_EXIT_TRACK_TRANSITIONS
        ExceptionSetFlags.NullReferenceException,   // CORINFO_HELP_GVMLOOKUP_FOR_SLOT
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_STACK_PROBE
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_PATCHPOINT
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_PATCHPOINT_FORCED
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CLASSPROFILE32
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_CLASSPROFILE64
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_DELEGATEPROFILE32
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_DELEGATEPROFILE64
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_VTABLEPROFILE32
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_VTABLEPROFILE64
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_COUNTPROFILE32
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_COUNTPROFILE64
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_VALUEPROFILE32
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_VALUEPROFILE64
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_VALIDATE_INDIRECT_CALL
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_DISPATCH_INDIRECT_CALL
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ALLOC_CONTINUATION
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ALLOC_CONTINUATION_METHOD
        ExceptionSetFlags.UnknownException,         // CORINFO_HELP_ALLOC_CONTINUATION_CLASS
    ];

    extension(CorInfoHelpFunc helperId)
    {
        public bool AlwaysThrow => (helperId.Flags & AlwaysThrow) != 0;

        public bool IsAllocator => (helperId.Flags & IsAllocator) != 0;

        public bool IsNoEscape => (helperId.Flags & IsNoEscape) != 0;

        public bool IsNoGC => (helperId.Flags & IsNoGC) != 0;

        public bool IsPure => (helperId.Flags & IsPure) != 0;

        public bool MayRunCctor => (helperId.Flags & MayRunCctor) != 0;

        public bool MutatesHeap => (helperId.Flags & MutatesHeap) != 0;

        public bool NonNullReturn => (helperId.Flags & NonNullReturn) != 0;

        public bool NoThrow => helperId.ThrownExceptions == ExceptionSetFlags.None;

        public ExceptionSetFlags ThrownExceptions
        {
            get
            {
                assert(s_thrownExceptions.Length == (int)(CORINFO_HELP_COUNT));
                return s_thrownExceptions[(int)(helperId)];
            }
        }

        private Flags Flags
        {
            get
            {
                assert(s_flags.Length == (int)(CORINFO_HELP_COUNT));
                return s_flags[(int)(helperId)];
            }
        }
    }

    [Flags]
    internal enum Flags : byte
    {
        None = 0,
        IsPure = 1 << 0,
        AlwaysThrow = 1 << 1,
        NonNullReturn = 1 << 2,
        IsAllocator = 1 << 3,
        MutatesHeap = 1 << 4,
        MayRunCctor = 1 << 5,
        IsNoEscape = 1 << 6,
        IsNoGC = 1 << 7,
    }
}
