// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Compiler
{
    /// <summary>Only present for inlinees</summary>
    public InlineInfo? impInlineInfo;

    /// <summary>Size of the full stack</summary>
    protected int impStkSize;

    protected NodeToUnsignedMap? impEnumeratorGdvLocalMap;

    protected VarToLikelyClassMap? impEnumeratorLikelyTypeMap;

    /// <summary>Statements for the BB being imported.</summary>
    protected Statement? impStmtList;

    /// <summary>The last statement for the current BB.</summary>
    protected Statement? impLastStmt;

#if DEBUG
    private int impCurOpcOffs;

    private string? impCurOpcName;

    private bool impNestedStackSpill;

    /// <summary>oldest stmt added for which we did not call SetLastILOffset().</summary>
    /// <remarks>For displaying instrs with generated native code (-n:B)</remarks>
    private Statement? impLastILoffsStmt;
#endif

    /// <summary>The context used for looking up tokens.</summary>
    private unsafe CORINFO_CONTEXT_HANDLE impTokenLookupContextHandle;

    /// <summary>Debug info of current statement being imported</summary>
    /// <remarks>
    ///   <para>It gets set to contain no IL location (!impCurStmtDI.GetLocation().IsValid) after it has been set in the appended trees.</para>
    ///   <para>Then it gets updated at IL instructions for which we have to report mapping info.</para>
    ///   <para>It will always contain the current inline context.</para>
    /// </remarks>
    private DebugInfo impCurStmtDI;

    /// <summary>list of BBs currently waiting to be imported.</summary>
    private PendingDsc? impPendingList;

    /// <summary>Freed up dscs that can be reused</summary>
    private PendingDsc? impPendingFree;

    // We keep a byte-per-block map (dynamically extended) in the top-level Compiler object of a compilation.
    private List<byte> impPendingBlockMembers;

    private bool impCanReimport;

    // When we compute a "spill clique" (see above) these byte-maps are allocated to have a byte per basic
    // block, and represent the predecessor and successor members of the clique currently being computed.
    // *** Access to these will need to be locked in a parallel compiler.

    private List<byte> impSpillCliquePredMembers;

    private List<byte> impSpillCliqueSuccMembers;

    private BlockListNode? impBlockListNodeFreeList;

    /// <summary>the temp below is valid and available</summary>
    public bool impBoxTempInUse;

    /// <summary>a temporary that is used for boxing</summary>
    public int impBoxTemp;

#if DEBUG
    public int impInlinedCodeSize;
#endif

    // The Compiler that is the root of the inlining tree of which "this" is a member.
    public Compiler impInlineRoot
    {
        get
        {
            var result = this;

            if (impInlineInfo is not null)
            {
                result = impInlineInfo.InlineRoot;
            }
            return result;
        }
    }

    // One might think it is worth caching these values, but results indicate that it isn't.
    // In addition, caching them causes SuperPMI to be unable to completely encapsulate an individual method context.

    public unsafe CORINFO_CLASS_HANDLE impRefAnyClass
    {
        get
        {
            var refAnyClass = info.compCompHnd->getBuiltinClass(CLASSID_TYPED_BYREF);
            assert(refAnyClass is not null);
            return refAnyClass;
        }
    }
    public unsafe CORINFO_CLASS_HANDLE impRuntimeArgumentHandle
    {
        get
        {
            var argIteratorClass = info.compCompHnd->getBuiltinClass(CLASSID_ARGUMENT_HANDLE);
            assert(argIteratorClass is not null);
            return argIteratorClass;
        }
    }
    public unsafe CORINFO_CLASS_HANDLE impTypeHandleClass
    {
        get
        {
            var typeHandleClass = info.compCompHnd->getBuiltinClass(CLASSID_TYPE_HANDLE);
            assert(typeHandleClass is not null);
            return typeHandleClass;
        }
    }
    public unsafe CORINFO_CLASS_HANDLE impStringClass
    {
        get
        {
            var stringClass = info.compCompHnd->getBuiltinClass(CLASSID_STRING);
            assert(stringClass is not null);
            return stringClass;
        }
    }

    public unsafe CORINFO_CLASS_HANDLE impObjectClass
    {
        get
        {
            var objectClass = info.compCompHnd->getBuiltinClass(CLASSID_SYSTEM_OBJECT);
            assert(objectClass is not null);
            return objectClass;
        }
    }

    public unsafe int impBoxPatternMatch(CORINFO_RESOLVED_TOKEN* pResolvedToken, byte* codeAddr, byte* codeEndp, BoxPatterns opts)
    {
        // TODO: Port Compiler.impBoxPatternMatch
        return -1;
    }

    /// <summary>check that the node's type is compatible with the signature's type using ECMA implicit argument coercion table.</summary>
    /// <param name="sigType">the type in the call signature</param>
    /// <param name="nodeType">the node type</param>
    /// <returns>true if they are compatible, false otherwise</returns>
    /// <remarks>
    ///   <para>it is currently allowing byref->long passing, should be fixed in VM</para>
    ///   <para>it can't check long -> native int case on 64-bit platforms, so the behavior is different depending on the target bitness</para>
    /// </remarks>
    public static bool impCheckImplicitArgumentCoercion(var_types sigType, var_types nodeType)
    {
        if (sigType == nodeType)
        {
            return true;
        }

        assert(AreContiguous(TYP_BYTE, TYP_UBYTE, TYP_SHORT, TYP_USHORT, TYP_INT, TYP_UINT));

        if (sigType is >= TYP_BYTE and <= TYP_UINT)
        {
            if (nodeType is (>= TYP_BYTE and <= TYP_UINT) or TYP_I_IMPL)
            {
                return true;
            }
        }
        else if (sigType is TYP_LONG or TYP_ULONG)
        {
            if (nodeType is TYP_LONG)
            {
                return true;
            }
        }
        else if (sigType is TYP_FLOAT or TYP_DOUBLE)
        {
            if (nodeType is TYP_FLOAT or TYP_DOUBLE)
            {
                return true;
            }
        }
        else if (sigType is TYP_BYREF)
        {
            if (nodeType is TYP_I_IMPL)
            {
                return true;
            }

            // This condition tolerates such IL:
            // ;  V00 this              ref  this class-hnd
            // ldarg.0
            // call(byref)
            if (nodeType is TYP_REF)
            {
                return true;
            }
        }
        else if (varTypeIsStruct(sigType))
        {
            if (varTypeIsStruct(nodeType))
            {
                return true;
            }
        }

        // This condition should not be under `else` because `TYP_I_IMPL`
        // intersects with `TYP_LONG` or `TYP_INT`.
        if (sigType is TYP_I_IMPL or TYP_U_IMPL)
        {
            // Note that it allows `ldc.i8 1; call(nint)` on 64-bit platforms,
            // but we can't distinguish `nint` from `long` there.
            if (nodeType is TYP_I_IMPL or TYP_U_IMPL or TYP_INT or TYP_UINT)
            {
                return true;
            }

            // It tolerates IL that ECMA does not allow but that is commonly used.
            // Example:
            //   V02 loc1           struct <RTL_OSVERSIONINFOEX, 32>
            //   ldloca.s     0x2
            //   call(native int)
            if (nodeType is TYP_BYREF)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>screen inline candate based on info from the method header</summary>
    /// <param name="fncHandle">inline candidate method</param>
    /// <param name="methInfo">method info from VM</param>
    /// <param name="forceInline">true if method is marked with AggressiveInlining</param>
    /// <param name="inlineResult">ongoing inline evaluation</param>
    public unsafe void impCanInlineIL(CORINFO_METHOD_HANDLE fncHandle, CORINFO_METHOD_INFO* methInfo, bool forceInline, InlineResult inlineResult)
    {
        var codeSize = methInfo->ILCodeSize;

        // We shouldn't have made up our minds yet...
        assert(!inlineResult.IsDecided);

        if (methInfo->EHcount > 0)
        {
            if (!opts.compInlineMethodsWithEH)
            {
                inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_EH);
                return;
            }
        }

        if ((methInfo->ILCode is null) || (codeSize == 0))
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_NO_BODY);
            return;
        }

        // For now we don't inline varargs (import code can't handle it)

        if (methInfo->args.isVarArg())
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_MANAGED_VARARGS);
            return;
        }

        // Reject if it has too many locals.
        // This is currently an implementation limit due to fixed-size arrays in the
        // inline info, rather than a performance heuristic.

        inlineResult.NoteInt(InlineObservation.CALLEE_NUMBER_OF_LOCALS, methInfo->locals.numArgs);

        if (methInfo->locals.numArgs > MAX_INL_LCLS)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_TOO_MANY_LOCALS);
            return;
        }

        // Make sure there aren't too many arguments.
        // This is currently an implementation limit due to fixed-size arrays in the
        // inline info, rather than a performance heuristic.

        inlineResult.NoteInt(InlineObservation.CALLEE_NUMBER_OF_ARGUMENTS, methInfo->args.numArgs);

        if (methInfo->args.numArgs > MAX_INL_ARGS)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_TOO_MANY_ARGUMENTS);
            return;
        }

        // Note force inline state

        inlineResult.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, forceInline);

        // Note IL code size

        inlineResult.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, codeSize);

        if (inlineResult.IsFailure)
        {
            return;
        }

        // Make sure maxstack is not too big

        inlineResult.NoteInt(InlineObservation.CALLEE_MAXSTACK, methInfo->maxStack);

        if (inlineResult.IsFailure)
        {
            return;
        }
    }

    /// <summary>Get the first non-prefix opcode.</summary>
    /// <param name="codeAddr"></param>
    /// <param name="codeEndp"></param>
    /// <returns></returns>
    /// <remarks>Used for verification of valid combinations of prefixes and actual opcodes.</remarks>
    private static unsafe OPCODE impGetNonPrefixOpcode(byte* codeAddr, byte* codeEndp)
    {
        while (codeAddr < codeEndp)
        {
            var opcode = (OPCODE)(codeAddr[0]);
            codeAddr += sizeof(byte);

            if (opcode == CEE_PREFIX1)
            {
                if (codeAddr >= codeEndp)
                {
                    break;
                }

                opcode = (OPCODE)(codeAddr[0] + 0x0100);
                codeAddr += sizeof(byte);
            }

            switch (opcode)
            {
                case CEE_UNALIGNED:
                case CEE_VOLATILE:
                case CEE_TAILCALL:
                case CEE_CONSTRAINED:
                case CEE_READONLY:
                {
                    break;
                }

                default:
                {
                    return opcode;
                }
            }

            codeAddr += opcode.Size;
        }
        return CEE_ILLEGAL;
    }

    /// <summary>Look for special cases where a call to an intrinsic returns an exact type</summary>
    /// <param name="call">handle for the special intrinsic method</param>
    /// <returns>Exact class handle returned by the intrinsic call, if known; otherwise <c>null</c> if not known, or not likely to lead to beneficial optimization.</returns>
    /// <remarks>This computes the return type for generic factory methods, where the type returned is determined by a generic method or class parameter.</remarks>
    public unsafe CORINFO_CLASS_HANDLE impGetSpecialIntrinsicExactReturnType(GenTreeCall call)
    {
        var methodHnd = call._callMethHnd;
        JITDUMP($"Special intrinsic: looking for exact type returned by {eeGetMethodFullName(methodHnd)}\n");

        CORINFO_CLASS_HANDLE result = null;

        // See what intrinsic we have...
        var ni = lookupNamedIntrinsic(methodHnd);

        switch (ni)
        {
            case NI_System_Collections_Generic_Comparer_get_Default:
            case NI_System_Collections_Generic_EqualityComparer_get_Default:
            case NI_System_Array_T_GetEnumerator:
            {
                // Expect one class generic parameter; figure out which it is.
                CORINFO_SIG_INFO sig;
                info.compCompHnd->getMethodSig(methodHnd, &sig);
                assert(sig.sigInst.classInstCount == 1);

                var typeHnd = sig.sigInst.classInst[0];
                assert(typeHnd is not null);

                var instParam = call.Args.FindWellKnownArg(WellKnownArg.InstParam);

                if (instParam is not null)
                {
                    assert(instParam.Next is null);

                    var hClass = gtGetHelperArgClassHandle(instParam.Node);

                    if (hClass != NO_CLASS_HANDLE)
                    {
                        typeHnd = getTypeInstantiationArgument(hClass, 0);
                    }
                }

                if (ni == NI_System_Collections_Generic_EqualityComparer_get_Default)
                {
                    result = info.compCompHnd->getDefaultEqualityComparerClass(typeHnd);
                }
                else if (ni == NI_System_Collections_Generic_Comparer_get_Default)
                {
                    result = info.compCompHnd->getDefaultComparerClass(typeHnd);
                }
                else
                {
                    assert(ni == NI_System_Array_T_GetEnumerator);
                    result = info.compCompHnd->getSZArrayHelperEnumeratorClass(typeHnd);
                }

                if (result != NO_CLASS_HANDLE)
                {
                    JITDUMP($"Special intrinsic for type {eeGetClassName(typeHnd)}: return type is {((result is not null) ? eeGetClassName(result) : "unknown")}\n");
                }
                else
                {
                    JITDUMP($"Special intrinsic for type {eeGetClassName(typeHnd)}: return type undetermined, so deferring opt\n");
                }
                break;
            }

            case NI_System_SZArrayHelper_GetEnumerator:
            {
                // Expect one method generic parameter; figure out which it is.
                CORINFO_SIG_INFO sig;
                info.compCompHnd->getMethodSig(methodHnd, &sig);

                assert(sig.sigInst.methInstCount == 1);
                assert(sig.sigInst.classInstCount == 0);

                var typeHnd = sig.sigInst.methInst[0];
                assert(typeHnd is not null);

                var instParam = call.Args.FindWellKnownArg(WellKnownArg.InstParam);
                if (instParam is not null)
                {
                    assert(instParam.Next is null);

                    var hMethod = gtGetHelperArgMethodHandle(instParam.Node);

                    if (hMethod != NO_METHOD_HANDLE)
                    {
                        typeHnd = getMethodInstantiationArgument(hMethod, 0);
                    }
                }

                result = info.compCompHnd->getSZArrayHelperEnumeratorClass(typeHnd);

                if (result != NO_CLASS_HANDLE)
                {
                    JITDUMP($"Special intrinsic for type {eeGetClassName(typeHnd)}: return type is {((result is not null) ? eeGetClassName(result) : "unknown")}\n");
                }
                else
                {
                    JITDUMP($"Special intrinsic for type {eeGetClassName(typeHnd)}: return type undetermined, so deferring opt\n");
                }
                break;
            }

            default:
            {
                JITDUMP("This special intrinsic not handled, sorry...\n");
                break;
            }
        }

        return result;
    }

    /// <summary>helper function that will tell us if the IL instruction at the addr passed by param consumes an address at the top of the stack.</summary>
    /// <param name="codeAddr"></param>
    /// <param name="codeEndp"></param>
    /// <returns></returns>
    /// <remarks>We use it to save us lvAddrTaken</remarks>
    public unsafe bool impILConsumesAddr(byte* codeAddr, byte* codeEndp)
    {
        var opcode = impGetNonPrefixOpcode(codeAddr, codeEndp);
        return opcode is CEE_LDFLD;
    }

    public unsafe bool impIsTailCallILPattern(bool tailPrefixed, OPCODE curOpcode, byte* codeAddrOfNextOpcode, byte* codeEnd, bool isRecursive)
    {
        // Bail out if the current opcode is not a call.
        if (!impOpcodeIsCallOpcode(curOpcode))
        {
            return false;
        }

#if !FEATURE_TAILCALL_OPT_SHARED_RETURN
        // If shared ret tail opt is not enabled, we will enable it for recursive methods.

        if (isRecursive)
#endif
        {
            // we can actually handle if the ret is in a fallthrough block, as long as that is the only part of the
            // sequence. Make sure we don't go past the end of the IL however.
            codeEnd = unchecked((byte*)(nint.Min((nint)(codeEnd + 1), (nint)(info.compCode + info.compILCodeSize))));
        }

        // Bail out if there is no next opcode after call
        if (codeAddrOfNextOpcode >= codeEnd)
        {
            return false;
        }
        return (OPCODE)(codeAddrOfNextOpcode[0]) == CEE_RET;
    }

    /// <summary>Check for the special case where the object is the methods original 'this' pointer.</summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    /// <remarks>Note that, the original 'this' pointer is always local var 0 for non-static method, even if we might have created the copy of 'this' pointer in lvaArg0Var.</remarks>
    public bool impIsThis(GenTree obj)
    {
        if (compIsForInlining)
        {
            return impInlineInfo.InlinerCompiler.impIsThis(obj);
        }
        else
        {
            return ((obj is not null) && (obj.Oper is GT_LCL_VAR) &&
                    lvaIsOriginalThisArg(obj.AsLclVarCommon().LclNum));
        }
    }

    private static bool impOpcodeIsCallOpcode(OPCODE opcode)
        => opcode is CEE_CALL or CEE_CALLI or CEE_CALLVIRT;

    private static unsafe void impValidateMemoryAccessOpcode(byte* codeAddr, byte* codeEndp, bool volatilePrefix)
    {
        var opcode = impGetNonPrefixOpcode(codeAddr, codeEndp);

        if (opcode is (>= CEE_LDIND_I1 and <= CEE_STIND_R8) or CEE_STIND_I or CEE_LDFLD or CEE_STFLD or CEE_LDOBJ or CEE_STOBJ or CEE_INITBLK or CEE_CPBLK)
        {
            // Opcode of all ldind and stdind happen to be in continuous, except stind.i.
            return;
        }

        if (volatilePrefix && (opcode is CEE_LDSFLD or CEE_STSFLD))
        {
            // volatile. prefix is allowed with the ldsfld and stsfld
            return;
        }

        BADCODE("Invalid opcode for unaligned. or volatile. prefix");
    }

    public unsafe void impResolveToken(byte* addr, out CORINFO_RESOLVED_TOKEN resolvedToken, CorInfoTokenKind kind)
    {
        resolvedToken = new CORINFO_RESOLVED_TOKEN {
            tokenContext = impTokenLookupContextHandle,
            tokenScope = info.compScopeHnd,
            token = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(addr, sizeof(int))),
            tokenType = kind,
        };

        fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
        {
            info.compCompHnd->resolveToken(pResolvedToken);
        }
    }

    /// <summary>make observations that help determine the profitability of a discretionary inline</summary>
    /// <param name="inlineInfo">InlineInfo for the inline, or null for the prejit root</param>
    /// <param name="inlineResult">InlineResult accumulating information about this inline</param>
    /// <remarks>
    ///   <para>If inlining or prejitting the root, this method also makes various observations about the method that factor into inline decisions.</para>
    ///   <para>It sets `compNativeSizeEstimate` as a side effect.</para>
    /// </remarks>
    public unsafe void impMakeDiscretionaryInlineObservations(InlineInfo? inlineInfo, InlineResult inlineResult)
    {
        assert((inlineInfo is not null) == compIsForInlining);

        // If we're really inlining, we should just have one result in play.
        assert((inlineInfo is null) || (inlineResult == inlineInfo.inlineResult));

        // If this is a "forceinline" method, the JIT probably shouldn't have gone
        // to the trouble of estimating the native code size. Even if it did, it
        // shouldn't be relying on the result of this method.
        assert(inlineResult.Observation is InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);

        // Note if the caller contains NEWOBJ or NEWARR.
        var rootCompiler = impInlineRoot;

        if ((rootCompiler.optMethodFlags & OMF_HAS_NEWARRAY) != 0)
        {
            inlineResult.Note(InlineObservation.CALLER_HAS_NEWARRAY);
        }

        if ((rootCompiler.optMethodFlags & OMF_HAS_NEWOBJ) != 0)
        {
            inlineResult.Note(InlineObservation.CALLER_HAS_NEWOBJ);
        }

        var calleeIsStatic = (info.compFlags & CORINFO_FLG_STATIC) != 0;
        var isSpecialMethod = (info.compFlags & CORINFO_FLG_CONSTRUCTOR) != 0;

        if (isSpecialMethod)
        {
            if (calleeIsStatic)
            {
                inlineResult.Note(InlineObservation.CALLEE_IS_CLASS_CTOR);
            }
            else
            {
                inlineResult.Note(InlineObservation.CALLEE_IS_INSTANCE_CTOR);
            }
        }
        else if (!calleeIsStatic)
        {
            // Callee is an instance method.
            // Check if the callee has the same 'this' as the root.

            if (inlineInfo is not null)
            {
                var iciCall = inlineInfo.iciCall;
                assert(iciCall is not null);

                var thisArg = iciCall.AsCall().Args.ThisArg;
                assert(thisArg is not null);

                var isSameThis = impIsThis(thisArg.Node);
                inlineResult.NoteBool(InlineObservation.CALLSITE_IS_SAME_THIS, isSameThis);
            }
        }

        ref var rootSigInst = ref rootCompiler.info.compMethodInfo->args.sigInst;
        ref var sigInst = ref info.compMethodInfo->args.sigInst;

        var callsiteIsGeneric = (rootSigInst.methInstCount != 0) || (rootSigInst.classInstCount != 0);
        var calleeIsGeneric = (sigInst.methInstCount != 0) || (sigInst.classInstCount != 0);

        if (!callsiteIsGeneric && calleeIsGeneric)
        {
            inlineResult.Note(InlineObservation.CALLSITE_NONGENERIC_CALLS_GENERIC);
        }

        // Inspect callee's arguments (and the actual values at the callsite for them)
        var sig = info.compMethodInfo->args;
        var sigArg = sig.args;

        CallArg? argUse = null;

        if (inlineInfo is not null)
        {
            var iciCall = inlineInfo.iciCall;
            assert(iciCall is not null);
            argUse = iciCall.AsCall().Args.Args.FirstOrDefault();
        }

        for (var i = 0; i < info.compMethodInfo->args.numArgs; i++)
        {
            if ((argUse is not null) && (argUse.WellKnownArg == WellKnownArg.ThisPointer))
            {
                argUse = argUse.Next;
            }

            CORINFO_CLASS_HANDLE sigClass;
            var corType = strip(info.compCompHnd->getArgType(&sig, sigArg, &sigClass));
            var argNode = argUse?.EarlyNode;

            if (corType == CORINFO_TYPE_CLASS)
            {
                sigClass = info.compCompHnd->getArgClass(&sig, sigArg);
            }
            else if (corType == CORINFO_TYPE_VALUECLASS)
            {
                inlineResult.Note(InlineObservation.CALLEE_ARG_STRUCT);
            }
            else if (corType == CORINFO_TYPE_BYREF)
            {
                sigClass = info.compCompHnd->getArgClass(&sig, sigArg);
                corType = info.compCompHnd->getChildType(sigClass, &sigClass);
            }

            if (argNode is not null)
            {
                var argCls = gtGetClassHandle(argNode, out var isExact, out var isNonNull);

                if (argCls is not null)
                {
                    var isArgValueType = eeIsValueClass(argCls);

                    // Exact class of the arg is known
                    if (isExact && !isArgValueType)
                    {
                        inlineResult.Note(InlineObservation.CALLSITE_ARG_EXACT_CLS);

                        if ((argCls != sigClass) && (sigClass is not null))
                        {
                            // .. but the signature accepts a less concrete type.
                            inlineResult.Note(InlineObservation.CALLSITE_ARG_EXACT_CLS_SIG_IS_NOT);
                        }
                    }
                    // Arg is a reference type in the signature and a boxed value type was passed.
                    else if (isArgValueType && (corType == CORINFO_TYPE_CLASS))
                    {
                        inlineResult.Note(InlineObservation.CALLSITE_ARG_BOXED);
                    }
                }

                if (argNode.Oper.IsConst)
                {
                    inlineResult.Note(InlineObservation.CALLSITE_ARG_CONST);
                }

                assert(argUse is not null);
                argUse = argUse.Next;
            }
            sigArg = info.compCompHnd->getArgNext(sigArg);
        }

        // Note if the callee's return type is a value type
        if (info.compMethodInfo->args.retType == CORINFO_TYPE_VALUECLASS)
        {
            inlineResult.Note(InlineObservation.CALLEE_RETURNS_STRUCT);
        }

        // Note if the callee's class is a promotable struct
        if ((info.compClassAttr & CORINFO_FLG_VALUECLASS) != 0)
        {
            assert(structPromotionHelper is not null);
            if (structPromotionHelper.CanPromoteStructType(info.compClassHnd))
            {
                inlineResult.Note(InlineObservation.CALLEE_CLASS_PROMOTABLE);
            }
            inlineResult.Note(InlineObservation.CALLEE_CLASS_VALUETYPE);
        }

#if FEATURE_SIMD

        // Note if this method is has SIMD args or return value
        if ((inlineInfo is not null) && inlineInfo.hasSimdTypeArgLocalOrReturn)
        {
            inlineResult.Note(InlineObservation.CALLEE_HAS_SIMD);
        }

#endif

        // Roughly classify callsite frequency.
        var frequency = InlineCallsiteFrequency.UNUSED;

        // If this is a prejit root, or a maximally hot block...
        if (inlineInfo is null)
        {
            frequency = InlineCallsiteFrequency.HOT;
        }
        else
        {
            var iciBlock = inlineInfo.iciBlock;
            assert(iciBlock is not null);

            // No training data.  Look for loop-like things.
            // We consider a recursive call loop-like.  Do not give the inlining boost to the method itself.
            // However, give it to things nearby.
            if (iciBlock.isMaxBBWeight)
            {
                frequency = InlineCallsiteFrequency.HOT;
            }
            else if (iciBlock.HasFlag(BBF_BACKWARD_JUMP) &&
                     (inlineInfo.fncHandle != inlineInfo.inlineCandidateInfo.ilCallerHandle))
            {
                frequency = InlineCallsiteFrequency.LOOP;
            }
            else if (iciBlock.hasProfileWeight && (iciBlock.bbWeight > BB_ZERO_WEIGHT))
            {
                frequency = InlineCallsiteFrequency.WARM;
            }
            // Now modify the multiplier based on where we're called from.
            else if (iciBlock.isRunRarely || ((info.compFlags & FLG_CCTOR) == FLG_CCTOR))
            {
                frequency = InlineCallsiteFrequency.RARE;
            }
            else
            {
                frequency = InlineCallsiteFrequency.BORING;
            }
        }

        // Also capture the block weight of the call site.
        //
        // In the prejit root case, assume at runtime there might be a hot call site
        // for this method, so we won't prematurely conclude this method should never
        // be inlined.
        //
        weight_t weight = 0;

        if (inlineInfo is not null)
        {
            assert(inlineInfo.iciBlock is not null);
            weight = inlineInfo.iciBlock.bbWeight;
        }
        else
        {
            const weight_t prejitHotCallerWeight = 1000000.0;
            weight = prejitHotCallerWeight;
        }

        inlineResult.NoteInt(InlineObservation.CALLSITE_FREQUENCY, (int)(frequency));
        inlineResult.NoteInt(InlineObservation.CALLSITE_WEIGHT, (int)(weight));

        var hasProfile = false;
        var profileFreq = 0.0;

        // If the call site has profile data, report the relative frequency of the site.
        if ((inlineInfo is not null) && rootCompiler.fgHaveSufficientProfileWeights)
        {
            assert(inlineInfo.iciBlock is not null);
            var callSiteWeight = inlineInfo.iciBlock.bbWeight;
            var entryWeight = rootCompiler.fgCalledCount;
            profileFreq = fgProfileWeightsEqual(entryWeight, 0.0) ? 0.0 : callSiteWeight / entryWeight;
            hasProfile = true;

            assert(callSiteWeight >= 0);
            assert(entryWeight >= 0);
        }
        else if (inlineInfo is null)
        {
            // Simulate a hot callsite for PrejitRoot mode.
            hasProfile = true;
            profileFreq = 1.0;
        }

        inlineResult.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, hasProfile);
        inlineResult.NoteDouble(InlineObservation.CALLSITE_PROFILE_FREQUENCY, profileFreq);
    }

    public unsafe var_types impNormStructType(CORINFO_CLASS_HANDLE structHnd)
        => impNormStructType(structHnd, out Unsafe.NullRef<var_types>());

    /// <summary>Normalize the type of a (known to be) struct class handle.</summary>
    /// <param name="structHnd">The class handle for the struct type of interest.</param>
    /// <param name="pSimdBaseJitType">if the struct is a SIMD type, set to the SIMD base JIT type</param>
    /// <returns>The JIT type for the struct (e.g. TYP_STRUCT, or TYP_SIMD*).</returns>
    /// <remarks>
    ///   <para>This may also modify the compFloatingPointUsed flag if the type is a SIMD type.</para>
    ///   <para>Normalizing the type involves examining the struct type to determine if it should be modified to one that is handled specially by the JIT, possibly being a candidate for full enregistration, e.g. TYP_SIMD16.</para>
    ///   <para>If the size of the struct is already known call <see cref="structSizeMightRepresentSimdType" /> to determine if this api needs to be called.</para>
    /// </remarks>
    public unsafe var_types impNormStructType(CORINFO_CLASS_HANDLE structHnd, out var_types pSimdBaseJitType)
    {
        Unsafe.SkipInit(out pSimdBaseJitType);

        assert(structHnd != NO_CLASS_HANDLE);
        var structType = TYP_STRUCT;

#if FEATURE_SIMD
        var structFlags = info.compCompHnd->getClassAttribs(structHnd);

        // Don't bother if the struct contains GC references of byrefs, it can't be a SIMD type.
        if ((structFlags & (CORINFO_FLG_CONTAINS_GC_PTR | CORINFO_FLG_BYREF_LIKE)) == 0)
        {
            var originalSize = info.compCompHnd->getClassSize(structHnd);

            if (structSizeMightRepresentSimdType(originalSize))
            {
                var simdBaseType = getBaseTypeAndSizeOfSimdType(structHnd, out var sizeBytes);

                if (simdBaseType != TYP_UNDEF)
                {
                    assert((sizeBytes == originalSize) || (sizeBytes is SIZE_UNKNOWN));
                    structType = GetSimdTypeForSize(sizeBytes);

                    if (!Unsafe.IsNullRef(in pSimdBaseJitType))
                    {
                        pSimdBaseJitType = simdBaseType;
                    }

                    // Also indicate that we use floating point registers.
                    compFloatingPointUsed = true;
                }
            }
        }
#endif

        return structType;
    }

    public bool IsIntrinsicImplementedByUserCall(NamedIntrinsic intrinsicName)
    {
        // Currently, if a math intrinsic is not implemented by target-specific
        // instructions, it will be implemented by a System.Math call. In the
        // future, if we turn to implementing some of them with helper calls,
        // this predicate needs to be revisited.
        return !IsTargetIntrinsic(intrinsicName);
    }

    public bool IsMathIntrinsic(NamedIntrinsic intrinsicName)
    {
        switch (intrinsicName)
        {
            case NI_System_Math_Abs:
            case NI_System_Math_Acos:
            case NI_System_Math_Acosh:
            case NI_System_Math_Asin:
            case NI_System_Math_Asinh:
            case NI_System_Math_Atan:
            case NI_System_Math_Atanh:
            case NI_System_Math_Atan2:
            case NI_System_Math_Cbrt:
            case NI_System_Math_Ceiling:
            case NI_System_Math_Cos:
            case NI_System_Math_Cosh:
            case NI_System_Math_Exp:
            case NI_System_Math_Floor:
            case NI_System_Math_FusedMultiplyAdd:
            case NI_System_Math_ILogB:
            case NI_System_Math_Log:
            case NI_System_Math_Log2:
            case NI_System_Math_Log10:
            case NI_System_Math_Max:
            case NI_System_Math_MaxMagnitude:
            case NI_System_Math_MaxMagnitudeNumber:
            case NI_System_Math_MaxNative:
            case NI_System_Math_MaxNumber:
            case NI_System_Math_MaxUnsigned:
            case NI_System_Math_Min:
            case NI_System_Math_MinMagnitude:
            case NI_System_Math_MinMagnitudeNumber:
            case NI_System_Math_MinNative:
            case NI_System_Math_MinNumber:
            case NI_System_Math_MinUnsigned:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_Pow:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            case NI_System_Math_Round:
            case NI_System_Math_Sin:
            case NI_System_Math_Sinh:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Tan:
            case NI_System_Math_Tanh:
            case NI_System_Math_Truncate:
            {
                assert((intrinsicName > NI_SYSTEM_MATH_START) && (intrinsicName < NI_SYSTEM_MATH_END));
                return true;
            }

            default:
            {
                assert((intrinsicName < NI_SYSTEM_MATH_START) || (intrinsicName > NI_SYSTEM_MATH_END));
                return false;
            }
        }
    }

    public bool IsTargetIntrinsic(NamedIntrinsic intrinsicName)
    {
        switch (intrinsicName)
        {
#if TARGET_XARCH
            case NI_System_Math_Abs:
            case NI_System_Math_Ceiling:
            case NI_System_Math_Floor:
            case NI_System_Math_Max:
            case NI_System_Math_MaxMagnitude:
            case NI_System_Math_MaxMagnitudeNumber:
            case NI_System_Math_MaxNative:
            case NI_System_Math_MaxNumber:
            case NI_System_Math_Min:
            case NI_System_Math_MinMagnitude:
            case NI_System_Math_MinMagnitudeNumber:
            case NI_System_Math_MinNative:
            case NI_System_Math_MinNumber:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            case NI_System_Math_Round:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Truncate:
            {
                return true;
            }

            case NI_System_Math_FusedMultiplyAdd:
            {
                return compOpportunisticallyDependsOn(InstructionSet_AVX2);
            }
#elif TARGET_ARM64
            case NI_System_Math_Abs:
            case NI_System_Math_Ceiling:
            case NI_System_Math_Floor:
            case NI_System_Math_FusedMultiplyAdd:
            case NI_System_Math_Max:
            case NI_System_Math_MaxMagnitude:
            case NI_System_Math_MaxMagnitudeNumber:
            case NI_System_Math_MaxNative:
            case NI_System_Math_MaxNumber:
            case NI_System_Math_Min:
            case NI_System_Math_MinMagnitude:
            case NI_System_Math_MinMagnitudeNumber:
            case NI_System_Math_MinNative:
            case NI_System_Math_MinNumber:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            case NI_System_Math_Round:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Truncate:
            {
                return true;
            }
#elif TARGET_ARM
            case NI_System_Math_Abs:
            case NI_System_Math_Sqrt:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            {
                return true;
            }
#elif TARGET_RISCV64
            case NI_System_Math_Abs:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Max:
            case NI_System_Math_MaxNumber:
            case NI_System_Math_MaxNative:
            case NI_System_Math_Min:
            case NI_System_Math_MinNumber:
            case NI_System_Math_MinNative:
            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            {
                return true;
            }

            case NI_System_Math_MinUnsigned:
            case NI_System_Math_MaxUnsigned:
            case NI_PRIMITIVE_LeadingZeroCount:
            case NI_PRIMITIVE_TrailingZeroCount:
            case NI_PRIMITIVE_PopCount:
            {
                return compOpportunisticallyDependsOn(InstructionSet_Zbb);
            }
#elif TARGET_LOONGARCH64
            case NI_System_Math_Abs:
            case NI_System_Math_Sqrt:
            case NI_System_Math_ReciprocalSqrtEstimate:
            {
                // TODO-LoongArch64: support these standard intrinsics
                return false;
            }

            case NI_System_Math_MultiplyAddEstimate:
            case NI_System_Math_ReciprocalEstimate:
            {
                return true;
            }
#elif TARGET_WASM
            case NI_System_Math_Abs:
            case NI_System_Math_Ceiling:
            case NI_System_Math_Floor:
            case NI_System_Math_Max:
            case NI_System_Math_MaxNative:
            case NI_System_Math_Min:
            case NI_System_Math_MinNative:
            case NI_System_Math_Round:
            case NI_System_Math_Sqrt:
            case NI_System_Math_Truncate:
            case NI_PRIMITIVE_LeadingZeroCount:
            case NI_PRIMITIVE_TrailingZeroCount:
            case NI_PRIMITIVE_PopCount:
            {
                return true;
            }
#endif

            default:
                return false;
        }
    }

    public unsafe NamedIntrinsic lookupNamedIntrinsic(CORINFO_METHOD_HANDLE method)
    {
        // TODO: Port Compiler.lookupNamedIntrinsic
        return NI_Illegal;
    }
}
