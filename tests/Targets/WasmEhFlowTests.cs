// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BasicBlockFlags;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.EHHandlerType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.Target.UnitTests;

[NonParallelizable]
internal static unsafe class WasmEhFlowTests
{
    [Test]
    public static void CatchReturnDispatchesToContinuationAndDefaultsToRethrow()
    {
        WithCompiler(compiler =>
        {
            var tryEntry = NewBlock(compiler, BBJ_ALWAYS);
            var continuation = NewBlock(compiler, BBJ_RETURN);
            var catchRet = NewBlock(compiler, BBJ_EHCATCHRET);
            tryEntry.SetFlags(BBF_DONT_REMOVE);
            catchRet.SetFlags(BBF_DONT_REMOVE);
            catchRet.CatchType = (RyuJitSharp.bbCatchType)1;
            Link(compiler, tryEntry, continuation, catchRet);

            tryEntry.TryIndex = 0;
            catchRet.HndIndex = 0;
            tryEntry.SetKindAndTargetEdge(BBJ_ALWAYS, compiler.fgAddRefPred(continuation, tryEntry));
            catchRet.SetKindAndTargetEdge(BBJ_EHCATCHRET, compiler.fgAddRefPred(continuation, catchRet));

            var normalPathStore = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1));
            tryEntry.InsertAtEnd(LIR.SeqTree(compiler, normalPathStore));
            continuation.InsertAtEnd(LIR.SeqTree(
                compiler,
                compiler.gtNewUnaryNode(GT_RETURN, TYP_VOID, null)));

            compiler.compHndBBtab =
            [
                new EHblkDsc
                {
                    ebdTryBeg = tryEntry,
                    ebdTryLast = tryEntry,
                    ebdHndBeg = catchRet,
                    ebdHndLast = catchRet,
                    ebdHandlerType = EH_HANDLER_CATCH,
                    ebdID = 0,
                    ebdEnclosingTryIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                    ebdEnclosingHndIndex = EHblkDsc.NO_ENCLOSING_INDEX,
                },
            ];
            compiler.compHndBBtabCount = 1;
            compiler.compEHID = 1;
            compiler.fgFuncletsCreated = true;
            compiler.fgFirstFuncletBB = catchRet;

            Assert.That(compiler.fgWasmEhFlow(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(s_assertions, Is.Empty);

            var dispatch = compiler.Blocks.Single(block => block.Kind is BBJ_SWITCH);
            var switchTargets = dispatch.SwitchTargets;
            Assert.That(dispatch.HasFlag(RyuJitSharp.BasicBlockFlags.BBF_CATCH_RESUMPTION), Is.True);
            Assert.That(dispatch.LastNode?.Oper, Is.EqualTo(GT_SWITCH));
            Assert.That(switchTargets.HasDefaultCase, Is.True);
            Assert.That(switchTargets.Cases.Length, Is.EqualTo(2));

            var resumePad = switchTargets.Cases[0].DestinationBlock;
            Assert.That(resumePad.Kind, Is.EqualTo(BBJ_ALWAYS));
            Assert.That(resumePad.Target, Is.SameAs(continuation));
            var resetNode = resumePad.LastNode
                ?? throw new AssertionException("The continuation resume pad did not clear its resume index.");
            Assert.That(resetNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            var reset = resetNode.AsLclVar();
            Assert.That(reset.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(reset.LclNum, Is.EqualTo(compiler.lvaWasmResumeIP));
            Assert.That(reset.Data.IsIntegralConst(0), Is.True);

            var rethrow = switchTargets.DefaultCase.DestinationBlock;
            Assert.That(rethrow.Kind, Is.EqualTo(BBJ_THROW));
            Assert.That(rethrow.LastNode?.Oper, Is.EqualTo(GT_WASM_THROW_REF));

            Assert.That(tryEntry.Kind, Is.EqualTo(BBJ_COND));
            Assert.That(tryEntry.TrueTarget, Is.SameAs(dispatch));
            Assert.That(tryEntry.LastNode?.Oper, Is.EqualTo(GT_WASM_JEXCEPT));
            Assert.That(tryEntry.FalseTarget.LastNode, Is.SameAs(normalPathStore));

            var resumeIndexStoreNode = catchRet.LastNode
                ?? throw new AssertionException("The catch-return block did not set its resume index.");
            Assert.That(resumeIndexStoreNode.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            var resumeIndexStore = resumeIndexStoreNode.AsLclVar();
            Assert.That(resumeIndexStore.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(resumeIndexStore.LclNum, Is.EqualTo(compiler.lvaWasmResumeIP));
            Assert.That(resumeIndexStore.Data.IsIntegralConst(1), Is.True);
        });
    }

    private static BasicBlock NewBlock(Compiler compiler, BBKinds kind)
    {
        var block = BasicBlock.New(compiler, kind);
        block.MakeLir(null, null);
        return block;
    }

    private static void Link(Compiler compiler, params BasicBlock[] blocks)
    {
        compiler.fgFirstBB = blocks[0];
        compiler.fgLastBB = blocks[^1];
        blocks[0].Prev = null;
        blocks[^1].Next = null;
        for (var i = 0; i < blocks.Length - 1; i++)
        {
            blocks[i].Next = blocks[i + 1];
            blocks[i + 1].Prev = blocks[i];
        }
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        var previous = JitTls.Compiler;
#if DEBUG
        var previousConfig = JitConfig;
#endif
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitTls.Compiler = compiler;

        try
        {
            JitFlags flags = default;
            flags.Set(JitFlags.JIT_FLAG_ALT_JIT);
            compiler.opts.jitFlags = &flags;
            compiler.opts.SetMinOpts(false);
            compiler.info.compFullName = nameof(WasmEhFlowTests);
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.fgPredsComputed = true;
            compiler.fgImportDone = true;
            compiler.compHndBBtab = [];
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
            compiler.fgSafeFlowEdgeCreation = true;
            JitConfig = new JitConfigValues();
            AltJitSkipOnAssert(ref JitConfig) = 1;
#endif
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
            compiler.lvaCount = 1;
            s_assertions.Clear();
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
#if DEBUG
            JitConfig = previousConfig;
#endif
        }
    }

    private static readonly List<string> s_assertions = [];

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "");

        return 0;
    }

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_altJitSkipOnAssert")]
    private static extern ref int AltJitSkipOnAssert(ref JitConfigValues config);
#endif
}
#endif
