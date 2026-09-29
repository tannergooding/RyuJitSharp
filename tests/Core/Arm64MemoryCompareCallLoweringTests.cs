// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
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
internal static unsafe class Arm64MemoryCompareCallLoweringTests
{
    [TestCase(5, TYP_INT)]
    [TestCase(9, TYP_LONG)]
    public static void ScalarOverlappingLoadsUseTwoEqualitiesAndAnd(int size, var_types loadType)
    {
        WithCompiler((compiler, block, lowering) => {
            var (call, left, right, length) = NewCall(compiler, size);
            var use = new GenTreeUnOp(GT_NEG, TYP_INT, call);
            block.InsertAtEnd(left);
            block.InsertAtEnd(right);
            block.InsertAtEnd(length);
            block.InsertAtEnd(call);
            block.InsertAtEnd(use);

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.True);
            Assert.That(next?.Oper, Is.EqualTo(GT_LCL_VAR));
            var result = use.Op1.AsOp();
            Assert.That(result.Oper, Is.EqualTo(GT_AND));
            Assert.That(result.Op1.Oper, Is.EqualTo(GT_EQ));
            Assert.That(result.Op2.Oper, Is.EqualTo(GT_EQ));
            Assert.That(result.Op1.AsOp().Op1.Type, Is.EqualTo(loadType));
            Assert.That(result.Op2.AsOp().Op2.Type, Is.EqualTo(loadType));
            Assert.That(result.Op1.AsOp().Op1.AsIndir().Addr.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(result.Op2.AsOp().Op2.AsIndir().Addr.Oper, Is.EqualTo(GT_ADD));
            Assert.That(result.Op2.AsOp().Op2.AsIndir().Addr.AsOp().Op2.IsIntegralConst(size - loadType.Size), Is.True);
            Assert.That(length.Next, Is.Null);
            Assert.That(call.Next, Is.Null);
        });
    }

#if FEATURE_SIMD
    [Test]
    public static void VectorOverlappingLoadsKeepXorOrEquality()
    {
        WithCompiler((compiler, block, lowering) => {
            var (call, left, right, length) = NewCall(compiler, 17);
            var use = new GenTreeUnOp(GT_NEG, TYP_INT, call);
            block.InsertAtEnd(left);
            block.InsertAtEnd(right);
            block.InsertAtEnd(length);
            block.InsertAtEnd(call);
            block.InsertAtEnd(use);

            Assert.That(LowerCallMemcmp(lowering, call, out var next), Is.True);
            Assert.That(next?.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(use.Op1.Oper, Is.EqualTo(GT_HWINTRINSIC));
            Assert.That(use.Op1.AsHWIntrinsic().HWIntrinsicId, Is.EqualTo(NamedIntrinsic.NI_Vector_op_Equality));
            Assert.That(length.Next, Is.Null);
            Assert.That(call.Next, Is.Null);
        });
    }
#endif

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

    private static void WithCompiler(Action<Compiler, BasicBlock, Lowering> action)
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
            compiler.opts.SetMinOpts(false);
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
#endif
