// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ImporterCallTests
{
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
}
