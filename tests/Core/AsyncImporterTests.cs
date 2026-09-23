// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AsyncImporterTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void SynthesizedUserCallsQueryEntrypointsOnlyForReadyToRun(bool readyToRun)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getFunctionEntryPoint = &GetAwaitEntryPoint;
            vtable.Base.Base.getEEInfo = &GetEEInfo;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var stateValue = new AwaitReturnState();
            var state = &stateValue;

            if (readyToRun)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            }

            var call = compiler.gtNewUserCallNode(var_types.TYP_INT, (CORINFO_METHOD_STRUCT_*)state);

            Assert.That(state->EntryPointQueries, Is.EqualTo(readyToRun ? 1 : 0));

            if (readyToRun)
            {
                Assert.That(call._entryPoint.accessType, Is.EqualTo(InfoAccessType.IAT_PVALUE));
                Assert.That((nint)call._entryPoint.addr, Is.EqualTo((nint)0x4321));
            }
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public static void StaticMonitorObjectsUseTheExactClassContext(int lookupKind)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getLocationOfThisType = &GetThisTypeLocation;
            vtable.Base.Base.getRuntimeTypePointer = &GetRuntimeTypePointer;
            vtable.Base.embedClassHandle = &EmbedMonitorClass;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            var stateValue = new MonitorState { LookupKind = lookupKind };
            var state = &stateValue;
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)state;
            compiler.info.compClassHnd = (CORINFO_CLASS_STRUCT_*)state;
            compiler.info.compFlags = CorInfoFlag.CORINFO_FLG_STATIC;
            compiler.info.compIsStatic = true;
            compiler.info.compTypeCtxtArg = 0;
            compiler.lvaTable[0].Type = Globals.TYP_I_IMPL;
            compiler.opts.SetMinOpts(false);

            var lockObject = compiler.fgGetCritSectOfStaticMethod();

            Assert.That(lockObject.Type, Is.EqualTo(var_types.TYP_REF));
            Assert.That(compiler.lvaGenericsContextInUse, Is.EqualTo(lookupKind >= 2));

            if (lookupKind == 0)
            {
                Assert.That(lockObject.AsIntCon().IconValue, Is.EqualTo((nint)0x5678));
                Assert.That(lockObject.AsIntCon().IsIconHandle(Globals.GTF_ICON_OBJ_HDL), Is.True);

                return;
            }

            Assert.That(lockObject.AsCall().HelperNum, Is.EqualTo(CorInfoHelpFunc.CORINFO_HELP_GETSYNCFROMCLASSHANDLE));
            var classHandle = lockObject.AsCall().Args.GetUserArgByIndex(0)?.Node
                ?? throw new InvalidOperationException("Missing monitor class.");

            if (lookupKind == 1)
            {
                Assert.That(classHandle.AsIntCon().IconValue, Is.EqualTo((nint)state));
                Assert.That(classHandle.AsIntCon().IsIconHandle(Globals.GTF_ICON_CLASS_HDL), Is.True);
            }
            else
            {
                if (lookupKind == 3)
                {
                    Assert.That(classHandle.AsCall().HelperNum, Is.EqualTo(CorInfoHelpFunc.CORINFO_HELP_GETCLASSFROMMETHODPARAM));
                    classHandle = classHandle.AsCall().Args.GetUserArgByIndex(0)?.Node
                        ?? throw new InvalidOperationException("Missing method context.");
                }

                Assert.That(classHandle.AsLclVar().LclNum, Is.Zero);
                Assert.That((classHandle.Flags & GenTreeFlags.GTF_VAR_CONTEXT) != 0, Is.True);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NopBashingPreservesIdentityAndRemovesOperandUses(bool callNode)
    {
        WithCompiler(compiler => {
            GenTree node;
            var child = compiler.gtNewIconNode(var_types.TYP_INT, 1);

            if (callNode)
            {
                var call = compiler.gtNewCallNode(var_types.TYP_INT, gtCallTypes.CT_USER_FUNC, (CORINFO_METHOD_STRUCT_*)1);
                call.Args.PushBack(NewCallArg.CreateForPrimitive(child));
                node = call;
            }
            else
            {
                node = compiler.gtNewUnaryNode(genTreeOps.GT_NEG, var_types.TYP_INT, child);
            }

            node.Flags |= GenTreeFlags.GTF_DONT_CSE | GenTreeFlags.GTF_REVERSE_OPS;
            node._vnPair.SetBoth(42);
            node.Next = child;
            child.Prev = node;
            var originalUses = node.Operands.GetEnumerator();
            Assert.That(originalUses.MoveNext(), Is.True);
            Assert.That(originalUses.Current, Is.SameAs(child));
            Assert.That(originalUses.MoveNext(), Is.False);
            var leafUses = child.Operands.GetEnumerator();
            Assert.That(leafUses.MoveNext(), Is.False);
#if DEBUG
            var nextTreeId = compiler.compGenTreeID;
            var treeId = node.TreeId;
#endif

            node.BashToNOP();

            Assert.That(node.Type, Is.EqualTo(var_types.TYP_VOID));
            Assert.That(node.Oper, Is.EqualTo(genTreeOps.GT_NOP));
            Assert.That(node.Flags, Is.EqualTo(GenTreeFlags.GTF_DONT_CSE));
            Assert.That(node._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(node._vnPair.Conservative, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(node.Next, Is.SameAs(child));
            Assert.That(child.Prev, Is.SameAs(node));
            var uses = node.Operands.GetEnumerator();
            Assert.That(uses.MoveNext(), Is.False);
#if DEBUG
            Assert.That(node.TreeId, Is.EqualTo(treeId));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextTreeId));
#endif
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void AsyncVersionReturnsUnwrapTheTaskAndPreserveHiddenArguments(bool returnsVoid, bool generic)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getAwaitReturnCall = &GetAwaitReturnCall;
            vtable.Base.Base.getMethodSig = &GetAwaitSignature;
            vtable.Base.Base.getArgType = &GetTaskArgumentType;
            vtable.Base.Base.getMethodAttribs = &GetAwaitMethodFlags;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            var stateValue = new AwaitReturnState { ReturnsVoid = returnsVoid, Generic = generic };
            var state = &stateValue;
            CORINFO_METHOD_INFO methodInfo = default;
            methodInfo.options = CorInfoOptions.CORINFO_ASYNC_VERSION;
            compiler.info.compMethodInfo = &methodInfo;
            compiler.info.compMethodHnd = (CORINFO_METHOD_STRUCT_*)state;
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compRetType = returnsVoid ? var_types.TYP_VOID : var_types.TYP_INT;
            compiler.info.compRetBuffArg = Globals.BAD_VAR_NUM;
            compiler.opts.SetMinOpts(false);
            compiler.opts.compTailCallOpt = true;
            compiler.compInlineContext = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
            PrepareStack(compiler);
            var task = compiler.gtNewNull();
            task.Flags |= GenTreeFlags.GTF_ORDER_SIDEEFF;
            compiler.impPushOnStack(task, new typeInfo(var_types.TYP_REF));
            var opcode = OPCODE.CEE_RET;

            Assert.That(compiler.impReturnInstruction(0, ref opcode), Is.True);
            var returnStatement = compiler.impExtractLastStmt();
            var returnTree = returnStatement.RootNode;
            Assert.That(returnTree.Oper, Is.EqualTo(genTreeOps.GT_RETURN));
            var call = returnsVoid
                ? (returnStatement.PrevStmt ?? throw new InvalidOperationException("Missing await statement.")).RootNode.AsCall()
                : returnTree.AsUnOp().Op1.AsCall();
            Assert.Multiple(() => {
                Assert.That(state->AwaitQueries, Is.EqualTo(1));
                Assert.That(state->SignatureQueries, Is.EqualTo(1));
                Assert.That(compiler.stackState.esStackDepth, Is.Zero);
                Assert.That(call.IsAsync, Is.True);
                Assert.That(call.GetAsyncInfo().IsTailAwait, Is.True);
                Assert.That(call.IsImplicitTailCall, Is.True);
                Assert.That((call.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.True);
                Assert.That(call.Args.GetUserArgByIndex(0)?.Node, Is.SameAs(task));
                Assert.That(call.Args.FindWellKnownArg(WellKnownArg.AsyncContinuation)?.Node.IsIntegralConst(0), Is.True);
                Assert.That(call.Args.FindWellKnownArg(WellKnownArg.InstParam) is not null, Is.EqualTo(generic));
            });

            if (generic)
            {
                var instParam = call.Args.FindWellKnownArg(WellKnownArg.InstParam)
                    ?? throw new InvalidOperationException("Missing instantiation argument.");
                Assert.That(instParam.Node.AsIntCon().IconValue, Is.EqualTo((nint)0x1234));
            }

            List<WellKnownArg> argumentKinds = [];

            foreach (var argument in call.Args.Args)
            {
                argumentKinds.Add(argument.WellKnownArg);
            }

            List<WellKnownArg> expectedKinds = [WellKnownArg.AsyncContinuation];

            if (generic)
            {
                expectedKinds.Insert(0, WellKnownArg.InstParam);
            }

            if (Target.TgtArgOrder == Target.ARG_ORDER_R2L)
            {
                expectedKinds.Add(WellKnownArg.None);
            }
            else
            {
                expectedKinds.Reverse();
                expectedKinds.Insert(0, WellKnownArg.None);
            }

            Assert.That(argumentKinds, Is.EqualTo(expectedKinds));
        });
    }

    [TestCase(var_types.TYP_VOID)]
    [TestCase(var_types.TYP_INT)]
    [TestCase(var_types.TYP_REF)]
    [TestCase(var_types.TYP_DOUBLE)]
    public static void DefaultValueTaskReturnsFoldWithoutChangingStatementIdentity(var_types returnType)
    {
        WithCompiler(compiler => {
            PrepareStack(compiler);
            compiler.opts.SetMinOpts(false);
            compiler.info.compRetType = returnType;
            compiler.lvaTable[0].Type = var_types.TYP_STRUCT;
            var store = new GenTreeLclVar(var_types.TYP_STRUCT, 0, compiler.gtNewIconNode(var_types.TYP_INT, 0));
            store.Flags |= GenTreeFlags.GTF_ASG | GenTreeFlags.GTF_DONT_CSE;
            var statement = compiler.gtNewStmt(store);
            compiler.impAppendStmt(statement);
            compiler.impPushOnStack(compiler.gtNewLclvNode(var_types.TYP_STRUCT, 0), new typeInfo(var_types.TYP_STRUCT));
#if DEBUG
            var treeId = store.TreeId;
#endif

            Assert.That(compiler.impFoldAwaitedTopOfStack(), Is.True);
            Assert.Multiple(() => {
                Assert.That(compiler.impExtractLastStmt(), Is.SameAs(statement));
                Assert.That(statement.RootNode, Is.SameAs(store));
                Assert.That(store.Oper, Is.EqualTo(genTreeOps.GT_NOP));
                Assert.That(store.Type, Is.EqualTo(var_types.TYP_VOID));
                Assert.That(store.Flags & GenTreeFlags.GTF_ALL_EFFECT, Is.EqualTo(GenTreeFlags.GTF_EMPTY));
                Assert.That((store.Flags & GenTreeFlags.GTF_DONT_CSE) != 0, Is.True);
                Assert.That(compiler.stackState.esStackDepth, Is.EqualTo(returnType == var_types.TYP_VOID ? 0 : 1));
#if DEBUG
                Assert.That(store.TreeId, Is.EqualTo(treeId));
#endif
            });

            if (returnType != var_types.TYP_VOID)
            {
                var result = compiler.impStackTop().val;
                Assert.That(result.Type, Is.EqualTo(returnType));
                Assert.That(returnType == var_types.TYP_DOUBLE ? result.AsDblCon().IsPositiveZero : result.IsIntegralConst(0), Is.True);
            }
        });
    }

    [TestCase(true, 0, 1)]
    [TestCase(true, 1, 0)]
    [TestCase(false, 0, 0)]
    public static void UnfoldedValueTasksLeaveTheStackAndStoreUnchanged(bool optimize, int storeLocal, int value)
    {
        WithCompiler(compiler => {
            PrepareStack(compiler);
            compiler.opts.compDbgCode = !optimize;
            compiler.opts.SetMinOpts(false);
            compiler.info.compRetType = var_types.TYP_INT;
            compiler.lvaTable[0].Type = var_types.TYP_STRUCT;
            var store = new GenTreeLclVar(var_types.TYP_STRUCT, storeLocal, compiler.gtNewIconNode(var_types.TYP_INT, value));
            compiler.impAppendStmt(compiler.gtNewStmt(store));
            var task = compiler.gtNewLclvNode(var_types.TYP_STRUCT, 0);
            compiler.impPushOnStack(task, new typeInfo(var_types.TYP_STRUCT));

            Assert.That(compiler.impFoldAwaitedTopOfStack(), Is.False);
            Assert.Multiple(() => {
                Assert.That(compiler.impStackTop().val, Is.SameAs(task));
                Assert.That(compiler.stackState.esStackDepth, Is.EqualTo(1));
                Assert.That(store.Oper, Is.EqualTo(genTreeOps.GT_STORE_LCL_VAR));
            });
        });
    }

    [TestCase(new byte[] { 0x0A, 0x12, 0 }, 0)]
    [TestCase(new byte[] { 0x0B, 0xFE, 0x0D, 1, 0 }, 1)]
    [TestCase(new byte[] { 0x0C, 0x12, 2 }, 2)]
    [TestCase(new byte[] { 0x0D, 0xFE, 0x0D, 3, 0 }, 3)]
    [TestCase(new byte[] { 0x13, 255, 0x12, 255 }, 255)]
    [TestCase(new byte[] { 0x13, 255, 0xFE, 0x0D, 255, 0 }, 255)]
    [TestCase(new byte[] { 0xFE, 0x0E, 0, 1, 0xFE, 0x0D, 0, 1 }, 256)]
    [TestCase(new byte[] { 0xFE, 0x0E, 255, 255, 0xFE, 0x0D, 255, 255 }, 65535)]
    [TestCase(new byte[] { 0xFE, 0x0E, 3, 0, 0x12, 3 }, 3)]
    public static void LocalAddressPatternsCheckEveryTruncationAndPreserveTheCursor(byte[] pattern, int expectedLocal)
    {
        var bytes = new byte[pattern.Length + 1];
        pattern.CopyTo(bytes, 0);

        fixed (byte* start = bytes)
        {
            for (var length = 0; length <= bytes.Length; length++)
            {
                var cursor = start;
                var matched = Compiler.impMatchStlocLdloca(ref cursor, start + length, out var local);
                var expectedMatch = length >= pattern.Length;
                Assert.That(matched, Is.EqualTo(expectedMatch), $"Input length {length}");
                Assert.That(local, Is.EqualTo(expectedMatch ? expectedLocal : Globals.BAD_VAR_NUM));
                Assert.That(cursor - start, Is.EqualTo(expectedMatch ? (long)pattern.Length : 0L));
            }
        }

        const int configuredAwaitLength = 1 + (2 * (1 + sizeof(int)));
        var awaitBytes = new byte[sizeof(int) + pattern.Length + configuredAwaitLength];
        pattern.CopyTo(awaitBytes, sizeof(int));

        fixed (byte* start = awaitBytes)
        {
            var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            var result = compiler.impMatchTaskAwaitPattern(start, start + awaitBytes.Length, out var config, out var awaitOffset);
            Assert.That((nint)result, Is.EqualTo((nint)0));
            Assert.That(config, Is.EqualTo(-1));
            Assert.That(awaitOffset, Is.EqualTo(Globals.BAD_IL_OFFSET));
        }
    }

    [TestCase(new byte[] { 0x0A, 0x12, 1 })]
    [TestCase(new byte[] { 0xFE, 0x0E, 255, 255, 0x12, 255 })]
    [TestCase(new byte[] { 0x13, 255, 0xFE, 0x0D, 255, 1 })]
    [TestCase(new byte[] { 0xFE, 0x0D, 1, 0, 0x12, 1 })]
    [TestCase(new byte[] { 0x0B, 0xFE, 0x0C, 1, 0 })]
    [TestCase(new byte[] { 0x06, 0x12, 0 })]
    public static void LocalAddressPatternsRejectMismatchedLocalsAndOpcodes(byte[] bytes)
    {
        fixed (byte* start = bytes)
        {
            var cursor = start;
            var matched = Compiler.impMatchStlocLdloca(ref cursor, start + bytes.Length, out var local);
            Assert.That(matched, Is.False);
            Assert.That(local, Is.EqualTo(Globals.BAD_VAR_NUM));
            Assert.That(cursor - start, Is.EqualTo(0L));
        }
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public static void TailAwaitsInheritTheWholeContextChain(int outerFrames)
    {
        WithCompiler(compiler => {
            var inliningCall = new GenTreeCall(var_types.TYP_VOID);
            List<ContinuationContextHandling> handling = [];
            inliningCall.SetIsAsync(new AsyncCallInfo { InlineFrameContextHandling = handling });
            List<CallArg> inherited = [];

            AddArgument(WellKnownArg.AsyncResumedDef, compiler.gtNewLclAddrNode(var_types.TYP_BYREF, 0, 0));
            AddArgument(WellKnownArg.AsyncResumedUse, compiler.gtNewLclvNode(var_types.TYP_INT, 0));
            AddArgument(WellKnownArg.AsyncExecutionContext, compiler.gtNewLclvNode(var_types.TYP_REF, 1));
            AddArgument(WellKnownArg.AsyncSynchronizationContext, compiler.gtNewLclvNode(var_types.TYP_REF, 2));

            for (var frame = 1; frame <= outerFrames; frame++)
            {
                handling.Add(ContinuationContextHandling.ContinueOnThreadPool);
                AddArgument(WellKnownArg.AsyncResumedUse, compiler.gtNewLclvNode(var_types.TYP_INT, frame * 3));
                AddArgument(WellKnownArg.AsyncExecutionContext, compiler.gtNewLclvNode(var_types.TYP_REF, (frame * 3) + 1));
                AddArgument(WellKnownArg.AsyncSynchronizationContext, compiler.gtNewLclvNode(var_types.TYP_REF, (frame * 3) + 2));
            }

            inherited[0].Node.Flags |= GenTreeFlags.GTF_ORDER_SIDEEFF;
            inherited[^1].Node.Flags |= GenTreeFlags.GTF_GLOB_REF;

            compiler.opts.compFlags = Globals.CLFLG_INLINING;
            compiler.impInlineInfo = new InlineInfo {
                InlineRoot = compiler,
                InlinerCompiler = compiler,
                iciCall = inliningCall,
            };
            var call = new GenTreeCall(var_types.TYP_VOID);
            call.SetIsAsync(default);
            var userArgument = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(var_types.TYP_INT, 42)));

            compiler.impInheritAsyncContextsFromInliner(call);

            List<CallArg> arguments = [];

            foreach (var argument in call.Args.Args)
            {
                arguments.Add(argument);
            }

            Assert.Multiple(() => {
                Assert.That(arguments.Count, Is.EqualTo(inherited.Count + 1));
                Assert.That((call.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.True);
                Assert.That((call.Flags & GenTreeFlags.GTF_GLOB_REF) != 0, Is.True);
                Assert.That(call.GetAsyncInfo().InlineFrameContextHandling, Is.SameAs(handling));
            });
            Assert.That(arguments[4], Is.SameAs(userArgument));
            arguments.RemoveAt(4);

            for (var index = 0; index < inherited.Count; index++)
            {
                Assert.Multiple(() => {
                    Assert.That(arguments[index].WellKnownArg, Is.EqualTo(inherited[index].WellKnownArg));
                    Assert.That(arguments[index].Node, Is.Not.SameAs(inherited[index].Node));
                    Assert.That(arguments[index].Node.Oper, Is.EqualTo(inherited[index].Node.Oper));
                    Assert.That(arguments[index].Node.AsLclVarCommon().LclNum, Is.EqualTo(inherited[index].Node.AsLclVarCommon().LclNum));
                });
            }

            void AddArgument(WellKnownArg kind, GenTree node)
            {
                inherited.Add(inliningCall.Args.PushBack(NewCallArg.CreateForPrimitive(node).WithWellKnownArg(kind)));
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CallsWithoutInheritedContextsKeepTheirArguments(bool inlining)
    {
        WithCompiler(compiler => {
            if (inlining)
            {
                compiler.opts.compFlags = Globals.CLFLG_INLINING;
                var inliningCall = new GenTreeCall(var_types.TYP_VOID);
                inliningCall.SetIsAsync(default);
                compiler.impInlineInfo = new InlineInfo { iciCall = inliningCall };
            }

            var call = new GenTreeCall(var_types.TYP_VOID);
            call.SetIsAsync(default);
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewNull()));

            compiler.impInheritAsyncContextsFromInliner(call);

            Assert.That(call.Args.Head, Is.SameAs(argument));
            Assert.That(call.GetAsyncInfo().InlineFrameContextHandling, Is.Null);
        });
    }

    private static void PrepareStack(Compiler compiler)
    {
        compiler.compCurBB = new BasicBlock(null, null);
        compiler.info.compMaxStack = 4;
        compiler.stackState.esStack = new StackEntry[4];
    }

    private struct AwaitReturnState
    {
        public bool ReturnsVoid;
        public bool Generic;
        public int AwaitQueries;
        public int SignatureQueries;
        public int EntryPointQueries;
    }

    private struct MonitorState
    {
        public int LookupKind;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetAwaitEntryPoint(
        ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method, CORINFO_CONST_LOOKUP* lookup, CORINFO_ACCESS_FLAGS flags)
    {
        ((AwaitReturnState*)method)->EntryPointQueries++;
        *lookup = default;
        lookup->accessType = InfoAccessType.IAT_PVALUE;
        lookup->addr = (void*)0x4321;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetEEInfo(ICorJitInfo* jitInfo, CORINFO_EE_INFO* info)
    {
        *info = default;
        info->targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetThisTypeLocation(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method, CORINFO_LOOKUP_KIND* kind)
    {
        var lookupKind = ((MonitorState*)method)->LookupKind;
        *kind = default;
        kind->needsRuntimeLookup = lookupKind >= 2;
        kind->runtimeLookupKind = lookupKind == 2
            ? CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_CLASSPARAM
            : CORINFO_RUNTIME_LOOKUP_KIND.CORINFO_LOOKUP_METHODPARAM;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_OBJECT_STRUCT_* GetRuntimeTypePointer(ICorJitInfo* jitInfo, CORINFO_CLASS_STRUCT_* cls)
    {
        return ((MonitorState*)cls)->LookupKind == 0 ? (CORINFO_OBJECT_STRUCT_*)0x5678 : null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* EmbedMonitorClass(ICorJitInfo* jitInfo, CORINFO_CLASS_STRUCT_* cls, void** indirection)
    {
        *indirection = null;

        return cls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_METHOD_STRUCT_* GetAwaitReturnCall(
        ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* caller, CORINFO_CONTEXT_STRUCT_** context, CORINFO_LOOKUP* lookup)
    {
        ((AwaitReturnState*)caller)->AwaitQueries++;
        *context = Globals.MAKE_METHODCONTEXT(caller);
        *lookup = default;
        lookup->constLookup.accessType = InfoAccessType.IAT_VALUE;
        lookup->constLookup.handle = (CORINFO_GENERIC_STRUCT_*)0x1234;

        return caller;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetAwaitSignature(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method, CORINFO_SIG_INFO* sig, CORINFO_CLASS_STRUCT_* owner)
    {
        var state = (AwaitReturnState*)method;
        state->SignatureQueries++;
        *sig = default;
        sig->retType = state->ReturnsVoid ? CorInfoType.CORINFO_TYPE_VOID : CorInfoType.CORINFO_TYPE_INT;
        sig->callConv = CorInfoCallConv.CORINFO_CALLCONV_ASYNCCALL;

        if (state->Generic)
        {
            sig->callConv |= CorInfoCallConv.CORINFO_CALLCONV_PARAMTYPE;
        }

        sig->numArgs = 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoTypeWithMod GetTaskArgumentType(
        ICorJitInfo* jitInfo, CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_STRUCT_* arg, CORINFO_CLASS_STRUCT_** type)
    {
        *type = null;

        return (CorInfoTypeWithMod)CorInfoType.CORINFO_TYPE_CLASS;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetAwaitMethodFlags(ICorJitInfo* jitInfo, CORINFO_METHOD_STRUCT_* method)
    {
        return CorInfoFlag.CORINFO_FLG_STATIC;
    }

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var jitTls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.info = new Compiler.Info();
        compiler.lvaTable = new LclVarDsc[9];
        compiler.lvaCount = compiler.lvaTable.Length;

        for (var index = 0; index < compiler.lvaCount; index++)
        {
            compiler.lvaTable[index].Type = index % 3 == 0 ? var_types.TYP_INT : var_types.TYP_REF;
        }

        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
