// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
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
internal static unsafe class ReturnLoweringTests
{
    [TestCase(TYP_INT, TYP_FLOAT)]
    [TestCase(TYP_DOUBLE, TYP_LONG)]
    public static void PrimitiveRegisterClassChangesInsertBitcasts(var_types sourceType, var_types returnType)
    {
        WithCompiler(returnType, returnType, returnType.Size, (compiler, block, lowering) => {
            compiler.lvaTable[0].Type = sourceType;
            var value = compiler.gtNewLclvNode(sourceType, 0);
            var ret = AppendReturn(block, returnType, value);

            LowerRet(lowering, ret);

            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_BITCAST));
            Assert.That(ret.Op1.Type, Is.EqualTo(returnType));
            Assert.That(ret.Op1.AsUnOp().Op1, Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(ret.Op1));
            Assert.That(ret.Op1.Next, Is.SameAs(ret));
            Assert.That(value.IsRegOptional, Is.True);
        });
    }

    [Test]
    public static void SameRegisterClassDoesNotIntroduceABitcast()
    {
        WithCompiler(TYP_INT, TYP_INT, 4, (compiler, block, lowering) => {
            var value = compiler.gtNewIconNode(TYP_INT, 123);
            var ret = AppendReturn(block, TYP_INT, value);
            LowerRet(lowering, ret);
            Assert.That(ret.Op1, Is.SameAs(value));
            Assert.That(value.Next, Is.SameAs(ret));
        });
    }

    [TestCase(TYP_FLOAT, 0x80000000L)]
    [TestCase(TYP_FLOAT, 0x7FC01234L)]
    [TestCase(TYP_DOUBLE, long.MinValue)]
    [TestCase(TYP_DOUBLE, 0x7FF8000000001234L)]
    public static void StructConstantsPreserveFloatingBitsAndLogicalIdentity(var_types nativeType, long bits)
    {
        WithCompiler(TYP_STRUCT, nativeType, nativeType.Size, (compiler, block, lowering) => {
            var sourceType = nativeType is TYP_FLOAT ? TYP_INT : TYP_LONG;
            var value = compiler.gtNewIconNode(sourceType, unchecked((nint)bits));
            value.Flags |= GTF_DONT_CSE;
            value._vnPair.SetBoth(123);
            var ret = AppendReturn(block, TYP_STRUCT, value);
            var flags = value.Flags;

            LowerRet(lowering, ret);

            Assert.That(ret.Type, Is.EqualTo(nativeType));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_CNS_DBL));
            Assert.That(ret.Op1.Type, Is.EqualTo(nativeType));
            var actualBits = nativeType is TYP_FLOAT
                ? unchecked((uint)BitConverter.SingleToInt32Bits((float)ret.Op1.AsDblCon().DconVal))
                : BitConverter.DoubleToInt64Bits(ret.Op1.AsDblCon().DconVal);
            Assert.That(actualBits, Is.EqualTo(bits));
            Assert.That(ret.Op1.Flags, Is.EqualTo(flags & GTF_NODE_MASK));
            Assert.That(ret.Op1._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(block.FirstNode, Is.SameAs(ret.Op1));
            Assert.That(value.Prev is null && value.Next is null, Is.True);
#if DEBUG
            Assert.That(ret.Op1.TreeId, Is.EqualTo(value.TreeId));
#endif
        });
    }

    [TestCase(TYP_STRUCT, TYP_UBYTE, TYP_INT, false)]
    [TestCase(TYP_BYTE, TYP_BYTE, TYP_BYTE, false)]
    [TestCase(TYP_STRUCT, TYP_LONG, TYP_LONG, true)]
    public static void MemoryAndPromotedStructLocalsUseTheRequiredFieldType(
        var_types signatureType, var_types nativeType, var_types fieldType, bool promoted)
    {
        WithCompiler(signatureType, nativeType, nativeType.Size, (compiler, block, lowering) => {
            ref var descriptor = ref compiler.lvaTable[0];
            descriptor.lvPromoted = promoted;
            descriptor.lvDoNotEnregister = !promoted;
            var value = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            value.SsaNum = 17;
            value._vnPair.SetBoth(123);
            var ret = AppendReturn(block, signatureType is TYP_STRUCT ? TYP_STRUCT : nativeType.ActualType, value);

            LowerRet(lowering, ret);

            Assert.That(ret.Type, Is.EqualTo(nativeType.ActualType));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(ret.Op1.Type, Is.EqualTo(fieldType));
            Assert.That(ret.Op1.AsLclFld().LclNum, Is.Zero);
            Assert.That(ret.Op1.AsLclFld().LclOffs, Is.Zero);
            Assert.That(ret.Op1.AsLclFld().SsaNum, Is.EqualTo(17));
            Assert.That(ret.Op1._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(descriptor.lvDoNotEnregister, Is.True);
            Assert.That(value.Prev is null && value.Next is null, Is.True);
#if DEBUG
            Assert.That(ret.Op1.TreeId, Is.EqualTo(value.TreeId));
#endif
        });
    }

    [TestCase(TYP_LONG, false)]
    [TestCase(TYP_DOUBLE, true)]
    public static void EnregisteredStructLocalsRetypeBeforeOptionalBitcasts(var_types nativeType, bool bitcast)
    {
        WithCompiler(TYP_STRUCT, nativeType, 8, (compiler, block, lowering) => {
            var value = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            value._vnPair.SetBoth(123);
            var ret = AppendReturn(block, TYP_STRUCT, value);

            LowerRet(lowering, ret);

            Assert.That(value.Type, Is.EqualTo(TYP_LONG));
            Assert.That(value._vnPair.Liberal, Is.EqualTo(123));
            Assert.That(ret.Op1.Oper, Is.EqualTo(bitcast ? GT_BITCAST : GT_LCL_VAR));
            Assert.That(bitcast ? ret.Op1.AsUnOp().Op1 : ret.Op1, Is.SameAs(value));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void StructMemoryReadsRetypeWithChangeOperFlagAndVnSemantics(bool blockLoad)
    {
        WithCompiler(TYP_STRUCT, TYP_LONG, 8, (compiler, block, lowering) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var layout = compiler.lvaTable[0].Layout;
            assert(layout is not null);
            var value = blockLoad
                ? compiler.gtNewBlkIndir(address, layout)
                : new GenTreeIndir(GT_IND, TYP_LONG, address);
            value.Flags = GTF_IND_NONFAULTING | GTF_IND_VOLATILE | GTF_DONT_CSE;
            value._vnPair.SetBoth(123);
            block.InsertAtEnd(address);
            var ret = AppendReturn(block, TYP_STRUCT, value);

            LowerRet(lowering, ret);

            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_IND));
            Assert.That(ret.Op1.Type, Is.EqualTo(TYP_LONG));
            Assert.That(ret.Op1.AsIndir().Addr, Is.SameAs(address));
            Assert.That(ret.Op1.Flags & GTF_IND_VOLATILE, Is.EqualTo(GTF_EMPTY));
            Assert.That(ret.Op1.Flags & GTF_IND_NONFAULTING, Is.EqualTo(GTF_IND_NONFAULTING));
            Assert.That(ret.Op1._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(ret.Op1 == value, Is.EqualTo(!blockLoad));
        });
    }

    [Test]
    public static void UndersizedStructIndirectionSpillsWithoutWideningTheMemoryRead()
    {
        WithCompiler(TYP_STRUCT, TYP_INT, 3, (compiler, block, lowering) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var layout = compiler.lvaTable[0].Layout;
            assert(layout is not null);
            var value = compiler.gtNewBlkIndir(address, layout);
            block.InsertAtEnd(address);
            var ret = AppendReturn(block, TYP_STRUCT, value);
            var originalLocalCount = compiler.lvaCount;

            LowerRet(lowering, ret);

            Assert.That(compiler.lvaCount, Is.EqualTo(originalLocalCount + 1));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(ret.Op1.Type, Is.EqualTo(TYP_INT));
            var localNumber = ret.Op1.AsLclFld().LclNum;
            Assert.That(compiler.lvaTable[localNumber].Layout, Is.SameAs(layout));
            Assert.That(compiler.lvaTable[localNumber].lvDoNotEnregister, Is.True);
            Assert.That(value.Size, Is.EqualTo(3));
            Assert.That(value.Type, Is.EqualTo(TYP_STRUCT));
        });
    }

    [TestCase(TYP_REF, CorInfoGCType.TYPE_GC_REF)]
    [TestCase(TYP_BYREF, CorInfoGCType.TYPE_GC_BYREF)]
    public static void EnregisteredGcStructsRetainTheirGcRegisterType(var_types type, CorInfoGCType gcType)
    {
        WithCompiler(TYP_STRUCT, type, 8, (compiler, block, lowering) => {
            var layout = compiler.lvaTable[0].Layout;
            assert(layout is not null);
            layout.GCPtrCount = 1;
            layout._inlineGCPtrs[0] = gcType;
            var value = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            var ret = AppendReturn(block, TYP_STRUCT, value);

            LowerRet(lowering, ret);

            Assert.That(ret.Type, Is.EqualTo(type));
            Assert.That(value.Type, Is.EqualTo(type));
            Assert.That(ret.Op1, Is.SameAs(value));
        });
    }

    [Test]
    public static void NarrowScalarIndirectionIsZeroExtendedRatherThanWidened()
    {
        WithCompiler(TYP_STRUCT, TYP_LONG, 8, (compiler, block, lowering) => {
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            var value = new GenTreeIndir(GT_IND, TYP_INT, address);
            block.InsertAtEnd(address);
            var ret = AppendReturn(block, TYP_STRUCT, value);

            // Earlier promotion has retyped this load. Exercise the struct
            // helper directly because the outer debug ABI check rejects it.
            LowerRetStruct(lowering, ret);

            Assert.That(value.Type, Is.EqualTo(TYP_INT));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_CAST));
            Assert.That(ret.Op1.AsCast().IsZeroExtending, Is.True);
            Assert.That(ret.Op1.AsCast().CastOp, Is.SameAs(value));
            Assert.That(ret.Op1.Type, Is.EqualTo(TYP_LONG));
        });
    }

    [Test]
    public static void CompatibleFieldListPacksFieldsIntoItsReturnRegister()
    {
        WithCompiler(TYP_STRUCT, TYP_LONG, 8, (compiler, block, lowering) => {
            var first = compiler.gtNewIconNode(TYP_INT, 3);
            var second = compiler.gtNewIconNode(TYP_INT, 5);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, first, 0, TYP_INT);
            fields.AddFieldLIR(compiler, second, 4, TYP_INT);
            block.InsertAtEnd(first);
            block.InsertAtEnd(second);
            var ret = AppendReturn(block, TYP_STRUCT, fields);

            LowerRet(lowering, ret);

            Assert.That(ret.Op1, Is.SameAs(fields));
            Assert.That(fields.Uses.Head, Is.Not.Null);
            var use = fields.Uses.Head ?? throw new InvalidOperationException();
            Assert.That(use.Next, Is.Null);
            Assert.That(use.Node.Oper, Is.EqualTo(GT_OR));
            Assert.That(use.Node.Type, Is.EqualTo(TYP_LONG));
            Assert.That(use.Node.AsOp().Op2.Oper, Is.EqualTo(GT_LSH));
            Assert.That(use.Node.AsOp().Op2.AsOp().Op2.AsIntCon().IconValue, Is.EqualTo((nint)32));
            Assert.That(use.Node.Next, Is.SameAs(fields));
        });
    }

    [Test]
    public static void IncompatibleFloatingFieldListSpillsAndReloadsItsExactLayout()
    {
        WithCompiler(TYP_STRUCT, TYP_DOUBLE, 8, (compiler, block, lowering) => {
            var value = compiler.gtNewDconNode(TYP_FLOAT, 3.0);
            var fields = new GenTreeFieldList();
            fields.AddFieldLIR(compiler, value, 4, TYP_FLOAT);
            block.InsertAtEnd(value);
            var ret = AppendReturn(block, TYP_STRUCT, fields);
            var originalLocalCount = compiler.lvaCount;

            LowerRet(lowering, ret);

            Assert.That(compiler.lvaCount, Is.EqualTo(originalLocalCount + 1));
            Assert.That(ret.Type, Is.EqualTo(TYP_DOUBLE));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(ret.Op1.Type, Is.EqualTo(TYP_DOUBLE));
            var localNumber = ret.Op1.AsLclFld().LclNum;
            Assert.That(compiler.lvaTable[localNumber].Layout, Is.SameAs(compiler.lvaTable[0].Layout));
            Assert.That(compiler.lvaTable[localNumber].lvDoNotEnregister, Is.True);
            var storedValue = block.FirstNode ?? throw new InvalidOperationException();
            Assert.That(storedValue.AsIntCon().IconValue, Is.EqualTo((nint)BitConverter.SingleToInt32Bits(3.0f)));
            Assert.That(storedValue.Next?.Oper, Is.EqualTo(GT_STORE_LCL_FLD));
            Assert.That(storedValue.Next?.AsLclFld().LclNum, Is.EqualTo(localNumber));
            Assert.That(storedValue.Next?.AsLclFld().LclOffs, Is.EqualTo(4));
            Assert.That(value.Prev is null && value.Next is null, Is.True);
            Assert.That(fields.Prev is null && fields.Next is null, Is.True);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void VoidReturnsRunTheNativePInvokeEpilog(bool ilStub, bool helpers)
    {
        WithCompiler(TYP_VOID, TYP_VOID, 8, (compiler, block, lowering) => {
            compiler.info.compUnmanagedCallCountWithGCTransition = 1;
            if (ilStub)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_IL_STUB);
            }
            if (helpers)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_USE_PINVOKE_HELPERS);
            }
            var ret = AppendReturn(block, TYP_VOID, null);

            LowerRet(lowering, ret);

            Assert.That(block.LastNode, Is.SameAs(ret));
            if (ilStub && !helpers)
            {
                Assert.That(ret.Prev?.Oper, Is.EqualTo(GT_STOREIND));
                Assert.That(ret.Prev?.AsStoreInd().Addr.AsAddrMode().Offset, Is.EqualTo(16));
                Assert.That(ret.Prev?.AsStoreInd().Data.AsLclFld().LclOffs, Is.EqualTo(8));
            }
            else
            {
                Assert.That(block.FirstNode, Is.SameAs(ret));
            }
        });
    }

    [Test]
    public static void PrimitiveBitcastPrecedesThePInvokeFramePop()
    {
        WithCompiler(TYP_LONG, TYP_LONG, 8, (compiler, block, lowering) => {
            compiler.info.compUnmanagedCallCountWithGCTransition = 1;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_IL_STUB);
            compiler.lvaTable[0].Type = TYP_DOUBLE;
            var value = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var ret = AppendReturn(block, TYP_LONG, value);

            LowerRet(lowering, ret);

            Assert.That(value.Next, Is.SameAs(ret.Op1));
            Assert.That(ret.Op1.Oper, Is.EqualTo(GT_BITCAST));
            Assert.That(ret.Op1.Next, Is.Not.SameAs(ret));
            Assert.That(ret.Prev?.Oper, Is.EqualTo(GT_STOREIND));
            Assert.That(block.LastNode, Is.SameAs(ret));
        });
    }

    private static GenTreeUnOp AppendReturn(BasicBlock block, var_types type, GenTree? value)
    {
        if (value is not null)
        {
            block.InsertAtEnd(value);
        }
        var ret = new GenTreeUnOp(GT_RETURN, type, value);
        block.InsertAtEnd(ret);

        return ret;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_classLayoutTable")]
    private static extern ref ClassLayoutTable? LayoutTable(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerRet")]
    private static extern void LowerRet(Lowering lowering, GenTreeUnOp ret);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerRetStruct")]
    private static extern void LowerRetStruct(Lowering lowering, GenTreeUnOp ret);

    private static void WithCompiler(var_types signatureType, var_types nativeType, int layoutSize,
        Action<Compiler, BasicBlock, Lowering> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        vtable.getRelocTypeHint = &GetRelocTypeHint;
        ICorJitInfo ee = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&ee);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compFlags = CLFLG_REGVAR;
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.info.compCompHnd = &ee;
        compiler.info.compMatchedVM = true;
        compiler.info.compRetType = signatureType;
        compiler.info.compRetNativeType = nativeType;
        var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)0x1234, true, checked((uint)layoutSize), TYP_STRUCT, "Return", "Return");
        CORINFO_METHOD_INFO methodInfo = default;
        methodInfo.args.retTypeClass = layout.ClassHandle;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.lvaTable = new LclVarDsc[16];
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_STRUCT;
        compiler.lvaTable[0].Layout = layout;
        compiler.lvaTable[1].Type = TYP_I_IMPL;
        compiler.lvaTable[2].Type = TYP_STRUCT;
        compiler.lvaTable[2].Layout = new ClassLayout(64);
        compiler.lvaTable[2].SetAddressExposed(true, AddressExposedReason.EXTERNALLY_VISIBLE_IMPLICITLY);
        compiler.info.compLvFrameListRoot = 1;
        compiler.lvaInlinedPInvokeFrameVar = 2;
        var layouts = new ClassLayoutTable();
        _ = layouts.AddObjLayout(compiler, layout);
        LayoutTable(compiler) = layouts;
        compiler.compRetTypeDesc = new ReturnTypeDesc();
        compiler.compRetTypeDesc.InitializeReturnType(compiler, nativeType, null, CorInfoCallConvExtension.Managed);
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null) { Kind = BBJ_RETURN };
            block.MakeLir(null, null);
            compiler.compCurBB = block;
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
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* self, void* target)
    {
        _ = self;
        _ = target;

        return CorInfoReloc.NONE;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
    {
        _ = self;
        _ = handle;

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        _ = self;
        *info = new CORINFO_EE_INFO {
            targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI,
            offsetOfThreadFrame = 16,
            inlinedCallFrameInfo = new CORINFO_EE_INFO.InlinedCallFrameInfo { offsetOfFrameLink = 8 },
        };
    }
}
