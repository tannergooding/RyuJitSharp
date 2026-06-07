// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    public bool hasImpEnumeratorGdvLocalMap => impInlineRoot.impEnumeratorGdvLocalMap is not null;

    public bool hasImpEnumeratorLikelyTypeMap => impInlineRoot.impEnumeratorLikelyTypeMap is not null;

    /// <summary>check whether PInvoke inlining should enabled in current method.</summary>
    /// <remarks>Checks a number of ambient conditions where we could pinvoke but choose not to</remarks>
    public bool impCanPInvokeInline => InlinePInvokeEnabled && (!opts.compDbgCode) && (compCodeOpt is not SMALL_CODE) && !opts.compNoPInvokeInlineCB;

    public NodeToUnsignedMap ImpEnumeratorGdvLocalMap
    {
        get
        {
            var enumeratorGdvLocalMap = impInlineRoot.impEnumeratorGdvLocalMap;

            if (enumeratorGdvLocalMap is null)
            {
                enumeratorGdvLocalMap = [];
                impInlineRoot.impEnumeratorGdvLocalMap = enumeratorGdvLocalMap;
            }
            return enumeratorGdvLocalMap;
        }
    }

    public VarToLikelyClassMap ImpEnumeratorLikelyTypeMap
    {
        get
        {
            var enumeratorLikelyTypeMap = impInlineRoot.impEnumeratorLikelyTypeMap;

            if (enumeratorLikelyTypeMap is null)
            {
                enumeratorLikelyTypeMap = [];
                impInlineRoot.impEnumeratorLikelyTypeMap = enumeratorLikelyTypeMap;
            }
            return enumeratorLikelyTypeMap;
        }
    }

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

    public unsafe CORINFO_CLASS_HANDLE impObjectClass
    {
        get
        {
            var objectClass = info.compCompHnd->getBuiltinClass(CLASSID_SYSTEM_OBJECT);
            assert(objectClass is not null);
            return objectClass;
        }
    }

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

    public int impStackHeight => stackState.esStackDepth;

    public unsafe CORINFO_CLASS_HANDLE impStringClass
    {
        get
        {
            var stringClass = info.compCompHnd->getBuiltinClass(CLASSID_STRING);
            assert(stringClass is not null);
            return stringClass;
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

    /// <summary>mark the call and the method, that they have a fat pointer candidate.</summary>
    /// <param name="call">fat calli candidate</param>
    /// <remarks>Spill ret_expr in the call node, because they can't be cloned.</remarks>
    public void addFatPointerCandidate(GenTreeCall call)
    {
        JITDUMP($"Marking call [{call.TreeId:D6}] as fat pointer candidate\n");

        MethodHasFatPointer = true;
        call.IsFatPointerCandidate = true;

        var helper = new SpillRetExprHelper(this);
        helper.StoreRetExprResultsInArgs(call);
    }

    private static ConfigMethodRange s_jitGuardedDevirtualizationRange;

    /// <summary>potentially mark the call as a guarded devirtualization candidate</summary>
    /// <param name="call">potential guarded devirtualization candidate</param>
    /// <param name="methodHandle">method that will be invoked if the class test succeeds</param>
    /// <param name="classHandle">class that will be tested for at runtime</param>
    /// <param name="contextHandle">context for the devirtualized method/class</param>
    /// <param name="methodAttr">attributes of the method</param>
    /// <param name="classAttr">attributes of the class</param>
    /// <param name="likelihood">odds that this class is the class seen at runtime</param>
    /// <param name="instParamLookup">lookup to use if the target signature requires an instantiation argument</param>
    /// <param name="originalMethodHandle">method handle of base method (before devirt)</param>
    /// <param name="resolvedToken">resolved token for methodHandle, used to get R2R call info; nullptr when unavailable</param>
    /// <param name="unboxedResolvedToken">resolved token for the unboxed entry, paired with pResolvedToken</param>
    /// <remarks>
    ///   <para>Call sites in rare or unoptimized code, and calls that require cookies are not marked as candidates.</para>
    ///   <para>As part of marking the candidate, the code spills GT_RET_EXPRs anywhere in any child tree, because and we need to clone all these trees when we clone the call as part of guarded devirtualization, and these IR nodes can't be cloned.</para>
    /// </remarks>
    public unsafe void addGuardedDevirtualizationCandidate(GenTreeCall call, CORINFO_METHOD_HANDLE methodHandle, CORINFO_CLASS_HANDLE classHandle, CORINFO_CONTEXT_HANDLE contextHandle, CorInfoFlag methodAttr, CorInfoFlag classAttr, int likelihood, in CORINFO_LOOKUP instParamLookup, CORINFO_METHOD_HANDLE originalMethodHandle, in CORINFO_RESOLVED_TOKEN resolvedToken, in CORINFO_RESOLVED_TOKEN unboxedResolvedToken)
    {
        // This transformation only makes sense for delegate and virtual calls
        assert(call.IsDelegateInvoke || call.IsVirtual);

        assert(compCurBB is not null);

        // Only mark calls if the feature is enabled.
        var isEnabled = JitConfig[ConfigInteger.JitEnableGuardedDevirtualization] > 0;

        if (!isEnabled)
        {
            JITDUMP($"NOT Marking call [{call.TreeId:D6}] as guarded devirtualization candidate -- disabled by jit config\n");
            return;
        }

        // Bail if not optimizing or the call site is very likely cold
        if (compCurBB.isRunRarely || opts.OptimizationDisabled)
        {
            JITDUMP($"NOT Marking call [{call.TreeId:D6}] as guarded devirtualization candidate -- rare / dbg / minopts\n");
            return;
        }

        // CT_INDIRECT calls may use the cookie, bail if so...
        //
        // If transforming these provides a benefit, we could save this off in the same way
        // we save the stub address below.
        if ((call._callType is CT_INDIRECT) && (call._callCookie.addr is not null))
        {
            JITDUMP($"NOT Marking call [{call.TreeId:D6}] as guarded devirtualization candidate -- CT_INDIRECT with cookie\n");
            return;
        }

#if DEBUG
        // See if disabled by range

        s_jitGuardedDevirtualizationRange.EnsureInit(JitConfig[ConfigString.JitGuardedDevirtualizationRange]);
        assert(!s_jitGuardedDevirtualizationRange.Error);

        if (!s_jitGuardedDevirtualizationRange.Contains(impInlineRoot.info.compMethodHash()))
        {
            JITDUMP($"NOT Marking call [{call.TreeId:D6}] as guarded devirtualization candidate -- excluded by JitGuardedDevirtualizationRange");
            return;
        }
#endif

        // We're all set, proceed with candidate creation.
        JITDUMP($"Marking call [{call.TreeId:D6}] as guarded devirtualization candidate; will guess for {(classHandle != NO_CLASS_HANDLE ? "class" : "method")} {(classHandle != NO_CLASS_HANDLE ? eeGetClassName(classHandle) : eeGetMethodFullName(methodHandle))}\n");

        MethodHasGuardedDevirtualization = true;

        // Spill off any GT_RET_EXPR subtrees so we can clone the call.
        var helper = new SpillRetExprHelper(this);
        helper.StoreRetExprResultsInArgs(call);

        // Gather some information for later. Note we actually allocate InlineCandidateInfo
        // here, as the devirtualized half of this call will likely become an inline candidate.
        var inlineCandidateInfo = new InlineCandidateInfo {
            guardedMethodHandle = methodHandle,
            guardedClassHandle = classHandle,
            likelihood = likelihood,
            exactContextHandle = contextHandle,
            originalMethodHandle = originalMethodHandle,
            guardedMethodInstParamLookup = instParamLookup,
            guardedMethodResolvedToken = resolvedToken,
            guardedMethodUnboxedResolvedToken = unboxedResolvedToken,
        };

        // If the guarded class is a value class, look for an unboxed entry point.
        //
        if ((classAttr & CORINFO_FLG_VALUECLASS) is not 0)
        {
            JITDUMP("    ... class is a value class, looking for unboxed entry\n");

            var requiresInstMethodTableArg = false;
            var unboxedEntryMethodHandle = info.compCompHnd->getUnboxedEntry(methodHandle, &requiresInstMethodTableArg);

            if (unboxedEntryMethodHandle is not null)
            {
                JITDUMP("    ... updating GDV candidate with unboxed entry info\n");
                inlineCandidateInfo.guardedMethodUnboxedEntryHandle = unboxedEntryMethodHandle;
            }
        }

        call.AddGdvCandidateInfo(this, inlineCandidateInfo);
    }

    /// <summary>Check whether two argument nodes are contiguous or not.</summary>
    /// <param name="op1"></param>
    /// <param name="op2"></param>
    /// <returns>if the argument node op1 is located before argument node op2, and they are located contiguously, then return true. Otherwise, return false.</returns>
    /// <remarks>Right now this can only check field and array. In future we should add more cases.</remarks>
    public bool areArgumentsContiguous(GenTree op1, GenTree op2)
    {
        var type = op1.Type;

        if (op2.Type != type)
        {
            return false;
        }

        assert(op1.Type is not TYP_STRUCT);

        var oper = op1.Oper;

        if (op2.Oper != oper)
        {
            return false;
        }

        if (oper.IsTrueIndir)
        {
            var op1IndirAddr = op1.AsIndir().Addr;
            var op2IndirAddr = op2.AsIndir().Addr;

            var indirAddrOper = op1IndirAddr.Oper;

            if (op2IndirAddr.Oper != indirAddrOper)
            {
                return false;
            }

            if (indirAddrOper is GT_INDEX_ADDR)
            {
                return areArrayElementsContiguous(op1IndirAddr.AsIndexAddr(), op2IndirAddr.AsIndexAddr());
            }
            if (indirAddrOper is GT_FIELD_ADDR)
            {
                return areFieldsContiguous(op1IndirAddr.AsFieldAddr(), op2IndirAddr.AsFieldAddr(), type.Size);
            }
        }
        else if (oper is GT_LCL_FLD)
        {
            return areLocalFieldsContiguous(op1.AsLclFld(), op2.AsLclFld(), type.Size);
        }
        return false;
    }

    /// <summary>Check whether two array element nodes are located contiguously or not.</summary>
    /// <param name="op1"></param>
    /// <param name="op2"></param>
    /// <returns>if the array element op1 is located before array element op2, and they are contiguous, then return true. Otherwise, return false.</returns>
    public bool areArrayElementsContiguous(GenTreeIndexAddr op1, GenTreeIndexAddr op2)
    {
        // TODO-CQ:
        //   Right this can only check array element with const number as index. In future,
        //   we should consider to allow this function to check the index using expression.

        var op1Index = op1.Index;
        var op2Index = op2.Index;

        if ((op1Index.Oper is not GT_CNS_INT) || (op2Index.Oper is not GT_CNS_INT))
        {
            return false;
        }

        if ((op1Index.AsIntCon().IconVal + 1) != op2Index.AsIntCon().IconVal)
        {
            return false;
        }

        var op1Arr = op1.Arr;
        var op2Arr = op2.Arr;

        if ((op1Arr.Oper is GT_IND) && (op2Arr.Oper is GT_IND))
        {
            var op1ArrIndirAddr = op1Arr.AsIndir().Addr;
            var op2ArrIndirAddr = op2Arr.AsIndir().Addr;

            if ((op1ArrIndirAddr.Oper is not GT_FIELD_ADDR) || (op2ArrIndirAddr.Oper is not GT_FIELD_ADDR))
            {
                return false;
            }

            return areFieldAddressesTheSame(op1ArrIndirAddr.AsFieldAddr(), op2ArrIndirAddr.AsFieldAddr());
        }
        else if ((op1Arr.Oper is GT_LCL_VAR) && (op2Arr.Oper is GT_LCL_VAR))
        {
            return (op1Arr.AsLclVar().LclNum == op2Arr.AsLclVar().LclNum);
        }

        return false;
    }

    /// <summary>Check if two field address nodes reference at the same location.</summary>
    /// <param name="op1">first field address</param>
    /// <param name="op2">second field address</param>
    /// <returns>If op1's parents node and op2's parents node are at the same location, return true. Otherwise, return false</returns>
    public unsafe bool areFieldAddressesTheSame(GenTreeFieldAddr op1, GenTreeFieldAddr op2)
    {
        assert((op1.Oper is GT_FIELD_ADDR) && (op2.Oper is GT_FIELD_ADDR));

        var op1ObjRef = op1.FldObj;
        var op2ObjRef = op2.FldObj;

        while ((op1ObjRef is not null) && (op2ObjRef is not null))
        {
            assert(varTypeIsI(op1ObjRef.Type.ActualType) && varTypeIsI(op2ObjRef.Type.ActualType));

            var oper = op1ObjRef.Oper;

            if (op2ObjRef.Oper != oper)
            {
                break;
            }

            if (((oper is GT_LCL_VAR) || op1ObjRef.IsLclVarAddr) && (op1ObjRef.AsLclVarCommon().LclNum == op2ObjRef.AsLclVarCommon().LclNum))
            {
                return true;
            }

            if (oper is GT_IND)
            {
                op1ObjRef = op1ObjRef.AsIndir().Addr;
                op2ObjRef = op2ObjRef.AsIndir().Addr;
                continue;
            }

            if (oper is GT_FIELD_ADDR)
            {
                var op1FieldAddr = op1ObjRef.AsFieldAddr();
                var op2FieldAddr = op2ObjRef.AsFieldAddr();

                if (op1FieldAddr.FldHnd == op2FieldAddr.FldHnd)
                {
                    op1ObjRef = op1FieldAddr.FldObj;
                    op2ObjRef = op2FieldAddr.FldObj;
                    continue;
                }
            }
            break;
        }

        return false;
    }

    /// <summary>Check whether two fields are contiguous.</summary>
    /// <param name="op1"></param>
    /// <param name="op2"></param>
    /// <param name="fldSize"></param>
    /// <returns>If the first field is located before second field, and they are located contiguously, then return true. Otherwise, return false.</returns>
    public bool areFieldsContiguous(GenTreeFieldAddr op1, GenTreeFieldAddr op2, int fldSize)
    {
        if ((op1.FldOffset + fldSize) != op2.FldOffset)
        {
            return false;
        }
        return areFieldAddressesTheSame(op1, op2);
    }

    /// <summary>Check whether two local field are contiguous</summary>
    /// <param name="op1"></param>
    /// <param name="op2"></param>
    /// <param name="fldSize"></param>
    /// <returns>If the first field is located before second field, and they are located contiguously, then return true. Otherwise, return false.</returns>
    public bool areLocalFieldsContiguous(GenTreeLclFld op1, GenTreeLclFld op2, int fldSize)
    {
        assert(op1.Type == op2.Type);
        return (op1.LclOffs + fldSize) == op2.LclOffs;
    }

    /// <summary>see if we can profitably guess at the class involved in an interface or virtual call.</summary>
    /// <param name="call">potential guarded devirtualization candidate</param>
    /// <param name="ilOffset">IL ofset of the call instruction</param>
    /// <param name="isInterface"></param>
    /// <param name="baseMethod">target method of the call</param>
    /// <param name="baseClass">class that introduced the target method</param>
    /// <param name="contextHandle">context handle for the call</param>
    /// <remarks>Consults with VM to see if there's a likely class at runtime, if so, adds a candidate for guarded devirtualization.</remarks>
    public unsafe void considerGuardedDevirtualization(GenTreeCall call, IL_OFFSET ilOffset, bool isInterface, CORINFO_METHOD_HANDLE baseMethod, CORINFO_CLASS_HANDLE baseClass, ref CORINFO_CONTEXT_HANDLE contextHandle)
    {
        JITDUMP($"Considering guarded devirtualization at IL offset {ilOffset} (0x{ilOffset:X})\n");

        var hasPgoData = true;

        Unsafe.SkipInit(out InlineArrayMaxGdvTypeChecks<nint> inlineLikelyClasses);
        var likelyClasses = (Span<nint>)(inlineLikelyClasses);

        Unsafe.SkipInit(out InlineArrayMaxGdvTypeChecks<nint> inlineLikelyMethods);
        var likelyMethods = (Span<nint>)(inlineLikelyMethods);

        Unsafe.SkipInit(out InlineArrayMaxGdvTypeChecks<int> inlineLikelihoods);
        var likelihoods = (Span<int>)(inlineLikelihoods);

        var originalContext = contextHandle;
        var candidatesCount = 0;

        // Remember the original context, if any.
        // We currently only get likely class guesses when there is PGO data
        // with class profiles.
        //
        if ((fgPgoClassProfiles is 0) && (fgPgoMethodProfiles is 0))
        {
            hasPgoData = false;
        }
        else
        {
            pickGDV(call, ilOffset, isInterface, likelyClasses, likelyMethods, out candidatesCount, likelihoods);

            assert((uint)candidatesCount <= MAX_GDV_TYPE_CHECKS);
            assert((uint)candidatesCount <= (uint)GetGdvMaxTypeChecks());

            if (candidatesCount is 0)
            {
                hasPgoData = false;
            }
        }

        // NativeAOT is the only target that currently supports getExactClasses-based GDV
        // where we know the exact number of classes implementing the given base in compile-time.
        // For now, let's only do this when we don't have any PGO data. In future, we should be able to benefit
        // from both.
        if (!hasPgoData && (baseClass != NO_CLASS_HANDLE) && (JitConfig[ConfigInteger.JitEnableExactDevirtualization] is not 0))
        {
            var maxTypeChecks = int.Min(GetGdvMaxTypeChecks(), MAX_GDV_TYPE_CHECKS);

            Unsafe.SkipInit(out InlineArrayMaxGdvTypeChecks<nint> inlineExactClasses);
            var exactClasses = (Span<nint>)(inlineExactClasses);

            var numExactClasses = info.compCompHnd->getExactClasses(baseClass, MAX_GDV_TYPE_CHECKS, (CORINFO_CLASS_HANDLE*)(&inlineExactClasses.e0));

            if (numExactClasses is 0)
            {
                JITDUMP($"No exact classes implementing {eeGetClassName(baseClass)}\n");
            }
            else if (numExactClasses < 0 || numExactClasses > maxTypeChecks)
            {
                JITDUMP($"Too many exact classes implementing {eeGetClassName(baseClass)} ({numExactClasses} > {maxTypeChecks})\n");
            }
            else
            {
                assert((numExactClasses > 0) && (numExactClasses <= maxTypeChecks));
                JITDUMP($"We have exactly {numExactClasses} classes implementing {eeGetClassName(baseClass)}:\n");

                for (var exactClsIdx = 0; exactClsIdx < numExactClasses; exactClsIdx++)
                {
                    var exactCls = unchecked((CORINFO_CLASS_HANDLE)(exactClasses[exactClsIdx]));
                    assert(exactCls != NO_CLASS_HANDLE);

                    var clsAttrs = info.compCompHnd->getClassAttribs(exactCls);

                    // The getExactClasses method is expected to return precise data, thus eliminating the need
                    // to check if it is stale.
                    //
                    assert((clsAttrs & CORINFO_FLG_ABSTRACT) is 0);

                    JITDUMP($"  {exactClsIdx}) {eeGetClassName(exactCls)}\n");

                    // Figure out which method will be called.
                    //
                    var dvInfo = new CORINFO_DEVIRTUALIZATION_INFO {
                        virtualMethod = baseMethod,
                        objClass = exactCls,
                        context = originalContext,
                        pResolvedTokenVirtualMethod = null,
                    };

                    JITDUMP($"GDV exact: resolveVirtualMethod (method {dspPtr(dvInfo.virtualMethod)} class {dspPtr(dvInfo.objClass)} context {dspPtr(dvInfo.context)})\n");

                    if (!info.compCompHnd->resolveVirtualMethod(&dvInfo))
                    {
                        // Maybe other candidates will be resolved.
                        // Although, we no longer can remove the fallback (we never do it currently anyway)

                        JITDUMP("Can't figure out which method would be invoked, sorry\n");
                        break;
                    }

                    var exactContext = dvInfo.tokenLookupContext;
                    var exactMethod = dvInfo.devirtualizedMethod;
                    var exactMethodAttrs = info.compCompHnd->getMethodAttribs(exactMethod);

                    // NOTE: This is currently used only with NativeAOT. In theory, we could also check if we
                    // have static PGO data to decide which class to guess first. Presumably, this is a rare case.

                    var likelyHood = 100 / numExactClasses;

                    // If numExactClasses is 3, then likelyHood is 33 and 33*3=99.
                    // Apply the error to the first guess, so we'll have [34,33,33]
                    if (exactClsIdx is 0)
                    {
                        likelyHood += 100 - likelyHood * numExactClasses;
                    }

                    addGuardedDevirtualizationCandidate(call, exactMethod, exactCls, exactContext, exactMethodAttrs, clsAttrs, likelyHood, dvInfo.instParamLookup, baseMethod, dvInfo.resolvedTokenDevirtualizedMethod, dvInfo.resolvedTokenDevirtualizedUnboxedMethod);
                }

                if (call.InlineCandidatesCount == numExactClasses)
                {
                    assert(numExactClasses > 0);
                    call._callMoreFlags |= GTF_CALL_M_GUARDED_DEVIRT_EXACT;
                    // NOTE: we have to drop this flag if we change the number of candidates before we expand.
                }

                return;
            }
        }

        if (!hasPgoData)
        {
            JITDUMP("Not guessing; no PGO and no exact classes\n");
            return;
        }

        // Iterate over the guesses
        for (var candidateId = 0; candidateId < candidatesCount; candidateId++)
        {
            var likelyClass = unchecked((CORINFO_CLASS_HANDLE)(likelyClasses[candidateId]));
            var likelyMethod = unchecked((CORINFO_METHOD_HANDLE)(likelyMethods[candidateId]));
            var likelihood = likelihoods[candidateId];

            scoped ref var instParamLookup = ref Unsafe.NullRef<CORINFO_LOOKUP>();
            scoped ref var resolvedToken = ref Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>();
            scoped ref var unboxedResolvedToken = ref Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>();

            var likelyContext = originalContext;

            CORINFO_DEVIRTUALIZATION_INFO dvInfo;
            var likelyClassAttribs = (CorInfoFlag)(0);

            if (likelyClass != NO_CLASS_HANDLE)
            {
                likelyClassAttribs = info.compCompHnd->getClassAttribs(likelyClass);

                if ((likelyClassAttribs & CORINFO_FLG_ABSTRACT) is not 0)
                {
                    // We may see an abstract likely class, if we have a stale profile.
                    // No point guessing for this.
                    JITDUMP("Not guessing for class; abstract (stale profile)\n");

                    // Continue checking other candidates, maybe some of them aren't stale.
                    break;
                }

                // Figure out which method will be called.
                dvInfo.virtualMethod = baseMethod;
                dvInfo.objClass = likelyClass;
                dvInfo.context = originalContext;
                dvInfo.pResolvedTokenVirtualMethod = null;

                JITDUMP($"GDV likely: resolveVirtualMethod (method {dspPtr(dvInfo.virtualMethod)} class {dspPtr(dvInfo.objClass)} context {dspPtr(dvInfo.context)})\n");
                var canResolve = info.compCompHnd->resolveVirtualMethod(&dvInfo);

                if (!canResolve)
                {
                    // Continue checking other candidates, maybe some of them will succeed.
                    JITDUMP($"Can't figure out which method would be invoked, sorry. [{DevirtualizationDetailToString(dvInfo.detail)}]\n");
                    break;
                }

                likelyContext = dvInfo.tokenLookupContext;
                likelyMethod = dvInfo.devirtualizedMethod;
                resolvedToken = ref dvInfo.resolvedTokenDevirtualizedMethod;
                unboxedResolvedToken = ref dvInfo.resolvedTokenDevirtualizedUnboxedMethod;
                instParamLookup = ref dvInfo.instParamLookup;
            }
            else
            {
                likelyContext = MAKE_METHODCONTEXT(likelyMethod);
            }

            var likelyMethodAttribs = info.compCompHnd->getMethodAttribs(likelyMethod);

            if (likelyClass == NO_CLASS_HANDLE)
            {
                // We don't support multiple candidates for method guessing yet.
                assert(candidateId is 0);

                // For method GDV do a few more checks that we get for free in the
                // resolve call above for class-based GDV.
                if ((likelyMethodAttribs & CORINFO_FLG_STATIC) is not 0)
                {
                    assert((fgPgoSource is not ICorJitInfo.PgoSource.Dynamic) || call.IsDelegateInvoke);
                    JITDUMP("Cannot currently handle devirtualizing static delegate calls, sorry\n");
                    break;
                }

                var definingClass = info.compCompHnd->getMethodClass(likelyMethod);
                likelyClassAttribs = info.compCompHnd->getClassAttribs(definingClass);

                // For instance methods on value classes we need an extended check to
                // check for the unboxing stub. This is NYI.
                // Note: For dynamic PGO likelyMethod above will be the unboxing stub
                // which would fail GDV for other reasons.
                // However, with static profiles or textual PGO input it is still
                // possible that likelyMethod is not the unboxing stub. So we do need
                // this explicit check.
                if ((likelyClassAttribs & CORINFO_FLG_VALUECLASS) is not 0)
                {
                    JITDUMP("Cannot currently handle devirtualizing delegate calls on value types, sorry\n");
                    break;
                }

                // Verify that the call target and args look reasonable so that the JIT
                // does not blow up during inlining/call morphing.
                //
                // NOTE: Once we want to support devirtualization of delegate calls to
                // static methods and remove the check above we will start failing here
                // for delegates pointing to static methods that have the first arg
                // bound. For example:
                //
                // public static void E(this C c) ...
                // Action a = new C().E;
                //
                // The delegate instance looks exactly like one pointing to an instance
                // method in this case and the call will have zero args while the
                // signature has 1 arg.
                //
                if (!isCompatibleMethodGdv(call, likelyMethod))
                {
                    JITDUMP("Target for method-based GDV is incompatible (stale profile?)\n");
                    assert((fgPgoSource is not ICorJitInfo.PgoSource.Dynamic), "Unexpected stale profile in dynamic PGO data");
                    break;
                }
            }

#if DEBUG
            JITDUMP($"{(isInterface ? "interface" : call.IsDelegateInvoke ? "delegate" : "virtual")} call would invoke method {eeGetMethodFullName(likelyMethod, includeReturnType: true, includeThisSpecifier: true)}\n");
#endif

            // Add this as a potential candidate.
            addGuardedDevirtualizationCandidate(call, likelyMethod, likelyClass, likelyContext, likelyMethodAttribs, likelyClassAttribs, likelihood, instParamLookup, baseMethod, resolvedToken, unboxedResolvedToken);
        }
    }

    public unsafe int GetGdvMaxTypeChecks()
    {
        var typeChecks = JitConfig[ConfigInteger.JitGuardedDevirtualizationMaxTypeChecks];

        if (typeChecks < 0)
        {
            // Negative value means "it's up to JIT to decide"
            if (IsTargetAbi(CORINFO_NATIVEAOT_ABI) && !opts.jitFlags->IsSet(JitFlags.JIT_FLAG_SIZE_OPT))
            {
                return 3;
            }

            // We plan to use 3 for CoreCLR too, but we need to make sure it doesn't regress performance
            // as CoreCLR heavily relies on Dynamic PGO while for NativeAOT we *usually* don't have it and
            // can only perform the "exact" devirtualization.
            return 1;
        }

        // MAX_GDV_TYPE_CHECKS is the upper limit. The constant can be changed, we just suspect that even
        // 4 type checks is already too much.
        return int.Min(MAX_GDV_TYPE_CHECKS, typeChecks);
    }

    /// <summary>find pointer to context for runtime lookup.</summary>
    /// <param name="kind">lookup kind.</param>
    /// <returns>Return GenTree pointer to generic shared context.</returns>
    /// <remarks>Reports about generic context using.</remarks>
    public GenTree getRuntimeContextTree(CORINFO_RUNTIME_LOOKUP_KIND kind)
    {
        // Collectible types requires that for shared generic code, if we use the generic context parameter
        // that we report it. Conservatively mark the root method as using generic context, MARK_LOCAL_VARS phase
        // will clean it up if it turns out to be unnecessary.
        impInlineRoot.lvaGenericsContextInUse = true;

        // Always use generic context from the callsite if we're inlining and it's available.
        if (compIsForInlining && (impInlineInfo.inlInstParamArgInfo is not null))
        {
            // Create a dummy lclInfo node, we know that nobody's going to do stloc or take address of the generic context, so we don't need to scan IL for it.
            var lclInfo = new InlLclVarInfo {
                lclTypeInfo = TYP_I_IMPL,
            };

            var ctxTree = impInlineFetchArg(ref impInlineInfo.inlInstParamArgInfo[0], lclInfo);
            assert(ctxTree.Type is TYP_I_IMPL);

            // We don't need to worry about GTF_VAR_CONTEXT here, it should be set on the callsite anyway.
            return ctxTree;
        }
        else if (kind == CORINFO_LOOKUP_THISOBJ)
        {
            GenTree ctxTree;

            // Use "this" from the callsite if we're inlining
            if (compIsForInlining)
            {
                // "this" is always the first argument in inlArgInfo
                assert(impInlineInfo.argCnt > 0);
                assert(impInlineInfo.inlArgInfo[0].argIsThis);

                ctxTree = impInlineFetchArg(ref impInlineInfo.inlArgInfo[0], impInlineInfo.lclVarInfo[0]);

                // "this" is expected to be always a local, and we must mark it as a context
                assert(ctxTree.Oper is GT_LCL_VAR);
                ctxTree.Flags |= GTF_VAR_CONTEXT;
            }
            else
            {
                assert(info.compThisArg is not BAD_VAR_NUM);
                ctxTree = gtNewLclvNode(TYP_REF, info.compThisArg);
                ctxTree.Flags |= GTF_VAR_CONTEXT;
            }

            // context is the method table pointer of the this object
            return gtNewMethodTableLookup(ctxTree);
        }
        else
        {
            // Exact method descriptor as passed in
            assert(kind is CORINFO_LOOKUP_METHODPARAM or CORINFO_LOOKUP_CLASSPARAM);

            var ctxTree = gtNewLclvNode(TYP_I_IMPL, impInlineRoot.info.compTypeCtxtArg);
            ctxTree.Flags |= GTF_VAR_CONTEXT;
            return ctxTree;
        }
    }

#if FEATURE_SIMD
    /// <summary>Checking whether the field belongs to a simd struct or not. If it is, return the GenTree* for the struct node, also base type, field index and simd size. If it is not, just return  nullptr. Usually if the tree node is from a simd lclvar which is not used in any simd intrinsic, then we should return nullptr, since in this case we should treat simd struct as a regular struct. However if no matter what, you just want get simd struct node, you can set the ignoreUsedInSimdIntrinsic as true. Then there will be no IsUsedInSimdIntrinsic checking, and it will return simd struct node if the struct is a simd struct.</summary>
    /// <param name="tree">This node will be checked to see this is a field which belongs to a simd struct used for simd intrinsic or not.</param>
    /// <param name="index">if the tree is used for simd intrinsic, we will set this to the index number of this field.</param>
    /// <param name="simdSize">if the tree is used for simd intrinsic, set this to the simd struct size which this tree belongs to.</param>
    /// <param name="ignoreUsedInSimdIntrinsic">If this is set to true, then this function will ignore the UsedInSimdIntrinsic check.</param>
    /// <returns>A node which points the simd lclvar tree belongs to. If the tree is not the simd instrinic related field, return nullptr.</returns>
    public GenTreeLclFld? getSimdStructFromField(GenTree tree, out int index, out int simdSize, bool ignoreUsedInSimdIntrinsic = false)
    {
        index = 0;
        simdSize = 0;

        if (tree.Oper.IsTrueIndir)
        {
            var addr = tree.AsIndir().Addr;

            if (addr.Oper is not GT_FIELD_ADDR)
            {
                return null;
            }

            var fieldAddr = addr.AsFieldAddr();

            if (!fieldAddr.IsInstance)
            {
                return null;
            }

            var objRef = fieldAddr.FldObj;

            if (objRef.IsLclVarAddr)
            {
                var lclVarAddr = objRef.AsLclFld();
                ref var varDsc = ref lvaGetDesc(lclVarAddr.LclNum);

                if (varTypeIsSimd(varDsc.Type) && (varDsc.lvIsUsedInSimdIntrinsic || ignoreUsedInSimdIntrinsic))
                {
                    var elementType = tree.Type;
                    var fieldOffset = addr.AsFieldAddr().FldOffset;
                    var elementSize = elementType.Size;

                    if (varTypeIsArithmetic(elementType) && ((fieldOffset % elementSize) == 0))
                    {
                        simdSize = varDsc.lvExactSize;
                        index = fieldOffset / elementSize;
                        return lclVarAddr;
                    }
                }
            }
        }
        return null;
    }
#endif

    /// <summary>create methodPointerInfo into jit-allocated memory and init it.</summary>
    /// <param name="token">init value for the allocated token.</param>
    /// <param name="tokenConstrained">init value for the constraint associated with the token</param>
    /// <returns></returns>
    public methodPointerInfo impAllocateMethodPointerInfo(in CORINFO_RESOLVED_TOKEN token, mdToken tokenConstrained) => new methodPointerInfo {
        _token = token,
        _tokenConstraint = tokenConstrained
    };

    /// <summary>Set some flags on a field indirection.</summary>
    /// <param name="indir">The field indirection node</param>
    /// <remarks>Exists to preserve previous behavior. New code should not call this.</remarks>
    public void impAnnotateFieldIndir(GenTreeIndir indir)
    {
        var addr = indir.Addr;

        if (addr.Oper is GT_FIELD_ADDR)
        {
            var fieldAddr = addr.AsFieldAddr();

            if (fieldAddr.IsInstance && (fieldAddr.FldObj.Oper is GT_LCL_ADDR))
            {
                indir.Flags &= ~GTF_GLOB_REF;
            }
            else
            {
                assert((indir.Flags & GTF_GLOB_REF) is not 0);
            }
            addr.Flags |= GTF_FLD_DEREFERENCED;
        }
    }

    /// <summary>Add the statement to the current stmts list.</summary>
    /// <param name="stmt">the statement to add.</param>
    public void impAppendStmt(Statement stmt)
    {
        if (impStmtList is null)
        {
            // The stmt is the first in the list.
            impStmtList = stmt;
        }
        else
        {
            // Append the expression statement to the existing list.
            assert(impLastStmt is not null);
            impLastStmt.NextStmt = stmt;
            stmt.PrevStmt = impLastStmt;
        }
        impLastStmt = stmt;
    }

    /// <summary>Append the given statement to the current block's tree list.</summary>
    /// <param name="stmt">The statement to add.</param>
    /// <param name="chkLevel">[0..chkLevel) is the portion of the stack which we will check for interference with stmt and spilled if needed.</param>
    /// <param name="checkConsumedDebugInfo">Whether to check for consumption of impCurStmtDI. impCurStmtDI marks the debug info of the current boundary and is set when we start importing IL at that boundary. If this parameter is true, then the function checks if 'stmt' has been associated with the current boundary, and if so, clears it so that we do not attach it to more upcoming statements.</param>
    public void impAppendStmt(Statement stmt, int chkLevel, bool checkConsumedDebugInfo = true)
    {
        if (chkLevel == CHECK_SPILL_ALL)
        {
            chkLevel = stackState.esStackDepth;
        }

        if ((chkLevel is not 0) && (chkLevel != CHECK_SPILL_NONE))
        {
            assert(chkLevel <= stackState.esStackDepth);

            // If the statement being appended has any side-effects, check the stack to see if anything
            // needs to be spilled to preserve correct ordering.
            //
            var expr = stmt.RootNode;
            var oper = expr.Oper;
            var flags = expr.Flags & GTF_GLOB_EFFECT;

            // Stores to unaliased locals require special handling. Here, we look for trees that
            // can modify them and spill the references. In doing so, we make two assumptions:
            //
            // 1. All locals which can be modified indirectly are marked as address-exposed or with
            //    "lvHasLdAddrOp" -- we will rely on "impSpillSideEffects(spillGlobEffects: true)"
            //    below to spill them.
            // 2. Trees that assign to unaliased locals are always top-level (this avoids having to
            //    walk down the tree here), and are a subset of what is recognized here.
            //
            // If any of the above are violated (say for some temps), the relevant code must spill
            // things manually.

            ref var dstVarDsc = ref Unsafe.NullRef<LclVarDsc>();

            if (oper.IsLocalStore)
            {
                dstVarDsc = lvaGetDesc(expr.AsLclVarCommon().LclNum);
            }
            else if (oper is GT_CALL or GT_RET_EXPR) // The special case of calls with return buffers.
            {
                var call = (oper is GT_RET_EXPR) ? expr.AsRetExpr().InlineCandidate.AsCall() : expr.AsCall();

                if ((call.Type is TYP_VOID) && call.ShouldHaveRetBufArg)
                {
                    var args = call.Args;
                    assert(args.HasRetBuffer);

                    var retBuf = args.RetBufferArg.Node;

                    assert(retBuf.Type is TYP_I_IMPL or TYP_BYREF);

                    if (retBuf.Oper is GT_LCL_ADDR)
                    {
                        dstVarDsc = ref lvaGetDesc(retBuf.AsLclVarCommon().LclNum);
                    }
                }
            }

            // In the case of GT_RET_EXPR any subsequent spills will appear in the wrong place -- after
            // the call. We need to move them to before the call
            //
            var lastStmt = impLastStmt;

            if (!Unsafe.IsNullRef(in dstVarDsc) && !dstVarDsc.IsAddressExposed && !dstVarDsc.lvHasLdAddrOp)
            {
                impSpillLclRefs(lvaGetLclNum(dstVarDsc), chkLevel);

                if (expr.Oper.IsLocalStore)
                {
                    // For stores, limit the checking to what the value could modify/interfere with.
                    var value = expr.AsLclVarCommon().Data;
                    flags = value.Flags & GTF_GLOB_EFFECT;

                    // We don't mark indirections off of "aliased" locals with GLOB_REF, but they must still be
                    // considered as such in the interference checking.
                    if (((flags & GTF_GLOB_REF) is 0) && !impIsAddressInLocal(value) && gtHasLocalsWithAddrOp(value))
                    {
                        flags |= GTF_GLOB_REF;
                    }
                }
            }

            if (flags is not 0)
            {
                impSpillSideEffects((flags & (GTF_ASG | GTF_CALL)) is not 0, chkLevel, "impAppendStmt");
            }
            else
            {
                impSpillSpecialSideEff();
            }

            assert(impLastStmt is not null);

            if ((lastStmt != impLastStmt) && (oper is GT_RET_EXPR))
            {
                var call = expr.AsRetExpr().InlineCandidate;
                JITDUMP($"\nimpAppendStmt: after sinking a local struct store into inline candidate [{call.TreeId:D6}], we need to reorder subsequent spills.\n");

                // Move all newly appended statements to just before the call's statement.
                // First, find the statement containing the call.

                assert(lastStmt is not null);
                var insertBeforeStmt = lastStmt;

                while (insertBeforeStmt.RootNode != call)
                {
                    assert(insertBeforeStmt != impStmtList);
                    insertBeforeStmt = insertBeforeStmt.PrevStmt;
                    assert(insertBeforeStmt is not null);
                }

                var movingStmt = lastStmt.NextStmt;
                assert(movingStmt is not null);

                JITDUMP($"Moving {FMT_STMT(movingStmt.Id)} through {FMT_STMT(impLastStmt.Id)} before {FMT_STMT(insertBeforeStmt.Id)}\n");

                // We move these backwards, so must keep moving the insert point to keep them in order.
                while (impLastStmt != lastStmt)
                {
                    movingStmt = impExtractLastStmt();
                    impInsertStmtBefore(movingStmt, insertBeforeStmt);
                    insertBeforeStmt = movingStmt;
                }
            }
        }

        impAppendStmtCheck(stmt, chkLevel);
        impAppendStmt(stmt);

#if FEATURE_SIMD
        impMarkContiguousSimdFieldStores(stmt);
#endif

        // Once we set the current offset as debug info in an appended tree, we are
        // ready to report the following offsets. Note that we need to compare
        // offsets here instead of debug info, since we do not set the "is call"
        // bit in impCurStmtDI.
        assert(impLastStmt is not null);

        if (checkConsumedDebugInfo && (impLastStmt.DebugInfo.Location.Offset == impCurStmtDI.Location.Offset))
        {
            impCurStmtOffsSet(BAD_IL_OFFSET);
        }

#if DEBUG
        impLastILoffsStmt ??= stmt;

        if (verbose)
        {
            jitprintf("\n\n");
            gtDispStmt(stmt);
        }
#endif
    }

    /// <summary>Check that storing the given tree doesnt mess up the semantic order.</summary>
    /// <param name="stmt"></param>
    /// <param name="chkLevel"></param>
    /// <remarks>Note that this has only limited value as we can only check [0..chkLevel).</remarks>
    [Conditional("DEBUG")]
    public void impAppendStmtCheck(Statement stmt, int chkLevel)
    {
        if (chkLevel == CHECK_SPILL_ALL)
        {
            chkLevel = stackState.esStackDepth;
        }

        if (stackState.esStackDepth is 0 || chkLevel is 0 || chkLevel == CHECK_SPILL_NONE)
        {
            return;
        }
        var stack = stackState.esStack.AsSpan(0, chkLevel);

        var tree = stmt.RootNode;
        var flags = tree.Flags;

        // Calls can only be appended if there are no GTF_GLOB_EFFECT on the stack

        if ((flags & GTF_CALL) is not 0)
        {
            for (var level = 0; level < stack.Length; level++)
            {
                assert((stack[level].val.Flags & GTF_GLOB_EFFECT) is 0);
            }
        }

        var oper = tree.Oper;

        if (oper.IsStore)
        {
            // For a store to a local variable, all references of that variable have to be spilled.
            // If it is aliased, all calls and indirect accesses have to be spilled.
            if (oper.IsLocalStore)
            {
                var lclNum = tree.AsLclVarCommon().LclNum;

                for (var level = 0; level < stack.Length; level++)
                {
                    var stkTree = stack[level].val;
                    assert(!gtHasRef(stkTree, lclNum) || impIsInvariant(stkTree));
                    assert(!lvaTable[lclNum].IsAddressExposed || ((stkTree.Flags & GTF_SIDE_EFFECT) is 0));
                }
            }
            // If the access may be to global memory, all side effects have to be spilled.
            else if ((flags & GTF_GLOB_REF) is not 0)
            {
                for (var level = 0; level < stack.Length; level++)
                {
                    assert((stack[level].val.Flags & GTF_GLOB_REF) is 0);
                }
            }
        }
    }

    /// <summary>Append the given expression tree to the current block's tree list.</summary>
    /// <param name="tree">The tree that will be the root of the newly created statement.</param>
    /// <param name="chkLevel">[0..chkLevel) is the portion of the stack which we will check for interference with stmt and spill if needed.</param>
    /// <param name="di">Debug information to associate with the statement.</param>
    /// <param name="checkConsumedDebugInfo">Whether to check for consumption of impCurStmtDI. impCurStmtDI marks the debug info of the current boundary and is set when we start importing IL at that boundary. If this parameter is true, then the function checks if 'stmt' has been associated with the current boundary, and if so, clears it so that we do not attach it to more upcoming statements.</param>
    /// <returns>The newly created statement.</returns>
    public Statement impAppendTree(GenTree tree, int chkLevel, in DebugInfo di, bool checkConsumedDebugInfo = true)
    {
        // Allocate an 'expression statement' node
        var stmt = gtNewStmt(tree, di);

        // Append the statement to the current block's stmt list
        impAppendStmt(stmt, chkLevel, checkConsumedDebugInfo);
        return stmt;
    }

    /// <summary>try to replace a multi-dimensional array intrinsics with IR nodes.</summary>
    /// <param name="clsHnd">handle for the intrinsic method's class</param>
    /// <param name="sigInfo">signature of the intrinsic method</param>
    /// <param name="memberRef">the token for the intrinsic method</param>
    /// <param name="readonlyCall">true if call has a readonly prefix</param>
    /// <param name="intrinsicName">the intrinsic to expand: one of NI_Array_Address, NI_Array_Get, NI_Array_Set</param>
    /// <returns>The intrinsic expansion, or null if the expansion was not done (and a function call should be made instead).</returns>
    public unsafe GenTree? impArrayAccessIntrinsic(CORINFO_CLASS_HANDLE clsHnd, in CORINFO_SIG_INFO sigInfo, int memberRef, bool readonlyCall, NamedIntrinsic intrinsicName)
    {
        assert(intrinsicName is NI_Array_Address or NI_Array_Get or NI_Array_Set);

        // If we are generating SMALL_CODE, we don't want to use intrinsics, as it generates fatter code.
        if (compCodeOpt == SMALL_CODE)
        {
            JITDUMP("impArrayAccessIntrinsic: rejecting array intrinsic due to SMALL_CODE\n");
            return null;
        }

        var rank = (intrinsicName is NI_Array_Set) ? (sigInfo.numArgs - 1) : sigInfo.numArgs;

        // The rank 1 case is special because it has to handle two array formats. We will simply not do that case.
        if (rank <= 1)
        {
            JITDUMP($"impArrayAccessIntrinsic: rejecting array intrinsic because rank ({rank}) <= 1\n");
            return null;
        }

        var elemClsHnd = NO_CLASS_HANDLE;
        var elemJitType = info.compCompHnd->getChildType(clsHnd, &elemClsHnd);
        var elemType = TypeHandleToVarType(elemJitType, elemClsHnd, out var elemLayout);

        // For the ref case, we will only be able to inline if the types match
        // (verifier checks for this, we don't care for the nonverified case and the
        // type is final (so we don't need to do the cast))
        if ((intrinsicName != NI_Array_Get) && !readonlyCall && varTypeIsGC(elemType))
        {
            // Get the call site signature
            eeGetCallSiteSig(memberRef, info.compScopeHnd, impTokenLookupContextHandle, out var localSig);
            assert(localSig.hasThis());

            CORINFO_CLASS_HANDLE actualElemClsHnd;

            if (intrinsicName is NI_Array_Set)
            {
                // Fetch the last argument, the one that indicates the type we are setting.
                var argList = localSig.args;

                for (var r = 0; r < rank; r++)
                {
                    argList = info.compCompHnd->getArgNext(argList);
                }
                actualElemClsHnd = eeGetArgClass(&localSig, argList);
            }
            else
            {
                assert(intrinsicName is NI_Array_Address);
                assert(localSig.retType is CORINFO_TYPE_BYREF);
                assert(localSig.retTypeClass != NO_CLASS_HANDLE);
                _ = info.compCompHnd->getChildType(localSig.retTypeClass, &actualElemClsHnd);
            }

            // if it's not final, we can't do the optimization
            if ((info.compCompHnd->getClassAttribs(actualElemClsHnd) & CORINFO_FLG_FINAL) is 0)
            {
                JITDUMP($"impArrayAccessIntrinsic: rejecting array intrinsic because actualElemClsHnd ({dspPtr(actualElemClsHnd)}) is not final\n");
                return null;
            }
        }

        var arrayElemSize = (int)(elemType.Size);

        if (elemType is TYP_STRUCT)
        {
            assert(elemLayout is not null);
            arrayElemSize = elemLayout.Size;
        }

        var val = null as GenTree;

        if (intrinsicName is NI_Array_Set)
        {
            // Stores of structs require more work, and there are more gets than sets.
            // TODO-CQ: support SET (`a[i,j,k] = s`) for struct element arrays.
            if (varTypeIsStruct(elemType))
            {
                JITDUMP("impArrayAccessIntrinsic: rejecting SET array intrinsic because elemType is TYP_STRUCT (implementation limitation)\n");
                return null;
            }

            val = impPopStack().val;
            assert((elemType.ActualType == val.Type.ActualType) ||
                   ((elemType is TYP_FLOAT) && (val.Type is TYP_DOUBLE)) ||
                   ((elemType is TYP_INT) && (val.Type is TYP_BYREF)) ||
                   ((elemType is TYP_DOUBLE) && (val.Type is TYP_FLOAT)));
        }

        // Here, we're committed to expanding the intrinsic and creating a GT_ARR_ELEM node.
        optMethodFlags |= OMF_HAS_MDARRAYREF;

        assert(compCurBB is not null);
        compCurBB.SetFlags(BBF_HAS_MDARRAYREF);

        var inds = new GenTree[rank];

        for (var k = rank; k > 0; k--)
        {
            // The indices should be converted to `int` type, as they would be if the intrinsic was not expanded.
            var argVal = impPopStack().val;
            argVal = impImplicitIorI4Cast(argVal, TYP_INT);
            inds[k - 1] = argVal;
        }

        var arr = impPopStack().val;
        assert(arr.Type is TYP_REF);

        var arrElem = new GenTreeArrElem(TYP_BYREF, arr, arrayElemSize, inds) as GenTree;

        switch (intrinsicName)
        {
            case NI_Array_Set:
            {
                assert(!varTypeIsStruct(elemType));
                assert(val is not null);

                arrElem = gtNewStoreIndNode(elemType, arrElem, val);
                break;
            }

            case NI_Array_Get:
            {
                if (elemType is TYP_STRUCT)
                {
                    assert(elemLayout is not null);
                    arrElem = gtNewBlkIndir(arrElem, elemLayout);
                }
                else
                {
                    arrElem = gtNewIndir(elemType, arrElem);
                }
                break;
            }

            default:
            {
                break;
            }
        }
        return arrElem;
    }

    /// <summary>"&amp;var" can be used either as TYP_BYREF or TYP_I_IMPL, but we set its type to TYP_BYREF when we create it. We know if it can be changed to TYP_I_IMPL only at the point where we use it</summary>
    /// <param name="tree"></param>
    public static void impBashVarAddrsToI(GenTree tree)
    {
        if (tree.Oper is GT_LCL_ADDR)
        {
            tree.Type = TYP_I_IMPL;
        }
    }

    /// <summary>Get the tree list started for a new basic block.</summary>
    public void impBeginTreeList()
    {
        assert((impStmtList is null) && (impLastStmt is null));
    }

    /// <summary>check if a block might be in a loop</summary>
    /// <param name="block">block to check</param>
    /// <returns>true if the block might be in a loop.</returns>
    /// <remarks>Conservatively correct; may return true for some blocks that are not actually in loops.</remarks>
    public bool impBlockIsInALoop(BasicBlock block)
    {
        var result = false;

        if (compIsForInlining)
        {
            var iciBlock = impInlineInfo.iciBlock;
            assert(iciBlock is not null);
            result = iciBlock.HasFlag(BBF_BACKWARD_JUMP);
        }
        return result || block.HasFlag(BBF_BACKWARD_JUMP);
    }

    public unsafe byte impBoxPatternMatch(in CORINFO_RESOLVED_TOKEN resolvedToken, byte* codeAddr, byte* codeEndp, BoxPatterns opts)
    {
        // TODO: Port Compiler.impBoxPatternMatch
        return 0;
    }

    /// <summary>basic legality checks using information from a call to see if the call qualifies as an inline pinvoke.</summary>
    /// <param name="block">block containing the call</param>
    /// <returns>true if this call can legally qualify as an inline pinvoke, false otherwise</returns>
    public unsafe bool impCanPInvokeInlineCallSite(BasicBlock block)
    {
        // Notes:
        //    For runtimes that support exception handling interop there are
        //    restrictions on using inline pinvoke in handler regions.
        //
        //    * We have to disable pinvoke inlining inside of filters because
        //    in case the main execution (i.e. in the try block) is inside
        //    unmanaged code, we cannot reuse the inlined stub (we still need
        //    the original state until we are in the catch handler)
        //
        //    TODO-CQ: The inlining frame no longer has a GSCookie, so the common on this
        //             restriction is out of date. However, given that there is a comment
        //             about protecting the framelet, I'm not confident about what this
        //             is actually protecting, so I don't want to remove this
        //             restriction without further analysis analysis.
        //    * We disable pinvoke inlining inside handlers since the GSCookie
        //    is in the inlined Frame (see
        //    CORINFO_EE_INFO.InlinedCallFrameInfo.offsetOfGSCookie), but
        //    this would not protect framelets/return-address of handlers.
        //
        //    These restrictions are currently also in place for CoreCLR but
        //    can be relaxed when coreclr/#8459 is addressed.

        if (block.hasHndIndex)
        {
            return false;
        }

        // The following limitations do not apply to NativeAOT
        //
        if (!IsTargetAbi(CORINFO_NATIVEAOT_ABI))
        {
            // The VM assumes that the PInvoke frame in IL Stub is only going to be used
            // for the PInvoke target call. The PInvoke frame cannot be reused by marshalling helper
            // calls (see InlinedCallFrame.GetActualInteropMethodDesc and related stackwalking code).
            if (opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB))
            {
                return false;
            }

#if USE_PER_FRAME_PINVOKE_INIT
            // For platforms that use per-P/Invoke InlinedCallFrame initialization,
            // we can't inline P/Invokes inside of try blocks where we can resume execution in the same function.
            // The runtime can correctly unwind out of an InlinedCallFrame and out of managed code. However,
            // it cannot correctly unwind out of an InlinedCallFrame and stop at that frame without also unwinding
            // at least one managed frame. In particular, the runtime struggles to restore non-volatile registers
            // from the top-most unmanaged call before the InlinedCallFrame. As a result, the runtime does not support
            // re-entering the same method frame as the InlinedCallFrame after an exception in unmanaged code.
            if (block.hasTryIndex)
            {
                // Check if this block's try block or any containing try blocks have catch handlers.
                // If any of the containing try blocks have catch handlers,
                // we cannot inline a P/Invoke for reasons above. If the handler is a fault or finally handler,
                // we can inline a P/Invoke into this block in the try since the code will not resume execution
                // in the same method after throwing an exception if only fault or finally handlers are executed.
                for (var ehIndex = block.TryIndex; ehIndex != EHblkDsc.NO_ENCLOSING_INDEX; ehIndex = ehGetEnclosingTryIndex(ehIndex))
                {
                    if (ehGetDsc(ehIndex).HasCatchHandler)
                    {
                        return false;
                    }
                }
            }
#endif
        }

        if (!compIsForInlining)
        {
            return true;
        }

        // If inlining, verify conditions for the call site block too.
        assert(impInlineInfo.iciBlock is not null);
        return impInlineRoot.impCanPInvokeInlineCallSite(impInlineInfo.iciBlock);
    }

    /// <summary>Check if the specified tree can be reordered with a null check.</summary>
    /// <param name="tree">The tree</param>
    /// <returns>True if it would not be observable whether a null check threw before or after the specified node.</returns>
    public bool impCanReorderWithNullCheck(GenTree tree)
    {
        if ((tree.Flags & (GTF_PERSISTENT_SIDE_EFFECTS | GTF_ORDER_SIDEEFF)) is not 0)
        {
            return false;
        }

        if (((tree.Flags & GTF_EXCEPT) is not 0) && (gtCollectExceptions(tree) != ExceptionSetFlags.NullReferenceException))
        {
            return false;
        }
        return true;
    }

#if FEATURE_READYTORUN
    /// <summary>build and import castclass/isinst</summary>
    /// <param name="op1">value to cast</param>
    /// <param name="op2">type handle for type to cast to</param>
    /// <param name="resolvedToken">resolved token from the cast operation</param>
    /// <param name="isCastClass">true if this is castclass, false means isinst</param>
    /// <param name="booleanCheck">If true, allow creating a boolean-returning check instead of returning the object reference. Set to false if this function was not able to create a boolean check.</param>
    /// <param name="ilOffset"></param>
    /// <returns>Tree representing the cast</returns>
    /// <remarks>May expand into a series of runtime checks or a helper call.</remarks>
    public unsafe GenTree impCastClassOrIsInstToTree(GenTree op1, GenTree op2, ref CORINFO_RESOLVED_TOKEN resolvedToken, bool isCastClass, ref bool booleanCheck, IL_OFFSET ilOffset)
    {
        assert(op1.Type is TYP_REF);

        // Optimistically assume the jit should expand this as an inline test
        var isClassExact = info.compCompHnd->isExactType(resolvedToken.hClass);

        // ECMA-335 III.4.3:  If typeTok is a nullable type, Nullable<T>, it is interpreted as "boxed" T
        // We can convert constant-ish tokens of nullable to its underlying type.
        // However, when the type is shared generic parameter like Nullable<Struct<__Canon>>, the actual type will require
        // runtime lookup. It's too complex to add another level of indirection in op2, fallback to the cast helper instead.
        if (isClassExact && !eeIsSharedInst(resolvedToken.hClass))
        {
            var hClass = info.compCompHnd->getTypeForBox(resolvedToken.hClass);

            if (hClass != resolvedToken.hClass)
            {
                resolvedToken.hClass = hClass;

                var tokenHandle = impTokenToHandle(resolvedToken, out var runtimeLookup);
                assert(tokenHandle is not null);

                op2 = tokenHandle;
                assert(!runtimeLookup);
            }
        }

        CorInfoHelpFunc helper;

        fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
        {
            helper = info.compCompHnd->getCastingHelper(pResolvedToken, isCastClass);
        }

        assert(compCurBB is not null);

        var shouldExpandEarly = false;
        var tooManyLocals = (((op1.Flags & GTF_GLOB_EFFECT) is not 0) && lvaHaveManyLocals());

        if (isClassExact && opts.OptimizationEnabled && !compCurBB.isRunRarely && !tooManyLocals)
        {
            // TODO-InlineCast: Fix size regressions for these two cases if they're moved to the
            // late cast expansion path and remove this early expansion entirely.
            if (helper is CORINFO_HELP_ISINSTANCEOFCLASS)
            {
                shouldExpandEarly = true;
            }
            else if ((helper is CORINFO_HELP_ISINSTANCEOFARRAY) && op2.Oper.IsCnsIntOrI && !op2.AsIntCon().IsIconHandle(GTF_ICON_CLASS_HDL))
            {
                shouldExpandEarly = true;
            }
        }

        if (!shouldExpandEarly)
        {
            JITDUMP($"\nImporting {(isCastClass ? "castclass" : "isinst")} as call\n");

            // If we CSE this class handle we prevent assertionProp from making SubType assertions
            // so instead we force the CSE logic to not consider CSE-ing this class handle.

            op2.Flags |= GTF_DONT_CSE;

            var call = gtNewHelperCallNode(TYP_REF, helper, op2, op1);

            call._castHelperILOffset = ilOffset;

            // Instrument this castclass/isinst
            if ((JitConfig[ConfigInteger.JitClassProfiling] > 0) && impIsCastHelperEligibleForClassProbe(call) && !isClassExact && !compCurBB.isRunRarely)
            {
                // It doesn't make sense to instrument "x is T" or "(T)x" for shared T
                if (!eeIsSharedInst(resolvedToken.hClass))
                {
                    var candidateInfo = new HandleHistogramProfileCandidateInfo {
                        ilOffset = ilOffset,
                        probeIndex = info.compHandleHistogramProbeCount++,
                    };
                    call._handleHistogramProfileCandidateInfo = candidateInfo;
                    compCurBB.SetFlags(BBF_HAS_HISTOGRAM_PROFILE);
                }
            }
            else
            {
                // Leave a note for fgLateCastExpand to expand this helper call
                call._callMoreFlags |= GTF_CALL_M_CAST_CAN_BE_EXPANDED;
                call._castHelperILOffset = ilOffset;
            }

            booleanCheck = false;
            return call;
        }

        JITDUMP("\nExpanding isinst inline\n");

        impSpillSideEffects(true, CHECK_SPILL_ALL, ("bubbling "));

        // Now we import it as two QMark nodes representing this:
        //
        //  tmp = op1;
        //  if (tmp is not null) // condNull
        //  {
        //      if (tmp->pMT == op2) // condMT
        //          result = tmp;
        //      else
        //          result = null;
        //  }
        //  else
        //      result = null;
        //
        // When a boolean check is possible we create 1/0 instead of tmp/null.

        // Spill op1 if it's a complex expression
        op1 = impCloneExpr(op1, out var op1Clone, CHECK_SPILL_ALL, "ISINST eval op1");
        assert(op1Clone is not null);

        var op1Clone2 = gtClone(op1);
        assert(op1Clone2 is not null);

        var condNull = gtNewBinaryNode(GT_EQ, TYP_INT, op1Clone2, gtNewNull());
        var condMT = gtNewBinaryNode(GT_NE, TYP_INT, gtNewMethodTableLookup(op1Clone), op2);

        GenTreeQmark qmarkResult;

        if (booleanCheck)
        {
            var colon = gtNewColonNode(TYP_INT, gtNewZeroConNode(TYP_INT), gtNewOneConNode(TYP_INT));
            var qmarkMT = gtNewQmarkNode(TYP_INT, condMT, colon);
            qmarkResult = gtNewQmarkNode(TYP_INT, condNull, gtNewColonNode(TYP_INT, gtNewZeroConNode(TYP_INT), qmarkMT));
        }
        else
        {
            var op1Clone3 = gtClone(op1);
            assert(op1Clone3 is not null);

            var colon = gtNewColonNode(TYP_REF, gtNewNull(), op1Clone3);
            var qmarkMT = gtNewQmarkNode(TYP_REF, condMT, colon);
            qmarkResult = gtNewQmarkNode(TYP_REF, condNull, gtNewColonNode(TYP_REF, gtNewNull(), qmarkMT));
        }

        // Make QMark node a top level node by spilling it.
        var result = lvaGrabTemp(shortLifetime: true, "spilling qmarkNull");
        impStoreToTemp(result, qmarkResult, CHECK_SPILL_NONE);

        if (!booleanCheck)
        {
            // See also gtGetHelperCallClassHandle where we make the same
            // determination for the helper call variants.
            lvaSetClass(result, resolvedToken.hClass);
        }
        return gtNewLclvNode(qmarkResult.Type, result);
    }
#endif

    /// <summary>do more detailed checks to determine if a method can be inlined, and collect information that will be needed later</summary>
    /// <param name="call">inline candidate</param>
    /// <param name="candidateIndex">index of inline candidate in the call's inline candidate list</param>
    /// <param name="fncHandle">method that will be called</param>
    /// <param name="methAttr">attributes for the method</param>
    /// <param name="exactContextHnd">exact context for the method</param>
    /// <param name="inlinersContext">the inliner's context</param>
    /// <param name="inlineCandidateInfo">information needed later for inlining</param>
    /// <param name="inlineResult">result of ongoing inline evaluation</param>
    /// <remarks>Will update inlineResult with observations and possible failure status (if method cannot be inlined)</remarks>
    public unsafe void impCheckCanInline(GenTreeCall call, byte candidateIndex, CORINFO_METHOD_HANDLE fncHandle, CorInfoFlag methAttr, CORINFO_CONTEXT_HANDLE exactContextHnd, InlineContext inlinersContext, out InlineCandidateInfo? inlineCandidateInfo, InlineResult inlineResult)
    {
        // Either EE or JIT might throw exceptions below.
        // If that happens, just don't inline the method.

        var candidateInfo = null as InlineCandidateInfo;
        exactContextHnd = (exactContextHnd is not null) ? exactContextHnd : MAKE_METHODCONTEXT(fncHandle);

        var success = eeRunFunctorWithErrorTrap(() => {
            // Cache some frequently accessed state.
            var compCompHnd = info.compCompHnd;

            if (JitConfig[ConfigInteger.JitNoInline] is not 0)
            {
                inlineResult.NoteFatal(InlineObservation.CALLEE_IS_JIT_NOINLINE);
                return;
            }

            JITDUMP($"\nCheckCanInline: fetching method info for inline candidate {eeGetMethodName(fncHandle)} -- context {dspPtr(exactContextHnd)}\n");

            if (exactContextHnd == METHOD_BEING_COMPILED_CONTEXT())
            {
                JITDUMP("Current method context\n");
            }
            else if (((unchecked((nint)(exactContextHnd)) & (nint)(CORINFO_CONTEXTFLAGS_MASK)) == (nint)(CORINFO_CONTEXTFLAGS_METHOD)))
            {
                JITDUMP($"Method context: {eeGetMethodFullName((CORINFO_METHOD_HANDLE)(exactContextHnd))}\n");
            }
            else
            {
                JITDUMP($"Class context: {eeGetClassName(unchecked((CORINFO_CLASS_HANDLE)((nint)(exactContextHnd) & ~(nint)(CORINFO_CONTEXTFLAGS_MASK))))}\n");
            }

            // Fetch method info. This may fail, if the method doesn't have IL.

            CORINFO_METHOD_INFO methInfo;
            if (!compCompHnd->getMethodInfo(fncHandle, &methInfo, exactContextHnd))
            {
                inlineResult.NoteFatal(InlineObservation.CALLEE_NO_METHOD_INFO);
                return;
            }

            // Profile data allows us to avoid early "too many IL bytes" outs.
            assert(compCurBB is not null);

            inlineResult.NoteBool(InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS, fgHaveSufficientProfileWeights);
            inlineResult.NoteBool(InlineObservation.CALLSITE_INSIDE_THROW_BLOCK, (compCurBB.Kind is BBJ_THROW));

            var forceInline = (methAttr & CORINFO_FLG_FORCEINLINE) is not 0;

            impCanInlineIL(fncHandle, &methInfo, forceInline, inlineResult);

            if (inlineResult.IsFailure)
            {
                assert(inlineResult.IsNever);
                return;
            }

            // Speculatively check if initClass() can be done.
            // If it can be done, we will try to inline the method.
            var initClassResult = compCompHnd->initClass(field: null, fncHandle, exactContextHnd);

            if ((initClassResult & CORINFO_INITCLASS_DONT_INLINE) is not 0)
            {
                inlineResult.NoteFatal(InlineObservation.CALLSITE_CANT_CLASS_INIT);
                return;
            }

            // Given the VM the final say in whether to inline or not.
            // This should be last since for verifiable code, this can be expensive
            //
            var vmResult = compCompHnd->canInline(info.compMethodHnd, fncHandle);

            if (vmResult is INLINE_FAIL)
            {
                inlineResult.NoteFatal(InlineObservation.CALLSITE_IS_VM_NOINLINE);
            }
            else if (vmResult is INLINE_NEVER)
            {
                inlineResult.NoteFatal(InlineObservation.CALLEE_IS_VM_NOINLINE);
            }

            if (inlineResult.IsFailure)
            {
                // The VM already self-reported this failure, so mark it specially so the JIT doesn't also try reporting it.
                inlineResult.SetVMFailure();
                return;
            }

            // Get the method's class properties
            var clsHandle = compCompHnd->getMethodClass(fncHandle);
            var clsAttr = compCompHnd->getClassAttribs(clsHandle);

#if DEBUG
            // Return type
            var fncRetType = call._returnType;
            var fncRealRetType = methInfo.args.retType.VarType;

            // <BUGNUM> VSW 288602 </BUGNUM>
            // In case of IJW, we allow to assign a native pointer to a BYREF.
            assert((fncRealRetType.ActualType == fncRetType.ActualType) ||
                   ((fncRetType is TYP_BYREF) && (methInfo.args.retType is CORINFO_TYPE_PTR)) ||
                   (varTypeIsStruct(fncRetType) && (fncRealRetType is TYP_STRUCT)));
#endif

            // Allocate an InlineCandidateInfo structure,
            //
            // Or, reuse the existing GuardedDevirtualizationCandidateInfo,
            // which was pre-allocated to have extra room.

            if (call.IsGuardedDevirtualizationCandidate)
            {
                candidateInfo = call.GetGdvCandidateInfo(candidateIndex);
            }
            else
            {
                candidateInfo = new InlineCandidateInfo();
            }

            candidateInfo.methInfo = methInfo;
            candidateInfo.ilCallerHandle = info.compMethodHnd;
            candidateInfo.clsHandle = clsHandle;
            candidateInfo.exactContextHandle = exactContextHnd;
            candidateInfo.retExpr = null;
            candidateInfo.preexistingSpillTemp = BAD_VAR_NUM;
            candidateInfo.clsAttr = clsAttr;
            candidateInfo.methAttr = methAttr;
            candidateInfo.initClassResult = initClassResult;
            candidateInfo.exactContextNeedsRuntimeLookup = false;
            candidateInfo.inlinersContext = inlinersContext;
        });

        // Note exactContextNeedsRuntimeLookup is reset later on, over in impMarkInlineCandidate.
        inlineCandidateInfo = candidateInfo;

        if (!success)
        {
            inlineResult.NoteFatal(InlineObservation.CALLSITE_COMPILATION_ERROR);
        }
    }

    /// <summary>examine call to see if it is a pinvoke and if so if it can be expressed as an inline pinvoke.</summary>
    /// <param name="call">tree for the call</param>
    /// <param name="methHnd">handle for the method being called (may be null)</param>
    /// <param name="sigInfo">signature of the method being called</param>
    /// <param name="mflags">method flags for the method being called</param>
    /// <param name="block">block containing the call</param>
    /// <remarks>
    ///   <para>Sets GTF_CALL_M_PINVOKE on the call for pinvokes.</para>
    ///   <para>Also sets GTF_CALL_UNMANAGED on call for inline pinvokes if the call passes a combination of legality and profitability checks.</para>
    ///   <para>If GTF_CALL_UNMANAGED is set, increments info.compUnmanagedCallCountWithGCTransition</para>
    /// </remarks>
    public unsafe void impCheckForPInvokeCall(GenTreeCall call, CORINFO_METHOD_HANDLE methHnd, in CORINFO_SIG_INFO sigInfo, CorInfoFlag mflags, BasicBlock block)
    {
        CorInfoCallConvExtension unmanagedCallConv;

        // If VM flagged it as Pinvoke, flag the call node accordingly
        if ((mflags & CORINFO_FLG_PINVOKE) is not 0)
        {
            call._callMoreFlags |= GTF_CALL_M_PINVOKE;
        }

        var suppressGCTransition = false;

        if (methHnd is not null)
        {
            if ((mflags & CORINFO_FLG_PINVOKE) is 0)
            {
                return;
            }

            unmanagedCallConv = info.compCompHnd->getUnmanagedCallConv(methHnd, null, &suppressGCTransition);
        }
        else
        {
            if (sigInfo.getCallConv() is CORINFO_CALLCONV_DEFAULT or CORINFO_CALLCONV_VARARG)
            {
                return;
            }

            fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
            {
                unmanagedCallConv = info.compCompHnd->getUnmanagedCallConv(method: null, pSigInfo, &suppressGCTransition);
            }
            assert((call._callCookie.accessType is IAT_VALUE) && (call._callCookie.addr is null));
        }

        if (suppressGCTransition)
        {
            call._callMoreFlags |= GTF_CALL_M_SUPPRESS_GC_TRANSITION;
        }

        if ((unmanagedCallConv is CorInfoCallConvExtension.Thiscall) && (sigInfo.numArgs is 0))
        {
            BADCODE("thiscall with 0 arguments");
        }

        // If we can't get the unmanaged calling convention or the calling convention is unsupported in the JIT,
        // return here without inlining the native call.
        if (unmanagedCallConv is CorInfoCallConvExtension.Managed or CorInfoCallConvExtension.Fastcall or CorInfoCallConvExtension.FastcallMemberFunction)
        {
            return;
        }
        optNativeCallCount++;

        if (methHnd is null && (IsTargetAbi(CORINFO_NATIVEAOT_ABI) || (opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB) && !compIsForInlining)))
        {
            // PInvoke CALLI in NativeAOT ABI must be always inlined. Non-inlineable CALLI cases have been
            // converted to regular method calls earlier using convertPInvokeCalliToCall.

            // PInvoke CALLI in IL stubs must be inlined
        }
        else if (opts.jitFlags->IsSet(JitFlags.JIT_FLAG_IL_STUB) && IsReadyToRun)
        {
            // The raw PInvoke call that is inside the no marshalling R2R compiled pinvoke ILStub must
            // be inlined into the stub, otherwise we would end up with a stub that recursively calls
            // itself, and end up with a stack overflow.
        }
        else
        {
            // Check legality
            if (!impCanPInvokeInlineCallSite(block))
            {
                return;
            }

            // Legal PInvoke CALL in PInvoke IL stubs must be inlined to avoid infinite recursive
            // inlining in NativeAOT. Skip the ambient conditions checks and profitability checks.
            if (!IsTargetAbi(CORINFO_NATIVEAOT_ABI) || (info.compFlags & CORINFO_FLG_PINVOKE) is 0)
            {
                if (!impCanPInvokeInline)
                {
                    return;
                }

                // Size-speed tradeoff: don't use inline pinvoke at rarely
                // executed call sites.  The non-inline version is more
                // compact.
                //
                // Zero-diff quirk: the first clause below should simply be block->isRunRarely()
                //
                if (compIsForInlining)
                {
                    assert(impInlineInfo.iciBlock is not null);

                    if (impInlineInfo.iciBlock.isRunRarely)
                    {
                        return;
                    }
                }
                else if (block.isRunRarely)
                {
                    return;
                }
            }

            // The expensive check should be last
            var pinvokeMarshallingRequired = false;

            fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
            {
                pinvokeMarshallingRequired = info.compCompHnd->pInvokeMarshalingRequired(methHnd, pSigInfo);
            }

            if (pinvokeMarshallingRequired)
            {
                return;
            }
        }

        JITLOG(LL_INFO1000000, $"\nInline a PINVOKE call from method {info.compFullName}\n");

        call.Flags |= GTF_CALL_UNMANAGED;
        call._unmgdCallConv = unmanagedCallConv;

        if (!call.IsSuppressGCTransition)
        {
            info.compUnmanagedCallCountWithGCTransition++;
        }

        if (unmanagedCallConv is CorInfoCallConvExtension.C or CorInfoCallConvExtension.CMemberFunction)
        {
            call.Flags |= GTF_CALL_POP_ARGS;
        }
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

    /// <summary>check is it possible to spill all values from eeStack to local variables.</summary>
    /// <param name="prevOpcode">last importer opcode</param>
    /// <returns>true if it is legal, false if it could be a sequence that we do not want to divide.</returns>
    public bool impCanSpillNow(OPCODE prevOpcode)
    {
        // Don't spill after ldtoken, newarr and newobj, because it could be a part of the InitializeArray sequence.
        // Avoid breaking up to guarantee that impInitializeArrayIntrinsic can succeed.
        return prevOpcode is not CEE_LDTOKEN and not CEE_NEWARR and not CEE_NEWOBJ;
    }

    public GenTree impCloneExpr(GenTree tree, out GenTree? clone, int curLevel, string reason)
        => impCloneExpr(tree, out clone, ref Unsafe.NullRef<Statement>(), curLevel, reason);

    /// <summary>Given a tree, clone it. clone is set to the cloned tree.</summary>
    /// <param name="tree"></param>
    /// <param name="clone"></param>
    /// <param name="curLevel"></param>
    /// <param name="afterStmt"></param>
    /// <param name="reason"></param>
    /// <returns>The original tree if the cloning was easy, else returns the temp to which the tree had to be spilled to.</returns>
    /// <remarks>If the tree has side-effects, it will be spilled to a temp.</remarks>
    public GenTree impCloneExpr(GenTree tree, out GenTree? clone, ref Statement afterStmt, int curLevel, string reason)
    {
        if ((tree.Flags & GTF_GLOB_EFFECT) is 0)
        {
            clone = gtClone(tree, complexOK: true);
            return tree;
        }

        // Store the operand in a temp and return the temp
        var temp = lvaGrabTemp(shortLifetime: true, reason);

        // impStoreToTemp() may change tree->gtType to TYP_VOID for calls which
        // return a struct type. It also may modify the struct type to a more
        // specialized type (e.g. a SIMD type).  So we will get the type from
        // the lclVar AFTER calling impStoreToTemp().

        impStoreToTemp(temp, tree, ref afterStmt, curLevel, impCurStmtDI);

        var type = lvaTable[temp].Type.ActualType;
        clone = gtNewLclvNode(type, temp);

        return gtNewLclvNode(type, temp);
    }

    /// <summary>Consider whether a call should get a histogram probe and mark it if so.</summary>
    /// <param name="call">The call</param>
    /// <param name="ilOffset">The precise IL offset of the call</param>
    /// <returns>True if the call was marked such that we will add a class or method probe for it.</returns>
    public unsafe bool impConsiderCallProbe(GenTreeCall call, IL_OFFSET ilOffset)
    {
        // Possibly instrument. Note for OSR+PGO we will instrument when
        // optimizing and (currently) won't devirtualize. We may want
        // to revisit -- if we can devirtualize we should be able to
        // suppress the probe.
        //
        if (!opts.jitFlags->IsSet(JitFlags.JIT_FLAG_BBINSTR))
        {
            return false;
        }

        assert(opts.OptimizationDisabled || opts.IsInstrumentedAndOptimized);

        // During importation, optionally flag this block as one that
        // contains calls requiring class profiling. Ideally perhaps
        // we'd just keep track of the calls themselves, so we don't
        // have to search for them later.
        //
        if (compClassifyGDVProbeType(call) is GDVProbeType.None)
        {
            return false;
        }

        assert(compCurBB is not null);
        JITDUMP($"\n ... marking [{call.TreeId:D6}] in {FMT_BB(compCurBB.bbNum)} for method/class profile instrumentation\n");

        // Record some info needed for the class profiling probe.
        call._handleHistogramProfileCandidateInfo = new HandleHistogramProfileCandidateInfo {
            ilOffset = ilOffset,
            probeIndex = info.compHandleHistogramProbeCount++,
        };

        // Flag block as needing scrutiny
        compCurBB.SetFlags(BBF_HAS_HISTOGRAM_PROFILE);

        return true;
    }

    /// <summary>convert a helper call to a user call and mark it for inlining.</summary>
    /// <param name="call">the helper call to convert</param>
    /// <remarks>This is used for helper calls that are known to be backed by a user method that can be inlined.</remarks>
    public void impConvertToUserCallAndMarkForInlining(GenTreeCall call)
    {
    }

    /// <summary>Create a DebugInfo instance with the specified IL offset and 'is call' bit, using the current stack to determine whether to set the 'stack empty' bit.</summary>
    /// <param name="offs">The IL offset for the DebugInfo.</param>
    /// <param name="isCall">Whether the created DebugInfo should have the IsCall bit set.</param>
    /// <returns>The DebugInfo instance.</returns>
    public DebugInfo impCreateDIWithCurrentStackInfo(IL_OFFSET offs, bool isCall)
    {
        assert(offs != BAD_IL_OFFSET);

        var sourceTypes = ICorDebugInfo.SOURCE_TYPE_INVALID;

        if (isCall)
        {
            sourceTypes |= ICorDebugInfo.CALL_INSTRUCTION;
        }
        if (stackState.esStackDepth <= 0)
        {
            sourceTypes |= ICorDebugInfo.STACK_EMPTY;
        }
        return new DebugInfo(compInlineContext, new ILLocation(offs, sourceTypes));
    }

    /// <summary>create a GT_LCL_VAR node to access a local that might need to be normalized on load</summary>
    /// <param name="lclNum">The index into lvaTable</param>
    /// <param name="offset">The offset to associate with the node</param>
    /// <returns>The node</returns>
    public GenTreeLclVar impCreateLocalNode(int lclNum, IL_OFFSET offset)
    {
        ref var lvaDsc = ref lvaTable[lclNum];
        var lclTyp = lvaDsc.lvNormalizeOnLoad ? lvaDsc.Type : lvaDsc.Type.ActualType;
        return gtNewLclvNode(lclTyp, lclNum, offset);
    }

    public unsafe GenTree? impCreateSpanIntrinsic(in CORINFO_SIG_INFO sigInfo)
    {
        assert(sigInfo.numArgs is 1);
        assert(sigInfo.sigInst.methInstCount is 1);

        var maybeFieldTokenNode = impStackTop(0).val;

        // Verify that the field token is known and valid.  Note that it's also
        // possible for the token to come from reflection, in which case we cannot do
        // the optimization and must therefore revert to calling the helper.  You can
        // see an example of this in bvt\DynIL\initarray2.exe (in Main).

        // Check to see if the ldtoken helper call is what we see here.
        if (maybeFieldTokenNode.Oper is not GT_CALL)
        {
            return null;
        }

        var call = maybeFieldTokenNode.AsCall();

        if (!call.IsHelperCall(eeFindHelper(CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD)))
        {
            return null;
        }

        // Strip helper call away
        var callArg = call.Args.GetArgByIndex(0);
        assert(callArg is not null);
        maybeFieldTokenNode = callArg.Node;

        if (maybeFieldTokenNode.Oper is GT_IND)
        {
            maybeFieldTokenNode = maybeFieldTokenNode.AsOp().Op1;
        }

        // Check for constant
        if (maybeFieldTokenNode.Oper is not GT_CNS_INT)
        {
            return null;
        }

        var fieldTokenNode = maybeFieldTokenNode.AsIntCon();
        var fieldToken = (CORINFO_FIELD_HANDLE)(fieldTokenNode.CompileTimeHandle);

        if (!fieldTokenNode.IsIconHandle(GTF_ICON_FIELD_HDL) || (fieldToken is null))
        {
            return null;
        }

        CORINFO_CLASS_HANDLE fieldClsHnd;
        var fieldElementType = info.compCompHnd->getFieldType(fieldToken, &fieldClsHnd).VarType;

        // Most static initialization data fields are of some structure, but it is possible for them to be of various primitive types as well
        int totalFieldSize;

        if (fieldElementType is var_types.TYP_STRUCT)
        {
            totalFieldSize = info.compCompHnd->getClassSize(fieldClsHnd);
        }
        else
        {
            totalFieldSize = fieldElementType.Size;
        }

        // Limit to primitive or enum type - see ArrayNative.GetSpanDataFrom()
        var targetElemHnd = sigInfo.sigInst.methInst[0];

        if (info.compCompHnd->getTypeForPrimitiveValueClass(targetElemHnd) is CORINFO_TYPE_UNDEF)
        {
            return null;
        }

        var targetElemSize = info.compCompHnd->getClassSize(targetElemHnd);
        assert(targetElemSize is not 0);

        var count = totalFieldSize / targetElemSize;

        if (count is 0)
        {
            return null;
        }

        var data = info.compCompHnd->getArrayInitializationData(fieldToken, totalFieldSize);

        if (data is null)
        {
            return null;
        }

        // Ready to commit to the work
        _ = impPopStack();

        // Turn count and pointer value into constants.
        var lengthValue = gtNewIconNode(TYP_INT, count);
        var fldSeq = FieldSeqStore.Create(fieldToken, unchecked((nint)(data)), FieldSeq.FieldKind.SimpleStaticKnownAddress);
        var pointerValue = gtNewIconHandleNode(unchecked((nint)(data)), GTF_ICON_STATIC_HDL, fldSeq);

        // Construct ReadOnlySpan<T> to return.
        var spanHnd = sigInfo.retTypeClass;
        var spanTempNum = lvaGrabTemp(shortLifetime: true, "ReadOnlySpan<T> for CreateSpan<T>");
        lvaSetStruct(spanTempNum, spanHnd, unsafeValueClsCheck: false);

        var dataFieldStore = gtNewStoreLclFldNode(TYP_BYREF, spanTempNum, OFFSETOF__CORINFO_Span__reference, pointerValue);
        var lengthFieldStore = gtNewStoreLclFldNode(TYP_INT, spanTempNum, OFFSETOF__CORINFO_Span__length, lengthValue);

        // Now append a few statements the initialize the span
        _ = impAppendTree(lengthFieldStore, CHECK_SPILL_NONE, impCurStmtDI);
        _ = impAppendTree(dataFieldStore, CHECK_SPILL_NONE, impCurStmtDI);

        // And finally create a tree that points at the span.
        return impCreateLocalNode(spanTempNum, offset: 0);
    }

    /// <summary>Set the "current debug info" to attach to statements that we are generating next.</summary>
    /// <param name="offs">the IL offset</param>
    /// <remarks>This function will be called in the main IL processing loop when it is determined that we have reached a location in the IL stream for which we want to report debug information. This is the main way we determine which statements to report debug info for to the EE: for other statements, they will have no debug information attached.</remarks>
    public void impCurStmtOffsSet(IL_OFFSET offs)
    {
        if (offs == BAD_IL_OFFSET)
        {
            impCurStmtDI = new DebugInfo(compInlineContext, new ILLocation());
        }
        else
        {
            impCurStmtDI = impCreateDIWithCurrentStackInfo(offs, isCall: false);
        }
    }

    /// <summary>Attempt to change a virtual vtable call into a normal call</summary>
    /// <param name="call">the call node to examine/modify</param>
    /// <param name="resolvedToken">the resolved token used to create the call. Used for R2R.</param>
    /// <param name="method">the method handle for call. Updated iff call devirtualized.</param>
    /// <param name="methodFlags">flags for the method to call. Updated iff call devirtualized.</param>
    /// <param name="contextHandle">context handle for the call. Updated iff call devirtualized.</param>
    /// <param name="exactContextHandle">updated context handle iff call devirtualized</param>
    /// <param name="isLateDevirtualization">if devirtualization is happening after importation</param>
    /// <param name="isExplicitTailCall">true if we plan on using an explicit tail call</param>
    /// <param name="ilOffset">IL offset of the call</param>
    public unsafe void impDevirtualizeCall(GenTreeCall call, in CORINFO_RESOLVED_TOKEN resolvedToken, ref CORINFO_METHOD_HANDLE method, ref CorInfoFlag methodFlags, ref CORINFO_CONTEXT_HANDLE contextHandle, out CORINFO_CONTEXT_HANDLE exactContextHandle, bool isLateDevirtualization, bool isExplicitTailCall, IL_OFFSET ilOffset = BAD_IL_OFFSET)
    {
        // Notes:
        //     Virtual calls in IL will always "invoke" the base class method.
        //
        //     This transformation looks for evidence that the type of 'this'
        //     in the call is exactly known, is a final class or would invoke
        //     a final method, and if that and other safety checks pan out,
        //     modifies the call and the call info to create a direct call.
        //
        //     This transformation is initially done in the importer and not
        //     in some subsequent optimization pass because we want it to be
        //     upstream of inline candidate identification.
        //
        //     However, later phases may supply improved type information that
        //     can enable further devirtualization. We currently reinvoke this
        //     code after inlining, if the return value of the inlined call is
        //     the 'this obj' of a subsequent virtual call.
        //
        //     If devirtualization succeeds and the call's this object is a
        //     (boxed) value type, the jit will ask the EE for the unboxed entry
        //     point. If this exists, the jit will invoke the unboxed entry
        //     on the box payload. In addition if the boxing operation is
        //     visible to the jit and the call is the only consmer of the box,
        //     the jit will try analyze the box to see if the call can be instead
        //     instead made on a local copy. If that is doable, the call is
        //     updated to invoke the unboxed entry on the local copy and the
        //     boxing operation is removed.
        //
        //     When guarded devirtualization is enabled, this method will mark
        //     calls as guarded devirtualization candidates, if the type of `this`
        //     is not exactly known, and there is a plausible guess for the type.

        // This should be a devirtualization candidate.
        assert(call.IsDevirtualizationCandidate(this));
        assert(opts.OptimizationEnabled);

#if DEBUG
        // Bail if devirt is disabled.
        if (JitConfig[ConfigInteger.JitEnableDevirtualization] is 0)
        {
            exactContextHandle = null;
            return;
        }

#endif

        // Fetch information about the virtual method we're calling.
        var baseMethod = method;
        var baseMethodAttribs = methodFlags;

        if (baseMethodAttribs is 0)
        {
            // For late devirt we may not have method attributes, so fetch them.
            baseMethodAttribs = info.compCompHnd->getMethodAttribs(baseMethod);
        }
#if DEBUG
        else
        {
            // Validate that callInfo has up to date method flags
            var freshBaseMethodAttribs = info.compCompHnd->getMethodAttribs(baseMethod);

            // All the base method attributes should agree, save that
            // CORINFO_FLG_DONT_INLINE may have changed from 0 to 1
            // because of concurrent jitting activity.
            //
            // Note we don't look at this particular flag bit below, and
            // later on (if we do try and inline) we will rediscover why
            // the method can't be inlined, so there's no danger here in
            // seeing this particular flag bit in different states between
            // the cached and fresh values.
            if ((freshBaseMethodAttribs & ~CORINFO_FLG_DONT_INLINE) != (baseMethodAttribs & ~CORINFO_FLG_DONT_INLINE))
            {
                NO_WAY("mismatched method attributes");
            }
        }
#endif

        // In R2R mode, we might see virtual stub calls / gvm calls to
        // non-virtuals. For instance cases where the non-virtual method
        // is in a different assembly but is called via CALLVIRT. For
        // version resilience we must allow for the fact that the method
        // might become virtual in some update.
        //
        // In non-R2R modes CALLVIRT <nonvirtual> will be turned into a
        // regular call+nullcheck by normal call importation.
        //
        if ((baseMethodAttribs & CORINFO_FLG_VIRTUAL) is 0)
        {
            exactContextHandle = null;

            assert(call.IsVirtualStub || call.IsGenericVirtual(this));
            assert(IsAot);

            JITDUMP("\nimpDevirtualizeCall: [R2R] base method not virtual, sorry\n");
            return;
        }

        // Fetch information about the class that introduced the virtual method.
        var baseClass = info.compCompHnd->getMethodClass(baseMethod);
        var baseClassAttribs = info.compCompHnd->getClassAttribs(baseClass);

        // Is the call an interface call?
        var isInterface = (baseClassAttribs & CORINFO_FLG_INTERFACE) is not 0;

        // See what we know about the type of 'this' in the call.
        assert(call.Args.HasThisPointer);

        var thisArg = call.Args.ThisArg;
        var thisObj = thisArg.EarlyNode.EffectiveVal;

        var objClass = gtGetClassHandle(thisObj, out var isExact, out var objIsNonNull);

        // Bail if we know nothing.
        if (objClass == NO_CLASS_HANDLE)
        {
            JITDUMP($"\nimpDevirtualizeCall: no type available (op={thisObj.Oper})\n");
            exactContextHandle = null;

            if (isLateDevirtualization)
            {
                // Don't try guarded devirtualiztion when we're doing late devirtualization.
                JITDUMP("No guarded devirt during late devirtualization\n");
                return;
            }

            considerGuardedDevirtualization(call, ilOffset, isInterface, baseMethod, baseClass, ref contextHandle);
            return;
        }

        // If the objClass is sealed (final), then we may be able to devirtualize.
        var objClassAttribs = info.compCompHnd->getClassAttribs(objClass);
        var objClassIsFinal = (objClassAttribs & CORINFO_FLG_FINAL) is not 0;

#if DEBUG
        var objClassNote = "[?]";
        var objClassName = "?objClass";
        var baseMethodFullName = "?baseMethod";

        if (verbose)
        {
            objClassNote = isExact ? " [exact]" : objClassIsFinal ? " [final]" : "";
            objClassName = eeGetClassName(objClass);
            baseMethodFullName = eeGetMethodFullName(baseMethod);

            jitprintf($"\nimpDevirtualizeCall: Trying to devirtualize {(isInterface ? "interface" : "virtual")} call:\n");
            jitprintf($"    class for 'this' is {objClassName}{objClassNote} (attrib {objClassAttribs})\n");
            jitprintf($"    base method is {baseMethodFullName}\n");
        }
#endif

        // See if the jit's best type for `obj` is an interface.
        // See for instance System.ValueTuple`8.GetHashCode, where lcl 0 is System.IValueTupleInternal
        //   IL_021d:  ldloc.0
        //   IL_021e:  callvirt   instance int32 System.Object.GetHashCode()
        //
        // If so, we can't devirtualize, but we may be able to do guarded devirtualization.

        if ((objClassAttribs & CORINFO_FLG_INTERFACE) is not 0)
        {
            exactContextHandle = null;

            if (isLateDevirtualization)
            {
                // Don't try guarded devirtualiztion when we're doing late devirtualization.
                JITDUMP("No guarded devirt during late devirtualization\n");
                return;
            }

            considerGuardedDevirtualization(call, ilOffset, isInterface, baseMethod, baseClass, ref contextHandle);
            return;
        }

        // If we get this far, the jit has a lower bound class type for the `this` object being used for dispatch.
        // It may or may not know enough to devirtualize...
        if (isInterface)
        {
            assert(call.IsVirtualStub || call.IsGenericVirtual(this));
            JITDUMP("--- base class is interface\n");
        }

        // Fetch the method that would be called based on the declared type of 'this', and prepare to fetch the method attributes.

        var dvInfo = new CORINFO_DEVIRTUALIZATION_INFO {
            virtualMethod = baseMethod,
            objClass = objClass,
            context = contextHandle,
            detail = CORINFO_DEVIRTUALIZATION_UNKNOWN
        };

        fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
        {
            dvInfo.pResolvedTokenVirtualMethod = pResolvedToken;
            JITDUMP($"ResolveVirtualMethod (method {dspPtr(dvInfo.virtualMethod)} class {dspPtr(dvInfo.objClass)} context {dspPtr(dvInfo.context)})\n");
            info.compCompHnd->resolveVirtualMethod(&dvInfo);
        }

        var derivedMethod = dvInfo.devirtualizedMethod;
        var exactContext = dvInfo.tokenLookupContext;
        ref var derivedResolvedToken = ref dvInfo.resolvedTokenDevirtualizedMethod;

        var derivedMethodAttribs = (CorInfoFlag)(0);
        var derivedMethodIsFinal = false;
        var canDevirtualize = false;

#if DEBUG
        var note = "inexact or not final";
#endif

        Unsafe.SkipInit(out CORINFO_SIG_INFO derivedSig);

        // If we failed to get a method handle, we can't directly devirtualize.
        //
        // This can happen with AOT, if the devirtualization crosses
        // servicing bubble boundaries, or if objClass is a shared class.

        if (derivedMethod is null)
        {
            JITDUMP($"--- no derived method: {DevirtualizationDetailToString(dvInfo.detail)}\n");
        }
        else
        {
            // Fetch method attributes to see if method is marked final.
            derivedMethodAttribs = info.compCompHnd->getMethodAttribs(derivedMethod);
            derivedMethodIsFinal = ((derivedMethodAttribs & CORINFO_FLG_FINAL) is not 0);

            info.compCompHnd->getMethodSig(derivedMethod, &derivedSig);

            // Array interface devirt can return a nonvirtual generic method of the non-generic SZArrayHelper class.
            //
            if (derivedSig.hasTypeArg())
            {
                // If we don't know the array type exactly we may have the wrong interface type here.
                // Bail out.
                //
                var isArrayInterfaceDevirt = (objClassAttribs & CORINFO_FLG_ARRAY) is not 0;

                if (isArrayInterfaceDevirt && !isExact)
                {
                    exactContextHandle = null;
                    JITDUMP("Array interface devirt: array type is inexact, sorry.\n");
                    return;
                }
            }

#if DEBUG
            if (isExact)
            {
                note = "exact";
            }
            else if (objClassIsFinal)
            {
                note = "final class";
            }
            else if (derivedMethodIsFinal)
            {
                note = "final method";
            }

            if (verbose)
            {
                jitprintf($"    devirt to {eeGetMethodFullName(derivedMethod)} -- {note}\n");
                gtDispTree(call);
            }
#endif

            canDevirtualize = isExact || objClassIsFinal || (!isInterface && derivedMethodIsFinal);
        }

        // We still might be able to do a guarded devirtualization.
        // Note the call might be an interface call or a virtual call.
        //
        if (!canDevirtualize)
        {
            JITDUMP($"    Class not final or exact{(isInterface ? "" : ", and method not final")}\n");

#if DEBUG
            // If we know the object type exactly, we generally expect we can devirtualize.
            // (don't when doing late devirt as we won't have an owner type (yet))

            if (!isLateDevirtualization && (isExact || objClassIsFinal) && (JitConfig[ConfigInteger.JitNoteFailedExactDevirtualization] is not 0))
            {
                jitprintf($"@@@ Exact/Final devirt failure in {info.compFullName} at [{call.TreeId:D6}] $ {DevirtualizationDetailToString(dvInfo.detail)}\n");
            }
#endif

            exactContextHandle = null;

            if (isLateDevirtualization)
            {
                // Don't try guarded devirtualiztion if we're doing late devirtualization.
                JITDUMP("No guarded devirt during late devirtualization\n");
                return;
            }

            considerGuardedDevirtualization(call, ilOffset, isInterface, baseMethod, objClass, ref contextHandle);
            return;
        }

        // All checks done. Time to transform the call.

        assert(canDevirtualize);
        assert(compCurBB is not null);

        JITDUMP($"    {note}; can devirtualize\n");

        var dcInfo = new DevirtualizedCallInfo {
            tokenLookupContext = exactContext,
            instParamLookup = ref dvInfo.instParamLookup,
            resolvedToken = ref derivedResolvedToken,
            unboxedResolvedToken = ref dvInfo.resolvedTokenDevirtualizedUnboxedMethod,
            methSig = ref derivedSig,
            objIsNonNull = objIsNonNull,
            hadImplicitNullCheck = true,
            isDelegateCall = false,
            isExplicitTailCall = isExplicitTailCall,
            objClassIsExact = isExact,
            objClassIsFinal = objClassIsFinal,
            ilOffset = ilOffset,
        };
        impTransformDevirtualizedCall(call, ref derivedMethod, ref derivedMethodAttribs, in dcInfo, compCurBB, out contextHandle, baseMethod);

        method = derivedMethod;
        methodFlags = derivedMethodAttribs;

        // Update exact context handle.
        exactContextHandle = exactContext;
    }

    /// <summary>duplicates a call with a profiled argument</summary>
    /// <param name="call">call to optimize with profiled argument</param>
    /// <param name="ilOffset">Raw IL offset of the call</param>
    /// <returns>Optimized tree (or the original call tree if we can't optimize it).</returns>
    public unsafe GenTree impDuplicateWithProfiledArg(GenTreeCall call, IL_OFFSET ilOffset)
    {
        // Given `Buffer.Memmove(dst, src, len)` call,
        // optimize it to:
        //
        // if (len == popularSize)
        //     Buffer.Memmove(dst, src, popularSize); // can be unrolled now
        // else
        //     Buffer.Memmove(dst, src, len); // fallback
        //
        // if we can obtain the popular size from PGO data.

        assert(call.IsSpecialIntrinsic());
        assert(opts.IsOptimizedWithProfile);

        if (call.IsInlineCandidate)
        {
            // We decided to inline the whole thing? We won't be able to clone it then.
            return call;
        }

        Unsafe.SkipInit(out InlineArray8<LikelyValueRecord> inlineLikelyValues);
        var likelyValues = (Span<LikelyValueRecord>)(inlineLikelyValues);

        var schemas = new ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema>(fgPgoSchema, fgPgoSchemaCount);
        var valuesCount = getLikelyValues(likelyValues, schemas, fgPgoData, ilOffset);

        JITDUMP($"{valuesCount} likely values:\n");

        for (var i = 0; i < valuesCount; i++)
        {
            JITDUMP($"  {i}) {likelyValues[i].value} - {likelyValues[i].likelihood}%\n");
        }

        // For now, we only do a single guess, but it's pretty straightforward to extend it to support multiple guesses.
        var likelyValue = likelyValues[0];

#if DEBUG
        // Re-use JitRandomGuardedDevirtualization for stress-testing.
        var jitRandomGuardedDevirtualization = JitConfig[ConfigInteger.JitRandomGuardedDevirtualization];

        if (jitRandomGuardedDevirtualization is not 0)
        {
            assert(impInlineRoot._inlineStrategy is not null);
            var random = impInlineRoot._inlineStrategy.GetRandom(jitRandomGuardedDevirtualization);

            valuesCount = 1;
            likelyValue.value = random.Next(256);
            likelyValue.likelihood = 100;
        }
#endif

        // TODO: Tune the likelihood threshold, for now it's 50%
        if ((valuesCount > 0) && (likelyValue.likelihood >= 50))
        {
            var profiledValue = likelyValue.value;

            var argNum = 0;
            var minValue = 0;
            var maxValue = 0;

            if (call.IsSpecialIntrinsic(this, NI_System_SpanHelpers_Memmove))
            {
                // dst(0), src(1), len(2)
                argNum = 2;

                minValue = 1; // TODO: enable for 0 as well.
                maxValue = GetUnrollThreshold(ProfiledMemmove);
            }
            else if (call.IsSpecialIntrinsic(this, NI_System_SpanHelpers_SequenceEqual))
            {
                // dst(0), src(1), len(2)
                argNum = 2;

                minValue = 1; // TODO: enable for 0 as well.
                maxValue = GetUnrollThreshold(ProfiledMemcmp);
            }
            else
            {
                // only Memmove is expected at the moment.
                // Possible future extensions: Memset, Memcpy
                unreached();
            }

            if ((profiledValue >= minValue) && (profiledValue <= maxValue))
            {
                JITDUMP($"Duplicating for popular value = {profiledValue}\n");
                DISPTREE(call);

                var callArgs = call.Args;

                var userArg = callArgs.GetUserArgByIndex(argNum);
                assert(userArg is not null);

                if (userArg.Node.Oper.IsConst)
                {
                    JITDUMP("Profiled arg is already a constant - bail out.\n");
                    return call;
                }

                // Spill all the arguments to temp locals to preserve the execution order

                ref var argRef = ref Unsafe.NullRef<GenTree>();
                var argClone = null as GenTree;

                var userArgCount = callArgs.CountUserArgs();

                for (var i = 0; i < userArgCount; i++)
                {
                    userArg = callArgs.GetUserArgByIndex(i);
                    assert(userArg is not null);

                    ref var node = ref userArg.EarlyNodeRef;
                    var cloned = impCloneExpr(node, out node, CHECK_SPILL_ALL, "spilling arg");

                    if (i == argNum)
                    {
                        // Record the reference to the argument we're going to replace.
                        argRef = node;
                        argClone = cloned;
                    }
                }
                assert(argClone is not null);

                var fallbackCall = gtCloneExpr(call);
                var profiledValueNode = gtNewIconNode(argClone.Type, profiledValue);
                argRef = profiledValueNode;

                // TODO: Specify weights for the branches in the Qmark node.
                var colon = gtNewColonNode(call.Type, call, fallbackCall);
                var cond = gtNewBinaryNode(GT_EQ, TYP_INT, argClone, gtCloneExpr(profiledValueNode));
                var qmark = gtNewQmarkNode(call.Type, cond, colon);

                JITDUMP("\n\nResulting tree:\n");
                DISPTREE(qmark);
    
                return qmark;
            }
        }
        return call;
    }

    // Store the given start and end stmt in the given basic block. This is
    // mostly called by impEndTreeList(BasicBlock *block). It is called
    // directly only for handling CEE_LEAVEs out of finally-protected try's.

    public void impEndTreeList(BasicBlock block, Statement firstStmt, Statement? lastStmt)
    {
        // Make the list circular, so that we can easily walk it backwards
        firstStmt.PrevStmt = lastStmt;

        // Store the tree list in the basic block
        block.FirstStmt = firstStmt;

        // The block should not already be marked as imported
        assert(!block.HasFlag(BBF_IMPORTED));

        block.SetFlags(BBF_IMPORTED);
    }

    public void impEndTreeList(BasicBlock block)
    {
        if (impStmtList is null)
        {
            // The block should not already be marked as imported.
            assert(!block.HasFlag(BBF_IMPORTED));

            // Empty block. Just mark it as imported.
            block.SetFlags(BBF_IMPORTED);
        }
        else
        {
            impEndTreeList(block, impStmtList, impLastStmt);
        }

#if DEBUG
        impLastILoffsStmt?.LastILOffset = compIsForInlining ? BAD_IL_OFFSET : impCurOpcOffs;
        impLastILoffsStmt = null;
#endif

        impLastStmt = null;
        impStmtList = null;
    }

    /// <summary>Imports one of the *Estimate intrinsics which are explicitly allowed to differ in result based on the hardware they're running against</summary>
    /// <param name="method">The handle of the method being imported</param>
    /// <param name="sigInfo"></param>
    /// <param name="callJitType">The underlying type for the call</param>
    /// <param name="intrinsicName">The intrinsic being imported</param>
    /// <param name="mustExpand">true if the intrinsic must return a GenTree*; otherwise, false</param>
    /// <returns></returns>
    public unsafe GenTree? impEstimateIntrinsic(CORINFO_METHOD_HANDLE method, in CORINFO_SIG_INFO sigInfo, CorInfoType callJitType, NamedIntrinsic intrinsicName, bool mustExpand)
    {
        var callType = callJitType.VarType;
        assert(varTypeIsFloating(callType));

        if (BlockNonDeterministicIntrinsics(mustExpand))
        {
            return null;
        }

        if (IsIntrinsicImplementedByUserCall(intrinsicName))
        {
            return null;
        }

#if FEATURE_HW_INTRINSICS
        // We use compExactlyDependsOn since these are estimate APIs where
        // the behavior is explicitly allowed to differ across machines

        var simdType = TYP_UNKNOWN;
        var intrinsicId = NI_Illegal;
        var swapOp1AndOp3 = false;

        switch (intrinsicName)
        {
            case NI_System_Math_MultiplyAddEstimate:
            {
                assert(sigInfo.numArgs is 3);

#if TARGET_XARCH
                if (compExactlyDependsOn(InstructionSet_AVX2))
                {
                    simdType = TYP_SIMD16;
                    intrinsicId = NI_AVX2_MultiplyAddScalar;
                }
#elif TARGET_ARM64
                if (compExactlyDependsOn(InstructionSet_AdvSimd))
                {
                    simdType = TYP_SIMD8;
                    intrinsicId = NI_AdvSimd_FusedMultiplyAddScalar;

                    // AdvSimd.FusedMultiplyAdd expects (addend, left, right), while the APIs take (left, right, addend)

                    impSpillSideEffect(true, stackState.esStackDepth - 3, "Spilling op1 side effects for MultiplyAddEstimate");
                    impSpillSideEffect(true, stackState.esStackDepth - 2, "Spilling op2 side effects for MultiplyAddEstimate");

                    swapOp1AndOp3 = true;
                }
#endif
                break;
            }

            case NI_System_Math_ReciprocalEstimate:
            {
                assert(sigInfo.numArgs is 1);

#if TARGET_XARCH
                if (compExactlyDependsOn(InstructionSet_AVX512))
                {
                    simdType = TYP_SIMD16;
                    intrinsicId = NI_AVX512_Reciprocal14Scalar;
                }
                else if ((callType == TYP_FLOAT) && compExactlyDependsOn(InstructionSet_X86Base))
                {
                    simdType = TYP_SIMD16;
                    intrinsicId = NI_X86Base_ReciprocalScalar;
                }
#elif TARGET_ARM64
                if (compExactlyDependsOn(InstructionSet_AdvSimd_Arm64))
                {
                    simdType = TYP_SIMD8;
                    intrinsicId = NI_AdvSimd_Arm64_ReciprocalEstimateScalar;
                }
#endif
                break;
            }

            case NI_System_Math_ReciprocalSqrtEstimate:
            {
                assert(sigInfo.numArgs is 1);

#if TARGET_XARCH
                if (compExactlyDependsOn(InstructionSet_AVX512))
                {
                    simdType = TYP_SIMD16;
                    intrinsicId = NI_AVX512_ReciprocalSqrt14Scalar;
                }
                else if ((callType == TYP_FLOAT) && compExactlyDependsOn(InstructionSet_X86Base))
                {
                    simdType = TYP_SIMD16;
                    intrinsicId = NI_X86Base_ReciprocalSqrtScalar;
                }
#elif TARGET_ARM64
                if (compExactlyDependsOn(InstructionSet_AdvSimd_Arm64))
                {
                    simdType = TYP_SIMD8;
                    intrinsicId = NI_AdvSimd_Arm64_ReciprocalSquareRootEstimateScalar;
                }
#endif
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
#endif

        var op3 = null as GenTree;
        var op2 = null as GenTree;
        var op1 = null as GenTree;

        switch (sigInfo.numArgs)
        {
            case 3:
            {
                op3 = impImplicitR4orR8Cast(impPopStack().val, callType);
                goto case 2;
            }

            case 2:
            {
                op2 = impImplicitR4orR8Cast(impPopStack().val, callType);
                goto case 1;
            }

            case 1:
            {
                op1 = impImplicitR4orR8Cast(impPopStack().val, callType);
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

#if FEATURE_HW_INTRINSICS
        if (intrinsicId is not NI_Illegal)
        {
#if TARGET_XARCH
            var simdSize = (byte)(16);
#elif TARGET_ARM64
            var simdSize = (byte)((simdType is TYP_SIMD8) ? 8 : 16);
#endif

            var simdBaseType = callJitType.PreciseVarType;

            switch (sigInfo.numArgs)
            {
                case 3:
                {
                    assert(op2 is not null);
                    assert(op3 is not null);

                    if (swapOp1AndOp3)
                    {
                        (op1, op3) = (op3, op1);
                    }

                    op1 = gtNewSimdCreateScalarUnsafeNode(simdType, op1, simdBaseType, simdSize);
                    op2 = gtNewSimdCreateScalarUnsafeNode(simdType, op2, simdBaseType, simdSize);
                    op3 = gtNewSimdCreateScalarUnsafeNode(simdType, op3, simdBaseType, simdSize);

                    op1 = gtNewSimdHWIntrinsicNode(simdType, intrinsicId, simdBaseType, simdSize, op1, op2, op3);
                    break;
                }

                case 1:
                {
                    assert(!swapOp1AndOp3);
                    op1 = gtNewSimdCreateScalarUnsafeNode(simdType, op1, simdBaseType, simdSize);
                    op1 = gtNewSimdHWIntrinsicNode(simdType, intrinsicId, simdBaseType, simdSize, op1);
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            return gtNewSimdToScalarNode(callType, op1, simdBaseType, simdSize);
        }

        assert(!swapOp1AndOp3);
#endif

            callType = callType.ActualType;

        switch (intrinsicName)
        {
            case NI_System_Math_MultiplyAddEstimate:
            {
                assert(op2 is not null);
                assert(op3 is not null);

                var mulNode = gtNewBinaryNode(GT_MUL, callType, op1, op2);
                return gtNewBinaryNode(GT_ADD, callType, mulNode, op3);
            }

            case NI_System_Math_ReciprocalEstimate:
            case NI_System_Math_ReciprocalSqrtEstimate:
            {
                if (intrinsicName == NI_System_Math_ReciprocalSqrtEstimate)
                {
                    assert(!IsIntrinsicImplementedByUserCall(NI_System_Math_Sqrt));

                    op1 = new GenTreeIntrinsic(callType, op1, NI_System_Math_Sqrt, methodHandle: null);
                }
                return gtNewBinaryNode(GT_DIV, callType, gtNewDconNode(callType, 1.0), op1);
            }

            default:
            {
                unreached();
                return null;
            }
        }
    }

    /// <summary>Extract the last statement from the current stmts list.</summary>
    /// <returns>The extracted statement.</returns>
    /// <remarks>It assumes that the stmt will be reinserted later.</remarks>
    public Statement impExtractLastStmt()
    {
        assert(impLastStmt is not null);

        var stmt = impLastStmt;
        assert(stmt is not null);
        impLastStmt = impLastStmt.PrevStmt;

        if (impLastStmt is null)
        {
            impStmtList = null;
        }
        return stmt;
    }

    /// <summary>If the stack contains any trees with side effects in them, assign those trees to temps and append the stores to the statement list.</summary>
    /// <remarks>On return the stack is guaranteed to be empty.</remarks>
    public void impEvalSideEffects()
    {
        impSpillSideEffects(spillGlobEffects: false, CHECK_SPILL_ALL, "impEvalSideEffects");
        stackState.esStackDepth = 0;
    }

    /// <summary>Attempts to unroll and vectorize Equals against a constant WCHAR data for Length in [8..32] range using either SWAR or SIMD.</summary>
    /// <param name="data">Pointer (LCL_VAR) to a data to vectorize</param>
    /// <param name="lengthFld">Pointer (LCL_VAR or GT_IND) to Length field</param>
    /// <param name="checkForNull">Check data for null</param>
    /// <param name="kind">Is it StartsWith, Equals or EndsWith?</param>
    /// <param name="cnsData">Constant data (array of 2-byte chars)</param>
    /// <param name="dataOffset">Offset for data</param>
    /// <param name="cmpMode">Ordinal or OrdinalIgnoreCase mode (works only for ASCII cns)</param>
    /// <returns>A pointer to the newly created SWAR/SIMD node or nullptr if unrolling is not possible, not profitable or constant data contains non-ASCII char(s) in 'ignoreCase' mode</returns>
    public GenTree? impExpandHalfConstEquals(GenTreeLclVarCommon data, GenTree lengthFld, bool checkForNull, StringComparisonKind kind, Span<char> cnsData, int dataOffset, StringComparison cmpMode)
    {
        // In a general case it will look like this:
        //   bool equals = obj != null && obj.Length == len && (SWAR or SIMD)

        assert(compCurBB is not null);

        if (compCurBB.isRunRarely)
        {
            // Not profitable to expand
            JITDUMP("impExpandHalfConstEquals: block is cold - not profitable to expand.\n");
            return null;
        }

        var cmpOp = (kind is StringComparisonKind.Equals) ? GT_EQ : GT_GE;

        var elementsCount = gtNewIconNode(TYP_INT, cnsData.Length);
        GenTree lenCheckNode;

        if (cnsData.Length == 0)
        {
            // For zero length we don't need to compare content, the following expression is enough:
            //
            //   varData != null && lengthFld cmpOp 0
            //
            lenCheckNode = gtNewBinaryNode(cmpOp, TYP_INT, lengthFld, elementsCount);
        }
        else
        {
            assert(!cnsData.IsEmpty);

            var dataAddr = gtCloneLclVarCommon(data);

            if (kind is StringComparisonKind.EndsWith)
            {
                // For EndsWith we need to adjust dataAddr to point to the end of the string minus value's length
                // We spawn a local that we're going to set below
                var dataTmp = lvaGrabTemp(shortLifetime: true, "clonning data ptr");
                lvaGetDesc(dataTmp).Type = TYP_BYREF;
                dataAddr = gtNewLclvNode(TYP_BYREF, dataTmp);
            }

            var indirCmp = impExpandHalfConstEquals(dataAddr, cnsData, dataOffset, cmpMode);

            if (indirCmp is null)
            {
                JITDUMP("unable to compose indirCmp\n");
                return null;
            }
            assert(indirCmp.Type is TYP_INT or  TYP_UBYTE);

            if (kind is StringComparisonKind.EndsWith)
            {
                // len is expected to be small, so no overflow is possible
                assert(CheckedOps.TryMul(cnsData.Length, 2, out _));

                // dataAddr = dataAddr + (length * 2 - len * 2)
                var castedLen = gtNewCastNode(TYP_I_IMPL, gtCloneExpr(lengthFld), false, TYP_I_IMPL);
                var byteLen = gtNewBinaryNode(GT_MUL, TYP_I_IMPL, castedLen, gtNewIconNode(TYP_I_IMPL, 2));

                var cmpStart = gtNewBinaryNode(GT_SUB, TYP_I_IMPL, byteLen, gtNewIconNode(TYP_I_IMPL, cnsData.Length * 2));
                cmpStart = gtNewBinaryNode(GT_ADD, TYP_BYREF, gtCloneLclVarCommon(data), cmpStart);

                var storeTmp = gtNewTempStore(dataAddr.LclNum, cmpStart);
                indirCmp = gtNewCommaNode(indirCmp.Type, storeTmp, indirCmp);
            }

            var lenCheckColon = gtNewColonNode(TYP_INT, indirCmp, gtNewFalse());
            var lenCheckCond = gtNewBinaryNode(cmpOp, TYP_INT, lengthFld, elementsCount);

            // For StartsWith/EndsWith we use GT_GE, e.g.: `x.Length >= 10`
            lenCheckNode = gtNewQmarkNode(TYP_INT, lenCheckCond, lenCheckColon);
        }

        GenTree rootQmark;

        if (checkForNull)
        {
            // varData == nullptr
            var nullCheckColon = gtNewColonNode(TYP_INT, lenCheckNode, gtNewFalse());
            var nullCheckCond = gtNewBinaryNode(GT_NE, TYP_INT, data, gtNewNull());
            rootQmark = gtNewQmarkNode(TYP_INT, nullCheckCond, nullCheckColon);
        }
        else
        {
            // no nullcheck, just "obj.Length == len && (SWAR or SIMD)"
            rootQmark = lenCheckNode;
        }
        return rootQmark;
    }

    /// <summary>Attempts to unroll and vectorize Equals against a constant WCHAR data</summary>
    /// <param name="data">Pointer to a data to vectorize</param>
    /// <param name="cns">Constant data (array of 2-byte chars)</param>
    /// <param name="dataOffset">Offset for data</param>
    /// <param name="cmpMode">Ordinal or OrdinalIgnoreCase mode (works only for ASCII cns)</param>
    /// <returns>A tree representing unrolled comparison or nullptr if unrolling is not possible (possible only if cns contains non-ASCII char(s) in OrdinalIgnoreCase mode)</returns>
    public unsafe GenTree? impExpandHalfConstEquals(GenTreeLclVarCommon data, Span<char> cns, int dataOffset, StringComparison cmpMode)
    {
        assert(cns.Length <= MaxPossibleUnrollSize);

        // Convert charLen to byteLen. It never overflows because charLen is a small value
        var byteLen = cns.Length * 2;

        // Find the largest possible type to read data
        var readType = roundDownMaxType(byteLen, true);
        var result = null as GenTree;
        var byteLenRemaining = byteLen;

        while (byteLenRemaining > 0)
        {
            // We have a remaining data to process and it's smaller than the
            // previously processed data
            if (byteLenRemaining < readType.Size)
            {
                if (varTypeIsIntegral(readType))
                {
                    // Use a smaller GPR load for the remaining data, we're going to zero-extend it
                    // since the previous GPR load was larger. Hence, for e.g. 6 bytes we're going to do
                    // "(IND<INT> ^ cns1) | (UINT)(IND<USHORT> ^ cns2)"
                    readType = roundUpGprType(byteLenRemaining);
                }
                else
                {
                    // TODO-CQ: We should probably do the same for SIMD, e.g. 34 bytes -> SIMD32 and SIMD16
                    // while currently we do SIMD32 and SIMD32. This involves a bit more complex upcasting logic.
                }

                // Overlap with the previously processed data
                byteLenRemaining = readType.Size;
                assert(byteLenRemaining <= byteLen);
            }

            var byteOffset = byteLen - byteLenRemaining;

            // Total offset includes dataOffset (e.g. 12 for String)
            var totalOffset = byteOffset + dataOffset;

            // Clone dst and add offset if necessary.
            var absOffset = gtNewIconNode(TYP_I_IMPL, totalOffset);
            var currData = gtNewBinaryNode(GT_ADD, TYP_BYREF, gtCloneExpr(data), absOffset);
            var loadedData = gtNewIndir(readType, currData, GTF_IND_UNALIGNED | GTF_IND_ALLOW_NON_ATOMIC) as GenTree;

            // For OrdinalIgnoreCase mode we need to convert both data and cns to lower case
            if (cmpMode is StringComparison.OrdinalIgnoreCase)
            {
                Unsafe.SkipInit(out InlineArrayMaxPossibleUnrollSize<char> inlineMask);
                var mask = (Span<char>)(inlineMask);

                var maskSize = readType.Size / 2;

                if (!ConvertToLowerCase(cns[(byteOffset / 2)..], mask[..maskSize]))
                {
                    // value contains non-ASCII chars, we can't proceed further
                    return null;
                }

                // 0x20 mask for the current chunk to convert it to lower case
                var toLowerMask = gtNewGenericCon(readType, MemoryMarshal.AsBytes(mask));

                // loadedData is now "loadedData | toLowerMask"
                loadedData = BitwiseOp(this, GT_OR, readType.ActualType, loadedData, toLowerMask);
            }
            else
            {
                assert(cmpMode is StringComparison.Ordinal);
            }

            var srcCns = gtNewGenericCon(readType, MemoryMarshal.AsBytes(cns)[byteOffset..]);

            // A small optimization: prefer X == Y over X ^ Y == 0 since
            // just one comparison is needed, and we can do it with a single load.
            if ((readType.Size == byteLen) && varTypeIsIntegral(readType))
            {
                // TODO-CQ: Figure out why it's a size regression for SIMD
                return BitwiseOp(this, GT_EQ, TYP_INT, loadedData, srcCns);
            }

            // loadedData ^ srcCns
            var xorNode = BitwiseOp(this, GT_XOR, readType.ActualType, loadedData, srcCns);

            // Merge with the previous result with OR
            if (result is null)
            {
                // It's the first check
                result = xorNode;
            }
            else
            {
                if (result.Type != readType)
                {
                    assert(varTypeIsIntegral(result.Type) && varTypeIsIntegral(readType));
                    xorNode = gtNewCastNode(result.Type, xorNode, true, result.Type);
                }

                // Merge with the previous result via OR
                result = BitwiseOp(this, GT_OR, result.Type.ActualType, result, xorNode);
            }

            // Move to the next chunk.
            byteLenRemaining -= readType.Size;
        }
        assert(result is not null);

        // Compare the result against zero, e.g. (chunk1 ^ cns1) | (chunk2 ^ cns2) == 0
        return BitwiseOp(this, GT_EQ, TYP_INT, result, gtNewZeroConNode(result.Type));

        // A gtNewOperNode which can handle SIMD operands (used for bitwise operations):
        static GenTree BitwiseOp(Compiler compiler, genTreeOps oper, var_types type, GenTree op1, GenTree op2)
        {
#if FEATURE_HW_INTRINSICS
            if (varTypeIsSimd(type))
            {
                return compiler.gtNewSimdBinOpNode(oper, type, op1, op2, TYP_U_IMPL, type.Size);
            }

            if (varTypeIsSimd(op1.Type))
            {
                // E.g. a comparison of SIMD ops returning TYP_INT;
                assert(varTypeIsSimd(op2.Type));
                return compiler.gtNewSimdCmpOpAllNode(oper, type, op1, op2, TYP_U_IMPL, op1.Type.Size);
            }
#endif
            return compiler.gtNewBinaryNode(oper, type, op1, op2);
        }
    }

    /// <summary>add pred edges from finally returns to their continuations</summary>
    /// <remarks>
    ///   <para>These edges were not added during the initial pred list computation, because the initial flow graph does not contain the callfinally block pairs; those blocks are added during importation.</para>
    ///   <para>We rely on handler blocks being lexically contiguous between begin and last.</para>
    /// </remarks>
    public void impFixPredLists()
    {
        var added = false;
        var usingProfileWeights = fgIsUsingProfileWeights;

        for (ushort XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var HBtab = ref ehGetDsc(XTnum);

            if (HBtab.HasFinallyHandler)
            {
                var finallyBegBlock = HBtab.ebdHndBeg;
                var finallyLastBlock = HBtab.ebdHndLast;
                var predCount = -1;
                var finallyWeight = finallyBegBlock.bbWeight;

                foreach (var finallyBlock in new BasicBlockRangeList(finallyBegBlock, finallyLastBlock))
                {
                    if (finallyBlock.HndIndex != XTnum)
                    {
                        // Must be a nested handler... we could skip to its last
                        continue;
                    }

                    if (finallyBlock.Kind is not BBJ_EHFINALLYRET)
                    {
                        continue;
                    }

                    // Count the number of predecessors. Then we can allocate the bbEhfTargets table and fill it in.
                    // We only need to count once, since it's invariant with the finally block.
                    if (predCount == -1)
                    {
                        predCount = 0;
                        foreach (var predBlock in finallyBegBlock.PredBlocks)
                        {
                            // We only care about preds that are callfinallies.
                            if (predBlock.Kind is not BBJ_CALLFINALLY)
                            {
                                continue;
                            }
                            predCount++;
                        }
                    }

                    BBJumpTable jumpEhf;

                    if (predCount > 0)
                    {
                        var succTab = new FlowEdge[predCount];
                        var predNum = 0;
                        var remainingLikelihood = 1.0;

                        foreach (var predBlock in finallyBegBlock.PredBlocks)
                        {
                            // We only care about preds that are callfinallies.
                            //
                            if (predBlock.Kind is not BBJ_CALLFINALLY)
                            {
                                continue;
                            }

                            var continuation = predBlock.Next;
                            assert(continuation is not null);

                            var newEdge = fgAddRefPred(continuation, finallyBlock);

                            if (usingProfileWeights && (finallyWeight != BB_ZERO_WEIGHT))
                            {
                                // Derive edge likelihood from the entry block's weight relative to other entries.
                                var callFinallyWeight = predBlock.bbWeight;
                                var likelihood = weight_t.Min(callFinallyWeight / finallyWeight, 1.0);
                                newEdge.Likelihood = weight_t.Min(likelihood, remainingLikelihood);
                                remainingLikelihood = weight_t.Max(BB_ZERO_WEIGHT, remainingLikelihood - likelihood);
                            }
                            else
                            {
                                // If we don't have profile data, evenly distribute the likelihoods.
                                //
                                newEdge.Likelihood = 1.0 / predCount;
                            }

                            succTab[predNum++] = newEdge;

                            if (!added)
                            {
                                JITDUMP("\nAdding pred edges from BBJ_EHFINALLYRET blocks\n");
                                added = true;
                            }
                        }

                        assert(predNum == predCount);
                        jumpEhf = new BBJumpTable(succTab);
                    }
                    else
                    {
                        // It's possible for the `finally` to have no CALLFINALLY predecessors if the `try` block
                        // has an unconditional `throw` (the finally will still be invoked in the exceptional
                        // case via the runtime). In that case, jumpEhf->succCount remains the default, zero,
                        // and jumpEhf->succs remains the default, null.
                        jumpEhf = new BBJumpTable();
                    }
                    finallyBlock.EhfTargets = jumpEhf;
                }

                if (usingProfileWeights)
                {
                    // Compute new flow into the finally region's continuation successors.
                    var profileConsistent = true;

                    foreach (var callFinally in finallyBegBlock.PredBlocks)
                    {
                        var callFinallyRet = callFinally.Next;
                        assert(callFinallyRet is not null);

                        callFinallyRet.setBBProfileWeight(callFinallyRet.computeIncomingWeight());
                        profileConsistent &= fgProfileWeightsConsistentOrSmall(callFinally.bbWeight, callFinallyRet.bbWeight);
                    }

                    if (!profileConsistent)
                    {
                        JITDUMP($"Flow into finally handler EH{XTnum} does not match outgoing flow. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");
                        fgPgoConsistent = false;
                    }
                }
            }
        }
    }

    /// <summary>
    ///   <para>For a call node that returns a struct do one of the following:</para>
    ///   <list type="bullet">
    ///     <item>set the flag to indicate struct return via retbuf arg;</item>
    ///     <item>adjust the return type to a SIMD type if it is returned in 1 reg;</item>
    ///     <item>spill call result into a temp if it is returned into 2 registers or more and not tail call or inline candidate.</item>
    ///   </list>
    /// </summary>
    /// <param name="call">GT_CALL GenTree node</param>
    /// <param name="retClsHnd">Class handle of return type of the call</param>
    /// <returns>Returns new GenTree node after fixing struct return of call node</returns>
    public unsafe GenTree impFixupCallStructReturn(GenTreeCall call, CORINFO_CLASS_HANDLE retClsHnd)
    {
        if (!varTypeIsStruct(call.Type))
        {
            return call;
        }
        call._retClsHnd = retClsHnd;

        // Recognize SIMD types as we do for LCL_VARs,
        // note it could be not the ABI specific type, for example, on x64 we can set 'TYP_SIMD8`
        // for `System.Numerics.Vector2` here but lower will change it to long as ABI dictates.
        var simdReturnType = impNormStructType(retClsHnd);

        if (simdReturnType != call.Type)
        {
            assert(varTypeIsSimd(simdReturnType));
            JITDUMP($"changing the type of a call [{call.TreeId:D6}] from {call.Type.Name} to {simdReturnType.Name}\n");
            call.ChangeType(simdReturnType);
        }

#if FEATURE_MULTIREG_RET
        call.InitializeStructReturnType(this, retClsHnd, call.UnmanagedCallConv);
        var retTypeDesc = call.ReturnTypeDesc;
        var retRegCount = retTypeDesc.ReturnRegCount;
#endif

        var returnType = GetReturnTypeForStruct(retClsHnd, call.UnmanagedCallConv, out var howToReturnStruct);

        if (howToReturnStruct is SPK_ByReference)
        {
            assert(returnType == TYP_UNKNOWN);
            call._callMoreFlags |= GTF_CALL_M_RETBUFFARG;

            if (call.IsUnmanaged)
            {
                // Native ABIs do not allow retbufs to alias anything.
                // This is allowed by the managed ABI and impStoreStruct will
                // never introduce copies due to this.
                var tmpNum = lvaGrabTemp(shortLifetime: true, "Retbuf for unmanaged call");
                impStoreToTemp(tmpNum, call, CHECK_SPILL_ALL);
                return gtNewLclvNode(lvaGetDesc(tmpNum).Type, tmpNum);
            }

            return call;
        }

#if FEATURE_MULTIREG_RET
        if (retRegCount is not 1)
        {
            // It could be a SIMD returned in several regs.
            assert(varTypeIsStruct(call.Type));

            assert(returnType is TYP_STRUCT);
            assert((howToReturnStruct is SPK_ByValueAsHfa) || (howToReturnStruct is SPK_ByValue));

            assert(retRegCount >= 2);

            if (!call.CanTailCall && !call.IsInlineCandidate)
            {
                // Force a call returning multi-reg struct to be always of the IR form
                //   tmp = call
                //
                // No need to assign a multi-reg struct to a local var if:
                //  - It is a tail call or
                //  - The call is marked for in-lining later
                return impStoreMultiRegValueToVar(call, retClsHnd, call.UnmanagedCallConv);
            }
        }
#endif

        return call;
    }

    /// <summary>Adjust a struct value being returned.</summary>
    /// <param name="op">the return value</param>
    /// <returns>The (possibly modified) value to return.</returns>
    /// <remarks>In the multi-reg case, we we force IR to be one of the following: GT_RETURN(LCL_VAR) or GT_RETURN(CALL). If op is anything other than a lclvar or call, it is assigned to a temp, which is then returned. In the non-multireg case, the two special helpers with "fake" return buffers are handled ("GETFIELDSTRUCT" and "UNBOX_NULLABLE").</remarks>
    public unsafe GenTree impFixupStructReturnType(GenTree op)
    {
        assert(varTypeIsStruct(info.compRetType));
        assert(info.compRetBuffArg is BAD_VAR_NUM);

        JITDUMP("\nimpFixupStructReturnType: retyping\n");
        DISPTREE(op);

        if (op.Oper.IsCall && op.AsCall().ShouldHaveRetBufArg)
        {
            // This must be one of those 'special' helpers that don't really have a return buffer, but instead
            // use it as a way to keep the trees cleaner with fewer address-taken temps. Well now we have to
            // materialize the return buffer as an address-taken temp. Then we can return the temp.
            var tmpNum = lvaGrabTemp(shortLifetime: true, "pseudo return buffer");

            // No need to spill anything as we're about to return.
            impStoreToTemp(tmpNum, op, CHECK_SPILL_NONE);

            op = gtNewLclvNode(info.compRetType, tmpNum);
            JITDUMP("\nimpFixupStructReturnType: created a pseudo-return buffer for a special helper\n");
            DISPTREE(op);

            return op;
        }

        if (compMethodReturnsMultiRegRetType || op.IsMultiRegNode)
        {
            // We can use any local with multiple registers (it will be forced to memory on mismatch),
            // except for implicit byrefs (they may turn into indirections).
            if (op.Oper is GT_LCL_VAR)
            {
                var lclNum = op.AsLclVar().LclNum;

                if (!lvaIsImplicitByRefLocal(lclNum))
                {
                    // Note that this is a multi-reg return.
                    lvaTable[lclNum].lvIsMultiRegRet = true;

                    // TODO-1stClassStructs: Handle constant propagation and CSE-ing of multireg returns.
                    op.Flags |= GTF_DONT_CSE;

                    return op;
                }
            }

            if (op.Oper.IsCall)
            {
                var call = op.AsCall();

                if (call.UnmanagedCallConv == info.compCallConv)
                {
                    // In contrast, we can only use multi-reg calls directly if they have the exact same ABI.
                    // Calling convention equality is a conservative approximation for that check.
#if TARGET_ARMARCH || TARGET_LOONGARCH64 || TARGET_RISCV64
                    if (!call.IsVarargs)
                    {
                        // TODO-Review: this seems unnecessary. Return ABI doesn't change under varargs.
                        return op;
                    }
#else
                    return op;
#endif
                }

                // We cannot tail call because control needs to return to fixup the calling convention for result return.
                call._callMoreFlags &= ~(GTF_CALL_M_TAILCALL | GTF_CALL_M_EXPLICIT_TAILCALL);
            }

            // The backend does not support other struct-producing nodes (e. g. OBJs) as sources of multi-reg returns.
            // It also does not support assembling a multi-reg node into one register (for RETURN nodes at least).
            return impStoreMultiRegValueToVar(op, info.compMethodInfo->args.retTypeClass, info.compCallConv);
        }

        // Not a multi-reg return or value, we can simply use it directly.
        return op;
    }

    /// <summary>Determine the result type of an arithmetic operation</summary>
    /// <param name="oper"></param>
    /// <param name="fUnsigned"></param>
    /// <param name="op1Ref"></param>
    /// <param name="op2Ref"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>On 64-bit inserts upcasts when native int is mixed with int32</para>
    ///   <para>Also inserts upcasts to double when float and double are mixed.</para>
    /// </remarks>
    public var_types impGetByRefResultType(genTreeOps oper, bool fUnsigned, ref GenTree op1Ref, ref GenTree op2Ref)
    {
        var op1 = op1Ref;
        var op2 = op2Ref;

        // Arithmetic operations are generally only allowed with primitive types, but certain operations are allowed with byrefs.

        if (oper is GT_SUB)
        {
            if (op1.Type is TYP_BYREF)
            {
                if (op2.Type is TYP_BYREF)
                {
                    // byref1-byref2 => gives a native int
                    return TYP_I_IMPL;
                }
                else
                {
                    // byref - [native] int => gives a byref
                    assert(genActualTypeIsIntOrI(op2.Type));

                    // Insert an explicit upcast if needed.
                    op2 = impImplicitIorI4Cast(op2, TYP_I_IMPL, fUnsigned);
                    op2Ref = op2;

                    return TYP_BYREF;
                }
            }
            else if (op2.Type is TYP_BYREF)
            {
                assert(genActualTypeIsIntOrI(op1.Type));

                // [native] int - byref => gives a native int

                //
                // The reason is that it is possible, in managed C++,
                // to have a tree like this:
                //
                //              -
                //             / \.
                //            /   \.
                //           /     \.
                //          /       \.
                // const(h) int     addr byref
                //
                // <BUGNUM> VSW 318822 </BUGNUM>
                //
                // So here we decide to make the resulting type to be a native int.

                // Insert an explicit upcast if needed.
                op1 = impImplicitIorI4Cast(op1, TYP_I_IMPL, fUnsigned);
                op1Ref = op1;

                return TYP_I_IMPL;
            }
        }

        if (oper == GT_ADD)
        {
            if (op1.Type is TYP_BYREF)
            {
                // byref + [native] int => gives a byref
                assert(genActualTypeIsIntOrI(op1.Type));

                // Insert explicit upcasts if needed.
                op1 = impImplicitIorI4Cast(op1, TYP_I_IMPL, fUnsigned);
                op2 = impImplicitIorI4Cast(op2, TYP_I_IMPL, fUnsigned);

                op1Ref = op1;
                op2Ref = op2;

                return TYP_BYREF;
            }
            else if (op2.Type is TYP_BYREF)
            {
                // [native] int + byref => gives a byref
                assert(genActualTypeIsIntOrI(op2.Type));

                // Insert explicit upcasts if needed.
                op1 = impImplicitIorI4Cast(op1, TYP_I_IMPL, fUnsigned);
                op2 = impImplicitIorI4Cast(op2, TYP_I_IMPL, fUnsigned);

                op1Ref = op1;
                op2Ref = op2;

                return TYP_BYREF;
            }
        }

        var actualType = op1.Type.ActualType;

        if ((actualType is TYP_LONG) || (op2.Type.ActualType is TYP_LONG))
        {
            assert(!varTypeIsFloating(op1.Type) && !varTypeIsFloating(op2.Type));

            // int + long => gives long
            // long + int => gives long

#if TARGET_64BIT
            // We get this because in the IL the long isn't Int64, it's just IntPtr.
            // Insert explicit upcasts if needed.

            if (actualType is not TYP_I_IMPL)
            {
                // insert an explicit upcast
                op1 = gtNewCastNode(TYP_I_IMPL, op1, fUnsigned, TYP_I_IMPL);
            }
            else if (op2.Type.ActualType is not TYP_I_IMPL)
            {
                // insert an explicit upcast
                op2 = gtNewCastNode(TYP_I_IMPL, op2, fUnsigned, TYP_I_IMPL);
            }

            if (opts.OptimizationEnabled)
            {
                op1 = gtFoldExpr(op1);
                op2 = gtFoldExpr(op2);
            }

            op1Ref = op1;
            op2Ref = op2;
#endif

            return TYP_LONG;
        }

        // int + int => gives an int
        assert((actualType is not TYP_BYREF) && (op2.Type.ActualType is not TYP_BYREF));
        assert((actualType == op2.Type.ActualType) || (varTypeIsFloating(op1.Type) && varTypeIsFloating(op2.Type)));

        // If both operands are TYP_FLOAT, then leave it as TYP_FLOAT. Otherwise, turn floats into doubles
        if (varTypeIsFloating(actualType) && (op2.Type != actualType))
        {
            op1 = impImplicitR4orR8Cast(op1, TYP_DOUBLE);
            op2 = impImplicitR4orR8Cast(op2, TYP_DOUBLE);

            op1Ref = op1;
            op2Ref = op2;

            return TYP_DOUBLE;
        }

        assert(actualType is TYP_BYREF or TYP_DOUBLE or TYP_FLOAT or TYP_LONG or TYP_INT);
        return actualType;
    }

    /// <summary>gets the generic type definition from a 'typeof' expression.</summary>
    /// <param name="type">The 'GenTree' node to inspect.</param>
    /// <returns></returns>
    /// <remarks>If successful, this method will call 'impPopStack()' before returning.</remarks>
    public unsafe GenTree? impGetGenericTypeDefinition(GenTree type)
    {
        // This intrinsic requires the first arg to be some `typeof()` expression,
        // ie. it applies to cases such as `typeof(...).GetGenericTypeDefinition()`.

        if (gtIsTypeof(type, out var hClassType))
        {
            // Check that the 'typeof()' expression is being used on a type that is in fact generic.
            // If that is not the case, we don't expand the intrinsic. This will end up using
            // the usual Type.GetGenericTypeDefinition() at runtime, which will throw in this case.
            if (info.compCompHnd->getTypeInstantiationArgument(hClassType, 0) != NO_CLASS_HANDLE)
            {
                var hClassResult = info.compCompHnd->getTypeDefinition(hClassType);

                var handle = gtNewIconEmbClsHndNode(hClassResult);
                var retNode = gtNewHelperCallNode(TYP_REF, CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE, handle);

                // Drop the typeof(T) node
                _ = impPopStack();

                return retNode;
            }
        }
        return null;
    }

    /// <summary>Get the address of a value.</summary>
    /// <param name="val">The value in question</param>
    /// <param name="curLevel">Stack level for spilling</param>
    /// <param name="allowedMustPreserveIndirFlags">If 'val' is an indirection and it has any must-preserve indir flags (like volatile), then those flags must be included in this mask to be allowed through without creating a temp.</param>
    /// <param name="indirFlags">Flags that indirs created based on this address can and should set.</param>
    /// <returns>In case "val" represents a location (is an indirection/local), will return its address. Otherwise, address of a temporary assigned the value of "val" will be returned.</returns>
    /// <remarks>Returned flags are included in the GTF_IND_COPYABLE_FLAGS mask.</remarks>
    public GenTree impGetNodeAddr(GenTree val, int curLevel, GenTreeFlags allowedMustPreserveIndirFlags, out GenTreeFlags indirFlags)
    {
        indirFlags = GTF_EMPTY;

        switch (val.Oper)
        {
            case GT_BLK:
            case GT_IND:
            case GT_STOREIND:
            case GT_STORE_BLK:
            {
                if ((val.Flags & GTF_IND_MUST_PRESERVE_FLAGS & ~allowedMustPreserveIndirFlags) is not 0)
                {
                    break;
                }

                indirFlags = val.Flags & GTF_IND_COPYABLE_FLAGS;
                return val.AsIndir().Addr;
            }

            case GT_LCL_VAR:
            case GT_STORE_LCL_VAR:
            {
                val.Flags |= GTF_VAR_MOREUSES;
                return gtNewLclVarAddrNode(TYP_BYREF, val.AsLclVar().LclNum);
            }

            case GT_LCL_FLD:
            case GT_STORE_LCL_FLD:
            {
                var lclFld = val.AsLclFld();
                val.Flags |= GTF_VAR_MOREUSES;
                return gtNewLclAddrNode(TYP_BYREF, lclFld.LclNum, lclFld.LclOffs);
            }

            case GT_COMMA:
            {
                var op = val.AsOp();
                _ = impAppendTree(op.Op1, curLevel, impCurStmtDI);
                return impGetNodeAddr(op.Op2, curLevel, allowedMustPreserveIndirFlags, out indirFlags);
            }

            default:
            {
                break;
            }
        }

        assert(!val.Oper.IsStore);
        var lclNum = lvaGrabTemp(shortLifetime: true, "location for address-of(RValue)");
        impStoreToTemp(lclNum, val, curLevel);

        // The 'return value' is now address of the temp itself.
        return gtNewLclVarAddrNode(TYP_BYREF, lclNum);
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

    /// <summary>Return the byte for "b" (allocating/extending impPendingBlockMembers if necessary.)</summary>
    /// <param name="blk"></param>
    /// <returns></returns>
    /// <remarks>Operates on the map in the top-level ancestor.</remarks>
    public byte impGetPendingBlockMember(BasicBlock blk)
        => impInlineRoot.impPendingBlockMembers[blk.bbInd];

    /// <summary>Set the byte for "b" to "val" (allocating/extending impPendingBlockMembers if necessary.)</summary>
    /// <param name="blk"></param>
    /// <param name="val"></param>
    /// <remarks>Operates on the map in the top-level ancestor.</remarks>
    public void impSetPendingBlockMember(BasicBlock blk, byte val)
        => impInlineRoot.impPendingBlockMembers[blk.bbInd] = val;

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

    // Assumes that "block" is a basic block that completes with a non-empty stack. We will assign the values
    // on the stack to local variables (the "spill temp" variables). The successor blocks will assume that
    // its incoming stack contents are in those locals. This requires "block" and its successors to agree on
    // the variables that will be used -- and for all the predecessors of those successors, and the
    // successors of those predecessors, etc. Call such a set of blocks closed under alternating
    // successor/predecessor edges a "spill clique." A block is a "predecessor" or "successor" member of the
    // clique (or, conceivably, both). Each block has a specified sequence of incoming and outgoing spill
    // temps. If "block" already has its outgoing spill temps assigned (they are always a contiguous series
    // of local variable numbers, so we represent them with the base local variable number), returns that.
    // Otherwise, picks a set of spill temps, and propagates this choice to all blocks in the spill clique of
    // which "block" is a member (asserting, in debug mode, that no block in this clique had its spill temps
    // chosen already. More precisely, that the incoming or outgoing spill temps are not chosen, depending
    // on which kind of member of the clique the block is).
    public int impGetSpillTmpBase(BasicBlock block)
    {
        if (block.bbStkTempsOut != NO_BASE_TMP)
        {
            return block.bbStkTempsOut;
        }

#if DEBUG
        if (verbose)
        {
            jitprintf($"\n*************** In impGetSpillTmpBase({FMT_BB(block.bbNum)})\n");
        }
#endif

        // Otherwise, choose one, and propagate to all members of the spill clique.
        // Grab enough temps for the whole stack.
        var baseTmp = lvaGrabTemps(stackState.esStackDepth, "IL Stack Entries");

        // We do *NOT* need to reset the SpillClique*Members because a block can only be the predecessor
        // to one spill clique, and similarly can only be the successor to one spill clique
        impWalkSpillCliqueFromPred(block, SetSpillTempsBase);

        return baseTmp;

        void SetSpillTempsBase(SpillCliqueDir predOrSucc, BasicBlock blk)
        {
            if (predOrSucc == SpillCliqueSucc)
            {
                assert(blk.bbStkTempsIn == NO_BASE_TMP); // Should not already be a member of a clique as a successor.
                blk.bbStkTempsIn = baseTmp;
            }
            else
            {
                assert(predOrSucc == SpillCliquePred);
                assert(blk.bbStkTempsOut == NO_BASE_TMP); // Should not already be a member of a clique as a predecessor.
                blk.bbStkTempsOut = baseTmp;
            }
        }
    }

    /// <summary>Try to obtain string literal out of a span</summary>
    /// <param name="span">String_op_Implicit or MemoryExtensions_AsSpan call with a string literal</param>
    /// <returns>GenTreeStrCon node or nullptr</returns>
    public unsafe GenTreeStrCon? impGetStrConFromSpan(GenTree span)
    {
        // var span = "str".AsSpan();
        // var span = (ReadOnlySpan<char>)"str"

        var argCall = null as GenTreeCall;

        if (span.Oper is GT_RET_EXPR)
        {
            // NOTE: we don't support chains of RET_EXPR here
            var inlineCandidate = span.AsRetExpr().InlineCandidate;

            if (inlineCandidate.Oper is GT_CALL)
            {
                argCall = inlineCandidate.AsCall();
            }
        }
        else if (span.Oper is GT_CALL)
        {
            argCall = span.AsCall();
        }

        if ((argCall is not null) && argCall.IsSpecialIntrinsic())
        {
            var  ni = lookupNamedIntrinsic(argCall._callMethHnd);

            if ((ni == NI_System_MemoryExtensions_AsSpan) || (ni == NI_System_String_op_Implicit))
            {
                assert(argCall.Args.CountArgs() == 1);

                var arg = argCall.Args.GetArgByIndex(0);
                assert(arg is not null);

                var argNode = arg.Node;

                if (argNode.Oper is GT_CNS_STR)
                {
                    return argNode.AsStrCon();
                }
            }
        }
        return null;
    }

    public void impHandleAccessAllowed(CorInfoIsAccessAllowedResult result, in CORINFO_HELPER_DESC helperCall)
    {
        // In general try to call this before most of the verification work.  Most people expect the access
        // exceptions before the verification exceptions.  If you do this after, that usually doesn't happen.  Turns
        // out if you can't access something we also think that you're unverifiable for other reasons.

        if (result != CORINFO_ACCESS_ALLOWED)
        {
            impHandleAccessAllowedInternal(result, helperCall);
        }
    }

    public void impHandleAccessAllowedInternal(CorInfoIsAccessAllowedResult result, in CORINFO_HELPER_DESC helperCall)
    {
        if (result is CORINFO_ACCESS_ILLEGAL)
        {
            impInsertHelperCall(helperCall);
        }
    }

    //------------------------------------------------------------------------
    // impHWIntrinsic: Import a hardware intrinsic as a GT_HWINTRINSIC node if possible
    //
    // Arguments:
    //    intrinsic  -- id of the intrinsic function.
    //    clsHnd     -- class handle containing the intrinsic function.
    //    method     -- method handle of the intrinsic function.
    //    sig        -- signature of the intrinsic call
    //    entryPoint -- The entry point information required for R2R scenarios
    //    mustExpand -- true if the intrinsic must return a GenTree*; otherwise, false

    // Return Value:
    //    The GT_HWINTRINSIC node, or nullptr if not a supported intrinsic
    //
    public unsafe GenTree? impHWIntrinsic(NamedIntrinsic intrinsic, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE method, in CORINFO_SIG_INFO sig, in CORINFO_CONST_LOOKUP entryPoint, bool mustExpand)
    {
        // TODO: Port impHWIntrinsic
        return null;
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

    // TYP_INT and TYP_I_IMPL can be used almost interchangeably, but we want
    // to make that an explicit cast in our trees, so any implicit casts that
    // exist in the IL (at least on 64-bit where TYP_I_IMPL != TYP_INT) are
    // turned into explicit casts here.
    // We also allow an implicit conversion of a ldnull into a TYP_I_IMPL(0)
    public GenTree impImplicitIorI4Cast(GenTree tree, var_types dstTyp, bool zeroExtend = false)
    {
        var currType = tree.Type.ActualType;
        var wantedType = dstTyp.ActualType;

        if (wantedType != currType)
        {
            // Automatic upcast for a GT_CNS_INT into TYP_I_IMPL
            if (tree.Oper.IsCnsIntOrI && varTypeIsI(dstTyp))
            {
                if ((currType is TYP_REF) && (tree.AsIntCon().IconValue is 0))
                {
                    tree.Type = TYP_I_IMPL;
                }
#if TARGET_64BIT
                else if (currType == TYP_INT)
                {
                    tree.Type = TYP_I_IMPL;
                }
#endif
            }
#if TARGET_64BIT
            else if (varTypeIsI(wantedType) && (currType is TYP_INT))
            {
                // Note that this allows TYP_INT to be cast to a TYP_I_IMPL when wantedType is a TYP_BYREF or TYP_REF
                tree = gtNewCastNode(TYP_I_IMPL, tree, zeroExtend, TYP_I_IMPL);
            }
            else if ((wantedType is TYP_INT) && varTypeIsI(currType))
            {
                // Note that this allows TYP_BYREF or TYP_REF to be cast to a TYP_INT
                tree = gtNewCastNode(TYP_INT, tree, fromUnsigned: false, TYP_INT);
            }
#endif
        }

        return tree;
    }

    // TYP_FLOAT and TYP_DOUBLE can be used almost interchangeably in most cases,
    // but we want to make that an explicit cast in our trees, so any implicit casts
    // that exist in the IL are turned into explicit casts here.
    public GenTree impImplicitR4orR8Cast(GenTree tree, var_types dstTyp)
    {
        if (varTypeIsFloating(tree.Type) && varTypeIsFloating(dstTyp) && (dstTyp != tree.Type))
        {
            tree = gtNewCastNode(dstTyp, tree, fromUnsigned: false, dstTyp);
        }
        return tree;
    }

    /// <summary>convert IL into jit IR</summary>
    /// <remarks>
    ///   <para>The basic flowgraph has already been constructed. Blocks are filled in by the importer as they are discovered to be reachable.</para>
    ///   <para>Blocks may be added to provide the right structure for various EH constructs (notably LEAVEs from catches and finallies).</para>
    /// </remarks>
    public void impImport()
    {
        var inlineRoot = impInlineRoot;

        if (info.compMaxStack <= SMALL_STACK_SIZE)
        {
            impStkSize = SMALL_STACK_SIZE;
        }
        else
        {
            impStkSize = info.compMaxStack;
        }

        if (this == inlineRoot)
        {
            // Allocate the stack contents
            stackState.esStack = new StackEntry[impStkSize];
        }
        else
        {
            // This is the inlinee compiler, steal the stack from the inliner compiler
            // (after ensuring that it is large enough).
            if (inlineRoot.impStkSize < impStkSize)
            {
                inlineRoot.impStkSize = impStkSize;
                inlineRoot.stackState.esStack = new StackEntry[impStkSize];
            }

            stackState.esStack = inlineRoot.stackState.esStack;
        }

        // initialize the entry state at start of method
        initCurrentState();

        // Initialize stuff related to figuring "spill cliques" (see spec comment for impGetSpillTmpBase).
        if (this == inlineRoot) // These are only used on the root of the inlining tree.
        {
            // We have initialized these previously, but to size 0.  Make them larger.
            impPendingBlockMembers.Capacity = fgBBNumMax * 2;
            impSpillCliquePredMembers.Capacity = fgBBNumMax * 2;
            impSpillCliqueSuccMembers.Capacity = fgBBNumMax * 2;
        }

        inlineRoot.impPendingBlockMembers.Clear();
        inlineRoot.impSpillCliquePredMembers.Clear();
        inlineRoot.impSpillCliqueSuccMembers.Clear();

        inlineRoot.impPendingBlockMembers.Capacity = fgBBNumMax * 2;
        inlineRoot.impSpillCliquePredMembers.Capacity = fgBBNumMax * 2;
        inlineRoot.impSpillCliqueSuccMembers.Capacity = fgBBNumMax * 2;

        impBlockListNodeFreeList = null;

#if DEBUG
        impLastILoffsStmt = null;
        impNestedStackSpill = false;
#endif
        impBoxTemp = BAD_VAR_NUM;

        impPendingFree = null;
        impPendingList = null;

        // Skip leading internal blocks.
        // These can arise from needing a leading scratch BB, from EH normalization, and from OSR entry redirects.

        assert(fgFirstBB is not null);
        var entryBlock = fgFirstBB;

        while (entryBlock.HasFlag(BBF_INTERNAL))
        {
            JITDUMP($"Marking leading BBF_INTERNAL block {FMT_BB(entryBlock.bbNum)} as BBF_IMPORTED\n");
            entryBlock.SetFlags(BBF_IMPORTED);

            assert(entryBlock.Kind is BBJ_ALWAYS);
            entryBlock = entryBlock.Target;
        }

        // Note for OSR we'd like to be able to verify this block must be
        // stack empty, but won't know that until we've imported...so instead
        // we'll BADCODE out if we mess up.
        //
        // (the concern here is that the runtime asks us to OSR a
        // different IL version than the one that matched the method that
        // triggered OSR).  This should not happen but I might have the
        // IL versioning stuff wrong.
        //
        // TODO: we also currently expect this block to be a join point,
        // which we should verify over when we find jump targets.
        impImportBlockPending(entryBlock);

        if (opts.IsOSR)
        {
            // We now import all the IR and keep it around so we can
            // analyze address exposure more robustly.

            assert(fgEntryBB is not null);
            JITDUMP($"OSR: protecting original method entry {FMT_BB(fgEntryBB.bbNum)}\n");
            impImportBlockPending(fgEntryBB);
            fgEntryBB.bbRefs++;
            fgEntryBBExtraRefs++;
        }

        // Import blocks in the worker-list until there are no more

        while (impPendingList is not null)
        {
            // Remove the entry at the front of the list

            var dsc = impPendingList;
            assert(dsc.pdBB is not null);

            impPendingList = impPendingList.pdNext;
            impSetPendingBlockMember(dsc.pdBB, val: 0);

            // Restore the stack state
            impRestoreStackState(dsc.pdSavedStack);

            // Add the entry to the free list for reuse
            dsc.pdNext = impPendingFree;
            impPendingFree = dsc;

            // Now import the block
            impImportBlock(dsc.pdBB);

            if (compDonotInline)
            {
                return;
            }
        }

        // If the method had EH, we may be missing some pred edges
        // (notably those from BBJ_EHFINALLYRET blocks). Add them.
        //
        if (info.compXcptnsCount > 0)
        {
            impFixPredLists();
            JITDUMP("\nAfter impImport() added blocks for try,catch,finally");

            if (verbose)
            {
                fgDispBasicBlocks();
            }
        }
    }

    /// <summary>build and import a value-type box</summary>
    /// <param name="resolvedToken">resolved token from the box operation</param>
    /// <remarks>
    ///   <para>Side EFfect: The value to be boxed is popped from the stack, and a tree for the boxed value is pushed. This method may create upstream statements, spill side effecting trees, and create new temps.</para>
    ///   <para>Side EFfect: If importing an inlinee, we may also discover the inline must fail. If so there is no new value pushed on the stack. Callers should use CompDoNotInline after calling this method to see if ongoing importation should be aborted.</para>
    ///   <para>Boxing of ref classes results in the same value as the value on the top of the stack, so is handled inline in impImportBlockCode for the CEE_BOX case. Only value or primitive type boxes make it here.</para>
    ///   <para>Boxing for nullable types is done via a helper call; boxing of other value types is expanded inline or handled via helper call, depending on the jit's codegen mode.</para>
    ///   <para>When the jit is operating in size and time constrained modes, using a helper call here can save jit time and code size. But it also may inhibit cleanup optimizations that could have also had a even greater benefit effect on code size and jit time. An optimal strategy may need to peek ahead and see if it is easy to tell how the box is being used. For now, we defer.</para>
    /// </remarks>
    public unsafe void impImportAndPushBox(in CORINFO_RESOLVED_TOKEN resolvedToken)
    {
        // Spill any special side effects
        impSpillSpecialSideEff();

        // In async methods, if the value to box contains an async call, we need to spill it
        // before popping it from the stack. This is because we will later create a byref
        // destination (box temp + offset) for storing the value, and we cannot have a
        // byref live across an async call.
        if (gtTreeContainsAsyncCall(impStackTop().val))
        {
            impSpillSideEffects(spillGlobEffects: true, CHECK_SPILL_ALL, "async box with call");
        }

        // Look at what helper we should use.
        var boxHelper = info.compCompHnd->getBoxHelper(resolvedToken.hClass);

        // Expand nullable boxes inline in optimized code in hot paths, it's slightly different from
        // other expansions since we have to use QMARK to return null for nullables with no value.
        //
        //  obj = nullable._hasValue is 0 ? null : (boxed underlying value)
        //
        if ((boxHelper is CORINFO_HELP_BOX_NULLABLE) && impImportAndPushBoxForNullable(resolvedToken))
        {
            return;
        }

        // Get get the expression to box from the stack.
        var op1 = null as GenTree;
        var op2 = null as GenTree;

        var se = impPopStack();
        var exprToBox = se.val;

        // Determine what expansion to prefer.
        //
        // In size/time/debuggable constrained modes, the helper call
        // expansion for box is generally smaller and is preferred, unless
        // the value to box is a struct that comes from a call. In that
        // case the call can construct its return value directly into the
        // box payload, saving possibly some up-front zeroing.
        //
        // Currently primitive type boxes always get inline expanded. We may
        // want to do the same for small structs if they don't come from
        // calls and don't have GC pointers, since explicitly copying such
        // structs is cheap.
        JITDUMP("\nCompiler.impImportAndPushBox -- handling BOX(value class) via");

        var canExpandInline = boxHelper is CORINFO_HELP_BOX;
        var optForSize = !exprToBox.Oper.IsCall && varTypeIsStruct(exprToBox.Type) && opts.OptimizationDisabled;
        var expandInline = canExpandInline && !optForSize;

        if (expandInline)
        {
            JITDUMP(" inline allocate/copy sequence\n");

            // we are doing 'normal' boxing.  This means that we can inline the box operation
            // Box(expr) gets morphed into
            // temp = new(clsHnd)
            // cpobj(temp+4, expr, clsHnd)
            // push temp
            // The code paths differ slightly below for structs and primitives because
            // "cpobj" differs in these cases.  In one case you get
            //    impStoreStructPtr(temp+4, expr, clsHnd)
            // and the other you get
            //    *(temp+4) = expr

            // For minopts/debug code, try and minimize the total number
            // of box temps by reusing an existing temp when possible. However,
            var shareBoxedTemps = opts.OptimizationDisabled;

            // Avoid sharing in some tier 0 cases to, potentially, avoid boxing in Enum.HasFlag.
            if (shareBoxedTemps && varTypeIsIntegral(exprToBox.Type) && !lvaHaveManyLocals() && (info.compCompHnd->isEnum(resolvedToken.hClass, null) != TypeCompareState.Must))
            {
                shareBoxedTemps = false;
            }

            if (shareBoxedTemps)
            {
                // For minopts/debug code, try and minimize the total number
                // of box temps by reusing an existing temp when possible.
                if (impBoxTempInUse || (impBoxTemp is BAD_VAR_NUM))
                {
                    impBoxTemp = lvaGrabTemp(shortLifetime: true, "Reusable Box Helper");
                }
            }
            else
            {
                // When optimizing, use a new temp for each box operation
                // since we then know the exact class of the box temp.
                impBoxTemp = lvaGrabTemp(shortLifetime: true, "Single-def Box Helper");
                ref var lvaDsc = ref lvaTable[impBoxTemp];

                lvaDsc.Type = TYP_REF;
                lvaDsc.lvSingleDef = true;

                JITDUMP($"Marking V{impBoxTemp:D2} as a single def local\n");
                lvaSetClass(impBoxTemp, resolvedToken.hClass, isExact: true);
            }

            // needs to stay in use until this box expression is appended
            // some other node.  We approximate this by keeping it alive until
            // the opcode stack becomes empty
            impBoxTempInUse = true;

            // Remember the current last statement in case we need to move
            // a range of statements to ensure the box temp is initialized
            // before it's used.

            var cursor = impLastStmt;
            op1 = gtNewAllocObjNode(resolvedToken, info.compMethodHnd, useParent: false);

            if (op1 is null)
            {
                // If we fail to create the newobj node, we must be inlining
                // and have run across a type we can't describe.

                assert(compDonotInline);
                return;
            }

            // Remember that this basic block contains 'new' of an object,
            // and so does this method

            assert(compCurBB is not null);
            compCurBB.SetFlags(BBF_HAS_NEWOBJ);
            optMethodFlags |= OMF_HAS_NEWOBJ;

            // Assign the boxed object to the box temp.
            var allocBoxStore = gtNewTempStore(impBoxTemp, op1);
            var allocBoxStmt = impAppendTree(allocBoxStore, CHECK_SPILL_NONE, impCurStmtDI);

            // If the exprToBox is a call that returns its value via a ret buf arg,
            // move the store statement(s) before the call (which must be a top level tree).
            //
            // We do this because impStoreStructPtr (invoked below) will
            // back-substitute into a call when it sees a GT_RET_EXPR and the call
            // has a hidden buffer pointer, So we need to reorder things to avoid
            // creating out-of-sequence IR.

            if (varTypeIsStruct(exprToBox.Type) && (exprToBox.Oper is GT_RET_EXPR))
            {
                var call = exprToBox.AsRetExpr().InlineCandidate.AsCall();

                // If the call was flagged for possible enumerator cloning, flag the allocation as well.
                //
                if (compIsForInlining && hasImpEnumeratorGdvLocalMap)
                {
                    var map = ImpEnumeratorGdvLocalMap;

                    var iciCall = impInlineInfo.iciCall;
                    assert(iciCall is not null);

                    if (map.TryGetValue(iciCall, out var enumeratorLcl))
                    {
                        JITDUMP($"Flagging [{op1.TreeId:D6}] for enumerator cloning via V{enumeratorLcl:D2}\n");
                        _ = map.Remove(iciCall);
                        map[op1] = enumeratorLcl;
                    }
                }

                if (call.ShouldHaveRetBufArg)
                {
                    JITDUMP($"Must insert newobj stmts for box before call [{call.TreeId:D6}]\n");

                    // Walk back through the statements in this block, looking for the one
                    // that has this call as the root node.
                    //
                    // Because gtNewTempStore (above) may have added statements that
                    // feed into the actual store we need to move this set of added
                    // statements as a group.
                    //
                    // Note boxed allocations are side-effect free (no com or finalizer) so
                    // our only worries here are (correctness) not overlapping the box temp
                    // lifetime and (perf) stretching the temp lifetime across the inlinee
                    // body.
                    //
                    // Since this is an inline candidate, we must be optimizing, and so we have
                    // a unique box temp per call. So no worries about overlap.
                    //
                    assert(!opts.OptimizationDisabled);

                    // Lifetime stretching could addressed with some extra cleverness--sinking
                    // the allocation back down to just before the copy, once we figure out
                    // where the copy is. We defer for now.
                    //
                    var insertBeforeStmt = cursor;
                    noway_assert(insertBeforeStmt is not null);

                    while (insertBeforeStmt.RootNode != call)
                    {
                        // If we've searched all the statements in the block and failed to find the call, then something's wrong.

                        noway_assert(insertBeforeStmt != impStmtList);
                        insertBeforeStmt = insertBeforeStmt.PrevStmt;

                        assert(insertBeforeStmt is not null);
                    }

                    // Found the call. Move the statements comprising the store.

                    assert(cursor is not null);
                    assert(cursor.NextStmt is not null);
                    assert(allocBoxStmt == impLastStmt);

                    JITDUMP($"Moving {FMT_STMT(cursor.NextStmt.Id)}...{FMT_STMT(allocBoxStmt.Id)} before {FMT_STMT(insertBeforeStmt.Id)}\n");

                    do
                    {
                        var movingStmt = impExtractLastStmt();
                        impInsertStmtBefore(movingStmt, insertBeforeStmt);
                        insertBeforeStmt = movingStmt;
                    }
                    while (impLastStmt != cursor);
                }
            }

            // Create a pointer to the box payload in op1.
            //
            op1 = gtNewLclvNode(TYP_REF, impBoxTemp);
            op2 = gtNewIconNode(TYP_I_IMPL, TARGET_POINTER_SIZE);
            op1 = gtNewBinaryNode(GT_ADD, TYP_BYREF, op1, op2);

            // Copy from the exprToBox to the box payload.
            //
            if (varTypeIsStruct(exprToBox.Type))
            {
                op1 = impStoreStructPtr(op1, exprToBox, CHECK_SPILL_ALL);
            }
            else
            {
                var lclTyp = exprToBox.Type;

                if (lclTyp is TYP_BYREF)
                {
                    lclTyp = TYP_I_IMPL;
                }

                var jitType = info.compCompHnd->asCorInfoType(resolvedToken.hClass);

                if (impIsPrimitive(jitType))
                {
                    lclTyp = jitType.VarType;
                }

                var srcTyp = exprToBox.Type;
                var dstTyp = lclTyp;

                // We allow float <-> double mismatches and implicit truncation for small types.
                assert((srcTyp.ActualType == dstTyp.ActualType) || (varTypeIsFloating(srcTyp) == varTypeIsFloating(dstTyp)));

                // Note regarding small types.
                // We are going to store to the box here via an indirection, so the cast added below is
                // redundant, since the store has an implicit truncation semantic. The reason we still
                // add this cast is so that the code which deals with GT_BOX optimizations does not have
                // to account for this implicit truncation (e. g. understand that BOX<byte>(0xFF + 1) is
                // actually BOX<byte>(0) or deal with signedness mismatch and other GT_CAST complexities).
                if (srcTyp != dstTyp)
                {
                    exprToBox = gtNewCastNode(dstTyp.ActualType, exprToBox, fromUnsigned: false, dstTyp);
                }
                op1 = gtNewStoreIndNode(dstTyp, op1, exprToBox, GTF_IND_NONFAULTING);
            }

            // Spill eval stack to flush out any pending side effects.
            impSpillSideEffects(spillGlobEffects: true, CHECK_SPILL_ALL, "impImportAndPushBox");

            // Set up this copy as a second store.
            var copyStmt = impAppendTree(op1, CHECK_SPILL_NONE, impCurStmtDI);

            op1 = gtNewLclvNode(TYP_REF, impBoxTemp);

            // Record that this is a "box" node and keep track of the matching parts.
            var box = new GenTreeBox(TYP_REF, op1, allocBoxStmt, copyStmt);

            // If it is a value class, mark the "box" node.  We can use this information
            // to optimise several cases:
            //    "box(x) is null" --> false
            //    "(box(x)).CallAnInterfaceMethod(...)" --> "(&x).CallAValueTypeMethod"
            //    "(box(x)).CallAnObjectMethod(...)" --> "(&x).CallAValueTypeMethod"

            box.Flags |= GTF_BOX_VALUE;
            assert(box.IsBoxedValue && (allocBoxStore.Oper is GT_STORE_LCL_VAR));

            op1 = box;
        }
        else
        {
            // Don't optimize, just call the helper and be done with it.
            JITDUMP($" helper call because: {(canExpandInline ? "optimizing for size" : "nullable")}\n");

            // Ensure that the value class is restored
            op2 = impTokenToHandle(resolvedToken, mustRestoreHandle: true);

            if (op2 is null)
            {
                // We must be backing out of an inline.
                assert(compDonotInline);
                return;
            }

            // Boxing helpers allow the "initclass" indir flag, but not volatile/unaligned flags
            op1 = gtNewHelperCallNode(TYP_REF, boxHelper, op2, impGetNodeAddr(exprToBox, CHECK_SPILL_ALL, GTF_IND_INITCLASS, out _));
        }

        // Push the result back on the stack, even if clsHnd is a value class we want the TYP_REF
        impPushOnStack(op1, new typeInfo(info.compCompHnd->getTypeForBox(resolvedToken.hClass)));
    }

    /// <summary>import a "box Nullable" as an inlined sequence</summary>
    /// <param name="resolvedToken">resolved token from the box operation</param>
    /// <returns></returns>
    public unsafe bool impImportAndPushBoxForNullable(in CORINFO_RESOLVED_TOKEN resolvedToken)
    {
        // arg._hasValue is 0 ?
        //      null :
        //      (lcl = allocobj; *(lcl + sizeof(void*)) = arg._value; lcl)

        var nullableCls = resolvedToken.hClass;

        assert(info.compCompHnd->getBoxHelper(nullableCls) is CORINFO_HELP_BOX_NULLABLE);
        assert(compCurBB is not null);

        if (opts.OptimizationDisabled || compCurBB.isRunRarely)
        {
            return false;
        }

        if (eeIsSharedInst(nullableCls) || IsReadyToRun)
        {
            // TODO-CQ: Enable the optimization for shared generics and R2R scenarios.
            // The current machinery requires a ResolvedToken (basically, 'newobj underlyingType'
            // that we don't have).
            return false;
        }

        var nullableObj = impPopStack().val;

        // Decompose the Nullable<> arg into _hasValue and _value fields
        // and calculate the type and layout of the 'value' field
        //
        // Boxing allows the "initclass" flag, but not volatile/unaligned flags
        nullableObj = impGetNodeAddr(nullableObj, CHECK_SPILL_ALL, GTF_IND_INITCLASS, out var indirFlags);
        nullableObj = impCloneExpr(nullableObj, out var nullableObjClone, CHECK_SPILL_ALL, "nullable obj clone");
        assert(nullableObjClone is not null);

        var valueFldHnd = info.compCompHnd->getFieldInClass(nullableCls, 1);
        var cnsValueOffset = info.compCompHnd->getFieldOffset(valueFldHnd);

        var valueStructCls = NO_CLASS_HANDLE;
        var corFldType = info.compCompHnd->getFieldType(valueFldHnd, &valueStructCls);
        var valueType = TypeHandleToVarType(corFldType, valueStructCls, out var layout);

        var valueOffset = gtNewIconNode(TYP_I_IMPL, cnsValueOffset);
        var valueAddr = gtNewBinaryNode(GT_ADD, TYP_BYREF, nullableObjClone, valueOffset);
        var value = gtNewLoadValueNode(valueType, valueAddr, layout);
        var hasValue = gtNewLoadValueNode(TYP_UBYTE, nullableObj, layout: null);

        // Create the allocation node for the box
        var typeToBox = info.compCompHnd->getTypeForBox(nullableCls);

        bool hasSideEffects;
        var helperTemp = info.compCompHnd->getNewHelper(typeToBox, &hasSideEffects);

        var typeToBoxHnd = gtNewIconEmbClsHndNode(typeToBox);
        var allocObj = gtNewAllocObjNode(TYP_REF, typeToBoxHnd, helperTemp, hasSideEffects, typeToBox);

        // Now we need to copy value into the allocated box
        var objLclNum = lvaGrabTemp(shortLifetime: true, "obj nullable box");
        var storeAlloc = gtNewTempStore(objLclNum, allocObj);
        var objLcl = gtNewLclvNode(TYP_REF, objLclNum);
        var pOffset = gtNewIconNode(TYP_I_IMPL, TARGET_POINTER_SIZE);
        var dataPtr = gtNewBinaryNode(GT_ADD, TYP_BYREF, gtCloneExpr(objLcl), pOffset);
        var storeData = gtNewStoreValueNode(valueType, dataPtr, gtCloneExpr(value), layout, GTF_IND_NONFAULTING);

        // Wrap it all in two commas, it will look like:
        //   lcl = allocobj
        //   *(lcl + sizeof(void*)) = value
        //   lcl
        var copyData = gtNewCommaNode(TYP_REF, storeData, gtCloneExpr(objLcl));
        var allocRoot = gtNewCommaNode(TYP_REF, storeAlloc, copyData);

        // QMARK expansion will propagate block flags properly.
        compCurBB.SetFlags(BBF_HAS_NEWOBJ);
        optMethodFlags |= (OMF_HAS_NEWOBJ | OMF_HAS_EARLY_QMARKS);

        var cond = gtNewBinaryNode(GT_EQ, TYP_INT, hasValue, gtNewIconNode(TYP_INT, 0));
        var colon = gtNewColonNode(TYP_REF, gtNewNull(), allocRoot);
        var qmark = gtNewQmarkNode(TYP_REF, cond, colon);

        // We have to expand early since GT_ALLOCOBJ must be a top-level statement
        qmark.IsEarlyExpandableQmark = true;

        // QMARK has to be a top-level statement
        var result = lvaGrabTemp(shortLifetime: true, "spilling qmarkNullableBox");
        impStoreToTemp(result, qmark, CHECK_SPILL_ALL);
        lvaSetClass(result, typeToBox, true);
        lvaSetClass(objLclNum, typeToBox, true);
        impPushOnStack(gtNewLclvNode(TYP_REF, result), new typeInfo(typeToBox));

        JITDUMP($" inlined BOX({eeGetClassName(nullableCls)}) as QMARK allocating box and copying fields:\n");
        DISPTREE(qmark);
        JITDUMP("\n");
        return true;
    }

    /// <summary>Import the instructions for the given basic block.</summary>
    /// <param name="block"></param>
    /// <remarks>
    ///   <para>Perform verification, throwing an exception on failure.</para>
    ///   <para>Push any successor blocks that are enabled for the first time, or whose verification pre-state is changed.</para>
    /// </remarks>
    public void impImportBlock(BasicBlock block)
    {
        // BBF_INTERNAL blocks only exist during importation due to EH canonicalization. We need to
        // handle them specially. In particular, there is no IL to import for them, but we do need
        // to mark them as imported and put their successors on the pending import list.
        if (block.HasFlag(BBF_INTERNAL))
        {
            JITDUMP($"Marking BBF_INTERNAL block {FMT_BB(block.bbNum)} as BBF_IMPORTED\n");
            block.SetFlags(BBF_IMPORTED);

            foreach (var succBlock in block.Succs)
            {
                impImportBlockPending(succBlock);
            }
            return;
        }

        bool markImport;

        // Make the block globally available
        compCurBB = block;

#if DEBUG
        // Initialize the debug variables
        impCurOpcName = "unknown";
        impCurOpcOffs = block.bbCodeOffs;
#endif

        // Set the current stack state to the merged result
        resetCurrentState(block, ref stackState);

        if (block.hasTryIndex)
        {
            impVerifyEHBlock(block);
        }

        // Now walk the code and import the IL into GenTrees.
        impImportBlockCode(block);

        if (compDonotInline)
        {
            return;
        }
        markImport = false;

        // If the stack is non-empty, we might have to spill its contents
        var spillStack = stackState.esStackDepth is not 0;
        var reimportSpillClique = false;

        while (spillStack)
        {
            // assume the main loop below will handle everything
            spillStack = false;

            // input temps assigned to successor blocks
            var baseTmp = NO_BASE_TMP;

            var tgtBlock = null as BasicBlock;
            var addStmt = null as Statement;

            // if a box temp is used in a block that leaves something on the stack, its lifetime is hard to determine, simply don't reuse such temps.
            impBoxTemp = BAD_VAR_NUM;

            // Do the successors of 'block' have any other predecessors ?
            // We do not want to do some of the optimizations related to multiRef if we can reimport blocks

            var multRef = impCanReimport ? -1 : 0;

            switch (block.Kind)
            {
                case BBJ_COND:
                {
                    addStmt = impExtractLastStmt();
                    assert(addStmt.RootNode.Oper is GT_JTRUE);

                    tgtBlock = block.FalseTarget;

                    // Note if the next block has more than one ancestor
                    multRef |= tgtBlock.bbRefs;

                    // Does the next block have temps assigned?
                    baseTmp = tgtBlock.bbStkTempsIn;

                    if (baseTmp != NO_BASE_TMP)
                    {
                        break;
                    }

                    // Try the target of the jump then
                    tgtBlock = block.TrueTarget;

                    multRef |= tgtBlock.bbRefs;
                    baseTmp = tgtBlock.bbStkTempsIn;
                    break;
                }

                case BBJ_ALWAYS:
                {
                    tgtBlock = block.Target;
                    multRef |= tgtBlock.bbRefs;
                    baseTmp = tgtBlock.bbStkTempsIn;
                    break;
                }

                case BBJ_SWITCH:
                {
                    addStmt = impExtractLastStmt();
                    assert(addStmt.RootNode.Oper is GT_SWITCH);

                    foreach (var switchSucc in block.SwitchSuccs)
                    {
                        tgtBlock = switchSucc;
                        multRef |= tgtBlock.bbRefs;

                        // Thanks to spill cliques, we should have assigned all or none
                        assert((baseTmp == NO_BASE_TMP) || (baseTmp == tgtBlock.bbStkTempsIn));
                        baseTmp = tgtBlock.bbStkTempsIn;

                        if (multRef > 1)
                        {
                            break;
                        }
                    }
                    break;
                }

                case BBJ_CALLFINALLY:
                case BBJ_EHCATCHRET:
                case BBJ_RETURN:
                case BBJ_EHFINALLYRET:
                case BBJ_EHFAULTRET:
                case BBJ_EHFILTERRET:
                case BBJ_THROW:
                {
                    BADCODE("can't have 'unreached' end of BB with non-empty stack");
                    break;
                }

                default:
                {
                    NO_WAY("Unexpected bbKind");
                    break;
                }
            }

            assert(multRef >= 1);

            // Do we have a base temp number?
            var newTemps = baseTmp is NO_BASE_TMP;

            if (newTemps)
            {
                // Grab enough temps for the whole stack
                baseTmp = impGetSpillTmpBase(block);
            }

            // Spill all stack entries into temps
            JITDUMP("\nSpilling stack entries into temps\n");

            var stack = stackState.esStack.AsSpan(0, stackState.esStackDepth);

            for (int level = 0, tempNum = baseTmp; level < stack.Length; level++, tempNum++)
            {
                ref var stackEntry = ref stack[level];
                var tree = stackEntry.val;

                var treeType = tree.Type;
                var treeActualType = treeType.ActualType;

                // VC generates code where it pushes a byref from one branch, and an int (ldc.i4 0) from
                // the other. This should merge to a byref in unverifiable code.
                // However, if the branch which leaves the TYP_I_IMPL on the stack is imported first, the
                // successor would be imported assuming there was a TYP_I_IMPL on
                // the stack. Thus the value would not get GC-tracked. Hence,
                // change the temp to TYP_BYREF and reimport the clique.
                ref var tempDsc = ref lvaGetDesc(tempNum);

                if ((tree.Type is TYP_BYREF) && (tempDsc.Type is TYP_I_IMPL))
                {
                    tempDsc.Type = TYP_BYREF;
                    reimportSpillClique = true;
                }

#if TARGET_64BIT
                if ((treeActualType is TYP_I_IMPL) && (tempDsc.Type is TYP_INT))
                {
                    // Some other block in the spill clique set this to "int", but now we have "native int".
                    // Change the type and go back to re-import any blocks that used the wrong type.
                    tempDsc.Type = TYP_I_IMPL;
                    reimportSpillClique = true;
                }
                else if ((treeActualType is TYP_INT) && (tempDsc.Type is TYP_I_IMPL))
                {
                    // Spill clique has decided this should be "native int", but this block only pushes an "int".
                    // Insert a sign-extension to "native int" so we match the clique.
                    stackEntry.val = gtNewCastNode(TYP_I_IMPL, tree, fromUnsigned: false, TYP_I_IMPL);
                }

                // Consider the case where one branch left a 'byref' on the stack and the other leaves
                // an 'int'. On 32-bit, this is allowed (in non-verifiable code) since they are the same
                // size. JIT64 managed to make this work on 64-bit. For compatibility, we support JIT64
                // behavior instead of asserting and then generating bad code (where we save/restore the
                // low 32 bits of a byref pointer to an 'int' sized local). If the 'int' side has been
                // imported already, we need to change the type of the local and reimport the spill clique.
                // If the 'byref' side has imported, we insert a cast from int to 'native int' to match
                // the 'byref' size.
                if ((treeActualType is TYP_BYREF) && (tempDsc.Type is TYP_INT))
                {
                    // Some other block in the spill clique set this to "int", but now we have "byref".
                    // Change the type and go back to re-import any blocks that used the wrong type.
                    tempDsc.Type = TYP_BYREF;
                    reimportSpillClique = true;
                }
                else if ((treeActualType is TYP_INT) && (tempDsc.Type is TYP_BYREF))
                {
                    // Spill clique has decided this should be "byref", but this block only pushes an "int".
                    // Insert a sign-extension to "native int" so we match the clique size.
                    stackEntry.val = gtNewCastNode(TYP_I_IMPL, tree, fromUnsigned: false, TYP_I_IMPL);
                }

#endif

                if ((treeType is TYP_DOUBLE) && (tempDsc.Type is TYP_FLOAT))
                {
                    // Some other block in the spill clique set this to "float", but now we have "double".
                    // Change the type and go back to re-import any blocks that used the wrong type.
                    tempDsc.Type = TYP_DOUBLE;
                    reimportSpillClique = true;
                }
                else if ((treeType is TYP_FLOAT) && (tempDsc.Type is TYP_DOUBLE))
                {
                    // Spill clique has decided this should be "double", but this block only pushes a "float".
                    // Insert a cast to "double" so we match the clique.
                    stackEntry.val = gtNewCastNode(TYP_DOUBLE, tree, false, TYP_DOUBLE);
                }

                // If addStmt has a reference to tempNum (can only happen if we are spilling to the temps already used by a previous block), we need to spill addStmt

                if ((addStmt is not null) && !newTemps && gtHasRef(addStmt.RootNode, tempNum))
                {
                    var addTree = addStmt.RootNode;

                    if (addTree.Oper is GT_JTRUE)
                    {
                        var compare = addTree.AsOp().Op1.AsOp();
                        assert(compare.Oper.IsCompare);

                        ref var compareOp1Ref = ref compare.Op1Ref;
                        var type = compareOp1Ref.Type.ActualType;

                        if (gtHasRef(compareOp1Ref, tempNum))
                        {
                            var lclNum = lvaGrabTemp(shortLifetime: true, "spill addStmt JTRUE ref Op1");
                            impStoreToTemp(lclNum, compareOp1Ref, level);
                            type = lvaTable[lclNum].Type.ActualType;
                            compareOp1Ref = gtNewLclvNode(type, lclNum);
                        }

                        ref var compareOp2Ref = ref compare.Op2Ref;

                        if (gtHasRef(compareOp2Ref, tempNum))
                        {
                            var lclNum = lvaGrabTemp(shortLifetime: true, "spill addStmt JTRUE ref Op2");
                            impStoreToTemp(lclNum, compareOp2Ref, level);
                            type = lvaTable[lclNum].Type.ActualType;
                            compareOp2Ref = gtNewLclvNode(type, lclNum);
                        }
                    }
                    else
                    {
                        assert(addTree.Oper is GT_SWITCH);

                        var valueRef = addTree.AsOp().Op1;
                        assert(genActualTypeIsIntOrI(valueRef.Type));

                        var lclNum = lvaGrabTemp(shortLifetime: true, "spill addStmt SWITCH");
                        impStoreToTemp(lclNum, valueRef, level);
                        valueRef = gtNewLclvNode(valueRef.Type.ActualType, lclNum);
                    }
                }

                // Spill the stack entry, and replace with the temp
                if (!impSpillStackEntry(level, tempNum, assertOnRecursion: true, "Spill Stack Entry"))
                {
                    if (markImport)
                    {
                        BADCODE("bad stack state");
                    }

                    // We failed to spill, so we need to restart the outer loop
                    spillStack = stackState.esStackDepth is not 0;
                    addStmt = null;
                    break;
                }
            }

            if (addStmt is not null)
            {
                // Put back the 'jtrue'/'switch' if we removed it earlier
                impAppendStmt(addStmt, CHECK_SPILL_NONE);
            }
        }

        // Some of the append/spill logic works on compCurBB
        assert(compCurBB == block);

        /* Save the tree list in the block */
        impEndTreeList(block);

        // impEndTreeList sets BBF_IMPORTED on the block
        // We do *NOT* want to set it later than this because
        // impReimportSpillClique might clear it if this block is both a
        // predecessor and successor in the current spill clique
        assert(block.HasFlag(BBF_IMPORTED));

        // If we had a int/native int, or float/double collision, we need to re-import
        if (reimportSpillClique)
        {
            // This will re-import all the successors of block (as well as each of their predecessors)
            impReimportSpillClique(block);

            // We don't expect to see BBJ_EHFILTERRET here.
            assert(block.Kind is not BBJ_EHFILTERRET);

            foreach (var succ in block.Succs)
            {
                if (!succ.HasFlag(BBF_IMPORTED))
                {
                    impImportBlockPending(succ);
                }
            }
        }
        else
        {
            // the normal case: otherwise just import the successors of block

            // Does this block jump to any other blocks?
            // Filter successor from BBJ_EHFILTERRET have already been handled above in the call
            // to impVerifyEHBlock().
            if (block.Kind is not BBJ_EHFILTERRET)
            {
                foreach (var succ in block.Succs)
                {
                    impImportBlockPending(succ);
                }
            }
        }
    }

#if DEBUG
    private static ConfigMethodRange s_jitEnablePatchpointRange;
#endif

    public unsafe void impImportBlockCode(BasicBlock block)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf($"\nImporting {FMT_BB(block.bbNum)} (PC={block.bbCodeOffs:D3}) of '{info.compFullName}'");
        }
#endif

        var nxtStmtIndex = impInitBlockLineInfo();
        var nxtStmtOffs = BAD_IL_OFFSET;

        // Get the tree list started
        impBeginTreeList();

#if FEATURE_ON_STACK_REPLACEMENT
        var enableOSR = !opts.compDbgCode && opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0) && (JitConfig[ConfigInteger.TC_OnStackReplacement] > 0);
        var enablePartialCompilation = opts.jitFlags->IsSet(JitFlags.JIT_FLAG_TIER0) && (JitConfig[ConfigInteger.TC_PartialCompilation] > 0);

#if DEBUG
        // Optionally suppress patchpoints by method hash
        s_jitEnablePatchpointRange.EnsureInit(JitConfig[ConfigString.JitEnablePatchpointRange]);

        var inRange = s_jitEnablePatchpointRange.Contains(impInlineRoot.info.compMethodHash());
        enableOSR &= inRange;
        enablePartialCompilation &= inRange;
#endif

        if (enableOSR)
        {
            // We don't inline at Tier0, if we do, we may need rethink our approach.
            // Could probably support inlines that don't introduce flow.
            assert(!compIsForInlining);

            // OSR is not yet supported for methods with explicit tail calls.
            //
            // But we also do not have to switch these methods to be optimized, as we should be
            // able to avoid getting trapped in Tier0 code by normal call counting.
            //
            // So instead, just suppress adding patchpoints.

            if (!compTailPrefixSeen)
            {
                // We only need to add patchpoints if the method can loop.
                if (compHasBackwardJump)
                {
                    assert(compCanHavePatchpoints());

                    // By default we use the "adaptive" strategy.
                    //
                    // This can create both source and target patchpoints within a given
                    // loop structure, which isn't ideal, but is not incorrect. We will
                    // just have some extra Tier0 overhead.
                    //
                    // Todo: implement support for mid-block patchpoints. If `block`
                    // is truly a backedge source (and not in a handler) then we should be
                    // able to find a stack empty point somewhere in the block.

                    var patchpointStrategy = JitConfig[ConfigInteger.TC_PatchpointStrategy];
                    var addPatchpoint = false;
                    var mustUseTargetPatchpoint = false;

                    switch (patchpointStrategy)
                    {
                        default:
                        {
                            // Patchpoints at backedge sources, if possible, otherwise targets.
                            addPatchpoint = block.HasFlag(BBF_BACKWARD_JUMP_SOURCE);
                            mustUseTargetPatchpoint = (stackState.esStackDepth is not 0) || block.hasHndIndex;
                            break;
                        }

                        case 1:
                        {
                            // Patchpoints at stackempty backedge targets.
                            // Note if we have loops where the IL stack is not empty on the backedge we can't patchpoint them.
                            //
                            // We should not have allowed OSR if there were backedges in handlers.

                            assert(!block.hasHndIndex);
                            addPatchpoint = block.HasFlag(BBF_BACKWARD_JUMP_TARGET) && (stackState.esStackDepth is 0);
                            break;
                        }

                        case 2:
                        {
                            // Adaptive strategy.
                            //
                            // Patchpoints at backedge targets if there are multiple backedges,
                            // otherwise at backedge sources, if possible. Note a block can be both; if so we
                            // just need one patchpoint.

                            if (block.HasFlag(BBF_BACKWARD_JUMP_TARGET))
                            {
                                // We don't know backedge count, so just use ref count.
                                //
                                addPatchpoint = (block.bbRefs > 1) && (stackState.esStackDepth is 0);
                            }

                            if (!addPatchpoint && block.HasFlag(BBF_BACKWARD_JUMP_SOURCE))
                            {
                                addPatchpoint = true;
                                mustUseTargetPatchpoint = (stackState.esStackDepth is not 0) || block.hasHndIndex;

                                // Also force target patchpoint if target block has multiple (backedge) preds.
                                //
                                if (!mustUseTargetPatchpoint)
                                {
                                    foreach (var succBlock in block.Succs)
                                    {
                                        if ((succBlock.bbNum <= block.bbNum) && (succBlock.bbRefs > 1))
                                        {
                                            mustUseTargetPatchpoint = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    }

                    if (addPatchpoint)
                    {
                        if (mustUseTargetPatchpoint)
                        {
                            // We wanted a source patchpoint, but could not have one.
                            // So, add patchpoints to the backedge targets.

                            foreach (var succBlock in block.Succs)
                            {
                                if (succBlock.bbNum <= block.bbNum)
                                {
                                    // The succBlock had better agree it's a target.
                                    assert(succBlock.HasFlag(BBF_BACKWARD_JUMP_TARGET));

                                    // We may already have decided to put a patchpoint in succBlock. If not, add one.
                                    if (succBlock.HasFlag(BBF_OSR_PATCHPOINT))
                                    {
                                        // In some cases the target may not be stack-empty at entry.
                                        // If so, we will bypass patchpoints for this backedge.

                                        if (succBlock.bbStackDepthOnEntry > 0)
                                        {
                                            JITDUMP($"\nCan't set source patchpoint at {FMT_BB(block.bbNum)}, can't use target {FMT_BB(succBlock.bbNum)} as it has non-empty stack on entry.\n");
                                        }
                                        else
                                        {
                                            JITDUMP($"\nCan't set source patchpoint at {FMT_BB(block.bbNum)}, using target {FMT_BB(succBlock.bbNum)} instead\n");
                                            assert(!succBlock.hasHndIndex);
                                            succBlock.SetFlags(BBF_OSR_PATCHPOINT);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            assert(!block.hasHndIndex);
                            block.SetFlags(BBF_OSR_PATCHPOINT);
                        }

                        MethodHasPatchpoint = true;
                    }
                }
                else
                {
                    // Should not see backward branch targets w/o backwards branches.
                    // So if !compHasBackwardsBranch, these flags should never be set.
                    assert(block.HasAnyFlag(BBF_BACKWARD_JUMP_TARGET | BBF_BACKWARD_JUMP_SOURCE) != 0);
                }
            }

#if DEBUG
            // As a stress test, we can place patchpoints at the start of any block
            // that is a stack empty point and is not within a handler.
            //
            // Todo: enable for mid-block stack empty points too.
            //
            var offsetOSR = JitConfig[ConfigInteger.JitOffsetOnStackReplacement];
            var randomOSR = JitConfig[ConfigInteger.JitRandomOnStackReplacement];
            var tryOffsetOSR = offsetOSR >= 0;
            var tryRandomOSR = randomOSR > 0;

            if (compCanHavePatchpoints() && (tryOffsetOSR || tryRandomOSR) && (stackState.esStackDepth is 0) && !block.hasHndIndex && !block.HasFlag(BBF_OSR_PATCHPOINT))
            {
                // Block start can have a patchpoint. See if we should add one.
                var addPatchpoint = false;

                if (tryOffsetOSR)
                {
                    // Specific offset

                    if (impCurOpcOffs == (uint)offsetOSR)
                    {
                        addPatchpoint = true;
                    }
                }
                else
                {
                    // Random
                    //
                    // Reuse the random inliner's random state.
                    // Note _inlineStrategy is always created, even if we're not inlining.

                    var inlineStrategy = impInlineRoot._inlineStrategy;
                    assert(inlineStrategy is not null);

                    var randomValue = inlineStrategy.GetRandom(randomOSR).Next(100);
                    addPatchpoint = (randomValue < randomOSR);
                }

                if (addPatchpoint)
                {
                    block.SetFlags(BBF_OSR_PATCHPOINT);
                    MethodHasPatchpoint = true;
                }

                JITDUMP($"\n** {(tryOffsetOSR ? "offset" : "random")} patchpoint{(addPatchpoint ? "" : " not")} added to {FMT_BB(block.bbNum)} (il offset {impCurOpcOffs})\n");
            }
#endif
        }

        // Mark stack-empty rare blocks to be considered for partial compilation.
        //
        // Ideally these are conditionally executed blocks -- if the method is going
        // to unconditionally throw, there's not as much to be gained by deferring jitting.
        // For now, we just screen out the entry bb.
        //
        // In general we might want track all the IL stack empty points so we can
        // propagate rareness back through flow and place the partial compilation patchpoints "earlier"
        // so there are fewer overall.
        //
        // Note unlike OSR, it's ok to forgo these.
        //
        // For runtime async we cannot allow partial compilation as that removes IR from the blocks
        // that we need to do proper liveness analysis.

        if (enablePartialCompilation && compCanHavePatchpoints() && !compTailPrefixSeen && !compIsAsync && (stackState.esStackDepth is 0) && !block.HasFlag(BBF_OSR_PATCHPOINT) && !block.hasHndIndex)
        {
            // Is this block a good place for partial compilation?
            var addPartialCompilationPatchpoint = (block != fgFirstBB) && block.isRunRarely;

#if DEBUG
            // Stress mode
            var reason = "rarely run";
            var randomPartialCompilation = JitConfig[ConfigInteger.JitRandomPartialCompilation];

            if (randomPartialCompilation > 0)
            {
                // Reuse the random inliner's random state.
                // Note _inlineStrategy is always created, even if we're not inlining.

                var inlineStrategy = impInlineRoot._inlineStrategy;
                assert(inlineStrategy is not null);

                var randomValue = inlineStrategy.GetRandom(randomPartialCompilation).Next(100);
                addPartialCompilationPatchpoint = (randomValue < randomPartialCompilation);
                reason = "randomly chosen";
            }
#endif

            if (addPartialCompilationPatchpoint)
            {
                JITDUMP($"\nBlock {FMT_BB(block.bbNum)} ({reason}) will be a partial compilation patchpoint -- not importing\n");

                block.SetFlags(BBF_PARTIAL_COMPILATION_PATCHPOINT);
                MethodHasPatchpoint = true;

                // Block will no longer flow to any of its successors.
                foreach (var succ in block.Succs)
                {
                    // We may have degenerate flow, make sure to fully remove
                    fgRemoveAllRefPreds(succ, block);
                }

                // Change block to BBJ_THROW so we won't trigger importation of successors.
                block.SetKindAndTargetEdge(BBJ_THROW, targetEdge: null);

                // If this method has a explicit generic context, the only uses of it may be in
                // the IL for this block. So assume it's used.
                //
                if ((info.compMethodInfo->options & (CORINFO_GENERICS_CTXT_FROM_METHODDESC | CORINFO_GENERICS_CTXT_FROM_METHODTABLE)) != 0)
                {
                    lvaGenericsContextInUse = true;
                }
                return;
            }
        }
#endif

        // Walk the opcodes that comprise the basic block

        var codeAddr = info.compCode + block.bbCodeOffs;
        var codeEndp = info.compCode + block.bbCodeOffsEnd;

        var opcodeOffs = block.bbCodeOffs;
        var lastSpillOffs = opcodeOffs;

        var prevOpcode = CEE_ILLEGAL;
        var callTyp = TYP_COUNT;

        if (block.CatchType is not BBCT_NONE)
        {
            if ((info.compStmtOffsetsImplicit & ICorDebugInfo.CALL_SITE_BOUNDARIES) != 0)
            {
                impCurStmtOffsSet(block.bbCodeOffs);
            }

            // We will spill the GT_CATCH_ARG and the input of the BB_QMARK block
            // to a temp. This is a trade off for code simplicity
            impSpillSpecialSideEff();
        }

        while (codeAddr < codeEndp)
        {
            //---------------------------------------------------------------------

            /* We need to restrict the max tree depth as many of the Compiler
               functions are recursive. We do this by spilling the stack */

            if ((stackState.esStackDepth) != 0)
            {
                // Has it been a while since we last saw a non-empty stack (which guarantees that the tree depth isnt accumulating.

                if (((opcodeOffs - lastSpillOffs) > MAX_TREE_SIZE) && impCanSpillNow(prevOpcode))
                {
                    impSpillStackEnsure();
                    lastSpillOffs = opcodeOffs;
                }
            }
            else
            {
                // nothing on the stack, box temp OK to use again
                lastSpillOffs = opcodeOffs;
                impBoxTempInUse = false;
            }

            // Compute the current instr offset
            opcodeOffs = (IL_OFFSET)(codeAddr - info.compCode);

#if !DEBUG
            if (opts.compDbgInfo)
#endif
            {
                nxtStmtOffs = (nxtStmtIndex < info.compStmtOffsetsCount) ? info.compStmtOffsets[nxtStmtIndex] : BAD_IL_OFFSET;

                // Have we reached the next stmt boundary?

                if ((nxtStmtOffs != BAD_IL_OFFSET) && (opcodeOffs >= nxtStmtOffs))
                {
                    assert(nxtStmtOffs == info.compStmtOffsets[nxtStmtIndex]);

                    if ((stackState.esStackDepth != 0) && opts.compDbgCode)
                    {
                        // We need to provide accurate IP-mapping at this point.
                        // So spill anything on the stack so that it will form gtStmts with the correct stmt offset noted
                        impSpillStackEnsure(spillLeaves: true);
                    }

                    // Have we reported debug info for any tree?

                    if (impCurStmtDI.IsValid && opts.compDbgCode)
                    {
                        var placeHolder = new GenTree(GT_NO_OP, TYP_VOID);
                        impAppendTree(placeHolder, CHECK_SPILL_NONE, impCurStmtDI);
                        assert(!impCurStmtDI.IsValid);
                    }

                    if (!impCurStmtDI.IsValid)
                    {
                        // Make sure that nxtStmtIndex is in sync with opcodeOffs.
                        // If opcodeOffs has gone past nxtStmtIndex, catch up

                        while (((nxtStmtIndex + 1) < info.compStmtOffsetsCount) && (info.compStmtOffsets[nxtStmtIndex + 1] <= opcodeOffs))
                        {
                            nxtStmtIndex++;
                        }

                        // Go to the new stmt
                        impCurStmtOffsSet(info.compStmtOffsets[nxtStmtIndex]);

                        // Update the stmt boundary index
                        nxtStmtIndex++;
                        assert(nxtStmtIndex <= info.compStmtOffsetsCount);

                        // Are there any more line# entries after this one?

                        if (nxtStmtIndex < info.compStmtOffsetsCount)
                        {
                            // Remember where the next line# starts
                            nxtStmtOffs = info.compStmtOffsets[nxtStmtIndex];
                        }
                        else
                        {
                            // No more line# entries
                            nxtStmtOffs = BAD_IL_OFFSET;
                        }
                    }
                }
                else if (((info.compStmtOffsetsImplicit & ICorDebugInfo.STACK_EMPTY_BOUNDARIES) != 0) && (stackState.esStackDepth is 0))
                {
                    // At stack-empty locations, we have already added the tree to the stmt list with the last offset.
                    // We just need to update impCurStmtDI
                    impCurStmtOffsSet(opcodeOffs);
                }
                else if (((info.compStmtOffsetsImplicit & ICorDebugInfo.CALL_SITE_BOUNDARIES) != 0) && impOpcodeIsCallSiteBoundary(prevOpcode))
                {
                    // Make sure we have a type cached
                    assert(callTyp != TYP_COUNT);

                    if (callTyp == TYP_VOID)
                    {
                        impCurStmtOffsSet(opcodeOffs);
                    }
                    else if (opts.compDbgCode)
                    {
                        impSpillStackEnsure(spillLeaves: true);
                        impCurStmtOffsSet(opcodeOffs);
                    }
                }
                else if (((info.compStmtOffsetsImplicit & ICorDebugInfo.NOP_BOUNDARIES) != 0) && (prevOpcode == CEE_NOP))
                {
                    if (opts.compDbgCode)
                    {
                        impSpillStackEnsure(spillLeaves: true);
                    }
                    impCurStmtOffsSet(opcodeOffs);
                }

                assert(!impCurStmtDI.IsValid || (nxtStmtOffs == BAD_IL_OFFSET) || (impCurStmtDI.Location.Offset <= nxtStmtOffs));
            }

            // Get the next opcode and the size of its parameters

            var prefixFlags = 0;
            var opcode = (OPCODE)(codeAddr[0]);
            codeAddr += sizeof(byte);

#if DEBUG
            impCurOpcOffs = (IL_OFFSET)(codeAddr - info.compCode - 1);
            JITDUMP($"\n    [{stackState.esStackDepth:D2}] {impCurOpcOffs:D3} (0x{impCurOpcOffs:X3}) ");
#endif
            var constrainedResolvedToken = new CORINFO_RESOLVED_TOKEN();

            while (opcode == CEE_PREFIX1)
            {
                opcode = (OPCODE)(0x0100 + codeAddr[0]);
                opcodeOffs = (IL_OFFSET)(codeAddr - info.compCode);
                codeAddr += sizeof(byte);

                switch (opcode)
                {
                    case CEE_UNALIGNED:
                    {
                        assert(opcode.Size is 1);
                        prefixFlags |= PREFIX_UNALIGNED;

                        var val = codeAddr[0];
                        codeAddr += sizeof(byte);
                        JITDUMP($" {val}");

                        if ((val is not 1) && (val is not 2) && (val is not 4))
                        {
                            BADCODE("Alignment unaligned. must be 1, 2, or 4");
                        }
                        impValidateMemoryAccessOpcode(codeAddr, codeEndp, volatilePrefix: false);

                        opcode = (OPCODE)(codeAddr[0]);
                        opcodeOffs = (IL_OFFSET)(codeAddr - info.compCode);
                        codeAddr += sizeof(byte);
                        break;
                    }

                    case CEE_VOLATILE:
                    {
                        assert(opcode.Size is 0);
                        prefixFlags |= PREFIX_VOLATILE;

                        impValidateMemoryAccessOpcode(codeAddr, codeEndp, volatilePrefix: true);

                        opcode = (OPCODE)(codeAddr[0]);
                        opcodeOffs = (IL_OFFSET)(codeAddr - info.compCode);
                        codeAddr += sizeof(byte);
                        break;
                    }

                    case CEE_TAILCALL:
                    {
                        assert(opcode.Size is 0);
                        prefixFlags |= PREFIX_TAILCALL_EXPLICIT;
                        JITDUMP(" tail.");

                        var actualOpcode = impGetNonPrefixOpcode(codeAddr, codeEndp);

                        if (!impOpcodeIsCallOpcode(actualOpcode))
                        {
                            BADCODE("tailcall. has to be followed by call, callvirt or calli");
                        }

                        opcode = (OPCODE)(codeAddr[0]);
                        opcodeOffs = (IL_OFFSET)(codeAddr - info.compCode);
                        codeAddr += sizeof(byte);
                        break;
                    }

                    case CEE_CONSTRAINED:
                    {
                        assertImp(opcode.Size == sizeof(int));
                        prefixFlags |= PREFIX_CONSTRAINED;

                        impResolveToken(codeAddr, out constrainedResolvedToken, CORINFO_TOKENKIND_Constrained);
                        JITDUMP($" ({constrainedResolvedToken.token:X8}) ");

                        // prefix instructions must increment codeAddr manually
                        codeAddr += sizeof(uint);

                        var actualOpcode = impGetNonPrefixOpcode(codeAddr, codeEndp);

                        if (actualOpcode is not CEE_CALLVIRT and not CEE_CALL and not CEE_LDFTN)
                        {
                            BADCODE("constrained. has to be followed by callvirt, call or ldftn");
                        }

                        opcode = (OPCODE)(codeAddr[0]);
                        opcodeOffs = (IL_OFFSET)(codeAddr - info.compCode);
                        codeAddr += sizeof(byte);
                        break;
                    }

                    case CEE_READONLY:
                    {
                        assert(opcode.Size is 0);
                        prefixFlags |= PREFIX_READONLY;
                        JITDUMP(" readonly.");

                        var actualOpcode = impGetNonPrefixOpcode(codeAddr, codeEndp);

                        if ((actualOpcode is not CEE_LDELEMA) && !impOpcodeIsCallOpcode(actualOpcode))
                        {
                            BADCODE("readonly. has to be followed by ldelema or call");
                        }

                        opcode = (OPCODE)(codeAddr[0]);
                        opcodeOffs = (IL_OFFSET)(codeAddr - info.compCode);
                        codeAddr += sizeof(byte);
                        break;
                    }

                    default:
                    {
                        break;
                    }
                }
            }

            // Return if any previous code has caused inline to fail.
            if (compDonotInline)
            {
                return;
            }

            // Get the size of additional parameters
            var sz = opcode.Size;

#if DEBUG
            impCurOpcOffs = (IL_OFFSET)(codeAddr - info.compCode - 1);
            impCurOpcName = opcode.Name;

            if (verbose && (opcode != CEE_PREFIX1))
            {
                jitprintf(impCurOpcName);
            }
#endif

            switch (opcode)
            {
                case CEE_LDNULL:
                {
                    var intCon = gtNewIconNode(TYP_REF, 0);
                    impPushOnStack(intCon, new typeInfo(TYP_REF));
                    break;
                }

                case CEE_LDC_I4_M1:
                case CEE_LDC_I4_0:
                case CEE_LDC_I4_1:
                case CEE_LDC_I4_2:
                case CEE_LDC_I4_3:
                case CEE_LDC_I4_4:
                case CEE_LDC_I4_5:
                case CEE_LDC_I4_6:
                case CEE_LDC_I4_7:
                case CEE_LDC_I4_8:
                {
                    var value = (opcode - CEE_LDC_I4_0);
                    assert(value is >= -1 and <= 8);
                    PushI4Con(this, value);
                    break;
                }

                case CEE_LDC_I4_S:
                {
                    var value = unchecked((sbyte)(codeAddr[0]));
                    PushI4Con(this, value);
                    break;
                }

                case CEE_LDC_I4:
                {
                    var value = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                    PushI4Con(this, value);
                    break;
                }

                case CEE_LDC_I8:
                {
                    var value = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(long)));
                    var lcon = gtNewLconNode(value);
                    impPushOnStack(lcon, new typeInfo(TYP_LONG));
                    JITDUMP($" 0x{value:X16}");
                    break;
                }

                case CEE_LDC_R8:
                {
                    var value = BinaryPrimitives.ReadDoubleLittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(double)));
                    var dcon = gtNewDconNode(TYP_DOUBLE, value);
                    impPushOnStack(dcon, new typeInfo(TYP_DOUBLE));
                    JITDUMP($" {value:G17}");
                    break;
                }

                case CEE_LDC_R4:
                {
                    var value = BinaryPrimitives.ReadSingleLittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(float)));
                    var dcon = gtNewDconNode(TYP_FLOAT, value);
                    impPushOnStack(dcon, new typeInfo(TYP_FLOAT));
                    JITDUMP($" {value:G17}");
                    break;
                }

                case CEE_LDSTR:
                {
                    var value = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                    var scon = gtNewSconNode(value, info.compScopeHnd);
                    impPushOnStack(scon, new typeInfo());
                    JITDUMP($" {value:X8}");
                    break;
                }

                case CEE_LDARG:
                {
                    var lclNum = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));
                    JITDUMP($" {lclNum}");
                    impLoadArg(lclNum, opcodeOffs + sz + 1);
                    break;
                }

                case CEE_LDARG_S:
                {
                    var lclNum = codeAddr[0];
                    JITDUMP($" {lclNum}");
                    impLoadArg(lclNum, opcodeOffs + sz + 1);
                    break;
                }

                case CEE_LDARG_0:
                case CEE_LDARG_1:
                case CEE_LDARG_2:
                case CEE_LDARG_3:
                {
                    var lclNum = (opcode - CEE_LDARG_0);
                    assert(lclNum is >= 0 and <= 3);
                    impLoadArg(lclNum, opcodeOffs + sz + 1);
                    break;
                }

                case CEE_LDLOC:
                {
                    var lclNum = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));
                    JITDUMP($" {lclNum}");
                    impLoadLoc(lclNum, opcodeOffs + sz + 1);
                    break;
                }

                case CEE_LDLOC_S:
                {
                    var lclNum = codeAddr[0];
                    JITDUMP($" {lclNum}");
                    impLoadLoc(lclNum, opcodeOffs + sz + 1);
                    break;
                }

                case CEE_LDLOC_0:
                case CEE_LDLOC_1:
                case CEE_LDLOC_2:
                case CEE_LDLOC_3:
                {
                    var lclNum = (opcode - CEE_LDLOC_0);
                    assert(lclNum is >= 0 and <= 3);
                    impLoadLoc(lclNum, opcodeOffs + sz + 1);
                    break;
                }

                case CEE_STARG:
                {
                    var lclNum = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));
                    Starg(this, block, TYP_UNKNOWN, lclNum, clsHnd: null);
                    break;
                }

                case CEE_STARG_S:
                {
                    var lclNum = codeAddr[0];
                    Starg(this, block, TYP_UNKNOWN, lclNum, clsHnd: null);
                    break;
                }

                case CEE_STLOC:
                {
                    var lclNum = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));
                    JITDUMP($" {lclNum}");
                    LocSt(this, block, TYP_UNKNOWN, lclNum, clsHnd: null);
                    break;
                }

                case CEE_STLOC_S:
                {
                    var lclNum = codeAddr[0];
                    JITDUMP($" {lclNum}");
                    LocSt(this, block, TYP_UNKNOWN, lclNum, clsHnd: null);
                    break;
                }

                case CEE_STLOC_0:
                case CEE_STLOC_1:
                case CEE_STLOC_2:
                case CEE_STLOC_3:
                {
                    var lclNum = (opcode - CEE_STLOC_0);
                    assert(lclNum is >= 0 and <= 3);
                    LocSt(this, block, TYP_UNKNOWN, lclNum, clsHnd: null);
                    break;
                }

                case CEE_LDLOCA:
                {
                    var lclNum = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));
                    Ldloca(this, lclNum);
                    break;
                }

                case CEE_LDLOCA_S:
                {
                    var lclNum = codeAddr[0];
                    Ldloca(this, lclNum);
                    break;
                }

                case CEE_LDARGA:
                {
                    var lclNum = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(ushort)));

                    if (!TryLdarga(this, lclNum))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDARGA_S:
                {
                    var lclNum = codeAddr[0];

                    if (!TryLdarga(this, lclNum))
                    {
                        return;
                    }
                    break;
                }

                case CEE_ARGLIST:
                {
                    if (!info.compIsVarArgs)
                    {
                        BADCODE("arglist in non-vararg method");
                    }

                    assertImp((info.compMethodInfo->args.callConv & CORINFO_CALLCONV_MASK) == CORINFO_CALLCONV_VARARG);

                    // The ARGLIST cookie is a hidden 'last' parameter, we have already
                    // adjusted the arg count cos this is like fetching the last param.
                    assertImp(info.compArgsCount > 0);

                    var clsHnd = impRuntimeArgumentHandle;

                    var argListTmp = lvaGrabTemp(shortLifetime: false, "arglist tmp");
                    lvaSetStruct(argListTmp, clsHnd, unsafeValueClsCheck: false);

                    var op1 = gtNewLclVarAddrNode(TYP_I_IMPL, lvaVarargsHandleArg) as GenTreeLclVarCommon;
                    impAppendTree(gtNewStoreLclFldNode(TYP_I_IMPL, argListTmp, offset: 0, op1), CHECK_SPILL_ALL, impCurStmtDI);

                    op1 = gtNewLclVarNode(TYP_STRUCT, argListTmp);
                    impPushOnStack(op1, makeTypeInfo(clsHnd));
                    break;
                }

                case CEE_ENDFINALLY:
                {
                    if (compIsForInlining && !opts.compInlineMethodsWithEH)
                    {
                        NO_WAY("Shouldn't have exception handlers in the inlinee!");
                        compInlineResult.NoteFatal(InlineObservation.CALLEE_HAS_ENDFINALLY);
                        return;
                    }

                    if (stackState.esStackDepth > 0)
                    {
                        impEvalSideEffects();
                    }

                    if (info.compXcptnsCount is 0)
                    {
                        BADCODE("endfinally outside finally");
                    }

                    assert(stackState.esStackDepth is 0);

                    var op1 = gtNewUnaryNode(GT_RETFILT, TYP_VOID, op1: null);
                    Append(this, op1);
                    break;
                }

                case CEE_ENDFILTER:
                {
                    if (compIsForInlining && !opts.compInlineMethodsWithEH)
                    {
                        NO_WAY("Shouldn't have exception handlers in the inlinee!");
                        compInlineResult.NoteFatal(InlineObservation.CALLEE_HAS_ENDFILTER);
                        return;
                    }

                    if (!fgPgoSynthesized)
                    {
                        // filters are rare
                        block.bbSetRunRarely();
                    }

                    if (info.compXcptnsCount is 0)
                    {
                        BADCODE("endfilter outside filter");
                    }

                    var op1 = impPopStack().val;
                    assertImp(op1.Type is TYP_INT, op1);

                    if (!bbInFilterILRange(block))
                    {
                        BADCODE("EndFilter outside a filter handler");
                    }

                    // Mark current bb as end of filter

                    assert(compCurBB is not null);
                    assert(compCurBB.HasFlag(BBF_DONT_REMOVE));
                    assert(compCurBB.Kind is BBJ_EHFILTERRET);

                    // Mark catch handler as successor

                    op1 = gtNewUnaryNode(GT_RETFILT, op1.Type, op1);

                    if (stackState.esStackDepth is not 0)
                    {
                        BADCODE("stack must be 1 on end of filter");
                    }
                    Append(this, op1);
                    break;
                }

                case CEE_RET:
                {
                    // ret without call before it
                    prefixFlags &= ~PREFIX_TAILCALL;

                    if (!impReturnInstruction(prefixFlags, ref opcode))
                    {
                        return;
                    }
                    break;
                }

                case CEE_JMP:
                {
                    assert(!compIsForInlining);

                    if (IsReadyToRun)
                    {
                        // jmp is not supported on ReadyToRun
                        // The call to the delayload method would not be properly set up to put the indirection cell address
                        // in the correct register. See https://github.com/dotnet/runtime/issues/125252
                        implReadyToRunUnsupported();
                    }

                    if (((info.compFlags & CORINFO_FLG_SYNCH) != 0) || block.hasTryIndex || block.hasHndIndex)
                    {
                        /* CEE_JMP does not make sense in some "protected" regions. */

                        BADCODE("Jmp not allowed in protected region");
                    }

                    if (opts.IsReversePInvoke)
                    {
                        BADCODE("Jmp not allowed in reverse P/Invoke");
                    }

                    if (stackState.esStackDepth is not 0)
                    {
                        BADCODE("Stack must be empty after CEE_JMPs");
                    }

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Method);

                    JITDUMP($" {resolvedToken.token:X8}");

                    /* The signature of the target has to be identical to ours.
                       At least check that argCnt and returnType match */

                    eeGetMethodSig(resolvedToken.hMethod, out var sig);

                    if ((sig.numArgs != info.compMethodInfo->args.numArgs) ||
                        (sig.retType != info.compMethodInfo->args.retType) ||
                        (sig.callConv != info.compMethodInfo->args.callConv))
                    {
                        BADCODE("Incompatible target for CEE_JMPs");
                    }

                    var op1 = new GenTreeVal(GT_JMP, TYP_VOID, unchecked((nint)(resolvedToken.hMethod)));

                    /* Mark the basic block as being a JUMP instead of RETURN */

                    block.SetFlags(BBF_HAS_JMP);

                    /* Set this flag to make sure register arguments have a location assigned
                     * even if we don't use them inside the method */

                    compJmpOpUsed = true;

                    fgNoStructPromotion = true;

                    Append(this, op1);
                    break;
                }

                case CEE_LDELEMA:
                {
                    assertImp(sz == sizeof(int));
                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);

                    JITDUMP($" {resolvedToken.token:X8}");

                    var ldelemClsHnd = resolvedToken.hClass;
                    var lclTyp = info.compCompHnd->asCorInfoType(ldelemClsHnd).VarType;

                    // If it's a value class / pointer array, or a readonly access, we don't need a type check.
                    // TODO-CQ: adapt "gtCanSkipCovariantStoreCheck" to handle "ldelema"s and call it here to
                    // skip using the helper in more cases.
                    if ((lclTyp != TYP_REF) || ((prefixFlags & PREFIX_READONLY) is not 0))
                    {
                        if (!TryArrLd(this, lclTyp, new typeInfo(), ldelemClsHnd, isLdelema: true))
                        {
                            return;
                        }
                        break;
                    }

                    // Otherwise we need the full helper function with run-time type check
                    var type = impTokenToHandle(resolvedToken);

                    if (type is null)
                    {
                        assert(compDonotInline);
                        return;
                    }

                    if (opts.OptimizationEnabled && (gtGetArrayElementClassHandle(impStackTop(1).val) == ldelemClsHnd) && info.compCompHnd->isExactType(ldelemClsHnd))
                    {
                        JITDUMP("\nldelema of T[] with T exact: skipping covariant check\n");

                        if (!TryArrLd(this, lclTyp, new typeInfo(), ldelemClsHnd, isLdelema: true))
                        {
                            return;
                        }
                        break;
                    }

                    var index = impPopStack().val;
                    var arr = impPopStack().val;

                    // The CLI Spec allows an array to be indexed by either an int32 or a native int.
                    // The array helper takes a native int for array length.
                    // So if we have an int, explicitly extend it to be a native int.
                    index = impImplicitIorI4Cast(index, TYP_I_IMPL);

                    var op1 = gtNewHelperCallNode(TYP_BYREF, CORINFO_HELP_LDELEMA_REF, arr, index, type);
                    impPushOnStack(op1, new typeInfo());
                    break;
                }

                // ldelem for reference and value types
                case CEE_LDELEM:
                {
                    assertImp(sz == sizeof(int));
                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);

                    JITDUMP($" {resolvedToken.token:X8}");

                    var ldelemClsHnd = resolvedToken.hClass;
                    var lclTyp = TypeHandleToVarType(ldelemClsHnd);
                    var tiRetVal = makeTypeInfo(ldelemClsHnd);

                    if (!TryArrLd(this, lclTyp, tiRetVal, ldelemClsHnd, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDELEM_I1:
                {
                    if (!TryArrLd(this, TYP_BYTE, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDELEM_I2:
                {
                    if (!TryArrLd(this, TYP_SHORT, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDELEM_I:
                {
                    if (!TryArrLd(this, TYP_I_IMPL, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDELEM_U4:
                {
                    if (!TryArrLd(this, TYP_INT, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDELEM_I4:
                {
                    if (!TryArrLd(this, TYP_INT, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDELEM_I8:
                {
                    if (!TryArrLd(this, TYP_LONG, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }
                case CEE_LDELEM_REF:
                {
                    if (!TryArrLd(this, TYP_REF, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDELEM_R4:
                {
                    if (!TryArrLd(this, TYP_FLOAT, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }
                case CEE_LDELEM_R8:
                {
                    if (!TryArrLd(this, TYP_DOUBLE, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDELEM_U1:
                {
                    if (!TryArrLd(this, TYP_UBYTE, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDELEM_U2:
                {
                    if (!TryArrLd(this, TYP_USHORT, new typeInfo(), ldelemClsHnd: null, isLdelema: false))
                    {
                        return;
                    }
                    break;
                }

                // stelem for reference and value types
                case CEE_STELEM:
                {
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);

                    JITDUMP($" {resolvedToken.token:X8}");

                    var stelemClsHnd = resolvedToken.hClass;
                    var lclTyp = TypeHandleToVarType(stelemClsHnd);

                    if (lclTyp != TYP_REF)
                    {
                        ArrSt(this, lclTyp, stelemClsHnd);
                        break;
                    }
                    goto case CEE_STELEM_REF;
                }

                case CEE_STELEM_REF:
                {
                    var value = impStackTop(0).val;
                    var index = impStackTop(1).val;
                    var array = impStackTop(2).val;

                    if (opts.OptimizationEnabled)
                    {
                        // Is this a case where we can skip the covariant store check?
                        if (gtCanSkipCovariantStoreCheck(value, array))
                        {
                            ArrSt(this, TYP_REF, stelemClsHnd: null);
                            break;
                        }
                    }

                    // Else call a helper function to do the store
                    impPopStack(3);

                    // The CLI Spec allows an array to be indexed by either an int32 or a native int.
                    // The array helper takes a native int for array length.
                    // So if we have an int, explicitly extend it to be a native int.
                    index = impImplicitIorI4Cast(index, TYP_I_IMPL);

                    var call = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_ARRADDR_ST, array, index, value);
#if DEBUG
                    call._rawILOffset = opcodeOffs;
#endif
                    impConvertToUserCallAndMarkForInlining(call);
                    var op1 = call;

                    Append(this, op1, CHECK_SPILL_ALL);
                    break;
                }

                case CEE_STELEM_I1:
                {
                    ArrSt(this, TYP_BYTE, stelemClsHnd: null);
                    break;
                }

                case CEE_STELEM_I2:
                {
                    ArrSt(this, TYP_SHORT, stelemClsHnd: null);
                    break;
                }

                case CEE_STELEM_I:
                {
                    ArrSt(this, TYP_I_IMPL, stelemClsHnd: null);
                    break;
                }

                case CEE_STELEM_I4:
                {
                    ArrSt(this, TYP_INT, stelemClsHnd: null);
                    break;
                }

                case CEE_STELEM_I8:
                {
                    ArrSt(this, TYP_LONG, stelemClsHnd: null);
                    break;
                }

                case CEE_STELEM_R4:
                {
                    ArrSt(this, TYP_FLOAT, stelemClsHnd: null);
                    break;
                }

                case CEE_STELEM_R8:
                {
                    ArrSt(this, TYP_DOUBLE, stelemClsHnd: null);
                    break;
                }

                case CEE_ADD:
                {
                    MathOp2(this, GT_ADD);
                    break;
                }

                case CEE_ADD_OVF:
                {
                    AddOvf(this, uns: false);
                    break;
                }

                case CEE_ADD_OVF_UN:
                {
                    AddOvf(this, uns: true);
                    break;
                }

                case CEE_SUB:
                {

                    MathOp2(this, GT_SUB);
                    break;
                }

                case CEE_SUB_OVF:
                {
                    SubOvf(this, uns: false);
                    break;
                }

                case CEE_SUB_OVF_UN:
                {
                    SubOvf(this, uns: true);
                    break;
                }

                case CEE_MUL:
                {
                    MathOp2(this, GT_MUL);
                    break;
                }

                case CEE_MUL_OVF:
                {
                    MulOvf(this, uns: false);
                    break;
                }

                case CEE_MUL_OVF_UN:
                {
                    MulOvf(this, uns: true);
                    break;
                }

                case CEE_DIV:
                {
                    MathOp2(this, GT_DIV);
                    break;
                }

                case CEE_DIV_UN:
                {
                    MathOp2(this, GT_UDIV);
                    break;
                }

                case CEE_REM:
                {
                    MathOp2(this, GT_MOD);
                    break;
                }

                case CEE_REM_UN:
                {
                    MathOp2(this, GT_UMOD);
                    break;
                }

                case CEE_AND:
                {
                    MathOp2(this, GT_AND);
                    break;
                }

                case CEE_OR:
                {
                    MathOp2(this, GT_OR);
                    break;
                }

                case CEE_XOR:
                {
                    MathOp2(this, GT_XOR);
                    break;
                }

                case CEE_SHL:
                {
                    ShOp2(this, GT_LSH);
                    break;
                }

                case CEE_SHR:
                {
                    ShOp2(this, GT_RSH);
                    break;
                }

                case CEE_SHR_UN:

                {
                    ShOp2(this, GT_RSZ);
                    break;
                }

                case CEE_NOT:
                {
                    var op1 = impPopStack().val;
                    impBashVarAddrsToI(op1);

                    var type = op1.Type.ActualType;
                    op1 = gtNewUnaryNode(GT_NOT, type, op1);

                    // Fold result, if possible.
                    op1 = gtFoldExpr(op1);

                    impPushOnStack(op1, new typeInfo());
                    break;
                }

                case CEE_CKFINITE:
                {
                    var op1 = impPopStack().val;
                    var type = op1.Type;
                    op1 = gtNewUnaryNode(GT_CKFINITE, type, op1);
                    op1.Flags |= GTF_EXCEPT;

                    impPushOnStack(op1, new typeInfo());
                    break;
                }

                case CEE_LEAVE:
                {
                    var jmpDist = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                    var jmpAddr = (IL_OFFSET)((codeAddr - info.compCode + sizeof(int)) + jmpDist);
                    TryLeave(this, block, jmpAddr);
                    break;
                }

                case CEE_LEAVE_S:
                {
                    var jumpDist = unchecked((sbyte)(codeAddr[0]));
                    var jmpAddr = (IL_OFFSET)((codeAddr - info.compCode + sizeof(sbyte)) + jumpDist);
                    TryLeave(this, block, jmpAddr);
                    break;
                }

                case CEE_BR:
                case CEE_BR_S:
                {
                    var jmpDist = (sz is 1) ? unchecked((sbyte)(codeAddr[0])) : BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));

                    if ((jmpDist is 0) && opts.DoEarlyBlockMerging)
                    {
                        break;
                    }

                    impNoteBranchOffs();
                    break;
                }

                case CEE_BRTRUE:
                case CEE_BRTRUE_S:
                case CEE_BRFALSE:
                case CEE_BRFALSE_S:
                {
                    // Pop the comparand (now there's a neat term) from the stack
                    var op1 = gtFoldExpr(impPopStack().val);

                    var type = op1.Type;

                    // Per Ecma-355, brfalse and brtrue are only specified for nint, ref, and byref.
                    //
                    // We've historically been a bit more permissive, so here we allow
                    // any type that gtNewZeroConNode can handle.
                    if (!varTypeIsArithmetic(type) && !varTypeIsGC(type))
                    {
                        BADCODE("invalid type for brtrue/brfalse");
                    }

                    if (opts.OptimizationEnabled)
                    {
                        // We may have already modified `block`'s jump kind, if this is a re-importation.
                        var jumpToNextOptimization = false;

                        if ((block.Kind is BBJ_COND) && (block.TrueEdge == block.FalseEdge))
                        {
                            JITDUMP($"{FMT_BB(block.bbNum)} always branches to {FMT_BB(block.FalseTarget.bbNum)}, changing to BBJ_ALWAYS\n");
                            fgRemoveRefPred(block.FalseEdge);
                            block.SetKindAndTargetEdge(BBJ_ALWAYS, block.TrueEdge);

                            jumpToNextOptimization = true;
                        }
                        else if ((block.Kind is BBJ_ALWAYS) && block.JumpsToNext)
                        {
                            jumpToNextOptimization = true;
                        }

                        if (jumpToNextOptimization)
                        {
                            if ((op1.Flags & GTF_GLOB_EFFECT) != 0)
                            {
                                op1 = gtUnusedValNode(op1);
                                Append(this, op1, CHECK_SPILL_ALL);
                                break;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    if (op1.Oper.IsCompare)
                    {
                        if (opcode is CEE_BRFALSE or CEE_BRFALSE_S)
                        {
                            // Flip the sense of the compare
                            op1 = gtReverseCond(op1);
                        }
                    }
                    else
                    {
                        // We'll compare against an equally-sized integer 0
                        // For small types, we always compare against int
                        var op2 = gtNewZeroConNode(op1.Type.ActualType);

                        // Create the comparison operator and try to fold it
                        var oper = (opcode is CEE_BRTRUE or CEE_BRTRUE_S) ? GT_NE : GT_EQ;
                        op1 = gtNewBinaryNode(oper, TYP_INT, op1, op2);
                    }

                    CondJump(this, block, op1);
                    break;
                }

                case CEE_CEQ:
                {
                    Cmp2Ops(this, GT_EQ, CEE_CEQ, uns: false);
                    break;
                }

                case CEE_CGT_UN:
                {
                    Cmp2Ops(this, GT_GT, CEE_CGT_UN, uns: true);
                    break;
                }

                case CEE_CGT:
                {
                    Cmp2Ops(this, GT_GT, CEE_CGT, uns: false);
                    break;
                }

                case CEE_CLT_UN:
                {
                    Cmp2Ops(this, GT_LT, CEE_CLT_UN, uns: true);
                    break;
                }

                case CEE_CLT:
                {
                    Cmp2Ops(this, GT_LT, CEE_CLT, uns: false);
                    break;
                }

                case CEE_BEQ_S:
                case CEE_BEQ:
                {
                    Cmp2OpsAndBr(this, block, GT_EQ);
                    break;
                }

                case CEE_BGE_S:
                case CEE_BGE:
                {
                    Cmp2OpsAndBr(this, block, GT_GE);
                    break;
                }

                case CEE_BGE_UN_S:
                case CEE_BGE_UN:
                {
                    Cmp2OpsAndBrUn(this, block, GT_GE);
                    break;
                }

                case CEE_BGT_S:
                case CEE_BGT:
                {
                    Cmp2OpsAndBr(this, block, GT_GT);
                    break;
                }

                case CEE_BGT_UN_S:
                case CEE_BGT_UN:
                {
                    Cmp2OpsAndBrUn(this, block, GT_GT);
                    break;
                }

                case CEE_BLE_S:
                case CEE_BLE:
                {
                    Cmp2OpsAndBr(this, block, GT_LE);
                    break;
                }

                case CEE_BLE_UN_S:
                case CEE_BLE_UN:
                {
                    Cmp2OpsAndBrUn(this, block, GT_LE);
                    break;
                }

                case CEE_BLT_S:
                case CEE_BLT:
                {
                    Cmp2OpsAndBr(this, block, GT_LT);
                    break;
                }

                case CEE_BLT_UN_S:
                case CEE_BLT_UN:
                {
                    Cmp2OpsAndBrUn(this, block, GT_LT);
                    break;
                }

                case CEE_BNE_UN_S:
                case CEE_BNE_UN:
                {
                    Cmp2OpsAndBrUn(this, block, GT_NE);
                    break;
                }

                case CEE_SWITCH:
                {
                    // Pop the switch value off the stack
                    var op1 = gtFoldExpr(impPopStack().val);
                    assertImp(genActualTypeIsIntOrI(op1.Type), op1);

                    // Fold Switch for GT_CNS_INT
                    if (opts.OptimizationEnabled && op1.Oper.IsCnsIntOrI)
                    {
                        // Find the jump target
                        var switchVal = op1.AsIntCon().IconVal;
                        var jumpTab = block.SwitchTargets.Cases;
                        var foundVal = false;
                        Metrics.ImporterSwitchFold++;

                        for (var i = 0; i < jumpTab.Length; i++)
                        {
                            var curEdge = jumpTab[i];
                            assert(curEdge.DestinationBlock.CountOfInEdges > 0);

                            // If val matches switchVal or we are at the last entry and
                            // we never found the switch value then set the new jump dest

                            if ((i == switchVal) || (!foundVal && (i == (jumpTab.Length - 1))))
                            {
                                // transform the basic block into a BBJ_ALWAYS
                                block.SetKindAndTargetEdge(BBJ_ALWAYS, curEdge);
                                foundVal = true;
                            }
                            else
                            {
                                // Remove 'curEdge'
                                fgRemoveRefPred(curEdge);
                            }
                        }

                        assert(foundVal);
                        JITDUMP($"\nSwitch folded at {FMT_BB(block.bbNum)}\n");
                        JITDUMP($"{FMT_BB(block.bbNum)} becomes a BBJ_ALWAYS to {FMT_BB(block.Target.bbNum)}\n");

                        if (block.hasProfileWeight)
                        {
                            // We are unlikely to be able to repair the profile.
                            // For now we don't even try.

                            JITDUMP($"Profile data could not be locally repaired. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");

                            if (fgPgoConsistent)
                            {
                                Metrics.ProfileInconsistentImporterSwitchFold++;
                                fgPgoConsistent = false;
                            }
                        }

                        // Create a NOP node
                        op1 = gtNewNothingNode();
                    }
                    else
                    {
                        // We can create a switch node
                        op1 = gtNewUnaryNode(GT_SWITCH, TYP_VOID, op1);
                    }

                    var switchCount = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int)));
                    codeAddr += (sizeof(int) + (switchCount * sizeof(int))); // skip over the switch-table

                    Append(this, op1, CHECK_SPILL_ALL);
                    break;
                }

                case CEE_CONV_OVF_I1:
                {
                    ConvOvf(this, TYP_BYTE);
                    break;
                }

                case CEE_CONV_OVF_I2:
                {
                    ConvOvf(this, TYP_SHORT);
                    break;
                }
                case CEE_CONV_OVF_I:
                {
                    ConvOvf(this, TYP_I_IMPL);
                    break;
                }

                case CEE_CONV_OVF_I4:
                {
                    ConvOvf(this, TYP_INT);
                    break;
                }

                case CEE_CONV_OVF_I8:
                {
                    ConvOvf(this, TYP_LONG);
                    break;
                }

                case CEE_CONV_OVF_U1:
                {
                    ConvOvf(this, TYP_UBYTE);
                    break;
                }

                case CEE_CONV_OVF_U2:
                {
                    ConvOvf(this, TYP_USHORT);
                    break;
                }

                case CEE_CONV_OVF_U:
                {
                    ConvOvf(this, TYP_U_IMPL);
                    break;
                }

                case CEE_CONV_OVF_U4:
                {
                    ConvOvf(this, TYP_UINT);
                    break;
                }

                case CEE_CONV_OVF_U8:
                {
                    ConvOvf(this, TYP_ULONG);
                    break;
                }

                case CEE_CONV_OVF_I1_UN:
                {
                    ConvOvfUn(this, TYP_BYTE);
                    break;
                }

                case CEE_CONV_OVF_I2_UN:
                {
                    ConvOvfUn(this, TYP_SHORT);
                    break;
                }

                case CEE_CONV_OVF_I_UN:
                {
                    ConvOvfUn(this, TYP_I_IMPL);
                    break;
                }

                case CEE_CONV_OVF_I4_UN:
                {
                    ConvOvfUn(this, TYP_INT);
                    break;
                }

                case CEE_CONV_OVF_I8_UN:
                {
                    ConvOvfUn(this, TYP_LONG);
                    break;
                }

                case CEE_CONV_OVF_U1_UN:
                {
                    ConvOvfUn(this, TYP_UBYTE);
                    break;
                }

                case CEE_CONV_OVF_U2_UN:
                {
                    ConvOvfUn(this, TYP_USHORT);
                    break;
                }

                case CEE_CONV_OVF_U_UN:
                {
                    ConvOvfUn(this, TYP_U_IMPL);
                    break;
                }

                case CEE_CONV_OVF_U4_UN:
                {
                    ConvOvfUn(this, TYP_UINT);
                    break;
                }

                case CEE_CONV_OVF_U8_UN:
                {
                    ConvOvfUn(this, TYP_ULONG);
                    break;
                }

                case CEE_CONV_I1:
                {
                    Conv(this, TYP_BYTE);
                    break;
                }

                case CEE_CONV_I2:
                {
                    Conv(this, TYP_SHORT);
                    break;
                }

                case CEE_CONV_I:
                {
                    Conv(this, TYP_I_IMPL);
                    break;
                }

                case CEE_CONV_I4:
                {
                    Conv(this, TYP_INT);
                    break;
                }

                case CEE_CONV_I8:
                {
                    Conv(this, TYP_LONG);
                    break;
                }

                case CEE_CONV_U1:
                {
                    Conv(this, TYP_UBYTE);
                    break;
                }

                case CEE_CONV_U2:
                {
                    Conv(this, TYP_USHORT);
                    break;
                }

                case CEE_CONV_U:
                {
#if TARGET_AMD64
                    ConvUn(this, TYP_U_IMPL);
#else
                    Conv(this, TYP_U_IMPL);
#endif
                    break;
                }

                case CEE_CONV_U4:
                {
                    Conv(this, TYP_UINT);
                    break;
                }

                case CEE_CONV_U8:
                {
                    ConvUn(this, TYP_ULONG);
                    break;
                }

                case CEE_CONV_R4:
                {
                    Conv(this, TYP_FLOAT);
                    break;
                }

                case CEE_CONV_R8:
                {
                    Conv(this, TYP_DOUBLE);
                    break;
                }

                case CEE_CONV_R_UN:
                {
                    // Because there is no IL instruction conv.r4.un, compilers consistently
                    // emit conv.r.un followed immediately by conv.r4 for uint->float casts.
                    // We recognize this pattern and create the intended cast.
                    // Otherwise, conv.r.un is treated as a cast to double.
                    var lclTyp = ((OPCODE)(codeAddr[0]) == CEE_CONV_R4) ? TYP_FLOAT : TYP_DOUBLE;
                    ConvUn(this, lclTyp);
                    break;
                }

                case CEE_NEG:
                {
                    var op1 = impPopStack().val;

                    impBashVarAddrsToI(op1);
                    op1 = gtNewUnaryNode(GT_NEG, op1.Type.ActualType, op1);

                    // Fold result, if possible.
                    op1 = gtFoldExpr(op1);

                    impPushOnStack(op1, new typeInfo());
                    break;
                }

                case CEE_POP:
                {
                    // Pull the top value from the stack
                    var op1 = impPopStack().val;

                    // Get hold of the type of the value being duplicated
                    var lclTyp = op1.Type.ActualType;

                    // Does the value have any side effects?
                    if (((op1.Flags & GTF_SIDE_EFFECT) is not 0) || opts.compDbgCode)
                    {
                        // Since we are throwing away the value, just normalize
                        // it to its address.  This is more efficient.

                        if (varTypeIsStruct(op1.Type))
                        {
                            JITDUMP("\n ... CEE_POP struct ...\n");
                            DISPTREE(op1);

                            // If the value being produced comes from loading
                            // via an underlying address, just null check the address.
                            if (op1.Oper.IsLoad)
                            {
                                op1 = gtNewNullCheck(op1.AsIndir().Op1);
                                op1.SetIndirExceptionFlags(this);
                            }
                            else
                            {
                                op1 = impGetNodeAddr(op1, CHECK_SPILL_ALL, GTF_EMPTY, out _);
                            }

                            JITDUMP("\n ... optimized to ...\n");
                            DISPTREE(op1);
                        }

                        // If op1 is non-overflow cast, throw it away since it is useless.
                        // Another reason for throwing away the useless cast is in the context of
                        // implicit tail calls when the operand of pop is GT_CAST(GT_CALL(..)).
                        // The cast gets added as part of importing GT_CALL, which gets in the way
                        // of fgMorphCall() on the forms of tail call nodes that we assert.
                        if ((op1.Oper is GT_CAST) && !op1.HasOverflowCheck)
                        {
                            op1 = op1.AsOp().Op1;
                        }

                        if (!op1.Oper.IsCall)
                        {
                            if ((op1.Flags & GTF_SIDE_EFFECT) is not 0)
                            {
                                op1 = gtUnusedValNode(op1);
                            }
                            else
                            {
                                // Can't bash to NOP here because op1 can be referenced from `currentBlock->bbEntryState`,
                                // if we ever need to reimport we need a valid LCL_VAR on it.
                                op1 = gtNewNothingNode();
                            }
                        }

                        /* Append the value to the tree list */
                        Append(this, op1, CHECK_SPILL_ALL);
                        break;
                    }
                    else if (op1.Oper is GT_BOX)
                    {
                        var box = op1.AsBox();

                        if (box.IsBoxedValue)
                        {
                            JITDUMP("\n CEE_POP box...\n");
                            gtTryRemoveBoxUpstreamEffects(box);
                        }
                    }

                    /* No side effects - just throw the <BEEP> thing away */
                    break;
                }

                case CEE_DUP:
                {
                    var se = impPopStack();
                    var tree = se.val;
                    var tiRetVal = se.seTypeInfo;

                    var op1 = tree;
                    var op2 = null as GenTree;
                    ;

                    // In unoptimized code we leave the decision of
                    // cloning/creating temps up to impCloneExpr, while in
                    // optimized code we prefer temps except for some cases we know
                    // are profitable.

                    if (opts.OptimizationEnabled)
                    {
                        var clone = false;

                        if (op1.IsIntegralConst(0) || op1.IsFloatPositiveZero)
                        {
                            // Duplicate 0 and +0.0
                            clone = true;
                        }
                        else if (op1.Oper.IsLocal)
                        {
                            // Duplicate locals and addresses of them
                            clone = true;
                        }
                        else if ((op1.Type is TYP_BYREF or TYP_I_IMPL) && impIsAddressInLocal(op1))
                        {
                            clone = true;
                        }

                        if (clone)
                        {
                            op2 = gtCloneExpr(op1);
                        }
                        else
                        {
                            var tmpNum = lvaGrabTemp(shortLifetime: true, "dup spill");
                            impStoreToTemp(tmpNum, op1, CHECK_SPILL_ALL);

                            ref var lvaDsc = ref lvaTable[tmpNum];
                            var type = lvaDsc.Type.ActualType;

                            assert(!lvaDsc.lvSingleDef);
                            lvaDsc.lvSingleDef = true;

                            JITDUMP($"Marked V{tmpNum:D2} as a single def local\n");

                            if (type == TYP_REF)
                            {
                                // Propagate type info to the temp from the stack and the original tree
                                lvaSetClass(tmpNum, tree, tiRetVal.ClassHandleForObjRef);
                            }

                            op1 = gtNewLclvNode(type, tmpNum);
                            op2 = gtNewLclvNode(type, tmpNum);
                        }
                    }
                    else
                    {
                        op1 = impCloneExpr(op1, out op2, CHECK_SPILL_ALL, "DUP instruction");
                    }

                    assert(op2 is not null);
                    assert(((op1.Flags & GTF_GLOB_EFFECT) is 0) && ((op2.Flags & GTF_GLOB_EFFECT) is 0));

                    impPushOnStack(op1, tiRetVal);
                    impPushOnStack(op2, tiRetVal);
                    break;
                }

                case CEE_STIND_I1:
                {
                    Stind(this, TYP_BYTE, CEE_STIND_I1, prefixFlags);
                    break;
                }

                case CEE_STIND_I2:
                {
                    Stind(this, TYP_SHORT, CEE_STIND_I2, prefixFlags);
                    break;
                }

                case CEE_STIND_I4:
                {
                    Stind(this, TYP_INT, CEE_STIND_I4, prefixFlags);
                    break;
                }

                case CEE_STIND_I8:
                {
                    Stind(this, TYP_LONG, CEE_STIND_I8, prefixFlags);
                    break;
                }

                case CEE_STIND_I:
                {
                    Stind(this, TYP_I_IMPL, CEE_STIND_I, prefixFlags);
                    break;
                }

                case CEE_STIND_REF:
                {
                    Stind(this, TYP_REF, CEE_STIND_REF, prefixFlags);
                    break;
                }

                case CEE_STIND_R4:
                {
                    Stind(this, TYP_FLOAT, CEE_STIND_R4, prefixFlags);
                    break;
                }

                case CEE_STIND_R8:
                {
                    Stind(this, TYP_DOUBLE, CEE_STIND_R8, prefixFlags);
                    break;
                }

                case CEE_LDIND_I1:
                {
                    Ldind(this, TYP_BYTE, prefixFlags);
                    break;
                }

                case CEE_LDIND_I2:
                {
                    Ldind(this, TYP_SHORT, prefixFlags);
                    break;
                }

                case CEE_LDIND_U4:
                case CEE_LDIND_I4:
                {
                    Ldind(this, TYP_INT, prefixFlags);
                    break;
                }

                case CEE_LDIND_I8:
                {
                    Ldind(this, TYP_LONG, prefixFlags);
                    break;
                }

                case CEE_LDIND_REF:
                {
                    Ldind(this, TYP_REF, prefixFlags);
                    break;
                }
                case CEE_LDIND_I:
                {
                    Ldind(this, TYP_I_IMPL, prefixFlags);
                    break;
                }

                case CEE_LDIND_R4:
                {
                    Ldind(this, TYP_FLOAT, prefixFlags);
                    break;
                }

                case CEE_LDIND_R8:
                {
                    Ldind(this, TYP_DOUBLE, prefixFlags);
                    break;
                }

                case CEE_LDIND_U1:
                {
                    Ldind(this, TYP_UBYTE, prefixFlags);
                    break;
                }

                case CEE_LDIND_U2:
                {
                    Ldind(this, TYP_USHORT, prefixFlags);
                    break;
                }

                case CEE_LDFTN:
                {
                    // Need to do a lookup here so that we perform an access check and do a NOWAY if protections are violated
                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Method);

                    JITDUMP($" {resolvedToken.token:X8}");
                    eeGetCallInfo(resolvedToken, ((prefixFlags & PREFIX_CONSTRAINED) is not 0) ? constrainedResolvedToken : Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(), CORINFO_CALLINFO_SECURITYCHECKS | CORINFO_CALLINFO_LDFTN, out var callInfo);

                    // This check really only applies to intrinsic Array.Address methods
                    if ((callInfo.sig.callConv & CORINFO_CALLCONV_PARAMTYPE) is not 0)
                    {
                        NO_WAY("Currently do not support LDFTN of Parameterized functions");
                    }

                    // Do this before DO_LDFTN since CEE_LDVIRTFN does it on its own.
                    impHandleAccessAllowed(callInfo.accessAllowed, callInfo.callsiteCalloutHelper);

                    if (!TryDoLdftn(this, prefixFlags, resolvedToken, constrainedResolvedToken, callInfo))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDVIRTFTN:
                {
                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Method);

                    JITDUMP($" {resolvedToken.token:X8}");
                    eeGetCallInfo(resolvedToken, constrainedToken: Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(), CORINFO_CALLINFO_SECURITYCHECKS | CORINFO_CALLINFO_LDFTN | CORINFO_CALLINFO_CALLVIRT, out var callInfo);

                    // This check really only applies to intrinsic Array.Address methods
                    if ((callInfo.sig.callConv & CORINFO_CALLCONV_PARAMTYPE) is not 0)
                    {
                        NO_WAY("Currently do not support LDFTN of Parameterized functions");
                    }

                    var mflags = callInfo.methodFlags;
                    impHandleAccessAllowed(callInfo.accessAllowed, callInfo.callsiteCalloutHelper);

                    if (compIsForInlining)
                    {
                        if (((mflags & (CORINFO_FLG_FINAL | CORINFO_FLG_STATIC)) is not 0) || ((mflags & CORINFO_FLG_VIRTUAL) is 0))
                        {
                            compInlineResult.NoteFatal(InlineObservation.CALLSITE_LDVIRTFN_ON_NON_VIRTUAL);
                            return;
                        }
                    }

                    ref var ftnSig = ref callInfo.sig;

                    // Get the object-ref
                    var op1 = impPopStack().val;
                    assertImp(op1.Type is TYP_REF, op1);

                    if (IsAot)
                    {
                        if (callInfo.kind != CORINFO_VIRTUALCALL_LDVIRTFTN)
                        {
                            if ((op1.Flags & GTF_SIDE_EFFECT) is not 0)
                            {
                                op1 = gtUnusedValNode(op1);
                                impAppendTree(op1, CHECK_SPILL_ALL, impCurStmtDI);
                            }

                            if (!TryDoLdftn(this, prefixFlags, resolvedToken, constrainedResolvedToken, callInfo))
                            {
                                return;
                            }
                            break;
                        }
                    }
                    else if (((mflags & (CORINFO_FLG_FINAL | CORINFO_FLG_STATIC)) is not 0) || ((mflags & CORINFO_FLG_VIRTUAL) is 0))
                    {
                        if ((op1.Flags & GTF_SIDE_EFFECT) is not 0)
                        {
                            op1 = gtUnusedValNode(op1);
                            impAppendTree(op1, CHECK_SPILL_ALL, impCurStmtDI);
                        }

                        if (!TryDoLdftn(this, prefixFlags, resolvedToken, constrainedResolvedToken, callInfo))
                        {
                            return;
                        }
                        break;
                    }

                    var fptr = impImportLdvirtftn(op1, resolvedToken, callInfo);

                    if (compDonotInline)
                    {
                        return;
                    }

                    var heapToken = impAllocateMethodPointerInfo(resolvedToken, 0);

                    assert(heapToken._token.tokenType == CORINFO_TOKENKIND_Method);
                    assert(callInfo.hMethod is not null);

                    heapToken._token.tokenType = CORINFO_TOKENKIND_Ldvirtftn;
                    heapToken._token.hMethod = callInfo.hMethod;

                    assert(fptr is not null);
                    impPushOnStack(fptr, new typeInfo(heapToken));
                    break;
                }

                case CEE_NEWOBJ:
                {
                    // Since we will implicitly insert newObjThisPtr at the start of the argument list, spill any GTF_ORDER_SIDEEFF
                    impSpillSpecialSideEff();

                    // NEWOBJ does not respond to TAIL or CONSTRAINED
                    prefixFlags &= ~(PREFIX_TAILCALL_EXPLICIT | PREFIX_CONSTRAINED);

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_NewObj);
                    eeGetCallInfo(resolvedToken, constrainedToken: Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(), CORINFO_CALLINFO_SECURITYCHECKS | CORINFO_CALLINFO_ALLOWINSTPARAM, out var callInfo);

                    var mflags = callInfo.methodFlags;

                    if ((mflags & (CORINFO_FLG_STATIC | CORINFO_FLG_ABSTRACT)) is not 0)
                    {
                        BADCODE("newobj on static or abstract method");
                    }

                    // Insert the security callout before any actual code is generated
                    impHandleAccessAllowed(callInfo.accessAllowed, callInfo.callsiteCalloutHelper);

                    // There are three different cases for new.
                    // Object size is variable (depends on arguments).
                    //      1) Object is an array (arrays treated specially by the EE)
                    //      2) Object is some other variable sized object (e.g. String)
                    //      3) Class Size can be determined beforehand (normal case)
                    // In the first case, we need to call a NEWOBJ helper (multinewarray).
                    // In the second case we call the constructor with a '0' this pointer.
                    // In the third case we alloc the memory, then call the constructor.

                    var clsFlags = callInfo.classFlags;
                    var newObjThis = null as GenTree;

                    if ((clsFlags & CORINFO_FLG_ARRAY) is not 0)
                    {
                        // Arrays need to call the NEWOBJ helper.
                        assertImp((clsFlags & CORINFO_FLG_VAROBJSIZE) is not 0);

                        impImportNewObjArray(resolvedToken, callInfo);

                        if (compDonotInline)
                        {
                            return;
                        }

                        callTyp = TYP_REF;
                        break;
                    }
                    // At present this can only be String
                    else if ((clsFlags & CORINFO_FLG_VAROBJSIZE) is not 0)
                    {
                        // Skip this thisPtr argument
                        newObjThis = null;

                        // Remember that this basic block contains 'new' of an object
                        block.SetFlags(BBF_HAS_NEWOBJ);
                        optMethodFlags |= OMF_HAS_NEWOBJ;
                    }
                    else
                    {
                        // This is the normal case where the size of the object is
                        // fixed.  Allocate the memory and call the constructor.

                        // Note: We cannot add a peep to avoid use of temp here
                        // because we don't have enough interference info to detect when
                        // sources and destination interfere, example: s = new S(ref);

                        // TODO: We find the correct place to introduce a general
                        // reverse copy prop for struct return values from newobj or
                        // any function returning structs.

                        // get a temporary for the new object
                        var lclNum = lvaGrabTemp(shortLifetime: true, "NewObj constructor temp");

                        if (compDonotInline)
                        {
                            // Fail fast if lvaGrabTemp fails with CALLSITE_TOO_MANY_LOCALS.
                            assert(compInlineResult.Observation == InlineObservation.CALLSITE_TOO_MANY_LOCALS);
                            return;
                        }

                        // In the value class case we only need clsHnd for size calcs.
                        //
                        // The lookup of the code pointer will be handled by CALL in this case
                        if ((clsFlags & CORINFO_FLG_VALUECLASS) is not 0)
                        {
                            if (compIsForInlining)
                            {
                                // If value class has GC fields, inform the inliner.
                                // It may choose to bail out on the inline.
                                var typeFlags = info.compCompHnd->getClassAttribs(resolvedToken.hClass);

                                if ((typeFlags & CORINFO_FLG_CONTAINS_GC_PTR) is not 0)
                                {
                                    compInlineResult.Note(InlineObservation.CALLEE_HAS_GC_STRUCT);

                                    if (compInlineResult.IsFailure)
                                    {
                                        return;
                                    }

                                    // Do further notification in the case where the call site is rare;
                                    // some policies do not track the relative hotness of call sites for "always" inline cases.

                                    var iciBlock = impInlineInfo.iciBlock;
                                    assert(iciBlock is not null);

                                    if (iciBlock.isRunRarely)
                                    {
                                        compInlineResult.Note(InlineObservation.CALLSITE_RARE_GC_STRUCT);

                                        if (compInlineResult.IsFailure)
                                        {
                                            return;
                                        }
                                    }
                                }
                            }

                            var jitTyp = info.compCompHnd->asCorInfoType(resolvedToken.hClass);
                            ref var lclDsc = ref lvaGetDesc(lclNum);

                            if (impIsPrimitive(jitTyp))
                            {
                                lclDsc.Type = jitTyp.VarType;
                            }
                            else
                            {
                                // The local variable itself is the allocated space.
                                // Here we need unsafe value cls check, since the address of struct is taken for further use and potentially exploitable.
                                lvaSetStruct(lclNum, resolvedToken.hClass, unsafeValueClsCheck: true);
                            }

                            var bbInALoop = impBlockIsInALoop(block);
                            var bbIsReturn = (block.Kind is BBJ_RETURN) && (!compIsForInlining || (impInlineInfo.iciBlock!.Kind is BBJ_RETURN));

                            if (fgVarNeedsExplicitZeroInit(lclNum, bbInALoop, bbIsReturn))
                            {
                                // Append a tree to zero-out the temp
                                var newObjInit = gtNewZeroConNode((lclDsc.Type is TYP_STRUCT) ? TYP_INT : lclDsc.Type);
                                impStoreToTemp(lclNum, newObjInit, CHECK_SPILL_NONE);
                            }
                            else
                            {
                                JITDUMP($"\nSuppressing zero-init for V{lclNum:D2} -- expect to zero in prolog\n");
                                lclDsc.lvSuppressedZeroInit = true;
                                compSuppressedZeroInit = true;
                            }

                            // The constructor may store "this", with subsequent code mutating the underlying local
                            // through the captured reference. To correctly spill the node we'll push onto the stack
                            // in such a case, we must mark the temp as potentially aliased.
                            lclDsc.lvHasLdAddrOp = true;

                            // Obtain the address of the temp
                            newObjThis = gtNewLclVarAddrNode(TYP_BYREF, lclNum);
                        }
                        else
                        {
                            // If we're newing up a finalizable object, spill anything that can cause exceptions.
                            var hasSideEffects = false;
                            var newHelper = info.compCompHnd->getNewHelper(resolvedToken.hClass, &hasSideEffects);

                            if (hasSideEffects)
                            {
                                JITDUMP("\nSpilling stack for finalizable newobj\n");
                                impSpillSideEffects(spillGlobEffects: true, CHECK_SPILL_ALL, "finalizable newobj spill");
                            }

                            var op1 = gtNewAllocObjNode(resolvedToken, info.compMethodHnd, useParent: true);

                            if (op1 is null)
                            {
                                return;
                            }

                            // Flag if this allocation happens within a method that uses the static empty
                            // pattern (if we stack allocate this object, we can optimize the empty side away)
                            //
                            if (lookupNamedIntrinsic(info.compMethodHnd) == NI_System_SZArrayHelper_GetEnumerator)
                            {
                                JITDUMP("Allocation is part of empty static pattern\n");
                                op1.Flags |= GTF_ALLOCOBJ_EMPTY_STATIC;
                            }

                            // If the method being imported is an inlinee, and the original call was flagged
                            // for possible enumerator cloning, flag the allocation as well.
                            //
                            if (compIsForInlining && hasImpEnumeratorGdvLocalMap)
                            {
                                var map = ImpEnumeratorGdvLocalMap;

                                var call = impInlineInfo.iciCall;
                                assert(call is not null);

                                if (map.TryGetValue(call, out var enumeratorLcl))
                                {
                                    JITDUMP($"Flagging [{op1.TreeId:D6}] for enumerator cloning via V{enumeratorLcl:D2}\n");
                                    map.Remove(call);
                                    map[op1] = enumeratorLcl;
                                }
                            }

                            // Remember that this basic block contains 'new' of an object
                            block.SetFlags(BBF_HAS_NEWOBJ);
                            optMethodFlags |= OMF_HAS_NEWOBJ;

                            // Append the store to the temp/local. Dont need to spill at all as
                            // we are just calling an EE-Jit helper which can only cause
                            // an (async) OutOfMemoryException.

                            // We assign the newly allocated object (by a GT_ALLOCOBJ node)
                            // to a temp. Note that the pattern "temp = allocObj" is required
                            // by ObjectAllocator phase to be able to determine GT_ALLOCOBJ nodes
                            // without exhaustive walk over all expressions.

                            impStoreToTemp(lclNum, op1, CHECK_SPILL_NONE);

                            assert(!lvaTable[lclNum].lvSingleDef);
                            lvaTable[lclNum].lvSingleDef = true;

                            JITDUMP($"Marked V{lclNum:D2} as a single def local\n");
                            lvaSetClass(lclNum, resolvedToken.hClass, isExact: true);

                            newObjThis = gtNewLclvNode(TYP_REF, lclNum);
                        }
                    }

                    var importCallHelper = new ImportCallHelper {
                        opcode = opcode,
                        resolvedToken = ref resolvedToken,
                        constrainedResolvedToken = ref constrainedResolvedToken,
                        newObjThis = newObjThis,
                        prefixFlags = prefixFlags,
                        callInfo = ref callInfo,
                        opcodeOffs = opcodeOffs,
                    };

                    if (!importCallHelper.TryImport(this, codeAddr, codeEndp, sz))
                    {
                        return;
                    }
                    break;
                }

                case CEE_CALLI:
                {

                    // CALLI does not respond to CONSTRAINED
                    prefixFlags &= ~PREFIX_CONSTRAINED;

                    var resolvedToken = new CORINFO_RESOLVED_TOKEN {
                        token = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(codeAddr, sizeof(int))),
                        tokenContext = impTokenLookupContextHandle,
                        tokenScope = info.compScopeHnd,
                    };

                    var callInfo = new CORINFO_CALL_INFO();

                    var importCallHelper = new ImportCallHelper {
                        opcode = opcode,
                        resolvedToken = ref resolvedToken,
                        prefixFlags = prefixFlags,
                        callInfo = ref callInfo,
                        opcodeOffs = opcodeOffs,
                    };

                    if (!importCallHelper.TryImport(this, codeAddr, codeEndp, sz))
                    {
                        return;
                    }
                    break;
                }

                case CEE_CALLVIRT:
                case CEE_CALL:
                {
                    var isAwait = false;
                    var codeAddrAfterMatch = (byte*)(null);
                    var awaitOffset = BAD_IL_OFFSET;

#if DEBUG
                    if (compIsAsync && (JitConfig[ConfigInteger.JitDoOptimizeAwait] is not 0))
#else
                    if (compIsAsync)
#endif
                    {
                        codeAddrAfterMatch = impMatchTaskAwaitPattern(codeAddr, codeEndp, out var configVal, out awaitOffset);

                        if (codeAddrAfterMatch is not null)
                        {
                            JITDUMP($"Recognized await{(configVal is 0 ? " (with ConfigureAwait(false))" : "")}\n");

                            isAwait = true;
                            prefixFlags |= PREFIX_IS_TASK_AWAIT;

                            if (configVal is not 0)
                            {
                                prefixFlags |= PREFIX_TASK_AWAIT_CONTINUE_ON_CAPTURED_CONTEXT;
                            }
                        }
                    }

                    CORINFO_RESOLVED_TOKEN resolvedToken;

                    if (isAwait)
                    {
                        impResolveToken(codeAddr, out resolvedToken, CORINFO_TOKENKIND_Await);

                        if (resolvedToken.hMethod is null)
                        {
                            // This can happen in cases when the Task-returning method is not a runtime Async
                            // function. For example "T M1<T>(T arg) => arg" when called with a Task argument.
                            // It can also happen generally if the VM does not think using the async entry point
                            // is worth it. Treat these as a regular call that is Awaited.
                            impResolveToken(codeAddr, out resolvedToken, CORINFO_TOKENKIND_Method);
                            prefixFlags &= ~(PREFIX_IS_TASK_AWAIT | PREFIX_TASK_AWAIT_CONTINUE_ON_CAPTURED_CONTEXT);
                            isAwait = false;

                            JITDUMP("No async variant provided by VM, treating as regular call that is awaited\n");
                        }
                    }
                    else
                    {
                        impResolveToken(codeAddr, out resolvedToken, CORINFO_TOKENKIND_Method);
                    }

                    var flags = CORINFO_CALLINFO_ALLOWINSTPARAM | CORINFO_CALLINFO_SECURITYCHECKS;

                    if (opcode == CEE_CALLVIRT)
                    {
                        flags |= CORINFO_CALLINFO_CALLVIRT;
                    }

                    eeGetCallInfo(resolvedToken, ((prefixFlags & PREFIX_CONSTRAINED) is not 0) ? constrainedResolvedToken : Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(), flags, out var callInfo);

                    if (isAwait && (callInfo.kind is CORINFO_CALL))
                    {
                        assert(callInfo.sig.isAsyncCall());

                        bool isSyncCallThunk;
                        info.compCompHnd->getAsyncOtherVariant(callInfo.hMethod, &isSyncCallThunk);

                        if (!isSyncCallThunk)
                        {
                            // The async variant that we got is a thunk. Switch
                            // back to the non-async task-returning call. There
                            // is no reason to go through the thunk.
                            impResolveToken(codeAddr, out resolvedToken, CORINFO_TOKENKIND_Method);
                            prefixFlags &= ~(PREFIX_IS_TASK_AWAIT | PREFIX_TASK_AWAIT_CONTINUE_ON_CAPTURED_CONTEXT);
                            isAwait = false;

                            JITDUMP("Async variant provided by VM is a thunk, switching direct call to synchronous task-returning method\n");
                            eeGetCallInfo(resolvedToken, ((prefixFlags & PREFIX_CONSTRAINED) is not 0) ? constrainedResolvedToken : Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(), flags, out callInfo);
                        }
                    }

                    if (isAwait)
                    {
                        // If the synchronous call is a thunk then it means the async variant is not a thunk and we
                        // prefer to directly call it. Skip the await pattern to the last token.
                        codeAddr = codeAddrAfterMatch;
                        opcodeOffs = awaitOffset;
                    }

                    var importCallHelper = new ImportCallHelper {
                        opcode = opcode,
                        resolvedToken = ref resolvedToken,
                        constrainedResolvedToken = ref constrainedResolvedToken,
                        prefixFlags = prefixFlags,
                        callInfo = ref callInfo,
                        opcodeOffs = opcodeOffs,
                    };

                    if (!importCallHelper.TryImport(this, codeAddr, codeEndp, sz))
                    {
                        return;
                    }
                    break;
                }

                case CEE_LDFLD:
                case CEE_LDSFLD:
                case CEE_LDFLDA:
                case CEE_LDSFLDA:
                {
                    var isLoadAddress = opcode is CEE_LDFLDA or CEE_LDSFLDA;
                    var isLoadStatic = opcode is CEE_LDSFLD or CEE_LDSFLDA;

                    // Get the CP_Fieldref index
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Field);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var indirFlags = impPrefixFlagsToIndirFlags(prefixFlags);
                    var aflags = isLoadAddress ? CORINFO_ACCESS_ADDRESS : CORINFO_ACCESS_GET;
                    var obj = null as GenTree;

                    if (!isLoadStatic)
                    {
                        obj = impPopStack().val;

                        if (impIsThis(obj))
                        {
                            aflags |= CORINFO_ACCESS_THIS;
                        }
                    }

                    eeGetFieldInfo(resolvedToken, aflags, out var fieldInfo);

                    // Note we avoid resolving the normalized (struct) type just yet; we may not need it (for ld[s]flda).
                    var lclTyp = fieldInfo.fieldType.VarType;
                    var clsHnd = fieldInfo.structType;

                    if (compIsForInlining)
                    {
                        switch (fieldInfo.fieldAccessor)
                        {
                            case CORINFO_FIELD_INSTANCE_HELPER:
                            case CORINFO_FIELD_INSTANCE_ADDR_HELPER:
                            case CORINFO_FIELD_STATIC_ADDR_HELPER:
                            case CORINFO_FIELD_STATIC_TLS:
                            {
                                compInlineResult.NoteFatal(InlineObservation.CALLEE_LDFLD_NEEDS_HELPER);
                                return;
                            }

                            case CORINFO_FIELD_STATIC_READYTORUN_HELPER:
                            {
                                compInlineResult.NoteFatal(InlineObservation.CALLSITE_LDFLD_NEEDS_HELPER);
                                return;
                            }

                            default:
                            {
                                break;
                            }
                        }

                        if (!isLoadAddress && ((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_STATIC) is not 0) && (lclTyp is TYP_STRUCT))
                        {
                            if ((info.compCompHnd->getTypeForPrimitiveValueClass(clsHnd) is CORINFO_TYPE_UNDEF) && ((info.compFlags & CORINFO_FLG_FORCEINLINE) is 0))
                            {
                                // Loading a static valuetype field usually will cause a JitHelper to be called
                                // for the static base. This will bloat the code.

                                // Make an exception - small getters (6 bytes of IL) returning initialized fields, e.g.:
                                //
                                //  static DateTime Foo { get; } = DateTime.Now;
                                //
                                if ((opcode is not CEE_LDSFLD) || (info.compILCodeSize > 6) || ((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_FINAL) is 0))
                                {
                                    compInlineResult.Note(InlineObservation.CALLEE_LDFLD_STATIC_VALUECLASS);

                                    if (compInlineResult.IsFailure)
                                    {
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    var tiRetVal = isLoadAddress ? new typeInfo(TYP_BYREF) : makeTypeInfo(fieldInfo.fieldType, clsHnd);
                    impHandleAccessAllowed(fieldInfo.accessAllowed, fieldInfo.accessCalloutHelper);

                    // Raise InvalidProgramException if static load accesses non-static field
                    if (isLoadStatic && ((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_STATIC) is 0))
                    {
                        BADCODE("static access on an instance field");
                    }

                    // We are using ldfld/a on a static field. We allow it, but need to get side-effect from obj.
                    if (((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_STATIC) is not 0) && (obj is not null))
                    {
                        if ((obj.Flags & GTF_SIDE_EFFECT) is not 0)
                        {
                            obj = gtUnusedValNode(obj);
                            impAppendTree(obj, CHECK_SPILL_ALL, impCurStmtDI);
                        }
                        obj = null;
                    }

                    var usesHelper = false;
                    var op1 = null as GenTree;

                    switch (fieldInfo.fieldAccessor)
                    {
                        case CORINFO_FIELD_INSTANCE:
#if FEATURE_READYTORUN
                        case CORINFO_FIELD_INSTANCE_WITH_BASE:
#endif
                        {
                            // If the object is a struct, what we really want is
                            // for the field to operate on the address of the struct.
                            assert(obj is not null);

                            if (varTypeIsStruct(obj.Type))
                            {
                                if (opcode != CEE_LDFLD)
                                {
                                    BADCODE($"Unexpected opcode (has to be LDFLD): {(ushort)(opcode):X2}");
                                }

                                // Get the address and any flags from the original access (volatile, unaligned, etc.)
                                obj = impGetNodeAddr(obj, CHECK_SPILL_ALL, GTF_IND_MUST_PRESERVE_FLAGS, out var objAddrFlags);

                                // Combine the flags from the object address with any prefix flags
                                indirFlags |= objAddrFlags;
                            }

                            op1 = gtNewFieldAddrNode(obj, resolvedToken.hField, fieldInfo.offset);

#if FEATURE_READYTORUN
                            if (fieldInfo.fieldAccessor == CORINFO_FIELD_INSTANCE_WITH_BASE)
                            {
                                op1.AsFieldAddr().FieldLookup = fieldInfo.fieldLookup;
                            }
#endif
                            if (StructHasOverlappingFields(info.compCompHnd->getClassAttribs(resolvedToken.hClass)))
                            {
                                op1.AsFieldAddr().MayOverlap = true;
                            }

                            if (!isLoadAddress && compIsForInlining && impInlineIsGuaranteedThisDerefBeforeAnySideEffects(additionalTree: null, additionalCallArgs: Unsafe.NullRef<CallArgs>(), obj, impInlineInfo.inlArgInfo))
                            {
                                impInlineInfo.thisDereferencedFirst = true;
                            }
                            break;
                        }

                        case CORINFO_FIELD_STATIC_TLS:
                        {
#if TARGET_X86
                            // Legacy TLS access is implemented as intrinsic on x86 only
                            op1 = gtNewFieldAddrNode(TYP_I_IMPL, resolvedToken.hField, null, fieldInfo.offset);
                            op1.Flags |= GTF_FLD_TLS; // fgMorphExpandTlsField will handle the transformation.
                            break;
#else
                            fieldInfo.fieldAccessor = CORINFO_FIELD_STATIC_ADDR_HELPER;
                            goto case CORINFO_FIELD_STATIC_ADDR_HELPER;
#endif
                        }

                        case CORINFO_FIELD_STATIC_ADDR_HELPER:
                        case CORINFO_FIELD_INSTANCE_HELPER:
                        case CORINFO_FIELD_INSTANCE_ADDR_HELPER:
                        {
                            op1 = gtNewRefComField(obj, resolvedToken, aflags, fieldInfo, lclTyp, value: null);
                            usesHelper = true;
                            break;
                        }

                        case CORINFO_FIELD_STATIC_TLS_MANAGED:
                        {
                            MethodHasTlsFieldAccess = true;
                            goto case CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER;
                        }

                        case CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER:
                        case CORINFO_FIELD_STATIC_ADDRESS:
                        case CORINFO_FIELD_STATIC_RELOCATABLE:
                        {
                            // Replace static read-only fields with constant if possible
                            if (((aflags & CORINFO_ACCESS_GET) is not 0) && ((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_FINAL) is not 0))
                            {
                                var newTree = impImportStaticReadOnlyField(resolvedToken.hField, resolvedToken.hClass);

                                if (newTree is not null)
                                {
                                    op1 = newTree;
                                    impPushOnStack(op1, tiRetVal);
                                    break;
                                }
                            }
                            goto case CORINFO_FIELD_STATIC_RVA_ADDRESS;
                        }

                        case CORINFO_FIELD_STATIC_RVA_ADDRESS:
                        case CORINFO_FIELD_STATIC_GENERICS_STATIC_HELPER:
                        case CORINFO_FIELD_STATIC_READYTORUN_HELPER:
                        {
                            op1 = impImportStaticFieldAddress(resolvedToken, aflags, fieldInfo, lclTyp, ref indirFlags);
                            break;
                        }

                        case CORINFO_FIELD_INTRINSIC_ZERO:
                        {
                            assert((aflags & CORINFO_ACCESS_GET) is not 0);

                            // Widen to stack type
                            lclTyp = lclTyp.ActualType;
                            op1 = gtNewIconNode(lclTyp, 0);

                            impPushOnStack(op1, tiRetVal);
                            break;
                        }

                        case CORINFO_FIELD_INTRINSIC_EMPTY_STRING:
                        {
                            assert((aflags & CORINFO_ACCESS_GET) is not 0);

                            // Import String.Empty as "" (GT_CNS_STR with a fake SconCPX = 0)
                            op1 = gtNewSconNode(EMPTY_STRING_SCON, scpHandle: null);

                            impPushOnStack(op1, tiRetVal);
                            break;
                        }

                        case CORINFO_FIELD_INTRINSIC_ISLITTLEENDIAN:
                        {
                            assert((aflags & CORINFO_ACCESS_GET) is not 0);

                            // Widen to stack type
                            lclTyp = lclTyp.ActualType;
#if BIGENDIAN
                            op1 = gtNewIconNode(lclTyp, 0);
#else
                            op1 = gtNewIconNode(lclTyp, 1);
#endif
                            impPushOnStack(op1, tiRetVal);
                            break;
                        }

                        default:
                        {
                            NO_WAY("Unexpected fieldAccessor");
                            break;
                        }
                    }

                    assert(op1 is not null);

                    if (!isLoadAddress && !usesHelper)
                    {
                        lclTyp = TypeHandleToVarType(fieldInfo.fieldType, clsHnd, out var layout);

                        if (lclTyp is TYP_STRUCT)
                        {
                            assert(layout is not null);
                            op1 = gtNewBlkIndir(op1, layout, indirFlags);
                        }
                        else
                        {
                            op1 = gtNewIndir(lclTyp, op1, indirFlags);
                        }
                        impAnnotateFieldIndir(op1.AsIndir());
                    }

                    // Check if the class needs explicit initialization.
                    if ((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_INITCLASS) is not 0)
                    {
                        var helperNode = impInitClass(resolvedToken);

                        if (compDonotInline)
                        {
                            return;
                        }

                        if (helperNode is not null)
                        {
                            op1 = gtNewCommaNode(op1.Type, helperNode, op1);
                        }
                    }

                    impPushOnStack(op1, tiRetVal);
                    break;
                }

                case CEE_STFLD:
                case CEE_STSFLD:
                {
                    var isStoreStatic = opcode is CEE_STSFLD;

                    // Get the CP_Fieldref index
                    assertImp(sz == sizeof(int));
                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Field);

                    JITDUMP($" {resolvedToken.token:X8}");

                    var indirFlags = impPrefixFlagsToIndirFlags(prefixFlags);
                    var aflags = CORINFO_ACCESS_SET;
                    var obj = null as GenTree;

                    eeGetFieldInfo(resolvedToken, aflags, out var fieldInfo);
                    var lclTyp = TypeHandleToVarType(fieldInfo.fieldType, fieldInfo.structType, out var layout);

                    if (compIsForInlining)
                    {
                        // Is this a 'special' (COM) field? or a TLS ref static field?, field stored int GC heap? or per-inst static?

                        switch (fieldInfo.fieldAccessor)
                        {
                            case CORINFO_FIELD_INSTANCE_HELPER:
                            case CORINFO_FIELD_INSTANCE_ADDR_HELPER:
                            case CORINFO_FIELD_STATIC_ADDR_HELPER:
                            case CORINFO_FIELD_STATIC_TLS:
                            {
                                compInlineResult.NoteFatal(InlineObservation.CALLEE_STFLD_NEEDS_HELPER);
                                return;
                            }

                            case CORINFO_FIELD_STATIC_GENERICS_STATIC_HELPER:
                            case CORINFO_FIELD_STATIC_READYTORUN_HELPER:
                            {
                                // We may be able to inline the field accessors in specific instantiations of generic methods
                                compInlineResult.NoteFatal(InlineObservation.CALLSITE_STFLD_NEEDS_HELPER);
                                return;
                            }

                            default:
                            {
                                break;
                            }
                        }
                    }

                    impHandleAccessAllowed(fieldInfo.accessAllowed, fieldInfo.accessCalloutHelper);

                    // Check if the class needs explicit initialization.
                    if ((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_INITCLASS) is not 0)
                    {
                        var helperNode = impInitClass(resolvedToken);

                        if (compDonotInline)
                        {
                            return;
                        }

                        if (helperNode is not null)
                        {
                            var isHoistable = (info.compCompHnd->getClassAttribs(resolvedToken.hClass) & CORINFO_FLG_BEFOREFIELDINIT) is not 0;
                            var checkSpill = isHoistable ? CHECK_SPILL_NONE : CHECK_SPILL_ALL;
                            impAppendTree(helperNode, checkSpill, impCurStmtDI);
                        }
                    }

                    // Handle the cases that might trigger type initialization
                    // (and possibly need to spill the tree for the stored value)
                    var expandAddrInline = (fieldInfo.fieldAccessor is CORINFO_FIELD_INSTANCE) && !fgIsBigOffset(fieldInfo.offset);
                    var op1 = null as GenTree;

                    switch (fieldInfo.fieldAccessor)
                    {
                        case CORINFO_FIELD_INSTANCE:
#if FEATURE_READYTORUN
                        case CORINFO_FIELD_INSTANCE_WITH_BASE:
#endif
                        {
                            // We will create STOREIND/STOREBLK(FIELD_ADDR(obj, fld), data).
                            // The required IL evaluation order is obj -> data -> nullcheck(obj) -> store.
                            // Take care not to reorder the data with the null check.
                            //
                            // When the field offset is small enough, we can expand the address
                            // inline and rely on the store itself to perform the null check,
                            // so no spill is needed.
                            if (!expandAddrInline && !impCanReorderWithNullCheck(impStackTop().val) && fgAddrCouldBeNull(impStackTop(1).val))
                            {
                                impSpillStackEntry(stackState.esStackDepth - 1, BAD_VAR_NUM, assertOnRecursion: false, "non-reorderable data to stfld");
                            }
                            break;
                        }

                        case CORINFO_FIELD_STATIC_TLS:
                        case CORINFO_FIELD_STATIC_ADDR_HELPER:
                        case CORINFO_FIELD_INSTANCE_HELPER:
                        case CORINFO_FIELD_INSTANCE_ADDR_HELPER:
                        {
                            // Nothing now - handled later
                            break;
                        }

                        case CORINFO_FIELD_STATIC_TLS_MANAGED:
                        case CORINFO_FIELD_STATIC_ADDRESS:
                        case CORINFO_FIELD_STATIC_RVA_ADDRESS:
                        case CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER:
                        case CORINFO_FIELD_STATIC_GENERICS_STATIC_HELPER:
                        case CORINFO_FIELD_STATIC_READYTORUN_HELPER:
                        case CORINFO_FIELD_STATIC_RELOCATABLE:
                        {
                            op1 = impImportStaticFieldAddress(resolvedToken, aflags, fieldInfo, lclTyp, ref indirFlags, out var isHoistable);

                            if (!isHoistable)
                            {
                                impSpillSideEffects(spillGlobEffects: true, CHECK_SPILL_ALL, "value for stsfld with typeinit");
                            }
                            else if ((op1.Type is TYP_BYREF) && gtTreeContainsAsyncCall(impStackTop().val))
                            {
                                // Spill if we have a byref address and the value to store contains
                                // an async call. This avoids keeping the byref live across an await.
                                impSpillSideEffects(spillGlobEffects: true, CHECK_SPILL_ALL, "byref address with async call in value");
                            }
                            break;
                        }

                        default:
                        {
                            NO_WAY("Unexpected fieldAccessor");
                            break;
                        }
                    }

                    // Pull the value from the stack.
                    var op2 = impPopStack().val;

                    if (opcode == CEE_STFLD)
                    {
                        obj = impPopStack().val;

                        if (impIsThis(obj))
                        {
                            aflags |= CORINFO_ACCESS_THIS;
                        }
                    }

                    // Raise InvalidProgramException if static store accesses non-static field
                    if (isStoreStatic && ((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_STATIC) is 0))
                    {
                        BADCODE("static access on an instance field");
                    }

                    // We are using stfld on a static field.
                    // We allow it, but need to eval any side-effects for obj
                    if (((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_STATIC) is not 0) && (obj is not null))
                    {
                        if ((obj.Flags & GTF_SIDE_EFFECT) is not 0)
                        {
                            obj = gtUnusedValNode(obj);
                            impAppendTree(obj, CHECK_SPILL_ALL, impCurStmtDI);
                        }
                        obj = null;
                    }

                    // Handle the cases that use the stored value (obj).
                    // Conveniently these don't trigger type initialization, so there aren't
                    // any ordering issues between it and the tree for the stored value.
                    switch (fieldInfo.fieldAccessor)
                    {
                        case CORINFO_FIELD_INSTANCE:
#if FEATURE_READYTORUN
                        case CORINFO_FIELD_INSTANCE_WITH_BASE:
#endif
                        {
                            var mayOverlap = StructHasOverlappingFields(info.compCompHnd->getClassAttribs(resolvedToken.hClass));

                            if (expandAddrInline)
                            {
                                assert(obj is not null);

                                if (obj.IsLclVarAddr)
                                {
                                    lvaGetDesc(obj.AsLclFld().LclNum).lvFieldAccessed = true;
                                }

                                // When the offset is small enough, expand the address inline
                                // as ADD(obj, offset). The null check will happen as part of
                                // the store itself.
                                var fieldSeq = null as FieldSeq;

                                if ((obj.Type is TYP_REF) && !mayOverlap)
                                {
                                    fieldSeq = FieldSeqStore.Create(resolvedToken.hField, fieldInfo.offset, FieldSeq.FieldKind.Instance);
                                }

                                if (fieldInfo.offset is not 0)
                                {
                                    if ((obj.Oper is GT_LCL_ADDR) && IsValidLclAddr(obj.AsLclFld().LclNum, obj.AsLclFld().LclOffs + fieldInfo.offset))
                                    {
                                        obj.AsLclFld().LclOffs += unchecked((ushort)(fieldInfo.offset));
                                        op1 = obj;
                                    }
                                    else
                                    {
                                        var addrType = (obj.Type is TYP_I_IMPL) ? TYP_I_IMPL : TYP_BYREF;
                                        op1 = gtNewBinaryNode(GT_ADD, addrType, obj, gtNewIconNode(fieldInfo.offset, fieldSeq));
                                        op1 = gtFoldExpr(op1);
                                    }
                                }
                                else
                                {
                                    op1 = obj;
                                }
                            }
                            else
                            {
                                assert(obj is not null);
                                op1 = gtNewFieldAddrNode(obj, resolvedToken.hField, fieldInfo.offset);

#if FEATURE_READYTORUN
                                if (fieldInfo.fieldAccessor == CORINFO_FIELD_INSTANCE_WITH_BASE)
                                {
                                    op1.AsFieldAddr().FieldLookup = fieldInfo.fieldLookup;
                                }
#endif
                                op1.AsFieldAddr().MayOverlap = mayOverlap;
                            }

                            if (compIsForInlining && impInlineIsGuaranteedThisDerefBeforeAnySideEffects(op2, additionalCallArgs: Unsafe.NullRef<CallArgs>(), obj, impInlineInfo.inlArgInfo))
                            {
                                impInlineInfo.thisDereferencedFirst = true;
                            }
                            break;
                        }

                        case CORINFO_FIELD_STATIC_TLS:
                        {
#if TARGET_X86
                            // Legacy TLS access is implemented as intrinsic on x86 only.
                            op1 = gtNewFieldAddrNode(TYP_I_IMPL, obj: null, resolvedToken.hField, fieldInfo.offset);
                            op1.Flags |= GTF_FLD_TLS; // fgMorphExpandTlsField will handle the transformation.
                            break;
#else
                            fieldInfo.fieldAccessor = CORINFO_FIELD_STATIC_ADDR_HELPER;
                            goto case CORINFO_FIELD_STATIC_ADDR_HELPER;
#endif
                        }

                        case CORINFO_FIELD_STATIC_ADDR_HELPER:
                        case CORINFO_FIELD_INSTANCE_HELPER:
                        case CORINFO_FIELD_INSTANCE_ADDR_HELPER:
                        {
                            op1 = gtNewRefComField(obj, resolvedToken, aflags, fieldInfo, lclTyp, op2);
                            assert(op1 is not null);

                            Append(this, op1, CHECK_SPILL_ALL);
                            break;
                        }

                        case CORINFO_FIELD_STATIC_TLS_MANAGED:
                        {
                            MethodHasTlsFieldAccess = true;
                            goto case CORINFO_FIELD_STATIC_ADDRESS;
                        }

                        case CORINFO_FIELD_STATIC_ADDRESS:
                        case CORINFO_FIELD_STATIC_RVA_ADDRESS:
                        case CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER:
                        case CORINFO_FIELD_STATIC_GENERICS_STATIC_HELPER:
                        case CORINFO_FIELD_STATIC_READYTORUN_HELPER:
                        case CORINFO_FIELD_STATIC_RELOCATABLE:
                        {
                            // Handled above
                            break;
                        }

                        default:
                        {
                            NO_WAY("Unexpected fieldAccessor");
                            break;
                        }
                    }

                    // V4.0 allows stores of i4 constant values to i8 type vars when IL verifier is bypassed (full
                    // trust apps). The reason this works is that JIT stores an i4 constant in GenTree union during
                    // importation and reads from the union as if it were a long during code generation. Though this
                    // can potentially read garbage, one can get lucky to have this working correctly.
                    //
                    // This code pattern is generated by Dev10 MC++ compiler while storing to fields when compiled with
                    // /O2 switch (default when compiling retail configs in Dev10) and a customer app has taken a
                    // dependency on it. To be backward compatible, we will explicitly add an upward cast here so that
                    // it works correctly always.
                    //
                    // Note that this is limited to x86 alone as there is no back compat to be addressed for Arm JIT
                    // for V4.0.

#if !TARGET_64BIT
                    // In UWP6.0 and beyond (post-.NET Core 2.0), we decided to let this cast from int to long be
                    // generated for ARM as well as x86, so the following IR will be accepted:
                    // STMTx (IL 0x... ???)
                    //   *  STORE_LCL_VAR long
                    //   \--*  CNS_INT   int    2

                    if ((lclTyp != op2.Type) && op2.Oper.IsConst && varTypeIsIntOrI(op2.Type) && (lclTyp is TYP_LONG))
                    {
                        op2 = gtNewCastNode(lclTyp, op2, fromUnsigned: false, lclTyp);
                    }
#endif
                    // Allow a downcast of op2 from TYP_I_IMPL into a 32-bit Int for x86 JIT compatibility.
                    // Allow an upcast of op2 from a 32-bit Int into TYP_I_IMPL for x86 JIT compatibility.
                    op2 = impImplicitIorI4Cast(op2, lclTyp);
                    op2 = impImplicitR4orR8Cast(op2, lclTyp);

                    // Currently, *all* TYP_REF statics are stored inside an "object[]" array that itself
                    // resides on the managed heap, and so we can use an unchecked write barrier for this
                    // store. Likewise if we're storing to a field of an on-heap object.
                    if ((lclTyp is TYP_REF) && (((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_STATIC) is not 0) || (obj!.Type is TYP_REF)))
                    {
                        indirFlags |= GTF_IND_TGT_HEAP;
                    }
                    else if ((lclTyp is TYP_STRUCT) && (fieldInfo.structType != NO_CLASS_HANDLE) && eeIsByrefLike(fieldInfo.structType))
                    {
                        // Field's type is a byref-like struct -> address is not on the heap.
                        indirFlags |= GTF_IND_TGT_NOT_HEAP;
                    }
                    else if ((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_STATIC) is 0)
                    {
                        // Field's owner is a byref-like struct and the field is not static -> address is not on the heap.
                        var fldOwner = info.compCompHnd->getFieldClass(resolvedToken.hField);

                        if ((fldOwner != NO_CLASS_HANDLE) && eeIsByrefLike(fldOwner))
                        {
                            indirFlags |= GTF_IND_TGT_NOT_HEAP;
                        }
                    }

                    assert(op1 is not null);
                    assert(varTypeIsI(op1.Type));

                    if (lclTyp is TYP_STRUCT)
                    {
                        assert(layout is not null);
                        op1 = gtNewStoreBlkNode(op1, op2, layout, indirFlags);
                    }
                    else
                    {
                        op1 = gtNewStoreIndNode(lclTyp, op1, op2, indirFlags);
                    }
                    impAnnotateFieldIndir(op1.AsIndir());

                    if (varTypeIsStruct(op1.Type))
                    {
                        op1 = impStoreStruct(op1, CHECK_SPILL_ALL);
                    }
                    Append(this, op1, CHECK_SPILL_ALL);
                    break;
                }

                case CEE_NEWARR:
                {
                    // Get the class type index operand
                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Newarr);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var op1 = null as GenTree;

                    if (!IsAot)
                    {
                        // Need to restore array classes before creating array objects on the heap
                        op1 = impTokenToHandle(resolvedToken, mustRestoreHandle: true);

                        if (op1 is null)
                        {
                            return;
                        }
                    }

                    var tiRetVal = makeTypeInfo(resolvedToken.hClass);

                    CORINFO_HELPER_DESC calloutHelper;
                    var accessAllowedResult = info.compCompHnd->canAccessClass(&resolvedToken, info.compMethodHnd, &calloutHelper);
                    impHandleAccessAllowed(accessAllowedResult, calloutHelper);

                    // Form the arglist: array class handle, size
                    var op2 = impPopStack().val;
                    assertImp(genActualTypeIsIntOrI(op2.Type), op1, op2);

                    // The array helper takes a native int for array length.
                    // So if we have an int, explicitly extend it to be a native int.
                    op2 = impImplicitIorI4Cast(op2, TYP_I_IMPL);

                    var isFrozenAllocator = false;
                    // If we're jitting a static constructor and detect the following code pattern:
                    //
                    //  newarr
                    //  stsfld
                    //  ret
                    //
                    // we emit a "frozen" allocator for newarr to, hopefully, allocate that array on a frozen segment.
                    // This is a very simple and conservative implementation targeting Array.Empty<T>()'s shape
                    // Ideally, we want to be able to use frozen allocators more broadly, but such an analysis is
                    // not trivial.
                    //
                    if (((info.compFlags & FLG_CCTOR) is not 0) && opts.jitFlags->IsSet(JitFlags.JIT_FLAG_FROZEN_ALLOC_ALLOWED))
                    {
                        // Check next two opcodes (have to be STSFLD and RET)
                        var nextOpcode1 = codeAddr + sizeof(mdToken);
                        var nextOpcode2 = nextOpcode1 + sizeof(mdToken) + 1;

                        if ((nextOpcode2 < codeEndp) && ((OPCODE)(nextOpcode1[0]) == CEE_STSFLD))
                        {
                            if ((OPCODE)(nextOpcode2[0]) == CEE_RET)
                            {
                                // Check that the field is "static readonly", we don't want to waste memory for potentially mutable fields.

                                impResolveToken(nextOpcode1 + 1, out var fldToken, CORINFO_TOKENKIND_Field);
                                eeGetFieldInfo(fldToken, CORINFO_ACCESS_SET, out var fieldInfo);
                                var flagsToCheck = CORINFO_FLG_FIELD_STATIC | CORINFO_FLG_FIELD_FINAL;

                                if (((fieldInfo.fieldFlags & flagsToCheck) == flagsToCheck) && !eeIsSharedInst(info.compClassHnd))
                                {
#if FEATURE_READYTORUN
                                    if (IsAot)
                                    {
                                        // Need to restore array classes before creating array objects on the heap
                                        op1 = impTokenToHandle(resolvedToken, mustRestoreHandle: true);
                                    }
#endif
                                    assert(op1 is not null);
                                    op1 = gtNewHelperCallNode(TYP_REF, CORINFO_HELP_NEWARR_1_MAYBEFROZEN, op1, op2);
                                    isFrozenAllocator = true;
                                }
                            }
                        }
                    }

                    var helper = CORINFO_HELP_UNDEF;

#if FEATURE_READYTORUN
                    var usingReadyToRunHelper = false;

                    if (IsAot && !isFrozenAllocator)
                    {
                        helper = CORINFO_HELP_READYTORUN_NEWARR_1;
                        op1 = impReadyToRunHelperToTree(resolvedToken, helper, TYP_REF, op2);
                        usingReadyToRunHelper = (op1 is not null);

                        if (!usingReadyToRunHelper)
                        {
                            // TODO: ReadyToRun: When generic dictionary lookups are necessary, replace the lookup call
                            // and the newarr call with a single call to a dynamic R2R cell that will:
                            //      1) Load the context
                            //      2) Perform the generic dictionary lookup and caching, and generate the appropriate stub
                            //      3) Allocate the new array
                            // Reason: performance (today, we'll always use the slow helper for the R2R generics case)

                            op1 = impTokenToHandle(resolvedToken, mustRestoreHandle: true);

                            if (op1 is null)
                            {
                                return;
                            }
                        }
                    }

                    if (!usingReadyToRunHelper && !isFrozenAllocator)
#endif
                    {
                        assert(op1 is not null);

                        // Create a call to 'new'
                        helper = info.compCompHnd->getNewArrHelper(resolvedToken.hClass);

                        // Note that this only works for shared generic code because the same helper is used for all reference array types
                        op1 = gtNewHelperCallNode(TYP_REF, helper, op1, op2);
                    }

                    assert(op1 is not null);
                    op1.AsCall()._compileTimeHelperArgumentHandle = (CORINFO_GENERIC_HANDLE)(resolvedToken.hClass);

                    // Remember that this function contains 'new' of an SD array.
                    optMethodFlags |= OMF_HAS_NEWARRAY;
                    block.SetFlags(BBF_HAS_NEWARR);

                    if (opts.OptimizationEnabled)
                    {
                        // We assign the newly allocated object (by a GT_CALL to newarr node)
                        // to a temp. Note that the pattern "temp = allocArr" is required
                        // by ObjectAllocator phase to be able to determine newarr nodes
                        // without exhaustive walk over all expressions.

                        var lclNum = lvaGrabTemp(shortLifetime: true, "NewArr temp");
                        impStoreToTemp(lclNum, op1, CHECK_SPILL_ALL);

                        assert(!lvaTable[lclNum].lvSingleDef);
                        lvaTable[lclNum].lvSingleDef = true;

                        JITDUMP($"Marked V{lclNum:D2} as a single def local\n");
                        lvaSetClass(lclNum, resolvedToken.hClass, isExact: true);

                        // Push the result of the call on the stack
                        impPushOnStack(gtNewLclvNode(TYP_REF, lclNum), tiRetVal);

#if DEBUG
                        // Under SPMI, look up info we might ask for if we stack allocate this array,
                        // but only if we know the precise type
                        //
                        if ((JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] is not 0) && !eeIsSharedInst(resolvedToken.hClass))
                        {
                            void* pEmbedClsHnd;
                            info.compCompHnd->embedClassHandle(resolvedToken.hClass, &pEmbedClsHnd);

                            var elemClsHnd = NO_CLASS_HANDLE;
                            var elemCorType = info.compCompHnd->getChildType(resolvedToken.hClass, &elemClsHnd);

                            var elemType = elemCorType.VarType;

                            if (elemType is  TYP_STRUCT)
                            {
                                typGetObjLayout(elemClsHnd);
                                info.compCompHnd->isValueClass(elemClsHnd);
                            }
                            compGetHelperFtn(CORINFO_HELP_MEMZERO);
                        }
#endif
                    }
                    else
                    {
                        /* Push the result of the call on the stack */
                        impPushOnStack(op1, tiRetVal);
                    }

                    callTyp = TYP_REF;
                    break;
                }

                case CEE_LOCALLOC:
                {
                    // We don't allow locallocs inside handlers
                    if (block.hasHndIndex)
                    {
                        BADCODE("Localloc can't be inside handler");
                    }

                    // Get the size to allocate

                    var op1 = null as GenTree;
                    var op2 = impPopStack().val;

                    assertImp(genActualTypeIsIntOrI(op2.Type), op1, op2);

                    if (stackState.esStackDepth is not 0)
                    {
                        BADCODE("Localloc can only be used when the stack is empty");
                    }

                    // If the localloc is not in a loop and its size is a small constant,
                    // create a new block layout struct local var and return its address.

                    var convertedToLocal = false;

                    // Need to aggressively fold here, as even fixed-size locallocs
                    // will have casts in the way.
                    op2 = gtFoldExpr(op2);

                    if (op2.Oper.IsIntegralConst)
                    {
                        var allocSize = op2.AsIntCon().IconValue;
                        var bbInALoop = impBlockIsInALoop(block);

                        if (allocSize is 0)
                        {
                            // Result is null
                            JITDUMP("Converting stackalloc of 0 bytes to push null unmanaged pointer\n");
                            op1 = gtNewIconNode(TYP_I_IMPL, 0);
                            convertedToLocal = true;
                        }
                        else if ((allocSize > 0) && !bbInALoop)
                        {
                            // Get the size threshold for local conversion
                            var maxSize = DEFAULT_MAX_LOCALLOC_TO_LOCAL_SIZE;

#if DEBUG
                            // Optionally allow this to be modified
                            maxSize = JitConfig[ConfigInteger.JitStackAllocToLocalSize];
#endif

                            if (allocSize <= maxSize)
                            {
                                var stackallocAsLocal = lvaGrabTemp(shortLifetime: false, "stackallocLocal");
                                JITDUMP($"Converting stackalloc of {allocSize} bytes to new local V{stackallocAsLocal:D2}\n");

                                lvaSetStruct(stackallocAsLocal, typGetBlkLayout((int)(allocSize)), unsafeValueClsCheck: false);

                                ref var lvaDsc = ref lvaTable[stackallocAsLocal];

                                lvaDsc.lvHasLdAddrOp = true;
                                lvaDsc.lvIsUnsafeBuffer = true;

                                op1 = gtNewLclVarAddrNode(TYP_I_IMPL, stackallocAsLocal);
                                convertedToLocal = true;

                                if (compIsForInlining && info.compInitMem && !impInlineRoot.info.compInitMem)
                                {
                                    // Explicitly zero out the local if we're inlining a method with InitLocals into a
                                    // method without InitLocals.
                                    impStoreToTemp(stackallocAsLocal, gtNewIconNode(TYP_INT, 0), CHECK_SPILL_ALL);
                                }

                                // Request stack security for this method.
                                NeedsGSSecurityCookie = true;
                            }
                        }
                    }

                    if (!convertedToLocal)
                    {
                        // Bail out if inlining and the localloc was not converted.
                        //
                        // Note we might consider allowing the inline, if the call
                        // site is not in a loop.
                        if (compIsForInlining)
                        {
                            var obs = op2.Oper.IsIntegralConst ? InlineObservation.CALLEE_LOCALLOC_TOO_LARGE
                                                               : InlineObservation.CALLSITE_LOCALLOC_SIZE_UNKNOWN;
                            compInlineResult.NoteFatal(obs);
                            return;
                        }

                        op1 = gtNewUnaryNode(GT_LCLHEAP, TYP_I_IMPL, op2);

                        // We do not model stack overflow from localloc as an exception side effect.
                        // Obviously, we don't want locallocs to be CSE'd.
                        op1.Flags |= GTF_DONT_CSE;

                        // Request stack security for this method.
                        NeedsGSSecurityCookie = true;

                        /* The FP register may not be back to the original value at the end
                            of the method, even if the frame size is 0, as localloc may
                            have modified it. So we will HAVE to reset it */
                        compLocallocUsed = true;
                    }
                    else
                    {
                        compLocallocOptimized = true;
                    }

                    assert(op1 is not null);
                    impPushOnStack(op1, new typeInfo());
                    break;
                }

                case CEE_ISINST:
                {
                    // Get the type token
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Casting);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var op2 = null as GenTree;

                    if (!IsAot)
                    {
                        op2 = impTokenToHandle(resolvedToken, mustRestoreHandle: false);

                        if (op2 is null)
                        {
                            return;
                        }
                    }

                    CORINFO_HELPER_DESC calloutHelper;
                    var accessAllowedResult = info.compCompHnd->canAccessClass(&resolvedToken, info.compMethodHnd, &calloutHelper);
                    impHandleAccessAllowed(accessAllowedResult, calloutHelper);

                    var op1 = impPopStack().val;

                    var optTree = impOptimizeCastClassOrIsInst(op1, resolvedToken, false);

                    if (optTree is not null)
                    {
                        impPushOnStack(optTree, new typeInfo());
                    }
                    else
                    {

#if FEATURE_READYTORUN
                        var usingReadyToRunHelper = false;

                        if (IsAot)
                        {
                            var opLookup = impReadyToRunHelperToTree(resolvedToken, CORINFO_HELP_READYTORUN_ISINSTANCEOF, TYP_REF, op1);
                            usingReadyToRunHelper = opLookup is not null;

                            op1 = usingReadyToRunHelper ? opLookup : op1;

                            if (!usingReadyToRunHelper)
                            {
                                // TODO: ReadyToRun: When generic dictionary lookups are necessary, replace the lookup call
                                // and the isinstanceof_any call with a single call to a dynamic R2R cell that will:
                                //      1) Load the context
                                //      2) Perform the generic dictionary lookup and caching, and generate the appropriate
                                //      stub
                                //      3) Perform the 'is instance' check on the input object
                                // Reason: performance (today, we'll always use the slow helper for the R2R generics case)

                                op2 = impTokenToHandle(resolvedToken, mustRestoreHandle:false);

                                if (op2 is null)
                                {
                                    return;
                                }
                            }
                        }

                        if (!usingReadyToRunHelper)
#endif
                        {
                            assert(op1 is not null);
                            assert(op2 is not null);

                            var booleanCheck = impMatchIsInstBooleanConversion(codeAddr + sz, codeEndp, out var consumed);
                            op1 = impCastClassOrIsInstToTree(op1, op2, ref resolvedToken, false, ref booleanCheck, opcodeOffs);

                            if (booleanCheck)
                            {
                                sz += consumed;
                            }
                        }

                        if (compDonotInline)
                        {
                            return;
                        }

                        assert(op1 is not null);
                        impPushOnStack(op1, new typeInfo());
                    }
                    break;
                }

                case CEE_REFANYVAL:
                {

                    // get the class handle and make a ICON node out of it

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var op2 = impTokenToHandle(resolvedToken);

                    if (op2 is null)
                    {
                        return;
                    }

                    var op1 = impPopStack().val;

                    // make certain it is normalized;
                    op1 = impNormStructVal(op1, CHECK_SPILL_ALL);

                    // Call helper GETREFANY(classHandle, op1);
                    var helperCall = gtNewHelperCallNode(TYP_BYREF, CORINFO_HELP_GETREFANY);

                    var clsHandleArg = NewCallArg.CreateForPrimitive(op2);
                    var typedRefArg = NewCallArg.CreateForStruct(op1, TYP_STRUCT, typGetObjLayout(impRefAnyClass));

                    helperCall.Args.PushFront(typedRefArg);
                    helperCall.Args.PushFront(clsHandleArg);
                    helperCall.Flags |= (op1.Flags | op2.Flags) & GTF_ALL_EFFECT;

                    impPushOnStack(helperCall, new typeInfo());
                    break;
                }

                case CEE_REFANYTYPE:
                {
                    var op1 = impPopStack().val;

                    // Get the address of the refany
                    op1 = impGetNodeAddr(op1, CHECK_SPILL_ALL, GTF_IND_MUST_PRESERVE_FLAGS, out var indirFlags);

                    // Fetch the type from the correct slot
                    op1 = gtNewBinaryNode(GT_ADD, TYP_BYREF, op1, gtNewIconNode(TYP_I_IMPL, OFFSETOF__CORINFO_TypedReference__type));
                    op1 = gtNewIndir(TYP_BYREF, op1, indirFlags);

                    // Convert native TypeHandle to RuntimeTypeHandle.
                    var call = gtNewHelperCallNode(TYP_STRUCT, CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE_MAYBENULL, op1);

                    var classHandle = impTypeHandleClass;

                    // The handle struct is returned in register
                    call._returnType = RuntimeHandleUnderlyingType;
                    call.RetClsHnd = classHandle;
#if FEATURE_MULTIREG_RET
                    call.InitializeStructReturnType(this, classHandle, call.UnmanagedCallConv);
#endif

                    impPushOnStack(call, new typeInfo(TYP_STRUCT));
                    break;
                }

                case CEE_LDTOKEN:
                {
                    // Get the Class index
                    assertImp(sz == sizeof(int));

                    var lastLoadToken = codeAddr;
                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Ldtoken);

                    var tokenType = info.compCompHnd->getTokenTypeAsHandle(&resolvedToken);

                    var op1 = impTokenToHandle(resolvedToken, mustRestoreHandle: true);

                    if (op1 is null)
                    {
                        return;
                    }

                    var helper = CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE;
                    assert(resolvedToken.hClass is not null);

                    if (resolvedToken.hMethod is not null)
                    {
                        helper = CORINFO_HELP_METHODDESC_TO_STUBRUNTIMEMETHOD;
                    }
                    else if (resolvedToken.hField is not null)
                    {
                        helper = CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD;
                    }

                    var call = gtNewHelperCallNode(TYP_STRUCT, helper, op1);

                    // The handle struct is returned in register and
                    // it could be consumed both as `TYP_STRUCT` and `TYP_REF`.
                    call._returnType = RuntimeHandleUnderlyingType;

#if FEATURE_MULTIREG_RET
                    call.InitializeStructReturnType(this, tokenType, call.UnmanagedCallConv);
#endif

                    call.RetClsHnd = tokenType;

                    impPushOnStack(call, makeTypeInfo(tokenType));
                    break;
                }

                case CEE_UNBOX:
                case CEE_UNBOX_ANY:
                {
                    // Get the Class index
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var op2 = impTokenToHandle(resolvedToken, out var runtimeLookup);

                    if (op2 is null)
                    {
                        assert(compDonotInline);
                        return;
                    }

                    // Run this always so we can get access exceptions even with SkipVerification.

                    CORINFO_HELPER_DESC calloutHelper;
                    var accessAllowedResult = info.compCompHnd->canAccessClass(&resolvedToken, info.compMethodHnd, &calloutHelper);
                    impHandleAccessAllowed(accessAllowedResult, calloutHelper);

                    var op1 = null as GenTree;

                    if ((opcode is CEE_UNBOX_ANY) && !eeIsValueClass(resolvedToken.hClass))
                    {
                        JITDUMP("\n Importing UNBOX.ANY(refClass) as CASTCLASS\n");
                        op1 = impPopStack().val;

                        if (!TryCastClass(this, opcodeOffs, ref resolvedToken, op1, op2))
                        {
                            return;
                        }
                    }

                    // Pop the object and create the unbox helper call
                    // You might think that for UNBOX_ANY we need to push a different
                    // (non-byref) type, but here we're making the tiRetVal that is used
                    // for the intermediate pointer which we then transfer onto the BLK
                    // instruction. BLK then creates the appropriate tiRetVal.

                    op1 = impPopStack().val;
                    assertImp(op1.Type is TYP_REF, op1, op2);

                    var helper = info.compCompHnd->getUnBoxHelper(resolvedToken.hClass);
                    assert(helper is CORINFO_HELP_UNBOX or CORINFO_HELP_UNBOX_NULLABLE);

                    // Check legality and profitability of inline expansion for unboxing.
                    assert(compCurBB is not null);

                    var canExpandInline = helper is CORINFO_HELP_UNBOX;
                    var shouldExpandInline = !compCurBB.isRunRarely && opts.OptimizationEnabled;

                    if (canExpandInline && shouldExpandInline)
                    {
                        var cloneOperand = null as GenTree;

                        // See if we know anything about the type of op1, the object being unboxed.
                        var clsHnd = gtGetClassHandle(op1, out var isExact, out var isNonNull);

                        // We can skip the "exact" bit here as we are comparing to a value class.
                        // compareTypesForEquality should bail on comparisons for shared value classes.
                        if (clsHnd != NO_CLASS_HANDLE)
                        {
                            var compare = info.compCompHnd->compareTypesForEquality(resolvedToken.hClass, clsHnd);

                            if (compare is TypeCompareState.Must)
                            {
                                JITDUMP($"\nOptimizing {((opcode is CEE_UNBOX) ? "UNBOX" : "UNBOX.ANY")} ({eeGetClassName(clsHnd)}) -- type test will succeed\n");

                                var boxPayloadOffset = gtNewIconNode(TYP_I_IMPL, TARGET_POINTER_SIZE);
                                var boxPayloadAddress = gtNewBinaryNode(GT_ADD, TYP_BYREF, op1, boxPayloadOffset);

                                // For UNBOX, null check (if necessary), and then leave the box payload byref on the stack.
                                if (opcode is CEE_UNBOX)
                                {
                                    op1 = impCloneExpr(op1, out cloneOperand, CHECK_SPILL_ALL, "optimized unbox clone");
                                    assert(cloneOperand is not null);

                                    boxPayloadAddress.Op1 = cloneOperand;

                                    if (fgAddrCouldBeNull(op1))
                                    {
                                        var nullcheck = gtNewNullCheck(op1);

                                        // Add an ordering dependency between the null
                                        // check and forming the byref; the JIT assumes
                                        // in many places that the only legal null
                                        // byref is literally 0, and since the byref
                                        // leaks out here, we need to ensure it is
                                        // nullchecked.
                                        nullcheck.HasOrderingSideEffect = true;
                                        boxPayloadAddress.HasOrderingSideEffect = true;

                                        var result = gtNewCommaNode(TYP_BYREF, nullcheck, boxPayloadAddress);
                                        impPushOnStack(result, new typeInfo());
                                    }
                                    else
                                    {
                                        // We don't need a nullcheck if this is e.g. a preinitialized value
                                        impPushOnStack(boxPayloadAddress, new typeInfo());
                                    }
                                    break;
                                }

                                // For UNBOX.ANY load the struct from the box payload byref (the load will nullcheck)
                                assert(opcode is CEE_UNBOX_ANY);
                                impPushOnStack(boxPayloadAddress, new typeInfo());

                                Obj(this, resolvedToken, prefixFlags);
                                break;
                            }
                            else
                            {
                                JITDUMP($"\nUnable to optimize {((opcode is CEE_UNBOX) ? "UNBOX" : "UNBOX.ANY")} -- can't resolve type comparison\n");
                            }
                        }
                        else
                        {
                            JITDUMP($"\nUnable to optimize {((opcode is CEE_UNBOX) ? "UNBOX" : "UNBOX.ANY")} -- class for [{op1.TreeId:D6}] not known\n");
                        }

                        JITDUMP($"\n Importing {((opcode is CEE_UNBOX) ? "UNBOX" : "UNBOX.ANY")} as inline sequence\n");

                        // we are doing normal unboxing
                        // inline the common case of the unbox helper
                        // UNBOX(exp) morphs into
                        // clone = pop(exp);
                        // ((*clone == typeToken) ? nop : helper(clone, typeToken));
                        // push(clone + TARGET_POINTER_SIZE)

                        op1 = impCloneExpr(op1, out cloneOperand, CHECK_SPILL_ALL, "inline UNBOX clone1");
                        assert(cloneOperand is not null);

                        op1 = gtNewMethodTableLookup(op1);
                        var condBox = gtNewBinaryNode(GT_EQ, TYP_INT, op1, op2);

                        op1 = impCloneExpr(cloneOperand, out cloneOperand, CHECK_SPILL_ALL, "inline UNBOX clone2");
                        assert(cloneOperand is not null);

                        op2 = impTokenToHandle(resolvedToken);

                        if (op2 is null)
                        {
                            return;
                        }
                        op1 = gtNewHelperCallNode(TYP_VOID, helper, op2, op1);

                        op1 = gtNewColonNode(TYP_VOID, gtNewNothingNode(), op1);
                        op1 = gtNewQmarkNode(TYP_VOID, condBox, op1.AsColon());

                        // QMARK nodes cannot reside on the evaluation stack. Because there
                        // may be other trees on the evaluation stack that side-effect the
                        // sources of the UNBOX operation we must spill the stack.

                        impAppendTree(op1, CHECK_SPILL_ALL, impCurStmtDI);

                        // Create the address-expression to reference past the object header
                        // to the beginning of the value-type. Today this means adjusting
                        // past the base of the objects vtable field which is pointer sized.

                        op2 = gtNewIconNode(TYP_I_IMPL, TARGET_POINTER_SIZE);
                        op1 = gtNewBinaryNode(GT_ADD, TYP_BYREF, cloneOperand, op2);
                    }
                    else if (helper is CORINFO_HELP_UNBOX_NULLABLE)
                    {
                        // op1 is the object being unboxed
                        // op2 is either a class handle node or a runtime lookup node (it's fine to reorder)
                        op1 = impInlineUnboxNullable(resolvedToken.hClass, op2, op1);
                    }
                    else
                    {
                        // Don't optimize, just call the helper and be done with it
                        JITDUMP($"\n Importing {((opcode is CEE_UNBOX) ? "UNBOX" : "UNBOX.ANY")} as helper call because {(canExpandInline ? "want smaller code or faster jitting" : "inline expansion not legal")}\n");

                        assert(helper == CORINFO_HELP_UNBOX);
                        op1 = gtNewHelperCallNode(TYP_BYREF, helper, op2, op1);
                    }

                    // Unbox helper returns a byref.
                    // UnboxNullable helper returns a struct.
                    assert((helper is CORINFO_HELP_UNBOX && (op1.Type is TYP_BYREF)) || (helper is CORINFO_HELP_UNBOX_NULLABLE && (op1.Type is TYP_STRUCT)));

                    // ----------------------------------------------------------------------
                    // | \ helper  |                         |                              |
                    // |   \       |                         |                              |
                    // |     \     | CORINFO_HELP_UNBOX      | CORINFO_HELP_UNBOX_NULLABLE  |
                    // |       \   | (which returns a BYREF) | (which returns a STRUCT)     |
                    // | opcode  \ |                         |                              |
                    // |---------------------------------------------------------------------
                    // | UNBOX     | push the BYREF          | spill the STRUCT to a local, |
                    // |           |                         | push the BYREF to this local |
                    // |---------------------------------------------------------------------
                    // | UNBOX_ANY | push a GT_BLK of        | push the STRUCT local        |
                    // |           | the BYREF               |                              |
                    // |---------------------------------------------------------------------

                    if (opcode is CEE_UNBOX)
                    {
                        if (helper is CORINFO_HELP_UNBOX_NULLABLE)
                        {
                            // NOTE: what we do here doesn't comply with the ECMA spec, see
                            // https://github.com/dotnet/runtime/issues/86203#issuecomment-1546709542
                            // Although, now with escape analysis being enabled we can afford a temp GC alloc here?

                            // Unbox nullable helper returns a struct type.
                            // We need to spill it to a temp so than can take the address of it.
                            // Here we need unsafe value cls check, since the address of struct is taken to be used
                            // further along and potetially be exploitable.

                            // op1 is always a local, see code above for CORINFO_HELP_UNBOX_NULLABLE
                            op1 = gtNewLclVarAddrNode(TYP_I_IMPL, op1.AsLclVar().LclNum);
                        }

                        impPushOnStack(op1, new typeInfo());
                    }
                    else
                    {
                        assert(opcode is CEE_UNBOX_ANY);

                        if (helper is CORINFO_HELP_UNBOX)
                        {
                            // Normal unbox helper returns a TYP_BYREF.
                            impPushOnStack(op1, new typeInfo());
                            Obj(this, resolvedToken, prefixFlags);
                        }
                        else
                        {
                            assert(helper is CORINFO_HELP_UNBOX_NULLABLE, "Make sure the helper is nullable!");

                            // If non register passable struct we have it materialized in the RetBuf.
                            assert(op1.Type is TYP_STRUCT);
                            impPushOnStack(op1, makeTypeInfo(resolvedToken.hClass));
                        }
                    }
                    break;
                }

                case CEE_BOX:
                {
                    /* Get the Class index */
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Box);
                    JITDUMP($" {resolvedToken.token:X8}");

                    CORINFO_HELPER_DESC calloutHelper;
                    var accessAllowedResult = info.compCompHnd->canAccessClass(&resolvedToken, info.compMethodHnd, &calloutHelper);
                    impHandleAccessAllowed(accessAllowedResult, calloutHelper);

                    // Note BOX can be used on things that are not value classes, in which
                    // case we get a NOP.  However the verifier's view of the type on the
                    // stack changes (in generic code a 'T' becomes a 'boxed T')
                    if (!eeIsValueClass(resolvedToken.hClass))
                    {
                        JITDUMP("\n Importing BOX(refClass) as NOP\n");
                        stackState.esStack[stackState.esStackDepth - 1].seTypeInfo = new typeInfo();
                        break;
                    }

                    var isByRefLike = eeIsByrefLike(resolvedToken.hClass);

                    if (isByRefLike)
                    {
                        // For ByRefLike types we are required to either fold the
                        // recognized patterns in impBoxPatternMatch or otherwise
                        // throw InvalidProgramException at runtime. In either case
                        // we will need to spill side effects of the expression.
                        impSpillSideEffects(spillGlobEffects: false, CHECK_SPILL_ALL, "Required for box of ByRefLike type");
                    }

                    // Look ahead for box idioms
                    var matched = impBoxPatternMatch(resolvedToken, codeAddr + sz, codeEndp, isByRefLike ? BoxPatterns.IsByRefLike : BoxPatterns.None);

                    if (matched >= 0)
                    {
                        // Skip the matched IL instructions
                        sz += matched;
                        break;
                    }

                    if (isByRefLike)
                    {
                        // ByRefLike types are supported in boxing scenarios when the instruction can be elided
                        // due to a recognized pattern above. If the pattern is not recognized, the code is invalid.
                        BADCODE("ByRefLike types cannot be boxed");
                    }
                    else
                    {
                        impImportAndPushBox(resolvedToken);

                        if (compDonotInline)
                        {
                            return;
                        }
                    }
                    break;
                }

                case CEE_SIZEOF:
                {
                    // Get the Class index
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var op1 = gtNewIconNode(TYP_INT, info.compCompHnd->getClassSize(resolvedToken.hClass));
                    impPushOnStack(op1, new typeInfo());
                    break;
                }

                case CEE_CASTCLASS:
                {
                    // Get the Class index
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Casting);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var op2 = null as GenTree;

                    if (!IsAot)
                    {
                        op2 = impTokenToHandle(resolvedToken, mustRestoreHandle: false);

                        if (op2 is null)
                        {
                            return;
                        }
                    }

                    CORINFO_HELPER_DESC calloutHelper;
                    var accessAllowedResult = info.compCompHnd->canAccessClass(&resolvedToken, info.compMethodHnd, &calloutHelper);
                    impHandleAccessAllowed(accessAllowedResult, calloutHelper);

                    var op1 = impPopStack().val;

                    // Pop the address and create the 'checked cast' helper call

                    // At this point we expect typeRef to contain the token, op1 to contain the value being cast,
                    // and op2 to contain code that creates the type handle corresponding to typeRef
                    if (!TryCastClass(this, opcodeOffs, ref resolvedToken, op1, op2))
                    {
                        return;
                    }
                    break;
                }

                case CEE_THROW:
                {
                    if (!fgPgoSynthesized)
                    {
                        // Any block with a throw is rarely executed.
                        block.bbSetRunRarely();
                    }

                    // Pop the exception object and create the 'throw' helper call
                    var op1 = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_THROW, impPopStack().val);

                    // Fall through to clear out the eval stack.
                    EvalAppend(this, stackState, op1);
                    break;
                }

                case CEE_RETHROW:
                {
                    assert(!compIsForInlining);

                    if (info.compXcptnsCount is 0)
                    {
                        BADCODE("rethrow outside catch");
                    }

                    // Create the 'rethrow' helper call
                    var op1 = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_RETHROW);

                    EvalAppend(this, stackState, op1);
                    break;
                }

                case CEE_INITOBJ:
                {
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var lclTyp = TypeHandleToVarType(resolvedToken.hClass, out var layout);

                    if (lclTyp != TYP_STRUCT)
                    {
                        var op2 = gtNewZeroConNode(lclTyp);
                        StindValue(this, lclTyp, opcode, prefixFlags, op2);
                    }
                    else
                    {
                        assert(layout is not null);

                        var op1 = impPopStack().val;
                        var op2 = gtNewIconNode(TYP_INT, 0);

                        op1 = gtNewStoreValueNode(op1, op2, layout);
                        Append(this, op1, CHECK_SPILL_ALL);
                    }
                    break;
                }

                case CEE_INITBLK:
                case CEE_CPBLK:
                {
                    var indirFlags = impPrefixFlagsToIndirFlags(prefixFlags);
                    var isVolatile = (indirFlags & GTF_IND_VOLATILE) is not 0;
#if !TARGET_X86
                    if (isVolatile && !impStackTop(0).val.Oper.IsCnsIntOrI)
                    {
                        // We're going to emit a helper call surrounded by memory barriers, so we need to spill any side
                        // effects.
                        impSpillSideEffects(spillGlobEffects: true, CHECK_SPILL_ALL, "spilling side-effects");
                    }
#endif

                    var op3 = gtFoldExpr(impPopStack().val); // Size
                    var op2 = gtFoldExpr(impPopStack().val); // Value / Src addr
                    var op1 = impPopStack().val;             // Dst addr

                    if (op3.Oper.IsCnsIntOrI)
                    {
                        if (op3.IsIntegralConst(0))
                        {
                            if ((op1.Flags & GTF_SIDE_EFFECT) is not 0)
                            {
                                impAppendTree(gtUnusedValNode(op1), CHECK_SPILL_ALL, impCurStmtDI);
                            }

                            if ((op2.Flags & GTF_SIDE_EFFECT) is not 0)
                            {
                                impAppendTree(gtUnusedValNode(op2), CHECK_SPILL_ALL, impCurStmtDI);
                            }
                            break;
                        }

                        var layout = typGetBlkLayout((int)(op3.AsIntConCommon().IconValue));

                        if (opcode is CEE_INITBLK)
                        {
                            if (!op2.IsIntegralConst(0))
                            {
                                op2 = gtNewUnaryNode(GT_INIT_VAL, TYP_INT, op2);
                            }
                        }
                        else
                        {
                            op2 = gtNewLoadValueNode(op2, layout, indirFlags);
                        }
                        op1 = gtNewStoreValueNode(op1, op2, layout, indirFlags);
                    }
                    else
                    {
#if TARGET_64BIT
                        // Cast size to TYP_LONG on 64-bit targets
                        op3 = gtNewCastNode(TYP_LONG, op3, /* fromuint */ true, TYP_LONG);
#endif

                        var call = null as GenTreeCall;

                        if (opcode == CEE_INITBLK)
                        {
                            // value is zero -> memzero, otherwise -> memset
                            if (op2.IsIntegralConst(0))
                            {
                                call = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_MEMZERO, op1, op3);
                            }
                            else
                            {
                                call = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_MEMSET, op1, op2, op3);
                            }
                        }
                        else
                        {
                            call = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_MEMCPY, op1, op2, op3);
                        }

                        if (isVolatile)
                        {
                            // Wrap with memory barriers: store-barrier + call + load-barrier
                            impAppendTree(gtNewMemoryBarrierNode(BARRIER_STORE_ONLY), CHECK_SPILL_ALL, impCurStmtDI);
                            impAppendTree(call, CHECK_SPILL_ALL, impCurStmtDI);
                            op1 = gtNewMemoryBarrierNode(BARRIER_LOAD_ONLY);
                        }
                        else
                        {
                            op1 = call;
                        }
                    }
                    Append(this, op1, CHECK_SPILL_ALL);
                    break;
                }

                case CEE_CPOBJ:
                {
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var lclTyp = TypeHandleToVarType(resolvedToken.hClass, out var layout);

                    if (lclTyp is not TYP_STRUCT)
                    {
                        var op2 = impPopStack().val; // address to load from
                        op2 = gtNewIndir(lclTyp, op2);
                        StindValue(this, lclTyp, opcode, prefixFlags, op2);
                    }
                    else
                    {
                        assert(layout is not null);

                        var op2 = impPopStack().val; // Src addr
                        var op1 = impPopStack().val; // Dest addr

                        op2 = gtNewLoadValueNode(op2, layout);
                        op1 = gtNewStoreValueNode(op1, op2, layout);

                        Append(this, op1, CHECK_SPILL_ALL);
                    }
                    break;
                }

                case CEE_STOBJ:
                {
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var lclTyp = TypeHandleToVarType(resolvedToken.hClass, out var layout);

                    if (!varTypeIsStruct(lclTyp))
                    {
                        Stind(this, lclTyp, opcode, prefixFlags);
                    }
                    else
                    {
                        assert(layout is not null);

                        var op2 = impPopStack().val; // Value
                        var op1 = impPopStack().val; // Ptr
                        assertImp(varTypeIsStruct(op2.Type), op1, op2);

                        var indirFlags = impPrefixFlagsToIndirFlags(prefixFlags);

                        if (eeIsByrefLike(resolvedToken.hClass))
                        {
                            indirFlags |= GTF_IND_TGT_NOT_HEAP;
                        }

                        op1 = gtNewStoreValueNode(op1, op2, layout, indirFlags);
                        op1 = impStoreStruct(op1, CHECK_SPILL_ALL);

                        Append(this, op1, CHECK_SPILL_ALL);
                    }
                    break;
                }

                case CEE_MKREFANY:
                {
                    assert(!compIsForInlining);
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);
                    JITDUMP($" {resolvedToken.token:X8}");

                    var op2 = impTokenToHandle(resolvedToken, mustRestoreHandle: true);

                    if (op2 is null)
                    {
                        return;
                    }

                    CORINFO_HELPER_DESC calloutHelper;
                    var accessAllowedResult = info.compCompHnd->canAccessClass(&resolvedToken, info.compMethodHnd, &calloutHelper);
                    impHandleAccessAllowed(accessAllowedResult, calloutHelper);

                    var op1 = impPopStack().val;

                    // @SPECVIOLATION: TYP_INT should not be allowed here by a strict reading of the spec.
                    // But JIT32 allowed it, so we continue to allow it.
                    assertImp(op1.Type is TYP_BYREF or TYP_I_IMPL or TYP_INT, op1, op2);

                    var refAnyLcl = lvaGrabTemp(shortLifetime: false, "mkrefany temp");
                    lvaSetStruct(refAnyLcl, impRefAnyClass, unsafeValueClsCheck: false);

                    var storeData = gtNewStoreLclFldNode(op1.Type, refAnyLcl, OFFSETOF__CORINFO_TypedReference__dataPtr, op1);
                    var storeType = gtNewStoreLclFldNode(op2.Type, refAnyLcl, OFFSETOF__CORINFO_TypedReference__type, op2);

                    impAppendTree(storeData, CHECK_SPILL_ALL, impCurStmtDI);
                    impAppendTree(storeType, CHECK_SPILL_ALL, impCurStmtDI);

                    impPushOnStack(gtNewLclVarNode(TYP_STRUCT, refAnyLcl), makeTypeInfo(impRefAnyClass));
                    break;
                }

                case CEE_LDOBJ:
                {
                    assertImp(sz == sizeof(int));

                    impResolveToken(codeAddr, out var resolvedToken, CORINFO_TOKENKIND_Class);
                    JITDUMP($" {resolvedToken.token:X8}");

                    Obj(this, resolvedToken, prefixFlags);
                    break;
                }

                case CEE_LDLEN:
                {
                    var op1 = impPopStack().val;

                    if (opts.OptimizationEnabled)
                    {
                        // Use GT_ARR_LENGTH operator so rng check opts see this
                        op1 = gtNewArrLen(TYP_INT, op1, OFFSETOF__CORINFO_Array__length);
                    }
                    else
                    {
                        // Create the expression "*(array_addr + ArrLenOffs)"
                        op1 = gtNewBinaryNode(GT_ADD, TYP_BYREF, op1, gtNewIconNode(TYP_I_IMPL, OFFSETOF__CORINFO_Array__length));
                        op1 = gtNewIndir(TYP_INT, op1);
                    }

                    // Push the result back on the stack
                    impPushOnStack(op1, new typeInfo());
                    break;
                }

                case CEE_BREAK:
                {
                    var op1 = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_USER_BREAKPOINT);
                    Append(this, op1, CHECK_SPILL_ALL);
                    break;
                }

                case CEE_NOP:
                {
                    if (opts.compDbgCode)
                    {
                        var op1 = new GenTree(GT_NO_OP, TYP_VOID);
                        Append(this, op1, CHECK_SPILL_ALL);
                        break;
                    }
                    break;
                }

                /******************************** NYI *******************************/

                case (OPCODE)(0xCC):
                {
                    Debug.WriteLine("CLR: Invalid x86 breakpoint in IL stream");
                    goto case CEE_ILLEGAL;
                }

                case CEE_ILLEGAL:
                case CEE_MACRO_END:
                {
                    goto case default;
                }

                default:
                {
                    if (compIsForInlining)
                    {
                        compInlineResult.NoteFatal(InlineObservation.CALLEE_COMPILATION_ERROR);
                        return;
                    }

                    BADCODE($"unknown opcode: {(ushort)(opcode):X2}");
                    break;
                }
            }

            codeAddr += sz;
            prevOpcode = opcode;

            prefixFlags = 0;
        }

        static void AddOvf(Compiler compiler, bool uns)
        {
            MathOp2Flags(compiler, GT_ADD, ovfl: true, uns);
        }

        static void AdrVar(Compiler compiler, int lclNum)
        {
            // Note that this is supposed to create the transient type "*"
            // which may be used as a TYP_I_IMPL. However we catch places
            // where it is used as a TYP_I_IMPL and change the node if needed.
            // Thus we are pessimistic and may report byrefs in the GC info
            // where it was not absolutely needed, but doing otherwise would
            // require careful rethinking of the importer routines which use
            // the IL validity model (e. g. "impGetByRefResultType").
            var op1 = compiler.gtNewLclVarAddrNode(TYP_BYREF, lclNum);
            PushAdrVar(compiler, op1);
        }

        static void Append(Compiler compiler, GenTree op1, int chkLevel = CHECK_SPILL_NONE)
        {
            compiler.impAppendTree(op1, chkLevel, compiler.impCurStmtDI);

#if DEBUG
            // Remember at which BC offset the tree was finished
            compiler.impNoteLastILoffs();
#endif
        }

        static void ArrSt(Compiler compiler, var_types lclTyp, CORINFO_CLASS_HANDLE stelemClsHnd)
        {
            // The strict order of evaluation is 'array', 'index', 'value', range-check
            // and then store. However, the tree we create does the range-check before
            // evaluating 'value'. So to maintain strict ordering, we spill the stack.
            if ((compiler.impStackTop().val.Flags & GTF_SIDE_EFFECT) is not 0)
            {
                compiler.impSpillSideEffects(spillGlobEffects: false, CHECK_SPILL_ALL, "Strict ordering of exceptions for Array store");
            }

            // Pull the new value from the stack.
            var op2 = compiler.impPopStack().val;
            impBashVarAddrsToI(op2);

            // Pull the index value.
            var op1 = compiler.impPopStack().val;

            // Pull the array address.
            var op3 = compiler.impPopStack().val;
            compiler.assertImp(op3.Type is TYP_REF, op1, op2);

            // Mark the block as containing an index expression
            if ((op3.Oper is GT_LCL_VAR) && (op1.Oper is GT_LCL_VAR or GT_CNS_INT or GT_ADD))
            {
                compiler.optMethodFlags |= OMF_HAS_ARRAYREF;
            }

            // Create the index address node.
            op1 = compiler.gtNewArrayIndexAddr(op3, op1, lclTyp, stelemClsHnd);
            op2 = compiler.impImplicitR4orR8Cast(op2, lclTyp);

            // Create the store node and append it.
            if (lclTyp == TYP_STRUCT)
            {
                var layout = compiler.typGetObjLayout(stelemClsHnd);
                op1 = compiler.gtNewStoreBlkNode(op1, op2, layout);
            }
            else
            {
                op1 = compiler.gtNewStoreIndNode(lclTyp, op1, op2);
            }

            if (varTypeIsStruct(op1.Type))
            {
                op1 = compiler.impStoreStruct(op1, CHECK_SPILL_ALL);
            }
            Append(compiler, op1, CHECK_SPILL_ALL);
        }

        static void Cmp2Ops(Compiler compiler, genTreeOps oper, OPCODE opcode, bool uns)
        {
            var op2 = compiler.impPopStack().val;
            var op1 = compiler.impPopStack().val;

            // Recognize the IL idiom of CGT_UN(op1, 0) and normalize
            // it so that downstream optimizations don't have to.
            if ((opcode == CEE_CGT_UN) && op2.IsIntegralConst(0))
            {
                oper = GT_NE;
                uns = false;
            }

            var op1Type = op1.Type;
            var op2Type = op2.Type;

#if TARGET_64BIT
            if (varTypeIsI(op1Type) && genActualTypeIsInt(op2Type))
            {
                op2 = compiler.impImplicitIorI4Cast(op2, TYP_I_IMPL);
            }
            else if (varTypeIsI(op2Type) && genActualTypeIsInt(op1Type))
            {
                op1 = compiler.impImplicitIorI4Cast(op1, TYP_I_IMPL);
            }
#endif

            compiler.assertImp((op1Type.ActualType == op2Type.ActualType) || (varTypeIsI(op1Type) && varTypeIsI(op2Type)) || (varTypeIsFloating(op1Type) && varTypeIsFloating(op2Type)));

            if ((op1Type != op2Type) && varTypeIsFloating(op1Type))
            {
                op1 = compiler.impImplicitR4orR8Cast(op1, TYP_DOUBLE);
                op2 = compiler.impImplicitR4orR8Cast(op2, TYP_DOUBLE);
            }

            // Create the comparison node.
            var op = compiler.gtNewBinaryNode(oper, TYP_INT, op1, op2);

            // TODO: setting both flags when only one is appropriate.
            if (uns)
            {
                op.Flags |= GTF_RELOP_NAN_UN;
                op.IsUnsigned = true;
            }

            // Fold result, if possible.
            compiler.impPushOnStack(compiler.gtFoldExpr(op), new typeInfo());
        }

        static void Cmp2OpsAndBrAll(Compiler compiler, BasicBlock block, genTreeOps oper, bool uns, bool unordered)
        {
            // Pull two values
            var op2 = compiler.impPopStack().val;
            var op1 = compiler.impPopStack().val;

            var op1Type = op1.Type;
            var op2Type = op2.Type;

#if TARGET_64BIT
            // TODO-Review: this differs in the extending behavior from plain relop import. Why?
            if ((op1Type is TYP_I_IMPL) && genActualTypeIsInt(op2Type))
            {
                op2 = compiler.impImplicitIorI4Cast(op2, TYP_I_IMPL, uns);
            }
            else if ((op2Type is TYP_I_IMPL) && genActualTypeIsInt(op1Type))
            {
                op1 = compiler.impImplicitIorI4Cast(op1, TYP_I_IMPL, uns);
            }
#endif

            compiler.assertImp((op1Type.ActualType == op2Type.ActualType) || (varTypeIsI(op1Type) && varTypeIsI(op2Type)) || (varTypeIsFloating(op1Type) && varTypeIsFloating(op2Type)), op1, op2);

            if (compiler.opts.OptimizationEnabled)
            {
                // We may have already modified `block`'s jump kind, if this is a re-importation.
                var jumpToNextOptimization = false;

                if (block.Kind is BBJ_COND)
                {
                    var trueEdge = block.TrueEdge;
                    var falseEdge = block.FalseEdge;

                    if (trueEdge == falseEdge)
                    {
                        JITDUMP($"{FMT_BB(block.bbNum)} always branches to {FMT_BB(block.FalseTarget.bbNum)}, changing to BBJ_ALWAYS\n");

                        compiler.fgRemoveRefPred(falseEdge);
                        block.SetKindAndTargetEdge(BBJ_ALWAYS, trueEdge);

                        jumpToNextOptimization = true;
                    }
                }
                else if (block.Kind is BBJ_ALWAYS)
                {
                    if (block.JumpsToNext)
                    {
                        jumpToNextOptimization = true;
                    }
                }

                if (jumpToNextOptimization)
                {
                    if ((op1.Flags & GTF_GLOB_EFFECT) != 0)
                    {
                        compiler.impSpillSideEffects(spillGlobEffects: false, CHECK_SPILL_ALL, "Branch to next Optimization, op1 side effect");
                        compiler.impAppendTree(compiler.gtUnusedValNode(op1), CHECK_SPILL_NONE, compiler.impCurStmtDI);
                    }

                    if ((op2.Flags & GTF_GLOB_EFFECT) != 0)
                    {
                        compiler.impSpillSideEffects(spillGlobEffects: false, CHECK_SPILL_ALL, "Branch to next Optimization, op2 side effect");
                        compiler.impAppendTree(compiler.gtUnusedValNode(op2), CHECK_SPILL_NONE, compiler.impCurStmtDI);
                    }

#if DEBUG
                    if (((op1.Flags | op2.Flags) & GTF_GLOB_EFFECT) != 0)
                    {
                        compiler.impNoteLastILoffs();
                    }
#endif
                    return;
                }
            }

            // We can generate an compare of different sized floating point op1 and op2.
            // We insert a cast to double.
            //
            if ((op1Type != op2Type) && varTypeIsFloating(op1Type))
            {
                op1 = compiler.impImplicitR4orR8Cast(op1, TYP_DOUBLE);
                op2 = compiler.impImplicitR4orR8Cast(op2, TYP_DOUBLE);
            }

            // Create and append the operator.
            var op = compiler.gtNewBinaryNode(oper, TYP_INT, op1, op2);

            if (uns)
            {
                op.IsUnsigned = true;
            }

            if (unordered)
            {
                op.Flags |= GTF_RELOP_NAN_UN;
            }
            CondJump(compiler, block, op);
        }

        static void Cmp2OpsAndBr(Compiler compiler, BasicBlock block, genTreeOps oper)
        {
            Cmp2OpsAndBrAll(compiler, block, oper, uns: false, unordered: false);
        }

        static void Cmp2OpsAndBrUn(Compiler compiler, BasicBlock block, genTreeOps oper)
        {
            Cmp2OpsAndBrAll(compiler, block, oper, uns: true, unordered: true);
        }

        static void CondJump(Compiler compiler, BasicBlock block, GenTree op1)
        {
            // Fold comparison if we can
            op1 = compiler.gtFoldExpr(op1);

            // Try to fold the really simple cases like 'iconst *, ifne/ifeq
            // Don't make any blocks unreachable in import only mode
            var effectiveOp1 = op1.EffectiveVal;

            if (effectiveOp1.Oper is GT_CNS_INT)
            {
                // gtFoldExpr() should prevent this as we don't want to make any blocks unreachable under compDbgCode
                assert(!compiler.opts.compDbgCode);

                // BBJ_COND: normal case
                // BBJ_ALWAYS: this can happen if we are reimporting the block for the second time
                compiler.assertImp(block.Kind is BBJ_COND or BBJ_ALWAYS, op1); // normal case

                if (block.Kind is BBJ_COND)
                {
                    var removedEdge = block.TrueEdge;
                    var retainedEdge = block.FalseEdge;

                    if (effectiveOp1.AsIntCon().IconVal is not 0)
                    {
                        (removedEdge, retainedEdge) = (retainedEdge, removedEdge);
                    }

                    JITDUMP($"\nThe conditional jump becomes an unconditional jump to {FMT_BB(retainedEdge.DestinationBlock.bbNum)}\n");

                    compiler.fgRemoveRefPred(removedEdge);
                    block.SetKindAndTargetEdge(BBJ_ALWAYS, retainedEdge);
                    compiler.Metrics.ImporterBranchFold++;
                    compiler.fgRepairProfileCondToUncond(block, retainedEdge, removedEdge, ref compiler.Metrics.ProfileInconsistentImporterBranchFold);
                }

                if (op1.Oper is not GT_CNS_INT)
                {
                    // Ensure we spill any side effects and don't drop them
                    op1 = compiler.gtUnusedValNode(op1);
                    Append(compiler, op1, CHECK_SPILL_ALL);
                }
                return;
            }

            var op = compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, op1);

            // GT_JTRUE is handled specially for non-empty stacks.
            // See 'addStmt' in impImportBlock(block). For correct line numbers, spill stack.

            if (compiler.opts.compDbgCode && compiler.impCurStmtDI.IsValid)
            {
                compiler.impSpillStackEnsure(spillLeaves: true);
            }
            Append(compiler, op, CHECK_SPILL_ALL);
        }

        static void Conv(Compiler compiler, var_types lclTyp, bool uns = false, bool ovfl = false)
        {
            var op1 = compiler.impPopStack().val;
            impBashVarAddrsToI(op1);

            // Casts from floating point types must not have GTF_uint set.
            if (varTypeIsFloating(op1.Type))
            {
                uns = false;
            }

            // At this point uns, ovf, callNode are all set.
            if (varTypeIsSmall(lclTyp) && !ovfl && (op1.Type is TYP_INT) && (op1.Oper is GT_AND))
            {
                var andOp = op1.AsOp();
                var op2 = andOp.Op2;

                if (op2.Oper is GT_CNS_INT)
                {
                    var ival = op2.AsIntCon().IconVal;

                    nint mask;
                    nint umask;

                    switch (lclTyp)
                    {
                        case TYP_BYTE:
                        case TYP_UBYTE:
                        {
                            mask = 0x00FF;
                            umask = 0x007F;
                            break;
                        }

                        case TYP_USHORT:
                        case TYP_SHORT:
                        {
                            mask = 0xFFFF;
                            umask = 0x7FFF;
                            break;
                        }

                        default:
                        {
                            NO_WAY("unexpected type");
                            mask = 0;
                            umask = 0;
                            break;
                        }
                    }

                    if (((ival & umask) == ival) || (((ival & mask) == ival) && uns))
                    {
                        // Toss the cast, it's a waste of time
                        compiler.impPushOnStack(op1, new typeInfo());
                        return;
                    }
                    else if (ival == mask)
                    {
                        // Toss the masking, it's a waste of time, since we sign-extend from the small value anyways
                        op1 = andOp.Op1;
                    }
                }
            }

            // The 'op2' sub-operand of a cast is the 'real' type number, since the result of a cast to one of the 'small' integer types is an integer.
            var type = lclTyp.ActualType;

            // If this is a no-op cast, just use op1.
            if (!ovfl && (type == op1.Type) && (type.Size == lclTyp.Size))
            {
                // Nothing needs to change
                compiler.impPushOnStack(op1, new typeInfo());
            }
            else
            {
                // Work is evidently required, add cast node
                var op = compiler.gtNewCastNode(type, op1, uns, lclTyp);

                if (ovfl)
                {
                    op.Flags |= (GTF_OVERFLOW | GTF_EXCEPT);
                }

                // Try and fold the introduced cast
                compiler.impPushOnStack(compiler.gtFoldExpr(op), new typeInfo());
            }
        }

        static void ConvOvf(Compiler compiler, var_types lclTyp)
        {
            ConvOvfCommon(compiler, lclTyp, uns: false);
        }

        static void ConvOvfCommon(Compiler compiler, var_types lclTyp, bool uns)
        {
            Conv(compiler, lclTyp, uns, ovfl: true);
        }

        static void ConvOvfUn(Compiler compiler, var_types lclTyp)
        {
            ConvOvfCommon(compiler, lclTyp, uns: true);
        }

        static void ConvUn(Compiler compiler, var_types lclTyp)
        {
            Conv(compiler, lclTyp, uns: true, ovfl: false);
        }

        static void Ldind(Compiler compiler, var_types lclTyp, int prefixFlags)
        {
            var op1 = compiler.impPopStack().val; // address to load from
            impBashVarAddrsToI(op1);

#if TARGET_64BIT
            if (op1.Type.ActualType == TYP_INT)
            {
                // Allow an upcast of op1 from a 32-bit Int into TYP_I_IMPL for x86 JIT compatibility
                op1 = compiler.gtNewCastNode(TYP_I_IMPL, op1, fromUnsigned: false, TYP_I_IMPL);
            }
#endif

            compiler.assertImp((op1.Type.ActualType == TYP_I_IMPL) || (op1.Type is TYP_BYREF), op1);

            var indir = compiler.gtNewIndir(lclTyp, op1, impPrefixFlagsToIndirFlags(prefixFlags));
            compiler.impPushOnStack(indir, new typeInfo());
        }

        static void EvalAppend(Compiler compiler, in EntryState stackState, GenTree op1)
        {
            if (stackState.esStackDepth > 0)
            {
                compiler.impEvalSideEffects();
            }
            assert(stackState.esStackDepth is 0);

            Append(compiler, op1);
        }

        static void Ldloca(Compiler compiler, int lclNum)
        {
            JITDUMP($" {lclNum}");

            if (compiler.compIsForInlining)
            {
                // Have we allocated a temp for this local?
                lclNum = compiler.impInlineFetchLocal(lclNum, "Inline ldloca(s) first use temp");

                assert(!compiler.lvaGetDesc(lclNum).lvNormalizeOnLoad);
                var op1 = compiler.gtNewLclVarAddrNode(TYP_BYREF, lclNum);
                PushAdrVar(compiler, op1);
            }
            else
            {
                lclNum += compiler.info.compArgsCount;
                compiler.assertImp(lclNum < compiler.info.compLocalsCount);
                AdrVar(compiler, lclNum);
            }
        }

        static void LocSt(Compiler compiler, BasicBlock block, var_types lclTyp, int lclNum, CORINFO_CLASS_HANDLE clsHnd)
        {
            if (compiler.compIsForInlining)
            {
                var inlineInfo = compiler.impInlineInfo;
                lclTyp = inlineInfo.lclVarInfo[lclNum + inlineInfo.argCnt].lclTypeInfo;

                // Have we allocated a temp for this local?
                lclNum = compiler.impInlineFetchLocal(lclNum, "Inline stloc first use temp");
                PopValue(compiler, block, lclTyp, lclNum, isLocal: true);
            }
            else
            {
                lclNum += compiler.info.compArgsCount;
                VarSt(compiler, block, lclTyp, lclNum, clsHnd, isLocal: true);
            }
        }

        static void MathOp2(Compiler compiler, genTreeOps oper)
        {
            MathOp2Flags(compiler, oper, ovfl: false, uns: false);
        }

        static void MathOp2Flags(Compiler compiler, genTreeOps oper, bool ovfl, bool uns)
        {
            // Pull two values and push back the result

            var op2 = compiler.impPopStack().val;
            var op1 = compiler.impPopStack().val;

            // Can't do arithmetic with references
            compiler.assertImp((op1.Type.ActualType is not TYP_REF) && (op2.Type.ActualType is not TYP_REF), op1, op2);

            // Change both to TYP_I_IMPL (impBashVarAddrsToI won't change if its a true byref, only if it is in the stack)
            impBashVarAddrsToI(op1);
            impBashVarAddrsToI(op2);

            var type = compiler.impGetByRefResultType(oper, uns, ref op1, ref op2);

            assert(!ovfl || !varTypeIsFloating(op1.Type));

            // Special case: "int + 0", "int - 0", "int * 1", "int / 1"

            if (op2.Oper is GT_CNS_INT)
            {
                if ((op2.IsIntegralConst(0) && (oper is GT_ADD or GT_SUB)) ||
                    (op2.IsIntegralConst(1) && (oper is GT_MUL or GT_DIV)))

                {
                    compiler.impPushOnStack(op1, new typeInfo());
                    return;
                }
            }

            var op = compiler.gtNewBinaryNode(oper, type, op1, op2);

            if (varTypeIsIntegral(op.Type) && op1.MayThrow(compiler))
            {
                // Special case: integer/long division may throw an exception
                op.Flags |= GTF_EXCEPT;
            }

            if (ovfl)
            {
                assert(oper is GT_ADD or GT_SUB or GT_MUL);

                if (uns)
                {
                    op.IsUnsigned = true;
                }
                op.Flags |= (GTF_EXCEPT | GTF_OVERFLOW);
            }

            // Fold result, if possible.
            compiler.impPushOnStack(compiler.gtFoldExpr(op), new typeInfo());
        }

        static void MathOp2Ovf(Compiler compiler, genTreeOps oper)
        {
            MathOp2Flags(compiler, oper, ovfl: true, uns: false);
        }

        static void MulOvf(Compiler compiler, bool uns)
        {
            MathOp2Ovf(compiler, GT_MUL);
        }

        static void Obj(Compiler compiler, in CORINFO_RESOLVED_TOKEN resolvedToken, int prefixFlags)
        {
            var lclTyp = compiler.TypeHandleToVarType(resolvedToken.hClass, out var layout);
            var tiRetVal = compiler.makeTypeInfo(resolvedToken.hClass);

            var op1 = compiler.impPopStack().val;
            compiler.assertImp((op1.Type.ActualType is TYP_I_IMPL) || (op1.Type is TYP_BYREF), op1);

            op1 = compiler.gtNewLoadValueNode(lclTyp, op1, layout, impPrefixFlagsToIndirFlags(prefixFlags));
            compiler.impPushOnStack(op1, tiRetVal);
        }

        static void PopValue(Compiler compiler, BasicBlock block, var_types lclTyp, int lclNum, bool isLocal)
        {
            // Pop the value being assigned
            var se = compiler.impPopStack();
            var op1 = se.val;
            var tiRetVal = se.seTypeInfo;

            // Note this will downcast TYP_I_IMPL into a 32-bit Int on 64 bit (for x86 JIT compatibility).
            op1 = compiler.impImplicitIorI4Cast(op1, lclTyp);
            op1 = compiler.impImplicitR4orR8Cast(op1, lclTyp);

            var actualTyp = lclTyp.ActualType;

            // We had better assign it a value of the correct type
            compiler.assertImp(
                (actualTyp == op1.Type.ActualType) ||
                (actualTyp is TYP_I_IMPL && ((op1.Oper is GT_LCL_ADDR) || (op1.Type is TYP_BYREF or TYP_REF))) ||
                ((op1.Type.ActualType is TYP_I_IMPL) && (lclTyp is TYP_BYREF)) ||
                (varTypeIsFloating(lclTyp) && varTypeIsFloating(op1.Type)) ||
                ((actualTyp == TYP_BYREF) && (op1.Type.ActualType is TYP_REF)),
                op1
            );

            // If op1 is "&var" then its type is the transient "*" and it can
            // be used either as BYREF or TYP_I_IMPL.
            if (actualTyp == TYP_I_IMPL)
            {
                impBashVarAddrsToI(op1);
            }

            // If this is a local and the local is a ref type, see if we can improve type information based on the value being assigned.
            if (isLocal && (lclTyp == TYP_REF))
            {
                ref var lvaDsc = ref compiler.lvaTable[lclNum];

                // We should have seen a stloc in our IL prescan.
                assert(lvaDsc.lvHasILStoreOp);

                // Is there just one place this local is defined?
                var isSingleDefLocal = lvaDsc.lvSingleDef;

                // Conservative check that there is just one
                // definition that reaches this store.
                var hasSingleReachingDef = (block.bbStackDepthOnEntry is 0);

                if (isSingleDefLocal && hasSingleReachingDef)
                {
                    compiler.lvaUpdateClass(lclNum, op1, tiRetVal.ClassHandleForObjRef);
                }

                // If we see a local being assigned the result of a GDV-inlineable
                // GetEnumerator call, keep track of both the local and the call.

                if (op1.Oper is GT_RET_EXPR)
                {
                    JITDUMP(".... checking for GDV returning IEnumerator<T>...\n");

                    var call = op1.AsRetExpr().InlineCandidate.AsCall();
                    var retCls = compiler.gtGetClassHandle(call, out var isExact, out var isNonNull);

                    if ((retCls == NO_CLASS_HANDLE) && call.IsGuardedDevirtualizationCandidate)
                    {
                        // Just check one of the GDV candidates (all should have the same original method handle)
                        var inlineInfo = call.GetGdvCandidateInfo(0);

                        CORINFO_SIG_INFO sig;
                        compiler.info.compCompHnd->getMethodSig(inlineInfo.originalMethodHandle, &sig);

                        retCls = sig.retTypeClass;
                    }

                    if ((retCls != NO_CLASS_HANDLE) && compiler.info.compCompHnd->isIntrinsicType(retCls))
                    {
                        byte* pNamespaceName;
                        var pClassName = compiler.info.compCompHnd->getClassNameFromMetadata(retCls, &pNamespaceName);

                        var namespaceNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pNamespaceName);
                        var classNameUtf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(pClassName);

                        if (namespaceNameUtf8.SequenceEqual("System.Collections.Generic"u8) && classNameUtf8.SequenceEqual("IEnumerator`1"u8))
                        {
                            JITDUMP($"V{lclNum:D2} value is IEnumerator<T> via GDV\n");
                            compiler.lvaTable[lclNum].lvIsEnumerator = true;

                            JITDUMP($"Flagging [{call.TreeId:D6}] for enumerator cloning via V{lclNum:D2}\n");
                            compiler.ImpEnumeratorGdvLocalMap[call] = lclNum;
                            compiler.Metrics.EnumeratorGDV++;
                        }
                    }
                }
            }

            // Filter out simple stores to itself

            if (op1.Oper is GT_LCL_VAR && lclNum == op1.AsLclVarCommon().LclNum)
            {
                if (compiler.opts.compDbgCode)
                {
                    op1 = compiler.gtNewNothingNode();
                    Append(compiler, op1, CHECK_SPILL_ALL);
                }
            }
            else
            {
                // Stores to pinned locals can have the implicit side effect of "unpinning", so we must spill
                // things that could depend on the pin. TODO-Bug: which can actually be anything, including
                // unpinned unaliased locals, not just side-effecting trees.

                if (compiler.lvaTable[lclNum].lvPinned)
                {
                    compiler.impSpillSideEffects(spillGlobEffects: false, CHECK_SPILL_ALL, "Spill before store to pinned local");
                }

                op1 = compiler.gtNewStoreLclVarNode(lclNum, op1);

                // TODO-ASG: delete this zero-diff quirk. Requires some forward substitution work.
                op1.Type = lclTyp;

                if (varTypeIsStruct(lclTyp))
                {
                    op1 = compiler.impStoreStruct(op1, CHECK_SPILL_ALL);
                }
                Append(compiler, op1, CHECK_SPILL_ALL);
            }
        }

        static void PushAdrVar(Compiler compiler, GenTree op1)
        {
            assert(op1.IsLclVarAddr);
            compiler.impPushOnStack(op1, new typeInfo(TYP_BYREF));
        }

        static void PushI4Con(Compiler compiler, int value)
        {
            JITDUMP($" {value}");
            var intCon = compiler.gtNewIconNode(TYP_INT, value);
            compiler.impPushOnStack(intCon, new typeInfo(TYP_INT));
        }

        static void ShOp2(Compiler compiler, genTreeOps oper)
        {
            var op2 = compiler.impPopStack().val;
            var op1 = compiler.impPopStack().val; // operand to be shifted

            impBashVarAddrsToI(op1);
            impBashVarAddrsToI(op2);

            var type = op1.Type.ActualType;
            var op = compiler.gtNewBinaryNode(oper, type, op1, op2);

            // Fold result, if possible.
            compiler.impPushOnStack(compiler.gtFoldExpr(op), new typeInfo());
        }

        static void Starg(Compiler compiler, BasicBlock block, var_types lclTyp, int lclNum, CORINFO_CLASS_HANDLE clsHnd)
        {
            JITDUMP($" {lclNum}");

            if (compiler.compIsForInlining)
            {
                var inlineInfo = compiler.impInlineInfo;
                var op1 = compiler.impInlineFetchArg(ref inlineInfo.inlArgInfo[lclNum], inlineInfo.lclVarInfo[lclNum]);

                noway_assert(op1.Oper is GT_LCL_VAR);
                lclNum = op1.AsLclVar().LclNum;
                VarStValid(compiler, block, lclTyp, lclNum, clsHnd, isLocal: false);
            }
            else
            {
                // account for possible hidden param
                lclNum = compiler.compMapILargNum(lclNum);
                compiler.assertImp(lclNum < compiler.info.compArgsCount);

                if (lclNum == compiler.info.compThisArg)
                {
                    lclNum = compiler.lvaArg0Var;
                }

                // We should have seen this arg write in the prescan
                assert(compiler.lvaTable[lclNum].lvHasILStoreOp);
                VarSt(compiler, block, lclTyp, lclNum, clsHnd, isLocal: false);
            }
        }

        static void Stind(Compiler compiler, var_types lclTyp, OPCODE opcode, int prefixFlags)
        {
            var op2 = compiler.impPopStack().val; // value to store
            StindValue(compiler, lclTyp, opcode, prefixFlags, op2);
        }

        static void StindValue(Compiler compiler, var_types lclTyp, OPCODE opcode, int prefixFlags, GenTree op2)
        {
            var op1 = compiler.impPopStack().val; // address to store to

            // you can indirect off of a TYP_I_IMPL (if we are in C) or a BYREF
            compiler.assertImp((op1.Type.ActualType is TYP_I_IMPL) || (op1.Type is TYP_BYREF), op1, op2);

            impBashVarAddrsToI(op1);
            impBashVarAddrsToI(op2);

            // Allow a downcast of op2 from TYP_I_IMPL into a 32-bit Int for x86 JIT compatibility.
            // Allow an upcast of op2 from a 32-bit Int into TYP_I_IMPL for x86 JIT compatibility.
            op2 = compiler.impImplicitIorI4Cast(op2, lclTyp);
            op2 = compiler.impImplicitR4orR8Cast(op2, lclTyp);

            if (opcode == CEE_STIND_REF)
            {
                // STIND_REF can be used to store TYP_INT, TYP_I_IMPL, TYP_REF, or TYP_BYREF
                compiler.assertImp(varTypeIsIntOrI(op2.Type) || varTypeIsGC(op2.Type), op1, op2);
                lclTyp = op2.Type.ActualType;
            }

#if DEBUG
            // Check target type.
            if ((op2.Type is TYP_BYREF) || (lclTyp is TYP_BYREF))
            {
                if (op2.Type is TYP_BYREF)
                {
                    compiler.assertImp(lclTyp is TYP_BYREF or TYP_I_IMPL, op1, op2);
                }
                else if (lclTyp is TYP_BYREF)
                {
                    compiler.assertImp((op2.Type is TYP_BYREF) || varTypeIsIntOrI(op2.Type), op1, op2);
                }
            }
            else
            {
                compiler.assertImp(
                    (op2.Type.ActualType == lclTyp.ActualType) ||
                    ((lclTyp is TYP_I_IMPL) && (op2.Type.ActualType is TYP_INT)) ||
                    (varTypeIsFloating(op2.Type) && varTypeIsFloating(lclTyp)),
                    op1, op2
                );
            }
#endif

            var storeInd = compiler.gtNewStoreIndNode(lclTyp, op1, op2, impPrefixFlagsToIndirFlags(prefixFlags));
            Append(compiler, storeInd, CHECK_SPILL_ALL);
        }

        static void SubOvf(Compiler compiler, bool uns)
        {
            MathOp2Flags(compiler, GT_SUB, ovfl: true, uns);
        }

        static bool TryArrLd(Compiler compiler, var_types lclTyp, typeInfo tiRetVal, CORINFO_CLASS_HANDLE ldelemClsHnd, bool isLdelema)
        {
            var op2 = compiler.impPopStack().val; // index
            var op1 = compiler.impPopStack().val; // array
            compiler.assertImp(op1.Type is TYP_REF, op1, op2);

            // Check for null pointer - in the inliner case we simply abort.
            if (compiler.compIsForInlining && op1.IsIntegralConst(0))
            {
                compiler.compInlineResult.NoteFatal(InlineObservation.CALLEE_HAS_NULL_FOR_LDELEM);
                return false;
            }

            // Mark the block as containing an index expression.

            if ((op1.Oper is GT_LCL_VAR) && (op2.Oper is GT_LCL_VAR or GT_CNS_INT or GT_ADD))
            {
                compiler.optMethodFlags |= OMF_HAS_ARRAYREF;
            }

            op1 = compiler.gtNewArrayIndexAddr(op1, op2, lclTyp, ldelemClsHnd);

            if (!isLdelema)
            {
                op1 = compiler.gtNewIndexIndir(op1.AsIndexAddr());
            }

            compiler.impPushOnStack(op1, tiRetVal);
            return true;
        }

        static bool TryCastClass(Compiler compiler, int opcodeOffs, ref CORINFO_RESOLVED_TOKEN resolvedToken, GenTree op1, GenTree? op2)
        {
            var optTree = compiler.impOptimizeCastClassOrIsInst(op1, resolvedToken, true);

            if (optTree is not null)
            {
                compiler.impPushOnStack(optTree, new typeInfo());
            }
            else
            {
                var usingReadyToRunHelper = false;

#if FEATURE_READYTORUN
                if (compiler.IsAot)
                {
                    var opLookup = compiler.impReadyToRunHelperToTree(resolvedToken, CORINFO_HELP_READYTORUN_CHKCAST, TYP_REF, op1);

                    if (opLookup is not null)
                    {
                        usingReadyToRunHelper = true;
                        op1 = opLookup;
                    }

                    if (!usingReadyToRunHelper)
                    {
                        // TODO: ReadyToRun: When generic dictionary lookups are necessary, replace the lookup call
                        // and the chkcastany call with a single call to a dynamic R2R cell that will:
                        //      1) Load the context
                        //      2) Perform the generic dictionary lookup and caching, and generate the appropriate
                        //      stub
                        //      3) Check the object on the stack for the type-cast
                        // Reason: performance (today, we'll always use the slow helper for the R2R generics case)

                        op2 = compiler.impTokenToHandle(resolvedToken);

                        if (op2 is null)
                        {
                            return false;
                        }
                    }
                }

                if (!usingReadyToRunHelper)
#endif
                {
                    assert(op2 is not null);

                    var booleanCheck = false;
                    op1 = compiler.impCastClassOrIsInstToTree(op1, op2, ref resolvedToken, true, ref booleanCheck, opcodeOffs);
                }

                if (compiler.compDonotInline)
                {
                    return false;
                }

                // Push the result back on the stack
                compiler.impPushOnStack(op1, new typeInfo());
            }
            return true;
        }

        static bool TryDoLdftn(Compiler compiler, int prefixFlags, in CORINFO_RESOLVED_TOKEN resolvedToken, in CORINFO_RESOLVED_TOKEN constrainedResolvedToken, in CORINFO_CALL_INFO callInfo)
        {
            var op1 = compiler.impMethodPointer(callInfo);

            if (compiler.compDonotInline)
            {
                return false;
            }

            // Call info may have more precise information about the function than the resolved token.
            var constrainedToken = ((prefixFlags & PREFIX_CONSTRAINED) is not 0) ? constrainedResolvedToken.token : 0;
            var heapToken = compiler.impAllocateMethodPointerInfo(resolvedToken, constrainedToken);

            assert(callInfo.hMethod is not null);
            heapToken._token.hMethod = callInfo.hMethod;

            compiler.impPushOnStack(op1, new typeInfo(heapToken));
            return true;
        }

        static bool TryLdarga(Compiler compiler, int lclNum)
        {
            JITDUMP($" {lclNum}");

            if (lclNum >= compiler.info.compILargsCount)
            {
                BADCODE("Bad IL");
            }

            if (compiler.compIsForInlining)
            {
                var inlineInfo = compiler.impInlineInfo;

                // In IL, LDARGA(_S) is used to load the byref managed pointer of struct argument,
                // followed by a ldfld to load the field.

                var op1 = compiler.impInlineFetchArg(ref inlineInfo.inlArgInfo[lclNum], inlineInfo.lclVarInfo[lclNum]);

                if (op1.Oper is not GT_LCL_VAR)
                {
                    compiler.compInlineResult.NoteFatal(InlineObservation.CALLSITE_LDARGA_NOT_LOCAL_VAR);
                    return false;
                }

                op1 = compiler.gtNewLclAddrNode(TYP_BYREF, op1.AsLclVar().LclNum, lclOffs: 0);
                PushAdrVar(compiler, op1);
            }
            else
            {
                // account for possible hidden param
                lclNum = compiler.compMapILargNum(lclNum);
                compiler.assertImp(lclNum < compiler.info.compArgsCount);

                if (lclNum == compiler.info.compThisArg)
                {
                    lclNum = compiler.lvaArg0Var;
                }

                AdrVar(compiler, lclNum);
            }
            return true;
        }

        static bool TryLeave(Compiler compiler, BasicBlock block, int jmpAddr)
        {
            if (compiler.compIsForInlining && !compiler.opts.compInlineMethodsWithEH)
            {
                compiler.compInlineResult.NoteFatal(InlineObservation.CALLEE_HAS_LEAVE);
                return false;
            }

            JITDUMP($" {jmpAddr:X4}");

            if (block.Kind is not BBJ_LEAVE)
            {
                compiler.impResetLeaveBlock(block, jmpAddr);
            }
            assert(jmpAddr == block.Target.bbCodeOffs);

            compiler.impImportLeave(block);
            compiler.impNoteBranchOffs();

            return true;
        }

        static void VarSt(Compiler compiler, BasicBlock block, var_types lclTyp, int lclNum, CORINFO_CLASS_HANDLE clsHnd, bool isLocal)
        {
            if ((lclNum >= compiler.info.compLocalsCount) && (lclNum != compiler.lvaArg0Var))
            {
                BADCODE("Bad IL");
            }
            VarStValid(compiler, block, lclTyp, lclNum, clsHnd, isLocal);
        }

        static void VarStValid(Compiler compiler, BasicBlock block, var_types lclTyp, int lclNum, CORINFO_CLASS_HANDLE clsHnd, bool isLocal)
        {
            // if it is a struct store, make certain we don't overflow the buffer
            assert((lclTyp != TYP_STRUCT) || (compiler.lvaLclStackHomeSize(lclNum) >= compiler.info.compCompHnd->getClassSize(clsHnd)));

            ref var lvaDsc = ref compiler.lvaTable[lclNum];
            lclTyp = lvaDsc.Type;

            if (!lvaDsc.lvNormalizeOnLoad)
            {
                lclTyp = lclTyp.ActualType;
            }
            PopValue(compiler, block, lclTyp, lclNum, isLocal);
        }
    }

    /// <summary>ensure that block will be imported</summary>
    /// <param name="block">block that should be imported.</param>
    public void impImportBlockPending(BasicBlock block)
    {
        // Notes:
        //   Ensures that "block" is a member of the list of BBs waiting to be imported, pushing it on the list if
        //   necessary (and ensures that it is a member of the set of BB's on the list, by setting its byte in
        //   impPendingBlockMembers).  Does *NOT* change the existing "pre-state" of the block.
        //
        //   Merges the current verification state into the verification state of "block" (its "pre-state")./

        JITDUMP($"\nimpImportBlockPending for {FMT_BB(block.bbNum)}\n");

        // We will add a block to the pending set if it has not already been imported (or needs to be re-imported),
        // or if it has, but merging in a predecessor's post-state changes the block's pre-state.
        // (When we're doing verification, we always attempt the merge to detect verification errors.)

        // If the block has not been imported, add to pending set.
        var addToPending = !block.HasFlag(BBF_IMPORTED);

        // Initialize bbEntryState the first time we try to add this block to the pending list.
        // A null bbEntryState means that the block does not yet have a recorded pre-state,
        // which corresponds to having no established (i.e. empty) stack depth on entry.
        if ((block.EntryState.esStackDepth is 0) && !block.HasFlag(BBF_IMPORTED) && (impGetPendingBlockMember(block) is 0))
        {
            initBBEntryState(block, stackState);
            assert(addToPending);
            assert(impGetPendingBlockMember(block) is 0);
        }
        else
        {
            // The stack should have the same height on entry to the block from all its predecessors.
            if (block.bbStackDepthOnEntry != stackState.esStackDepth)
            {
#if DEBUG
                NO_WAY($"Block at offset {block.bbCodeOffs:X} to {block.bbCodeOffsEnd:X} in {info.compFullName} entered with different stack depths.\nPrevious depth was {block.bbStackDepthOnEntry}, current depth is {stackState.esStackDepth}");
#else
                NO_WAY("Block entered with different stack depths");
#endif
            }

            if (!addToPending)
            {
                return;
            }

            if (block.bbStackDepthOnEntry > 0)
            {
                // We need to fix the types of any spill temps that might have changed:
                //   int->native int, float->double, int->byref, etc.
                impRetypeEntryStateTemps(block);
            }

            // OK, we must add to the pending list, if it's not already in it.
            if (impGetPendingBlockMember(block) is not 0)
            {
                return;
            }
        }

        // Get an entry to add to the pending list

        var dsc = null as PendingDsc;

        if (impPendingFree is not null)
        {
            // We can reuse one of the freed up dscs.
            dsc = impPendingFree;
            impPendingFree = dsc.pdNext;
            dsc.pdBB = block;
        }
        else
        {
            // We have to create a new dsc
            dsc = new PendingDsc(block);
        }

        // Save the stack trees for later
        impSaveStackState(out dsc.pdSavedStack, false);

        // Add the entry to the pending list

        dsc.pdNext = impPendingList;
        impPendingList = dsc;
        impSetPendingBlockMember(block, 1); // And indicate that it's now a member of the set.

        // Various assertions require us to now to consider the block as not imported (at least for
        // the final time...)
        block.RemoveFlags(BBF_IMPORTED);

#if DEBUG
        if (false && verbose)
        {
            jitprintf($"Added PendingDsc - {dsc.GetHashCode():X8} for {FMT_BB(block.bbNum)}\n");
        }
#endif
    }

    public GenTreeCall impImportIndirectCall(in CORINFO_SIG_INFO sig, in DebugInfo di = default)
    {
        var callRetTyp = sig.retType.VarType;

        // The function pointer is on top of the stack - It may be a
        // complex expression. As it is evaluated after the args,
        // it may cause registered args to be spilled. Simply spill it.

        // Ignore no args or trivial cases.
        if ((sig.callConv is not CORINFO_CALLCONV_DEFAULT || sig.totalILArgs() > 0) && (impStackTop().val.Oper is not GT_LCL_VAR and not GT_FTN_ADDR and not GT_CNS_INT))
        {
            impSpillStackEntry(stackState.esStackDepth - 1, BAD_VAR_NUM, assertOnRecursion: false, "impImportIndirectCall");
        }

        // Get the function pointer
        var fptr = impPopStack().val;

        // The function pointer is typically a sized to match the target pointer size
        // However, stubgen IL optimization can change LDC.I8 to LDC.I4
        // See ILCodeStream.LowerOpcode
        assert(fptr.Type.ActualType is TYP_I_IMPL or TYP_INT);

#if DEBUG
        // This temporary must never be converted to a double in stress mode,
        // because that can introduce a call to the cast helper after the
        // arguments have already been evaluated.

        if (fptr.Oper is GT_LCL_VAR)
        {
            lvaGetDesc(fptr.AsLclVarCommon().LclNum).lvKeepType = true;
        }
#endif

        // Create the call node
        var call = gtNewIndCallNode(callRetTyp, fptr, di);

        call.Flags |= (GTF_EXCEPT | (fptr.Flags & GTF_GLOB_EFFECT));
#if UNIX_X86_ABI
        call.Flags &= ~GTF_CALL_POP_ARGS;
#endif

        return call;
    }

#if DEBUG
    public var_types impImportJitTestLabelMark(int numArgs)
    {
        TestLabelAndNum tlAndN;

        if (numArgs is 2)
        {
            tlAndN._num = 0;

            var se = impPopStack();
            var val = se.val;

            assert(val.Oper.IsCnsIntOrI);
            tlAndN._tl = (TestLabel)val.AsIntConCommon().IconValue;
        }
        else if (numArgs is 3)
        {
            var se = impPopStack();
            var val = se.val;

            assert(val.Oper.IsCnsIntOrI);
            tlAndN._num = val.AsIntConCommon().IconValue;

            se = impPopStack();
            val = se.val;

            assert(val.Oper.IsCnsIntOrI);
            tlAndN._tl = (TestLabel)val.AsIntConCommon().IconValue;
        }
        else
        {
            Unsafe.SkipInit(out tlAndN);
            unreached();
        }

        var expSe = impPopStack();
        var node = expSe.val;

        // There are a small number of special cases, where we actually put the annotation on a subnode.
        var nodeTestData = NodeTestData;

        if ((tlAndN._tl == TL_LoopHoist) && (tlAndN._num >= 100))
        {
            // A loop hoist annotation with value >= 100 means that the expression should be a static field access,
            // a GT_IND of a static field address, which should be the sum of a (hoistable) helper call and possibly some
            // offset within the static field block whose address is returned by the helper call.
            // The annotation is saying that this address calculation, but not the entire access, should be hoisted.
            assert(node.Oper is GT_IND);
            tlAndN._num -= 100;

            nodeTestData[node.AsOp().Op1] = tlAndN;
            _ = nodeTestData.Remove(node);
        }
        else
        {
            nodeTestData[node] = tlAndN;
        }

        impPushOnStack(node, expSe.seTypeInfo);
        return node.Type;
    }
#endif

    public unsafe GenTree? impImportLdvirtftn(GenTree thisPtr, in CORINFO_RESOLVED_TOKEN resolvedToken, in CORINFO_CALL_INFO callInfo)
    {
        var isInterface = (callInfo.classFlags & CORINFO_FLG_INTERFACE) is CORINFO_FLG_INTERFACE;

        if (((callInfo.methodFlags & CORINFO_FLG_EnC) != 0) && !isInterface)
        {
            NO_WAY("Virtual call to a function added via EnC is not supported");
        }

        var call = null as GenTreeCall;

        // NativeAOT generic virtual method
        if ((callInfo.sig.sigInst.methInstCount is not 0) && IsTargetAbi(CORINFO_NATIVEAOT_ABI))
        {
            var runtimeMethodHandle = impLookupToTree(callInfo.codePointerLookup, GTF_ICON_METHOD_HDL, callInfo.hMethod);
            assert(runtimeMethodHandle is not null);
            call = gtNewVirtualFunctionLookupHelperCallNode(TYP_I_IMPL, CORINFO_HELP_GVMLOOKUP_FOR_SLOT, thisPtr, runtimeMethodHandle);
        }

#if FEATURE_READYTORUN
        else if (IsAot)
        {
            if (!callInfo.exactContextNeedsRuntimeLookup)
            {
                call = gtNewHelperCallNode(TYP_I_IMPL, CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR, thisPtr);
                call._entryPoint = callInfo.codePointerLookup.constLookup;
            }
            // We need a runtime lookup. NativeAOT has a ReadyToRun helper for that too.
            else if (IsTargetAbi(CORINFO_NATIVEAOT_ABI))
            {
                var ctxTree = getRuntimeContextTree(callInfo.codePointerLookup.lookupKind.runtimeLookupKind);
                assert(callInfo.codePointerLookup.runtimeLookup.indirections == CORINFO_USEHELPER);
                call = gtNewRuntimeLookupHelperCallNode(callInfo.codePointerLookup.runtimeLookup, ctxTree, null);
            }
        }
#endif

        if (call is null)
        {
            // Get the exact descriptor for the static callsite
            var exactTypeDesc = impParentClassTokenToHandle(resolvedToken);

            if (exactTypeDesc is null)
            {
                assert(compIsForInlining);
                return null;
            }

            var exactMethodDesc = impTokenToHandle(resolvedToken);

            if (exactMethodDesc is null)
            {
                assert(compIsForInlining);
                return null;
            }

            // Call helper function.  This gets the target address of the final destination callsite.
            //
            call = gtNewVirtualFunctionLookupHelperCallNode(TYP_I_IMPL, CORINFO_HELP_VIRTUAL_FUNC_PTR, thisPtr, exactMethodDesc, exactTypeDesc);
        }

        assert(call is not null);

        if (isInterface)
        {
            // Annotate helper so later on if helper result is unconsumed we know it is not sound
            // to optimize the call into a null check.
            //
            call._callMoreFlags |= GTF_CALL_M_LDVIRTFTN_INTERFACE;
        }
        return call;
    }

    /// <summary>canonicalize flow when leaving a protected region</summary>
    /// <param name="block">block with BBJ_LEAVE jump kind to canonicalize</param>
    /// <remarks>
    ///   <para>CEE_LEAVE may be jumping out of a protected block, viz, a catch or a finally-protected try. We find the finally blocks protecting the current offset (in order) by walking over the complete exception table and finding enclosing clauses. This assumes that the table is sorted. This will create a series of BBJ_CALLFINALLY/BBJ_CALLFINALLYRET -> BBJ_CALLFINALLY/BBJ_CALLFINALLYRET ... -> BBJ_ALWAYS.</para>
    ///   <para>If we are leaving a catch handler, we need to attach the ENDCATCHes to the correct BBJ_CALLFINALLY blocks.</para>
    ///   <para>After this function, the BBJ_LEAVE block has been converted to a different type.</para>
    /// </remarks>
    public void impImportLeave(BasicBlock block)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf($"\nBefore import CEE_LEAVE in {FMT_BB(block.bbNum)} (targeting {FMT_BB(block.Target.bbNum)}):\n");
            fgDispBasicBlocks();
            fgDispHandlerTab();
        }
#endif

        var blkAddr = block.bbCodeOffs;
        var leaveTarget = block.Target;
        var jmpAddr = leaveTarget.bbCodeOffs;

        // LEAVE clears the stack, spill side effects, and set stack to 0

        impSpillSideEffects(true, CHECK_SPILL_ALL, ("impImportLeave"));
        stackState.esStackDepth = 0;

        assert(block.Kind is BBJ_LEAVE);
        assert((fgBBs.Length is 0) || (fgLookupBB(jmpAddr) is not null)); // should be a BB boundary

        var step = null as BasicBlock;
        var stepType = ST_None;

        for (ushort XTnum = 0; XTnum < compHndBBtabCount; XTnum++)
        {
            ref var HBtab = ref ehGetDsc(XTnum);

            // Grab the handler offsets

            var tryBeg = HBtab.ebdTryBegOffs;
            var tryEnd = HBtab.ebdTryEndOffs;
            var hndBeg = HBtab.ebdHndBegOffs;
            var hndEnd = HBtab.ebdHndEndOffs;

            // Is this a catch-handler we are CEE_LEAVE'ing out of?

            if (jitIsBetween(blkAddr, hndBeg, hndEnd) && !jitIsBetween(jmpAddr, hndBeg, hndEnd))
            {
                // Can't CEE_LEAVE out of a finally/fault handler
                if (HBtab.HasFinallyOrFaultHandler)
                {
                    BADCODE("leave out of fault/finally block");
                }

                // We are jumping out of a catch.

                if (step is null)
                {
                    step = block;
                    step.Kind = BBJ_EHCATCHRET; // convert the BBJ_LEAVE to BBJ_EHCATCHRET
                    stepType = ST_Catch;

#if DEBUG
                    if (verbose)
                    {
                        jitprintf($"impImportLeave - jumping out of a catch (EH#{XTnum}), convert block {FMT_BB(step.bbNum)} to BBJ_EHCATCHRET block\n");
                    }
#endif
                }
                else
                {
                    // Create a new catch exit block in the catch region for the existing step block to jump to in this scope.
                    // Note: we don't know the jump target yet
                    var exitBlock = fgNewBBinRegion(BBJ_EHCATCHRET, tryIndex: 0, hndIndex: (ushort)(XTnum + 1), step);

                    assert(step.Kind is BBJ_ALWAYS or BBJ_CALLFINALLYRET or BBJ_EHCATCHRET);
                    assert((step == block) || !step.HasInitializedTarget);

                    if (step == block)
                    {
                        fgRedirectEdge(ref step.TargetEdgeRef, exitBlock);
                    }
                    else
                    {
                        // the previous step (maybe a call to a nested finally, or a nested catch exit) returns to this block
                        var newEdge = fgAddRefPred(exitBlock, step);
                        step.TargetEdge = newEdge;
                    }

                    // The new block will inherit this block's weight.
                    exitBlock.inheritWeight(block);
                    exitBlock.SetFlags(BBF_IMPORTED);

                    // This exit block is the new step.
                    step = exitBlock;
                    stepType = ST_Catch;

#if DEBUG
                    if (verbose)
                    {
                        jitprintf($"impImportLeave - jumping out of a catch (EH#{XTnum}), new BBJ_EHCATCHRET block {FMT_BB(exitBlock.bbNum)}\n");
                    }
#endif
                }
            }
            else if (HBtab.HasFinallyHandler && jitIsBetween(blkAddr, tryBeg, tryEnd) && !jitIsBetween(jmpAddr, tryBeg, tryEnd))
            {
                // We are jumping out of a finally-protected try.

                var callBlock = null as BasicBlock;

                if (step is null)
                {
                    // Put the call to the finally in the enclosing region.
                    var callFinallyTryIndex = (HBtab.ebdEnclosingTryIndex is EHblkDsc.NO_ENCLOSING_INDEX) ? (ushort)(0) : (ushort)(HBtab.ebdEnclosingTryIndex + 1);
                    var callFinallyHndIndex = (HBtab.ebdEnclosingHndIndex is EHblkDsc.NO_ENCLOSING_INDEX) ? (ushort)(0) : (ushort)(HBtab.ebdEnclosingHndIndex + 1);

                    callBlock = fgNewBBinRegion(BBJ_CALLFINALLY, callFinallyTryIndex, callFinallyHndIndex, block);

                    // Convert the BBJ_LEAVE to BBJ_ALWAYS, jumping to the new BBJ_CALLFINALLY. This is because
                    // the new BBJ_CALLFINALLY is in a different EH region, thus it can't just replace the BBJ_LEAVE,
                    // which might be in the middle of the "try". In most cases, the BBJ_ALWAYS will jump to the
                    // next block, and flow optimizations will remove it.
                    fgRedirectEdge(ref block.TargetEdgeRef, callBlock);
                    block.Kind = BBJ_ALWAYS;

                    // The new block will inherit this block's weight.
                    callBlock.inheritWeight(block);
                    callBlock.SetFlags(BBF_IMPORTED);

                    // callBlock calls the finally handler
                    var newEdge = fgAddRefPred(HBtab.ebdHndBeg, callBlock);
                    callBlock.SetKindAndTargetEdge(BBJ_CALLFINALLY, newEdge);

#if DEBUG
                    if (verbose)
                    {
                        jitprintf($"impImportLeave - jumping out of a finally-protected try (EH#{XTnum}), convert block {FMT_BB(block.bbNum)} to BBJ_ALWAYS, add BBJ_CALLFINALLY block {FMT_BB(callBlock.bbNum)}\n");
                    }
#endif
                }
                else
                {
                    // Calling the finally block. We already have a step block that is either the call-to-finally from a
                    // more nested try/finally (thus we are jumping out of multiple nested 'try' blocks, each protected by
                    // a 'finally'), or the step block is the return from a catch.
                    //
                    // Due to ThreadAbortException, we can't have the catch return target the call-to-finally block
                    // directly. Note that if a 'catch' ends without resetting the ThreadAbortException, the VM will
                    // automatically re-raise the exception, using the return address of the catch (that is, the target
                    // block of the BBJ_EHCATCHRET) as the re-raise address. If this address is in a finally, the VM will
                    // refuse to do the re-raise, and the ThreadAbortException will get eaten (and lost). On AMD64/ARM64,
                    // we put the call-to-finally thunk in a special "cloned finally" EH region that does look like a
                    // finally clause to the VM. Thus, on these platforms, we can't have BBJ_EHCATCHRET target a
                    // BBJ_CALLFINALLY directly. (Note that on ARM32, we don't mark the thunk specially -- it lives directly
                    // within the 'try' region protected by the finally, since we generate code in such a way that execution
                    // never returns to the call-to-finally call, and the finally-protected 'try' region doesn't appear on
                    // stack walks.)

                    assert(step.Kind is BBJ_ALWAYS or BBJ_CALLFINALLYRET or BBJ_EHCATCHRET);
                    assert((step == block) || !step.HasInitializedTarget);

                    if (step.Kind is BBJ_EHCATCHRET)
                    {
                        // Need to create another step block in the 'try' region that will actually branch to the call-to-finally thunk.
                        // Note: we don't know the jump target yet
                        var step2 = fgNewBBinRegion(BBJ_ALWAYS, tryIndex: (ushort)(XTnum + 1), hndIndex: 0, step);

                        if (step == block)
                        {
                            fgRedirectEdge(ref step.TargetEdgeRef, step2);
                        }
                        else
                        {
                            var newEdge = fgAddRefPred(step2, step);
                            step.TargetEdge = newEdge;
                        }

                        step2.inheritWeight(block);
                        step2.SetFlags(BBF_IMPORTED);

#if DEBUG
                        if (verbose)
                        {
                            jitprintf($"impImportLeave - jumping out of a finally-protected try (EH#{XTnum}), step block is BBJ_EHCATCHRET ({FMT_BB(step.bbNum)}), new BBJ_ALWAYS step-step block {FMT_BB(step2.bbNum)}\n");
                        }
#endif

                        step = step2;
                        assert(stepType == ST_Catch); // Leave it as catch type for now.
                    }

                    var callFinallyTryIndex = (HBtab.ebdEnclosingTryIndex is EHblkDsc.NO_ENCLOSING_INDEX) ? (ushort)(0) : (ushort)(HBtab.ebdEnclosingTryIndex + 1);
                    var callFinallyHndIndex = (HBtab.ebdEnclosingHndIndex is EHblkDsc.NO_ENCLOSING_INDEX) ? (ushort)(0) : (ushort)(HBtab.ebdEnclosingHndIndex + 1);

                    assert(step.Kind is BBJ_ALWAYS or BBJ_CALLFINALLYRET or BBJ_EHCATCHRET);
                    assert((step == block) || !step.HasInitializedTarget);

                    // callBlock will call the finally handler
                    callBlock = fgNewBBinRegion(BBJ_CALLFINALLY, callFinallyTryIndex, callFinallyHndIndex, step);

                    if (step == block)
                    {
                        fgRedirectEdge(ref step.TargetEdgeRef, callBlock);
                    }
                    else
                    {
                        // the previous call to a finally returns to this call (to the next finally in the chain)
                        var newEdge = fgAddRefPred(callBlock, step);
                        step.TargetEdge = newEdge;
                    }

                    // The new block will inherit this block's weight.
                    callBlock.inheritWeight(block);
                    callBlock.SetFlags(BBF_IMPORTED);

                    {
                        // callBlock calls the finally handler
                        var newEdge = fgAddRefPred(HBtab.ebdHndBeg, callBlock);
                        callBlock.SetKindAndTargetEdge(BBJ_CALLFINALLY, newEdge);
                    }

#if DEBUG
                    if (verbose)
                    {
                        jitprintf($"impImportLeave - jumping out of a finally-protected try (EH#{XTnum}), new BBJ_CALLFINALLY block {FMT_BB(callBlock.bbNum)}\n");
                    }
#endif
                }

                // callBlock should be set up at this point
                assert(callBlock.Target == HBtab.ebdHndBeg);

                // Note: we don't know the jump target yet
                step = fgNewBBafter(BBJ_CALLFINALLYRET, callBlock, true);
                stepType = ST_FinallyReturn;

                // The new block will inherit this block's weight.
                step.inheritWeight(block);
                step.SetFlags(BBF_IMPORTED);

#if DEBUG
                if (verbose)
                {
                    jitprintf($"impImportLeave - jumping out of a finally-protected try (EH#{XTnum}), created step (BBJ_CALLFINALLYRET) block {FMT_BB(step.bbNum)}\n");
                }
#endif
            }
            else if (HBtab.HasCatchHandler && jitIsBetween(blkAddr, tryBeg, tryEnd) && !jitIsBetween(jmpAddr, tryBeg, tryEnd))
            {
                // We are jumping out of a catch-protected try.
                //
                // If we are returning from a call to a finally, then we must have a step block within a try
                // that is protected by a catch. This is so when unwinding from that finally (e.g., if code within the
                // finally raises an exception), the VM will find this step block, notice that it is in a protected region,
                // and invoke the appropriate catch.
                //
                // We also need to handle a special case with the handling of ThreadAbortException. If a try/catch
                // catches a ThreadAbortException (which might be because it catches a parent, e.g. System.Exception),
                // and the catch doesn't call System.Threading.Thread.ResetAbort(), then when the catch returns to the VM,
                // the VM will automatically re-raise the ThreadAbortException. When it does this, it uses the target
                // address of the catch return as the new exception address. That is, the re-raised exception appears to
                // occur at the catch return address. If this exception return address skips an enclosing try/catch that
                // catches ThreadAbortException, then the enclosing try/catch will not catch the exception, as it should.
                // For example:
                //
                // try {
                //    try {
                //       // something here raises ThreadAbortException
                //       LEAVE LABEL_1; // no need to stop at LABEL_2
                //    } catch (Exception) {
                //       // This catches ThreadAbortException, but doesn't call System.Threading.Thread.ResetAbort(), so
                //       // ThreadAbortException is re-raised by the VM at the address specified by the LEAVE opcode.
                //       // This is bad, since it means the outer try/catch won't get a chance to catch the re-raised
                //       // ThreadAbortException. So, instead, create step block LABEL_2 and LEAVE to that. We only
                //       // need to do this transformation if the current EH block is a try/catch that catches
                //       // ThreadAbortException (or one of its parents), however we might not be able to find that
                //       // information, so currently we do it for all catch types.
                //       LEAVE LABEL_1; // Convert this to LEAVE LABEL2;
                //    }
                //    LABEL_2: LEAVE LABEL_1; // inserted by this step creation code
                // } catch (ThreadAbortException) {
                // }
                // LABEL_1:
                //
                // Note that this pattern isn't theoretical: it occurs in ASP.NET, in IL code generated by the Roslyn C#
                // compiler.

                if (stepType is ST_FinallyReturn or ST_Catch)
                {
                    assert(step is not null);
                    assert((step == block) || !step.HasInitializedTarget);

                    if (stepType == ST_FinallyReturn)
                    {
                        assert(step.Kind is BBJ_CALLFINALLYRET);
                    }
                    else
                    {
                        assert(stepType == ST_Catch);
                        assert(step.Kind is BBJ_EHCATCHRET);
                    }

                    // Create a new exit block in the try region for the existing step block to jump to in this scope.
                    // Note: we don't know the jump target yet
                    var catchStep = fgNewBBinRegion(BBJ_ALWAYS, tryIndex: (ushort)(XTnum + 1), hndIndex: 0, step);

                    if (step == block)
                    {
                        fgRedirectEdge(ref step.TargetEdgeRef, catchStep);
                    }
                    else
                    {
                        var newEdge = fgAddRefPred(catchStep, step);
                        step.TargetEdge = newEdge;
                    }

                    // The new block will inherit this block's weight.
                    catchStep.inheritWeight(block);
                    catchStep.SetFlags(BBF_IMPORTED);

#if DEBUG
                    if (verbose)
                    {
                        if (stepType is ST_FinallyReturn)
                        {
                            jitprintf($"impImportLeave - return from finally jumping out of a catch-protected try (EH#{XTnum}), new BBJ_ALWAYS block {FMT_BB(catchStep.bbNum)}\n");
                        }
                        else
                        {
                            assert(stepType is ST_Catch);
                            jitprintf($"impImportLeave - return from catch jumping out of a catch-protected try (EH#{XTnum}), new BBJ_ALWAYS block {FMT_BB(catchStep.bbNum)}\n");
                        }
                    }
#endif

                    // This block is the new step.
                    step = catchStep;
                    stepType = ST_Try;
                }
            }
        }

        if (step is null)
        {
            // convert the BBJ_LEAVE to a BBJ_ALWAYS
            block.Kind = BBJ_ALWAYS;

#if DEBUG
            if (verbose)
            {
                jitprintf($"impImportLeave - no enclosing finally-protected try blocks or catch handlers; convert CEE_LEAVE block {FMT_BB(block.bbNum)} to BBJ_ALWAYS\n");
            }
#endif
        }
        else
        {
            assert((step == block) || !step.HasInitializedTarget);

            // leaveTarget is the ultimate destination of the LEAVE
            if (step == block)
            {
                fgRedirectEdge(ref step.TargetEdgeRef, leaveTarget);
            }
            else
            {
                var newEdge = fgAddRefPred(leaveTarget, step);
                step.TargetEdge = newEdge;
            }

#if DEBUG
            if (verbose)
            {
                jitprintf($"impImportLeave - final destination of step blocks set to {FMT_BB(leaveTarget.bbNum)}\n");
            }
#endif

            // Queue up the jump target for importing

            impImportBlockPending(leaveTarget);
        }

#if DEBUG
        fgVerifyHandlerTab();

        if (verbose)
        {
            jitprintf("\nAfter import CEE_LEAVE:\n");
            fgDispBasicBlocks();
            fgDispHandlerTab();
        }
#endif
    }

    /// <summary>Build and import `new` of multi-dimensional array</summary>
    /// <param name="resolvedToken">The CORINFO_RESOLVED_TOKEN that has been initialized by a call to CEEInfo.resolveToken().</param>
    /// <param name="callInfo">The CORINFO_CALL_INFO that has been initialized by a call to CEEInfo.getCallInfo().</param>
    /// <remarks>
    ///   <para>This methods assumes the multi-dimensional array constructor arguments (array dimensions) are pushed on the IL stack on entry to this method.</para>
    ///   <para>Multi-dimensional array constructors are imported as calls to a JIT helper, not as regular calls.</para>
    /// </remarks>
    public unsafe void impImportNewObjArray(in CORINFO_RESOLVED_TOKEN resolvedToken, in CORINFO_CALL_INFO callInfo)
    {
        var classHandle = impParentClassTokenToHandle(resolvedToken);

        if (classHandle is null)
        {
            return;
        }

        var numArgs = callInfo.sig.numArgs;
        assert(numArgs is not 0);

        var dimensionsSize = numArgs * sizeof(int);

        // Reuse the temp used to pass the array dimensions to avoid bloating
        // the stack frame in case there are multiple calls to multi-dim array
        // constructors within a single method.
        if (lvaNewObjArrayArgs is BAD_VAR_NUM)
        {
            lvaNewObjArrayArgs = lvaGrabTemp(shortLifetime: false, "NewObjArrayArgs");
            lvaSetStruct(lvaNewObjArrayArgs, typGetBlkLayout(dimensionsSize), unsafeValueClsCheck: false);
        }

        // Increase size of lvaNewObjArrayArgs to be the largest size needed to hold 'numArgs' integers for our call to CORINFO_HELP_NEW_MDARR.
        ref var lvaDsc = ref lvaTable[lvaNewObjArrayArgs];

        if (dimensionsSize > lvaDsc.lvExactSize)
        {
            lvaDsc.GrowBlockLayout(typGetBlkLayout(dimensionsSize));
        }

        // The side-effects may include allocation of more multi-dimensional arrays. Spill all side-effects
        // to ensure that the shared lvaNewObjArrayArgs local variable is only ever used to pass arguments
        // to one allocation at a time.
        impSpillSideEffects(spillGlobEffects: true, CHECK_SPILL_ALL, "impImportNewObjArray");

        //
        // The arguments of the CORINFO_HELP_NEW_MDARR helper are:
        //  - Array class handle
        //  - Number of dimension arguments
        //  - Pointer to block of int32 dimensions: address of lvaNewObjArrayArgs temp.
        //

        GenTree node = gtNewLclVarAddrNode(TYP_I_IMPL, lvaNewObjArrayArgs);

        // Pop dimension arguments from the stack one at a time and store it into lvaNewObjArrayArgs temp.

        for (var i = numArgs - 1; i >= 0; i--)
        {
            var arg = impImplicitIorI4Cast(impPopStack().val, TYP_INT);
            var store = gtNewStoreLclFldNode(TYP_INT, lvaNewObjArrayArgs, (ushort)(i * sizeof(int)), arg);
            node = gtNewCommaNode(node.Type, store, node);
        }

        var helper = info.compCompHnd->getArrayRank(resolvedToken.hClass) is 1 ? CORINFO_HELP_NEW_MDARR_RARE : CORINFO_HELP_NEW_MDARR;

        node = gtNewHelperCallNode(TYP_REF, helper, classHandle, gtNewIconNode(TYP_INT, numArgs), node);

        node.AsCall()._compileTimeHelperArgumentHandle = (CORINFO_GENERIC_HANDLE)(resolvedToken.hClass);

        // Remember that this function contains 'new' of a MD array.
        optMethodFlags |= OMF_HAS_MDNEWARRAY;

        impPushOnStack(node, new typeInfo(resolvedToken.hClass));
    }

    public GenTree impImportStaticFieldAddress(in CORINFO_RESOLVED_TOKEN resolvedToken, CORINFO_ACCESS_FLAGS access, in CORINFO_FIELD_INFO fieldInfo, var_types lclTyp, ref GenTreeFlags indirFlags)
    {
        return impImportStaticFieldAddress(resolvedToken, access, fieldInfo, lclTyp, ref indirFlags, out _);
    }

    /// <summary>Generate an address of a static field</summary>
    /// <param name="resolvedToken">resolved token for the static field to access</param>
    /// <param name="access">type of access to the field, distinguishes address vs load/store</param>
    /// <param name="fieldInfo">EE instructions for accessing the field</param>
    /// <param name="lclTyp">type of the field</param>
    /// <param name="indirFlags">the field indirection flags (e. g. IND_INITCLASS)</param>
    /// <param name="isHoistable">hether any type initialization side effects of the returned tree can be hoisted to occur earlier</param>
    /// <returns>Tree representing the field's address.</returns>
    /// <remarks>Ordinary static fields never overlap. RVA statics, however, can overlap (if they're mapped to the same ".data" declaration). That said, such mappings only appear to be possible with ILASM, and in ILASM-produced (ILONLY) images, RVA statics are always read-only (using "stsfld" on them is UB). In mixed-mode assemblies, RVA statics can be mutable, but the only current producer of such images, the C++/CLI compiler, does not appear to support mapping different fields to the same address. So we will say that "mutable overlapping RVA statics" are UB as well.</remarks>
    public unsafe GenTree impImportStaticFieldAddress(in CORINFO_RESOLVED_TOKEN resolvedToken, CORINFO_ACCESS_FLAGS access, in CORINFO_FIELD_INFO fieldInfo, var_types lclTyp, ref GenTreeFlags indirFlags, out bool isHoistable)
    {
        // For statics that are not "boxed", the initial address tree will contain the field sequence.
        // For those that are, we will attach it later, when adding the indirection for the box, since
        // that tree will represent the true address.
        var isBoxedStatic = (fieldInfo.fieldFlags & CORINFO_FLG_FIELD_STATIC_IN_HEAP) is not 0;
        var isSharedStatic = fieldInfo.fieldAccessor is CORINFO_FIELD_STATIC_GENERICS_STATIC_HELPER or CORINFO_FIELD_STATIC_READYTORUN_HELPER;
        var fieldKind = isSharedStatic ? FieldSeq.FieldKind.SharedStatic : FieldSeq.FieldKind.SimpleStatic;

        var hasConstAddr = fieldInfo.fieldAccessor is CORINFO_FIELD_STATIC_ADDRESS or CORINFO_FIELD_STATIC_RVA_ADDRESS;

        FieldSeq? innerFldSeq;
        FieldSeq? outerFldSeq;

        if (isBoxedStatic)
        {
            innerFldSeq = null;
            outerFldSeq = FieldSeqStore.Create(resolvedToken.hField, TARGET_POINTER_SIZE, fieldKind);
        }
        else
        {
            nint offset;

            if (hasConstAddr)
            {
                // Change SimpleStatic to SimpleStaticKnownAddress
                assert(fieldKind is FieldSeq.FieldKind.SimpleStatic);
                fieldKind = FieldSeq.FieldKind.SimpleStaticKnownAddress;

                assert(fieldInfo.fieldLookup.accessType is IAT_VALUE);
                offset = unchecked((nint)(fieldInfo.fieldLookup.addr));
            }
            else
            {
                offset = fieldInfo.offset;
            }

            innerFldSeq = FieldSeqStore.Create(resolvedToken.hField, offset, fieldKind);
            outerFldSeq = null;
        }

        var typeIndex = 0;
        var additionalIndirFlags = GTF_EMPTY;
        var op1 = null as GenTree;

        isHoistable = false;

        switch (fieldInfo.fieldAccessor)
        {
            case CORINFO_FIELD_STATIC_GENERICS_STATIC_HELPER:
            {
                // We first call a special helper to get the statics base pointer
                op1 = impParentClassTokenToHandle(resolvedToken);

                // compIsForInlining() is false so we should not get NULL here
                assert(op1 is not null);
                var type = TYP_BYREF;

                switch (fieldInfo.helper)
                {
                    case CORINFO_HELP_GET_NONGCTHREADSTATIC_BASE:
                    case CORINFO_HELP_GET_GCSTATIC_BASE:
                    case CORINFO_HELP_GET_NONGCSTATIC_BASE:
                    case CORINFO_HELP_GET_GCTHREADSTATIC_BASE:
                    {
                        break;
                    }

                    default:
                    {
                        NO_WAY("unknown generic statics helper");
                        break;
                    }
                }

                isHoistable = !fieldInfo.helper.MayRunCctor || ((info.compCompHnd->getClassAttribs(resolvedToken.hClass) & CORINFO_FLG_BEFOREFIELDINIT) is not 0);
                op1 = gtNewHelperCallNode(type, fieldInfo.helper, op1);

                if (IsStaticHelperEligibleForExpansion(op1))
                {
                    // Mark the helper call with the initClsHnd so that rewriting it for expansion can reliably fail
                    op1.AsCall()._initClsHnd = resolvedToken.hClass;
                }

                op1 = gtNewBinaryNode(GT_ADD, type, op1, gtNewIconNode(fieldInfo.offset, innerFldSeq));
                break;
            }

            case CORINFO_FIELD_STATIC_TLS_MANAGED:
            {
#if FEATURE_READYTORUN
                if (!IsAot)
#endif // FEATURE_READYTORUN
                {
                    if (fieldInfo.helper is CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED
                                         or CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2
                                         or CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2_NOJITOPT)
                    {
                        typeIndex = info.compCompHnd->getThreadLocalFieldInfo(resolvedToken.hField, isGCType: false);
                    }
                    else
                    {
                        assert(fieldInfo.helper == CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED);
                        typeIndex = info.compCompHnd->getThreadLocalFieldInfo(resolvedToken.hField, isGCType: true);
                    }
                }
                goto case CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER;
            }

            case CORINFO_FIELD_STATIC_SHARED_STATIC_HELPER:
            {
#if FEATURE_READYTORUN
                if (IsAot)
                {
                    var callFlags = GTF_EMPTY;

                    if (!fieldInfo.helper.MayRunCctor || ((info.compCompHnd->getClassAttribs(resolvedToken.hClass) & CORINFO_FLG_BEFOREFIELDINIT) is not 0))
                    {
                        isHoistable = true;
                        callFlags |= GTF_CALL_HOISTABLE;
                    }

                    if (fieldInfo.fieldAccessor is CORINFO_FIELD_STATIC_TLS_MANAGED)
                    {
                        assert(fieldInfo.helper is CORINFO_HELP_READYTORUN_THREADSTATIC_BASE);
                        var call = gtNewHelperCallNode(TYP_BYREF, CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR);

                        call._initClsHnd = resolvedToken.hClass;
                        call._entryPoint = fieldInfo.fieldLookup;
                        call.Flags |= callFlags;

                        op1 = gtNewBinaryNode(GT_ADD, call.Type, call, gtNewIconNode(fieldInfo.offset, innerFldSeq));

                        _preferredInitCctor = CORINFO_HELP_READYTORUN_GCSTATIC_BASE;
                        break;
                    }
                    else
                    {
                        var call = gtNewHelperCallNode(TYP_BYREF, fieldInfo.helper);

                        if ((resolvedToken.hClass == info.compClassHnd) && (_preferredInitCctor is CORINFO_HELP_UNDEF) && (fieldInfo.helper is CORINFO_HELP_READYTORUN_GCSTATIC_BASE or CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE))
                        {
                            _preferredInitCctor = fieldInfo.helper;
                        }

                        if (IsStaticHelperEligibleForExpansion(call))
                        {
                            // Keep class handle attached to the helper call since it's difficult to restore it.
                            call._initClsHnd = resolvedToken.hClass;
                        }

                        call.Flags |= callFlags;
                        call._entryPoint = fieldInfo.fieldLookup;

                        op1 = call;
                    }
                }
                else
#endif
                {
                    op1 = fgGetStaticsCCtorHelper(resolvedToken.hClass, fieldInfo.helper, typeIndex);
                    isHoistable |= (op1.Flags & GTF_CALL_HOISTABLE) is not 0;
                }

                op1 = gtNewBinaryNode(GT_ADD, op1.Type, op1, gtNewIconNode(fieldInfo.offset, innerFldSeq));
                break;
            }

            case CORINFO_FIELD_STATIC_RELOCATABLE:
            {
#if FEATURE_READYTORUN
                assert(fieldKind == FieldSeq.FieldKind.SimpleStatic);

                var fldAddr = unchecked((nint)(fieldInfo.fieldLookup.addr));

                if (fieldInfo.fieldLookup.accessType == IAT_VALUE)
                {
                    op1 = gtNewIconHandleNode(fldAddr, GTF_ICON_STATIC_HDL);
                }
                else
                {
                    assert(fieldInfo.fieldLookup.accessType == IAT_PVALUE);
                    op1 = gtNewIndOfIconHandleNode(TYP_I_IMPL, fldAddr, GTF_ICON_STATIC_ADDR_PTR);
                }

                var offset = gtNewIconNode(fieldInfo.offset, innerFldSeq);
                isHoistable = true;
                op1 = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, op1, offset);
#else
                unreached();
#endif
                break;
            }

            case CORINFO_FIELD_STATIC_READYTORUN_HELPER:
            {
#if FEATURE_READYTORUN
                assert(IsAot);
                assert(!compIsForInlining);

                CORINFO_LOOKUP_KIND kind;
                info.compCompHnd->getLocationOfThisType(info.compMethodHnd, &kind);
                assert(kind.needsRuntimeLookup);

                var ctxTree = getRuntimeContextTree(kind.runtimeLookupKind);

                var helper = CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE;
                var callFlags = GTF_EMPTY;

                if (!helper.MayRunCctor || ((info.compCompHnd->getClassAttribs(resolvedToken.hClass) & CORINFO_FLG_BEFOREFIELDINIT) is not 0))
                {
                    isHoistable = true;
                    callFlags |= GTF_CALL_HOISTABLE;
                }

                var call = gtNewHelperCallNode(TYP_BYREF, helper, ctxTree);

                call.Flags |= callFlags;
                call._entryPoint = fieldInfo.fieldLookup;

                op1 = gtNewBinaryNode(GT_ADD, call.Type, call, gtNewIconNode(fieldInfo.offset, innerFldSeq));
#else
                unreached();
#endif
                break;
            }

            default:
            {
                var isStaticReadOnlyInitedRef = false;

#if TARGET_64BIT
                // TODO-CQ: enable this optimization for 32 bit targets.
                if (!isBoxedStatic && (lclTyp is TYP_REF) && ((access & CORINFO_ACCESS_GET) is not 0) && ((indirFlags & GTF_IND_VOLATILE) is 0))
                {
                    var isSpeculative = true;

                    if ((info.compCompHnd->getStaticFieldCurrentClass(resolvedToken.hField, &isSpeculative) != NO_CLASS_HANDLE))
                    {
                        isStaticReadOnlyInitedRef = !isSpeculative;
                    }
                }
#endif

                assert(fieldInfo.fieldLookup.accessType == IAT_VALUE);
                var fldAddr = unchecked((nint)(fieldInfo.fieldLookup.addr));

                GenTreeFlags handleKind;

                if (isBoxedStatic)
                {
                    handleKind = GTF_ICON_STATIC_BOX_PTR;
                }
                else if (isStaticReadOnlyInitedRef)
                {
                    handleKind = GTF_ICON_CONST_PTR;
                }
                else
                {
                    handleKind = GTF_ICON_STATIC_HDL;
                }

                isHoistable = true;
                var intCon = gtNewIconHandleNode(fldAddr, handleKind, innerFldSeq);

#if DEBUG
                intCon.TargetHandle = unchecked((nint)(resolvedToken.hField));
#endif

                op1 = intCon;

                if ((fieldInfo.fieldFlags & CORINFO_FLG_FIELD_INITCLASS) is not 0)
                {
                    additionalIndirFlags |= GTF_IND_INITCLASS;
                }
                if (isStaticReadOnlyInitedRef)
                {
                    additionalIndirFlags |= (GTF_IND_INVARIANT | GTF_IND_NONNULL);
                }
                break;
            }
        }

        if (isBoxedStatic)
        {
            op1 = gtNewIndir(TYP_REF, op1, GTF_IND_NONFAULTING | GTF_IND_INVARIANT | GTF_IND_NONNULL | additionalIndirFlags);
            op1 = gtNewBinaryNode(GT_ADD, TYP_BYREF, op1, gtNewIconNode(TARGET_POINTER_SIZE, outerFldSeq));

            additionalIndirFlags &= ~GTF_IND_INITCLASS;
        }

        indirFlags |= additionalIndirFlags;
        return op1;
    }

    /// <summary>Tries to import 'static readonly' field as a constant if the host type is statically initialized</summary>
    /// <param name="field">'static readonly' field</param>
    /// <param name="ownerCls">class handle of the type the given field defined in</param>
    /// <returns>The tree representing the constant value of the statically initialized readonly tree.</returns>
    public unsafe GenTree? impImportStaticReadOnlyField(CORINFO_FIELD_HANDLE field, CORINFO_CLASS_HANDLE ownerCls)
    {
        if (!opts.OptimizationEnabled)
        {
            return null;
        }

        JITDUMP("\nChecking if we can import 'static readonly' as a jit-time constant... ");

        CORINFO_CLASS_HANDLE fieldClsHnd;
        var fieldType = info.compCompHnd->getFieldType(field, &fieldClsHnd, ownerCls).VarType;

        if (varTypeIsIntegral(fieldType) || varTypeIsFloating(fieldType) || (fieldType is TYP_REF))
        {
            const int PrimitiveBufferSize = 8;

            assert(PrimitiveBufferSize >= fieldType.Size);
            Unsafe.SkipInit(out InlineArray8<byte> inlinePrimitiveBuffer);
            var primitiveBuffer = (Span<byte>)(inlinePrimitiveBuffer);

            if (info.compCompHnd->getStaticFieldContent(field, (byte*)(&inlinePrimitiveBuffer), fieldType.Size))
            {
                var cnsValue = gtNewGenericCon(fieldType, primitiveBuffer[..fieldType.Size]);

                if (cnsValue is not null)
                {
                    JITDUMP("... success! The value is:\n");
                    DISPTREE(cnsValue);
                    return cnsValue;
                }
            }
        }
        else if (fieldType is TYP_STRUCT)
        {
            var totalSize = info.compCompHnd->getClassSize(fieldClsHnd);
            var fieldsCnt = info.compCompHnd->getClassNumInstanceFields(fieldClsHnd);

            // For large structs we only want to handle "initialized with zero" case
            // e.g. Guid.Empty and decimal.Zero static readonly fields.

            if ((totalSize > TARGET_POINTER_SIZE) || (fieldsCnt is not 1))
            {
                const int LargeStructBufferSize = 64;

                JITDUMP("checking if we can do anything for a large struct ...");

                if (totalSize is 0 or > LargeStructBufferSize)
                {
                    // Limit to simd_t bytes for better throughput
                    JITDUMP($"struct is larger than {LargeStructBufferSize} bytes - bail out.");
                    return null;
                }

                Unsafe.SkipInit(out InlineArray64<byte> inlineLargeStructBuffer);
                var largeStructBuffer = (Span<byte>)(inlineLargeStructBuffer);

                if (info.compCompHnd->getStaticFieldContent(field, &inlineLargeStructBuffer.e0, totalSize))
                {
#if FEATURE_SIMD
                    // First, let's check whether field is a SIMD vector and import it as GT_CNS_VEC
                    var simdWidth = GetSimdTypeSizeInBytes(fieldClsHnd);

                    if (simdWidth > 0)
                    {
                        assert((totalSize <= sizeof(simd_t)) && (totalSize <= LargeStructBufferSize));
                        var simdType = GetSimdTypeForSize(simdWidth);

                        var hwAccelerated = true;

#if TARGET_XARCH
                        if (simdType is TYP_SIMD64)
                        {
                            hwAccelerated = compOpportunisticallyDependsOn(InstructionSet_AVX512);
                        }
                        else if (simdType is TYP_SIMD32)
                        {
                            hwAccelerated = compOpportunisticallyDependsOn(InstructionSet_AVX);
                        }
                        else
#endif
                        {
                            // SIMD8, SIMD12, SIMD16 are covered by baseline ISA requirement
                            assert(simdType is TYP_SIMD8 or TYP_SIMD12 or TYP_SIMD16);
                        }

                        if (hwAccelerated)
                        {
                            var vecCon = gtNewVconNode(simdType);
                            Unsafe.CopyBlockUnaligned(ref vecCon.SimdVal.u8[0], in largeStructBuffer[0], (uint)(totalSize));
                            return vecCon;
                        }
                    }
#endif

                    if (((ReadOnlySpan<byte>)(largeStructBuffer)).ContainsAnyExcept((byte)(0)))
                    {
                        // Value is not all zeroes - bail out.
                        // Although, We might eventually support that too.
                        JITDUMP("value is not all zeros - bail out.");
                        return null;
                    }

                    JITDUMP("Success! Optimizing to STORE_LCL_VAR<struct>(0).");
                    var largeStructTempNum = lvaGrabTemp(shortLifetime: true, "folding static readonly field empty struct");
                    lvaSetStruct(largeStructTempNum, fieldClsHnd, unsafeValueClsCheck: false);

                    impStoreToTemp(largeStructTempNum, gtNewIconNode(TYP_INT, 0), CHECK_SPILL_NONE);
                    return gtNewLclVarNode(TYP_UNDEF, largeStructTempNum);
                }

                JITDUMP("getStaticFieldContent returned false - bail out.");
                return null;
            }

            // Only single-field structs are supported here to avoid potential regressions where
            // Metadata-driven struct promotion leads to regressions.

            var innerField = info.compCompHnd->getFieldInClass(fieldClsHnd, num: 0);

            CORINFO_CLASS_HANDLE innerFieldClsHnd;
            var fieldVarType = info.compCompHnd->getFieldType(innerField, &innerFieldClsHnd, fieldClsHnd).VarType;

            // Technically, we can support frozen gc refs here and maybe floating point in future
            if (!varTypeIsIntegral(fieldVarType))
            {
                JITDUMP("struct has non-primitive fields - bail out.");
                return null;
            }

            var fldOffset = (ushort)(info.compCompHnd->getFieldOffset(innerField));

            if ((fldOffset is not 0) || (totalSize != fieldVarType.Size) || (totalSize is 0))
            {
                // The field is expected to be of the exact size as the struct with 0 offset
                JITDUMP("struct has complex layout - bail out.");
                return null;
            }

            Unsafe.SkipInit(out InlineArrayTargetPointerSize<byte> inlineSmallStructBuffer);
            var smallStructBuffer = (Span<byte>)(inlineSmallStructBuffer);

            if ((totalSize > smallStructBuffer.Length) || !info.compCompHnd->getStaticFieldContent(field, &inlineSmallStructBuffer.e0, totalSize))
            {
                return null;
            }

            var structTempNum = lvaGrabTemp(shortLifetime: true, "folding static readonly field struct");
            lvaSetStruct(structTempNum, fieldClsHnd, unsafeValueClsCheck: false);

            var constValTree = gtNewGenericCon(fieldVarType, new ReadOnlySpan<byte>(&inlineSmallStructBuffer.e0, totalSize));
            assert(constValTree is not null);

            var fieldStoreTree = gtNewStoreLclFldNode(fieldVarType, structTempNum, fldOffset, constValTree);
            impAppendTree(fieldStoreTree, CHECK_SPILL_NONE, impCurStmtDI);

            JITDUMP($"Folding 'static readonly {eeGetClassName(fieldClsHnd)}' field to a STORE_LCL_FLD(CNS) node\n");
            return impCreateLocalNode(structTempNum, (0));
        }
        return null;
    }

    /// <summary>Inherit async args from inlining call as part of a new async call.</summary>
    /// <param name="call">The async call</param>
    /// <remarks>Currently we only allow inlining of async calls when all awaits are tail awaits. In that case inlining is simplified as we can just inherit everything from the inlining call.</remarks>
    public void impInheritAsyncContextsFromInliner(GenTreeCall call)
    {
        if (!compIsForInlining)
        {
            return;
        }

        var inlCall = impInlineInfo.iciCall;
        assert(inlCall is not null);

        var execArg = inlCall.Args.FindWellKnownArg(WellKnownArg.AsyncExecutionContext);
        var syncArg = inlCall.Args.FindWellKnownArg(WellKnownArg.AsyncSynchronizationContext);

        if (execArg is null)
        {
            // Caller also has no async contexts handling
            assert(syncArg is null);
            return;
        }
        assert(syncArg is not null);

        // We are inlining an async call that does not save contexts into a call
        // that does. We currently allow this only in cases where the tail of the
        // inlinee can run in the caller's context, and hence we propagate the
        // caller's context here. It means we do not need to worry about switching
        // into the caller's context when the inlinee is returning to the caller
        // after the await.
        assert((execArg.Node.Oper is GT_LCL_VAR) && (syncArg.Node.Oper is GT_LCL_VAR));
        JITDUMP($"Inheriting contexts [{execArg.Node.TreeId:D6}] and [{syncArg.Node.TreeId:D6}] from caller node\n");

        var execNode = gtCloneExpr(execArg.Node);
        var syncNode = gtCloneExpr(syncArg.Node);

        _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(syncNode).WithWellKnownArg(WellKnownArg.AsyncSynchronizationContext));
        _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(execNode).WithWellKnownArg(WellKnownArg.AsyncExecutionContext));
    }

    /// <summary>Locate the next stmt boundary for which we need to record info.</summary>
    /// <returns>The next stmt boundary (after the start of the block)</returns>
    /// <remarks>We will have to spill the stack at such boundaries if it is not already empty.</remarks>
    public int impInitBlockLineInfo()
    {
        // Assume the block does not correspond with any IL offset. This prevents
        // us from reporting extra offsets. Extra mappings can cause confusing
        // stepping, especially if the extra mapping is a jump-target, and the
        // debugger does not ignore extra mappings, but instead rewinds to the
        // nearest known offset

        impCurStmtOffsSet(BAD_IL_OFFSET);

        assert(compCurBB is not null);
        var blockOffs = compCurBB.bbCodeOffs;

        if ((stackState.esStackDepth is 0) && ((info.compStmtOffsetsImplicit & ICorDebugInfo.STACK_EMPTY_BOUNDARIES) != 0))
        {
            impCurStmtOffsSet(blockOffs);
        }

        // Always report IL offset 0 or some tests get confused. Probably a good idea anyways

        if (blockOffs is 0)
        {
            impCurStmtOffsSet(blockOffs);
        }

        if (info.compStmtOffsetsCount != 0)
        {
            return ~0;
        }

        // Find the lowest explicit stmt boundary within the block
        // Start looking at an entry that is based on our instr offset

        var index = (info.compStmtOffsetsCount * blockOffs) / info.compILCodeSize;

        if (index >= info.compStmtOffsetsCount)
        {
            index = info.compStmtOffsetsCount - 1;
        }

        // If we've guessed too far, back up

        while ((index > 0) && (info.compStmtOffsets[index - 1] >= blockOffs))
        {
            index--;
        }

        // If we guessed short, advance ahead

        while (info.compStmtOffsets[index] < blockOffs)
        {
            index++;

            if (index == info.compStmtOffsetsCount)
            {
                return info.compStmtOffsetsCount;
            }
        }

        assert(index < info.compStmtOffsetsCount);

        if (info.compStmtOffsets[index] == blockOffs)
        {
            // There is an explicit boundary for the start of this basic block.
            // So we will start with bbCodeOffs, else we will wait until we get to the next explicit boundary

            impCurStmtOffsSet(blockOffs);
            index++;
        }
        return index;
    }

    /// <summary>Build a node to initialize the class before accessing the field if necessary</summary>
    /// <param name="resolvedToken">The CORINFO_RESOLVED_TOKEN that has been initialized by a call to CEEInfo.resolveToken().</param>
    /// <returns>If needed, a pointer to the node that will perform the class initializtion.  Otherwise, null.</returns>
    public unsafe GenTree? impInitClass(in CORINFO_RESOLVED_TOKEN resolvedToken)
    {
        var initClassResult = info.compCompHnd->initClass(resolvedToken.hField, info.compMethodHnd, impTokenLookupContextHandle);

        if ((initClassResult & CORINFO_INITCLASS_USE_HELPER) is 0)
        {
            return null;
        }

        var node = impParentClassTokenToHandle(resolvedToken, out var runtimeLookup);

        if (node is null)
        {
            assert(compDonotInline);
            return null;
        }

        if (runtimeLookup)
        {
            node = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_INITCLASS, node);
        }
        else
        {
            // Call the shared non gc static helper, as its the fastest
            node = fgGetSharedCCtor(resolvedToken.hClass);
        }
        return node;
    }

    /// <summary>Attempts to replace a call to InitializeArray with a GT_COPYBLK node.</summary>
    /// <param name="sigInfo">The InitializeArray signature.</param>
    /// <returns>A pointer to the newly created GT_COPYBLK node if the replacement succeeds or null otherwise.</returns>
    public unsafe GenTree? impInitializeArrayIntrinsic(in CORINFO_SIG_INFO sigInfo)
    {
        // Notes:
        //    The function recognizes the following IL pattern:
        //      ldc <length> or a list of ldc <lower bound>/<length>
        //      newarr or newobj
        //      dup
        //      ldtoken <field handle>
        //      call InitializeArray
        //    The lower bounds need not be constant except when the array rank is 1.
        //    The function recognizes all kinds of arrays thus enabling a small runtime
        //    such as NativeAOT to skip providing an implementation for InitializeArray.

        assert(sigInfo.numArgs is 2);

        var maybeFieldTokenNode = impStackTop(0).val;
        var maybeArrayLocalNode = impStackTop(1).val;

        //
        // Verify that the field token is known and valid.  Note that it's also
        // possible for the token to come from reflection, in which case we cannot do
        // the optimization and must therefore revert to calling the helper.  You can
        // see an example of this in bvt\DynIL\initarray2.exe (in Main).
        //

        // Check to see if the ldtoken helper call is what we see here.
        if (maybeFieldTokenNode.Oper is not GT_CALL)
        {
            return null;
        }

        var call = maybeFieldTokenNode.AsCall();

        if (!call.IsHelperCall(eeFindHelper(CORINFO_HELP_FIELDDESC_TO_STUBRUNTIMEFIELD)))
        {
            return null;
        }

        // Strip helper call away

        var callArg = call.Args.GetArgByIndex(0);
        assert(callArg is not null);
        maybeFieldTokenNode = callArg.EarlyNode;

        if (maybeFieldTokenNode.Oper is GT_IND)
        {
            maybeFieldTokenNode = maybeFieldTokenNode.AsOp().Op1;
        }

        // Check for constant
        if (maybeFieldTokenNode.Oper is not GT_CNS_INT)
        {
            return null;
        }

        var fieldTokenNode = maybeFieldTokenNode.AsIntCon();
        var fieldToken = (CORINFO_FIELD_HANDLE)(fieldTokenNode.CompileTimeHandle);

        if (!fieldTokenNode.IsIconHandle(GTF_ICON_FIELD_HDL) || (fieldToken is null))
        {
            return null;
        }

        // We need to get the number of elements in the array and the size of each element.
        // We verify that the newarr statement is exactly what we expect it to be.
        // If it's not then we just return NULL and we don't optimize this call

        // It is possible the we don't have any statements in the block yet.
        if (impLastStmt is null)
        {
            return null;
        }

        // We start by looking at the last statement, making sure it's a store.
        var maybeArrayLocalStore = impLastStmt.RootNode;

        if ((maybeArrayLocalStore.Oper is not GT_STORE_LCL_VAR) || (maybeArrayLocalNode.Oper is not GT_LCL_VAR))
        {
            return null;
        }

        var arrayLocalStore = maybeArrayLocalStore.AsLclVar();
        var arrayLocalNode = maybeArrayLocalNode.AsLclVar();

        // Make sure the target of the store is the array passed to InitializeArray.
        if (arrayLocalStore.LclNum != arrayLocalNode.LclNum)
        {
            if (opts.OptimizationDisabled)
            {
                return null;
            }

            // The array can be spilled to a temp for stack allocation.
            // Try getting the actual store node from the previous statement.
            if (arrayLocalStore.Data.Oper is GT_LCL_VAR)
            {
                var prevStmt = impLastStmt.PrevStmt;

                if (prevStmt is not null)
                {
                    maybeArrayLocalStore = prevStmt.RootNode;

                    if (maybeArrayLocalStore.Oper is not GT_STORE_LCL_VAR)
                    {
                        return null;
                    }

                    arrayLocalStore = maybeArrayLocalStore.AsLclVar();

                    if (arrayLocalStore.LclNum != arrayLocalNode.LclNum)
                    {
                        return null;
                    }
                }
            }
        }

        // Make sure that the object being assigned is a helper call.
        var maybeNewArrayCall = arrayLocalStore.Data;

        if (maybeNewArrayCall.Oper is not GT_CALL)
        {
            return null;
        }

        var newArrayCall = maybeNewArrayCall.AsCall();

        if (!newArrayCall.IsHelperCall())
        {
            return null;
        }

        // Verify that it is one of the new array helpers.
        var isMDArray = false;

        switch (newArrayCall.HelperNum)
        {
            case CORINFO_HELP_NEWARR_1_DIRECT:
            case CORINFO_HELP_NEWARR_1_PTR:
            case CORINFO_HELP_NEWARR_1_MAYBEFROZEN:
            case CORINFO_HELP_NEWARR_1_VC:
            case CORINFO_HELP_NEWARR_1_ALIGN8:
#if FEATURE_READYTORUN
            case CORINFO_HELP_READYTORUN_NEWARR_1:
#endif
            {
                break;
            }

            case CORINFO_HELP_NEW_MDARR:
            case CORINFO_HELP_NEW_MDARR_RARE:
            {
                isMDArray = true;
                break;
            }

            default:
            {
                return null;
            }
        }

        var arrayClsHnd = (CORINFO_CLASS_HANDLE)(newArrayCall._compileTimeHelperArgumentHandle);

        // Make sure we found a compile time handle to the array

        if (arrayClsHnd is null)
        {
            return null;
        }

        var rank = 0;
        var numElements = 0;

        if (isMDArray)
        {
            rank = info.compCompHnd->getArrayRank(arrayClsHnd);

            if (rank is 0)
            {
                return null;
            }

            assert(newArrayCall.Args.CountArgs() is 3);

            var callArg1 = newArrayCall.Args.GetArgByIndex(1);
            assert(callArg1 is not null);
            var numArgsArg = callArg1.Node;

            var callArg2 = newArrayCall.Args.GetArgByIndex(2);
            assert(callArg2 is not null);
            var argsArg = callArg2.Node;

            // The number of arguments should be a constant between 1 and 64. The rank can't be 0
            // so at least one length must be present and the rank can't exceed 32 so there can
            // be at most 64 arguments - 32 lengths and 32 lower bounds.

            if (!numArgsArg.Oper.IsCnsIntOrI)
            {
                return null;
            }

            var numArgs = numArgsArg.AsIntCon().IconValue;

            if (numArgs is < 1 or > 64)
            {
                return null;
            }

            var lowerBoundsSpecified = false;

            if (numArgs == (rank * 2))
            {
                lowerBoundsSpecified = true;
            }
            else if (numArgs == rank)
            {
                lowerBoundsSpecified = false;

                // If the rank is 1 and a lower bound isn't specified then the runtime creates
                // a SDArray. Note that even if a lower bound is specified it can be 0 and then
                // we get a SDArray as well, see the for loop below.

                if (rank is 1)
                {
                    isMDArray = false;
                }
            }
            else
            {
                return null;
            }

            // The rank is known to be at least 1 so we can start with numElements being 1
            // to avoid the need to special case the first dimension.

            numElements = 1;

            var argIndex = 0;
            GenTree maybeComma;
            GenTreeOp comma;

            for (maybeComma = argsArg; MatchIsComma(maybeComma); maybeComma = comma.Op2)
            {
                comma = maybeComma.AsOp();

                if (lowerBoundsSpecified)
                {
                    // In general lower bounds can be ignored because they're not needed to
                    // calculate the total number of elements. But for single dimensional arrays
                    // we need to know if the lower bound is 0 because in this case the runtime
                    // creates a SDArray and this affects the way the array data offset is calculated.

                    if (rank is 1)
                    {
                        var lowerBoundStore = comma.Op1;
                        assert(MatchIsArgsFieldInit(lowerBoundStore, argIndex, lvaNewObjArrayArgs));
                        var lowerBoundNode = lowerBoundStore.AsLclVarCommon().Data;

                        if (lowerBoundNode.IsIntegralConst(0))
                        {
                            isMDArray = false;
                        }
                    }

                    maybeComma = comma.Op2;
                    argIndex++;
                }

                var lengthNodeStore = maybeComma.AsOp().Op1;
                assert(MatchIsArgsFieldInit(lengthNodeStore, argIndex, lvaNewObjArrayArgs));
                var lengthNode = lengthNodeStore.AsLclVarCommon().Data;

                if (!lengthNode.Oper.IsCnsIntOrI)
                {
                    return null;
                }

                if (!CheckedOps.TryMul(numElements, (int)(lengthNode.AsIntCon().IconValue), out numElements))
                {
                    return null;
                }
                argIndex++;
            }

            assert((maybeComma is not null) && maybeComma.IsLclVarAddr);
            assert(maybeComma.AsLclVarCommon().LclNum == lvaNewObjArrayArgs);

            if (argIndex != numArgs)
            {
                return null;
            }
        }
        else
        {
            // Make sure there are exactly two arguments:  the array class and the number of elements.
            GenTree arrayLengthNode;

#if FEATURE_READYTORUN
            if (newArrayCall.IsHelperCall(CORINFO_HELP_READYTORUN_NEWARR_1) ||
                newArrayCall.IsHelperCall(CORINFO_HELP_NEWARR_1_MAYBEFROZEN))
            {
                // Array length is 1st argument for readytorun helper
                var callArg0 = newArrayCall.Args.GetArgByIndex(0);
                assert(callArg0 is not null);
                arrayLengthNode = callArg0.Node;
            }
            else
#endif
            {
                // Array length is 2nd argument for regular helper
                var callArg1 = newArrayCall.Args.GetArgByIndex(1);
                assert(callArg1 is not null);
                arrayLengthNode = callArg1.Node;
            }

            if (arrayLengthNode.Oper is not GT_CNS_INT)
            {
                // This optimization is only valid for a constant array size.
                return null;
            }

            numElements = (int)(arrayLengthNode.AsIntCon().IconVal);

            if (!info.compCompHnd->isSDArray(arrayClsHnd))
            {
                return null;
            }
        }

        CORINFO_CLASS_HANDLE elemClsHnd;
        var elementType = info.compCompHnd->getChildType(arrayClsHnd, &elemClsHnd).VarType;

        //
        // Note that genTypeSize will return zero for non primitive types, which is exactly
        // what we want (size will then be 0, and we will catch this in the conditional below).
        // Note that we don't expect this to fail for valid binaries, so we assert in the
        // non-verification case (the verification case should not assert but rather correctly
        // handle bad binaries).  This assert is not guarding any specific invariant, but rather
        // saying that we don't expect this to happen, and if it is hit, we need to investigate
        // why.
        //

        var elemSize = elementType.Size;

        if (!CheckedOps.TryMul(elemSize, numElements, out var size))
        {
            return null;
        }

        if ((size is 0) || (varTypeIsGC(elementType)))
        {
            return null;
        }

        var initData = info.compCompHnd->getArrayInitializationData(fieldToken, size);

        if (initData is null)
        {
            return null;
        }

        // At this point we are ready to commit to implementing the InitializeArray
        // intrinsic using a struct store.  Pop the arguments from the stack and
        // return the store node.

        impPopStack(2);

        var blkSize = size;
        int dataOffset;

        if (isMDArray)
        {
            dataOffset = eeGetMDArrayDataOffset(rank);
        }
        else
        {
            dataOffset = eeGetArrayDataOffset();
        }

        var blkLayout = typGetBlkLayout(blkSize);
        var srcAddr = gtNewIconHandleNode(unchecked((nint)(initData)), GTF_ICON_CONST_PTR);
        var src = gtNewBlkIndir(srcAddr, blkLayout);
        var dstAddr = gtNewBinaryNode(GT_ADD, TYP_BYREF, arrayLocalNode, gtNewIconNode(TYP_I_IMPL, dataOffset));
        var store = gtNewStoreBlkNode(dstAddr, src, blkLayout);

#if DEBUG
        src.Op1.AsIntCon().TargetHandle = (byte)(THT_InitializeArrayIntrinsics);
#endif

        return store;

        static bool MatchIsArgsFieldInit(GenTree tree, int index, int lvaNewObjArrayArgs)
        {
            if (tree.Oper is not GT_STORE_LCL_FLD)
            {
                return false;
            }

            var lclFld = tree.AsLclFld();

            return (lclFld.LclNum == lvaNewObjArrayArgs) &&
                   (lclFld.LclOffs == (sizeof(int) * index));
        }

        static bool MatchIsComma(GenTree tree)
        {
            return (tree is not null) && (tree.Oper is GT_COMMA);
        }
    }

    /// <summary>return tree node for argument value in an inlinee</summary>
    /// <param name="argInfo">argument info for inlinee</param>
    /// <param name="lclInfo">var info for inlinee</param>
    /// <returns>Tree for the argument's value. Often an inlinee-scoped temp GT_LCL_VAR but can be other tree kinds, if the argument expression from the caller can be directly substituted into the inlinee body.</returns>
    /// <remarks>
    ///   <para>Must be used only for arguments -- use impInlineFetchLocal for inlinee locals.</para>
    ///   <para>Direct substitution is performed when the formal argument cannot change value in the inlinee body (no starg or ldarga), and the actual argument expression's value cannot be changed if it is substituted it into the inlinee body.</para>
    ///   <para>Even if an inlinee-scoped temp is returned here, it may later be "bashed" to a caller-supplied tree when arguments are actually passed (see fgInlinePrependStatements). Bashing can happen if the argument ends up being single use and other conditions are met. So the contents of the tree returned here may not end up being the ones ultimately used for the argument.</para>
    ///   <para>This method will side effect inlArgInfo. It should only be called for actual uses of the argument in the inlinee.</para>
    /// </remarks>
    public unsafe GenTree impInlineFetchArg(ref InlArgInfo argInfo, in InlLclVarInfo lclInfo)
    {
        // Cache the relevant arg and lcl info for this argument.
        // We will modify argInfo but not lclVarInfo.
        var argCanBeModified = argInfo.argHasLdargaOp || argInfo.argHasStargOp;
        var lclTyp = lclInfo.lclTypeInfo;

        GenTree op1;
        var argNode = argInfo.arg.Node;
        assert(argNode.Oper is not GT_RET_EXPR);

        // For TYP_REF args, if the argNode doesn't have any class information we will lose some type info if we directly substitute it.
        // We can at least rely on the declared type of the arg here.

        var argLosesTypeInfo = false;

        if (argNode.Type is TYP_REF)
        {
            var argClass = gtGetClassHandle(argNode, out _, out _);
            argLosesTypeInfo = (argClass == NO_CLASS_HANDLE);
        }

        if (argInfo.argIsInvariant && !argCanBeModified)
        {
            // Directly substitute constants or addresses of locals
            //
            // Clone the constant. Note that we cannot directly use
            // argNode in the trees even if !argInfo.argIsUsed as this
            // would introduce aliasing between inlArgInfo[].argNode and
            // impInlineExpr. Then gtFoldExpr() could change it, causing
            // further references to the argument working off of the
            // bashed copy.

            op1 = gtCloneExpr(argNode);
            assert(op1 is not null);
            argInfo.argTmpNum = BAD_VAR_NUM;

            // We may need to retype to ensure we match the callee's view of the type.
            // Otherwise callee-pass throughs of arguments can create return type
            // mismatches that block inlining.
            //
            // Note argument type mismatches that prevent inlining should
            // have been caught in impInlineInitVars.

            if (op1.Type != lclTyp)
            {
                op1.Type = lclTyp.ActualType;
            }
        }
        else if (argInfo.argIsLclVar && !argCanBeModified && !argInfo.argHasCallerLocalRef && !argLosesTypeInfo)
        {
            // Directly substitute unaliased caller locals for args that cannot be modified
            // Use the caller-supplied node if this is the first use.

            op1 = argNode;
            var argLclNum = op1.AsLclVarCommon().LclNum;
            argInfo.argTmpNum = argLclNum;

            // Use an equivalent copy if this is the second or subsequent
            // use.
            //
            // Note argument type mismatches that prevent inlining should
            // have been caught in impInlineInitVars. If inlining is not prevented
            // but a cast is necessary, we similarly expect it to have been inserted then.
            // So here we may have argument type mismatches that are benign, for instance
            // passing a TYP_SHORT local (eg. normalized-on-load) as a TYP_INT arg.
            // The exception is when the inlining means we should start tracking the argument.

            if (argInfo.argIsUsed || ((lclTyp == TYP_BYREF) && (op1.Type is not TYP_BYREF)))
            {
                assert(op1.Oper is GT_LCL_VAR);

                // Create a new lcl var node - remember the argument lclNum
                op1 = impCreateLocalNode(argLclNum, (op1.AsLclVar().LclIlOffs));

                // Start tracking things as a byref if the parameter is a byref.
                if (lclTyp == TYP_BYREF)
                {
                    op1.Type = TYP_BYREF;
                }
            }
        }
        else if (argInfo.argIsByRefToStructLocal && !argInfo.argHasStargOp)
        {
            // Argument is a by-ref address to a struct, a normed struct, or its field.
            // In these cases, don't spill the byref to a local, simply clone the tree and use it.
            // This way we will increase the chance for this byref to be optimized away by
            // a subsequent "dereference" operation.
            //
            // From Dev11 bug #139955: Argument node can also be TYP_I_IMPL if we've bashed the tree
            // (in impInlineInitVars()), if the arg has argHasLdargaOp as well as argIsByRefToStructLocal.
            // For example, if the caller is:
            //      ldloca.s   V_1  // V_1 is a local struct
            //      call       void Test.ILPart.RunLdargaOnPointerArg(int32*)
            // and the callee being inlined has:
            //      .method public static void  RunLdargaOnPointerArg(int32* ptrToInts) cil managed
            //          ldarga.s   ptrToInts
            //          call       void Test.FourInts.NotInlined_SetExpectedValuesThroughPointerToPointer(int32**)
            // then we change the argument tree (of "ldloca.s V_1") to TYP_I_IMPL to match the callee signature. We'll
            // soon afterwards reject the inlining anyway, since the tree we return isn't a GT_LCL_VAR.

            assert(argNode.Type is TYP_BYREF or TYP_I_IMPL);
            op1 = gtCloneExpr(argNode);
        }
        else
        {
            // Argument is a complex expression - it must be evaluated into a temp

            if (argInfo.argHasTmp)
            {
                assert(argInfo.argIsUsed);
                assert(argInfo.argTmpNum < lvaCount);

                // Create a new lcl var node - remember the argument lclNum
                op1 = gtNewLclvNode(lclTyp.ActualType, argInfo.argTmpNum);

                // This is the second or later use of the this argument, so we have to use the temp (instead of the actual arg)
                argInfo.argBashTmpNode = null;
            }
            else
            {
                // First time use
                assert(!argInfo.argIsUsed);

                // Reserve a temp for the expression.
                var tmpNum = lvaGrabTemp(shortLifetime: true, "Inlining Arg");
                lvaTable[tmpNum].Type = lclTyp;

                // If arg can't be modified, mark it as single def.
                // For ref types, determine the class of the arg temp.
                if (!argCanBeModified)
                {
                    assert(!lvaTable[tmpNum].lvSingleDef);

                    lvaTable[tmpNum].lvSingleDef = true;
                    JITDUMP($"Marked V{tmpNum:D2} as a single def temp\n");

                    if (lclTyp == TYP_REF)
                    {
                        // Use argNode type (when it exists) or lclInfo type
                        lvaSetClass(tmpNum, argNode, lclInfo.lclTypeHandle);
                    }
                }
                else
                {
                    if (lclTyp == TYP_REF)
                    {
                        // Arg might be modified. Use the declared type of the argument.
                        lvaSetClass(tmpNum, lclInfo.lclTypeHandle);
                    }
                }

                assert(!lvaTable[tmpNum].IsAddressExposed);

                if (argInfo.argHasLdargaOp)
                {
                    lvaTable[tmpNum].lvHasLdAddrOp = true;
                }

                if (varTypeIsStruct(lclTyp))
                {
                    lvaSetStruct(tmpNum, lclInfo.lclTypeHandle, unsafeValueClsCheck: true);
                }

                argInfo.argHasTmp = true;
                argInfo.argTmpNum = tmpNum;

                // If we require strict exception order, then arguments must
                // be evaluated in sequence before the body of the inlined method.
                // So we need to evaluate them to a temp.
                // Also, if arguments have global or local references, we need to
                // evaluate them to a temp before the inlined body as the
                // inlined body may be modifying the global ref.
                // TODO-1stClassStructs: We currently do not reuse an existing lclVar
                // if it is a struct, because it requires some additional handling.

                op1 = gtNewLclvNode(lclTyp.ActualType, tmpNum);

                if (!varTypeIsStruct(lclTyp) && !argInfo.argHasSideEff && !argInfo.argHasGlobRef && !argInfo.argHasCallerLocalRef)
                {
                    // Record op1 as the very first use of this argument.
                    // If there are no further uses of the arg, we may be able to use the actual arg node instead of the temp.
                    // If we do see any further uses, we will clear this.
                    argInfo.argBashTmpNode = op1;
                }
            }
        }

        // Mark this argument as used.
        argInfo.argIsUsed = true;

        return op1;
    }

    /// <summary>get a local var that represents an inlinee local</summary>
    /// <param name="lclNum">number of the inlinee local</param>
    /// <param name="reason">debug string describing purpose of the local var</param>
    /// <returns>Number of the local to use</returns>
    /// <remarks>
    ///   <para>This method is invoked only for locals actually used in the inlinee body.</para>
    ///   <para>Allocates a new temp if necessary, and copies key properties over from the inlinee local var info.</para>
    /// </remarks>
    public unsafe int impInlineFetchLocal(int lclNum, string reason)
    {
        assert(compIsForInlining);

        var tmpNum = impInlineInfo.lclTmpNum[lclNum];

        if (tmpNum == BAD_VAR_NUM)
        {
            ref readonly var inlineeLocal = ref impInlineInfo.lclVarInfo[lclNum + impInlineInfo.argCnt];
            var lclTyp = inlineeLocal.lclTypeInfo;

            // The lifetime of this local might span multiple BBs.
            // So it is a long lifetime local.
            tmpNum = lvaGrabTemp(shortLifetime: false, reason);
            impInlineInfo.lclTmpNum[lclNum] = tmpNum;

            // Copy over key info
            ref var lvaDsc = ref lvaTable[tmpNum];
            lvaDsc.Type = lclTyp;
            lvaDsc.lvHasLdAddrOp = inlineeLocal.lclHasLdlocaOp;
            lvaDsc.lvPinned = inlineeLocal.lclIsPinned;
            lvaDsc.lvHasILStoreOp = inlineeLocal.lclHasStlocOp;
            lvaDsc.lvHasMultipleILStoreOp = inlineeLocal.lclHasMultipleStlocOp;

            assert(!lvaDsc.lvSingleDef);
            lvaDsc.lvSingleDef = !inlineeLocal.lclHasMultipleStlocOp && !inlineeLocal.lclHasLdlocaOp;

            if (lvaDsc.lvSingleDef)
            {
                JITDUMP($"Marked V{tmpNum:D2} as a single def temp\n");
            }

            // Copy over class handle for ref types. Note this may be a
            // shared type -- someday perhaps we can get the exact
            // signature and pass in a more precise type.
            if (lclTyp == TYP_REF)
            {
                lvaSetClass(tmpNum, inlineeLocal.lclTypeHandle);
            }

            if (varTypeIsStruct(lclTyp))
            {
                lvaSetStruct(tmpNum, inlineeLocal.lclTypeHandle, true /* unsafe value cls check */);
            }

#if DEBUG
            // Sanity check that we're properly prepared for gc ref locals.
            if (varTypeIsGC(lclTyp))
            {
                // Since there are gc locals we should have seen them earlier
                // and if there was a return value, set up the spill temp.
                assert(impInlineInfo.HasGcRefLocals);
                assert((info.compRetNativeType == TYP_VOID) || fgNeedReturnSpillTemp);
            }
            else
            {
                // Make sure all pinned locals count as gc refs.
                assert(!inlineeLocal.lclIsPinned);
            }
#endif
        }

        return tmpNum;
    }

    /// <summary>Check if a dereference in the inlinee can guarantee that the "this" pointer is non-NULL.</summary>
    /// <param name="additionalTree">a tree to check for side effects</param>
    /// <param name="additionalCallArgs">a list of call args to check for side effects</param>
    /// <param name="dereferencedAddress">address expression being dereferenced</param>
    /// <param name="inlArgInfo">inlinee argument information</param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>If we haven't hit a branch or a side effect, and we are dereferencing from 'this' to access a field or make GTF_CALL_NULLCHECK call, then we can avoid a separate null pointer check.</para>
    ///   <para>The importer stack and current statement list are searched for side effects. Trees that have been popped of the stack but haven't been appended to the statement list and have to be checked for side effects may be provided via additionalTree and additionalCallArgs.</para>
    /// </remarks>
    public bool impInlineIsGuaranteedThisDerefBeforeAnySideEffects(GenTree? additionalTree, in CallArgs additionalCallArgs, GenTree dereferencedAddress, ReadOnlySpan<InlArgInfo> inlArgInfo)
    {
        assert(compIsForInlining);
        assert(opts.OptEnabled(CLFLG_INLINING));

        var block = compCurBB;

        if (block != fgFirstBB)
        {
            return false;
        }

        if (!impInlineIsThis(dereferencedAddress, inlArgInfo))
        {
            return false;
        }

        if ((additionalTree is not null) && GTF_GLOBALLY_VISIBLE_SIDE_EFFECTS(additionalTree.Flags))
        {
            return false;
        }

        if (!Unsafe.IsNullRef(in additionalCallArgs))
        {
            foreach (var arg in additionalCallArgs.Args)
            {
                if (GTF_GLOBALLY_VISIBLE_SIDE_EFFECTS(arg.EarlyNode.Flags))
                {
                    return false;
                }
            }
        }

        foreach (var stmt in new StatementList(impStmtList))
        {
            var expr = stmt.RootNode;

            if (GTF_GLOBALLY_VISIBLE_SIDE_EFFECTS(expr.Flags))
            {
                return false;
            }
        }

        var esStack = stackState.esStack.AsSpan(0, stackState.esStackDepth);

        for (var level = 0; level < esStack.Length; level++)
        {
            var stackTreeFlags = esStack[level].val.Flags;

            if (GTF_GLOBALLY_VISIBLE_SIDE_EFFECTS(stackTreeFlags))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Is this the original "this" argument to the call being inlined?</summary>
    /// <param name="tree"></param>
    /// <param name="inlArgInfo"></param>
    /// <returns></returns>
    /// <remarks>Note that we do not inline methods with "starg 0", and so we do not need to worry about it.</remarks>
    public bool impInlineIsThis(GenTree tree, ReadOnlySpan<InlArgInfo> inlArgInfo)
    {
        assert(compIsForInlining);
        return (tree.Oper is GT_LCL_VAR) && (tree.AsLclVarCommon().LclNum == inlArgInfo[0].argTmpNum);
    }

    /// <summary>Generate code for unboxing Nullable&lt;T&gt; from an object (obj)</summary>
    /// <param name="nullableCls">class handle representing the Nullable&lt;T&gt; type</param>
    /// <param name="nullableClsNode">tree node representing the Nullable&lt;T&gt; type (can be a runtime lookup tree)</param>
    /// <param name="obj">object to unbox</param>
    /// <returns>A local node representing the unboxed value (Nullable&lt;T&gt;)</returns>
    public unsafe GenTree impInlineUnboxNullable(CORINFO_CLASS_HANDLE nullableCls, GenTree nullableClsNode, GenTree obj)
    {
        // We either inline the unbox operation (if profitable) or call the helper.
        // The inline expansion is as follows:
        //
        // Nullable<T> result;
        // if (obj is null)
        // {
        //     result = default;
        // }
        // else if (obj->pMT == <real-boxed-type>)
        // {
        //     result._hasValue = true;
        //     result._value = *(T*)(obj + sizeof(void*));
        // }
        // else
        // {
        //     result = CORINFO_HELP_UNBOX_NULLABLE(&result, nullableCls, obj);
        // }

        assert(info.compCompHnd->isNullableType(nullableCls) is TypeCompareState.Must);

        var resultTmp = lvaGrabTemp(shortLifetime: true, "Nullable<T> tmp");
        lvaSetStruct(resultTmp, nullableCls, unsafeValueClsCheck: false);

        lvaGetDesc(resultTmp).lvHasLdAddrOp = true;
        var resultAddr = gtNewLclAddrNode(TYP_BYREF, resultTmp, lclOffs: 0);

        // Check profitability of inlining the unbox operation
        assert(compCurBB is not null);
        var shouldExpandInline = !compCurBB.isRunRarely && opts.OptimizationEnabled && !eeIsSharedInst(nullableCls);

        // It's less profitable to inline the unbox operation if the underlying type is too large
        var unboxType = NO_CLASS_HANDLE;

        if (shouldExpandInline)
        {
            // The underlying type of the nullable:
            unboxType = info.compCompHnd->getTypeForBox(nullableCls);
            shouldExpandInline = info.compCompHnd->getClassSize(unboxType) <= GetUnrollThreshold(Memcpy);
        }

        if (!shouldExpandInline)
        {
            // No expansion needed, just call the helper
            var call = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_UNBOX_NULLABLE, resultAddr, nullableClsNode, obj);
            _ = impAppendTree(call, CHECK_SPILL_ALL, impCurStmtDI);
            return gtNewLclvNode(TYP_STRUCT, resultTmp);
        }

        // Clone the object (and spill side effects)
        obj = impCloneExpr(obj, out var objClone, CHECK_SPILL_ALL, "op1 spilled for Nullable unbox");
        assert(objClone is not null);

        // Unbox the object to the result local:
        //
        //  result._hasValue = true;
        //  result._value = MethodTableLookup(obj);
        //
        var valueFldHnd = info.compCompHnd->getFieldInClass(nullableCls, 1);

        var valueStructCls = NO_CLASS_HANDLE;
        var corFldType = info.compCompHnd->getFieldType(valueFldHnd, &valueStructCls);

        var valueType = TypeHandleToVarType(corFldType, valueStructCls, out var layout);

        var hasValOffset = OFFSETOF__CORINFO_NullableOfT__hasValue;
        var valueOffset = (ushort)(info.compCompHnd->getFieldOffset(valueFldHnd));
        var boxedContentAddr = gtNewBinaryNode(GT_ADD, TYP_BYREF, gtCloneExpr(objClone), gtNewIconNode(TYP_I_IMPL, TARGET_POINTER_SIZE));

        // Load the boxed content from the object (op1):
        var boxedContent = gtNewLoadValueNode(valueType, boxedContentAddr, layout);

        // Now do two stores via a comma:
        var setHasValue = gtNewStoreLclFldNode(TYP_UBYTE, resultTmp, hasValOffset, gtNewIconNode(TYP_INT, 1));
        var setValue = gtNewStoreLclFldNode(valueType, resultTmp, valueOffset, boxedContent);
        var unboxTree = gtNewCommaNode(TYP_VOID, setHasValue, setValue);

        // Fallback helper call
        // TODO: Mark as no-return when appropriate
        var helperCall = gtNewHelperCallNode(TYP_VOID, CORINFO_HELP_UNBOX_NULLABLE, resultAddr, nullableClsNode, gtCloneExpr(objClone));

        // Nested QMARK - "obj->pMT == <boxed-type> ? unboxTree : helperCall"
        assert(unboxType != NO_CLASS_HANDLE);
        var unboxTypeNode = gtNewIconEmbClsHndNode(unboxType);
        var objMT = gtNewMethodTableLookup(objClone);
        var mtLookupCond = gtNewBinaryNode(GT_NE, TYP_INT, objMT, unboxTypeNode);
        var mtCheckColon = gtNewColonNode(TYP_VOID, helperCall, unboxTree);
        var mtCheckQmark = gtNewQmarkNode(TYP_VOID, mtLookupCond, mtCheckColon);
        mtCheckQmark.ThenNodeLikelihood = 0;

        // Zero initialize the result in case of "obj is null"
        var zeroInitResultNode = gtNewStoreLclVarNode(resultTmp, gtNewIconNode(TYP_INT, 0));

        // Root condition - "obj is null ? zeroInitResultNode : mtCheckQmark"
        var nullcheck = gtNewBinaryNode(GT_NE, TYP_INT, obj, gtNewNull());
        var nullCheckColon = gtNewColonNode(TYP_VOID, mtCheckQmark, zeroInitResultNode);
        var nullCheckQmark = gtNewQmarkNode(TYP_VOID, nullcheck, nullCheckColon);

        // Spill the root QMARK and return the result local
        _ = impAppendTree(nullCheckQmark, CHECK_SPILL_ALL, impCurStmtDI);
        return gtNewLclvNode(TYP_STRUCT, resultTmp);
    }

    /// <summary>Insert async arguments for a call the EE asked to be performed via ldvirtftn.</summary>
    /// <param name="call">The call</param>
    /// <remarks>Should be called before the 'this' arg is inserted, but after other IL args have been inserted.</remarks>
    public void impInsertAsyncArgsForLdvirtftnCall(GenTreeCall call)
    {
        assert(call.IsAsync);

        var arg = NewCallArg.CreateForPrimitive(gtNewNull(), TYP_REF).WithWellKnownArg(WellKnownArg.AsyncContinuation);

        if (Target.TgtArgOrder == Target.ARG_ORDER_R2L)
        {
            _ = call.Args.PushFront(arg);
        }
        else
        {
            _ = call.Args.PushBack(arg);
        }

        impInheritAsyncContextsFromInliner(call);
    }

    public unsafe void impInsertHelperCall(in CORINFO_HELPER_DESC helperInfo)
    {
        assert(helperInfo.helperNum != CORINFO_HELP_UNDEF);

        // TODO-Review:
        // Mark as CSE'able, and hoistable.  Consider marking hoistable unless you're in the inlinee.
        // Also, consider sticking this in the first basic block.
        var call = gtNewHelperCallNode(TYP_VOID, helperInfo.helperNum);
        var callArgs = call.Args;

        ReadOnlySpan<CORINFO_HELPER_ARG> helperArgs = helperInfo.args;

        // Add the arguments
        for (var i = helperInfo.numArgs; i > 0; i--)
        {
            ref readonly var helperArg = ref helperArgs[i - 1];
            var currentArg = null as GenTree;

            switch (helperArg.argType)
            {
                case CORINFO_HELPER_ARG_TYPE_Field:
                {
                    info.compCompHnd->classMustBeLoadedBeforeCodeIsRun(info.compCompHnd->getFieldClass(helperArg.fieldHandle));
                    currentArg = gtNewIconEmbFldHndNode(helperArg.fieldHandle);
                    break;
                }

                case CORINFO_HELPER_ARG_TYPE_Method:
                {
                    info.compCompHnd->methodMustBeLoadedBeforeCodeIsRun(helperArg.methodHandle);
                    currentArg = gtNewIconEmbMethHndNode(helperArg.methodHandle);
                    break;
                }

                case CORINFO_HELPER_ARG_TYPE_Class:
                {
                    info.compCompHnd->classMustBeLoadedBeforeCodeIsRun(helperArg.classHandle);
                    currentArg = gtNewIconEmbClsHndNode(helperArg.classHandle);
                    break;
                }

                case CORINFO_HELPER_ARG_TYPE_Module:
                {
                    currentArg = gtNewIconEmbScpHndNode(helperArg.moduleHandle);
                    break;
                }

                case CORINFO_HELPER_ARG_TYPE_Const:
                {
                    currentArg = gtNewIconNode(TYP_INT, helperArg.constant);
                    break;
                }

                default:
                {
                    NO_WAY("Illegal helper arg type");
                    break;
                }
            }
            _ = callArgs.PushFront(NewCallArg.CreateForPrimitive(currentArg));
        }

        _ = impAppendTree(call, CHECK_SPILL_NONE, impCurStmtDI);
    }

    /// <summary>Insert the given "stmt" before "stmtBefore".</summary>
    /// <param name="stmt">a statement to insert</param>
    /// <param name="stmtBefore">an insertion point to insert "stmt" before</param>
    public void impInsertStmtBefore(Statement stmt, Statement stmtBefore)
    {
        assert(stmt is not null);
        assert(stmtBefore is not null);

        if (stmtBefore == impStmtList)
        {
            impStmtList = stmt;
        }
        else
        {
            var stmtPrev = stmtBefore.PrevStmt;
            assert(stmtPrev is not null);

            stmt.PrevStmt = stmtPrev;
            stmtPrev.NextStmt = stmt;
        }

        stmt.NextStmt = stmtBefore;
        stmtBefore.PrevStmt = stmt;
    }

    public bool impIsAddressInLocal(GenTree tree) => impIsAddressInLocal(tree, out _);

    /// <summary>Check to see if the tree is the address of a local or the address of a field in a local.</summary>
    /// <param name="tree">The tree</param>
    /// <param name="lclVarTree">the local that this points into</param>
    /// <returns>true if it points into a local</returns>
    public bool impIsAddressInLocal(GenTree tree, [NotNullWhen(true)] out GenTreeLclFld? lclVarTree)
    {
        while (tree.Oper is GT_FIELD_ADDR)
        {
            var fieldAddr = tree.AsFieldAddr();

            if (!fieldAddr.IsInstance)
            {
                break;
            }
            tree = fieldAddr.FldObj;
        }

        if (tree.Oper is GT_LCL_ADDR)
        {
            lclVarTree = tree.AsLclFld();
            return true;
        }
        else
        {
            lclVarTree = null;
            return false;
        }
    }

#if FEATURE_READYTORUN
    /// <summary>Checks whether a tree is a cast helper eligible to to be profiled and then optimized with PGO data</summary>
    /// <param name="tree">the tree object to check</param>
    /// <returns>true if the tree is a cast helper eligible to be profiled</returns>
    public bool impIsCastHelperEligibleForClassProbe(GenTree tree)
    {
        if (!opts.IsInstrumented || (JitConfig[ConfigInteger.JitProfileCasts] is not 1))
        {
            return false;
        }

        if (tree.Oper.IsCall)
        {
            var call = tree.AsCall();

            if (call.IsHelperCall())
            {
                switch (call.HelperNum)
                {
                    case CORINFO_HELP_ISINSTANCEOFINTERFACE:
                    case CORINFO_HELP_ISINSTANCEOFARRAY:
                    case CORINFO_HELP_ISINSTANCEOFCLASS:
                    case CORINFO_HELP_ISINSTANCEOFANY:
                    case CORINFO_HELP_CHKCASTINTERFACE:
                    case CORINFO_HELP_CHKCASTARRAY:
                    case CORINFO_HELP_CHKCASTCLASS:
                    case CORINFO_HELP_CHKCASTANY:
                    {
                        return true;
                    }

                    default:
                    {
                        break;
                    }
                }
            }
        }
        return false;
    }
#endif

    public unsafe bool impIsImplicitTailCallCandidate(OPCODE opcode, byte* codeAddrOfNextOpcode, byte* codeEnd, int prefixFlags, bool isRecursive)
    {
#if FEATURE_TAILCALL_OPT
        if (!opts.compTailCallOpt)
        {
            return false;
        }

        if (opts.OptimizationDisabled)
        {
            return false;
        }

        // must not be tail prefixed
        if ((prefixFlags & PREFIX_TAILCALL_EXPLICIT) is not 0)
        {
            return false;
        }

#if !FEATURE_TAILCALL_OPT_SHARED_RETURN
        // the block containing call is marked as BBJ_RETURN
        // We allow shared ret tail call optimization on recursive calls even under
        // !FEATURE_TAILCALL_OPT_SHARED_RETURN.

        assert(compCurBB is not null);

        if (!isRecursive && (compCurBB.Kind is not BBJ_RETURN))
        {
            return false;
        }
#endif

        if (!impIsTailCallILPattern(false, opcode, codeAddrOfNextOpcode, codeEnd, isRecursive))
        {
            // must be call+ret or call+pop+ret
            return false;
        }

        return true;
#else
        return false;
#endif
    }

    /// <summary>check if a tree (created during import) is invariant.</summary>
    /// <param name="tree">The tree</param>
    /// <returns>true if it is invariant</returns>
    /// <remarks>This is a variant of GenTree.IsInvariant that is more suitable for use during import. Unlike that function, this one handles GT_FIELD_ADDR nodes.</remarks>
    public bool impIsInvariant(GenTree tree)
    {
        var oper = tree.Oper;
        return oper.IsConst || impIsAddressInLocal(tree) || (oper is GT_FTN_ADDR);
    }

    /// <summary>Check if a return buffer is of a legal shape.</summary>
    /// <param name="retBuf">The return buffer</param>
    /// <param name="call">The call that is passed the return buffer</param>
    /// <returns>True if it is legal according to ABI and IR invariants.</returns>
    /// <remarks>ABI requires all return buffers to point to stack. Also, we have an IR   invariant for async calls that return buffers must be the address of a local.</remarks>
    public unsafe bool impIsLegalRetBuf(GenTree retBuf, GenTreeCall call)
    {
        if (call.IsAsync)
        {
            // Async calls require LCL_ADDR shape for the retbuf to know where to
            // save the value on resumption.
            if (retBuf.Oper is not GT_LCL_ADDR)
            {
                return false;
            }

            // LCL_ADDR on an implicit byref will turn into LCL_VAR in morph.
            if (lvaIsImplicitByRefLocal(retBuf.AsLclVarCommon().LclNum))
            {
                return false;
            }

            return true;
        }

        // The ABI requires the retbuffer to point to stack.
        return !fgAddrCouldBeHeap(retBuf) || eeIsByrefLike(call.RetClsHnd);
    }

    public bool impIsPrimitive(CorInfoType jitType) => jitType is (>= CORINFO_TYPE_BOOL and <= CORINFO_TYPE_DOUBLE) or CORINFO_TYPE_PTR;

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

    /// <summary>Import the GC.KeepAlive intrinsic call</summary>
    /// <param name="objToKeepAlive">the intrinisic call's argument</param>
    /// <returns>The imported GT_KEEPALIVE or GT_NOP - see description.</returns>
    /// <remarks>Imports the intrinsic as a GT_KEEPALIVE node, and, as an optimization, if the object to keep alive is a GT_BOX, removes its side effects and uses the address of a local (copied from the box's source if needed) as the operand for GT_KEEPALIVE. For the BOX optimization, if the class of the box has no GC fields, a GT_NOP is returned.</remarks>
    public unsafe GenTree impKeepAliveIntrinsic(GenTree objToKeepAlive)
    {
        assert(objToKeepAlive.Type is TYP_REF);

        if (opts.OptimizationEnabled && (objToKeepAlive.Oper is GT_BOX))
        {
            var box = objToKeepAlive.AsBox();

            if (box.IsBoxedValue)
            {
                var boxedClass = lvaGetDesc(box.BoxOp.AsLclVar().LclNum).lvClassHnd;
                var layout = typGetObjLayout(boxedClass);

                if (!layout.HasGCPtr)
                {
                    _ = gtTryRemoveBoxUpstreamEffects(box, BR_REMOVE_AND_NARROW);
                    JITDUMP("\nBOX class has no GC fields, KEEPALIVE is a NOP");

                    return gtNewNothingNode();
                }

                var boxSrc = gtTryRemoveBoxUpstreamEffects(box, BR_REMOVE_BUT_NOT_NARROW);

                if (boxSrc is not null)
                {
                    int boxTempNum;

                    if (boxSrc.Oper is GT_LCL_VAR)
                    {
                        boxTempNum = boxSrc.AsLclVarCommon().LclNum;
                    }
                    else
                    {
                        boxTempNum = lvaGrabTemp(shortLifetime: true, "Temp for the box source");

                        var boxTempStore = gtNewTempStore(boxTempNum, boxSrc);
                        var boxStoreStmt = box.CopyStmtWhenInlinedBoxValue;

                        boxStoreStmt.RootNode = boxTempStore;
                    }

                    JITDUMP($"\nImporting KEEPALIVE(BOX) as KEEPALIVE(LCL_VAR_ADDR V{boxTempNum:D2})");
                    objToKeepAlive = gtNewLclVarAddrNode(TYP_I_IMPL, boxTempNum);
                }
            }
        }

        return gtNewKeepAliveNode(objToKeepAlive);
    }

    /// <summary>Load an argument on the operand stack</summary>
    /// <param name="ilArgNum">the argument index as specified in IL, it will be mapped to the correct lvaTable index</param>
    /// <param name="offset"></param>
    public unsafe void impLoadArg(int ilArgNum, IL_OFFSET offset)
    {
        if (compIsForInlining)
        {
            if (ilArgNum >= info.compArgsCount)
            {
                compInlineResult.NoteFatal(InlineObservation.CALLEE_BAD_ARGUMENT_NUMBER);
                return;
            }

            ref var lclVarInfo = ref impInlineInfo.lclVarInfo[ilArgNum];

            var type = lclVarInfo.lclTypeInfo;
            var tiRetVal = (type is TYP_REF) ? new typeInfo(lclVarInfo.lclTypeHandle) : new typeInfo(type);

            impPushOnStack(impInlineFetchArg(ref impInlineInfo.inlArgInfo[ilArgNum], lclVarInfo), tiRetVal);
        }
        else
        {
            if (ilArgNum >= info.compArgsCount)
            {
                BADCODE("Bad IL");
            }

            // account for possible hidden param
            var lclNum = compMapILargNum(ilArgNum);

            if (lclNum == info.compThisArg)
            {
                lclNum = lvaArg0Var;
            }
            impLoadVar(lclNum, offset);
        }
    }

    /// <summary>Load a local on the operand stack</summary>
    /// <param name="ilLclNum">the local index as specified in IL, it will be mapped to the correct lvaTable index</param>
    /// <param name="offset"></param>
    public unsafe void impLoadLoc(int ilLclNum, IL_OFFSET offset)
    {
        int lclNum;

        if (compIsForInlining)
        {
            if (ilLclNum >= info.compMethodInfo->locals.numArgs)
            {
                compInlineResult.NoteFatal(InlineObservation.CALLEE_BAD_LOCAL_NUMBER);
                return;
            }

            // Have we allocated a temp for this local?
            lclNum = impInlineFetchLocal(ilLclNum, "Inline ldloc first use temp");
        }
        else
        {
            if (ilLclNum >= info.compMethodInfo->locals.numArgs)
            {
                BADCODE("Bad IL");
            }
            lclNum = info.compArgsCount + ilLclNum;
        }

        impLoadVar(lclNum, offset);
    }

    /// <summary>Load a local/argument on the operand stack</summary>
    /// <param name="lclNum">An index into lvaTable *NOT* the arg/lcl index in the IL</param>
    /// <param name="offset"></param>
    public void impLoadVar(int lclNum, IL_OFFSET offset)
    {
        impPushOnStack(impCreateLocalNode(lclNum, (offset)), makeTypeInfoForLocal(lclNum));
    }

    public unsafe GenTree? impLookupToTree(in CORINFO_LOOKUP lookup, GenTreeFlags flags, void* compileTimeHandle)
    {
        if (!lookup.lookupKind.needsRuntimeLookup)
        {
            // No runtime lookup is required.
            // Access is direct or memory-indirect (of a fixed address) reference

            CORINFO_GENERIC_HANDLE handle = null;
            void* pIndirection = null;
            assert(lookup.constLookup.accessType is not IAT_PPVALUE and not IAT_RELPVALUE);

            if (lookup.constLookup.accessType == IAT_VALUE)
            {
                handle = lookup.constLookup.handle;
            }
            else if (lookup.constLookup.accessType == IAT_PVALUE)
            {
                pIndirection = lookup.constLookup.addr;
            }

            var addr = gtNewIconEmbHndNode(handle, pIndirection, flags, compileTimeHandle);

#if DEBUG
            var handleToTrack = (flags is not GTF_ICON_TOKEN_HDL) ? unchecked((nint)(compileTimeHandle)) : 0;

            if (handle is not null)
            {
                addr.AsIntCon().TargetHandle = handleToTrack;
            }
            else
            {
                addr.AsIndir().Op1.AsIntCon().TargetHandle = handleToTrack;
            }
#endif

            return addr;
        }

        if (lookup.lookupKind.runtimeLookupKind is CORINFO_LOOKUP_NOT_SUPPORTED)
        {
            // Runtime does not support inlining of all shapes of runtime lookups
            // Inlining has to be aborted in such a case

            assert(compIsForInlining);
            compInlineResult.NoteFatal(InlineObservation.CALLSITE_GENERIC_DICTIONARY_LOOKUP);

            return null;
        }

        // Need to use dictionary-based access which depends on the typeContext
        // which is only available at runtime, not at compile-time.
        return impRuntimeLookupToTree(lookup, compileTimeHandle);
    }

#if FEATURE_SIMD
    /// <summary>Try to identify if there are contiguous stores from simd field to memory. If there are, then mark the related lclvar so that it won't be promoted.</summary>
    /// <param name="stmt">Input statement node.</param>
    public void impMarkContiguousSimdFieldStores(Statement stmt)
    {
        if (opts.OptimizationDisabled)
        {
            return;
        }

        var expr = stmt.RootNode;

        if (expr.Oper.IsStore && (expr.Type is TYP_FLOAT))
        {
            var curValue = expr.Data;
            var simdBaseType = curValue.Type;
            var srcSimdLclAddr = getSimdStructFromField(curValue, out var index, out var simdSize, true);

            if ((srcSimdLclAddr is null) || (simdBaseType is not TYP_FLOAT))
            {
                fgPreviousCandidateSimdFieldStoreStmt = null;
            }
            else if (index == 0)
            {
                fgPreviousCandidateSimdFieldStoreStmt = stmt;
            }
            else if (fgPreviousCandidateSimdFieldStoreStmt is not null)
            {
                assert(index > 0);

                var curStore = expr;
                var prevStore = fgPreviousCandidateSimdFieldStoreStmt.RootNode;
                var prevValue = prevStore.Data;

                if (!areArgumentsContiguous(prevStore, curStore) || !areArgumentsContiguous(prevValue, curValue))
                {
                    fgPreviousCandidateSimdFieldStoreStmt = null;
                }
                else
                {
                    if (index == (simdSize / simdBaseType.Size - 1))
                    {
                        // Successfully found the pattern, mark the lclvar as UsedInSimdIntrinsic
                        setLclRelatedToSimdIntrinsic(srcSimdLclAddr);

                        if (curStore.Oper is GT_STOREIND)
                        {
                            var indirAddr = curStore.AsIndir().Addr;

                            if (indirAddr.Oper is GT_FIELD_ADDR)
                            {
                                var fieldAddr = indirAddr.AsFieldAddr();

                                if (fieldAddr.IsInstance)
                                {
                                    var fldObj = fieldAddr.FldObj;

                                    if (fldObj.IsLclVarAddr && varTypeIsStruct(lvaGetDesc(fldObj.AsLclFld().LclNum).Type))
                                    {
                                        setLclRelatedToSimdIntrinsic(fldObj.AsLclVarCommon());
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        fgPreviousCandidateSimdFieldStoreStmt = stmt;
                    }
                }
            }
        }
        else
        {
            fgPreviousCandidateSimdFieldStoreStmt = null;
        }
    }
#endif

    /// <summary>determine if this call can be subsequently inlined</summary>
    /// <param name="call">call under scrutiny</param>
    /// <param name="exactContextHnd">context handle for inlining</param>
    /// <param name="exactContextNeedsRuntimeLookup">true if context required runtime lookup</param>
    /// <param name="callInfo">call info from VM</param>
    /// <param name="inlinersContext">the inliner's context</param>
    /// <remarks>Mostly a wrapper for impMarkInlineCandidateHelper that also undoes guarded devirtualization for virtual calls where the method we'd devirtualize to cannot be inlined.</remarks>
    public unsafe void impMarkInlineCandidate(GenTreeCall call, CORINFO_CONTEXT_HANDLE exactContextHnd, bool exactContextNeedsRuntimeLookup, in CORINFO_CALL_INFO callInfo, InlineContext inlinersContext)
    {
        if (!opts.OptEnabled(CLFLG_INLINING))
        {
            assert(!compIsForInlining);
            return;
        }

        // Call might not have an inline candidate info yet (will be set by impMarkInlineCandidateHelper)
        // so we assume there is always a least one candidate:

        if (call.IsGuardedDevirtualizationCandidate)
        {
            assert(call.InlineCandidatesCount > 0);

            for (byte candidateId = 0; candidateId < call.InlineCandidatesCount; candidateId++)
            {
                var inlineResult = new InlineResult(this, call, stmt: null, "impMarkInlineCandidate for GDV");

                // Do the actual evaluation
                impMarkInlineCandidateHelper(call, candidateId, exactContextHnd, exactContextNeedsRuntimeLookup, callInfo, inlinersContext, inlineResult);

                // Ignore non-inlineable candidates
                // TODO: Consider keeping them to just devirtualize without inlining, at least for interface
                // calls on NativeAOT, but that requires more changes elsewhere too.
                if (!inlineResult.IsCandidate)
                {
                    call.RemoveGdvCandidateInfo(this, candidateId);
                    candidateId--;
                }
            }

            // None of the candidates made it, make sure the call is no longer marked as "has inline info"
            if (call.InlineCandidatesCount is 0)
            {
                assert(!call.IsInlineCandidate);
                assert(!call.IsGuardedDevirtualizationCandidate);
            }
        }
        else
        {
            var candidatesCount = call.InlineCandidatesCount;
            assert(candidatesCount <= 1);

            var inlineResult = new InlineResult(this, call, null, "impMarkInlineCandidate");
            impMarkInlineCandidateHelper(call, 0, exactContextHnd, exactContextNeedsRuntimeLookup, callInfo, inlinersContext, inlineResult);
        }

        // If this call is an inline candidate or is not a guarded devirtualization
        // candidate, we're done.
        if (call.IsInlineCandidate || !call.IsGuardedDevirtualizationCandidate)
        {
            return;
        }

        // If we can't inline the call we'd guardedly devirtualize to,
        // we undo the guarded devirtualization, as the benefit from
        // just guarded devirtualization alone is likely not worth the
        // extra jit time and code size.
        //
        // TODO: it is possibly interesting to allow this, but requires
        // fixes elsewhere too...
        JITDUMP($"Revoking guarded devirtualization candidacy for call [{call.TreeId}]: target method can't be inlined\n");

        call.ClearInlineInfo();
    }

    /// <summary>determine if this call can be subsequently inlined</summary>
    /// <param name="call">call under scrutiny</param>
    /// <param name="candidateIndex">index of the inline candidate to evaluate</param>
    /// <param name="exactContextHnd">context handle for inlining</param>
    /// <param name="exactContextNeedsRuntimeLookup">true if context required runtime lookup</param>
    /// <param name="callInfo">call info from VM</param>
    /// <param name="inlinersContext">the inliner's context</param>
    /// <param name="inlineResult"></param>
    /// <remarks>
    ///   <para>If callNode is an inline candidate, this method sets the flag GTF_CALL_INLINE_CANDIDATE, and ensures that helper methods have filled in the associated InlineCandidateInfo.</para>
    ///   <para>If callNode is not an inline candidate, and the reason is method may be marked as "noinline" to short-circuit any future assessments of calls to this method.</para>
    /// </remarks>
    public unsafe void impMarkInlineCandidateHelper(GenTreeCall call, byte candidateIndex, CORINFO_CONTEXT_HANDLE exactContextHnd, bool exactContextNeedsRuntimeLookup, in CORINFO_CALL_INFO callInfo, InlineContext inlinersContext, InlineResult inlineResult)
    {
        assert(compCurBB is not null);

        var inlineStrategy = impInlineRoot._inlineStrategy;
        assert(inlineStrategy is not null);

        // Let the strategy know there's another call
        inlineStrategy.NoteCall();

        assert(opts.OptEnabled(CLFLG_INLINING));

        // Don't inline if not optimizing root method
        if (opts.compDbgCode)
        {
            inlineResult.NoteFatal(InlineObservation.CALLER_DEBUG_CODEGEN);
            return;
        }

        // Don't inline if inlining into this method is disabled.
        if (inlineStrategy.IsInliningDisabled())
        {
            inlineResult.NoteFatal(InlineObservation.CALLER_IS_JIT_NOINLINE);
            return;
        }

        // Don't inline into callers that use the NextCallReturnAddress intrinsic.
        if (info.compHasNextCallRetAddr)
        {
            inlineResult.NoteFatal(InlineObservation.CALLER_USES_NEXT_CALL_RET_ADDR);
            return;
        }

        // Inlining candidate determination needs to honor only IL tail prefix.
        // Inlining takes precedence over implicit tail call optimization (if the call is not directly recursive).
        if (call.IsTailPrefixedCall)
        {
            inlineResult.NoteFatal(InlineObservation.CALLSITE_EXPLICIT_TAIL_PREFIX);
            return;
        }

        // Delegate Invoke method doesn't have a body and gets special cased instead.
        // Don't even bother trying to inline it.
        if (call.IsDelegateInvoke && !call.IsGuardedDevirtualizationCandidate)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_NO_BODY);
            return;
        }

        // Tail recursion elimination takes precedence over inlining.
        // TODO: We may want to do some of the additional checks from fgMorphCall
        // here to reduce the chance we don't inline a call that won't be optimized
        // as a fast tail call or turned into a loop.
        if (gtIsRecursiveCall(call) && call.IsImplicitTailCall)
        {
            inlineResult.NoteFatal(InlineObservation.CALLSITE_IMPLICIT_REC_TAIL_CALL);
            return;
        }

        if (call.IsVirtual && !call.IsGuardedDevirtualizationCandidate)
        {
            // Allow guarded devirt calls to be treated as inline candidates, but reject all other virtual calls.
            inlineResult.NoteFatal(InlineObservation.CALLSITE_IS_NOT_DIRECT);
            return;
        }

        if (call.IsHelperCall())
        {
            // Ignore helper calls
            assert(!call.IsGuardedDevirtualizationCandidate);

            inlineResult.NoteFatal(InlineObservation.CALLSITE_IS_CALL_TO_HELPER);
            return;
        }

        if (call._callType is CT_INDIRECT)
        {
            // Ignore indirect calls, unless they are indirect virtual stub calls with profile info.

            if (!call.IsGuardedDevirtualizationCandidate)
            {
                inlineResult.NoteFatal(InlineObservation.CALLSITE_IS_NOT_DIRECT_MANAGED);
                return;
            }
            else
            {
                assert(call.IsVirtualStub);
            }
        }

        // The inliner gets confused when the unmanaged convention reverses arg order (like x86).
        // Just suppress for all targets for now.

        if (call.UnmanagedCallConv != CorInfoCallConvExtension.Managed)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_UNMANAGED_CALLCONV);
            return;
        }

        // I removed the check for BBJ_THROW.  BBJ_THROW is usually marked as rarely run.  This more or less
        // restricts the inliner to non-expanding inlines.  I removed the check to allow for non-expanding
        // inlining in throw blocks.  I should consider the same thing for catch and filter regions.

        CORINFO_METHOD_HANDLE fncHandle;
        CorInfoFlag methAttr;

        if (call.IsGuardedDevirtualizationCandidate)
        {
            var gdvCandidate = call.GetGdvCandidateInfo(candidateIndex);

            if (gdvCandidate.guardedMethodUnboxedEntryHandle is not null)
            {
                fncHandle = gdvCandidate.guardedMethodUnboxedEntryHandle;
            }
            else
            {
                fncHandle = gdvCandidate.guardedMethodHandle;
            }
            exactContextHnd = gdvCandidate.exactContextHandle;

            methAttr = info.compCompHnd->getMethodAttribs(fncHandle);
        }
        else
        {
            fncHandle = call._callMethHnd;

            if (fncHandle == callInfo.hMethod)
            {
                // Reuse method flags from the original callInfo if possible
                methAttr = callInfo.methodFlags;
            }
            else
            {
                methAttr = info.compCompHnd->getMethodAttribs(fncHandle);
            }
        }

#if DEBUG
        if (compStressCompile(STRESS_FORCE_INLINE, 0))
        {
            methAttr |= CORINFO_FLG_FORCEINLINE;
        }
#endif

        if (compDoAggressiveInlining)
        {
            // Check for DOTNET_AggressiveInlining
            methAttr |= CORINFO_FLG_FORCEINLINE;
        }

        if ((methAttr & CORINFO_FLG_FORCEINLINE) is 0)
        {
            // Don't bother inline blocks that are in the filter region

            if (bbInCatchHandlerBBRange(compCurBB))
            {
#if DEBUG
                if (verbose)
                {
                    jitprintf("\nWill not inline blocks that are in the catch handler region\n");
                }
#endif

                inlineResult.NoteFatal(InlineObservation.CALLSITE_IS_WITHIN_CATCH);
                return;
            }

            if (bbInFilterBBRange(compCurBB))
            {
#if DEBUG
                if (verbose)
                {
                    jitprintf("\nWill not inline blocks that are in the filter region\n");
                }
#endif

                inlineResult.NoteFatal(InlineObservation.CALLSITE_IS_WITHIN_FILTER);
                return;
            }
        }

        // Check if we tried to inline this method before

        if ((methAttr & CORINFO_FLG_DONT_INLINE) is not 0)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_IS_NOINLINE);
            return;
        }

        // Cannot inline synchronized methods

        if ((methAttr & CORINFO_FLG_SYNCH) is not 0)
        {
            inlineResult.NoteFatal(InlineObservation.CALLEE_IS_SYNCHRONIZED);
            return;
        }

        // Check legality of PInvoke callsite (for inlining of marshalling code)

        if ((methAttr & CORINFO_FLG_PINVOKE) is not 0)
        {
            if (!impCanPInvokeInlineCallSite(compCurBB))
            {
                inlineResult.NoteFatal(InlineObservation.CALLSITE_PINVOKE_EH);
                return;
            }
        }

        impCheckCanInline(call, candidateIndex, fncHandle, methAttr, exactContextHnd, inlinersContext, out var inlineCandidateInfo, inlineResult);

        if (inlineResult.IsFailure)
        {
            return;
        }

        // The new value should not be null.
        assert(inlineCandidateInfo is not null);

        if (inlineCandidateInfo.methInfo.EHcount > 0)
        {
            if (bbInFilterBBRange(compCurBB))
            {
                // We cannot inline methods with EH into filter clauses, even if marked as aggressive inline
                inlineResult.NoteFatal(InlineObservation.CALLSITE_IS_WITHIN_FILTER);
                return;
            }

            if ((methAttr & CORINFO_FLG_PINVOKE) is not 0)
            {
                // Do not inline pinvoke stubs with EH.
                inlineResult.NoteFatal(InlineObservation.CALLEE_HAS_EH);
                return;
            }
        }

        // The old value should be null OR this call should be a guarded devirtualization candidate.
        assert(call.IsGuardedDevirtualizationCandidate || (call.SingleInlineCandidateInfo is null));

        inlineCandidateInfo.exactContextNeedsRuntimeLookup = exactContextNeedsRuntimeLookup;

        if (compIsForInlining && call.CanTailCall && (impInlineInfo.inlineCandidateInfo.preexistingSpillTemp is not BAD_VAR_NUM))
        {
            // If we're in an inlinee compiler, and have a return spill temp, and this inline candidate is also a tail call candidate, it can use the same return spill temp.
            inlineCandidateInfo.preexistingSpillTemp = impInlineInfo.inlineCandidateInfo.preexistingSpillTemp;
            JITDUMP($"Inline candidate [{call.TreeId:D6}] can share spill temp V{inlineCandidateInfo.preexistingSpillTemp:D2}\n");
        }

        if (call.IsGuardedDevirtualizationCandidate)
        {
            assert(call.GetGdvCandidateInfo(candidateIndex) == inlineCandidateInfo);
            call.Flags |= GTF_CALL_INLINE_CANDIDATE;
        }
        else
        {
            assert(candidateIndex is 0);
            call.SingleInlineCandidateInfo = inlineCandidateInfo;
        }

        // Let the strategy know there's another candidate.
        inlineStrategy.NoteCandidate();

        // Since we're not actually inlining yet, and this call site is still just an inline candidate, there's nothing to report.
        inlineResult.Result = INLINE_CHECK_CAN_INLINE_SUCCESS;
    }

    /// <summary>Match IL to determine whether an isinst IL instruction is used for a simple boolean check.</summary>
    /// <param name="codeAddr">IL after the isinst</param>
    /// <param name="codeEndp">End of IL code stream</param>
    /// <param name="consumed">If this function returns true, set to the number of IL bytes to consume to create the boolean check</param>
    /// <returns>True if the isinst is used as a boolean check; otherwise false.</returns>
    /// <remarks>The isinst instruction is specced to return the original object refernce when the type check succeeds. However, in many cases it is used strictly as a boolean type check (if (x is Foo) for example). In those cases it is beneficial for the JIT if we avoid creating QMARKs returning the object itself which may disable some important optimization in some cases.</remarks>
    public unsafe bool impMatchIsInstBooleanConversion(byte* codeAddr, byte* codeEndp, out byte consumed)
    {
        var nextOpcode = impGetNonPrefixOpcode(codeAddr, codeEndp);
        consumed = 0;

        switch (nextOpcode)
        {
            case CEE_BRFALSE:
            case CEE_BRFALSE_S:
            case CEE_BRTRUE:
            case CEE_BRTRUE_S:
            {
                // BRFALSE/BRTRUE importation are expected to transparently handle
                // that the created tree is a TYP_INT instead of TYP_REF, so we do
                // not consume them here.
                return true;
            }

            case CEE_LDNULL:
            {
                nextOpcode = impGetNonPrefixOpcode(codeAddr + 1, codeEndp);

                if (nextOpcode == CEE_CGT_UN)
                {
                    consumed = 3;
                    return true;
                }
                return false;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Check if a method call starts an a task await pattern that can be optimized for runtime async</summary>
    /// <param name="codeAddr">IL after call[virt]     NB: pointing at unconsumed token.</param>
    /// <param name="codeEndp">End of IL code stream</param>
    /// <param name="configVal">set to 0 or 1, accordingly, if we saw ConfigureAwait(0|1)</param>
    /// <param name="awaitOffset">IL offset of await call</param>
    /// <returns>null if we did not recognise an Await pattern that we can optimize; otherwise returns position at the end of the Await pattern with one token left unconsumed.</returns>
    public unsafe byte* impMatchTaskAwaitPattern(byte* codeAddr, byte* codeEndp, out int configVal, out IL_OFFSET awaitOffset)
    {
        // If we see the following code pattern in runtime async methods:
        //
        //    call[virt] <Method>
        //    [ OPTIONAL ]
        //    {
        //       [ OPTIONAL ]
        //       {
        //         stloc X;
        //         ldloca X
        //       }
        //       ldc.i4.0 / ldc.i4.1
        //       call[virt] <ConfigureAwait>
        //    }
        //    call       <Await>
        //
        // We emit an eqivalent of:
        //
        //    call[virt] <RtMethod>
        //
        //    where "RtMethod" is the runtime-async counterpart of a Task-returning method.
        //
        //  NOTE: we could potentially check if Method is not a thunk and, in cases when we can tell,
        //        bypass this optimization. Otherwise in a non-thunk case we would be
        //        replacing the pattern with a call to a thunk, which contains roughly the same code.

        var nextOpcode = codeAddr + sizeof(mdToken);

        configVal = -1;
        awaitOffset = BAD_IL_OFFSET;

        // There must be enough space after ldc for {call + tk + call + tk}
        if ((nextOpcode + (2 * (1 + sizeof(mdToken)))) < codeEndp)
        {
            // ConfigureAwait on a ValueTask will start with stloc/ldloca.
            // The longest encoding should fit in the length we asked for above.
            var maybeStLoc = (OPCODE)(nextOpcode[0]);

            var nextTmp = nextOpcode + 1;
            var stlocNum = -1;

            switch (maybeStLoc)
            {
                case CEE_STLOC_0:
                {
                    stlocNum = 0;
                    break;
                }

                case CEE_STLOC_1:
                {
                    stlocNum = 1;
                    break;
                }

                case CEE_STLOC_2:
                {
                    stlocNum = 2;
                    break;
                }

                case CEE_STLOC_3:
                {
                    stlocNum = 3;
                    break;
                }

                case CEE_STLOC_S:
                {
                    stlocNum = nextTmp[0];
                    nextTmp++;
                    break;
                }

                case CEE_PREFIX1:
                {
                    maybeStLoc = (OPCODE)(0x0100 + nextTmp[0]);
                    nextTmp++;

                    if (maybeStLoc is CEE_STLOC)
                    {
                        stlocNum = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(nextTmp, sizeof(ushort)));
                        nextTmp += 2;
                    }
                    break;
                }
            }

            // if it was a stloc, check for matching ldloca
            if (stlocNum != -1)
            {
                var maybeLdLoca = (OPCODE)(nextTmp[0]);
                nextTmp++;

                var ldlocaNum = -1;

                switch (maybeLdLoca)
                {
                    case CEE_LDLOCA_S:
                    {
                        ldlocaNum = nextTmp[0];
                        nextTmp++;
                        break;
                    }

                    case CEE_PREFIX1:
                    {
                        maybeLdLoca = (OPCODE)(0x0100 + nextTmp[0]);
                        nextTmp++;

                        if (maybeLdLoca is CEE_LDLOCA)
                        {
                            ldlocaNum = BinaryPrimitives.ReadUInt16LittleEndian(new ReadOnlySpan<byte>(nextTmp, sizeof(ushort)));
                            nextTmp += 2;
                        }
                        break;
                    }
                }

                // no ldloca or locals did not match, this can't be await pattern
                if (stlocNum != ldlocaNum)
                {
                    return null;
                }

                // locals match, but no space for ConfigureAwait call, this can't be await pattern
                if ((nextTmp + (2 * (1 + sizeof(mdToken)))) >= codeEndp)
                {
                    return null;
                }

                nextOpcode = nextTmp;
            }

            var nextOp = (OPCODE)(nextOpcode[0]);
            var nextNextOp = (OPCODE)(nextOpcode[1]);

            if ((nextOp is not CEE_LDC_I4_0 and not CEE_LDC_I4_1) || (nextNextOp is not CEE_CALL and not CEE_CALLVIRT))
            {
                if (stlocNum != -1)
                {
                    // we had stloc/ldloca, we must see ConfigAwait
                    return null;
                }
            }
            else
            {

                // check if the token after {ldc, call[virt]} is ConfigAwait
                impResolveToken(nextOpcode + 2, out var nextCallTok, CORINFO_TOKENKIND_Method);

                if (!eeIsIntrinsic(nextCallTok.hMethod) || (lookupNamedIntrinsic(nextCallTok.hMethod) is not NI_System_Threading_Tasks_Task_ConfigureAwait))
                {
                    if (stlocNum != -1)
                    {
                        // we had stloc/ldloca, we must see ConfigAwait
                        return null;
                    }
                }
                else
                {

                    configVal = (nextOp == CEE_LDC_I4_0) ? 0 : 1;
                    // skip {ldc; call; <ConfigureAwait>}
                    nextOpcode += 1 + 1 + sizeof(mdToken);
                }
            }
        }

        if (((nextOpcode + sizeof(mdToken)) < codeEndp) && ((OPCODE)(nextOpcode[0]) == CEE_CALL))
        {
            // resolve the next token
            impResolveToken(nextOpcode + 1, out var nextCallTok, CORINFO_TOKENKIND_Method);

            // check if it is an Await intrinsic
            if (eeIsIntrinsic(nextCallTok.hMethod) && lookupNamedIntrinsic(nextCallTok.hMethod) == NI_System_Runtime_CompilerServices_AsyncHelpers_Await)
            {
                awaitOffset = (IL_OFFSET)(nextOpcode - info.compCode);

                // yes, this is an Await
                // Consume the call opcode, but not the token.
                // The call importer always consumes one token before moving to the next opcode.
                return nextOpcode + 1;
            }
        }
        return null;
    }

    public unsafe GenTree? impMathIntrinsic(CORINFO_METHOD_HANDLE method, in CORINFO_SIG_INFO sigInfo, in CORINFO_CONST_LOOKUP entryPoint, var_types callType, NamedIntrinsic intrinsicName, bool tailCall, out bool isSpecial)
    {
        assert(callType is not TYP_STRUCT);
        assert(IsMathIntrinsic(intrinsicName));

        isSpecial = false;

        var op1 = null as GenTree;
        var op2 = null as GenTree;

        var isIntrinsicImplementedByUserCall = IsIntrinsicImplementedByUserCall(intrinsicName);

        if (isIntrinsicImplementedByUserCall)
        {
#if TARGET_XARCH
            // We want to track math intrinsics implemented as user calls as special
            // to ensure we don't lose track of the fact it will call into native code
            //
            // This is used on xarch to track that it may need vzeroupper inserted to
            // avoid the perf penalty on some hardware.

            isSpecial = true;
#endif
        }

#if !TARGET_X86
        // Intrinsics that are not implemented directly by target instructions will
        // be re-materialized as users calls in rationalizer. For prefixed tail calls,
        // don't do this optimization, because
        //  a) For back compatibility reasons on desktop .NET Framework 4.6 / 4.6.1
        //  b) It will be non-trivial task or too late to re-materialize a surviving
        //     tail prefixed GT_INTRINSIC as tail call in rationalizer.
        if (!isIntrinsicImplementedByUserCall || !tailCall)
#else
        // On x86 RyuJIT, importing intrinsics that are implemented as user calls can cause incorrect calculation
        // of the depth of the stack if these intrinsics are used as arguments to another call. This causes bad
        // code generation for certain EH constructs.
        if (!isIntrinsicImplementedByUserCall)
#endif
        {
            var arg = sigInfo.args;

            switch (sigInfo.numArgs)
            {
                case 1:
                    assert(eeGetArgType(arg, sigInfo) == callType);

                    op1 = impPopStack().val;
                    op1 = impImplicitR4orR8Cast(op1, callType);
                    op1 = new GenTreeIntrinsic(callType.ActualType, op1, intrinsicName, method) {
                        EntryPoint = entryPoint,
                    };
                    break;

                case 2:
                    assert(eeGetArgType(arg, sigInfo) == callType);
#if DEBUG
                    arg = info.compCompHnd->getArgNext(arg);
#endif
                    assert(eeGetArgType(arg, sigInfo) == callType);

                    op2 = impPopStack().val;
                    op1 = impPopStack().val;

                    op1 = impImplicitR4orR8Cast(op1, callType);
                    op2 = impImplicitR4orR8Cast(op2, callType);

                    op1 = new GenTreeIntrinsic(callType.ActualType, op1, op2, intrinsicName, method) {
                        EntryPoint = entryPoint,
                    };
                    break;

                default:
                {
                    NO_WAY("Unsupported number of args for Math Intrinsic");
                    break;
                }
            }

            if (isIntrinsicImplementedByUserCall)
            {
                op1.Flags |= GTF_CALL;
            }
        }
        return op1;
    }

    public unsafe GenTree impMethodPointer(in CORINFO_CALL_INFO callInfo)
    {
        var op1 = null as GenTree;

        switch (callInfo.kind)
        {
            case CORINFO_CALL:
            {
                var fptrVal = gtNewFptrValNode(TYP_I_IMPL, callInfo.hMethod);

#if FEATURE_READYTORUN
                if (IsAot)
                {
                    fptrVal.EntryPoint = callInfo.codePointerLookup.constLookup;
                }
#endif
                op1 = fptrVal;
                break;
            }

            case CORINFO_CALL_CODE_POINTER:
            {
                op1 = impLookupToTree(callInfo.codePointerLookup, GTF_ICON_FTN_ADDR, callInfo.hMethod);
                assert(op1 is not null);
                break;
            }

            default:
            {
                NO_WAY("unknown call kind");
                break;
            }
        }

        return op1;
    }

    /// <summary>We don't create any GenTree (excluding spills) for a branch.</summary>
    /// <remarks>For debugging info, we need a placeholder so that we can note the IL offset in gtStmt.gtStmtOffs. So append an empty statement.</remarks>
    public void impNoteBranchOffs()
    {
        if (opts.compDbgCode)
        {
            _ = impAppendTree(gtNewNothingNode(), CHECK_SPILL_NONE, impCurStmtDI);
        }
    }

#if DEBUG
    /// <summary>Remember the instr offset for the statements</summary>
    /// <remarks>
    ///   <para>When we do impAppendTree(tree), we can't set stmt->SetLastILOffset(impCurOpcOffs), if the append was done because of a partial stack spill, as some of the trees corresponding to code up to impCurOpcOffs might still be sitting on the stack.</para>
    ///   <para>So we delay calling of SetLastILOffset() until impNoteLastILoffs().</para>
    ///   <para>This should be called when an opcode finally/explicitly causes impAppendTree(tree) to be called (as opposed to being called because of a spill caused by the opcode)</para>
    /// </remarks>
    public void impNoteLastILoffs()
    {
        if (impLastILoffsStmt is null)
        {
            // We should have added a statement for the current basic block
            // Is this assert correct ?

            assert(impLastStmt is not null);
            impLastStmt.LastILOffset = compIsForInlining ? BAD_IL_OFFSET : impCurOpcOffs;
        }
        else
        {
            impLastILoffsStmt.LastILOffset = compIsForInlining ? BAD_IL_OFFSET : impCurOpcOffs;
            impLastILoffsStmt = null;
        }
    }
#endif

    /// <summary>Normalize a struct call argument</summary>
    /// <param name="structVal">The node to normalize</param>
    /// <param name="curLevel">The current stack level</param>
    /// <returns>The normalized "structVal"</returns>
    /// <remarks>Spills call-like STRUCT arguments to temporaries. "Unwraps" commas.</remarks>
    public GenTree impNormStructVal(GenTree structVal, int curLevel)
    {
        assert(varTypeIsStruct(structVal.Type));
        var structType = structVal.Type;

        switch (structVal.Oper)
        {
            case GT_CALL:
            case GT_RET_EXPR:
            {
                var lclNum = lvaGrabTemp(shortLifetime: true, "spilled call-like call argument");
                impStoreToTemp(lclNum, structVal, curLevel);

                // The structVal is now the temp itself
                structVal = gtNewLclvNode(structType, lclNum);
                break;
            }

            case GT_COMMA:
            {
                var blockNode = structVal.AsOp().Op2;
                assert(blockNode.Type == structType);

                // Is this GT_COMMA(op1, GT_COMMA())?
                var parent = structVal;

                // Find the last node in the comma chain.
                while (blockNode.Oper is GT_COMMA)
                {
                    assert(blockNode.Type == structType);
                    parent = blockNode;
                    blockNode = blockNode.AsOp().Op2;
                }

                if (blockNode.Oper.IsBlk)
                {
                    // Sink the GT_COMMA below the blockNode addr.
                    // That is GT_COMMA(op1, op2=blockNode) is transformed into
                    // blockNode(GT_COMMA(TYP_BYREF, op1, op2's op1)).
                    //
                    // In case of a chained GT_COMMA case, we sink the last
                    // GT_COMMA below the blockNode addr.

                    var blockNodeAddr = blockNode.AsOp().Op1;
                    assert(blockNodeAddr.Type is TYP_BYREF or TYP_I_IMPL);

                    var commaNode = parent.AsOp();

                    commaNode.Type = blockNodeAddr.Type;
                    commaNode.Op2 = blockNodeAddr;

                    blockNode.AsOp().Op1 = commaNode;
                    blockNode.AddAllEffectsFlags(commaNode);

                    if (parent == structVal)
                    {
                        structVal = blockNode;
                    }
                }
                break;
            }

            default:
            {
                break;
            }
        }

        return structVal;
    }

    private static bool impOpcodeIsCallOpcode(OPCODE opcode)
        => opcode is CEE_CALL or CEE_CALLI or CEE_CALLVIRT;

    private static bool impOpcodeIsCallSiteBoundary(OPCODE opcode)
        => impOpcodeIsCallOpcode(opcode) || opcode is CEE_JMP or CEE_NEWOBJ or CEE_NEWARR;

    /// <summary>attempt to resolve a cast when jitting</summary>
    /// <param name="op1">value to cast</param>
    /// <param name="pResolvedToken">resolved token for type to cast to</param>
    /// <param name="isCastClass">true if this is a castclass, false if isinst</param>
    /// <returns>tree representing optimized cast, or null if no optimization possible</returns>
    public unsafe GenTree? impOptimizeCastClassOrIsInst(GenTree op1, in CORINFO_RESOLVED_TOKEN pResolvedToken, bool isCastClass)
    {
        assert(op1.Type is TYP_REF);

        // Don't optimize for minopts or debug codegen.
        if (opts.OptimizationDisabled)
        {
            return null;
        }

        var toClass = pResolvedToken.hClass;

        if (info.compCompHnd->getExactClasses(toClass, maxExactClasses: 0, exactClsRet: null) is 0)
        {
            JITDUMP($"\nClass {dspPtr(toClass)} ({eeGetClassName(toClass)}) can never be allocated\n");

            if (!isCastClass)
            {
                JITDUMP("Cast will fail, optimizing to return null\n");

                // If the cast was fed by a box, we can remove that too.
                if (op1.Oper is GT_BOX)
                {
                    var box = op1.AsBox();

                    if (box.IsBoxedValue)
                    {
                        JITDUMP("Also removing upstream box\n");
                        _ = gtTryRemoveBoxUpstreamEffects(box);
                    }
                }

                if (gtTreeHasSideEffects(op1, GTF_SIDE_EFFECT))
                {
                    _ = impAppendTree(op1, CHECK_SPILL_ALL, impCurStmtDI);
                }
                return gtNewNull();
            }

            JITDUMP("Cast will always throw, but not optimizing yet\n");
        }

        // See what we know about the type of the object being cast.
        var fromClass = gtGetClassHandle(op1, out var isExact, out var isNonNull);

        if (fromClass is not null)
        {
            JITDUMP($"\nConsidering optimization of {(isCastClass ? "castclass" : "isinst")} from {(isExact ? "exact " : "")}{dspPtr(fromClass)} ({eeGetClassName(fromClass)}) to {dspPtr(toClass)} ({eeGetClassName(toClass)})\n");

            // Perhaps we know if the cast will succeed or fail.
            var castResult = info.compCompHnd->compareTypesForCast(fromClass, toClass);

            if (castResult == TypeCompareState.Must)
            {
                // Cast will succeed, result is simply op1.
                JITDUMP("Cast will succeed, optimizing to simply return input\n");
                return op1;
            }
            else if (castResult == TypeCompareState.MustNot)
            {
                // See if we can sharpen exactness by looking for final classes
                if (!isExact)
                {
                    isExact = info.compCompHnd->isExactType(fromClass);
                }

                // Cast to exact type will fail. Handle case where we have
                // an exact type (that is, fromClass is not a subtype)
                // and we're not going to throw on failure.
                if (isExact && !isCastClass)
                {
                    JITDUMP("Cast will fail, optimizing to return null\n");

                    // If the cast was fed by a box, we can remove that too.
                    if (op1.Oper is GT_BOX)
                    {
                        var box = op1.AsBox();

                        if (box.IsBoxedValue)
                        {
                            JITDUMP("Also removing upstream box\n");
                            _ = gtTryRemoveBoxUpstreamEffects(box);
                        }
                    }

                    if (gtTreeHasSideEffects(op1, GTF_SIDE_EFFECT))
                    {
                        _ = impAppendTree(op1, CHECK_SPILL_ALL, impCurStmtDI);
                    }
                    return gtNewNull();
                }
                else if (isExact)
                {
                    JITDUMP("Not optimizing failing castclass (yet)\n");
                }
                else
                {
                    JITDUMP("Can't optimize since fromClass is inexact\n");
                }
            }
            else
            {
                JITDUMP("Result of cast unknown, must generate runtime test\n");
            }
        }
        else
        {
            JITDUMP("\nCan't optimize since fromClass is unknown\n");
        }
        return null;
    }

    public GenTree? impParentClassTokenToHandle(in CORINFO_RESOLVED_TOKEN resolvedToken, bool mustRestoreHandle = false)
    {
        return impTokenToHandle(resolvedToken, out _, mustRestoreHandle, importParent: true);
    }

    public GenTree? impParentClassTokenToHandle(in CORINFO_RESOLVED_TOKEN resolvedToken, out bool runtimeLookup, bool mustRestoreHandle = false)
    {
        return impTokenToHandle(resolvedToken, out runtimeLookup, mustRestoreHandle, importParent: true);
    }

#if DEBUG
    /// <summary>Spill the stack and insert IR that poisons all implicit byrefs.</summary>
    /// <remarks>The memory pointed to by implicit byrefs is owned by the callee but usually exists on the caller's frame (or on the heap for some reflection invoke scenarios). This function helps catch situations where the caller reads from the memory after the invocation, for example due to a bug in the JIT's own last-use copy elision for implicit byrefs.</remarks>
    public void impPoisonImplicitByrefsBeforeReturn()
    {
        var spilled = false;

        for (var  lclNum = 0; lclNum < info.compArgsCount; lclNum++)
        {
            if (!lvaIsImplicitByRefLocal(lclNum))
            {
                continue;
            }

            compPoisoningAnyImplicitByrefs = true;

            if (!spilled)
            {
                for (var level = 0; level < stackState.esStackDepth; level++)
                {
                    _ = impSpillStackEntry(level, BAD_VAR_NUM, assertOnRecursion: true, "Stress poisoning byrefs before return");
                }
                spilled = true;
            }

            ref var varDsc = ref lvaGetDesc(lclNum);
            // Be conservative about this local to ensure we do not eliminate the poisoning.
            lvaSetVarAddrExposed(lclNum, AddressExposedReason.STRESS_POISON_IMPLICIT_BYREFS);

            assert(varTypeIsStruct(varDsc.Type));

            var layout = varDsc.Layout;
            assert(layout is not null);

            ushort startOffs = 0;
            var numSlots = layout.SlotCount;

            for (ushort curSlot = 0; curSlot < numSlots; curSlot++)
            {
                var offs = (ushort)(curSlot * TARGET_POINTER_SIZE);
                var gcPtr = layout.GetGCPtrType(curSlot);

                if (!varTypeIsGC(gcPtr))
                {
                    continue;
                }

                PoisonBlock(this, lclNum, startOffs, offs - startOffs);

                var zeroField = gtNewStoreLclFldNode(gcPtr, lclNum, offs, gtNewZeroConNode(gcPtr));
                _ = impAppendTree(zeroField, CHECK_SPILL_NONE, new DebugInfo());

                startOffs = (ushort)(offs + TARGET_POINTER_SIZE);
            }

            assert(startOffs <= lvaLclExactSize(lclNum));
            PoisonBlock(this, lclNum, startOffs, lvaLclExactSize(lclNum) - startOffs);
        }

        static void PoisonBlock(Compiler compiler, int lclNum, ushort start, int count)
        {
            if (count <= 0)
            {
                return;
            }

            var initValue = compiler.gtNewUnaryNode(GT_INIT_VAL, TYP_INT, compiler.gtNewIconNode(TYP_INT, 0xCD));
            var store = compiler.gtNewStoreLclFldNode(TYP_STRUCT, lclNum, start, initValue, compiler.typGetBlkLayout(count));
            _ = compiler.impAppendTree(store, CHECK_SPILL_NONE, new DebugInfo());
        }
    }
#endif

    //------------------------------------------------------------------------
    // impPopArgsForUnmanagedCall: 
    //
    // Arguments:
    //    call - 
    //    sig  - 
    //    swiftErrorNode - [out] 
    //
    /// <summary>Pop arguments from IL stack to a pinvoke call. </summary>
    /// <param name="call">The unmanaged call</param>
    /// <param name="sig">The signature of the call site</param>
    /// <param name="swiftErrorNode">If this is a Swift call with a SwiftError* argument, then swiftErrorNode points to the node. Otherwise left at its existing value.</param>
    public void impPopArgsForUnmanagedCall(GenTreeCall call, in CORINFO_SIG_INFO sig, ref GenTree? swiftErrorNode)
    {
        assert((call.Flags & GTF_CALL_UNMANAGED) is not 0);

#if SWIFT_SUPPORT
        if (call.unmgdCallConv is CorInfoCallConvExtension.Swift)
        {
            impPopArgsForSwiftCall(call, sig, swiftErrorNode);
            return;
        }
#endif

        // Since we push the arguments in reverse order (i.e. right -> left)
        // spill any side effects from the stack
        // 
        // OBS: If there is only one side effect we do not need to spill it
        //      thus we have to spill all side-effects except last one

        var lastLevelWithSideEffects = -1;
        var argsToReverse = sig.numArgs;

        // For "thiscall", the first argument goes in a register. Since its
        // order does not need to be changed, we do not need to spill it

        if (call._unmgdCallConv is CorInfoCallConvExtension.Thiscall)
        {
            assert(argsToReverse is not 0);
            argsToReverse--;
        }

#if !TARGET_X86
        // Don't reverse args on ARM or x64 - first four args always placed in regs in order
        argsToReverse = 0;
#endif

        var esStack = stackState.esStack.AsSpan(0, stackState.esStackDepth);

        for (var level = stackState.esStackDepth - argsToReverse; level < esStack.Length; level++)
        {
            var val = esStack[level].val;

            if ((val.Flags & GTF_ORDER_SIDEEFF) is not 0)
            {
                assert(lastLevelWithSideEffects is -1);
                impSpillStackEntry(level, BAD_VAR_NUM, assertOnRecursion: false, "impPopArgsForUnmanagedCall - other side effect");
            }
            else if ((val.Flags & GTF_SIDE_EFFECT) is not 0)
            {
                if (lastLevelWithSideEffects is not -1)
                {
                    // We had a previous side effect - must spill it
                    impSpillStackEntry(lastLevelWithSideEffects, BAD_VAR_NUM, assertOnRecursion: false, "impPopArgsForUnmanagedCall - side effect");

                    // Record the level for the current side effect in case we will spill it
                    lastLevelWithSideEffects = level;
                }
                else
                {
                    // This is the first side effect encountered - record its level
                    lastLevelWithSideEffects = level;
                }
            }
        }

        // The argument list is now "clean" - no out-of-order side effects
        // Pop the argument list in reverse order

        impPopReverseCallArgs(sig, call, sig.numArgs - argsToReverse);

        if (call._unmgdCallConv is CorInfoCallConvExtension.Thiscall)
        {
            var arg = call.Args.GetArgByIndex(0);
            assert(arg is not null);

            var thisPtr = arg.Node;
            impBashVarAddrsToI(thisPtr);

            assert(thisPtr.Type is TYP_I_IMPL or TYP_BYREF);
        }
        impRetypeUnmanagedCallArgs(call);
    }

    /// <summary>Pop the given number of values from the stack and return a list node with their values.</summary>
    /// <param name="sigInfo">Signature used to figure out classes the runtime must load, and also to record exact receiving argument types that may be needed for ABI purposes later.</param>
    /// <param name="call">The call to pop arguments into.</param>
    private unsafe void impPopCallArgs(in CORINFO_SIG_INFO sigInfo, GenTreeCall call)
    {
        assert(call.Args.IsEmpty);

        if (impStackHeight < sigInfo.numArgs)
        {
            BADCODE("not enough arguments for call");
        }

        Unsafe.SkipInit(out InlineArray16<SigParamInfo> inlineParameters);
        var parameters = (sigInfo.numArgs <= 16) ? (Span<SigParamInfo>)(inlineParameters) : new SigParamInfo[sigInfo.numArgs];

        // We will iterate and pop the args in reverse order as we sometimes need
        // to spill some args. However, we need signature information and the
        // JIT-EE interface only allows us to iterate the signature forwards. We
        // will collect the needed information here and at the same time notify the
        // EE that the signature types need to be loaded.
        var sigArg = sigInfo.args;

        for (var i = 0; i < sigInfo.numArgs; i++)
        {
            ref var parameter = ref parameters[i];

            fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
            fixed (CORINFO_CLASS_HANDLE* pClassHandle = &parameter.ClassHandle)
            {
                parameter.CorType = strip(info.compCompHnd->getArgType(pSigInfo, sigArg, pClassHandle));
            }

            if (parameter.CorType is not CORINFO_TYPE_CLASS and not CORINFO_TYPE_BYREF and not CORINFO_TYPE_PTR)
            {
                CORINFO_CLASS_HANDLE argRealClass;

                fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
                {
                    argRealClass = info.compCompHnd->getArgClass(pSigInfo, sigArg);
                }

                if (argRealClass is not null)
                {
                    // Make sure that all valuetypes (including enums) that we push are loaded.
                    // This is to guarantee that if a GC is triggered from the prestub of this methods,
                    // all valuetypes in the method signature are already loaded.
                    // We need to be able to find the size of the valuetypes, but we cannot
                    // do a class-load from within GC.
                    info.compCompHnd->classMustBeLoadedBeforeCodeIsRun(argRealClass);
                }
            }

            sigArg = info.compCompHnd->getArgNext(sigArg);
        }

        if ((sigInfo.retTypeSigClass is not null) && (sigInfo.retType is not CORINFO_TYPE_CLASS and not CORINFO_TYPE_BYREF and not CORINFO_TYPE_PTR))
        {
            // Make sure that all valuetypes (including enums) that we push are loaded.
            // This is to guarantee that if a GC is triggered from the prestub of this methods,
            // all valuetypes in the method signature are already loaded.
            // We need to be able to find the size of the valuetypes, but we cannot
            // do a class-load from within GC.
            info.compCompHnd->classMustBeLoadedBeforeCodeIsRun(sigInfo.retTypeSigClass);
        }

        // Now create the arguments in reverse.
        for (var i = sigInfo.numArgs; i > 0; i--)
        {
            var se = impPopStack();
            var ti = se.seTypeInfo;
            var argNode = se.val;

            var parameter = parameters[i - 1];

            var jitSigType = parameter.CorType.VarType;
            var classHnd = parameter.ClassHandle;

            if (!impCheckImplicitArgumentCoercion(jitSigType, argNode.Type))
            {
                BADCODE("the call argument has a type that can't be implicitly converted to the signature type");
            }

            if (varTypeIsStruct(argNode.Type))
            {
                JITDUMP("Calling impNormStructVal on:\n");
                DISPTREE(argNode);

                argNode = impNormStructVal(argNode, CHECK_SPILL_ALL);

                // For SIMD types the normalization can normalize TYP_STRUCT to
                // e.g. TYP_SIMD16 which we keep (along with the class handle) in
                // the CallArgs.

                jitSigType = argNode.Type;

                JITDUMP("resulting tree:\n");
                DISPTREE(argNode);
            }
            else
            {
                // Insert implied casts (from float to double or double to float).
                argNode = impImplicitR4orR8Cast(argNode, jitSigType);

                // insert any widening or narrowing casts for backwards compatibility
                argNode = impImplicitIorI4Cast(argNode, jitSigType);

                if ((compAppleArm64Abi() || TargetArchitecture.IsArm32) && call.IsUnmanaged && varTypeIsSmall(jitSigType))
                {
                    // Apple arm64 and arm32 ABIs require arguments to be zero/sign
                    // extended up to 32 bit. The managed ABI does not require
                    // this.

                    if (fgCastNeeded(argNode, jitSigType))
                    {
                        argNode = gtNewCastNode(TYP_INT, argNode, fromUnsigned: false, jitSigType);
                    }
                }
            }

            NewCallArg arg;

            if (varTypeIsStruct(jitSigType))
            {
                arg = NewCallArg.CreateForStruct(argNode, jitSigType, typGetObjLayout(classHnd));
            }
            else
            {
                arg = NewCallArg.CreateForPrimitive(argNode, jitSigType);

                if ((i is 1) && ((sigInfo.callConv & CORINFO_CALLCONV_EXPLICITTHIS) is not 0))
                {
                    arg = arg.WithWellKnownArg(WellKnownArg.ThisPointer);
                }
            }

            _ = call.Args.PushFront(arg);
            call.Flags |= (argNode.Flags & GTF_GLOB_EFFECT);
        }
    }

    /// <summary>Pop the given number of values from the stack in reverse order (STDCALL/CDECL etc.) </summary>
    /// <param name="sigInfo"></param>
    /// <param name="call"></param>
    /// <param name="skipReverseCount"></param>
    /// <remarks>The first "skipReverseCount" items are not reversed.</remarks>
    private void impPopReverseCallArgs(in CORINFO_SIG_INFO sigInfo, GenTreeCall call, int skipReverseCount)
    {
        assert(skipReverseCount <= sigInfo.numArgs);
        impPopCallArgs(sigInfo, call);
        call.Args.Reverse(skipReverseCount, sigInfo.numArgs - skipReverseCount);
    }

    /// <summary>Pop one tree from the stack.</summary>
    /// <returns>The stack entry for the popped tree.</returns>
    public StackEntry impPopStack()
    {
        if (stackState.esStackDepth is 0)
        {
            BADCODE("stack underflow");
        }
        return stackState.esStack[--stackState.esStackDepth];
    }

    /// <summary>Pop a variable number of trees from the stack.</summary>
    /// <param name="n">The number of trees to pop.</param>
    public void impPopStack(int n)
    {
        if (stackState.esStackDepth < n)
        {
            BADCODE("stack underflow");
        }
        stackState.esStackDepth -= n;
    }

    private static GenTreeFlags impPrefixFlagsToIndirFlags(int prefixFlags)
    {
        var indirFlags = GTF_EMPTY;

        if ((prefixFlags & PREFIX_VOLATILE) is not 0)
        {
            indirFlags |= GTF_IND_VOLATILE;
        }

        if ((prefixFlags & PREFIX_UNALIGNED) is not 0)
        {
            indirFlags |= GTF_IND_UNALIGNED;
        }
        return indirFlags;
    }

    /// <summary>import a NamedIntrinsic representing a primitive operation</summary>
    /// <param name="intrinsic">the intrinsic being imported</param>
    /// <param name="clsHnd">handle for the intrinsic method's class</param>
    /// <param name="method">handle for the intrinsic method</param>
    /// <param name="sigInfo">signature of the intrinsic method</param>
    /// <param name="mustExpand">true if the intrinsic must return a GenTree*; otherwise, false</param>
    /// <returns>IR tree to use in place of the call, or null if the jit should treat the intrinsic call like a normal call.</returns>
    public unsafe GenTree? impPrimitiveNamedIntrinsic(NamedIntrinsic intrinsic, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE method, in CORINFO_SIG_INFO sigInfo, bool mustExpand)
    {
        assert(sigInfo.sigInst.classInstCount is 0);

        var retType = sigInfo.retType.VarType;

        if (!varTypeIsArithmetic(retType))
        {
            assert((intrinsic is NI_PRIMITIVE_ConvertToInteger) || (intrinsic is NI_PRIMITIVE_ConvertToIntegerNative));
            return null;
        }

        var hwintrinsic = NI_Illegal;

        var args = sigInfo.args;
        assert(sigInfo.numArgs is 1 or 2);

        CORINFO_CLASS_HANDLE op1ClsHnd;
        CorInfoType baseJitType;

        fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
        {
            baseJitType = strip(info.compCompHnd->getArgType(pSigInfo, args, &op1ClsHnd));
        }
        var baseType = baseJitType.VarType;

        var result = null as GenTree;

        switch (intrinsic)
        {
            case NI_PRIMITIVE_ConvertToIntegerNative:
            {
                if (BlockNonDeterministicIntrinsics(mustExpand))
                {
                    return null;
                }
                goto case NI_PRIMITIVE_ConvertToInteger;
            }

            case NI_PRIMITIVE_ConvertToInteger:
            {
                assert(sigInfo.sigInst.methInstCount is 1);
                assert(varTypeIsFloating(baseType));

                var tgtType = sigInfo.retType.PreciseVarType;
                retType = retType.ActualType;
                var uns = varTypeIsUnsigned(tgtType) && !varTypeIsSmall(tgtType);

                var res = null as GenTree;
                var op1 = null as GenTree;

#if TARGET_XARCH && FEATURE_HW_INTRINSICS
                if (intrinsic is NI_PRIMITIVE_ConvertToIntegerNative)
                {
                    var hwIntrinsicId = NI_Illegal;

                    if (retType is TYP_INT)
                    {
                        if (baseType is TYP_FLOAT)
                        {
                            if (!uns)
                            {
                                hwIntrinsicId = NI_X86Base_ConvertToInt32WithTruncation;
                            }
                            else if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                            {
                                hwIntrinsicId = NI_AVX512_ConvertToUInt32WithTruncation;
                            }
                        }
                        else
                        {
                            assert(baseType is TYP_DOUBLE);

                            if (!uns)
                            {
                                hwIntrinsicId = NI_X86Base_ConvertToInt32WithTruncation;
                            }
                            else if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                            {
                                hwIntrinsicId = NI_AVX512_ConvertToUInt32WithTruncation;
                            }
                        }
                    }
#if TARGET_AMD64
                    else
                    {
                        assert(retType is TYP_LONG);

                        if (baseType is TYP_FLOAT)
                        {
                            if (!uns)
                            {
                                hwIntrinsicId = NI_X86Base_X64_ConvertToInt64WithTruncation;
                            }
                            else if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                            {
                                hwIntrinsicId = NI_AVX512_X64_ConvertToUInt64WithTruncation;
                            }
                        }
                        else
                        {
                            assert(baseType == TYP_DOUBLE);

                            if (!uns)
                            {
                                hwIntrinsicId = NI_X86Base_X64_ConvertToInt64WithTruncation;
                            }
                            else if (compOpportunisticallyDependsOn(InstructionSet_AVX512))
                            {
                                hwIntrinsicId = NI_AVX512_X64_ConvertToUInt64WithTruncation;
                            }
                        }
                    }
#endif

                    if (hwIntrinsicId is not NI_Illegal)
                    {
                        op1 = impPopStack().val;
                        res = gtNewSimdHWIntrinsicNode(retType, hwIntrinsicId, baseJitType.PreciseVarType, 16, op1);

                        if (varTypeIsSmall(tgtType))
                        {
                            res = gtNewCastNode(TYP_INT, res, fromUnsigned: false, tgtType);
                        }
                        return res;
                    }
                }
#endif

                op1 = impPopStack().val;

                if (varTypeIsSmall(tgtType))
                {
                    res = gtNewCastNode(retType, op1, fromUnsigned: false, retType);
                    res = gtFoldExpr(res);
                    res = gtNewCastNode(TYP_INT, res, fromUnsigned: false, tgtType);
                }
                else
                {
                    res = gtNewCastNode(retType, op1, fromUnsigned: false, tgtType);
                }
                return gtFoldExpr(res);
            }

            case NI_PRIMITIVE_Crc32C:
            {
                assert(sigInfo.numArgs is 2);
                assert(retType is TYP_INT);

                // Crc32 needs the base type from op2

                CORINFO_CLASS_HANDLE op2ClsHnd;
                args = info.compCompHnd->getArgNext(args);

                fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
                {
                    baseJitType = strip(info.compCompHnd->getArgType(pSigInfo, args, &op2ClsHnd));
                }
                baseType = baseJitType.VarType;

#if !TARGET_64BIT
                if (varTypeIsLong(baseType))
                {
                    // TODO-CQ: Adding long decomposition support is more complex
                    // and not supported today so early exit if we have a long and
                    // either input is not a constant.

                    break;
                }
#endif

#if FEATURE_HW_INTRINSICS
#if TARGET_XARCH
                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                if (varTypeIsLong(baseType))
                {
                    hwintrinsic = NI_X86Base_X64_Crc32;
                    op1 = gtFoldExpr(gtNewCastNode(baseType, op1, fromUnsigned: true, baseType));
                }
                else
                {
                    hwintrinsic = NI_X86Base_Crc32;
                    baseType = baseType.ActualType;
                }

                result = gtNewScalarHWIntrinsicNode(baseType, hwintrinsic, op1, op2);

                // We use the simdBaseJitType to bring the type of the second argument to codegen
                result.AsHWIntrinsic().SimdBaseType = baseJitType.PreciseVarType;
#elif TARGET_ARM64
                if (compOpportunisticallyDependsOn(InstructionSet_Crc32))
                {
                    var op2 = impPopStack().val;
                    var op1 = impPopStack().val;

                    hwintrinsic = varTypeIsLong(baseType) ? NI_Crc32_Arm64_ComputeCrc32C : NI_Crc32_ComputeCrc32C;
                    result = gtNewScalarHWIntrinsicNode(TYP_INT, hwintrinsic, op1, op2);
                    baseType = TYP_INT;

                    // We use the simdBaseJitType to bring the type of the second argument to codegen
                    result.AsHWIntrinsic().SimdBaseType = baseJitType.PreciseVarType;
                }
#endif
#endif

                break;
            }

            case NI_PRIMITIVE_LeadingZeroCount:
            {
                assert(sigInfo.numArgs is 1);
                assert(!varTypeIsSmall(retType) && !varTypeIsSmall(baseType));

                var op1 = impStackTop().val;

                if (op1.Oper.IsIntegralConst)
                {
                    // Pop the value from the stack
                    _ = impPopStack();

                    if (varTypeIsLong(baseType))
                    {
                        var cns = op1.AsIntConCommon().LngValue;
                        result = gtNewLconNode(long.LeadingZeroCount(cns));
                    }
                    else
                    {
                        var cns = (int)(op1.AsIntConCommon().IconValue);
                        result = gtNewIconNode(baseType, int.LeadingZeroCount(cns));
                    }
                    break;
                }

#if !TARGET_64BIT && !TARGET_WASM
                if (varTypeIsLong(baseType))
                {
                    // TODO-CQ: Adding long decomposition support is more complex
                    // and not supported today so early exit if we have a long and
                    // either input is not a constant.

                    break;
                }
#endif

#if TARGET_RISCV64
                if (compOpportunisticallyDependsOn(InstructionSet_Zbb))
                {
                    _ = impPopStack();
                    result = new GenTreeIntrinsic(retType, op1, NI_PRIMITIVE_LeadingZeroCount, methodHandle: null);
                }
#elif TARGET_WASM
                _ = impPopStack();
                result = new GenTreeIntrinsic(retType, op1, NI_PRIMITIVE_LeadingZeroCount, methodHandle: null);
#elif FEATURE_HW_INTRINSICS
#if TARGET_XARCH
                if (compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    // Pop the value from the stack
                    _ = impPopStack();

                    hwintrinsic = varTypeIsLong(baseType) ? NI_AVX2_X64_LeadingZeroCount : NI_AVX2_LeadingZeroCount;
                    result = gtNewScalarHWIntrinsicNode(baseType, hwintrinsic, op1);
                }
                else
                {
                    // Pop the value from the stack
                    _ = impPopStack();

                    // We're importing this as the following...
                    // * 32-bit lzcnt: (value is 0) ? 32 : (31 ^ BSR(value))
                    // * 64-bit lzcnt: (value is 0) ? 64 : (63 ^ BSR(value))

                    op1 = impCloneExpr(op1, out var op1Dup, CHECK_SPILL_ALL, "Cloning op1 for LeadingZeroCount");
                    assert(op1Dup is not null);

                    hwintrinsic = varTypeIsLong(baseType) ? NI_X86Base_X64_BitScanReverse : NI_X86Base_BitScanReverse;
                    op1Dup = gtNewScalarHWIntrinsicNode(baseType, hwintrinsic, op1Dup);

                    var cond = gtNewBinaryNode(GT_EQ, TYP_INT, op1, gtNewZeroConNode(baseType)) as GenTree;
                    cond = gtFoldExpr(cond);

                    GenTree trueRes;
                    GenTree icon;

                    if (varTypeIsLong(baseType))
                    {
                        trueRes = gtNewLconNode(64);
                        icon = gtNewLconNode(63);
                    }
                    else
                    {
                        trueRes = gtNewIconNode(baseType, 32);
                        icon = gtNewIconNode(baseType, 31);
                    }

                    var falseRes = gtNewBinaryNode(GT_XOR, baseType, op1Dup, icon);
                    var colon = gtNewColonNode(baseType, trueRes, falseRes);

                    result = gtNewQmarkNode(baseType, cond, colon);

                    var tmp = lvaGrabTemp(shortLifetime: true, "Grabbing temp for LeadingZeroCount Qmark");
                    impStoreToTemp(tmp, result, CHECK_SPILL_NONE);
                    result = gtNewLclvNode(baseType, tmp);
                }
#elif TARGET_ARM64
                // Pop the value from the stack
                _ = impPopStack();

                hwintrinsic = varTypeIsLong(baseType) ? NI_ArmBase_Arm64_LeadingZeroCount : NI_ArmBase_LeadingZeroCount;
                result = gtNewScalarHWIntrinsicNode(TYP_INT, op1, hwintrinsic);
                baseType = TYP_INT;
#endif
#endif
                break;
            }

            case NI_PRIMITIVE_Log2:
            {
                assert(sigInfo.numArgs is 1);
                assert(!varTypeIsSmall(retType) && !varTypeIsSmall(baseType));

                var op1 = impStackTop().val;

                if (op1.Oper.IsIntegralConst)
                {
                    // Pop the value from the stack
                    _ = impPopStack();

                    if (varTypeIsLong(baseType))
                    {
                        var cns = op1.AsIntConCommon().LngValue;

                        if (varTypeIsUnsigned(baseJitType.PreciseVarType) || (cns >= 0))
                        {
                            result = gtNewLconNode(BitOperations.Log2(unchecked((ulong)(cns))));
                        }
                    }
                    else
                    {
                        var cns = (int)(op1.AsIntConCommon().IconValue);

                        if (varTypeIsUnsigned(baseJitType.PreciseVarType) || (cns >= 0))
                        {
                            result = gtNewIconNode(baseType, BitOperations.Log2(unchecked((uint)(cns))));
                        }
                    }
                    break;
                }

#if !TARGET_64BIT
                if (varTypeIsLong(baseType))
                {
                    // TODO-CQ: Adding long decomposition support is more complex
                    // and not supported today so early exit if we have a long and
                    // either input is not a constant.

                    break;
                }
#endif

                if (varTypeIsSigned(baseType))
                {
                    // TODO-CQ: We should insert the `if (value < 0) { throw }` handling
                    break;
                }

#if FEATURE_HW_INTRINSICS
                var lzcnt = impPrimitiveNamedIntrinsic(NI_PRIMITIVE_LeadingZeroCount, clsHnd, method, sigInfo, mustExpand);

                if (lzcnt is not null)
                {
                    GenTree icon;

                    if (varTypeIsLong(retType))
                    {
                        icon = gtNewLconNode(63);
                    }
                    else
                    {
                        icon = gtNewIconNode(retType, 31);
                    }

                    result = gtNewBinaryNode(GT_XOR, retType, lzcnt, icon);
                    baseType = retType;
                }
#endif
                break;
            }

            case NI_PRIMITIVE_PopCount:
            {
                assert(sigInfo.numArgs is 1);
                assert(!varTypeIsSmall(retType) && !varTypeIsSmall(baseType));

                var op1 = impStackTop().val;

                if (op1.Oper.IsIntegralConst)
                {
                    // Pop the value from the stack
                    _ = impPopStack();

                    if (varTypeIsLong(baseType))
                    {
                        var cns = op1.AsIntConCommon().LngValue;
                        result = gtNewLconNode(long.PopCount(cns));
                    }
                    else
                    {
                        var cns = (int)(op1.AsIntConCommon().IconValue);
                        result = gtNewIconNode(baseType, int.PopCount(cns));
                    }
                    break;
                }

#if !TARGET_64BIT && !TARGET_WASM
                if (varTypeIsLong(baseType))
                {
                    // TODO-CQ: Adding long decomposition support is more complex
                    // and not supported today so early exit if we have a long and
                    // either input is not a constant.

                    break;
                }
#endif

#if TARGET_RISCV64
                if (compOpportunisticallyDependsOn(InstructionSet_Zbb))
                {
                    _ = impPopStack();
                    result = new GenTreeIntrinsic(retType, op1, NI_PRIMITIVE_PopCount, methodHandle: null);
                }
#elif TARGET_WASM
                _ = impPopStack();
                result = new GenTreeIntrinsic(retType, op1, NI_PRIMITIVE_PopCount, methodHandle: null);
#elif FEATURE_HW_INTRINSICS
#if TARGET_XARCH
                // Pop the value from the stack
                _ = impPopStack();

                hwintrinsic = varTypeIsLong(baseType) ? NI_X86Base_X64_PopCount : NI_X86Base_PopCount;
                result = gtNewScalarHWIntrinsicNode(baseType, hwintrinsic, op1);
#elif TARGET_ARM64
                _ = impPopStack();

                compFloatingPointUsed = true;
                result = new GenTreeIntrinsic(TYP_INT, op1, NI_PRIMITIVE_PopCount, methodHandle: nullptr);
                baseType = TYP_INT;
#endif
#endif
                break;
            }

            case NI_PRIMITIVE_RotateLeft:
            {
                assert(sigInfo.numArgs is 2);
                assert(!varTypeIsSmall(retType) && !varTypeIsSmall(baseType));

                var op2 = impStackTop().val;

                if (!op2.Oper.IsIntegralConst)
                {
                    // TODO-CQ: ROL currently expects op2 to be a constant
                    break;
                }

                // Pop the value from the stack
                _ = impPopStack();

                var op1 = impPopStack().val;
                var cns2 = (int)(op2.AsIntConCommon().IconValue);

                // Mask the offset to ensure deterministic xplat behavior for overshifting
                cns2 &= varTypeIsLong(baseType) ? 0x3F : 0x1F;

                if (cns2 is 0)
                {
                    // No rotation is a nop
                    return op1;
                }

                if (op1.Oper.IsIntegralConst)
                {
                    if (varTypeIsLong(baseType))
                    {
                        var cns1 = op1.AsIntConCommon().LngValue;
                        result = gtNewLconNode(long.RotateLeft(cns1, cns2));
                    }
                    else
                    {
                        var cns1 = (int)(op1.AsIntConCommon().IconValue);
                        result = gtNewIconNode(baseType, int.RotateLeft(cns1, cns2));
                    }
                    break;
                }

                op2.AsIntConCommon().IconValue = cns2;

                result = gtNewBinaryNode(GT_ROL, baseType, op1, op2);
                result = gtFoldExpr(result);
                break;
            }

            case NI_PRIMITIVE_RotateRight:
            {
                assert(sigInfo.numArgs is 2);
                assert(!varTypeIsSmall(retType) && !varTypeIsSmall(baseType));

                var op2 = impStackTop().val;

                if (!op2.Oper.IsIntegralConst)
                {
                    // TODO-CQ: ROR currently expects op2 to be a constant
                    break;
                }

                // Pop the value from the stack
                impPopStack();

                var op1 = impPopStack().val;
                var cns2 = (int)(op2.AsIntConCommon().IconValue);

                // Mask the offset to ensure deterministic xplat behavior for overshifting
                cns2 &= varTypeIsLong(baseType) ? 0x3F : 0x1F;

                if (cns2 is 0)
                {
                    // No rotation is a nop
                    return op1;
                }

                if (op1.Oper.IsIntegralConst)
                {
                    if (varTypeIsLong(baseType))
                    {
                        var cns1 = op1.AsIntConCommon().LngValue;
                        result = gtNewLconNode(long.RotateRight(cns1, cns2));
                    }
                    else
                    {
                        var cns1 = (int)(op1.AsIntConCommon().IconValue);
                        result = gtNewIconNode(baseType, int.RotateRight(cns1, cns2));
                    }
                    break;
                }

                op2.AsIntConCommon().IconValue = cns2;

                result = gtNewBinaryNode(GT_ROR, baseType, op1, op2);
                result = gtFoldExpr(result);
                break;
            }

            case NI_PRIMITIVE_TrailingZeroCount:
            {
                assert(sigInfo.numArgs is 1);
                assert(!varTypeIsSmall(baseType));

                var op1 = impStackTop().val;

                if (op1.Oper.IsIntegralConst)
                {
                    // Pop the value from the stack
                    impPopStack();

                    if (varTypeIsLong(baseType))
                    {
                        var cns = op1.AsIntConCommon().LngValue;
                        result = gtNewLconNode(long.TrailingZeroCount(cns));
                    }
                    else
                    {
                        var cns = (int)(op1.AsIntConCommon().IconValue);
                        result = gtNewIconNode(baseType, int.TrailingZeroCount(cns));
                    }

                    baseType = retType;
                    break;
                }

#if !TARGET_64BIT && !TARGET_WASM
                if (varTypeIsLong(baseType))
                {
                    // TODO-CQ: Adding long decomposition support is more complex
                    // and not supported today so early exit if we have a long and
                    // either input is not a constant.

                    break;
                }
#endif

#if TARGET_RISCV64
                if (compOpportunisticallyDependsOn(InstructionSet_Zbb))
                {
                    _ = impPopStack();
                    result = new GenTreeIntrinsic(retType, op1, NI_PRIMITIVE_TrailingZeroCount, methodHandle: null);
                }
#elif TARGET_WASM
                _ = impPopStack();
                result = new GenTreeIntrinsic(retType, op1, NI_PRIMITIVE_TrailingZeroCount, methodHandle: null);
#elif FEATURE_HW_INTRINSICS
#if TARGET_XARCH
                if (compOpportunisticallyDependsOn(InstructionSet_AVX2))
                {
                    // Pop the value from the stack
                    _ = impPopStack();

                    hwintrinsic = varTypeIsLong(baseType) ? NI_AVX2_X64_TrailingZeroCount : NI_AVX2_TrailingZeroCount;
                    result = gtNewScalarHWIntrinsicNode(baseType, hwintrinsic, op1);
                }
                else
                {
                    // Pop the value from the stack
                    _ = impPopStack();

                    // We're importing this as the following...
                    // * 32-bit tzcnt: (value is 0) ? 32 : BSF(value)
                    // * 64-bit tzcnt: (value is 0) ? 64 : BSF(value)

                    op1 = impCloneExpr(op1, out var op1Dup, CHECK_SPILL_ALL, "Cloning op1 for TrailingZeroCount");
                    assert(op1Dup is not null);

                    hwintrinsic = varTypeIsLong(baseType) ? NI_X86Base_X64_BitScanForward : NI_X86Base_BitScanForward;
                    op1Dup = gtNewScalarHWIntrinsicNode(baseType, hwintrinsic, op1Dup);

                    var cond = gtNewBinaryNode(GT_EQ, TYP_INT, op1, gtNewZeroConNode(baseType)) as GenTree;
                    cond = gtFoldExpr(cond);

                    GenTree trueRes;

                    if (varTypeIsLong(baseType))
                    {
                        trueRes = gtNewLconNode(64);
                    }
                    else
                    {
                        trueRes = gtNewIconNode(baseType, 32);
                    }

                    var falseRes = op1Dup;
                    var colon = gtNewColonNode(baseType, trueRes, falseRes);

                    result = gtNewQmarkNode(baseType, cond, colon);

                    var tmp = lvaGrabTemp(shortLifetime: true, "Grabbing temp for TrailingZeroCount Qmark");
                    impStoreToTemp(tmp, result, CHECK_SPILL_NONE);
                    result = gtNewLclvNode(baseType, tmp);
                }
#elif TARGET_ARM64
            // Pop the value from the stack
            _ = impPopStack();

            result = new GenTreeIntrinsic(TYP_INT, op1, NI_PRIMITIVE_TrailingZeroCount, methodHandle: nullptr);
            baseType = TYP_INT;
#endif
#endif

                break;
            }

            default:
            {
                unreached();
                break;
            }
        }

        if ((result is not null) && (retType != baseType))
        {
            // We're either LONG->INT or INT->LONG
            assert(!varTypeIsSmall(retType) && !varTypeIsSmall(baseType));
            result = gtFoldExpr(gtNewCastNode(retType, result, /* uint */ true, retType));
        }
        return result;
    }

    /// <summary>Push catch arg onto the stack.</summary>
    /// <param name="hndBlk">first block of the catch handler</param>
    /// <param name="clsHnd">type being caught</param>
    /// <param name="isSingleBlockFilter">true if catch has single block filtger</param>
    /// <returns>the basic block of the actual handler.</returns>
    /// <remarks>If there are jumps to the beginning of the handler, insert basic block and spill catch arg to a temp. Update the handler block if necessary.</remarks>
    public unsafe BasicBlock impPushCatchArgOnStack(BasicBlock hndBlk, CORINFO_CLASS_HANDLE clsHnd, bool isSingleBlockFilter)
    {
        // Do not inject the basic block twice on reimport. This should be
        // hit only under JIT stress. See if the block is the one we injected.
        // Note that EH canonicalization can inject internal blocks here. We might
        // be able to re-use such a block (but we don't, right now).
        if (hndBlk.HasAllFlags(BBF_IMPORTED | BBF_INTERNAL | BBF_DONT_REMOVE))
        {
            var stmt = hndBlk.FirstStmt;

            if (stmt is not null)
            {
                var tree = stmt.RootNode;

                if ((tree.Oper is GT_STORE_LCL_VAR) && (tree.AsLclVar().Data.Oper is GT_CATCH_ARG))
                {
                    tree = gtNewLclvNode(TYP_REF, tree.AsLclVar().LclNum);
                    impPushOnStack(tree, new typeInfo(clsHnd));

                    assert(hndBlk.Next is not null);
                    return hndBlk.Next;
                }
            }

            // If we get here, it must have been some other kind of internal block. It's possible that
            // someone prepended something to our injected block, but that's unlikely.
        }

        // Push the exception address value on the stack
        // and mark the node as having a side-effect - i.e. cannot be moved around since it is tied to a fixed location (EAX)
        var arg = new GenTree(GT_CATCH_ARG, TYP_REF) {
            HasOrderingSideEffect = true
        };

#if JIT32_GCENCODER
        var forceInsertNewBlock = isSingleBlockFilter || compStressCompile(STRESS_CATCH_ARG, 5);
#else
        var forceInsertNewBlock = compStressCompile(STRESS_CATCH_ARG, 5);
#endif

        // Spill GT_CATCH_ARG to a temp if there are jumps to the beginning of the handler.
        //
        // For typical normal handlers we expect ref count to be 2 here (one artificial, one for
        // the edge from the xxx...)

        if ((hndBlk.bbRefs > 2) || forceInsertNewBlock)
        {
            // Create extra basic block for the spill
            var newBlk = fgNewBBbefore(BBJ_ALWAYS, hndBlk, extendRegion: true);

            newBlk.SetFlags(BBF_IMPORTED | BBF_DONT_REMOVE);
            newBlk.inheritWeight(hndBlk);
            newBlk.bbCodeOffs = hndBlk.bbCodeOffs;

            var newEdge = fgAddRefPred(hndBlk, newBlk);
            newBlk.TargetEdge = newEdge;

            // Spill into a temp.
            var tempNum = lvaGrabTemp(shortLifetime: false, "SpillCatchArg");
            lvaTable[tempNum].Type = TYP_REF;

            var argStore = gtNewTempStore(tempNum, arg);
            arg = gtNewLclvNode(TYP_REF, tempNum);

            hndBlk.bbStkTempsIn = tempNum;

            Statement argStmt;

            if ((info.compStmtOffsetsImplicit & ICorDebugInfo.CALL_SITE_BOUNDARIES) is not 0)
            {
                // Report the debug info. impImportBlockCode won't treat the actual handler as exception block and thus won't do it for us.
                // TODO-Bug: Should be reported with ICorDebugInfo.CALL_SITE?
                impCurStmtDI = new DebugInfo(compInlineContext, new ILLocation(newBlk.bbCodeOffs, ICorDebugInfo.SOURCE_TYPE_INVALID));
                argStmt = gtNewStmt(argStore, impCurStmtDI);
            }
            else
            {
                argStmt = gtNewStmt(argStore);
            }

            fgInsertStmtAtEnd(newBlk, argStmt);
        }

        impPushOnStack(arg, new typeInfo(clsHnd));
        return hndBlk;
    }

    public void impPushOnStack(GenTree tree, typeInfo ti)
    {
        assert(compCurBB is not null);

        // Check for overflow. If inlining, we may be using a bigger stack
        var stackDepth = stackState.esStackDepth++;

        if ((stackDepth >= info.compMaxStack) && (stackDepth >= impStkSize || !compCurBB.HasFlag(BBF_IMPORTED)))
        {
            BADCODE("stack overflow");
        }

        ref var stackEntry = ref stackState.esStack[stackDepth];

        stackEntry.seTypeInfo = ti;
        stackEntry.val = tree;

        var type = tree.Type;

        if (type is TYP_LONG)
        {
            compLongUsed = true;
        }
        else if (varTypeIsFloating(type))
        {
            compFloatingPointUsed = true;
        }
    }

#if FEATURE_READYTORUN
    public unsafe GenTreeCall? impReadyToRunHelperToTree(in CORINFO_RESOLVED_TOKEN resolvedToken, CorInfoHelpFunc helper, var_types type, GenTree? arg1 = null)
    {
        CORINFO_CONST_LOOKUP lookup;

        fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
        {
            if (!info.compCompHnd->getReadyToRunHelper(pResolvedToken, helper, info.compMethodHnd, &lookup))
            {
                return null;
            }
        }

        var op1 = (arg1 is not null) ? gtNewHelperCallNode(type, helper, arg1) : gtNewHelperCallNode(type, helper);
        op1._entryPoint = lookup;

        if (IsStaticHelperEligibleForExpansion(op1))
        {
            // Keep class handle attached to the helper call since it's difficult to restore it
            // Keep class handle attached to the helper call since it's difficult to restore it.
            op1._initClsHnd = resolvedToken.hClass;
        }
        return op1;
    }
#endif

    // Similar to impImportBlockPending, but assumes that block has already been imported once and is being
    // reimported for some reason.  It specifically does *not* look at stackState to set the EntryState
    // for the block, but instead, just re-uses the block's existing EntryState.
    public void impReimportBlockPending(BasicBlock block)
    {
        JITDUMP($"\nimpReimportBlockPending for {FMT_BB(block.bbNum)}");
        assert(block.HasFlag(BBF_IMPORTED));

        // OK, we must add to the pending list, if it's not already in it.
        if (impGetPendingBlockMember(block) is not 0)
        {
            return;
        }

        // Get an entry to add to the pending list

        var dsc = null as PendingDsc;

        if (impPendingFree is not null)
        {
            // We can reuse one of the freed up dscs.
            dsc = impPendingFree;
            dsc.pdBB = block;
            impPendingFree = dsc.pdNext;
        }
        else
        {
            // We have to create a new dsc
            dsc = new PendingDsc(block);
        }

        ref var entryState = ref block.EntryState;

        dsc.pdSavedStack.ssDepth = entryState.esStackDepth;
        dsc.pdSavedStack.ssTrees = entryState.esStack;

        // Add the entry to the pending list

        dsc.pdNext = impPendingList;
        impPendingList = dsc;
        impSetPendingBlockMember(block, 1); // And indicate that it's now a member of the set.

        // Various assertions require us to now to consider the block as not imported (at least for
        // the final time...)
        block.RemoveFlags(BBF_IMPORTED);

#if DEBUG
        if (false && verbose)
        {
            jitprintf($"Added PendingDsc - {dsc.GetHashCode():X8} for {FMT_BB(block.bbNum)}\n");
        }
#endif
    }

    /// <summary>Mark the block as unimported.</summary>
    /// <param name="block"></param>
    /// <remarks>Note that the caller is responsible for calling impImportBlockPending(), with the appropriate stack-state</remarks>
    public void impReimportMarkBlock(BasicBlock block)
    {
#if DEBUG
        if (verbose && block.HasFlag(BBF_IMPORTED))
        {
            jitprintf($"\n{FMT_BB(block.bbNum)} will be reimported\n");
        }
#endif

        // We shouldn't be re-importing one of these special blocks.
        assert(block.Kind is not BBJ_CALLFINALLYRET);

        if (block.isBBCallFinallyPair)
        {
            // If we're going to re-import a BBJ_CALLFINALLY that has a paired BBJ_CALLFINALLYRET,
            // remove the BBJ_CALLFINALLYRET.
            var leaveBlock = block.Next;
            assert(leaveBlock is not null);

            fgPrepareCallFinallyRetForRemoval(leaveBlock);
            fgRemoveBlock(leaveBlock, unreachable: true);

            // The above code marked the BBJ_CALLFINALLY as retless. Remove that.
            block.RemoveFlags(BBF_RETLESS_CALL);
        }

        block.RemoveFlags(BBF_IMPORTED);
    }

    // Assumes that "block" is a basic block that completes with a non-empty stack. We have previously
    // assigned the values on the stack to local variables (the "spill temp" variables). The successor blocks
    // will assume that its incoming stack contents are in those locals. This requires "block" and its
    // successors to agree on the variables and their types that will be used.  The CLI spec allows implicit
    // conversions between 'int' and 'native int' or 'float' and 'double' stack types. So one predecessor can
    // push an int and another can push a native int.  For 64-bit we have chosen to implement this by typing
    // the "spill temp" as native int, and then importing (or re-importing as needed) so that all the
    // predecessors in the "spill clique" push a native int (sign-extending if needed), and all the
    // successors receive a native int. Similarly float and double are unified to double.
    // This routine is called after a type-mismatch is detected, and it will walk the spill clique to mark
    // blocks for re-importation as appropriate (both successors, so they get the right incoming type, and
    // predecessors, so they insert an upcast if needed).
    public void impReimportSpillClique(BasicBlock block)
    {
#if DEBUG
        if (verbose)
        {
            jitprintf($"\n*************** In impReimportSpillClique({FMT_BB(block.bbNum)})\n");
        }
#endif

        // If we get here, it is because this block is already part of a spill clique
        // and one predecessor had an outgoing live stack slot of type int, and this
        // block has an outgoing live stack slot of type native int.
        // We need to reset these before traversal because they have already been set
        // by the previous walk to determine all the members of the spill clique.
        var inlineRoot = impInlineRoot;

        inlineRoot.impSpillCliquePredMembers.Clear();
        inlineRoot.impSpillCliqueSuccMembers.Clear();

        impWalkSpillCliqueFromPred(block, ReimportSpillClique);
    }

    /// <summary>This is called when reimporting a leave block. It resets the JumpKind, JumpDest, and bbNext to the original values</summary>
    /// <param name="block"></param>
    /// <param name="jmpAddr"></param>
    public void impResetLeaveBlock(BasicBlock block, int jmpAddr)
    {
        // With EH Funclets, while importing leave opcode we create another block ending with BBJ_ALWAYS (call it B1)
        // and the block containing leave (say B0) is marked as BBJ_CALLFINALLY.   Say for some reason we reimport B0,
        // it is reset (in this routine) by marking as ending with BBJ_LEAVE and further down when B0 is reimported, we
        // create another BBJ_ALWAYS (call it B2). In this process B1 gets orphaned and any blocks to which B1 is the
        // only predecessor are also considered orphans and attempted to be deleted.
        //
        //  try  {
        //     ....
        //     try
        //     {
        //         ....
        //         leave OUTSIDE;  // B0 is the block containing this leave, following this would be B1
        //     } finally { }
        //  } finally { }
        //  OUTSIDE:
        //
        // In the above nested try-finally example, we create a step block (call it Bstep) which in branches to a block
        // where a finally would branch to (and such block is marked as finally target).  Block B1 branches to step block.
        // Because of re-import of B0, Bstep is also orphaned. Since Bstep is a finally target it cannot be removed.  To
        // work around this we will duplicate B0 (call it B0Dup) before resetting. B0Dup is marked as BBJ_CALLFINALLY and
        // only serves to pair up with B1 (BBJ_ALWAYS) that got orphaned. Now during orphan block deletion B0Dup and B1
        // will be treated as pair and handled correctly.
        if (block.Kind is BBJ_CALLFINALLY)
        {
            var dupBlock = BasicBlock.New(this);
            dupBlock.CopyFlags(block);

            var newEdge = fgAddRefPred(block.Target, dupBlock);
            dupBlock.SetKindAndTargetEdge(BBJ_CALLFINALLY, newEdge);
            dupBlock.copyEHRegion(block);
            dupBlock.CatchType = block.CatchType;

            // Mark this block as
            //  a) not referenced by any other block to make sure that it gets deleted
            //  b) weight zero
            //  c) prevent from being imported
            //  d) as internal
            dupBlock.bbRefs = 0;
            dupBlock.bbSetRunRarely();
            dupBlock.SetFlags(BBF_IMPORTED | BBF_INTERNAL);

            // Insert the block right after the block which is getting reset so that BBJ_CALLFINALLY and BBJ_ALWAYS
            // will be next to each other.
            fgInsertBBafter(block, dupBlock);

#if DEBUG
            if (verbose)
            {
                jitprintf($"New Basic Block {FMT_BB(dupBlock.bbNum)} duplicate of {FMT_BB(block.bbNum)} created.\n");
            }
#endif
        }

        fgInitBBLookup();

        var newTarget = fgLookupBB(jmpAddr);
        assert(newTarget is not null);

        fgRedirectEdge(ref block.TargetEdgeRef, newTarget);

        block.Kind = BBJ_LEAVE;

        // We will leave the BBJ_ALWAYS block we introduced. When it's reimported
        // the BBJ_ALWAYS block will be unreachable, and will be removed after. The
        // reason we don't want to remove the block at this point is that if we call
        // fgInitBBLookup() again we will do it wrong as the BBJ_ALWAYS block won't be
        // added and the linked list length will be different than fgBBcount.
        //
        // Because of this incomplete cleanup. profile data may be left inconsistent.
        //
        if (block.hasProfileWeight)
        {
            // We are unlikely to be able to repair the profile.
            // For now we don't even try.
            //
            JITDUMP($"\nimpResetLeaveBlock: Profile data could not be locally repaired. Data {(fgPgoConsistent ? "is now" : "was already")} inconsistent.\n");

            if (fgPgoConsistent)
            {
                Metrics.ProfileInconsistentResetLeave++;
                fgPgoConsistent = false;
            }
        }
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

    public void impRestoreStackState(SavedStack savePtr)
    {
        stackState.esStackDepth = savePtr.ssDepth;
        stackState.esStack = (savePtr.ssDepth is not 0) ? [.. savePtr.ssTrees] : [];
    }

    /// <summary>Re-type the incoming lclVar nodes to match the varDsc.</summary>
    /// <param name="blk"></param>
    public void impRetypeEntryStateTemps(BasicBlock blk)
    {
        ref var entryState = ref blk.EntryState;
        var stack = entryState.esStack.AsSpan(0, entryState.esStackDepth);

        for (var level = 0; level < stack.Length; level++)
        {
            var tree = stack[level].val;

            if (tree.Oper is GT_LCL_VAR or GT_LCL_FLD)
            {
                tree.Type = lvaGetDesc(tree.AsLclVarCommon().LclNum).Type;
            }
        }
    }

    /// <summary>Retype unmanaged call arguments from managed pointers to unmanaged ones.</summary>
    /// <param name="call">The call</param>
    /// <remarks>Make the "casting away" of GC explicit here instead of retyping.</remarks>
    public void impRetypeUnmanagedCallArgs(GenTreeCall call)
    {
        foreach (var arg in call.Args.Args)
        {
            var argNode = arg.EarlyNode;

            // We should not be passing gc typed args to an unmanaged call.
            if (varTypeIsGC(argNode.Type))
            {
                // Tolerate byrefs by casting to native int.
                //
                // This is needed or we'll generate inconsistent GC info
                // for this arg at the call site (gc info says byref,
                // pinvoke sig says native int).

                if (argNode.Type is TYP_BYREF)
                {
                    var cast = gtNewCastNode(TYP_I_IMPL, argNode, fromUnsigned: false, TYP_I_IMPL);
                    arg.EarlyNode = cast;
                }
                else
                {
                    NO_WAY("*** invalid IL: gc ref passed to unmanaged call");
                }
            }
        }
    }

    /// <summary>import a return or an explicit tail call</summary>
    /// <param name="prefixFlags">active IL prefixes</param>
    /// <param name="opcode">IL opcode</param>
    /// <returns>True if import was successful (may fail for some inlinees)</returns>
    public unsafe bool impReturnInstruction(int prefixFlags, ref OPCODE opcode)
    {
        var isTailCall = (prefixFlags & PREFIX_TAILCALL) is not 0;

#if DEBUG
        // If we are importing an inlinee and have GC ref locals we always
        // need to have a spill temp for the return value.  This temp
        // should have been set up in advance, over in fgFindBasicBlocks.
        if (compIsForInlining && impInlineInfo.HasGcRefLocals && (info.compRetType is not TYP_VOID))
        {
            assert(lvaInlineeReturnSpillTemp != BAD_VAR_NUM);
        }

        if (!compIsForInlining && ((prefixFlags & (PREFIX_TAILCALL_EXPLICIT | PREFIX_TAILCALL_STRESS)) is 0) && compStressCompile(STRESS_POISON_IMPLICIT_BYREFS, 25))
        {
            impPoisonImplicitByrefsBeforeReturn();
        }
#endif

        var op2 = null as GenTree;
        var op1 = null as GenTree;

        if (info.compRetType is not TYP_VOID)
        {
            op2 = impPopStack().val;

            if (!compIsForInlining)
            {
                impBashVarAddrsToI(op2);
                op2 = impImplicitIorI4Cast(op2, info.compRetType);
                op2 = impImplicitR4orR8Cast(op2, info.compRetType);

                assertImp(
                    (op2.Type.ActualType == info.compRetType.ActualType) ||
                    ((op2.Type is TYP_I_IMPL) && (info.compRetType is TYP_BYREF)) ||
                    ((op2.Type is TYP_BYREF) && (info.compRetType is TYP_I_IMPL)) ||
                    (varTypeIsFloating(op2.Type) && varTypeIsFloating(info.compRetType)) ||
                    (varTypeIsStruct(op2.Type) && varTypeIsStruct(info.compRetType))
                );

#if DEBUG
                if (!isTailCall && opts.compGcChecks && (info.compRetType == TYP_REF))
                {
                    // DDB 3483  : JIT Stress: early termination of GC ref's life time in exception code path
                    // VSW 440513: Incorrect gcinfo on the return value under DOTNET_JitGCChecks=1 for methods with
                    // one-return BB.

                    assert(op2.Type is TYP_REF);

                    // confirm that the argument is a GC pointer (for debugging (GC stress))
                    op2 = gtNewHelperCallNode(TYP_REF, CORINFO_HELP_CHECK_OBJ, op2);

                    if (verbose)
                    {
                        jitprintf("\ncompGcChecks tree:\n");
                        gtDispTree(op2);
                    }
                }
#endif
            }
            else
            {
                if (stackState.esStackDepth is not 0)
                {
                    assert(compIsForInlining);
                    JITDUMP("CALLSITE_COMPILATION_ERROR: inlinee's stack is not empty.");

                    compInlineResult.NoteFatal(InlineObservation.CALLSITE_COMPILATION_ERROR);
                    return false;
                }

#if DEBUG
                if (verbose)
                {
                    jitprintf("\n\n    Inlinee Return expression (before normalization)  =>\n");
                    gtDispTree(op2);
                }
#endif

                var inlCandInfo = impInlineInfo.inlineCandidateInfo;
                var inlRetExpr = inlCandInfo.retExpr;

                // Make sure the type matches the original call.

                var returnType = op2.Type.ActualType;
                var originalCallType = inlCandInfo.methInfo.args.retType.VarType.ActualType;

                if ((returnType != originalCallType) && (originalCallType is TYP_STRUCT))
                {
                    originalCallType = impNormStructType(inlCandInfo.methInfo.args.retTypeClass);
                }

                if (returnType != originalCallType)
                {
                    // Allow TYP_BYREF to be returned as TYP_I_IMPL and vice versa.
                    if (((returnType == TYP_BYREF) && (originalCallType == TYP_I_IMPL)) ||
                        ((returnType == TYP_I_IMPL) && (originalCallType == TYP_BYREF)))
                    {
                        JITDUMP($"Allowing return type mismatch: have {returnType.Name}, needed {originalCallType.Name}\n");
                    }
                    else
                    {
                        JITDUMP($"Return type mismatch: have {returnType.Name}, needed {originalCallType.Name}\n");
                        compInlineResult.NoteFatal(InlineObservation.CALLSITE_RETURN_TYPE_MISMATCH);
                        return false;
                    }
                }

                assert(inlRetExpr is not null);

                // Below, we are going to set impInlineInfo->retExpr to the tree with the return
                // expression. At this point, retExpr could already be set if there are multiple
                // return blocks (meaning fgNeedReturnSpillTemp() == true) and one of
                // the other blocks already set it. If there is only a single return block,
                // retExpr shouldn't be set. However, this is not true if we reimport a block
                // with a return. In that case, retExpr will be set, then the block will be
                // reimported, but retExpr won't get cleared as part of setting the block to
                // be reimported. The reimported retExpr value should be the same, so even if
                // we don't unconditionally overwrite it, it shouldn't matter.
                if (info.compRetNativeType != TYP_STRUCT)
                {
                    // compRetNativeType is not TYP_STRUCT.
                    // This implies it could be either a scalar type or SIMD vector type or
                    // a struct type that can be normalized to a scalar type.

                    if (varTypeIsStruct(info.compRetType))
                    {
                        noway_assert(info.compRetBuffArg == BAD_VAR_NUM);
                        // Handle calls with "fake" return buffers.
                        op2 = impFixupStructReturnType(op2);
                    }
                    else
                    {
                        // Do we have to normalize?
                        var fncRealRetType = info.compMethodInfo->args.retType.VarType;

                        // For RET_EXPR get the type info from the call. Regardless
                        // of whether it ends up inlined or not normalization will
                        // happen as part of that function's codegen.
                        var returnedTree = (op2.Oper is GT_RET_EXPR) ? op2.AsRetExpr().InlineCandidate : op2;

                        if ((varTypeIsSmall(returnedTree.Type) || varTypeIsSmall(fncRealRetType)) && fgCastNeeded(returnedTree, fncRealRetType))
                        {
                            // Small-typed return values are normalized by the callee
                            op2 = gtNewCastNode(TYP_INT, op2, fromUnsigned: false, fncRealRetType);
                        }
                    }

                    // If the call we're inlining was flagged as part of an enumerator
                    // GDV, and we're replacing it with another call, flag that call instead.
                    //
                    // This handles cases like ReadOnlyArray where GetEnumerator is
                    // expressed via another GetEnumerator call.
                    //
                    if ((info.compRetType is TYP_REF) && hasImpEnumeratorGdvLocalMap)
                    {
                        var origCall = impInlineInfo.iciCall;
                        var map = ImpEnumeratorGdvLocalMap;

                        assert(origCall is not null);

                        if (map.TryGetValue(origCall, out var enumeratorLcl))
                        {
                            var returnValue = op2;

                            if (returnValue.Oper is GT_RET_EXPR)
                            {
                                returnValue = returnValue.AsRetExpr().InlineCandidate;
                            }

                            if (returnValue.Oper.IsCall)
                            {
                                JITDUMP($"Flagging [{returnValue.TreeId:D6}] for enumerator cloning via V{enumeratorLcl:D2}\n");
                                _ = map.Remove(origCall);
                                map[returnValue] = enumeratorLcl;
                            }
                        }
                    }

                    if (fgNeedReturnSpillTemp)
                    {
                        assert(info.compRetNativeType != TYP_VOID);

                        // If this method returns a ref type, track the actual types seen in the returns.
                        if (info.compRetType == TYP_REF)
                        {
                            var returnClsHnd = gtGetClassHandle(op2, out var isExact, out var isNonNull);

                            if (inlRetExpr.SubstExpr is null)
                            {
                                // This is the first return, so best known type is the type
                                // of this return value.
                                impInlineInfo.retExprClassHnd = returnClsHnd;
                                impInlineInfo.retExprClassHndIsExact = isExact;
                            }
                            else
                            {
                                if (impInlineInfo.retExprClassHnd != returnClsHnd)
                                {
                                    // This return site type differs from earlier seen sites,
                                    // so reset the info and we'll fall back to using the method's
                                    // declared return type for the return spill temp.
                                    impInlineInfo.retExprClassHnd = null;
                                    impInlineInfo.retExprClassHndIsExact = false;
                                }
                                else
                                {
                                    // Same return type, but we may need to update exactness.
                                    impInlineInfo.retExprClassHndIsExact &= isExact;
                                }
                            }
                        }

                        impStoreToTemp(lvaInlineeReturnSpillTemp, op2, CHECK_SPILL_ALL);

                        var lclRetType = lvaGetDesc(lvaInlineeReturnSpillTemp).Type;
                        var tmpOp2 = gtNewLclvNode(lclRetType, lvaInlineeReturnSpillTemp);

                        op2 = tmpOp2;
#if DEBUG
                        if (inlRetExpr.SubstExpr is not null)
                        {
                            // Some other block(s) have seen the CEE_RET first.
                            // Better they spilled to the same temp.
                            assert(inlRetExpr.SubstExpr.Oper is GT_LCL_VAR);
                            assert(inlRetExpr.SubstExpr.AsLclVarCommon().LclNum == op2.AsLclVarCommon().LclNum);
                        }
#endif
                    }

#if DEBUG
                    if (verbose)
                    {
                        jitprintf("\n\n    Inlinee Return expression (after normalization) =>\n");
                        gtDispTree(op2);
                    }
#endif

                    // Report the return expression
                    inlRetExpr.SubstExpr = op2;
                }
                else
                {
                    // compRetNativeType is TYP_STRUCT.
                    // This implies that struct return via RetBuf arg or multi-reg struct return.
                    var iciCall = impInlineInfo.iciCall;

                    // Assign the inlinee return into a spill temp.
                    if (fgNeedReturnSpillTemp)
                    {
                        // in this case we have to insert multiple struct copies to the temp and the retexpr is just the temp.
                        impStoreToTemp(lvaInlineeReturnSpillTemp, op2, CHECK_SPILL_ALL);
                    }

                    assert(iciCall is not null);

                    if (compMethodReturnsMultiRegRetType)
                    {
                        assert(!iciCall.ShouldHaveRetBufArg);

                        if (fgNeedReturnSpillTemp)
                        {
                            // The inlinee compiler has figured out the type of the temp already. Use it here.
                            inlRetExpr.SubstExpr ??= gtNewLclvNode(lvaTable[lvaInlineeReturnSpillTemp].Type, lvaInlineeReturnSpillTemp);
                        }
                        else
                        {
                            inlRetExpr.SubstExpr = op2;
                        }
                    }
                    else
                    {
                        // The struct was to be returned via a return buffer.
                        assert(iciCall.Args.HasRetBuffer);

                        var dest = gtCloneExpr(iciCall.Args.RetBufferArg.EarlyNode);

                        if (fgNeedReturnSpillTemp)
                        {
                            // If this is the first return we have seen set the retExpr.
                            inlRetExpr.SubstExpr ??= impStoreStructPtr(dest, gtNewLclvNode(info.compRetType, lvaInlineeReturnSpillTemp), CHECK_SPILL_ALL);
                        }
                        else
                        {
                            inlRetExpr.SubstExpr = impStoreStructPtr(dest, op2, CHECK_SPILL_ALL);
                        }
                    }
                }

                // If gtSubstExpr is an arbitrary tree then we may need to
                // propagate mandatory "IR presence" flags to the BB it ends up in.
                inlRetExpr.SubstBB = fgNeedReturnSpillTemp ? null : compCurBB;
            }
        }

        if (compIsForInlining)
        {
            return true;
        }

        if (info.compRetBuffArg != BAD_VAR_NUM)
        {
            var retBuffType = lvaGetDesc(info.compRetBuffArg).Type;

            // Assign value to return buff (first param)
            var retBuffAddr = gtNewLclvNode(retBuffType, info.compRetBuffArg, impCurStmtDI.Location.Offset);

            assert(op2 is not null);
            op2 = impStoreStructPtr(retBuffAddr, op2, CHECK_SPILL_ALL, GTF_IND_TGT_NOT_HEAP);
            impAppendTree(op2, CHECK_SPILL_NONE, impCurStmtDI);

            // There are cases where the address of the implicit RetBuf should be returned explicitly.
            if (compMethodReturnsRetBufAddr)
            {
                op1 = gtNewUnaryNode(GT_RETURN, retBuffType, gtNewLclvNode(retBuffType, info.compRetBuffArg));
            }
            else
            {
                op1 = gtNewUnaryNode(GT_RETURN, TYP_VOID, op1: null);
            }
        }
        else if (varTypeIsStruct(info.compRetType))
        {
            assert(op2 is not null);

#if !FEATURE_MULTIREG_RET
            // For both ARM architectures the HFA native types are maintained as structs.
            // Also on System V AMD64 the multireg structs returns are also left as structs.
            noway_assert(info.compRetNativeType != TYP_STRUCT);
#endif
            op2 = impFixupStructReturnType(op2);
            op1 = gtNewUnaryNode(GT_RETURN, info.compRetType.ActualType, op2);
        }
        else if (info.compRetType != TYP_VOID)
        {
            op1 = gtNewUnaryNode(GT_RETURN, info.compRetType.ActualType, op2);
        }
        else
        {
            op1 = gtNewUnaryNode(GT_RETURN, TYP_VOID, op1: null);
        }

        if (isTailCall)
        {
            // We must have imported a tailcall and jumped to RET
            assert(stackState.esStackDepth is 0 && impOpcodeIsCallOpcode(opcode));

            opcode = CEE_RET; // To prevent trying to spill if CALL_SITE_BOUNDARIES

            // impImportCall() would have already appended TYP_VOID calls
            if (info.compRetType == TYP_VOID)
            {
                return true;
            }
        }

        _ = impAppendTree(op1, CHECK_SPILL_NONE, impCurStmtDI);

#if DEBUG
        // Remember at which BC offset the tree was finished
        impNoteLastILoffs();
#endif

        return true;
    }

    /// <summary>Import a dictionary lookup to access a handle in code shared between generic instantiations.</summary>
    /// <param name="lookup"></param>
    /// <param name="compileTimeHandle"></param>
    /// <returns></returns>
    public unsafe GenTree impRuntimeLookupToTree(in CORINFO_LOOKUP lookup, void* compileTimeHandle)
    {
        // The lookup depends on the typeContext which is only available at
        // runtime, and not at compile-time.
        // pLookup->token1 and pLookup->token2 specify the handle that is needed.
        // The cases are:
        //
        // 1. pLookup->indirections == CORINFO_USEHELPER : Call a helper passing it the
        //    instantiation-specific handle, and the tokens to lookup the handle.
        // 2. pLookup->indirections == CORINFO_USENULL : Pass null. Callee won't dereference
        //    the context.
        // 3. pLookup->indirections != CORINFO_USEHELPER or CORINFO_USENULL :
        //    2a. pLookup->testForNull == false : Dereference the instantiation-specific handle
        //        to get the handle.
        //    2b. pLookup->testForNull == true : Dereference the instantiation-specific handle.
        //        If it is non-NULL, it is the handle required. Else, call a helper
        //        to lookup the handle.

        var ctxTree = getRuntimeContextTree(lookup.lookupKind.runtimeLookupKind);
        ref var runtimeLookup = ref lookup.runtimeLookup;

        if (runtimeLookup.indirections is CORINFO_USEHELPER)
        {
            // It's available only via the run-time helper function
            return gtNewRuntimeLookupHelperCallNode(runtimeLookup, ctxTree, compileTimeHandle);
        }

#if FEATURE_READYTORUN
        if (runtimeLookup.indirections is CORINFO_USENULL)
        {
            return gtNewIconNode(TYP_I_IMPL, 0);
        }
#endif

        if (runtimeLookup.testForNull)
        {
            // Import just a helper call and mark it for late expansion in fgExpandRuntimeLookups phase
            assert(runtimeLookup.indirections is not 0);
            var helperCall = gtNewRuntimeLookupHelperCallNode(runtimeLookup, ctxTree, compileTimeHandle);

            // Spilling it to a temp improves CQ (mainly in Tier0)
            var callLclNum = lvaGrabTemp(shortLifetime: true, "spilling helperCall");
            impStoreToTemp(callLclNum, helperCall, CHECK_SPILL_NONE);

            return gtNewLclvNode(helperCall.Type, callLclNum);
        }

        // Size-check is not expected without testForNull
        assert(runtimeLookup.sizeOffset == CORINFO_NO_SIZE_CHECK);

        // Slot pointer
        var slotPtrTree = ctxTree;
        var indOffTree = null as GenTree;

        // TODO-CQ: consider relaxing where it's safe to do so
        var ctxTreeIsInvariant = !compIsForInlining;

        // Applied repeated indirections
        for (var i = 0; i < runtimeLookup.indirections; i++)
        {
            if ((i is 1 && runtimeLookup.indirectFirstOffset) || (i is 2 && runtimeLookup.indirectSecondOffset))
            {
                indOffTree = impCloneExpr(slotPtrTree, out slotPtrTree, CHECK_SPILL_ALL, "impRuntimeLookup indirectOffset");
                assert(slotPtrTree is not null);
            }

            if (i is not 0)
            {
                slotPtrTree = gtNewIndir(TYP_I_IMPL, slotPtrTree, ctxTreeIsInvariant ? (GTF_IND_NONFAULTING | GTF_IND_INVARIANT) : GTF_EMPTY);
            }

            if (((i is 1) && runtimeLookup.indirectFirstOffset) || ((i is 2) && runtimeLookup.indirectSecondOffset))
            {
                assert(indOffTree is not null);
                slotPtrTree = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, indOffTree, slotPtrTree);
            }

            if (runtimeLookup.offsets[i] is not 0)
            {
                slotPtrTree = gtNewBinaryNode(GT_ADD, TYP_I_IMPL, slotPtrTree, gtNewIconNode(TYP_I_IMPL, runtimeLookup.offsets[i]));
            }
        }

        // No null test required
        assert(!runtimeLookup.testForNull);

        return (runtimeLookup.indirections is not 0) ? gtNewIndir(TYP_I_IMPL, slotPtrTree, ctxTreeIsInvariant ? (GTF_IND_NONFAULTING | GTF_IND_INVARIANT) : GTF_EMPTY) : slotPtrTree;
    }

    public void impSaveStackState(out SavedStack savePtr, bool copy)
    {
        var depth = stackState.esStackDepth;
        var stack = stackState.esStack.AsSpan(0, depth);
        var trees = (depth is not 0) ? stack.ToArray() : [];

        savePtr.ssDepth = depth;
        savePtr.ssTrees = trees;

        if (copy)
        {
            // Make a fresh copy of all the stack entries

            for (var level = 0; level < stack.Length; level++)
            {
                ref var srcEntry = ref stack[level];
                ref var dstEntry = ref trees[level];

                dstEntry.seTypeInfo = srcEntry.seTypeInfo;
                var tree = srcEntry.val;

                if (impValidSpilledStackEntry(tree))
                {
                    dstEntry.val = gtCloneExpr(tree);
                }
                else
                {
                    NO_WAY("Bad oper - Not covered by impValidSpilledStackEntry()");
                }
            }
        }
    }

    /// <summary>Register a call as being async and set up context handling information depending on the IL.</summary>
    /// <param name="call">The call</param>
    /// <param name="opcode">The IL opcode for the call</param>
    /// <param name="prefixFlags">Flags containing context handling information from IL</param>
    /// <param name="callDI">Debug info for the async call</param>
    public unsafe void impSetupAsyncCall(GenTreeCall call, OPCODE opcode, int prefixFlags, in DebugInfo callDI)
    {
        Unsafe.SkipInit(out AsyncCallInfo asyncInfo);

        if (compIsForInlining)
        {
            if (!_nextAwaitIsTail)
            {
                compInlineResult.NoteFatal(InlineObservation.CALLEE_AWAIT);
                return;
            }

            var inlCall = impInlineInfo.iciCall;
            assert(inlCall is not null);

            JITDUMP($"Call [{inlCall.TreeId:D6}] is to call with a tail async call [{call.TreeId:D6}]\n");

            assert(inlCall.IsAsync);
            var inlAsyncInfo = inlCall._asyncInfo;

            asyncInfo.ContinuationContextHandling = inlAsyncInfo.ContinuationContextHandling;

            // Validate that below code won't override the handling
            assert((prefixFlags & PREFIX_IS_TASK_AWAIT) is 0);
            _nextAwaitIsTail = false;

            asyncInfo.IsTailAwait = inlAsyncInfo.IsTailAwait;
        }

        var newSourceTypes = ICorDebugInfo.ASYNC;
        newSourceTypes |= (callDI.Location.SourceTypes & ~ICorDebugInfo.CALL_INSTRUCTION);

        var newILLocation = new ILLocation(callDI.Location.Offset, newSourceTypes);
        asyncInfo.CallAsyncDebugInfo = new DebugInfo(callDI.InlineContext, newILLocation);

        if ((prefixFlags & PREFIX_IS_TASK_AWAIT) is not 0)
        {
            JITDUMP("Call is an async task await\n");

            if ((prefixFlags & PREFIX_TASK_AWAIT_CONTINUE_ON_CAPTURED_CONTEXT) is not 0)
            {
                asyncInfo.ContinuationContextHandling = ContinuationContextHandling.ContinueOnCapturedContext;
                JITDUMP("  Continuation continues on captured context\n");
            }
            else
            {
                asyncInfo.ContinuationContextHandling = ContinuationContextHandling.ContinueOnThreadPool;
                JITDUMP("  Continuation continues on thread pool\n");
            }
        }
        else
        {
            JITDUMP("Call is an async non-task await\n");
        }

        if (_nextAwaitIsTail)
        {
            asyncInfo.ContinuationContextHandling = ContinuationContextHandling.None;
            asyncInfo.IsTailAwait = true;
            _nextAwaitIsTail = false;
        }

        call.SetIsAsync(asyncInfo);

#if DEBUG
        if ((JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] is not 0) && (call._callType is CT_USER_FUNC))
        {
            // Query the async variants (twice, to get both directions)
            var method = call._callMethHnd;

            bool variantIsThunk;
            method = info.compCompHnd->getAsyncOtherVariant(method, &variantIsThunk);

            if (method != NO_METHOD_HANDLE)
            {
                method = info.compCompHnd->getAsyncOtherVariant(method, &variantIsThunk);
            }
        }
#endif
    }

    /// <summary>Creates a new Vector128.CreateScalar node for a System.Half value</summary>
    /// <param name="op1">The System.Half value</param>
    /// <returns>The Vector128.CreateScalar node that contains op1</returns>
    public GenTree impSimdCreateScalarHalf(GenTree op1)
    {
        int op1Tmp;

        if (op1.Oper is not GT_LCL_VAR)
        {
            op1Tmp = lvaGrabTemp(shortLifetime: true, "System.Half tmp");
            impStoreToTemp(op1Tmp, op1, CHECK_SPILL_ALL);
        }
        else
        {
            op1Tmp = op1.AsLclVarCommon().LclNum;
        }

        op1 = gtNewLclFldNode(TYP_USHORT, op1Tmp, lclOffs: 0);
        return gtNewSimdCreateScalarNode(TYP_SIMD16, op1, TYP_USHORT, 16);
    }

    /// <summary>Creates a new Vector128.ToScalar node for a System.Half value</summary>
    /// <param name="op1">The Vector128 from which to extract the System.Half value</param>
    /// <param name="halfClsHnd">The class handle for System.Half</param>
    /// <returns>The System.Half value extracted from op1</returns>
    public unsafe GenTree impSimdToScalarHalf(GenTree op1, CORINFO_CLASS_HANDLE halfClsHnd)
    {
        assert(IsSystemHalfClass(halfClsHnd));

        var resTmp = lvaGrabTemp(shortLifetime: true, "System.Half tmp");
        lvaSetStruct(resTmp, halfClsHnd, unsafeValueClsCheck: false);

        op1 = gtNewSimdToScalarNode(TYP_INT, op1, TYP_USHORT, 16);
        op1 = gtNewStoreLclFldNode(TYP_USHORT, resTmp, offset: 0, op1);

        _ = impAppendTree(op1, CHECK_SPILL_ALL, impCurStmtDI);
        return gtNewLclvNode(TYP_STRUCT, resTmp);
    }

    /// <summary>Spill all trees referencing the given local.</summary>
    /// <param name="lclNum">The local's number</param>
    /// <param name="chkLevel">Height (exclusive) of the portion of the stack to check</param>
    public void impSpillLclRefs(int lclNum, int chkLevel)
    {
        // Before we make any appends to the tree list we must spill the
        // "special" side effects (GTF_ORDER_SIDEEFF) - GT_CATCH_ARG.
        impSpillSpecialSideEff();

        if (chkLevel == CHECK_SPILL_ALL)
        {
            chkLevel = stackState.esStackDepth;
        }

        assert(chkLevel <= stackState.esStackDepth);
        assert(compCurBB is not null);

        var stack = stackState.esStack.AsSpan(0, chkLevel);

        for (var level = 0; level < stack.Length; level++)
        {
            var tree = stack[level].val;

            // If the tree may throw an exception, and the block has a handler,
            // then we need to spill stores to the local if the local is on entry
            // to the handler. Just spill 'em all without considering the liveness

            var xcptnCaught = ehBlockHasExnFlowDsc(compCurBB) && ((tree.Flags & (GTF_CALL | GTF_EXCEPT)) is not 0);

            //Skip the tree if it doesn't have an affected reference, unless xcptnCaught

            if (xcptnCaught || gtHasRef(tree, lclNum))
            {
                _ = impSpillStackEntry(level, BAD_VAR_NUM, assertOnRecursion: false, "impSpillLclRefs");
            }
        }
    }

    /// <summary>If the stack entry is a tree with side effects in it, assign that tree to a temp and replace it on the stack with refs to its temp.</summary>
    /// <param name="spillGlobEffects"></param>
    /// <param name="i">the stack entry which will be checked and spilled.</param>
    /// <param name="reason"></param>
    public void impSpillSideEffect(bool spillGlobEffects, int i, string reason)
    {
        assert(i <= stackState.esStackDepth);

        var spillFlags = spillGlobEffects ? GTF_GLOB_EFFECT : GTF_SIDE_EFFECT;
        var tree = stackState.esStack[i].val;

        if (((tree.Flags & spillFlags) is not 0) || (spillGlobEffects && !impIsAddressInLocal(tree) && gtHasLocalsWithAddrOp(tree)))
        {
            // When spillGlobEffects is true
            //   No need to spill the LCL_ADDR nodes.
            //   Spill if we still see GT_LCL_VAR that contains lvHasLdAddrOp or lvAddrTaken flag.
            _ = impSpillStackEntry(i, BAD_VAR_NUM, assertOnRecursion: false, reason);
        }
    }

    /// <summary>If the stack contains any trees with side effects in them, assign those trees to temps and replace them on the stack with refs to their temps.</summary>
    /// <param name="spillGlobEffects"></param>
    /// <param name="chkLevel">[0..chkLevel) is the portion of the stack which will be checked and spilled.</param>
    /// <param name="reason"></param>
    public void impSpillSideEffects(bool spillGlobEffects, int chkLevel, string reason)
    {
        assert(chkLevel != CHECK_SPILL_NONE);

        // Before we make any appends to the tree list we must spill the "special" side effects (GTF_ORDER_SIDEEFF on a GT_CATCH_ARG)
        impSpillSpecialSideEff();

        if (chkLevel == CHECK_SPILL_ALL)
        {
            chkLevel = stackState.esStackDepth;
        }

        assert(chkLevel <= stackState.esStackDepth);

        for (var i = 0; i < chkLevel; i++)
        {
            impSpillSideEffect(spillGlobEffects, i, reason);
        }
    }

    /// <summary>If the stack contains any trees with special side effects in them, assign those trees to temps and replace them on the stack with refs to their temps.</summary>
    public void impSpillSpecialSideEff()
    {
        // Only exception objects need to be carefully handled
        assert(compCurBB is not null);

        if (compCurBB.CatchType is BBCT_NONE)
        {
            return;
        }

        var stack = stackState.esStack.AsSpan(0, stackState.esStackDepth);

        for (var level = 0; level < stack.Length; level++)
        {
            var tree = stack[level].val;

            // Make sure if we have an exception object in the sub tree we spill ourselves.
            if (gtHasCatchArg(tree))
            {
                _ = impSpillStackEntry(level, BAD_VAR_NUM, assertOnRecursion: false, "impSpillSpecialSideEff");
            }
        }
    }

    /// <summary>Ensure that the stack has only spilled values</summary>
    /// <param name="spillLeaves"></param>
    public void impSpillStackEnsure(bool spillLeaves = false)
    {
        assert(!spillLeaves || opts.compDbgCode);

        for (var level = 0; level < stackState.esStackDepth; level++)
        {
            var tree = stackState.esStack[level].val;

            if (!spillLeaves && tree.Oper.IsLeaf)
            {
                continue;
            }

            // Temps introduced by the importer itself don't need to be spilled

            var isTempLcl = (tree.Oper is GT_LCL_VAR) && (tree.AsLclVarCommon().LclNum >= info.compLocalsCount);

            if (isTempLcl)
            {
                continue;
            }
            _ = impSpillStackEntry(level, BAD_VAR_NUM, assertOnRecursion: false, "impSpillStackEnsure");
        }
    }

    public unsafe bool impSpillStackEntry(int level, int tnum, bool assertOnRecursion, string reason)
    {
#if DEBUG
        using var guard = new RecursiveGuard(ref impNestedStackSpill, assertOnRecursion);
#endif

        ref var stackEntry = ref stackState.esStack[level];
        var tree = stackEntry.val;

        // Allocate a temp if we haven't been asked to use a particular one

        if ((tnum != BAD_VAR_NUM) && (tnum >= lvaCount))
        {
            return false;
        }

        var isNewTemp = false;

        if (tnum == BAD_VAR_NUM)
        {
            tnum = lvaGrabTemp(shortLifetime: true, reason);
            isNewTemp = true;
        }
        ref var lvaDsc = ref lvaGetDesc(tnum);

        // Assign the spilled entry to the temp
        impStoreToTemp(tnum, tree, level);

        if (isNewTemp)
        {
            assert(!lvaDsc.lvSingleDef);
            lvaDsc.lvSingleDef = true;
            JITDUMP($"Marked V{tnum:D2} as a single def temp\n");

            // If temp is newly introduced and a ref type, grab what type info we can.
            if (lvaDsc.Type == TYP_REF)
            {
                var stkHnd = stackEntry.seTypeInfo.ClassHandleForObjRef;
                lvaSetClass(tnum, tree, stkHnd);
            }

            // If we're assigning a GT_RET_EXPR, note the temp over on the call,
            // so the inliner can use it in case it needs a return spill temp.
            if (tree.Oper is GT_RET_EXPR)
            {
                JITDUMP($"\n*** see V{tnum:D2} = GT_RET_EXPR, noting temp\n");
                var call = tree.AsRetExpr().InlineCandidate.AsCall();

                if (call.IsGuardedDevirtualizationCandidate)
                {
                    for (byte i = 0; i < call.InlineCandidatesCount; i++)
                    {
                        call.GetGdvCandidateInfo(i).preexistingSpillTemp = tnum;
                    }
                }
                else
                {
                    call.SingleInlineCandidateInfo.preexistingSpillTemp = tnum;
                }
            }
        }

        // The tree type may be modified by impStoreToTemp, so use the type of the lclVar.
        var type = lvaDsc.Type.ActualType;
        var temp = gtNewLclvNode(type, tnum);
        stackEntry.val = temp;

        return true;
    }

    public unsafe GenTree? impSRCSUnsafeIntrinsic(NamedIntrinsic intrinsic, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE method, in CORINFO_SIG_INFO sig, in CORINFO_RESOLVED_TOKEN resolvedToken)
    {
        // NextCallRetAddr requires a CALL, so return null.
        if (info.compHasNextCallRetAddr)
        {
            return null;
        }

        assert(sig.sigInst.classInstCount is 0);

        switch (intrinsic)
        {
            case NI_SRCS_UNSAFE_Add:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // sizeof !!T
                // conv.i
                // mul
                // add
                // ret

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                impBashVarAddrsToI(op1);
                impBashVarAddrsToI(op2);

                op2 = impImplicitIorI4Cast(op2, TYP_I_IMPL);

                var classSize = info.compCompHnd->getClassSize(sig.sigInst.methInst[0]);

                if (classSize is not 1)
                {
                    var size = gtNewIconNode(TYP_I_IMPL, classSize);
                    op2 = gtNewBinaryNode(GT_MUL, TYP_I_IMPL, op2, size);
                }

                var type = impGetByRefResultType(GT_ADD, fUnsigned: false, ref op1, ref op2);
                return gtNewBinaryNode(GT_ADD, type, op1, op2);
            }

            case NI_SRCS_UNSAFE_AddByteOffset:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // add
                // ret

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                impBashVarAddrsToI(op1);
                impBashVarAddrsToI(op2);

                var type = impGetByRefResultType(GT_ADD, fUnsigned: false, ref op1, ref op2);
                return gtNewBinaryNode(GT_ADD, type, op1, op2);
            }

            case NI_SRCS_UNSAFE_AreSame:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // ceq
                // ret

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                var tmp = gtNewBinaryNode(GT_EQ, TYP_INT, op1, op2);
                return gtFoldExpr(tmp);
            }

            case NI_SRCS_UNSAFE_As:
            {
                assert((sig.sigInst.methInstCount is 1) || (sig.sigInst.methInstCount is 2));

                if (sig.sigInst.methInstCount is 1)
                {
                    CORINFO_SIG_INFO exactSig;
                    info.compCompHnd->getMethodSig(resolvedToken.hMethod, &exactSig);

                    var inst = exactSig.sigInst.methInst[0];
                    assert(inst is not null);

                    var op = impPopStack().val;
                    assert(op.Type is TYP_REF);

                    JITDUMP($"Expanding Unsafe.As<{eeGetClassName(inst)}>(...)\n");

                    var oldClass = gtGetClassHandle(op, out var isExact, out var isNonNull);

                    if ((oldClass != NO_CLASS_HANDLE) && ((oldClass == inst) || !info.compCompHnd->isMoreSpecificType(oldClass, inst)))
                    {
                        JITDUMP($"Unsafe.As: Keep using old '{eeGetClassName(oldClass)}' type\n");
                        return op;
                    }

                    // In order to change the class handle of the object we need to spill it to a temp
                    // and update class info for that temp.
                    var localNum = lvaGrabTemp(shortLifetime: true, "updating class info");
                    impStoreToTemp(localNum, op, CHECK_SPILL_ALL);

                    // NOTE: we still can't say for sure that it is the exact type of the argument
                    lvaSetClass(localNum, inst, isExact: false);
                    return gtNewLclvNode(TYP_REF, localNum);
                }

                // ldarg.0
                // ret

                return impPopStack().val;
            }

            case NI_SRCS_UNSAFE_AsPointer:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // conv.u
                // ret

                var op1 = impPopStack().val;
                impBashVarAddrsToI(op1);

                return gtNewCastNode(TYP_I_IMPL, op1, fromUnsigned: false, TYP_I_IMPL);
            }

            case NI_SRCS_UNSAFE_AsRef:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ret

                return impPopStack().val;
            }

            case NI_SRCS_UNSAFE_BitCast:
            {
                assert(sig.sigInst.methInstCount is 2);

                var fromTypeHnd = sig.sigInst.methInst[0];
                var fromType = TypeHandleToVarType(fromTypeHnd, out var fromLayout);

                var toTypeHnd = sig.sigInst.methInst[1];
                var toType = TypeHandleToVarType(toTypeHnd, out var toLayout);

                if ((fromType is TYP_REF) || (toType is TYP_REF))
                {
                    // Fallback to the software implementation to throw for reference types
                    return null;
                }

                var fromSize = (fromLayout is not null) ? fromLayout.Size : fromType.Size;
                var toSize = (toLayout is not null) ? toLayout.Size : toType.Size;

                // Runtime requires all types to be at least 1-byte
                assert((fromSize is not 0) && (toSize is not 0));

                if (fromSize != toSize)
                {
                    // Fallback to the software implementation to throw when sizes don't match
                    return null;
                }

                var op1 = impPopStack().val;

                op1 = impImplicitR4orR8Cast(op1, fromType);
                op1 = impImplicitIorI4Cast(op1, fromType);

                var valType = op1.Type;
                var effectiveVal = op1.EffectiveVal;

                if (effectiveVal.Oper is GT_LCL_VAR)
                {
                    valType = lvaGetDesc(effectiveVal.AsLclVar().LclNum).Type;
                }

                // Handle matching handles, compatible struct layouts or integrals where we can simply return op1
                if (varTypeIsSmall(toType))
                {
                    if (genActualTypeIsInt(valType))
                    {
                        if (fgCastNeeded(op1, toType))
                        {
                            op1 = gtNewCastNode(TYP_INT, op1, false, toType);
                        }
                        return op1;
                    }
                }
                else if (((toType != TYP_STRUCT) && (valType.ActualType == toType)) || ClassLayout.AreCompatible(fromLayout, toLayout))
                {
                    return op1;
                }

                // Handle bitcasting between floating and same sized integral, such as `float` to `int`
                if (varTypeIsFloating(fromType) && varTypeIsIntegral(toType))
                {
                    if (op1.Oper.IsCnsFltOrDbl)
                    {
                        if (fromType is TYP_DOUBLE)
                        {
                            var f64Cns = op1.AsDblCon().DconVal;
                            return gtNewLconNode(BitConverter.DoubleToInt64Bits(f64Cns));
                        }
                        else
                        {
                            assert(fromType is TYP_FLOAT);
                            var f32Cns = (float)op1.AsDblCon().DconVal;
                            return gtNewIconNode(TYP_INT, BitConverter.SingleToInt32Bits(f32Cns));
                        }
                    }
                    else if (TargetArchitecture.Is64Bit || (fromType is TYP_FLOAT))
                    {
                        // TODO-CQ: We should support this on 32-bit via decomposition
                        toType = varTypeToSigned(toType);
                        return gtNewBitCastNode(toType, op1);
                    }
                }
                else if (varTypeIsIntegral(fromType) && varTypeIsFloating(toType))
                {
                    if (op1.Oper.IsIntegralConst)
                    {
                        if (toType is TYP_DOUBLE)
                        {
                            var i64Cns = op1.AsIntConCommon().LngValue;
                            return gtNewDconNode(TYP_DOUBLE, BitConverter.Int64BitsToDouble(i64Cns));
                        }
                        else
                        {
                            assert(toType is TYP_FLOAT);

                            var i32Cns = (int)(op1.AsIntConCommon().IconValue);
                            return gtNewDconNode(TYP_FLOAT, BitConverter.Int32BitsToSingle(i32Cns));
                        }
                    }
                    else if (TargetArchitecture.Is64Bit || (toType is TYP_FLOAT))
                    {
                        // TODO-CQ: We should support this on 32-bit via decomposition
                        return gtNewBitCastNode(toType, op1);
                    }
                }

                GenTree addr;
                var indirFlags = GTF_EMPTY;

                if (varTypeIsIntegral(valType) && (valType.Size < fromSize))
                {
                    var lclNum = lvaGrabTemp(shortLifetime: true, "bitcast small type extension");
                    impStoreToTemp(lclNum, op1, CHECK_SPILL_ALL);
                    addr = gtNewLclVarAddrNode(TYP_I_IMPL, lclNum);
                }
                else
                {
                    addr = impGetNodeAddr(op1, CHECK_SPILL_ALL, GTF_IND_MUST_PRESERVE_FLAGS, out indirFlags);
                }

                if (info.compCompHnd->getClassAlignmentRequirement(fromTypeHnd) < info.compCompHnd->getClassAlignmentRequirement(toTypeHnd))
                {
                    indirFlags |= GTF_IND_UNALIGNED;
                }
                return gtNewLoadValueNode(toType, addr, toLayout, indirFlags);
            }

            case NI_SRCS_UNSAFE_ByteOffset:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.1
                // ldarg.0
                // sub
                // ret

                impSpillSideEffect(true, stackState.esStackDepth - 2, ("Spilling op1 side effects for Unsafe.ByteOffset"));

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                impBashVarAddrsToI(op1);
                impBashVarAddrsToI(op2);

                return gtNewBinaryNode(GT_SUB, TYP_I_IMPL, op2, op1);
            }

            case NI_SRCS_UNSAFE_Copy:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // ldobj !!T
                // stobj !!T
                // ret

                var typeHnd = sig.sigInst.methInst[0];
                var type = TypeHandleToVarType(typeHnd, out var layout);

                var source = impPopStack().val;
                var dest = impPopStack().val;

                var value = gtNewLoadValueNode(type, source, layout);
                return gtNewStoreValueNode(type, dest, value, layout);
            }

            case NI_SRCS_UNSAFE_CopyBlock:
            {
                assert(sig.sigInst.methInstCount is 0);

                // ldarg.0
                // ldarg.1
                // ldarg.2
                // cpblk
                // ret

                return null;
            }

            case NI_SRCS_UNSAFE_CopyBlockUnaligned:
            {
                assert(sig.sigInst.methInstCount is 0);

                // ldarg.0
                // ldarg.1
                // ldarg.2
                // unaligned. 0x1
                // cpblk
                // ret

                return null;
            }

            case NI_SRCS_UNSAFE_InitBlock:
            {
                assert(sig.sigInst.methInstCount is 0);

                // ldarg.0
                // ldarg.1
                // ldarg.2
                // initblk
                // ret

                return null;
            }

            case NI_SRCS_UNSAFE_InitBlockUnaligned:
            {
                assert(sig.sigInst.methInstCount is 0);

                // ldarg.0
                // ldarg.1
                // ldarg.2
                // unaligned. 0x1
                // initblk
                // ret

                return null;
            }

            case NI_SRCS_UNSAFE_IsAddressGreaterThan:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // cgt.un
                // ret

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                var tmp = gtNewBinaryNode(GT_GT, TYP_INT, op1, op2);
                tmp.IsUnsigned = true;
                return gtFoldExpr(tmp);
            }

            case NI_SRCS_UNSAFE_IsAddressGreaterThanOrEqualTo:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // clt.un
                // ldc.i4.0
                // ceq
                // ret

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                var tmp = gtNewBinaryNode(GT_GE, TYP_INT, op1, op2);
                tmp.IsUnsigned = true;
                return gtFoldExpr(tmp);
            }

            case NI_SRCS_UNSAFE_IsAddressLessThan:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // clt.un
                // ret

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                var tmp = gtNewBinaryNode(GT_LT, TYP_INT, op1, op2);
                tmp.IsUnsigned = true;
                return gtFoldExpr(tmp);
            }

            case NI_SRCS_UNSAFE_IsAddressLessThanOrEqualTo:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // cgt.un
                // ldc.i4.0
                // ceq
                // ret

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                var tmp = gtNewBinaryNode(GT_LE, TYP_INT, op1, op2);
                tmp.IsUnsigned = true;
                return gtFoldExpr(tmp);
            }

            case NI_SRCS_UNSAFE_IsNullRef:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldc.i4.0
                // conv.u
                // ceq
                // ret

                var op1 = impPopStack().val;
                var cns = gtNewIconNode(TYP_BYREF, 0);

                var tmp = gtNewBinaryNode(GT_EQ, TYP_INT, op1, cns);
                return gtFoldExpr(tmp);
            }

            case NI_SRCS_UNSAFE_NullRef:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldc.i4.0
                // conv.u
                // ret

                return gtNewIconNode(TYP_BYREF, 0);
            }

            case NI_SRCS_UNSAFE_Read:
            case NI_SRCS_UNSAFE_ReadUnaligned:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // if NI_SRCS_UNSAFE_ReadUnaligned: unaligned. 0x1
                // ldobj !!T
                // ret

                var typeHnd = sig.sigInst.methInst[0];
                var type = TypeHandleToVarType(typeHnd, out var layout);
                var flags = (intrinsic is NI_SRCS_UNSAFE_ReadUnaligned) ? GTF_IND_UNALIGNED : GTF_EMPTY;

                return gtNewLoadValueNode(type, impPopStack().val, layout, flags);
            }

            case NI_SRCS_UNSAFE_SizeOf:
            {
                assert(sig.sigInst.methInstCount is 1);

                // sizeof !!T
                // ret

                var classSize = info.compCompHnd->getClassSize(sig.sigInst.methInst[0]);
                return gtNewIconNode(TYP_INT, classSize);
            }

            case NI_SRCS_UNSAFE_SkipInit:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ret

                var op1 = impPopStack().val;

                if ((op1.Flags & GTF_SIDE_EFFECT) is not 0)
                {
                    return gtUnusedValNode(op1);
                }
                else
                {
                    return gtNewNothingNode();
                }
            }

            case NI_SRCS_UNSAFE_Subtract:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // sizeof !!T
                // conv.i
                // mul
                // sub
                // ret

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                impBashVarAddrsToI(op1);
                impBashVarAddrsToI(op2);

                op2 = impImplicitIorI4Cast(op2, TYP_I_IMPL);

                var classSize = info.compCompHnd->getClassSize(sig.sigInst.methInst[0]);

                if (classSize is not 1)
                {
                    var size = gtNewIconNode(TYP_I_IMPL, classSize);
                    op2 = gtNewBinaryNode(GT_MUL, TYP_I_IMPL, op2, size);
                }

                var type = impGetByRefResultType(GT_SUB, fUnsigned: false, ref op1, ref op2);
                return gtNewBinaryNode(GT_SUB, type, op1, op2);
            }

            case NI_SRCS_UNSAFE_SubtractByteOffset:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // sub
                // ret

                var op2 = impPopStack().val;
                var op1 = impPopStack().val;

                impBashVarAddrsToI(op1);
                impBashVarAddrsToI(op2);

                var type = impGetByRefResultType(GT_SUB, fUnsigned: false, ref op1, ref op2);
                return gtNewBinaryNode(GT_SUB, type, op1, op2);
            }

            case NI_SRCS_UNSAFE_Unbox:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // unbox !!T
                // ret

                return null;
            }

            case NI_SRCS_UNSAFE_Write:
            case NI_SRCS_UNSAFE_WriteUnaligned:
            {
                assert(sig.sigInst.methInstCount is 1);

                // ldarg.0
                // ldarg.1
                // if NI_SRCS_UNSAFE_WriteUnaligned: unaligned. 0x01
                // stobj !!T
                // ret

                var typeHnd = sig.sigInst.methInst[0];
                var type = TypeHandleToVarType(typeHnd, out var layout);
                var flags = intrinsic == NI_SRCS_UNSAFE_WriteUnaligned ? GTF_IND_UNALIGNED : GTF_EMPTY;

                var value = impPopStack().val;
                var addr = impPopStack().val;

                var store = gtNewStoreValueNode(type, addr, value, layout, flags);

                if (varTypeIsStruct(store.Type))
                {
                    store = impStoreStruct(store, CHECK_SPILL_ALL);
                }
                return store;
            }

            default:
            {
                unreached();
                return null;
            }
        }
    }

    public ref StackEntry impStackTop(int n = 0)
    {
        if (stackState.esStackDepth <= n)
        {
            BADCODE("stack underflow");
        }
        return ref stackState.esStack[stackState.esStackDepth - n - 1];
    }

    /// <summary>ensure calls that return structs in multiple registers return values to suitable temps.</summary>
    /// <param name="op">call returning a struct in registers</param>
    /// <param name="hClass">class handle for struct</param>
    /// <param name="callConv"></param>
    /// <returns>Tree with reference to struct local to use as call return value.</returns>
    public unsafe GenTree impStoreMultiRegValueToVar(GenTree op, CORINFO_CLASS_HANDLE hClass, CorInfoCallConvExtension callConv)
    {
        var tmpNum = lvaGrabTemp(shortLifetime: true, "Return value temp for multireg return");
        lvaSetStruct(tmpNum, hClass, unsafeValueClsCheck: false);

        impStoreToTemp(tmpNum, op, CHECK_SPILL_ALL);

        ref var varDsc = ref lvaGetDesc(tmpNum);
        varDsc.IsMultiRegDest = true;

        var ret = gtNewLclvNode(varDsc.Type, tmpNum);

        // TODO-1stClassStructs: Handle constant propagation and CSE-ing of multireg returns.
        ret.CanCse = false;

        assert(IsMultiRegReturnedType(hClass, callConv) || op.IsMultiRegNode);
        return ret;
    }

    public GenTree impStoreStruct(GenTree store, int curLevel = CHECK_SPILL_NONE, in DebugInfo di = default, BasicBlock? block = null)
        => impStoreStruct(store, ref Unsafe.NullRef<Statement>(), curLevel, di, block);

    /// <summary>Import a struct store.</summary>
    /// <param name="store">the store</param>
    /// <param name="afterStmt">statement to insert any additional statements after</param>
    /// <param name="curLevel">stack level for which a spill may be being done</param>
    /// <param name="di">debug info for new statements</param>
    /// <param name="block">block to insert any additional statements in</param>
    /// <returns>The tree that should be appended to the statement list that represents the store.</returns>
    /// <remarks>Temp stores may be appended to impStmtList if spilling is necessary.</remarks>
    public unsafe GenTree impStoreStruct(GenTree store, ref Statement afterStmt, int curLevel = CHECK_SPILL_NONE, in DebugInfo di = default, BasicBlock? block = null)
    {
        var storeOper = store.Oper;
        assert(varTypeIsStruct(store.Type) && storeOper.IsStore);

        ref var dataRef = ref store.DataRef;
        assert(store.Type == dataRef.Type);

        if (store.Type is TYP_STRUCT)
        {
            assert(ClassLayout.AreCompatible(store.GetLayout(this), dataRef.GetLayout(this)));
        }

        var usedDI = di;

        if (!usedDI.IsValid)
        {
            usedDI = impCurStmtDI;
        }

        var dataOper = dataRef.Oper;

        if (dataOper.IsCall)
        {
            var call = dataRef.AsCall();

            if (call.ShouldHaveRetBufArg)
            {
                // Case of call returning a struct via hidden retbuf arg.
                var destAddr = impGetNodeAddr(store, CHECK_SPILL_ALL, GTF_IND_MUST_PRESERVE_FLAGS, out var indirFlags);

                // Return buffers cannot have volatile, unaligned, or initclass flags
                if (((indirFlags & GTF_IND_MUST_PRESERVE_FLAGS) != 0) || !impIsLegalRetBuf(destAddr, call))
                {
                    var lclNum = lvaGrabTemp(shortLifetime: false, "stack copy for value returned via return buffer");
                    lvaSetStruct(lclNum, call.RetClsHnd, unsafeValueClsCheck: false);

                    var spilledCall = impStoreStruct(gtNewStoreLclVarNode(lclNum, call), ref afterStmt, curLevel, di, block);
                    dataRef = gtNewCommaNode(store.Type, spilledCall, gtNewLclvNode(lvaGetDesc(lclNum).Type, lclNum));

                    return impStoreStruct(store, ref afterStmt, curLevel, di, block);
                }

                var newArg = NewCallArg.CreateForPrimitive(destAddr).WithWellKnownArg(WellKnownArg.RetBuffer);

                if (destAddr.Oper is GT_LCL_ADDR)
                {
                    lvaSetVarDoNotEnregister(destAddr.AsLclVarCommon().LclNum, DoNotEnregisterReason.HiddenBufferStructArg);
                }

#if !TARGET_ARM
                var args = call.Args;

                // Unmanaged instance methods on Windows or Unix X86 need the retbuf arg after the first (this) parameter
                if ((TargetOS.IsWindows || compUnixX86Abi()) && call.IsUnmanaged)
                {
                    if (callConvIsInstanceMethodCallConv(call.UnmanagedCallConv))
                    {
                        var head = args.Head;

                        // The argument list has already been reversed. Insert the
                        // return buffer as the second-to-last node  so it will be
                        // pushed on to the stack after the user args but before
                        // the native this arg as required by the native ABI.
                        if (head is null)
                        {
                            // Empty arg list
                            _ = args.PushFront(newArg);
                        }
#if TARGET_X86
                        else if (call.UnmanagedCallConv == CorInfoCallConvExtension.Thiscall)
                        {
                            // For thiscall, the "this" parameter is not included in the argument list reversal,
                            // so we need to put the return buffer as the last parameter.
                            _ = args.PushBack(newArg);
                        }
                        else if (head.Next is null)
                        {
                            // Only 1 arg, so insert at beginning
                            _ = args.PushFront(newArg);
                        }
                        else
                        {
                            // Find second last arg
                            var  secondLastArg = null as CallArg;

                            foreach (var arg in args.Args)
                            {
                                assert(arg.Next is not null);

                                if (arg.Next.Next is null)
                                {
                                    secondLastArg = arg;
                                    break;
                                }
                            }

                            assert(secondLastArg is not null, "Expected to find second last arg");
                            _ = args.InsertAfter(secondLastArg, newArg);
                        }
#else
                        else
                        {
                            _ = args.InsertAfter(head, newArg);
                        }
#endif
                    }
                    else
                    {
#if TARGET_X86
                        // The argument list has already been reversed.
                        // Insert the return buffer as the last node so it will be pushed on to the stack last
                        // as required by the native ABI.
                        _ = args.PushBack(newArg);
#else
                        // insert the return value buffer into the argument list as first byref parameter
                        _ = args.PushFront(newArg);
#endif
                    }
                }
                else
#endif
                {
                    // insert the return value buffer into the argument list as first byref parameter after 'this'
                    _ = args.InsertAfterThisOrFirst(newArg);
                }

                // now returns void, not a struct
                dataRef.Type = TYP_VOID;

                // return the morphed call node
                return dataRef;
            }

#if UNIX_AMD64_ABI
            if (store.Oper is GT_STORE_LCL_VAR)
            {
                // TODO-Cleanup: delete this quirk.
                lvaGetDesc(store.AsLclVar().LclNum).lvIsMultiRegRet = true;
            }
#endif
        }
        else if (dataOper is GT_RET_EXPR)
        {
            var retExpr = dataRef.AsRetExpr();
            var call = retExpr.InlineCandidate.AsCall();

            if (call.ShouldHaveRetBufArg)
            {
                var args = call.Args;

                // insert the return value buffer into the argument list as first byref parameter after 'this'
                var destAddr = impGetNodeAddr(store, CHECK_SPILL_ALL, GTF_IND_MUST_PRESERVE_FLAGS, out var indirFlags);

                // Return buffers cannot have volatile, unaligned, or initclass flags
                if (((indirFlags & GTF_IND_MUST_PRESERVE_FLAGS) != 0) || !impIsLegalRetBuf(destAddr, call))
                {
                    var lclNum = lvaGrabTemp(shortLifetime: false, "stack copy for value returned via return buffer");
                    lvaSetStruct(lclNum, call.RetClsHnd, unsafeValueClsCheck: false);
                    destAddr = gtNewLclVarAddrNode(TYP_I_IMPL, lclNum);

                    // Insert address of temp into existing call
                    var retBufArg = NewCallArg.CreateForPrimitive(destAddr).WithWellKnownArg(WellKnownArg.RetBuffer);
                    _ = args.InsertAfterThisOrFirst(retBufArg);

                    // Now the store needs to copy from the new temp instead.
                    call.Type = TYP_VOID;
                    retExpr.Type = TYP_VOID;
                    var tmpType = lvaGetDesc(lclNum).Type;
                    dataRef = gtNewCommaNode(tmpType, retExpr, gtNewLclvNode(tmpType, lclNum));
                    return impStoreStruct(store, ref afterStmt, CHECK_SPILL_ALL, di, block);
                }

                _ = args.InsertAfterThisOrFirst(NewCallArg.CreateForPrimitive(destAddr).WithWellKnownArg(WellKnownArg.RetBuffer));

                // now returns void, not a struct
                dataRef.Type = TYP_VOID;
                call.Type = TYP_VOID;

                // We already have appended the write to 'dest' GT_CALL's args
                // So now we just return an empty node (pruning the GT_RET_EXPR)
                return dataRef;
            }
        }
        else if (dataOper is GT_COMMA)
        {
            var comma = dataRef.AsOp();
            var sideEffectAddressStore = null as GenTree;

            if (store.Oper is GT_STORE_BLK or GT_STOREIND)
            {
                var storeIndir = store.AsIndir();
                var addr = storeIndir.Addr;

                if ((addr.Flags & GTF_ALL_EFFECT) is not 0)
                {
                    var addrTmp = fgMakeTemp(addr);
                    sideEffectAddressStore = addrTmp.Store;
                    storeIndir.Addr = addrTmp.Load;
                }
            }

            if (!Unsafe.IsNullRef(in afterStmt))
            {
                assert(block is not null);

                // Insert op1 after '*afterStmt'
                if (sideEffectAddressStore is not null)
                {
                    var addrStmt = gtNewStmt(sideEffectAddressStore, usedDI);
                    fgInsertStmtAfter(block, afterStmt, addrStmt);
                    afterStmt = addrStmt;
                }

                var newStmt = gtNewStmt(comma.Op1, usedDI);
                fgInsertStmtAfter(block, afterStmt, newStmt);
                afterStmt = newStmt;
            }
            else if (impLastStmt is not null)
            {
                // Do the side-effect as a separate statement.
                if (sideEffectAddressStore is not null)
                {
                    impAppendTree(sideEffectAddressStore, curLevel, usedDI);
                }
                impAppendTree(comma.Op1, curLevel, usedDI);
            }
            else
            {
                // In this case we have neither been given a statement to insert after, nor are we
                // in the importer where we can append the side effect.
                // Instead, we're going to sink the store below the COMMA.
                dataRef = comma.Op2;
                comma.Op2 = impStoreStruct(store, ref afterStmt, curLevel, usedDI, block);
                gtUpdateNodeSideEffects(store);
                comma.SetAllEffectsFlags(comma.Op1, comma.Op2);

                if (sideEffectAddressStore is not null)
                {
                    comma = gtNewCommaNode(comma.Type, sideEffectAddressStore, comma);
                }
                return comma;
            }

            // Evaluate the second thing using recursion.
            dataRef = comma.Op2;
            gtUpdateNodeSideEffects(store);
            return impStoreStruct(store, ref afterStmt, curLevel, usedDI, block);
        }

        if ((storeOper is GT_STORE_LCL_VAR) && dataRef.IsMultiRegNode)
        {
            lvaGetDesc(store.AsLclVar().LclNum).IsMultiRegDest = true;
        }
        return store;
    }

    /// <summary>Store (copy) the structure from 'src' to 'destAddr'.</summary>
    /// <param name="destAddr">address of the destination of the store</param>
    /// <param name="value">value to store</param>
    /// <param name="curLevel">stack level for which a spill may be being done</param>
    /// <param name="indirFlags">flags to be used on the store node</param>
    /// <returns>The tree that should be appended to the statement list that represents the store.</returns>
    /// <remarks>Temp stores may be appended to impStmtList if spilling is necessary.</remarks>
    public GenTree impStoreStructPtr(GenTree destAddr, GenTree value, int curLevel, GenTreeFlags indirFlags = GTF_EMPTY)
    {
        var type = value.Type;
        var layout = (type == TYP_STRUCT) ? value.GetLayout(this) : null;
        var store = gtNewStoreValueNode(type, destAddr, value, layout, indirFlags);
        return impStoreStruct(store, curLevel);
    }

    public void impStoreToTemp(int lclNum, GenTree val, int curLevel, in DebugInfo di = default, BasicBlock? block = null)
        => impStoreToTemp(lclNum, val, ref Unsafe.NullRef<Statement>(), curLevel, di, block);

    /// <summary>Append a store of the given value to a temp to the current tree list.</summary>
    /// <param name="lclNum"></param>
    /// <param name="val"></param>
    /// <param name="afterStmt"></param>
    /// <param name="curLevel">The stack level for which the spill to the temp is being done.</param>
    /// <param name="di"></param>
    /// <param name="block"></param>
    public void impStoreToTemp(int lclNum, GenTree val, ref Statement afterStmt, int curLevel, in DebugInfo di = default, BasicBlock? block = null)
    {
        var store = gtNewTempStore(lclNum, val, ref afterStmt, curLevel, di, block);

        if (!store.IsNothingNode)
        {
            if (!Unsafe.IsNullRef(in afterStmt))
            {
                assert(block is not null);
                var storeStmt = gtNewStmt(store, di);

                fgInsertStmtAfter(block, afterStmt, storeStmt);
                afterStmt = storeStmt;
            }
            else
            {
                _ = impAppendTree(store, curLevel, impCurStmtDI);
            }
        }
    }

    /// <summary>Checks whether the return types of caller and callee are compatible so that calle can be tail called. sizes are not supported integral type sizes return values to temps.</summary>
    /// <param name="allowWidening">whether to allow implicit widening by the callee. For instance, allowing int32 -> int16 tailcalls. The managed calling convention allows this, but we don't want explicit tailcalls to depend on this detail of the managed calling convention.</param>
    /// <param name="callerRetType">the caller's return type</param>
    /// <param name="callerRetTypeClass">the caller's return struct type</param>
    /// <param name="callerCallConv">calling convention of the caller</param>
    /// <param name="calleeRetType">the callee's return type</param>
    /// <param name="calleeRetTypeClass">the callee return struct type</param>
    /// <param name="calleeCallConv">calling convention of the callee</param>
    /// <returns>True if the tailcall types are compatible.</returns>
    /// <remarks>Note that here we don't check compatibility in IL Verifier sense, but on the lines of return types getting returned in the same return register.</remarks>
    public unsafe bool impTailCallRetTypeCompatible(bool allowWidening, var_types callerRetType, CORINFO_CLASS_HANDLE callerRetTypeClass, CorInfoCallConvExtension callerCallConv, var_types calleeRetType, CORINFO_CLASS_HANDLE calleeRetTypeClass, CorInfoCallConvExtension calleeCallConv)
    {
        // Early out if the types are the same.
        if (callerRetType == calleeRetType)
        {
            return true;
        }

        // For integral types the managed calling convention dictates that callee
        // will widen the return value to 4 bytes, so we can allow implicit widening
        // in managed to managed tailcalls when dealing with <= 4 bytes.
        var isManaged = callerCallConv is CorInfoCallConvExtension.Managed or CorInfoCallConvExtension.Managed;

        if (allowWidening && isManaged && varTypeIsIntegral(callerRetType) && varTypeIsIntegral(calleeRetType) && (callerRetType.Size <= 4) && (calleeRetType.Size <= callerRetType.Size))
        {
            return true;
        }

        // If the class handles are the same and not null, the return types are compatible.
        if ((callerRetTypeClass is not null) && (callerRetTypeClass == calleeRetTypeClass))
        {
            return true;
        }

#if TARGET_64BIT
        // Jit64 compat:
        if (callerRetType is TYP_VOID)
        {
            // This needs to be allowed to support the following IL pattern that Jit64 allows:
            //     tail.call
            //     pop
            //     ret
            //
            // Note that the above IL pattern is not valid as per IL verification rules.
            // Therefore, only full trust code can take advantage of this pattern.
            return true;
        }

        // These checks return true if the return value type sizes are the same and
        // get returned in the same return register i.e. caller doesn't need to normalize
        // return value. Some of the tail calls permitted by below checks would have
        // been rejected by IL Verifier before we reached here.  Therefore, only full
        // trust code can make those tail calls.
        var isCallerRetTypMBEnreg = VarTypeIsMultiByteAndCanEnreg(callerRetType, callerRetTypeClass, out var callerRetTypeSize, info.compIsVarArgs, callerCallConv);
        var isCalleeRetTypMBEnreg = VarTypeIsMultiByteAndCanEnreg(calleeRetType, calleeRetTypeClass, out var calleeRetTypeSize, info.compIsVarArgs, calleeCallConv);

        if (varTypeIsIntegral(callerRetType) || isCallerRetTypMBEnreg)
        {
            return (varTypeIsIntegral(calleeRetType) || isCalleeRetTypMBEnreg) && (callerRetTypeSize == calleeRetTypeSize);
        }
#endif

        return false;
    }

    /// <summary>Remove redundant boxing from ArgumentNullException_ThrowIfNull it is done for Tier0 where we can't remove it without inlining otherwise.</summary>
    /// <param name="call">call representing ArgumentNullException_ThrowIfNull</param>
    /// <returns>Optimized tree (or the original call tree if we can't optimize it).</returns>
    public GenTree impThrowIfNull(GenTreeCall call)
    {
        // We have two overloads:
        //
        //  void ThrowIfNull(object argument, string paramName = null)
        //  void ThrowIfNull(object argument, ExceptionArgument paramName)
        //
        assert(call.IsSpecialIntrinsic(this, NI_System_ArgumentNullException_ThrowIfNull));
        assert(call.Args.CountUserArgs() is 2);
        assert(call.Type is TYP_VOID);

        if (!opts.Tier0OptimizationEnabled)
        {
            // Don't fold it for debug code or forced MinOpts
            return call;
        }

        ref var callArgs = ref call.Args;

        var callArg0 = callArgs.GetUserArgByIndex(0);
        assert(callArg0 is not null);
        var value = callArg0.Node;

        var callArg1 = callArgs.GetUserArgByIndex(1);
        assert(callArg1 is not null);
        var valueName = callArg1.Node;

        // Case 1: value-type (non-nullable):
        //
        //  ArgumentNullException_ThrowIfNull(GT_BOX(value), valueName)
        //    ->
        //  NOP (with side-effects if any)
        //
        if (value.Oper is GT_BOX)
        {
            // Now we need to spill the addr and argName arguments in the correct order  to preserve possible side effects.

            var boxedValTmp = lvaGrabTemp(shortLifetime: true, "boxedVal spilled");
            var boxedArgNameTmp = lvaGrabTemp(shortLifetime: true, "boxedArg spilled");

            impStoreToTemp(boxedValTmp, value, CHECK_SPILL_ALL);
            impStoreToTemp(boxedArgNameTmp, valueName, CHECK_SPILL_ALL);

            _ = gtTryRemoveBoxUpstreamEffects(value.AsBox(), BR_REMOVE_AND_NARROW);
            return gtNewNothingNode();
        }
        else
        {
            // Case 2: nullable:
            //
            //  ArgumentNullException.ThrowIfNull(CORINFO_HELP_BOX_NULLABLE(classHandle, addr), valueName);
            //    ->
            //  addr->hasValue is not 0 ? NOP : ArgumentNullException.ThrowIfNull(null, valueNameTmp)

            if (opts.OptimizationEnabled || !value.Oper.IsCall)
            {
                // We're not boxing - bail out.
                // NOTE: when opts are enabled, we remove the box as is (with better CQ)
                return call;
            }

            var valueCall = value.AsCall();

            if (!valueCall.IsHelperCall(CORINFO_HELP_BOX_NULLABLE))
            {
                return call;
            }

            ref var valueCallArgs = ref valueCall.Args;

            var valueCallArg0 = valueCallArgs.GetUserArgByIndex(0);
            assert(valueCallArg0 is not null);
            var boxHelperClsArg = valueCallArg0.Node;

            var valueCallArg1 = valueCallArgs.GetUserArgByIndex(1);
            assert(valueCallArg1 is not null);
            var boxHelperAddrArg = valueCallArg1.Node;

            if ((boxHelperClsArg.Flags & GTF_SIDE_EFFECT) is not 0)
            {
                // boxHelperClsArg is always just a class handle constant, so we don't bother spilling it.
                return call;
            }

            // Now we need to spill the addr and argName arguments in the correct order to preserve possible side effects.

            var boxedValTmp = lvaGrabTemp(shortLifetime: true, "boxedVal spilled");
            var boxedArgNameTmp = lvaGrabTemp(shortLifetime: true, "boxedArg spilled");

            impStoreToTemp(boxedValTmp, boxHelperAddrArg, CHECK_SPILL_ALL);
            impStoreToTemp(boxedArgNameTmp, valueName, CHECK_SPILL_ALL);

            // Change arguments to 'ThrowIfNull(null, valueNameTmp)'
            callArg0.EarlyNode = gtNewNull();
            callArg1.EarlyNode = gtNewLclvNode(valueName.Type, boxedArgNameTmp);

            // This is Tier0 specific, so we create a raw indir node to access Nullable<T>.hasValue field which is the first field of Nullable<T> struct and is of type 'bool'.
            var hasValueField = gtNewIndir(TYP_UBYTE, gtNewLclvNode(boxHelperAddrArg.Type, boxedValTmp));

            var cond = gtNewBinaryNode(GT_NE, TYP_INT, hasValueField, gtNewIconNode(TYP_INT, 0));
            var colon = gtNewColonNode(TYP_VOID, gtNewNothingNode(), call);

            return gtNewQmarkNode(TYP_VOID, cond, colon);
        }
    }

    public GenTree? impTokenToHandle(in CORINFO_RESOLVED_TOKEN resolvedToken, bool mustRestoreHandle = false, bool importParent = false)
        => impTokenToHandle(resolvedToken, out _, mustRestoreHandle, importParent);

    /// <summary>Given a type token, generate code that will evaluate to the correct handle representation of that token (type handle, field handle, or method handle)</summary>
    /// <param name="resolvedToken"></param>
    /// <param name="runtimeLookup"></param>
    /// <param name="mustRestoreHandle"></param>
    /// <param name="importParent"></param>
    /// <returns></returns>
    /// <remarks>
    ///   <para>For most cases, the handle is determined at compile-time, and the code generated is simply an embedded handle.</para>
    ///   <para>Run-time lookup is required if the enclosing method is shared between instantiations and the token refers to formal type parameters whose instantiation is not known at compile-time.</para>
    /// </remarks>
    public unsafe GenTree? impTokenToHandle(in CORINFO_RESOLVED_TOKEN resolvedToken, out bool runtimeLookup, bool mustRestoreHandle = false, bool importParent = false)
    {
        assert(!fgGlobalMorph);

        CORINFO_GENERICHANDLE_RESULT embedInfo;

        fixed (CORINFO_RESOLVED_TOKEN* pResolvedToken = &resolvedToken)
        {
            info.compCompHnd->embedGenericHandle(pResolvedToken, importParent, info.compMethodHnd, &embedInfo);
        }
        runtimeLookup = embedInfo.lookup.lookupKind.needsRuntimeLookup;

        if (mustRestoreHandle && !embedInfo.lookup.lookupKind.needsRuntimeLookup)
        {
            switch (embedInfo.handleType)
            {
                case CORINFO_HANDLETYPE_CLASS:
                {
                    info.compCompHnd->classMustBeLoadedBeforeCodeIsRun((CORINFO_CLASS_HANDLE)(embedInfo.compileTimeHandle));
                    break;
                }

                case CORINFO_HANDLETYPE_METHOD:
                {
                    info.compCompHnd->methodMustBeLoadedBeforeCodeIsRun((CORINFO_METHOD_HANDLE)(embedInfo.compileTimeHandle));
                    break;
                }

                case CORINFO_HANDLETYPE_FIELD:
                {
                    info.compCompHnd->classMustBeLoadedBeforeCodeIsRun(info.compCompHnd->getFieldClass((CORINFO_FIELD_HANDLE)(embedInfo.compileTimeHandle)));
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        // Generate the full lookup tree. May be null if we're abandoning an inline attempt.
        var handleType = importParent ? GTF_ICON_CLASS_HDL : gtTokenToIconFlags(resolvedToken.token);
        var result = impLookupToTree(embedInfo.lookup, handleType, embedInfo.compileTimeHandle);

        // If we have a result and it requires runtime lookup, wrap it in a runtime lookup node.
        if ((result is not null) && embedInfo.lookup.lookupKind.needsRuntimeLookup)
        {
            result = gtNewRuntimeLookup(result, embedInfo.compileTimeHandle, embedInfo.handleType);
        }
        return result;
    }

    /// <summary>transform a resolved virtual call target into a direct call.</summary>
    /// <param name="call">call to transform</param>
    /// <param name="method">method handle for call. Updated to the devirtualized target method</param>
    /// <param name="methodFlags">flags for the method to call. Updated to the devirtualized target method's flags</param>
    /// <param name="dcInfo">resolved target information for the call</param>
    /// <param name="block">block that will contain the transformed call</param>
    /// <param name="contextHandle">context handle for the transformed call</param>
    /// <param name="baseMethod"></param>
    public unsafe void impTransformDevirtualizedCall(GenTreeCall call, ref CORINFO_METHOD_HANDLE method, ref CorInfoFlag methodFlags, in DevirtualizedCallInfo dcInfo, BasicBlock block, out CORINFO_CONTEXT_HANDLE contextHandle, CORINFO_METHOD_HANDLE baseMethod)
    {
        var derivedMethod = method;
        var derivedMethodAttribs = methodFlags;
        ref var derivedResolvedToken = ref dcInfo.resolvedToken;
        var derivedClass = eeGetClassFromContext(dcInfo.tokenLookupContext);

        assert(derivedMethod is not null);
        assert(call.Args.HasThisPointer);

        var thisArg = call.Args.ThisArg;
        var thisObj = thisArg.EarlyNode.EffectiveVal;

#if DEBUG
        // Optionally, print info on devirtualization
        var rootCompiler = impInlineRoot;
        var doPrint = JitConfig[ConfigMethodSet.JitPrintDevirtualizedMethods].contains(rootCompiler.info.compMethodHnd, rootCompiler.info.compClassHnd, &rootCompiler.info.compMethodInfo->args);

        if (doPrint)
        {
            var baseClass = info.compCompHnd->getMethodClass(baseMethod);
            var baseClassAttribs = info.compCompHnd->getClassAttribs(baseClass);
            var derivedMethodIsFinal = ((derivedMethodAttribs & CORINFO_FLG_FINAL) is not 0);
            var callKind = ((baseClassAttribs & CORINFO_FLG_INTERFACE) is not 0) ? "interface" : "virtual";
            var note = dcInfo.objClassIsExact ? "exact" : dcInfo.objClassIsFinal ? "final class" : derivedMethodIsFinal ? "final method" : "inexact or not final";

            jitprintf($"Devirtualized {callKind} call to {eeGetMethodFullName(baseMethod)}; now direct call to {eeGetMethodFullName(derivedMethod)} [{note}]\n");
        }
#endif

        Metrics.DevirtualizedCall++;

        // Make the updates.
        call.Flags &= ~GTF_CALL_VIRT_VTABLE;
        call.Flags &= ~GTF_CALL_VIRT_STUB;
        call._callMethHnd = derivedMethod;
        call._callType = CT_USER_FUNC;
        call._controlExpr = null;

#if DEBUG
        call._callDebugFlags |= GTF_CALL_MD_DEVIRTUALIZED;
#endif

        if (dcInfo.isDelegateCall)
        {
            call._callMoreFlags &= ~GTF_CALL_M_DELEGATE_INV;
        }

        // Virtual calls include an implicit null check, which we may
        // now need to make explicit.
        if (dcInfo.hadImplicitNullCheck && !dcInfo.objIsNonNull)
        {
            call.Flags |= GTF_CALL_NULLCHECK;
        }

        // Clear the inline candidate info (may be non-null since
        // it's a union field used for other things by virtual
        // stubs)
        call.ClearInlineInfo();

#if DEBUG
        // If we successfully devirtualized based on an exact or final class,
        // and we have dynamic PGO data describing the likely class, make sure they agree.
        //
        // If pgo source is not dynamic we may see likely classes from other versions of this code
        // where types had different properties.
        //
        // If method is an inlinee we may be specializing to a class that wasn't seen at runtime.
        //
        var canSensiblyCheck = (dcInfo.objClassIsExact || dcInfo.objClassIsFinal) && (fgPgoSource is ICorJitInfo.PgoSource.Dynamic) && !compIsForInlining;

        if ((JitConfig[ConfigInteger.JitCrossCheckDevirtualizationAndPGO] is not 0) && canSensiblyCheck)
        {
            // We only can handle a single likely class for now

            Unsafe.SkipInit(out InlineArray1<LikelyClassMethodRecord> inlineLikelyClasses);
            var likelyClasses = (Span<LikelyClassMethodRecord>)(inlineLikelyClasses);

            var schema = new ReadOnlySpan<ICorJitInfo.PgoInstrumentationSchema>(fgPgoSchema, fgPgoSchemaCount);
            var numberOfClasses = getLikelyClasses(likelyClasses, schema, fgPgoData, dcInfo.ilOffset);

            var likelihood = likelyClasses[0].likelihood;
            var likelyClass = (CORINFO_CLASS_HANDLE)(likelyClasses[0].handle);

            if (numberOfClasses > 0)
            {
                // PGO had better agree the class we devirtualized to is plausible.

                if (likelyClass != derivedClass)
                {
                    // Managed type system may report different addresses for a class handle
                    // at different times....?
                    //
                    // Also, AOT may have a more nuanced notion of class equality.

                    if (!IsAot)
                    {
                        var mismatch = true;

                        // derivedClass will be the introducer of derived method, so it's possible
                        // likelyClass is a non-overriding subclass. Check up the hierarchy.

                        var parentClass = likelyClass;

                        while (parentClass != NO_CLASS_HANDLE)
                        {
                            if (parentClass == derivedClass)
                            {
                                mismatch = false;
                                break;
                            }

                            parentClass = info.compCompHnd->getParentType(parentClass);
                        }

                        if (mismatch || (numberOfClasses is not 1) || (likelihood is not 100))
                        {
                            jitprintf($"@@@ Likely {dspPtr(likelyClass)} ({eeGetClassName(likelyClass)}) != Derived {dspPtr(derivedClass)} ({eeGetClassName(derivedClass)}) [n={numberOfClasses}, l={likelihood}, il={dcInfo.ilOffset}] in {info.compFullName} \n");
                        }

                        assert(!mismatch && (numberOfClasses is 1) && (likelihood is 100));
                    }
                }
            }
        }
#endif

        var instParam = null as GenTree;

        // If the 'this' object is a value class, see if we can rework the call to invoke the
        // unboxed entry. This effectively inlines the normally un-inlineable wrapper stub
        // and exposes the potentially inlinable unboxed entry method.
        //
        // We won't optimize explicit tail calls, as ensuring we get the right tail call info
        // is tricky (we'd need to pass an updated sig and resolved token back to some callers).
        //
        // Note we may not have a derived class in some cases (eg interface call on an array)

        if (info.compCompHnd->isValueClass(derivedClass))
        {
            if (dcInfo.isExplicitTailCall)
            {
                JITDUMP("Have a direct explicit tail call to boxed entry point; can't optimize further\n");
            }
            else
            {
                JITDUMP("Have a direct call to boxed entry point. Trying to optimize to call an unboxed entry point\n");

                // Note for some shared methods the unboxed entry point requires an extra parameter.
                var requiresInstMethodTableArg = false;
                var unboxedEntryMethod = info.compCompHnd->getUnboxedEntry(derivedMethod, &requiresInstMethodTableArg);

                if (unboxedEntryMethod is not null)
                {
                    // Rewrite the call to target the unboxed entry on the box payload. Keep the heap box,
                    // since the callee may return an interior managed pointer into it; object stack allocation
                    // can later promote the box to the stack when escape analysis proves the receiver does not
                    // escape.

                    if (requiresInstMethodTableArg)
                    {
                        // Get the method table from the boxed object.
                        //
                        // TODO-CallArgs-REVIEW: Use thisObj here? Differs by gtEffectiveVal.
                        var clonedThisArg = gtClone(thisArg.EarlyNode);

                        if (clonedThisArg is null)
                        {
                            JITDUMP("unboxed entry needs MT arg, but `this` was too complex to clone. Deferring update.\n");
                        }
                        else
                        {
                            JITDUMP("revising call to invoke unboxed entry with additional method table arg\n");

                            instParam = gtNewMethodTableLookup(clonedThisArg);

                            // Update the 'this' pointer to refer to the box payload

                            var payloadOffset = gtNewIconNode(TYP_I_IMPL, TARGET_POINTER_SIZE);
                            var boxPayload = gtNewBinaryNode(GT_ADD, TYP_BYREF, thisArg.EarlyNode, payloadOffset);

                            assert(thisObj == thisArg.EarlyNode);

                            thisArg.EarlyNode = boxPayload;
                            call._callMethHnd = unboxedEntryMethod;

#if DEBUG
                            call._callDebugFlags |= GTF_CALL_MD_UNBOXED;
#endif

                            // Method attributes will differ because unboxed entry point is shared

                            var unboxedMethodAttribs = info.compCompHnd->getMethodAttribs(unboxedEntryMethod);
                            JITDUMP($"Updating method attribs from 0x{derivedMethodAttribs:X8} to 0x{unboxedMethodAttribs:X8}\n");

                            derivedMethod = unboxedEntryMethod;
                            derivedResolvedToken = dcInfo.unboxedResolvedToken;
                            derivedMethodAttribs = unboxedMethodAttribs;

                            Metrics.DevirtualizedCallUnboxedEntry++;
                        }
                    }
                    else
                    {
                        JITDUMP("revising call to invoke unboxed entry\n");

                        var payloadOffset = gtNewIconNode(TYP_I_IMPL, TARGET_POINTER_SIZE);
                        var boxPayload = gtNewBinaryNode(GT_ADD, TYP_BYREF, thisArg.EarlyNode, payloadOffset);

                        thisArg.EarlyNode = boxPayload;
                        call._callMethHnd = unboxedEntryMethod;

#if DEBUG
                        call._callDebugFlags |= GTF_CALL_MD_UNBOXED;
#endif

                        derivedMethod = unboxedEntryMethod;
                        derivedResolvedToken = dcInfo.unboxedResolvedToken;

                        Metrics.DevirtualizedCallUnboxedEntry++;
                    }
                }
                else
                {
                    // Many of the low-level methods on value classes won't have unboxed entries,
                    // as they need access to the type of the object.
                    //
                    // Note this may be a cue for us to stack allocate the boxed object, since
                    // we probably know that these objects don't escape.

                    JITDUMP("Sorry, failed to find unboxed entry point\n");
                }
            }
        }
        else if (dcInfo.methSig.hasTypeArg())
        {
            if ((unchecked((nint)(dcInfo.tokenLookupContext)) & (nint)(CORINFO_CONTEXTFLAGS_MASK)) == (nint)(CORINFO_CONTEXTFLAGS_METHOD))
            {
                var exactMethodHandle = (CORINFO_METHOD_HANDLE)((unchecked((nint)(dcInfo.tokenLookupContext)) & ~(nint)(CORINFO_CONTEXTFLAGS_MASK)));
                instParam = getLookupTree(dcInfo.instParamLookup, GTF_ICON_METHOD_HDL, exactMethodHandle);
            }
            else
            {
                assert((unchecked((nint)(dcInfo.tokenLookupContext)) & (nint)(CORINFO_CONTEXTFLAGS_MASK)) == (nint)(CORINFO_CONTEXTFLAGS_CLASS));
                var exactClassHandle = (CORINFO_CLASS_HANDLE)(unchecked((nint)(dcInfo.tokenLookupContext)) & ~(nint)(CORINFO_CONTEXTFLAGS_MASK));
                instParam = getLookupTree(dcInfo.instParamLookup, GTF_ICON_CLASS_HDL, exactClassHandle);
            }
        }

        if (instParam is not null)
        {
            assert(call.Args.FindWellKnownArg(WellKnownArg.InstParam) is null);
            call.Args.InsertInstParam(this, instParam);
        }

        // Need to update call info too.

        method = derivedMethod;
        methodFlags = derivedMethodAttribs;

        // Update context handle
        contextHandle = MAKE_METHODCONTEXT(derivedMethod);

        // We might have created a new recursive tail call candidate.
        //
        if (call.CanTailCall && gtIsRecursiveCall(derivedMethod))
        {
            assert(block is not null);
            MethodHasRecursiveTailCall = true;

            block.SetFlags(BBF_RECURSIVE_TAILCALL);
            JITDUMP($"[{call.TreeId:D6}] is a recursive call in tail position\n");
        }
        else
        {
            JITDUMP($"[{call.TreeId:D6}] is{(call.CanTailCall ? "" : " not")} in tail position and is{(gtIsRecursiveCall(derivedMethod) ? "" : " not")} recursive\n");
        }

#if FEATURE_READYTORUN
        if (IsAot)
        {
            // For R2R, getCallInfo triggers bookkeeping on the zap
            // side and acquires the actual symbol to call so we need to call it here.
            assert(!Unsafe.IsNullRef(in derivedResolvedToken));

            // Look up the new call info.
            eeGetCallInfo(derivedResolvedToken, Unsafe.NullRef<CORINFO_RESOLVED_TOKEN>(), CORINFO_CALLINFO_ALLOWINSTPARAM, out var derivedCallInfo);

            // Update the call.
            call._callMoreFlags &= ~GTF_CALL_M_VIRTSTUB_REL_INDIRECT;
            call._entryPoint = derivedCallInfo.codePointerLookup.constLookup;
        }
#endif

#if DEBUG
        if (verbose)
        {
            jitprintf("... after devirt...\n");
            gtDispTree(call);
        }
#endif
    }

    public unsafe GenTree? impTransformThis(GenTree thisPtr, in CORINFO_RESOLVED_TOKEN constrainedResolvedToken, CORINFO_THIS_TRANSFORM transform)
    {
        switch (transform)
        {
            case CORINFO_DEREF_THIS:
            {
                var obj = thisPtr;

                // This does a LDIND on the obj, which should be a byref. pointing to a ref
                impBashVarAddrsToI(obj);
                assert((obj.Type.ActualType is TYP_I_IMPL) || (obj.Type is TYP_BYREF));

                var constraintTyp = info.compCompHnd->asCorInfoType(constrainedResolvedToken.hClass);
                return gtNewLoadValueNode(constraintTyp.VarType, obj, layout: null);
            }

            case CORINFO_BOX_THIS:
            {
                // Constraint calls where there might be no
                // unboxed entry point require us to implement the call via helper.
                // These only occur when a possible target of the call
                // may have inherited an implementation of an interface
                // method from System.Object or System.ValueType.  The EE does not provide us with
                // "unboxed" versions of these methods.

                var obj = thisPtr;

                assert(obj.Type is TYP_BYREF or TYP_I_IMPL);
                var objType = TypeHandleToVarType(constrainedResolvedToken.hClass, out var layout);

                if (objType is TYP_STRUCT)
                {
                    assert(layout is not null);
                    obj = gtNewBlkIndir(obj, layout);
                }
                else
                {
                    obj = gtNewIndir(objType, obj);
                }

                // This pushes on the dereferenced byref
                // This is then used immediately to box.
                impPushOnStack(obj, makeTypeInfo(constrainedResolvedToken.hClass));

                // This pops off the byref-to-a-value-type remaining on the stack and
                // replaces it with a boxed object.
                // This is then used as the object to the virtual call immediately below.
                impImportAndPushBox(constrainedResolvedToken);

                if (compDonotInline)
                {
                    return null;
                }

                obj = impPopStack().val;
                return obj;
            }

            case CORINFO_NO_THIS_TRANSFORM:
            {
                goto default;
            }

            default:
            {
                return thisPtr;
            }
        }
    }

    public unsafe GenTree? impTypeIsAssignable(GenTree typeTo, GenTree typeFrom)
    {
        // Optimize patterns like:
        //
        //   typeof(TTo).IsAssignableFrom(typeof(TTFrom))
        //   valueTypeVar.GetType().IsAssignableFrom(typeof(TTFrom))
        //   typeof(TTFrom).IsAssignableTo(typeof(TTo))
        //   typeof(TTFrom).IsAssignableTo(valueTypeVar.GetType())
        //
        // to true/false

        // make sure both arguments are `typeof()`
        if (gtIsTypeof(typeTo, out var hClassTo) && gtIsTypeof(typeFrom, out var hClassFrom))
        {
            var castResult = info.compCompHnd->compareTypesForCast(hClassFrom, hClassTo);

            if (castResult is TypeCompareState.May)
            {
                // requires runtime check
                // e.g. __Canon, COMObjects, Nullable
                return null;
            }

            var retNode = gtNewIconNode(TYP_INT, (castResult is TypeCompareState.Must) ? 1 : 0);

            // drop both CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE calls
            impPopStack(2);

            return retNode;
        }
        return null;
    }

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

    public unsafe void impVerifyEHBlock(BasicBlock block)
    {
        assert(block.hasTryIndex);
        assert(!compIsForInlining || opts.compInlineMethodsWithEH);

        var tryIndex = block.TryIndex;
        ref var HBtab = ref ehGetDsc(tryIndex);

        if (bbIsTryBeg(block) && (block.bbStackDepthOnEntry is not 0))
        {
            BADCODE("Evaluation stack must be empty on entry into a try block");
        }

        // Save the stack contents, we'll need to restore it later
        impSaveStackState(out var blockState, copy: false);

        while (!Unsafe.IsNullRef(in HBtab))
        {
            // Recursively process the handler block, if we haven't already done so.
            var hndBegBB = HBtab.ebdHndBeg;

            if (!hndBegBB.HasFlag(BBF_IMPORTED) && (impGetPendingBlockMember(hndBegBB) is 0))
            {
                // Construct the proper verification stack state either empty or one that contains just the Exception Object that we are dealing with
                stackState.esStackDepth = 0;

                if (handlerGetsXcptnObj(hndBegBB.CatchType))
                {
                    CORINFO_CLASS_HANDLE clsHnd;

                    if (HBtab.HasFilter)
                    {
                        clsHnd = impObjectClass;
                    }
                    else
                    {
                        CORINFO_RESOLVED_TOKEN resolvedToken;

                        resolvedToken.tokenContext = impTokenLookupContextHandle;
                        resolvedToken.tokenScope = info.compScopeHnd;
                        resolvedToken.token = (int)(HBtab.ebdTyp);
                        resolvedToken.tokenType = CORINFO_TOKENKIND_Class;
                        info.compCompHnd->resolveToken(&resolvedToken);

                        clsHnd = resolvedToken.hClass;
                    }

                    // push catch arg the stack, spill to a temp if necessary
                    // Note: can update HBtab->ebdHndBeg!
                    hndBegBB = impPushCatchArgOnStack(hndBegBB, clsHnd, false);
                }

                // Queue up the handler for importing
                //
                impImportBlockPending(hndBegBB);
            }

            // Process the filter block, if we haven't already done so.
            if (HBtab.HasFilter)
            {
                var filterBB = HBtab.ebdFilter;

                if (!filterBB.HasFlag(BBF_IMPORTED) && (impGetPendingBlockMember(filterBB) is 0))
                {
                    stackState.esStackDepth = 0;

                    // push catch arg the stack, spill to a temp if necessary
                    // Note: can update HBtab->ebdFilter!
                    var isSingleBlockFilter = (filterBB.Next == hndBegBB);
                    filterBB = impPushCatchArgOnStack(filterBB, impObjectClass, isSingleBlockFilter);

                    impImportBlockPending(filterBB);
                }
            }

            // Now process our enclosing try index (if any)
            tryIndex = HBtab.ebdEnclosingTryIndex;

            if (tryIndex == EHblkDsc.NO_ENCLOSING_INDEX)
            {
                HBtab = ref Unsafe.NullRef<EHblkDsc>();
            }
            else
            {
                HBtab = ref ehGetDsc(tryIndex);
            }
        }

        // Restore the stack contents
        impRestoreStackState(blockState);
    }

    public void impWalkSpillCliqueFromPred(BasicBlock block, Action<SpillCliqueDir, BasicBlock> callback)
    {
        var toDo = true;

        var succCliqueToDo = null as BlockListNode;
        var predCliqueToDo = new BlockListNode(block);

        while (toDo)
        {
            toDo = false;

            // Look at the successors of every member of the predecessor to-do list.
            while (predCliqueToDo is not null)
            {
                var node = predCliqueToDo;
                predCliqueToDo = node.Next;

                var blk = node.Blk;
                FreeBlockListNode(node);

                foreach (var succ in blk.Succs)
                {
                    // If it's not already in the clique, add it, and also add it
                    // as a member of the successor "toDo" set.
                    if (impSpillCliqueGetMember(SpillCliqueSucc, succ) is 0)
                    {
                        callback(SpillCliqueSucc, succ);
                        impSpillCliqueSetMember(SpillCliqueSucc, succ, 1);
                        succCliqueToDo = new BlockListNode(succ, succCliqueToDo);
                        toDo = true;
                    }
                }
            }

            // Look at the predecessors of every member of the successor to-do list.
            while (succCliqueToDo is not null)
            {
                var node = succCliqueToDo;
                succCliqueToDo = node.Next;

                var blk = node.Blk;
                FreeBlockListNode(node);

                foreach (var predBlock in blk.PredBlocks)
                {
                    // If it's not already in the clique, add it, and also add it
                    // as a member of the predecessor "toDo" set.
                    if (impSpillCliqueGetMember(SpillCliquePred, predBlock) is 0)
                    {
                        callback(SpillCliquePred, predBlock);
                        impSpillCliqueSetMember(SpillCliquePred, predBlock, 1);
                        predCliqueToDo = new BlockListNode(predBlock, predCliqueToDo);
                        toDo = true;
                    }
                }
            }
        }

        // If this fails, it means we didn't walk the spill clique properly and somehow managed
        // miss walking back to include the predecessor we started from.
        // This most likely cause: missing or out of date bbPreds
        assert(impSpillCliqueGetMember(SpillCliquePred, block) is not 0);
    }

    public byte impSpillCliqueGetMember(SpillCliqueDir predOrSucc, BasicBlock blk)
    {
        if (predOrSucc == SpillCliquePred)
        {
            return impInlineRoot.impSpillCliquePredMembers[blk.bbInd];
        }
        else
        {
            assert(predOrSucc == SpillCliqueSucc);
            return impInlineRoot.impSpillCliqueSuccMembers[blk.bbInd];
        }
    }

    public void impSpillCliqueSetMember(SpillCliqueDir predOrSucc, BasicBlock blk, byte val)
    {
        if (predOrSucc == SpillCliquePred)
        {
            impInlineRoot.impSpillCliquePredMembers[blk.bbInd] = val;
        }
        else
        {
            assert(predOrSucc == SpillCliqueSucc);
            impInlineRoot.impSpillCliqueSuccMembers[blk.bbInd] = val;
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

        // Note if this method is has simd args or return value
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
        => impNormStructType(structHnd, out _);

    /// <summary>Normalize the type of a (known to be) struct class handle.</summary>
    /// <param name="structHnd">The class handle for the struct type of interest.</param>
    /// <param name="simdBaseJitType">if the struct is a simd type, set to the simd base JIT type</param>
    /// <returns>The JIT type for the struct (e.g. TYP_STRUCT, or TYP_SIMD*).</returns>
    /// <remarks>
    ///   <para>This may also modify the compFloatingPointUsed flag if the type is a simd type.</para>
    ///   <para>Normalizing the type involves examining the struct type to determine if it should be modified to one that is handled specially by the JIT, possibly being a candidate for full enregistration, e.g. TYP_SIMD16.</para>
    ///   <para>If the size of the struct is already known call <see cref="structSizeMightRepresentSimdType" /> to determine if this api needs to be called.</para>
    /// </remarks>
    public unsafe var_types impNormStructType(CORINFO_CLASS_HANDLE structHnd, out var_types simdBaseJitType)
    {
        assert(structHnd != NO_CLASS_HANDLE);
        var structType = TYP_STRUCT;

#if FEATURE_SIMD
        var structFlags = info.compCompHnd->getClassAttribs(structHnd);

        // Don't bother if the struct contains GC references of byrefs, it can't be a simd type.
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

                    simdBaseJitType = simdBaseType;

                    // Also indicate that we use floating point registers.
                    compFloatingPointUsed = true;
                }
            }
        }
#endif

        simdBaseJitType = TYP_UNDEF;
        return structType;
    }

    /// <summary>Throws an exception for an unsupported named intrinsic</summary>
    /// <param name="helper">JIT helper ID for the exception to be thrown</param>
    /// <param name="method">method handle of the intrinsic function.</param>
    /// <param name="sig">signature of the intrinsic call</param>
    /// <param name="mustExpand">true if the intrinsic must return a GenTree*; otherwise, false</param>
    /// <returns>a gtNewMustThrowException if mustExpand is true; otherwise, null</returns>
    public unsafe GenTree? impUnsupportedNamedIntrinsic(CorInfoHelpFunc helper, CORINFO_METHOD_HANDLE method, in CORINFO_SIG_INFO sig, bool mustExpand)
    {
        // We've hit some error case and may need to return a node for the given error.
        //
        // When `mustExpand=false`, we are attempting to inline the intrinsic directly into another method. In this
        // scenario, we need to return `null` so that a GT_CALL to the intrinsic is emitted instead. This is to
        // ensure that everything continues to behave correctly when optimizations are enabled (e.g. things like the
        // inliner may expect the node we return to have a certain signature, and the `MustThrowException` node won't
        // match that).
        //
        // When `mustExpand=true`, we are in a GT_CALL to the intrinsic and are attempting to JIT it. This will generally
        // be in response to an indirect call (e.g. done via reflection) or in response to an earlier attempt returning
        // `null` (under `mustExpand=false`). In that scenario, we are safe to return the `MustThrowException` node.

        if (mustExpand)
        {
            for (var i = 0; i < sig.numArgs; i++)
            {
                _ = impPopStack();
            }

            return gtNewMustThrowException(helper, sig.retType.VarType, sig.retTypeClass);
        }
        else
        {
            return null;
        }
    }

    /// <summary>The main entry-point for [ReadOnly]Span&lt;char&gt; methods</summary>
    /// <param name="kind">Is it StartsWith, EndsWith or Equals?</param>
    /// <param name="sigInfo">signature of StartsWith, EndsWith or Equals method</param>
    /// <param name="methodFlags">its flags</param>
    /// <returns>GenTree representing vectorized comparison or nullptr</returns>
    public unsafe GenTree? impUtf16SpanComparison(StringComparisonKind kind, in CORINFO_SIG_INFO sigInfo, CorInfoFlag methodFlags)
    {
        // We're going to unroll & vectorize the following cases:
        //  1) MemoryExtensions.SequenceEqual<char>(var, "cns")
        //  2) MemoryExtensions.SequenceEqual<char>("cns", var)
        //
        //  3) MemoryExtensions.Equals(var, "cns", Ordinal or OrdinalIgnoreCase)
        //  4) MemoryExtensions.Equals("cns", var, Ordinal or OrdinalIgnoreCase)
        //
        //  5) MemoryExtensions.StartsWith<char>("cns", var)
        //  6) MemoryExtensions.StartsWith<char>(var, "cns")
        //  7) MemoryExtensions.StartsWith("cns", var, Ordinal or OrdinalIgnoreCase)
        //  8) MemoryExtensions.StartsWith(var, "cns", Ordinal or OrdinalIgnoreCase)
        //
        //  9) MemoryExtensions.EndsWith<char>("cns", var)
        //  10) MemoryExtensions.EndsWith<char>(var, "cns")
        //  11) MemoryExtensions.EndsWith("cns", var, Ordinal or OrdinalIgnoreCase)
        //  12) MemoryExtensions.EndsWith(var, "cns", Ordinal or OrdinalIgnoreCase)

        var isStatic = (methodFlags & CORINFO_FLG_STATIC) is not 0;
        var argsCount = sigInfo.numArgs + (isStatic ? 0 : 1);

        if (lvaHaveManyLocals(0.75f))
        {
            // This optimization spawns several temps so make sure we have a room
            JITDUMP("impUtf16SpanComparison: Method has too many locals - bail out.\n");
            return null;
        }

        var cmpMode = StringComparison.Ordinal;

        GenTree op1;
        GenTree op2;

        if (argsCount == 3)
        {
            // overload with StringComparison

            var stackTop = impStackTop(0).val;

            if (stackTop.Oper.IsIntegralConst)
            {
                var intCon = stackTop.AsIntCon();

                if (intCon.IsIntegralConst((int)(StringComparison.OrdinalIgnoreCase)))
                {
                    cmpMode = StringComparison.OrdinalIgnoreCase;
                }
                else if (!intCon.IsIntegralConst((int)(StringComparison.Ordinal)))
                {
                    return null;
                }
            }

            op1 = impStackTop(2).val;
            op2 = impStackTop(1).val;
        }
        else
        {
            assert(argsCount is 2);

            op1 = impStackTop(1).val;
            op2 = impStackTop(0).val;
        }

        // For generic StartsWith, EndsWith and Equals we need to make sure T is char
        if (sigInfo.sigInst.methInstCount is not 0)
        {
            assert(sigInfo.sigInst.methInstCount is 1);

            var targetElemHnd = sigInfo.sigInst.methInst[0];
            var typ = info.compCompHnd->getTypeForPrimitiveValueClass(targetElemHnd);

            if (typ is not CORINFO_TYPE_SHORT and not CORINFO_TYPE_USHORT and not CORINFO_TYPE_CHAR)
            {
                return null;
            }
        }

        // Try to obtain original string literals out of span arguments
        var op1Str = impGetStrConFromSpan(op1);
        var op2Str = impGetStrConFromSpan(op2);

        GenTree spanObj;
        GenTreeStrCon cnsStr;

        if (op2Str is not null)
        {
            cnsStr = op2Str;
            spanObj = op1;
        }
        else if (op1Str is not null)
        {
            if (kind != StringComparisonKind.Equals)
            {
                // StartsWith and EndsWith are not commutative
                return null;
            }

            cnsStr = op1Str;
            spanObj = op2;
        }
        else
        {
            return null;
        }

        var cnsLength = -1;
        var str = (Span<char>)(stackalloc char[MaxPossibleUnrollSize]);

        if (cnsStr.IsStringEmptyField)
        {
            // check for fake "" first
            cnsLength = 0;
            JITDUMP("Trying to unroll MemoryExtensions.Equals|SequenceEqual|StartsWith(op1, \"\")...\n");
        }
        else
        {
            fixed (char* pStr = str)
            {
                cnsLength = info.compCompHnd->getStringLiteral(cnsStr.ScpHnd, cnsStr.SconCpx, pStr, str.Length, MaxPossibleUnrollSize);
            }

            if (cnsLength < 0)
            {
                // We were unable to get the literal (e.g. dynamic context)
                return null;
            }
            if (cnsLength > (GetUnrollThreshold(MemcmpU16) / 2))
            {
                JITDUMP("UTF16 data is too long to unroll - bail out.\n");
                return null;
            }

            JITDUMP($"Trying to unroll MemoryExtensions.Equals|SequenceEqual|StartsWith(op1, \"{str[..int.Min(cnsLength, 50)]}{((cnsLength > 50) ? "..." : "")}%s\")...\n");
        }

        int spanLclNum;

        if (spanObj.Oper is GT_LCL_VAR)
        {
            // Argument is already a local
            spanLclNum = spanObj.AsLclVarCommon().LclNum;
        }
        else
        {
            CORINFO_CLASS_HANDLE spanCls;

            // Access a local that will be set if we successfully unroll it
            spanLclNum = lvaGrabTemp(shortLifetime: true, "spilling spanObj");

            fixed (CORINFO_SIG_INFO* pSigInfo = &sigInfo)
            {
                _ = info.compCompHnd->getArgType(pSigInfo, sigInfo.args, &spanCls);
            }
            lvaSetStruct(spanLclNum, spanCls, unsafeValueClsCheck: false);
        }

        var spanReferenceFld = gtNewLclFldNode(TYP_BYREF, spanLclNum, OFFSETOF__CORINFO_Span__reference);
        var spanLengthFld = gtNewLclFldNode(TYP_INT, spanLclNum, OFFSETOF__CORINFO_Span__length);
        var unrolled = impExpandHalfConstEquals(spanReferenceFld, spanLengthFld, checkForNull: false, kind, str[..cnsLength], dataOffset: 0, cmpMode);

        if (unrolled is not null)
        {
            // Wrap with the reference equality check for Equals.
            // We believe it's less likely to be useful for StartsWith/EndsWith.
            if (kind == StringComparisonKind.Equals)
            {
                var refEqualityColon = gtNewColonNode(TYP_INT, gtNewTrue(), unrolled);
                unrolled = gtNewQmarkNode(TYP_INT, gtNewBinaryNode(GT_EQ, TYP_INT, gtCloneExpr(spanReferenceFld), gtCloneExpr(cnsStr)), refEqualityColon);
            }

            if (spanObj.Oper is not GT_LCL_VAR)
            {
                impStoreToTemp(spanLclNum, spanObj, CHECK_SPILL_NONE);
            }

            if (unrolled.Oper is GT_QMARK)
            {
                // QMARK can't be a root node, spill it to a temp
                var rootTmp = lvaGrabTemp(shortLifetime: true, "spilling unroll qmark");

                impStoreToTemp(rootTmp, unrolled, CHECK_SPILL_NONE);
                unrolled = gtNewLclvNode(TYP_INT, rootTmp);
            }

            JITDUMP("... Successfully unrolled to:\n");
            DISPTREE(unrolled);
    
            for (var i = 0; i < argsCount; i++)
            {
                _ = impPopStack();
            }

            // We have to clean up GT_RET_EXPR for String.op_Implicit or MemoryExtensions.AsSpans
            if ((spanObj != op1) && (op1.Oper is GT_RET_EXPR))
            {
                var inlineCandidate = op1.AsRetExpr().InlineCandidate;
                assert(inlineCandidate.Oper.IsCall);
                inlineCandidate  = gtNewNothingNode();
            }
            else if ((spanObj != op2) && (op2.Oper is GT_RET_EXPR))
            {
                var inlineCandidate = op2.AsRetExpr().InlineCandidate;
                assert(inlineCandidate.Oper.IsCall);
                inlineCandidate  = gtNewNothingNode();
            }
        }
        return unrolled;
    }

    /// <summary>The main entry-point for String methods</summary>
    /// <param name="kind">Is it StartsWith, EndsWith or Equals?</param>
    /// <param name="sig">signature of StartsWith, EndsWith or Equals method</param>
    /// <param name="methodFlags">its flags</param>
    /// <returns>GenTree representing vectorized comparison or nullptr</returns>
    public unsafe GenTree? impUtf16StringComparison(StringComparisonKind kind, in CORINFO_SIG_INFO sig, CorInfoFlag methodFlags)
    {
        // We're going to unroll & vectorize the following cases:
        //  1) String.Equals(obj, "cns")
        //  2) String.Equals(obj, "cns", Ordinal or OrdinalIgnoreCase)
        //  3) String.Equals("cns", obj)
        //  4) String.Equals("cns", obj, Ordinal or OrdinalIgnoreCase)
        //
        //  5) obj.Equals("cns")
        //  5) obj.Equals("cns")
        //  6) obj.Equals("cns", Ordinal or OrdinalIgnoreCase)
        //
        //  7) "cns".Equals(obj)
        //  8) "cns".Equals(obj, Ordinal or OrdinalIgnoreCase)
        //
        //  9) obj.StartsWith("cns", Ordinal or OrdinalIgnoreCase)
        // 10) "cns".StartsWith(obj, Ordinal or OrdinalIgnoreCase)
        //
        // 11) obj.EndsWith("cns", Ordinal or OrdinalIgnoreCase)
        // 12) "cns".EndsWith(obj, Ordinal or OrdinalIgnoreCase)
        //
        // For cases 5, 6 and 9 we don't emit "obj != null"
        // NOTE: String.Equals(object) is not supported currently

        var isStatic = (methodFlags & CORINFO_FLG_STATIC) is not 0;
        var argsCount = sig.numArgs + (isStatic ? 0 : 1);

        // This optimization spawns several temps so make sure we have a room
        if (lvaHaveManyLocals(0.75f))
        {
            JITDUMP("impUtf16StringComparison: Method has too many locals - bail out.\n");
            return null;
        }

        var cmpMode = StringComparison.Ordinal;

        GenTree op1;
        GenTree op2;

        if (argsCount == 3) // overload with StringComparison
        {
            var stackTop = impStackTop(0).val;

            if (stackTop.Oper.IsIntegralConst)
            {
                var intCon = stackTop.AsIntCon();

                if (intCon.IsIntegralConst((int)(StringComparison.OrdinalIgnoreCase)))
                {
                    cmpMode = StringComparison.OrdinalIgnoreCase;
                }
                else if (!intCon.IsIntegralConst((int)(StringComparison.Ordinal)))
                {
                    return null;
                }
            }
            op1 = impStackTop(2).val;
            op2 = impStackTop(1).val;
        }
        else
        {
            assert(argsCount == 2);

            op1 = impStackTop(1).val;
            op2 = impStackTop(0).val;
        }

        GenTree varStr;
        GenTreeStrCon cnsStr;

        if (op2.Oper is GT_CNS_STR)
        {
            cnsStr = op2.AsStrCon();
            varStr = op1;
        }
        else if (op1.Oper is GT_CNS_STR)
        {
            if (kind != StringComparisonKind.Equals)
            {
                // StartsWith and EndsWith are not commutative
                return null;
            }
            cnsStr = op1.AsStrCon();
            varStr = op2;
        }
        else
        {
            return null;
        }

        var needsNullcheck = true;

        if ((op1 != cnsStr) && !isStatic)
        {
            // for the following cases we should not check varStr for null:
            //
            //  obj.Equals("cns")
            //  obj.Equals("cns", Ordinal or OrdinalIgnoreCase)
            //  obj.StartsWith("cns", Ordinal or OrdinalIgnoreCase)
            //  obj.EndsWith("cns", Ordinal or OrdinalIgnoreCase)
            //
            // instead, it should throw NRE if it's null
            needsNullcheck = false;
        }

        int cnsLength;
        var str = (Span<char>)(stackalloc char[MaxPossibleUnrollSize]);

        if (cnsStr.IsStringEmptyField)
        {
            // check for fake "" first
            cnsLength = 0;
            JITDUMP($"Trying to unroll String.Equals|StartsWith|EndsWith(op1, \"{str}\")...\n");
        }
        else
        {
            fixed (char* pStr = str)
            {
                cnsLength = info.compCompHnd->getStringLiteral(cnsStr.ScpHnd, cnsStr.SconCpx, pStr, MaxPossibleUnrollSize);
            }

            if (cnsLength < 0)
            {
                // We were unable to get the literal (e.g. dynamic context)
                return null;
            }

            if (cnsLength > (GetUnrollThreshold(MemcmpU16) / 2))
            {
                JITDUMP("UTF16 data is too long to unroll - bail out.\n");
                return null;
            }
            JITDUMP("Trying to unroll String.Equals|StartsWith|EndsWith(op1, \"cns\")...\n");
        }

        // Create a temp which is safe to gtClone for varStr
        // We're not appending it as a statement until we figure out unrolling is profitable (and possible)
        var varStrTmp = lvaGrabTemp(shortLifetime: true, "spilling varStr");
        lvaGetDesc(varStrTmp).Type = varStr.Type;
        var varStrLcl = gtNewLclvNode(varStr.Type, varStrTmp);

        // Create a tree representing string's Length:
        var strLenOffset = OFFSETOF__CORINFO_String__stringLen;
        var lenNode = gtNewArrLen(TYP_INT, varStrLcl, strLenOffset);
        varStrLcl = gtCloneLclVar(varStrLcl);

        var unrolled = null as GenTree;

        fixed (char* pStr = str)
        {
            unrolled = impExpandHalfConstEquals(varStrLcl, lenNode, needsNullcheck, kind, str[..cnsLength], strLenOffset + sizeof(int), cmpMode);
        }

        if (unrolled is not null)
        {
            // Wrap with the reference equality check for Equals.
            // We believe it's less likely to be useful for StartsWith/EndsWith.
            if (kind is StringComparisonKind.Equals)
            {
                var refEqualityColon = gtNewColonNode(TYP_INT, gtNewTrue(), unrolled);
                unrolled = gtNewQmarkNode(TYP_INT, gtNewBinaryNode(GT_EQ, TYP_INT, gtCloneExpr(varStrLcl), gtCloneExpr(cnsStr)), refEqualityColon);
            }

            impStoreToTemp(varStrTmp, varStr, CHECK_SPILL_NONE);

            if (unrolled.Oper is GT_QMARK)
            {
                // QMARK nodes cannot reside on the evaluation stack
                var rootTmp = lvaGrabTemp(true, "spilling unroll qmark");
                impStoreToTemp(rootTmp, unrolled, CHECK_SPILL_NONE);
                unrolled = gtNewLclvNode(TYP_INT, rootTmp);
            }

            JITDUMP("\n... Successfully unrolled to:\n");
            DISPTREE(unrolled);

            for (var i = 0; i < argsCount; i++)
            {
                _ = impPopStack();
            }
        }
        return unrolled;
    }

    public static bool impValidSpilledStackEntry(GenTree tree)
    {
        var oper = tree.Oper;
        return (oper is GT_LCL_VAR) || oper.IsConst;
    }

    /// <summary>Set the pre-state of "block" (which should not have a pre-state allocated) to a copy of "srcState", cloning tree pointers as required.</summary>
    /// <param name="block"></param>
    /// <param name="srcState"></param>
    public void initBBEntryState(BasicBlock block, EntryState srcState)
    {
        ref var dstState = ref block.EntryState;

        var depth = srcState.esStackDepth;
        dstState.esStackDepth = depth;

        if (depth is not 0)
        {
            var srcStack = srcState.esStack.AsSpan(0, depth);
            dstState.esStack = [.. srcState.esStack];
            var dstStack = dstState.esStack.AsSpan(0, depth);

            for (var level = 0; level < srcStack.Length; level++)
            {
                var tree = srcStack[level].val;
                dstStack[level].val = gtCloneExpr(tree);
            }
        }
        else
        {
            dstState.esStack = [];
        }
    }

    public void initCurrentState()
    {
        // initialize stack info
        stackState.esStackDepth = 0;

        assert(fgFirstBB is not null);

        // copy current state to entry state of first BB
        initBBEntryState(fgFirstBB, stackState);
    }

    /// <summary>Check if devirtualizing a call node as a specified target method call is reasonable.</summary>
    /// <param name="call">the call</param>
    /// <param name="gdvTarget">the target method that we want to guess for and devirtualize to</param>
    /// <returns>true if we can proceed with GDV.</returns>
    /// <remarks>This implements a small simplified signature-compatibility check to verify that a guess is reasonable. The main goal here is to avoid blowing up the JIT on PGO data with stale GDV candidates; if they are not compatible in the ECMA sense then we do not expect the guard to ever pass at runtime, so we can get by with simplified rules here.</remarks>
    public unsafe bool isCompatibleMethodGdv(GenTreeCall call, CORINFO_METHOD_HANDLE gdvTarget)
    {
        CORINFO_SIG_INFO sigInfo;
        info.compCompHnd->getMethodSig(gdvTarget, &sigInfo);

        var sigParam = sigInfo.args;
        var numParams = sigInfo.numArgs;
        var numArgs = 0;

        var gdvType = sigInfo.retType.VarType;
        var callType = call._returnType;

        var sameRetTypes = (callType.ActualType == gdvType.ActualType) || (varTypeIsStruct(callType) && varTypeIsStruct(gdvType));

        if (!sameRetTypes)
        {
            JITDUMP("Incompatible method GDV: Return types do not match - bail out.\n");
            return false;
        }

        if (varTypeIsStruct(gdvType))
        {
            assert(varTypeIsStruct(callType));

            CORINFO_SIG_INFO callSig;
            info.compCompHnd->getMethodSig(call._callMethHnd, &callSig);

            _ = GetReturnTypeForStruct(callSig.retTypeClass, call.UnmanagedCallConv, out var callRetKind);
            _ = GetReturnTypeForStruct(sigInfo.retTypeClass, call.UnmanagedCallConv, out var gdvRetKind);

            if ((callRetKind != gdvRetKind) || !ClassLayout.AreCompatible(typGetObjLayout(callSig.retTypeClass), typGetObjLayout(sigInfo.retTypeClass)))
            {
                JITDUMP("Incompatible method GDV: Return struct types do not match - bail out.\n");
                return false;
            }
        }

        foreach (var arg in call.Args.Args)
        {
            switch (arg.WellKnownArg)
            {
                case WellKnownArg.RetBuffer:
                case WellKnownArg.ThisPointer:
                case WellKnownArg.AsyncContinuation:
                {
                    // Not part of signature but we still expect to see it here
                    continue;
                }

                case WellKnownArg.None:
                {
                    break;
                }

                default:
                {
                    NO_WAY("Unexpected well known arg to method GDV candidate");
                    continue;
                }
            }
            numArgs++;

            if (numArgs > numParams)
            {
                JITDUMP($"Incompatible method GDV: call [{call.TreeId:D6}] has more arguments than signature (sig has {numParams} parameters)\n");
                return false;
            }

            var classHnd = NO_CLASS_HANDLE;
            var corType = strip(info.compCompHnd->getArgType(&sigInfo, sigParam, &classHnd));
            var sigType = corType.VarType;

            if (!impCheckImplicitArgumentCoercion(sigType, arg.Node.Type))
            {
                JITDUMP($"Incompatible method GDV: arg [{arg.Node.TreeId:D6}] is type-incompatible with signature of target\n");
                return false;
            }

            // Best-effort check for struct compatibility here.
            if (varTypeIsStruct(sigType) && (arg.SignatureClassHandle != classHnd))
            {
                var callLayout = typGetObjLayout(arg.SignatureClassHandle);
                var tarLayout = typGetObjLayout(classHnd);

                if (!ClassLayout.AreCompatible(callLayout, tarLayout))
                {
                    JITDUMP($"Incompatible method GDV: struct arg [{arg.Node.TreeId:D6}] is layout-incompatible with signature of target\n");
                    return false;
                }
            }
            sigParam = info.compCompHnd->getArgNext(sigParam);
        }

        if (numArgs < numParams)
        {
            JITDUMP($"Incompatible method GDV: call [{call.TreeId:D6}] has fewer arguments ({numArgs}) than signature ({numParams})\n");
            return false;
        }
        return true;
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
            case NI_PRIMITIVE_PopCount:
            case NI_PRIMITIVE_TrailingZeroCount:
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

    /// <summary>Use profile information to pick a GDV/cast type candidate for a call site.</summary>
    /// <param name="call">the call (either virtual or cast helper)</param>
    /// <param name="ilOffset">exact IL offset of the call</param>
    /// <param name="isInterface">whether or not the call target is defined on an interface</param>
    /// <param name="classGuesses">the classes to guess for (mutually exclusive with methodGuess)</param>
    /// <param name="methodGuesses">the methods to guess for (mutually exclusive with classGuess)</param>
    /// <param name="candidatesCount">number of guesses</param>
    /// <param name="likelihoods">estimates of the likelihoods that the guesses will succeed</param>
    /// <param name="verboseLogging">whether or not to do verbose logging</param>
    public unsafe void pickGDV(GenTreeCall call, IL_OFFSET ilOffset, bool isInterface, Span<nint> classGuesses, Span<nint> methodGuesses, out int candidatesCount, Span<int> likelihoods, bool verboseLogging = true)
    {
        candidatesCount = 0;

        // Get the relevant pgo info for this call
        assert(call._inlineContext is not null);

        var pgoInfo = new PgoInfo(call._inlineContext);
        var schema = new Span<ICorJitInfo.PgoInstrumentationSchema>(pgoInfo.PgoSchema, pgoInfo.PgoSchemaCount);

        Unsafe.SkipInit(out InlineArrayMaxGdvTypeChecks<LikelyClassMethodRecord> inlineLikelyClasses);
        var likelyClasses = (Span<LikelyClassMethodRecord>)(inlineLikelyClasses);

        var numberOfClasses = 0;

        if (call.IsVirtualStub || call.IsVirtualVtable || call.IsHelperCall())
        {
            numberOfClasses = getLikelyClasses(likelyClasses, schema, pgoInfo.PgoData, ilOffset);
        }

        Unsafe.SkipInit(out InlineArrayMaxGdvTypeChecks<LikelyClassMethodRecord> inlineLikelyMethods);
        var likelyMethods = (Span<LikelyClassMethodRecord>)(inlineLikelyMethods);

        var numberOfMethods = 0;
        var isInferredGDV = false;

        // TODO-GDV: R2R support requires additional work to reacquire the
        // entrypoint, similar to what happens at the end of impDevirtualizeCall.
        // As part of supporting this we should merge the tail of
        // impDevirtualizeCall and what happens in
        // GuardedDevirtualizationTransformer.CreateThen for method GDV.
        //
        if (!IsAot && (call.IsVirtualVtable || call.IsDelegateInvoke))
        {
            assert(!call.IsHelperCall());
            numberOfMethods = getLikelyMethods(likelyMethods, schema, pgoInfo.PgoData, ilOffset);
        }

        if ((numberOfClasses < 1) && (numberOfMethods < 1) && hasImpEnumeratorLikelyTypeMap)
        {
            // See if we can infer a GDV here for enumerator var uses
            var thisArg = call.Args.FindWellKnownArg(WellKnownArg.ThisPointer);

            if (thisArg is not null)
            {
                var thisNode = thisArg.EarlyNode;

                if (thisNode.Oper is GT_LCL_VAR)
                {
                    var thisLclNum = thisNode.AsLclVarCommon().LclNum;

                    if (lvaGetDesc(thisLclNum).lvIsEnumerator)
                    {
                        var map = ImpEnumeratorLikelyTypeMap;

                        if (map.TryGetValue(thisLclNum, out var e))
                        {
                            JITDUMP($"Recalling that V{thisLclNum:D2} has {e._likelihood}% likely class {eeGetClassName(e._classHandle)}\n");
                            numberOfClasses = 1;

                            ref var likelyClass = ref likelyClasses[0];

                            likelyClass.handle = unchecked((nint)(e._classHandle));
                            likelyClass.likelihood = e._likelihood;

                            isInferredGDV = true;
                        }
                    }
                }
            }
        }

        if ((numberOfClasses < 1) && (numberOfMethods < 1))
        {
            if (verboseLogging)
            {
                JITDUMP("No likely class or method, sorry\n");
            }
            return;
        }

#if DEBUG
        if ((verbose || (JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] is not 0)) && (numberOfClasses > 0) && verboseLogging)
        {
            JITDUMP($"Likely classes for call [{call.TreeId:D6}]");

            if (!call.IsHelperCall())
            {
                var thisArg = call.Args.ThisArg;
                assert(thisArg is not null);

                var declaredThisClsHnd = gtGetClassHandle(thisArg.Node, out _, out _);

                if (declaredThisClsHnd != NO_CLASS_HANDLE)
                {
                    var baseClassName = eeGetClassName(declaredThisClsHnd);
                    JITDUMP($" on class {dspPtr(declaredThisClsHnd)} ({baseClassName})");
                }
            }
            JITDUMP("\n");

            for (var i = 0; i < numberOfClasses; i++)
            {
                ref var likelyClass = ref likelyClasses[i];
                var classHandle = unchecked((CORINFO_CLASS_HANDLE)(likelyClass.handle));

                var className = eeGetClassName(classHandle);
                JITDUMP($"  {i + 1}) {likelyClass.handle:X8} ({className}) [likelihood:{likelyClass.likelihood}%]\n");
            }
        }

        if ((verbose || (JitConfig[ConfigInteger.EnableExtraSuperPmiQueries] is not 0)) && (numberOfMethods > 0))
        {
            assert(call._callType is CT_USER_FUNC);

            var baseMethName = eeGetMethodFullName(call._callMethHnd);
            JITDUMP($"Likely methods for call [{call.TreeId:D6}] to method {baseMethName}\n");

            for (var i = 0; i < numberOfMethods; i++)
            {
                ref var likelyMethod = ref likelyMethods[i];
                var methodHandle = unchecked((CORINFO_METHOD_HANDLE)(likelyMethod.handle));

                var lookup = new CORINFO_CONST_LOOKUP();
                info.compCompHnd->getFunctionFixedEntryPoint(methodHandle, false, &lookup);

                var methName = eeGetMethodFullName(methodHandle);

                switch (lookup.accessType)
                {
                    case IAT_VALUE:
                    {
                        JITDUMP($"  {i + 1}) {dspPtr(lookup.addr)} ({methName}) [likelihood:{likelyMethod.likelihood}%]\n");
                        break;
                    }

                    case IAT_PVALUE:
                    {
                        JITDUMP($"  {i + 1}) [{dspPtr(lookup.addr)}] ({methName}) [likelihood:{likelyMethod.likelihood}%]\n");
                        break;
                    }

                    case IAT_PPVALUE:
                    {
                        JITDUMP($"  {i + 1}) [[{dspPtr(lookup.addr)}]] ({methName}) [likelihood:{likelyMethod.likelihood}%]\n");
                        break;
                    }

                    default:
                    {
                        JITDUMP($"  {i + 1}) {methName} [likelihood:{likelyMethod.likelihood}%]\n");
                        break;
                    }
                }
            }
        }

        // Optional stress mode to pick a random known class, rather than the most likely known class.
        if (JitConfig[ConfigInteger.JitRandomGuardedDevirtualization] is not 0)
        {
            assert(impInlineRoot._inlineStrategy is not null);

            // Reuse the random inliner's random state.
            var random = impInlineRoot._inlineStrategy.GetRandom(JitConfig[ConfigInteger.JitRandomGuardedDevirtualization]);
            var index = random.Next(numberOfClasses + numberOfMethods);

            if (index < numberOfClasses)
            {
                ref var likelyClass = ref likelyClasses[index];
                var classHandle = unchecked((CORINFO_CLASS_HANDLE)(likelyClass.handle));

                classGuesses[0] = likelyClass.handle;
                likelihoods[0] = 100;

                candidatesCount = 1;

                // TODO: report multiple random candidates. For now we don't do it because with the current impl
                // we might give up on all candidates if one of them is not inlinable, so we don't want to reduce
                // testing coverage.
                //
                JITDUMP($"Picked random class for GDV: {dspPtr(classHandle)} ({eeGetClassName(classHandle)})\n");
                return;
            }
            else
            {
                assert(!call.IsHelperCall());

                ref var likelyMethod = ref likelyMethods[index - numberOfClasses];
                var methodHandle = unchecked((CORINFO_METHOD_HANDLE)(likelyMethod.handle));

                methodGuesses[0] = likelyMethod.handle;
                likelihoods[0] = 100;

                candidatesCount = 1;

                // TODO: report multiple random candidates. For now we don't do it because with the current impl
                // we might give up on all candidates if one of them is not inlinable, so we don't want to reduce
                // testing coverage.

                JITDUMP($"Picked random method for GDV: {dspPtr(methodHandle)} ({eeGetMethodFullName(methodHandle)})\n");
                return;
            }
        }
#endif

        // Prefer class guess as it is cheaper
        if (numberOfClasses > 0)
        {
            var maxNumberOfGuesses = GetGdvMaxTypeChecks();

            if (maxNumberOfGuesses is 0)
            {
                // DOTNET_JitGuardedDevirtualizationMaxTypeChecks=0 means we don't want to do any guarded devirtualization
                // Although, we expect users to disable GDV by setting DOTNET_JitEnableGuardedDevirtualization=0
                return;
            }

            assert((maxNumberOfGuesses > 0) && (maxNumberOfGuesses <= MAX_GDV_TYPE_CHECKS));

            // Default: We're allowed to make more than 2 guesses - pick all types with likelihood >= 10%
            var likelihoodThreshold = 10;

            if (maxNumberOfGuesses is 1)
            {
                // We're allowed to make only a single guess - it means we want to work only with dominating types

                if (call.IsHelperCall())
                {
                    // Casts. Most casts aren't too expensive
                    likelihoodThreshold = 50;
                }
                else if (isInterface)
                {
                    // interface calls
                    likelihoodThreshold = 25;
                }
                else
                {
                    // virtual calls
                    likelihoodThreshold = 30;
                }
            }
            else if (maxNumberOfGuesses is 2)
            {
                // Two guesses - slightly relax the thresholds

                if (call.IsHelperCall())
                {
                    // Casts. Most casts aren't too expensive
                    likelihoodThreshold = 40;
                }
                else if (isInterface)
                {
                    // interface calls
                    likelihoodThreshold = 15;
                }
                else
                {
                    // virtual calls
                    likelihoodThreshold = 20;
                }
            }

            // We have 'maxNumberOfGuesses' number of classes available
            // and we're allowed to make 'maxNumberOfGuesses' number of guesses
            // Iterate over the available classes to find classes with likelihoods bigger than
            // a specific threshold

            var totalGuesses = int.Min(maxNumberOfGuesses, numberOfClasses);

            for (var guessIdx = 0; guessIdx < totalGuesses; guessIdx++)
            {
                ref var likelyClass = ref likelyClasses[guessIdx];
                var classHandle = unchecked((CORINFO_CLASS_HANDLE)(likelyClass.handle));

                if (likelyClass.likelihood >= likelihoodThreshold)
                {
                    classGuesses[guessIdx] = likelyClass.handle;
                    likelihoods[guessIdx] = likelyClass.likelihood;

                    candidatesCount += 1;

                    if (verboseLogging)
                    {
                        JITDUMP($"Accepting type {eeGetClassName(classHandle)} with likelihood {likelyClass.likelihood} as a candidate\n");
                    }

                    // If the 'this' arg to the call is an enumerator var, record any
                    // dominant likely class so we can possibly infer a GDV at places where we
                    // never observed the var's value. (eg an unreached Dispose call if
                    // control is hijacked out of Tier0+i by OSR).
                    //
                    // Note enumerator vars are special as they are generally not redefined
                    // and we want to ensure all methods called on them get inlined to enable
                    // escape analysis to kick in, if possible.
                    //
                    var dominantLikelihood = 50;

                    if (!isInferredGDV && (likelyClass.likelihood >= dominantLikelihood))
                    {
                        var thisArg = call.Args.FindWellKnownArg(WellKnownArg.ThisPointer);

                        if (thisArg is not null)
                        {
                            var thisNode = thisArg.EarlyNode;

                            if (thisNode.Oper is GT_LCL_VAR)
                            {
                                var thisLclNum = thisNode.AsLclVarCommon().LclNum;                                

                                if (lvaGetDesc(thisLclNum).lvIsEnumerator)
                                {
                                    var map = ImpEnumeratorLikelyTypeMap;

                                    // If we have multiple type observations, we just use the first.
                                    //
                                    // Note importation order is somewhat reverse-post-orderish;
                                    // a block is only imported if one of its imported preds is imported.
                                    //
                                    // Enumerator vars tend to have a dominating MoveNext call that will
                                    // be the one subsequent uses will see, if they lack their own
                                    // type observations.

                                    if (!map.TryGetValue(thisLclNum, out _))
                                    {
                                        var e = new InferredGdvEntry {
                                            _classHandle = classHandle,
                                            _likelihood = likelyClass.likelihood,
                                        };

                                        JITDUMP($"Remembering that V{thisLclNum:D2} has {e._likelihood}% likely class {eeGetClassName(e._classHandle)}\n");
                                        map[thisLclNum] = e;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // The candidates are sorted by likelihood so the rest of the
                    // guesses will have even lower likelihoods
                    break;
                }
            }
        }

        if (numberOfMethods > 0)
        {
            // For method guessing we only support a single target for now
            const int LikelihoodThreshold = 30;

            ref var likelyMethod = ref likelyMethods[0];

            if (likelyMethod.likelihood >= LikelihoodThreshold)
            {
                methodGuesses[0] = likelyMethod.handle;
                likelihoods[0] = likelyMethod.likelihood;

                candidatesCount = 1;
                return;
            }

            if (verboseLogging)
            {
                JITDUMP($"Not guessing for method; likelihood is below {(call.IsDelegateInvoke ? "delegate" : "virtual")} call threshold {LikelihoodThreshold}\n");
            }
        }
    }

    public void ReimportSpillClique(SpillCliqueDir predOrSucc, BasicBlock blk)
    {
        // For Preds we could be a little smarter and just find the existing store
        // and re-type it/add a cast, but that is complicated and hopefully very rare, so
        // just re-import the whole block (just like we do for successors)

        if (!blk.HasFlag(BBF_IMPORTED) && (impGetPendingBlockMember(blk) is 0))
        {
            // If we haven't imported this block (EntryState is a null ref) and we're not going to
            // (because it isn't on the pending list) then just ignore it for now.
            assert(blk.EntryState.esStackDepth is 0);
            assert(blk.EntryState.esStack.Length is 0);
            return;
        }

        // For successors we have a valid stackState, so just mark them for reimport
        // the 'normal' way
        // Unlike predecessors, we *DO* need to reimport the current block because the
        // initial import had the wrong entry state types.
        // Similarly, blocks that are currently on the pending list, still need to call
        // impImportBlockPending to fixup their entry state.
        if (predOrSucc == SpillCliqueSucc)
        {
            impReimportMarkBlock(blk);

            // Set the current stack state to that of the blk->bbEntryState
            resetCurrentState(blk, ref stackState);

            impImportBlockPending(blk);
        }
        else if ((blk != compCurBB) && blk.HasFlag(BBF_IMPORTED))
        {
            // As described above, we are only visiting predecessors so they can
            // add the appropriate casts, since we have already done that for the current
            // block, it does not need to be reimported.
            // Nor do we need to reimport blocks that are still pending, but not yet
            // imported.
            //
            // For predecessors, we have no state to seed the EntryState, so we just have
            // to assume the existing one is correct.
            // If the block is also a successor, it will get the EntryState properly
            // updated when it is visited as a successor in the above "if" block.
            assert(predOrSucc == SpillCliquePred);
            impReimportBlockPending(blk);
        }
    }

    /// <summary>Resets the current state to the state at the start of the basic block</summary>
    /// <param name="block"></param>
    /// <param name="currentState"></param>
    public void resetCurrentState(BasicBlock block, ref EntryState currentState)
    {
        ref var entryState = ref block.EntryState;

        currentState.esStackDepth = entryState.esStackDepth;
        currentState.esStack = (entryState.esStack.Length is not 0) ? [.. entryState.esStack] : [];
    }
}
