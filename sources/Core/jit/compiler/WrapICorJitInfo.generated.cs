// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_CLRAPI_CALLS
using static RyuJitSharp.ICorJitInfo;

namespace RyuJitSharp;

public sealed unsafe partial class WrapICorJitInfo
{
    public bool isIntrinsic(CORINFO_METHOD_HANDLE ftn)
    {
        wrapComp.CLR_API_Enter(API_isIntrinsic);
        bool temp = wrapHnd->isIntrinsic(ftn);
        wrapComp.CLR_API_Leave(API_isIntrinsic);

        return temp;
    }

    public bool canValueClassInstancePointerEscape(CORINFO_METHOD_HANDLE ftn)
    {
        wrapComp.CLR_API_Enter(API_canValueClassInstancePointerEscape);
        bool temp = wrapHnd->canValueClassInstancePointerEscape(ftn);
        wrapComp.CLR_API_Leave(API_canValueClassInstancePointerEscape);

        return temp;
    }

    public bool notifyMethodInfoUsage(CORINFO_METHOD_HANDLE ftn)
    {
        wrapComp.CLR_API_Enter(API_notifyMethodInfoUsage);
        bool temp = wrapHnd->notifyMethodInfoUsage(ftn);
        wrapComp.CLR_API_Leave(API_notifyMethodInfoUsage);

        return temp;
    }

    public CorInfoFlag getMethodAttribs(CORINFO_METHOD_HANDLE ftn)
    {
        wrapComp.CLR_API_Enter(API_getMethodAttribs);
        CorInfoFlag temp = wrapHnd->getMethodAttribs(ftn);
        wrapComp.CLR_API_Leave(API_getMethodAttribs);

        return temp;
    }

    public void setMethodAttribs(CORINFO_METHOD_HANDLE ftn, CorInfoMethodRuntimeFlags attribs)
    {
        wrapComp.CLR_API_Enter(API_setMethodAttribs);
        wrapHnd->setMethodAttribs(ftn, attribs);
        wrapComp.CLR_API_Leave(API_setMethodAttribs);
    }

    public void getMethodSig(CORINFO_METHOD_HANDLE ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_HANDLE memberParent = null)
    {
        wrapComp.CLR_API_Enter(API_getMethodSig);
        wrapHnd->getMethodSig(ftn, sig, memberParent);
        wrapComp.CLR_API_Leave(API_getMethodSig);
    }

    public bool getMethodInfo(CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_INFO* info, CORINFO_CONTEXT_HANDLE context = null)
    {
        wrapComp.CLR_API_Enter(API_getMethodInfo);
        bool temp = wrapHnd->getMethodInfo(ftn, info, context);
        wrapComp.CLR_API_Leave(API_getMethodInfo);

        return temp;
    }

    public bool haveSameMethodDefinition(CORINFO_METHOD_HANDLE meth1Hnd, CORINFO_METHOD_HANDLE meth2Hnd)
    {
        wrapComp.CLR_API_Enter(API_haveSameMethodDefinition);
        bool temp = wrapHnd->haveSameMethodDefinition(meth1Hnd, meth2Hnd);
        wrapComp.CLR_API_Leave(API_haveSameMethodDefinition);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getTypeDefinition(CORINFO_CLASS_HANDLE type)
    {
        wrapComp.CLR_API_Enter(API_getTypeDefinition);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getTypeDefinition(type);
        wrapComp.CLR_API_Leave(API_getTypeDefinition);

        return temp;
    }

    public CorInfoInline canInline(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd)
    {
        wrapComp.CLR_API_Enter(API_canInline);
        CorInfoInline temp = wrapHnd->canInline(callerHnd, calleeHnd);
        wrapComp.CLR_API_Leave(API_canInline);

        return temp;
    }

    public void beginInlining(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd)
    {
        wrapComp.CLR_API_Enter(API_beginInlining);
        wrapHnd->beginInlining(inlinerHnd, inlineeHnd);
        wrapComp.CLR_API_Leave(API_beginInlining);
    }

    public void reportInliningDecision(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd, CorInfoInline inlineResult, byte* reason)
    {
        wrapComp.CLR_API_Enter(API_reportInliningDecision);
        wrapHnd->reportInliningDecision(inlinerHnd, inlineeHnd, inlineResult, reason);
        wrapComp.CLR_API_Leave(API_reportInliningDecision);
    }

    public bool canTailCall(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE declaredCalleeHnd, CORINFO_METHOD_HANDLE exactCalleeHnd, bool fIsTailPrefix)
    {
        wrapComp.CLR_API_Enter(API_canTailCall);
        bool temp = wrapHnd->canTailCall(callerHnd, declaredCalleeHnd, exactCalleeHnd, fIsTailPrefix);
        wrapComp.CLR_API_Leave(API_canTailCall);

        return temp;
    }

    public void reportTailCallDecision(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd, bool fIsTailPrefix, CorInfoTailCall tailCallResult, byte* reason)
    {
        wrapComp.CLR_API_Enter(API_reportTailCallDecision);
        wrapHnd->reportTailCallDecision(callerHnd, calleeHnd, fIsTailPrefix, tailCallResult, reason);
        wrapComp.CLR_API_Leave(API_reportTailCallDecision);
    }

    public void getEHinfo(CORINFO_METHOD_HANDLE ftn, int EHnumber, CORINFO_EH_CLAUSE* clause)
    {
        wrapComp.CLR_API_Enter(API_getEHinfo);
        wrapHnd->getEHinfo(ftn, EHnumber, clause);
        wrapComp.CLR_API_Leave(API_getEHinfo);
    }

    public CORINFO_CLASS_HANDLE getMethodClass(CORINFO_METHOD_HANDLE method)
    {
        wrapComp.CLR_API_Enter(API_getMethodClass);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getMethodClass(method);
        wrapComp.CLR_API_Leave(API_getMethodClass);

        return temp;
    }

    public void getMethodVTableOffset(CORINFO_METHOD_HANDLE method, int* offsetOfIndirection, int* offsetAfterIndirection, bool* isRelative)
    {
        wrapComp.CLR_API_Enter(API_getMethodVTableOffset);
        wrapHnd->getMethodVTableOffset(method, offsetOfIndirection, offsetAfterIndirection, isRelative);
        wrapComp.CLR_API_Leave(API_getMethodVTableOffset);
    }

    public bool resolveVirtualMethod(CORINFO_DEVIRTUALIZATION_INFO* info)
    {
        wrapComp.CLR_API_Enter(API_resolveVirtualMethod);
        bool temp = wrapHnd->resolveVirtualMethod(info);
        wrapComp.CLR_API_Leave(API_resolveVirtualMethod);

        return temp;
    }

    public CORINFO_METHOD_HANDLE getAsyncOtherVariant(CORINFO_METHOD_HANDLE ftn, bool* variantIsThunk)
    {
        wrapComp.CLR_API_Enter(API_getAsyncOtherVariant);
        CORINFO_METHOD_HANDLE temp = wrapHnd->getAsyncOtherVariant(ftn, variantIsThunk);
        wrapComp.CLR_API_Leave(API_getAsyncOtherVariant);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getDefaultComparerClass(CORINFO_CLASS_HANDLE elemType)
    {
        wrapComp.CLR_API_Enter(API_getDefaultComparerClass);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getDefaultComparerClass(elemType);
        wrapComp.CLR_API_Leave(API_getDefaultComparerClass);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getDefaultEqualityComparerClass(CORINFO_CLASS_HANDLE elemType)
    {
        wrapComp.CLR_API_Enter(API_getDefaultEqualityComparerClass);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getDefaultEqualityComparerClass(elemType);
        wrapComp.CLR_API_Leave(API_getDefaultEqualityComparerClass);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getSZArrayHelperEnumeratorClass(CORINFO_CLASS_HANDLE elemType)
    {
        wrapComp.CLR_API_Enter(API_getSZArrayHelperEnumeratorClass);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getSZArrayHelperEnumeratorClass(elemType);
        wrapComp.CLR_API_Leave(API_getSZArrayHelperEnumeratorClass);

        return temp;
    }

    public void expandRawHandleIntrinsic(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_GENERICHANDLE_RESULT* pResult)
    {
        wrapComp.CLR_API_Enter(API_expandRawHandleIntrinsic);
        wrapHnd->expandRawHandleIntrinsic(pResolvedToken, callerHandle, pResult);
        wrapComp.CLR_API_Leave(API_expandRawHandleIntrinsic);
    }

    public bool isIntrinsicType(CORINFO_CLASS_HANDLE classHnd)
    {
        wrapComp.CLR_API_Enter(API_isIntrinsicType);
        bool temp = wrapHnd->isIntrinsicType(classHnd);
        wrapComp.CLR_API_Leave(API_isIntrinsicType);

        return temp;
    }

    public CorInfoCallConvExtension getUnmanagedCallConv(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig, bool* pSuppressGCTransition)
    {
        wrapComp.CLR_API_Enter(API_getUnmanagedCallConv);
        CorInfoCallConvExtension temp = wrapHnd->getUnmanagedCallConv(method, callSiteSig, pSuppressGCTransition);
        wrapComp.CLR_API_Leave(API_getUnmanagedCallConv);

        return temp;
    }

    public bool pInvokeMarshalingRequired(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig)
    {
        wrapComp.CLR_API_Enter(API_pInvokeMarshalingRequired);
        bool temp = wrapHnd->pInvokeMarshalingRequired(method, callSiteSig);
        wrapComp.CLR_API_Leave(API_pInvokeMarshalingRequired);

        return temp;
    }

    public bool satisfiesMethodConstraints(CORINFO_CLASS_HANDLE parent, CORINFO_METHOD_HANDLE method)
    {
        wrapComp.CLR_API_Enter(API_satisfiesMethodConstraints);
        bool temp = wrapHnd->satisfiesMethodConstraints(parent, method);
        wrapComp.CLR_API_Leave(API_satisfiesMethodConstraints);

        return temp;
    }

    public void methodMustBeLoadedBeforeCodeIsRun(CORINFO_METHOD_HANDLE method)
    {
        wrapComp.CLR_API_Enter(API_methodMustBeLoadedBeforeCodeIsRun);
        wrapHnd->methodMustBeLoadedBeforeCodeIsRun(method);
        wrapComp.CLR_API_Leave(API_methodMustBeLoadedBeforeCodeIsRun);
    }

    public void getGSCookie(GSCookie* pCookieVal, GSCookie** ppCookieVal)
    {
        wrapComp.CLR_API_Enter(API_getGSCookie);
        wrapHnd->getGSCookie(pCookieVal, ppCookieVal);
        wrapComp.CLR_API_Leave(API_getGSCookie);
    }

    public void setPatchpointInfo(PatchpointInfo* patchpointInfo)
    {
        wrapComp.CLR_API_Enter(API_setPatchpointInfo);
        wrapHnd->setPatchpointInfo(patchpointInfo);
        wrapComp.CLR_API_Leave(API_setPatchpointInfo);
    }

    public PatchpointInfo* getOSRInfo(int* ilOffset)
    {
        wrapComp.CLR_API_Enter(API_getOSRInfo);
        PatchpointInfo* temp = wrapHnd->getOSRInfo(ilOffset);
        wrapComp.CLR_API_Leave(API_getOSRInfo);

        return temp;
    }

    public void resolveToken(CORINFO_RESOLVED_TOKEN* pResolvedToken)
    {
        wrapComp.CLR_API_Enter(API_resolveToken);
        wrapHnd->resolveToken(pResolvedToken);
        wrapComp.CLR_API_Leave(API_resolveToken);
    }

    public void findSig(CORINFO_MODULE_HANDLE module, int sigTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig)
    {
        wrapComp.CLR_API_Enter(API_findSig);
        wrapHnd->findSig(module, sigTOK, context, sig);
        wrapComp.CLR_API_Leave(API_findSig);
    }

    public void findCallSiteSig(CORINFO_MODULE_HANDLE module, int methTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig)
    {
        wrapComp.CLR_API_Enter(API_findCallSiteSig);
        wrapHnd->findCallSiteSig(module, methTOK, context, sig);
        wrapComp.CLR_API_Leave(API_findCallSiteSig);
    }

    public CORINFO_CLASS_HANDLE getTokenTypeAsHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken)
    {
        wrapComp.CLR_API_Enter(API_getTokenTypeAsHandle);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getTokenTypeAsHandle(pResolvedToken);
        wrapComp.CLR_API_Leave(API_getTokenTypeAsHandle);

        return temp;
    }

    public int getStringLiteral(CORINFO_MODULE_HANDLE module, int metaTOK, char* buffer, int bufferSize, int startIndex = 0)
    {
        wrapComp.CLR_API_Enter(API_getStringLiteral);
        int temp = wrapHnd->getStringLiteral(module, metaTOK, buffer, bufferSize, startIndex);
        wrapComp.CLR_API_Leave(API_getStringLiteral);

        return temp;
    }

    public nint printObjectDescription(CORINFO_OBJECT_HANDLE handle, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null)
    {
        wrapComp.CLR_API_Enter(API_printObjectDescription);
        nint temp = wrapHnd->printObjectDescription(handle, buffer, bufferSize, pRequiredBufferSize);
        wrapComp.CLR_API_Leave(API_printObjectDescription);

        return temp;
    }

    public CorInfoType asCorInfoType(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_asCorInfoType);
        CorInfoType temp = wrapHnd->asCorInfoType(cls);
        wrapComp.CLR_API_Leave(API_asCorInfoType);

        return temp;
    }

    public byte* getClassNameFromMetadata(CORINFO_CLASS_HANDLE cls, byte** namespaceName)
    {
        wrapComp.CLR_API_Enter(API_getClassNameFromMetadata);
        byte* temp = wrapHnd->getClassNameFromMetadata(cls, namespaceName);
        wrapComp.CLR_API_Leave(API_getClassNameFromMetadata);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getTypeInstantiationArgument(CORINFO_CLASS_HANDLE cls, int index)
    {
        wrapComp.CLR_API_Enter(API_getTypeInstantiationArgument);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getTypeInstantiationArgument(cls, index);
        wrapComp.CLR_API_Leave(API_getTypeInstantiationArgument);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getMethodInstantiationArgument(CORINFO_METHOD_HANDLE ftn, int index)
    {
        wrapComp.CLR_API_Enter(API_getMethodInstantiationArgument);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getMethodInstantiationArgument(ftn, index);
        wrapComp.CLR_API_Leave(API_getMethodInstantiationArgument);

        return temp;
    }

    public nint printClassName(CORINFO_CLASS_HANDLE cls, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null)
    {
        wrapComp.CLR_API_Enter(API_printClassName);
        nint temp = wrapHnd->printClassName(cls, buffer, bufferSize, pRequiredBufferSize);
        wrapComp.CLR_API_Leave(API_printClassName);

        return temp;
    }

    public bool isValueClass(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_isValueClass);
        bool temp = wrapHnd->isValueClass(cls);
        wrapComp.CLR_API_Leave(API_isValueClass);

        return temp;
    }

    public CorInfoFlag getClassAttribs(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getClassAttribs);
        CorInfoFlag temp = wrapHnd->getClassAttribs(cls);
        wrapComp.CLR_API_Leave(API_getClassAttribs);

        return temp;
    }

    public byte* getClassAssemblyName(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getClassAssemblyName);
        byte* temp = wrapHnd->getClassAssemblyName(cls);
        wrapComp.CLR_API_Leave(API_getClassAssemblyName);

        return temp;
    }

    public void* LongLifetimeMalloc(nint sz)
    {
        wrapComp.CLR_API_Enter(API_LongLifetimeMalloc);
        void* temp = wrapHnd->LongLifetimeMalloc(sz);
        wrapComp.CLR_API_Leave(API_LongLifetimeMalloc);

        return temp;
    }

    public void LongLifetimeFree(void* obj)
    {
        wrapComp.CLR_API_Enter(API_LongLifetimeFree);
        wrapHnd->LongLifetimeFree(obj);
        wrapComp.CLR_API_Leave(API_LongLifetimeFree);
    }

    public bool getIsClassInitedFlagAddress(CORINFO_CLASS_HANDLE cls, CORINFO_CONST_LOOKUP* addr, int* offset)
    {
        wrapComp.CLR_API_Enter(API_getIsClassInitedFlagAddress);
        bool temp = wrapHnd->getIsClassInitedFlagAddress(cls, addr, offset);
        wrapComp.CLR_API_Leave(API_getIsClassInitedFlagAddress);

        return temp;
    }

    public void* getClassThreadStaticDynamicInfo(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getClassThreadStaticDynamicInfo);
        void* temp = wrapHnd->getClassThreadStaticDynamicInfo(cls);
        wrapComp.CLR_API_Leave(API_getClassThreadStaticDynamicInfo);

        return temp;
    }

    public void* getClassStaticDynamicInfo(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getClassStaticDynamicInfo);
        void* temp = wrapHnd->getClassStaticDynamicInfo(cls);
        wrapComp.CLR_API_Leave(API_getClassStaticDynamicInfo);

        return temp;
    }

    public bool getStaticBaseAddress(CORINFO_CLASS_HANDLE cls, bool isGc, CORINFO_CONST_LOOKUP* addr)
    {
        wrapComp.CLR_API_Enter(API_getStaticBaseAddress);
        bool temp = wrapHnd->getStaticBaseAddress(cls, isGc, addr);
        wrapComp.CLR_API_Leave(API_getStaticBaseAddress);

        return temp;
    }

    public int getClassSize(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getClassSize);
        int temp = wrapHnd->getClassSize(cls);
        wrapComp.CLR_API_Leave(API_getClassSize);

        return temp;
    }

    public int getHeapClassSize(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getHeapClassSize);
        int temp = wrapHnd->getHeapClassSize(cls);
        wrapComp.CLR_API_Leave(API_getHeapClassSize);

        return temp;
    }

    public bool canAllocateOnStack(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_canAllocateOnStack);
        bool temp = wrapHnd->canAllocateOnStack(cls);
        wrapComp.CLR_API_Leave(API_canAllocateOnStack);

        return temp;
    }

    public int getClassAlignmentRequirement(CORINFO_CLASS_HANDLE cls, bool fDoubleAlignHint = false)
    {
        wrapComp.CLR_API_Enter(API_getClassAlignmentRequirement);
        int temp = wrapHnd->getClassAlignmentRequirement(cls, fDoubleAlignHint);
        wrapComp.CLR_API_Leave(API_getClassAlignmentRequirement);

        return temp;
    }

    public int getClassGClayout(CORINFO_CLASS_HANDLE cls, CorInfoGCType* gcPtrs)
    {
        wrapComp.CLR_API_Enter(API_getClassGClayout);
        int temp = wrapHnd->getClassGClayout(cls, gcPtrs);
        wrapComp.CLR_API_Leave(API_getClassGClayout);

        return temp;
    }

    public int getClassNumInstanceFields(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getClassNumInstanceFields);
        int temp = wrapHnd->getClassNumInstanceFields(cls);
        wrapComp.CLR_API_Leave(API_getClassNumInstanceFields);

        return temp;
    }

    public CORINFO_FIELD_HANDLE getFieldInClass(CORINFO_CLASS_HANDLE clsHnd, int num)
    {
        wrapComp.CLR_API_Enter(API_getFieldInClass);
        CORINFO_FIELD_HANDLE temp = wrapHnd->getFieldInClass(clsHnd, num);
        wrapComp.CLR_API_Leave(API_getFieldInClass);

        return temp;
    }

    public GetTypeLayoutResult getTypeLayout(CORINFO_CLASS_HANDLE typeHnd, CORINFO_TYPE_LAYOUT_NODE* treeNodes, nint* numTreeNodes)
    {
        wrapComp.CLR_API_Enter(API_getTypeLayout);
        GetTypeLayoutResult temp = wrapHnd->getTypeLayout(typeHnd, treeNodes, numTreeNodes);
        wrapComp.CLR_API_Leave(API_getTypeLayout);

        return temp;
    }

    public bool checkMethodModifier(CORINFO_METHOD_HANDLE hMethod, byte* modifier, bool fOptional)
    {
        wrapComp.CLR_API_Enter(API_checkMethodModifier);
        bool temp = wrapHnd->checkMethodModifier(hMethod, modifier, fOptional);
        wrapComp.CLR_API_Leave(API_checkMethodModifier);

        return temp;
    }

    public CorInfoHelpFunc getNewHelper(CORINFO_CLASS_HANDLE classHandle, bool* pHasSideEffects)
    {
        wrapComp.CLR_API_Enter(API_getNewHelper);
        CorInfoHelpFunc temp = wrapHnd->getNewHelper(classHandle, pHasSideEffects);
        wrapComp.CLR_API_Leave(API_getNewHelper);

        return temp;
    }

    public CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_HANDLE arrayCls)
    {
        wrapComp.CLR_API_Enter(API_getNewArrHelper);
        CorInfoHelpFunc temp = wrapHnd->getNewArrHelper(arrayCls);
        wrapComp.CLR_API_Leave(API_getNewArrHelper);

        return temp;
    }

    public CorInfoHelpFunc getCastingHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fThrowing)
    {
        wrapComp.CLR_API_Enter(API_getCastingHelper);
        CorInfoHelpFunc temp = wrapHnd->getCastingHelper(pResolvedToken, fThrowing);
        wrapComp.CLR_API_Leave(API_getCastingHelper);

        return temp;
    }

    public CorInfoHelpFunc getSharedCCtorHelper(CORINFO_CLASS_HANDLE clsHnd)
    {
        wrapComp.CLR_API_Enter(API_getSharedCCtorHelper);
        CorInfoHelpFunc temp = wrapHnd->getSharedCCtorHelper(clsHnd);
        wrapComp.CLR_API_Leave(API_getSharedCCtorHelper);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getTypeForBox(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getTypeForBox);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getTypeForBox(cls);
        wrapComp.CLR_API_Leave(API_getTypeForBox);

        return temp;
    }

    public CorInfoHelpFunc getBoxHelper(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getBoxHelper);
        CorInfoHelpFunc temp = wrapHnd->getBoxHelper(cls);
        wrapComp.CLR_API_Leave(API_getBoxHelper);

        return temp;
    }

    public CorInfoHelpFunc getUnBoxHelper(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getUnBoxHelper);
        CorInfoHelpFunc temp = wrapHnd->getUnBoxHelper(cls);
        wrapComp.CLR_API_Leave(API_getUnBoxHelper);

        return temp;
    }

    public CORINFO_OBJECT_HANDLE getRuntimeTypePointer(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getRuntimeTypePointer);
        CORINFO_OBJECT_HANDLE temp = wrapHnd->getRuntimeTypePointer(cls);
        wrapComp.CLR_API_Leave(API_getRuntimeTypePointer);

        return temp;
    }

    public bool isObjectImmutable(CORINFO_OBJECT_HANDLE objPtr)
    {
        wrapComp.CLR_API_Enter(API_isObjectImmutable);
        bool temp = wrapHnd->isObjectImmutable(objPtr);
        wrapComp.CLR_API_Leave(API_isObjectImmutable);

        return temp;
    }

    public bool getStringChar(CORINFO_OBJECT_HANDLE strObj, int index, ushort* value)
    {
        wrapComp.CLR_API_Enter(API_getStringChar);
        bool temp = wrapHnd->getStringChar(strObj, index, value);
        wrapComp.CLR_API_Leave(API_getStringChar);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getObjectType(CORINFO_OBJECT_HANDLE objPtr)
    {
        wrapComp.CLR_API_Enter(API_getObjectType);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getObjectType(objPtr);
        wrapComp.CLR_API_Leave(API_getObjectType);

        return temp;
    }

    public bool getReadyToRunHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, CorInfoHelpFunc id, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CONST_LOOKUP* pLookup)
    {
        wrapComp.CLR_API_Enter(API_getReadyToRunHelper);
        bool temp = wrapHnd->getReadyToRunHelper(pResolvedToken, id, callerHandle, pLookup);
        wrapComp.CLR_API_Leave(API_getReadyToRunHelper);

        return temp;
    }

    public void getReadyToRunDelegateCtorHelper(CORINFO_RESOLVED_TOKEN* pTargetMethod, mdToken targetConstraint, CORINFO_CLASS_HANDLE delegateType, CORINFO_METHOD_HANDLE callerHandler, CORINFO_LOOKUP* pLookup)
    {
        wrapComp.CLR_API_Enter(API_getReadyToRunDelegateCtorHelper);
        wrapHnd->getReadyToRunDelegateCtorHelper(pTargetMethod, targetConstraint, delegateType, callerHandler, pLookup);
        wrapComp.CLR_API_Leave(API_getReadyToRunDelegateCtorHelper);
    }

    public CorInfoInitClassResult initClass(CORINFO_FIELD_HANDLE field, CORINFO_METHOD_HANDLE method, CORINFO_CONTEXT_HANDLE context)
    {
        wrapComp.CLR_API_Enter(API_initClass);
        CorInfoInitClassResult temp = wrapHnd->initClass(field, method, context);
        wrapComp.CLR_API_Leave(API_initClass);

        return temp;
    }

    public void classMustBeLoadedBeforeCodeIsRun(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_classMustBeLoadedBeforeCodeIsRun);
        wrapHnd->classMustBeLoadedBeforeCodeIsRun(cls);
        wrapComp.CLR_API_Leave(API_classMustBeLoadedBeforeCodeIsRun);
    }

    public CORINFO_CLASS_HANDLE getBuiltinClass(CorInfoClassId classId)
    {
        wrapComp.CLR_API_Enter(API_getBuiltinClass);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getBuiltinClass(classId);
        wrapComp.CLR_API_Leave(API_getBuiltinClass);

        return temp;
    }

    public CorInfoType getTypeForPrimitiveValueClass(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getTypeForPrimitiveValueClass);
        CorInfoType temp = wrapHnd->getTypeForPrimitiveValueClass(cls);
        wrapComp.CLR_API_Leave(API_getTypeForPrimitiveValueClass);

        return temp;
    }

    public CorInfoType getTypeForPrimitiveNumericClass(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getTypeForPrimitiveNumericClass);
        CorInfoType temp = wrapHnd->getTypeForPrimitiveNumericClass(cls);
        wrapComp.CLR_API_Leave(API_getTypeForPrimitiveNumericClass);

        return temp;
    }

    public bool canCast(CORINFO_CLASS_HANDLE child, CORINFO_CLASS_HANDLE parent)
    {
        wrapComp.CLR_API_Enter(API_canCast);
        bool temp = wrapHnd->canCast(child, parent);
        wrapComp.CLR_API_Leave(API_canCast);

        return temp;
    }

    public TypeCompareState compareTypesForCast(CORINFO_CLASS_HANDLE fromClass, CORINFO_CLASS_HANDLE toClass)
    {
        wrapComp.CLR_API_Enter(API_compareTypesForCast);
        TypeCompareState temp = wrapHnd->compareTypesForCast(fromClass, toClass);
        wrapComp.CLR_API_Leave(API_compareTypesForCast);

        return temp;
    }

    public TypeCompareState compareTypesForEquality(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2)
    {
        wrapComp.CLR_API_Enter(API_compareTypesForEquality);
        TypeCompareState temp = wrapHnd->compareTypesForEquality(cls1, cls2);
        wrapComp.CLR_API_Leave(API_compareTypesForEquality);

        return temp;
    }

    public bool isMoreSpecificType(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2)
    {
        wrapComp.CLR_API_Enter(API_isMoreSpecificType);
        bool temp = wrapHnd->isMoreSpecificType(cls1, cls2);
        wrapComp.CLR_API_Leave(API_isMoreSpecificType);

        return temp;
    }

    public bool isExactType(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_isExactType);
        bool temp = wrapHnd->isExactType(cls);
        wrapComp.CLR_API_Leave(API_isExactType);

        return temp;
    }

    public TypeCompareState isGenericType(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_isGenericType);
        TypeCompareState temp = wrapHnd->isGenericType(cls);
        wrapComp.CLR_API_Leave(API_isGenericType);

        return temp;
    }

    public TypeCompareState isNullableType(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_isNullableType);
        TypeCompareState temp = wrapHnd->isNullableType(cls);
        wrapComp.CLR_API_Leave(API_isNullableType);

        return temp;
    }

    public TypeCompareState isEnum(CORINFO_CLASS_HANDLE cls, CORINFO_CLASS_HANDLE* underlyingType)
    {
        wrapComp.CLR_API_Enter(API_isEnum);
        TypeCompareState temp = wrapHnd->isEnum(cls, underlyingType);
        wrapComp.CLR_API_Leave(API_isEnum);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getParentType(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getParentType);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getParentType(cls);
        wrapComp.CLR_API_Leave(API_getParentType);

        return temp;
    }

    public CorInfoType getChildType(CORINFO_CLASS_HANDLE clsHnd, CORINFO_CLASS_HANDLE* clsRet)
    {
        wrapComp.CLR_API_Enter(API_getChildType);
        CorInfoType temp = wrapHnd->getChildType(clsHnd, clsRet);
        wrapComp.CLR_API_Leave(API_getChildType);

        return temp;
    }

    public bool isSDArray(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_isSDArray);
        bool temp = wrapHnd->isSDArray(cls);
        wrapComp.CLR_API_Leave(API_isSDArray);

        return temp;
    }

    public int getArrayRank(CORINFO_CLASS_HANDLE cls)
    {
        wrapComp.CLR_API_Enter(API_getArrayRank);
        int temp = wrapHnd->getArrayRank(cls);
        wrapComp.CLR_API_Leave(API_getArrayRank);

        return temp;
    }

    public CorInfoArrayIntrinsic getArrayIntrinsicID(CORINFO_METHOD_HANDLE ftn)
    {
        wrapComp.CLR_API_Enter(API_getArrayIntrinsicID);
        CorInfoArrayIntrinsic temp = wrapHnd->getArrayIntrinsicID(ftn);
        wrapComp.CLR_API_Leave(API_getArrayIntrinsicID);

        return temp;
    }

    public void* getArrayInitializationData(CORINFO_FIELD_HANDLE field, int size)
    {
        wrapComp.CLR_API_Enter(API_getArrayInitializationData);
        void* temp = wrapHnd->getArrayInitializationData(field, size);
        wrapComp.CLR_API_Leave(API_getArrayInitializationData);

        return temp;
    }

    public CorInfoIsAccessAllowedResult canAccessClass(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_HELPER_DESC* pAccessHelper)
    {
        wrapComp.CLR_API_Enter(API_canAccessClass);
        CorInfoIsAccessAllowedResult temp = wrapHnd->canAccessClass(pResolvedToken, callerHandle, pAccessHelper);
        wrapComp.CLR_API_Leave(API_canAccessClass);

        return temp;
    }

    public nint printFieldName(CORINFO_FIELD_HANDLE field, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null)
    {
        wrapComp.CLR_API_Enter(API_printFieldName);
        nint temp = wrapHnd->printFieldName(field, buffer, bufferSize, pRequiredBufferSize);
        wrapComp.CLR_API_Leave(API_printFieldName);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getFieldClass(CORINFO_FIELD_HANDLE field)
    {
        wrapComp.CLR_API_Enter(API_getFieldClass);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getFieldClass(field);
        wrapComp.CLR_API_Leave(API_getFieldClass);

        return temp;
    }

    public CorInfoType getFieldType(CORINFO_FIELD_HANDLE field, CORINFO_CLASS_HANDLE* structType = null, CORINFO_CLASS_HANDLE memberParent = null)
    {
        wrapComp.CLR_API_Enter(API_getFieldType);
        CorInfoType temp = wrapHnd->getFieldType(field, structType, memberParent);
        wrapComp.CLR_API_Leave(API_getFieldType);

        return temp;
    }

    public int getFieldOffset(CORINFO_FIELD_HANDLE field)
    {
        wrapComp.CLR_API_Enter(API_getFieldOffset);
        int temp = wrapHnd->getFieldOffset(field);
        wrapComp.CLR_API_Leave(API_getFieldOffset);

        return temp;
    }

    public void getFieldInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_ACCESS_FLAGS flags, CORINFO_FIELD_INFO* pResult)
    {
        wrapComp.CLR_API_Enter(API_getFieldInfo);
        wrapHnd->getFieldInfo(pResolvedToken, callerHandle, flags, pResult);
        wrapComp.CLR_API_Leave(API_getFieldInfo);
    }

    public int getThreadLocalFieldInfo(CORINFO_FIELD_HANDLE field, bool isGCType)
    {
        wrapComp.CLR_API_Enter(API_getThreadLocalFieldInfo);
        int temp = wrapHnd->getThreadLocalFieldInfo(field, isGCType);
        wrapComp.CLR_API_Leave(API_getThreadLocalFieldInfo);

        return temp;
    }

    public void getThreadLocalStaticBlocksInfo(CORINFO_THREAD_STATIC_BLOCKS_INFO* pInfo)
    {
        wrapComp.CLR_API_Enter(API_getThreadLocalStaticBlocksInfo);
        wrapHnd->getThreadLocalStaticBlocksInfo(pInfo);
        wrapComp.CLR_API_Leave(API_getThreadLocalStaticBlocksInfo);
    }

    public void getThreadLocalStaticInfo_NativeAOT(CORINFO_THREAD_STATIC_INFO_NATIVEAOT* pInfo)
    {
        wrapComp.CLR_API_Enter(API_getThreadLocalStaticInfo_NativeAOT);
        wrapHnd->getThreadLocalStaticInfo_NativeAOT(pInfo);
        wrapComp.CLR_API_Leave(API_getThreadLocalStaticInfo_NativeAOT);
    }

    public bool isFieldStatic(CORINFO_FIELD_HANDLE fldHnd)
    {
        wrapComp.CLR_API_Enter(API_isFieldStatic);
        bool temp = wrapHnd->isFieldStatic(fldHnd);
        wrapComp.CLR_API_Leave(API_isFieldStatic);

        return temp;
    }

    public int getArrayOrStringLength(CORINFO_OBJECT_HANDLE objHnd)
    {
        wrapComp.CLR_API_Enter(API_getArrayOrStringLength);
        int temp = wrapHnd->getArrayOrStringLength(objHnd);
        wrapComp.CLR_API_Leave(API_getArrayOrStringLength);

        return temp;
    }

    public void getBoundaries(CORINFO_METHOD_HANDLE ftn, int* cILOffsets, int** pILOffsets, ICorDebugInfo.BoundaryTypes* implicitBoundaries)
    {
        wrapComp.CLR_API_Enter(API_getBoundaries);
        wrapHnd->getBoundaries(ftn, cILOffsets, pILOffsets, implicitBoundaries);
        wrapComp.CLR_API_Leave(API_getBoundaries);
    }

    public void setBoundaries(CORINFO_METHOD_HANDLE ftn, int cMap, ICorDebugInfo.OffsetMapping* pMap)
    {
        wrapComp.CLR_API_Enter(API_setBoundaries);
        wrapHnd->setBoundaries(ftn, cMap, pMap);
        wrapComp.CLR_API_Leave(API_setBoundaries);
    }

    public void getVars(CORINFO_METHOD_HANDLE ftn, int* cVars, ICorDebugInfo.ILVarInfo** vars, bool* extendOthers)
    {
        wrapComp.CLR_API_Enter(API_getVars);
        wrapHnd->getVars(ftn, cVars, vars, extendOthers);
        wrapComp.CLR_API_Leave(API_getVars);
    }

    public void setVars(CORINFO_METHOD_HANDLE ftn, int cVars, ICorDebugInfo.NativeVarInfo* vars)
    {
        wrapComp.CLR_API_Enter(API_setVars);
        wrapHnd->setVars(ftn, cVars, vars);
        wrapComp.CLR_API_Leave(API_setVars);
    }

    public void reportRichMappings(ICorDebugInfo.InlineTreeNode* inlineTreeNodes, int numInlineTreeNodes, ICorDebugInfo.RichOffsetMapping* mappings, int numMappings)
    {
        wrapComp.CLR_API_Enter(API_reportRichMappings);
        wrapHnd->reportRichMappings(inlineTreeNodes, numInlineTreeNodes, mappings, numMappings);
        wrapComp.CLR_API_Leave(API_reportRichMappings);
    }

    public void reportAsyncDebugInfo(ICorDebugInfo.AsyncInfo* asyncInfo, ICorDebugInfo.AsyncSuspensionPoint* suspensionPoints, ICorDebugInfo.AsyncContinuationVarInfo* vars, int numVars)
    {
        wrapComp.CLR_API_Enter(API_reportAsyncDebugInfo);
        wrapHnd->reportAsyncDebugInfo(asyncInfo, suspensionPoints, vars, numVars);
        wrapComp.CLR_API_Leave(API_reportAsyncDebugInfo);
    }

    public void reportMetadata(byte* key, void* value, nint length)
    {
        wrapComp.CLR_API_Enter(API_reportMetadata);
        wrapHnd->reportMetadata(key, value, length);
        wrapComp.CLR_API_Leave(API_reportMetadata);
    }

    public void* allocateArray(nint cBytes)
    {
        wrapComp.CLR_API_Enter(API_allocateArray);
        void* temp = wrapHnd->allocateArray(cBytes);
        wrapComp.CLR_API_Leave(API_allocateArray);

        return temp;
    }

    public void freeArray(void* array)
    {
        wrapComp.CLR_API_Enter(API_freeArray);
        wrapHnd->freeArray(array);
        wrapComp.CLR_API_Leave(API_freeArray);
    }

    public CORINFO_ARG_LIST_HANDLE getArgNext(CORINFO_ARG_LIST_HANDLE args)
    {
        wrapComp.CLR_API_Enter(API_getArgNext);
        CORINFO_ARG_LIST_HANDLE temp = wrapHnd->getArgNext(args);
        wrapComp.CLR_API_Leave(API_getArgNext);

        return temp;
    }

    public CorInfoTypeWithMod getArgType(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args, CORINFO_CLASS_HANDLE* vcTypeRet)
    {
        wrapComp.CLR_API_Enter(API_getArgType);
        CorInfoTypeWithMod temp = wrapHnd->getArgType(sig, args, vcTypeRet);
        wrapComp.CLR_API_Leave(API_getArgType);

        return temp;
    }

    public int getExactClasses(CORINFO_CLASS_HANDLE baseType, int maxExactClasses, CORINFO_CLASS_HANDLE* exactClsRet)
    {
        wrapComp.CLR_API_Enter(API_getExactClasses);
        int temp = wrapHnd->getExactClasses(baseType, maxExactClasses, exactClsRet);
        wrapComp.CLR_API_Leave(API_getExactClasses);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getArgClass(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args)
    {
        wrapComp.CLR_API_Enter(API_getArgClass);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getArgClass(sig, args);
        wrapComp.CLR_API_Leave(API_getArgClass);

        return temp;
    }

    public CorInfoHFAElemType getHFAType(CORINFO_CLASS_HANDLE hClass)
    {
        wrapComp.CLR_API_Enter(API_getHFAType);
        CorInfoHFAElemType temp = wrapHnd->getHFAType(hClass);
        wrapComp.CLR_API_Leave(API_getHFAType);

        return temp;
    }

    public bool runWithErrorTrap(errorTrapFunction function, void* parameter)
    {
        wrapComp.CLR_API_Enter(API_runWithErrorTrap);
        bool temp = wrapHnd->runWithErrorTrap(function, parameter);
        wrapComp.CLR_API_Leave(API_runWithErrorTrap);

        return temp;
    }

    public bool runWithSPMIErrorTrap(errorTrapFunction function, void* parameter)
    {
        wrapComp.CLR_API_Enter(API_runWithSPMIErrorTrap);
        bool temp = wrapHnd->runWithSPMIErrorTrap(function, parameter);
        wrapComp.CLR_API_Leave(API_runWithSPMIErrorTrap);

        return temp;
    }

    public void getEEInfo(CORINFO_EE_INFO* pEEInfoOut)
    {
        wrapComp.CLR_API_Enter(API_getEEInfo);
        wrapHnd->getEEInfo(pEEInfoOut);
        wrapComp.CLR_API_Leave(API_getEEInfo);
    }

    public void getAsyncInfo(CORINFO_ASYNC_INFO* pAsyncInfoOut)
    {
        wrapComp.CLR_API_Enter(API_getAsyncInfo);
        wrapHnd->getAsyncInfo(pAsyncInfoOut);
        wrapComp.CLR_API_Leave(API_getAsyncInfo);
    }

    public CORINFO_METHOD_HANDLE getAwaitReturnCall(CORINFO_METHOD_HANDLE callerHandle, CORINFO_CONTEXT_HANDLE* contextHandle, CORINFO_LOOKUP* instArg)
    {
        wrapComp.CLR_API_Enter(API_getAwaitReturnCall);
        CORINFO_METHOD_HANDLE temp = wrapHnd->getAwaitReturnCall(callerHandle, contextHandle, instArg);
        wrapComp.CLR_API_Leave(API_getAwaitReturnCall);

        return temp;
    }

    public CORINFO_METHOD_HANDLE getAwaitAwaiterInContinuationCall(CORINFO_METHOD_HANDLE callerHandle, CORINFO_RESOLVED_TOKEN* pResolvedToken, bool isUnsafe, CORINFO_CONTEXT_HANDLE* contextHandle, CORINFO_LOOKUP* instArg)
    {
        wrapComp.CLR_API_Enter(API_getAwaitAwaiterInContinuationCall);
        CORINFO_METHOD_HANDLE temp = wrapHnd->getAwaitAwaiterInContinuationCall(callerHandle, pResolvedToken, isUnsafe, contextHandle, instArg);
        wrapComp.CLR_API_Leave(API_getAwaitAwaiterInContinuationCall);

        return temp;
    }

    public mdMethodDef getMethodDefFromMethod(CORINFO_METHOD_HANDLE hMethod)
    {
        wrapComp.CLR_API_Enter(API_getMethodDefFromMethod);
        mdMethodDef temp = wrapHnd->getMethodDefFromMethod(hMethod);
        wrapComp.CLR_API_Leave(API_getMethodDefFromMethod);

        return temp;
    }

    public nint printMethodName(CORINFO_METHOD_HANDLE ftn, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null)
    {
        wrapComp.CLR_API_Enter(API_printMethodName);
        nint temp = wrapHnd->printMethodName(ftn, buffer, bufferSize, pRequiredBufferSize);
        wrapComp.CLR_API_Leave(API_printMethodName);

        return temp;
    }

    public byte* getMethodNameFromMetadata(CORINFO_METHOD_HANDLE ftn, byte** className, byte** namespaceName, byte** enclosingClassName, nint maxEnclosingClassNames)
    {
        wrapComp.CLR_API_Enter(API_getMethodNameFromMetadata);
        byte* temp = wrapHnd->getMethodNameFromMetadata(ftn, className, namespaceName, enclosingClassName, maxEnclosingClassNames);
        wrapComp.CLR_API_Leave(API_getMethodNameFromMetadata);

        return temp;
    }

    public int getMethodHash(CORINFO_METHOD_HANDLE ftn)
    {
        wrapComp.CLR_API_Enter(API_getMethodHash);
        int temp = wrapHnd->getMethodHash(ftn);
        wrapComp.CLR_API_Leave(API_getMethodHash);

        return temp;
    }

    public bool getSystemVAmd64PassStructInRegisterDescriptor(CORINFO_CLASS_HANDLE structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr)
    {
        wrapComp.CLR_API_Enter(API_getSystemVAmd64PassStructInRegisterDescriptor);
        bool temp = wrapHnd->getSystemVAmd64PassStructInRegisterDescriptor(structHnd, structPassInRegDescPtr);
        wrapComp.CLR_API_Leave(API_getSystemVAmd64PassStructInRegisterDescriptor);

        return temp;
    }

    public void getSwiftLowering(CORINFO_CLASS_HANDLE structHnd, CORINFO_SWIFT_LOWERING* pLowering)
    {
        wrapComp.CLR_API_Enter(API_getSwiftLowering);
        wrapHnd->getSwiftLowering(structHnd, pLowering);
        wrapComp.CLR_API_Leave(API_getSwiftLowering);
    }

    public void getFpStructLowering(CORINFO_CLASS_HANDLE structHnd, CORINFO_FPSTRUCT_LOWERING* pLowering)
    {
        wrapComp.CLR_API_Enter(API_getFpStructLowering);
        wrapHnd->getFpStructLowering(structHnd, pLowering);
        wrapComp.CLR_API_Leave(API_getFpStructLowering);
    }

    public CorInfoWasmType getWasmLowering(CORINFO_CLASS_HANDLE structHnd)
    {
        wrapComp.CLR_API_Enter(API_getWasmLowering);
        CorInfoWasmType temp = wrapHnd->getWasmLowering(structHnd);
        wrapComp.CLR_API_Leave(API_getWasmLowering);

        return temp;
    }

    public uint getAddressAlignment(void* address)
    {
        wrapComp.CLR_API_Enter(API_getAddressAlignment);
        uint temp = wrapHnd->getAddressAlignment(address);
        wrapComp.CLR_API_Leave(API_getAddressAlignment);

        return temp;
    }

    public void getWasmWellKnownGlobals(CORINFO_WASM_WELLKNOWN_GLOBALS* pWellKnownGlobalsOut)
    {
        wrapComp.CLR_API_Enter(API_getWasmWellKnownGlobals);
        wrapHnd->getWasmWellKnownGlobals(pWellKnownGlobalsOut);
        wrapComp.CLR_API_Leave(API_getWasmWellKnownGlobals);
    }

    public int getThreadTLSIndex(void** ppIndirection = null)
    {
        wrapComp.CLR_API_Enter(API_getThreadTLSIndex);
        int temp = wrapHnd->getThreadTLSIndex(ppIndirection);
        wrapComp.CLR_API_Leave(API_getThreadTLSIndex);

        return temp;
    }

    public int* getAddrOfCaptureThreadGlobal(void** ppIndirection = null)
    {
        wrapComp.CLR_API_Enter(API_getAddrOfCaptureThreadGlobal);
        int* temp = wrapHnd->getAddrOfCaptureThreadGlobal(ppIndirection);
        wrapComp.CLR_API_Leave(API_getAddrOfCaptureThreadGlobal);

        return temp;
    }

    public void* getHelperFtn(CorInfoHelpFunc ftnNum, CORINFO_CONST_LOOKUP* pNativeEntrypoint, CORINFO_METHOD_HANDLE* pMethodHandle = null)
    {
        wrapComp.CLR_API_Enter(API_getHelperFtn);
        void* temp = wrapHnd->getHelperFtn(ftnNum, pNativeEntrypoint, pMethodHandle);
        wrapComp.CLR_API_Leave(API_getHelperFtn);

        return temp;
    }

    public void getFunctionEntryPoint(CORINFO_METHOD_HANDLE ftn, CORINFO_CONST_LOOKUP* pResult, CORINFO_ACCESS_FLAGS accessFlags = CORINFO_ACCESS_ANY)
    {
        wrapComp.CLR_API_Enter(API_getFunctionEntryPoint);
        wrapHnd->getFunctionEntryPoint(ftn, pResult, accessFlags);
        wrapComp.CLR_API_Leave(API_getFunctionEntryPoint);
    }

    public void getFunctionFixedEntryPoint(CORINFO_METHOD_HANDLE ftn, bool isUnsafeFunctionPointer, CORINFO_CONST_LOOKUP* pResult)
    {
        wrapComp.CLR_API_Enter(API_getFunctionFixedEntryPoint);
        wrapHnd->getFunctionFixedEntryPoint(ftn, isUnsafeFunctionPointer, pResult);
        wrapComp.CLR_API_Leave(API_getFunctionFixedEntryPoint);
    }

    public CORINFO_MODULE_HANDLE embedModuleHandle(CORINFO_MODULE_HANDLE handle, void** ppIndirection = null)
    {
        wrapComp.CLR_API_Enter(API_embedModuleHandle);
        CORINFO_MODULE_HANDLE temp = wrapHnd->embedModuleHandle(handle, ppIndirection);
        wrapComp.CLR_API_Leave(API_embedModuleHandle);

        return temp;
    }

    public CORINFO_CLASS_HANDLE embedClassHandle(CORINFO_CLASS_HANDLE handle, void** ppIndirection = null)
    {
        wrapComp.CLR_API_Enter(API_embedClassHandle);
        CORINFO_CLASS_HANDLE temp = wrapHnd->embedClassHandle(handle, ppIndirection);
        wrapComp.CLR_API_Leave(API_embedClassHandle);

        return temp;
    }

    public CORINFO_METHOD_HANDLE embedMethodHandle(CORINFO_METHOD_HANDLE handle, void** ppIndirection = null)
    {
        wrapComp.CLR_API_Enter(API_embedMethodHandle);
        CORINFO_METHOD_HANDLE temp = wrapHnd->embedMethodHandle(handle, ppIndirection);
        wrapComp.CLR_API_Leave(API_embedMethodHandle);

        return temp;
    }

    public CORINFO_FIELD_HANDLE embedFieldHandle(CORINFO_FIELD_HANDLE handle, void** ppIndirection = null)
    {
        wrapComp.CLR_API_Enter(API_embedFieldHandle);
        CORINFO_FIELD_HANDLE temp = wrapHnd->embedFieldHandle(handle, ppIndirection);
        wrapComp.CLR_API_Leave(API_embedFieldHandle);

        return temp;
    }

    public void embedGenericHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fEmbedParent, CORINFO_METHOD_HANDLE callerHandle, CORINFO_GENERICHANDLE_RESULT* pResult)
    {
        wrapComp.CLR_API_Enter(API_embedGenericHandle);
        wrapHnd->embedGenericHandle(pResolvedToken, fEmbedParent, callerHandle, pResult);
        wrapComp.CLR_API_Leave(API_embedGenericHandle);
    }

    public void getLocationOfThisType(CORINFO_METHOD_HANDLE context, CORINFO_LOOKUP_KIND* pLookupKind)
    {
        wrapComp.CLR_API_Enter(API_getLocationOfThisType);
        wrapHnd->getLocationOfThisType(context, pLookupKind);
        wrapComp.CLR_API_Leave(API_getLocationOfThisType);
    }

    public void getAddressOfPInvokeTarget(CORINFO_METHOD_HANDLE method, CORINFO_CONST_LOOKUP* pLookup)
    {
        wrapComp.CLR_API_Enter(API_getAddressOfPInvokeTarget);
        wrapHnd->getAddressOfPInvokeTarget(method, pLookup);
        wrapComp.CLR_API_Leave(API_getAddressOfPInvokeTarget);
    }

    public void* GetCookieForInterpreterCalliSig(CORINFO_SIG_INFO* szMetaSig)
    {
        wrapComp.CLR_API_Enter(API_GetCookieForInterpreterCalliSig);
        void* temp = wrapHnd->GetCookieForInterpreterCalliSig(szMetaSig);
        wrapComp.CLR_API_Leave(API_GetCookieForInterpreterCalliSig);

        return temp;
    }

    public CORINFO_JUST_MY_CODE_HANDLE getJustMyCodeHandle(CORINFO_METHOD_HANDLE method, CORINFO_JUST_MY_CODE_HANDLE** ppIndirection = null)
    {
        wrapComp.CLR_API_Enter(API_getJustMyCodeHandle);
        CORINFO_JUST_MY_CODE_HANDLE temp = wrapHnd->getJustMyCodeHandle(method, ppIndirection);
        wrapComp.CLR_API_Leave(API_getJustMyCodeHandle);

        return temp;
    }

    public void GetProfilingHandle(bool* pbHookFunction, void** pProfilerHandle, bool* pbIndirectedHandles)
    {
        wrapComp.CLR_API_Enter(API_GetProfilingHandle);
        wrapHnd->GetProfilingHandle(pbHookFunction, pProfilerHandle, pbIndirectedHandles);
        wrapComp.CLR_API_Leave(API_GetProfilingHandle);
    }

    public void getCallInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult)
    {
        wrapComp.CLR_API_Enter(API_getCallInfo);
        wrapHnd->getCallInfo(pResolvedToken, pConstrainedResolvedToken, callerHandle, flags, pResult);
        wrapComp.CLR_API_Leave(API_getCallInfo);
    }

    public bool getStaticFieldContent(CORINFO_FIELD_HANDLE field, byte* buffer, int bufferSize, int valueOffset = 0, bool ignoreMovableObjects = true)
    {
        wrapComp.CLR_API_Enter(API_getStaticFieldContent);
        bool temp = wrapHnd->getStaticFieldContent(field, buffer, bufferSize, valueOffset, ignoreMovableObjects);
        wrapComp.CLR_API_Leave(API_getStaticFieldContent);

        return temp;
    }

    public bool getObjectContent(CORINFO_OBJECT_HANDLE obj, byte* buffer, int bufferSize, int valueOffset)
    {
        wrapComp.CLR_API_Enter(API_getObjectContent);
        bool temp = wrapHnd->getObjectContent(obj, buffer, bufferSize, valueOffset);
        wrapComp.CLR_API_Leave(API_getObjectContent);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getStaticFieldCurrentClass(CORINFO_FIELD_HANDLE field, bool* pIsSpeculative = null)
    {
        wrapComp.CLR_API_Enter(API_getStaticFieldCurrentClass);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getStaticFieldCurrentClass(field, pIsSpeculative);
        wrapComp.CLR_API_Leave(API_getStaticFieldCurrentClass);

        return temp;
    }

    public CORINFO_VARARGS_HANDLE getVarArgsHandle(CORINFO_SIG_INFO* pSig, CORINFO_METHOD_HANDLE methHnd, void** ppIndirection = null)
    {
        wrapComp.CLR_API_Enter(API_getVarArgsHandle);
        CORINFO_VARARGS_HANDLE temp = wrapHnd->getVarArgsHandle(pSig, methHnd, ppIndirection);
        wrapComp.CLR_API_Leave(API_getVarArgsHandle);

        return temp;
    }

    public InfoAccessType constructStringLiteral(CORINFO_MODULE_HANDLE module, mdToken metaTok, void** ppValue)
    {
        wrapComp.CLR_API_Enter(API_constructStringLiteral);
        InfoAccessType temp = wrapHnd->constructStringLiteral(module, metaTok, ppValue);
        wrapComp.CLR_API_Leave(API_constructStringLiteral);

        return temp;
    }

    public InfoAccessType emptyStringLiteral(void** ppValue)
    {
        wrapComp.CLR_API_Enter(API_emptyStringLiteral);
        InfoAccessType temp = wrapHnd->emptyStringLiteral(ppValue);
        wrapComp.CLR_API_Leave(API_emptyStringLiteral);

        return temp;
    }

    public int getFieldThreadLocalStoreID(CORINFO_FIELD_HANDLE field, void** ppIndirection = null)
    {
        wrapComp.CLR_API_Enter(API_getFieldThreadLocalStoreID);
        int temp = wrapHnd->getFieldThreadLocalStoreID(field, ppIndirection);
        wrapComp.CLR_API_Leave(API_getFieldThreadLocalStoreID);

        return temp;
    }

    public CORINFO_METHOD_HANDLE GetDelegateCtor(CORINFO_METHOD_HANDLE methHnd, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE targetMethodHnd, DelegateCtorArgs* pCtorData)
    {
        wrapComp.CLR_API_Enter(API_GetDelegateCtor);
        CORINFO_METHOD_HANDLE temp = wrapHnd->GetDelegateCtor(methHnd, clsHnd, targetMethodHnd, pCtorData);
        wrapComp.CLR_API_Leave(API_GetDelegateCtor);

        return temp;
    }

    public void MethodCompileComplete(CORINFO_METHOD_HANDLE methHnd)
    {
        wrapComp.CLR_API_Enter(API_MethodCompileComplete);
        wrapHnd->MethodCompileComplete(methHnd);
        wrapComp.CLR_API_Leave(API_MethodCompileComplete);
    }

    public bool getTailCallHelpers(CORINFO_RESOLVED_TOKEN* callToken, CORINFO_SIG_INFO* sig, CORINFO_GET_TAILCALL_HELPERS_FLAGS flags, CORINFO_TAILCALL_HELPERS* pResult)
    {
        wrapComp.CLR_API_Enter(API_getTailCallHelpers);
        bool temp = wrapHnd->getTailCallHelpers(callToken, sig, flags, pResult);
        wrapComp.CLR_API_Leave(API_getTailCallHelpers);

        return temp;
    }

    public CORINFO_CLASS_HANDLE getContinuationType(nint dataSize, bool* objRefs, nint objRefsSize)
    {
        wrapComp.CLR_API_Enter(API_getContinuationType);
        CORINFO_CLASS_HANDLE temp = wrapHnd->getContinuationType(dataSize, objRefs, objRefsSize);
        wrapComp.CLR_API_Leave(API_getContinuationType);

        return temp;
    }

    public CORINFO_METHOD_HANDLE getAsyncResumptionStub(void** entryPoint)
    {
        wrapComp.CLR_API_Enter(API_getAsyncResumptionStub);
        CORINFO_METHOD_HANDLE temp = wrapHnd->getAsyncResumptionStub(entryPoint);
        wrapComp.CLR_API_Leave(API_getAsyncResumptionStub);

        return temp;
    }

    public bool convertPInvokeCalliToCall(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fMustConvert)
    {
        wrapComp.CLR_API_Enter(API_convertPInvokeCalliToCall);
        bool temp = wrapHnd->convertPInvokeCalliToCall(pResolvedToken, fMustConvert);
        wrapComp.CLR_API_Leave(API_convertPInvokeCalliToCall);

        return temp;
    }

    public bool notifyInstructionSetUsage(CORINFO_InstructionSet instructionSet, bool supportEnabled, bool preserveNegativeDependency)
    {
        wrapComp.CLR_API_Enter(API_notifyInstructionSetUsage);
        bool temp = wrapHnd->notifyInstructionSetUsage(instructionSet, supportEnabled, preserveNegativeDependency);
        wrapComp.CLR_API_Leave(API_notifyInstructionSetUsage);

        return temp;
    }

    public void updateEntryPointForTailCall(CORINFO_CONST_LOOKUP* entryPoint)
    {
        wrapComp.CLR_API_Enter(API_updateEntryPointForTailCall);
        wrapHnd->updateEntryPointForTailCall(entryPoint);
        wrapComp.CLR_API_Leave(API_updateEntryPointForTailCall);
    }

    public void allocMem(AllocMemArgs* pArgs)
    {
        wrapComp.CLR_API_Enter(API_allocMem);
        wrapHnd->allocMem(pArgs);
        wrapComp.CLR_API_Leave(API_allocMem);
    }

    public void reserveUnwindInfo(bool isFunclet, bool isColdCode, int unwindSize)
    {
        wrapComp.CLR_API_Enter(API_reserveUnwindInfo);
        wrapHnd->reserveUnwindInfo(isFunclet, isColdCode, unwindSize);
        wrapComp.CLR_API_Leave(API_reserveUnwindInfo);
    }

    public void allocUnwindInfo(byte* pHotCode, byte* pColdCode, int startOffset, int endOffset, int unwindSize, byte* pUnwindBlock, CorJitFuncKind funcKind)
    {
        wrapComp.CLR_API_Enter(API_allocUnwindInfo);
        wrapHnd->allocUnwindInfo(pHotCode, pColdCode, startOffset, endOffset, unwindSize, pUnwindBlock, funcKind);
        wrapComp.CLR_API_Leave(API_allocUnwindInfo);
    }

    public void* allocGCInfo(nint size)
    {
        wrapComp.CLR_API_Enter(API_allocGCInfo);
        void* temp = wrapHnd->allocGCInfo(size);
        wrapComp.CLR_API_Leave(API_allocGCInfo);

        return temp;
    }

    public void setEHcount(int cEH)
    {
        wrapComp.CLR_API_Enter(API_setEHcount);
        wrapHnd->setEHcount(cEH);
        wrapComp.CLR_API_Leave(API_setEHcount);
    }

    public void setEHinfo(int EHnumber, CORINFO_EH_CLAUSE* clause)
    {
        wrapComp.CLR_API_Enter(API_setEHinfo);
        wrapHnd->setEHinfo(EHnumber, clause);
        wrapComp.CLR_API_Leave(API_setEHinfo);
    }

    public bool logMsg(int level, byte* fmt, void* args)
    {
        wrapComp.CLR_API_Enter(API_logMsg);
        bool temp = wrapHnd->logMsg(level, fmt, args);
        wrapComp.CLR_API_Leave(API_logMsg);

        return temp;
    }

    public int doAssert(byte* szFile, int iLine, byte* szExpr)
    {
        wrapComp.CLR_API_Enter(API_doAssert);
        int temp = wrapHnd->doAssert(szFile, iLine, szExpr);
        wrapComp.CLR_API_Leave(API_doAssert);

        return temp;
    }

    public void reportFatalError(CorJitResult result)
    {
        wrapComp.CLR_API_Enter(API_reportFatalError);
        wrapHnd->reportFatalError(result);
        wrapComp.CLR_API_Leave(API_reportFatalError);
    }

    public JITINTERFACE_HRESULT getPgoInstrumentationResults(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema** pSchema, int* pCountSchemaItems, byte** pInstrumentationData, PgoSource* pPgoSource, bool* pDynamicPgo)
    {
        wrapComp.CLR_API_Enter(API_getPgoInstrumentationResults);
        JITINTERFACE_HRESULT temp = wrapHnd->getPgoInstrumentationResults(ftnHnd, pSchema, pCountSchemaItems, pInstrumentationData, pPgoSource, pDynamicPgo);
        wrapComp.CLR_API_Leave(API_getPgoInstrumentationResults);

        return temp;
    }

    public JITINTERFACE_HRESULT allocPgoInstrumentationBySchema(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema* pSchema, int countSchemaItems, byte** pInstrumentationData)
    {
        wrapComp.CLR_API_Enter(API_allocPgoInstrumentationBySchema);
        JITINTERFACE_HRESULT temp = wrapHnd->allocPgoInstrumentationBySchema(ftnHnd, pSchema, countSchemaItems, pInstrumentationData);
        wrapComp.CLR_API_Leave(API_allocPgoInstrumentationBySchema);

        return temp;
    }

    public void recordCallSite(int instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_HANDLE methodHandle)
    {
        wrapComp.CLR_API_Enter(API_recordCallSite);
        wrapHnd->recordCallSite(instrOffset, callSig, methodHandle);
        wrapComp.CLR_API_Leave(API_recordCallSite);
    }

    public void recordWasmManagedCallSig(CORINFO_SIG_INFO* callSig)
    {
        wrapComp.CLR_API_Enter(API_recordWasmManagedCallSig);
        wrapHnd->recordWasmManagedCallSig(callSig);
        wrapComp.CLR_API_Leave(API_recordWasmManagedCallSig);
    }

    public void recordRelocation(void* location, void* locationRW, void* target, CorInfoReloc fRelocType, int addlDelta = 0)
    {
        wrapComp.CLR_API_Enter(API_recordRelocation);
        wrapHnd->recordRelocation(location, locationRW, target, fRelocType, addlDelta);
        wrapComp.CLR_API_Leave(API_recordRelocation);
    }

    public CorInfoReloc getRelocTypeHint(void* target)
    {
        wrapComp.CLR_API_Enter(API_getRelocTypeHint);
        CorInfoReloc temp = wrapHnd->getRelocTypeHint(target);
        wrapComp.CLR_API_Leave(API_getRelocTypeHint);

        return temp;
    }

    public CorInfoArch getExpectedTargetArchitecture()
    {
        wrapComp.CLR_API_Enter(API_getExpectedTargetArchitecture);
        CorInfoArch temp = wrapHnd->getExpectedTargetArchitecture();
        wrapComp.CLR_API_Leave(API_getExpectedTargetArchitecture);

        return temp;
    }

    public int getJitFlags(CORJIT_FLAGS* flags, int sizeInBytes)
    {
        wrapComp.CLR_API_Enter(API_getJitFlags);
        int temp = wrapHnd->getJitFlags(flags, sizeInBytes);
        wrapComp.CLR_API_Leave(API_getJitFlags);

        return temp;
    }

    public CORINFO_WASM_TYPE_SYMBOL_HANDLE getWasmTypeSymbol(CorInfoWasmType* types, nint typesSize)
    {
        wrapComp.CLR_API_Enter(API_getWasmTypeSymbol);
        CORINFO_WASM_TYPE_SYMBOL_HANDLE temp = wrapHnd->getWasmTypeSymbol(types, typesSize);
        wrapComp.CLR_API_Leave(API_getWasmTypeSymbol);

        return temp;
    }

    public CORINFO_METHOD_HANDLE getSpecialCopyHelper(CORINFO_CLASS_HANDLE type)
    {
        wrapComp.CLR_API_Enter(API_getSpecialCopyHelper);
        CORINFO_METHOD_HANDLE temp = wrapHnd->getSpecialCopyHelper(type);
        wrapComp.CLR_API_Leave(API_getSpecialCopyHelper);

        return temp;
    }

}
#endif