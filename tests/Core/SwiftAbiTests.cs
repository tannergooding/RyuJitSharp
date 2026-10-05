// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.CorInfoCallConvExtension;
using static RyuJitSharp.CorInfoType;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

#if SWIFT_SUPPORT
[NonParallelizable]
internal static unsafe class SwiftAbiTests
{
    [Test]
    public static void SwiftLoweringIsCachedPerClassAndCompiler()
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getSwiftLowering = &GetSwiftLowering;
        var state = new LoweringState { Interface = new ICorJitInfo { lpVtbl = &vtable } };
        var firstClass = (CORINFO_CLASS_STRUCT_*)0x1000;
        var secondClass = (CORINFO_CLASS_STRUCT_*)0x2000;

        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &state.Interface;

        ref readonly var first = ref compiler.GetSwiftLowering(firstClass);
        Assert.That(first.byReference, Is.False);
        Assert.That(first.numLoweredElements, Is.EqualTo((nint)1));
        Assert.That(first.loweredElements[0], Is.EqualTo(CorInfoType.CORINFO_TYPE_LONG));
        Assert.That(first.offsets[0], Is.EqualTo(4));
        Assert.That(state.Queries, Is.EqualTo(1));

        _ = compiler.GetSwiftLowering(firstClass);
        Assert.That(state.Queries, Is.EqualTo(1));

        ref readonly var second = ref compiler.GetSwiftLowering(secondClass);
        Assert.That(second.byReference, Is.True);
        Assert.That(state.Queries, Is.EqualTo(2));

        _ = compiler.GetSwiftLowering(firstClass);
        Assert.That(state.Queries, Is.EqualTo(2));

        for (var i = 0; i < 64; i++)
        {
            _ = compiler.GetSwiftLowering((CORINFO_CLASS_STRUCT_*)(0x3000 + (i * 16)));
        }

        GC.Collect();
        ref readonly var retained = ref compiler.GetSwiftLowering(firstClass);
        Assert.That(Unsafe.AreSame(ref Unsafe.AsRef(in first), ref Unsafe.AsRef(in retained)), Is.True);
        Assert.That(retained.loweredElements[0], Is.EqualTo(CorInfoType.CORINFO_TYPE_LONG));
        Assert.That(state.Queries, Is.EqualTo(66));

        var otherCompiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        otherCompiler.info.compCompHnd = &state.Interface;
        _ = otherCompiler.GetSwiftLowering(firstClass);
        Assert.That(state.Queries, Is.EqualTo(67));
    }

    [TestCase("SwiftSelf", CORINFO_TYPE_VALUECLASS, CORINFO_TYPE_VOID, true)]
    [TestCase("SwiftIndirectResult", CORINFO_TYPE_VALUECLASS, CORINFO_TYPE_VOID, true)]
    [TestCase("SwiftError", CORINFO_TYPE_PTR, CORINFO_TYPE_VOID, true)]
    [TestCase("Ordinary", CORINFO_TYPE_VALUECLASS, CORINFO_TYPE_VOID, false)]
    public static void SpecialParametersRecognizeOnlyTheirRequiredShapes(
        string name, CorInfoType parameterType, CorInfoType returnType, bool expected)
    {
        WithSwiftCompiler(name, (compiler, state, cls) => {
            compiler.info.compRetType = returnType.VarType;
            var result = InitSpecialParameter(compiler, (CORINFO_ARG_LIST_STRUCT_*)1, 0, parameterType,
                parameterType is CORINFO_TYPE_PTR ? null : cls);

            Assert.That(result, Is.EqualTo(expected));
            Assert.That(compiler.lvaSwiftSelfArg, Is.EqualTo(name is "SwiftSelf" ? 0 : BAD_VAR_NUM));
            Assert.That(compiler.lvaSwiftIndirectResultArg,
                Is.EqualTo(name is "SwiftIndirectResult" ? 0 : BAD_VAR_NUM));
            Assert.That(compiler.lvaSwiftErrorArg, Is.EqualTo(name is "SwiftError" ? 0 : BAD_VAR_NUM));

            if (name is "SwiftError")
            {
                Assert.That(compiler.lvaSwiftErrorLocal, Is.EqualTo(1));
                Assert.That(compiler.lvaGetDesc(1).lvImplicitlyReferenced, Is.True);
                Assert.That(compiler.lvaGetDesc(1).Layout, Is.SameAs(compiler.typGetObjLayout(cls)));
            }
        });
    }

    [TestCase("SwiftSelf", CORINFO_TYPE_PTR, CORINFO_TYPE_VOID)]
    [TestCase("SwiftIndirectResult", CORINFO_TYPE_BYREF, CORINFO_TYPE_VOID)]
    [TestCase("SwiftIndirectResult", CORINFO_TYPE_VALUECLASS, CORINFO_TYPE_INT)]
    [TestCase("SwiftError", CORINFO_TYPE_VALUECLASS, CORINFO_TYPE_VOID)]
    public static void SpecialParametersRejectInvalidShapes(
        string name, CorInfoType parameterType, CorInfoType returnType)
    {
        WithSwiftCompiler(name, (compiler, state, cls) => {
            compiler.info.compRetType = returnType.VarType;
            var error = Assert.Throws<FatalJitException>(() => InitSpecialParameter(
                compiler, (CORINFO_ARG_LIST_STRUCT_*)1, 0, parameterType,
                parameterType is CORINFO_TYPE_VALUECLASS ? cls : null));
            Assert.That(error!.Result, Is.EqualTo(CorJitResult.CORJIT_BADCODE));
        });
    }

    [TestCase("SwiftSelf", CORINFO_TYPE_VALUECLASS)]
    [TestCase("SwiftIndirectResult", CORINFO_TYPE_VALUECLASS)]
    [TestCase("SwiftError", CORINFO_TYPE_PTR)]
    public static void SpecialParametersRejectDuplicates(string name, CorInfoType parameterType)
    {
        WithSwiftCompiler(name, (compiler, state, cls) => {
            compiler.info.compRetType = TYP_VOID;
            var typeHnd = parameterType is CORINFO_TYPE_VALUECLASS ? cls : null;
            Assert.That(InitSpecialParameter(compiler, (CORINFO_ARG_LIST_STRUCT_*)1, 0, parameterType, typeHnd), Is.True);

            var error = Assert.Throws<FatalJitException>(() => InitSpecialParameter(
                compiler, (CORINFO_ARG_LIST_STRUCT_*)1, 0, parameterType, typeHnd));
            Assert.That(error!.Result, Is.EqualTo(CorJitResult.CORJIT_BADCODE));
            Assert.That(name is not "SwiftError" || compiler.lvaCount == 2, Is.True);
        });
    }

    [TestCase(false, "System.Runtime.InteropServices.Swift")]
    [TestCase(true, "Different.Namespace")]
    public static void SpecialParameterRequiresIntrinsicAndExactNamespace(bool intrinsic, string namespaceName)
    {
        WithSwiftCompiler("SwiftSelf", (compiler, state, cls) => {
            ((SwiftClass*)cls)->Intrinsic = intrinsic;
            fixed (byte* ns = Encoding.UTF8.GetBytes(namespaceName + '\0'))
            {
                ((SwiftClass*)cls)->Namespace = ns;
                Assert.That(InitSpecialParameter(compiler, (CORINFO_ARG_LIST_STRUCT_*)1, 0,
                    CORINFO_TYPE_VALUECLASS, cls), Is.False);
                Assert.That(compiler.lvaSwiftSelfArg, Is.EqualTo(BAD_VAR_NUM));
            }
        });
    }

    [Test]
    public static void SwiftErrorStorePreservesPointerAndOrdersErrorRegisterRead()
    {
        WithSwiftCompiler("SwiftError", (compiler, state, cls) => {
            var address = compiler.gtNewLclVarNode(TYP_I_IMPL, 0);
            AppendSwiftErrorStore(compiler, address);

            var store = ImportStatements(compiler)!.RootNode.AsStoreInd();
            var data = store.Data ?? throw new AssertionException("Missing Swift error register read.");
            Assert.That(store.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(store.Addr, Is.SameAs(address));
            Assert.That(data.Oper, Is.EqualTo(GT_SWIFT_ERROR));
            Assert.That(data.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(data.Flags & (GTF_ORDER_SIDEEFF | GTF_CALL | GTF_GLOB_REF),
                Is.EqualTo(GTF_ORDER_SIDEEFF | GTF_CALL | GTF_GLOB_REF));
        });
    }

    [Test]
    public static void SwiftErrorReturnPhaseSkipsMethodsWithoutSwiftErrorArgument()
    {
        WithSwiftCompiler("SwiftError", (compiler, _, _) => {
            var block = AddBlock(compiler, BBJ_RETURN);
            var ret = new GenTreeUnOp(GT_RETURN, TYP_INT, compiler.gtNewIconNode(TYP_INT, 7));
            var statement = AddStatement(compiler, block, ret);

            Assert.That(compiler.fgAddSwiftErrorReturns(), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(statement.RootNode, Is.SameAs(ret));
            Assert.That(ret.Oper, Is.EqualTo(GT_RETURN));
        });
    }

    [Test]
    public static void SwiftErrorReturnPhaseRewritesErrorReferencesAndReturnOperands()
    {
        WithSwiftCompiler("SwiftError", (compiler, _, _) => {
            ConfigureSwiftErrorReturnLocals(compiler);
            var block = AddBlock(compiler, BBJ_RETURN);
            var errorUse = compiler.gtNewLclVarNode(TYP_I_IMPL, compiler.lvaSwiftErrorArg);
            _ = AddStatement(compiler, block, errorUse);
            var value = compiler.gtNewLclVarNode(TYP_INT, 2);
            _ = AddStatement(compiler, block, new GenTreeUnOp(GT_RETURN, TYP_INT, value));

            Assert.That(compiler.fgAddSwiftErrorReturns(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(block.FirstStmt!.RootNode.Oper, Is.EqualTo(GT_LCL_ADDR));
            Assert.That(block.FirstStmt.RootNode.AsLclVarCommon().LclNum, Is.EqualTo(compiler.lvaSwiftErrorLocal));
            var swiftRet = block.LastStmt!.RootNode;
            Assert.That(swiftRet.Oper, Is.EqualTo(GT_SWIFT_ERROR_RET));
            Assert.That(swiftRet.AsOp().Op1.Oper, Is.EqualTo(GT_LCL_FLD));
            Assert.That(swiftRet.AsOp().Op1.AsLclVarCommon().LclNum, Is.EqualTo(compiler.lvaSwiftErrorLocal));
            Assert.That(swiftRet.AsOp().Op1.AsLclVarCommon().LclOffs, Is.Zero);
            Assert.That(swiftRet.AsOp().Op2, Is.SameAs(value));
        });
    }

    [Test]
    public static void SwiftErrorReturnPhaseUsesTheMergedReturnErrorLocal()
    {
        WithSwiftCompiler("SwiftError", (compiler, _, _) => {
            ConfigureSwiftErrorReturnLocals(compiler);
            var block = AddBlock(compiler, BBJ_RETURN);
            compiler.genReturnBB = block;
            var value = compiler.gtNewIconNode(TYP_INT, 7);
            _ = AddStatement(compiler, block, new GenTreeUnOp(GT_RETURN, TYP_INT, value));

            Assert.That(compiler.fgAddSwiftErrorReturns(), Is.EqualTo(PhaseStatus.MODIFIED_EVERYTHING));
            Assert.That(compiler.genReturnErrorLocal, Is.Not.EqualTo(BAD_VAR_NUM));
            Assert.That(compiler.lvaGetDesc(compiler.genReturnErrorLocal).Type, Is.EqualTo(TYP_I_IMPL));
            var swiftRet = block.LastStmt!.RootNode;
            Assert.That(swiftRet.Oper, Is.EqualTo(GT_SWIFT_ERROR_RET));
            Assert.That(swiftRet.AsOp().Op1.Oper, Is.EqualTo(GT_LCL_VAR));
            Assert.That(swiftRet.AsOp().Op1.AsLclVarCommon().LclNum, Is.EqualTo(compiler.genReturnErrorLocal));
            Assert.That(swiftRet.AsOp().Op2, Is.SameAs(value));
        });
    }

    [Test]
    public static void PartialLongLoweringReconstructsTheSevenByteTail()
    {
        WithSwiftCompiler("Ordinary", (compiler, state, cls) => {
            ((SwiftClass*)cls)->Intrinsic = false;
            var handles = stackalloc CORINFO_CLASS_STRUCT_*[1];
            var types = stackalloc CorInfoType[1];
            handles[0] = cls;
            types[0] = CORINFO_TYPE_VALUECLASS;
            ConfigureArgs(state, handles, types);
            state->LoweringOffset = 8;
            state->LoweringType = CORINFO_TYPE_LONG;
            compiler.lvaTable[0] = new LclVarDsc { Type = TYP_STRUCT, Layout = compiler.typGetObjLayout(cls) };
            compiler.impPushOnStack(compiler.gtNewLclVarNode(TYP_STRUCT, 0), new typeInfo(TYP_STRUCT));

            var call = new GenTreeCall(TYP_VOID);
            GenTree? error = null;
            var sig = new CORINFO_SIG_INFO {
                args = (CORINFO_ARG_LIST_STRUCT_*)1,
                numArgs = 1,
                retType = CORINFO_TYPE_VOID
            };
            PopSwiftArgs(compiler, call, in sig, ref error);

            Assert.That(error, Is.Null);
            Assert.That(compiler.impStackHeight, Is.Zero);
            Assert.That(state->LoweringQueries, Is.EqualTo(1));
            Assert.That(call.Args.CountArgs(), Is.EqualTo(1));
            var arg = call.Args.Head!;
            Assert.That(arg.SignatureType, Is.EqualTo(TYP_LONG));

            var high = arg.Node.AsOp();
            Assert.That(high.Oper, Is.EqualTo(GT_OR));
            AssertSegment(high.Op2, 14, TYP_UBYTE, 48);
            var middle = high.Op1.AsOp();
            Assert.That(middle.Oper, Is.EqualTo(GT_OR));
            AssertSegment(middle.Op2, 12, TYP_USHORT, 32);
            var low = middle.Op1.AsCast();
            Assert.That(low.CastType, Is.EqualTo(TYP_LONG));
            Assert.That(low.IsUnsigned, Is.True);
            Assert.That(low.Op1.AsLclFld().LclOffs, Is.EqualTo(8));
            Assert.That(low.Op1.Type, Is.EqualTo(TYP_INT));
        }, size: 15);
    }

    [Test]
    public static void SwiftSelfIndirectResultAndErrorAreReplacedInSignatureOrder()
    {
        WithSwiftCompiler("SwiftSelf", (compiler, state, cls) => {
            fixed (byte* indirectName = "SwiftIndirectResult\0"u8)
            fixed (byte* errorName = "SwiftError\0"u8)
            {
                var indirectClass = new SwiftClass {
                    Name = indirectName, Namespace = ((SwiftClass*)cls)->Namespace, Intrinsic = true
                };
                var errorClass = new SwiftClass {
                    Name = errorName, Namespace = ((SwiftClass*)cls)->Namespace, Intrinsic = true
                };
                var indirectHandle = (CORINFO_CLASS_STRUCT_*)&indirectClass;
                var errorHandle = (CORINFO_CLASS_STRUCT_*)&errorClass;
                AddLayout(compiler, indirectHandle, 8);
                AddLayout(compiler, errorHandle, 8);
                var handles = stackalloc CORINFO_CLASS_STRUCT_*[3];
                var types = stackalloc CorInfoType[3];
                handles[0] = cls;
                handles[1] = indirectHandle;
                handles[2] = errorHandle;
                types[0] = CORINFO_TYPE_VALUECLASS;
                types[1] = CORINFO_TYPE_VALUECLASS;
                types[2] = CORINFO_TYPE_PTR;
                ConfigureArgs(state, handles, types);

                compiler.lvaTable[0] = new LclVarDsc { Type = TYP_STRUCT, Layout = compiler.typGetObjLayout(cls) };
                compiler.lvaTable[1] = new LclVarDsc { Type = TYP_STRUCT, Layout = compiler.typGetObjLayout(indirectHandle) };
                compiler.lvaTable[2] = new LclVarDsc { Type = TYP_I_IMPL };
                compiler.lvaCount = 3;
                var errorPointer = compiler.gtNewLclVarNode(TYP_I_IMPL, 2);
                compiler.impPushOnStack(compiler.gtNewLclVarNode(TYP_STRUCT, 0), new typeInfo(TYP_STRUCT));
                compiler.impPushOnStack(compiler.gtNewLclVarNode(TYP_STRUCT, 1), new typeInfo(TYP_STRUCT));
                compiler.impPushOnStack(errorPointer, new typeInfo(TYP_I_IMPL));

                var call = new GenTreeCall(TYP_VOID);
                GenTree? error = null;
                var sig = new CORINFO_SIG_INFO {
                    args = (CORINFO_ARG_LIST_STRUCT_*)1, numArgs = 3, retType = CORINFO_TYPE_VOID
                };
                PopSwiftArgs(compiler, call, in sig, ref error);

                Assert.That(error, Is.SameAs(errorPointer));
                Assert.That(compiler.impStackHeight, Is.Zero);
                Assert.That(call.Args.CountArgs(), Is.EqualTo(3));
                var retBuffer = call.Args.GetArgByIndex(0)!;
                var swiftSelf = call.Args.GetArgByIndex(1)!;
                var sentinel = call.Args.GetArgByIndex(2)!;
                Assert.That(retBuffer.WellKnownArg, Is.EqualTo(WellKnownArg.RetBuffer));
                Assert.That(retBuffer.Node.AsLclFld().LclNum, Is.EqualTo(1));
                Assert.That(swiftSelf.WellKnownArg, Is.EqualTo(WellKnownArg.SwiftSelf));
                Assert.That(swiftSelf.Node.AsLclFld().LclNum, Is.EqualTo(0));
                Assert.That(sentinel.WellKnownArg, Is.EqualTo(WellKnownArg.SwiftError));
                Assert.That(sentinel.Node.AsIntCon().IconValue, Is.EqualTo((nint)0));
                Assert.That(state->LoweringQueries, Is.Zero);
            }
        });
    }

    private static void AssertSegment(GenTree node, ushort offset, var_types fieldType, nint shift)
    {
        var shl = node.AsOp();
        Assert.That(shl.Oper, Is.EqualTo(GT_LSH));
        Assert.That(shl.Op2.AsIntCon().IconValue, Is.EqualTo(shift));
        var cast = shl.Op1.AsCast();
        Assert.That(cast.CastType, Is.EqualTo(TYP_LONG));
        Assert.That(cast.IsUnsigned, Is.True);
        Assert.That(cast.Op1.AsLclFld().LclOffs, Is.EqualTo(offset));
        Assert.That(cast.Op1.Type, Is.EqualTo(fieldType));
    }

    private static BasicBlock AddBlock(Compiler compiler, BBKinds kind)
    {
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif
        var block = BasicBlock.New(compiler, kind);
        if (compiler.fgLastBB is BasicBlock previous)
        {
            previous.Next = block;
        }
        else
        {
            compiler.fgFirstBB = block;
        }
        compiler.fgLastBB = block;

        return block;
    }

    private static Statement AddStatement(Compiler compiler, BasicBlock block, GenTree tree)
    {
        var statement = compiler.fgNewStmtFromTree(tree);
        compiler.fgInsertStmtAtEnd(block, statement);
        return statement;
    }

    private static void ConfigureSwiftErrorReturnLocals(Compiler compiler)
    {
        compiler.info.compCallConv = Swift;
        compiler.lvaCount = 3;
        compiler.lvaTable[0].Type = TYP_I_IMPL;
        compiler.lvaTable[1].Type = TYP_I_IMPL;
        compiler.lvaTable[2].Type = TYP_INT;
        compiler.lvaSwiftErrorArg = 0;
        compiler.lvaSwiftErrorLocal = 1;
        compiler.genReturnBB = null;
        compiler.genReturnErrorLocal = BAD_VAR_NUM;
    }

    private static void ConfigureArgs(SwiftState* state, CORINFO_CLASS_STRUCT_** handles, CorInfoType* types)
    {
        state->Handles = handles;
        state->Types = types;
    }

    private static void AddLayout(Compiler compiler, CORINFO_CLASS_STRUCT_* cls, uint size)
    {
        _ = LayoutTable(compiler).AddObjLayout(compiler,
            new ClassLayout(cls, true, size, TYP_STRUCT, "Swift test struct", "Swift"));
    }

    private delegate void SwiftAction(Compiler compiler, SwiftState* state, CORINFO_CLASS_STRUCT_* cls);

    private static void WithSwiftCompiler(string name, SwiftAction action, uint size = 8)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getArgClass = &GetArgClass;
        vtable.Base.Base.getArgNext = &GetArgNext;
        vtable.Base.Base.getArgType = &GetArgType;
        vtable.Base.Base.getChildType = &GetChildType;
        vtable.Base.Base.isIntrinsicType = &IsIntrinsicType;
        vtable.Base.Base.getClassNameFromMetadata = &GetClassNameFromMetadata;
        vtable.Base.Base.classMustBeLoadedBeforeCodeIsRun = &ClassMustBeLoaded;
        vtable.Base.Base.getSwiftLowering = &GetConfiguredLowering;
        var state = new SwiftState { Interface = new ICorJitInfo { lpVtbl = &vtable } };
        JitFlags flags = default;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &state.Interface;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.info.compMethodInfo = &methodInfo;
#if DEBUG
        compiler.info.compFullName = name;
#endif
        compiler.info.compMaxStack = 8;
        compiler.opts.jitFlags = &flags;
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.stackState.esStack = new StackEntry[8];
        compiler.lvaTable = new LclVarDsc[8];
        compiler.lvaCount = 1;
        LayoutTable(compiler) = new ClassLayoutTable();
        compiler.lvaSwiftSelfArg = BAD_VAR_NUM;
        compiler.lvaSwiftIndirectResultArg = BAD_VAR_NUM;
        compiler.lvaSwiftErrorArg = BAD_VAR_NUM;
#if DEBUG
        using var tls = new JitTls(&state.Interface);
#endif
        var previous = JitTls.Compiler;
        JitTls.Compiler = compiler;
#if DEBUG
        JitTls.LogEnv.Compiler = compiler;
#endif
        try
        {
            fixed (byte* nameBytes = Encoding.UTF8.GetBytes(name + '\0'))
            fixed (byte* namespaceBytes = "System.Runtime.InteropServices.Swift\0"u8)
            {
                var cls = new SwiftClass {
                    Name = nameBytes, Namespace = namespaceBytes, Intrinsic = true
                };
                var handle = (CORINFO_CLASS_STRUCT_*)&cls;
                state.DefaultClass = handle;
                AddLayout(compiler, handle, size);
                action(compiler, &state, handle);
            }
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private struct SwiftClass
    {
        public byte* Name;
        public byte* Namespace;
        public bool Intrinsic;
    }

    private struct SwiftState
    {
        public ICorJitInfo Interface;
        public CORINFO_CLASS_STRUCT_* DefaultClass;
        public CORINFO_CLASS_STRUCT_** Handles;
        public CorInfoType* Types;
        public int LoweringOffset;
        public CorInfoType LoweringType;
        public bool ByReference;
        public int LoweringQueries;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_classLayoutTable")]
    private static extern ref ClassLayoutTable LayoutTable(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "impStmtList")]
    private static extern ref Statement? ImportStatements(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "lvaInitSpecialSwiftParam")]
    private static extern bool InitSpecialParameter(Compiler compiler, CORINFO_ARG_LIST_STRUCT_* arg,
        int local, CorInfoType type, CORINFO_CLASS_STRUCT_* typeClass);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impAppendSwiftErrorStore")]
    private static extern void AppendSwiftErrorStore(Compiler compiler, GenTree address);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "impPopArgsForSwiftCall")]
    private static extern void PopSwiftArgs(Compiler compiler, GenTreeCall call,
        in CORINFO_SIG_INFO signature, ref GenTree? error);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetArgClass(ICorJitInfo* info, CORINFO_SIG_INFO* sig,
        CORINFO_ARG_LIST_STRUCT_* arg)
    {
        var state = (SwiftState*)info;
        return state->Handles is null ? state->DefaultClass : state->Handles[(int)(nint)arg - 1];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_ARG_LIST_STRUCT_* GetArgNext(ICorJitInfo* info, CORINFO_ARG_LIST_STRUCT_* arg)
        => (CORINFO_ARG_LIST_STRUCT_*)((nint)arg + 1);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod GetArgType(ICorJitInfo* info, CORINFO_SIG_INFO* sig,
        CORINFO_ARG_LIST_STRUCT_* arg, CORINFO_CLASS_STRUCT_** cls)
    {
        var state = (SwiftState*)info;
        var index = (int)(nint)arg - 1;
        *cls = state->Handles is null ? state->DefaultClass : state->Handles[index];
        return (CorInfoTypeWithMod)(state->Types is null ? CORINFO_TYPE_VALUECLASS : state->Types[index]);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoType GetChildType(ICorJitInfo* info, CORINFO_CLASS_STRUCT_* cls,
        CORINFO_CLASS_STRUCT_** child)
    {
        *child = cls;
        return CORINFO_TYPE_VALUECLASS;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsIntrinsicType(ICorJitInfo* info, CORINFO_CLASS_STRUCT_* cls)
        => ((SwiftClass*)cls)->Intrinsic ? (byte)1 : (byte)0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte* GetClassNameFromMetadata(ICorJitInfo* info, CORINFO_CLASS_STRUCT_* cls, byte** ns)
    {
        var metadata = (SwiftClass*)cls;
        *ns = metadata->Namespace;
        return metadata->Name;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void ClassMustBeLoaded(ICorJitInfo* info, CORINFO_CLASS_STRUCT_* cls)
    {
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetConfiguredLowering(ICorJitInfo* info, CORINFO_CLASS_STRUCT_* cls,
        CORINFO_SWIFT_LOWERING* lowering)
    {
        var state = (SwiftState*)info;
        state->LoweringQueries++;
        lowering->byReference = state->ByReference;
        lowering->numLoweredElements = 1;
        lowering->loweredElements[0] = state->LoweringType;
        lowering->offsets[0] = state->LoweringOffset;
    }

    private struct LoweringState
    {
        public ICorJitInfo Interface;
        public int Queries;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetSwiftLowering(ICorJitInfo* info, CORINFO_CLASS_STRUCT_* cls,
        CORINFO_SWIFT_LOWERING* lowering)
    {
        var state = (LoweringState*)info;
        state->Queries++;
        lowering->byReference = cls == (CORINFO_CLASS_STRUCT_*)0x2000;
        lowering->numLoweredElements = 1;
        lowering->loweredElements[0] = CorInfoType.CORINFO_TYPE_LONG;
        lowering->offsets[0] = 4;
    }
}
#endif
