// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_ARM64
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoGCType;
using static RyuJitSharp.CorInfoHFAElemType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64ReturnTypeTests
{
    [TestCase(9, TYPE_GC_NONE, TYPE_GC_NONE, TYP_I_IMPL, TYP_I_IMPL)]
    [TestCase(9, TYPE_GC_REF, TYPE_GC_NONE, TYP_REF, TYP_I_IMPL)]
    [TestCase(9, TYPE_GC_BYREF, TYPE_GC_NONE, TYP_BYREF, TYP_I_IMPL)]
    [TestCase(16, TYPE_GC_NONE, TYPE_GC_REF, TYP_I_IMPL, TYP_REF)]
    [TestCase(16, TYPE_GC_BYREF, TYPE_GC_NONE, TYP_BYREF, TYP_I_IMPL)]
    [TestCase(16, TYPE_GC_NONE, TYPE_GC_BYREF, TYP_I_IMPL, TYP_BYREF)]
    [TestCase(16, TYPE_GC_REF, TYPE_GC_BYREF, TYP_REF, TYP_BYREF)]
    [TestCase(16, TYPE_GC_BYREF, TYPE_GC_REF, TYP_BYREF, TYP_REF)]
    [TestCase(16, TYPE_GC_REF, TYPE_GC_REF, TYP_REF, TYP_REF)]
    [TestCase(16, TYPE_GC_BYREF, TYPE_GC_BYREF, TYP_BYREF, TYP_BYREF)]
    public static void TwoRegisterReturnsMapBothTypedGcLayoutSlots(
        int size, CorInfoGCType first, CorInfoGCType second, var_types firstType, var_types secondType)
    {
        WithCompiler(size, first, second, (compiler, handle, metadata) => {
            var descriptor = new ReturnTypeDesc();
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);

            Assert.Multiple(() => {
                Assert.That(descriptor.ReturnRegCount, Is.EqualTo(2));
                Assert.That(descriptor.IsMultiRegRetType, Is.True);
                Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(firstType));
                Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(secondType));
                Assert.That(descriptor.GetAbiReturnReg(0, CorInfoCallConvExtension.Managed), Is.EqualTo(REG_R0));
                Assert.That(descriptor.GetAbiReturnReg(1, CorInfoCallConvExtension.Managed), Is.EqualTo(REG_R1));
                Assert.That(metadata.GcLayoutCalls, Is.EqualTo(1));
                Assert.That(metadata.GcLayoutWasInitialized, Is.True);
                Assert.That(metadata.SizeCalls, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public static void ResetClearsBothReturnSlotsBeforeReinitialization()
    {
        WithCompiler(16, TYPE_GC_REF, TYPE_GC_BYREF, (compiler, handle, metadata) => {
            var descriptor = new ReturnTypeDesc();
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);
            Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(TYP_REF));
            Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(TYP_BYREF));

            descriptor.Reset();
            metadata.First = TYPE_GC_NONE;
            metadata.Second = TYPE_GC_NONE;
            descriptor.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);

            Assert.That(descriptor.ReturnRegCount, Is.EqualTo(2));
            Assert.That(descriptor.GetReturnRegType(0), Is.EqualTo(TYP_I_IMPL));
            Assert.That(descriptor.GetReturnRegType(1), Is.EqualTo(TYP_I_IMPL));
            Assert.That(metadata.GcLayoutCalls, Is.EqualTo(2));
        });
    }

    [TestCase(TYP_SIMD8, 8)]
    [TestCase(TYP_SIMD12, 12)]
    [TestCase(TYP_SIMD16, 16)]
    public static void SimdHfaReturnsNormalizeToTheMultiRegisterStructAbi(var_types type, int size)
    {
        WithLowering(size, TYP_STRUCT, (compiler, lowering, block) => {
            var value = new GenTreeCall(type);
            var ret = new GenTreeUnOp(GT_RETURN, type, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);
            LowerRet(lowering, ret);

            Assert.That(compiler.compRetTypeDesc.ReturnRegCount, Is.EqualTo(size / 4));
            Assert.That(ret.Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(ret.Op1, Is.SameAs(value));
            Assert.That(value.Type, Is.EqualTo(type));
        });
    }

    [TestCase(TYP_INT, TYP_FLOAT)]
    [TestCase(TYP_DOUBLE, TYP_LONG)]
    public static void PrimitiveReturnRegisterChangesInsertBitcasts(var_types sourceType, var_types nativeType)
    {
        WithLowering(nativeType.Size, nativeType, (compiler, lowering, block) => {
            compiler.lvaTable[0].Type = sourceType;
            var value = compiler.gtNewLclvNode(sourceType, 0);
            var ret = new GenTreeUnOp(GT_RETURN, nativeType, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);
            LowerRet(lowering, ret);

            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_BITCAST));
            Assert.That(ret.Op1.Type, Is.EqualTo(nativeType));
            Assert.That(ret.Op1.AsUnOp().Op1, Is.SameAs(value));
            Assert.That(value.IsRegOptional, Is.True);
        });
    }

    [TestCase(TYP_UBYTE, TYP_INT)]
    [TestCase(TYP_LONG, TYP_LONG)]
    public static void StackStructReturnsPreserveTheNativeFieldType(var_types nativeType, var_types fieldType)
    {
        WithLowering(nativeType.Size, nativeType, (compiler, lowering, block) => {
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var value = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            value.SsaNum = 17;
            var ret = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, value);
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);
            LowerRet(lowering, ret);

            Assert.That(ret.Type, Is.EqualTo(nativeType.ActualType));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(ret.Op1.Type, Is.EqualTo(fieldType));
            Assert.That(ret.Op1.AsLclFld().SsaNum, Is.EqualTo(17));
            Assert.That(value.Next, Is.Null);
        });
    }

    [Test]
    public static void IncompatibleReturnFieldsSpillWithoutRetypingNonzeroFloatingStores()
    {
        WithLowering(8, TYP_DOUBLE, (compiler, lowering, block) => {
            var value = compiler.gtNewDconNode(TYP_FLOAT, 3.0);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, value, 4, TYP_FLOAT);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, fields);
            block.InsertAtEnd(value);
            block.InsertAtEnd(fields);
            block.InsertAtEnd(ret);
            var localCount = compiler.lvaCount;
            LowerRet(lowering, ret);

            Assert.That(compiler.lvaCount, Is.EqualTo(localCount + 1));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(ret.Op1.Type, Is.EqualTo(TYP_DOUBLE));
            Assert.That(block.FirstNode, Is.SameAs(value));
            Assert.That(value.Next?.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(value.Next?.AsLclFld().LclOffs, Is.EqualTo(4));
            Assert.That(fields.Next, Is.Null);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ReturnContainmentUsesTheValueRatherThanTheSwiftError(bool swift)
    {
        WithLowering(16, TYP_STRUCT, (compiler, lowering, block) => {
            compiler.lvaTable[0].lvIsMultiRegRet = true;
            compiler.lvaTable[0].lvDoNotEnregister = true;
            var error = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var value = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            GenTreeUnOp ret;
            if (swift)
            {
                block.InsertAtEnd(error);
                ret = new GenTreeOp(GT_SWIFT_ERROR_RET, TYP_STRUCT, error, value);
            }
            else
            {
                ret = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, value);
            }
            block.InsertAtEnd(value);
            block.InsertAtEnd(ret);
            ContainCheckRet(lowering, ret);

            Assert.That(value.IsContained, Is.True);
            Assert.That(error.IsContained, Is.False);
        });
    }

    [TestCase(GT_LCL_VAR)]
    [TestCase(GT_LCL_FLD)]
    [TestCase(GT_BLK)]
    [TestCase(GT_FIELD_LIST)]
    public static void StackStructArgumentsStayContainedWithoutPrimitiveRetyping(genTreeOps oper)
    {
        WithLowering(8, TYP_LONG, (compiler, lowering, block) => {
            var layout = compiler.lvaTable[0].Layout;
            assert(layout is not null);
            GenTree value;
            if (oper is GT_BLK)
            {
                var address = compiler.gtNewLclvNode(TYP_BYREF, 1);
                block.InsertAtEnd(address);
                value = compiler.gtNewBlkIndir(address, layout);
            }
            else if (oper is GT_FIELD_LIST)
            {
                value = new GenTreeFieldList();
            }
            else
            {
                value = oper is GT_LCL_VAR
                    ? compiler.gtNewLclvNode(TYP_STRUCT, 0)
                    : compiler.gtNewLclFldNode(TYP_STRUCT, 0, 0, layout);
            }
            var argument = new GenTreePutArgStk(TYP_STRUCT, value, null, 0, 8, false);
            block.InsertAtEnd(value);
            block.InsertAtEnd(argument);
            LowerPutArgStk(lowering, argument);

            Assert.That(value.IsContained, Is.True);
            Assert.That(value.Type, Is.EqualTo(TYP_STRUCT));
            Assert.That(compiler.lvaTable[0].lvDoNotEnregister, Is.EqualTo(oper is GT_LCL_VAR));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_classLayoutTable")]
    private static extern ref ClassLayoutTable? LayoutTable(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerRet")]
    private static extern void LowerRet(Lowering lowering, GenTreeUnOp ret);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ContainCheckRet")]
    private static extern void ContainCheckRet(Lowering lowering, GenTreeUnOp ret);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerPutArgStk")]
    private static extern void LowerPutArgStk(Lowering lowering, GenTreePutArgStk argument);

    private static void WithLowering(int size, var_types nativeType, Action<Compiler, Lowering, BasicBlock> action)
    {
        WithCompiler(size, TYPE_GC_NONE, TYPE_GC_NONE, (compiler, handle, metadata) => {
            compiler.opts.SetMinOpts(true);
            compiler.opts.compFlags = CLFLG_REGVAR;
            compiler.fgNodeThreading = NodeThreading.LIR;
            compiler.info.compRetBuffArg = BAD_VAR_NUM;
            compiler.info.compRetType = TYP_STRUCT;
            compiler.info.compRetNativeType = nativeType;
            CORINFO_METHOD_INFO methodInfo = default;
            methodInfo.args.retTypeClass = handle;
            compiler.info.compMethodInfo = &methodInfo;
            var layout = new ClassLayout(handle, true, (uint)size, TYP_STRUCT, "Return", "Return");
            var layouts = new ClassLayoutTable();
            _ = layouts.AddObjLayout(compiler, layout);
            LayoutTable(compiler) = layouts;
            compiler.lvaTable = new LclVarDsc[4];
            compiler.lvaCount = 2;
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            compiler.lvaTable[1].Type = TYP_BYREF;
            compiler.compRetTypeDesc = new ReturnTypeDesc();
            if (nativeType is TYP_STRUCT)
            {
                metadata.HfaType = CORINFO_HFA_ELEM_FLOAT;
                compiler.compRetTypeDesc.InitializeStructReturnType(compiler, handle, CorInfoCallConvExtension.Managed);
            }
            else
            {
                compiler.compRetTypeDesc.InitializeReturnType(compiler, nativeType, null, CorInfoCallConvExtension.Managed);
            }
            compiler.codeGen = new CodeGen(compiler);
            var block = new BasicBlock(null, null) { Kind = BBKinds.BBJ_RETURN };
            block.MakeLir(null, null);
            compiler.compCurBB = block;
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, lowering, block);
        });
    }

    private sealed class Metadata
    {
        public int Size;
        public CorInfoGCType First;
        public CorInfoGCType Second;
        public int SizeCalls;
        public int GcLayoutCalls;
        public bool GcLayoutWasInitialized = true;
        public CorInfoHFAElemType HfaType;
    }

    private delegate void ReturnAction(Compiler compiler, CORINFO_CLASS_STRUCT_* handle, Metadata metadata);

    private static void WithCompiler(int size, CorInfoGCType first, CorInfoGCType second, ReturnAction action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getClassGClayout = &GetClassGcLayout;
        vtable.Base.Base.getClassAttribs = &GetClassAttribs;
        vtable.Base.Base.getHFAType = &GetHfaType;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        compiler.opts.jitFlags = &flags;
        var metadata = new Metadata {
            Size = size,
            First = first,
            Second = second,
        };
        var metadataHandle = GCHandle.Alloc(metadata);

#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;

        try
        {
            action(compiler, (CORINFO_CLASS_STRUCT_*)GCHandle.ToIntPtr(metadataHandle), metadata);
        }
        finally
        {
            metadataHandle.Free();
            JitTls.Compiler = previous;
        }
    }

    private static Metadata GetMetadata(CORINFO_CLASS_STRUCT_* handle)
    {
        return (Metadata)(GCHandle.FromIntPtr((nint)handle).Target
            ?? throw new InvalidOperationException("Missing struct return metadata"));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
    {
        var metadata = GetMetadata(handle);
        metadata.SizeCalls++;
        return metadata.Size;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassGcLayout(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle, CorInfoGCType* layout)
    {
        var metadata = GetMetadata(handle);
        metadata.GcLayoutCalls++;
        metadata.GcLayoutWasInitialized &= (layout[0] == TYPE_GC_NONE) && (layout[1] == TYPE_GC_NONE);
        layout[0] = metadata.First;
        layout[1] = metadata.Second;

        return (metadata.First is TYPE_GC_NONE ? 0 : 1) + (metadata.Second is TYPE_GC_NONE ? 0 : 1);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttribs(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHFAElemType GetHfaType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
        => GetMetadata(handle).HfaType;
}
#endif
