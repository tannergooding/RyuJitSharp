// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeCallFlags;
#if DEBUG
using static RyuJitSharp.GenTreeDebugFlags;
#endif
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.LsraGlobals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regMask;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class LinearScanRegisterWritebackTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void WindowsAmd64CallsHaveNoIndexedReturnEvenWithAsyncOrSwift(bool async, bool swift)
    {
        WithAllocator((compiler, _) => {
            var call = new GenTreeCall(TYP_STRUCT);
            if (async)
            {
                call._callMoreFlags |= GTF_CALL_M_ASYNC;
            }
            if (swift)
            {
                call.Flags |= GTF_CALL_UNMANAGED;
                call._unmgdCallConv = CorInfoCallConvExtension.Swift;
            }

            Assert.That(MAX_RET_REG_COUNT, Is.EqualTo(1));
            Assert.That(call.HasMultiRegRetVal, Is.False);
            Assert.That(call.IsMultiRegCall, Is.False);
            Assert.That(call.GetRegisterDstCount(compiler), Is.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void WriteRegistersSelectsTheIndexedResult(bool indexed)
    {
        WithAllocator((compiler, allocator) => {
            var source = compiler.gtNewIconNode(TYP_INT, 1);
            var node = new GenTreeCopyOrReload(GT_COPY, TYP_INT, source);
            var interval = new Interval(TYP_INT, SRBM_ALLINT_INIT);
            var reference = allocator.newRefPosition(interval, 2, RefType.RefTypeDef, node, SRBM_RBX);
            if (indexed)
            {
                node.RegNum = regNumber.REG_RAX;
                reference.setMultiRegIdx(1);
            }

            WriteRegisters(allocator, reference, node);

            Assert.That(node.GetRegNumByIdx(indexed ? (byte)1 : (byte)0), Is.EqualTo(regNumber.REG_RBX));
            if (indexed)
            {
                Assert.That(node.RegNum, Is.EqualTo(regNumber.REG_RAX));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void InsertCopyOrReloadReplacesOwningUseAndPreservesExecutionOrder(bool reload)
    {
        WithAllocator((compiler, allocator) => {
            var block = NewBlock(compiler);
            var source = compiler.gtNewIconNode(TYP_INT, 1);
            var intervening = compiler.gtNewIconNode(TYP_INT, 2);
            var parent = new GenTreeOp(GT_ADD, TYP_INT, source, intervening);
            block.InsertAtEnd(source);
            block.InsertAtEnd(intervening);
            block.InsertAtEnd(parent);

            var reference = NewReference(allocator, source, regNumber.REG_RBX);
            reference.reload = reload;
            InsertCopyOrReload(allocator, block, source, 0, reference);

            var copy = parent.Op1.AsCopyOrReload();
            Assert.That(copy.Oper, Is.EqualTo(reload ? GT_RELOAD : GT_COPY));
            Assert.That(copy.Op1, Is.SameAs(source));
            Assert.That(copy.RegNum, Is.EqualTo(regNumber.REG_RBX));
            Assert.That(source.Next, Is.SameAs(copy));
            Assert.That(copy.Next, Is.SameAs(intervening));
            Assert.That(intervening.Next, Is.SameAs(parent));
#if DEBUG
            Assert.That(allocator.getLsraStat(LsraStat.STAT_COPY_REG, checked((uint)block.bbNum)), Is.EqualTo(reload ? 0u : 1u));
            Assert.That((copy._debugFlags & GTF_DEBUG_NODE_LSRA_ADDED) != 0, Is.True);
#endif
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SecondMultiRegUseFillsExistingCopyOrReloadInsteadOfInsertingAnother(bool reload)
    {
        WithAllocator((compiler, allocator) => {
            var block = NewBlock(compiler);
            var original = compiler.gtNewIconNode(TYP_INT, 1);
            var source = new GenTreeCopyOrReload(GT_COPY, TYP_INT, original) {
                RegNum = regNumber.REG_RAX,
            };
            var firstCopy = new GenTreeCopyOrReload(reload ? GT_RELOAD : GT_COPY, TYP_INT, source) {
                RegNum = regNumber.REG_RAX,
            };
            var parent = new GenTreeOp(GT_ADD, TYP_INT, firstCopy, compiler.gtNewIconNode(TYP_INT, 2));
            block.InsertAtEnd(original);
            block.InsertAtEnd(source);
            block.InsertAtEnd(firstCopy);
            block.InsertAtEnd(parent.Op2);
            block.InsertAtEnd(parent);

            var reference = NewReference(allocator, source, regNumber.REG_RBX);
            reference.reload = reload;
            InsertCopyOrReload(allocator, block, source, 1, reference);

            Assert.That(firstCopy.GetRegNumByIdx(1), Is.EqualTo(regNumber.REG_RBX));
            Assert.That(firstCopy.Next, Is.SameAs(parent.Op2));
            Assert.That(parent.Op1, Is.SameAs(firstCopy));
        });
    }

    [Test]
    public static void SingleRegisterStructLocalCopyUsesItsPrimitiveRegisterType()
    {
        WithAllocator((compiler, allocator) => {
            var layout = ClassLayout.Create(compiler, new ClassLayoutBuilder(compiler, TARGET_POINTER_SIZE));
            compiler.lvaTable = [new LclVarDsc { Type = TYP_STRUCT, Layout = layout }];
            compiler.lvaCount = 1;
            var block = NewBlock(compiler);
            var source = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            var parent = new GenTreeUnOp(GT_RETURN, TYP_VOID, source);
            block.InsertAtEnd(source);
            block.InsertAtEnd(parent);
            var reference = NewReference(allocator, source, regNumber.REG_RBX);

            InsertCopyOrReload(allocator, block, source, 0, reference);

            Assert.That(parent.Op1.Oper, Is.EqualTo(GT_COPY));
            Assert.That(parent.Op1.Type, Is.EqualTo(layout.RegisterType));
            Assert.That(parent.Op1.Type, Is.Not.EqualTo(TYP_STRUCT));
        }, enregStructs: true);
    }

    [Test]
    public static void TemporaryLocalCopyMarksItsResultLastUse()
    {
        WithAllocator((compiler, allocator) => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, lvLRACandidate = true }];
            compiler.lvaCount = 1;
            var block = NewBlock(compiler);
            var source = compiler.gtNewLclvNode(TYP_INT, 0);
            var parent = compiler.gtNewStoreLclVarNode(0, source);
            block.InsertAtEnd(source);
            block.InsertAtEnd(parent);
            var reference = NewReference(allocator, source, regNumber.REG_RBX);
            reference.copyReg = true;

            InsertCopyOrReload(allocator, block, source, 0, reference);

            Assert.That(parent.Data.Oper, Is.EqualTo(GT_COPY));
            Assert.That(parent.Data.IsLastUse(0), Is.True);
            Assert.That(parent.Data.AsCopyOrReload().Op1, Is.SameAs(source));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "writeRegisters")]
    private static extern void WriteRegisters(LinearScan allocator, RefPosition reference, GenTree tree);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "insertCopyOrReload")]
    private static extern void InsertCopyOrReload(LinearScan allocator, BasicBlock block, GenTree tree, uint index, RefPosition reference);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_blockInfo")]
    private static extern ref LsraBlockInfo[]? BlockInfo(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_bbNumMaxBeforeResolution")]
    private static extern ref uint BbNumMaxBeforeResolution(LinearScan allocator);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_jitEnregStructLocals")]
    private static extern ref int EnregStructLocals(ref JitConfigValues config);

    private static RefPosition NewReference(LinearScan allocator, GenTree tree, regNumber reg)
    {
        var interval = new Interval(TYP_INT, SRBM_ALLINT_INIT);
        _ = allocator.newRefPosition(interval, 0, RefType.RefTypeDef, tree, SRBM_ALLINT_INIT);
        return allocator.newRefPosition(interval, 2, RefType.RefTypeUse, tree, genSingleTypeRegMask(reg));
    }

    private static BasicBlock NewBlock(Compiler compiler)
    {
        compiler.fgBBNumMax = 0;
        return BasicBlock.New(compiler, BBJ_RETURN);
    }

    private static void WithAllocator(Action<Compiler, LinearScan> action, bool enregStructs = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var previousConfig = JitConfig;
        if (enregStructs)
        {
            EnregStructLocals(ref JitConfig) = 1;
        }
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        CompilerAllIntRegs(compiler) = SRBM_ALLINT_INIT;
        CompilerAllFloatRegs(compiler) = SRBM_ALLFLOAT_INIT;
        CompilerAllMaskRegs(compiler) = SRBM_ALLMASK_INIT;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        compiler.codeGen.CopyRegisterInfo();
        compiler.codeGen.RegSet.rsClearRegsModified();
        try
        {
            var allocator = new LinearScan(compiler);
            for (var index = 0; index < (int)regNumber.ACTUAL_REG_COUNT; index++)
            {
                allocator.physRegs[index].init((regNumber)index);
            }
            BlockInfo(allocator) = [new LsraBlockInfo(), new LsraBlockInfo()];
            BbNumMaxBeforeResolution(allocator) = 1;
            action(compiler, allocator);
        }
        finally
        {
            JitTls.Compiler = previous;
            JitConfig = previousConfig;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllInt")]
    private static extern ref regMask CompilerAllIntRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllFloat")]
    private static extern ref regMask CompilerAllFloatRegs(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "srbmAllMask")]
    private static extern ref regMask CompilerAllMaskRegs(Compiler compiler);
}
