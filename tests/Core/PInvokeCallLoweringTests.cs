// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class PInvokeCallLoweringTests
{
#if TARGET_ARM64
    [Test]
    public static void Arm64CallTargetRangeReliesOnRelocationStub()
    {
        WithCompiler((compiler, block, lowering, ee) => {
            var codeGen = compiler.codeGen;
            assert(codeGen is not null);
            Assert.That(codeGen.validImmForBL(0), Is.True);
            Assert.That(codeGen.validImmForBL(nint.MaxValue), Is.True);
        });
    }
#endif

    [TestCase(InfoAccessType.IAT_VALUE, false, GT_CNS_INT)]
    [TestCase(InfoAccessType.IAT_VALUE, true, GT_NONE)]
    [TestCase(InfoAccessType.IAT_PVALUE, false, GT_IND)]
    [TestCase(InfoAccessType.IAT_PPVALUE, false, GT_IND)]
    public static void SuppressedTransitionsRespectPInvokeTargetLookup(
        InfoAccessType accessType, bool aot, genTreeOps expected)
    {
        WithCompiler((compiler, block, lowering, ee) => {
            ee->LookupType = accessType;
            if (aot)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            }
            var call = NewPInvokeCall();
            call._callMoreFlags |= GTF_CALL_M_SUPPRESS_GC_TRANSITION;
            block.InsertAtEnd(call);

            var result = LowerNonvirtPinvokeCall(lowering, call);

            Assert.That(result?.Oper ?? GT_NONE, Is.EqualTo(expected));
            Assert.That(ee->TargetLookups, Is.EqualTo(1));
            Assert.That(block.FirstNode, Is.SameAs(call));
            Assert.That(block.LastNode, Is.SameAs(call));
            Assert.That((nint)call._directCallAddress, Is.EqualTo(aot ? ee->Target : (nint)0));
#if FEATURE_READYTORUN
            if (aot)
            {
                Assert.That((nint)call._entryPoint.addr, Is.EqualTo((nint)0));
                Assert.That(call._entryPoint.accessType, Is.EqualTo(InfoAccessType.IAT_VALUE));
            }
#endif
            if (accessType is InfoAccessType.IAT_PPVALUE)
            {
                Assert.That(result?.AsIndir().Addr.Oper, Is.EqualTo(GT_IND));
                Assert.That(result?.AsIndir().Addr.AsIndir().Addr.AsIntCon().IconValue, Is.EqualTo(ee->Target));
            }
            else if (accessType is InfoAccessType.IAT_PVALUE)
            {
                Assert.That(result?.AsIndir().Addr.AsIntCon().IconValue, Is.EqualTo(ee->Target));
            }
        });
    }

    [Test]
    public static void SuppressedIndirectCallDoesNotQueryDirectTargetOrModifyLir()
    {
        WithCompiler((compiler, block, lowering, ee) => {
            var target = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var call = NewPInvokeCall();
            call._callType = CT_INDIRECT;
            call._controlExpr = target;
            call._callMoreFlags |= GTF_CALL_M_SUPPRESS_GC_TRANSITION;
            block.InsertAtEnd(target);
            block.InsertAtEnd(call);

            Assert.That(LowerNonvirtPinvokeCall(lowering, call), Is.Null);
            Assert.That(ee->TargetLookups, Is.Zero);
            Assert.That(target.Next, Is.SameAs(call));
            Assert.That(call._controlExpr, Is.SameAs(target));
        });
    }

    [Test]
    public static void IndirectTransitionPublishesFrameBeforeEvaluatingCallTarget()
    {
        WithCompiler((compiler, block, lowering, ee) => {
            var target = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var call = NewPInvokeCall();
            call._callType = CT_INDIRECT;
            call._controlExpr = target;
            block.InsertAtEnd(target);
            block.InsertAtEnd(call);

            Assert.That(LowerNonvirtPinvokeCall(lowering, call), Is.Null);
            Assert.That(ee->TargetLookups, Is.Zero);

            var datumStores = 0;
            var returnAddressStores = 0;
            var beforeTarget = true;
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                if (node == target)
                {
                    beforeTarget = false;
                }
                if (node.Oper is GT_STORE_LCL_FLD)
                {
                    var store = node.AsLclFld();
                    if (store.LclOffs == 24)
                    {
                        Assert.That(beforeTarget, Is.True);
                        Assert.That(store.Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
                        datumStores++;
                    }
                    if (store.LclOffs == 32)
                    {
                        Assert.That(beforeTarget, Is.True);
                        returnAddressStores++;
                    }
                }
            }
            Assert.That(datumStores, Is.EqualTo(1));
            Assert.That(returnAddressStores, Is.EqualTo(1));
            Assert.That(target.Next, Is.SameAs(call));
        });
    }

    [TestCase(false, 2)]
    [TestCase(true, 0)]
    public static void InlineTransitionLinksFrameAndSwitchesGCStateAroundCall(bool isILStub, int expectedFrameLinks)
    {
        WithCompiler((compiler, block, lowering, ee) => {
            if (isILStub)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_IL_STUB);
            }
            var call = NewPInvokeCall();
            block.InsertAtEnd(call);

            Assert.That(LowerNonvirtPinvokeCall(lowering, call)?.Oper, Is.EqualTo(GT_CNS_INT));
            Assert.That(ee->TargetLookups, Is.EqualTo(1));

            var callTargetStores = 0;
            var returnAddressStores = 0;
            var gcStoresBefore = 0;
            var gcStoresAfter = 0;
            var frameLinks = 0;
            var returnTraps = 0;
            var preemptiveStarts = 0;
            var beforeCall = true;
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                if (node == call)
                {
                    beforeCall = false;
                }
                if (node.Oper is GT_STORE_LCL_FLD)
                {
                    var field = node.AsLclFld();
                    if (field.LclOffs == 24)
                    {
                        callTargetStores++;
                        Assert.That(beforeCall, Is.True);
                    }
                    if (field.LclOffs == 32)
                    {
                        returnAddressStores++;
                        Assert.That(beforeCall, Is.True);
                    }
                }
                if (node.Oper is GT_STOREIND)
                {
                    var store = node.AsStoreInd();
                    if (store.Addr.Oper is GT_LEA && store.Addr.AsAddrMode().Offset == 8)
                    {
#if DEBUG
                        Assert.That(store.Data.TreeId, Is.LessThan(store.Addr.TreeId));
#endif
                        if (beforeCall)
                        {
                            Assert.That(store.Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
                            gcStoresBefore++;
                        }
                        else
                        {
                            Assert.That(store.Data.AsIntCon().IconValue, Is.EqualTo((nint)1));
                            gcStoresAfter++;
                        }
                    }
                    if (store.Addr.Oper is GT_LEA && store.Addr.AsAddrMode().Offset == 16)
                    {
                        frameLinks++;
                    }
                }
                if (node.Oper is GT_START_PREEMPTGC)
                {
                    preemptiveStarts++;
                    Assert.That(node.Next, Is.SameAs(call));
                }
                if (node.Oper is GT_RETURNTRAP)
                {
                    returnTraps++;
                    Assert.That(beforeCall, Is.False);
                    Assert.That(node.AsUnOp().Op1.IsContained,
                        Is.EqualTo(TargetArchitecture.IsX64 || TargetArchitecture.IsX86));
                }
            }
            Assert.Multiple(() => {
                Assert.That(callTargetStores, Is.EqualTo(1));
                Assert.That(returnAddressStores, Is.EqualTo(1));
                Assert.That(gcStoresBefore, Is.EqualTo(1));
                Assert.That(gcStoresAfter, Is.EqualTo(1));
                Assert.That(frameLinks, Is.EqualTo(expectedFrameLinks));
                Assert.That(returnTraps, Is.EqualTo(1));
                Assert.That(preemptiveStarts, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public static void HelperTransitionsLowerTheBeginCallBeforeTheUnmanagedCall()
    {
        WithCompiler((compiler, block, lowering, ee) => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS);
            var call = NewPInvokeCall();
            block.InsertAtEnd(call);

            _ = LowerNonvirtPinvokeCall(lowering, call);

            GenTreeCall? begin = null;
            GenTreeCall? end = null;
            var beforeCall = true;
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                if (node == call)
                {
                    beforeCall = false;
                }
                if (node is GenTreeCall helper && helper.IsHelperCall(CorInfoHelpFunc.CORINFO_HELP_JIT_PINVOKE_BEGIN))
                {
                    Assert.That(beforeCall, Is.True);
                    Assert.That(begin, Is.Null);
                    begin = helper;
                }
                if (node is GenTreeCall endHelper && endHelper.IsHelperCall(CorInfoHelpFunc.CORINFO_HELP_JIT_PINVOKE_END))
                {
                    Assert.That(beforeCall, Is.False);
                    Assert.That(end, Is.Null);
                    end = endHelper;
                }
            }
            Assert.That(begin, Is.Not.Null);
            Assert.That(end, Is.Not.Null);
            assert(begin is not null);
            assert(end is not null);
            Assert.That(begin.Args.Head?.Node.Oper, Is.EqualTo(GT_PUTARG_REG));
            Assert.That((nint)begin._directCallAddress, Is.EqualTo((nint)0x1110));
            Assert.That(end.Args.Head?.Node.Oper, Is.EqualTo(GT_LCL_ADDR));
        });
    }

#if SWIFT_SUPPORT
    [TestCase(false)]
    [TestCase(true)]
    public static void SwiftErrorConsumerMovesBeforeThePInvokeEpilog(bool suppressTransition)
    {
        WithCompiler((compiler, block, lowering, ee) => {
            var call = NewPInvokeCall();
            call._unmgdCallConv = CorInfoCallConvExtension.Swift;
            if (suppressTransition)
            {
                call._callMoreFlags |= GTF_CALL_M_SUPPRESS_GC_TRANSITION;
            }
            var errorArgument = compiler.gtNewIconNode(TYP_I_IMPL, 0);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(errorArgument).WithWellKnownArg(WellKnownArg.SwiftError));
            var separator = new GenTree(GT_NO_OP, TYP_VOID);
            var error = new GenTree(GT_SWIFT_ERROR, TYP_I_IMPL);
            var successor = new GenTree(GT_NO_OP, TYP_VOID);
            block.InsertAtEnd(errorArgument);
            block.InsertAtEnd(call);
            block.InsertAtEnd(separator);
            block.InsertAtEnd(error);
            block.InsertAtEnd(successor);

            _ = LowerNonvirtPinvokeCall(lowering, call);

            Assert.That(call.Next, Is.SameAs(error));
            Assert.That(error.Prev, Is.SameAs(call));
            Assert.That(separator.Next, Is.SameAs(successor));
            Assert.That(block.LastNode, Is.SameAs(successor));
            if (suppressTransition)
            {
                Assert.That(error.Next, Is.SameAs(separator));
            }
            else
            {
                Assert.That(error.Next?.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(error.Next?.AsLclVar().LclNum, Is.EqualTo(compiler.info.compLvFrameListRoot));
            }
        });
    }

    [TestCase(false, false, false)]
    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public static void SwiftErrorHandlingRequiresBothTheCallingConventionAndArgument(
        bool swift, bool errorArgument, bool expected)
    {
        WithCompiler((compiler, block, lowering, ee) => {
            var call = NewPInvokeCall();
            call._unmgdCallConv = swift ? CorInfoCallConvExtension.Swift : CorInfoCallConvExtension.C;
            if (errorArgument)
            {
                var value = compiler.gtNewIconNode(TYP_I_IMPL, 0);
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(value).WithWellKnownArg(WellKnownArg.SwiftError));
            }

            Assert.That(call.HasSwiftErrorHandling, Is.EqualTo(expected));
        });
    }
#endif

    private static GenTreeCall NewPInvokeCall()
    {
        var call = new GenTreeCall(TYP_VOID) {
            _callType = CT_USER_FUNC,
            _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x1234,
        };
        call.Flags |= GTF_CALL_UNMANAGED;
        return call;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNonvirtPinvokeCall")]
    private static extern GenTree? LowerNonvirtPinvokeCall(Lowering lowering, GenTreeCall call);

    private delegate void LoweringAction(Compiler compiler, BasicBlock block, Lowering lowering, TargetEE* ee);

    private static void WithCompiler(LoweringAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        vtable.Base.getAddrOfCaptureThreadGlobal = &GetCaptureGlobal;
        vtable.Base.embedMethodHandle = &EmbedMethodHandle;
        vtable.Base.getAddressOfPInvokeTarget = &GetPInvokeTarget;
        vtable.Base.getHelperFtn = &GetHelperFtn;
        TargetEE ee = new() {
            Interface = new ICorJitInfo { lpVtbl = &vtable },
            Target = 0x5678,
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
        compiler.lvaTable = [new LclVarDsc(), new LclVarDsc(), new LclVarDsc()];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        compiler.lvaTable[1].Type = TYP_I_IMPL;
        compiler.lvaTable[2].Type = TYP_STRUCT;
        compiler.lvaTable[2].Layout = new ClassLayout(64);
        compiler.lvaTable[2].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            compiler.compCurBB = block;
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
        public InfoAccessType LookupType;
        public nint Target;
        public int TargetLookups;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = new CORINFO_EE_INFO {
            targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI,
            offsetOfGCState = 8,
            offsetOfThreadFrame = 16,
            inlinedCallFrameInfo = new CORINFO_EE_INFO.InlinedCallFrameInfo {
                offsetOfFrameLink = 8,
                offsetOfCallTarget = 24,
                offsetOfReturnAddress = 32,
            },
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int* GetCaptureGlobal(ICorJitInfo* self, void** indirection)
    {
        *indirection = null;
        return (int*)0xABC0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_STRUCT_* EmbedMethodHandle(ICorJitInfo* self,
        CORINFO_METHOD_STRUCT_* method, void** indirection)
    {
        *indirection = null;
        return method;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x1110;
        return lookup->addr;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetPInvokeTarget(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method,
        CORINFO_CONST_LOOKUP* lookup)
    {
        var ee = (TargetEE*)self;
        ee->TargetLookups++;
        lookup->addr = (void*)ee->Target;
        lookup->accessType = ee->LookupType;
    }
}
