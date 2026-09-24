// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AllocationLoweringTests
{
    [Test]
    public static void HelperReplacementPreservesIdentityAndCommonStateWithoutFactorySideEffects()
    {
        WithCompiler(compiler => {
            var left = compiler.gtNewLconNode(3);
            var right = compiler.gtNewLconNode(7);
            right.Flags |= GTF_ORDER_SIDEEFF;
            var source = compiler.gtNewBinaryNode(GT_MUL, TYP_LONG, left, right);
            source.Flags = GTF_ASG | GTF_DONT_CSE | GTF_COLON_COND | GTF_EXCEPT | GTF_MUL_64RSLT;
            source._vnPair.SetBoth(42);
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif
            var call = compiler.gtNewHelperCallNode(source, CORINFO_HELP_LMUL, left, right);

            Assert.That(call, Is.Not.SameAs(source));
            Assert.That(call.HelperNum, Is.EqualTo(CORINFO_HELP_LMUL));
            Assert.That(call.Type, Is.EqualTo(TYP_LONG));
            Assert.That(call._returnType, Is.EqualTo(TYP_LONG));
            Assert.That(call._vnPair, Is.EqualTo(source._vnPair));
            Assert.That(call.Flags, Is.EqualTo(GTF_ASG | GTF_DONT_CSE | GTF_COLON_COND | GTF_CALL | GTF_ORDER_SIDEEFF));
            Assert.That(call.Args.CountUserArgs(), Is.EqualTo(2));
            Assert.That(call.Args.GetUserArgByIndex(0)?.Node, Is.SameAs(left));
            Assert.That(call.Args.GetUserArgByIndex(1)?.Node, Is.SameAs(right));
            Assert.That(source.Oper, Is.EqualTo(GT_MUL));
#if DEBUG
            Assert.That(call.TreeId, Is.EqualTo(source.TreeId));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId));
            Assert.That(call._inlineObservation, Is.EqualTo(InlineObservation.CALLSITE_IS_CALL_TO_HELPER));
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ObjectHelperPreservesHandleAndAllocationEffects(bool sideEffects)
    {
        WithCompiler(compiler => {
            var handle = compiler.gtNewIconNode(TYP_I_IMPL, 123);
            handle.Flags |= GTF_ORDER_SIDEEFF;
            var allocation = new GenTreeAllocObj(TYP_REF, handle, CORINFO_HELP_NEWSFAST, sideEffects,
                (CORINFO_CLASS_STRUCT_*)123);
            allocation.Flags |= GTF_EXCEPT;
            allocation._vnPair.SetBoth(43);
            var call = new ObjectAllocator(compiler).MorphAllocObjNodeIntoHelperCall(allocation);

            Assert.That(call.HelperNum, Is.EqualTo(CORINFO_HELP_NEWSFAST));
            Assert.That(call.Args.CountUserArgs(), Is.EqualTo(1));
            Assert.That(call.Args.GetUserArgByIndex(0)?.Node, Is.SameAs(handle));
            Assert.That(call.Flags, Is.EqualTo(GTF_CALL | GTF_ORDER_SIDEEFF));
            Assert.That(call._callMoreFlags.HasFlag(GTF_CALL_M_ALLOC_SIDE_EFFECTS), Is.EqualTo(sideEffects));
            Assert.That(call._vnPair, Is.EqualTo(allocation._vnPair));
            Assert.That(allocation.Oper, Is.EqualTo(GT_ALLOCOBJ));
        });
    }

    [TestCase(CORINFO_HELP_READYTORUN_NEW, 0)]
    [TestCase(CORINFO_HELP_NEWSFAST, 1)]
    public static void ReadyToRunHelperPreservesEntryPoint(CorInfoHelpFunc helper, int argumentCount)
    {
        WithCompiler(compiler => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            var handle = compiler.gtNewIconNode(TYP_I_IMPL, 123);
            var allocation = new GenTreeAllocObj(TYP_REF, handle, helper, true, (CORINFO_CLASS_STRUCT_*)123) {
                EntryPoint = new CORINFO_CONST_LOOKUP {
                    accessType = InfoAccessType.IAT_PVALUE,
                    addr = (void*)456,
                },
            };
            var call = new ObjectAllocator(compiler).MorphAllocObjNodeIntoHelperCall(allocation);

            Assert.That(call.Args.CountUserArgs(), Is.EqualTo(argumentCount));
            Assert.That((nint)call._entryPoint.addr, Is.EqualTo((nint)456));
            Assert.That(call._entryPoint.accessType, Is.EqualTo(InfoAccessType.IAT_PVALUE));
            Assert.That(call._callMoreFlags & GTF_CALL_M_ALLOC_SIDE_EFFECTS, Is.EqualTo(GTF_CALL_M_ALLOC_SIDE_EFFECTS));
        });
    }

    [Test]
    public static void HeapTraversalReplacesTheOwningStoreAndPropagatesCallEffects()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = new LclVarDsc[1];
            compiler.lvaCount = 1;
            compiler.lvaTable[0].Type = TYP_REF;
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            block.SetFlags(BasicBlockFlags.BBF_HAS_NEWOBJ);
            compiler.fgFirstBB = block;
            compiler.fgLastBB = block;
            var handle = compiler.gtNewIconNode(TYP_I_IMPL, 123);
            var allocation = new GenTreeAllocObj(TYP_REF, handle, CORINFO_HELP_NEWSFAST, true,
                (CORINFO_CLASS_STRUCT_*)123);
            var store = compiler.gtNewStoreLclVarNode(0, allocation);
            compiler.fgInsertStmtAtEnd(block, compiler.gtNewStmt(store));

            new ObjectAllocator(compiler).MorphHeapAllocObjNodes();

            Assert.That(store.Data.Oper, Is.EqualTo(GT_CALL));
            Assert.That(store.Data.AsCall().HelperNum, Is.EqualTo(CORINFO_HELP_NEWSFAST));
            Assert.That(store.Flags & GTF_CALL, Is.EqualTo(GTF_CALL));
            Assert.That(store.Data, Is.Not.SameAs(allocation));
            Assert.That(allocation.Oper, Is.EqualTo(GT_ALLOCOBJ));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.isValueClass = &False;
        vtable.Base.Base.canAllocateOnStack = &False;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.eeInfoInitialized = true;
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte False(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* clsHnd) => 0;
}
