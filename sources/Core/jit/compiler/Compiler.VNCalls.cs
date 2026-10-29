// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public unsafe void fgValueNumberHelperCallFunc(GenTreeCall call, VNFunc vnf, ValueNumPair exceptions)
    {
        assert(vnStore is not null);
        assert(vnf is not VNF_Boundary);
        var arity = ValueNumStore.VNFuncArity(vnf);
        var unique = false;
        var useEntryPoint = false;

        switch (vnf)
        {
            case VNF_JitNew:
            {
                unique = true;
                exceptions = ValueNumStore.VNPForEmptyExcSet();
                break;
            }
            case VNF_JitNewArr:
            case VNF_JitNewLclArr:
            {
                unique = true;
                var length = vnStore.VNPNormalPair(call.Args.GetUserArgByIndex(1)!.Node._vnPair);
                exceptions = vnStore.VNPExcSetSingleton(
                    vnStore.VNPairForFunc(TYP_REF, VNF_NewArrOverflowExc, length));
                break;
            }
            case VNF_JitNewMdArr:
            case VNF_Box:
            case VNF_BoxNullable:
            {
                unique = true;
                break;
            }
            case VNF_JitReadyToRunNew:
            {
                unique = true;
                exceptions = ValueNumStore.VNPForEmptyExcSet();
                useEntryPoint = true;
                break;
            }
            case VNF_JitReadyToRunNewArr:
            case VNF_JitReadyToRunNewLclArr:
            {
                unique = true;
                var length = vnStore.VNPNormalPair(call.Args.GetUserArgByIndex(0)!.Node._vnPair);
                exceptions = vnStore.VNPExcSetSingleton(
                    vnStore.VNPairForFunc(TYP_REF, VNF_NewArrOverflowExc, length));
                useEntryPoint = true;
                break;
            }
            case VNF_ReadyToRunStaticBaseGC:
            case VNF_ReadyToRunStaticBaseNonGC:
            case VNF_ReadyToRunStaticBaseThread:
            case VNF_ReadyToRunStaticBaseThreadNoctor:
            case VNF_ReadyToRunStaticBaseThreadNonGC:
            case VNF_ReadyToRunGenericStaticBase:
            case VNF_ReadyToRunIsInstanceOf:
            case VNF_ReadyToRunCastClass:
            case VNF_ReadyToRunGenericHandle:
            case VNF_ReadyToRunVirtualFuncPtr:
            {
                useEntryPoint = true;
                break;
            }
            default:
            {
                assert(call.HelperNum.IsPure);
                if (Configuration.DEBUG)
                {
                    foreach (var argument in call.Args.Args)
                    {
                        assert(!argument.AbiInfo.IsPassedByReference);
                    }
                }
                break;
            }
        }

        if (unique)
        {
            arity--;
        }

        var uniquePair = unique ? vnStore.VNPairForExpr(compCurBB, call.Type) : default;
        if (call.IndirectionCellArgKind is not WellKnownArg.None)
        {
            useEntryPoint = false;
        }

        var arg = call.Args.Head;
        if (Configuration.TARGET_WASM && (arg?.WellKnownArg is WellKnownArg.WasmShadowStackPointer))
        {
            arg = arg.Next;
        }
        ValueNumPair result;
        if (arity == 0)
        {
            result = unique ? vnStore.VNPairForFunc(call.Type, vnf, uniquePair)
                : vnStore.VNPairForFunc(call.Type, vnf);
        }
        else
        {
            ValueNumPair first;
            if (Configuration.FEATURE_READYTORUN && useEntryPoint)
            {
                var address = vnStore.VNForHandle((nint)call._entryPoint.addr, GTF_ICON_FTN_ADDR);
                first = new(address, address);
            }
            else
            {
                assert(!useEntryPoint);
                vnStore.VNPUnpackExc(arg!.Node._vnPair, out first, out var argExceptions);
                exceptions = vnStore.VNPExcSetUnion(exceptions, argExceptions);
                arg = arg.Next;
            }

            if (arity == 1)
            {
                result = unique ? vnStore.VNPairForFunc(call.Type, vnf, first, uniquePair)
                    : vnStore.VNPairForFunc(call.Type, vnf, first);
            }
            else
            {
                vnStore.VNPUnpackExc(arg!.Node._vnPair, out var second, out var argExceptions);
                exceptions = vnStore.VNPExcSetUnion(exceptions, argExceptions);
                arg = arg.Next;
                if (arity == 2)
                {
                    result = unique ? vnStore.VNPairForFunc(call.Type, vnf, first, second, uniquePair)
                        : vnStore.VNPairForFunc(call.Type, vnf, first, second);
                }
                else
                {
                    vnStore.VNPUnpackExc(arg!.Node._vnPair, out var third, out argExceptions);
                    exceptions = vnStore.VNPExcSetUnion(exceptions, argExceptions);
                    arg = arg.Next;
                    assert(arity == 3);
                    assert(arg is null || arg.WellKnownArg is WellKnownArg.WasmPortableEntryPoint);
                    result = unique ? vnStore.VNPairForFunc(call.Type, vnf, first, second, third, uniquePair)
                        : vnStore.VNPairForFunc(call.Type, vnf, first, second, third);
                }
            }
            result = vnStore.VNPWithExc(result, exceptions);
        }

        call._vnPair = result;
        if (Configuration.TARGET_WASM)
        {
            if (!unique && (arg is not null))
            {
                assert(arg.WellKnownArg is WellKnownArg.WasmPortableEntryPoint);
            }
        }
        else
        {
            assert(arg is null || unique);
        }
    }

    public unsafe bool fgValueNumberSpecialIntrinsic(GenTreeCall call)
    {
        assert(vnStore is not null);
        assert(call.IsSpecialIntrinsic());
        switch (lookupNamedIntrinsic(call._callMethHnd))
        {
            case NI_System_String_FastAllocateString:
            {
                assert(call.Args.CountUserArgs() == 2);
                var methodTable = call.Args.GetUserArgByIndex(0)!.Node;
                var length = call.Args.GetUserArgByIndex(1)!.Node;
                vnStore.VNPUnpackExc(methodTable._vnPair, out var methodTablePair, out var methodTableExc);
                vnStore.VNPUnpackExc(length._vnPair, out var lengthPair, out var lengthExc);
                var overflow = vnStore.VNPExcSetSingleton(
                    vnStore.VNPairForFunc(TYP_REF, VNF_NewStringOverflowExc, lengthPair));
                var exceptions = vnStore.VNPExcSetUnion(vnStore.VNPExcSetUnion(methodTableExc, lengthExc), overflow);
                var unique = vnStore.VNPairForExpr(compCurBB, call.Type);
                var result = vnStore.VNPairForFunc(call.Type, VNF_StrFastAllocate,
                    methodTablePair, lengthPair, unique);
                call._vnPair = vnStore.VNPWithExc(result, exceptions);
                fgMutateGcHeap(call, "NI_System_String_FastAllocateString");
                return true;
            }
            case NI_System_Type_GetTypeFromHandle:
            {
                assert((RuntimeHandleUnderlyingType == TYP_I_IMPL));
                var bitcast = new VNFuncApp();
                var argument = call.Args.GetUserArgByIndex(0)!.Node._vnPair.Conservative;
                if (!vnStore.GetVNFunc(argument, ref bitcast) || !bitcast.FuncIs(VNF_BitCast))
                {
                    break;
                }
                var typeHandle = new VNFuncApp();
                if (!vnStore.GetVNFunc(bitcast.GetArg(0), ref typeHandle) ||
                    !typeHandle.FuncIs(VNF_TypeHandleToRuntimeTypeHandle) ||
                    !vnStore.IsVNTypeHandle(typeHandle.GetArg(0)))
                {
                    break;
                }
                var clsVN = typeHandle.GetArg(0);
                nint classHandle = 0;
                if (!vnStore.EmbeddedHandleMapLookup(vnStore.ConstantValue<nint>(clsVN), ref classHandle) ||
                    (classHandle == 0))
                {
                    break;
                }
                var typeObject = info.compCompHnd->getRuntimeTypePointer((CORINFO_CLASS_HANDLE)classHandle);
                if (typeObject is not null)
                {
                    call._vnPair.SetBoth(vnStore.VNForHandle((nint)typeObject, GTF_ICON_OBJ_HDL));
                    return true;
                }
                break;
            }
        }
        return false;
    }

    public void fgValueNumberCall(GenTreeCall call)
    {
        assert(vnStore is not null);
        if (call.IsHelperCall())
        {
            if (fgValueNumberHelperCall(call))
            {
                fgMutateGcHeap(call, "HELPER - modifies heap");
            }
        }
        else if (call.Type is TYP_VOID)
        {
            call._vnPair.SetBoth(ValueNumStore.VNForVoid());
            fgMutateGcHeap(call, "CALL");
        }
        else if (!call.IsSpecialIntrinsic() || !fgValueNumberSpecialIntrinsic(call))
        {
            call._vnPair = vnStore.VNPairForExpr(compCurBB, call.Type);
            fgMutateGcHeap(call, "CALL");
        }

        var resumedDef = gtCallGetDefinedAsyncResumedLclAddr(call);
        var resumedPair = new ValueNumPair();
        if (resumedDef is not null)
        {
            if (call.GetAsyncInfo().AlwaysSuspends)
            {
                JITDUMP($"Call [{call.TreeId:D6}] always suspends; establishing always resumed state for V{resumedDef.LclNum:D2}\n");
                resumedPair.SetBoth(vnStore.VNForIntPtrCon(1));
            }
            else
            {
                var resumedUse = call.Args.FindWellKnownArg(WellKnownArg.AsyncResumedUse);
                if (resumedUse is not null)
                {
                    assert(genActualTypeIsInt(resumedUse.Node.Type));
                    for (var kind = VNK_Liberal; kind <= VNK_Conservative; kind++)
                    {
                        var normal = vnStore.VNNormalValue(resumedUse.Node._vnPair[kind]);
                        if (vnStore.IsVNConstant(normal) && (vnStore.CoercedConstantValue<int>(normal) == 1))
                        {
                            JITDUMP($"Call [{call.TreeId:D6}] propagates always resumed state for V{resumedDef.LclNum:D2} ({kind} VN)\n");
                            resumedPair[kind] = vnStore.VNForIntPtrCon(1);
                        }
                    }
                }
            }
        }

        var visitor = new CallValueNumberDefVisitor(this, call, resumedDef, resumedPair);
        _ = call.VisitLogicalLocalDefs(this, ref visitor);
    }

    private readonly struct CallValueNumberDefVisitor(Compiler compiler, GenTreeCall call,
        GenTreeLclVarCommon? resumedDef, ValueNumPair resumedPair) : ILocalDefVisitor
    {
        public GenTree.VisitResult Visit<TDef>(TDef def) where TDef : struct, ILocalDef
        {
            var value = def.DefNode == resumedDef ? resumedPair : new ValueNumPair();
            if (!value.BothDefined())
            {
                assert(compiler.vnStore is not null);
                var unique = compiler.vnStore.VNForExpr(compiler.compCurBB, compiler.lvaGetDesc(def.LclNum).Type);
                if (value.Liberal == ValueNumStore.NoVN)
                {
                    value.Liberal = unique;
                }
                if (value.Conservative == ValueNumStore.NoVN)
                {
                    value.Conservative = unique;
                }
            }
            compiler.fgValueNumberLocalStore(call, def, value, true);
            return GenTree.VisitResult.Continue;
        }
    }

    public void fgValueNumberCastHelper(GenTreeCall call)
    {
        assert(vnStore is not null);
        var_types to;
        var_types from;
        var unsigned = false;
        var overflow = false;
        switch (call.HelperNum)
        {
            case CORINFO_HELP_LNG2FLT:
            {
                to = TYP_FLOAT;
                from = TYP_LONG;
                break;
            }
            case CORINFO_HELP_LNG2DBL:
            {
                to = TYP_DOUBLE;
                from = TYP_LONG;
                break;
            }
            case CORINFO_HELP_ULNG2FLT:
            {
                to = TYP_FLOAT;
                from = TYP_LONG;
                unsigned = true;
                break;
            }
            case CORINFO_HELP_ULNG2DBL:
            {
                to = TYP_DOUBLE;
                from = TYP_LONG;
                unsigned = true;
                break;
            }
            case CORINFO_HELP_DBL2INT_OVF:
            {
                to = TYP_INT;
                from = TYP_DOUBLE;
                overflow = true;
                break;
            }
            case CORINFO_HELP_DBL2LNG:
            {
                to = TYP_LONG;
                from = TYP_DOUBLE;
                break;
            }
            case CORINFO_HELP_DBL2LNG_OVF:
            {
                to = TYP_LONG;
                from = TYP_DOUBLE;
                overflow = true;
                break;
            }
            case CORINFO_HELP_DBL2UINT_OVF:
            {
                to = TYP_UINT;
                from = TYP_DOUBLE;
                overflow = true;
                break;
            }
            case CORINFO_HELP_DBL2ULNG:
            {
                to = TYP_ULONG;
                from = TYP_DOUBLE;
                break;
            }
            case CORINFO_HELP_DBL2ULNG_OVF:
            {
                to = TYP_ULONG;
                from = TYP_DOUBLE;
                overflow = true;
                break;
            }
            default:
            {
                JitExceptionHelpers.ThrowUnreachableException();
                return;
            }
        }
        call._vnPair = vnStore.VNPairForCast(call.Args.GetUserArgByIndex(0)!.Node._vnPair,
            to, from, unsigned, overflow);
    }

    public VNFunc fgValueNumberJitHelperMethodVNFunc(CorInfoHelpFunc helpFunc)
    {
        assert(helpFunc.IsPure || helpFunc.IsAllocator);
        switch (helpFunc)
        {
            case CORINFO_HELP_DIV or CORINFO_HELP_LDIV:
            {
                return VNF_DIV;
            }
            case CORINFO_HELP_MOD or CORINFO_HELP_LMOD or CORINFO_HELP_FLTREM or CORINFO_HELP_DBLREM:
            {
                return VNF_MOD;
            }
            case CORINFO_HELP_UDIV or CORINFO_HELP_ULDIV:
            {
                return VNF_UDIV;
            }
            case CORINFO_HELP_UMOD or CORINFO_HELP_ULMOD:
            {
                return VNF_UMOD;
            }
            case CORINFO_HELP_LLSH:
            {
                return VNF_LSH;
            }
            case CORINFO_HELP_LRSH:
            {
                return VNF_RSH;
            }
            case CORINFO_HELP_LRSZ:
            {
                return VNF_RSZ;
            }
            case CORINFO_HELP_LMUL:
            {
                return VNF_MUL;
            }
            case CORINFO_HELP_LMUL_OVF:
            {
                return VNF_MUL_OVF;
            }
            case CORINFO_HELP_ULMUL_OVF:
            {
                return VNF_MUL_UN_OVF;
            }
            case CORINFO_HELP_NEWFAST or CORINFO_HELP_NEWSFAST or CORINFO_HELP_NEWSFAST_FINALIZE or
                CORINFO_HELP_NEWSFAST_ALIGN8 or CORINFO_HELP_NEWSFAST_ALIGN8_VC or
                CORINFO_HELP_NEWSFAST_ALIGN8_FINALIZE:
            {
                return VNF_JitNew;
            }
            case CORINFO_HELP_READYTORUN_NEW:
            {
                return VNF_JitReadyToRunNew;
            }
            case CORINFO_HELP_NEWARR_1_DIRECT or CORINFO_HELP_NEWARR_1_PTR or
                CORINFO_HELP_NEWARR_1_VC or CORINFO_HELP_NEWARR_1_ALIGN8:
            {
                return VNF_JitNewArr;
            }
            case CORINFO_HELP_NEW_MDARR or CORINFO_HELP_NEW_MDARR_RARE:
            {
                return VNF_JitNewMdArr;
            }
            case CORINFO_HELP_READYTORUN_NEWARR_1:
            {
                return VNF_JitReadyToRunNewArr;
            }
            case CORINFO_HELP_NEWFAST_MAYBEFROZEN:
            {
                return IsAot ? VNF_JitReadyToRunNew : VNF_JitNew;
            }
            case CORINFO_HELP_NEWARR_1_MAYBEFROZEN:
            {
                return IsAot ? VNF_JitReadyToRunNewArr : VNF_JitNewArr;
            }
            case CORINFO_HELP_GET_GCSTATIC_BASE:
            {
                return VNF_GetGcstaticBase;
            }
            case CORINFO_HELP_GET_NONGCSTATIC_BASE:
            {
                return VNF_GetNongcstaticBase;
            }
            case CORINFO_HELP_GETDYNAMIC_GCSTATIC_BASE:
            {
                return VNF_GetdynamicGcstaticBase;
            }
            case CORINFO_HELP_GETDYNAMIC_NONGCSTATIC_BASE:
            {
                return VNF_GetdynamicNongcstaticBase;
            }
            case CORINFO_HELP_GETDYNAMIC_GCSTATIC_BASE_NOCTOR:
            {
                return VNF_GetdynamicGcstaticBaseNoctor;
            }
            case CORINFO_HELP_GETDYNAMIC_NONGCSTATIC_BASE_NOCTOR:
            {
                return VNF_GetdynamicNongcstaticBaseNoctor;
            }
            case CORINFO_HELP_GETPINNED_GCSTATIC_BASE:
            {
                return VNF_GetpinnedGcstaticBase;
            }
            case CORINFO_HELP_GETPINNED_NONGCSTATIC_BASE:
            {
                return VNF_GetpinnedNongcstaticBase;
            }
            case CORINFO_HELP_GETPINNED_GCSTATIC_BASE_NOCTOR:
            {
                return VNF_GetpinnedGcstaticBaseNoctor;
            }
            case CORINFO_HELP_GETPINNED_NONGCSTATIC_BASE_NOCTOR:
            {
                return VNF_GetpinnedNongcstaticBaseNoctor;
            }
            case CORINFO_HELP_READYTORUN_GCSTATIC_BASE:
            {
                return VNF_ReadyToRunStaticBaseGC;
            }
            case CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE:
            {
                return VNF_ReadyToRunStaticBaseNonGC;
            }
            case CORINFO_HELP_READYTORUN_THREADSTATIC_BASE:
            {
                return VNF_ReadyToRunStaticBaseThread;
            }
            case CORINFO_HELP_READYTORUN_THREADSTATIC_BASE_NOCTOR:
            {
                return VNF_ReadyToRunStaticBaseThreadNoctor;
            }
            case CORINFO_HELP_READYTORUN_NONGCTHREADSTATIC_BASE:
            {
                return VNF_ReadyToRunStaticBaseThreadNonGC;
            }
            case CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE:
            {
                return VNF_ReadyToRunGenericStaticBase;
            }
            case CORINFO_HELP_GET_GCTHREADSTATIC_BASE:
            {
                return VNF_GetGcthreadstaticBase;
            }
            case CORINFO_HELP_GET_NONGCTHREADSTATIC_BASE:
            {
                return VNF_GetNongcthreadstaticBase;
            }
            case CORINFO_HELP_GET_GCTHREADSTATIC_BASE_NOCTOR:
            {
                return VNF_GetGcthreadstaticBaseNoctor;
            }
            case CORINFO_HELP_GET_NONGCTHREADSTATIC_BASE_NOCTOR:
            {
                return VNF_GetNongcthreadstaticBaseNoctor;
            }
            case CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE:
            {
                return VNF_GetdynamicGcthreadstaticBase;
            }
            case CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE:
            {
                return VNF_GetdynamicNongcthreadstaticBase;
            }
            case CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_OPTIMIZED:
            {
                return VNF_GetdynamicGcthreadstaticBaseOptimized;
            }
            case CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_OPTIMIZED:
            {
                return VNF_GetdynamicNongcthreadstaticBaseOptimized;
            }
            case CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR:
            {
                return VNF_GetdynamicGcthreadstaticBaseNoctor;
            }
            case CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR:
            {
                return VNF_GetdynamicNongcthreadstaticBaseNoctor;
            }
            case CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED:
            {
                return VNF_GetdynamicGcthreadstaticBaseNoctorOptimized;
            }
            case CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED:
            {
                return VNF_GetdynamicNongcthreadstaticBaseNoctorOptimized;
            }
            case CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2:
            {
                return VNF_GetdynamicNongcthreadstaticBaseNoctorOptimized2;
            }
            case CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2_NOJITOPT:
            {
                return VNF_GetdynamicNongcthreadstaticBaseNoctorOptimized2NoJitOpt;
            }
            case CORINFO_HELP_GETSTATICFIELDADDR_TLS:
            {
                return VNF_GetStaticAddrTLS;
            }
            case CORINFO_HELP_RUNTIMEHANDLE_METHOD:
            {
                return VNF_RuntimeHandleMethod;
            }
            case CORINFO_HELP_READYTORUN_GENERIC_HANDLE:
            {
                return VNF_ReadyToRunGenericHandle;
            }
            case CORINFO_HELP_RUNTIMEHANDLE_CLASS:
            {
                return VNF_RuntimeHandleClass;
            }
            case CORINFO_HELP_CHKCASTCLASS or CORINFO_HELP_CHKCASTCLASS_SPECIAL or CORINFO_HELP_CHKCASTARRAY or
                CORINFO_HELP_CHKCASTINTERFACE or CORINFO_HELP_CHKCASTANY:
            {
                return VNF_CastClass;
            }
            case CORINFO_HELP_READYTORUN_CHKCAST:
            {
                return VNF_ReadyToRunCastClass;
            }
            case CORINFO_HELP_ISINSTANCEOFCLASS or CORINFO_HELP_ISINSTANCEOFINTERFACE or
                CORINFO_HELP_ISINSTANCEOFARRAY or CORINFO_HELP_ISINSTANCEOFANY:
            {
                return VNF_IsInstanceOf;
            }
            case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPE:
            {
                return VNF_TypeHandleToRuntimeType;
            }
            case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE:
            {
                return VNF_TypeHandleToRuntimeTypeHandle;
            }
            case CORINFO_HELP_READYTORUN_ISINSTANCEOF:
            {
                return VNF_ReadyToRunIsInstanceOf;
            }
            case CORINFO_HELP_LDELEMA_REF:
            {
                return VNF_LdElemA;
            }
            case CORINFO_HELP_UNBOX:
            {
                return VNF_Unbox;
            }
            case CORINFO_HELP_UNBOX_TYPETEST:
            {
                return VNF_Unbox_TypeTest;
            }
            case CORINFO_HELP_GETREFANY:
            {
                return VNF_GetRefanyVal;
            }
            case CORINFO_HELP_GETCLASSFROMMETHODPARAM:
            {
                return VNF_GetClassFromMethodParam;
            }
            case CORINFO_HELP_GETSYNCFROMCLASSHANDLE:
            {
                return VNF_GetSyncFromClassHandle;
            }
            case CORINFO_HELP_LOOP_CLONE_CHOICE_ADDR:
            {
                return VNF_LoopCloneChoiceAddr;
            }
            case CORINFO_HELP_BOX:
            {
                return VNF_Box;
            }
            case CORINFO_HELP_BOX_NULLABLE:
            {
                return VNF_BoxNullable;
            }
            case CORINFO_HELP_VIRTUAL_FUNC_PTR:
            {
                return VNF_VirtualFuncPtr;
            }
            case CORINFO_HELP_GVMLOOKUP_FOR_SLOT:
            {
                return VNF_GVMLookupForSlot;
            }
            case CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT:
            {
                return VNF_InterfaceLookupForSlot;
            }
            case CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR:
            {
                return VNF_ReadyToRunVirtualFuncPtr;
            }
            default:
            {
                if (Configuration.DETAILED_FATAL_ERROR_MESSAGES)
                {
                    JitExceptionHelpers.ThrowUnreachableException(
                        FatalJitErrorCode.UnmappedVNHelperArgument,
                        helpFunc);
                }
                else
                {
                    JitExceptionHelpers.ThrowUnreachableException(
                        FatalJitErrorCode.UnmappedVNHelperArgument);
                }

                return default;
            }
        }
    }

    public unsafe bool fgValueNumberHelperCall(GenTreeCall call)
    {
        assert(vnStore is not null);
        var helper = call.HelperNum;
        switch (helper)
        {
            case CORINFO_HELP_LNG2FLT:
            case CORINFO_HELP_LNG2DBL:
            case CORINFO_HELP_ULNG2FLT:
            case CORINFO_HELP_ULNG2DBL:
            case CORINFO_HELP_DBL2INT_OVF:
            case CORINFO_HELP_DBL2LNG:
            case CORINFO_HELP_DBL2LNG_OVF:
            case CORINFO_HELP_DBL2UINT_OVF:
            case CORINFO_HELP_DBL2ULNG:
            case CORINFO_HELP_DBL2ULNG_OVF:
            {
                fgValueNumberCastHelper(call);
                return false;
            }
            case CORINFO_HELP_TYPEHANDLE_TO_RUNTIMETYPEHANDLE:
            {
                var argVN = call.Args.GetUserArgByIndex(0)!.Node._vnPair.Conservative;
                if (RuntimeHandleUnderlyingType is TYP_REF && vnStore.IsVNTypeHandle(argVN))
                {
                    var typeObject = info.compCompHnd->getRuntimeTypePointer(
                        (CORINFO_CLASS_HANDLE)vnStore.ConstantValue<nint>(argVN));
                    if (typeObject is not null)
                    {
                        var objectVN = vnStore.VNForHandle((nint)typeObject, GTF_ICON_OBJ_HDL);
                        call._vnPair.SetBoth(vnStore.VNForBitCast(objectVN, TYP_STRUCT,
                            ValueSize.FromJitType(TYP_REF)));
                        return false;
                    }
                }
                break;
            }
        }

        var pure = helper.IsPure;
        var allocation = helper.IsAllocator;
        var mayRunCctor = helper.MayRunCctor;
        var modHeap = helper.MutatesHeap || mayRunCctor;
        var exceptions = ValueNumStore.VNPForEmptyExcSet();

        if (!helper.NoThrow)
        {
            switch (helper)
            {
                case CORINFO_HELP_OVERFLOW:
                {
                    exceptions = vnStore.VNPExcSetSingleton(
                        vnStore.VNPairForFunc(TYP_REF, VNF_OverflowExc, ValueNumStore.VNPForVoid()));
                    break;
                }
                case CORINFO_HELP_CHKCASTINTERFACE:
                case CORINFO_HELP_CHKCASTARRAY:
                case CORINFO_HELP_CHKCASTCLASS:
                case CORINFO_HELP_CHKCASTANY:
                {
                    break;
                }
                case CORINFO_HELP_READYTORUN_CHKCAST:
                {
                    var address = vnStore.VNForHandle((nint)call._entryPoint.addr, GTF_ICON_FTN_ADDR);
                    exceptions = vnStore.VNPExcSetSingleton(vnStore.VNPairForFunc(TYP_REF,
                        VNF_R2RInvalidCastExc, vnStore.VNPNormalPair(
                            call.Args.GetUserArgByIndex(0)!.Node._vnPair), new(address, address)));
                    break;
                }
                case CORINFO_HELP_READYTORUN_GCSTATIC_BASE:
                case CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE:
                case CORINFO_HELP_READYTORUN_THREADSTATIC_BASE:
                case CORINFO_HELP_READYTORUN_NONGCTHREADSTATIC_BASE:
                case CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE:
                {
                    if (Configuration.FEATURE_READYTORUN)
                    {
                        var address = vnStore.VNForHandle((nint)call._entryPoint.addr, GTF_ICON_FTN_ADDR);
                        exceptions = vnStore.VNPExcSetSingleton(vnStore.VNPairForFunc(
                            TYP_REF, VNF_R2RClassInitExc, new(address, address)));
                    }
                    break;
                }
                case CORINFO_HELP_GET_GCSTATIC_BASE:
                case CORINFO_HELP_GET_NONGCSTATIC_BASE:
                case CORINFO_HELP_GET_GCSTATIC_BASE_NOCTOR:
                case CORINFO_HELP_GET_NONGCSTATIC_BASE_NOCTOR:
                case CORINFO_HELP_GET_GCTHREADSTATIC_BASE:
                case CORINFO_HELP_GET_NONGCTHREADSTATIC_BASE:
                case CORINFO_HELP_GET_GCTHREADSTATIC_BASE_NOCTOR:
                case CORINFO_HELP_GET_NONGCTHREADSTATIC_BASE_NOCTOR:
                {
                    exceptions = vnStore.VNPExcSetSingleton(vnStore.VNPairForFunc(
                        TYP_REF, VNF_ClassInitGenericExc,
                        vnStore.VNPNormalPair(call.Args.GetUserArgByIndex(0)!.Node._vnPair)));
                    break;
                }
                case CORINFO_HELP_GETDYNAMIC_GCSTATIC_BASE:
                case CORINFO_HELP_GETDYNAMIC_NONGCSTATIC_BASE:
                case CORINFO_HELP_GETPINNED_GCSTATIC_BASE:
                case CORINFO_HELP_GETPINNED_NONGCSTATIC_BASE:
                case CORINFO_HELP_GETDYNAMIC_GCSTATIC_BASE_NOCTOR:
                case CORINFO_HELP_GETDYNAMIC_NONGCSTATIC_BASE_NOCTOR:
                case CORINFO_HELP_GETPINNED_GCSTATIC_BASE_NOCTOR:
                case CORINFO_HELP_GETPINNED_NONGCSTATIC_BASE_NOCTOR:
                {
                    exceptions = vnStore.VNPExcSetSingleton(vnStore.VNPairForFunc(
                        TYP_REF, VNF_DynamicClassInitExc,
                        vnStore.VNPNormalPair(call.Args.GetUserArgByIndex(0)!.Node._vnPair)));
                    break;
                }
                case CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE:
                case CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE:
                case CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_OPTIMIZED:
                case CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_OPTIMIZED:
                case CORINFO_HELP_GETDYNAMIC_GCTHREADSTATIC_BASE_NOCTOR:
                case CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR:
                {
                    exceptions = vnStore.VNPExcSetSingleton(vnStore.VNPairForFunc(
                        TYP_REF, VNF_ThreadClassInitExc,
                        vnStore.VNPNormalPair(call.Args.GetUserArgByIndex(0)!.Node._vnPair)));
                    break;
                }
                case CORINFO_HELP_DIV:
                case CORINFO_HELP_LDIV:
                case CORINFO_HELP_MOD:
                case CORINFO_HELP_LMOD:
                case CORINFO_HELP_UDIV:
                case CORINFO_HELP_ULDIV:
                case CORINFO_HELP_UMOD:
                case CORINFO_HELP_ULMOD:
                {
                    var oper = helper switch
                    {
                        CORINFO_HELP_DIV or CORINFO_HELP_LDIV => GT_DIV,
                        CORINFO_HELP_MOD or CORINFO_HELP_LMOD => GT_MOD,
                        CORINFO_HELP_UDIV or CORINFO_HELP_ULDIV => GT_UDIV,
                        _ => GT_UMOD,
                    };
                    exceptions = fgValueNumberDivisionExceptions(oper,
                        call.Args.GetUserArgByIndex(0)!.Node, call.Args.GetUserArgByIndex(1)!.Node);
                    break;
                }
                case CORINFO_HELP_VIRTUAL_FUNC_PTR:
                case CORINFO_HELP_GVMLOOKUP_FOR_SLOT:
                case CORINFO_HELP_INTERFACELOOKUP_FOR_SLOT:
                {
                    exceptions = fgValueNumberIndirNullCheckExceptions(call.Args.ThisArg!.Node);
                    break;
                }
                case CORINFO_HELP_READYTORUN_VIRTUAL_FUNC_PTR:
                {
                    exceptions = fgValueNumberIndirNullCheckExceptions(call.Args.GetUserArgByIndex(0)!.Node);
                    break;
                }
                default:
                {
                    exceptions = vnStore.VNPExcSetSingleton(vnStore.VNPairForFunc(TYP_REF,
                        VNF_HelperOpaqueExc, vnStore.VNPairForExpr(compCurBB, TYP_I_IMPL)));
                    break;
                }
            }
        }

        ValueNumPair normal;
        if (call.Type is TYP_VOID)
        {
            normal = ValueNumStore.VNPForVoid();
        }
        else if (pure || allocation)
        {
            var vnf = fgValueNumberJitHelperMethodVNFunc(helper);
            if (allocation && (call.Args.FindWellKnownArg(WellKnownArg.StackArrayLocal) is not null))
            {
                if (vnf is VNF_JitNewArr)
                {
                    vnf = VNF_JitNewLclArr;
                }
                else if (vnf is VNF_JitReadyToRunNewArr)
                {
                    vnf = VNF_JitReadyToRunNewLclArr;
                }
            }
            fgValueNumberHelperCallFunc(call, vnf, exceptions);
            return modHeap;
        }
        else
        {
            normal = vnStore.VNPairForExpr(compCurBB, call.Type);
        }

        call._vnPair = vnStore.VNPWithExc(normal, exceptions);
        return modHeap;
    }
}
