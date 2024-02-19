// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

/*****************************************************************************
 * ICorDynamicInfo contains EE interface methods which return values that may
 * change from invocation to invocation.  They cannot be embedded in persisted
 * data; they must be requeried each time the EE is run.
 *****************************************************************************/
public unsafe struct ICorDynamicInfo : ICorDynamicInfo.Interface
{
    internal Vtbl* lpVtbl;

    //
    // ICorMethodInfo
    //

    public bool isIntrinsic(CORINFO_METHOD_HANDLE ftn) => lpVtbl->isIntrinsic((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn);

    public bool notifyMethodInfoUsage(CORINFO_METHOD_HANDLE ftn) => lpVtbl->notifyMethodInfoUsage((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn);

    public uint getMethodAttribs(CORINFO_METHOD_HANDLE ftn) => lpVtbl->getMethodAttribs((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn);

    public void setMethodAttribs(CORINFO_METHOD_HANDLE ftn, CorInfoMethodRuntimeFlags attribs) => lpVtbl->setMethodAttribs((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, attribs);

    public void getMethodSig(CORINFO_METHOD_HANDLE ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->getMethodSig((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, sig, memberParent);

    public bool getMethodInfo(CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_INFO* info, CORINFO_CONTEXT_HANDLE context = null) => lpVtbl->getMethodInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, info, context);

    public bool haveSameMethodDefinition(CORINFO_METHOD_HANDLE meth1Hnd, CORINFO_METHOD_HANDLE meth2Hnd) => lpVtbl->haveSameMethodDefinition((ICorDynamicInfo*)Unsafe.AsPointer(ref this), meth1Hnd, meth2Hnd);

    public CorInfoInline canInline(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd) => lpVtbl->canInline((ICorDynamicInfo*)Unsafe.AsPointer(ref this), callerHnd, calleeHnd);

    public void beginInlining(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd) => lpVtbl->beginInlining((ICorDynamicInfo*)Unsafe.AsPointer(ref this), inlinerHnd, inlineeHnd);

    public void reportInliningDecision(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd, CorInfoInline inlineResult, sbyte* reason) => lpVtbl->reportInliningDecision((ICorDynamicInfo*)Unsafe.AsPointer(ref this), inlinerHnd, inlineeHnd, inlineResult, reason);

    public bool canTailCall(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE declaredCalleeHnd, CORINFO_METHOD_HANDLE exactCalleeHnd, bool fIsTailPrefix) => lpVtbl->canTailCall((ICorDynamicInfo*)Unsafe.AsPointer(ref this), callerHnd, declaredCalleeHnd, exactCalleeHnd, fIsTailPrefix);

    public void reportTailCallDecision(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd, bool fIsTailPrefix, CorInfoTailCall tailCallResult, sbyte* reason) => lpVtbl->reportTailCallDecision((ICorDynamicInfo*)Unsafe.AsPointer(ref this), callerHnd, calleeHnd, fIsTailPrefix, tailCallResult, reason);

    public void getEHinfo(CORINFO_METHOD_HANDLE ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause) => lpVtbl->getEHinfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, EHnumber, clause);

    public CORINFO_CLASS_HANDLE getMethodClass(CORINFO_METHOD_HANDLE method) => lpVtbl->getMethodClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), method);

    public void getMethodVTableOffset(CORINFO_METHOD_HANDLE method, uint* offsetOfIndirection, uint* offsetAfterIndirection, bool* isRelative) => lpVtbl->getMethodVTableOffset((ICorDynamicInfo*)Unsafe.AsPointer(ref this), method, offsetOfIndirection, offsetAfterIndirection, isRelative);

    public bool resolveVirtualMethod(CORINFO_DEVIRTUALIZATION_INFO* info) => lpVtbl->resolveVirtualMethod((ICorDynamicInfo*)Unsafe.AsPointer(ref this), info);

    public CORINFO_METHOD_HANDLE getUnboxedEntry(CORINFO_METHOD_HANDLE ftn, bool* requiresInstMethodTableArg) => lpVtbl->getUnboxedEntry((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, requiresInstMethodTableArg);

    public CORINFO_CLASS_HANDLE getDefaultComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->getDefaultComparerClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), elemType);

    public CORINFO_CLASS_HANDLE getDefaultEqualityComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->getDefaultEqualityComparerClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), elemType);

    public void expandRawHandleIntrinsic(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_GENERICHANDLE_RESULT* pResult) => lpVtbl->expandRawHandleIntrinsic((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken, pResult);

    public bool isIntrinsicType(CORINFO_CLASS_HANDLE classHnd) => lpVtbl->isIntrinsicType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), classHnd);

    public CorInfoCallConvExtension getUnmanagedCallConv(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig, bool* pSuppressGCTransition) => lpVtbl->getUnmanagedCallConv((ICorDynamicInfo*)Unsafe.AsPointer(ref this), method, callSiteSig, pSuppressGCTransition);

    public bool pInvokeMarshalingRequired(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig) => lpVtbl->pInvokeMarshalingRequired((ICorDynamicInfo*)Unsafe.AsPointer(ref this), method, callSiteSig);

    public bool satisfiesMethodConstraints(CORINFO_CLASS_HANDLE parent, CORINFO_METHOD_HANDLE method) => lpVtbl->satisfiesMethodConstraints((ICorDynamicInfo*)Unsafe.AsPointer(ref this), parent, method);

    public void methodMustBeLoadedBeforeCodeIsRun(CORINFO_METHOD_HANDLE method) => lpVtbl->methodMustBeLoadedBeforeCodeIsRun((ICorDynamicInfo*)Unsafe.AsPointer(ref this), method);

    public CORINFO_METHOD_HANDLE mapMethodDeclToMethodImpl(CORINFO_METHOD_HANDLE method) => lpVtbl->mapMethodDeclToMethodImpl((ICorDynamicInfo*)Unsafe.AsPointer(ref this), method);

    public void getGSCookie(GSCookie* pCookieVal, GSCookie** ppCookieVal) => lpVtbl->getGSCookie((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pCookieVal, ppCookieVal);

    public void setPatchpointInfo(PatchpointInfo* patchpointInfo) => lpVtbl->setPatchpointInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), patchpointInfo);

    public PatchpointInfo* getOSRInfo(uint* ilOffset) => lpVtbl->getOSRInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ilOffset);

    //
    // ICorModuleInfo
    //

    public void resolveToken(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->resolveToken((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken);

    public void findSig(CORINFO_MODULE_HANDLE module, uint sigTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->findSig((ICorDynamicInfo*)Unsafe.AsPointer(ref this), module, sigTOK, context, sig);

    public void findCallSiteSig(CORINFO_MODULE_HANDLE module, uint methTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->findCallSiteSig((ICorDynamicInfo*)Unsafe.AsPointer(ref this), module, methTOK, context, sig);

    public CORINFO_CLASS_HANDLE getTokenTypeAsHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->getTokenTypeAsHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken);

    public int getStringLiteral(CORINFO_MODULE_HANDLE module, uint metaTOK, char* buffer, int bufferSize, int startIndex = 0) => lpVtbl->getStringLiteral((ICorDynamicInfo*)Unsafe.AsPointer(ref this), module, metaTOK, buffer, bufferSize, 0);

    public nuint printObjectDescription(CORINFO_OBJECT_HANDLE handle, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printObjectDescription((ICorDynamicInfo*)Unsafe.AsPointer(ref this), handle, buffer, bufferSize, null);

    //
    // ICorClassInfo
    //

    public CorInfoType asCorInfoType(CORINFO_CLASS_HANDLE cls) => lpVtbl->asCorInfoType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public sbyte* getClassNameFromMetadata(CORINFO_CLASS_HANDLE cls, sbyte** namespaceName) => lpVtbl->getClassNameFromMetadata((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, namespaceName);

    public CORINFO_CLASS_HANDLE getTypeInstantiationArgument(CORINFO_CLASS_HANDLE cls, uint index) => lpVtbl->getTypeInstantiationArgument((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, index);

    public nuint printClassName(CORINFO_CLASS_HANDLE cls, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printClassName((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, buffer, bufferSize, null);

    public bool isValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->isValueClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getClassAttribs(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassAttribs((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_MODULE_HANDLE getClassModule(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassModule((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_ASSEMBLY_HANDLE getModuleAssembly(CORINFO_MODULE_HANDLE mod) => lpVtbl->getModuleAssembly((ICorDynamicInfo*)Unsafe.AsPointer(ref this), mod);

    public sbyte* getAssemblyName(CORINFO_ASSEMBLY_HANDLE assem) => lpVtbl->getAssemblyName((ICorDynamicInfo*)Unsafe.AsPointer(ref this), assem);

    public void* LongLifetimeMalloc(nuint sz) => lpVtbl->LongLifetimeMalloc((ICorDynamicInfo*)Unsafe.AsPointer(ref this), sz);

    public void LongLifetimeFree(void* obj) => lpVtbl->LongLifetimeFree((ICorDynamicInfo*)Unsafe.AsPointer(ref this), obj);

    public nuint getClassModuleIdForStatics(CORINFO_CLASS_HANDLE cls, CORINFO_MODULE_HANDLE* pModule, void** ppIndirection) => lpVtbl->getClassModuleIdForStatics((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, pModule, ppIndirection);

    public bool getIsClassInitedFlagAddress(CORINFO_CLASS_HANDLE cls, CORINFO_CONST_LOOKUP* addr, int* offset) => lpVtbl->getIsClassInitedFlagAddress((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, addr, offset);

    public bool getStaticBaseAddress(CORINFO_CLASS_HANDLE cls, bool isGc, CORINFO_CONST_LOOKUP* addr) => lpVtbl->getStaticBaseAddress((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, isGc, addr);

    public uint getClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassSize((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getHeapClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->getHeapClassSize((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public bool canAllocateOnStack(CORINFO_CLASS_HANDLE cls) => lpVtbl->canAllocateOnStack((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getClassAlignmentRequirement(CORINFO_CLASS_HANDLE cls, bool fDoubleAlignHint = false) => lpVtbl->getClassAlignmentRequirement((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, false);

    public uint getClassGClayout(CORINFO_CLASS_HANDLE cls, byte* gcPtrs) => lpVtbl->getClassGClayout((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, gcPtrs);

    public uint getClassNumInstanceFields(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassNumInstanceFields((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_FIELD_HANDLE getFieldInClass(CORINFO_CLASS_HANDLE clsHnd, int num) => lpVtbl->getFieldInClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), clsHnd, num);

    public GetTypeLayoutResult getTypeLayout(CORINFO_CLASS_HANDLE typeHnd, CORINFO_TYPE_LAYOUT_NODE* treeNodes, nuint* numTreeNodes) => lpVtbl->getTypeLayout((ICorDynamicInfo*)Unsafe.AsPointer(ref this), typeHnd, treeNodes, numTreeNodes);

    public bool checkMethodModifier(CORINFO_METHOD_HANDLE hMethod, sbyte* modifier, bool fOptional) => lpVtbl->checkMethodModifier((ICorDynamicInfo*)Unsafe.AsPointer(ref this), hMethod, modifier, fOptional);

    public CorInfoHelpFunc getNewHelper(CORINFO_CLASS_HANDLE classHandle, bool* pHasSideEffects) => lpVtbl->getNewHelper((ICorDynamicInfo*)Unsafe.AsPointer(ref this), classHandle, pHasSideEffects);

    public CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_HANDLE arrayCls) => lpVtbl->getNewArrHelper((ICorDynamicInfo*)Unsafe.AsPointer(ref this), arrayCls);

    public CorInfoHelpFunc getCastingHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fThrowing) => lpVtbl->getCastingHelper((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken, fThrowing);

    public CorInfoHelpFunc getSharedCCtorHelper(CORINFO_CLASS_HANDLE clsHnd) => lpVtbl->getSharedCCtorHelper((ICorDynamicInfo*)Unsafe.AsPointer(ref this), clsHnd);

    public CORINFO_CLASS_HANDLE getTypeForBox(CORINFO_CLASS_HANDLE cls) => lpVtbl->getTypeForBox((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoHelpFunc getBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->getBoxHelper((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoHelpFunc getUnBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->getUnBoxHelper((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_OBJECT_HANDLE getRuntimeTypePointer(CORINFO_CLASS_HANDLE cls) => lpVtbl->getRuntimeTypePointer((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public bool isObjectImmutable(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->isObjectImmutable((ICorDynamicInfo*)Unsafe.AsPointer(ref this), objPtr);

    public bool getStringChar(CORINFO_OBJECT_HANDLE strObj, int index, ushort* value) => lpVtbl->getStringChar((ICorDynamicInfo*)Unsafe.AsPointer(ref this), strObj, index, value);

    public CORINFO_CLASS_HANDLE getObjectType(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->getObjectType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), objPtr);

    public bool getReadyToRunHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_LOOKUP_KIND* pGenericLookupKind, CorInfoHelpFunc id, CORINFO_CONST_LOOKUP* pLookup) => lpVtbl->getReadyToRunHelper((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken, pGenericLookupKind, id, pLookup);

    public void getReadyToRunDelegateCtorHelper(CORINFO_RESOLVED_TOKEN* pTargetMethod, mdToken targetConstraint, CORINFO_CLASS_HANDLE delegateType, CORINFO_LOOKUP* pLookup) => lpVtbl->getReadyToRunDelegateCtorHelper((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pTargetMethod, targetConstraint, delegateType, pLookup);

    public CorInfoInitClassResult initClass(CORINFO_FIELD_HANDLE field, CORINFO_METHOD_HANDLE method, CORINFO_CONTEXT_HANDLE context) => lpVtbl->initClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field, method, context);

    public void classMustBeLoadedBeforeCodeIsRun(CORINFO_CLASS_HANDLE cls) => lpVtbl->classMustBeLoadedBeforeCodeIsRun((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_CLASS_HANDLE getBuiltinClass(CorInfoClassId classId) => lpVtbl->getBuiltinClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), classId);

    public CorInfoType getTypeForPrimitiveValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->getTypeForPrimitiveValueClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoType getTypeForPrimitiveNumericClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->getTypeForPrimitiveNumericClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public bool canCast(CORINFO_CLASS_HANDLE child, CORINFO_CLASS_HANDLE parent) => lpVtbl->canCast((ICorDynamicInfo*)Unsafe.AsPointer(ref this), child, parent);

    public TypeCompareState compareTypesForCast(CORINFO_CLASS_HANDLE fromClass, CORINFO_CLASS_HANDLE toClass) => lpVtbl->compareTypesForCast((ICorDynamicInfo*)Unsafe.AsPointer(ref this), fromClass, toClass);

    public TypeCompareState compareTypesForEquality(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->compareTypesForEquality((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls1, cls2);

    public bool isMoreSpecificType(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->isMoreSpecificType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls1, cls2);

    public bool isExactType(CORINFO_CLASS_HANDLE cls) => lpVtbl->isExactType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public TypeCompareState isEnum(CORINFO_CLASS_HANDLE cls, CORINFO_CLASS_HANDLE* underlyingType) => lpVtbl->isEnum((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, underlyingType);

    public CORINFO_CLASS_HANDLE getParentType(CORINFO_CLASS_HANDLE cls) => lpVtbl->getParentType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoType getChildType(CORINFO_CLASS_HANDLE clsHnd, CORINFO_CLASS_HANDLE* clsRet) => lpVtbl->getChildType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), clsHnd, clsRet);

    public bool isSDArray(CORINFO_CLASS_HANDLE cls) => lpVtbl->isSDArray((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getArrayRank(CORINFO_CLASS_HANDLE cls) => lpVtbl->getArrayRank((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoArrayIntrinsic getArrayIntrinsicID(CORINFO_METHOD_HANDLE ftn) => lpVtbl->getArrayIntrinsicID((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn);

    public void* getArrayInitializationData(CORINFO_FIELD_HANDLE field, uint size) => lpVtbl->getArrayInitializationData((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field, size);

    public CorInfoIsAccessAllowedResult canAccessClass(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_HELPER_DESC* pAccessHelper) => lpVtbl->canAccessClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken, callerHandle, pAccessHelper);

    //
    // ICorFieldInfo
    //

    public nuint printFieldName(CORINFO_FIELD_HANDLE field, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printFieldName((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field, buffer, bufferSize, null);

    public CORINFO_CLASS_HANDLE getFieldClass(CORINFO_FIELD_HANDLE field) => lpVtbl->getFieldClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field);

    public CorInfoType getFieldType(CORINFO_FIELD_HANDLE field, CORINFO_CLASS_HANDLE* structType = null, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->getFieldType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field, null, null);

    public uint getFieldOffset(CORINFO_FIELD_HANDLE field) => lpVtbl->getFieldOffset((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field);

    public void getFieldInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_ACCESS_FLAGS flags, CORINFO_FIELD_INFO* pResult) => lpVtbl->getFieldInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken, callerHandle, flags, pResult);

    public uint getThreadLocalFieldInfo(CORINFO_FIELD_HANDLE field, bool isGCType) => lpVtbl->getThreadLocalFieldInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field, isGCType);

    public void getThreadLocalStaticBlocksInfo(CORINFO_THREAD_STATIC_BLOCKS_INFO* pInfo, bool isGCType) => lpVtbl->getThreadLocalStaticBlocksInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pInfo, isGCType);

    public void getThreadLocalStaticInfo_NativeAOT(CORINFO_THREAD_STATIC_INFO_NATIVEAOT* pInfo) => lpVtbl->getThreadLocalStaticInfo_NativeAOT((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pInfo);

    public bool isFieldStatic(CORINFO_FIELD_HANDLE fldHnd) => lpVtbl->isFieldStatic((ICorDynamicInfo*)Unsafe.AsPointer(ref this), fldHnd);

    public int getArrayOrStringLength(CORINFO_OBJECT_HANDLE objHnd) => lpVtbl->getArrayOrStringLength((ICorDynamicInfo*)Unsafe.AsPointer(ref this), objHnd);

    //
    // ICorDebugInfo
    //

    public void getBoundaries(CORINFO_METHOD_HANDLE ftn, uint* cILOffsets, uint** pILOffsets, ICorDebugInfo.BoundaryTypes* implicitBoundaries) => lpVtbl->getBoundaries((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, cILOffsets, pILOffsets, implicitBoundaries);

    public void setBoundaries(CORINFO_METHOD_HANDLE ftn, uint cMap, ICorDebugInfo.OffsetMapping* pMap) => lpVtbl->setBoundaries((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, cMap, pMap);

    public void getVars(CORINFO_METHOD_HANDLE ftn, uint* cVars, ICorDebugInfo.ILVarInfo** vars, bool* extendOthers) => lpVtbl->getVars((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, cVars, vars, extendOthers);

    public void setVars(CORINFO_METHOD_HANDLE ftn, uint cVars, ICorDebugInfo.NativeVarInfo* vars) => lpVtbl->setVars((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, cVars, vars);

    public void reportRichMappings(ICorDebugInfo.InlineTreeNode* inlineTreeNodes, uint numInlineTreeNodes, ICorDebugInfo.RichOffsetMapping* mappings, uint numMappings) => lpVtbl->reportRichMappings((ICorDynamicInfo*)Unsafe.AsPointer(ref this), inlineTreeNodes, numInlineTreeNodes, mappings, numMappings);

    //
    // Misc
    //

    public void* allocateArray(nuint cBytes) => lpVtbl->allocateArray((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cBytes);

    public void freeArray(void* array) => lpVtbl->freeArray((ICorDynamicInfo*)Unsafe.AsPointer(ref this), array);

    //
    // ICorArgInfo
    //

    public CORINFO_ARG_LIST_HANDLE getArgNext(CORINFO_ARG_LIST_HANDLE args) => lpVtbl->getArgNext((ICorDynamicInfo*)Unsafe.AsPointer(ref this), args);

    public CorInfoTypeWithMod getArgType(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args, CORINFO_CLASS_HANDLE* vcTypeRet) => lpVtbl->getArgType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), sig, args, vcTypeRet);

    public int getExactClasses(CORINFO_CLASS_HANDLE baseType, int maxExactClasses, CORINFO_CLASS_HANDLE* exactClsRet) => lpVtbl->getExactClasses((ICorDynamicInfo*)Unsafe.AsPointer(ref this), baseType, maxExactClasses, exactClsRet);

    public CORINFO_CLASS_HANDLE getArgClass(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args) => lpVtbl->getArgClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), sig, args);

    public CorInfoHFAElemType getHFAType(CORINFO_CLASS_HANDLE hClass) => lpVtbl->getHFAType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), hClass);

    public bool runWithErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->runWithErrorTrap((ICorDynamicInfo*)Unsafe.AsPointer(ref this), function, parameter);

    public bool runWithSPMIErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->runWithSPMIErrorTrap((ICorDynamicInfo*)Unsafe.AsPointer(ref this), function, parameter);

    public void getEEInfo(CORINFO_EE_INFO* pEEInfoOut) => lpVtbl->getEEInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pEEInfoOut);

    public char* getJitTimeLogFilename() => lpVtbl->getJitTimeLogFilename((ICorDynamicInfo*)Unsafe.AsPointer(ref this));

    //
    // Diagnostic methods
    //

    public mdMethodDef getMethodDefFromMethod(CORINFO_METHOD_HANDLE hMethod) => lpVtbl->getMethodDefFromMethod((ICorDynamicInfo*)Unsafe.AsPointer(ref this), hMethod);

    public nuint printMethodName(CORINFO_METHOD_HANDLE ftn, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printMethodName((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, buffer, bufferSize, null);

    public sbyte* getMethodNameFromMetadata(CORINFO_METHOD_HANDLE ftn, sbyte** className, sbyte** namespaceName, sbyte** enclosingClassName) => lpVtbl->getMethodNameFromMetadata((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, className, namespaceName, enclosingClassName);

    public uint getMethodHash(CORINFO_METHOD_HANDLE ftn) => lpVtbl->getMethodHash((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn);

    public bool getSystemVAmd64PassStructInRegisterDescriptor(CORINFO_CLASS_HANDLE structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr) => lpVtbl->getSystemVAmd64PassStructInRegisterDescriptor((ICorDynamicInfo*)Unsafe.AsPointer(ref this), structHnd, structPassInRegDescPtr);

    public uint getLoongArch64PassStructInRegisterFlags(CORINFO_CLASS_HANDLE cls) => lpVtbl->getLoongArch64PassStructInRegisterFlags((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getRISCV64PassStructInRegisterFlags(CORINFO_CLASS_HANDLE cls) => lpVtbl->getRISCV64PassStructInRegisterFlags((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls);

    //
    // ICorDynamicInfo
    //

    public uint getThreadTLSIndex(void** ppIndirection = null) => lpVtbl->getThreadTLSIndex((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ppIndirection);

    public int* getAddrOfCaptureThreadGlobal(void** ppIndirection = null) => lpVtbl->getAddrOfCaptureThreadGlobal((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ppIndirection);

    public void* getHelperFtn(CorInfoHelpFunc ftnNum, void** ppIndirection = null) => lpVtbl->getHelperFtn((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftnNum, ppIndirection);

    public void getFunctionEntryPoint(CORINFO_METHOD_HANDLE ftn, CORINFO_CONST_LOOKUP* pResult, CORINFO_ACCESS_FLAGS accessFlags = CORINFO_ACCESS_ANY) => lpVtbl->getFunctionEntryPoint((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, pResult, accessFlags);

    public void getFunctionFixedEntryPoint(CORINFO_METHOD_HANDLE ftn, bool isUnsafeFunctionPointer, CORINFO_CONST_LOOKUP* pResult) => lpVtbl->getFunctionFixedEntryPoint((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, isUnsafeFunctionPointer, pResult);

    public void* getMethodSync(CORINFO_METHOD_HANDLE ftn, void** ppIndirection = null) => lpVtbl->getMethodSync((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftn, ppIndirection);

    public CorInfoHelpFunc getLazyStringLiteralHelper(CORINFO_MODULE_HANDLE handle) => lpVtbl->getLazyStringLiteralHelper((ICorDynamicInfo*)Unsafe.AsPointer(ref this), handle);

    public CORINFO_MODULE_HANDLE embedModuleHandle(CORINFO_MODULE_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedModuleHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), handle, ppIndirection);

    public CORINFO_CLASS_HANDLE embedClassHandle(CORINFO_CLASS_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedClassHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), handle, ppIndirection);

    public CORINFO_METHOD_HANDLE embedMethodHandle(CORINFO_METHOD_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedMethodHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), handle, ppIndirection);

    public CORINFO_FIELD_HANDLE embedFieldHandle(CORINFO_FIELD_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedFieldHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), handle, ppIndirection);

    public void embedGenericHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fEmbedParent, CORINFO_GENERICHANDLE_RESULT* pResult) => lpVtbl->embedGenericHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken, fEmbedParent, pResult);

    public void getLocationOfThisType(CORINFO_METHOD_HANDLE context, CORINFO_LOOKUP_KIND* pLookupKind) => lpVtbl->getLocationOfThisType((ICorDynamicInfo*)Unsafe.AsPointer(ref this), context, pLookupKind);

    public void getAddressOfPInvokeTarget(CORINFO_METHOD_HANDLE method, CORINFO_CONST_LOOKUP* pLookup) => lpVtbl->getAddressOfPInvokeTarget((ICorDynamicInfo*)Unsafe.AsPointer(ref this), method, pLookup);

    public void* GetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig, void** ppIndirection = null) => lpVtbl->GetCookieForPInvokeCalliSig((ICorDynamicInfo*)Unsafe.AsPointer(ref this), szMetaSig, ppIndirection);

    public bool canGetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig) => lpVtbl->canGetCookieForPInvokeCalliSig((ICorDynamicInfo*)Unsafe.AsPointer(ref this), szMetaSig);

    public CORINFO_JUST_MY_CODE_HANDLE getJustMyCodeHandle(CORINFO_METHOD_HANDLE method, CORINFO_JUST_MY_CODE_HANDLE** ppIndirection = null) => lpVtbl->getJustMyCodeHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), method, ppIndirection);

    public void GetProfilingHandle(bool* pbHookFunction, void** pProfilerHandle, bool* pbIndirectedHandles) => lpVtbl->GetProfilingHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pbHookFunction, pProfilerHandle, pbIndirectedHandles);

    public void getCallInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult) => lpVtbl->getCallInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken, pConstrainedResolvedToken, callerHandle, flags, pResult);

    public uint getClassDomainID(CORINFO_CLASS_HANDLE cls, void** ppIndirection = null) => lpVtbl->getClassDomainID((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cls, ppIndirection);

    public bool getStaticFieldContent(CORINFO_FIELD_HANDLE field, byte* buffer, int bufferSize, int valueOffset = 0, bool ignoreMovableObjects = true) => lpVtbl->getStaticFieldContent((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field, buffer, bufferSize, valueOffset, ignoreMovableObjects);

    public bool getObjectContent(CORINFO_OBJECT_HANDLE obj, byte* buffer, int bufferSize, int valueOffset) => lpVtbl->getObjectContent((ICorDynamicInfo*)Unsafe.AsPointer(ref this), obj, buffer, bufferSize, valueOffset);

    public CORINFO_CLASS_HANDLE getStaticFieldCurrentClass(CORINFO_FIELD_HANDLE field, bool* pIsSpeculative = null) => lpVtbl->getStaticFieldCurrentClass((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field, pIsSpeculative);

    public CORINFO_VARARGS_HANDLE getVarArgsHandle(CORINFO_SIG_INFO* pSig, void** ppIndirection = null) => lpVtbl->getVarArgsHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pSig, ppIndirection);

    public bool canGetVarArgsHandle(CORINFO_SIG_INFO* pSig) => lpVtbl->canGetVarArgsHandle((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pSig);

    public InfoAccessType constructStringLiteral(CORINFO_MODULE_HANDLE module, mdToken metaTok, void** ppValue) => lpVtbl->constructStringLiteral((ICorDynamicInfo*)Unsafe.AsPointer(ref this), module, metaTok, ppValue);

    public InfoAccessType emptyStringLiteral(void** ppValue) => lpVtbl->emptyStringLiteral((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ppValue);

    public uint getFieldThreadLocalStoreID(CORINFO_FIELD_HANDLE field, void** ppIndirection = null) => lpVtbl->getFieldThreadLocalStoreID((ICorDynamicInfo*)Unsafe.AsPointer(ref this), field, ppIndirection);

    public CORINFO_METHOD_HANDLE GetDelegateCtor(CORINFO_METHOD_HANDLE methHnd, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE targetMethodHnd, DelegateCtorArgs* pCtorData) => lpVtbl->GetDelegateCtor((ICorDynamicInfo*)Unsafe.AsPointer(ref this), methHnd, clsHnd, targetMethodHnd, pCtorData);

    public void MethodCompileComplete(CORINFO_METHOD_HANDLE methHnd) => lpVtbl->MethodCompileComplete((ICorDynamicInfo*)Unsafe.AsPointer(ref this), methHnd);

    public bool getTailCallHelpers(CORINFO_RESOLVED_TOKEN* callToken, CORINFO_SIG_INFO* sig, CORINFO_GET_TAILCALL_HELPERS_FLAGS flags, CORINFO_TAILCALL_HELPERS* pResult) => lpVtbl->getTailCallHelpers((ICorDynamicInfo*)Unsafe.AsPointer(ref this), callToken, sig, flags, pResult);

    public bool convertPInvokeCalliToCall(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fMustConvert) => lpVtbl->convertPInvokeCalliToCall((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pResolvedToken, fMustConvert);

    public bool notifyInstructionSetUsage(CORINFO_InstructionSet instructionSet, bool supportEnabled) => lpVtbl->notifyInstructionSetUsage((ICorDynamicInfo*)Unsafe.AsPointer(ref this), instructionSet, supportEnabled);

    public void updateEntryPointForTailCall(CORINFO_CONST_LOOKUP* entryPoint) => lpVtbl->updateEntryPointForTailCall((ICorDynamicInfo*)Unsafe.AsPointer(ref this), entryPoint);

    public interface Interface : ICorStaticInfo.Interface
    {
        //
        // These methods return values to the JIT which are not constant
        // from session to session.
        //
        // These methods take an extra parameter : void **ppIndirection.
        // If a JIT supports generation of prejit code (install-o-jit), it
        // must pass a non-null value for this parameter, and check the
        // resulting value.  If *ppIndirection is NULL, code should be
        // generated normally.  If non-null, then the value of
        // *ppIndirection is an address in the cookie table, and the code
        // generator needs to generate an indirection through the table to
        // get the resulting value.  In this case, the return result of the
        // function must NOT be directly embedded in the generated code.
        //
        // Note that if a JIT does not support prejit code generation, it
        // may ignore the extra parameter & pass the default of NULL - the
        // prejit ICorDynamicInfo implementation will see this & generate
        // an error if the jitter is used in a prejit scenario.
        //

        // Return details about EE internal data structures

        uint getThreadTLSIndex(void** ppIndirection = null);

        int* getAddrOfCaptureThreadGlobal(void** ppIndirection = null);

        // return the native entry point to an EE helper (see CorInfoHelpFunc)
        void* getHelperFtn(CorInfoHelpFunc ftnNum, void** ppIndirection = null);

        // return a callable address of the function (native code). This function
        // may return a different value (depending on whether the method has
        // been JITed or not.
        void getFunctionEntryPoint(CORINFO_METHOD_HANDLE ftn, CORINFO_CONST_LOOKUP* pResult, CORINFO_ACCESS_FLAGS accessFlags = CORINFO_ACCESS_ANY);

        // return a directly callable address. This can be used similarly to the
        // value returned by getFunctionEntryPoint() except that it is
        // guaranteed to be multi callable entrypoint.
        void getFunctionFixedEntryPoint(CORINFO_METHOD_HANDLE ftn, bool isUnsafeFunctionPointer, CORINFO_CONST_LOOKUP* pResult);

        // get the synchronization handle that is passed to monXstatic function
        void* getMethodSync(CORINFO_METHOD_HANDLE ftn, void** ppIndirection = null);

        // get slow lazy string literal helper to use (CORINFO_HELP_STRCNS*).
        // Returns CORINFO_HELP_UNDEF if lazy string literal helper cannot be used.
        CorInfoHelpFunc getLazyStringLiteralHelper(CORINFO_MODULE_HANDLE handle);

        CORINFO_MODULE_HANDLE embedModuleHandle(CORINFO_MODULE_HANDLE handle, void** ppIndirection = null);

        CORINFO_CLASS_HANDLE embedClassHandle(CORINFO_CLASS_HANDLE handle, void** ppIndirection = null);

        CORINFO_METHOD_HANDLE embedMethodHandle(CORINFO_METHOD_HANDLE handle, void** ppIndirection = null);

        CORINFO_FIELD_HANDLE embedFieldHandle(CORINFO_FIELD_HANDLE handle, void** ppIndirection = null);

        // Given a module scope (module), a method handle (context) and
        // a metadata token (metaTOK), fetch the handle
        // (type, field or method) associated with the token.
        // If this is not possible at compile-time (because the current method's
        // code is shared and the token contains generic parameters)
        // then indicate how the handle should be looked up at run-time.
        //
        // fEmbedParent
        //   `true` - embeds parent type handle of the field/method handle
        //
        void embedGenericHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fEmbedParent, CORINFO_GENERICHANDLE_RESULT* pResult);

        // Return information used to locate the exact enclosing type of the current method.
        // Used only to invoke .cctor method from code shared across generic instantiations
        //   !needsRuntimeLookup       statically known (enclosing type of method itself)
        //   needsRuntimeLookup:
        //      CORINFO_LOOKUP_THISOBJ     use vtable pointer of 'this' param
        //      CORINFO_LOOKUP_CLASSPARAM  use vtable hidden param
        //      CORINFO_LOOKUP_METHODPARAM use enclosing type of method-desc hidden param
        void getLocationOfThisType(CORINFO_METHOD_HANDLE context, CORINFO_LOOKUP_KIND* pLookupKind);

        // return the address of the PInvoke target. May be a fixup area in the
        // case of late-bound PInvoke calls.
        void getAddressOfPInvokeTarget(CORINFO_METHOD_HANDLE method, CORINFO_CONST_LOOKUP* pLookup);

        // Generate a cookie based on the signature that would needs to be passed
        // to CORINFO_HELP_PINVOKE_CALLI
        void* GetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig, void** ppIndirection = null);

        // returns true if a VM cookie can be generated for it (might be false due to cross-module
        // inlining, in which case the inlining should be aborted)
        bool canGetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig);

        // Gets a handle that is checked to see if the current method is
        // included in "JustMyCode"
        CORINFO_JUST_MY_CODE_HANDLE getJustMyCodeHandle(CORINFO_METHOD_HANDLE method, CORINFO_JUST_MY_CODE_HANDLE** ppIndirection = null);

        // Gets a method handle that can be used to correlate profiling data.
        // This is the IP of a native method, or the address of the descriptor struct
        // for IL.  Always guaranteed to be unique per process, and not to move. */
        void GetProfilingHandle(bool* pbHookFunction, void** pProfilerHandle, bool* pbIndirectedHandles);

        // Returns instructions on how to make the call. See code:CORINFO_CALL_INFO for possible return values.
        void getCallInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult);

        // Returns the class's domain ID for accessing shared statics
        uint getClassDomainID(CORINFO_CLASS_HANDLE cls, void** ppIndirection = null);

        //------------------------------------------------------------------------------
        // getStaticFieldContent: returns true and the actual field's value if the given
        //    field represents a statically initialized readonly field of any type.
        //
        // Arguments:
        //    field                - field handle
        //    buffer               - buffer field's value will be stored to
        //    bufferSize           - size of buffer
        //    ignoreMovableObjects - ignore movable reference types or not
        //
        // Return Value:
        //    Returns true if field's constant value was available and successfully copied to buffer
        //
        bool getStaticFieldContent(CORINFO_FIELD_HANDLE field, byte* buffer, int bufferSize, int valueOffset = 0, bool ignoreMovableObjects = true);

        bool getObjectContent(CORINFO_OBJECT_HANDLE obj, byte* buffer, int bufferSize, int valueOffset);

        // If pIsSpeculative is NULL, return the class handle for the value of ref-class typed
        // static readonly fields, if there is a unique location for the static and the class
        // is already initialized.
        //
        // If pIsSpeculative is not NULL, fetch the class handle for the value of all ref-class
        // typed static fields, if there is a unique location for the static and the field is
        // not null.
        //
        // Set *pIsSpeculative true if this type may change over time (field is not readonly or
        // is readonly but class has not yet finished initialization). Set *pIsSpeculative false
        // if this type will not change.
        CORINFO_CLASS_HANDLE getStaticFieldCurrentClass(CORINFO_FIELD_HANDLE field, bool* pIsSpeculative = null);

        // registers a vararg sig & returns a VM cookie for it (which can contain other stuff)
        CORINFO_VARARGS_HANDLE getVarArgsHandle(CORINFO_SIG_INFO* pSig, void** ppIndirection = null);

        // returns true if a VM cookie can be generated for it (might be false due to cross-module
        // inlining, in which case the inlining should be aborted)
        bool canGetVarArgsHandle(CORINFO_SIG_INFO* pSig);

        // Allocate a string literal on the heap and return a handle to it
        InfoAccessType constructStringLiteral(CORINFO_MODULE_HANDLE module, mdToken metaTok, void** ppValue);

        InfoAccessType emptyStringLiteral(void** ppValue);

        // (static fields only) given that 'field' refers to thread local store,
        // return the ID (TLS index), which is used to find the beginning of the
        // TLS data area for the particular DLL 'field' is associated with.
        uint getFieldThreadLocalStoreID(CORINFO_FIELD_HANDLE field, void** ppIndirection = null);

        CORINFO_METHOD_HANDLE GetDelegateCtor(CORINFO_METHOD_HANDLE methHnd, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE targetMethodHnd, DelegateCtorArgs* pCtorData);

        void MethodCompileComplete(CORINFO_METHOD_HANDLE methHnd);

        // Obtain tailcall help for the specified call site.
        bool getTailCallHelpers(CORINFO_RESOLVED_TOKEN* callToken, CORINFO_SIG_INFO* sig, CORINFO_GET_TAILCALL_HELPERS_FLAGS flags, CORINFO_TAILCALL_HELPERS* pResult);

        // Optionally, convert calli to regular method call. This is for PInvoke argument marshalling.
        bool convertPInvokeCalliToCall(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fMustConvert);

        // Notify EE about intent to use or not to use instruction set in the method. Returns true if the instruction set is supported unconditionally.
        bool notifyInstructionSetUsage(CORINFO_InstructionSet instructionSet, bool supportEnabled);

        // Notify EE that JIT needs an entry-point that is tail-callable.
        // This is used for AOT on x64 to support delay loaded fast tailcalls.
        // Normally the indirection cell is retrieved from the return address,
        // but for tailcalls, the contract is that JIT leaves the indirection cell in
        // a register during tailcall.
        void updateEntryPointForTailCall(CORINFO_CONST_LOOKUP* entryPoint);
    }

    public struct Vtbl
    {
        //
        // ICorMethodInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, bool> isIntrinsic;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, bool> notifyMethodInfoUsage;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, uint> getMethodAttribs;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CorInfoMethodRuntimeFlags, void> setMethodAttribs;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_SIG_INFO*, CORINFO_CLASS_HANDLE, void> getMethodSig;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_INFO*, CORINFO_CONTEXT_HANDLE, bool> getMethodInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, bool> haveSameMethodDefinition;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, CorInfoInline> canInline;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, void> beginInlining;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, CorInfoInline, sbyte*, void> reportInliningDecision;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, bool, bool> canTailCall;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, bool, CorInfoTailCall, sbyte*, void> reportTailCallDecision;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, uint, CORINFO_EH_CLAUSE*, void> getEHinfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_CLASS_HANDLE> getMethodClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, uint*, uint*, bool*, void> getMethodVTableOffset;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_DEVIRTUALIZATION_INFO*, bool> resolveVirtualMethod;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, bool*, CORINFO_METHOD_HANDLE> getUnboxedEntry;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getDefaultComparerClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getDefaultEqualityComparerClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_GENERICHANDLE_RESULT*, void> expandRawHandleIntrinsic;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, bool> isIntrinsicType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_SIG_INFO*, bool*, CorInfoCallConvExtension> getUnmanagedCallConv;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_SIG_INFO*, bool> pInvokeMarshalingRequired;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_METHOD_HANDLE, bool> satisfiesMethodConstraints;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, void> methodMustBeLoadedBeforeCodeIsRun;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE> mapMethodDeclToMethodImpl;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, GSCookie*, GSCookie**, void> getGSCookie;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, PatchpointInfo*, void> setPatchpointInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, uint*, PatchpointInfo*> getOSRInfo;

        //
        // ICorModuleInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, void> resolveToken;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_MODULE_HANDLE, uint, CORINFO_CONTEXT_HANDLE, CORINFO_SIG_INFO*, void> findSig;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_MODULE_HANDLE, uint, CORINFO_CONTEXT_HANDLE, CORINFO_SIG_INFO*, void> findCallSiteSig;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_CLASS_HANDLE> getTokenTypeAsHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_MODULE_HANDLE, uint, char*, int, int, int> getStringLiteral;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_OBJECT_HANDLE, sbyte*, nuint, nuint*, nuint> printObjectDescription;

        //
        // ICorClassInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CorInfoType> asCorInfoType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, sbyte**, sbyte*> getClassNameFromMetadata;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, uint, CORINFO_CLASS_HANDLE> getTypeInstantiationArgument;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, sbyte*, nuint, nuint*, nuint> printClassName;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, bool> isValueClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, uint> getClassAttribs;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_MODULE_HANDLE> getClassModule;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_MODULE_HANDLE, CORINFO_ASSEMBLY_HANDLE> getModuleAssembly;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_ASSEMBLY_HANDLE, sbyte*> getAssemblyName;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, nuint, void*> LongLifetimeMalloc;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, void*, void> LongLifetimeFree;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_MODULE_HANDLE*, void**, nuint> getClassModuleIdForStatics;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CONST_LOOKUP*, int*, bool> getIsClassInitedFlagAddress;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, bool, CORINFO_CONST_LOOKUP*, bool> getStaticBaseAddress;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, uint> getClassSize;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, uint> getHeapClassSize;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, bool> canAllocateOnStack;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, bool, uint> getClassAlignmentRequirement;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, byte*, uint> getClassGClayout;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, uint> getClassNumInstanceFields;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, int, CORINFO_FIELD_HANDLE> getFieldInClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_TYPE_LAYOUT_NODE*, nuint*, GetTypeLayoutResult> getTypeLayout;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, sbyte*, bool, bool> checkMethodModifier;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, bool*, CorInfoHelpFunc> getNewHelper;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getNewArrHelper;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, bool, CorInfoHelpFunc> getCastingHelper;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getSharedCCtorHelper;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getTypeForBox;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getBoxHelper;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getUnBoxHelper;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_OBJECT_HANDLE> getRuntimeTypePointer;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_OBJECT_HANDLE, bool> isObjectImmutable;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_OBJECT_HANDLE, int, ushort*, bool> getStringChar;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_OBJECT_HANDLE, CORINFO_CLASS_HANDLE> getObjectType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_LOOKUP_KIND*, CorInfoHelpFunc, CORINFO_CONST_LOOKUP*, bool> getReadyToRunHelper;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, mdToken, CORINFO_CLASS_HANDLE, CORINFO_LOOKUP*, void> getReadyToRunDelegateCtorHelper;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, CORINFO_METHOD_HANDLE, CORINFO_CONTEXT_HANDLE, CorInfoInitClassResult> initClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, void> classMustBeLoadedBeforeCodeIsRun;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CorInfoClassId, CORINFO_CLASS_HANDLE> getBuiltinClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CorInfoType> getTypeForPrimitiveValueClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CorInfoType> getTypeForPrimitiveNumericClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, bool> canCast;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, TypeCompareState> compareTypesForCast;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, TypeCompareState> compareTypesForEquality;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, bool> isMoreSpecificType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, bool> isExactType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*, TypeCompareState> isEnum;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getParentType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*, CorInfoType> getChildType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, bool> isSDArray;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, uint> getArrayRank;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CorInfoArrayIntrinsic> getArrayIntrinsicID;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, uint, void*> getArrayInitializationData;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_METHOD_HANDLE, CORINFO_HELPER_DESC*, CorInfoIsAccessAllowedResult> canAccessClass;

        //
        // ICorFieldInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, sbyte*, nuint, nuint*, nuint> printFieldName;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, CORINFO_CLASS_HANDLE> getFieldClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, CORINFO_CLASS_HANDLE*, CORINFO_FIELD_HANDLE, CorInfoType> getFieldType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, uint> getFieldOffset;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_METHOD_HANDLE, CORINFO_ACCESS_FLAGS, CORINFO_FIELD_INFO*, void> getFieldInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, bool, uint> getThreadLocalFieldInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_THREAD_STATIC_BLOCKS_INFO*, bool, void> getThreadLocalStaticBlocksInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_THREAD_STATIC_INFO_NATIVEAOT*, void> getThreadLocalStaticInfo_NativeAOT;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, bool> isFieldStatic;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_OBJECT_HANDLE, int> getArrayOrStringLength;

        //
        // ICorDebugInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, uint*, uint**, ICorDebugInfo.BoundaryTypes*, void> getBoundaries;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, uint, ICorDebugInfo.OffsetMapping*, void> setBoundaries;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, uint*, ICorDebugInfo.ILVarInfo**, bool*, void> getVars;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, uint, ICorDebugInfo.NativeVarInfo*, void> setVars;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, ICorDebugInfo.InlineTreeNode*, uint, ICorDebugInfo.RichOffsetMapping*, uint, void> reportRichMappings;

        //
        // Misc
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, nuint, void*> allocateArray;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, void*, void> freeArray;

        //
        // ICorArgInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_ARG_LIST_HANDLE, CORINFO_ARG_LIST_HANDLE> getArgNext;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*, CorInfoTypeWithMod> getArgType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, int, CORINFO_CLASS_HANDLE*, int> getExactClasses;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE> getArgClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, CorInfoHFAElemType> getHFAType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, errorTrapFunction, void*, bool> runWithErrorTrap;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, errorTrapFunction, void*, bool> runWithSPMIErrorTrap;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_EE_INFO*, void> getEEInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, char*> getJitTimeLogFilename;

        //
        // Diagnostic methods
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, mdMethodDef> getMethodDefFromMethod;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, sbyte*, nuint, nuint*, nuint> printMethodName;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, sbyte**, sbyte**, sbyte**, sbyte*> getMethodNameFromMetadata;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, uint> getMethodHash;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR*, bool> getSystemVAmd64PassStructInRegisterDescriptor;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, uint> getLoongArch64PassStructInRegisterFlags;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, uint> getRISCV64PassStructInRegisterFlags;

        //
        // ICorDynamicInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, void**, uint> getThreadTLSIndex;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, void**, int*> getAddrOfCaptureThreadGlobal;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CorInfoHelpFunc, void**, void*> getHelperFtn;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_CONST_LOOKUP*, CORINFO_ACCESS_FLAGS, void> getFunctionEntryPoint;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, bool, CORINFO_CONST_LOOKUP*, void> getFunctionFixedEntryPoint;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, void**, void*> getMethodSync;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_MODULE_HANDLE, CorInfoHelpFunc> getLazyStringLiteralHelper;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_MODULE_HANDLE, void**, CORINFO_MODULE_HANDLE> embedModuleHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, void**, CORINFO_CLASS_HANDLE> embedClassHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, void**, CORINFO_METHOD_HANDLE> embedMethodHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, void**, CORINFO_FIELD_HANDLE> embedFieldHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, bool, CORINFO_GENERICHANDLE_RESULT*, void> embedGenericHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_LOOKUP_KIND*, void> getLocationOfThisType;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_CONST_LOOKUP*, void> getAddressOfPInvokeTarget;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_SIG_INFO*, void**, void*> GetCookieForPInvokeCalliSig;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_SIG_INFO*, bool> canGetCookieForPInvokeCalliSig;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_JUST_MY_CODE_HANDLE**, CORINFO_JUST_MY_CODE_HANDLE> getJustMyCodeHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, bool*, void**, bool*, void> GetProfilingHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_RESOLVED_TOKEN*, CORINFO_METHOD_HANDLE, CORINFO_CALLINFO_FLAGS, CORINFO_CALL_INFO*, void> getCallInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CLASS_HANDLE, void**, uint> getClassDomainID;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, byte*, int, int, bool, bool> getStaticFieldContent;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_OBJECT_HANDLE, byte*, int, int, bool> getObjectContent;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, bool*, CORINFO_CLASS_HANDLE> getStaticFieldCurrentClass;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_SIG_INFO*, void**, CORINFO_VARARGS_HANDLE> getVarArgsHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_SIG_INFO*, bool> canGetVarArgsHandle;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_MODULE_HANDLE, mdToken, void**, InfoAccessType> constructStringLiteral;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, void**, InfoAccessType> emptyStringLiteral;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_FIELD_HANDLE, void**, uint> getFieldThreadLocalStoreID;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, CORINFO_CLASS_HANDLE, CORINFO_METHOD_HANDLE, DelegateCtorArgs*, CORINFO_METHOD_HANDLE> GetDelegateCtor;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, void> MethodCompileComplete;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_SIG_INFO*, CORINFO_GET_TAILCALL_HELPERS_FLAGS, CORINFO_TAILCALL_HELPERS*, bool> getTailCallHelpers;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_RESOLVED_TOKEN*, bool, bool> convertPInvokeCalliToCall;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_InstructionSet, bool, bool> notifyInstructionSetUsage;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_CONST_LOOKUP*, void> updateEntryPointForTailCall;
    }
}
