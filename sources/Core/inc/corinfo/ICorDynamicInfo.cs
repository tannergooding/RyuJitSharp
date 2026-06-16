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
    internal Vtbl<ICorDynamicInfo>* lpVtbl;

    //
    // ICorMethodInfo
    //

    public bool isIntrinsic(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.isIntrinsic((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public bool notifyMethodInfoUsage(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.notifyMethodInfoUsage((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public CorInfoFlag getMethodAttribs(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.getMethodAttribs((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public void setMethodAttribs(CORINFO_METHOD_HANDLE ftn, CorInfoMethodRuntimeFlags attribs) => lpVtbl->Base.setMethodAttribs((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, attribs);

    public void getMethodSig(CORINFO_METHOD_HANDLE ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->Base.getMethodSig((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, sig, memberParent);

    public bool getMethodInfo(CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_INFO* info, CORINFO_CONTEXT_HANDLE context = null) => lpVtbl->Base.getMethodInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, info, context);

    public bool haveSameMethodDefinition(CORINFO_METHOD_HANDLE meth1Hnd, CORINFO_METHOD_HANDLE meth2Hnd) => lpVtbl->Base.haveSameMethodDefinition((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), meth1Hnd, meth2Hnd);

    public CORINFO_CLASS_HANDLE getTypeDefinition(CORINFO_CLASS_HANDLE type) => lpVtbl->Base.getTypeDefinition((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), type);

    public CorInfoInline canInline(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd) => lpVtbl->Base.canInline((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), callerHnd, calleeHnd);

    public void beginInlining(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd) => lpVtbl->Base.beginInlining((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), inlinerHnd, inlineeHnd);

    public void reportInliningDecision(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd, CorInfoInline inlineResult, byte* reason) => lpVtbl->Base.reportInliningDecision((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), inlinerHnd, inlineeHnd, inlineResult, reason);

    public bool canTailCall(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE declaredCalleeHnd, CORINFO_METHOD_HANDLE exactCalleeHnd, bool fIsTailPrefix) => lpVtbl->Base.canTailCall((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), callerHnd, declaredCalleeHnd, exactCalleeHnd, fIsTailPrefix);

    public void reportTailCallDecision(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd, bool fIsTailPrefix, CorInfoTailCall tailCallResult, byte* reason) => lpVtbl->Base.reportTailCallDecision((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), callerHnd, calleeHnd, fIsTailPrefix, tailCallResult, reason);

    public void getEHinfo(CORINFO_METHOD_HANDLE ftn, int EHnumber, CORINFO_EH_CLAUSE* clause) => lpVtbl->Base.getEHinfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, EHnumber, clause);

    public CORINFO_CLASS_HANDLE getMethodClass(CORINFO_METHOD_HANDLE method) => lpVtbl->Base.getMethodClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), method);

    public void getMethodVTableOffset(CORINFO_METHOD_HANDLE method, int* offsetOfIndirection, int* offsetAfterIndirection, bool* isRelative) => lpVtbl->Base.getMethodVTableOffset((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), method, offsetOfIndirection, offsetAfterIndirection, isRelative);

    public bool resolveVirtualMethod(CORINFO_DEVIRTUALIZATION_INFO* info) => lpVtbl->Base.resolveVirtualMethod((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), info);

    public CORINFO_METHOD_HANDLE getUnboxedEntry(CORINFO_METHOD_HANDLE ftn, bool* requiresInstMethodTableArg) => lpVtbl->Base.getUnboxedEntry((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, requiresInstMethodTableArg);

    public CORINFO_METHOD_HANDLE getInstantiatedEntry(CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_HANDLE* methodArg, CORINFO_CLASS_HANDLE* classArg) => lpVtbl->Base.getInstantiatedEntry((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, methodArg, classArg);

    public CORINFO_METHOD_HANDLE getAsyncOtherVariant(CORINFO_METHOD_HANDLE ftn, bool* variantIsThunk) => lpVtbl->Base.getAsyncOtherVariant((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, variantIsThunk);

    public CORINFO_CLASS_HANDLE getDefaultComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->Base.getDefaultComparerClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), elemType);

    public CORINFO_CLASS_HANDLE getDefaultEqualityComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->Base.getDefaultEqualityComparerClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), elemType);

    public CORINFO_CLASS_HANDLE getSZArrayHelperEnumeratorClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->Base.getSZArrayHelperEnumeratorClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), elemType);

    public void expandRawHandleIntrinsic(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_GENERICHANDLE_RESULT* pResult) => lpVtbl->Base.expandRawHandleIntrinsic((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, callerHandle, pResult);

    public bool isIntrinsicType(CORINFO_CLASS_HANDLE classHnd) => lpVtbl->Base.isIntrinsicType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), classHnd);

    public CorInfoCallConvExtension getUnmanagedCallConv(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig, bool* pSuppressGCTransition) => lpVtbl->Base.getUnmanagedCallConv((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), method, callSiteSig, pSuppressGCTransition);

    public bool pInvokeMarshalingRequired(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig) => lpVtbl->Base.pInvokeMarshalingRequired((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), method, callSiteSig);

    public bool satisfiesMethodConstraints(CORINFO_CLASS_HANDLE parent, CORINFO_METHOD_HANDLE method) => lpVtbl->Base.satisfiesMethodConstraints((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), parent, method);

    public void methodMustBeLoadedBeforeCodeIsRun(CORINFO_METHOD_HANDLE method) => lpVtbl->Base.methodMustBeLoadedBeforeCodeIsRun((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), method);

    public void getGSCookie(GSCookie* pCookieVal, GSCookie** ppCookieVal) => lpVtbl->Base.getGSCookie((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pCookieVal, ppCookieVal);

    public void setPatchpointInfo(PatchpointInfo* patchpointInfo) => lpVtbl->Base.setPatchpointInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), patchpointInfo);

    public PatchpointInfo* getOSRInfo(int* ilOffset) => lpVtbl->Base.getOSRInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ilOffset);

    //
    // ICorModuleInfo
    //

    public void resolveToken(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->Base.resolveToken((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken);

    public void findSig(CORINFO_MODULE_HANDLE module, int sigTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->Base.findSig((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), module, sigTOK, context, sig);

    public void findCallSiteSig(CORINFO_MODULE_HANDLE module, int methTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->Base.findCallSiteSig((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), module, methTOK, context, sig);

    public CORINFO_CLASS_HANDLE getTokenTypeAsHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->Base.getTokenTypeAsHandle((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken);

    public int getStringLiteral(CORINFO_MODULE_HANDLE module, int metaTOK, char* buffer, int bufferSize, int startIndex = 0) => lpVtbl->Base.getStringLiteral((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), module, metaTOK, buffer, bufferSize, startIndex);

    public nint printObjectDescription(CORINFO_OBJECT_HANDLE handle, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null) => lpVtbl->Base.printObjectDescription((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), handle, buffer, bufferSize, pRequiredBufferSize);

    //
    // ICorClassInfo
    //

    public CorInfoType asCorInfoType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.asCorInfoType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public byte* getClassNameFromMetadata(CORINFO_CLASS_HANDLE cls, byte** namespaceName) => lpVtbl->Base.getClassNameFromMetadata((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls, namespaceName);

    public CORINFO_CLASS_HANDLE getTypeInstantiationArgument(CORINFO_CLASS_HANDLE cls, int index) => lpVtbl->Base.getTypeInstantiationArgument((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls, index);

    public CORINFO_CLASS_HANDLE getMethodInstantiationArgument(CORINFO_METHOD_HANDLE ftn, int index) => lpVtbl->Base.getMethodInstantiationArgument((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, index);

    public nint printClassName(CORINFO_CLASS_HANDLE cls, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null) => lpVtbl->Base.printClassName((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls, buffer, bufferSize, pRequiredBufferSize);

    public bool isValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.isValueClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoFlag getClassAttribs(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getClassAttribs((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public byte* getClassAssemblyName(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getClassAssemblyName((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public void* LongLifetimeMalloc(nint sz) => lpVtbl->Base.LongLifetimeMalloc((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), sz);

    public void LongLifetimeFree(void* obj) => lpVtbl->Base.LongLifetimeFree((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), obj);

    public bool getIsClassInitedFlagAddress(CORINFO_CLASS_HANDLE cls, CORINFO_CONST_LOOKUP* addr, int* offset) => lpVtbl->Base.getIsClassInitedFlagAddress((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls, addr, offset);

    public void* getClassStaticDynamicInfo(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getClassStaticDynamicInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public void* getClassThreadStaticDynamicInfo(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getClassThreadStaticDynamicInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public bool getStaticBaseAddress(CORINFO_CLASS_HANDLE cls, bool isGc, CORINFO_CONST_LOOKUP* addr) => lpVtbl->Base.getStaticBaseAddress((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls, isGc, addr);

    public int getClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getClassSize((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public int getHeapClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getHeapClassSize((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public bool canAllocateOnStack(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.canAllocateOnStack((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public int getClassAlignmentRequirement(CORINFO_CLASS_HANDLE cls, bool fDoubleAlignHint = false) => lpVtbl->Base.getClassAlignmentRequirement((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls, fDoubleAlignHint);

    public int getClassGClayout(CORINFO_CLASS_HANDLE cls, CorInfoGCType* gcPtrs) => lpVtbl->Base.getClassGClayout((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls, gcPtrs);

    public int getClassNumInstanceFields(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getClassNumInstanceFields((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CORINFO_FIELD_HANDLE getFieldInClass(CORINFO_CLASS_HANDLE clsHnd, int num) => lpVtbl->Base.getFieldInClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), clsHnd, num);

    public GetTypeLayoutResult getTypeLayout(CORINFO_CLASS_HANDLE typeHnd, CORINFO_TYPE_LAYOUT_NODE* treeNodes, nint* numTreeNodes) => lpVtbl->Base.getTypeLayout((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), typeHnd, treeNodes, numTreeNodes);

    public bool checkMethodModifier(CORINFO_METHOD_HANDLE hMethod, byte* modifier, bool fOptional) => lpVtbl->Base.checkMethodModifier((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), hMethod, modifier, fOptional);

    public CorInfoHelpFunc getNewHelper(CORINFO_CLASS_HANDLE classHandle, bool* pHasSideEffects) => lpVtbl->Base.getNewHelper((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), classHandle, pHasSideEffects);

    public CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_HANDLE arrayCls) => lpVtbl->Base.getNewArrHelper((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), arrayCls);

    public CorInfoHelpFunc getCastingHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fThrowing) => lpVtbl->Base.getCastingHelper((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, fThrowing);

    public CorInfoHelpFunc getSharedCCtorHelper(CORINFO_CLASS_HANDLE clsHnd) => lpVtbl->Base.getSharedCCtorHelper((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), clsHnd);

    public CORINFO_CLASS_HANDLE getTypeForBox(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getTypeForBox((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoHelpFunc getBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getBoxHelper((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoHelpFunc getUnBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getUnBoxHelper((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CORINFO_OBJECT_HANDLE getRuntimeTypePointer(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getRuntimeTypePointer((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public bool isObjectImmutable(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->Base.isObjectImmutable((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), objPtr);

    public bool getStringChar(CORINFO_OBJECT_HANDLE strObj, int index, ushort* value) => lpVtbl->Base.getStringChar((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), strObj, index, value);

    public CORINFO_CLASS_HANDLE getObjectType(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->Base.getObjectType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), objPtr);

    public bool getReadyToRunHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, CorInfoHelpFunc id, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CONST_LOOKUP* pLookup) => lpVtbl->Base.getReadyToRunHelper((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, id, callerHandle, pLookup);

    public void getReadyToRunDelegateCtorHelper(CORINFO_RESOLVED_TOKEN* pTargetMethod, mdToken targetConstraint, CORINFO_CLASS_HANDLE delegateType, CORINFO_METHOD_HANDLE callerHandler, CORINFO_LOOKUP* pLookup) => lpVtbl->Base.getReadyToRunDelegateCtorHelper((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pTargetMethod, targetConstraint, delegateType, callerHandler, pLookup);

    public CorInfoInitClassResult initClass(CORINFO_FIELD_HANDLE field, CORINFO_METHOD_HANDLE method, CORINFO_CONTEXT_HANDLE context) => lpVtbl->Base.initClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field, method, context);

    public void classMustBeLoadedBeforeCodeIsRun(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.classMustBeLoadedBeforeCodeIsRun((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CORINFO_CLASS_HANDLE getBuiltinClass(CorInfoClassId classId) => lpVtbl->Base.getBuiltinClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), classId);

    public CorInfoType getTypeForPrimitiveValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getTypeForPrimitiveValueClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoType getTypeForPrimitiveNumericClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getTypeForPrimitiveNumericClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public bool canCast(CORINFO_CLASS_HANDLE child, CORINFO_CLASS_HANDLE parent) => lpVtbl->Base.canCast((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), child, parent);

    public TypeCompareState compareTypesForCast(CORINFO_CLASS_HANDLE fromClass, CORINFO_CLASS_HANDLE toClass) => lpVtbl->Base.compareTypesForCast((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), fromClass, toClass);

    public TypeCompareState compareTypesForEquality(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->Base.compareTypesForEquality((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls1, cls2);

    public bool isMoreSpecificType(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->Base.isMoreSpecificType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls1, cls2);

    public bool isExactType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.isExactType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public TypeCompareState isGenericType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.isGenericType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public TypeCompareState isNullableType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.isNullableType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public TypeCompareState isEnum(CORINFO_CLASS_HANDLE cls, CORINFO_CLASS_HANDLE* underlyingType) => lpVtbl->Base.isEnum((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls, underlyingType);

    public CORINFO_CLASS_HANDLE getParentType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getParentType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoType getChildType(CORINFO_CLASS_HANDLE clsHnd, CORINFO_CLASS_HANDLE* clsRet) => lpVtbl->Base.getChildType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), clsHnd, clsRet);

    public bool isSDArray(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.isSDArray((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public int getArrayRank(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.getArrayRank((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoArrayIntrinsic getArrayIntrinsicID(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.getArrayIntrinsicID((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public void* getArrayInitializationData(CORINFO_FIELD_HANDLE field, int size) => lpVtbl->Base.getArrayInitializationData((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field, size);

    public CorInfoIsAccessAllowedResult canAccessClass(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_HELPER_DESC* pAccessHelper) => lpVtbl->Base.canAccessClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, callerHandle, pAccessHelper);

    //
    // ICorFieldInfo
    //

    public nint printFieldName(CORINFO_FIELD_HANDLE field, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null) => lpVtbl->Base.printFieldName((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field, buffer, bufferSize, pRequiredBufferSize);

    public CORINFO_CLASS_HANDLE getFieldClass(CORINFO_FIELD_HANDLE field) => lpVtbl->Base.getFieldClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field);

    public CorInfoType getFieldType(CORINFO_FIELD_HANDLE field, CORINFO_CLASS_HANDLE* structType = null, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->Base.getFieldType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field, structType, memberParent);

    public int getFieldOffset(CORINFO_FIELD_HANDLE field) => lpVtbl->Base.getFieldOffset((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field);

    public void getFieldInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_ACCESS_FLAGS flags, CORINFO_FIELD_INFO* pResult) => lpVtbl->Base.getFieldInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, callerHandle, flags, pResult);

    public int getThreadLocalFieldInfo(CORINFO_FIELD_HANDLE field, bool isGCType) => lpVtbl->Base.getThreadLocalFieldInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field, isGCType);

    public void getThreadLocalStaticBlocksInfo(CORINFO_THREAD_STATIC_BLOCKS_INFO* pInfo, bool isGCType) => lpVtbl->Base.getThreadLocalStaticBlocksInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pInfo, isGCType);

    public void getThreadLocalStaticInfo_NativeAOT(CORINFO_THREAD_STATIC_INFO_NATIVEAOT* pInfo) => lpVtbl->Base.getThreadLocalStaticInfo_NativeAOT((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pInfo);

    public bool isFieldStatic(CORINFO_FIELD_HANDLE fldHnd) => lpVtbl->Base.isFieldStatic((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), fldHnd);

    public int getArrayOrStringLength(CORINFO_OBJECT_HANDLE objHnd) => lpVtbl->Base.getArrayOrStringLength((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), objHnd);

    //
    // ICorDebugInfo
    //

    public void getBoundaries(CORINFO_METHOD_HANDLE ftn, int* cILOffsets, int** pILOffsets, ICorDebugInfo.BoundaryTypes* implicitBoundaries) => lpVtbl->Base.getBoundaries((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, cILOffsets, pILOffsets, implicitBoundaries);

    public void setBoundaries(CORINFO_METHOD_HANDLE ftn, int cMap, ICorDebugInfo.OffsetMapping* pMap) => lpVtbl->Base.setBoundaries((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, cMap, pMap);

    public void getVars(CORINFO_METHOD_HANDLE ftn, int* cVars, ICorDebugInfo.ILVarInfo** vars, bool* extendOthers) => lpVtbl->Base.getVars((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, cVars, vars, extendOthers);

    public void setVars(CORINFO_METHOD_HANDLE ftn, int cVars, ICorDebugInfo.NativeVarInfo* vars) => lpVtbl->Base.setVars((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, cVars, vars);

    public void reportRichMappings(ICorDebugInfo.InlineTreeNode* inlineTreeNodes, int numInlineTreeNodes, ICorDebugInfo.RichOffsetMapping* mappings, int numMappings) => lpVtbl->Base.reportRichMappings((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), inlineTreeNodes, numInlineTreeNodes, mappings, numMappings);

    public void reportAsyncDebugInfo(ICorDebugInfo.AsyncInfo* asyncInfo, ICorDebugInfo.AsyncSuspensionPoint* suspensionPoints, ICorDebugInfo.AsyncContinuationVarInfo* vars, int numVars) => lpVtbl->Base.reportAsyncDebugInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), asyncInfo, suspensionPoints, vars, numVars);

    public void reportMetadata(byte* key, void* value, nint length) => lpVtbl->Base.reportMetadata((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), key, value, length);

    //
    // Misc
    //

    public void* allocateArray(nint cBytes) => lpVtbl->Base.allocateArray((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), cBytes);

    public void freeArray(void* array) => lpVtbl->Base.freeArray((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), array);

    //
    // ICorArgInfo
    //

    public CORINFO_ARG_LIST_HANDLE getArgNext(CORINFO_ARG_LIST_HANDLE args) => lpVtbl->Base.getArgNext((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), args);

    public CorInfoTypeWithMod getArgType(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args, CORINFO_CLASS_HANDLE* vcTypeRet) => lpVtbl->Base.getArgType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), sig, args, vcTypeRet);

    public int getExactClasses(CORINFO_CLASS_HANDLE baseType, int maxExactClasses, CORINFO_CLASS_HANDLE* exactClsRet) => lpVtbl->Base.getExactClasses((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), baseType, maxExactClasses, exactClsRet);

    public CORINFO_CLASS_HANDLE getArgClass(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args) => lpVtbl->Base.getArgClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), sig, args);

    public CorInfoHFAElemType getHFAType(CORINFO_CLASS_HANDLE hClass) => lpVtbl->Base.getHFAType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), hClass);

    public bool runWithErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->Base.runWithErrorTrap((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), function, parameter);

    public bool runWithSPMIErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->Base.runWithSPMIErrorTrap((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), function, parameter);

    public void getEEInfo(CORINFO_EE_INFO* pEEInfoOut) => lpVtbl->Base.getEEInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pEEInfoOut);

    public void getAsyncInfo(CORINFO_ASYNC_INFO* pAsyncInfoOut) => lpVtbl->Base.getAsyncInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pAsyncInfoOut);

    //
    // Diagnostic methods
    //

    public mdMethodDef getMethodDefFromMethod(CORINFO_METHOD_HANDLE hMethod) => lpVtbl->Base.getMethodDefFromMethod((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), hMethod);

    public nint printMethodName(CORINFO_METHOD_HANDLE ftn, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null) => lpVtbl->Base.printMethodName((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, buffer, bufferSize, pRequiredBufferSize);

    public byte* getMethodNameFromMetadata(CORINFO_METHOD_HANDLE ftn, byte** className, byte** namespaceName, byte** enclosingClassName, nint maxEnclosingClassNames) => lpVtbl->Base.getMethodNameFromMetadata((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, className, namespaceName, enclosingClassName, maxEnclosingClassNames);

    public int getMethodHash(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.getMethodHash((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public bool getSystemVAmd64PassStructInRegisterDescriptor(CORINFO_CLASS_HANDLE structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr) => lpVtbl->Base.getSystemVAmd64PassStructInRegisterDescriptor((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), structHnd, structPassInRegDescPtr);

    public void getSwiftLowering(CORINFO_CLASS_HANDLE structHnd, CORINFO_SWIFT_LOWERING* pLowering) => lpVtbl->Base.getSwiftLowering((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), structHnd, pLowering);

    public void getFpStructLowering(CORINFO_CLASS_HANDLE structHnd, CORINFO_FPSTRUCT_LOWERING* pLowering) => lpVtbl->Base.getFpStructLowering((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), structHnd, pLowering);

    public CorInfoWasmType getWasmLowering(CORINFO_CLASS_HANDLE structHnd) => lpVtbl->Base.getWasmLowering((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), structHnd);

    //
    // ICorDynamicInfo
    //

    public int getThreadTLSIndex(void** ppIndirection = null) => lpVtbl->getThreadTLSIndex((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ppIndirection);

    public int* getAddrOfCaptureThreadGlobal(void** ppIndirection = null) => lpVtbl->getAddrOfCaptureThreadGlobal((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ppIndirection);

    public void* getHelperFtn(CorInfoHelpFunc ftnNum, CORINFO_CONST_LOOKUP* pNativeEntrypoint, CORINFO_METHOD_HANDLE* pMethodHandle = null) => lpVtbl->getHelperFtn((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftnNum, pNativeEntrypoint, pMethodHandle);

    public void getFunctionEntryPoint(CORINFO_METHOD_HANDLE ftn, CORINFO_CONST_LOOKUP* pResult, CORINFO_ACCESS_FLAGS accessFlags = CORINFO_ACCESS_ANY) => lpVtbl->getFunctionEntryPoint((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, pResult, accessFlags);

    public void getFunctionFixedEntryPoint(CORINFO_METHOD_HANDLE ftn, bool isUnsafeFunctionPointer, CORINFO_CONST_LOOKUP* pResult) => lpVtbl->getFunctionFixedEntryPoint((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ftn, isUnsafeFunctionPointer, pResult);

    public CORINFO_MODULE_HANDLE embedModuleHandle(CORINFO_MODULE_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedModuleHandle((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), handle, ppIndirection);

    public CORINFO_CLASS_HANDLE embedClassHandle(CORINFO_CLASS_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedClassHandle((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), handle, ppIndirection);

    public CORINFO_METHOD_HANDLE embedMethodHandle(CORINFO_METHOD_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedMethodHandle((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), handle, ppIndirection);

    public CORINFO_FIELD_HANDLE embedFieldHandle(CORINFO_FIELD_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedFieldHandle((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), handle, ppIndirection);

    public void embedGenericHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fEmbedParent, CORINFO_METHOD_HANDLE callerHandle, CORINFO_GENERICHANDLE_RESULT* pResult) => lpVtbl->embedGenericHandle((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, fEmbedParent, callerHandle, pResult);

    public void getLocationOfThisType(CORINFO_METHOD_HANDLE context, CORINFO_LOOKUP_KIND* pLookupKind) => lpVtbl->getLocationOfThisType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), context, pLookupKind);

    public void getAddressOfPInvokeTarget(CORINFO_METHOD_HANDLE method, CORINFO_CONST_LOOKUP* pLookup) => lpVtbl->getAddressOfPInvokeTarget((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), method, pLookup);

    public void* GetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig, void** ppIndirection = null) => lpVtbl->GetCookieForPInvokeCalliSig((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), szMetaSig, ppIndirection);

    public void* GetCookieForInterpreterCalliSig(CORINFO_SIG_INFO* szMetaSig) => lpVtbl->GetCookieForInterpreterCalliSig((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), szMetaSig);

    public CORINFO_JUST_MY_CODE_HANDLE getJustMyCodeHandle(CORINFO_METHOD_HANDLE method, CORINFO_JUST_MY_CODE_HANDLE** ppIndirection = null) => lpVtbl->getJustMyCodeHandle((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), method, ppIndirection);

    public void GetProfilingHandle(bool* pbHookFunction, void** pProfilerHandle, bool* pbIndirectedHandles) => lpVtbl->GetProfilingHandle((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pbHookFunction, pProfilerHandle, pbIndirectedHandles);

    public void getCallInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult) => lpVtbl->getCallInfo((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, pConstrainedResolvedToken, callerHandle, flags, pResult);

    public bool getStaticFieldContent(CORINFO_FIELD_HANDLE field, byte* buffer, int bufferSize, int valueOffset = 0, bool ignoreMovableObjects = true) => lpVtbl->getStaticFieldContent((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field, buffer, bufferSize, valueOffset, ignoreMovableObjects);

    public bool getObjectContent(CORINFO_OBJECT_HANDLE obj, byte* buffer, int bufferSize, int valueOffset) => lpVtbl->getObjectContent((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), obj, buffer, bufferSize, valueOffset);

    public CORINFO_CLASS_HANDLE getStaticFieldCurrentClass(CORINFO_FIELD_HANDLE field, bool* pIsSpeculative = null) => lpVtbl->getStaticFieldCurrentClass((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field, pIsSpeculative);

    public CORINFO_VARARGS_HANDLE getVarArgsHandle(CORINFO_SIG_INFO* pSig, CORINFO_METHOD_HANDLE methHnd, void** ppIndirection = null) => lpVtbl->getVarArgsHandle((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pSig, methHnd, ppIndirection);

    public InfoAccessType constructStringLiteral(CORINFO_MODULE_HANDLE module, mdToken metaTok, void** ppValue) => lpVtbl->constructStringLiteral((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), module, metaTok, ppValue);

    public InfoAccessType emptyStringLiteral(void** ppValue) => lpVtbl->emptyStringLiteral((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), ppValue);

    public int getFieldThreadLocalStoreID(CORINFO_FIELD_HANDLE field, void** ppIndirection = null) => lpVtbl->getFieldThreadLocalStoreID((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), field, ppIndirection);

    public CORINFO_METHOD_HANDLE GetDelegateCtor(CORINFO_METHOD_HANDLE methHnd, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE targetMethodHnd, DelegateCtorArgs* pCtorData) => lpVtbl->GetDelegateCtor((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), methHnd, clsHnd, targetMethodHnd, pCtorData);

    public void MethodCompileComplete(CORINFO_METHOD_HANDLE methHnd) => lpVtbl->MethodCompileComplete((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), methHnd);

    public bool getTailCallHelpers(CORINFO_RESOLVED_TOKEN* callToken, CORINFO_SIG_INFO* sig, CORINFO_GET_TAILCALL_HELPERS_FLAGS flags, CORINFO_TAILCALL_HELPERS* pResult) => lpVtbl->getTailCallHelpers((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), callToken, sig, flags, pResult);

    public CORINFO_METHOD_HANDLE getAsyncResumptionStub(void** entryPoint) => lpVtbl->getAsyncResumptionStub((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), entryPoint);

    public CORINFO_CLASS_HANDLE getContinuationType(nint dataSize, bool* objRefs, nint objRefsSize) => lpVtbl->getContinuationType((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), dataSize, objRefs, objRefsSize);

    public bool convertPInvokeCalliToCall(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fMustConvert) => lpVtbl->convertPInvokeCalliToCall((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, fMustConvert);

    public bool notifyInstructionSetUsage(CORINFO_InstructionSet instructionSet, bool supportEnabled) => lpVtbl->notifyInstructionSetUsage((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), instructionSet, supportEnabled);

    public void updateEntryPointForTailCall(CORINFO_CONST_LOOKUP* entryPoint) => lpVtbl->updateEntryPointForTailCall((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), entryPoint);

    public CORINFO_WASM_TYPE_SYMBOL_HANDLE getWasmTypeSymbol(CorInfoWasmType* types, nint typesSize) => lpVtbl->getWasmTypeSymbol((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), types, typesSize);

    public CORINFO_METHOD_HANDLE getSpecialCopyHelper(CORINFO_CLASS_HANDLE type) => lpVtbl->getSpecialCopyHelper((ICorDynamicInfo*)(Unsafe.AsPointer(ref this)), type);

    public interface Interface : ICorStaticInfo.Interface
    {
        //
        // These methods return values to the JIT which are not constant
        // from session to session.
        //
        // These methods take an extra parameter : void **ppIndirection.
        // If a JIT supports generation of prejit code (install-o-jit), it
        // must pass a non-null value for this parameter, and check the
        // resulting value.  If *ppIndirection is null, code should be
        // generated normally.  If non-null, then the value of
        // *ppIndirection is an address in the cookie table, and the code
        // generator needs to generate an indirection through the table to
        // get the resulting value.  In this case, the return result of the
        // function must NOT be directly embedded in the generated code.
        //
        // Note that if a JIT does not support prejit code generation, it
        // may ignore the extra parameter & pass the default of null - the
        // prejit ICorDynamicInfo implementation will see this & generate
        // an error if the jitter is used in a prejit scenario.
        //

        // Return details about EE internal data structures

        int getThreadTLSIndex(void** ppIndirection = null);

        int* getAddrOfCaptureThreadGlobal(void** ppIndirection = null);

        // return the native entry point to an EE helper (see CorInfoHelpFunc)
        void* getHelperFtn(CorInfoHelpFunc ftnNum, CORINFO_CONST_LOOKUP* pNativeEntrypoint, CORINFO_METHOD_HANDLE* pMethodHandle = null);

        // return a callable address of the function (native code). This function
        // may return a different value (depending on whether the method has
        // been JITed or not.
        void getFunctionEntryPoint(CORINFO_METHOD_HANDLE ftn, CORINFO_CONST_LOOKUP* pResult, CORINFO_ACCESS_FLAGS accessFlags = CORINFO_ACCESS_ANY);

        void getFunctionFixedEntryPoint(CORINFO_METHOD_HANDLE ftn, bool isUnsafeFunctionPointer, CORINFO_CONST_LOOKUP* pResult);

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
        void embedGenericHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fEmbedParent, CORINFO_METHOD_HANDLE callerHandle, CORINFO_GENERICHANDLE_RESULT* pResult);

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

        // Generate a cookie based on the signature to pass to INTOP_CALLI in the interpreter.
        void* GetCookieForInterpreterCalliSig(CORINFO_SIG_INFO* szMetaSig);

        // Gets a handle that is checked to see if the current method is
        // included in "JustMyCode"
        CORINFO_JUST_MY_CODE_HANDLE getJustMyCodeHandle(CORINFO_METHOD_HANDLE method, CORINFO_JUST_MY_CODE_HANDLE** ppIndirection = null);

        // Gets a method handle that can be used to correlate profiling data.
        // This is the IP of a native method, or the address of the descriptor struct
        // for IL.  Always guaranteed to be unique per process, and not to move. */
        void GetProfilingHandle(bool* pbHookFunction, void** pProfilerHandle, bool* pbIndirectedHandles);

        // Returns instructions on how to make the call. See code:CORINFO_CALL_INFO for possible return values.
        void getCallInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult);

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

        // If pIsSpeculative is null, return the class handle for the value of ref-class typed
        // static readonly fields, if there is a unique location for the static and the class
        // is already initialized.
        //
        // If pIsSpeculative is not null, fetch the class handle for the value of all ref-class
        // typed static fields, if there is a unique location for the static and the field is
        // not null.
        //
        // Set *pIsSpeculative true if this type may change over time (field is not readonly or
        // is readonly but class has not yet finished initialization). Set *pIsSpeculative false
        // if this type will not change.
        CORINFO_CLASS_HANDLE getStaticFieldCurrentClass(CORINFO_FIELD_HANDLE field, bool* pIsSpeculative = null);

        // registers a vararg sig & returns a VM cookie for it (which can contain other stuff)
        CORINFO_VARARGS_HANDLE getVarArgsHandle(CORINFO_SIG_INFO* pSig, CORINFO_METHOD_HANDLE methHnd, void** ppIndirection = null);

        // Allocate a string literal on the heap and return a handle to it
        InfoAccessType constructStringLiteral(CORINFO_MODULE_HANDLE module, mdToken metaTok, void** ppValue);

        InfoAccessType emptyStringLiteral(void** ppValue);

        // (static fields only) given that 'field' refers to thread local store,
        // return the ID (TLS index), which is used to find the beginning of the
        // TLS data area for the particular DLL 'field' is associated with.
        int getFieldThreadLocalStoreID(CORINFO_FIELD_HANDLE field, void** ppIndirection = null);

        CORINFO_METHOD_HANDLE GetDelegateCtor(CORINFO_METHOD_HANDLE methHnd, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE targetMethodHnd, DelegateCtorArgs* pCtorData);

        void MethodCompileComplete(CORINFO_METHOD_HANDLE methHnd);

        // Obtain tailcall help for the specified call site.
        bool getTailCallHelpers(CORINFO_RESOLVED_TOKEN* callToken, CORINFO_SIG_INFO* sig, CORINFO_GET_TAILCALL_HELPERS_FLAGS flags, CORINFO_TAILCALL_HELPERS* pResult);

        CORINFO_METHOD_HANDLE getAsyncResumptionStub(void** entryPoint);

        CORINFO_CLASS_HANDLE getContinuationType(nint dataSize, bool* objRefs, nint objRefsSize);

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

        CORINFO_WASM_TYPE_SYMBOL_HANDLE getWasmTypeSymbol(CorInfoWasmType* types, nint typesSize);

        CORINFO_METHOD_HANDLE getSpecialCopyHelper(CORINFO_CLASS_HANDLE type);
    }

    public struct Vtbl<TSelf>
        where TSelf : unmanaged, Interface
    {
        public ICorStaticInfo.Vtbl<TSelf> Base;

        //
        // ICorDynamicInfo
        //

        public delegate* unmanaged[MemberFunction]<TSelf*, void**, int> getThreadTLSIndex;

        public delegate* unmanaged[MemberFunction]<TSelf*, void**, int*> getAddrOfCaptureThreadGlobal;

        public delegate* unmanaged[MemberFunction]<TSelf*, CorInfoHelpFunc, CORINFO_CONST_LOOKUP*, CORINFO_METHOD_HANDLE*, void*> getHelperFtn;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, CORINFO_CONST_LOOKUP*, CORINFO_ACCESS_FLAGS, void> getFunctionEntryPoint;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, bool, CORINFO_CONST_LOOKUP*, void> getFunctionFixedEntryPoint;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_MODULE_HANDLE, void**, CORINFO_MODULE_HANDLE> embedModuleHandle;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_CLASS_HANDLE, void**, CORINFO_CLASS_HANDLE> embedClassHandle;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, void**, CORINFO_METHOD_HANDLE> embedMethodHandle;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_FIELD_HANDLE, void**, CORINFO_FIELD_HANDLE> embedFieldHandle;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_RESOLVED_TOKEN*, bool, CORINFO_METHOD_HANDLE, CORINFO_GENERICHANDLE_RESULT*, void> embedGenericHandle;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, CORINFO_LOOKUP_KIND*, void> getLocationOfThisType;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, CORINFO_CONST_LOOKUP*, void> getAddressOfPInvokeTarget;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_SIG_INFO*, void**, void*> GetCookieForPInvokeCalliSig;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_SIG_INFO*, void*> GetCookieForInterpreterCalliSig;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, CORINFO_JUST_MY_CODE_HANDLE**, CORINFO_JUST_MY_CODE_HANDLE> getJustMyCodeHandle;

        public delegate* unmanaged[MemberFunction]<TSelf*, bool*, void**, bool*, void> GetProfilingHandle;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_RESOLVED_TOKEN*, CORINFO_RESOLVED_TOKEN*, CORINFO_METHOD_HANDLE, CORINFO_CALLINFO_FLAGS, CORINFO_CALL_INFO*, void> getCallInfo;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_FIELD_HANDLE, byte*, int, int, bool, bool> getStaticFieldContent;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_OBJECT_HANDLE, byte*, int, int, bool> getObjectContent;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_FIELD_HANDLE, bool*, CORINFO_CLASS_HANDLE> getStaticFieldCurrentClass;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_SIG_INFO*, CORINFO_METHOD_HANDLE, void**, CORINFO_VARARGS_HANDLE> getVarArgsHandle;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_MODULE_HANDLE, mdToken, void**, InfoAccessType> constructStringLiteral;

        public delegate* unmanaged[MemberFunction]<TSelf*, void**, InfoAccessType> emptyStringLiteral;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_FIELD_HANDLE, void**, int> getFieldThreadLocalStoreID;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, CORINFO_CLASS_HANDLE, CORINFO_METHOD_HANDLE, DelegateCtorArgs*, CORINFO_METHOD_HANDLE> GetDelegateCtor;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, void> MethodCompileComplete;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_RESOLVED_TOKEN*, CORINFO_SIG_INFO*, CORINFO_GET_TAILCALL_HELPERS_FLAGS, CORINFO_TAILCALL_HELPERS*, bool> getTailCallHelpers;

        public delegate* unmanaged[MemberFunction]<TSelf*, void**, CORINFO_METHOD_HANDLE> getAsyncResumptionStub;

        public delegate* unmanaged[MemberFunction]<TSelf*, nint, bool*, nint, CORINFO_CLASS_HANDLE> getContinuationType;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_RESOLVED_TOKEN*, bool, bool> convertPInvokeCalliToCall;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_InstructionSet, bool, bool> notifyInstructionSetUsage;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_CONST_LOOKUP*, void> updateEntryPointForTailCall;

        public delegate* unmanaged[MemberFunction]<TSelf*, CorInfoWasmType*, nint, CORINFO_WASM_TYPE_SYMBOL_HANDLE> getWasmTypeSymbol;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_CLASS_HANDLE, CORINFO_METHOD_HANDLE> getSpecialCopyHelper;
    }
}
