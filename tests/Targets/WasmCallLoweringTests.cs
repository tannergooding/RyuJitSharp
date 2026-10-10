// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.Target.UnitTests;

[NonParallelizable]
internal static unsafe class WasmCallLoweringTests
{
    [TestCase(IAT_VALUE, true)]
    [TestCase(IAT_VALUE, false)]
    [TestCase(IAT_PVALUE, true)]
    [TestCase(IAT_PVALUE, false)]
    public static void PortableEntryPointArgumentDistinguishesDirectSymbolsFromCells(
        InfoAccessType accessType, bool readyToRun)
    {
        WithLowering((compiler, lowering, block) => {
            if (readyToRun)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            }

            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_PORTABLE_ENTRY_POINTS);
            var value = compiler.gtNewIconNode(TYP_INT, 42);
            var call = new GenTreeCall(TYP_VOID)
            {
                _callType = CT_USER_FUNC,
                _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x5678,
                _entryPoint = new CORINFO_CONST_LOOKUP { accessType = accessType, addr = (void*)0x1234 },
            };
            var userArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            userArg.AbiInfo = AbiPassingInformation.FromSegmentByValue(compiler,
                AbiPassingSegment.InRegister(regNumberExtensions.MakeWasmReg(0, WasmValueType.I), 0, TARGET_POINTER_SIZE));
            block.InsertAtEnd(value);
            block.InsertAtEnd(call);

            Assert.That(LowerCall(lowering, call), Is.Null);

            var direct = readyToRun && (accessType is IAT_VALUE);
            Assert.That((nint)call._directCallAddress, Is.EqualTo(direct ? (nint)0x1234 : 0));
            Assert.That(call.Args.CountArgs(), Is.EqualTo(2));
            Assert.That(call.Args.GetArgByIndex(0), Is.SameAs(userArg));
            var pepArg = call.Args.GetArgByIndex(1) ??
                throw new AssertionException("The final portable entrypoint argument is missing.");
            Assert.That(pepArg.WellKnownArg, Is.EqualTo(WellKnownArg.WasmPortableEntryPoint));
            Assert.That(pepArg.EarlyNode, Is.Null);
            Assert.That(call.Args.LateArgs.Last(), Is.SameAs(pepArg));
            Assert.That(pepArg.AbiInfo.NumSegments, Is.EqualTo(1));
            var register = regNumberExtensions.MakeWasmReg(1, WasmValueType.I);
            Assert.That(pepArg.AbiInfo.Segments[0].Register, Is.EqualTo(register));
            Assert.That(pepArg.AbiInfo.Segments[0].Size, Is.EqualTo(TARGET_POINTER_SIZE));
            var pepValue = pepArg.Node;

            if (direct)
            {
                Assert.That(call._controlExpr, Is.Null);
                Assert.That(pepValue.IsIntegralConst(0), Is.True);
                Assert.That(pepValue.Type, Is.EqualTo(TYP_I_IMPL));
                Assert.That(compiler.lvaCount, Is.EqualTo(1));
            }
            else
            {
                var control = call._controlExpr ??
                    throw new AssertionException("An indirect PEP call requires a control expression.");
                Assert.That(control.Oper, Is.EqualTo(GT_IND));
                Assert.That(pepValue.Oper, Is.EqualTo(GT_LCL_VAR));
                Assert.That(control.AsIndir().Addr.AsLclVar().LclNum, Is.EqualTo(pepValue.AsLclVar().LclNum));
                Assert.That(control.Flags & GenTreeFlags.GTF_IND_NONFAULTING, Is.Not.EqualTo(GenTreeFlags.GTF_EMPTY));
            }

            Assert.That(block.LastNode, Is.SameAs(call));
        });
    }

    [TestCase(GT_EQ, false, true)]
    [TestCase(GT_EQ, true, true)]
    [TestCase(GT_NE, false, true)]
    [TestCase(GT_NE, true, true)]
    [TestCase(GT_LT, false, false)]
    public static void CompareContainmentContainsOnlyEqualityZeroOperands(
        genTreeOps oper, bool zeroOnFirst, bool expectedContained)
    {
        WithLowering((compiler, lowering, block) =>
        {
            var value = compiler.gtNewIconNode(TYP_INT, 5);
            var zero = compiler.gtNewIconNode(TYP_INT, 0);
            var compare = compiler.gtNewBinaryNode(oper, TYP_INT,
                zeroOnFirst ? zero : value, zeroOnFirst ? value : zero).AsOp();
            block.InsertAtEnd(compare.Op1);
            block.InsertAtEnd(compare.Op2);
            block.InsertAtEnd(compare);

            ContainCheckCompare(lowering, compare);

            Assert.That(zero.IsContained, Is.EqualTo(expectedContained));
            Assert.That(value.IsContained, Is.False);
            Assert.That(block.LastNode, Is.SameAs(compare));
        });
    }

    [TestCase(3, TYP_INT)]
    [TestCase(5, TYP_LONG)]
    [TestCase(6, TYP_LONG)]
    [TestCase(7, TYP_LONG)]
    public static void StructCallSpillsAtOffsetZeroWithoutAMultiRegisterReturnDescriptor(int size, var_types type)
    {
        WithLowering((compiler, lowering, block) => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
            vtable.Base.Base.getClassAlignmentRequirement =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, bool, int>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_CLASS_STRUCT_*, byte, int>)&GetAlignment;
            ICorJitInfo ee = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &ee;
            var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)0x1234, true, checked((uint)size),
                TYP_STRUCT, "Return", "Return");
            var layouts = new ClassLayoutTable();
            _ = layouts.AddObjLayout(compiler, layout);
            LayoutTable(compiler) = layouts;
            var call = new GenTreeCall(type)
            {
                _callType = CT_USER_FUNC,
                _returnType = TYP_STRUCT,
                RetClsHnd = layout.ClassHandle,
            };
            block.InsertAtEnd(call);

            var result = SpillStructCallResult(lowering, call);

            var spill = call.Next?.AsLclFld() ??
                throw new AssertionException("The struct result was not spilled after the call.");
            Assert.That(spill.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(spill.Type, Is.EqualTo(type));
            Assert.That(spill.LclOffs, Is.Zero);
            Assert.That(spill.Data, Is.SameAs(call));
            Assert.That(spill.Next, Is.SameAs(result));
            Assert.That(result.Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(result.LclNum, Is.EqualTo(spill.LclNum));
            Assert.That(compiler.lvaGetDesc(result.LclNum).Layout, Is.SameAs(layout));
            Assert.That(compiler.lvaGetDesc(result.LclNum).lvDoNotEnregister, Is.True);
            Assert.That(block.LastNode, Is.SameAs(result));
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetAlignment(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle, byte doubleAlign) => 4;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SpillStructCallResult")]
    private static extern GenTreeLclVar SpillStructCallResult(Lowering lowering, GenTreeCall call);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_classLayoutTable")]
    private static extern ref ClassLayoutTable? LayoutTable(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerCall")]
    private static extern GenTree? LowerCall(Lowering lowering, GenTree node);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckCompare")]
    private static extern void ContainCheckCompare(Lowering lowering, GenTreeOp comparison);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    private static void WithLowering(Action<Compiler, Lowering, BasicBlock> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.lvaTable = [new LclVarDsc { Type = TYP_I_IMPL }];
        compiler.lvaCount = 1;
        compiler.fgNodeThreading = NodeThreading.LIR;
        JitTls.Compiler = compiler;

        try
        {
            compiler.codeGen = new CodeGen(compiler);
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
