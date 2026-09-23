// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.CorInfoTypeWithMod;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class InlineVariableTests
{
    private static CorInfoTypeWithMod[] s_types = [];
    private static readonly List<nuint> s_queries = [];
    private static int s_nextCalls;

    [TestCase(false)]
    [TestCase(true)]
    public static void InitializesPersistentTablesInSignatureOrder(bool pseudoArguments)
    {
        WithCompiler((compiler, info, call) => {
            s_types = [(CorInfoTypeWithMod)CORINFO_TYPE_INT, (CorInfoTypeWithMod)CORINFO_TYPE_DOUBLE];
            info.inlineCandidateInfo.methInfo.args.numArgs = 2;
            var first = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 1)));
            CallArg? context = null;

            if (pseudoArguments)
            {
                ReadOnlySpan<WellKnownArg> kinds = [
                    WellKnownArg.AsyncContinuation, WellKnownArg.AsyncResumedUse,
                    WellKnownArg.AsyncResumedDef, WellKnownArg.AsyncAwaiter, WellKnownArg.RetBuffer
                ];

                foreach (var kind in kinds)
                {
                    _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_I_IMPL, 0)).WithWellKnownArg(kind));
                }

                context = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_I_IMPL, 8)).WithWellKnownArg(WellKnownArg.InstParam));
            }

            var second = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewDconNode(TYP_DOUBLE, 2)));
            info.inlArgInfo[0].argHasSideEff = true;
            info.lclVarInfo[0].lclHasLdlocaOp = true;

            compiler.impInlineInitVars(info);

            Assert.Multiple(() => {
                Assert.That(info.inlineResult.IsFailure, Is.False);
                Assert.That(info.argCnt, Is.EqualTo(2));
                Assert.That(info.inlArgInfo[0].arg, Is.SameAs(first));
                Assert.That(info.inlArgInfo[1].arg, Is.SameAs(second));
                Assert.That(info.inlArgInfo[0].argHasSideEff, Is.False);
                Assert.That(info.inlArgInfo[0].argIsInvariant, Is.True);
                Assert.That(info.lclVarInfo[0].lclTypeInfo, Is.EqualTo(TYP_INT));
                Assert.That(info.lclVarInfo[1].lclTypeInfo, Is.EqualTo(TYP_DOUBLE));
                Assert.That(info.lclVarInfo[0].lclHasLdlocaOp, Is.False);
                Assert.That(s_queries, Is.EqualTo(new nuint[] { 1, 2 }));
                Assert.That(s_nextCalls, Is.EqualTo(2));
            });

            foreach (ref var argInfo in (Span<InlArgInfo>)info.inlArgInfo)
            {
                Assert.That(argInfo.argTmpNum, Is.EqualTo(BAD_VAR_NUM));
            }

            Assert.That(info.inlInstParamArgInfo?[0].arg, Is.SameAs(context));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RecordsReceiverTypeAndRejectsNullThis(bool nullThis)
    {
        WithCompiler((compiler, info, call) => {
            info.inlineCandidateInfo.methInfo.args.callConv = CorInfoCallConv.CORINFO_CALLCONV_HASTHIS;
            info.inlineCandidateInfo.clsHandle = (CORINFO_CLASS_STRUCT_*)0x800;
            compiler.lvaTable[0].Type = TYP_REF;
            GenTree node = nullThis ? compiler.gtNewNull() : compiler.gtNewLclvNode(TYP_REF, 0);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(node).WithWellKnownArg(WellKnownArg.ThisPointer));

            compiler.impInlineInitVars(info);

            Assert.That(info.argCnt, Is.EqualTo(1));
            Assert.That(info.inlArgInfo[0].argIsThis, Is.True);
            Assert.That(info.inlineResult.IsFailure, Is.EqualTo(nullThis));
            Assert.That(s_queries, Is.Empty);

            if (nullThis)
            {
                Assert.That(info.inlineResult.Observation, Is.EqualTo(InlineObservation.CALLSITE_ARG_HAS_NULL_THIS));
            }
            else
            {
                Assert.That(info.lclVarInfo[0].lclTypeInfo, Is.EqualTo(TYP_REF));
                Assert.That((nuint)info.lclVarInfo[0].lclTypeHandle, Is.EqualTo((nuint)0x800));
            }
        });
    }

    [TestCase(TYP_UBYTE, CORINFO_TYPE_UBYTE)]
    [TestCase(TYP_SHORT, CORINFO_TYPE_SHORT)]
    public static void NarrowArgumentsKeepSignatureTypeAndCast(var_types expected, CorInfoType signature)
    {
        WithCompiler((compiler, info, call) => {
            s_types = [(CorInfoTypeWithMod)signature];
            info.inlineCandidateInfo.methInfo.args.numArgs = 1;
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 0)));

            compiler.impInlineInitVars(info);

            Assert.That(info.inlineResult.IsFailure, Is.False);
            Assert.That(info.lclVarInfo[0].lclTypeInfo, Is.EqualTo(expected));
            Assert.That(arg.Node.Oper, Is.EqualTo(GT_CAST));
            Assert.That(arg.Node.AsCast().CastType, Is.EqualTo(expected));
            Assert.That(info.inlArgInfo[0].argIsLclVar, Is.False);
        });
    }

    [Test]
    public static void LocalsRetainTypesHandlesAndPinning()
    {
        WithCompiler((compiler, info, call) => {
            s_types = [
                (CorInfoTypeWithMod)CORINFO_TYPE_CLASS | CORINFO_TYPE_MOD_PINNED,
                (CorInfoTypeWithMod)CORINFO_TYPE_BYREF,
                (CorInfoTypeWithMod)CORINFO_TYPE_INT | CORINFO_TYPE_MOD_PINNED
            ];
            info.inlineCandidateInfo.methInfo.locals.numArgs = 3;
            info.inlineCandidateInfo.methInfo.locals.args = (CORINFO_ARG_LIST_STRUCT_*)1;

            compiler.impInlineInitVars(info);

            Assert.Multiple(() => {
                Assert.That(info.inlineResult.IsFailure, Is.False);
                Assert.That(info.argCnt, Is.Zero);
                Assert.That(info.numberOfGcRefLocals, Is.EqualTo(2));
                Assert.That(info.lclVarInfo[0].lclTypeInfo, Is.EqualTo(TYP_REF));
                Assert.That((nuint)info.lclVarInfo[0].lclTypeHandle, Is.EqualTo((nuint)0x800));
                Assert.That(info.lclVarInfo[0].lclIsPinned, Is.True);
                Assert.That(info.lclVarInfo[1].lclTypeInfo, Is.EqualTo(TYP_BYREF));
                Assert.That(info.lclVarInfo[1].lclIsPinned, Is.False);
                Assert.That(info.lclVarInfo[2].lclTypeInfo, Is.EqualTo(TYP_INT));
                Assert.That(info.lclVarInfo[2].lclIsPinned, Is.False);
                Assert.That(s_queries, Is.EqualTo(new nuint[] { 1, 2, 3 }));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void RecordsAddressAliasesAndEffectsWithoutPretendingTheArgumentIsWritten(bool structAddress)
    {
        WithCompiler((compiler, info, call) => {
            compiler.lvaTable[0].lvHasLdAddrOp = true;
            compiler.lvaTable[0].Type = structAddress ? TYP_STRUCT : TYP_INT;
            GenTree node = structAddress
                ? compiler.gtNewLclVarAddrNode(TYP_BYREF, 0)
                : compiler.gtNewLclvNode(TYP_INT, 0);
            node.Flags |= GTF_GLOB_REF | GTF_ORDER_SIDEEFF;
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(node));
            InlArgInfo argInfo = default;

            compiler.impInlineRecordArgInfo(info, arg, ref argInfo, info.inlineResult);

            Assert.Multiple(() => {
                Assert.That(argInfo.arg, Is.SameAs(arg));
                Assert.That(argInfo.argIsByRefToStructLocal, Is.EqualTo(structAddress));
                Assert.That(argInfo.argIsInvariant, Is.EqualTo(structAddress));
                Assert.That(argInfo.argHasCallerLocalRef, Is.EqualTo(!structAddress));
                Assert.That(argInfo.argHasGlobRef, Is.True);
                Assert.That(argInfo.argHasSideEff, Is.True);
                Assert.That(argInfo.argHasLdargaOp, Is.False);
                Assert.That(argInfo.argHasStargOp, Is.False);
            });
        });
    }

    private static void WithCompiler(Action<Compiler, InlineInfo, GenTreeCall> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler._inlineStrategy = (InlineStrategy)RuntimeHelpers.GetUninitializedObject(typeof(InlineStrategy));
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 1;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArgType = &GetArgumentType;
        vtable.Base.Base.getArgNext = &GetNextArgument;
        vtable.Base.Base.getArgClass = &GetArgumentClass;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        compiler.info.compCompHnd = &jitInfo;
        JitTls.Compiler = compiler;
        s_types = [];
        s_queries.Clear();
        s_nextCalls = 0;

        try
        {
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            var result = new InlineResult(compiler, call, null, "inline variable setup", doNotReport: true);
            result.NoteBool(InlineObservation.CALLEE_IS_FORCE_INLINE, false);
            result.NoteInt(InlineObservation.CALLEE_IL_CODE_SIZE, 1);
            var info = new InlineInfo {
                iciCall = call,
                iciBlock = new BasicBlock(null, null),
                inlineCandidateInfo = new InlineCandidateInfo(),
                inlineResult = result,
            };
            info.inlineCandidateInfo.methInfo.args.args = (CORINFO_ARG_LIST_STRUCT_*)1;
            action(compiler, info, call);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod GetArgumentType(ICorJitInfo* self, CORINFO_SIG_INFO* signature,
        CORINFO_ARG_LIST_STRUCT_* argument, CORINFO_CLASS_STRUCT_** type)
    {
        var index = (nuint)argument;
        s_queries.Add(index);
        *type = null;

        return index > 0 && index <= (nuint)s_types.Length ? s_types[(int)index - 1] : (CorInfoTypeWithMod)CORINFO_TYPE_INT;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_ARG_LIST_STRUCT_* GetNextArgument(ICorJitInfo* self, CORINFO_ARG_LIST_STRUCT_* argument)
    {
        s_nextCalls++;

        return (CORINFO_ARG_LIST_STRUCT_*)((nuint)argument + 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetArgumentClass(ICorJitInfo* self, CORINFO_SIG_INFO* signature, CORINFO_ARG_LIST_STRUCT_* argument)
        => (CORINFO_CLASS_STRUCT_*)0x800;
}
