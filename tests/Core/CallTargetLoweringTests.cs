// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Reflection;
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
internal static unsafe class CallTargetLoweringTests
{
    [Test]
    public static void MaterializingAUseRechecksOnlyTheInsertedRange()
    {
        WithCompiler((compiler, block, lowering, unused) => {
            compiler.lvaTable = [new LclVarDsc()];
            compiler.lvaCount = 1;
            compiler.lvaTable[0].Type = TYP_INT;
            var definition = compiler.gtNewIconNode(TYP_INT, 11);
            var interveningValue = compiler.gtNewLclvNode(TYP_INT, 0);
            var interveningConstant = compiler.gtNewIconNode(TYP_INT, 5);
            var interveningAdd = new GenTreeOp(GT_ADD, TYP_INT, interveningValue, interveningConstant);
            var keepAlive = new GenTreeUnOp(GT_KEEPALIVE, TYP_VOID, interveningAdd);
            var other = compiler.gtNewLclvNode(TYP_INT, 0);
            var owner = new GenTreeOp(GT_ADD, TYP_INT, definition, other);
            GenTree[] nodes = [definition, interveningValue, interveningConstant, interveningAdd, keepAlive, other, owner];
            foreach (var node in nodes)
            {
                block.InsertAtEnd(node);
            }
            var use = new LIR.Use(block, ref owner.Op1Ref, owner);

            var replacement = ReplaceWithLclVar(lowering, use);

            Assert.That(owner.Op1, Is.SameAs(replacement));
            Assert.That(definition.Next?.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(definition.IsContained, Is.True);
            Assert.That(replacement.Next, Is.SameAs(interveningValue));
            Assert.That(interveningConstant.IsContained, Is.False);
            Assert.That(interveningAdd.Op2, Is.SameAs(interveningConstant));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReplaceWithLclVar")]
    private static extern GenTreeLclVar ReplaceWithLclVar(
        Lowering lowering, LIR.Use use, int localNumber = BAD_VAR_NUM);

    [Test]
    public static void IndirectVirtualStubCallReplacesTheOwnedControlExpression()
    {
        WithCompiler((compiler, block, lowering, _) => {
            var address = new GenTreeLclVar(TYP_I_IMPL, 0);
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_INDIRECT,
                _controlExpr = address,
                Flags = GTF_CALL_VIRT_STUB,
            };
            block.InsertAtEnd(address);
            block.InsertAtEnd(call);

            var result = Invoke(lowering, "LowerVirtualStubCall", call);
            var indirect = call._controlExpr;

            Assert.That(result, Is.Null);
            Assert.That(indirect, Is.TypeOf<GenTreeIndir>());
            Assert.That(indirect?.AsIndir().Addr, Is.SameAs(address));
            Assert.That(indirect?.Flags & (GTF_IND_NONFAULTING | GTF_IND_REQ_ADDR_IN_REG),
                Is.EqualTo(GTF_IND_NONFAULTING | GTF_IND_REQ_ADDR_IN_REG));
            Assert.That(address.Next, Is.SameAs(indirect));
            Assert.That(indirect?.Next, Is.SameAs(call));
        });
    }

    [Test]
    public static void DirectVirtualStubCallKeepsTargetInHiddenArgumentOnAmd64()
    {
        WithCompiler((compiler, block, lowering, _) => {
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_USER_FUNC,
                _callMoreFlags = GTF_CALL_M_VIRTSTUB_REL_INDIRECT,
                Flags = GTF_CALL_VIRT_STUB,
                StubCallStubAddr = (void*)0x1234,
            };
            block.InsertAtEnd(call);

            Assert.That(Invoke(lowering, "LowerVirtualStubCall", call), Is.Null);
            Assert.That(call.IsVirtualStub, Is.True);
            Assert.That(call._controlExpr, Is.Null);
        });
    }

    [Test]
    public static void DispatchHelperConvertsIndirectStubCallWithoutDroppingTheCellArgument()
    {
        WithCompiler((compiler, block, lowering, _) => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_DISPATCH_HELPERS);
            compiler.info.compMatchedVM = true;
            var control = new GenTreeLclVar(TYP_I_IMPL, 0);
            var cell = new GenTreeLclVar(TYP_I_IMPL, 0);
            var putCell = new GenTreeUnOp(GT_PUTARG_REG, TYP_I_IMPL, cell);
            var call = new GenTreeCall(TYP_VOID) {
                _callType = CT_INDIRECT,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x1234,
                _controlExpr = control,
                _callMoreFlags = GTF_CALL_M_VIRTSTUB_REL_INDIRECT,
                Flags = GTF_CALL_VIRT_STUB,
            };
            var cellArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(cell)
                .WithWellKnownArg(WellKnownArg.VirtualStubCell));
            cellArg.EarlyNode = null;
            cellArg.LateNode = putCell;
            block.InsertAtEnd(control);
            block.InsertAtEnd(cell);
            block.InsertAtEnd(putCell);
            block.InsertAtEnd(call);

            Assert.That(Invoke(lowering, "LowerVirtualStubCall", call), Is.Null);
            Assert.That(control.IsUnusedValue, Is.True);
            Assert.That(call._controlExpr, Is.Null);
            Assert.That(call._callType, Is.EqualTo(CT_USER_FUNC));
            Assert.That((nint)call._callMethHnd, Is.EqualTo((nint)0));
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x5678));
            Assert.That(call.Args.FindWellKnownArg(WellKnownArg.VirtualStubCell)?.Node, Is.SameAs(putCell));
            Assert.That(putCell.Op1, Is.SameAs(cell));
            Assert.That(call.IsVirtualStub, Is.False);
            Assert.That(call.IsVirtualStubRelativeIndir, Is.False);
        });
    }

    [TestCase(Globals.CORINFO_VIRTUALCALL_NO_CHUNK, 24)]
    [TestCase(32, 8)]
    public static void VirtualVtableCallBuildsTheMetadataSpecifiedIndirections(int chunk, int slot)
    {
        WithCompiler((compiler, block, lowering, ee) => {
            ee->ChunkOffset = chunk;
            ee->SlotOffset = slot;

            var (call, thisValue, putArg) = NewInstanceCall();
            block.InsertAtEnd(thisValue);
            block.InsertAtEnd(putArg);
            block.InsertAtEnd(call);

            var target = Invoke(lowering, "LowerVirtualVtableCall", call);
            Assert.That(target?.Oper, Is.EqualTo(GT_IND));
            var targetIndir = (target ?? throw new InvalidOperationException()).AsIndir();
            Assert.That(targetIndir.Addr.AsAddrMode().Offset, Is.EqualTo(slot));
            var vtable = targetIndir.Addr.AsAddrMode().BaseAddress ?? throw new InvalidOperationException();
            if (chunk != Globals.CORINFO_VIRTUALCALL_NO_CHUNK)
            {
                Assert.That(vtable.Oper, Is.EqualTo(GT_IND));
                Assert.That(vtable.AsIndir().Addr.AsAddrMode().Offset, Is.EqualTo(chunk));
                vtable = vtable.AsIndir().Addr.AsAddrMode().BaseAddress ?? throw new InvalidOperationException();
            }
            Assert.That(vtable.Oper, Is.EqualTo(GT_IND));
            var receiver = vtable.AsIndir().Addr.AsAddrMode().BaseAddress ?? throw new InvalidOperationException();
            Assert.That(receiver.AsLclVar().LclNum, Is.EqualTo(0));
            Assert.That(call.Args.ThisArg?.Node, Is.SameAs(putArg));
        });
    }

    [Test]
    public static void RelativeVirtualVtableCallMaterializesBothDependentOffsets()
    {
        WithCompiler((compiler, block, lowering, ee) => {
            compiler.lvaTable = [new LclVarDsc()];
            compiler.lvaCount = 1;
            ee->ChunkOffset = 32;
            ee->SlotOffset = 8;
            ee->Relative = true;

            var (call, thisValue, putArg) = NewInstanceCall();
            block.InsertAtEnd(thisValue);
            block.InsertAtEnd(putArg);
            block.InsertAtEnd(call);

            var target = Invoke(lowering, "LowerVirtualVtableCall", call);
            Assert.That(target?.Oper, Is.EqualTo(GT_ADD));
            var finalAdd = target?.AsOp() ?? throw new InvalidOperationException();
            Assert.That(finalAdd.Op1.Oper, Is.EqualTo(GT_IND));
            Assert.That(finalAdd.Op2.Oper, Is.EqualTo(GT_LCL_VAR));
            var secondTemp = finalAdd.Op2.AsLclVar().LclNum;
            Assert.That(finalAdd.Op1.AsIndir().Addr.AsLclVar().LclNum, Is.EqualTo(secondTemp));

            var stores = 0;
            for (var node = putArg.Next; node is not null && node != call; node = node.Next)
            {
                if (node.Oper is GT_STORE_LCL_VAR)
                {
                    stores++;
                }
            }
            Assert.That(stores, Is.EqualTo(2));
            var secondStore = call.Prev;
            Assert.That(secondStore?.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(secondStore?.AsLclVar().LclNum, Is.EqualTo(secondTemp));
            var address = secondStore?.AsLclVar().Data?.AsAddrMode();
            Assert.That(address?.Index?.AsIndir().Addr.AsAddrMode().Offset, Is.EqualTo(32));
            Assert.That(address?.BaseAddress?.AsOp().Op2.AsIntCon().IconVal, Is.EqualTo((nint)40));
        });
    }

    [Test]
    public static void DelegateInvokeMovesInstanceLoadAfterArgumentEvaluation()
    {
        WithCompiler((compiler, block, lowering, _) => {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.offsetOfDelegateInstance = 16;
            compiler.eeInfo.offsetOfDelegateFirstTarget = 24;

            var (call, thisValue, putArg) = NewInstanceCall();
            var otherArg = new GenTreeIntCon(TYP_INT, 3);
            block.InsertAtEnd(thisValue);
            block.InsertAtEnd(putArg);
            block.InsertAtEnd(otherArg);
            block.InsertAtEnd(call);

            var target = Invoke(lowering, "LowerDelegateInvoke", call);
            Assert.That(target?.Oper, Is.EqualTo(GT_IND));
            Assert.That(target?.AsIndir().Addr.AsAddrMode().Offset, Is.EqualTo(24));
            Assert.That(target?.AsIndir().Addr.AsAddrMode().BaseAddress, Is.Not.SameAs(thisValue));

            var newThis = putArg.Op1;
            Assert.That(newThis.Oper, Is.EqualTo(GT_IND));
            Assert.That(newThis.AsIndir().Addr.AsAddrMode().Offset, Is.EqualTo(16));
            Assert.That(otherArg.Next, Is.SameAs(newThis.AsIndir().Addr));
            Assert.That(newThis.Next, Is.SameAs(putArg));
            Assert.That(putArg.Next, Is.SameAs(call));
        });
    }

    private static (GenTreeCall Call, GenTreeLclVar ThisValue, GenTreeUnOp PutArg) NewInstanceCall()
    {
        var thisValue = new GenTreeLclVar(TYP_REF, 0);
        var putArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_REF, thisValue);
        var call = new GenTreeCall(TYP_VOID) {
            _callType = CT_USER_FUNC,
            _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x1234,
        };
        var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(thisValue)
            .WithWellKnownArg(WellKnownArg.ThisPointer));
        arg.EarlyNode = null;
        arg.LateNode = putArg;
        return (call, thisValue, putArg);
    }

    private static GenTree? Invoke(Lowering lowering, string name, GenTreeCall call)
    {
        var method = typeof(Lowering).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        return (GenTree?)method.Invoke(lowering, [call]);
    }

    private delegate void LoweringAction(Compiler compiler, BasicBlock block, Lowering lowering, TargetEE* ee);

    private static void WithCompiler(LoweringAction action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.fgNodeThreading = NodeThreading.LIR;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getMethodVTableOffset = &GetVtableOffset;
        vtable.Base.Base.getMethodAttribs = &GetMethodAttribs;
        vtable.Base.getHelperFtn = &GetHelperFtn;
        TargetEE ee = new() { Interface = new ICorJitInfo { lpVtbl = &vtable } };
        compiler.info.compCompHnd = &ee.Interface;
        JitTls.Compiler = compiler;
        try
        {
            compiler.codeGen = new CodeGen(compiler);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            var blockField = typeof(Lowering).GetField("_block", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException();
            blockField.SetValue(lowering, block);
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
        public int ChunkOffset;
        public int SlotOffset;
        public bool Relative;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetVtableOffset(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method,
        int* chunk, int* slot, bool* relative)
    {
        var ee = (TargetEE*)self;
        *chunk = ee->ChunkOffset;
        *slot = ee->SlotOffset;
        *relative = ee->Relative;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetMethodAttribs(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method)
    {
        return CorInfoFlag.CORINFO_FLG_DELEGATE_INVOKE | CorInfoFlag.CORINFO_FLG_FINAL;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x5678;
        return lookup->addr;
    }
}
