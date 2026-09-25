// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class BlockTraversalLoweringTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void EmptyBlocksNeedNotAlreadyBeMarkedLir(bool lir)
    {
        WithCompiler(true, (compiler, lowering, ee) => {
            var block = new BasicBlock(null, null);
            if (lir)
            {
                block.MakeLir(null, null);
            }
            compiler.compCurBB = block;

            LowerBlock(lowering, block);

            Assert.That(CurrentBlock(lowering), Is.SameAs(block));
            Assert.That(block.IsEmpty, Is.True);
            Assert.That(block.IsLIR, Is.EqualTo(lir));
        });
    }

    [Test]
    public static void RemovedCurrentNodeDoesNotEndTraversalBeforeItsReturnedSuccessor()
    {
        WithCompiler(false, (compiler, lowering, ee) => {
            var bits = compiler.gtNewIconNode(TYP_INT, 0x3f800000);
            var cast = new GenTreeUnOp(GT_BITCAST, TYP_FLOAT, bits) { IsUnusedValue = true };
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var increment = compiler.gtNewIconNode(TYP_INT, 1);
            var atomic = new GenTreeOp(GT_XADD, TYP_INT, address, increment) { IsUnusedValue = true };
            var block = NewBlock(bits, cast, address, increment, atomic);
            compiler.compCurBB = block;

            LowerBlock(lowering, block);

            Assert.That(CurrentBlock(lowering), Is.SameAs(block));
            Assert.That(block.FirstNode?.Oper, Is.EqualTo(GT_CNS_DBL));
            Assert.That(block.FirstNode?.AsDblCon().DconVal, Is.EqualTo(1.0));
            Assert.That(block.FirstNode?.IsUnusedValue, Is.True);
            Assert.That(cast.Prev is null && cast.Next is null, Is.True);
            Assert.That(bits.Prev is null && bits.Next is null, Is.True);
            Assert.That(atomic.Oper, Is.EqualTo(GT_LOCKADD));
            Assert.That(atomic.Type, Is.EqualTo(TYP_VOID));
            Assert.That(increment.IsContained, Is.True);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [Test]
    public static void EachBlockBecomesCurrentAndLoweringRevisitsReplacementOwners()
    {
        WithCompiler(true, (compiler, lowering, ee) => {
            var left = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var right = compiler.gtNewIconNode(TYP_I_IMPL, 4);
            var condition = new GenTreeOp(GT_EQ, TYP_INT, left, right);
            var jump = new GenTreeUnOp(GT_JTRUE, TYP_VOID, condition);
            var first = NewBlock(left, right, condition, jump);
            compiler.compCurBB = first;

            LowerBlock(lowering, first);

            Assert.That(first.LastNode?.Oper, Is.EqualTo(GT_JCC));
            Assert.That(condition.Oper, Is.EqualTo(GT_CMP));
            Assert.That(condition.Type, Is.EqualTo(TYP_VOID));
            Assert.That(jump.Prev is null && jump.Next is null, Is.True);
            Assert.That(right.IsContained, Is.True);

            var value = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var keepAlive = new GenTreeUnOp(GT_KEEPALIVE, TYP_VOID, value);
            var second = NewBlock(value, keepAlive);
            compiler.compCurBB = second;
            LowerBlock(lowering, second);

            Assert.That(CurrentBlock(lowering), Is.SameAs(second));
            Assert.That(value.IsRegOptional, Is.True);
            Assert.That(second.LastNode, Is.SameAs(keepAlive));
#if DEBUG
            Assert.That(first.CheckLir(compiler, checkUnusedValues: true), Is.True);
            Assert.That(second.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InitializationWrappersReachTheirStoreAndTraversalContinues(bool minOpts)
    {
        WithCompiler(minOpts, (compiler, lowering, ee) => {
            var destination = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var fill = compiler.gtNewIconNode(TYP_INT, 17);
            var init = new GenTreeUnOp(GT_INIT_VAL, TYP_INT, fill);
            var store = new GenTreeBlk(TYP_STRUCT, destination, init, new ClassLayout(8));
            var address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var increment = compiler.gtNewIconNode(TYP_INT, 1);
            var atomic = new GenTreeOp(GT_XADD, TYP_INT, address, increment) { IsUnusedValue = true };
            var block = NewBlock(destination, fill, init, store, address, increment, atomic);
            compiler.compCurBB = block;

            LowerBlock(lowering, block);

            Assert.That(address.Prev!.Oper, Is.EqualTo(minOpts ? GT_STORE_BLK : GT_STOREIND));
            Assert.That(atomic.Oper, Is.EqualTo(GT_LOCKADD));
            Assert.That(increment.IsContained, Is.True);
#if DEBUG
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    public static void MethodJumpPopsOnlyRequiredInlinedStubFrames(bool requiresFrame, bool helpers, bool stub)
    {
        WithCompiler(true, (compiler, lowering, ee) => {
            compiler.info.compUnmanagedCallCountWithGCTransition = requiresFrame ? 1 : 0;
            if (helpers)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS);
            }
            if (stub)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_IL_STUB);
            }
            var marker = new GenTree(GT_NO_OP, TYP_VOID);
            var jump = new GenTreeVal(GT_JMP, TYP_VOID, 0x1234);
            var block = NewBlock(marker, jump);
            block.Kind = BBJ_RETURN;
            compiler.compCurBB = block;
            CurrentBlock(lowering) = block;

            LowerJmpMethod(lowering, jump);

            Assert.That(block.LastNode, Is.SameAs(jump));
            Assert.That(jump.Val1, Is.EqualTo((nint)0x1234));
            var insertsEpilog = requiresFrame && !helpers && stub;
            Assert.That(ee->InfoLookups, Is.EqualTo(insertsEpilog ? 1 : 0));
            if (insertsEpilog)
            {
                var pop = jump.Prev?.AsStoreInd() ?? throw new AssertionException("Missing frame unlink.");
                Assert.That(pop.Oper, Is.EqualTo(GT_STOREIND));
                Assert.That(pop.Addr.AsAddrMode().Offset, Is.EqualTo(16));
                Assert.That(pop.Addr.IsContained, Is.True);
                Assert.That(pop.Data.AsLclFld().LclNum, Is.EqualTo(2));
                Assert.That(pop.Data.AsLclFld().LclOffs, Is.EqualTo(8));
                Assert.That(marker.Next, Is.Not.SameAs(jump));
            }
            else
            {
                Assert.That(marker.Next, Is.SameAs(jump));
            }
#if DEBUG
            Assert.That(CheckBlock(null, compiler, block), Is.True);
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
#endif
        });
    }

#if DEBUG
    [Test]
    public static void CallValidationVisitsEarlyThenLateArgumentsAndChecksFieldListElements()
    {
        WithCompiler(true, (compiler, lowering, ee) => {
            var value = compiler.gtNewIconNode(TYP_INT, 1);
            var fields = new GenTreeFieldList { IsContained = false };
            fields.AddFieldLIR(compiler, value, 0, TYP_INT);
            var call = new GenTreeCall(TYP_VOID);
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            arg.EarlyNode = new GenTree(GT_NO_OP, TYP_VOID);
            arg.LateNode = fields;
            call.Args.PushLateBack(arg);
            var block = NewBlock(arg.EarlyNode, value, fields, call);

            var failures = CollectAssertions(() => CheckBlock(null, compiler, block));

            Assert.That(failures, Has.Length.EqualTo(3));
            Assert.That(failures[0], Is.EqualTo("arg.Oper.IsStore"));
            Assert.That(failures[1], Is.EqualTo("list.IsContained"));
            Assert.That(failures[2], Is.EqualTo("use.Node.Oper.IsPutArg"));
        });
    }

    [Test]
    public static void ValidationAcceptsEarlyStoresAndOrderedLateRegisterFields()
    {
        WithCompiler(true, (compiler, lowering, ee) => {
            var firstValue = compiler.gtNewIconNode(TYP_INT, 1);
            var earlyStore = compiler.gtNewStoreLclVarNode(3, firstValue);
            var firstRead = compiler.gtNewLclvNode(TYP_INT, 3);
            var firstArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, firstRead);
            var secondValue = compiler.gtNewIconNode(TYP_INT, 2);
            var secondArg = new GenTreeUnOp(GT_PUTARG_REG, TYP_INT, secondValue);
            var fields = new GenTreeFieldList { IsContained = true };
            fields.AddFieldLIR(compiler, firstArg, 0, TYP_INT);
            fields.AddFieldLIR(compiler, secondArg, 4, TYP_INT);
            var call = new GenTreeCall(TYP_VOID);
            var arg = call.Args.PushBack(NewCallArg.CreateForPrimitive(firstValue));
            arg.EarlyNode = null;
            arg.LateNode = fields;
            call.Args.PushLateBack(arg);
            var block = NewBlock(firstValue, earlyStore, firstRead, firstArg, secondValue, secondArg, fields, call);

            Assert.That(CollectAssertions(() => {
                CheckCallArg(null, earlyStore);
                return true;
            }), Is.Empty);
            Assert.That(CollectAssertions(() => CheckBlock(null, compiler, block)), Is.Empty);
            Assert.That(block.CheckLir(compiler, checkUnusedValues: true), Is.True);
            Assert.That(fields.Uses.Head?.Node, Is.SameAs(firstArg));
            Assert.That(firstArg.Next, Is.SameAs(secondValue));
            Assert.That(secondArg.Next, Is.SameAs(fields));
        });
    }

    [TestCase(GT_LCL_VAR)]
    [TestCase(GT_LCL_FLD)]
    [TestCase(GT_LCL_ADDR)]
    public static void StackAndPromotedLocalChecksRequireNativeEnregistrationState(genTreeOps oper)
    {
        WithCompiler(true, (compiler, lowering, ee) => {
            GenTree node = oper switch {
                GT_LCL_VAR => compiler.gtNewLclvNode(TYP_I_IMPL, 0),
                GT_LCL_FLD => compiler.gtNewLclFldNode(TYP_INT, 0, 0),
                _ => compiler.gtNewLclVarAddrNode(TYP_BYREF, 0),
            };
            compiler.lvaTable[0].lvPromoted = oper is GT_LCL_VAR;
            var block = NewBlock(node);
            Assert.That(CollectAssertions(() => CheckBlock(null, compiler, block)).Length, Is.EqualTo(1));

            compiler.lvaTable[0].lvDoNotEnregister = true;

            Assert.That(CollectAssertions(() => CheckBlock(null, compiler, block)), Is.Empty);
        });
    }

    [Test]
    public static void UncontainedTrackedScalarGcDefinitionIsRejected()
    {
        WithCompiler(true, (compiler, lowering, ee) => {
            compiler.lvaTable[0].Type = TYP_REF;
            compiler.lvaTable[0].lvTracked = true;
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var address = compiler.gtNewLclVarAddrNode(TYP_BYREF, 0);
            address.Flags |= GTF_VAR_DEF;
            var block = NewBlock(address);
            var failures = CollectAssertions(() => CheckBlock(null, compiler, block));
            Assert.That(failures.Length, Is.EqualTo(1));
            Assert.That(failures[0], Does.Contain("address.IsContained"));

            address.IsContained = true;

            Assert.That(CollectAssertions(() => CheckBlock(null, compiler, block)), Is.Empty);
        });
    }

    private static readonly List<string> s_assertions = [];

    private static string[] CollectAssertions(Func<bool> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.doAssert = &RecordAssertion;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();
        Assert.That(action(), Is.True);

        return [.. s_assertions];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression) ?? "");

        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "CheckBlock")]
    private static extern bool CheckBlock(Lowering? _, Compiler compiler, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "CheckCallArg")]
    private static extern void CheckCallArg(Lowering? _, GenTree arg);
#endif

    private static BasicBlock NewBlock(params GenTree[] nodes)
    {
        var block = new BasicBlock(null, null);
        block.MakeLir(null, null);
        foreach (var node in nodes)
        {
            block.InsertAtEnd(node);
        }

        return block;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerBlock")]
    private static extern void LowerBlock(Lowering lowering, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerJmpMethod")]
    private static extern void LowerJmpMethod(Lowering lowering, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? CurrentBlock(Lowering lowering);

    private delegate void LoweringAction(Compiler compiler, Lowering lowering, TargetEE* ee);

    private static void WithCompiler(bool minopts, LoweringAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        TargetEE ee = new() { Interface = new ICorJitInfo { lpVtbl = &vtable } };
#if DEBUG
        using var tls = new JitTls(&ee.Interface);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minopts);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.compRationalIRForm = true;
        compiler.info.compCompHnd = &ee.Interface;
        compiler.info.compMatchedVM = true;
        compiler.info.compLvFrameListRoot = 1;
        compiler.lvaInlinedPInvokeFrameVar = 2;
        compiler.lvaTable = [
            new LclVarDsc { Type = TYP_I_IMPL },
            new LclVarDsc { Type = TYP_I_IMPL },
            new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(64), lvDoNotEnregister = true },
            new LclVarDsc { Type = TYP_INT },
        ];
        compiler.lvaTable[2].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
        compiler.lvaCount = 4;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            action(compiler, new Lowering(compiler, new LinearScan(compiler)), &ee);
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
            },
        };
    }
}
