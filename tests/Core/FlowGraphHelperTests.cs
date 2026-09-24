// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class FlowGraphHelperTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void RootClassInitializationUsesDirectClassHandle(bool aot)
    {
        WithClassInitializationCompiler((compiler, ee) => {
            if (aot)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            }

            var call = (compiler.fgInitThisClass() ?? throw new InvalidOperationException()).AsCall();
            Assert.That(ee->LocatedMethod, Is.EqualTo((nint)compiler.info.compMethodHnd));
            Assert.That(compiler.lvaGenericsContextInUse, Is.False);
            if (aot)
            {
                Assert.That(call.HelperNum, Is.EqualTo(CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE));
                Assert.That((nint)ee->Token.hClass, Is.EqualTo((nint)compiler.info.compClassHnd));
                Assert.That(call.Args.CountArgs(), Is.Zero);
            }
            else
            {
                Assert.That(call.HelperNum, Is.EqualTo(CorInfoHelpFunc.CORINFO_HELP_INITCLASS));
                Assert.That(ee->SharedClass, Is.EqualTo((nint)compiler.info.compClassHnd));
                var argument = call.Args.GetArgByIndex(0) ?? throw new InvalidOperationException();
                Assert.That(argument.Node.AsIntCon().IconValue, Is.EqualTo((nint)compiler.info.compClassHnd));
                Assert.That(argument.Node.AsIntCon().IconHandleFlag, Is.EqualTo(Globals.GTF_ICON_CLASS_HDL));
            }
        });
    }

    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ, false)]
    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM, false)]
    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM, false)]
    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ, true)]
    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM, true)]
    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM, true)]
    public static void CoreClrRootClassInitializationReportsRuntimeContext(CORINFO_RUNTIME_LOOKUP_KIND kind, bool readyToRun)
    {
        WithClassInitializationCompiler((compiler, ee) => {
            ee->Lookup.needsRuntimeLookup = true;
            ee->Lookup.runtimeLookupKind = kind;
            if (readyToRun)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            }

            var call = (compiler.fgInitThisClass() ?? throw new InvalidOperationException()).AsCall();
            Assert.That(compiler.lvaGenericsContextInUse, Is.True);
            Assert.That(ee->ReadyToRunRequests, Is.Zero);
            Assert.That(call.Type, Is.EqualTo(Compiler.HelperInitClassRetType));
            var first = (call.Args.GetArgByIndex(0) ?? throw new InvalidOperationException()).Node;
            var expectedHelper = kind == CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM
                ? CorInfoHelpFunc.CORINFO_HELP_INITCLASS : CorInfoHelpFunc.CORINFO_HELP_INITINSTCLASS;
            Assert.That(call.HelperNum, Is.EqualTo(expectedHelper));
            Assert.That(call.Args.CountArgs(), Is.EqualTo(kind == CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM ? 1 : 2));

            GenTree context;
            switch (kind)
            {
                case CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ:
                {
                    Assert.That(first.Oper, Is.EqualTo(genTreeOps.GT_IND));
                    context = first.AsIndir().Addr;
                    var method = (call.Args.GetArgByIndex(1) ?? throw new InvalidOperationException()).Node.AsIntCon();
                    Assert.That(method.IconValue, Is.EqualTo((nint)compiler.info.compMethodHnd));
                    Assert.That(method.IconHandleFlag, Is.EqualTo(Globals.GTF_ICON_METHOD_HDL));
                    Assert.That(ee->EmbeddedMethod, Is.EqualTo((nint)compiler.info.compMethodHnd));
                    break;
                }

                case CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM:
                {
                    Assert.That(first.IsIntegralConst(0), Is.True);
                    context = (call.Args.GetArgByIndex(1) ?? throw new InvalidOperationException()).Node;
                    break;
                }

                default:
                {
                    context = first;
                    break;
                }
            }

            Assert.That(context.AsLclVar().LclNum, Is.EqualTo(kind == CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ ? 0 : 1));
            Assert.That(context.Flags & GenTreeFlags.GTF_VAR_CONTEXT, Is.EqualTo(GenTreeFlags.GTF_VAR_CONTEXT));
        });
    }

    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ, true)]
    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM, true)]
    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM, true)]
    [TestCase(CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM, false)]
    public static void NativeAotRootClassInitializationSelectsSharedTokenAndContext(CORINFO_RUNTIME_LOOKUP_KIND kind, bool shared)
    {
        WithClassInitializationCompiler((compiler, ee) => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            compiler.info.compClassAttr = shared ? CorInfoFlag.CORINFO_FLG_SHAREDINST : 0;
            ee->Abi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
            ee->Lookup.needsRuntimeLookup = true;
            ee->Lookup.runtimeLookupKind = kind;
            var call = (compiler.fgInitThisClass() ?? throw new InvalidOperationException()).AsCall();
            Assert.That(call.HelperNum, Is.EqualTo(shared ? CorInfoHelpFunc.CORINFO_HELP_READYTORUN_GENERIC_STATIC_BASE
                : CorInfoHelpFunc.CORINFO_HELP_READYTORUN_NONGCSTATIC_BASE));
            Assert.That(ee->ReadyToRunRequests, Is.EqualTo(1));
            Assert.That(ee->HelperMethod, Is.EqualTo((nint)compiler.info.compMethodHnd));
            Assert.That(call.Type, Is.EqualTo(var_types.TYP_BYREF));
            Assert.That((nint)call._entryPoint.addr, Is.EqualTo((nint)0x4000));
            Assert.That(call.Args.CountArgs(), Is.EqualTo(shared ? 1 : 0));
            Assert.That(compiler.lvaGenericsContextInUse, Is.EqualTo(shared));
            Assert.That((nint)ee->Token.hClass, Is.EqualTo(shared ? 0 : (nint)compiler.info.compClassHnd));
            Assert.That((nint)ee->Token.tokenContext, Is.EqualTo((nint)0));
            Assert.That((nint)ee->Token.tokenScope, Is.EqualTo((nint)0));
            Assert.That((nint)ee->Token.hMethod, Is.EqualTo((nint)0));
            Assert.That((nint)ee->Token.hField, Is.EqualTo((nint)0));
            Assert.That(ee->Token.token, Is.Zero);
            if (shared)
            {
                var context = (call.Args.GetArgByIndex(0) ?? throw new InvalidOperationException()).Node;
                if (kind == CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ)
                {
                    Assert.That(context.Oper, Is.EqualTo(genTreeOps.GT_IND));
                    context = context.AsIndir().Addr;
                }

                Assert.That(context.AsLclVar().LclNum, Is.EqualTo(kind == CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_THISOBJ ? 0 : 1));
                Assert.That(context.Flags & GenTreeFlags.GTF_VAR_CONTEXT, Is.EqualTo(GenTreeFlags.GTF_VAR_CONTEXT));
            }
        });
    }

    [Test]
    public static void RootClassInitializationPreservesReadyToRunHelperRejection()
    {
        WithClassInitializationCompiler((compiler, ee) => {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            ee->HelperAvailable = false;
            Assert.That(compiler.fgInitThisClass(), Is.Null);
            Assert.That(ee->ReadyToRunRequests, Is.EqualTo(1));
        });
    }

    [TestCase(1, 1)]
    [TestCase(1, 3)]
    [TestCase(3, 1)]
    public static void IRMeasurementCountsAllStatements(int blockCount, int statementsPerBlock)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags jitFlags = default;
        compiler.opts.jitFlags = &jitFlags;
        JitTls.Compiler = compiler;
#if DEBUG
        compiler.fgSafeBasicBlockCreation = true;
#endif

        try
        {
            BasicBlock? last = null;

            for (var i = 0; i < blockCount; i++)
            {
                var block = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);

                if (last is null)
                {
                    compiler.fgFirstBB = block;
                }
                else
                {
                    last.Next = block;
                }

                last = block;

                for (var j = 0; j < statementsPerBlock; j++)
                {
                    var statement = new Statement(compiler.gtNewNothingNode(), (i * statementsPerBlock) + j + 1);
                    compiler.fgInsertStmtAtEnd(block, statement);
                }
            }

            compiler.fgLastBB = last;

            Assert.That(compiler.fgMeasureIR(), Is.EqualTo(blockCount * statementsPerBlock));
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(false, 0)]
    [TestCase(false, 8)]
    [TestCase(false, -8)]
    [TestCase(true, 0)]
    [TestCase(true, 8)]
    [TestCase(true, -8)]
    public static void StackAddressesRemainNonHeapAfterPeeling(bool isReturnBuffer, int offset)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags jitFlags = default;
        compiler.opts.jitFlags = &jitFlags;
        compiler.lvaTable = [new LclVarDsc { Type = var_types.TYP_INT }];
        compiler.lvaCount = 1;
        compiler.info.compRetBuffArg = isReturnBuffer ? 0 : -1;
        JitTls.Compiler = compiler;

        try
        {
            GenTree address = isReturnBuffer
                ? compiler.gtNewLclVarNode(var_types.TYP_BYREF, 0)
                : compiler.gtNewLclVarAddrNode(var_types.TYP_BYREF, 0);

            if (offset != 0)
            {
                address = compiler.gtNewBinaryNode(genTreeOps.GT_ADD, var_types.TYP_BYREF,
                    address, compiler.gtNewIconNode(Globals.TYP_I_IMPL, offset));
            }

            Assert.That(compiler.fgAddrCouldBeHeap(address), Is.False);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(CorInfoHelpFunc.CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2, false)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2, true)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2_NOJITOPT, false)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_GETDYNAMIC_NONGCTHREADSTATIC_BASE_NOCTOR_OPTIMIZED2_NOJITOPT, true)]
    public static void PinnedStaticHelperResultTracksAsyncSuspensions(CorInfoHelpFunc helper, bool isAsync)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags jitFlags = default;
        compiler.opts.jitFlags = &jitFlags;

        if (isAsync)
        {
            jitFlags.Set(JitFlags.JIT_FLAG_ASYNC);
        }

        JitTls.Compiler = compiler;

        try
        {
            var call = compiler.fgGetStaticsCCtorHelper(null, helper, 7);
            var argument = call.Args.GetArgByIndex(0) ?? throw new InvalidOperationException("Missing static block index argument.");
            Assert.Multiple(() => {
                Assert.That(compiler.compIsAsync, Is.EqualTo(isAsync));
                Assert.That(call.Type, Is.EqualTo(isAsync ? var_types.TYP_BYREF : Globals.TYP_I_IMPL));
                Assert.That((call.Flags & GenTreeFlags.GTF_CALL_HOISTABLE) != 0, Is.True);
                Assert.That(argument.Node.AsIntCon().IconVal, Is.EqualTo((nint)7));
            });
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [TestCase(Compiler.AcdKeyDesignator.KD_TRY, (ushort)1, 1)]
    [TestCase(Compiler.AcdKeyDesignator.KD_HND, (ushort)2, 0x40000002)]
    [TestCase(Compiler.AcdKeyDesignator.KD_FLT, (ushort)3, int.MinValue + 3)]
    public static void ThrowHelperKeyDecodesRegion(Compiler.AcdKeyDesignator designator, ushort index, int data)
    {
        var key = new Compiler.AddCodeDscKey(new Compiler.AddCodeDsc {
            acdKind = SpecialCodeKind.SCK_RNGCHK_FAIL,
            acdKeyDsg = designator,
            acdTryIndex = index,
            acdHndIndex = index
        });

        Assert.Multiple(() => {
            Assert.That(key.Data, Is.EqualTo(data));
            Assert.That(key.Designator, Is.EqualTo(designator));
            Assert.That(key.RegionIndex, Is.EqualTo(index - 1));
            Assert.That(default(Compiler.AddCodeDscKey).Designator, Is.EqualTo(Compiler.AcdKeyDesignator.KD_NONE));
        });
    }

    private struct ClassInitializationEE
    {
        public ICorJitInfo Interface;
        public CORINFO_LOOKUP_KIND Lookup;
        public CORINFO_RUNTIME_ABI Abi;
        public CORINFO_RESOLVED_TOKEN Token;
        public nint LocatedMethod;
        public nint EmbeddedMethod;
        public nint SharedClass;
        public nint HelperMethod;
        public int ReadyToRunRequests;
        public bool HelperAvailable;
    }

    private delegate void ClassInitializationAction(Compiler compiler, ClassInitializationEE* ee);

    private static void WithClassInitializationCompiler(ClassInitializationAction action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)0x1000;
        compiler.info.compClassHnd = (CORINFO_CLASS_STRUCT_*)0x2000;
        compiler.info.compThisArg = 0;
        compiler.info.compTypeCtxtArg = 1;
        compiler.lvaTable = [new LclVarDsc { Type = var_types.TYP_REF }, new LclVarDsc { Type = Globals.TYP_I_IMPL }];
        compiler.lvaCount = 2;
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.getLocationOfThisType = &GetLocationOfThisType;
        vtable.Base.embedMethodHandle = &EmbedMethodHandle;
        vtable.Base.embedClassHandle = &EmbedClassHandle;
        vtable.Base.Base.getSharedCCtorHelper = &GetSharedCCtorHelper;
        vtable.Base.Base.getClassAttribs = &GetClassAttribs;
        vtable.Base.Base.getReadyToRunHelper = &GetReadyToRunHelper;
        vtable.Base.Base.getEEInfo = &GetEEInfo;
        ClassInitializationEE ee = new() {
            Interface = new ICorJitInfo { lpVtbl = &vtable },
            Abi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI,
            HelperAvailable = true,
        };
        compiler.info.compCompHnd = &ee.Interface;
        JitTls.Compiler = compiler;
        try
        {
            action(compiler, &ee);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetLocationOfThisType(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, CORINFO_LOOKUP_KIND* kind)
    {
        var ee = (ClassInitializationEE*)self;
        ee->LocatedMethod = (nint)method;
        *kind = ee->Lookup;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_STRUCT_* EmbedMethodHandle(ICorJitInfo* self, CORINFO_METHOD_STRUCT_* method, void** indirection)
    {
        ((ClassInitializationEE*)self)->EmbeddedMethod = (nint)method;
        *indirection = null;
        return method;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* EmbedClassHandle(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls, void** indirection)
    {
        *indirection = null;
        return cls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc GetSharedCCtorHelper(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls)
    {
        ((ClassInitializationEE*)self)->SharedClass = (nint)cls;
        return CorInfoHelpFunc.CORINFO_HELP_INITCLASS;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttribs(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* cls) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = default;
        info->targetAbi = ((ClassInitializationEE*)self)->Abi;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte GetReadyToRunHelper(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* token, CorInfoHelpFunc helper,
        CORINFO_METHOD_STRUCT_* method, CORINFO_CONST_LOOKUP* lookup)
    {
        var ee = (ClassInitializationEE*)self;
        ee->Token = *token;
        ee->HelperMethod = (nint)method;
        ee->ReadyToRunRequests++;
        *lookup = default;
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x4000;
        return ee->HelperAvailable ? (byte)1 : (byte)0;
    }
}
