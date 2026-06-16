// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

//------------------------------------------------------------------------------------------
// #JitToEEInterface
//
// ICorJitInfo is the main interface that the JIT uses to call back to the EE and get information. It is
// the companion to code:ICorJitCompiler#EEToJitInterface. The concrete implementation of this in the
// runtime is the code:CEEJitInfo type.  There is also a version of this for the NGEN case.
//
// See code:ICorMethodInfo#EEJitContractDetails for subtle conventions used by this interface.
//
// There is more information on the JIT in the book of the runtime entry
// http://devdiv/sites/CLR/Product%20Documentation/2.0/BookOfTheRuntime/JIT/JIT%20Design.doc
//
public unsafe partial struct ICorJitInfo : ICorJitInfo.Interface
{
    internal Vtbl<ICorJitInfo>* lpVtbl;

    //
    // ICorMethodInfo
    //

    public bool isIntrinsic(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.Base.isIntrinsic((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public bool notifyMethodInfoUsage(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.Base.notifyMethodInfoUsage((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public CorInfoFlag getMethodAttribs(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.Base.getMethodAttribs((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public void setMethodAttribs(CORINFO_METHOD_HANDLE ftn, CorInfoMethodRuntimeFlags attribs) => lpVtbl->Base.Base.setMethodAttribs((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, attribs);

    public void getMethodSig(CORINFO_METHOD_HANDLE ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->Base.Base.getMethodSig((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, sig, memberParent);

    public bool getMethodInfo(CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_INFO* info, CORINFO_CONTEXT_HANDLE context = null) => lpVtbl->Base.Base.getMethodInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, info, context);

    public bool haveSameMethodDefinition(CORINFO_METHOD_HANDLE meth1Hnd, CORINFO_METHOD_HANDLE meth2Hnd) => lpVtbl->Base.Base.haveSameMethodDefinition((ICorJitInfo*)(Unsafe.AsPointer(ref this)), meth1Hnd, meth2Hnd);

    public CORINFO_CLASS_HANDLE getTypeDefinition(CORINFO_CLASS_HANDLE type) => lpVtbl->Base.Base.getTypeDefinition((ICorJitInfo*)(Unsafe.AsPointer(ref this)), type);

    public CorInfoInline canInline(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd) => lpVtbl->Base.Base.canInline((ICorJitInfo*)(Unsafe.AsPointer(ref this)), callerHnd, calleeHnd);

    public void beginInlining(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd) => lpVtbl->Base.Base.beginInlining((ICorJitInfo*)(Unsafe.AsPointer(ref this)), inlinerHnd, inlineeHnd);

    public void reportInliningDecision(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd, CorInfoInline inlineResult, byte* reason) => lpVtbl->Base.Base.reportInliningDecision((ICorJitInfo*)(Unsafe.AsPointer(ref this)), inlinerHnd, inlineeHnd, inlineResult, reason);

    public bool canTailCall(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE declaredCalleeHnd, CORINFO_METHOD_HANDLE exactCalleeHnd, bool fIsTailPrefix) => lpVtbl->Base.Base.canTailCall((ICorJitInfo*)(Unsafe.AsPointer(ref this)), callerHnd, declaredCalleeHnd, exactCalleeHnd, fIsTailPrefix);

    public void reportTailCallDecision(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd, bool fIsTailPrefix, CorInfoTailCall tailCallResult, byte* reason) => lpVtbl->Base.Base.reportTailCallDecision((ICorJitInfo*)(Unsafe.AsPointer(ref this)), callerHnd, calleeHnd, fIsTailPrefix, tailCallResult, reason);

    public void getEHinfo(CORINFO_METHOD_HANDLE ftn, int EHnumber, CORINFO_EH_CLAUSE* clause) => lpVtbl->Base.Base.getEHinfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, EHnumber, clause);

    public CORINFO_CLASS_HANDLE getMethodClass(CORINFO_METHOD_HANDLE method) => lpVtbl->Base.Base.getMethodClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), method);

    public void getMethodVTableOffset(CORINFO_METHOD_HANDLE method, int* offsetOfIndirection, int* offsetAfterIndirection, bool* isRelative) => lpVtbl->Base.Base.getMethodVTableOffset((ICorJitInfo*)(Unsafe.AsPointer(ref this)), method, offsetOfIndirection, offsetAfterIndirection, isRelative);

    public bool resolveVirtualMethod(CORINFO_DEVIRTUALIZATION_INFO* info) => lpVtbl->Base.Base.resolveVirtualMethod((ICorJitInfo*)(Unsafe.AsPointer(ref this)), info);

    public CORINFO_METHOD_HANDLE getUnboxedEntry(CORINFO_METHOD_HANDLE ftn, bool* requiresInstMethodTableArg) => lpVtbl->Base.Base.getUnboxedEntry((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, requiresInstMethodTableArg);

    public CORINFO_METHOD_HANDLE getInstantiatedEntry(CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_HANDLE* methodArg, CORINFO_CLASS_HANDLE* classArg) => lpVtbl->Base.Base.getInstantiatedEntry((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, methodArg, classArg);

    public CORINFO_METHOD_HANDLE getAsyncOtherVariant(CORINFO_METHOD_HANDLE ftn, bool* variantIsThunk) => lpVtbl->Base.Base.getAsyncOtherVariant((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, variantIsThunk);

    public CORINFO_CLASS_HANDLE getDefaultComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->Base.Base.getDefaultComparerClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), elemType);

    public CORINFO_CLASS_HANDLE getDefaultEqualityComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->Base.Base.getDefaultEqualityComparerClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), elemType);

    public CORINFO_CLASS_HANDLE getSZArrayHelperEnumeratorClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->Base.Base.getSZArrayHelperEnumeratorClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), elemType);

    public void expandRawHandleIntrinsic(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_GENERICHANDLE_RESULT* pResult) => lpVtbl->Base.Base.expandRawHandleIntrinsic((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, callerHandle, pResult);

    public bool isIntrinsicType(CORINFO_CLASS_HANDLE classHnd) => lpVtbl->Base.Base.isIntrinsicType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), classHnd);

    public CorInfoCallConvExtension getUnmanagedCallConv(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig, bool* pSuppressGCTransition) => lpVtbl->Base.Base.getUnmanagedCallConv((ICorJitInfo*)(Unsafe.AsPointer(ref this)), method, callSiteSig, pSuppressGCTransition);

    public bool pInvokeMarshalingRequired(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig) => lpVtbl->Base.Base.pInvokeMarshalingRequired((ICorJitInfo*)(Unsafe.AsPointer(ref this)), method, callSiteSig);

    public bool satisfiesMethodConstraints(CORINFO_CLASS_HANDLE parent, CORINFO_METHOD_HANDLE method) => lpVtbl->Base.Base.satisfiesMethodConstraints((ICorJitInfo*)(Unsafe.AsPointer(ref this)), parent, method);

    public void methodMustBeLoadedBeforeCodeIsRun(CORINFO_METHOD_HANDLE method) => lpVtbl->Base.Base.methodMustBeLoadedBeforeCodeIsRun((ICorJitInfo*)(Unsafe.AsPointer(ref this)), method);

    public void getGSCookie(GSCookie* pCookieVal, GSCookie** ppCookieVal) => lpVtbl->Base.Base.getGSCookie((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pCookieVal, ppCookieVal);

    public void setPatchpointInfo(PatchpointInfo* patchpointInfo) => lpVtbl->Base.Base.setPatchpointInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), patchpointInfo);

    public PatchpointInfo* getOSRInfo(int* ilOffset) => lpVtbl->Base.Base.getOSRInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ilOffset);

    //
    // ICorModuleInfo
    //

    public void resolveToken(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->Base.Base.resolveToken((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken);

    public void findSig(CORINFO_MODULE_HANDLE module, int sigTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->Base.Base.findSig((ICorJitInfo*)(Unsafe.AsPointer(ref this)), module, sigTOK, context, sig);

    public void findCallSiteSig(CORINFO_MODULE_HANDLE module, int methTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->Base.Base.findCallSiteSig((ICorJitInfo*)(Unsafe.AsPointer(ref this)), module, methTOK, context, sig);

    public CORINFO_CLASS_HANDLE getTokenTypeAsHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->Base.Base.getTokenTypeAsHandle((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken);

    public int getStringLiteral(CORINFO_MODULE_HANDLE module, int metaTOK, char* buffer, int bufferSize, int startIndex = 0) => lpVtbl->Base.Base.getStringLiteral((ICorJitInfo*)(Unsafe.AsPointer(ref this)), module, metaTOK, buffer, bufferSize, startIndex);

    public nint printObjectDescription(CORINFO_OBJECT_HANDLE handle, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null) => lpVtbl->Base.Base.printObjectDescription((ICorJitInfo*)(Unsafe.AsPointer(ref this)), handle, buffer, bufferSize, pRequiredBufferSize);

    //
    // ICorClassInfo
    //

    public CorInfoType asCorInfoType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.asCorInfoType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public byte* getClassNameFromMetadata(CORINFO_CLASS_HANDLE cls, byte** namespaceName) => lpVtbl->Base.Base.getClassNameFromMetadata((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls, namespaceName);

    public CORINFO_CLASS_HANDLE getTypeInstantiationArgument(CORINFO_CLASS_HANDLE cls, int index) => lpVtbl->Base.Base.getTypeInstantiationArgument((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls, index);

    public CORINFO_CLASS_HANDLE getMethodInstantiationArgument(CORINFO_METHOD_HANDLE ftn, int index) => lpVtbl->Base.Base.getMethodInstantiationArgument((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, index);

    public nint printClassName(CORINFO_CLASS_HANDLE cls, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null) => lpVtbl->Base.Base.printClassName((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls, buffer, bufferSize, pRequiredBufferSize);

    public bool isValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.isValueClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoFlag getClassAttribs(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getClassAttribs((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public byte* getClassAssemblyName(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getClassAssemblyName((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public void* LongLifetimeMalloc(nint sz) => lpVtbl->Base.Base.LongLifetimeMalloc((ICorJitInfo*)(Unsafe.AsPointer(ref this)), sz);

    public void LongLifetimeFree(void* obj) => lpVtbl->Base.Base.LongLifetimeFree((ICorJitInfo*)(Unsafe.AsPointer(ref this)), obj);

    public bool getIsClassInitedFlagAddress(CORINFO_CLASS_HANDLE cls, CORINFO_CONST_LOOKUP* addr, int* offset) => lpVtbl->Base.Base.getIsClassInitedFlagAddress((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls, addr, offset);

    public void* getClassStaticDynamicInfo(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getClassStaticDynamicInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public void* getClassThreadStaticDynamicInfo(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getClassThreadStaticDynamicInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public bool getStaticBaseAddress(CORINFO_CLASS_HANDLE cls, bool isGc, CORINFO_CONST_LOOKUP* addr) => lpVtbl->Base.Base.getStaticBaseAddress((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls, isGc, addr);

    public int getClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getClassSize((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public int getHeapClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getHeapClassSize((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public bool canAllocateOnStack(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.canAllocateOnStack((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public int getClassAlignmentRequirement(CORINFO_CLASS_HANDLE cls, bool fDoubleAlignHint = false) => lpVtbl->Base.Base.getClassAlignmentRequirement((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls, fDoubleAlignHint);

    public int getClassGClayout(CORINFO_CLASS_HANDLE cls, CorInfoGCType* gcPtrs) => lpVtbl->Base.Base.getClassGClayout((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls, gcPtrs);

    public int getClassNumInstanceFields(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getClassNumInstanceFields((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CORINFO_FIELD_HANDLE getFieldInClass(CORINFO_CLASS_HANDLE clsHnd, int num) => lpVtbl->Base.Base.getFieldInClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), clsHnd, num);

    public GetTypeLayoutResult getTypeLayout(CORINFO_CLASS_HANDLE typeHnd, CORINFO_TYPE_LAYOUT_NODE* treeNodes, nint* numTreeNodes) => lpVtbl->Base.Base.getTypeLayout((ICorJitInfo*)(Unsafe.AsPointer(ref this)), typeHnd, treeNodes, numTreeNodes);

    public bool checkMethodModifier(CORINFO_METHOD_HANDLE hMethod, byte* modifier, bool fOptional) => lpVtbl->Base.Base.checkMethodModifier((ICorJitInfo*)(Unsafe.AsPointer(ref this)), hMethod, modifier, fOptional);

    public CorInfoHelpFunc getNewHelper(CORINFO_CLASS_HANDLE classHandle, bool* pHasSideEffects) => lpVtbl->Base.Base.getNewHelper((ICorJitInfo*)(Unsafe.AsPointer(ref this)), classHandle, pHasSideEffects);

    public CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_HANDLE arrayCls) => lpVtbl->Base.Base.getNewArrHelper((ICorJitInfo*)(Unsafe.AsPointer(ref this)), arrayCls);

    public CorInfoHelpFunc getCastingHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fThrowing) => lpVtbl->Base.Base.getCastingHelper((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, fThrowing);

    public CorInfoHelpFunc getSharedCCtorHelper(CORINFO_CLASS_HANDLE clsHnd) => lpVtbl->Base.Base.getSharedCCtorHelper((ICorJitInfo*)(Unsafe.AsPointer(ref this)), clsHnd);

    public CORINFO_CLASS_HANDLE getTypeForBox(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getTypeForBox((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoHelpFunc getBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getBoxHelper((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoHelpFunc getUnBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getUnBoxHelper((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CORINFO_OBJECT_HANDLE getRuntimeTypePointer(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getRuntimeTypePointer((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public bool isObjectImmutable(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->Base.Base.isObjectImmutable((ICorJitInfo*)(Unsafe.AsPointer(ref this)), objPtr);

    public bool getStringChar(CORINFO_OBJECT_HANDLE strObj, int index, ushort* value) => lpVtbl->Base.Base.getStringChar((ICorJitInfo*)(Unsafe.AsPointer(ref this)), strObj, index, value);

    public CORINFO_CLASS_HANDLE getObjectType(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->Base.Base.getObjectType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), objPtr);

    public bool getReadyToRunHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, CorInfoHelpFunc id, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CONST_LOOKUP* pLookup) => lpVtbl->Base.Base.getReadyToRunHelper((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, id, callerHandle, pLookup);

    public void getReadyToRunDelegateCtorHelper(CORINFO_RESOLVED_TOKEN* pTargetMethod, mdToken targetConstraint, CORINFO_CLASS_HANDLE delegateType, CORINFO_METHOD_HANDLE callerHandler, CORINFO_LOOKUP* pLookup) => lpVtbl->Base.Base.getReadyToRunDelegateCtorHelper((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pTargetMethod, targetConstraint, delegateType, callerHandler, pLookup);

    public CorInfoInitClassResult initClass(CORINFO_FIELD_HANDLE field, CORINFO_METHOD_HANDLE method, CORINFO_CONTEXT_HANDLE context) => lpVtbl->Base.Base.initClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field, method, context);

    public void classMustBeLoadedBeforeCodeIsRun(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.classMustBeLoadedBeforeCodeIsRun((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CORINFO_CLASS_HANDLE getBuiltinClass(CorInfoClassId classId) => lpVtbl->Base.Base.getBuiltinClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), classId);

    public CorInfoType getTypeForPrimitiveValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getTypeForPrimitiveValueClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoType getTypeForPrimitiveNumericClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getTypeForPrimitiveNumericClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public bool canCast(CORINFO_CLASS_HANDLE child, CORINFO_CLASS_HANDLE parent) => lpVtbl->Base.Base.canCast((ICorJitInfo*)(Unsafe.AsPointer(ref this)), child, parent);

    public TypeCompareState compareTypesForCast(CORINFO_CLASS_HANDLE fromClass, CORINFO_CLASS_HANDLE toClass) => lpVtbl->Base.Base.compareTypesForCast((ICorJitInfo*)(Unsafe.AsPointer(ref this)), fromClass, toClass);

    public TypeCompareState compareTypesForEquality(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->Base.Base.compareTypesForEquality((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls1, cls2);

    public bool isMoreSpecificType(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->Base.Base.isMoreSpecificType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls1, cls2);

    public bool isExactType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.isExactType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public TypeCompareState isGenericType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.isGenericType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public TypeCompareState isNullableType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.isNullableType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public TypeCompareState isEnum(CORINFO_CLASS_HANDLE cls, CORINFO_CLASS_HANDLE* underlyingType) => lpVtbl->Base.Base.isEnum((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls, underlyingType);

    public CORINFO_CLASS_HANDLE getParentType(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getParentType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoType getChildType(CORINFO_CLASS_HANDLE clsHnd, CORINFO_CLASS_HANDLE* clsRet) => lpVtbl->Base.Base.getChildType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), clsHnd, clsRet);

    public bool isSDArray(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.isSDArray((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public int getArrayRank(CORINFO_CLASS_HANDLE cls) => lpVtbl->Base.Base.getArrayRank((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cls);

    public CorInfoArrayIntrinsic getArrayIntrinsicID(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.Base.getArrayIntrinsicID((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public void* getArrayInitializationData(CORINFO_FIELD_HANDLE field, int size) => lpVtbl->Base.Base.getArrayInitializationData((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field, size);

    public CorInfoIsAccessAllowedResult canAccessClass(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_HELPER_DESC* pAccessHelper) => lpVtbl->Base.Base.canAccessClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, callerHandle, pAccessHelper);

    //
    // ICorFieldInfo
    //

    public nint printFieldName(CORINFO_FIELD_HANDLE field, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null) => lpVtbl->Base.Base.printFieldName((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field, buffer, bufferSize, pRequiredBufferSize);

    public CORINFO_CLASS_HANDLE getFieldClass(CORINFO_FIELD_HANDLE field) => lpVtbl->Base.Base.getFieldClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field);

    public CorInfoType getFieldType(CORINFO_FIELD_HANDLE field, CORINFO_CLASS_HANDLE* structType = null, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->Base.Base.getFieldType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field, structType, memberParent);

    public int getFieldOffset(CORINFO_FIELD_HANDLE field) => lpVtbl->Base.Base.getFieldOffset((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field);

    public void getFieldInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_ACCESS_FLAGS flags, CORINFO_FIELD_INFO* pResult) => lpVtbl->Base.Base.getFieldInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, callerHandle, flags, pResult);

    public int getThreadLocalFieldInfo(CORINFO_FIELD_HANDLE field, bool isGCType) => lpVtbl->Base.Base.getThreadLocalFieldInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field, isGCType);

    public void getThreadLocalStaticBlocksInfo(CORINFO_THREAD_STATIC_BLOCKS_INFO* pInfo, bool isGCType) => lpVtbl->Base.Base.getThreadLocalStaticBlocksInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pInfo, isGCType);

    public void getThreadLocalStaticInfo_NativeAOT(CORINFO_THREAD_STATIC_INFO_NATIVEAOT* pInfo) => lpVtbl->Base.Base.getThreadLocalStaticInfo_NativeAOT((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pInfo);

    public bool isFieldStatic(CORINFO_FIELD_HANDLE fldHnd) => lpVtbl->Base.Base.isFieldStatic((ICorJitInfo*)(Unsafe.AsPointer(ref this)), fldHnd);

    public int getArrayOrStringLength(CORINFO_OBJECT_HANDLE objHnd) => lpVtbl->Base.Base.getArrayOrStringLength((ICorJitInfo*)(Unsafe.AsPointer(ref this)), objHnd);

    //
    // ICorDebugInfo
    //

    public void getBoundaries(CORINFO_METHOD_HANDLE ftn, int* cILOffsets, int** pILOffsets, ICorDebugInfo.BoundaryTypes* implicitBoundaries) => lpVtbl->Base.Base.getBoundaries((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, cILOffsets, pILOffsets, implicitBoundaries);

    public void setBoundaries(CORINFO_METHOD_HANDLE ftn, int cMap, ICorDebugInfo.OffsetMapping* pMap) => lpVtbl->Base.Base.setBoundaries((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, cMap, pMap);

    public void getVars(CORINFO_METHOD_HANDLE ftn, int* cVars, ICorDebugInfo.ILVarInfo** vars, bool* extendOthers) => lpVtbl->Base.Base.getVars((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, cVars, vars, extendOthers);

    public void setVars(CORINFO_METHOD_HANDLE ftn, int cVars, ICorDebugInfo.NativeVarInfo* vars) => lpVtbl->Base.Base.setVars((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, cVars, vars);

    public void reportRichMappings(ICorDebugInfo.InlineTreeNode* inlineTreeNodes, int numInlineTreeNodes, ICorDebugInfo.RichOffsetMapping* mappings, int numMappings) => lpVtbl->Base.Base.reportRichMappings((ICorJitInfo*)(Unsafe.AsPointer(ref this)), inlineTreeNodes, numInlineTreeNodes, mappings, numMappings);

    public void reportAsyncDebugInfo(ICorDebugInfo.AsyncInfo* asyncInfo, ICorDebugInfo.AsyncSuspensionPoint* suspensionPoints, ICorDebugInfo.AsyncContinuationVarInfo* vars, int numVars) => lpVtbl->Base.Base.reportAsyncDebugInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), asyncInfo, suspensionPoints, vars, numVars);

    public void reportMetadata(byte* key, void* value, nint length) => lpVtbl->Base.Base.reportMetadata((ICorJitInfo*)(Unsafe.AsPointer(ref this)), key, value, length);

    //
    // Misc
    //

    public void* allocateArray(nint cBytes) => lpVtbl->Base.Base.allocateArray((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cBytes);

    public void freeArray(void* array) => lpVtbl->Base.Base.freeArray((ICorJitInfo*)(Unsafe.AsPointer(ref this)), array);

    //
    // ICorArgInfo
    //

    public CORINFO_ARG_LIST_HANDLE getArgNext(CORINFO_ARG_LIST_HANDLE args) => lpVtbl->Base.Base.getArgNext((ICorJitInfo*)(Unsafe.AsPointer(ref this)), args);

    public CorInfoTypeWithMod getArgType(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args, CORINFO_CLASS_HANDLE* vcTypeRet) => lpVtbl->Base.Base.getArgType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), sig, args, vcTypeRet);

    public int getExactClasses(CORINFO_CLASS_HANDLE baseType, int maxExactClasses, CORINFO_CLASS_HANDLE* exactClsRet) => lpVtbl->Base.Base.getExactClasses((ICorJitInfo*)(Unsafe.AsPointer(ref this)), baseType, maxExactClasses, exactClsRet);

    public CORINFO_CLASS_HANDLE getArgClass(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args) => lpVtbl->Base.Base.getArgClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), sig, args);

    public CorInfoHFAElemType getHFAType(CORINFO_CLASS_HANDLE hClass) => lpVtbl->Base.Base.getHFAType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), hClass);

    public bool runWithErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->Base.Base.runWithErrorTrap((ICorJitInfo*)(Unsafe.AsPointer(ref this)), function, parameter);

    public bool runWithSPMIErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->Base.Base.runWithSPMIErrorTrap((ICorJitInfo*)(Unsafe.AsPointer(ref this)), function, parameter);

    public void getEEInfo(CORINFO_EE_INFO* pEEInfoOut) => lpVtbl->Base.Base.getEEInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pEEInfoOut);

    public void getAsyncInfo(CORINFO_ASYNC_INFO* pAsyncInfoOut) => lpVtbl->Base.Base.getAsyncInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pAsyncInfoOut);

    //
    // Diagnostic methods
    //

    public mdMethodDef getMethodDefFromMethod(CORINFO_METHOD_HANDLE hMethod) => lpVtbl->Base.Base.getMethodDefFromMethod((ICorJitInfo*)(Unsafe.AsPointer(ref this)), hMethod);

    public nint printMethodName(CORINFO_METHOD_HANDLE ftn, byte* buffer, nint bufferSize, nint* pRequiredBufferSize = null) => lpVtbl->Base.Base.printMethodName((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, buffer, bufferSize, pRequiredBufferSize);

    public byte* getMethodNameFromMetadata(CORINFO_METHOD_HANDLE ftn, byte** className, byte** namespaceName, byte** enclosingClassName, nint maxEnclosingClassNames) => lpVtbl->Base.Base.getMethodNameFromMetadata((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, className, namespaceName, enclosingClassName, maxEnclosingClassNames);

    public int getMethodHash(CORINFO_METHOD_HANDLE ftn) => lpVtbl->Base.Base.getMethodHash((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn);

    public bool getSystemVAmd64PassStructInRegisterDescriptor(CORINFO_CLASS_HANDLE structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr) => lpVtbl->Base.Base.getSystemVAmd64PassStructInRegisterDescriptor((ICorJitInfo*)(Unsafe.AsPointer(ref this)), structHnd, structPassInRegDescPtr);

    public void getSwiftLowering(CORINFO_CLASS_HANDLE structHnd, CORINFO_SWIFT_LOWERING* pLowering) => lpVtbl->Base.Base.getSwiftLowering((ICorJitInfo*)(Unsafe.AsPointer(ref this)), structHnd, pLowering);

    public void getFpStructLowering(CORINFO_CLASS_HANDLE structHnd, CORINFO_FPSTRUCT_LOWERING* pLowering) => lpVtbl->Base.Base.getFpStructLowering((ICorJitInfo*)(Unsafe.AsPointer(ref this)), structHnd, pLowering);

    public CorInfoWasmType getWasmLowering(CORINFO_CLASS_HANDLE structHnd) => lpVtbl->Base.Base.getWasmLowering((ICorJitInfo*)(Unsafe.AsPointer(ref this)), structHnd);

    //
    // ICorDynamicInfo
    //

    public int getThreadTLSIndex(void** ppIndirection = null) => lpVtbl->Base.getThreadTLSIndex((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ppIndirection);

    public int* getAddrOfCaptureThreadGlobal(void** ppIndirection = null) => lpVtbl->Base.getAddrOfCaptureThreadGlobal((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ppIndirection);

    public void* getHelperFtn(CorInfoHelpFunc ftnNum, CORINFO_CONST_LOOKUP* pNativeEntrypoint, CORINFO_METHOD_HANDLE* pMethodHandle = null) => lpVtbl->Base.getHelperFtn((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftnNum, pNativeEntrypoint, pMethodHandle);

    public void getFunctionEntryPoint(CORINFO_METHOD_HANDLE ftn, CORINFO_CONST_LOOKUP* pResult, CORINFO_ACCESS_FLAGS accessFlags = CORINFO_ACCESS_ANY) => lpVtbl->Base.getFunctionEntryPoint((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, pResult, accessFlags);

    public void getFunctionFixedEntryPoint(CORINFO_METHOD_HANDLE ftn, bool isUnsafeFunctionPointer, CORINFO_CONST_LOOKUP* pResult) => lpVtbl->Base.getFunctionFixedEntryPoint((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftn, isUnsafeFunctionPointer, pResult);

    public CORINFO_MODULE_HANDLE embedModuleHandle(CORINFO_MODULE_HANDLE handle, void** ppIndirection = null) => lpVtbl->Base.embedModuleHandle((ICorJitInfo*)(Unsafe.AsPointer(ref this)), handle, ppIndirection);

    public CORINFO_CLASS_HANDLE embedClassHandle(CORINFO_CLASS_HANDLE handle, void** ppIndirection = null) => lpVtbl->Base.embedClassHandle((ICorJitInfo*)(Unsafe.AsPointer(ref this)), handle, ppIndirection);

    public CORINFO_METHOD_HANDLE embedMethodHandle(CORINFO_METHOD_HANDLE handle, void** ppIndirection = null) => lpVtbl->Base.embedMethodHandle((ICorJitInfo*)(Unsafe.AsPointer(ref this)), handle, ppIndirection);

    public CORINFO_FIELD_HANDLE embedFieldHandle(CORINFO_FIELD_HANDLE handle, void** ppIndirection = null) => lpVtbl->Base.embedFieldHandle((ICorJitInfo*)(Unsafe.AsPointer(ref this)), handle, ppIndirection);

    public void embedGenericHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fEmbedParent, CORINFO_METHOD_HANDLE callerHandle, CORINFO_GENERICHANDLE_RESULT* pResult) => lpVtbl->Base.embedGenericHandle((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, fEmbedParent, callerHandle, pResult);

    public void getLocationOfThisType(CORINFO_METHOD_HANDLE context, CORINFO_LOOKUP_KIND* pLookupKind) => lpVtbl->Base.getLocationOfThisType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), context, pLookupKind);

    public void getAddressOfPInvokeTarget(CORINFO_METHOD_HANDLE method, CORINFO_CONST_LOOKUP* pLookup) => lpVtbl->Base.getAddressOfPInvokeTarget((ICorJitInfo*)(Unsafe.AsPointer(ref this)), method, pLookup);

    public void* GetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig, void** ppIndirection = null) => lpVtbl->Base.GetCookieForPInvokeCalliSig((ICorJitInfo*)(Unsafe.AsPointer(ref this)), szMetaSig, ppIndirection);

    public void* GetCookieForInterpreterCalliSig(CORINFO_SIG_INFO* szMetaSig) => lpVtbl->Base.GetCookieForInterpreterCalliSig((ICorJitInfo*)(Unsafe.AsPointer(ref this)), szMetaSig);

    public CORINFO_JUST_MY_CODE_HANDLE getJustMyCodeHandle(CORINFO_METHOD_HANDLE method, CORINFO_JUST_MY_CODE_HANDLE** ppIndirection = null) => lpVtbl->Base.getJustMyCodeHandle((ICorJitInfo*)(Unsafe.AsPointer(ref this)), method, ppIndirection);

    public void GetProfilingHandle(bool* pbHookFunction, void** pProfilerHandle, bool* pbIndirectedHandles) => lpVtbl->Base.GetProfilingHandle((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pbHookFunction, pProfilerHandle, pbIndirectedHandles);

    public void getCallInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult) => lpVtbl->Base.getCallInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, pConstrainedResolvedToken, callerHandle, flags, pResult);

    public bool getStaticFieldContent(CORINFO_FIELD_HANDLE field, byte* buffer, int bufferSize, int valueOffset = 0, bool ignoreMovableObjects = true) => lpVtbl->Base.getStaticFieldContent((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field, buffer, bufferSize, valueOffset, ignoreMovableObjects);

    public bool getObjectContent(CORINFO_OBJECT_HANDLE obj, byte* buffer, int bufferSize, int valueOffset) => lpVtbl->Base.getObjectContent((ICorJitInfo*)(Unsafe.AsPointer(ref this)), obj, buffer, bufferSize, valueOffset);

    public CORINFO_CLASS_HANDLE getStaticFieldCurrentClass(CORINFO_FIELD_HANDLE field, bool* pIsSpeculative = null) => lpVtbl->Base.getStaticFieldCurrentClass((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field, pIsSpeculative);

    public CORINFO_VARARGS_HANDLE getVarArgsHandle(CORINFO_SIG_INFO* pSig, CORINFO_METHOD_HANDLE methHnd, void** ppIndirection = null) => lpVtbl->Base.getVarArgsHandle((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pSig, methHnd, ppIndirection);

    public InfoAccessType constructStringLiteral(CORINFO_MODULE_HANDLE module, mdToken metaTok, void** ppValue) => lpVtbl->Base.constructStringLiteral((ICorJitInfo*)(Unsafe.AsPointer(ref this)), module, metaTok, ppValue);

    public InfoAccessType emptyStringLiteral(void** ppValue) => lpVtbl->Base.emptyStringLiteral((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ppValue);

    public int getFieldThreadLocalStoreID(CORINFO_FIELD_HANDLE field, void** ppIndirection = null) => lpVtbl->Base.getFieldThreadLocalStoreID((ICorJitInfo*)(Unsafe.AsPointer(ref this)), field, ppIndirection);

    public CORINFO_METHOD_HANDLE GetDelegateCtor(CORINFO_METHOD_HANDLE methHnd, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE targetMethodHnd, DelegateCtorArgs* pCtorData) => lpVtbl->Base.GetDelegateCtor((ICorJitInfo*)(Unsafe.AsPointer(ref this)), methHnd, clsHnd, targetMethodHnd, pCtorData);

    public void MethodCompileComplete(CORINFO_METHOD_HANDLE methHnd) => lpVtbl->Base.MethodCompileComplete((ICorJitInfo*)(Unsafe.AsPointer(ref this)), methHnd);

    public bool getTailCallHelpers(CORINFO_RESOLVED_TOKEN* callToken, CORINFO_SIG_INFO* sig, CORINFO_GET_TAILCALL_HELPERS_FLAGS flags, CORINFO_TAILCALL_HELPERS* pResult) => lpVtbl->Base.getTailCallHelpers((ICorJitInfo*)(Unsafe.AsPointer(ref this)), callToken, sig, flags, pResult);

    public CORINFO_METHOD_HANDLE getAsyncResumptionStub(void** entryPoint) => lpVtbl->Base.getAsyncResumptionStub((ICorJitInfo*)(Unsafe.AsPointer(ref this)), entryPoint);

    public CORINFO_CLASS_HANDLE getContinuationType(nint dataSize, bool* objRefs, nint objRefsSize) => lpVtbl->Base.getContinuationType((ICorJitInfo*)(Unsafe.AsPointer(ref this)), dataSize, objRefs, objRefsSize);

    public bool convertPInvokeCalliToCall(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fMustConvert) => lpVtbl->Base.convertPInvokeCalliToCall((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pResolvedToken, fMustConvert);

    public bool notifyInstructionSetUsage(CORINFO_InstructionSet instructionSet, bool supportEnabled) => lpVtbl->Base.notifyInstructionSetUsage((ICorJitInfo*)(Unsafe.AsPointer(ref this)), instructionSet, supportEnabled);

    public void updateEntryPointForTailCall(CORINFO_CONST_LOOKUP* entryPoint) => lpVtbl->Base.updateEntryPointForTailCall((ICorJitInfo*)(Unsafe.AsPointer(ref this)), entryPoint);

    public CORINFO_WASM_TYPE_SYMBOL_HANDLE getWasmTypeSymbol(CorInfoWasmType* types, nint typesSize) => lpVtbl->Base.getWasmTypeSymbol((ICorJitInfo*)(Unsafe.AsPointer(ref this)), types, typesSize);

    public CORINFO_METHOD_HANDLE getSpecialCopyHelper(CORINFO_CLASS_HANDLE type) => lpVtbl->Base.getSpecialCopyHelper((ICorJitInfo*)(Unsafe.AsPointer(ref this)), type);

    //
    // ICorJitInfo
    //

    public void allocMem(AllocMemArgs* pArgs) => lpVtbl->allocMem((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pArgs);

    public void reserveUnwindInfo(bool isFunclet, bool isColdCode, int unwindSize) => lpVtbl->reserveUnwindInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), isFunclet, isColdCode, unwindSize);

    public void allocUnwindInfo(byte* pHotCode, byte* pColdCode, int startOffset, int endOffset, int unwindSize, byte* pUnwindBlock, CorJitFuncKind funcKind) => lpVtbl->allocUnwindInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), pHotCode, pColdCode, startOffset, endOffset, unwindSize, pUnwindBlock, funcKind);

    public void* allocGCInfo(nint size) => lpVtbl->allocGCInfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), size);

    public void setEHcount(int cEH) => lpVtbl->setEHcount((ICorJitInfo*)(Unsafe.AsPointer(ref this)), cEH);

    public void setEHinfo(int EHnumber, CORINFO_EH_CLAUSE* clause) => lpVtbl->setEHinfo((ICorJitInfo*)(Unsafe.AsPointer(ref this)), EHnumber, clause);

    public bool logMsg(int level, byte* fmt, void* args) => lpVtbl->logMsg((ICorJitInfo*)(Unsafe.AsPointer(ref this)), level, fmt, args);

    public int doAssert(byte* szFile, int iLine, byte* szExpr) => lpVtbl->doAssert((ICorJitInfo*)(Unsafe.AsPointer(ref this)), szFile, iLine, szExpr);

    public void reportFatalError(CorJitResult result) => lpVtbl->reportFatalError((ICorJitInfo*)(Unsafe.AsPointer(ref this)), result);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnknownHandle(nint handle) => handle is >= UNKNOWN_HANDLE_MIN and <= UNKNOWN_HANDLE_MAX;

    public JITINTERFACE_HRESULT getPgoInstrumentationResults(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema** pSchema, int* pCountSchemaItems, byte** pInstrumentationData, PgoSource* pPgoSource, bool* pDynamicPgo) => lpVtbl->getPgoInstrumentationResults((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftnHnd, pSchema, pCountSchemaItems, pInstrumentationData, pPgoSource, pDynamicPgo);

    public JITINTERFACE_HRESULT allocPgoInstrumentationBySchema(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema* pSchema, int countSchemaItems, byte** pInstrumentationData) => lpVtbl->allocPgoInstrumentationBySchema((ICorJitInfo*)(Unsafe.AsPointer(ref this)), ftnHnd, pSchema, countSchemaItems, pInstrumentationData);

    public void recordCallSite(int instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_HANDLE methodHandle) => lpVtbl->recordCallSite((ICorJitInfo*)(Unsafe.AsPointer(ref this)), instrOffset, callSig, methodHandle);

    public void recordWasmManagedCallSig(CORINFO_SIG_INFO* callSig) => lpVtbl->recordWasmManagedCallSig((ICorJitInfo*)(Unsafe.AsPointer(ref this)), callSig);

    public void recordRelocation(void* location, void* locationRW, void* target, ushort fRelocType, int addlDelta = 0) => lpVtbl->recordRelocation((ICorJitInfo*)(Unsafe.AsPointer(ref this)), location, locationRW, target, fRelocType, addlDelta);

    public ushort getRelocTypeHint(void* target) => lpVtbl->getRelocTypeHint((ICorJitInfo*)(Unsafe.AsPointer(ref this)), target);

    public CorInfoArch getExpectedTargetArchitecture() => lpVtbl->getExpectedTargetArchitecture((ICorJitInfo*)(Unsafe.AsPointer(ref this)));

    public int getJitFlags(CORJIT_FLAGS* flags, int sizeInBytes) => lpVtbl->getJitFlags((ICorJitInfo*)(Unsafe.AsPointer(ref this)), flags, sizeInBytes);

    public interface Interface : ICorDynamicInfo.Interface
    {
        // get a block of memory for the code, readonly data, and read-write data
        void allocMem(AllocMemArgs* pArgs);

        // Reserve memory for the method/funclet's unwind information.
        // Note that this must be called before allocMem. It should be
        // called once for the main method, once for every funclet, and
        // once for every block of cold code for which allocUnwindInfo
        // will be called.
        //
        // This is necessary because jitted code must allocate all the
        // memory needed for the unwindInfo at the allocMem call.
        // For prejitted code we split up the unwinding information into
        // separate sections .rdata and .pdata.
        //
        void reserveUnwindInfo(bool isFunclet, bool isColdCode, int unwindSize);

        // Allocate and initialize the .rdata and .pdata for this method or
        // funclet, and get the block of memory needed for the machine-specific
        // unwind information (the info for crawling the stack frame).
        // Note that allocMem must be called first.
        //
        // Parameters:
        //
        //    pHotCode        main method code buffer, always filled in
        //    pColdCode       cold code buffer, only filled in if this is cold code,
        //                      null otherwise
        //    startOffset     start of code block, relative to appropriate code buffer
        //                      (e.g. pColdCode if cold, pHotCode if hot).
        //    endOffset       end of code block, relative to appropriate code buffer
        //    unwindSize      size of unwind info pointed to by pUnwindBlock
        //    pUnwindBlock    pointer to unwind info
        //    funcKind        type of funclet (main method code, handler, filter)
        //
        void allocUnwindInfo(byte* pHotCode, byte* pColdCode, int startOffset, int endOffset, int unwindSize, byte* pUnwindBlock, CorJitFuncKind funcKind);

        // Get a block of memory needed for the code manager information,
        // (the info for enumerating the GC pointers while crawling the
        // stack frame). Note that allocMem must be called first.
        void* allocGCInfo(nint size);

        // Indicate how many exception handler blocks are to be returned.
        // This is guaranteed to be called before any 'setEHinfo' call.
        // Note that allocMem must be called before this method can be called.
        void setEHcount(int cEH);

        // Set the values for one particular exception handler block.
        //
        // Handler regions should be lexically contiguous.
        // This is because FinallyIsUnwinding() uses lexicality to
        // determine if a "finally" clause is executing.
        void setEHinfo(int EHnumber, CORINFO_EH_CLAUSE* clause);

        // Level -> fatalError, Level 2 -> Error, Level 3 -> Warning
        // Level 4 means happens 10 times in a run, level 5 means 100, level 6 means 1000 ...
        // returns non-zero if the logging succeeded
        bool logMsg(int level, byte* fmt, void* args);

        // do an assert.  will return true if the code should retry (DebugBreak)
        // returns false, if the assert should be ignored.
        int doAssert(byte* szFile, int iLine, byte* szExpr);

        void reportFatalError(CorJitResult result);

        // get profile information to be used for optimizing a current method.  The format
        // of the buffer is the same as the format the JIT passes to allocPgoInstrumentationBySchema.
        //
        // pSchema
        //   pointer to the schema table (array) which describes the instrumentation results
        //   (pointer will not remain valid after jit completes).
        //
        // pCountSchemaItems
        //   pointer to the count of schema items in `pSchema` array.
        //
        // pInstrumentationData
        //   `*pInstrumentationData` is set to the address of the instrumentation data
        //    (pointer will not remain valid after jit completes).
        //
        // pPgoSource
        //   value describing source of pgo data
        //
        // pDynamicPgo
        //   dynamic PGO is enabled (valid even when return value is failure)
        //
        JITINTERFACE_HRESULT getPgoInstrumentationResults(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema** pSchema, int* pCountSchemaItems, byte** pInstrumentationData, PgoSource* pPgoSource, bool* pDynamicPgo);

        // Allocate a profile buffer for use in the current process
        // The JIT shall call this api with the schema entries other than Offset filled in.
        // The VM is responsible for allocating the buffer, and computing the various offsets
        // The offset calculation shall obey the following rules
        //  1. All data fields shall be naturally aligned.
        //  2. The first offset may be arbitrarily large.
        //  3. The JIT may mark a schema item with an alignment flag. This may be used to increase the alignment of a field.
        //  4. Each data entry shall be laid out without extra padding.
        //
        //  The intention here is that it becomes possible to describe a C data structure with the alignment for ease of use with
        //  instrumentation helper functions
        //
        // pSchema
        //   pointer to the schema table (array) which describes the instrumentation results. `Offset` field
        //   is filled in by VM; other fields are set and passed in by caller.
        //
        // countSchemaItems
        //   count of schema items in `pSchema` array.
        //
        // pInstrumentationData
        //   `*pInstrumentationData` is set to the address of the instrumentation data.
        //
        JITINTERFACE_HRESULT allocPgoInstrumentationBySchema(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema* pSchema, int countSchemaItems, byte** pInstrumentationData);

        // Associates a native call site, identified by its offset in the native code stream, with
        // the signature information and method handle the JIT used to lay out the call site. If
        // the call site has no signature information (e.g. a helper call) or has no method handle
        // (e.g. a CALLI P/Invoke), then null should be passed instead.
        void recordCallSite(int instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_HANDLE methodHandle);

        // Records the signature of a managed call site for Wasm R2R thunk generation.
        // This is a no-op on all targets except ReadyToRun Wasm compilation.
        void recordWasmManagedCallSig(CORINFO_SIG_INFO* callSig);

        // A relocation is recorded if we are pre-jitting.
        // A jump thunk may be inserted if we are jitting
        void recordRelocation(void* location, void* locationRW, void* target, ushort fRelocType, int addlDelta = 0);

        ushort getRelocTypeHint(void* target);

        // For what machine does the VM expect the JIT to generate code? The VM
        // returns one of the IMAGE_FILE_MACHINE_* values. Note that if the VM
        // is cross-compiling (such as the case for crossgen), it will return a
        // different value than if it was compiling for the host architecture.
        //
        CorInfoArch getExpectedTargetArchitecture();

        // Fetches extended flags for a particular compilation instance. Returns
        // the number of bytes written to the provided buffer.
        //
        // flags
        //   Points to a buffer that will hold the extended flags.
        //
        // sizeInBytes
        //   The size of the buffer. Note that this is effectively a version number for the CORJIT_FLAGS value
        //
        int getJitFlags(CORJIT_FLAGS* flags, int sizeInBytes);
    }

    public struct Vtbl<TSelf>
        where TSelf : unmanaged, Interface
    {
        public ICorDynamicInfo.Vtbl<TSelf> Base;

        //
        // ICorJitInfo
        //

        public delegate* unmanaged[MemberFunction]<TSelf*, AllocMemArgs*, void> allocMem;

        public delegate* unmanaged[MemberFunction]<TSelf*, bool, bool, int, void> reserveUnwindInfo;

        public delegate* unmanaged[MemberFunction]<TSelf*, byte*, byte*, int, int, int, byte*, CorJitFuncKind, void> allocUnwindInfo;

        public delegate* unmanaged[MemberFunction]<TSelf*, nint, void*> allocGCInfo;

        public delegate* unmanaged[MemberFunction]<TSelf*, int, void> setEHcount;

        public delegate* unmanaged[MemberFunction]<TSelf*, int, CORINFO_EH_CLAUSE*, void> setEHinfo;

        public delegate* unmanaged[MemberFunction]<TSelf*, int, byte*, void*, bool> logMsg;

        public delegate* unmanaged[MemberFunction]<TSelf*, byte*, int, byte*, int> doAssert;

        public delegate* unmanaged[MemberFunction]<TSelf*, CorJitResult, void> reportFatalError;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, PgoInstrumentationSchema**, int*, byte**, PgoSource*, bool*, JITINTERFACE_HRESULT> getPgoInstrumentationResults;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_METHOD_HANDLE, PgoInstrumentationSchema*, int, byte**, JITINTERFACE_HRESULT> allocPgoInstrumentationBySchema;

        public delegate* unmanaged[MemberFunction]<TSelf*, int, CORINFO_SIG_INFO*, CORINFO_METHOD_HANDLE, void> recordCallSite;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORINFO_SIG_INFO*, void> recordWasmManagedCallSig;

        public delegate* unmanaged[MemberFunction]<TSelf*, void*, void*, void*, ushort, int, void> recordRelocation;

        public delegate* unmanaged[MemberFunction]<TSelf*, void*, ushort> getRelocTypeHint;

        public delegate* unmanaged[MemberFunction]<TSelf*, CorInfoArch> getExpectedTargetArchitecture;

        public delegate* unmanaged[MemberFunction]<TSelf*, CORJIT_FLAGS*, int, int> getJitFlags;
    }
}
