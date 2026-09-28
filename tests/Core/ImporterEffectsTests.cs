// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class ImporterEffectsTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void EarlyIsInstPreservesNativeNodeConstructionOrder(bool booleanCheck)
    {
        WithCompiler(compiler =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.isExactType = &IsExactType;
            vtable.Base.Base.getClassAttribs = &GetClassAttributes;
            vtable.Base.Base.getTypeForBox = &GetTypeForBox;
            vtable.Base.Base.getCastingHelper =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, bool, CorInfoHelpFunc>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_RESOLVED_TOKEN*, byte, CorInfoHelpFunc>)&GetCastingHelper;
            vtable.Base.Base.getExactClasses = &GetExactClasses;
            vtable.Base.Base.runWithSPMIErrorTrap = &UnavailableClassName;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.opts.SetMinOpts(false);
            var block = new BasicBlock(null, null);
            block.setBBProfileWeight(100);
            compiler.compCurBB = block;
            var instance = compiler.gtNewLclvNode(var_types.TYP_REF, 0);
            var target = compiler.gtNewIconHandleNode(0x1000, Globals.GTF_ICON_CLASS_HDL);
            CORINFO_RESOLVED_TOKEN token = new() { hClass = (CORINFO_CLASS_STRUCT_*)0x1000 };

            var result = compiler.impCastClassOrIsInstToTree(instance, target, ref token, false, ref booleanCheck, 0);
            var statement = ImporterStatements(compiler) ?? throw new AssertionException("Missing isinst spill.");
            var outer = statement.RootNode.AsLclVar().Data.AsQmark();
            var inner = outer.ElseNode.AsQmark();
            var nullCheck = outer.Cond.AsOp();

            Assert.That(result.Type, Is.EqualTo(booleanCheck ? var_types.TYP_INT : var_types.TYP_REF));
            Assert.That(nullCheck.Op1.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
            Assert.That(nullCheck.Op2.IsIntegralConst(0), Is.True);
            Assert.That(outer.ThenNode.IsIntegralConst(0), Is.True);
            Assert.That(inner.ThenNode.IsIntegralConst(0), Is.True);
            Assert.That(inner.ElseNode.Oper, Is.EqualTo(booleanCheck ? genTreeOps.GT_CNS_INT : genTreeOps.GT_LCL_VAR));
#if DEBUG
            Assert.That(nullCheck.Op2.TreeId + 1, Is.EqualTo(nullCheck.Op1.TreeId));
            Assert.That(inner.ElseNode.TreeId + 1, Is.EqualTo(inner.ThenNode.TreeId));
#endif
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "impStmtList")]
    private static extern ref Statement? ImporterStatements(Compiler compiler);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsExactType(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoFlag GetClassAttributes(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetTypeForBox(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type) => type;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoHelpFunc GetCastingHelper(ICorJitInfo* self, CORINFO_RESOLVED_TOKEN* token, byte throwing)
        => CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFCLASS;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetExactClasses(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* type, int count, CORINFO_CLASS_STRUCT_** classes)
        => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte UnavailableClassName(ICorJitInfo* self, delegate* unmanaged[Cdecl]<void*, void> callback, void* state)
        => 0;

    [Test]
    public static void StructArgumentNormalizationSinksCommaIntoTheBlockAddress(
        [Values(var_types.TYP_BYREF, Globals.TYP_I_IMPL)] var_types addressType,
        [Values(false, true)] bool withCall)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = addressType;
            var address = compiler.gtNewLclvNode(addressType, 0);
            var layout = new ClassLayout(8);
            var block = compiler.gtNewBlkIndir(address, layout);
            Assert.That(compiler.impNormStructVal(block, Compiler.CHECK_SPILL_ALL), Is.SameAs(block));

            var effect = withCall
                ? compiler.gtNewCallNode(var_types.TYP_VOID, gtCallTypes.CT_USER_FUNC, null)
                : compiler.gtNewNothingNode();
            var comma = compiler.gtNewCommaNode(var_types.TYP_STRUCT, effect, block);

            var result = compiler.impNormStructVal(comma, Compiler.CHECK_SPILL_ALL);

            Assert.That(result, Is.SameAs(block));
            Assert.That(block.Type, Is.EqualTo(var_types.TYP_STRUCT));
            Assert.That(block.Layout, Is.SameAs(layout));
            Assert.That(block.Addr, Is.SameAs(comma));
            Assert.That(comma.Type, Is.EqualTo(addressType));
            Assert.That(comma.Op1, Is.SameAs(effect));
            Assert.That(comma.Op2, Is.SameAs(address));
            Assert.That((block.Flags & GenTreeFlags.GTF_CALL) != 0, Is.EqualTo(withCall));
        });
    }

    [Test]
    public static void StringReferenceEqualityPreservesNativeCloneOrder(
        [Values(false, true)] bool constantFirst,
        [Values(false, true)] bool isStatic)
    {
        SsaLivenessTests.WithCompiler(2, compiler => {
            compiler.opts.SetMinOpts(false);
            compiler.compCurBB = new BasicBlock(null, null);
            compiler.compCurBB.setBBProfileWeight(100);
            compiler.lvaTable[0].Type = var_types.TYP_REF;
            compiler.info.compMaxStack = 2;
            compiler.info.compRetBuffArg = Globals.BAD_VAR_NUM;
            compiler.stackState.esStack = new StackEntry[2];
            var value = compiler.gtNewLclvNode(var_types.TYP_REF, 0);
            var literal = new GenTreeStrCon(Globals.EMPTY_STRING_SCON, null);
            compiler.impPushOnStack(constantFirst ? literal : value, new typeInfo());
            compiler.impPushOnStack(constantFirst ? value : literal, new typeInfo());
            CORINFO_SIG_INFO signature = new() { numArgs = (ushort)(isStatic ? 2 : 1) };

            var result = compiler.impUtf16StringComparison(Compiler.StringComparisonKind.Equals,
                signature, isStatic ? CorInfoFlag.CORINFO_FLG_STATIC : 0);

            Assert.That(result, Is.Not.Null);
            Assert.That(compiler.stackState.esStackDepth, Is.Zero);
            var statement = ImporterStatements(compiler) ?? throw new AssertionException("Missing string spill.");
            var comparison = statement.NextStmt?.RootNode.AsLclVar().Data.AsQmark().Cond.AsOp()
                ?? throw new AssertionException("Missing reference equality comparison.");
            Assert.That(comparison.Oper, Is.EqualTo(genTreeOps.GT_EQ));
            Assert.That(comparison.Op1.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
            Assert.That(comparison.Op2.Oper, Is.EqualTo(genTreeOps.GT_CNS_STR));
#if DEBUG
            Assert.That(comparison.Op2.TreeId + 1, Is.EqualTo(comparison.Op1.TreeId));
#endif
        });
    }

    [Test]
    public static void ConstantStringComparisonUsesExactChunkWidths(
        [Values("AbC", "AbC-d", "AbC-deF", "AbC-deFGh")] string value,
        [Values(StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase)] StringComparison comparison)
    {
        WithCompiler(compiler => {
            compiler.opts.SetMinOpts(false);
            compiler.lvaTable[0].Type = var_types.TYP_BYREF;
            var data = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var characters = value.ToCharArray();

            var result = compiler.impExpandHalfConstEquals(data, characters, 0, comparison)
                ?? throw new AssertionException("ASCII comparison was not expanded.");

            Assert.That(result.Type, Is.EqualTo(var_types.TYP_INT));
            Assert.That(new string(characters),
                Is.EqualTo(comparison == StringComparison.OrdinalIgnoreCase ? "abc-defgh"[..value.Length] : value));
        });
    }

    [Test]
    public static void MultiplicationImportPreservesOverflowAndUnsignedFlags(
        [Values(OPCODE.CEE_MUL, OPCODE.CEE_MUL_OVF, OPCODE.CEE_MUL_OVF_UN)] OPCODE opcode,
        [Values(var_types.TYP_INT, var_types.TYP_LONG)] var_types type)
    {
        WithCompiler(compiler => {
            compiler.opts.SetMinOpts(false);
            compiler.info.compMaxStack = 2;
            compiler.info.compRetBuffArg = Globals.BAD_VAR_NUM;
            compiler.lvaTable[0].Type = type;
            compiler.lvaTable[1].Type = type;
            compiler.stackState.esStack = new StackEntry[2];
            var block = new BasicBlock(null, null) { bbCodeOffsEnd = 1 };
            compiler.fgFirstBB = compiler.compCurBB = block;
            var left = compiler.gtNewLclvNode(type, 0);
            var right = compiler.gtNewLclvNode(type, 1);
            compiler.impPushOnStack(left, new typeInfo());
            compiler.impPushOnStack(right, new typeInfo());
            var code = (byte)opcode;
            compiler.info.compCode = &code;
            compiler.info.compILCodeSize = 1;

            compiler.impImportBlockCode(block);

            var result = compiler.impStackTop().val.AsOp();
            var checkedMultiply = opcode != OPCODE.CEE_MUL;
            Assert.Multiple(() => {
                Assert.That(compiler.stackState.esStackDepth, Is.EqualTo(1));
                Assert.That(result.Oper, Is.EqualTo(genTreeOps.GT_MUL));
                Assert.That(result.Type, Is.EqualTo(type));
                Assert.That(result.Op1, Is.SameAs(left));
                Assert.That(result.Op2, Is.SameAs(right));
                Assert.That(result.IsUnsigned, Is.EqualTo(opcode == OPCODE.CEE_MUL_OVF_UN));
                Assert.That((result.Flags & GenTreeFlags.GTF_OVERFLOW) != 0, Is.EqualTo(checkedMultiply));
                Assert.That((result.Flags & GenTreeFlags.GTF_EXCEPT) != 0, Is.EqualTo(checkedMultiply));
            });
        });
    }

    [Test]
    public static void NativeIntegerComparisonsValidateTheWidenedOperands(
        [Values(OPCODE.CEE_CEQ, OPCODE.CEE_CGT, OPCODE.CEE_CGT_UN, OPCODE.CEE_CLT, OPCODE.CEE_CLT_UN,
            OPCODE.CEE_BEQ_S, OPCODE.CEE_BGT_UN_S, OPCODE.CEE_BLT_S)] OPCODE opcode,
        [Values(false, true)] bool nativeFirst)
    {
        WithCompiler(compiler => {
            compiler.opts.SetMinOpts(true);
            compiler.info.compMaxStack = 2;
            compiler.info.compRetBuffArg = Globals.BAD_VAR_NUM;
            compiler.stackState.esStack = new StackEntry[2];
            compiler.lvaTable[0].Type = nativeFirst ? Globals.TYP_I_IMPL : var_types.TYP_INT;
            compiler.lvaTable[1].Type = nativeFirst ? var_types.TYP_INT : Globals.TYP_I_IMPL;
            var left = compiler.gtNewLclvNode(compiler.lvaTable[0].Type, 0);
            var right = compiler.gtNewLclvNode(compiler.lvaTable[1].Type, 1);
            compiler.impPushOnStack(left, new typeInfo());
            compiler.impPushOnStack(right, new typeInfo());

            var branch = opcode < OPCODE.CEE_ARGLIST;
            byte[] il = branch
                ? [(byte)opcode, 1, (byte)OPCODE.CEE_NOP, (byte)OPCODE.CEE_NOP]
                : [0xFE, (byte)(opcode - OPCODE.CEE_ARGLIST)];
            var block = new BasicBlock(null, null) { bbCodeOffsEnd = 2 };
            compiler.fgFirstBB = compiler.compCurBB = block;
            if (branch)
            {
                var next = new BasicBlock(null, null) { bbCodeOffs = 2, bbCodeOffsEnd = 3 };
                var target = new BasicBlock(null, null) { bbCodeOffs = 3, bbCodeOffsEnd = 4 };
                block.Next = next;
                next.Next = target;
                block.SetCond(new FlowEdge(block, target, null), new FlowEdge(block, next, null));
            }

            fixed (byte* code = il)
            {
                compiler.info.compCode = code;
                compiler.info.compILCodeSize = il.Length;
                compiler.impImportBlockCode(block);
            }

            var comparison = branch
                ? (ImporterStatements(compiler) ?? throw new AssertionException("Missing conditional jump."))
                    .RootNode.AsUnOp().Op1.AsOp()
                : compiler.impStackTop().val.AsOp();
            var expectedOper = opcode switch {
                OPCODE.CEE_CEQ or OPCODE.CEE_BEQ_S => genTreeOps.GT_EQ,
                OPCODE.CEE_CGT or OPCODE.CEE_CGT_UN or OPCODE.CEE_BGT_UN_S => genTreeOps.GT_GT,
                _ => genTreeOps.GT_LT,
            };
            var unsigned = opcode is OPCODE.CEE_CGT_UN or OPCODE.CEE_CLT_UN or OPCODE.CEE_BGT_UN_S;
            var widened = (nativeFirst ? comparison.Op2 : comparison.Op1).AsCast();

            Assert.That(compiler.stackState.esStackDepth, Is.EqualTo(branch ? 0 : 1));
            Assert.That(comparison.Oper, Is.EqualTo(expectedOper));
            Assert.That(comparison.Op1.Type, Is.EqualTo(Globals.TYP_I_IMPL));
            Assert.That(comparison.Op2.Type, Is.EqualTo(Globals.TYP_I_IMPL));
            Assert.That(comparison.IsUnsigned, Is.EqualTo(unsigned));
            Assert.That(widened.Op1, Is.SameAs(nativeFirst ? right : left));
            Assert.That(widened.IsUnsigned, Is.EqualTo(branch && unsigned));
        });
    }

    [TestCase(false, var_types.TYP_INT)]
    [TestCase(true, var_types.TYP_INT)]
    [TestCase(false, Globals.TYP_I_IMPL)]
    [TestCase(true, Globals.TYP_I_IMPL)]
    public static void ByrefAdditionChecksAndWidensTheIntegerOperand(bool byrefOnRight, var_types offsetType)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = var_types.TYP_BYREF;
            var byref = compiler.gtNewLclvNode(var_types.TYP_BYREF, 0);
            var offset = compiler.gtNewIconNode(offsetType, 7);
            GenTree left = byrefOnRight ? offset : byref;
            GenTree right = byrefOnRight ? byref : offset;

            var result = compiler.impGetByRefResultType(genTreeOps.GT_ADD, false, ref left, ref right);

            Assert.That(result, Is.EqualTo(var_types.TYP_BYREF));
            Assert.That(byrefOnRight ? right : left, Is.SameAs(byref));
            Assert.That(byref.Type, Is.EqualTo(var_types.TYP_BYREF));
            Assert.That((byrefOnRight ? left : right).Type, Is.EqualTo(Globals.TYP_I_IMPL));
        });
    }

    [TestCase(genTreeOps.GT_ADD, var_types.TYP_INT, false)]
    [TestCase(genTreeOps.GT_ADD, var_types.TYP_BYREF, true)]
    [TestCase(genTreeOps.GT_DIV, var_types.TYP_INT, true)]
    [TestCase(genTreeOps.GT_UDIV, var_types.TYP_INT, true)]
    [TestCase(genTreeOps.GT_MOD, var_types.TYP_INT, true)]
    [TestCase(genTreeOps.GT_UMOD, var_types.TYP_INT, true)]
    public static void OperationEffectsRetainOnlySupportedOrdering(genTreeOps oper, var_types type, bool supported)
    {
        WithCompiler(compiler => {
            var tree = compiler.gtNewBinaryNode(oper, type,
                compiler.gtNewIconNode(var_types.TYP_INT, 4), compiler.gtNewIconNode(var_types.TYP_INT, 2));
            tree.Flags |= GenTreeFlags.GTF_ORDER_SIDEEFF;
            Assert.That(tree.SupportsOrderingSideEffect(), Is.EqualTo(supported));

            compiler.gtUpdateNodeSideEffects(tree);

            Assert.That((tree.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.EqualTo(supported));
        });
    }

    [Test]
    public static void OperandEffectsAreRestoredAfterResettingCallOperationEffects()
    {
        WithCompiler(compiler => {
            var argument = compiler.gtNewIndir(var_types.TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8),
                GenTreeFlags.GTF_IND_VOLATILE);
            var call = compiler.gtNewCallNode(var_types.TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(argument));
            call.Flags |= GenTreeFlags.GTF_ORDER_SIDEEFF;

            compiler.gtUpdateNodeOperSideEffects(call);
            Assert.That((call.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.False);

            compiler.gtUpdateNodeSideEffects(call);
            Assert.That((call.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.True);
        });
    }

    [TestCase(gtCallTypes.CT_USER_FUNC, CorInfoHelpFunc.CORINFO_HELP_UNDEF, true)]
    [TestCase(gtCallTypes.CT_INDIRECT, CorInfoHelpFunc.CORINFO_HELP_UNDEF, true)]
    [TestCase(gtCallTypes.CT_HELPER, CorInfoHelpFunc.CORINFO_HELP_DIV, true)]
    [TestCase(gtCallTypes.CT_HELPER, CorInfoHelpFunc.CORINFO_HELP_GETCURRENTMANAGEDTHREADID, false)]
    public static void CallCreationIncludesItsOwnExceptions(gtCallTypes kind, CorInfoHelpFunc helper, bool mayThrow)
    {
        WithCompiler(compiler => {
            var handle = kind == gtCallTypes.CT_HELPER ? Compiler.eeFindHelper(helper) : null;
            var call = compiler.gtNewCallNode(var_types.TYP_INT, kind, handle);
            Assert.That((call.Flags & GenTreeFlags.GTF_EXCEPT) != 0, Is.EqualTo(mayThrow));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void IndirectCallsPreserveVolatilePointerEvaluation(bool isVolatile)
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8);
            var pointer = compiler.gtNewIndir(Globals.TYP_I_IMPL, address,
                isVolatile ? GenTreeFlags.GTF_IND_VOLATILE : 0);
            Assert.That((pointer.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.EqualTo(isVolatile));
            compiler.info.compMaxStack = 1;
            compiler.stackState.esStack = [new() { val = pointer }];
            compiler.stackState.esStackDepth = 1;
            var signature = new CORINFO_SIG_INFO {
                callConv = CorInfoCallConv.CORINFO_CALLCONV_DEFAULT,
                retType = CorInfoType.CORINFO_TYPE_VOID,
            };

            var call = compiler.impImportIndirectCall(signature);

            Assert.Multiple(() => {
                Assert.That(call.ControlExpr, Is.SameAs(pointer));
                Assert.That((call.Flags & GenTreeFlags.GTF_ORDER_SIDEEFF) != 0, Is.EqualTo(isVolatile));
                Assert.That((call.Flags & GenTreeFlags.GTF_EXCEPT) != 0, Is.True);
                Assert.That(compiler.stackState.esStackDepth, Is.Zero);
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void OrderingEffectsSpillEarlierGlobalReads(bool localStore)
    {
        WithCompiler(compiler => {
            var read = compiler.gtNewIndir(var_types.TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8));
            compiler.stackState.esStack = [new() { val = read }];
            compiler.stackState.esStackDepth = 1;
            var effect = new GenTree(genTreeOps.GT_CATCH_ARG, var_types.TYP_REF) { Flags = GenTreeFlags.GTF_ORDER_SIDEEFF };

            if (localStore)
            {
                effect = compiler.gtNewStoreLclVarNode(0, effect);
            }

            var statement = new Statement(effect, 1);

            compiler.impAppendStmt(statement, Compiler.CHECK_SPILL_ALL);

            Assert.Multiple(() => {
                Assert.That(compiler.stackState.esStack[0].val.Oper, Is.EqualTo(genTreeOps.GT_LCL_VAR));
                Assert.That(statement.PrevStmt, Is.Not.Null);
                Assert.That(statement.PrevStmt?.RootNode.Oper, Is.EqualTo(genTreeOps.GT_STORE_LCL_VAR));
            });
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalFieldAnnotationPreservesStoredGlobalReads(bool store, bool globalRead)
    {
        WithCompiler(compiler => {
            var address = new GenTreeFieldAddr(var_types.TYP_BYREF,
                compiler.gtNewLclVarAddrNode(var_types.TYP_BYREF, 1), null, 0);
            GenTree value = globalRead
                ? compiler.gtNewIndir(var_types.TYP_INT, compiler.gtNewIconNode(Globals.TYP_I_IMPL, 8))
                : compiler.gtNewIconNode(var_types.TYP_INT, 1);
            var indir = store
                ? compiler.gtNewStoreIndNode(var_types.TYP_INT, address, value)
                : compiler.gtNewIndir(var_types.TYP_INT, address);

            compiler.impAnnotateFieldIndir(indir);

            Assert.Multiple(() => {
                Assert.That((indir.Flags & GenTreeFlags.GTF_GLOB_REF) != 0, Is.EqualTo(store && globalRead));
                Assert.That((address.Flags & GenTreeFlags.GTF_FLD_DEREFERENCED) != 0, Is.True);
            });
        });
    }

    [Test]
    public static void InlineNullCheckCannotMovePastEHVisibleStore(
        [Values(0, 1, 2, 3)] int region,
        [Values("tree", "args", "statement", "stack")] string location)
    {
        WithCompiler(compiler => {
            var callSite = new BasicBlock(null, null);
            var handler = new BasicBlock(null, null);

            if (region == 1)
            {
                callSite.TryIndex = 0;
            }
            else if (region >= 2)
            {
                callSite.HndIndex = 0;
                callSite.Next = handler;
                compiler.compHndBBtab = [new EHblkDsc {
                    ebdHandlerType = region == 2 ? EHHandlerType.EH_HANDLER_FILTER : EHHandlerType.EH_HANDLER_CATCH,
                    ebdFilter = callSite,
                    ebdHndBeg = handler,
                    ebdHndLast = handler,
                }];
                compiler.compHndBBtabCount = 1;
            }

            compiler.impInlineInfo = new InlineInfo { InlineRoot = compiler, InlinerCompiler = compiler, iciBlock = callSite };
            compiler.opts.compFlags = Globals.CLFLG_INLINING;
            compiler.compCurBB = compiler.fgFirstBB = new BasicBlock(null, null);
            var store = compiler.gtNewCommaNode(var_types.TYP_INT,
                compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(var_types.TYP_INT, 1)),
                compiler.gtNewIconNode(var_types.TYP_INT, 0));
            GenTree? additionalTree = null;
            CallArgs args = default;

            switch (location)
            {
                case "tree":
                {
                    additionalTree = store;
                    break;
                }

                case "args":
                {
                    _ = args.PushBack(NewCallArg.CreateForPrimitive(store));
                    break;
                }

                case "statement":
                {
                    compiler.impAppendStmt(new Statement(store, 1));
                    break;
                }

                case "stack":
                {
                    compiler.stackState.esStack = [new() { val = store }];
                    compiler.stackState.esStackDepth = 1;
                    break;
                }
            }

            InlArgInfo[] arguments = [new() { argTmpNum = 0 }];
            var result = compiler.impInlineIsGuaranteedThisDerefBeforeAnySideEffects(
                additionalTree, args, compiler.gtNewLclVarNode(var_types.TYP_REF, 0), arguments);
            Assert.That(result, Is.EqualTo(region is 0 or 3));
        });
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
        compiler.lvaTable = [new LclVarDsc { Type = var_types.TYP_REF }, new LclVarDsc { Type = var_types.TYP_INT }];
        compiler.lvaCount = 2;
        compiler.stackState.esStack = [];
        compiler.compCurBB = new BasicBlock(null, null);
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
