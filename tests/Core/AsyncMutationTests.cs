// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AsyncMutationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void DefaultValueAnalysisIncludesCallDefinedTrackedLocals(bool tracked)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_I_IMPL;
            compiler.lvaTable[0].lvTracked = tracked;
#if DEBUG
            compiler.lvaTable[0].IsDefinedViaAddress = true;
#endif
            var address = compiler.gtNewLclVarAddrNode(TYP_BYREF, 0);
            address.Flags |= GTF_VAR_DEF;
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.SetIsAsync(default);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(address)
                .WithWellKnownArg(WellKnownArg.AsyncResumedDef));
            var mutated = VarSetOps.MakeEmpty(compiler);

            InvokeMutation(compiler, call, mutated, countDefaultStores: false);

            Assert.That(VarSetOps.IsMember(compiler, mutated, 0), Is.EqualTo(tracked));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void PromotedParentAddressMarksTrackedFieldsNotParent(
        bool parentTracked, bool countDefaultStores)
    {
        WithCompiler(compiler => {
            SetPromotedParent(compiler, parentTracked);
            var address = compiler.gtNewLclVarAddrNode(TYP_BYREF, 0);
            var mutated = VarSetOps.MakeEmpty(compiler);

            InvokeMutation(compiler, address, mutated, countDefaultStores);

            Assert.That(VarSetOps.IsMember(compiler, mutated, 0), Is.False);
            Assert.That(VarSetOps.IsMember(compiler, mutated, 1), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, mutated, 2), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void LogicalFieldDefinitionMarksOnlyTheDefinedTrackedField(bool countDefaultStores)
    {
        WithCompiler(compiler => {
            SetPromotedParent(compiler, parentTracked: true);
            var store = compiler.gtNewStoreLclFldNode(TYP_INT, 0, 0, compiler.gtNewIconNode(TYP_INT, 7));
            var mutated = VarSetOps.MakeEmpty(compiler);

            InvokeMutation(compiler, store, mutated, countDefaultStores);

            Assert.That(VarSetOps.IsMember(compiler, mutated, 0), Is.False);
            Assert.That(VarSetOps.IsMember(compiler, mutated, 1), Is.True);
            Assert.That(VarSetOps.IsMember(compiler, mutated, 2), Is.False);
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    public static void ZeroStoreCanBeIgnoredOnlyWhenPrologInitializationIsSufficient(
        bool explicitInitialization, bool countDefaultStores, bool expectedMutation)
    {
        WithCompiler(compiler => {
            compiler.info.compInitMem = true;
            compiler.lvaTable[0].lvHasExplicitInit = explicitInitialization;
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 0));
            var mutated = VarSetOps.MakeEmpty(compiler);

            InvokeMutation(compiler, store, mutated, countDefaultStores);

            Assert.That(VarSetOps.IsMember(compiler, mutated, 0), Is.EqualTo(expectedMutation));
        });
    }

    private static void InvokeMutation(Compiler compiler, GenTree node, object mutated, bool countDefaultStores)
    {
        var method = typeof(Compiler).GetMethod(
            countDefaultStores ? "MarkAsyncMutatedLocals" : "UpdateAsyncMutatedLocals",
            BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException();
        _ = method.Invoke(null, [compiler, node, mutated]);
    }

    private static void SetPromotedParent(Compiler compiler, bool parentTracked)
    {
        compiler.lvaTable[0].Type = TYP_STRUCT;
        compiler.lvaSetStruct(0, compiler.typGetBlkLayout(2 * sizeof(int)), unsafeValueClsCheck: false);
        compiler.lvaTable[0].lvTracked = parentTracked;
        compiler.lvaTable[0].lvPromoted = true;
        compiler.lvaTable[0].lvFieldLclStart = 1;
        compiler.lvaTable[0].lvFieldCnt = 2;
        for (var i = 1; i < 3; i++)
        {
            compiler.lvaTable[i].lvIsStructField = true;
            compiler.lvaTable[i].lvParentLcl = 0;
            compiler.lvaTable[i].lvFldOffset = (byte)((i - 1) * sizeof(int));
        }
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
        compiler.lvaInlinedPInvokeFrameVar = BAD_VAR_NUM;
        compiler.lvaRetAddrVar = BAD_VAR_NUM;
#if FEATURE_FIXED_OUT_ARGS
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
#endif
#if TARGET_ARM64
        compiler.lvaFfrRegister = BAD_VAR_NUM;
#endif
        compiler.lvaCount = 3;
        compiler.lvaTrackedCount = 3;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTable =
        [
            new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = 0 },
            new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = 1 },
            new LclVarDsc { Type = TYP_INT, lvTracked = true, _varIndex = 2 },
        ];
        JitTls.Compiler = compiler;
        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
