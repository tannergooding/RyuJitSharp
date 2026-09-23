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
            (delegate* unmanaged[MemberFunction]<ICorJitInfo*, delegate* unmanaged[Cdecl]<void*, void>, void*, bool>)
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
}
