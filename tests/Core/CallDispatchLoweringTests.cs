// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CallDispatchLoweringTests
{
    [Test]
    public static void DirectUserCallUsesMetadataAndReplacesStaleTargetAddress()
    {
        WithCompiler((compiler, block, lowering) => {
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x1234,
                _directCallAddress = (void*)0x9999,
            };
            block.InsertAtEnd(call);

            Assert.That(LowerCall(lowering, call), Is.Null);
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x5678));
            Assert.That(call._controlExpr, Is.Null);
            Assert.That(block.FirstNode, Is.SameAs(call));
        });
    }

    [TestCase(false, MIN_ARG_AREA_FOR_CALL)]
    [TestCase(true, 0)]
    public static void FastTailCallDispatchDoesNotReserveOrdinaryOutgoingArgumentSpace(bool tailCall, int expectedSpace)
    {
        WithCompiler((compiler, block, lowering) => {
            block.Kind = BBKinds.BBJ_RETURN;
            compiler.fgFirstBB = block;
            compiler.fgBBcount = 1;
            compiler.compCurBB = block;
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x1234,
                _callMoreFlags = tailCall ? GenTreeCallFlags.GTF_CALL_M_TAILCALL : 0,
            };
            block.InsertAtEnd(call);

            Assert.That(LowerCall(lowering, call), Is.Null);
            Assert.That(OutgoingArgSpaceSize(lowering), Is.EqualTo(expectedSpace));
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x5678));
            Assert.That(block.FirstNode, Is.SameAs(call));
            Assert.That(block.LastNode, Is.SameAs(call));
        });
    }

    [Test]
    public static void DirectHelperCallUsesHelperLookup()
    {
        WithCompiler((compiler, block, lowering) => {
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_HELPER,
                _callMethHnd = Compiler.eeFindHelper(CorInfoHelpFunc.CORINFO_HELP_THROWNULLREF),
            };
            block.InsertAtEnd(call);

            Assert.That(LowerCall(lowering, call), Is.Null);
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x5678));
            Assert.That(call._controlExpr, Is.Null);
        });
    }

    [Test]
    public static void IndirectCallRetainsItsControlOwnerAndContainsSafeMemoryTarget()
    {
        WithCompiler((compiler, block, lowering) => {
            Assert.That(compiler.opts.Tier0OptimizationEnabled, Is.True);
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var target = compiler.gtNewIndir(TYP_I_IMPL, address);
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_INDIRECT,
                _controlExpr = target,
            };
            block.InsertAtEnd(address);
            block.InsertAtEnd(target);
            block.InsertAtEnd(call);

            Assert.That(LowerCall(lowering, call), Is.Null);
            Assert.That(call._controlExpr, Is.SameAs(target));
            Assert.That(target.IsContained, Is.True);
            Assert.That(target.Next, Is.SameAs(call));
        });
    }

    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMCPY, 8)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMCPY, 0)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMSET, 8)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMSET, 0)]
    public static void MemoryHelpersExpandBeforeArgumentPlacementOrRetainOrdinaryCallLowering(
        CorInfoHelpFunc helper, int size)
    {
        WithCompiler((compiler, block, lowering) => {
            var destination = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            GenTree source = helper is CorInfoHelpFunc.CORINFO_HELP_MEMSET
                ? compiler.gtNewIconNode(TYP_INT, 0)
                : compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var length = compiler.gtNewIconNode(TYP_I_IMPL, size);
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_HELPER,
                _callMethHnd = Compiler.eeFindHelper(helper),
            };
            var destinationArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(destination));
            destinationArg.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(regNumber.REG_RCX, 0, 8);
            var sourceArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(source));
            sourceArg.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(regNumber.REG_RDX, 0, source.Type.Size);
            var lengthArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(length));
            lengthArg.AbiInfo.Segments[0] = AbiPassingSegment.InRegister(regNumber.REG_R8, 0, 8);
            var successor = compiler.gtNewNothingNode();
            foreach (var node in new GenTree[] { destination, source, length, call, successor })
            {
                block.InsertAtEnd(node);
            }

            var next = LowerCall(lowering, call);

            if (size == 0)
            {
                Assert.That(next, Is.Null);
                Assert.That(call.Next, Is.SameAs(successor));
                Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x5678));
                Assert.That(destinationArg.Node.Oper, Is.EqualTo(genTreeOps.GT_PUTARG_REG));
                Assert.That(sourceArg.Node.Oper, Is.EqualTo(genTreeOps.GT_PUTARG_REG));
                Assert.That(lengthArg.Node.Oper, Is.EqualTo(genTreeOps.GT_PUTARG_REG));
            }
            else
            {
                var store = successor.Prev?.AsBlk() ?? throw new InvalidOperationException();
                Assert.That(store.Layout.Size, Is.EqualTo(size));
                Assert.That(next, Is.SameAs(helper is CorInfoHelpFunc.CORINFO_HELP_MEMSET ? store : successor));
                Assert.That(call.Next, Is.Null);
                Assert.That(call.Prev, Is.Null);
                Assert.That(destinationArg.Node, Is.SameAs(destination));
            }
        });
    }

    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMZERO, 0x80000000u, false)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMZERO, 0xFFFFFFF0u, true)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMSET, 0x80000000u, false)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_MEMCPY, 0xFFFFFFF0u, false)]
    public static void LargeBlocksLowerToHelpersWithPositiveNativeWidthSizes(
        CorInfoHelpFunc helper, uint size, bool isVolatile)
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.fgNodeThreading = NodeThreading.LIR;
            var destination = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            block.InsertAtEnd(destination);
            GenTree data;
            if (helper is CorInfoHelpFunc.CORINFO_HELP_MEMCPY)
            {
                var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
                block.InsertAtEnd(address);
                data = compiler.gtNewIndir(TYP_STRUCT, address);
            }
            else
            {
                data = compiler.gtNewIconNode(TYP_INT, helper is CorInfoHelpFunc.CORINFO_HELP_MEMZERO ? 0 : 17);
                if (helper is CorInfoHelpFunc.CORINFO_HELP_MEMSET)
                {
                    block.InsertAtEnd(data);
                    data = new GenTreeUnOp(genTreeOps.GT_INIT_VAL, TYP_INT, data);
                }
            }
            block.InsertAtEnd(data);
            var store = new GenTreeBlk(TYP_STRUCT, destination, data, new ClassLayout(size));
            if (isVolatile)
            {
                store.Flags |= GenTreeFlags.GTF_IND_VOLATILE;
            }
            var successor = compiler.gtNewNothingNode();
            block.InsertAtEnd(store);
            block.InsertAtEnd(successor);

            Assert.That(LowerNode(lowering, store), Is.SameAs(successor));

            GenTreeCall? call = null;
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                if (node.Oper is genTreeOps.GT_CALL)
                {
                    Assert.That(call, Is.Null);
                    call = node.AsCall();
                }
            }
            Assert.That(call, Is.Not.Null);
            Assert.That(call!.IsHelperCall(helper), Is.True);
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x5678));
            var sizeIndex = helper is CorInfoHelpFunc.CORINFO_HELP_MEMZERO ? 1 : 2;
            var sizeArg = call.Args.GetUserArgByIndex(sizeIndex)?.Node ??
                throw new InvalidOperationException("Missing block helper size argument.");
            Assert.That(sizeArg.Oper, Is.EqualTo(genTreeOps.GT_PUTARG_REG));
            Assert.That(sizeArg.AsUnOp().Op1.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(sizeArg.AsUnOp().Op1.AsIntCon().IconValue, Is.EqualTo((nint)size));
            Assert.That(store.Oper, Is.EqualTo(genTreeOps.GT_NOP));
            Assert.That(store.Next, Is.SameAs(successor));
            if (isVolatile)
            {
                Assert.That(call.Prev!.Oper, Is.EqualTo(genTreeOps.GT_MEMORYBARRIER));
                Assert.That(call.Next!.Oper, Is.EqualTo(genTreeOps.GT_MEMORYBARRIER));
                Assert.That(call.Prev.Flags & GenTreeFlags.GTF_MEMORYBARRIER_STORE,
                    Is.EqualTo(GenTreeFlags.GTF_MEMORYBARRIER_STORE));
                Assert.That(call.Next.Flags & GenTreeFlags.GTF_MEMORYBARRIER_LOAD,
                    Is.EqualTo(GenTreeFlags.GTF_MEMORYBARRIER_LOAD));
            }
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(128, CorInfoHelpFunc.CORINFO_HELP_BULK_WRITEBARRIER_SMALL)]
    [TestCase(136, CorInfoHelpFunc.CORINFO_HELP_BULK_WRITEBARRIER)]
    public static void GcCopiesSelectTheBoundedHelperAndCheckBothAddresses(
        int size, CorInfoHelpFunc helper)
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.lvaTable[0].Type = TYP_BYREF;
            var layoutBuilder = new ClassLayoutBuilder(compiler, size);
            layoutBuilder.SetGCPtrType(0, TYP_REF);
            layoutBuilder.SetGCPtrType(1, TYP_REF);
            var destination = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var sourceAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var source = compiler.gtNewIndir(TYP_STRUCT, sourceAddress);
            var store = new GenTreeBlk(TYP_STRUCT, destination, source, ClassLayout.Create(compiler, layoutBuilder));
            block.InsertAtEnd(destination);
            block.InsertAtEnd(sourceAddress);
            block.InsertAtEnd(source);
            block.InsertAtEnd(store);

            _ = LowerNode(lowering, store);

            var nullchecks = 0;
            var sawPutArg = false;
            GenTreeCall? call = null;
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                if (node.Oper is genTreeOps.GT_NULLCHECK)
                {
                    Assert.That(sawPutArg, Is.False);
                    nullchecks++;
                }
                else if (node.Oper is genTreeOps.GT_PUTARG_REG)
                {
                    sawPutArg = true;
                }
                else if (node.Oper is genTreeOps.GT_CALL)
                {
                    Assert.That(call, Is.Null);
                    call = node.AsCall();
                }
            }
            Assert.That(nullchecks, Is.EqualTo(2));
            Assert.That(call, Is.Not.Null);
            Assert.That(call!.IsHelperCall(helper), Is.True);
            Assert.That(store.Oper, Is.EqualTo(genTreeOps.GT_NOP));
            Assert.That(source.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void VolatileGcCopiesDecomposeReferencesAndContiguousScalarRuns()
    {
        WithCompiler((compiler, block, lowering) => {
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.lvaTable[0].Type = TYP_BYREF;
            var layoutBuilder = new ClassLayoutBuilder(compiler, 32);
            layoutBuilder.SetGCPtrType(0, TYP_REF);
            layoutBuilder.SetGCPtrType(3, TYP_REF);
            var destination = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var sourceAddress = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var source = compiler.gtNewIndir(TYP_STRUCT, sourceAddress);
            source.Flags |= GenTreeFlags.GTF_IND_VOLATILE;
            var store = new GenTreeBlk(TYP_STRUCT, destination, source, ClassLayout.Create(compiler, layoutBuilder));
            store.Flags |= GenTreeFlags.GTF_IND_VOLATILE;
            block.InsertAtEnd(destination);
            block.InsertAtEnd(sourceAddress);
            block.InsertAtEnd(source);
            block.InsertAtEnd(store);

            _ = LowerNode(lowering, store);

            var referenceStores = 0;
            var scalarRuns = 0;
            for (var node = block.FirstNode; node is not null; node = node.Next)
            {
                Assert.That(node.Oper, Is.Not.EqualTo(genTreeOps.GT_CALL));
                if (node.Oper is genTreeOps.GT_STOREIND)
                {
                    referenceStores++;
                    Assert.That(node.Type, Is.EqualTo(TYP_REF));
                    Assert.That(node.AsIndir().IsVolatile, Is.True);
                    Assert.That(node.AsIndir().Data.AsIndir().IsVolatile, Is.True);
                }
                else if (node.Oper is genTreeOps.GT_STORE_BLK)
                {
                    scalarRuns++;
                    Assert.That(node.AsBlk().Size, Is.EqualTo(16u));
                    Assert.That(node.AsBlk().ContainsReferences, Is.False);
                    Assert.That(node.AsBlk().IsVolatile, Is.True);
                }
            }
            Assert.That(referenceStores, Is.EqualTo(2));
            Assert.That(scalarRuns, Is.EqualTo(1));
            Assert.That(store.Oper, Is.EqualTo(genTreeOps.GT_NOP));
            Assert.That(source.Next, Is.Null);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_outgoingArgSpaceSize")]
    private static extern ref int OutgoingArgSpaceSize(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerNode")]
    private static extern GenTree? LowerNode(Lowering lowering, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerCall")]
    private static extern GenTree? LowerCall(Lowering lowering, GenTree call);

    private static void WithCompiler(Action<Compiler, BasicBlock, Lowering> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaCount = 1;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        vtable.Base.getHelperFtn = &GetHelperFtn;
        vtable.Base.getFunctionEntryPoint = &GetFunctionEntryPoint;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        compiler.info.compCompHnd = &ee;
        compiler.info.compMatchedVM = true;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, block, lowering);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = new CORINFO_EE_INFO {
            targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI,
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x5678;
        return lookup->addr;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetFunctionEntryPoint(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_ACCESS_FLAGS flags)
    {
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x5678;
    }
}
