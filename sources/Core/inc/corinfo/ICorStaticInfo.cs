// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

// This interface is logically split into sections for each class of information
// (ICorMethodInfo, ICorModuleInfo, etc.). This split used to exist physically as well
// using virtual inheritance, but was eliminated to improve efficiency of the JIT-EE
// interface calls.
public unsafe struct ICorStaticInfo : ICorStaticInfo.Interface
{
    internal Vtbl* lpVtbl;

    //
    // ICorMethodInfo
    //

    public bool isIntrinsic(CORINFO_METHOD_HANDLE ftn) => lpVtbl->isIntrinsic((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn);

    public bool notifyMethodInfoUsage(CORINFO_METHOD_HANDLE ftn) => lpVtbl->notifyMethodInfoUsage((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn);

    public uint getMethodAttribs(CORINFO_METHOD_HANDLE ftn) => lpVtbl->getMethodAttribs((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn);

    public void setMethodAttribs(CORINFO_METHOD_HANDLE ftn, CorInfoMethodRuntimeFlags attribs) => lpVtbl->setMethodAttribs((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, attribs);

    public void getMethodSig(CORINFO_METHOD_HANDLE ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->getMethodSig((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, sig, memberParent);

    public bool getMethodInfo(CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_INFO* info, CORINFO_CONTEXT_HANDLE context = null) => lpVtbl->getMethodInfo((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, info, context);

    public bool haveSameMethodDefinition(CORINFO_METHOD_HANDLE meth1Hnd, CORINFO_METHOD_HANDLE meth2Hnd) => lpVtbl->haveSameMethodDefinition((ICorStaticInfo*)Unsafe.AsPointer(ref this), meth1Hnd, meth2Hnd);

    public CorInfoInline canInline(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd) => lpVtbl->canInline((ICorStaticInfo*)Unsafe.AsPointer(ref this), callerHnd, calleeHnd);

    public void beginInlining(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd) => lpVtbl->beginInlining((ICorStaticInfo*)Unsafe.AsPointer(ref this), inlinerHnd, inlineeHnd);

    public void reportInliningDecision(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd, CorInfoInline inlineResult, sbyte* reason) => lpVtbl->reportInliningDecision((ICorStaticInfo*)Unsafe.AsPointer(ref this), inlinerHnd, inlineeHnd, inlineResult, reason);

    public bool canTailCall(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE declaredCalleeHnd, CORINFO_METHOD_HANDLE exactCalleeHnd, bool fIsTailPrefix) => lpVtbl->canTailCall((ICorStaticInfo*)Unsafe.AsPointer(ref this), callerHnd, declaredCalleeHnd, exactCalleeHnd, fIsTailPrefix);

    public void reportTailCallDecision(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd, bool fIsTailPrefix, CorInfoTailCall tailCallResult, sbyte* reason) => lpVtbl->reportTailCallDecision((ICorStaticInfo*)Unsafe.AsPointer(ref this), callerHnd, calleeHnd, fIsTailPrefix, tailCallResult, reason);

    public void getEHinfo(CORINFO_METHOD_HANDLE ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause) => lpVtbl->getEHinfo((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, EHnumber, clause);

    public CORINFO_CLASS_HANDLE getMethodClass(CORINFO_METHOD_HANDLE method) => lpVtbl->getMethodClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), method);

    public void getMethodVTableOffset(CORINFO_METHOD_HANDLE method, uint* offsetOfIndirection, uint* offsetAfterIndirection, bool* isRelative) => lpVtbl->getMethodVTableOffset((ICorStaticInfo*)Unsafe.AsPointer(ref this), method, offsetOfIndirection, offsetAfterIndirection, isRelative);

    public bool resolveVirtualMethod(CORINFO_DEVIRTUALIZATION_INFO* info) => lpVtbl->resolveVirtualMethod((ICorStaticInfo*)Unsafe.AsPointer(ref this), info);

    public CORINFO_METHOD_HANDLE getUnboxedEntry(CORINFO_METHOD_HANDLE ftn, bool* requiresInstMethodTableArg) => lpVtbl->getUnboxedEntry((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, requiresInstMethodTableArg);

    public CORINFO_CLASS_HANDLE getDefaultComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->getDefaultComparerClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), elemType);

    public CORINFO_CLASS_HANDLE getDefaultEqualityComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->getDefaultEqualityComparerClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), elemType);

    public void expandRawHandleIntrinsic(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_GENERICHANDLE_RESULT* pResult) => lpVtbl->expandRawHandleIntrinsic((ICorStaticInfo*)Unsafe.AsPointer(ref this), pResolvedToken, pResult);

    public bool isIntrinsicType(CORINFO_CLASS_HANDLE classHnd) => lpVtbl->isIntrinsicType((ICorStaticInfo*)Unsafe.AsPointer(ref this), classHnd);

    public CorInfoCallConvExtension getUnmanagedCallConv(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig, bool* pSuppressGCTransition) => lpVtbl->getUnmanagedCallConv((ICorStaticInfo*)Unsafe.AsPointer(ref this), method, callSiteSig, pSuppressGCTransition);

    public bool pInvokeMarshalingRequired(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig) => lpVtbl->pInvokeMarshalingRequired((ICorStaticInfo*)Unsafe.AsPointer(ref this), method, callSiteSig);

    public bool satisfiesMethodConstraints(CORINFO_CLASS_HANDLE parent, CORINFO_METHOD_HANDLE method) => lpVtbl->satisfiesMethodConstraints((ICorStaticInfo*)Unsafe.AsPointer(ref this), parent, method);

    public void methodMustBeLoadedBeforeCodeIsRun(CORINFO_METHOD_HANDLE method) => lpVtbl->methodMustBeLoadedBeforeCodeIsRun((ICorStaticInfo*)Unsafe.AsPointer(ref this), method);

    public CORINFO_METHOD_HANDLE mapMethodDeclToMethodImpl(CORINFO_METHOD_HANDLE method) => lpVtbl->mapMethodDeclToMethodImpl((ICorStaticInfo*)Unsafe.AsPointer(ref this), method);

    public void getGSCookie(GSCookie* pCookieVal, GSCookie** ppCookieVal) => lpVtbl->getGSCookie((ICorStaticInfo*)Unsafe.AsPointer(ref this), pCookieVal, ppCookieVal);

    public void setPatchpointInfo(PatchpointInfo* patchpointInfo) => lpVtbl->setPatchpointInfo((ICorStaticInfo*)Unsafe.AsPointer(ref this), patchpointInfo);

    public PatchpointInfo* getOSRInfo(uint* ilOffset) => lpVtbl->getOSRInfo((ICorStaticInfo*)Unsafe.AsPointer(ref this), ilOffset);

    //
    // ICorModuleInfo
    //

    public void resolveToken(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->resolveToken((ICorStaticInfo*)Unsafe.AsPointer(ref this), pResolvedToken);

    public void findSig(CORINFO_MODULE_HANDLE module, uint sigTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->findSig((ICorStaticInfo*)Unsafe.AsPointer(ref this), module, sigTOK, context, sig);

    public void findCallSiteSig(CORINFO_MODULE_HANDLE module, uint methTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->findCallSiteSig((ICorStaticInfo*)Unsafe.AsPointer(ref this), module, methTOK, context, sig);

    public CORINFO_CLASS_HANDLE getTokenTypeAsHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->getTokenTypeAsHandle((ICorStaticInfo*)Unsafe.AsPointer(ref this), pResolvedToken);

    public int getStringLiteral(CORINFO_MODULE_HANDLE module, uint metaTOK, char* buffer, int bufferSize, int startIndex = 0) => lpVtbl->getStringLiteral((ICorStaticInfo*)Unsafe.AsPointer(ref this), module, metaTOK, buffer, bufferSize, 0);

    public nuint printObjectDescription(CORINFO_OBJECT_HANDLE handle, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printObjectDescription((ICorStaticInfo*)Unsafe.AsPointer(ref this), handle, buffer, bufferSize, null);

    //
    // ICorClassInfo
    //

    public CorInfoType asCorInfoType(CORINFO_CLASS_HANDLE cls) => lpVtbl->asCorInfoType((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public sbyte* getClassNameFromMetadata(CORINFO_CLASS_HANDLE cls, sbyte** namespaceName) => lpVtbl->getClassNameFromMetadata((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls, namespaceName);

    public CORINFO_CLASS_HANDLE getTypeInstantiationArgument(CORINFO_CLASS_HANDLE cls, uint index) => lpVtbl->getTypeInstantiationArgument((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls, index);

    public nuint printClassName(CORINFO_CLASS_HANDLE cls, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printClassName((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls, buffer, bufferSize, null);

    public bool isValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->isValueClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getClassAttribs(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassAttribs((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_MODULE_HANDLE getClassModule(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassModule((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_ASSEMBLY_HANDLE getModuleAssembly(CORINFO_MODULE_HANDLE mod) => lpVtbl->getModuleAssembly((ICorStaticInfo*)Unsafe.AsPointer(ref this), mod);

    public sbyte* getAssemblyName(CORINFO_ASSEMBLY_HANDLE assem) => lpVtbl->getAssemblyName((ICorStaticInfo*)Unsafe.AsPointer(ref this), assem);

    public void* LongLifetimeMalloc(nuint sz) => lpVtbl->LongLifetimeMalloc((ICorStaticInfo*)Unsafe.AsPointer(ref this), sz);

    public void LongLifetimeFree(void* obj) => lpVtbl->LongLifetimeFree((ICorStaticInfo*)Unsafe.AsPointer(ref this), obj);

    public nuint getClassModuleIdForStatics(CORINFO_CLASS_HANDLE cls, CORINFO_MODULE_HANDLE* pModule, void** ppIndirection) => lpVtbl->getClassModuleIdForStatics((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls, pModule, ppIndirection);

    public bool getIsClassInitedFlagAddress(CORINFO_CLASS_HANDLE cls, CORINFO_CONST_LOOKUP* addr, int* offset) => lpVtbl->getIsClassInitedFlagAddress((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls, addr, offset);

    public bool getStaticBaseAddress(CORINFO_CLASS_HANDLE cls, bool isGc, CORINFO_CONST_LOOKUP* addr) => lpVtbl->getStaticBaseAddress((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls, isGc, addr);

    public uint getClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassSize((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getHeapClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->getHeapClassSize((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public bool canAllocateOnStack(CORINFO_CLASS_HANDLE cls) => lpVtbl->canAllocateOnStack((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getClassAlignmentRequirement(CORINFO_CLASS_HANDLE cls, bool fDoubleAlignHint = false) => lpVtbl->getClassAlignmentRequirement((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls, false);

    public uint getClassGClayout(CORINFO_CLASS_HANDLE cls, byte* gcPtrs) => lpVtbl->getClassGClayout((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls, gcPtrs);

    public uint getClassNumInstanceFields(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassNumInstanceFields((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_FIELD_HANDLE getFieldInClass(CORINFO_CLASS_HANDLE clsHnd, int num) => lpVtbl->getFieldInClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), clsHnd, num);

    public GetTypeLayoutResult getTypeLayout(CORINFO_CLASS_HANDLE typeHnd, CORINFO_TYPE_LAYOUT_NODE* treeNodes, nuint* numTreeNodes) => lpVtbl->getTypeLayout((ICorStaticInfo*)Unsafe.AsPointer(ref this), typeHnd, treeNodes, numTreeNodes);

    public bool checkMethodModifier(CORINFO_METHOD_HANDLE hMethod, sbyte* modifier, bool fOptional) => lpVtbl->checkMethodModifier((ICorStaticInfo*)Unsafe.AsPointer(ref this), hMethod, modifier, fOptional);

    public CorInfoHelpFunc getNewHelper(CORINFO_CLASS_HANDLE classHandle, bool* pHasSideEffects) => lpVtbl->getNewHelper((ICorStaticInfo*)Unsafe.AsPointer(ref this), classHandle, pHasSideEffects);

    public CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_HANDLE arrayCls) => lpVtbl->getNewArrHelper((ICorStaticInfo*)Unsafe.AsPointer(ref this), arrayCls);

    public CorInfoHelpFunc getCastingHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fThrowing) => lpVtbl->getCastingHelper((ICorStaticInfo*)Unsafe.AsPointer(ref this), pResolvedToken, fThrowing);

    public CorInfoHelpFunc getSharedCCtorHelper(CORINFO_CLASS_HANDLE clsHnd) => lpVtbl->getSharedCCtorHelper((ICorStaticInfo*)Unsafe.AsPointer(ref this), clsHnd);

    public CORINFO_CLASS_HANDLE getTypeForBox(CORINFO_CLASS_HANDLE cls) => lpVtbl->getTypeForBox((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoHelpFunc getBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->getBoxHelper((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoHelpFunc getUnBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->getUnBoxHelper((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_OBJECT_HANDLE getRuntimeTypePointer(CORINFO_CLASS_HANDLE cls) => lpVtbl->getRuntimeTypePointer((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public bool isObjectImmutable(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->isObjectImmutable((ICorStaticInfo*)Unsafe.AsPointer(ref this), objPtr);

    public bool getStringChar(CORINFO_OBJECT_HANDLE strObj, int index, ushort* value) => lpVtbl->getStringChar((ICorStaticInfo*)Unsafe.AsPointer(ref this), strObj, index, value);

    public CORINFO_CLASS_HANDLE getObjectType(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->getObjectType((ICorStaticInfo*)Unsafe.AsPointer(ref this), objPtr);

    public bool getReadyToRunHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_LOOKUP_KIND* pGenericLookupKind, CorInfoHelpFunc id, CORINFO_CONST_LOOKUP* pLookup) => lpVtbl->getReadyToRunHelper((ICorStaticInfo*)Unsafe.AsPointer(ref this), pResolvedToken, pGenericLookupKind, id, pLookup);

    public void getReadyToRunDelegateCtorHelper(CORINFO_RESOLVED_TOKEN* pTargetMethod, mdToken targetConstraint, CORINFO_CLASS_HANDLE delegateType, CORINFO_LOOKUP* pLookup) => lpVtbl->getReadyToRunDelegateCtorHelper((ICorStaticInfo*)Unsafe.AsPointer(ref this), pTargetMethod, targetConstraint, delegateType, pLookup);

    public CorInfoInitClassResult initClass(CORINFO_FIELD_HANDLE field, CORINFO_METHOD_HANDLE method, CORINFO_CONTEXT_HANDLE context) => lpVtbl->initClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), field, method, context);

    public void classMustBeLoadedBeforeCodeIsRun(CORINFO_CLASS_HANDLE cls) => lpVtbl->classMustBeLoadedBeforeCodeIsRun((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_CLASS_HANDLE getBuiltinClass(CorInfoClassId classId) => lpVtbl->getBuiltinClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), classId);

    public CorInfoType getTypeForPrimitiveValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->getTypeForPrimitiveValueClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoType getTypeForPrimitiveNumericClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->getTypeForPrimitiveNumericClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public bool canCast(CORINFO_CLASS_HANDLE child, CORINFO_CLASS_HANDLE parent) => lpVtbl->canCast((ICorStaticInfo*)Unsafe.AsPointer(ref this), child, parent);

    public TypeCompareState compareTypesForCast(CORINFO_CLASS_HANDLE fromClass, CORINFO_CLASS_HANDLE toClass) => lpVtbl->compareTypesForCast((ICorStaticInfo*)Unsafe.AsPointer(ref this), fromClass, toClass);

    public TypeCompareState compareTypesForEquality(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->compareTypesForEquality((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls1, cls2);

    public bool isMoreSpecificType(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->isMoreSpecificType((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls1, cls2);

    public bool isExactType(CORINFO_CLASS_HANDLE cls) => lpVtbl->isExactType((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public TypeCompareState isEnum(CORINFO_CLASS_HANDLE cls, CORINFO_CLASS_HANDLE* underlyingType) => lpVtbl->isEnum((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls, underlyingType);

    public CORINFO_CLASS_HANDLE getParentType(CORINFO_CLASS_HANDLE cls) => lpVtbl->getParentType((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoType getChildType(CORINFO_CLASS_HANDLE clsHnd, CORINFO_CLASS_HANDLE* clsRet) => lpVtbl->getChildType((ICorStaticInfo*)Unsafe.AsPointer(ref this), clsHnd, clsRet);

    public bool isSDArray(CORINFO_CLASS_HANDLE cls) => lpVtbl->isSDArray((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getArrayRank(CORINFO_CLASS_HANDLE cls) => lpVtbl->getArrayRank((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoArrayIntrinsic getArrayIntrinsicID(CORINFO_METHOD_HANDLE ftn) => lpVtbl->getArrayIntrinsicID((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn);

    public void* getArrayInitializationData(CORINFO_FIELD_HANDLE field, uint size) => lpVtbl->getArrayInitializationData((ICorStaticInfo*)Unsafe.AsPointer(ref this), field, size);

    public CorInfoIsAccessAllowedResult canAccessClass(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_HELPER_DESC* pAccessHelper) => lpVtbl->canAccessClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), pResolvedToken, callerHandle, pAccessHelper);

    //
    // ICorFieldInfo
    //

    public nuint printFieldName(CORINFO_FIELD_HANDLE field, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printFieldName((ICorStaticInfo*)Unsafe.AsPointer(ref this), field, buffer, bufferSize, null);

    public CORINFO_CLASS_HANDLE getFieldClass(CORINFO_FIELD_HANDLE field) => lpVtbl->getFieldClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), field);

    public CorInfoType getFieldType(CORINFO_FIELD_HANDLE field, CORINFO_CLASS_HANDLE* structType = null, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->getFieldType((ICorStaticInfo*)Unsafe.AsPointer(ref this), field, null, null);

    public uint getFieldOffset(CORINFO_FIELD_HANDLE field) => lpVtbl->getFieldOffset((ICorStaticInfo*)Unsafe.AsPointer(ref this), field);

    public void getFieldInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_ACCESS_FLAGS flags, CORINFO_FIELD_INFO* pResult) => lpVtbl->getFieldInfo((ICorStaticInfo*)Unsafe.AsPointer(ref this), pResolvedToken, callerHandle, flags, pResult);

    public uint getThreadLocalFieldInfo(CORINFO_FIELD_HANDLE field, bool isGCType) => lpVtbl->getThreadLocalFieldInfo((ICorStaticInfo*)Unsafe.AsPointer(ref this), field, isGCType);

    public void getThreadLocalStaticBlocksInfo(CORINFO_THREAD_STATIC_BLOCKS_INFO* pInfo, bool isGCType) => lpVtbl->getThreadLocalStaticBlocksInfo((ICorStaticInfo*)Unsafe.AsPointer(ref this), pInfo, isGCType);

    public void getThreadLocalStaticInfo_NativeAOT(CORINFO_THREAD_STATIC_INFO_NATIVEAOT* pInfo) => lpVtbl->getThreadLocalStaticInfo_NativeAOT((ICorStaticInfo*)Unsafe.AsPointer(ref this), pInfo);

    public bool isFieldStatic(CORINFO_FIELD_HANDLE fldHnd) => lpVtbl->isFieldStatic((ICorStaticInfo*)Unsafe.AsPointer(ref this), fldHnd);

    public int getArrayOrStringLength(CORINFO_OBJECT_HANDLE objHnd) => lpVtbl->getArrayOrStringLength((ICorStaticInfo*)Unsafe.AsPointer(ref this), objHnd);

    //
    // ICorDebugInfo
    //

    public void getBoundaries(CORINFO_METHOD_HANDLE ftn, uint* cILOffsets, uint** pILOffsets, ICorDebugInfo.BoundaryTypes* implicitBoundaries) => lpVtbl->getBoundaries((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, cILOffsets, pILOffsets, implicitBoundaries);

    public void setBoundaries(CORINFO_METHOD_HANDLE ftn, uint cMap, ICorDebugInfo.OffsetMapping* pMap) => lpVtbl->setBoundaries((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, cMap, pMap);

    public void getVars(CORINFO_METHOD_HANDLE ftn, uint* cVars, ICorDebugInfo.ILVarInfo** vars, bool* extendOthers) => lpVtbl->getVars((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, cVars, vars, extendOthers);

    public void setVars(CORINFO_METHOD_HANDLE ftn, uint cVars, ICorDebugInfo.NativeVarInfo* vars) => lpVtbl->setVars((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, cVars, vars);

    public void reportRichMappings(ICorDebugInfo.InlineTreeNode* inlineTreeNodes, uint numInlineTreeNodes, ICorDebugInfo.RichOffsetMapping* mappings, uint numMappings) => lpVtbl->reportRichMappings((ICorStaticInfo*)Unsafe.AsPointer(ref this), inlineTreeNodes, numInlineTreeNodes, mappings, numMappings);

    //
    // Misc
    //

    public void* allocateArray(nuint cBytes) => lpVtbl->allocateArray((ICorStaticInfo*)Unsafe.AsPointer(ref this), cBytes);

    public void freeArray(void* array) => lpVtbl->freeArray((ICorStaticInfo*)Unsafe.AsPointer(ref this), array);

    //
    // ICorArgInfo
    //

    public CORINFO_ARG_LIST_HANDLE getArgNext(CORINFO_ARG_LIST_HANDLE args) => lpVtbl->getArgNext((ICorStaticInfo*)Unsafe.AsPointer(ref this), args);

    public CorInfoTypeWithMod getArgType(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args, CORINFO_CLASS_HANDLE* vcTypeRet) => lpVtbl->getArgType((ICorStaticInfo*)Unsafe.AsPointer(ref this), sig, args, vcTypeRet);

    public int getExactClasses(CORINFO_CLASS_HANDLE baseType, int maxExactClasses, CORINFO_CLASS_HANDLE* exactClsRet) => lpVtbl->getExactClasses((ICorStaticInfo*)Unsafe.AsPointer(ref this), baseType, maxExactClasses, exactClsRet);

    public CORINFO_CLASS_HANDLE getArgClass(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args) => lpVtbl->getArgClass((ICorStaticInfo*)Unsafe.AsPointer(ref this), sig, args);

    public CorInfoHFAElemType getHFAType(CORINFO_CLASS_HANDLE hClass) => lpVtbl->getHFAType((ICorStaticInfo*)Unsafe.AsPointer(ref this), hClass);

    public bool runWithErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->runWithErrorTrap((ICorStaticInfo*)Unsafe.AsPointer(ref this), function, parameter);

    public bool runWithSPMIErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->runWithSPMIErrorTrap((ICorStaticInfo*)Unsafe.AsPointer(ref this), function, parameter);

    public void getEEInfo(CORINFO_EE_INFO* pEEInfoOut) => lpVtbl->getEEInfo((ICorStaticInfo*)Unsafe.AsPointer(ref this), pEEInfoOut);

    public char* getJitTimeLogFilename() => lpVtbl->getJitTimeLogFilename((ICorStaticInfo*)Unsafe.AsPointer(ref this));

    //
    // Diagnostic methods
    //

    public mdMethodDef getMethodDefFromMethod(CORINFO_METHOD_HANDLE hMethod) => lpVtbl->getMethodDefFromMethod((ICorStaticInfo*)Unsafe.AsPointer(ref this), hMethod);

    public nuint printMethodName(CORINFO_METHOD_HANDLE ftn, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printMethodName((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, buffer, bufferSize, null);

    public sbyte* getMethodNameFromMetadata(CORINFO_METHOD_HANDLE ftn, sbyte** className, sbyte** namespaceName, sbyte** enclosingClassName) => lpVtbl->getMethodNameFromMetadata((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn, className, namespaceName, enclosingClassName);

    public uint getMethodHash(CORINFO_METHOD_HANDLE ftn) => lpVtbl->getMethodHash((ICorStaticInfo*)Unsafe.AsPointer(ref this), ftn);

    public bool getSystemVAmd64PassStructInRegisterDescriptor(CORINFO_CLASS_HANDLE structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr) => lpVtbl->getSystemVAmd64PassStructInRegisterDescriptor((ICorStaticInfo*)Unsafe.AsPointer(ref this), structHnd, structPassInRegDescPtr);

    public uint getLoongArch64PassStructInRegisterFlags(CORINFO_CLASS_HANDLE cls) => lpVtbl->getLoongArch64PassStructInRegisterFlags((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getRISCV64PassStructInRegisterFlags(CORINFO_CLASS_HANDLE cls) => lpVtbl->getRISCV64PassStructInRegisterFlags((ICorStaticInfo*)Unsafe.AsPointer(ref this), cls);

    public interface Interface
    {
        //
        // ICorMethodInfo
        //

        // Quick check whether the method is a jit intrinsic. Returns the same value as getMethodAttribs(ftn) & CORINFO_FLG_INTRINSIC, except faster.
        bool isIntrinsic(CORINFO_METHOD_HANDLE ftn);

        // Notify EE about intent to rely on given MethodInfo in the current method
        // EE returns false if we're not allowed to do so and the methodinfo may change.
        // Example of a scenario addressed by notifyMethodInfoUsage:
        //  1) Crossgen (with --opt-cross-module=MyLib) attempts to inline a call from MyLib.dll into MyApp.dll
        //     and realizes that the call always throws.
        //  2) JIT aborts the inlining attempt and marks the call as no-return instead. The code that follows the call is 
        //     replaced with a breakpoint instruction that is expected to be unreachable.
        //  3) MyLib is updated to a new version so it's no longer within the same version bubble with MyApp.dll
        //     and the new version of the call no longer throws and does some work.
        //  4) The breakpoint instruction is now reachable in the MyApp.dll.
        bool notifyMethodInfoUsage(CORINFO_METHOD_HANDLE ftn);

        // return flags (a bitfield of CorInfoFlags values)
        uint getMethodAttribs(CORINFO_METHOD_HANDLE ftn);

        // sets private JIT flags, which can be, retrieved using getAttrib.
        void setMethodAttribs(CORINFO_METHOD_HANDLE ftn, CorInfoMethodRuntimeFlags attribs);

        // Given a method descriptor ftnHnd, extract signature information into sigInfo
        //
        // 'memberParent' is typically only set when verifying.  It should be the
        // result of calling getMemberParent.
        void getMethodSig(CORINFO_METHOD_HANDLE ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_HANDLE memberParent = null);

        /*********************************************************************
         * Note the following methods can only be used on functions known
         * to be IL.  This includes the method being compiled and any method
         * that 'getMethodInfo' returns true for
         *********************************************************************/

        // return information about a method private to the implementation
        //      returns false if method is not IL, or is otherwise unavailable.
        //      This method is used to fetch data needed to inline functions
        bool getMethodInfo(CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_INFO* info, CORINFO_CONTEXT_HANDLE context = null);

        //------------------------------------------------------------------------------
        // haveSameMethodDefinition: Check if two method handles have the same
        // method definition.
        //
        // Arguments:
        //    meth1 - First method handle
        //    meth2 - Second method handle
        //
        // Return Value:
        //   True if the methods share definitions.
        //
        // Remarks:
        //   For example, Foo<int> and Foo<uint> have different method handles but
        //   share the same method definition.
        bool haveSameMethodDefinition(CORINFO_METHOD_HANDLE meth1Hnd, CORINFO_METHOD_HANDLE meth2Hnd);

        // Decides if you have any limitations for inlining. If everything's OK, it will return
        // INLINE_PASS.
        //
        // The callerHnd must be the immediate caller (i.e. when we have a chain of inlined calls)
        CorInfoInline canInline(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd);

        // Report that an inlining related process has begun. This will always be paired with
        // a call to reportInliningDecision unless the jit fails.
        void beginInlining(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd);

        // Reports whether or not a method can be inlined, and why.  canInline is responsible for reporting all
        // inlining results when it returns INLINE_FAIL and INLINE_NEVER.  All other results are reported by the
        // JIT.
        void reportInliningDecision(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd, CorInfoInline inlineResult, sbyte* reason);

        // Returns false if the call is across security boundaries thus we cannot tailcall
        //
        // The callerHnd must be the immediate caller (i.e. when we have a chain of inlined calls)
        bool canTailCall(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE declaredCalleeHnd, CORINFO_METHOD_HANDLE exactCalleeHnd, bool fIsTailPrefix);

        // Reports whether or not a method can be tail called, and why.
        // canTailCall is responsible for reporting all results when it returns
        // false.  All other results are reported by the JIT.
        void reportTailCallDecision(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd, bool fIsTailPrefix, CorInfoTailCall tailCallResult, sbyte* reason);

        // get individual exception handler
        void getEHinfo(CORINFO_METHOD_HANDLE ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause);

        // return class it belongs to
        CORINFO_CLASS_HANDLE getMethodClass(CORINFO_METHOD_HANDLE method);

        // This function returns the offset of the specified method in the
        // vtable of it's owning class or interface.
        void getMethodVTableOffset(CORINFO_METHOD_HANDLE method, uint* offsetOfIndirection, uint* offsetAfterIndirection, bool* isRelative);

        // Finds the virtual method in info->objClass that overrides info->virtualMethod,
        // or the method in info->objClass that implements the interface method
        // represented by info->virtualMethod.
        //
        // Returns false if devirtualization is not possible.
        bool resolveVirtualMethod(CORINFO_DEVIRTUALIZATION_INFO* info);

        // Get the unboxed entry point for a method, if possible.
        CORINFO_METHOD_HANDLE getUnboxedEntry(CORINFO_METHOD_HANDLE ftn, bool* requiresInstMethodTableArg);

        // Given T, return the type of the default Comparer<T>.
        // Returns null if the type can't be determined exactly.
        CORINFO_CLASS_HANDLE getDefaultComparerClass(CORINFO_CLASS_HANDLE elemType);

        // Given T, return the type of the default EqualityComparer<T>.
        // Returns null if the type can't be determined exactly.
        CORINFO_CLASS_HANDLE getDefaultEqualityComparerClass(CORINFO_CLASS_HANDLE elemType);

        // Given resolved token that corresponds to an intrinsic classified to
        // get a raw handle (NI_System_Activator_AllocatorOf etc.), fetch the
        // handle associated with the token. If this is not possible at
        // compile-time (because the current method's code is shared and the
        // token contains generic parameters) then indicate how the handle
        // should be looked up at runtime.
        void expandRawHandleIntrinsic(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_GENERICHANDLE_RESULT* pResult);

        // Is the given type in System.Private.Corelib and marked with IntrinsicAttribute?
        // This defaults to false.
        bool isIntrinsicType(CORINFO_CLASS_HANDLE classHnd) => false;

        // return the entry point calling convention for any of the following
        // - a P/Invoke
        // - a method marked with UnmanagedCallersOnly
        // - a function pointer with the CORINFO_CALLCONV_UNMANAGED calling convention.
        CorInfoCallConvExtension getUnmanagedCallConv(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig, bool* pSuppressGCTransition);

        // return if any marshaling is required for PInvoke methods.  Note that
        // method == 0 => calli.  The call site sig is only needed for the varargs or calli case
        bool pInvokeMarshalingRequired(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig);

        // Check constraints on method type arguments (only).
        bool satisfiesMethodConstraints(CORINFO_CLASS_HANDLE parent, CORINFO_METHOD_HANDLE method);

        // load and restore the method
        void methodMustBeLoadedBeforeCodeIsRun(CORINFO_METHOD_HANDLE method);

        CORINFO_METHOD_HANDLE mapMethodDeclToMethodImpl(CORINFO_METHOD_HANDLE method);

        // Returns the global cookie for the /GS unsafe buffer checks
        // The cookie might be a constant value (JIT), or a handle to memory location (Ngen)
        void getGSCookie(GSCookie* pCookieVal, GSCookie** ppCookieVal);

        // Provide patchpoint info for the method currently being jitted.
        void setPatchpointInfo(PatchpointInfo* patchpointInfo);

        // Get patchpoint info and il offset for the method currently being jitted.
        PatchpointInfo* getOSRInfo(uint* ilOffset);

        //
        // ICorModuleInfo
        //

        // Resolve metadata token into runtime method handles. This function may not
        // return normally (e.g. it may throw) if it encounters invalid metadata or other
        // failures during token resolution.
        void resolveToken(CORINFO_RESOLVED_TOKEN* pResolvedToken);

        // Signature information about the call sig
        void findSig(CORINFO_MODULE_HANDLE module, uint sigTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig);

        // for Varargs, the signature at the call site may differ from
        // the signature at the definition.  Thus we need a way of
        // fetching the call site information
        void findCallSiteSig(CORINFO_MODULE_HANDLE module, uint methTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig);

        CORINFO_CLASS_HANDLE getTokenTypeAsHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken);

        // Returns (sub)string length and content (can be null for dynamic context)
        // for given metaTOK and module, length `-1` means input is incorrect
        int getStringLiteral(CORINFO_MODULE_HANDLE module, uint metaTOK, char* buffer, int bufferSize, int startIndex = 0);

        //------------------------------------------------------------------------------
        // printObjectDescription: Prints a (possibly truncated) textual UTF8 representation of the given
        //    object to a preallocated buffer. It's intended to be used only for debug/diagnostic
        //    purposes such as JitDisasm. The buffer is null-terminated (even if truncated).
        //
        // Arguments:
        //    handle     -          Direct object handle
        //    buffer     -          Pointer to buffer. Can be null.
        //    bufferSize -          Buffer size (in bytes).
        //    pRequiredBufferSize - Full length of the textual UTF8 representation, in bytes.
        //                          Includes the null terminator, so the value is always at least 1,
        //                          where 1 indicates an empty string.
        //                          Can be used to call this API again with a bigger buffer to get the full
        //                          string.
        //
        // Return Value:
        //    Bytes written to the buffer, excluding the null terminator. The range is [0..bufferSize).
        //    If bufferSize is 0, returns 0.
        //
        // Remarks:
        //    buffer and bufferSize can be respectively null and 0 to query just the required buffer size.
        //
        //    If the return value is less than bufferSize - 1 then the full string was written. In this case
        //    it is guaranteed that return value == *pRequiredBufferSize - 1.
        //
        nuint printObjectDescription(CORINFO_OBJECT_HANDLE handle, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null);

        //
        // ICorClassInfo
        //

        // If the value class 'cls' is isomorphic to a primitive type it will
        // return that type, otherwise it will return CORINFO_TYPE_VALUECLASS
        CorInfoType asCorInfoType(CORINFO_CLASS_HANDLE cls);

        // Return class name as in metadata, or null if there is none.
        // Suitable for non-debugging use.
        sbyte* getClassNameFromMetadata(CORINFO_CLASS_HANDLE cls, sbyte** namespaceName);

        // Return the type argument of the instantiated generic class,
        // which is specified by the index
        CORINFO_CLASS_HANDLE getTypeInstantiationArgument(CORINFO_CLASS_HANDLE cls, uint index);

        // Prints the name for a specified class including namespaces and enclosing
        // classes.
        // See printObjectDescription for documentation for the parameters.
        nuint printClassName(CORINFO_CLASS_HANDLE cls, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null);

        // Quick check whether the type is a value class. Returns the same value as getClassAttribs(cls) & CORINFO_FLG_VALUECLASS, except faster.
        bool isValueClass(CORINFO_CLASS_HANDLE cls);

        // return flags (a bitfield of CorInfoFlags values)
        uint getClassAttribs(CORINFO_CLASS_HANDLE cls);

        CORINFO_MODULE_HANDLE getClassModule(CORINFO_CLASS_HANDLE cls);

        // Returns the assembly that contains the module "mod".
        CORINFO_ASSEMBLY_HANDLE getModuleAssembly(CORINFO_MODULE_HANDLE mod);

        // Returns the name of the assembly "assem".
        sbyte* getAssemblyName(CORINFO_ASSEMBLY_HANDLE assem);

        // Allocate and delete process-lifetime objects.  Should only be
        // referred to from static fields, lest a leak occur.
        // Note that "LongLifetimeFree" does not execute destructors, if "obj"
        // is an array of a struct type with a destructor.
        void* LongLifetimeMalloc(nuint sz);

        void LongLifetimeFree(void* obj);

        nuint getClassModuleIdForStatics(CORINFO_CLASS_HANDLE cls, CORINFO_MODULE_HANDLE* pModule, void** ppIndirection);

        bool getIsClassInitedFlagAddress(CORINFO_CLASS_HANDLE cls, CORINFO_CONST_LOOKUP* addr, int* offset);

        bool getStaticBaseAddress(CORINFO_CLASS_HANDLE cls, bool isGc, CORINFO_CONST_LOOKUP* addr);

        // return the number of bytes needed by an instance of the class
        uint getClassSize(CORINFO_CLASS_HANDLE cls);

        // return the number of bytes needed by an instance of the class allocated on the heap
        uint getHeapClassSize(CORINFO_CLASS_HANDLE cls);

        bool canAllocateOnStack(CORINFO_CLASS_HANDLE cls);

        uint getClassAlignmentRequirement(CORINFO_CLASS_HANDLE cls, bool fDoubleAlignHint = false);

        // This is only called for Value classes.  It returns a boolean array
        // in representing of 'cls' from a GC perspective.  The class is
        // assumed to be an array of machine words
        // (of length // getClassSize(cls) / TARGET_POINTER_SIZE),
        // 'gcPtrs' is a pointer to an array of uint8_ts of this length.
        // getClassGClayout fills in this array so that gcPtrs[i] is set
        // to one of the CorInfoGCType values which is the GC type of
        // the i-th machine word of an object of type 'cls'
        // returns the number of GC pointers in the array
        uint getClassGClayout(CORINFO_CLASS_HANDLE cls, byte* gcPtrs);

        // returns the number of instance fields in a class
        uint getClassNumInstanceFields(CORINFO_CLASS_HANDLE cls);

        CORINFO_FIELD_HANDLE getFieldInClass(CORINFO_CLASS_HANDLE clsHnd, int num);

        //------------------------------------------------------------------------------
        // getTypeLayout: Obtain a tree describing the layout of a type.
        //
        // Parameters:
        //   typeHnd            - Handle of the type.
        //   treeNodes          - [in, out] Pointer to tree node entries to write.
        //   numTreeNodes       - [in, out] Size of 'treeNodes' on entry. Updated to contain
        //                         the number of entries written in 'treeNodes'.
        //
        // Returns:
        //   A result indicating whether the type layout was successfully
        //   retrieved and whether the result is partial or not.
        //
        // Remarks:
        //   The type layout should be stored in preorder in 'treeNodes': the root
        //   node is always at index 0, and the first child of any node is at its
        //   own index + 1. The fields returned are NOT guaranteed to be ordered
        //   by offset.
        //
        //   SIMD and HW SIMD types are returned as a single entry without any
        //   children. For those, CORINFO_TYPE_LAYOUT_NODE::simdTypeHnd is set, but
        //   can only be used in a very restricted capacity, see
        //   CORINFO_TYPE_LAYOUT_NODE. Note that this special treatment is only for
        //   fields; if typeHnd itself is a SIMD type this function will treat it
        //   like a normal struct type and expand its fields.
        //
        //   IMPORTANT: except for GC pointers the fields returned to the JIT by
        //   this function should be considered as a hint only. The JIT CANNOT make
        //   assumptions in its codegen that the specified fields are actually part
        //   of the type when the code finally runs. This means the JIT should not
        //   make optimizations based on the field information returned by this
        //   function that would break if those fields were removed or shifted
        //   around.
        //
        GetTypeLayoutResult getTypeLayout(CORINFO_CLASS_HANDLE typeHnd, CORINFO_TYPE_LAYOUT_NODE* treeNodes, nuint* numTreeNodes);

        bool checkMethodModifier(CORINFO_METHOD_HANDLE hMethod, sbyte* modifier, bool fOptional);

        //------------------------------------------------------------------------------
        // getNewHelper: Returns the allocation helper optimized for a specific class.
        //
        // Parameters:
        //   classHandle     - Handle of the type.
        //   pHasSideEffects - [out] Whether or not the allocation of the specified
        //                     type can have user-visible side effects; for example,
        //                     because a finalizer may run as a result.
        //
        // Returns:
        //   Helper to call to allocate the specified type.
        //
        CorInfoHelpFunc getNewHelper(CORINFO_CLASS_HANDLE classHandle, bool* pHasSideEffects);

        // returns the newArr (1-Dim array) helper optimized for "arrayCls."
        CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_HANDLE arrayCls);

        // returns the optimized "IsInstanceOf" or "ChkCast" helper
        CorInfoHelpFunc getCastingHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fThrowing);

        // returns helper to trigger static constructor
        CorInfoHelpFunc getSharedCCtorHelper(CORINFO_CLASS_HANDLE clsHnd);

        // Boxing nullable<T> actually returns a boxed<T> not a boxed Nullable<T>.
        CORINFO_CLASS_HANDLE getTypeForBox(CORINFO_CLASS_HANDLE cls);

        // returns the correct box helper for a particular class.  Note
        // that if this returns CORINFO_HELP_BOX, the JIT can assume
        // 'standard' boxing (allocate object and copy), and optimize
        CorInfoHelpFunc getBoxHelper(CORINFO_CLASS_HANDLE cls);

        // returns the unbox helper.  If 'helperCopies' points to a true
        // value it means the JIT is requesting a helper that unboxes the
        // value into a particular location and thus has the signature
        //     void unboxHelper(void* dest, CORINFO_CLASS_HANDLE cls, Object* obj)
        // Otherwise (it is null or points at a FALSE value) it is requesting
        // a helper that returns a pointer to the unboxed data
        //     void* unboxHelper(CORINFO_CLASS_HANDLE cls, Object* obj)
        // The EE has the option of NOT returning the copy style helper
        // (But must be able to always honor the non-copy style helper)
        // The EE set 'helperCopies' on return to indicate what kind of
        // helper has been created.

        CorInfoHelpFunc getUnBoxHelper(CORINFO_CLASS_HANDLE cls);

        CORINFO_OBJECT_HANDLE getRuntimeTypePointer(CORINFO_CLASS_HANDLE cls);

        //------------------------------------------------------------------------------
        // isObjectImmutable: checks whether given object is known to be immutable or not
        //
        // Arguments:
        //    objPtr - Direct object handle
        //
        // Return Value:
        //    Returns true if object is known to be immutable
        //
        bool isObjectImmutable(CORINFO_OBJECT_HANDLE objPtr);

        //------------------------------------------------------------------------------
        // getStringChar: returns char at the given index if the given object handle
        //    represents String and index is not out of bounds.
        //
        // Arguments:
        //    strObj - object handle
        //    index  - index of the char to return
        //    value  - output char
        //
        // Return Value:
        //    Returns true if value was successfully obtained
        //
        bool getStringChar(CORINFO_OBJECT_HANDLE strObj, int index, ushort* value);

        //------------------------------------------------------------------------------
        // getObjectType: obtains type handle for given object
        //
        // Arguments:
        //    objPtr - Direct object handle
        //
        // Return Value:
        //    Returns CORINFO_CLASS_HANDLE handle that represents given object's type
        //
        CORINFO_CLASS_HANDLE getObjectType(CORINFO_OBJECT_HANDLE objPtr);

        bool getReadyToRunHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_LOOKUP_KIND* pGenericLookupKind, CorInfoHelpFunc id, CORINFO_CONST_LOOKUP* pLookup);

        void getReadyToRunDelegateCtorHelper(CORINFO_RESOLVED_TOKEN* pTargetMethod, mdToken targetConstraint, CORINFO_CLASS_HANDLE delegateType, CORINFO_LOOKUP* pLookup);

        // This function tries to initialize the class (run the class constructor).
        // this function returns whether the JIT must insert helper calls before
        // accessing static field or method.
        //
        // See code:ICorClassInfo#ClassConstruction.
        //
        // field
        //   Non-NULL - inquire about cctor trigger before static field access
        //   NULL - inquire about cctor trigger in method prolog
        //
        // method
        //   Method referencing the field or prolog
        //   NULL - method being compiled
        //
        // context
        //   Exact context of method
        //
        CorInfoInitClassResult initClass(CORINFO_FIELD_HANDLE field, CORINFO_METHOD_HANDLE method, CORINFO_CONTEXT_HANDLE context);

        // This used to be called "loadClass".  This records the fact
        // that the class must be loaded (including restored if necessary) before we execute the
        // code that we are currently generating.  When jitting code
        // the function loads the class immediately.  When zapping code
        // the zapper will if necessary use the call to record the fact that we have
        // to do a fixup/restore before running the method currently being generated.
        //
        // This is typically used to ensure value types are loaded before zapped
        // code that manipulates them is executed, so that the GC can access information
        // about those value types.
        void classMustBeLoadedBeforeCodeIsRun(CORINFO_CLASS_HANDLE cls);

        // returns the class handle for the special builtin classes
        CORINFO_CLASS_HANDLE getBuiltinClass(CorInfoClassId classId);

        // "System.Int32" ==> CORINFO_TYPE_INT..
        CorInfoType getTypeForPrimitiveValueClass(CORINFO_CLASS_HANDLE cls);

        // "System.Int32" ==> CORINFO_TYPE_INT..
        // "System.UInt32" ==> CORINFO_TYPE_UINT..
        CorInfoType getTypeForPrimitiveNumericClass(CORINFO_CLASS_HANDLE cls);

        // `true` if child is a subtype of parent
        // if parent is an interface, then does child implement / extend parent
        //
        // child
        //   subtype (extends parent)
        //
        // parent
        //   base type
        //
        bool canCast(CORINFO_CLASS_HANDLE child, CORINFO_CLASS_HANDLE parent);

        // See if a cast from fromClass to toClass will succeed, fail, or needs
        // to be resolved at runtime.
        TypeCompareState compareTypesForCast(CORINFO_CLASS_HANDLE fromClass, CORINFO_CLASS_HANDLE toClass);

        // See if types represented by cls1 and cls2 compare equal, not
        // equal, or the comparison needs to be resolved at runtime.
        TypeCompareState compareTypesForEquality(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2);

        // Returns true if cls2 is known to be a more specific type
        // than cls1 (a subtype or more restrictive shared type)
        // for purposes of jit type tracking. This is a hint to the
        // jit for optimization; it does not have correctness
        // implications.
        bool isMoreSpecificType(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2);

        // Returns true if a class handle can only describe values of exactly one type.
        bool isExactType(CORINFO_CLASS_HANDLE cls);

        // Returns TypeCompareState::Must if cls is known to be an enum.
        // For enums with known exact type returns the underlying
        // type in underlyingType when the provided pointer is
        // non-NULL.
        // Returns TypeCompareState::May when a runtime check is required.
        TypeCompareState isEnum(CORINFO_CLASS_HANDLE cls, CORINFO_CLASS_HANDLE* underlyingType);

        // Given a class handle, returns the Parent type.
        // For COMObjectType, it returns Class Handle of System.Object.
        // Returns 0 if System.Object is passed in.
        CORINFO_CLASS_HANDLE getParentType(CORINFO_CLASS_HANDLE cls);

        // Returns the CorInfoType of the "child type". If the child type is
        // not a primitive type, *clsRet will be set.
        // Given an Array of Type Foo, returns Foo.
        // Given BYREF Foo, returns Foo
        CorInfoType getChildType(CORINFO_CLASS_HANDLE clsHnd, CORINFO_CLASS_HANDLE* clsRet);

        // Check if this is a single dimensional array type
        bool isSDArray(CORINFO_CLASS_HANDLE cls);

        // Get the number of dimensions in an array
        uint getArrayRank(CORINFO_CLASS_HANDLE cls);

        // Get the index of runtime provided array method
        CorInfoArrayIntrinsic getArrayIntrinsicID(CORINFO_METHOD_HANDLE ftn);

        // Get static field data for an array
        void* getArrayInitializationData(CORINFO_FIELD_HANDLE field, uint size);

        // Check Visibility rules.
        //
        // pAccessHelper
        //   If canAccessMethod returns something other than ALLOWED, then this is filled in.
        CorInfoIsAccessAllowedResult canAccessClass(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_HELPER_DESC* pAccessHelper);

        //
        // ICorFieldInfo
        //

        // Prints the name of a field into a buffer. See printObjectDescription for more documentation.
        nuint printFieldName(CORINFO_FIELD_HANDLE field, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null);

        // return class it belongs to
        CORINFO_CLASS_HANDLE getFieldClass(CORINFO_FIELD_HANDLE field);

        // Return the field's type, if it is CORINFO_TYPE_VALUECLASS 'structType' is set
        // the field's value class (if 'structType' == 0, then don't bother
        // the structure info).
        //
        // 'memberParent' is typically only set when verifying.  It should be the
        // result of calling getMemberParent.
        CorInfoType getFieldType(CORINFO_FIELD_HANDLE field, CORINFO_CLASS_HANDLE* structType = null, CORINFO_CLASS_HANDLE memberParent = null);

        // return the data member's instance offset
        uint getFieldOffset(CORINFO_FIELD_HANDLE field);

        void getFieldInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_ACCESS_FLAGS flags, CORINFO_FIELD_INFO* pResult);

        // Returns the index against which the field's thread static block in stored in TLS.
        uint getThreadLocalFieldInfo(CORINFO_FIELD_HANDLE field, bool isGCType);

        // Returns the thread static block information like offsets, etc. from current TLS.
        void getThreadLocalStaticBlocksInfo(CORINFO_THREAD_STATIC_BLOCKS_INFO* pInfo, bool isGCType);

        void getThreadLocalStaticInfo_NativeAOT(CORINFO_THREAD_STATIC_INFO_NATIVEAOT* pInfo);

        // Returns true iff "fldHnd" represents a static field.
        bool isFieldStatic(CORINFO_FIELD_HANDLE fldHnd);

        // Returns Length of an Array or of a String object, otherwise -1.
        // objHnd must not be null.
        int getArrayOrStringLength(CORINFO_OBJECT_HANDLE objHnd);

        //
        // ICorDebugInfo
        //

        // Query the EE to find out where interesting break points
        // in the code are.  The native compiler will ensure that these places
        // have a corresponding break point in native code.
        //
        // Note that unless CORJIT_FLAG_DEBUG_CODE is specified, this function will
        // be used only as a hint and the native compiler should not change its
        // code generation.
        //
        // ftn
        //   method of interest
        //
        // cILOffsets
        //   size of pILOffsets
        //
        // pILOffsets
        //   IL offsets of interest, jit MUST free with freeArray!
        //
        // implicitBoundaries
        //   tell jit, all boundaries of this type
        //
        void getBoundaries(CORINFO_METHOD_HANDLE ftn, uint* cILOffsets, uint** pILOffsets, ICorDebugInfo.BoundaryTypes* implicitBoundaries);

        // Report back the mapping from IL to native code,
        // this map should include all boundaries that 'getBoundaries'
        // reported as interesting to the debugger.

        // Note that debugger (and profiler) is assuming that all of the
        // offsets form a contiguous block of memory, and that the
        // OffsetMapping is sorted in order of increasing native offset.
        //
        // ftn
        //   method of interest
        //
        // cMap
        //   size of pMap
        //
        // pMap
        //   map including all points of interest. jit allocated with allocateArray, EE frees
        //
        void setBoundaries(CORINFO_METHOD_HANDLE ftn, uint cMap, ICorDebugInfo.OffsetMapping* pMap);

        // Query the EE to find out the scope of local variables.
        // normally the JIT would trash variables after last use, but
        // under debugging, the JIT needs to keep them live over their
        // entire scope so that they can be inspected.
        //
        // Note that unless CORJIT_FLAG_DEBUG_CODE is specified, this function will
        // be used only as a hint and the native compiler should not change its
        // code generation.
        //
        // ftn
        //   method of interest
        //
        // cVars
        //   size of 'vars'
        //
        // vars
        //   scopes of variables of interest. jit MUST free with freeArray!
        //
        // extendOthers
        //   if `true`, then assume the scope of unmentioned vars is entire method
        //
        void getVars(CORINFO_METHOD_HANDLE ftn, uint* cVars, ICorDebugInfo.ILVarInfo** vars, bool* extendOthers);

        // Report back to the EE the location of every variable.
        // note that the JIT might split lifetimes into different
        // locations etc.
        //
        // ftn
        //   method of interest
        //
        // cVars
        //   size of 'vars'
        //
        // vars
        //   map telling where local vars are stored at what points jit allocated with allocateArray, EE frees
        //
        void setVars(CORINFO_METHOD_HANDLE ftn, uint cVars, ICorDebugInfo.NativeVarInfo* vars);

        // Report inline tree and rich offset mappings to EE.
        // The arrays are expected to be allocated with allocateArray
        // and ownership is transferred to the EE with this call.
        //
        // inlineTreeNodes
        //   Nodes of the inline tree
        //
        // numInlineTreeNodes
        //   Number of nodes in the inline tree
        //
        // mappings
        //   Rich mappings
        //
        // numMappings
        //   Number of rich mappings
        //
        void reportRichMappings(ICorDebugInfo.InlineTreeNode* inlineTreeNodes, uint numInlineTreeNodes, ICorDebugInfo.RichOffsetMapping* mappings, uint numMappings);

        //
        // Misc
        //

        // Used to allocate memory that needs to handed to the EE.
        // For eg, use this to allocated memory for reporting debug info,
        // which will be handed to the EE by setVars() and setBoundaries()
        void* allocateArray(nuint cBytes);

        // JitCompiler will free arrays passed by the EE using this
        // For eg, The EE returns memory in getVars() and getBoundaries()
        // to the JitCompiler, which the JitCompiler should release using
        // freeArray()
        void freeArray(void* array);

        //
        // ICorArgInfo
        //

        // advance the pointer to the argument list.
        // a ptr of 0, is special and always means the first argument
        CORINFO_ARG_LIST_HANDLE getArgNext(CORINFO_ARG_LIST_HANDLE args);

        // Get the type of a particular argument
        // CORINFO_TYPE_UNDEF is returned when there are no more arguments
        // If the type returned is a primitive type (or an enum) *vcTypeRet set to NULL
        // otherwise it is set to the TypeHandle associted with the type
        // Enumerations will always look their underlying type (probably should fix this)
        // Otherwise vcTypeRet is the type as would be seen by the IL,
        // The return value is the type that is used for calling convention purposes
        // (Thus if the EE wants a value class to be passed like an int, then it will
        // return CORINFO_TYPE_INT
        CorInfoTypeWithMod getArgType(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args, CORINFO_CLASS_HANDLE* vcTypeRet);

        // Obtains a list of exact classes for a given base type. Returns 0 if the number of
        // the exact classes is greater than maxExactClasses or if more types might be loaded
        // in future.
        int getExactClasses(CORINFO_CLASS_HANDLE baseType, int maxExactClasses, CORINFO_CLASS_HANDLE* exactClsRet);

        // If the Arg is a CORINFO_TYPE_CLASS fetch the class handle associated with it
        CORINFO_CLASS_HANDLE getArgClass(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args);

        // Returns type of HFA for valuetype
        CorInfoHFAElemType getHFAType(CORINFO_CLASS_HANDLE hClass);

        // function
        //   The function to run
        //
        // parameter
        //   The context parameter that will be passed to the function and the handler
        //
        bool runWithErrorTrap(errorTrapFunction function, void* parameter);

        // Runs the given function under an error trap. This allows the JIT to make calls
        // to interface functions that may throw exceptions without needing to be aware of
        // the EH ABI, exception types, etc. Returns true if the given function completed
        // successfully and false otherwise. This error trap checks for SuperPMI exceptions
        //
        // function
        //   The function to run
        //
        // parameter
        //   The context parameter that will be passed to the function and the handler
        //
        bool runWithSPMIErrorTrap(errorTrapFunction function, void* parameter);

        /*****************************************************************************
         * ICorStaticInfo contains EE interface methods which return values that are
         * constant from invocation to invocation.  Thus they may be embedded in
         * persisted information like statically generated code. (This is of course
         * assuming that all code versions are identical each time.)
         *****************************************************************************/

        // Return details about EE internal data structures
        void getEEInfo(CORINFO_EE_INFO* pEEInfoOut);

        // Returns name of the JIT timer log
        char* getJitTimeLogFilename();

        //
        // Diagnostic methods
        //

        // this function is for debugging only. Returns method token.
        // Returns mdMethodDefNil for dynamic methods.
        mdMethodDef getMethodDefFromMethod(CORINFO_METHOD_HANDLE hMethod);

        // This is similar to getMethodNameFromMetadata except that it also returns
        // reasonable names for functions without metadata.
        // See printObjectDescription for documentation of parameters.
        nuint printMethodName(CORINFO_METHOD_HANDLE ftn, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null);

        // Return method name as in metadata, or null if there is none,
        // and optionally return the class, enclosing class, and namespace names
        // as in metadata.
        // Suitable for non-debugging use.
        sbyte* getMethodNameFromMetadata(CORINFO_METHOD_HANDLE ftn, sbyte** className, sbyte** namespaceName, sbyte** enclosingClassName);

        // this function is for debugging only.  It returns a value that
        // is will always be the same for a given method.  It is used
        // to implement the 'jitRange' functionality
        uint getMethodHash(CORINFO_METHOD_HANDLE ftn);

        // returns whether the struct is enregisterable. Only valid on a System V VM. Returns true on success, false on failure.
        bool getSystemVAmd64PassStructInRegisterDescriptor(CORINFO_CLASS_HANDLE structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr);

        uint getLoongArch64PassStructInRegisterFlags(CORINFO_CLASS_HANDLE cls);

        uint getRISCV64PassStructInRegisterFlags(CORINFO_CLASS_HANDLE cls);
    }

    public struct Vtbl
    {
        //
        // ICorMethodInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, bool> isIntrinsic;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, bool> notifyMethodInfoUsage;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, uint> getMethodAttribs;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CorInfoMethodRuntimeFlags, void> setMethodAttribs;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_SIG_INFO*, CORINFO_CLASS_HANDLE, void> getMethodSig;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_INFO*, CORINFO_CONTEXT_HANDLE, bool> getMethodInfo;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, bool> haveSameMethodDefinition;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, CorInfoInline> canInline;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, void> beginInlining;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, CorInfoInline, sbyte*, void> reportInliningDecision;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, bool, bool> canTailCall;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, bool, CorInfoTailCall, sbyte*, void> reportTailCallDecision;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, uint, CORINFO_EH_CLAUSE*, void> getEHinfo;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_CLASS_HANDLE> getMethodClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, uint*, uint*, bool*, void> getMethodVTableOffset;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_DEVIRTUALIZATION_INFO*, bool> resolveVirtualMethod;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, bool*, CORINFO_METHOD_HANDLE> getUnboxedEntry;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getDefaultComparerClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getDefaultEqualityComparerClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_GENERICHANDLE_RESULT*, void> expandRawHandleIntrinsic;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, bool> isIntrinsicType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_SIG_INFO*, bool*, CorInfoCallConvExtension> getUnmanagedCallConv;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_SIG_INFO*, bool> pInvokeMarshalingRequired;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_METHOD_HANDLE, bool> satisfiesMethodConstraints;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, void> methodMustBeLoadedBeforeCodeIsRun;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE> mapMethodDeclToMethodImpl;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, GSCookie*, GSCookie**, void> getGSCookie;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, PatchpointInfo*, void> setPatchpointInfo;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, uint*, PatchpointInfo*> getOSRInfo;

        //
        // ICorModuleInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_RESOLVED_TOKEN*, void> resolveToken;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_MODULE_HANDLE, uint, CORINFO_CONTEXT_HANDLE, CORINFO_SIG_INFO*, void> findSig;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_MODULE_HANDLE, uint, CORINFO_CONTEXT_HANDLE, CORINFO_SIG_INFO*, void> findCallSiteSig;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_CLASS_HANDLE> getTokenTypeAsHandle;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_MODULE_HANDLE, uint, char*, int, int, int> getStringLiteral;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_OBJECT_HANDLE, sbyte*, nuint, nuint*, nuint> printObjectDescription;

        //
        // ICorClassInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CorInfoType> asCorInfoType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, sbyte**, sbyte*> getClassNameFromMetadata;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, uint, CORINFO_CLASS_HANDLE> getTypeInstantiationArgument;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, sbyte*, nuint, nuint*, nuint> printClassName;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, bool> isValueClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, uint> getClassAttribs;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_MODULE_HANDLE> getClassModule;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_MODULE_HANDLE, CORINFO_ASSEMBLY_HANDLE> getModuleAssembly;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_ASSEMBLY_HANDLE, sbyte*> getAssemblyName;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, nuint, void*> LongLifetimeMalloc;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, void*, void> LongLifetimeFree;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_MODULE_HANDLE*, void**, nuint> getClassModuleIdForStatics;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CONST_LOOKUP*, int*, bool> getIsClassInitedFlagAddress;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, bool, CORINFO_CONST_LOOKUP*, bool> getStaticBaseAddress;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, uint> getClassSize;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, uint> getHeapClassSize;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, bool> canAllocateOnStack;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, bool, uint> getClassAlignmentRequirement;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, byte*, uint> getClassGClayout;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, uint> getClassNumInstanceFields;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, int, CORINFO_FIELD_HANDLE> getFieldInClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_TYPE_LAYOUT_NODE*, nuint*, GetTypeLayoutResult> getTypeLayout;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, sbyte*, bool, bool> checkMethodModifier;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, bool*, CorInfoHelpFunc> getNewHelper;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getNewArrHelper;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_RESOLVED_TOKEN*, bool, CorInfoHelpFunc> getCastingHelper;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getSharedCCtorHelper;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getTypeForBox;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getBoxHelper;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getUnBoxHelper;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_OBJECT_HANDLE> getRuntimeTypePointer;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_OBJECT_HANDLE, bool> isObjectImmutable;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_OBJECT_HANDLE, int, ushort*, bool> getStringChar;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_OBJECT_HANDLE, CORINFO_CLASS_HANDLE> getObjectType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_LOOKUP_KIND*, CorInfoHelpFunc, CORINFO_CONST_LOOKUP*, bool> getReadyToRunHelper;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_RESOLVED_TOKEN*, mdToken, CORINFO_CLASS_HANDLE, CORINFO_LOOKUP*, void> getReadyToRunDelegateCtorHelper;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_FIELD_HANDLE, CORINFO_METHOD_HANDLE, CORINFO_CONTEXT_HANDLE, CorInfoInitClassResult> initClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, void> classMustBeLoadedBeforeCodeIsRun;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CorInfoClassId, CORINFO_CLASS_HANDLE> getBuiltinClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CorInfoType> getTypeForPrimitiveValueClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CorInfoType> getTypeForPrimitiveNumericClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, bool> canCast;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, TypeCompareState> compareTypesForCast;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, TypeCompareState> compareTypesForEquality;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, bool> isMoreSpecificType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, bool> isExactType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*, TypeCompareState> isEnum;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getParentType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*, CorInfoType> getChildType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, bool> isSDArray;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, uint> getArrayRank;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, CorInfoArrayIntrinsic> getArrayIntrinsicID;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_FIELD_HANDLE, uint, void*> getArrayInitializationData;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_METHOD_HANDLE, CORINFO_HELPER_DESC*, CorInfoIsAccessAllowedResult> canAccessClass;

        //
        // ICorFieldInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_FIELD_HANDLE, sbyte*, nuint, nuint*, nuint> printFieldName;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_FIELD_HANDLE, CORINFO_CLASS_HANDLE> getFieldClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_FIELD_HANDLE, CORINFO_CLASS_HANDLE*, CORINFO_FIELD_HANDLE, CorInfoType> getFieldType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_FIELD_HANDLE, uint> getFieldOffset;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_METHOD_HANDLE, CORINFO_ACCESS_FLAGS, CORINFO_FIELD_INFO*, void> getFieldInfo;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_FIELD_HANDLE, bool, uint> getThreadLocalFieldInfo;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_THREAD_STATIC_BLOCKS_INFO*, bool, void> getThreadLocalStaticBlocksInfo;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_THREAD_STATIC_INFO_NATIVEAOT*, void> getThreadLocalStaticInfo_NativeAOT;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_FIELD_HANDLE, bool> isFieldStatic;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_OBJECT_HANDLE, int> getArrayOrStringLength;

        //
        // ICorDebugInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, uint*, uint**, ICorDebugInfo.BoundaryTypes*, void> getBoundaries;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, uint, ICorDebugInfo.OffsetMapping*, void> setBoundaries;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, uint*, ICorDebugInfo.ILVarInfo**, bool*, void> getVars;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, uint, ICorDebugInfo.NativeVarInfo*, void> setVars;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, ICorDebugInfo.InlineTreeNode*, uint, ICorDebugInfo.RichOffsetMapping*, uint, void> reportRichMappings;

        //
        // Misc
        //

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, nuint, void*> allocateArray;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, void*, void> freeArray;

        //
        // ICorArgInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_ARG_LIST_HANDLE, CORINFO_ARG_LIST_HANDLE> getArgNext;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*, CorInfoTypeWithMod> getArgType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, int, CORINFO_CLASS_HANDLE*, int> getExactClasses;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE> getArgClass;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, CorInfoHFAElemType> getHFAType;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, errorTrapFunction, void*, bool> runWithErrorTrap;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, errorTrapFunction, void*, bool> runWithSPMIErrorTrap;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_EE_INFO*, void> getEEInfo;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, char*> getJitTimeLogFilename;

        //
        // Diagnostic methods
        //

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, mdMethodDef> getMethodDefFromMethod;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, sbyte*, nuint, nuint*, nuint> printMethodName;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, sbyte**, sbyte**, sbyte**, sbyte*> getMethodNameFromMetadata;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_METHOD_HANDLE, uint> getMethodHash;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR*, bool> getSystemVAmd64PassStructInRegisterDescriptor;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, uint> getLoongArch64PassStructInRegisterFlags;

        public delegate* unmanaged[MemberFunction]<ICorStaticInfo*, CORINFO_CLASS_HANDLE, uint> getRISCV64PassStructInRegisterFlags;
    }
}
