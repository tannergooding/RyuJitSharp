// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ImporterCallTests
{
    [Test]
    public static void VirtualMethodPointerEmbedsMethodBeforeParentAndKeepsHelperArgumentOrder()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.embedGenericHandle =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, bool, CORINFO_METHOD_STRUCT_*, CORINFO_GENERICHANDLE_RESULT*, void>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, byte, CORINFO_METHOD_STRUCT_*, CORINFO_GENERICHANDLE_RESULT*, void>)&EmbedVirtualMethodHandle;
        var state = new VirtualMethodLookup { Interface = new ICorJitInfo { lpVtbl = &vtable } };
#if DEBUG
        using var tls = new JitTls(&state.Interface);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info.compCompHnd = &state.Interface;
        compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)0x6000;
        JitTls.Compiler = compiler;
        try
        {
            var token = new CORINFO_RESOLVED_TOKEN { token = 0x06000001 };
            var instance = compiler.gtNewZeroConNode(var_types.TYP_REF);
            instance.Flags |= GenTreeFlags.GTF_ORDER_SIDEEFF;
            var result = compiler.getVirtMethodPointerTree(instance, token).AsCall();
            Assert.That(state.Order, Is.EqualTo(12));
            Assert.That(state.ContextMatched, Is.EqualTo(2));
            Assert.That(result.IsHelperCall(CorInfoHelpFunc.CORINFO_HELP_VIRTUAL_FUNC_PTR), Is.True);
            Assert.That(result.Type, Is.EqualTo(Globals.TYP_I_IMPL));
            Assert.That(result.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF, Is.EqualTo(GenTreeFlags.GTF_ORDER_SIDEEFF));
            Assert.That(result.Args.CountArgs(), Is.EqualTo(3));
            var thisArg = result.Args.GetArgByIndex(0) ?? throw new InvalidOperationException();
            var typeArg = result.Args.GetArgByIndex(1) ?? throw new InvalidOperationException();
            var methodArg = result.Args.GetArgByIndex(2) ?? throw new InvalidOperationException();
            Assert.That(thisArg.Node, Is.SameAs(instance));
            Assert.That(typeArg.Node.AsIntCon().IconValue, Is.EqualTo((nint)0x5000));
            Assert.That(methodArg.Node.AsIntCon().IconValue, Is.EqualTo((nint)0x4000));
            Assert.That(methodArg.WellKnownArg, Is.EqualTo(WellKnownArg.RuntimeMethodHandle));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct VirtualMethodLookup
    {
        public ICorJitInfo Interface;
        public int Order;
        public int ContextMatched;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void EmbedVirtualMethodHandle(ICorJitInfo* info, CORINFO_RESOLVED_TOKEN* token, byte parent, CORINFO_METHOD_STRUCT_* context, CORINFO_GENERICHANDLE_RESULT* result)
    {
        var state = (VirtualMethodLookup*)info;
        state->Order = (state->Order * 10) + parent + 1;
        state->ContextMatched += (context == (CORINFO_METHOD_STRUCT_*)0x6000) && (token->token == 0x06000001) ? 1 : 0;
        *result = default;
        result->lookup.constLookup.accessType = InfoAccessType.IAT_VALUE;
        result->lookup.constLookup.handle = (CORINFO_GENERIC_STRUCT_*)(parent == 0 ? 0x4000 : 0x5000);
        result->compileTimeHandle = result->lookup.constLookup.handle;
    }

    [TestCase(CorInfoType.CORINFO_TYPE_INT)]
    [TestCase(CorInfoType.CORINFO_TYPE_LONG)]
    public static void NegativeLog2LeavesItsArgumentForTheManagedFallback(CorInfoType type)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArgType = &GetPrimitiveArgumentType;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var jitTls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo, compMaxStack = 1 };
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.stackState.esStack = new StackEntry[1];
        JitTls.Compiler = compiler;

        try
        {
            GenTree value = type == CorInfoType.CORINFO_TYPE_LONG
                ? compiler.gtNewLconNode(-1)
                : compiler.gtNewIconNode(var_types.TYP_INT, -1);
            compiler.impPushOnStack(value, new typeInfo(value.Type));
            var signature = new CORINFO_SIG_INFO { numArgs = 1, retType = type };
            var result = compiler.impPrimitiveNamedIntrinsic(NamedIntrinsic.NI_PRIMITIVE_Log2, null, null, signature, default, false);
            Assert.Multiple(() => {
                Assert.That(result, Is.Null);
                Assert.That(compiler.impStackHeight, Is.EqualTo(1));
                Assert.That(compiler.impStackTop().val, Is.SameAs(value));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod GetPrimitiveArgumentType(ICorJitInfo* self, CORINFO_SIG_INFO* signature,
        CORINFO_ARG_LIST_STRUCT_* argument, CORINFO_CLASS_STRUCT_** type)
    {
        *type = null;
        return (CorInfoTypeWithMod)signature->retType;
    }

    [TestCase(var_types.TYP_INT, genTreeOps.GT_ROL)]
    [TestCase(var_types.TYP_INT, genTreeOps.GT_ROR)]
    [TestCase(var_types.TYP_LONG, genTreeOps.GT_ROL)]
    [TestCase(var_types.TYP_LONG, genTreeOps.GT_ROR)]
    public static void VariableRotatesMaskTheirAmount(var_types type, genTreeOps operation)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.info.compMaxStack = 2;
        compiler.stackState.esStack = new StackEntry[2];
        JitTls.Compiler = compiler;

        try
        {
            GenTree value = type == var_types.TYP_LONG
                ? compiler.gtNewLconNode(42)
                : compiler.gtNewIconNode(type, 42);
            var amount = compiler.gtNewLclvNode(var_types.TYP_INT, 0);
            compiler.impPushOnStack(value, new typeInfo(type));
            compiler.impPushOnStack(amount, new typeInfo(var_types.TYP_INT));

            var result = compiler.impRotateHelper(type, operation)
                ?? throw new InvalidOperationException("Variable rotate was not imported.");
            var maskedAmount = result.AsOp().Op2.AsOp();
            Assert.Multiple(() => {
                Assert.That(compiler.impStackHeight, Is.Zero);
                Assert.That(result.Oper, Is.EqualTo(operation));
                Assert.That(result.AsOp().Op1, Is.SameAs(value));
                Assert.That(maskedAmount.Oper, Is.EqualTo(genTreeOps.GT_AND));
                Assert.That(maskedAmount.Op1, Is.SameAs(amount));
                Assert.That(maskedAmount.Op2.AsIntCon().IconValue,
                    Is.EqualTo((nint)(type == var_types.TYP_LONG ? 63 : 31)));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    public static void HiddenInstantiationArgumentsPreserveHandleKindAndRestoration(bool classContext, bool indirect, bool readonlyArray)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.embedClassHandle = &EmbedInstantiationClass;
        vtable.Base.embedMethodHandle = &EmbedInstantiationMethod;
        vtable.Base.Base.classMustBeLoadedBeforeCodeIsRun = &RestoreInstantiationClass;
        vtable.Base.Base.methodMustBeLoadedBeforeCodeIsRun = &RestoreInstantiationMethod;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var jitTls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo };
        JitTls.Compiler = compiler;

        try
        {
            var stateValue = new InstantiationHandle { Value = 0x40, Indirect = indirect };
            var state = &stateValue;
            var context = classContext
                ? Globals.MAKE_CLASSCONTEXT((CORINFO_CLASS_STRUCT_*)state)
                : Globals.MAKE_METHODCONTEXT((CORINFO_METHOD_STRUCT_*)state);
            var result = compiler.impGetInstParamArg(default, default, context, false,
                readonlyArray ? CorInfoFlag.CORINFO_FLG_ARRAY : 0, readonlyArray)
                ?? throw new InvalidOperationException("Missing instantiation argument.");

            if (readonlyArray)
            {
                Assert.Multiple(() => {
                    Assert.That(result.Type, Is.EqualTo(var_types.TYP_REF));
                    Assert.That(result.AsIntCon().IconValue, Is.EqualTo((nint)0));
                    Assert.That(state->Embedded, Is.Zero);
                    Assert.That(state->Restored, Is.Zero);
                });
            }
            else
            {
                Assert.That(result.Oper, Is.EqualTo(indirect ? genTreeOps.GT_IND : genTreeOps.GT_CNS_INT));
                var icon = indirect ? result.AsIndir().Addr.AsIntCon() : result.AsIntCon();
                Assert.Multiple(() => {
                    Assert.That(icon.Flags & GenTreeFlags.GTF_ICON_HDL_MASK,
                        Is.EqualTo(classContext ? Globals.GTF_ICON_CLASS_HDL : Globals.GTF_ICON_METHOD_HDL));
                    Assert.That(icon.IconValue, Is.EqualTo(indirect ? (nint)(&state->Value) : state->Value));
                    Assert.That(state->Embedded, Is.EqualTo(1));
                    Assert.That(state->Restored, Is.EqualTo(1));
                });
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(CorInfoCallConvExtension.Managed, CorInfoCallConvExtension.Managed, true, true)]
    [TestCase(CorInfoCallConvExtension.Managed, CorInfoCallConvExtension.Managed, false, false)]
    [TestCase(CorInfoCallConvExtension.Managed, CorInfoCallConvExtension.C, true, false)]
    [TestCase(CorInfoCallConvExtension.C, CorInfoCallConvExtension.Managed, true, false)]
    [TestCase(CorInfoCallConvExtension.C, CorInfoCallConvExtension.C, true, false)]
    public static void TailReturnWideningRequiresTwoManagedConventions(
        CorInfoCallConvExtension caller, CorInfoCallConvExtension callee, bool allowWidening, bool expected)
    {
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        var result = compiler.impTailCallRetTypeCompatible(allowWidening,
            var_types.TYP_INT, null, caller, var_types.TYP_SHORT, null, callee);
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void StructReturnsMaterializeCandidateReturnBuffers(bool placeholder, bool returnBuffer)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassAttribs = &GetStructFlags;
        vtable.Base.Base.getClassSize = &GetStructSize;
        vtable.Base.Base.isValueClass =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, byte>)&IsValueClass;
        vtable.Base.Base.isIntrinsicType =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, byte>)&IsIntrinsicType;
        vtable.Base.Base.getArrayRank = &GetBoxRank;
        vtable.Base.Base.getTypeInstantiationArgument = &GetBoxTypeArgument;
        vtable.Base.Base.printClassName = &PrintBoxClassName;
        vtable.Base.Base.runWithSPMIErrorTrap =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, delegate* unmanaged[Cdecl]<void*, void>, void*, byte>)&RunWithErrorTrap;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var jitTls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.compMinOptsIsSet = true;
        compiler.info = new Compiler.Info {
            compCompHnd = &jitInfo,
            compRetType = var_types.TYP_STRUCT,
            compRetBuffArg = Globals.BAD_VAR_NUM,
        };
        compiler.lvaTable = [];
        compiler.stackState.esStack = [];
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var call = new GenTreeCall(var_types.TYP_STRUCT) {
                _callType = gtCallTypes.CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)1,
                RetClsHnd = (CORINFO_CLASS_STRUCT_*)2,
            };

            if (returnBuffer)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_RETBUFFARG;
            }

            GenTree value = call;

            if (placeholder)
            {
                call.SingleInlineCandidateInfo = new InlineCandidateInfo();
                compiler.impAppendStmt(compiler.gtNewStmt(call));
                value = new GenTreeRetExpr(var_types.TYP_STRUCT, call);
            }

            var result = compiler.impFixupStructReturnType(value);

            Assert.Multiple(() => {
                Assert.That(result.Oper, Is.EqualTo(returnBuffer ? genTreeOps.GT_LCL_VAR : value.Oper));
                Assert.That(call.Args.HasRetBuffer, Is.EqualTo(returnBuffer));
                Assert.That(call.Type, Is.EqualTo(returnBuffer ? var_types.TYP_VOID : var_types.TYP_STRUCT));
                Assert.That(compiler.lvaCount, Is.EqualTo(returnBuffer ? 1 : 0));
            });

            if (returnBuffer)
            {
                var argument = call.Args.RetBufferArg ?? throw new InvalidOperationException("Missing return buffer argument.");
                Assert.That(argument.Node.AsLclVarCommon().LclNum, Is.EqualTo(result.AsLclVar().LclNum));
                Assert.That(compiler.lvaTable[result.AsLclVar().LclNum].lvExactSize, Is.EqualTo(sizeof(int)));
            }
            else
            {
                Assert.That(result, Is.SameAs(value));
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(TypeCompareState.Must)]
    [TestCase(TypeCompareState.MustNot)]
    [TestCase(TypeCompareState.May)]
    public static void KnownEnumBoxesUseSingleDefinitionTemps(TypeCompareState enumState)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.embedGenericHandle =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, bool, CORINFO_METHOD_STRUCT_*, CORINFO_GENERICHANDLE_RESULT*, void>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, byte, CORINFO_METHOD_STRUCT_*, CORINFO_GENERICHANDLE_RESULT*, void>)&EmbedClassHandle;
        vtable.Base.Base.getBoxHelper = &GetBoxHelper;
        vtable.Base.Base.getNewHelper = &GetNewHelper;
        vtable.Base.Base.getTypeForBox = &GetBoxedType;
        vtable.Base.Base.asCorInfoType = &GetBoxType;
        vtable.Base.Base.isEnum = &IsEnum;
        vtable.Base.Base.classMustBeLoadedBeforeCodeIsRun = &RestoreBoxClass;
        vtable.Base.Base.getArrayRank = &GetBoxRank;
        vtable.Base.Base.getTypeInstantiationArgument = &GetBoxTypeArgument;
        vtable.Base.Base.printClassName = &PrintBoxClassName;
        vtable.Base.Base.runWithSPMIErrorTrap =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, delegate* unmanaged[Cdecl]<void*, void>, void*, byte>)&RunWithErrorTrap;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var jitTls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = Globals.JitConfig;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.compMinOpts = true;
        compiler.opts.compMinOptsIsSet = true;
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo, compMaxStack = 1 };
        compiler.lvaTable = [];
        compiler.impBoxTemp = Globals.BAD_VAR_NUM;
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            object config = previousConfig;
            var limitField = typeof(JitConfigValues).GetField("_jitMaxLocalsToTrack", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing local tracking limit.");
            limitField.SetValue(config, 0x400);
            Globals.JitConfig = (JitConfigValues)config;

            compiler.stackState.esStack = [new() { val = compiler.gtNewIconNode(var_types.TYP_INT, 1) }];
            compiler.stackState.esStackDepth = 1;
            var boxClass = new BoxClass { EnumState = enumState };
            var token = new CORINFO_RESOLVED_TOKEN { hClass = (CORINFO_CLASS_STRUCT_*)&boxClass, token = 0x02000001 };

            compiler.impImportAndPushBox(token);
            var restoreCount = boxClass.RestoreCount;
            var enumQueries = boxClass.EnumQueries;

            Assert.Multiple(() => {
                Assert.That(compiler.lvaTable[compiler.impBoxTemp].lvSingleDef, Is.EqualTo(enumState == TypeCompareState.Must));
                Assert.That(compiler.stackState.esStackDepth, Is.EqualTo(1));
                Assert.That(compiler.stackState.esStack[0].val.Oper, Is.EqualTo(genTreeOps.GT_BOX));
                Assert.That(restoreCount, Is.EqualTo(1));
                Assert.That(enumQueries, Is.EqualTo(1));
            });
        }
        finally
        {
            Globals.JitConfig = previousConfig;
            JitTls.Compiler = previous;
        }
    }

    [TestCase(1, 3)]
    [TestCase(3, 1)]
    [TestCase(3, 3)]
    public static void ArrayDimensionTempsKeepEarlierLayouts(int firstRank, int secondRank)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.embedGenericHandle =
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, bool, CORINFO_METHOD_STRUCT_*, CORINFO_GENERICHANDLE_RESULT*, void>)
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, byte, CORINFO_METHOD_STRUCT_*, CORINFO_GENERICHANDLE_RESULT*, void>)&EmbedClassHandle;
        vtable.Base.Base.getArrayRank = &GetArrayRank;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var jitTls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo, compMaxStack = Math.Max(firstRank, secondRank) };
        compiler.lvaTable = [];
        compiler.lvaNewObjArrayArgs = Globals.BAD_VAR_NUM;
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var ranks = stackalloc int[] { firstRank, secondRank };
            var firstTemp = Globals.BAD_VAR_NUM;

            for (var invocation = 0; invocation < 2; invocation++)
            {
                var rank = ranks[invocation];
                compiler.stackState.esStack = new StackEntry[rank];
                compiler.stackState.esStackDepth = rank;

                for (var i = 0; i < rank; i++)
                {
                    compiler.stackState.esStack[i].val = compiler.gtNewIconNode(var_types.TYP_INT, i + 1);
                }

                var token = new CORINFO_RESOLVED_TOKEN { hClass = (CORINFO_CLASS_STRUCT_*)(ranks + invocation) };
                CORINFO_CALL_INFO callInfo = default;
                callInfo.sig.numArgs = (ushort)rank;
                compiler.impImportNewObjArray(token, callInfo);

                if (invocation == 0)
                {
                    firstTemp = compiler.lvaNewObjArrayArgs;
                }
            }

            Assert.Multiple(() => {
                Assert.That(compiler.lvaTable[firstTemp].lvExactSize, Is.EqualTo(firstRank * sizeof(int)));
                Assert.That(compiler.lvaNewObjArrayArgs == firstTemp, Is.EqualTo(secondRank <= firstRank));
                Assert.That(compiler.lvaTable[compiler.lvaNewObjArrayArgs].lvExactSize,
                    Is.EqualTo(Math.Max(firstRank, secondRank) * sizeof(int)));
                Assert.That(compiler.stackState.esStackDepth, Is.EqualTo(1));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(InfoAccessType.IAT_VALUE, false)]
    [TestCase(InfoAccessType.IAT_VALUE, true)]
    [TestCase(InfoAccessType.IAT_PVALUE, false)]
    [TestCase(InfoAccessType.IAT_PVALUE, true)]
    public static void NativeAotGenericVirtualLookupUsesDispatchCell(InfoAccessType accessType, bool isInterface)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var jitTls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info { compCompHnd = &jitInfo };
        JitTls.Compiler = compiler;

        try
        {
            CORINFO_CALL_INFO callInfo = default;
            callInfo.sig.sigInst.methInstCount = 1;
            callInfo.hMethod = (CORINFO_METHOD_STRUCT_*)0x5678;
            callInfo.classFlags = isInterface ? CorInfoFlag.CORINFO_FLG_INTERFACE : 0;
            callInfo.codePointerLookup.constLookup.accessType = accessType;
            callInfo.codePointerLookup.constLookup.handle = (CORINFO_GENERIC_STRUCT_*)0x1234;
            var result = compiler.impImportLdvirtftn(compiler.gtNewIconNode(var_types.TYP_REF, 0), default, callInfo);
            var call = result?.AsCall() ?? throw new InvalidOperationException("Missing virtual lookup helper.");
            var argument = call.Args.GetArgByIndex(1) ?? throw new InvalidOperationException("Missing dispatch cell argument.");
            var handle = accessType == InfoAccessType.IAT_VALUE ? argument.Node : argument.Node.AsIndir().Addr;

            Assert.Multiple(() => {
                Assert.That(handle.AsIntCon().IconVal, Is.EqualTo((nint)0x1234));
                Assert.That(handle.Flags & GenTreeFlags.GTF_ICON_HDL_MASK, Is.EqualTo(Globals.GTF_ICON_FTN_ADDR));
                Assert.That(argument.WellKnownArg, Is.EqualTo(WellKnownArg.RuntimeMethodHandle));
                Assert.That((call._callMoreFlags & GenTreeCallFlags.GTF_CALL_M_LDVIRTFTN_INTERFACE) != 0, Is.EqualTo(isInterface));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = new CORINFO_EE_INFO { targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void EmbedClassHandle(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* token, byte importParent,
        CORINFO_METHOD_STRUCT_* caller, CORINFO_GENERICHANDLE_RESULT* result)
    {
        *result = default;
        result->handleType = CorInfoGenericHandleType.CORINFO_HANDLETYPE_CLASS;
        result->compileTimeHandle = (CORINFO_GENERIC_STRUCT_*)token->hClass;
        result->lookup.constLookup.accessType = InfoAccessType.IAT_VALUE;
        result->lookup.constLookup.handle = result->compileTimeHandle;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetArrayRank(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => *(int*)type;

    private struct InstantiationHandle
    {
        public nint Value;
        public int Embedded;
        public int Restored;
        public bool Indirect;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* EmbedInstantiationClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle, void** indirection)
        => (CORINFO_CLASS_STRUCT_*)EmbedInstantiation((InstantiationHandle*)handle, indirection);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_STRUCT_* EmbedInstantiationMethod(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* handle, void** indirection)
        => (CORINFO_METHOD_STRUCT_*)EmbedInstantiation((InstantiationHandle*)handle, indirection);

    private static void* EmbedInstantiation(InstantiationHandle* handle, void** indirection)
    {
        handle->Embedded++;
        *indirection = handle->Indirect ? &handle->Value : null;
        return handle->Indirect ? null : (void*)handle->Value;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RestoreInstantiationClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
        => ((InstantiationHandle*)handle)->Restored++;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RestoreInstantiationMethod(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* handle)
        => ((InstantiationHandle*)handle)->Restored++;

    private struct BoxClass
    {
        public TypeCompareState EnumState;
        public int RestoreCount;
        public int EnumQueries;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc GetBoxHelper(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => CorInfoHelpFunc.CORINFO_HELP_BOX;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc GetNewHelper(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, bool* sideEffects)
    {
        *sideEffects = false;
        return CorInfoHelpFunc.CORINFO_HELP_NEWSFAST;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetBoxedType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetBoxType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => CorInfoType.CORINFO_TYPE_INT;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState IsEnum(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, CORINFO_CLASS_STRUCT_** underlyingType)
    {
        ((BoxClass*)type)->EnumQueries++;

        if (underlyingType is not null)
        {
            *underlyingType = null;
        }

        return ((BoxClass*)type)->EnumState;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RestoreBoxClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => ((BoxClass*)type)->RestoreCount++;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetBoxRank(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetBoxTypeArgument(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int index) => null;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static nint PrintBoxClassName(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, byte* buffer, nint length, nint* required)
    {
        var name = "Box"u8;
        *required = name.Length + 1;
        var written = (int)Math.Min(Math.Max(length - 1, 0), name.Length);
        name[..written].CopyTo(new Span<byte>(buffer, written));

        if (length > 0)
        {
            buffer[written] = 0;
        }

        return written;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte RunWithErrorTrap(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
    {
        callback(state);
        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetStructFlags(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => CorInfoFlag.CORINFO_FLG_VALUECLASS;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetStructSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => sizeof(int);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsValueClass(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 0;
}
