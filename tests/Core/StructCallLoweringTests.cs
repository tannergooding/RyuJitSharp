// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.CorInfoGCType;
using static RyuJitSharp.CORINFO_RUNTIME_ABI;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class StructCallLoweringTests
{
    [TestCase(1, TYPE_GC_NONE, TYP_INT)]
    [TestCase(2, TYPE_GC_NONE, TYP_INT)]
    [TestCase(4, TYPE_GC_NONE, TYP_INT)]
    [TestCase(8, TYPE_GC_NONE, TYP_I_IMPL)]
    [TestCase(8, TYPE_GC_REF, TYP_REF)]
    [TestCase(8, TYPE_GC_BYREF, TYP_BYREF)]
    public static void UnusedReturnsNormalizeRegisterTypesWithoutChangingCallMetadata(
        int size, CorInfoGCType gcType, var_types expectedType)
    {
        WithCompiler(size, gcType, (compiler, block, lowering, handle) => {
            var argumentNode = compiler.gtNewIconNode(TYP_INT, 42);
            var call = CreateCall(handle);
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(argumentNode));
            call._directCallAddress = (void*)0x5678;
            call.IsUnusedValue = true;
            call._vnPair.SetBoth(123);
            var flags = call.Flags;
            block.InsertAtEnd(argumentNode);
            block.InsertAtEnd(call);
#if DEBUG
            var treeId = call.TreeId;
#endif

            LowerCallStruct(lowering, call);

            Assert.That(call.Type, Is.EqualTo(expectedType));
            Assert.That(call._returnType, Is.EqualTo(TYP_STRUCT));
            Assert.That((nint)call.RetClsHnd, Is.EqualTo((nint)handle));
            Assert.That((nint)call._callMethHnd, Is.EqualTo((nint)0x1234));
            Assert.That((nint)call._directCallAddress, Is.EqualTo((nint)0x5678));
            Assert.That(call.Flags, Is.EqualTo(flags));
            Assert.That(call._vnPair.Liberal, Is.EqualTo(123u));
            Assert.That(call.Args.GetUserArgByIndex(0), Is.SameAs(argument));
            Assert.That(argument.Node, Is.SameAs(argumentNode));
            Assert.That(call.IsUnusedValue, Is.True);
            Assert.That(block.LastNode, Is.SameAs(call));
            Assert.That(argumentNode.Next, Is.SameAs(call));
            Assert.That(call.Prev, Is.SameAs(argumentNode));
            Assert.That(((ClassInfo*)handle)->SizeQueries, Is.EqualTo(1));
            Assert.That(((ClassInfo*)handle)->GcQueries, Is.EqualTo(size == 8 ? 1 : 0));
#if DEBUG
            Assert.That(call.TreeId, Is.EqualTo(treeId));
#endif
        });
    }

    [TestCase(GT_RETURN, TYP_STRUCT, 8)]
    [TestCase(GT_RETURN, TYP_SIMD8, 8)]
    [TestCase(GT_STORE_LCL_VAR, TYP_STRUCT, 8)]
    [TestCase(GT_STORE_LCL_FLD, TYP_STRUCT, 8)]
    [TestCase(GT_STORE_LCL_FLD, TYP_I_IMPL, 8)]
    [TestCase(GT_STORE_LCL_FLD, TYP_UBYTE, 1)]
    [TestCase(GT_STORE_BLK, TYP_STRUCT, 8)]
    public static void StructOwnersRetainTheirRepresentationAndLayout(genTreeOps oper, var_types ownerType, int size)
    {
        WithCompiler(size, TYPE_GC_NONE, (compiler, block, lowering, handle) => {
            var call = CreateCall(handle);
            var layout = compiler.lvaTable[1].Layout;
            assert(layout is not null);
            GenTree? address = null;
            GenTree owner;
            switch (oper)
            {
                case GT_RETURN:
                {
                    owner = new GenTreeUnOp(GT_RETURN, ownerType, call);
                    break;
                }

                case GT_STORE_LCL_VAR:
                {
                    owner = compiler.gtNewStoreLclVarNode(1, call);
                    break;
                }

                case GT_STORE_LCL_FLD:
                {
                    owner = compiler.gtNewStoreLclFldNode(ownerType, 1, 0, call,
                        ownerType is TYP_STRUCT ? layout : null);
                    break;
                }

                case GT_STORE_BLK:
                {
                    address = compiler.gtNewLclvNode(TYP_BYREF, 0);
                    owner = new GenTreeBlk(TYP_STRUCT, address, call, layout);
                    break;
                }

                default:
                {
                    throw new InvalidOperationException();
                }
            }
            if (address is not null)
            {
                block.InsertAtEnd(address);
            }
            block.InsertAtEnd(call);
            block.InsertAtEnd(owner);
            var ownerFlags = owner.Flags;

            LowerCallStruct(lowering, call);

            Assert.That(call.Type, Is.EqualTo(size == 8 ? TYP_I_IMPL : TYP_INT));
            Assert.That(owner.Type, Is.EqualTo(ownerType));
            Assert.That(owner.Oper, Is.EqualTo(oper));
            Assert.That(owner.Flags, Is.EqualTo(ownerFlags));
            Assert.That(call.Next, Is.SameAs(owner));
            Assert.That(owner.Prev, Is.SameAs(call));
            Assert.That(block.LastNode, Is.SameAs(owner));
            Assert.That(compiler.lvaTable[1].Layout, Is.SameAs(layout));
            Assert.That(block.TryGetUse(call, out var use), Is.True);
            Assert.That(use.User(), Is.SameAs(owner));
            if (oper is GT_STORE_BLK)
            {
                Assert.That(owner.AsBlk().Layout, Is.SameAs(layout));
                Assert.That(owner.AsBlk().Addr, Is.SameAs(address));
            }
        });
    }

    [Test]
    public static void SimdStoreChangesItsStorageTypeWithoutReplacingTheCallOrAddress()
    {
        WithCompiler(8, TYPE_GC_NONE, (compiler, block, lowering, handle) => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var call = CreateCall(handle, TYP_SIMD8);
            var store = new GenTreeStoreInd(TYP_SIMD8, address, call) {
                Flags = GTF_ASG | GTF_IND_VOLATILE | GTF_IND_UNALIGNED,
            };
            block.InsertAtEnd(address);
            block.InsertAtEnd(call);
            block.InsertAtEnd(store);
            var flags = store.Flags;

            LowerCallStruct(lowering, call);

            Assert.That(call.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(store.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(store.Data, Is.SameAs(call));
            Assert.That(store.Addr, Is.SameAs(address));
            Assert.That(store.Flags, Is.EqualTo(flags));
            Assert.That(store.Prev, Is.SameAs(call));
            Assert.That(call.Prev, Is.SameAs(address));
        });
    }

    [TestCase(TYPE_GC_REF, TYP_REF, CORINFO_CORECLR_ABI)]
    [TestCase(TYPE_GC_NONE, TYP_I_IMPL, CORINFO_NATIVEAOT_ABI)]
    public static void ImporterRetypedHelperStoresRetainTheirGcAndNativeAbiTypes(
        CorInfoGCType gcType, var_types storeType, CORINFO_RUNTIME_ABI abi)
    {
        WithCompiler(8, gcType, (compiler, block, lowering, handle) => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var call = CreateCall(handle);
            call._callType = CT_HELPER;
            call._callMethHnd = Compiler.eeFindHelper(CorInfoHelpFunc.CORINFO_HELP_BOX);
            var store = new GenTreeStoreInd(storeType, address, call);
            block.InsertAtEnd(address);
            block.InsertAtEnd(call);
            block.InsertAtEnd(store);

            LowerCallStruct(lowering, call);

            Assert.That(call.Type, Is.EqualTo(storeType));
            Assert.That(store.Type, Is.EqualTo(storeType));
            Assert.That(store.Data, Is.SameAs(call));
            Assert.That(store.Addr, Is.SameAs(address));
            Assert.That(call.IsHelperCall(CorInfoHelpFunc.CORINFO_HELP_BOX), Is.True);
            Assert.That(block.LastNode, Is.SameAs(store));
        }, abi);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SimdArgumentOwnersKeepTheirUsesForArgumentLowering(bool fieldList)
    {
        WithCompiler(8, TYPE_GC_NONE, (compiler, block, lowering, handle) => {
            var call = CreateCall(handle, TYP_SIMD8);
            GenTree owner;
            if (fieldList)
            {
                var fields = new GenTreeFieldList();
                fields.AddFieldLIR(compiler, call, 0, TYP_SIMD8);
                owner = fields;
            }
            else
            {
                var consumer = new GenTreeCall(TYP_VOID) {
                    _callType = CT_USER_FUNC,
                    _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x9999,
                };
                _ = consumer.Args.PushBack(NewCallArg.CreateForStruct(call, TYP_SIMD8, new ClassLayout(8)));
                owner = consumer;
            }
            block.InsertAtEnd(call);
            block.InsertAtEnd(owner);

            LowerCallStruct(lowering, call);

            Assert.That(call.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(block.TryGetUse(call, out var use), Is.True);
            Assert.That(use.User(), Is.SameAs(owner));
            Assert.That(call.Next, Is.SameAs(owner));
            Assert.That(owner.Prev, Is.SameAs(call));
            Assert.That(owner.Type, Is.EqualTo(fieldList ? TYP_STRUCT : TYP_VOID));
        });
    }

    [Test]
    public static void HardwareIntrinsicUseGetsABitCastImmediatelyAfterTheRetypedCall()
    {
        WithCompiler(8, TYPE_GC_NONE, (compiler, block, lowering, handle) => {
            var call = CreateCall(handle, TYP_SIMD8);
            var independentNode = compiler.gtNewIconNode(TYP_INT, 7);
            independentNode.IsUnusedValue = true;
            var intrinsic = new GenTreeHWIntrinsic(TYP_FLOAT, NamedIntrinsic.NI_Vector_ToScalar, TYP_FLOAT, 8, call) {
                IsUnusedValue = true,
            };
            block.InsertAtEnd(call);
            block.InsertAtEnd(independentNode);
            block.InsertAtEnd(intrinsic);

            LowerCallStruct(lowering, call);

            var bitCast = intrinsic.GetOp(1).AsUnOp();
            Assert.That(bitCast.Oper, Is.EqualTo(GT_BITCAST));
            Assert.That(bitCast.Type, Is.EqualTo(TYP_SIMD8));
            Assert.That(bitCast.Op1, Is.SameAs(call));
            Assert.That(call.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(call.Next, Is.SameAs(bitCast));
            Assert.That(bitCast.Prev, Is.SameAs(call));
            Assert.That(bitCast.Next, Is.SameAs(independentNode));
            Assert.That(independentNode.Prev, Is.SameAs(bitCast));
            Assert.That(independentNode.Next, Is.SameAs(intrinsic));
            Assert.That(block.FirstNode, Is.SameAs(call));
            Assert.That(block.LastNode, Is.SameAs(intrinsic));
            Assert.That(call.IsContained, Is.False);
            Assert.That(bitCast.IsContained, Is.False);
            Assert.That(block.TryGetUse(call, out var callUse), Is.True);
            Assert.That(callUse.User(), Is.SameAs(bitCast));
            Assert.That(block.TryGetUse(bitCast, out var bitCastUse), Is.True);
            Assert.That(bitCastUse.User(), Is.SameAs(intrinsic));
        });
    }

    private static GenTreeCall CreateCall(CORINFO_CLASS_STRUCT_* handle, var_types type = TYP_STRUCT)
    {
        return new GenTreeCall(type) {
            _callType = CT_USER_FUNC,
            _callMethHnd = (CORINFO_METHOD_STRUCT_*)0x1234,
            _returnType = TYP_STRUCT,
            RetClsHnd = handle,
            Flags = GTF_CALL | GTF_GLOB_REF | GTF_EXCEPT,
        };
    }

    private delegate void StructCallAction(
        Compiler compiler, BasicBlock block, Lowering lowering, CORINFO_CLASS_STRUCT_* handle);

    private static void WithCompiler(int size, CorInfoGCType gcType, StructCallAction action,
        CORINFO_RUNTIME_ABI abi = CORINFO_CORECLR_ABI)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassSize = &GetClassSize;
        vtable.Base.Base.getClassGClayout = &GetClassGcLayout;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        var ee = new EEState {
            Interface = new ICorJitInfo { lpVtbl = &vtable },
            Abi = abi,
        };
        ClassInfo classInfo = new() { Size = size, GcType = gcType };
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &ee.Interface;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.lvaTable = new LclVarDsc[2];
        compiler.lvaCount = 2;
        compiler.lvaTable[0].Type = TYP_BYREF;
        compiler.lvaTable[1].Type = TYP_STRUCT;
        compiler.lvaTable[1].Layout = new ClassLayout(size);
#if DEBUG
        using var tls = new JitTls(&ee.Interface);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;
        compiler.codeGen = new CodeGen(compiler);
        try
        {
            var block = new BasicBlock(null, null);
            block.MakeLir(null, null);
            var lowering = new Lowering(compiler, new LinearScan(compiler));
            LoweringBlock(lowering) = block;
            action(compiler, block, lowering, (CORINFO_CLASS_STRUCT_*)&classInfo);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct ClassInfo
    {
        public int Size;
        public CorInfoGCType GcType;
        public int SizeQueries;
        public int GcQueries;
    }

    private struct EEState
    {
        public ICorJitInfo Interface;
        public CORINFO_RUNTIME_ABI Abi;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_block")]
    private static extern ref BasicBlock? LoweringBlock(Lowering lowering);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "LowerCallStruct")]
    private static extern void LowerCallStruct(Lowering lowering, GenTreeCall call);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle)
    {
        var info = (ClassInfo*)handle;
        info->SizeQueries++;

        return info->Size;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetClassGcLayout(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* handle, CorInfoGCType* layout)
    {
        var info = (ClassInfo*)handle;
        info->GcQueries++;
        *layout = info->GcType;

        return info->GcType is TYPE_GC_NONE ? 0 : 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = new CORINFO_EE_INFO { targetAbi = ((EEState*)self)->Abi };
    }
}
