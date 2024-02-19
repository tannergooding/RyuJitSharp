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
    internal Vtbl* lpVtbl;

    //
    // ICorMethodInfo
    //

    public bool isIntrinsic(CORINFO_METHOD_HANDLE ftn) => lpVtbl->isIntrinsic((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn);

    public bool notifyMethodInfoUsage(CORINFO_METHOD_HANDLE ftn) => lpVtbl->notifyMethodInfoUsage((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn);

    public uint getMethodAttribs(CORINFO_METHOD_HANDLE ftn) => lpVtbl->getMethodAttribs((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn);

    public void setMethodAttribs(CORINFO_METHOD_HANDLE ftn, CorInfoMethodRuntimeFlags attribs) => lpVtbl->setMethodAttribs((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, attribs);

    public void getMethodSig(CORINFO_METHOD_HANDLE ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->getMethodSig((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, sig, memberParent);

    public bool getMethodInfo(CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_INFO* info, CORINFO_CONTEXT_HANDLE context = null) => lpVtbl->getMethodInfo((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, info, context);

    public bool haveSameMethodDefinition(CORINFO_METHOD_HANDLE meth1Hnd, CORINFO_METHOD_HANDLE meth2Hnd) => lpVtbl->haveSameMethodDefinition((ICorJitInfo*)Unsafe.AsPointer(ref this), meth1Hnd, meth2Hnd);

    public CorInfoInline canInline(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd) => lpVtbl->canInline((ICorJitInfo*)Unsafe.AsPointer(ref this), callerHnd, calleeHnd);

    public void beginInlining(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd) => lpVtbl->beginInlining((ICorJitInfo*)Unsafe.AsPointer(ref this), inlinerHnd, inlineeHnd);

    public void reportInliningDecision(CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd, CorInfoInline inlineResult, sbyte* reason) => lpVtbl->reportInliningDecision((ICorJitInfo*)Unsafe.AsPointer(ref this), inlinerHnd, inlineeHnd, inlineResult, reason);

    public bool canTailCall(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE declaredCalleeHnd, CORINFO_METHOD_HANDLE exactCalleeHnd, bool fIsTailPrefix) => lpVtbl->canTailCall((ICorJitInfo*)Unsafe.AsPointer(ref this), callerHnd, declaredCalleeHnd, exactCalleeHnd, fIsTailPrefix);

    public void reportTailCallDecision(CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd, bool fIsTailPrefix, CorInfoTailCall tailCallResult, sbyte* reason) => lpVtbl->reportTailCallDecision((ICorJitInfo*)Unsafe.AsPointer(ref this), callerHnd, calleeHnd, fIsTailPrefix, tailCallResult, reason);

    public void getEHinfo(CORINFO_METHOD_HANDLE ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause) => lpVtbl->getEHinfo((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, EHnumber, clause);

    public CORINFO_CLASS_HANDLE getMethodClass(CORINFO_METHOD_HANDLE method) => lpVtbl->getMethodClass((ICorJitInfo*)Unsafe.AsPointer(ref this), method);

    public void getMethodVTableOffset(CORINFO_METHOD_HANDLE method, uint* offsetOfIndirection, uint* offsetAfterIndirection, bool* isRelative) => lpVtbl->getMethodVTableOffset((ICorJitInfo*)Unsafe.AsPointer(ref this), method, offsetOfIndirection, offsetAfterIndirection, isRelative);

    public bool resolveVirtualMethod(CORINFO_DEVIRTUALIZATION_INFO* info) => lpVtbl->resolveVirtualMethod((ICorJitInfo*)Unsafe.AsPointer(ref this), info);

    public CORINFO_METHOD_HANDLE getUnboxedEntry(CORINFO_METHOD_HANDLE ftn, bool* requiresInstMethodTableArg) => lpVtbl->getUnboxedEntry((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, requiresInstMethodTableArg);

    public CORINFO_CLASS_HANDLE getDefaultComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->getDefaultComparerClass((ICorJitInfo*)Unsafe.AsPointer(ref this), elemType);

    public CORINFO_CLASS_HANDLE getDefaultEqualityComparerClass(CORINFO_CLASS_HANDLE elemType) => lpVtbl->getDefaultEqualityComparerClass((ICorJitInfo*)Unsafe.AsPointer(ref this), elemType);

    public void expandRawHandleIntrinsic(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_GENERICHANDLE_RESULT* pResult) => lpVtbl->expandRawHandleIntrinsic((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken, pResult);

    public bool isIntrinsicType(CORINFO_CLASS_HANDLE classHnd) => lpVtbl->isIntrinsicType((ICorJitInfo*)Unsafe.AsPointer(ref this), classHnd);

    public CorInfoCallConvExtension getUnmanagedCallConv(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig, bool* pSuppressGCTransition) => lpVtbl->getUnmanagedCallConv((ICorJitInfo*)Unsafe.AsPointer(ref this), method, callSiteSig, pSuppressGCTransition);

    public bool pInvokeMarshalingRequired(CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig) => lpVtbl->pInvokeMarshalingRequired((ICorJitInfo*)Unsafe.AsPointer(ref this), method, callSiteSig);

    public bool satisfiesMethodConstraints(CORINFO_CLASS_HANDLE parent, CORINFO_METHOD_HANDLE method) => lpVtbl->satisfiesMethodConstraints((ICorJitInfo*)Unsafe.AsPointer(ref this), parent, method);

    public void methodMustBeLoadedBeforeCodeIsRun(CORINFO_METHOD_HANDLE method) => lpVtbl->methodMustBeLoadedBeforeCodeIsRun((ICorJitInfo*)Unsafe.AsPointer(ref this), method);

    public CORINFO_METHOD_HANDLE mapMethodDeclToMethodImpl(CORINFO_METHOD_HANDLE method) => lpVtbl->mapMethodDeclToMethodImpl((ICorJitInfo*)Unsafe.AsPointer(ref this), method);

    public void getGSCookie(GSCookie* pCookieVal, GSCookie** ppCookieVal) => lpVtbl->getGSCookie((ICorJitInfo*)Unsafe.AsPointer(ref this), pCookieVal, ppCookieVal);

    public void setPatchpointInfo(PatchpointInfo* patchpointInfo) => lpVtbl->setPatchpointInfo((ICorJitInfo*)Unsafe.AsPointer(ref this), patchpointInfo);

    public PatchpointInfo* getOSRInfo(uint* ilOffset) => lpVtbl->getOSRInfo((ICorJitInfo*)Unsafe.AsPointer(ref this), ilOffset);

    //
    // ICorModuleInfo
    //

    public void resolveToken(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->resolveToken((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken);

    public void findSig(CORINFO_MODULE_HANDLE module, uint sigTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->findSig((ICorJitInfo*)Unsafe.AsPointer(ref this), module, sigTOK, context, sig);

    public void findCallSiteSig(CORINFO_MODULE_HANDLE module, uint methTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig) => lpVtbl->findCallSiteSig((ICorJitInfo*)Unsafe.AsPointer(ref this), module, methTOK, context, sig);

    public CORINFO_CLASS_HANDLE getTokenTypeAsHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken) => lpVtbl->getTokenTypeAsHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken);

    public int getStringLiteral(CORINFO_MODULE_HANDLE module, uint metaTOK, char* buffer, int bufferSize, int startIndex = 0) => lpVtbl->getStringLiteral((ICorJitInfo*)Unsafe.AsPointer(ref this), module, metaTOK, buffer, bufferSize, 0);

    public nuint printObjectDescription(CORINFO_OBJECT_HANDLE handle, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printObjectDescription((ICorJitInfo*)Unsafe.AsPointer(ref this), handle, buffer, bufferSize, null);

    //
    // ICorClassInfo
    //

    public CorInfoType asCorInfoType(CORINFO_CLASS_HANDLE cls) => lpVtbl->asCorInfoType((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public sbyte* getClassNameFromMetadata(CORINFO_CLASS_HANDLE cls, sbyte** namespaceName) => lpVtbl->getClassNameFromMetadata((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, namespaceName);

    public CORINFO_CLASS_HANDLE getTypeInstantiationArgument(CORINFO_CLASS_HANDLE cls, uint index) => lpVtbl->getTypeInstantiationArgument((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, index);

    public nuint printClassName(CORINFO_CLASS_HANDLE cls, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printClassName((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, buffer, bufferSize, null);

    public bool isValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->isValueClass((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getClassAttribs(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassAttribs((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_MODULE_HANDLE getClassModule(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassModule((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_ASSEMBLY_HANDLE getModuleAssembly(CORINFO_MODULE_HANDLE mod) => lpVtbl->getModuleAssembly((ICorJitInfo*)Unsafe.AsPointer(ref this), mod);

    public sbyte* getAssemblyName(CORINFO_ASSEMBLY_HANDLE assem) => lpVtbl->getAssemblyName((ICorJitInfo*)Unsafe.AsPointer(ref this), assem);

    public void* LongLifetimeMalloc(nuint sz) => lpVtbl->LongLifetimeMalloc((ICorJitInfo*)Unsafe.AsPointer(ref this), sz);

    public void LongLifetimeFree(void* obj) => lpVtbl->LongLifetimeFree((ICorJitInfo*)Unsafe.AsPointer(ref this), obj);

    public nuint getClassModuleIdForStatics(CORINFO_CLASS_HANDLE cls, CORINFO_MODULE_HANDLE* pModule, void** ppIndirection) => lpVtbl->getClassModuleIdForStatics((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, pModule, ppIndirection);

    public bool getIsClassInitedFlagAddress(CORINFO_CLASS_HANDLE cls, CORINFO_CONST_LOOKUP* addr, int* offset) => lpVtbl->getIsClassInitedFlagAddress((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, addr, offset);

    public bool getStaticBaseAddress(CORINFO_CLASS_HANDLE cls, bool isGc, CORINFO_CONST_LOOKUP* addr) => lpVtbl->getStaticBaseAddress((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, isGc, addr);

    public uint getClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassSize((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getHeapClassSize(CORINFO_CLASS_HANDLE cls) => lpVtbl->getHeapClassSize((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public bool canAllocateOnStack(CORINFO_CLASS_HANDLE cls) => lpVtbl->canAllocateOnStack((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getClassAlignmentRequirement(CORINFO_CLASS_HANDLE cls, bool fDoubleAlignHint = false) => lpVtbl->getClassAlignmentRequirement((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, false);

    public uint getClassGClayout(CORINFO_CLASS_HANDLE cls, byte* gcPtrs) => lpVtbl->getClassGClayout((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, gcPtrs);

    public uint getClassNumInstanceFields(CORINFO_CLASS_HANDLE cls) => lpVtbl->getClassNumInstanceFields((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_FIELD_HANDLE getFieldInClass(CORINFO_CLASS_HANDLE clsHnd, int num) => lpVtbl->getFieldInClass((ICorJitInfo*)Unsafe.AsPointer(ref this), clsHnd, num);

    public GetTypeLayoutResult getTypeLayout(CORINFO_CLASS_HANDLE typeHnd, CORINFO_TYPE_LAYOUT_NODE* treeNodes, nuint* numTreeNodes) => lpVtbl->getTypeLayout((ICorJitInfo*)Unsafe.AsPointer(ref this), typeHnd, treeNodes, numTreeNodes);

    public bool checkMethodModifier(CORINFO_METHOD_HANDLE hMethod, sbyte* modifier, bool fOptional) => lpVtbl->checkMethodModifier((ICorJitInfo*)Unsafe.AsPointer(ref this), hMethod, modifier, fOptional);

    public CorInfoHelpFunc getNewHelper(CORINFO_CLASS_HANDLE classHandle, bool* pHasSideEffects) => lpVtbl->getNewHelper((ICorJitInfo*)Unsafe.AsPointer(ref this), classHandle, pHasSideEffects);

    public CorInfoHelpFunc getNewArrHelper(CORINFO_CLASS_HANDLE arrayCls) => lpVtbl->getNewArrHelper((ICorJitInfo*)Unsafe.AsPointer(ref this), arrayCls);

    public CorInfoHelpFunc getCastingHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fThrowing) => lpVtbl->getCastingHelper((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken, fThrowing);

    public CorInfoHelpFunc getSharedCCtorHelper(CORINFO_CLASS_HANDLE clsHnd) => lpVtbl->getSharedCCtorHelper((ICorJitInfo*)Unsafe.AsPointer(ref this), clsHnd);

    public CORINFO_CLASS_HANDLE getTypeForBox(CORINFO_CLASS_HANDLE cls) => lpVtbl->getTypeForBox((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoHelpFunc getBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->getBoxHelper((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoHelpFunc getUnBoxHelper(CORINFO_CLASS_HANDLE cls) => lpVtbl->getUnBoxHelper((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_OBJECT_HANDLE getRuntimeTypePointer(CORINFO_CLASS_HANDLE cls) => lpVtbl->getRuntimeTypePointer((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public bool isObjectImmutable(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->isObjectImmutable((ICorJitInfo*)Unsafe.AsPointer(ref this), objPtr);

    public bool getStringChar(CORINFO_OBJECT_HANDLE strObj, int index, ushort* value) => lpVtbl->getStringChar((ICorJitInfo*)Unsafe.AsPointer(ref this), strObj, index, value);

    public CORINFO_CLASS_HANDLE getObjectType(CORINFO_OBJECT_HANDLE objPtr) => lpVtbl->getObjectType((ICorJitInfo*)Unsafe.AsPointer(ref this), objPtr);

    public bool getReadyToRunHelper(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_LOOKUP_KIND* pGenericLookupKind, CorInfoHelpFunc id, CORINFO_CONST_LOOKUP* pLookup) => lpVtbl->getReadyToRunHelper((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken, pGenericLookupKind, id, pLookup);

    public void getReadyToRunDelegateCtorHelper(CORINFO_RESOLVED_TOKEN* pTargetMethod, mdToken targetConstraint, CORINFO_CLASS_HANDLE delegateType, CORINFO_LOOKUP* pLookup) => lpVtbl->getReadyToRunDelegateCtorHelper((ICorJitInfo*)Unsafe.AsPointer(ref this), pTargetMethod, targetConstraint, delegateType, pLookup);

    public CorInfoInitClassResult initClass(CORINFO_FIELD_HANDLE field, CORINFO_METHOD_HANDLE method, CORINFO_CONTEXT_HANDLE context) => lpVtbl->initClass((ICorJitInfo*)Unsafe.AsPointer(ref this), field, method, context);

    public void classMustBeLoadedBeforeCodeIsRun(CORINFO_CLASS_HANDLE cls) => lpVtbl->classMustBeLoadedBeforeCodeIsRun((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CORINFO_CLASS_HANDLE getBuiltinClass(CorInfoClassId classId) => lpVtbl->getBuiltinClass((ICorJitInfo*)Unsafe.AsPointer(ref this), classId);

    public CorInfoType getTypeForPrimitiveValueClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->getTypeForPrimitiveValueClass((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoType getTypeForPrimitiveNumericClass(CORINFO_CLASS_HANDLE cls) => lpVtbl->getTypeForPrimitiveNumericClass((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public bool canCast(CORINFO_CLASS_HANDLE child, CORINFO_CLASS_HANDLE parent) => lpVtbl->canCast((ICorJitInfo*)Unsafe.AsPointer(ref this), child, parent);

    public TypeCompareState compareTypesForCast(CORINFO_CLASS_HANDLE fromClass, CORINFO_CLASS_HANDLE toClass) => lpVtbl->compareTypesForCast((ICorJitInfo*)Unsafe.AsPointer(ref this), fromClass, toClass);

    public TypeCompareState compareTypesForEquality(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->compareTypesForEquality((ICorJitInfo*)Unsafe.AsPointer(ref this), cls1, cls2);

    public bool isMoreSpecificType(CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2) => lpVtbl->isMoreSpecificType((ICorJitInfo*)Unsafe.AsPointer(ref this), cls1, cls2);

    public bool isExactType(CORINFO_CLASS_HANDLE cls) => lpVtbl->isExactType((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public TypeCompareState isEnum(CORINFO_CLASS_HANDLE cls, CORINFO_CLASS_HANDLE* underlyingType) => lpVtbl->isEnum((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, underlyingType);

    public CORINFO_CLASS_HANDLE getParentType(CORINFO_CLASS_HANDLE cls) => lpVtbl->getParentType((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoType getChildType(CORINFO_CLASS_HANDLE clsHnd, CORINFO_CLASS_HANDLE* clsRet) => lpVtbl->getChildType((ICorJitInfo*)Unsafe.AsPointer(ref this), clsHnd, clsRet);

    public bool isSDArray(CORINFO_CLASS_HANDLE cls) => lpVtbl->isSDArray((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getArrayRank(CORINFO_CLASS_HANDLE cls) => lpVtbl->getArrayRank((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public CorInfoArrayIntrinsic getArrayIntrinsicID(CORINFO_METHOD_HANDLE ftn) => lpVtbl->getArrayIntrinsicID((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn);

    public void* getArrayInitializationData(CORINFO_FIELD_HANDLE field, uint size) => lpVtbl->getArrayInitializationData((ICorJitInfo*)Unsafe.AsPointer(ref this), field, size);

    public CorInfoIsAccessAllowedResult canAccessClass(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_HELPER_DESC* pAccessHelper) => lpVtbl->canAccessClass((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken, callerHandle, pAccessHelper);

    //
    // ICorFieldInfo
    //

    public nuint printFieldName(CORINFO_FIELD_HANDLE field, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printFieldName((ICorJitInfo*)Unsafe.AsPointer(ref this), field, buffer, bufferSize, null);

    public CORINFO_CLASS_HANDLE getFieldClass(CORINFO_FIELD_HANDLE field) => lpVtbl->getFieldClass((ICorJitInfo*)Unsafe.AsPointer(ref this), field);

    public CorInfoType getFieldType(CORINFO_FIELD_HANDLE field, CORINFO_CLASS_HANDLE* structType = null, CORINFO_CLASS_HANDLE memberParent = null) => lpVtbl->getFieldType((ICorJitInfo*)Unsafe.AsPointer(ref this), field, null, null);

    public uint getFieldOffset(CORINFO_FIELD_HANDLE field) => lpVtbl->getFieldOffset((ICorJitInfo*)Unsafe.AsPointer(ref this), field);

    public void getFieldInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_ACCESS_FLAGS flags, CORINFO_FIELD_INFO* pResult) => lpVtbl->getFieldInfo((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken, callerHandle, flags, pResult);

    public uint getThreadLocalFieldInfo(CORINFO_FIELD_HANDLE field, bool isGCType) => lpVtbl->getThreadLocalFieldInfo((ICorJitInfo*)Unsafe.AsPointer(ref this), field, isGCType);

    public void getThreadLocalStaticBlocksInfo(CORINFO_THREAD_STATIC_BLOCKS_INFO* pInfo, bool isGCType) => lpVtbl->getThreadLocalStaticBlocksInfo((ICorJitInfo*)Unsafe.AsPointer(ref this), pInfo, isGCType);

    public void getThreadLocalStaticInfo_NativeAOT(CORINFO_THREAD_STATIC_INFO_NATIVEAOT* pInfo) => lpVtbl->getThreadLocalStaticInfo_NativeAOT((ICorJitInfo*)Unsafe.AsPointer(ref this), pInfo);

    public bool isFieldStatic(CORINFO_FIELD_HANDLE fldHnd) => lpVtbl->isFieldStatic((ICorJitInfo*)Unsafe.AsPointer(ref this), fldHnd);

    public int getArrayOrStringLength(CORINFO_OBJECT_HANDLE objHnd) => lpVtbl->getArrayOrStringLength((ICorJitInfo*)Unsafe.AsPointer(ref this), objHnd);

    //
    // ICorDebugInfo
    //

    public void getBoundaries(CORINFO_METHOD_HANDLE ftn, uint* cILOffsets, uint** pILOffsets, ICorDebugInfo.BoundaryTypes* implicitBoundaries) => lpVtbl->getBoundaries((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, cILOffsets, pILOffsets, implicitBoundaries);

    public void setBoundaries(CORINFO_METHOD_HANDLE ftn, uint cMap, ICorDebugInfo.OffsetMapping* pMap) => lpVtbl->setBoundaries((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, cMap, pMap);

    public void getVars(CORINFO_METHOD_HANDLE ftn, uint* cVars, ICorDebugInfo.ILVarInfo** vars, bool* extendOthers) => lpVtbl->getVars((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, cVars, vars, extendOthers);

    public void setVars(CORINFO_METHOD_HANDLE ftn, uint cVars, ICorDebugInfo.NativeVarInfo* vars) => lpVtbl->setVars((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, cVars, vars);

    public void reportRichMappings(ICorDebugInfo.InlineTreeNode* inlineTreeNodes, uint numInlineTreeNodes, ICorDebugInfo.RichOffsetMapping* mappings, uint numMappings) => lpVtbl->reportRichMappings((ICorJitInfo*)Unsafe.AsPointer(ref this), inlineTreeNodes, numInlineTreeNodes, mappings, numMappings);

    //
    // Misc
    //

    public void* allocateArray(nuint cBytes) => lpVtbl->allocateArray((ICorJitInfo*)Unsafe.AsPointer(ref this), cBytes);

    public void freeArray(void* array) => lpVtbl->freeArray((ICorJitInfo*)Unsafe.AsPointer(ref this), array);

    //
    // ICorArgInfo
    //

    public CORINFO_ARG_LIST_HANDLE getArgNext(CORINFO_ARG_LIST_HANDLE args) => lpVtbl->getArgNext((ICorJitInfo*)Unsafe.AsPointer(ref this), args);

    public CorInfoTypeWithMod getArgType(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args, CORINFO_CLASS_HANDLE* vcTypeRet) => lpVtbl->getArgType((ICorJitInfo*)Unsafe.AsPointer(ref this), sig, args, vcTypeRet);

    public int getExactClasses(CORINFO_CLASS_HANDLE baseType, int maxExactClasses, CORINFO_CLASS_HANDLE* exactClsRet) => lpVtbl->getExactClasses((ICorJitInfo*)Unsafe.AsPointer(ref this), baseType, maxExactClasses, exactClsRet);

    public CORINFO_CLASS_HANDLE getArgClass(CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args) => lpVtbl->getArgClass((ICorJitInfo*)Unsafe.AsPointer(ref this), sig, args);

    public CorInfoHFAElemType getHFAType(CORINFO_CLASS_HANDLE hClass) => lpVtbl->getHFAType((ICorJitInfo*)Unsafe.AsPointer(ref this), hClass);

    public bool runWithErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->runWithErrorTrap((ICorJitInfo*)Unsafe.AsPointer(ref this), function, parameter);

    public bool runWithSPMIErrorTrap(errorTrapFunction function, void* parameter) => lpVtbl->runWithSPMIErrorTrap((ICorJitInfo*)Unsafe.AsPointer(ref this), function, parameter);

    public void getEEInfo(CORINFO_EE_INFO* pEEInfoOut) => lpVtbl->getEEInfo((ICorJitInfo*)Unsafe.AsPointer(ref this), pEEInfoOut);

    public char* getJitTimeLogFilename() => lpVtbl->getJitTimeLogFilename((ICorJitInfo*)Unsafe.AsPointer(ref this));

    //
    // Diagnostic methods
    //

    public mdMethodDef getMethodDefFromMethod(CORINFO_METHOD_HANDLE hMethod) => lpVtbl->getMethodDefFromMethod((ICorJitInfo*)Unsafe.AsPointer(ref this), hMethod);

    public nuint printMethodName(CORINFO_METHOD_HANDLE ftn, sbyte* buffer, nuint bufferSize, nuint* pRequiredBufferSize = null) => lpVtbl->printMethodName((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, buffer, bufferSize, null);

    public sbyte* getMethodNameFromMetadata(CORINFO_METHOD_HANDLE ftn, sbyte** className, sbyte** namespaceName, sbyte** enclosingClassName) => lpVtbl->getMethodNameFromMetadata((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, className, namespaceName, enclosingClassName);

    public uint getMethodHash(CORINFO_METHOD_HANDLE ftn) => lpVtbl->getMethodHash((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn);

    public bool getSystemVAmd64PassStructInRegisterDescriptor(CORINFO_CLASS_HANDLE structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr) => lpVtbl->getSystemVAmd64PassStructInRegisterDescriptor((ICorJitInfo*)Unsafe.AsPointer(ref this), structHnd, structPassInRegDescPtr);

    public uint getLoongArch64PassStructInRegisterFlags(CORINFO_CLASS_HANDLE cls) => lpVtbl->getLoongArch64PassStructInRegisterFlags((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    public uint getRISCV64PassStructInRegisterFlags(CORINFO_CLASS_HANDLE cls) => lpVtbl->getRISCV64PassStructInRegisterFlags((ICorJitInfo*)Unsafe.AsPointer(ref this), cls);

    //
    // ICorDynamicInfo
    //

    public uint getThreadTLSIndex(void** ppIndirection = null) => lpVtbl->getThreadTLSIndex((ICorJitInfo*)Unsafe.AsPointer(ref this), ppIndirection);

    public int* getAddrOfCaptureThreadGlobal(void** ppIndirection = null) => lpVtbl->getAddrOfCaptureThreadGlobal((ICorJitInfo*)Unsafe.AsPointer(ref this), ppIndirection);

    public void* getHelperFtn(CorInfoHelpFunc ftnNum, void** ppIndirection = null) => lpVtbl->getHelperFtn((ICorJitInfo*)Unsafe.AsPointer(ref this), ftnNum, ppIndirection);

    public void getFunctionEntryPoint(CORINFO_METHOD_HANDLE ftn, CORINFO_CONST_LOOKUP* pResult, CORINFO_ACCESS_FLAGS accessFlags = CORINFO_ACCESS_ANY) => lpVtbl->getFunctionEntryPoint((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, pResult, accessFlags);

    public void getFunctionFixedEntryPoint(CORINFO_METHOD_HANDLE ftn, bool isUnsafeFunctionPointer, CORINFO_CONST_LOOKUP* pResult) => lpVtbl->getFunctionFixedEntryPoint((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, isUnsafeFunctionPointer, pResult);

    public void* getMethodSync(CORINFO_METHOD_HANDLE ftn, void** ppIndirection = null) => lpVtbl->getMethodSync((ICorJitInfo*)Unsafe.AsPointer(ref this), ftn, ppIndirection);

    public CorInfoHelpFunc getLazyStringLiteralHelper(CORINFO_MODULE_HANDLE handle) => lpVtbl->getLazyStringLiteralHelper((ICorJitInfo*)Unsafe.AsPointer(ref this), handle);

    public CORINFO_MODULE_HANDLE embedModuleHandle(CORINFO_MODULE_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedModuleHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), handle, ppIndirection);

    public CORINFO_CLASS_HANDLE embedClassHandle(CORINFO_CLASS_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedClassHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), handle, ppIndirection);

    public CORINFO_METHOD_HANDLE embedMethodHandle(CORINFO_METHOD_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedMethodHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), handle, ppIndirection);

    public CORINFO_FIELD_HANDLE embedFieldHandle(CORINFO_FIELD_HANDLE handle, void** ppIndirection = null) => lpVtbl->embedFieldHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), handle, ppIndirection);

    public void embedGenericHandle(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fEmbedParent, CORINFO_GENERICHANDLE_RESULT* pResult) => lpVtbl->embedGenericHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken, fEmbedParent, pResult);

    public void getLocationOfThisType(CORINFO_METHOD_HANDLE context, CORINFO_LOOKUP_KIND* pLookupKind) => lpVtbl->getLocationOfThisType((ICorJitInfo*)Unsafe.AsPointer(ref this), context, pLookupKind);

    public void getAddressOfPInvokeTarget(CORINFO_METHOD_HANDLE method, CORINFO_CONST_LOOKUP* pLookup) => lpVtbl->getAddressOfPInvokeTarget((ICorJitInfo*)Unsafe.AsPointer(ref this), method, pLookup);

    public void* GetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig, void** ppIndirection = null) => lpVtbl->GetCookieForPInvokeCalliSig((ICorJitInfo*)Unsafe.AsPointer(ref this), szMetaSig, ppIndirection);

    public bool canGetCookieForPInvokeCalliSig(CORINFO_SIG_INFO* szMetaSig) => lpVtbl->canGetCookieForPInvokeCalliSig((ICorJitInfo*)Unsafe.AsPointer(ref this), szMetaSig);

    public CORINFO_JUST_MY_CODE_HANDLE getJustMyCodeHandle(CORINFO_METHOD_HANDLE method, CORINFO_JUST_MY_CODE_HANDLE** ppIndirection = null) => lpVtbl->getJustMyCodeHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), method, ppIndirection);

    public void GetProfilingHandle(bool* pbHookFunction, void** pProfilerHandle, bool* pbIndirectedHandles) => lpVtbl->GetProfilingHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), pbHookFunction, pProfilerHandle, pbIndirectedHandles);

    public void getCallInfo(CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult) => lpVtbl->getCallInfo((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken, pConstrainedResolvedToken, callerHandle, flags, pResult);

    public uint getClassDomainID(CORINFO_CLASS_HANDLE cls, void** ppIndirection = null) => lpVtbl->getClassDomainID((ICorJitInfo*)Unsafe.AsPointer(ref this), cls, ppIndirection);

    public bool getStaticFieldContent(CORINFO_FIELD_HANDLE field, byte* buffer, int bufferSize, int valueOffset = 0, bool ignoreMovableObjects = true) => lpVtbl->getStaticFieldContent((ICorJitInfo*)Unsafe.AsPointer(ref this), field, buffer, bufferSize, valueOffset, ignoreMovableObjects);

    public bool getObjectContent(CORINFO_OBJECT_HANDLE obj, byte* buffer, int bufferSize, int valueOffset) => lpVtbl->getObjectContent((ICorJitInfo*)Unsafe.AsPointer(ref this), obj, buffer, bufferSize, valueOffset);

    public CORINFO_CLASS_HANDLE getStaticFieldCurrentClass(CORINFO_FIELD_HANDLE field, bool* pIsSpeculative = null) => lpVtbl->getStaticFieldCurrentClass((ICorJitInfo*)Unsafe.AsPointer(ref this), field, pIsSpeculative);

    public CORINFO_VARARGS_HANDLE getVarArgsHandle(CORINFO_SIG_INFO* pSig, void** ppIndirection = null) => lpVtbl->getVarArgsHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), pSig, ppIndirection);

    public bool canGetVarArgsHandle(CORINFO_SIG_INFO* pSig) => lpVtbl->canGetVarArgsHandle((ICorJitInfo*)Unsafe.AsPointer(ref this), pSig);

    public InfoAccessType constructStringLiteral(CORINFO_MODULE_HANDLE module, mdToken metaTok, void** ppValue) => lpVtbl->constructStringLiteral((ICorJitInfo*)Unsafe.AsPointer(ref this), module, metaTok, ppValue);

    public InfoAccessType emptyStringLiteral(void** ppValue) => lpVtbl->emptyStringLiteral((ICorJitInfo*)Unsafe.AsPointer(ref this), ppValue);

    public uint getFieldThreadLocalStoreID(CORINFO_FIELD_HANDLE field, void** ppIndirection = null) => lpVtbl->getFieldThreadLocalStoreID((ICorJitInfo*)Unsafe.AsPointer(ref this), field, ppIndirection);

    public CORINFO_METHOD_HANDLE GetDelegateCtor(CORINFO_METHOD_HANDLE methHnd, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE targetMethodHnd, DelegateCtorArgs* pCtorData) => lpVtbl->GetDelegateCtor((ICorJitInfo*)Unsafe.AsPointer(ref this), methHnd, clsHnd, targetMethodHnd, pCtorData);

    public void MethodCompileComplete(CORINFO_METHOD_HANDLE methHnd) => lpVtbl->MethodCompileComplete((ICorJitInfo*)Unsafe.AsPointer(ref this), methHnd);

    public bool getTailCallHelpers(CORINFO_RESOLVED_TOKEN* callToken, CORINFO_SIG_INFO* sig, CORINFO_GET_TAILCALL_HELPERS_FLAGS flags, CORINFO_TAILCALL_HELPERS* pResult) => lpVtbl->getTailCallHelpers((ICorJitInfo*)Unsafe.AsPointer(ref this), callToken, sig, flags, pResult);

    public bool convertPInvokeCalliToCall(CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fMustConvert) => lpVtbl->convertPInvokeCalliToCall((ICorJitInfo*)Unsafe.AsPointer(ref this), pResolvedToken, fMustConvert);

    public bool notifyInstructionSetUsage(CORINFO_InstructionSet instructionSet, bool supportEnabled) => lpVtbl->notifyInstructionSetUsage((ICorJitInfo*)Unsafe.AsPointer(ref this), instructionSet, supportEnabled);

    public void updateEntryPointForTailCall(CORINFO_CONST_LOOKUP* entryPoint) => lpVtbl->updateEntryPointForTailCall((ICorJitInfo*)Unsafe.AsPointer(ref this), entryPoint);

    //
    // ICorJitInfo
    //

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUnknownHandle(nint handle) => handle is >= UNKNOWN_HANDLE_MIN and <= UNKNOWN_HANDLE_MAX;

    public void allocMem(AllocMemArgs* pArgs) => lpVtbl->allocMem((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pArgs);

    public void reserveUnwindInfo(bool isFunclet, bool isColdCode, uint unwindSize) => lpVtbl->reserveUnwindInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), isFunclet, isColdCode, unwindSize);

    public void allocUnwindInfo(byte* pHotCode, byte* pColdCode, uint startOffset, uint endOffset, uint unwindSize, byte* pUnwindBlock, CorJitFuncKind funcKind) => lpVtbl->allocUnwindInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), pHotCode, pColdCode, startOffset, endOffset, unwindSize, pUnwindBlock, funcKind);

    public void* allocGCInfo(nuint size) => lpVtbl->allocGCInfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), size);

    public void setEHcount(uint cEH) => lpVtbl->setEHcount((ICorDynamicInfo*)Unsafe.AsPointer(ref this), cEH);

    public void setEHinfo(uint EHnumber, CORINFO_EH_CLAUSE* clause) => lpVtbl->setEHinfo((ICorDynamicInfo*)Unsafe.AsPointer(ref this), EHnumber, clause);

    public bool logMsg(uint level, sbyte* fmt, void* args) => lpVtbl->logMsg((ICorDynamicInfo*)Unsafe.AsPointer(ref this), level, fmt, args);

    public int doAssert(sbyte* szFile, int iLine, sbyte* szExpr) => lpVtbl->doAssert((ICorDynamicInfo*)Unsafe.AsPointer(ref this), szFile, iLine, szExpr);

    public void reportFatalError(CorJitResult result) => lpVtbl->reportFatalError((ICorDynamicInfo*)Unsafe.AsPointer(ref this), result);

    public JITINTERFACE_HRESULT getPgoInstrumentationResults(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema** pSchema, uint* pCountSchemaItems, byte** pInstrumentationData, PgoSource* pPgoSource) => lpVtbl->getPgoInstrumentationResults((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftnHnd, pSchema, pCountSchemaItems, pInstrumentationData, pPgoSource);

    public JITINTERFACE_HRESULT allocPgoInstrumentationBySchema(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema* pSchema, uint countSchemaItems, byte** pInstrumentationData) => lpVtbl->allocPgoInstrumentationBySchema((ICorDynamicInfo*)Unsafe.AsPointer(ref this), ftnHnd, pSchema, countSchemaItems, pInstrumentationData);

    public void recordCallSite(uint instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_HANDLE methodHandle) => lpVtbl->recordCallSite((ICorDynamicInfo*)Unsafe.AsPointer(ref this), instrOffset, callSig, methodHandle);

    public void recordRelocation(void* location, void* locationRW, void* target, ushort fRelocType, int addlDelta = 0) => lpVtbl->recordRelocation((ICorDynamicInfo*)Unsafe.AsPointer(ref this), location, locationRW, target, fRelocType, addlDelta);

    public ushort getRelocTypeHint(void* target) => lpVtbl->getRelocTypeHint((ICorDynamicInfo*)Unsafe.AsPointer(ref this), target);

    public uint getExpectedTargetArchitecture() => lpVtbl->getExpectedTargetArchitecture((ICorDynamicInfo*) Unsafe.AsPointer(ref this));

    public uint getJitFlags(CORJIT_FLAGS* flags, uint sizeInBytes) => lpVtbl->getJitFlags((ICorDynamicInfo*)Unsafe.AsPointer(ref this), flags, sizeInBytes);

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
        void reserveUnwindInfo(bool isFunclet, bool isColdCode, uint unwindSize);

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
        void allocUnwindInfo(byte* pHotCode, byte* pColdCode, uint startOffset, uint endOffset, uint unwindSize, byte* pUnwindBlock, CorJitFuncKind funcKind);

        // Get a block of memory needed for the code manager information,
        // (the info for enumerating the GC pointers while crawling the
        // stack frame). Note that allocMem must be called first.
        void* allocGCInfo(nuint size);

        // Indicate how many exception handler blocks are to be returned.
        // This is guaranteed to be called before any 'setEHinfo' call.
        // Note that allocMem must be called before this method can be called.
        void setEHcount(uint cEH);

        // Set the values for one particular exception handler block.
        //
        // Handler regions should be lexically contiguous.
        // This is because FinallyIsUnwinding() uses lexicality to
        // determine if a "finally" clause is executing.
        void setEHinfo(uint EHnumber, CORINFO_EH_CLAUSE* clause);

        // Level -> fatalError, Level 2 -> Error, Level 3 -> Warning
        // Level 4 means happens 10 times in a run, level 5 means 100, level 6 means 1000 ...
        // returns non-zero if the logging succeeded
        bool logMsg(uint level, sbyte* fmt, void* args);

        // do an assert.  will return true if the code should retry (DebugBreak)
        // returns false, if the assert should be ignored.
        int doAssert(sbyte* szFile, int iLine, sbyte* szExpr);

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
        JITINTERFACE_HRESULT getPgoInstrumentationResults(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema** pSchema, uint* pCountSchemaItems, byte** pInstrumentationData, PgoSource* pPgoSource);

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
        JITINTERFACE_HRESULT allocPgoInstrumentationBySchema(CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema* pSchema, uint countSchemaItems, byte** pInstrumentationData);

        // Associates a native call site, identified by its offset in the native code stream, with
        // the signature information and method handle the JIT used to lay out the call site. If
        // the call site has no signature information (e.g. a helper call) or has no method handle
        // (e.g. a CALLI P/Invoke), then null should be passed instead.
        void recordCallSite(uint instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_HANDLE methodHandle);

        // A relocation is recorded if we are pre-jitting.
        // A jump thunk may be inserted if we are jitting
        void recordRelocation(void* location, void* locationRW, void* target, ushort fRelocType, int addlDelta = 0);

        ushort getRelocTypeHint(void* target);

        // For what machine does the VM expect the JIT to generate code? The VM
        // returns one of the IMAGE_FILE_MACHINE_* values. Note that if the VM
        // is cross-compiling (such as the case for crossgen), it will return a
        // different value than if it was compiling for the host architecture.
        //
        uint getExpectedTargetArchitecture();

        // Fetches extended flags for a particular compilation instance. Returns
        // the number of bytes written to the provided buffer.
        //
        // flags
        //   Points to a buffer that will hold the extended flags.
        //
        // sizeInBytes
        //   The size of the buffer. Note that this is effectively a version number for the CORJIT_FLAGS value
        //
        uint getJitFlags(CORJIT_FLAGS* flags, uint sizeInBytes);
    }

    public struct Vtbl
    {
        //
        // ICorMethodInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, bool> isIntrinsic;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, bool> notifyMethodInfoUsage;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, uint> getMethodAttribs;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CorInfoMethodRuntimeFlags, void> setMethodAttribs;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_SIG_INFO*, CORINFO_CLASS_HANDLE, void> getMethodSig;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_INFO*, CORINFO_CONTEXT_HANDLE, bool> getMethodInfo;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, bool> haveSameMethodDefinition;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, CorInfoInline> canInline;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, void> beginInlining;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, CorInfoInline, sbyte*, void> reportInliningDecision;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, bool, bool> canTailCall;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE, bool, CorInfoTailCall, sbyte*, void> reportTailCallDecision;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, uint, CORINFO_EH_CLAUSE*, void> getEHinfo;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_CLASS_HANDLE> getMethodClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, uint*, uint*, bool*, void> getMethodVTableOffset;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_DEVIRTUALIZATION_INFO*, bool> resolveVirtualMethod;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, bool*, CORINFO_METHOD_HANDLE> getUnboxedEntry;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getDefaultComparerClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getDefaultEqualityComparerClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_GENERICHANDLE_RESULT*, void> expandRawHandleIntrinsic;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, bool> isIntrinsicType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_SIG_INFO*, bool*, CorInfoCallConvExtension> getUnmanagedCallConv;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_SIG_INFO*, bool> pInvokeMarshalingRequired;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_METHOD_HANDLE, bool> satisfiesMethodConstraints;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, void> methodMustBeLoadedBeforeCodeIsRun;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_METHOD_HANDLE> mapMethodDeclToMethodImpl;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, GSCookie*, GSCookie**, void> getGSCookie;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, PatchpointInfo*, void> setPatchpointInfo;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, uint*, PatchpointInfo*> getOSRInfo;

        //
        // ICorModuleInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, void> resolveToken;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_MODULE_HANDLE, uint, CORINFO_CONTEXT_HANDLE, CORINFO_SIG_INFO*, void> findSig;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_MODULE_HANDLE, uint, CORINFO_CONTEXT_HANDLE, CORINFO_SIG_INFO*, void> findCallSiteSig;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_CLASS_HANDLE> getTokenTypeAsHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_MODULE_HANDLE, uint, char*, int, int, int> getStringLiteral;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_OBJECT_HANDLE, sbyte*, nuint, nuint*, nuint> printObjectDescription;

        //
        // ICorClassInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CorInfoType> asCorInfoType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, sbyte**, sbyte*> getClassNameFromMetadata;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, uint, CORINFO_CLASS_HANDLE> getTypeInstantiationArgument;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, sbyte*, nuint, nuint*, nuint> printClassName;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, bool> isValueClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, uint> getClassAttribs;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_MODULE_HANDLE> getClassModule;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_MODULE_HANDLE, CORINFO_ASSEMBLY_HANDLE> getModuleAssembly;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_ASSEMBLY_HANDLE, sbyte*> getAssemblyName;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, nuint, void*> LongLifetimeMalloc;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, void*, void> LongLifetimeFree;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_MODULE_HANDLE*, void**, nuint> getClassModuleIdForStatics;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CONST_LOOKUP*, int*, bool> getIsClassInitedFlagAddress;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, bool, CORINFO_CONST_LOOKUP*, bool> getStaticBaseAddress;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, uint> getClassSize;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, uint> getHeapClassSize;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, bool> canAllocateOnStack;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, bool, uint> getClassAlignmentRequirement;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, byte*, uint> getClassGClayout;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, uint> getClassNumInstanceFields;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, int, CORINFO_FIELD_HANDLE> getFieldInClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_TYPE_LAYOUT_NODE*, nuint*, GetTypeLayoutResult> getTypeLayout;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, sbyte*, bool, bool> checkMethodModifier;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, bool*, CorInfoHelpFunc> getNewHelper;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getNewArrHelper;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, bool, CorInfoHelpFunc> getCastingHelper;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getSharedCCtorHelper;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getTypeForBox;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getBoxHelper;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CorInfoHelpFunc> getUnBoxHelper;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_OBJECT_HANDLE> getRuntimeTypePointer;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_OBJECT_HANDLE, bool> isObjectImmutable;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_OBJECT_HANDLE, int, ushort*, bool> getStringChar;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_OBJECT_HANDLE, CORINFO_CLASS_HANDLE> getObjectType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_LOOKUP_KIND*, CorInfoHelpFunc, CORINFO_CONST_LOOKUP*, bool> getReadyToRunHelper;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, mdToken, CORINFO_CLASS_HANDLE, CORINFO_LOOKUP*, void> getReadyToRunDelegateCtorHelper;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, CORINFO_METHOD_HANDLE, CORINFO_CONTEXT_HANDLE, CorInfoInitClassResult> initClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, void> classMustBeLoadedBeforeCodeIsRun;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CorInfoClassId, CORINFO_CLASS_HANDLE> getBuiltinClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CorInfoType> getTypeForPrimitiveValueClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CorInfoType> getTypeForPrimitiveNumericClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, bool> canCast;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, TypeCompareState> compareTypesForCast;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, TypeCompareState> compareTypesForEquality;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE, bool> isMoreSpecificType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, bool> isExactType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*, TypeCompareState> isEnum;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE> getParentType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CORINFO_CLASS_HANDLE*, CorInfoType> getChildType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, bool> isSDArray;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, uint> getArrayRank;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CorInfoArrayIntrinsic> getArrayIntrinsicID;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, uint, void*> getArrayInitializationData;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_METHOD_HANDLE, CORINFO_HELPER_DESC*, CorInfoIsAccessAllowedResult> canAccessClass;

        //
        // ICorFieldInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, sbyte*, nuint, nuint*, nuint> printFieldName;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, CORINFO_CLASS_HANDLE> getFieldClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, CORINFO_CLASS_HANDLE*, CORINFO_FIELD_HANDLE, CorInfoType> getFieldType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, uint> getFieldOffset;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_METHOD_HANDLE, CORINFO_ACCESS_FLAGS, CORINFO_FIELD_INFO*, void> getFieldInfo;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, bool, uint> getThreadLocalFieldInfo;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_THREAD_STATIC_BLOCKS_INFO*, bool, void> getThreadLocalStaticBlocksInfo;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_THREAD_STATIC_INFO_NATIVEAOT*, void> getThreadLocalStaticInfo_NativeAOT;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, bool> isFieldStatic;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_OBJECT_HANDLE, int> getArrayOrStringLength;

        //
        // ICorDebugInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, uint*, uint**, ICorDebugInfo.BoundaryTypes*, void> getBoundaries;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, uint, ICorDebugInfo.OffsetMapping*, void> setBoundaries;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, uint*, ICorDebugInfo.ILVarInfo**, bool*, void> getVars;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, uint, ICorDebugInfo.NativeVarInfo*, void> setVars;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, ICorDebugInfo.InlineTreeNode*, uint, ICorDebugInfo.RichOffsetMapping*, uint, void> reportRichMappings;

        //
        // Misc
        //

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, nuint, void*> allocateArray;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, void*, void> freeArray;

        //
        // ICorArgInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_ARG_LIST_HANDLE, CORINFO_ARG_LIST_HANDLE> getArgNext;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE*, CorInfoTypeWithMod> getArgType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, int, CORINFO_CLASS_HANDLE*, int> getExactClasses;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_HANDLE, CORINFO_CLASS_HANDLE> getArgClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, CorInfoHFAElemType> getHFAType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, errorTrapFunction, void*, bool> runWithErrorTrap;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, errorTrapFunction, void*, bool> runWithSPMIErrorTrap;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_EE_INFO*, void> getEEInfo;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, char*> getJitTimeLogFilename;

        //
        // Diagnostic methods
        //

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, mdMethodDef> getMethodDefFromMethod;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, sbyte*, nuint, nuint*, nuint> printMethodName;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, sbyte**, sbyte**, sbyte**, sbyte*> getMethodNameFromMetadata;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, uint> getMethodHash;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR*, bool> getSystemVAmd64PassStructInRegisterDescriptor;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, uint> getLoongArch64PassStructInRegisterFlags;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, uint> getRISCV64PassStructInRegisterFlags;

        //
        // ICorDynamicInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, void**, uint> getThreadTLSIndex;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, void**, int*> getAddrOfCaptureThreadGlobal;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CorInfoHelpFunc, void**, void*> getHelperFtn;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_CONST_LOOKUP*, CORINFO_ACCESS_FLAGS, void> getFunctionEntryPoint;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, bool, CORINFO_CONST_LOOKUP*, void> getFunctionFixedEntryPoint;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, void**, void*> getMethodSync;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_MODULE_HANDLE, CorInfoHelpFunc> getLazyStringLiteralHelper;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_MODULE_HANDLE, void**, CORINFO_MODULE_HANDLE> embedModuleHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, void**, CORINFO_CLASS_HANDLE> embedClassHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, void**, CORINFO_METHOD_HANDLE> embedMethodHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, void**, CORINFO_FIELD_HANDLE> embedFieldHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, bool, CORINFO_GENERICHANDLE_RESULT*, void> embedGenericHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_LOOKUP_KIND*, void> getLocationOfThisType;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_CONST_LOOKUP*, void> getAddressOfPInvokeTarget;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_SIG_INFO*, void**, void*> GetCookieForPInvokeCalliSig;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_SIG_INFO*, bool> canGetCookieForPInvokeCalliSig;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_JUST_MY_CODE_HANDLE**, CORINFO_JUST_MY_CODE_HANDLE> getJustMyCodeHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, bool*, void**, bool*, void> GetProfilingHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_RESOLVED_TOKEN*, CORINFO_METHOD_HANDLE, CORINFO_CALLINFO_FLAGS, CORINFO_CALL_INFO*, void> getCallInfo;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_HANDLE, void**, uint> getClassDomainID;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, byte*, int, int, bool, bool> getStaticFieldContent;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_OBJECT_HANDLE, byte*, int, int, bool> getObjectContent;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, bool*, CORINFO_CLASS_HANDLE> getStaticFieldCurrentClass;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_SIG_INFO*, void**, CORINFO_VARARGS_HANDLE> getVarArgsHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_SIG_INFO*, bool> canGetVarArgsHandle;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_MODULE_HANDLE, mdToken, void**, InfoAccessType> constructStringLiteral;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, void**, InfoAccessType> emptyStringLiteral;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_HANDLE, void**, uint> getFieldThreadLocalStoreID;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, CORINFO_CLASS_HANDLE, CORINFO_METHOD_HANDLE, DelegateCtorArgs*, CORINFO_METHOD_HANDLE> GetDelegateCtor;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_METHOD_HANDLE, void> MethodCompileComplete;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, CORINFO_SIG_INFO*, CORINFO_GET_TAILCALL_HELPERS_FLAGS, CORINFO_TAILCALL_HELPERS*, bool> getTailCallHelpers;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, bool, bool> convertPInvokeCalliToCall;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_InstructionSet, bool, bool> notifyInstructionSetUsage;

        public delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CONST_LOOKUP*, void> updateEntryPointForTailCall;

        //
        // ICorJitInfo
        //

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, AllocMemArgs*, void> allocMem;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, bool, bool, uint, void> reserveUnwindInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, byte*, byte*, uint, uint, uint, byte*, CorJitFuncKind, void> allocUnwindInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, nuint, void*> allocGCInfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, uint, void> setEHcount;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, uint, CORINFO_EH_CLAUSE*, void> setEHinfo;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, uint, sbyte*, void*, bool> logMsg;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, sbyte*, int, sbyte*, int> doAssert;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CorJitResult, void> reportFatalError;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, PgoInstrumentationSchema**, uint*, byte**, PgoSource*, JITINTERFACE_HRESULT> getPgoInstrumentationResults;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORINFO_METHOD_HANDLE, PgoInstrumentationSchema*, uint, byte**, JITINTERFACE_HRESULT> allocPgoInstrumentationBySchema;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, uint, CORINFO_SIG_INFO*, CORINFO_METHOD_HANDLE, void> recordCallSite;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, void*, void*, void*, ushort, int, void> recordRelocation;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, void*, ushort> getRelocTypeHint;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, uint> getExpectedTargetArchitecture;

        public delegate* unmanaged[MemberFunction]<ICorDynamicInfo*, CORJIT_FLAGS*, uint, uint> getJitFlags;
    }
}
