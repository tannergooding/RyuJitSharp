// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class PInvokeFrameLoweringTests
{
    [Test]
    public static void HelperMethodWithoutSecretArgumentDoesNotInitializeFrame()
    {
        WithCompiler((compiler, block, lowering, ee) => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS);
            var original = new GenTree(GT_NO_OP, TYP_VOID);
            block.InsertAtEnd(original);

            InsertPInvokeMethodProlog(lowering);

            Assert.That(block.FirstNode, Is.SameAs(original));
            Assert.That(block.LastNode, Is.SameAs(original));
            Assert.That(ee->InfoLookups, Is.Zero);
        });
    }

    [Test]
    public static void HelperMethodStoresSecretArgumentAfterCatchArgument()
    {
        WithCompiler((compiler, block, lowering, ee) => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS);
            compiler.info.compPublishStubParam = true;
            var catchArg = new GenTree(GT_CATCH_ARG, TYP_I_IMPL);
            var original = new GenTree(GT_NO_OP, TYP_VOID);
            block.InsertAtEnd(catchArg);
            block.InsertAtEnd(original);

            InsertPInvokeMethodProlog(lowering);

            var secretValue = catchArg.Next;
            var secretStore = secretValue?.Next;
            Assert.Multiple(() => {
                Assert.That(secretValue?.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(secretValue?.AsLclVar().LclNum, Is.EqualTo(compiler.lvaStubArgumentVar));
                Assert.That(secretStore?.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
                Assert.That(secretStore?.AsLclFld().LclNum, Is.EqualTo(compiler.lvaInlinedPInvokeFrameVar));
                Assert.That(secretStore?.AsLclFld().LclOffs, Is.EqualTo(40));
                Assert.That(secretStore?.Next, Is.SameAs(original));
                Assert.That(ee->InfoLookups, Is.EqualTo(1));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void MethodExitPopsOnlyILStubFrameAfterReturnOperands(bool isILStub)
    {
        WithCompiler((compiler, block, lowering, ee) => {
            if (isILStub)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_IL_STUB);
            }

            block.Kind = BBJ_RETURN;
            var value = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_I_IMPL, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);

            InsertPInvokeMethodEpilog(lowering, block, ret);

            if (isILStub)
            {
                var link = block.LastNode?.Prev;
                Assert.Multiple(() => {
                    Assert.That(block.LastNode, Is.SameAs(ret));
                    Assert.That(link?.Oper, Is.EqualTo(GT_STOREIND));
                    Assert.That(link?.AsStoreInd().Addr.AsAddrMode().Offset, Is.EqualTo(16));
                    Assert.That(link?.AsStoreInd().Data.Oper, Is.EqualTo(GT_LCL_FLD));
                    Assert.That(link?.AsStoreInd().Data.AsLclFld().LclOffs, Is.EqualTo(8));
                    Assert.That(value.Next, Is.Not.SameAs(ret));
                    Assert.That(ee->InfoLookups, Is.EqualTo(1));
                });
            }
            else
            {
                Assert.That(value.Next, Is.SameAs(ret));
                Assert.That(ee->InfoLookups, Is.Zero);
            }
        });
    }

    [Test]
    public static void HelperMethodExitDoesNotPopFrame()
    {
        WithCompiler((compiler, block, lowering, ee) => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS);
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_IL_STUB);
            block.Kind = BBJ_RETURN;
            var ret = new GenTreeUnOp(GT_RETURN, TYP_VOID, null);
            block.InsertAtEnd(ret);

            InsertPInvokeMethodEpilog(lowering, block, ret);

            Assert.That(block.FirstNode, Is.SameAs(ret));
            Assert.That(ee->InfoLookups, Is.Zero);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InsertPInvokeMethodProlog")]
    private static extern void InsertPInvokeMethodProlog(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InsertPInvokeMethodEpilog")]
    private static extern void InsertPInvokeMethodEpilog(Lowering lowering, BasicBlock block, GenTree lastExpr);

    private delegate void LoweringAction(Compiler compiler, BasicBlock block, Lowering lowering, TargetEE* ee);

    private static void WithCompiler(LoweringAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        TargetEE ee = new() {
            Interface = new ICorJitInfo { lpVtbl = &vtable },
        };
#if DEBUG
        using var tls = new JitTls(&ee.Interface);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.info.compCompHnd = &ee.Interface;
        compiler.info.compMatchedVM = true;
        compiler.info.compUnmanagedCallCountWithGCTransition = 1;
        compiler.info.compLvFrameListRoot = 1;
        compiler.lvaInlinedPInvokeFrameVar = 2;
        compiler.lvaStubArgumentVar = 3;
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc(), new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 4;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        compiler.lvaTable[1].Type = TYP_I_IMPL;
        compiler.lvaTable[2].Type = TYP_STRUCT;
        compiler.lvaTable[2].Layout = new ClassLayout(64);
        compiler.lvaTable[2].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
        compiler.lvaTable[3].Type = TYP_I_IMPL;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            compiler.fgFirstBB = block;
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, block, lowering, &ee);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TargetEE
    {
        public ICorJitInfo Interface;
        public int InfoLookups;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        ((TargetEE*)self)->InfoLookups++;
        *info = new CORINFO_EE_INFO {
            targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI,
            offsetOfThreadFrame = 16,
            inlinedCallFrameInfo = new CORINFO_EE_INFO.InlinedCallFrameInfo {
                offsetOfFrameLink = 8,
                offsetOfSecretStubArg = 40,
                offsetOfCallSiteSP = 48,
                offsetOfCalleeSavedFP = 56,
            },
        };
    }
}
