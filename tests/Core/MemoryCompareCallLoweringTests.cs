// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class MemoryCompareCallLoweringTests
{
    [TestCase(1, TYP_UBYTE)]
    [TestCase(2, TYP_USHORT)]
    [TestCase(4, TYP_INT)]
    [TestCase(8, TYP_LONG)]
#if FEATURE_SIMD
    [TestCase(16, TYP_SIMD16)]
#endif
    public static void PowerOfTwoLengthUsesOneComparisonAndReplacesItsOwner(int size, var_types loadType)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var (call, left, right, length) = NewCall(compiler, size);
            var use = new GenTreeUnOp(GT_NEG, TYP_INT, call);
            foreach (var node in new GenTree[] { left, right, length, call, use })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.True);
            var firstLoad = next?.AsIndir() ?? throw new InvalidOperationException();
            var secondLoad = firstLoad.Next?.AsIndir() ?? throw new InvalidOperationException();
            Assert.That(firstLoad.Type, Is.EqualTo(loadType));
            Assert.That(secondLoad.Type, Is.EqualTo(loadType));
            Assert.That(firstLoad.Addr, Is.SameAs(left));
            Assert.That(secondLoad.Addr, Is.SameAs(right));
            Assert.That(use.Op1.Type, Is.EqualTo(TYP_INT));
            Assert.That(use.Op1.Oper, Is.EqualTo(loadType is TYP_SIMD16 ? GT_HWINTRINSIC : GT_EQ));
            Assert.That(use.Op1.Prev, Is.SameAs(secondLoad));
            Assert.That(length.Next, Is.Null);
            Assert.That(call.Next, Is.Null);
            Assert.That(use.Op1.IsUnusedValue, Is.False);
        });
    }

    [TestCase(5, TYP_INT, 1)]
    [TestCase(9, TYP_LONG, 1)]
#if FEATURE_SIMD
    [TestCase(17, TYP_SIMD16, 1)]
    [TestCase(31, TYP_SIMD16, 15)]
#endif
    public static void NonPowerOfTwoLengthUsesOverlappingLoadsAndXorOrEquality(
        int size, var_types loadType, int offset)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var (call, left, right, length) = NewCall(compiler, size);
            var use = new GenTreeUnOp(GT_NEG, TYP_INT, call);
            foreach (var node in new GenTree[] { left, right, length, call, use })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.True);
            Assert.That(next?.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(next?.AsLclVar().LclNum, Is.GreaterThanOrEqualTo(2));
            Assert.That(left.Next?.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
            Assert.That(right.Next?.Oper, Is.EqualTo(GT_STORE_LCL_VAR));

            var leftSecond = FindOffsetLoad(next, offset, loadType);
            Assert.That(leftSecond.Addr.AsOp().Op1, Is.SameAs(next));
            Assert.That(leftSecond.Addr.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)offset));

            var result = use.Op1;
            Assert.That(result.Type, Is.EqualTo(TYP_INT));
            Assert.That(result.Oper, Is.EqualTo(loadType is TYP_SIMD16 ? GT_HWINTRINSIC : GT_EQ));
            if (loadType is TYP_SIMD16)
            {
                Assert.That(result.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NamedIntrinsic.NI_Vector_op_Equality));
            }
            else
            {
                Assert.That(result.AsOp().Op1.Oper, Is.EqualTo(GT_OR));
                Assert.That(result.AsOp().Op2.IsIntegralConst(0), Is.True);
            }
            Assert.That(length.Next, Is.Null);
            Assert.That(call.Next, Is.Null);
        });
    }

#if FEATURE_SIMD && TARGET_XARCH
    [TestCase(CORINFO_InstructionSet.InstructionSet_AVX2, 32, TYP_SIMD32, true)]
    [TestCase(CORINFO_InstructionSet.InstructionSet_AVX2, 64, TYP_SIMD32, false)]
    [TestCase(CORINFO_InstructionSet.InstructionSet_AVX512, 64, TYP_SIMD64, true)]
    [TestCase(CORINFO_InstructionSet.InstructionSet_AVX512, 128, TYP_SIMD64, false)]
    public static void VectorWidthAndThresholdFollowAvailableInstructionSets(
        CORINFO_InstructionSet isa, int size, var_types loadType, bool singleLoad)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            EnableIsa(compiler, CORINFO_InstructionSet.InstructionSet_AVX);
            EnableIsa(compiler, CORINFO_InstructionSet.InstructionSet_AVX2);
            if (isa is CORINFO_InstructionSet.InstructionSet_AVX512)
            {
                EnableIsa(compiler, isa);
            }

            var (call, left, right, length) = NewCall(compiler, size);
            var use = new GenTreeUnOp(GT_NEG, TYP_INT, call);
            foreach (var node in new GenTree[] { left, right, length, call, use })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.True);
            if (singleLoad)
            {
                Assert.That(next?.AsIndir().Type, Is.EqualTo(loadType));
                Assert.That(next?.Next?.AsIndir().Type, Is.EqualTo(loadType));
            }
            else
            {
                Assert.That(next?.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(FindOffsetLoad(next, size - loadType.Size, loadType).Type, Is.EqualTo(loadType));
            }
            Assert.That(use.Op1.AsHWIntrinsic().HWIntrinsicId,
                Is.EqualTo(NamedIntrinsic.NI_Vector_op_Equality));
        });
    }

    [TestCase(CORINFO_InstructionSet.InstructionSet_AVX2, 65)]
    [TestCase(CORINFO_InstructionSet.InstructionSet_AVX512, 129)]
    public static void AvailableInstructionSetsDoNotExceedNativeMaximum(
        CORINFO_InstructionSet isa, int size)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            EnableIsa(compiler, CORINFO_InstructionSet.InstructionSet_AVX2);
            if (isa is CORINFO_InstructionSet.InstructionSet_AVX512)
            {
                EnableIsa(compiler, isa);
            }
            var (call, left, right, length) = NewCall(compiler, size);
            foreach (var node in new GenTree[] { left, right, length, call })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(length.Next, Is.SameAs(call));
        });
    }
#endif

    [Test]
    public static void UnusedResultMarksItsRootAndUnusedRuntimeCell()
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var (call, left, right, length) = NewCall(compiler, 4);
            var cell = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(cell)
                .WithWellKnownArg(WellKnownArg.R2RIndirectionCell));
            foreach (var node in new GenTree[] { left, right, length, cell, call })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.True);
            Assert.That(next, Is.Not.Null);
            Assert.That(cell.IsUnusedValue, Is.True);
            Assert.That(cell.Next?.Next?.Next?.IsUnusedValue, Is.True);
            Assert.That(call.Next, Is.Null);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(33)]
    public static void ZeroNegativeAndAboveThresholdLengthsKeepTheCall(int size)
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var (call, left, right, length) = NewCall(compiler, size);
            foreach (var node in new GenTree[] { left, right, length, call })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(length.Next, Is.SameAs(call));
        });
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public static void OptimizationAndReturnAddressRequirementsKeepTheCall(bool minOpts, bool hasNextRetAddr)
    {
        WithCompiler(minOpts, (compiler, block, lowering) => {
            var (call, left, right, length) = NewCall(compiler, 4);
            foreach (var node in new GenTree[] { left, right, length, call })
            {
                block.InsertAtEnd(node);
            }
            compiler.info.compHasNextCallRetAddr = hasNextRetAddr;

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(length.Next, Is.SameAs(call));
        });
    }

    [Test]
    public static void DynamicLengthKeepsTheCall()
    {
        WithCompiler(false, (compiler, block, lowering) => {
            var (call, left, right, _) = NewCall(compiler, 4);
            var length = compiler.gtNewLclvNode(TYP_I_IMPL, 2);
            var lengthArg = call.Args.GetUserArgByIndex(2) ?? throw new InvalidOperationException();
            lengthArg.EarlyNode = length;
            foreach (var node in new GenTree[] { left, right, length, call })
            {
                block.InsertAtEnd(node);
            }

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.False);
            Assert.That(next, Is.Null);
            Assert.That(length.Next, Is.SameAs(call));
        });
    }

    private static GenTreeIndir FindOffsetLoad(GenTree? first, int offset, var_types type)
    {
        for (var node = first; node is not null; node = node.Next)
        {
            if ((node.Oper is GT_IND) && (node.Type == type) && (node.AsIndir().Addr.Oper is GT_ADD) &&
                node.AsIndir().Addr.AsOp().Op2.IsIntegralConst(offset))
            {
                return node.AsIndir();
            }
        }
        throw new InvalidOperationException("Second overlapping load was not produced.");
    }

    private static void EnableIsa(Compiler compiler, CORINFO_InstructionSet isa)
    {
        compiler.opts.compSupportsISA.AddInstructionSet(isa);
        compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
    }

    private static (GenTreeCall Call, GenTreeLclVar Left, GenTreeLclVar Right, GenTreeIntCon Length)
        NewCall(Compiler compiler, int size)
    {
        var left = compiler.gtNewLclvNode(TYP_BYREF, 0);
        var right = compiler.gtNewLclvNode(TYP_BYREF, 1);
        var length = compiler.gtNewIconNode(TYP_I_IMPL, size);
        var call = new GenTreeCall(TYP_INT) {
            _callType = gtCallTypes.CT_USER_FUNC,
            _callMethHnd = compiler.info.compMethodHnd,
            _callMoreFlags = GTF_CALL_M_SPECIAL_INTRINSIC,
        };
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(left));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(right));
        _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(length));
        return (call, left, right, length);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerCallMemcmp")]
    private static extern bool LowerCallMemcmp(Lowering lowering, GenTreeCall call, out GenTree? next);

    private static void WithCompiler(bool minOpts, Action<Compiler, BasicBlock, Lowering> action)
    {
        fixed (byte* namespaceName = "System\0"u8)
        fixed (byte* className = "SpanHelpers\0"u8)
        fixed (byte* methodName = "SequenceEqual\0"u8)
        {
            var metadata = new MethodMetadata(namespaceName, className, methodName);
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getMethodNameFromMetadata = &GetMethodName;
            ICorJitInfo ee = new() { lpVtbl = &vtable };
#if DEBUG
            using var tls = new JitTls(&ee);
#endif
            var previous = JitTls.Compiler;
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            JitFlags flags = default;
            compiler.opts.jitFlags = &flags;
            compiler.opts.SetMinOpts(minOpts);
            compiler.lvaTable = [new LclVarDsc(), new LclVarDsc(), new LclVarDsc()];
            compiler.lvaCount = 3;
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[1].Type = TYP_BYREF;
            compiler.lvaTable[2].Type = TYP_I_IMPL;
            compiler.info.compCompHnd = &ee;
            compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)&metadata;
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
    }

    private readonly struct MethodMetadata(byte* namespaceName, byte* className, byte* methodName)
    {
        public readonly byte* Namespace = namespaceName;
        public readonly byte* Class = className;
        public readonly byte* Method = methodName;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetMethodName(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, byte** className,
        byte** namespaceName, byte** enclosingClasses, nint maxEnclosingClasses)
    {
        var metadata = (MethodMetadata*)method;
        *className = metadata->Class;
        *namespaceName = metadata->Namespace;
        return metadata->Method;
    }
}
