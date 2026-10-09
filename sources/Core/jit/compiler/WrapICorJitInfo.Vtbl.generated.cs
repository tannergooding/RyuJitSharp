// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if MEASURE_CLRAPI_CALLS
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static RyuJitSharp.ICorJitInfo;

namespace RyuJitSharp;

public sealed unsafe partial class WrapICorJitInfo
{
    private static readonly ICorJitInfo.Vtbl<ICorJitInfo>* s_vtbl = CreateVtbl();

    private static ICorJitInfo.Vtbl<ICorJitInfo>* CreateVtbl()
    {
        var vtable = (ICorJitInfo.Vtbl<ICorJitInfo>*)RuntimeHelpers.AllocateTypeAssociatedMemory(
            typeof(WrapICorJitInfo), sizeof(ICorJitInfo.Vtbl<ICorJitInfo>));

        if (sizeof(ICorJitInfo.Vtbl<ICorJitInfo>) != 183 * sizeof(nint))
        {
            throw new InvalidOperationException("ICorJitInfo vtable layout does not match the generated API order.");
        }

        vtable->Base.Base.isIntrinsic = &Thunk_isIntrinsic;
        vtable->Base.Base.canValueClassInstancePointerEscape = &Thunk_canValueClassInstancePointerEscape;
        vtable->Base.Base.notifyMethodInfoUsage = &Thunk_notifyMethodInfoUsage;
        vtable->Base.Base.getMethodAttribs = &Thunk_getMethodAttribs;
        vtable->Base.Base.setMethodAttribs = &Thunk_setMethodAttribs;
        vtable->Base.Base.getMethodSig = &Thunk_getMethodSig;
        vtable->Base.Base.getMethodInfo = &Thunk_getMethodInfo;
        vtable->Base.Base.haveSameMethodDefinition = &Thunk_haveSameMethodDefinition;
        vtable->Base.Base.getTypeDefinition = &Thunk_getTypeDefinition;
        vtable->Base.Base.canInline = &Thunk_canInline;
        vtable->Base.Base.beginInlining = &Thunk_beginInlining;
        vtable->Base.Base.reportInliningDecision = &Thunk_reportInliningDecision;
        vtable->Base.Base.canTailCall = &Thunk_canTailCall;
        vtable->Base.Base.reportTailCallDecision = &Thunk_reportTailCallDecision;
        vtable->Base.Base.getEHinfo = &Thunk_getEHinfo;
        vtable->Base.Base.getMethodClass = &Thunk_getMethodClass;
        vtable->Base.Base.getMethodVTableOffset = &Thunk_getMethodVTableOffset;
        vtable->Base.Base.resolveVirtualMethod = &Thunk_resolveVirtualMethod;
        vtable->Base.Base.getAsyncOtherVariant = &Thunk_getAsyncOtherVariant;
        vtable->Base.Base.getDefaultComparerClass = &Thunk_getDefaultComparerClass;
        vtable->Base.Base.getDefaultEqualityComparerClass = &Thunk_getDefaultEqualityComparerClass;
        vtable->Base.Base.getSZArrayHelperEnumeratorClass = &Thunk_getSZArrayHelperEnumeratorClass;
        vtable->Base.Base.expandRawHandleIntrinsic = &Thunk_expandRawHandleIntrinsic;
        vtable->Base.Base.isIntrinsicType = &Thunk_isIntrinsicType;
        vtable->Base.Base.getUnmanagedCallConv = &Thunk_getUnmanagedCallConv;
        vtable->Base.Base.pInvokeMarshalingRequired = &Thunk_pInvokeMarshalingRequired;
        vtable->Base.Base.satisfiesMethodConstraints = &Thunk_satisfiesMethodConstraints;
        vtable->Base.Base.methodMustBeLoadedBeforeCodeIsRun = &Thunk_methodMustBeLoadedBeforeCodeIsRun;
        vtable->Base.Base.getGSCookie = &Thunk_getGSCookie;
        vtable->Base.Base.setPatchpointInfo = &Thunk_setPatchpointInfo;
        vtable->Base.Base.getOSRInfo = &Thunk_getOSRInfo;
        vtable->Base.Base.resolveToken = &Thunk_resolveToken;
        vtable->Base.Base.findSig = &Thunk_findSig;
        vtable->Base.Base.findCallSiteSig = &Thunk_findCallSiteSig;
        vtable->Base.Base.getTokenTypeAsHandle = &Thunk_getTokenTypeAsHandle;
        vtable->Base.Base.getStringLiteral = &Thunk_getStringLiteral;
        vtable->Base.Base.printObjectDescription = &Thunk_printObjectDescription;
        vtable->Base.Base.asCorInfoType = &Thunk_asCorInfoType;
        vtable->Base.Base.getClassNameFromMetadata = &Thunk_getClassNameFromMetadata;
        vtable->Base.Base.getTypeInstantiationArgument = &Thunk_getTypeInstantiationArgument;
        vtable->Base.Base.getMethodInstantiationArgument = &Thunk_getMethodInstantiationArgument;
        vtable->Base.Base.printClassName = &Thunk_printClassName;
        vtable->Base.Base.isValueClass = &Thunk_isValueClass;
        vtable->Base.Base.getClassAttribs = &Thunk_getClassAttribs;
        vtable->Base.Base.getClassAssemblyName = &Thunk_getClassAssemblyName;
        vtable->Base.Base.LongLifetimeMalloc = &Thunk_LongLifetimeMalloc;
        vtable->Base.Base.LongLifetimeFree = &Thunk_LongLifetimeFree;
        vtable->Base.Base.getIsClassInitedFlagAddress = &Thunk_getIsClassInitedFlagAddress;
        vtable->Base.Base.getClassThreadStaticDynamicInfo = &Thunk_getClassThreadStaticDynamicInfo;
        vtable->Base.Base.getClassStaticDynamicInfo = &Thunk_getClassStaticDynamicInfo;
        vtable->Base.Base.getStaticBaseAddress = &Thunk_getStaticBaseAddress;
        vtable->Base.Base.getClassSize = &Thunk_getClassSize;
        vtable->Base.Base.getHeapClassSize = &Thunk_getHeapClassSize;
        vtable->Base.Base.canAllocateOnStack = &Thunk_canAllocateOnStack;
        vtable->Base.Base.getClassAlignmentRequirement = &Thunk_getClassAlignmentRequirement;
        vtable->Base.Base.getClassGClayout = &Thunk_getClassGClayout;
        vtable->Base.Base.getClassNumInstanceFields = &Thunk_getClassNumInstanceFields;
        vtable->Base.Base.getFieldInClass = &Thunk_getFieldInClass;
        vtable->Base.Base.getTypeLayout = &Thunk_getTypeLayout;
        vtable->Base.Base.checkMethodModifier = &Thunk_checkMethodModifier;
        vtable->Base.Base.getNewHelper = &Thunk_getNewHelper;
        vtable->Base.Base.getNewArrHelper = &Thunk_getNewArrHelper;
        vtable->Base.Base.getCastingHelper = &Thunk_getCastingHelper;
        vtable->Base.Base.getSharedCCtorHelper = &Thunk_getSharedCCtorHelper;
        vtable->Base.Base.getTypeForBox = &Thunk_getTypeForBox;
        vtable->Base.Base.getBoxHelper = &Thunk_getBoxHelper;
        vtable->Base.Base.getUnBoxHelper = &Thunk_getUnBoxHelper;
        vtable->Base.Base.getRuntimeTypePointer = &Thunk_getRuntimeTypePointer;
        vtable->Base.Base.isObjectImmutable = &Thunk_isObjectImmutable;
        vtable->Base.Base.getStringChar = &Thunk_getStringChar;
        vtable->Base.Base.getObjectType = &Thunk_getObjectType;
        vtable->Base.Base.getReadyToRunHelper = &Thunk_getReadyToRunHelper;
        vtable->Base.Base.getReadyToRunDelegateCtorHelper = &Thunk_getReadyToRunDelegateCtorHelper;
        vtable->Base.Base.initClass = &Thunk_initClass;
        vtable->Base.Base.classMustBeLoadedBeforeCodeIsRun = &Thunk_classMustBeLoadedBeforeCodeIsRun;
        vtable->Base.Base.getBuiltinClass = &Thunk_getBuiltinClass;
        vtable->Base.Base.getTypeForPrimitiveValueClass = &Thunk_getTypeForPrimitiveValueClass;
        vtable->Base.Base.getTypeForPrimitiveNumericClass = &Thunk_getTypeForPrimitiveNumericClass;
        vtable->Base.Base.canCast = &Thunk_canCast;
        vtable->Base.Base.compareTypesForCast = &Thunk_compareTypesForCast;
        vtable->Base.Base.compareTypesForEquality = &Thunk_compareTypesForEquality;
        vtable->Base.Base.isMoreSpecificType = &Thunk_isMoreSpecificType;
        vtable->Base.Base.isExactType = &Thunk_isExactType;
        vtable->Base.Base.isGenericType = &Thunk_isGenericType;
        vtable->Base.Base.isNullableType = &Thunk_isNullableType;
        vtable->Base.Base.isEnum = &Thunk_isEnum;
        vtable->Base.Base.getParentType = &Thunk_getParentType;
        vtable->Base.Base.getChildType = &Thunk_getChildType;
        vtable->Base.Base.isSDArray = &Thunk_isSDArray;
        vtable->Base.Base.getArrayRank = &Thunk_getArrayRank;
        vtable->Base.Base.getArrayIntrinsicID = &Thunk_getArrayIntrinsicID;
        vtable->Base.Base.getArrayInitializationData = &Thunk_getArrayInitializationData;
        vtable->Base.Base.canAccessClass = &Thunk_canAccessClass;
        vtable->Base.Base.printFieldName = &Thunk_printFieldName;
        vtable->Base.Base.getFieldClass = &Thunk_getFieldClass;
        vtable->Base.Base.getFieldType = &Thunk_getFieldType;
        vtable->Base.Base.getFieldOffset = &Thunk_getFieldOffset;
        vtable->Base.Base.getFieldInfo = &Thunk_getFieldInfo;
        vtable->Base.Base.getThreadLocalFieldInfo = &Thunk_getThreadLocalFieldInfo;
        vtable->Base.Base.getThreadLocalStaticBlocksInfo = &Thunk_getThreadLocalStaticBlocksInfo;
        vtable->Base.Base.getThreadLocalStaticInfo_NativeAOT = &Thunk_getThreadLocalStaticInfo_NativeAOT;
        vtable->Base.Base.isFieldStatic = &Thunk_isFieldStatic;
        vtable->Base.Base.getArrayOrStringLength = &Thunk_getArrayOrStringLength;
        vtable->Base.Base.getBoundaries = &Thunk_getBoundaries;
        vtable->Base.Base.setBoundaries = &Thunk_setBoundaries;
        vtable->Base.Base.getVars = &Thunk_getVars;
        vtable->Base.Base.setVars = &Thunk_setVars;
        vtable->Base.Base.reportRichMappings = &Thunk_reportRichMappings;
        vtable->Base.Base.reportAsyncDebugInfo = &Thunk_reportAsyncDebugInfo;
        vtable->Base.Base.reportMetadata = &Thunk_reportMetadata;
        vtable->Base.Base.allocateArray = &Thunk_allocateArray;
        vtable->Base.Base.freeArray = &Thunk_freeArray;
        vtable->Base.Base.getArgNext = &Thunk_getArgNext;
        vtable->Base.Base.getArgType = &Thunk_getArgType;
        vtable->Base.Base.getExactClasses = &Thunk_getExactClasses;
        vtable->Base.Base.getArgClass = &Thunk_getArgClass;
        vtable->Base.Base.getHFAType = &Thunk_getHFAType;
        vtable->Base.Base.runWithErrorTrap = &Thunk_runWithErrorTrap;
        vtable->Base.Base.runWithSPMIErrorTrap = &Thunk_runWithSPMIErrorTrap;
        vtable->Base.Base.getEEInfo = &Thunk_getEEInfo;
        vtable->Base.Base.getAsyncInfo = &Thunk_getAsyncInfo;
        vtable->Base.Base.getAwaitReturnCall = &Thunk_getAwaitReturnCall;
        vtable->Base.Base.getAwaitAwaiterInContinuationCall = &Thunk_getAwaitAwaiterInContinuationCall;
        vtable->Base.Base.getMethodDefFromMethod = &Thunk_getMethodDefFromMethod;
        vtable->Base.Base.printMethodName = &Thunk_printMethodName;
        vtable->Base.Base.getMethodNameFromMetadata = &Thunk_getMethodNameFromMetadata;
        vtable->Base.Base.getMethodHash = &Thunk_getMethodHash;
        vtable->Base.Base.getSystemVAmd64PassStructInRegisterDescriptor = &Thunk_getSystemVAmd64PassStructInRegisterDescriptor;
        vtable->Base.Base.getSwiftLowering = &Thunk_getSwiftLowering;
        vtable->Base.Base.getFpStructLowering = &Thunk_getFpStructLowering;
        vtable->Base.Base.getWasmLowering = &Thunk_getWasmLowering;
        vtable->Base.Base.getAddressAlignment = &Thunk_getAddressAlignment;
        vtable->Base.Base.getWasmWellKnownGlobals = &Thunk_getWasmWellKnownGlobals;
        vtable->Base.getThreadTLSIndex = &Thunk_getThreadTLSIndex;
        vtable->Base.getAddrOfCaptureThreadGlobal = &Thunk_getAddrOfCaptureThreadGlobal;
        vtable->Base.getHelperFtn = &Thunk_getHelperFtn;
        vtable->Base.getFunctionEntryPoint = &Thunk_getFunctionEntryPoint;
        vtable->Base.getFunctionFixedEntryPoint = &Thunk_getFunctionFixedEntryPoint;
        vtable->Base.embedModuleHandle = &Thunk_embedModuleHandle;
        vtable->Base.embedClassHandle = &Thunk_embedClassHandle;
        vtable->Base.embedMethodHandle = &Thunk_embedMethodHandle;
        vtable->Base.embedFieldHandle = &Thunk_embedFieldHandle;
        vtable->Base.embedGenericHandle = &Thunk_embedGenericHandle;
        vtable->Base.getLocationOfThisType = &Thunk_getLocationOfThisType;
        vtable->Base.getAddressOfPInvokeTarget = &Thunk_getAddressOfPInvokeTarget;
        vtable->Base.GetCookieForInterpreterCalliSig = &Thunk_GetCookieForInterpreterCalliSig;
        vtable->Base.getJustMyCodeHandle = &Thunk_getJustMyCodeHandle;
        vtable->Base.GetProfilingHandle = &Thunk_GetProfilingHandle;
        vtable->Base.getCallInfo = &Thunk_getCallInfo;
        vtable->Base.getStaticFieldContent = &Thunk_getStaticFieldContent;
        vtable->Base.getObjectContent = &Thunk_getObjectContent;
        vtable->Base.getStaticFieldCurrentClass = &Thunk_getStaticFieldCurrentClass;
        vtable->Base.getVarArgsHandle = &Thunk_getVarArgsHandle;
        vtable->Base.constructStringLiteral = &Thunk_constructStringLiteral;
        vtable->Base.emptyStringLiteral = &Thunk_emptyStringLiteral;
        vtable->Base.getFieldThreadLocalStoreID = &Thunk_getFieldThreadLocalStoreID;
        vtable->Base.GetDelegateCtor = &Thunk_GetDelegateCtor;
        vtable->Base.MethodCompileComplete = &Thunk_MethodCompileComplete;
        vtable->Base.getTailCallHelpers = &Thunk_getTailCallHelpers;
        vtable->Base.getContinuationType = &Thunk_getContinuationType;
        vtable->Base.getAsyncResumptionStub = &Thunk_getAsyncResumptionStub;
        vtable->Base.convertPInvokeCalliToCall = &Thunk_convertPInvokeCalliToCall;
        vtable->Base.notifyInstructionSetUsage = &Thunk_notifyInstructionSetUsage;
        vtable->Base.updateEntryPointForTailCall = &Thunk_updateEntryPointForTailCall;
        vtable->allocMem = &Thunk_allocMem;
        vtable->reserveUnwindInfo = &Thunk_reserveUnwindInfo;
        vtable->allocUnwindInfo = &Thunk_allocUnwindInfo;
        vtable->allocGCInfo = &Thunk_allocGCInfo;
        vtable->setEHcount = &Thunk_setEHcount;
        vtable->setEHinfo = &Thunk_setEHinfo;
        vtable->logMsg = &Thunk_logMsg;
        vtable->doAssert = &Thunk_doAssert;
        vtable->reportFatalError = &Thunk_reportFatalError;
        vtable->getPgoInstrumentationResults = &Thunk_getPgoInstrumentationResults;
        vtable->allocPgoInstrumentationBySchema = &Thunk_allocPgoInstrumentationBySchema;
        vtable->recordCallSite = &Thunk_recordCallSite;
        vtable->recordWasmManagedCallSig = &Thunk_recordWasmManagedCallSig;
        vtable->recordRelocation = &Thunk_recordRelocation;
        vtable->getRelocTypeHint = &Thunk_getRelocTypeHint;
        vtable->getExpectedTargetArchitecture = &Thunk_getExpectedTargetArchitecture;
        vtable->getJitFlags = &Thunk_getJitFlags;
        vtable->Base.getWasmTypeSymbol = &Thunk_getWasmTypeSymbol;
        vtable->Base.getSpecialCopyHelper = &Thunk_getSpecialCopyHelper;
        return vtable;
    }

    private static WrapICorJitInfo GetWrapper(ICorJitInfo* self)
    {
        var proxy = (ProxyContext*)self;
        var handle = GCHandle.FromIntPtr(proxy->WrapperHandle);

        return handle.Target is WrapICorJitInfo wrapper
            ? wrapper
            : throw new InvalidOperationException("ICorJitInfo proxy context is no longer valid.");
    }

    [DoesNotReturn]
    private static void FailFastCallback(Exception exception)
    {
        // The native JIT cannot continue safely with a fabricated EE result.
        Environment.FailFast("An exception escaped an ICorJitInfo timing proxy callback.", exception);
        throw new InvalidOperationException("Environment.FailFast returned unexpectedly.", exception);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_isIntrinsic(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn)
    {
        try
        {
            return GetWrapper(self).isIntrinsic(ftn) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_canValueClassInstancePointerEscape(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn)
    {
        try
        {
            return GetWrapper(self).canValueClassInstancePointerEscape(ftn) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_notifyMethodInfoUsage(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn)
    {
        try
        {
            return GetWrapper(self).notifyMethodInfoUsage(ftn) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag Thunk_getMethodAttribs(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn)
    {
        try
        {
            return GetWrapper(self).getMethodAttribs(ftn);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_setMethodAttribs(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, CorInfoMethodRuntimeFlags attribs)
    {
        try
        {
            GetWrapper(self).setMethodAttribs(ftn, attribs);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getMethodSig(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, CORINFO_SIG_INFO* sig, CORINFO_CLASS_HANDLE memberParent)
    {
        try
        {
            GetWrapper(self).getMethodSig(ftn, sig, memberParent);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_getMethodInfo(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, CORINFO_METHOD_INFO* info, CORINFO_CONTEXT_HANDLE context)
    {
        try
        {
            return GetWrapper(self).getMethodInfo(ftn, info, context) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_haveSameMethodDefinition(ICorJitInfo* self, CORINFO_METHOD_HANDLE meth1Hnd, CORINFO_METHOD_HANDLE meth2Hnd)
    {
        try
        {
            return GetWrapper(self).haveSameMethodDefinition(meth1Hnd, meth2Hnd) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getTypeDefinition(ICorJitInfo* self, CORINFO_CLASS_HANDLE type)
    {
        try
        {
            return GetWrapper(self).getTypeDefinition(type);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoInline Thunk_canInline(ICorJitInfo* self, CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd)
    {
        try
        {
            return GetWrapper(self).canInline(callerHnd, calleeHnd);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_beginInlining(ICorJitInfo* self, CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd)
    {
        try
        {
            GetWrapper(self).beginInlining(inlinerHnd, inlineeHnd);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_reportInliningDecision(ICorJitInfo* self, CORINFO_METHOD_HANDLE inlinerHnd, CORINFO_METHOD_HANDLE inlineeHnd, CorInfoInline inlineResult, byte* reason)
    {
        try
        {
            GetWrapper(self).reportInliningDecision(inlinerHnd, inlineeHnd, inlineResult, reason);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_canTailCall(ICorJitInfo* self, CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE declaredCalleeHnd, CORINFO_METHOD_HANDLE exactCalleeHnd, bool fIsTailPrefix)
    {
        try
        {
            return GetWrapper(self).canTailCall(callerHnd, declaredCalleeHnd, exactCalleeHnd, fIsTailPrefix) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_reportTailCallDecision(ICorJitInfo* self, CORINFO_METHOD_HANDLE callerHnd, CORINFO_METHOD_HANDLE calleeHnd, bool fIsTailPrefix, CorInfoTailCall tailCallResult, byte* reason)
    {
        try
        {
            GetWrapper(self).reportTailCallDecision(callerHnd, calleeHnd, fIsTailPrefix, tailCallResult, reason);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getEHinfo(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, int EHnumber, CORINFO_EH_CLAUSE* clause)
    {
        try
        {
            GetWrapper(self).getEHinfo(ftn, EHnumber, clause);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getMethodClass(ICorJitInfo* self, CORINFO_METHOD_HANDLE method)
    {
        try
        {
            return GetWrapper(self).getMethodClass(method);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getMethodVTableOffset(ICorJitInfo* self, CORINFO_METHOD_HANDLE method, int* offsetOfIndirection, int* offsetAfterIndirection, bool* isRelative)
    {
        try
        {
            GetWrapper(self).getMethodVTableOffset(method, offsetOfIndirection, offsetAfterIndirection, isRelative);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_resolveVirtualMethod(ICorJitInfo* self, CORINFO_DEVIRTUALIZATION_INFO* info)
    {
        try
        {
            return GetWrapper(self).resolveVirtualMethod(info) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_HANDLE Thunk_getAsyncOtherVariant(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, bool* variantIsThunk)
    {
        try
        {
            return GetWrapper(self).getAsyncOtherVariant(ftn, variantIsThunk);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getDefaultComparerClass(ICorJitInfo* self, CORINFO_CLASS_HANDLE elemType)
    {
        try
        {
            return GetWrapper(self).getDefaultComparerClass(elemType);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getDefaultEqualityComparerClass(ICorJitInfo* self, CORINFO_CLASS_HANDLE elemType)
    {
        try
        {
            return GetWrapper(self).getDefaultEqualityComparerClass(elemType);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getSZArrayHelperEnumeratorClass(ICorJitInfo* self, CORINFO_CLASS_HANDLE elemType)
    {
        try
        {
            return GetWrapper(self).getSZArrayHelperEnumeratorClass(elemType);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_expandRawHandleIntrinsic(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_GENERICHANDLE_RESULT* pResult)
    {
        try
        {
            GetWrapper(self).expandRawHandleIntrinsic(pResolvedToken, callerHandle, pResult);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_isIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_HANDLE classHnd)
    {
        try
        {
            return GetWrapper(self).isIntrinsicType(classHnd) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoCallConvExtension Thunk_getUnmanagedCallConv(ICorJitInfo* self, CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig, bool* pSuppressGCTransition)
    {
        try
        {
            return GetWrapper(self).getUnmanagedCallConv(method, callSiteSig, pSuppressGCTransition);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_pInvokeMarshalingRequired(ICorJitInfo* self, CORINFO_METHOD_HANDLE method, CORINFO_SIG_INFO* callSiteSig)
    {
        try
        {
            return GetWrapper(self).pInvokeMarshalingRequired(method, callSiteSig) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_satisfiesMethodConstraints(ICorJitInfo* self, CORINFO_CLASS_HANDLE parent, CORINFO_METHOD_HANDLE method)
    {
        try
        {
            return GetWrapper(self).satisfiesMethodConstraints(parent, method) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_methodMustBeLoadedBeforeCodeIsRun(ICorJitInfo* self, CORINFO_METHOD_HANDLE method)
    {
        try
        {
            GetWrapper(self).methodMustBeLoadedBeforeCodeIsRun(method);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getGSCookie(ICorJitInfo* self, GSCookie* pCookieVal, GSCookie** ppCookieVal)
    {
        try
        {
            GetWrapper(self).getGSCookie(pCookieVal, ppCookieVal);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_setPatchpointInfo(ICorJitInfo* self, PatchpointInfo* patchpointInfo)
    {
        try
        {
            GetWrapper(self).setPatchpointInfo(patchpointInfo);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static PatchpointInfo* Thunk_getOSRInfo(ICorJitInfo* self, int* ilOffset)
    {
        try
        {
            return GetWrapper(self).getOSRInfo(ilOffset);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_resolveToken(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken)
    {
        try
        {
            GetWrapper(self).resolveToken(pResolvedToken);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_findSig(ICorJitInfo* self, CORINFO_MODULE_HANDLE module, int sigTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig)
    {
        try
        {
            GetWrapper(self).findSig(module, sigTOK, context, sig);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_findCallSiteSig(ICorJitInfo* self, CORINFO_MODULE_HANDLE module, int methTOK, CORINFO_CONTEXT_HANDLE context, CORINFO_SIG_INFO* sig)
    {
        try
        {
            GetWrapper(self).findCallSiteSig(module, methTOK, context, sig);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getTokenTypeAsHandle(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken)
    {
        try
        {
            return GetWrapper(self).getTokenTypeAsHandle(pResolvedToken);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getStringLiteral(ICorJitInfo* self, CORINFO_MODULE_HANDLE module, int metaTOK, char* buffer, int bufferSize, int startIndex)
    {
        try
        {
            return GetWrapper(self).getStringLiteral(module, metaTOK, buffer, bufferSize, startIndex);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint Thunk_printObjectDescription(ICorJitInfo* self, CORINFO_OBJECT_HANDLE handle, byte* buffer, nint bufferSize, nint* pRequiredBufferSize)
    {
        try
        {
            return GetWrapper(self).printObjectDescription(handle, buffer, bufferSize, pRequiredBufferSize);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType Thunk_asCorInfoType(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).asCorInfoType(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* Thunk_getClassNameFromMetadata(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, byte** namespaceName)
    {
        try
        {
            return GetWrapper(self).getClassNameFromMetadata(cls, namespaceName);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getTypeInstantiationArgument(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, int index)
    {
        try
        {
            return GetWrapper(self).getTypeInstantiationArgument(cls, index);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getMethodInstantiationArgument(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, int index)
    {
        try
        {
            return GetWrapper(self).getMethodInstantiationArgument(ftn, index);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint Thunk_printClassName(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, byte* buffer, nint bufferSize, nint* pRequiredBufferSize)
    {
        try
        {
            return GetWrapper(self).printClassName(cls, buffer, bufferSize, pRequiredBufferSize);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_isValueClass(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).isValueClass(cls) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag Thunk_getClassAttribs(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getClassAttribs(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* Thunk_getClassAssemblyName(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getClassAssemblyName(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Thunk_LongLifetimeMalloc(ICorJitInfo* self, nint sz)
    {
        try
        {
            return GetWrapper(self).LongLifetimeMalloc(sz);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_LongLifetimeFree(ICorJitInfo* self, void* obj)
    {
        try
        {
            GetWrapper(self).LongLifetimeFree(obj);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_getIsClassInitedFlagAddress(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, CORINFO_CONST_LOOKUP* addr, int* offset)
    {
        try
        {
            return GetWrapper(self).getIsClassInitedFlagAddress(cls, addr, offset) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Thunk_getClassThreadStaticDynamicInfo(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getClassThreadStaticDynamicInfo(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Thunk_getClassStaticDynamicInfo(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getClassStaticDynamicInfo(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_getStaticBaseAddress(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, bool isGc, CORINFO_CONST_LOOKUP* addr)
    {
        try
        {
            return GetWrapper(self).getStaticBaseAddress(cls, isGc, addr) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getClassSize(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getClassSize(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getHeapClassSize(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getHeapClassSize(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_canAllocateOnStack(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).canAllocateOnStack(cls) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getClassAlignmentRequirement(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, bool fDoubleAlignHint)
    {
        try
        {
            return GetWrapper(self).getClassAlignmentRequirement(cls, fDoubleAlignHint);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getClassGClayout(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, CorInfoGCType* gcPtrs)
    {
        try
        {
            return GetWrapper(self).getClassGClayout(cls, gcPtrs);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getClassNumInstanceFields(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getClassNumInstanceFields(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_FIELD_HANDLE Thunk_getFieldInClass(ICorJitInfo* self, CORINFO_CLASS_HANDLE clsHnd, int num)
    {
        try
        {
            return GetWrapper(self).getFieldInClass(clsHnd, num);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static GetTypeLayoutResult Thunk_getTypeLayout(ICorJitInfo* self, CORINFO_CLASS_HANDLE typeHnd, CORINFO_TYPE_LAYOUT_NODE* treeNodes, nint* numTreeNodes)
    {
        try
        {
            return GetWrapper(self).getTypeLayout(typeHnd, treeNodes, numTreeNodes);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_checkMethodModifier(ICorJitInfo* self, CORINFO_METHOD_HANDLE hMethod, byte* modifier, bool fOptional)
    {
        try
        {
            return GetWrapper(self).checkMethodModifier(hMethod, modifier, fOptional) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc Thunk_getNewHelper(ICorJitInfo* self, CORINFO_CLASS_HANDLE classHandle, bool* pHasSideEffects)
    {
        try
        {
            return GetWrapper(self).getNewHelper(classHandle, pHasSideEffects);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc Thunk_getNewArrHelper(ICorJitInfo* self, CORINFO_CLASS_HANDLE arrayCls)
    {
        try
        {
            return GetWrapper(self).getNewArrHelper(arrayCls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc Thunk_getCastingHelper(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fThrowing)
    {
        try
        {
            return GetWrapper(self).getCastingHelper(pResolvedToken, fThrowing);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc Thunk_getSharedCCtorHelper(ICorJitInfo* self, CORINFO_CLASS_HANDLE clsHnd)
    {
        try
        {
            return GetWrapper(self).getSharedCCtorHelper(clsHnd);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getTypeForBox(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getTypeForBox(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc Thunk_getBoxHelper(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getBoxHelper(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc Thunk_getUnBoxHelper(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getUnBoxHelper(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_OBJECT_HANDLE Thunk_getRuntimeTypePointer(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getRuntimeTypePointer(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_isObjectImmutable(ICorJitInfo* self, CORINFO_OBJECT_HANDLE objPtr)
    {
        try
        {
            return GetWrapper(self).isObjectImmutable(objPtr) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_getStringChar(ICorJitInfo* self, CORINFO_OBJECT_HANDLE strObj, int index, ushort* value)
    {
        try
        {
            return GetWrapper(self).getStringChar(strObj, index, value) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getObjectType(ICorJitInfo* self, CORINFO_OBJECT_HANDLE objPtr)
    {
        try
        {
            return GetWrapper(self).getObjectType(objPtr);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_getReadyToRunHelper(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken, CorInfoHelpFunc id, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CONST_LOOKUP* pLookup)
    {
        try
        {
            return GetWrapper(self).getReadyToRunHelper(pResolvedToken, id, callerHandle, pLookup) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getReadyToRunDelegateCtorHelper(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pTargetMethod, mdToken targetConstraint, CORINFO_CLASS_HANDLE delegateType, CORINFO_METHOD_HANDLE callerHandler, CORINFO_LOOKUP* pLookup)
    {
        try
        {
            GetWrapper(self).getReadyToRunDelegateCtorHelper(pTargetMethod, targetConstraint, delegateType, callerHandler, pLookup);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoInitClassResult Thunk_initClass(ICorJitInfo* self, CORINFO_FIELD_HANDLE field, CORINFO_METHOD_HANDLE method, CORINFO_CONTEXT_HANDLE context)
    {
        try
        {
            return GetWrapper(self).initClass(field, method, context);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_classMustBeLoadedBeforeCodeIsRun(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            GetWrapper(self).classMustBeLoadedBeforeCodeIsRun(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getBuiltinClass(ICorJitInfo* self, CorInfoClassId classId)
    {
        try
        {
            return GetWrapper(self).getBuiltinClass(classId);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType Thunk_getTypeForPrimitiveValueClass(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getTypeForPrimitiveValueClass(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType Thunk_getTypeForPrimitiveNumericClass(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getTypeForPrimitiveNumericClass(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_canCast(ICorJitInfo* self, CORINFO_CLASS_HANDLE child, CORINFO_CLASS_HANDLE parent)
    {
        try
        {
            return GetWrapper(self).canCast(child, parent) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState Thunk_compareTypesForCast(ICorJitInfo* self, CORINFO_CLASS_HANDLE fromClass, CORINFO_CLASS_HANDLE toClass)
    {
        try
        {
            return GetWrapper(self).compareTypesForCast(fromClass, toClass);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState Thunk_compareTypesForEquality(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2)
    {
        try
        {
            return GetWrapper(self).compareTypesForEquality(cls1, cls2);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_isMoreSpecificType(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls1, CORINFO_CLASS_HANDLE cls2)
    {
        try
        {
            return GetWrapper(self).isMoreSpecificType(cls1, cls2) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_isExactType(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).isExactType(cls) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState Thunk_isGenericType(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).isGenericType(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState Thunk_isNullableType(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).isNullableType(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState Thunk_isEnum(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls, CORINFO_CLASS_HANDLE* underlyingType)
    {
        try
        {
            return GetWrapper(self).isEnum(cls, underlyingType);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getParentType(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getParentType(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType Thunk_getChildType(ICorJitInfo* self, CORINFO_CLASS_HANDLE clsHnd, CORINFO_CLASS_HANDLE* clsRet)
    {
        try
        {
            return GetWrapper(self).getChildType(clsHnd, clsRet);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_isSDArray(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).isSDArray(cls) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getArrayRank(ICorJitInfo* self, CORINFO_CLASS_HANDLE cls)
    {
        try
        {
            return GetWrapper(self).getArrayRank(cls);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoArrayIntrinsic Thunk_getArrayIntrinsicID(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn)
    {
        try
        {
            return GetWrapper(self).getArrayIntrinsicID(ftn);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Thunk_getArrayInitializationData(ICorJitInfo* self, CORINFO_FIELD_HANDLE field, int size)
    {
        try
        {
            return GetWrapper(self).getArrayInitializationData(field, size);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoIsAccessAllowedResult Thunk_canAccessClass(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_HELPER_DESC* pAccessHelper)
    {
        try
        {
            return GetWrapper(self).canAccessClass(pResolvedToken, callerHandle, pAccessHelper);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint Thunk_printFieldName(ICorJitInfo* self, CORINFO_FIELD_HANDLE field, byte* buffer, nint bufferSize, nint* pRequiredBufferSize)
    {
        try
        {
            return GetWrapper(self).printFieldName(field, buffer, bufferSize, pRequiredBufferSize);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getFieldClass(ICorJitInfo* self, CORINFO_FIELD_HANDLE field)
    {
        try
        {
            return GetWrapper(self).getFieldClass(field);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType Thunk_getFieldType(ICorJitInfo* self, CORINFO_FIELD_HANDLE field, CORINFO_CLASS_HANDLE* structType, CORINFO_CLASS_HANDLE memberParent)
    {
        try
        {
            return GetWrapper(self).getFieldType(field, structType, memberParent);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getFieldOffset(ICorJitInfo* self, CORINFO_FIELD_HANDLE field)
    {
        try
        {
            return GetWrapper(self).getFieldOffset(field);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getFieldInfo(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_ACCESS_FLAGS flags, CORINFO_FIELD_INFO* pResult)
    {
        try
        {
            GetWrapper(self).getFieldInfo(pResolvedToken, callerHandle, flags, pResult);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getThreadLocalFieldInfo(ICorJitInfo* self, CORINFO_FIELD_HANDLE field, bool isGCType)
    {
        try
        {
            return GetWrapper(self).getThreadLocalFieldInfo(field, isGCType);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getThreadLocalStaticBlocksInfo(ICorJitInfo* self, CORINFO_THREAD_STATIC_BLOCKS_INFO* pInfo)
    {
        try
        {
            GetWrapper(self).getThreadLocalStaticBlocksInfo(pInfo);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getThreadLocalStaticInfo_NativeAOT(ICorJitInfo* self, CORINFO_THREAD_STATIC_INFO_NATIVEAOT* pInfo)
    {
        try
        {
            GetWrapper(self).getThreadLocalStaticInfo_NativeAOT(pInfo);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_isFieldStatic(ICorJitInfo* self, CORINFO_FIELD_HANDLE fldHnd)
    {
        try
        {
            return GetWrapper(self).isFieldStatic(fldHnd) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getArrayOrStringLength(ICorJitInfo* self, CORINFO_OBJECT_HANDLE objHnd)
    {
        try
        {
            return GetWrapper(self).getArrayOrStringLength(objHnd);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getBoundaries(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, int* cILOffsets, int** pILOffsets, ICorDebugInfo.BoundaryTypes* implicitBoundaries)
    {
        try
        {
            GetWrapper(self).getBoundaries(ftn, cILOffsets, pILOffsets, implicitBoundaries);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_setBoundaries(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, int cMap, ICorDebugInfo.OffsetMapping* pMap)
    {
        try
        {
            GetWrapper(self).setBoundaries(ftn, cMap, pMap);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getVars(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, int* cVars, ICorDebugInfo.ILVarInfo** vars, bool* extendOthers)
    {
        try
        {
            GetWrapper(self).getVars(ftn, cVars, vars, extendOthers);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_setVars(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, int cVars, ICorDebugInfo.NativeVarInfo* vars)
    {
        try
        {
            GetWrapper(self).setVars(ftn, cVars, vars);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_reportRichMappings(ICorJitInfo* self, ICorDebugInfo.InlineTreeNode* inlineTreeNodes, int numInlineTreeNodes, ICorDebugInfo.RichOffsetMapping* mappings, int numMappings)
    {
        try
        {
            GetWrapper(self).reportRichMappings(inlineTreeNodes, numInlineTreeNodes, mappings, numMappings);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_reportAsyncDebugInfo(ICorJitInfo* self, ICorDebugInfo.AsyncInfo* asyncInfo, ICorDebugInfo.AsyncSuspensionPoint* suspensionPoints, ICorDebugInfo.AsyncContinuationVarInfo* vars, int numVars)
    {
        try
        {
            GetWrapper(self).reportAsyncDebugInfo(asyncInfo, suspensionPoints, vars, numVars);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_reportMetadata(ICorJitInfo* self, byte* key, void* value, nint length)
    {
        try
        {
            GetWrapper(self).reportMetadata(key, value, length);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Thunk_allocateArray(ICorJitInfo* self, nint cBytes)
    {
        try
        {
            return GetWrapper(self).allocateArray(cBytes);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_freeArray(ICorJitInfo* self, void* array)
    {
        try
        {
            GetWrapper(self).freeArray(array);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_ARG_LIST_HANDLE Thunk_getArgNext(ICorJitInfo* self, CORINFO_ARG_LIST_HANDLE args)
    {
        try
        {
            return GetWrapper(self).getArgNext(args);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod Thunk_getArgType(ICorJitInfo* self, CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args, CORINFO_CLASS_HANDLE* vcTypeRet)
    {
        try
        {
            return GetWrapper(self).getArgType(sig, args, vcTypeRet);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getExactClasses(ICorJitInfo* self, CORINFO_CLASS_HANDLE baseType, int maxExactClasses, CORINFO_CLASS_HANDLE* exactClsRet)
    {
        try
        {
            return GetWrapper(self).getExactClasses(baseType, maxExactClasses, exactClsRet);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getArgClass(ICorJitInfo* self, CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_HANDLE args)
    {
        try
        {
            return GetWrapper(self).getArgClass(sig, args);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHFAElemType Thunk_getHFAType(ICorJitInfo* self, CORINFO_CLASS_HANDLE hClass)
    {
        try
        {
            return GetWrapper(self).getHFAType(hClass);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_runWithErrorTrap(ICorJitInfo* self, errorTrapFunction function, void* parameter)
    {
        try
        {
            return GetWrapper(self).runWithErrorTrap(function, parameter) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_runWithSPMIErrorTrap(ICorJitInfo* self, errorTrapFunction function, void* parameter)
    {
        try
        {
            return GetWrapper(self).runWithSPMIErrorTrap(function, parameter) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* pEEInfoOut)
    {
        try
        {
            GetWrapper(self).getEEInfo(pEEInfoOut);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getAsyncInfo(ICorJitInfo* self, CORINFO_ASYNC_INFO* pAsyncInfoOut)
    {
        try
        {
            GetWrapper(self).getAsyncInfo(pAsyncInfoOut);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_HANDLE Thunk_getAwaitReturnCall(ICorJitInfo* self, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CONTEXT_HANDLE* contextHandle, CORINFO_LOOKUP* instArg)
    {
        try
        {
            return GetWrapper(self).getAwaitReturnCall(callerHandle, contextHandle, instArg);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_HANDLE Thunk_getAwaitAwaiterInContinuationCall(ICorJitInfo* self, CORINFO_METHOD_HANDLE callerHandle, CORINFO_RESOLVED_TOKEN* pResolvedToken, bool isUnsafe, CORINFO_CONTEXT_HANDLE* contextHandle, CORINFO_LOOKUP* instArg)
    {
        try
        {
            return GetWrapper(self).getAwaitAwaiterInContinuationCall(callerHandle, pResolvedToken, isUnsafe, contextHandle, instArg);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static mdMethodDef Thunk_getMethodDefFromMethod(ICorJitInfo* self, CORINFO_METHOD_HANDLE hMethod)
    {
        try
        {
            return GetWrapper(self).getMethodDefFromMethod(hMethod);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint Thunk_printMethodName(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, byte* buffer, nint bufferSize, nint* pRequiredBufferSize)
    {
        try
        {
            return GetWrapper(self).printMethodName(ftn, buffer, bufferSize, pRequiredBufferSize);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* Thunk_getMethodNameFromMetadata(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, byte** className, byte** namespaceName, byte** enclosingClassName, nint maxEnclosingClassNames)
    {
        try
        {
            return GetWrapper(self).getMethodNameFromMetadata(ftn, className, namespaceName, enclosingClassName, maxEnclosingClassNames);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getMethodHash(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn)
    {
        try
        {
            return GetWrapper(self).getMethodHash(ftn);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_getSystemVAmd64PassStructInRegisterDescriptor(ICorJitInfo* self, CORINFO_CLASS_HANDLE structHnd, SYSTEMV_AMD64_CORINFO_STRUCT_REG_PASSING_DESCRIPTOR* structPassInRegDescPtr)
    {
        try
        {
            return GetWrapper(self).getSystemVAmd64PassStructInRegisterDescriptor(structHnd, structPassInRegDescPtr) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getSwiftLowering(ICorJitInfo* self, CORINFO_CLASS_HANDLE structHnd, CORINFO_SWIFT_LOWERING* pLowering)
    {
        try
        {
            GetWrapper(self).getSwiftLowering(structHnd, pLowering);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getFpStructLowering(ICorJitInfo* self, CORINFO_CLASS_HANDLE structHnd, CORINFO_FPSTRUCT_LOWERING* pLowering)
    {
        try
        {
            GetWrapper(self).getFpStructLowering(structHnd, pLowering);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoWasmType Thunk_getWasmLowering(ICorJitInfo* self, CORINFO_CLASS_HANDLE structHnd)
    {
        try
        {
            return GetWrapper(self).getWasmLowering(structHnd);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static uint Thunk_getAddressAlignment(ICorJitInfo* self, void* address)
    {
        try
        {
            return GetWrapper(self).getAddressAlignment(address);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getWasmWellKnownGlobals(ICorJitInfo* self, CORINFO_WASM_WELLKNOWN_GLOBALS* pWellKnownGlobalsOut)
    {
        try
        {
            GetWrapper(self).getWasmWellKnownGlobals(pWellKnownGlobalsOut);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getThreadTLSIndex(ICorJitInfo* self, void** ppIndirection)
    {
        try
        {
            return GetWrapper(self).getThreadTLSIndex(ppIndirection);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int* Thunk_getAddrOfCaptureThreadGlobal(ICorJitInfo* self, void** ppIndirection)
    {
        try
        {
            return GetWrapper(self).getAddrOfCaptureThreadGlobal(ppIndirection);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getHelperFtn(ICorJitInfo* self, CorInfoHelpFunc ftnNum, CORINFO_CONST_LOOKUP* pNativeEntrypoint, CORINFO_METHOD_HANDLE* pMethodHandle)
    {
        try
        {
            GetWrapper(self).getHelperFtn(ftnNum, pNativeEntrypoint, pMethodHandle);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getFunctionEntryPoint(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, CORINFO_CONST_LOOKUP* pResult, CORINFO_ACCESS_FLAGS accessFlags)
    {
        try
        {
            GetWrapper(self).getFunctionEntryPoint(ftn, pResult, accessFlags);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getFunctionFixedEntryPoint(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftn, bool isUnsafeFunctionPointer, CORINFO_CONST_LOOKUP* pResult)
    {
        try
        {
            GetWrapper(self).getFunctionFixedEntryPoint(ftn, isUnsafeFunctionPointer, pResult);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_MODULE_HANDLE Thunk_embedModuleHandle(ICorJitInfo* self, CORINFO_MODULE_HANDLE handle, void** ppIndirection)
    {
        try
        {
            return GetWrapper(self).embedModuleHandle(handle, ppIndirection);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_embedClassHandle(ICorJitInfo* self, CORINFO_CLASS_HANDLE handle, void** ppIndirection)
    {
        try
        {
            return GetWrapper(self).embedClassHandle(handle, ppIndirection);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_HANDLE Thunk_embedMethodHandle(ICorJitInfo* self, CORINFO_METHOD_HANDLE handle, void** ppIndirection)
    {
        try
        {
            return GetWrapper(self).embedMethodHandle(handle, ppIndirection);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_FIELD_HANDLE Thunk_embedFieldHandle(ICorJitInfo* self, CORINFO_FIELD_HANDLE handle, void** ppIndirection)
    {
        try
        {
            return GetWrapper(self).embedFieldHandle(handle, ppIndirection);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_embedGenericHandle(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fEmbedParent, CORINFO_METHOD_HANDLE callerHandle, CORINFO_GENERICHANDLE_RESULT* pResult)
    {
        try
        {
            GetWrapper(self).embedGenericHandle(pResolvedToken, fEmbedParent, callerHandle, pResult);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getLocationOfThisType(ICorJitInfo* self, CORINFO_METHOD_HANDLE context, CORINFO_LOOKUP_KIND* pLookupKind)
    {
        try
        {
            GetWrapper(self).getLocationOfThisType(context, pLookupKind);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getAddressOfPInvokeTarget(ICorJitInfo* self, CORINFO_METHOD_HANDLE method, CORINFO_CONST_LOOKUP* pLookup)
    {
        try
        {
            GetWrapper(self).getAddressOfPInvokeTarget(method, pLookup);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Thunk_GetCookieForInterpreterCalliSig(ICorJitInfo* self, CORINFO_SIG_INFO* szMetaSig)
    {
        try
        {
            return GetWrapper(self).GetCookieForInterpreterCalliSig(szMetaSig);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_JUST_MY_CODE_HANDLE Thunk_getJustMyCodeHandle(ICorJitInfo* self, CORINFO_METHOD_HANDLE method, CORINFO_JUST_MY_CODE_HANDLE** ppIndirection)
    {
        try
        {
            return GetWrapper(self).getJustMyCodeHandle(method, ppIndirection);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_GetProfilingHandle(ICorJitInfo* self, bool* pbHookFunction, void** pProfilerHandle, bool* pbIndirectedHandles)
    {
        try
        {
            GetWrapper(self).GetProfilingHandle(pbHookFunction, pProfilerHandle, pbIndirectedHandles);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_getCallInfo(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken, CORINFO_RESOLVED_TOKEN* pConstrainedResolvedToken, CORINFO_METHOD_HANDLE callerHandle, CORINFO_CALLINFO_FLAGS flags, CORINFO_CALL_INFO* pResult)
    {
        try
        {
            GetWrapper(self).getCallInfo(pResolvedToken, pConstrainedResolvedToken, callerHandle, flags, pResult);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_getStaticFieldContent(ICorJitInfo* self, CORINFO_FIELD_HANDLE field, byte* buffer, int bufferSize, int valueOffset, bool ignoreMovableObjects)
    {
        try
        {
            return GetWrapper(self).getStaticFieldContent(field, buffer, bufferSize, valueOffset, ignoreMovableObjects) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_getObjectContent(ICorJitInfo* self, CORINFO_OBJECT_HANDLE obj, byte* buffer, int bufferSize, int valueOffset)
    {
        try
        {
            return GetWrapper(self).getObjectContent(obj, buffer, bufferSize, valueOffset) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getStaticFieldCurrentClass(ICorJitInfo* self, CORINFO_FIELD_HANDLE field, bool* pIsSpeculative)
    {
        try
        {
            return GetWrapper(self).getStaticFieldCurrentClass(field, pIsSpeculative);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_VARARGS_HANDLE Thunk_getVarArgsHandle(ICorJitInfo* self, CORINFO_SIG_INFO* pSig, CORINFO_METHOD_HANDLE methHnd, void** ppIndirection)
    {
        try
        {
            return GetWrapper(self).getVarArgsHandle(pSig, methHnd, ppIndirection);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static InfoAccessType Thunk_constructStringLiteral(ICorJitInfo* self, CORINFO_MODULE_HANDLE module, mdToken metaTok, void** ppValue)
    {
        try
        {
            return GetWrapper(self).constructStringLiteral(module, metaTok, ppValue);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static InfoAccessType Thunk_emptyStringLiteral(ICorJitInfo* self, void** ppValue)
    {
        try
        {
            return GetWrapper(self).emptyStringLiteral(ppValue);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getFieldThreadLocalStoreID(ICorJitInfo* self, CORINFO_FIELD_HANDLE field, void** ppIndirection)
    {
        try
        {
            return GetWrapper(self).getFieldThreadLocalStoreID(field, ppIndirection);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_HANDLE Thunk_GetDelegateCtor(ICorJitInfo* self, CORINFO_METHOD_HANDLE methHnd, CORINFO_CLASS_HANDLE clsHnd, CORINFO_METHOD_HANDLE targetMethodHnd, DelegateCtorArgs* pCtorData)
    {
        try
        {
            return GetWrapper(self).GetDelegateCtor(methHnd, clsHnd, targetMethodHnd, pCtorData);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_MethodCompileComplete(ICorJitInfo* self, CORINFO_METHOD_HANDLE methHnd)
    {
        try
        {
            GetWrapper(self).MethodCompileComplete(methHnd);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_getTailCallHelpers(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* callToken, CORINFO_SIG_INFO* sig, CORINFO_GET_TAILCALL_HELPERS_FLAGS flags, CORINFO_TAILCALL_HELPERS* pResult)
    {
        try
        {
            return GetWrapper(self).getTailCallHelpers(callToken, sig, flags, pResult) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_HANDLE Thunk_getContinuationType(ICorJitInfo* self, nint dataSize, bool* objRefs, nint objRefsSize)
    {
        try
        {
            return GetWrapper(self).getContinuationType(dataSize, objRefs, objRefsSize);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_HANDLE Thunk_getAsyncResumptionStub(ICorJitInfo* self, void** entryPoint)
    {
        try
        {
            return GetWrapper(self).getAsyncResumptionStub(entryPoint);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_convertPInvokeCalliToCall(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* pResolvedToken, bool fMustConvert)
    {
        try
        {
            return GetWrapper(self).convertPInvokeCalliToCall(pResolvedToken, fMustConvert) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_notifyInstructionSetUsage(ICorJitInfo* self, CORINFO_InstructionSet instructionSet, bool supportEnabled, bool preserveNegativeDependency)
    {
        try
        {
            return GetWrapper(self).notifyInstructionSetUsage(instructionSet, supportEnabled, preserveNegativeDependency) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_updateEntryPointForTailCall(ICorJitInfo* self, CORINFO_CONST_LOOKUP* entryPoint)
    {
        try
        {
            GetWrapper(self).updateEntryPointForTailCall(entryPoint);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_allocMem(ICorJitInfo* self, AllocMemArgs* pArgs)
    {
        try
        {
            GetWrapper(self).allocMem(pArgs);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_reserveUnwindInfo(ICorJitInfo* self, bool isFunclet, bool isColdCode, int unwindSize)
    {
        try
        {
            GetWrapper(self).reserveUnwindInfo(isFunclet, isColdCode, unwindSize);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_allocUnwindInfo(ICorJitInfo* self, byte* pHotCode, byte* pColdCode, int startOffset, int endOffset, int unwindSize, byte* pUnwindBlock, CorJitFuncKind funcKind)
    {
        try
        {
            GetWrapper(self).allocUnwindInfo(pHotCode, pColdCode, startOffset, endOffset, unwindSize, pUnwindBlock, funcKind);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* Thunk_allocGCInfo(ICorJitInfo* self, nint size)
    {
        try
        {
            return GetWrapper(self).allocGCInfo(size);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_setEHcount(ICorJitInfo* self, int cEH)
    {
        try
        {
            GetWrapper(self).setEHcount(cEH);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_setEHinfo(ICorJitInfo* self, int EHnumber, CORINFO_EH_CLAUSE* clause)
    {
        try
        {
            GetWrapper(self).setEHinfo(EHnumber, clause);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte Thunk_logMsg(ICorJitInfo* self, int level, byte* fmt, void* args)
    {
        try
        {
            return GetWrapper(self).logMsg(level, fmt, args) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_doAssert(ICorJitInfo* self, byte* szFile, int iLine, byte* szExpr)
    {
        try
        {
            return GetWrapper(self).doAssert(szFile, iLine, szExpr);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_reportFatalError(ICorJitInfo* self, CorJitResult result)
    {
        try
        {
            GetWrapper(self).reportFatalError(result);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static JITINTERFACE_HRESULT Thunk_getPgoInstrumentationResults(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema** pSchema, int* pCountSchemaItems, byte** pInstrumentationData, PgoSource* pPgoSource, bool* pDynamicPgo)
    {
        try
        {
            return GetWrapper(self).getPgoInstrumentationResults(ftnHnd, pSchema, pCountSchemaItems, pInstrumentationData, pPgoSource, pDynamicPgo);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static JITINTERFACE_HRESULT Thunk_allocPgoInstrumentationBySchema(ICorJitInfo* self, CORINFO_METHOD_HANDLE ftnHnd, PgoInstrumentationSchema* pSchema, int countSchemaItems, byte** pInstrumentationData)
    {
        try
        {
            return GetWrapper(self).allocPgoInstrumentationBySchema(ftnHnd, pSchema, countSchemaItems, pInstrumentationData);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_recordCallSite(ICorJitInfo* self, int instrOffset, CORINFO_SIG_INFO* callSig, CORINFO_METHOD_HANDLE methodHandle)
    {
        try
        {
            GetWrapper(self).recordCallSite(instrOffset, callSig, methodHandle);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_recordWasmManagedCallSig(ICorJitInfo* self, CORINFO_SIG_INFO* callSig)
    {
        try
        {
            GetWrapper(self).recordWasmManagedCallSig(callSig);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void Thunk_recordRelocation(ICorJitInfo* self, void* location, void* locationRW, void* target, CorInfoReloc fRelocType, int addlDelta)
    {
        try
        {
            GetWrapper(self).recordRelocation(location, locationRW, target, fRelocType, addlDelta);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc Thunk_getRelocTypeHint(ICorJitInfo* self, void* target)
    {
        try
        {
            return GetWrapper(self).getRelocTypeHint(target);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoArch Thunk_getExpectedTargetArchitecture(ICorJitInfo* self)
    {
        try
        {
            return GetWrapper(self).getExpectedTargetArchitecture();
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int Thunk_getJitFlags(ICorJitInfo* self, CORJIT_FLAGS* flags, int sizeInBytes)
    {
        try
        {
            return GetWrapper(self).getJitFlags(flags, sizeInBytes);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_WASM_TYPE_SYMBOL_HANDLE Thunk_getWasmTypeSymbol(ICorJitInfo* self, CorInfoWasmType* types, nint typesSize)
    {
        try
        {
            return GetWrapper(self).getWasmTypeSymbol(types, typesSize);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_HANDLE Thunk_getSpecialCopyHelper(ICorJitInfo* self, CORINFO_CLASS_HANDLE type)
    {
        try
        {
            return GetWrapper(self).getSpecialCopyHelper(type);
        }
        catch (Exception exception)
        {
            FailFastCallback(exception);
            throw;
        }
    }

}
#endif