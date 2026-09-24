// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Compiler.optAssertionKind;
using static RyuJitSharp.Compiler.optOp2Kind;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.SymbolicIntegerValue;
using static RyuJitSharp.var_types;
using AssertionDsc = RyuJitSharp.Compiler.AssertionDsc;
using BitOps = RyuJitSharp.BitSetOps<RyuJitSharp.BitVecTraits, RyuJitSharp.BitVecTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class AssertionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void MorphUsesLocalAssertionsWithoutGlobalRangeAnalysis(bool cast)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
            compiler.fgGlobalMorph = true;
            compiler.apLocal = InstallAssertions(compiler, cast
                ? [AssertionDsc.CreateSubrange(compiler, 0, new(Zero, UByteMax))]
                : [IntAssertion(compiler, 7)]);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            compiler.apLocalPostorder = BitOps.MakeCopy(traits, compiler.apLocal);
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            GenTree tree = cast
                ? new GenTreeCast(TYP_INT, local, false, TYP_UBYTE)
                : compiler.gtNewBinaryNode(GT_EQ, TYP_INT, local, compiler.gtNewIconNode(TYP_INT, 7));
            var result = compiler.fgMorphTree(tree);
            if (cast)
            {
                Assert.That(result, Is.SameAs(local));
            }
            else
            {
                Assert.That(result.Oper, Is.EqualTo(GT_CNS_INT));
                Assert.That(result.AsIntCon().IconValue, Is.EqualTo((nint)1));
            }
            Assert.That(compiler.vnStore, Is.Null);
        });
    }

    [TestCase(VNFunc.VNF_CastClass, false)]
    [TestCase(VNFunc.VNF_IsInstanceOf, false)]
    [TestCase(VNFunc.VNF_JitNew, true)]
    public static void ObjectTypeRequiresMappedHandlesAndKeepsExactnessSeparate(VNFunc function, bool exact)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var type = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var value = store.VNForFuncNoFolding(TYP_REF, function, type, store.VNForExpr(null, TYP_REF));
            Assert.That(store.IsVNTypeHandle(type, out var handle), Is.False);
            Assert.That((nint)handle, Is.EqualTo((nint)0));
            Assert.That((nint)store.GetObjectType(value, out var isExact, out var nonNull), Is.EqualTo((nint)0));
            Assert.That(isExact || nonNull, Is.False);
            store.AddToEmbeddedHandleMap(0x1000, 0);
            Assert.That(store.IsVNTypeHandle(type, out _), Is.False);
            store.AddToEmbeddedHandleMap(0x1000, 0x2000);
            Assert.That(store.IsVNTypeHandle(type, out handle), Is.True);
            Assert.That((nint)handle, Is.EqualTo((nint)0x2000));
            Assert.That((nint)store.GetObjectType(value, out isExact, out nonNull), Is.EqualTo((nint)0x2000));
            Assert.That(isExact, Is.EqualTo(exact));
            Assert.That(nonNull, Is.EqualTo(exact));
            Assert.That(store.IsVNTypeHandle(store.VNForHandle(0x1000, GTF_ICON_OBJ_HDL), out _), Is.False);
        });
    }

    [Test]
    public static void ObjectTypeUsesRuntimeMetadataWithoutInventingExactness()
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getObjectType = &GetAssertionObjectType;
            vtable.Base.Base.getBuiltinClass = &GetAssertionBuiltinClass;
            var ee = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &ee;
            var store = new ValueNumStore(compiler);
            var value = store.VNForHandle(0x4000, GTF_ICON_OBJ_HDL);
            Assert.That((nint)store.GetObjectType(value, out var exact, out var nonNull), Is.EqualTo((nint)0x2000));
            Assert.That(exact && nonNull, Is.True);
            var getType = store.VNForFunc(TYP_REF, VNFunc.VNF_ObjGetType, value);
            Assert.That((nint)store.GetObjectType(getType, out exact, out nonNull), Is.EqualTo((nint)0x3000));
            Assert.That(exact, Is.False);
            Assert.That(nonNull, Is.True);
            int[] unknown = [ValueNumStore.NoVN, ValueNumStore.VNForNull(), store.VNForIntCon(1), store.VNForExpr(null, TYP_REF)];
            foreach (var vn in unknown)
            {
                Assert.That((nint)store.GetObjectType(vn, out exact, out nonNull), Is.EqualTo((nint)0));
                Assert.That(exact || nonNull, Is.False);
            }
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SubtypeProofRequiresAnActiveMatchingFactAndDefiniteRuntimeAnswer(bool exact)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.compareTypesForCast = &CompareAssertionTypes;
            var ee = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &ee;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var type = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            var otherType = store.VNForHandle(0x5000, GTF_ICON_CLASS_HDL);
            store.AddToEmbeddedHandleMap(0x1000, 0x2000);
            store.AddToEmbeddedHandleMap(0x5000, 0x6000);
            var value = store.VNForExpr(null, TYP_REF);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(AssertionDsc.CreateSubtype(compiler, value, type, exact));
            Assert.That(compiler.optAssertionVNIsSubtype(value, type, assertions), Is.False);
            BitOps.AddElemD(traits, assertions, index - 1);
            Assert.That(compiler.optAssertionVNIsSubtype(value, type, assertions), Is.True);
            Assert.That(compiler.optAssertionVNIsSubtype(value, type, assertions, budget: 0), Is.False);
            Assert.That(compiler.optAssertionVNIsSubtype(value, otherType, assertions), Is.False);
            Assert.That(compiler.optAssertionVNIsSubtype(store.VNForExpr(null, TYP_REF), type, assertions), Is.False);
            Assert.That(compiler.optAssertionVNIsSubtype(ValueNumStore.NoVN, type, assertions), Is.False);
            var allocation = store.VNForFuncNoFolding(TYP_REF, VNFunc.VNF_JitNew, type, ValueNumStore.VNForVoid());
            Assert.That(compiler.optAssertionVNIsSubtype(allocation, type, null), Is.True);
            Assert.That(compiler.optAssertionVNIsSubtype(allocation, type, null, budget: 0), Is.False);
        }, local: false);
    }

    [Test]
    public static void PhiSubtypeProofConsumesBudgetAndUsesPredecessorAssertions()
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.compareTypesForCast = &CompareAssertionTypes;
            var ee = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &ee;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            ref var definitions = ref compiler.lvaTable[0].lvPerSsaData;
            var argumentSsa = definitions.AllocSsaNum();
            var phiSsa = definitions.AllocSsaNum();
            var value = store.VNForExpr(null, TYP_REF);
            definitions.GetSsaDef(argumentSsa)._vnPair.SetBoth(value);
            var type = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            store.AddToEmbeddedHandleMap(0x1000, 0x2000);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(AssertionDsc.CreateSubtype(compiler, value, type, false));
            BitOps.AddElemD(traits, assertions, index - 1);
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var entry = BasicBlock.New(compiler, BBKinds.BBJ_COND);
            var join = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var other = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = entry;
            entry.SetCond(new FlowEdge(entry, join, null), new FlowEdge(entry, other, null));
            join.bbPreds = entry.TrueEdge;
            var outgoing = new nint[compiler.fgBBNumMax + 1][];
            outgoing[entry.bbNum] = assertions;
            JtrueAssertionOut(compiler) = outgoing;
            var argument = new GenTreePhiArg(TYP_REF, 0, argumentSsa, entry);
            var phi = new GenTreePhi(TYP_REF) { FirstUse = new GenTreePhi.Use(argument) };
            var definition = compiler.gtNewStoreLclVarNode(0, phi);
            definition.SsaNum = phiSsa;
            definitions.GetSsaDef(phiSsa) = new LclSsaVarDsc(join, definition);
            var phiVN = store.VNForPhiDef(TYP_REF, 0, phiSsa, [argumentSsa]);

            Assert.That(compiler.optAssertionVNIsSubtype(phiVN, type, null, budget: 1), Is.False);
            Assert.That(compiler.optAssertionVNIsSubtype(phiVN, type, null, budget: 2), Is.True);
            Assert.That(argument._vnPair, Is.EqualTo(new ValueNumPair()));
            outgoing[entry.bbNum] = BitOps.MakeEmpty(traits);
            Assert.That(compiler.optAssertionVNIsSubtype(phiVN, type, null), Is.False);
        }, local: false);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void CastAssertionApplicationPreservesArgumentEffectsAndSingleEvaluation(bool spillObject)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.compareTypesForCast = &CompareAssertionTypes;
            var ee = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &ee;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            compiler.compCurBB = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var type = store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL);
            store.AddToEmbeddedHandleMap(0x1000, 0x2000);
            var value = store.VNForExpr(null, TYP_REF);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(AssertionDsc.CreateSubtype(compiler, value, type, false));
            BitOps.AddElemD(traits, assertions, index - 1);
            var typeEffect = compiler.gtNewHelperCallNode(TYP_VOID, CorInfoHelpFunc.CORINFO_HELP_POLL_GC);
            var typeArgument = compiler.gtNewCommaNode(TYP_I_IMPL, typeEffect, compiler.gtNewIconHandleNode(0x1000, GTF_ICON_CLASS_HDL));
            typeArgument._vnPair.SetBoth(type);
            GenTree objectArgument = compiler.gtNewLclvNode(TYP_REF, 0);
            if (spillObject)
            {
                var objectEffect = compiler.gtNewHelperCallNode(TYP_VOID, CorInfoHelpFunc.CORINFO_HELP_POLL_GC);
                objectArgument = compiler.gtNewCommaNode(TYP_REF, objectEffect, objectArgument);
            }

            objectArgument._vnPair.SetBoth(value);
            var call = compiler.gtNewHelperCallNode(TYP_REF, CorInfoHelpFunc.CORINFO_HELP_CHKCASTCLASS, typeArgument, objectArgument);
            var firstException = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, value));
            var secondException = store.VNExcSetSingleton(store.VNForFunc(TYP_REF, VNFunc.VNF_OverflowExc, ValueNumStore.VNForVoid()));
            var exceptions = new ValueNumPair(firstException, secondException);
            call._vnPair = store.VNPWithExc(new(value, value), exceptions);
            var statement = compiler.gtNewStmt(call);
            var result = compiler.optAssertionProp_Call(assertions, call, statement) ?? throw new InvalidOperationException();
            Assert.That(statement.RootNode, Is.SameAs(result));
            Assert.That(result.Oper, Is.EqualTo(GT_COMMA));
            Assert.That(call.Oper, Is.EqualTo(GT_CALL));
            var effects = result.AsOp().Op1;
            var returnedObject = result.AsOp().Op2.AsLclVarCommon();
            if (spillObject)
            {
                Assert.That(effects.Oper, Is.EqualTo(GT_COMMA));
                Assert.That(effects.AsOp().Op1, Is.SameAs(typeEffect));
                var assignment = effects.AsOp().Op2;
                Assert.That(assignment.Oper, Is.EqualTo(GT_STORE_LCL_VAR));
                Assert.That(assignment.AsUnOp().Op1, Is.SameAs(objectArgument));
                Assert.That(returnedObject.LclNum, Is.EqualTo(assignment.AsLclVarCommon().LclNum));
                Assert.That(returnedObject.LclNum, Is.GreaterThanOrEqualTo(2));
                var argument = call.Args.GetUserArgByIndex(1) ?? throw new InvalidOperationException();
                Assert.That(argument.Node?.AsOp().Op1, Is.SameAs(assignment));
            }
            else
            {
                Assert.That(effects, Is.SameAs(typeEffect));
                Assert.That(returnedObject.LclNum, Is.Zero);
                Assert.That(store.VNPExceptionSet(result._vnPair), Is.EqualTo(exceptions));
                Assert.That(store.VNNormalValue(result._vnPair.Conservative), Is.EqualTo(value));
            }
        }, local: false);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void UnprovenCastOnlyReceivesNonNullHintWhenExpansionIsEnabled(bool expandable)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var value = store.VNForExpr(null, TYP_REF);
            var argument = compiler.gtNewLclvNode(TYP_REF, 0);
            argument._vnPair.SetBoth(value);
            var type = compiler.gtNewIconHandleNode(0x1000, GTF_ICON_CLASS_HDL);
            type._vnPair.SetBoth(store.VNForHandle(0x1000, GTF_ICON_CLASS_HDL));
            var call = compiler.gtNewHelperCallNode(TYP_REF, CorInfoHelpFunc.CORINFO_HELP_ISINSTANCEOFCLASS, type, argument);
            if (expandable)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_CAST_CAN_BE_EXPANDED;
            }

            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(AssertionDsc.CreateVNNonNullAssertion(compiler, value));
            BitOps.AddElemD(traits, assertions, index - 1);
            var result = compiler.optAssertionProp_Call(assertions, call, compiler.gtNewStmt(call));
            Assert.That(result, expandable ? Is.SameAs(call) : Is.Null);
            Assert.That((call._callMoreFlags & GenTreeCallFlags.GTF_CALL_M_CAST_OBJ_NONNULL) != 0, Is.EqualTo(expandable));
        }, local: false);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void ConstantApplicationPreservesScalarIdentityAndInstallsThreadedGlobalUses(bool floating, bool global)
    {
        WithCompiler(compiler => {
            var type = floating ? TYP_DOUBLE : TYP_INT;
            compiler.lvaTable = [new LclVarDsc { Type = type }, new LclVarDsc { Type = TYP_INT }];
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var original = compiler.gtNewLclvNode(type, 0);
            var unknown = store.VNForExpr(null, type);
            original._vnPair.SetBoth(unknown);
            original.Flags |= GTF_COLON_COND | GTF_VAR_DEATH;
            var constant = floating ? store.VNForDoubleCon(1.5) : store.VNForIntCon(7);
            var assertion = floating
                ? AssertionDsc.CreateConstLclVarAssertion(compiler, 0, unknown, 1.5, constant, true)
                : AssertionDsc.CreateConstLclVarAssertion(compiler, 0, unknown, (nint)7, constant, true);
            var index = compiler.optAddAssertion(assertion);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, assertions, index - 1);
            var parent = compiler.gtNewUnaryNode(GT_RETURN, type, original);
            var statement = global ? compiler.gtNewStmt(parent) : null;
            if (global)
            {
                original.Prev = compiler.gtNewNothingNode();
                original.Next = parent;
            }

            var replacement = compiler.optAssertionProp_LclVar(assertions, original, statement);
            Assert.That(replacement, Is.Not.Null);
            var result = replacement ?? throw new InvalidOperationException();
            Assert.That(result.Oper, Is.EqualTo(floating ? GT_CNS_DBL : GT_CNS_INT));
            Assert.That(result.Type, Is.EqualTo(type));
            Assert.That(floating ? result.AsDblCon().DconVal : (double)result.AsIntCon().IconValue,
                Is.EqualTo(floating ? 1.5 : 7.0));
            Assert.That(result.Flags, Is.EqualTo(GTF_COLON_COND));
            Assert.That(result._vnPair, Is.EqualTo(global ? new ValueNumPair(constant, constant) : new ValueNumPair()));
            Assert.That(original.Oper, Is.EqualTo(GT_LCL_VAR));
#if DEBUG
            Assert.That(result.TreeId, Is.EqualTo(original.TreeId));
#endif
            if (global)
            {
                Assert.That(parent.AsUnOp().Op1, Is.SameAs(result));
                Assert.That(result.Next, Is.SameAs(parent));
            }
        }, local: !global);
    }

    [TestCase(0L)]
    [TestCase(long.MinValue)]
    public static void FloatingEqualityAssertionsDoNotPropagateEitherSignedZero(long bits)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_DOUBLE }, new LclVarDsc { Type = TYP_INT }];
            var value = compiler.gtNewLclvNode(TYP_DOUBLE, 0);
            var assertion = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN,
                BitConverter.Int64BitsToDouble(bits), ValueNumStore.NoVN, true);
            Assert.That(compiler.optConstantAssertionProp(assertion, value, null), Is.Null);
            Assert.That(value.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void GlobalConstantApplicationOnlyBypassesCseProtectionForCheckedBounds(bool checkedBound)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, lvIsCSE = true }, new LclVarDsc { Type = TYP_REF }];
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var vn = checkedBound
                ? store.VNForFunc(TYP_INT, VNFunc.VNF_ARR_LENGTH, store.VNForExpr(null, TYP_REF))
                : store.VNForExpr(null, TYP_INT);
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            local._vnPair.SetBoth(vn);
            var statement = compiler.gtNewStmt(local);
            var constant = store.VNForIntCon(3);
            var assertion = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, vn, (nint)3, constant, true);
            var result = compiler.optConstantAssertionProp(assertion, local, statement);
            Assert.That(result is not null, Is.EqualTo(checkedBound));
            Assert.That(statement.RootNode.Oper, Is.EqualTo(checkedBound ? GT_CNS_INT : GT_LCL_VAR));
        }, local: false);
    }

    [TestCase(false, false, false, true)]
    [TestCase(true, false, false, false)]
    [TestCase(true, true, false, true)]
    [TestCase(false, false, true, false)]
    public static void CopyApplicationRetainsEnregistrationAndSynchronousPathRestrictions(bool field, bool doNotEnregister, bool synchronous, bool expected)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_LONG, lvIsMultiRegRet = true },
                new LclVarDsc { Type = TYP_LONG, lvDoNotEnregister = doNotEnregister, lvOnlyUsedOnSynchronousPath = synchronous },
            ];
            var tree = field ? (GenTreeLclVarCommon)compiler.gtNewLclFldNode(TYP_INT, 0, 4) : compiler.gtNewLclvNode(TYP_LONG, 0);
            tree.Flags |= GTF_VAR_DEATH;
            var index = compiler.optAddAssertion(AssertionDsc.CreateLclvarCopy(compiler, 0, 1, true));
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, assertions, index - 1);

            var result = field ? compiler.optAssertionProp_LclFld(assertions, tree, null)
                : compiler.optAssertionProp_LclVar(assertions, tree, null);
            Assert.That(result is not null, Is.EqualTo(expected));
            Assert.That(tree.LclNum, Is.EqualTo(expected ? 1 : 0));
            Assert.That((tree.Flags & GTF_VAR_DEATH) == 0, Is.EqualTo(expected));
            Assert.That(compiler.lvaTable[1].lvIsMultiRegRet, Is.EqualTo(expected));
            if (field)
            {
                Assert.That(tree.AsLclFld().LclOffs, Is.EqualTo(4));
                Assert.That(compiler.lvaTable[1].lvDoNotEnregister, Is.EqualTo(doNotEnregister));
            }
        });
    }

    [TestCase(TYP_STRUCT, true)]
    [TestCase(TYP_REF, true)]
    [TestCase(TYP_INT, false)]
    public static void RedundantZeroStoresPreserveIntegralLoopInitializers(var_types type, bool removed)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = type }, new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(8) }];
            if (type is TYP_STRUCT)
            {
                compiler.lvaTable[0].Layout = new ClassLayout(8);
            }
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            compiler.compCurBB = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var zero = type is TYP_STRUCT
                ? AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, O2K_ZEROOBJ, ValueNumStore.NoVN, true)
                : AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, (nint)0, ValueNumStore.NoVN, true);
            var sourceZero = AssertionDsc.CreateConstLclVarAssertion(compiler, 1, ValueNumStore.NoVN, O2K_ZEROOBJ, ValueNumStore.NoVN, true);
            var assertions = InstallAssertions(compiler, [zero, sourceZero]);
            GenTree value = type is TYP_STRUCT ? compiler.gtNewLclvNode(TYP_STRUCT, 1) : compiler.gtNewIconNode(type, 0);
            var tree = compiler.gtNewStoreLclVarNode(0, value);

            var result = compiler.optAssertionProp_LocalStore(assertions, tree, null);
            Assert.That(result is not null, Is.EqualTo(removed));
            Assert.That(tree.Oper, Is.EqualTo(removed ? GT_NOP : GT_STORE_LCL_VAR));
            if (type is TYP_STRUCT)
            {
                Assert.That(value.Oper, Is.EqualTo(GT_LCL_VAR));
            }
        });
    }

    [Test]
    public static void BlockStoreZeroPropagationUpdatesTheOwningDataOperand()
    {
        WithCompiler(compiler => {
            var layout = new ClassLayout(8);
            compiler.lvaTable = [new LclVarDsc { Type = TYP_STRUCT, Layout = layout }, new LclVarDsc { Type = TYP_BYREF }];
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            compiler.compCurBB = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var assertions = InstallAssertions(compiler, [
                AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, O2K_ZEROOBJ, ValueNumStore.NoVN, true),
            ]);
            var original = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            var address = compiler.gtNewLclvNode(TYP_BYREF, 1);
            var block = new GenTreeBlk(TYP_STRUCT, address, original, layout);

            Assert.That(compiler.optAssertionProp_BlockStore(assertions, block, null), Is.SameAs(block));
            Assert.That(block.Data, Is.TypeOf<GenTreeIntCon>());
            Assert.That(block.Data.AsIntCon().IconValue, Is.EqualTo((nint)0));
            Assert.That(block.Addr, Is.SameAs(address));
            Assert.That(original.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void AssertionUpdatesReplaceTheOwningUseAndPreserveForwardTraversal(bool root)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_I_IMPL }, new LclVarDsc { Type = TYP_INT }];
            var original = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            var parent = compiler.gtNewIndir(TYP_REF, original);
            var statement = compiler.gtNewStmt(root ? original : parent);
            var next = compiler.gtNewNothingNode();
            original.Next = next;
            var replacement = compiler.gtNewIconHandleNode(0x1000, GTF_ICON_STR_HDL);
            var previous = compiler.gtNewNothingNode();
            replacement.Prev = previous;

            Assert.That(compiler.optAssertionProp_Update(replacement, original, statement), Is.SameAs(replacement));
            Assert.That(root ? statement.RootNode : parent.Addr, Is.SameAs(replacement));
            Assert.That(replacement.Next, Is.SameAs(next));
            Assert.That(replacement.Prev, Is.SameAs(previous));
            Assert.That(AssertionPropagated(compiler), Is.True);
            Assert.That(AssertionPropagatedCurrentStmt(compiler), Is.True);
            if (!root)
            {
                Assert.That(parent.Flags & (GTF_IND_INVARIANT | GTF_IND_NONNULL),
                    Is.EqualTo(GTF_IND_INVARIANT | GTF_IND_NONNULL));
            }
        }, local: false);
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ZeroObjectPropagationReplacesTheOrdinaryOrSwiftReturnValue(bool swift)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_STRUCT, Layout = new ClassLayout(8) }, new LclVarDsc { Type = TYP_INT }];
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            compiler.compCurBB = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.info.compRetNativeType = TYP_LONG;
            var assertions = InstallAssertions(compiler, [
                AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, O2K_ZEROOBJ, ValueNumStore.NoVN, true),
            ]);
            var original = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            original._vnPair.SetBoth(42);
            var error = compiler.gtNewNull();
            var tree = swift
                ? compiler.gtNewBinaryNode(GT_SWIFT_ERROR_RET, TYP_STRUCT, error, original)
                : compiler.gtNewUnaryNode(GT_RETURN, TYP_STRUCT, original);

            Assert.That(compiler.optAssertionProp_Return(assertions, tree, null), Is.SameAs(tree));
            var value = swift ? tree.AsOp().Op2 : tree.AsUnOp().Op1;
            Assert.That(value, Is.TypeOf<GenTreeIntCon>());
            Assert.That(value.Type, Is.EqualTo(TYP_INT));
            Assert.That(value.AsIntCon().IconValue, Is.EqualTo((nint)0));
            Assert.That(value._vnPair, Is.EqualTo(new ValueNumPair()));
            Assert.That(original.Oper, Is.EqualTo(GT_LCL_VAR));
            if (swift)
            {
                Assert.That(tree.AsOp().Op1, Is.SameAs(error));
            }
#if DEBUG
            Assert.That(value.TreeId, Is.EqualTo(original.TreeId));
#endif
            Assert.That(AssertionPropagatedCurrentStmt(compiler), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NonNullApplicationRetainsCallEffectsAndIndirectionOrdering(bool local)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            compiler.compCurBB = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var receiver = compiler.gtNewLclvNode(TYP_REF, 0);
            var vn = store.VNForExpr(null, TYP_REF);
            receiver._vnPair.SetBoth(vn);
            var assertion = compiler.optAddAssertion(local
                ? AssertionDsc.CreateLclNonNullAssertion(compiler, 0) : AssertionDsc.CreateVNNonNullAssertion(compiler, vn));
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            BitOps.AddElemD(traits, assertions, assertion - 1);
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.Flags |= GTF_CALL_NULLCHECK;
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(receiver).WithWellKnownArg(WellKnownArg.ThisPointer));
            var indir = compiler.gtNewIndir(TYP_INT, receiver);
            var callFlags = call.Flags;
            var indirFlags = indir.Flags;

            Assert.That(compiler.optNonNullAssertionProp_Call(null, call), Is.Null);
            Assert.That(compiler.optNonNullAssertionProp_Ind(null, indir), Is.False);
            Assert.That(call.Flags, Is.EqualTo(callFlags));
            Assert.That(indir.Flags, Is.EqualTo(indirFlags));
            Assert.That(compiler.optNonNullAssertionProp_Call(assertions, call), Is.SameAs(call));
            Assert.That(call.Flags, Is.EqualTo(callFlags & ~(GTF_CALL_NULLCHECK | GTF_EXCEPT)));
            Assert.That(call.Flags & GTF_CALL, Is.Not.EqualTo(GTF_EMPTY));
            Assert.That(compiler.optAssertionProp_Ind(assertions, indir, local ? null : compiler.gtNewStmt(indir)), Is.SameAs(indir));
            Assert.That(indir.Flags, Is.EqualTo((indirFlags & ~GTF_EXCEPT) | GTF_IND_NONFAULTING | GTF_ORDER_SIDEEFF));
            Assert.That(compiler.optNonNullAssertionProp_Ind(assertions, indir), Is.False);
        }, local);
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public static void BarrierPropagationRejectsVariableOffsetsButRecognizesNullValues(bool variableOffset, bool nullValue, bool noBarrier)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_BYREF }, new LclVarDsc { Type = TYP_REF }];
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var zero = store.VNForIntCon(0);
            var stack = store.VNForFuncNoFolding(TYP_BYREF, VNFunc.VNF_PtrToLoc, zero, zero);
            var offset = variableOffset ? store.VNForExpr(null, TYP_I_IMPL) : store.VNForLongCon(8);
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            address._vnPair.SetBoth(store.VNForFuncNoFolding(TYP_BYREF, VNFunc.VNF_ADD, stack, offset));
            var value = compiler.gtNewLclvNode(TYP_REF, 1);
            value._vnPair = new(ValueNumStore.VNForNull(), nullValue ? ValueNumStore.VNForNull() : store.VNForExpr(null, TYP_REF));
            var indir = new GenTreeStoreInd(TYP_REF, address, value);

            Assert.That(compiler.optWriteBarrierAssertionProp_StoreInd(null, indir), Is.EqualTo(noBarrier));
            Assert.That((indir.Flags & GTF_IND_TGT_NOT_HEAP) != 0, Is.EqualTo(noBarrier));
            Assert.That(indir.Flags & GTF_IND_TGT_HEAP, Is.EqualTo(GTF_EMPTY));
        }, local: false);
    }

    [Test]
    public static void BarrierPropagationClassifiesArrayAndBoxedStaticAddressesAndHonorsExistingFlags()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_BYREF }, new LclVarDsc { Type = TYP_REF }];
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var zero = store.VNForIntCon(0);
            var stack = store.VNForFuncNoFolding(TYP_BYREF, VNFunc.VNF_PtrToLoc, zero, zero);
            var heap = store.VNForExpr(null, TYP_REF);
            var boxed = store.VNForHandle(0x1000, GTF_ICON_STATIC_BOX_PTR);
            var addresses = new[] {
                (stack, GTF_IND_TGT_NOT_HEAP),
                (heap, GTF_IND_TGT_HEAP),
                (StoreFunctionRecord(store, TYP_BYREF, VNFunc.VNF_PtrToStatic, boxed, zero, zero), GTF_IND_TGT_HEAP),
                (StoreFunctionRecord(store, TYP_BYREF, VNFunc.VNF_PtrToArrElem, zero, stack, zero, zero), GTF_IND_TGT_NOT_HEAP),
                (StoreFunctionRecord(store, TYP_BYREF, VNFunc.VNF_PtrToArrElem, zero, heap, zero, zero), GTF_IND_TGT_HEAP),
            };
            var builder = new ClassLayoutBuilder(compiler, TARGET_POINTER_SIZE);
            builder.SetGCPtrType(0, TYP_REF);
            var layout = ClassLayout.Create(compiler, builder);
            foreach (var (vn, expected) in addresses)
            {
                var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
                address._vnPair.SetBoth(vn);
                var value = compiler.gtNewLclvNode(TYP_REF, 1);
                value._vnPair.SetBoth(store.VNForExpr(null, TYP_REF));
                var indir = new GenTreeStoreInd(TYP_REF, address, value);
                var block = new GenTreeBlk(TYP_STRUCT, address, compiler.gtNewIconNode(TYP_INT, 0), layout);
                Assert.That(compiler.optWriteBarrierAssertionProp_StoreInd(null, indir), Is.True);
                Assert.That(indir.Flags & (GTF_IND_TGT_HEAP | GTF_IND_TGT_NOT_HEAP), Is.EqualTo(expected));
                Assert.That(compiler.optWriteBarrierAssertionProp_StoreBlk(null, block), Is.True);
                Assert.That(block.Flags & (GTF_IND_TGT_HEAP | GTF_IND_TGT_NOT_HEAP), Is.EqualTo(expected));
                Assert.That(compiler.optWriteBarrierAssertionProp_StoreBlk(null, block), Is.False);
                Assert.That(compiler.optWriteBarrierAssertionProp_StoreInd(null, indir), Is.False);

                if (expected is GTF_IND_TGT_HEAP)
                {
                    value._vnPair.SetBoth(ValueNumStore.VNForNull());
                    Assert.That(compiler.optWriteBarrierAssertionProp_StoreInd(null, indir), Is.True);
                    Assert.That(indir.Flags & GTF_IND_TGT_NOT_HEAP, Is.Not.EqualTo(GTF_EMPTY));
                }
            }
        }, local: false);
    }

    [TestCase(TYP_BYTE, true)]
    [TestCase(TYP_SHORT, true)]
    [TestCase(TYP_INT, false)]
    public static void ValueNumberMasksRespectLaneWidthsAndBitwiseInputs(var_types baseType, bool expected)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            simd16_t value = default;
            value.AsSpan<ushort>()[0] = ushort.MaxValue;
            var constant = store.VNForSimd16Con(value);
            Assert.That(store.IsVectorPerElementMask(constant, baseType, 16), Is.EqualTo(expected));
#if TARGET_XARCH
            var simdType = store.VNForFuncNoFolding(TYP_UNKNOWN, VNFunc.VNF_SimdType,
                store.VNForIntCon(16), store.VNForIntCon((int)TYP_SHORT));
            var unknown = store.VNForExpr(null, TYP_SIMD16);
            var compare = StoreFunctionRecord(store, TYP_SIMD16, VNFunc.VNF_HWI_SSE2_CompareEqual, unknown, unknown, simdType);
            var bitwise = StoreFunctionRecord(store, TYP_SIMD16, VNFunc.VNF_HWI_SSE2_AndNot, compare, constant, simdType);
            Assert.That(store.IsVectorPerElementMask(compare, baseType, 16), Is.EqualTo(expected));
            Assert.That(store.IsVectorPerElementMask(bitwise, baseType, 16), Is.EqualTo(expected));
            var unproven = StoreFunctionRecord(store, TYP_SIMD16, VNFunc.VNF_HWI_SSE2_AndNot, compare, unknown, simdType);
            Assert.That(store.IsVectorPerElementMask(unproven, baseType, 16), Is.False);
#endif
        });
    }

    [Test]
    public static void HardwareMaskPropagationRequiresAllConservativePhiInputs()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_SIMD16, lvSingleDef = true }, new LclVarDsc { Type = TYP_INT }];
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            simd16_t value = default;
            var mask = store.VNForSimd16Con(value);
            ref var definitions = ref compiler.lvaTable[0].lvPerSsaData;
            var first = definitions.AllocSsaNum();
            var second = definitions.AllocSsaNum();
            var definition = definitions.AllocSsaNum();
            definitions.GetSsaDef(first)._vnPair.SetBoth(mask);
            definitions.GetSsaDef(second)._vnPair = new(mask, store.VNForExpr(null, TYP_SIMD16));
            var local = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            local._vnPair.SetBoth(store.VNForPhiDef(TYP_SIMD16, 0, definition, [first, second]));
            var extract = new GenTreeHWIntrinsic(TYP_INT, NamedIntrinsic.NI_Vector_ExtractMostSignificantBits, TYP_SHORT, 16, local);

            compiler.optAssertionProp_HWIntrinsic(extract);
            Assert.That(compiler.lvaTable[0].IsVectorPerElementMask(TYP_SHORT), Is.False);
            definitions.GetSsaDef(second)._vnPair.Conservative = mask;
            compiler.optAssertionProp_HWIntrinsic(extract);
            Assert.That(compiler.lvaTable[0].IsVectorPerElementMask(TYP_SHORT), Is.True);
            Assert.That(compiler.lvaTable[0].IsVectorPerElementMask(TYP_BYTE), Is.True);
            Assert.That(compiler.lvaTable[0].IsVectorPerElementMask(TYP_INT), Is.False);
        }, local: false);
    }

    [TestCase(-1, false)]
    [TestCase(0, true)]
    [TestCase(4095, true)]
    [TestCase(4096, false)]
    public static void LocalNonNullProofFollowsEffectiveValuesWithinTheNullGuard(int offset, bool expected)
    {
        WithCompiler(compiler => {
            MaxUncheckedOffset(compiler) = 4095;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_REF }];
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var assertion = compiler.optAddAssertion(AssertionDsc.CreateLclNonNullAssertion(compiler, 0));
            BitOps.AddElemD(traits, assertions, assertion - 1);
            var local = compiler.gtNewLclvNode(TYP_REF, 0);
            var comma = compiler.gtNewBinaryNode(GT_COMMA, TYP_REF, compiler.gtNewNothingNode(), local);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, comma, compiler.gtNewIconNode(TYP_I_IMPL, offset));

            Assert.That(compiler.optAssertionIsNonNull(address, assertions), Is.EqualTo(expected));
            Assert.That(compiler.optAssertionIsNonNull(local, null), Is.False);
            Assert.That(compiler.optAssertionIsNonNull(compiler.gtNewLclvNode(TYP_REF, 1), assertions), Is.False);
        });
    }

    [TestCase(-1, false)]
    [TestCase(0, true)]
    [TestCase(4095, true)]
    [TestCase(4096, false)]
    public static void KnownNonNullValuesRespectPointerCastsAndNullGuardOffsets(int offset, bool expected)
    {
        WithCompiler(compiler => {
            MaxUncheckedOffset(compiler) = 4095;
            var store = new ValueNumStore(compiler);
            var handle = store.VNForHandle(0x1000, GTF_ICON_OBJ_HDL);
            var cast = store.VNForFuncNoFolding(TYP_I_IMPL, VNFunc.VNF_Cast, handle, store.VNForCastOper(TYP_I_IMPL, false));
            var address = store.VNForFuncNoFolding(TYP_BYREF, VNFunc.VNF_ADD, cast, store.VNForLongCon(offset));
            var peeled = address;
            store.PeelOffsets(ref peeled, out var displacement);

            Assert.That(peeled, Is.EqualTo(handle));
            Assert.That(displacement, Is.EqualTo(offset));
            Assert.That(store.IsKnownNonNull(address), Is.EqualTo(expected));
            Assert.That(store.IsKnownNonNull(ValueNumStore.NoVN), Is.False);
            Assert.That(store.IsKnownNonNull(ValueNumStore.VNForNull()), Is.False);
            Assert.That(store.IsKnownNonNull(store.VNForIntCon(123)), Is.False);
            var load = store.VNForFunc(TYP_REF, VNFunc.VNF_InvariantNonNullLoad, handle);
            Assert.That(store.IsKnownNonNull(load), Is.True);
        });
    }

    [TestCase(-1, false)]
    [TestCase(0, true)]
    [TestCase(4095, true)]
    [TestCase(4096, false)]
    public static void NonNullAssertionsOnlyReuseSafeBasesButStillRecognizeFullAddresses(int offset, bool expected)
    {
        WithCompiler(compiler => {
            MaxUncheckedOffset(compiler) = 4095;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var baseVN = store.VNForExpr(null, TYP_REF);
            var address = store.VNForFuncNoFolding(TYP_BYREF, VNFunc.VNF_ADD, baseVN, store.VNForLongCon(offset));
            var assertions = BitOps.MakeEmpty(traits);
            var baseAssertion = compiler.optAddAssertion(AssertionDsc.CreateVNNonNullAssertion(compiler, baseVN));
            BitOps.AddElemD(traits, assertions, baseAssertion - 1);

            Assert.That(compiler.optAssertionVNIsNonNull(address, assertions, budget: 0), Is.EqualTo(expected));
            var addressAssertion = compiler.optAddAssertion(AssertionDsc.CreateVNNonNullAssertion(compiler, address));
            BitOps.AddElemD(traits, assertions, addressAssertion - 1);
            Assert.That(compiler.optAssertionVNIsNonNull(address, assertions, budget: 0), Is.True);
            Assert.That(compiler.optAssertionVNIsNonNull(ValueNumStore.NoVN, assertions), Is.False);
        }, local: false);
    }

    [Test]
    public static void GlobalTreeNonNullProofUsesNormalConservativeValues()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var handle = store.VNForHandle(0x1000, GTF_ICON_OBJ_HDL);
            var unknown = store.VNForExpr(null, TYP_REF);
            var exception = store.VNForFunc(TYP_REF, VNFunc.VNF_OverflowExc, ValueNumStore.VNForVoid());
            var local = compiler.gtNewLclvNode(TYP_REF, 0);
            local._vnPair = new(handle, store.VNWithExc(unknown, store.VNExcSetSingleton(exception)));
            Assert.That(compiler.optAssertionIsNonNull(local, null), Is.False);

            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(AssertionDsc.CreateVNNonNullAssertion(compiler, unknown));
            BitOps.AddElemD(traits, assertions, index - 1);
            Assert.That(compiler.optAssertionIsNonNull(local, assertions), Is.True);
            var constant = compiler.gtNewIconHandleNode(0x1000, GTF_ICON_OBJ_HDL);
            constant._vnPair.SetBoth(handle);
            Assert.That(compiler.optAssertionIsNonNull(constant, null), Is.True);
        }, local: false);
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, true)]
    public static void PhiNonNullProofUsesSsaDefinitionsAndTheCorrectPredecessorEdge(int trueFacts, bool expected)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            ref var definitions = ref compiler.lvaTable[0].lvPerSsaData;
            var argumentSsa = definitions.AllocSsaNum();
            var phiSsa = definitions.AllocSsaNum();
            var unknown = store.VNForExpr(null, TYP_REF);
            definitions.GetSsaDef(argumentSsa)._vnPair = new(store.VNForHandle(0x1000, GTF_ICON_OBJ_HDL), unknown);
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var assertions = BitOps.MakeEmpty(traits);
            var index = compiler.optAddAssertion(AssertionDsc.CreateVNNonNullAssertion(compiler, unknown));
            BitOps.AddElemD(traits, assertions, index - 1);
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var entry = BasicBlock.New(compiler, BBKinds.BBJ_COND);
            var join = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var other = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var stale = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            compiler.fgFirstBB = entry;
            entry.SetCond(new FlowEdge(entry, join, null), new FlowEdge(entry, other, null));
            join.bbPreds = entry.TrueEdge;
            entry.bbAssertionOut = assertions;
            if (trueFacts != 0)
            {
                var outgoing = new nint[compiler.fgBBNumMax + 1][];
                outgoing[entry.bbNum] = trueFacts == 2 ? assertions : BitOps.MakeEmpty(traits);
                JtrueAssertionOut(compiler) = outgoing;
            }

            var argument = new GenTreePhiArg(TYP_REF, 0, argumentSsa, entry);
            var firstUse = new GenTreePhi.Use(argument);
            var phi = new GenTreePhi(TYP_REF) { FirstUse = firstUse };
            var definition = compiler.gtNewStoreLclVarNode(0, phi);
            definition.SsaNum = phiSsa;
            definitions.GetSsaDef(phiSsa) = new LclSsaVarDsc(join, definition);
            var phiVN = store.VNForPhiDef(TYP_REF, 0, phiSsa, [argumentSsa]);

            Assert.That(store.IsKnownNonNull(phiVN), Is.False);
            Assert.That(compiler.optAssertionVNIsNonNull(phiVN, null, budget: 0), Is.False);
            Assert.That(compiler.optAssertionVNIsNonNull(phiVN, null, budget: 1), Is.EqualTo(expected));
            Assert.That(argument._vnPair, Is.EqualTo(new ValueNumPair()));
            Assert.That(compiler.optGetEdgeAssertions(other, entry), Is.SameAs(assertions));
            Assert.That(BitOps.IsEmpty(traits, compiler.optGetEdgeAssertions(stale, entry)), Is.True);

            firstUse.Next = new GenTreePhi.Use(new GenTreePhiArg(TYP_REF, 0, argumentSsa, stale));
            Assert.That(compiler.optVisitReachingAssertions(phiVN, static (_, _) => Compiler.AssertVisit.Continue),
                Is.EqualTo(Compiler.AssertVisit.Continue));
            stale.bbPreds = new FlowEdge(entry, stale, null);
            Assert.That(compiler.optVisitReachingAssertions(phiVN, static (_, _) => Compiler.AssertVisit.Continue),
                Is.EqualTo(Compiler.AssertVisit.Abort));
            firstUse.Next = null;
            join.bbPreds = new FlowEdge(stale, join, join.bbPreds);
            Assert.That(compiler.optVisitReachingAssertions(phiVN, static (_, _) => Compiler.AssertVisit.Continue),
                Is.EqualTo(Compiler.AssertVisit.Abort));

            definitions.GetSsaDef(argumentSsa)._vnPair.Conservative = store.VNForHandle(0x2000, GTF_ICON_OBJ_HDL);
            Assert.That(store.IsKnownNonNull(phiVN), Is.True);
        }, local: false);
    }

    [TestCase(0L, false, false, true, false)]
    [TestCase(4087L, false, false, true, false)]
    [TestCase(4088L, false, false, false, true)]
    [TestCase(-9L, false, false, false, true)]
    [TestCase(0L, true, false, false, true)]
    [TestCase(0L, true, true, true, false)]
    [TestCase(long.MaxValue, false, false, false, false)]
    public static void FieldNullCheckMarkingPreservesStoreEffectsAndOffsetBoundaries(
        long displacement, bool effectfulStore, bool nonNull, bool elided, bool ordered)
    {
        WithCompiler(compiler => {
            MaxUncheckedOffset(compiler) = 4095;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            compiler.apLocal = BitOps.MakeEmpty(traits);
            if (nonNull)
            {
                var index = compiler.optAddAssertion(AssertionDsc.CreateLclNonNullAssertion(compiler, 0));
                BitOps.AddElemD(traits, compiler.apLocal, index - 1);
            }
            var field = new GenTreeFieldAddr(TYP_BYREF, compiler.gtNewLclvNode(TYP_REF, 0), (CORINFO_FIELD_STRUCT_*)4, 8);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, field, compiler.gtNewIconNode(TYP_I_IMPL, (nint)displacement));
            var indirection = effectfulStore
                ? compiler.gtNewStoreIndNode(TYP_INT, address, compiler.gtNewStoreLclVarNode(1, compiler.gtNewIconNode(TYP_INT, 1)))
                : compiler.gtNewIndir(TYP_INT, address);
            indirection.Flags |= GTF_IND_NONFAULTING;

            compiler.fgMarkAddrModeForFieldAddr(indirection);
            Assert.That((field.Flags & GTF_FLD_TGT_NONFAULTING) != 0, Is.EqualTo(elided));
            Assert.That((indirection.Flags & GTF_IND_NONFAULTING) == 0, Is.EqualTo(elided));
            Assert.That(indirection.HasOrderingSideEffect, Is.EqualTo(ordered));
            Assert.That(indirection.Addr, Is.SameAs(address));
        });
    }

    [TestCase(FieldSeq.FieldKind.SharedStatic, false)]
    [TestCase(FieldSeq.FieldKind.SimpleStaticKnownAddress, true)]
    public static void TreeConstantMetadataRegistersOnlyKnownStaticAddresses(FieldSeq.FieldKind kind, bool registered)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.isFieldStatic = &IsFieldStatic;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var sequence = new FieldSeq((CORINFO_FIELD_STRUCT_*)0x1000, 0x2000, kind);
            var node = compiler.gtNewIconNode(0x2000, sequence);
            compiler.fgValueNumberTreeConst(node);
            Assert.That(store.GetFieldSeqFromAddress(node._vnPair.Liberal), registered ? Is.SameAs(sequence) : Is.Null);
        });
    }

    [Test]
    public static void PairedUnaryAndExceptionCompositionKeepsConservativeValuesIndependent()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var pair = store.VNPairForFunc(TYP_INT, VNFunc.VNF_NEG, new(store.VNForIntCon(1), store.VNForIntCon(2)));
            Assert.That(pair.Liberal, Is.EqualTo(store.VNForIntCon(-1)));
            Assert.That(pair.Conservative, Is.EqualTo(store.VNForIntCon(-2)));
            var exception = store.VNForFunc(TYP_REF, VNFunc.VNF_OverflowExc, ValueNumStore.VNForVoid());
            var wrapped = store.VNPWithExc(pair, new(ValueNumStore.VNForEmptyExcSet(), store.VNExcSetSingleton(exception)));
            Assert.That(wrapped.Liberal, Is.EqualTo(pair.Liberal));
            store.VNUnpackExc(wrapped.Conservative, out var normal, out var exceptions);
            Assert.That(normal, Is.EqualTo(pair.Conservative));
            Assert.That(exceptions, Is.EqualTo(store.VNExcSetSingleton(exception)));
        });
    }

    [Test]
    public static void MorphCompletionKillsOldFactsBeforeGeneratingNewOnes()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            compiler.apLocal = BitOps.MakeEmpty(traits);
            compiler.apLocalPostorder = BitOps.MakeEmpty(traits);
            compiler.fgGlobalMorph = true;
            var first = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 1));
            compiler.fgMorphTreeDone(first);
            var firstIndex = first.AssertionInfo.AssertionIndex;
            Assert.That(BitOps.IsMember(traits, compiler.apLocal, firstIndex - 1), Is.True);
            var read = compiler.gtNewLclvNode(TYP_INT, 0);
            Assert.That(compiler.optAssertionIsSubrange(read, new(Zero, One), compiler.apLocal), Is.Not.Zero);
            compiler.apLocalPostorder = BitOps.MakeCopy(traits, compiler.apLocal);

            var second = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 2));
            compiler.fgMorphTreeDone(second);
            Assert.That(BitOps.IsMember(traits, compiler.apLocal, firstIndex - 1), Is.False);
            Assert.That(BitOps.IsEmpty(traits, compiler.apLocalPostorder), Is.True);
            Assert.That(BitOps.IsMember(traits, compiler.apLocal, second.AssertionInfo.AssertionIndex - 1), Is.True);
            Assert.That(compiler.optAssertionIsSubrange(read, new(Zero, One), compiler.apLocal), Is.Zero);

            second.Flags |= GTF_COLON_COND;
            compiler.optAssertionGen(second);
            Assert.That(second.GeneratesAssertion, Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(true, false)]
    public static void MorphCompletionHonorsGlobalAndEarlyPropagationGates(bool global, bool alreadyPropagated)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            compiler.apLocal = BitOps.MakeEmpty(traits);
            compiler.apLocalPostorder = BitOps.MakeEmpty(traits);
            compiler.fgGlobalMorph = global;
            var tree = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 3));
            compiler.fgMorphTreeDone(tree, alreadyPropagated);
            Assert.That(tree.GeneratesAssertion, Is.EqualTo(global && !alreadyPropagated));
#if DEBUG
            Assert.That(tree.WasMorphed, Is.EqualTo(global));
#endif
        });
    }

    [TestCase(GT_EQ)]
    [TestCase(GT_NE)]
    [TestCase(GT_LT)]
    public static void ConditionalGenerationSeparatesEdgesAndBooleanRanges(genTreeOps oper)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            compiler.apLocal = BitOps.MakeEmpty(traits);
#if DEBUG
            compiler.fgSafeBasicBlockCreation = true;
#endif
            var block = BasicBlock.New(compiler, BBKinds.BBJ_COND);
            var ifTrue = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            var ifFalse = BasicBlock.New(compiler, BBKinds.BBJ_RETURN);
            block.SetCond(new FlowEdge(block, ifTrue, null), new FlowEdge(block, ifFalse, null));
            compiler.compCurBB = block;
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var compare = compiler.gtNewBinaryNode(oper, TYP_INT, local, compiler.gtNewIconNode(TYP_INT, 1));
            var branch = compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, compare);
            compiler.fgAssertionGen(branch);
            Assert.That(compiler.apLocalIfTrue, Is.Not.Null.And.Not.SameAs(compiler.apLocal));
            if (oper == GT_LT)
            {
                Assert.That(branch.GeneratesAssertion, Is.False);
                Assert.That(BitOps.IsEmpty(traits, compiler.apLocalIfTrue), Is.True);
                Assert.That(BitOps.IsEmpty(traits, compiler.apLocal), Is.True);
                return;
            }

            var index = branch.AssertionInfo.AssertionIndex;
            var complement = compiler.optFindComplementary(index);
            Assert.That(complement, Is.Not.Zero);
            Assert.That(BitOps.IsMember(traits, compiler.apLocalIfTrue, index - 1), Is.True);
            Assert.That(BitOps.IsMember(traits, compiler.apLocal, index - 1), Is.False);
            Assert.That(BitOps.IsMember(traits, compiler.apLocal, complement - 1), Is.True);
            var equalEdge = (oper == GT_EQ ? compiler.apLocalIfTrue : compiler.apLocal) ?? throw new InvalidOperationException();
            var unequalEdge = (oper == GT_EQ ? compiler.apLocal : compiler.apLocalIfTrue) ?? throw new InvalidOperationException();
            Assert.That(compiler.optAssertionIsSubrange(local, new(Zero, One), equalEdge), Is.Not.Zero);
            Assert.That(compiler.optAssertionIsSubrange(local, new(Zero, One), unequalEdge), Is.Zero);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void NodeGenerationKeepsNullFactsSeparateFromNonfaultingLoads(bool nonfaulting)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            var load = new GenTreeIndir(GT_IND, TYP_INT, compiler.gtNewLclvNode(TYP_REF, 0));
            if (nonfaulting)
            {
                load.Flags |= GTF_IND_NONFAULTING;
            }

            compiler.optAssertionGen(load);
            Assert.That(load.GeneratesAssertion, Is.EqualTo(!nonfaulting));
            if (!nonfaulting)
            {
                var assertion = compiler.optGetAssertion(load.AssertionInfo.AssertionIndex);
                Assert.That(assertion.Kind, Is.EqualTo(OAK_NOT_EQUAL));
                Assert.That(assertion.Op1.LclNum, Is.Zero);
            }
        });
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public static void CallNullFactsRespectTailCallAndExplicitCheckGates(bool tailCall, bool nullCheck, bool expected)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            var call = compiler.gtNewCallNode(TYP_VOID, gtCallTypes.CT_USER_FUNC, null);
            call.Flags |= GTF_CALL_VIRT_VTABLE;
            if (tailCall)
            {
                call._callMoreFlags |= GenTreeCallFlags.GTF_CALL_M_TAILCALL;
            }

            if (nullCheck)
            {
                call.Flags |= GTF_CALL_NULLCHECK;
            }

            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_REF, 0))
                .WithWellKnownArg(WellKnownArg.ThisPointer));
            compiler.optAssertionGen(call);
            Assert.That(call.GeneratesAssertion, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void GlobalGenerationUsesConservativeFactsForLocalsBoundsAndDivision()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT, IsNeverNegative = true }, new LclVarDsc { Type = TYP_INT }];
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var vn = store.VNForExpr(null, TYP_INT);
            local._vnPair.SetBoth(vn);
            compiler.optAssertionGen(local);
            Assert.That(compiler.optGetAssertion(local.AssertionInfo.AssertionIndex).Kind, Is.EqualTo(OAK_GE));
            var divisor = compiler.gtNewLclvNode(TYP_INT, 1);
            var divisorVN = store.VNForExpr(null, TYP_INT);
            divisor._vnPair = new(store.VNForIntCon(0), divisorVN);
            var divide = compiler.gtNewBinaryNode(GT_DIV, TYP_INT, local, divisor);
            compiler.optAssertionGen(divide);
            var assertion = compiler.optGetAssertion(divide.AssertionInfo.AssertionIndex);
            Assert.That(assertion.Kind, Is.EqualTo(OAK_NOT_EQUAL));
            Assert.That(assertion.Op1.VN, Is.EqualTo(divisorVN));
            var check = new GenTreeBoundsChk(divisor, local, SpecialCodeKind.SCK_RNGCHK_FAIL);
            compiler.optAssertionGen(check);
            Assert.That(compiler.optGetAssertion(check.AssertionInfo.AssertionIndex).IsBoundsCheckNoThrow, Is.True);
            local._vnPair.SetBoth(ValueNumStore.NoVN);
            compiler.optAssertionGen(check);
            Assert.That(check.GeneratesAssertion, Is.False);
        }, local: false);
    }

    [TestCase(CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_DIRECT)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_ARRADDR_ST)]
    [TestCase(CorInfoHelpFunc.CORINFO_HELP_LDELEMA_REF)]
    public static void HelperCallsGenerateAllocationAndArrayBoundsFacts(CorInfoHelpFunc helper)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            compiler.optMethodFlags |= OMF_HAS_NEWARRAY;
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var array = compiler.gtNewLclvNode(TYP_REF, 0);
            array._vnPair.SetBoth(store.VNForExpr(null, TYP_REF));
            var index = compiler.gtNewLclvNode(TYP_INT, 1);
            var indexVN = store.VNForExpr(null, TYP_INT);
            index._vnPair.SetBoth(indexVN);
            var call = compiler.gtNewCallNode(TYP_REF, gtCallTypes.CT_HELPER, Compiler.eeFindHelper(helper));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(array));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(index));
            if (helper != CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_DIRECT)
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewNull()));
            }

            compiler.optAssertionGen(call);
            var assertion = compiler.optGetAssertion(call.AssertionInfo.AssertionIndex);
            Assert.That(assertion.Op1.VN, Is.EqualTo(indexVN));
            if (helper == CorInfoHelpFunc.CORINFO_HELP_NEWARR_1_DIRECT)
            {
                Assert.That(assertion.Kind, Is.EqualTo(OAK_GE));
            }
            else
            {
                Assert.That(assertion.IsBoundsCheckNoThrow, Is.True);
                Assert.That(assertion.Op2.VN, Is.EqualTo(store.VNForFunc(TYP_INT, VNFunc.VNF_ARR_LENGTH, array._vnPair.Conservative)));
            }
        }, local: false);
    }

    [TestCase(VNFunc.VNF_NEG, int.MinValue, int.MinValue)]
    [TestCase(VNFunc.VNF_NOT, int.MinValue, int.MaxValue)]
    [TestCase(VNFunc.VNF_BSWAP16, -32767, 384)]
    [TestCase(VNFunc.VNF_BSWAP, 128, int.MinValue)]
    public static void UnaryIntValueNumbersUseNativeWidth(VNFunc func, int value, int expected)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var input = store.VNForIntCon(value);
            var result = store.VNForFunc(TYP_INT, func, input);
            Assert.That(result, Is.EqualTo(store.VNForIntCon(expected)));
            Assert.That(store.VNForFunc(TYP_INT, func, input), Is.EqualTo(result));
        });
    }

    [TestCase(VNFunc.VNF_NEG, long.MinValue, long.MinValue)]
    [TestCase(VNFunc.VNF_NOT, long.MinValue, long.MaxValue)]
    [TestCase(VNFunc.VNF_BSWAP16, -32767L, 384L)]
    [TestCase(VNFunc.VNF_BSWAP, 128L, long.MinValue)]
    public static void UnaryLongValueNumbersUseNativeWidth(VNFunc func, long value, long expected)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            Assert.That(store.VNForFunc(TYP_LONG, func, store.VNForLongCon(value)), Is.EqualTo(store.VNForLongCon(expected)));
        });
    }

    [TestCase(0L, 0)]
    [TestCase(long.MinValue, int.MinValue)]
    [TestCase(0x7FF8123456789ABCL, 0x7FC12345)]
    [TestCase(0x7FF0000000000001L, 0x7F800001)]
    [TestCase(0x7FF0000000000000L, 0x7F800000)]
    public static void UnaryFloatingNegationPreservesPayloadAndFlipsSign(long doubleBits, int floatBits)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var doubleVN = store.VNForFunc(TYP_DOUBLE, VNFunc.VNF_NEG, store.VNForDoubleCon(BitConverter.Int64BitsToDouble(doubleBits)));
            var floatVN = store.VNForFunc(TYP_FLOAT, VNFunc.VNF_NEG, store.VNForFloatCon(BitConverter.Int32BitsToSingle(floatBits)));
            Assert.That(BitConverter.DoubleToInt64Bits(store.GetConstantDouble(doubleVN)), Is.EqualTo(doubleBits ^ long.MinValue));
            Assert.That(BitConverter.SingleToInt32Bits(store.GetConstantSingle(floatVN)), Is.EqualTo(floatBits ^ int.MinValue));
        });
    }

    [Test]
    public static void UnaryInterningCancelsDoubleNotAndRetainsStableFunctionViews()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var input = store.VNForExpr(null, TYP_INT);
            var first = store.VNForFunc(TYP_INT, VNFunc.VNF_NOT, input);
            Assert.That(first, Is.EqualTo(input + 1));
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(first, ref app), Is.True);
            for (var index = 0; index < 300; index++)
            {
                _ = store.VNForFunc(TYP_INT, VNFunc.VNF_NOT, store.VNForExpr(null, TYP_INT));
            }

            Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_NOT));
            Assert.That(app.GetArg(0), Is.EqualTo(input));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_NOT, input), Is.EqualTo(first));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_NOT, first), Is.EqualTo(input));
            var nullLength = store.VNForFunc(TYP_INT, VNFunc.VNF_ARR_LENGTH, ValueNumStore.VNForNull());
            Assert.That(store.VNHasExc(nullLength), Is.False);
            Assert.That(store.IsVNConstant(nullLength), Is.False);
        });
    }

    [Test]
    public static void ExceptionSetsUnionInOrderAndFlattenValueWrappers()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var first = store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, ValueNumStore.VNForNull());
            var second = store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, store.VNForExpr(null, TYP_REF));
            var third = store.VNForFunc(TYP_REF, VNFunc.VNF_NullPtrExc, store.VNForExpr(null, TYP_REF));
            var one = store.VNExcSetSingleton(first);
            var two = store.VNExcSetSingleton(second);
            var three = store.VNExcSetSingleton(third);
            var union = store.VNExcSetUnion(store.VNExcSetUnion(three, one), store.VNExcSetUnion(two, three));
            var cursor = union;
            int[] ordered = [first, second, third];
            foreach (var expected in ordered)
            {
                var app = new VNFuncApp();
                Assert.That(store.GetVNFunc(cursor, ref app), Is.True);
                Assert.That(app.Func, Is.EqualTo(VNFunc.VNF_ExcSetCons));
                Assert.That(app.GetArg(0), Is.EqualTo(expected));
                cursor = app.GetArg(1);
            }

            Assert.That(cursor, Is.EqualTo(ValueNumStore.VNForEmptyExcSet()));
            Assert.That(store.VNExcSetUnion(union, union), Is.EqualTo(union));
            var value = store.VNForIntCon(42);
            var wrapped = store.VNWithExc(store.VNWithExc(value, one), union);
            store.VNUnpackExc(wrapped, out var normal, out var exceptions);
            Assert.That(normal, Is.EqualTo(value));
            Assert.That(exceptions, Is.EqualTo(union));
            Assert.That(store.VNHasExc(wrapped), Is.True);
            Assert.That(store.VNExceptionSet(wrapped), Is.EqualTo(union));
            Assert.That(store.VNExceptionSet(value), Is.EqualTo(ValueNumStore.VNForEmptyExcSet()));
            Assert.That(store.VNExceptionSet(ValueNumStore.NoVN), Is.EqualTo(ValueNumStore.VNForEmptyExcSet()));
            Assert.That(store.VNWithExc(wrapped, ValueNumStore.VNForEmptyExcSet()), Is.EqualTo(wrapped));
            store.VNUnpackExc(value, out normal, out exceptions);
            Assert.That(normal, Is.EqualTo(value));
            Assert.That(exceptions, Is.EqualTo(ValueNumStore.VNForEmptyExcSet()));
        });
    }

    [TestCase(VNFunc.VNF_JitNewArr)]
    [TestCase(VNFunc.VNF_JitNewLclArr)]
    [TestCase(VNFunc.VNF_JitReadyToRunNewArr)]
    [TestCase(VNFunc.VNF_JitReadyToRunNewLclArr)]
    public static void NewArrayValueNumbersRespectConstantLengthBounds(VNFunc func)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            long[] sizes = [-1, 0, int.MaxValue, (long)int.MaxValue + 1];
            foreach (var size in sizes)
            {
                var array = StoreFunctionRecord(store, TYP_REF, func, ValueNumStore.VNForNull(),
                    store.VNForLongCon(size), store.VNForExpr(null, TYP_REF));
                var valid = size is >= 0 and <= int.MaxValue;
                Assert.That(store.TryGetNewArrSize(array, out var actual), Is.EqualTo(valid));
                Assert.That(actual, Is.EqualTo(valid ? (int)size : 0));
                var length = store.VNForFunc(TYP_INT, VNFunc.VNF_ARR_LENGTH, array);
                var folds = valid || ((func == VNFunc.VNF_JitNewArr) && (size is >= int.MinValue and <= int.MaxValue));
                Assert.That(store.IsVNConstant(length), Is.EqualTo(folds));
                if (folds)
                {
                    Assert.That(store.GetConstantInt32(length), Is.EqualTo((int)size));
                }
            }
        });
    }

    [TestCase(VNFunc.VNF_JitNewArr)]
    [TestCase(VNFunc.VNF_StrFastAllocate)]
    public static void AllocationLengthsNormalizeSignedWidening(VNFunc func)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var size = store.VNForExpr(null, TYP_INT);
            var widened = store.VNForFuncNoFolding(TYP_LONG, VNFunc.VNF_Cast, size, store.VNForCastOper(TYP_LONG, false));
            var array = StoreFunctionRecord(store, TYP_REF, func, ValueNumStore.VNForNull(),
                widened, store.VNForExpr(null, TYP_REF));
            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_ARR_LENGTH, array), Is.EqualTo(size));
        });
    }

    [TestCase(0)]
    [TestCase(42)]
    [TestCase(-1)]
    public static void FrozenObjectLengthsUseAndCacheEEAnswers(int length)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getArrayOrStringLength = &GetArrayLength;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var metadata = new ArrayMetadata { Length = length };
            var store = new ValueNumStore(compiler);
            var obj = store.VNForHandle((nint)(&metadata), GTF_ICON_OBJ_HDL);
            var result = store.VNForFunc(TYP_INT, VNFunc.VNF_ARR_LENGTH, obj);
            Assert.That(store.IsVNConstant(result), Is.EqualTo(length >= 0));
            if (length >= 0)
            {
                Assert.That(store.GetConstantInt32(result), Is.EqualTo(length));
            }

            Assert.That(store.VNForFunc(TYP_INT, VNFunc.VNF_ARR_LENGTH, obj), Is.EqualTo(result));
            Assert.That(metadata.Calls, Is.EqualTo(1));
        });
    }

    [TestCase(true, 17)]
    [TestCase(true, -1)]
    [TestCase(false, 17)]
    public static void ReadonlyFieldLengthsPreserveEEContractAndCanonicalIdentity(bool readable, int length)
    {
        WithCompiler(compiler => {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getArrayOrStringLength = &GetArrayLength;
            vtable.Base.Base.isFieldStatic = &IsFieldStatic;
            vtable.Base.getStaticFieldContent =
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_STRUCT_*, byte*, int, int, bool, byte>)
                (delegate* unmanaged[MemberFunction]<ICorJitInfo*, CORINFO_FIELD_STRUCT_*, byte*, int, int, byte, byte>)&GetStaticFieldContent;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            var array = new ArrayMetadata { Length = length };
            var field = new FieldMetadata { Object = (nint)(&array), Readable = readable };
            var sequence = new FieldSeq((CORINFO_FIELD_STRUCT_*)&field, 0, FieldSeq.FieldKind.SharedStatic);
            var other = new FieldSeq((CORINFO_FIELD_STRUCT_*)&field, 0, FieldSeq.FieldKind.SharedStatic);
            var store = new ValueNumStore(compiler);
            var sequenceVN = store.VNForFieldSeq(sequence);
            Assert.That(store.VNForFieldSeq(sequence), Is.EqualTo(sequenceVN));
            Assert.That(store.VNForFieldSeq(other), Is.Not.EqualTo(sequenceVN));
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            Assert.That(store.FieldSeqVNToFieldSeq(sequenceVN), Is.SameAs(sequence));
            Assert.That(store.FieldSeqVNToFieldSeq(store.VNForFieldSeq(null)), Is.Null);
            var load = store.VNForFunc(TYP_REF, VNFunc.VNF_InvariantNonNullLoad, sequenceVN);
            var result = store.VNForFunc(TYP_INT, VNFunc.VNF_ARR_LENGTH, load);
            Assert.That(field.ValidRequest, Is.True);
            Assert.That(field.Calls, Is.EqualTo(1));
            Assert.That(array.Calls, Is.EqualTo(readable ? 1 : 0));
            Assert.That(store.IsVNConstant(result), Is.EqualTo(readable && (length >= 0)));
            if (readable && (length >= 0))
            {
                Assert.That(store.GetConstantInt32(result), Is.EqualTo(length));
            }
        });
    }

#if DEBUG
    [Test]
    public static void NullFieldSequenceDumpUsesNativeSymbolicFormat()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            int vn;
            try
            {
                s_jitstdout = writer;
                compiler.verbose = true;
                vn = store.VNForFieldSeq(null);
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
                compiler.verbose = false;
            }

            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo($"     {{ }} is ${vn:x}{Environment.NewLine}"));
        });
    }
#endif

    private struct ArrayMetadata
    {
        public int Length;
        public int Calls;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetAssertionObjectType(ICorJitInfo* info, CORINFO_OBJECT_STRUCT_* handle)
        => (nint)handle == 0x4000 ? (CORINFO_CLASS_STRUCT_*)0x2000 : null;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CORINFO_CLASS_STRUCT_* GetAssertionBuiltinClass(ICorJitInfo* info, CorInfoClassId id)
        => id is CorInfoClassId.CLASSID_RUNTIME_TYPE ? (CORINFO_CLASS_STRUCT_*)0x3000 : null;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static TypeCompareState CompareAssertionTypes(ICorJitInfo* info, CORINFO_CLASS_STRUCT_* from, CORINFO_CLASS_STRUCT_* to)
        => ((nint)from == 0x2000) && ((nint)to == 0x2000) ? TypeCompareState.Must : TypeCompareState.May;

    private struct FieldMetadata
    {
        public nint Object;
        public int Calls;
        public bool Readable;
        public bool ValidRequest;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int GetArrayLength(ICorJitInfo* info, CORINFO_OBJECT_STRUCT_* handle)
    {
        var metadata = (ArrayMetadata*)handle;
        metadata->Calls++;
        return metadata->Length;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte IsFieldStatic(ICorJitInfo* info, CORINFO_FIELD_STRUCT_* handle) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static byte GetStaticFieldContent(ICorJitInfo* info, CORINFO_FIELD_STRUCT_* handle, byte* buffer,
        int size, int offset, byte ignoreMovableObjects)
    {
        var metadata = (FieldMetadata*)handle;
        metadata->Calls++;
        metadata->ValidRequest = (size == TARGET_POINTER_SIZE) && (offset == 0) && (ignoreMovableObjects == 0);
        if (!metadata->Readable || !metadata->ValidRequest)
        {
            return 0;
        }

        Unsafe.CopyBlockUnaligned(buffer, &metadata->Object, (uint)size);
        return 1;
    }

    private static int StoreFunctionRecord(ValueNumStore store, var_types type, VNFunc func, params int[] arguments)
    {
        var allocator = typeof(ValueNumStore).GetMethod("GetAllocChunk", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        var attribute = Enum.Parse(allocator.GetParameters()[1].ParameterType, $"CEA_Func{arguments.Length}");
        var chunk = allocator.Invoke(store, [type, attribute]) ?? throw new InvalidOperationException();
        var chunkType = typeof(ValueNumStore).GetNestedType("Chunk", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        var allocVN = chunkType.GetMethod("AllocVN") ?? throw new InvalidOperationException();
        var offset = (int)(allocVN.Invoke(chunk, null) ?? throw new InvalidOperationException());
        var funcApp = chunkType.GetMethod("FuncApp") ?? throw new InvalidOperationException();
        var record = (Memory<int>)(funcApp.Invoke(chunk, [offset, arguments.Length]) ?? throw new InvalidOperationException());
        record.Span[0] = (int)func;
        arguments.CopyTo(record.Span[1..]);
        var baseVN = chunkType.GetField("BaseVN") ?? throw new InvalidOperationException();
        return unchecked((int)(baseVN.GetValue(chunk) ?? throw new InvalidOperationException()) + offset);
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void VectorValueNumbersOwnExactPayloadAndZeroPadRetrieval(var_types type)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var bytes = new byte[type.Size];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = (byte)(index + 1);
            }

            var original = (byte[])bytes.Clone();
            var vn = store.VNForGenericCon(type, bytes);
            compiler.vnStore = store;
            var node = new GenTreeVecCon(type);
            bytes.CopyTo(node.SimdVal.AsSpan<byte>());
            compiler.fgValueNumberTreeConst(node);
            Assert.That(node._vnPair, Is.EqualTo(new ValueNumPair(vn, vn)));
            Assert.That(store.VNForGenericCon(type, bytes), Is.EqualTo(vn));
            bytes[^1] ^= 128;
            Assert.That(store.VNForGenericCon(type, bytes), Is.Not.EqualTo(vn));
            var vector = store.GetConstantSimd(vn);
            Assert.That(vector.AsSpan<byte>()[..type.Size].ToArray(), Is.EqualTo(original));
            Assert.That(vector.AsSpan<byte>()[type.Size..].ToArray(), Is.All.Zero);
            Assert.That(store.GetConstantSimd(store.VNZeroForType(type)).IsZero, Is.True);
        });
    }

    [Test]
    public static void GenericValueNumbersPreserveSignednessHandlesAndMaskBits()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            byte[] bits = [255, 255, 255, 255, 255, 255, 255, 255];
            Assert.That(store.GetConstantInt32(store.VNForGenericCon(TYP_BYTE, bits)), Is.EqualTo(-1));
            Assert.That(store.GetConstantInt32(store.VNForGenericCon(TYP_UBYTE, bits)), Is.EqualTo(255));
            Assert.That(store.GetConstantInt32(store.VNForGenericCon(TYP_USHORT, bits)), Is.EqualTo(65535));
            Assert.That(store.GetConstantInt32(store.VNForGenericCon(TYP_UINT, bits)), Is.EqualTo(-1));
            Assert.That(store.GetConstantInt64(store.VNForGenericCon(TYP_ULONG, bits)), Is.EqualTo(-1L));
            var mask = store.VNForGenericCon(TYP_MASK, bits);
            compiler.vnStore = store;
            var maskNode = new GenTreeMskCon(store.GetConstantSimdMask(mask));
            compiler.fgValueNumberTreeConst(maskNode);
            Assert.That(maskNode._vnPair, Is.EqualTo(new ValueNumPair(mask, mask)));
            Assert.That(store.GetConstantSimdMask(mask).RawBits, Is.EqualTo(-1L));
            Assert.That(store.VNForSimdMaskCon(store.GetConstantSimdMask(mask)), Is.EqualTo(mask));
            var negativeZero = store.VNForGenericCon(TYP_DOUBLE, BitConverter.GetBytes(-0.0));
            Assert.That(BitConverter.DoubleToInt64Bits(store.GetConstantDouble(negativeZero)), Is.EqualTo(long.MinValue));
            Assert.That(store.VNZeroForType(TYP_DOUBLE), Is.Not.EqualTo(negativeZero));
            Assert.That(store.VNZeroForType(TYP_UBYTE), Is.EqualTo(store.VNForIntCon(0)));
            Assert.That(store.VNZeroForType(TYP_BYREF), Is.EqualTo(store.VNForByrefCon(0)));
            Assert.That(store.VNZeroForType(TYP_REF), Is.EqualTo(ValueNumStore.VNForNull()));
            var handle = store.VNForGenericCon(TYP_REF, BitConverter.GetBytes(42L));
            Assert.That(store.IsVNObjHandle(handle), Is.True);
            Assert.That(store.ConstantValue<nint>(handle), Is.EqualTo((nint)42));
        });
    }

    [Test]
    public static void WideningVNNormalizationRequiresUnsignedSourceNonnegativity()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var unknown = store.VNForExpr(null, TYP_INT);
            var signed = store.VNForFuncNoFolding(TYP_LONG, VNFunc.VNF_Cast, unknown, store.VNForCastOper(TYP_LONG, false));
            var unsigned = store.VNForFuncNoFolding(TYP_LONG, VNFunc.VNF_Cast, unknown, store.VNForCastOper(TYP_LONG, true));
            Assert.That(store.VNIgnoreIntToLongCast(signed), Is.EqualTo(unknown));
            Assert.That(store.VNIgnoreIntToLongCast(unsigned), Is.EqualTo(unsigned));
            var positive = store.VNForIntCon(1);
            var positiveCast = store.VNForFuncNoFolding(TYP_LONG, VNFunc.VNF_Cast, positive, store.VNForCastOper(TYP_LONG, true));
            Assert.That(store.VNIgnoreIntToLongCast(positiveCast), Is.EqualTo(positive));
            Assert.That(store.VNIgnoreIntToLongCast(store.VNForLongCon(int.MinValue)), Is.EqualTo(store.VNForIntCon(int.MinValue)));
            var wide = store.VNForLongCon(long.MaxValue);
            Assert.That(store.VNIgnoreIntToLongCast(wide), Is.EqualTo(wide));
        });
    }

    [TestCase(-1, false)]
    [TestCase(0, true)]
    [TestCase(1, false)]
    public static void NonNullAssertionsRespectUnsignedOffsetLimit(int offset, bool expected)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_INT }];
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, compiler.gtNewLclvNode(TYP_REF, 0),
                compiler.gtNewIconNode(TYP_I_IMPL, offset));
            Assert.That(compiler.fgIsBigOffset(offset), Is.EqualTo(!expected));
            Assert.That(compiler.optCreateAssertion(address, null, false) != 0, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void AssertionCreationPreservesSmallLocalAndCopyNormalization()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_UBYTE }, new LclVarDsc { Type = TYP_UBYTE, lvIsParam = true }];
            var local = compiler.gtNewLclvNode(TYP_INT, 0);
            var large = compiler.gtNewIconNode(TYP_INT, 256);
            Assert.That(compiler.optCreateAssertion(local, large, true), Is.Zero);
            var store = compiler.gtNewStoreLclVarNode(0, large);
            var index = compiler.optCreateAssertion(store, large, true);
            Assert.That(compiler.optGetAssertion(index).Op2.IntConstant, Is.EqualTo((nint)0));
            var other = compiler.gtNewLclvNode(TYP_INT, 1);
            Assert.That(compiler.optCreateAssertion(local, other, true), Is.Zero);
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[1].lvRedefinedInEmbeddedStatement = true;
            Assert.That(compiler.optCreateAssertion(local, other, true), Is.Zero);
            compiler.lvaTable[1].lvRedefinedInEmbeddedStatement = false;
            Assert.That(compiler.optGetAssertion(compiler.optCreateAssertion(local, other, true)).Op2.Kind, Is.EqualTo(O2K_LCLVAR_COPY));
            Assert.That(compiler.optCreateAssertion(local, compiler.gtNewDconNode(TYP_DOUBLE, double.NaN), true), Is.Zero);
        });
    }

    [TestCase(TYP_INT, true)]
    [TestCase(TYP_FLOAT, false)]
    public static void VectorBranchAssertionsRequireBitwiseEquality(var_types baseType, bool expected)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = [new LclVarDsc { Type = TYP_SIMD16 }, new LclVarDsc { Type = TYP_INT }];
            var vector = compiler.gtNewLclvNode(TYP_SIMD16, 0);
            var intrinsic = new GenTreeHWIntrinsic(TYP_INT, NamedIntrinsic.NI_Vector_op_Equality, baseType, 16,
                vector, new GenTreeVecCon(TYP_SIMD16));
            var compare = compiler.gtNewBinaryNode(GT_EQ, TYP_INT, intrinsic, compiler.gtNewIconNode(TYP_INT, 1));
            var info = compiler.optAssertionGenJtrue(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, compare));
            Assert.That(info.HasAssertion, Is.EqualTo(expected));
            if (expected)
            {
                Assert.That(compiler.optGetAssertion(info.AssertionIndex).Kind, Is.EqualTo(OAK_EQUAL));
            }
        });
    }

    [TestCase(GT_EQ, false)]
    [TestCase(GT_NE, true)]
    public static void ExactTypeAssertionsLookThroughCommaStoresAndRetainEdge(genTreeOps oper, bool nextEdge)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            compiler.lvaTable = [new LclVarDsc { Type = TYP_REF }, new LclVarDsc { Type = TYP_I_IMPL }];
            var obj = compiler.gtNewLclvNode(TYP_REF, 0);
            var objVN = store.VNForExpr(null, TYP_REF);
            obj._vnPair.SetBoth(objVN);
            var indirection = new GenTreeIndir(GT_IND, TYP_I_IMPL, obj);
            var save = compiler.gtNewStoreLclVarNode(1, indirection);
            var comma = compiler.gtNewBinaryNode(GT_COMMA, TYP_I_IMPL, save, compiler.gtNewLclvNode(TYP_I_IMPL, 1));
            Assert.That(comma.CommaStoreVal, Is.SameAs(indirection));
            var handle = compiler.gtNewIconNode(TYP_I_IMPL, 123);
            handle._vnPair.SetBoth(store.VNForHandle(123, GTF_ICON_CLASS_HDL));
            var compare = compiler.gtNewBinaryNode(oper, TYP_INT, comma, handle);
            var info = compiler.optAssertionGenJtrue(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, compare));
            Assert.That(info.HasAssertion, Is.True);
            Assert.That(info.AssertionHoldsOnFalseEdge, Is.EqualTo(nextEdge));
            Assert.That(compiler.optGetAssertion(info.AssertionIndex).Op1.Kind, Is.EqualTo(Compiler.optOp1Kind.O1K_EXACT_TYPE));
            Assert.That(compiler.optFindComplementary(info.AssertionIndex), Is.Zero);
        }, local: false);
    }

    [TestCase(VNFunc.VNF_LT_UN, false, false)]
    [TestCase(VNFunc.VNF_GE_UN, false, true)]
    [TestCase(VNFunc.VNF_GT_UN, true, false)]
    [TestCase(VNFunc.VNF_LE_UN, true, true)]
    public static void BoundsAssertionsRetainUnsignedEdgePolarity(VNFunc func, bool swapped, bool nextEdge)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var index = store.VNForExpr(null, TYP_INT);
            var bound = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(bound);
            var info = GenerateBounds(compiler, store, func, swapped ? bound : index, swapped ? index : bound);
            Assert.That(info.HasAssertion, Is.True);
            Assert.That(info.AssertionHoldsOnFalseEdge, Is.EqualTo(nextEdge));
            var assertion = compiler.optGetAssertion(info.AssertionIndex);
            Assert.That(assertion.Kind, Is.EqualTo(OAK_LT_UN));
            Assert.That(assertion.Op1.VN, Is.EqualTo(index));
            Assert.That(assertion.Op2.VN, Is.EqualTo(bound));
            Assert.That(assertion.Op2.IsVNNeverNegative, Is.True);
        }, local: false);
    }

    [Test]
    public static void CheckedBoundQueriesPreserveCastAndConstantCanonicalization()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var bound = store.VNForExpr(null, TYP_INT);
            store.SetVNIsCheckedBound(bound);
            var five = store.VNForIntCon(5);
            var info = GenerateBounds(compiler, store, VNFunc.VNF_GE_UN, bound, five);
            Assert.That(info.AssertionHoldsOnFalseEdge, Is.False);
            var assertion = compiler.optGetAssertion(info.AssertionIndex);
            Assert.That(store.ConstantValue<int>(assertion.Op1.VN), Is.EqualTo(4));
            Assert.That(assertion.Op2.VN, Is.EqualTo(bound));
            var cast = store.VNForFuncNoFolding(TYP_LONG, VNFunc.VNF_Cast, bound, store.VNForCastOper(TYP_LONG, true));
            var comparison = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_LT_UN, store.VNForLongCon(0), cast);
            var decoded = new ValueNumStore.UnsignedCompareCheckedBoundInfo();
            Assert.That(store.IsVNUnsignedCompareCheckedBound(comparison, ref decoded), Is.True);
            Assert.That(decoded.VNBound, Is.EqualTo(bound));
            var subtract = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_SUB, bound, five);
            var checkedBound = ValueNumStore.NoVN;
            var addend = 0;
            Assert.That(store.IsVNCheckedBoundAddConst(subtract, ref checkedBound, ref addend), Is.True);
            Assert.That(addend, Is.EqualTo(-5));
            var overflow = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_SUB, bound, store.VNForIntCon(int.MinValue));
            Assert.That(store.IsVNCheckedBoundAddConst(overflow, ref checkedBound, ref addend), Is.False);
            Assert.That(checkedBound, Is.EqualTo(bound));
            Assert.That(addend, Is.EqualTo(-5));
        }, local: false);
    }

    [Test]
    public static void BoundsGenerationRespectsEqualityAndTablePressureGates()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var left = store.VNForExpr(null, TYP_INT);
            var right = store.VNForExpr(null, TYP_INT);
            Assert.That(GenerateBounds(compiler, store, VNFunc.VNF_LT, left, right).HasAssertion, Is.False);
            Assert.That(GenerateBounds(compiler, store, VNFunc.VNF_LT, left, store.VNForIntCon(10)).HasAssertion, Is.True);
            Assert.That(GenerateBounds(compiler, store, VNFunc.VNF_LT, left, right).HasAssertion, Is.True);
            store.SetVNIsCheckedBound(right);
            Assert.That(GenerateBounds(compiler, store, VNFunc.VNF_NE, right, store.VNForIntCon(0)).HasAssertion, Is.False);
            Assert.That(GenerateBounds(compiler, store, VNFunc.VNF_NE, left, right).HasAssertion, Is.True);
            var addition = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_ADD, right, store.VNForIntCon(1));
            Assert.That(GenerateBounds(compiler, store, VNFunc.VNF_EQ, left, addition).HasAssertion, Is.False);
            var info = GenerateBounds(compiler, store, VNFunc.VNF_LT, left, addition);
            Assert.That(info.HasAssertion, Is.True);
            Assert.That(compiler.optGetAssertion(info.AssertionIndex).Op2.Cns, Is.EqualTo(1));
            Assert.That(GenerateBounds(compiler, store, VNFunc.VNF_LT_UN, left, store.VNForIntCon(0)).HasAssertion, Is.False);
        }, local: false);
    }

    [Test]
    public static void PhiQueriesOwnArgumentsAndTraverseConservativeValuesInNativeOrder()
    {
        WithCompiler(compiler => {
            compiler.lvaTable = new LclVarDsc[2];
            ref var definitions = ref compiler.lvaTable[0].lvPerSsaData;
            var first = definitions.AllocSsaNum();
            var second = definitions.AllocSsaNum();
            var third = definitions.AllocSsaNum();
            var fourth = definitions.AllocSsaNum();
            var store = new ValueNumStore(compiler);
            var seven = store.VNForIntCon(7);
            var eleven = store.VNForIntCon(11);
            var negative = store.VNForIntCon(-1);
            int[] arguments = [first, second, first];
            var phi = store.VNForPhiDef(TYP_INT, 0, fourth, arguments);
            var nested = store.VNForPhiDef(TYP_INT, 0, second, [third, fourth]);
            arguments[0] = fourth;
            definitions.GetSsaDef(first)._vnPair = new(negative, seven);
            definitions.GetSsaDef(second)._vnPair.SetBoth(nested);
            definitions.GetSsaDef(third)._vnPair.SetBoth(eleven);
            definitions.GetSsaDef(fourth)._vnPair.SetBoth(phi);
            var visited = new System.Collections.Generic.List<int>();
            Assert.That(store.VNVisitReachingVNs(phi, vn => {
                visited.Add(vn);
                return ValueNumStore.VNVisit.Continue;
            }), Is.EqualTo(ValueNumStore.VNVisit.Continue));
            int[] expected = [eleven, seven];
            Assert.That(visited, Is.EqualTo(expected));
            Assert.That(store.IsVNNeverNegative(phi), Is.True);
            definitions.GetSsaDef(third)._vnPair.Conservative = negative;
            Assert.That(store.IsVNNeverNegative(phi), Is.False);
            VNPhiDef view = default;
            Assert.That(store.GetPhiDef(phi, ref view), Is.True);
            Assert.That(store.GetPhiDef(seven, ref view), Is.False);
            Assert.That(view.SsaArgs.Span[0], Is.EqualTo(first));
            definitions.Reset();
            Assert.That(definitions.AllocSsaNum(), Is.EqualTo(first));
            Assert.That(definitions.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public static void NonnegativeVNsSupplySignedComparisonAndCheckedBoundFacts()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var unknown = store.VNForExpr(null, TYP_INT);
            var zero = store.VNForIntCon(0);
            var length = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_MDArrLength,
                store.VNForExpr(null, TYP_REF), zero);
            var comparison = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_LT, unknown, zero);
            Assert.That(store.IsVNNeverNegative(ValueNumStore.NoVN), Is.False);
            Assert.That(store.IsVNNeverNegative(store.VNForLongCon(long.MinValue)), Is.False);
            Assert.That(store.IsVNNeverNegative(store.VNForLongCon(long.MaxValue)), Is.True);
            Assert.That(store.IsVNNeverNegative(store.VNForFloatCon(1)), Is.False);
            Assert.That(store.IsVNNeverNegative(unknown), Is.False);
            Assert.That(store.IsVNNeverNegative(length), Is.True);
            Assert.That(store.IsVNNeverNegative(comparison), Is.True);
            var bounds = AssertionDsc.CreateCompareCheckedBound(compiler, VNFunc.VNF_LT, unknown, length, -5);
            Assert.That(bounds.Op2.IsVNNeverNegative, Is.True);
            Assert.That(bounds.Op2.Cns, Is.EqualTo(-5));
            Assert.That(AssertionDsc.CreateRelopVN(compiler, VNFunc.VNF_LT, unknown, comparison).Op2.IsVNNeverNegative, Is.True);
            Assert.That(AssertionDsc.CreateRelopVN(compiler, VNFunc.VNF_LT, length, unknown).Op2.IsVNNeverNegative, Is.False);
        }, local: false);
    }

    [TestCase(16, TYP_UBYTE, true)]
    [TestCase(32, TYP_UBYTE, false)]
    [TestCase(64, TYP_INT, true)]
    [TestCase(64, TYP_SHORT, false)]
    public static void IntrinsicNonnegativityRetainsNativeElementCountThreshold(int size, var_types baseType, bool expected)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var simdType = store.VNForFuncNoFolding(TYP_REF, VNFunc.VNF_SimdType,
                store.VNForIntCon(size), store.VNForIntCon((int)baseType));
            var vector = store.VNForExpr(null, TYP_SIMD16);
            var mask = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_HWI_Vector_ExtractMostSignificantBits, vector, simdType);
            Assert.That(store.IsVNNeverNegative(mask), Is.EqualTo(expected));
        });
    }

    [TestCase(31, VNFunc.VNF_HWI_AVX2_LeadingZeroCount, TYP_INT)]
    [TestCase(63, VNFunc.VNF_HWI_AVX2_X64_LeadingZeroCount, TYP_LONG)]
    public static void Log2ValueNumbersRecognizeNativePattern(int xorBy, VNFunc lzcnt, var_types type)
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            var variable = store.VNForExpr(null, type);
            var one = type == TYP_INT ? store.VNForIntCon(1) : store.VNForLongCon(1);
            var operand = store.VNForFuncNoFolding(type, VNFunc.VNF_OR, variable, one);
            var simdType = store.VNForFuncNoFolding(TYP_REF, VNFunc.VNF_SimdType,
                store.VNForIntCon(0), store.VNForIntCon((int)type));
            var count = store.VNForFuncNoFolding(type, lzcnt, operand, simdType);
            var constant = type == TYP_INT ? store.VNForIntCon(xorBy) : store.VNForLongCon(xorBy);
            var log2 = store.VNForFuncNoFolding(type, VNFunc.VNF_XOR, count, constant);
            var upperBound = -1;
            Assert.That(store.IsVNLog2(log2, ref upperBound), Is.True);
            Assert.That(upperBound, Is.EqualTo(xorBy));
            Assert.That(store.IsVNNeverNegative(log2), Is.True);
            Assert.That(store.IsVNLog2(variable, ref upperBound), Is.False);
            Assert.That(upperBound, Is.EqualTo(xorBy));
        });
    }

    [Test]
    public static void ValueNumberChunksRetainReservedIdsAndAllocationOrder()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            Assert.That(store.VNIsValid(ValueNumStore.VNForNull()), Is.True);
            Assert.That(store.VNIsValid(ValueNumStore.VNForVoid()), Is.True);
            Assert.That(store.VNIsValid(ValueNumStore.VNForEmptyExcSet()), Is.True);
            Assert.That(store.VNIsValid(3), Is.False);
            Assert.That(store.VNIsValid(ValueNumStore.NoVN), Is.False);
            Assert.That(store.IsVNConstant(ValueNumStore.VNForVoid()), Is.False);
            for (var index = 0; index < 65; index++)
            {
                var vn = store.VNForIntCon(1000 + index);
                Assert.That(vn, Is.EqualTo(64 + index));
                Assert.That(store.ConstantValue<int>(vn), Is.EqualTo(1000 + index));
            }

            Assert.That(store.VNForIntCon(1000), Is.EqualTo(64));
            Assert.That(store.VNForLongCon(1000), Is.EqualTo(192));
            Assert.That(store.VNForIntCon(0), Is.EqualTo(store.VNForIntCon(0)));
            Assert.That(store.IsVNIntegralConstant(store.VNForLongCon(-1), out uint value), Is.False);
            Assert.That(value, Is.Zero);
            var handle = store.VNForHandle(123, GTF_ICON_CLASS_HDL);
            Assert.That(store.IsVNTypeHandle(handle), Is.True);
            Assert.That(store.ConstantValue<nint>(handle), Is.EqualTo((nint)123));
            Assert.That(store.VNForHandle(123, GTF_ICON_CLASS_HDL), Is.EqualTo(handle));
            Assert.That(store.VNForHandle(123, GTF_ICON_OBJ_HDL), Is.Not.EqualTo(handle));
            Assert.That(sizeof(simd12_t), Is.EqualTo(12));
        });
    }

    [Test]
    public static void ValueNumberFloatingConstantsPreserveBitIdentity()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            Assert.That(store.VNForDoubleCon(0.0), Is.Not.EqualTo(store.VNForDoubleCon(-0.0)));
            Assert.That(store.VNForFloatCon(0.0f), Is.Not.EqualTo(store.VNForFloatCon(-0.0f)));
            var nan = BitConverter.Int64BitsToDouble(0x7ff8000000000001);
            var vn = store.VNForDoubleCon(nan);
            Assert.That(store.VNForDoubleCon(nan), Is.EqualTo(vn));
            Assert.That(store.VNForDoubleCon(BitConverter.Int64BitsToDouble(0x7ff8000000000002)), Is.Not.EqualTo(vn));
            Assert.That(BitConverter.DoubleToInt64Bits(store.ConstantValue<double>(vn)), Is.EqualTo(0x7ff8000000000001));
            Assert.That(BitConverter.DoubleToInt64Bits(store.ConstantValue<double>(store.VNForDoubleCon(-0.0))), Is.EqualTo(long.MinValue));
        });
    }

    [Test]
    public static void GlobalInsertionRegistersBothBoundsAndUnderlyingAdditionOperand()
    {
        WithCompiler(compiler => {
            var store = new ValueNumStore(compiler);
            compiler.vnStore = store;
            var variable = store.VNForExpr(null, TYP_INT);
            var bound = store.VNForExpr(null, TYP_INT);
            var constant = store.VNForIntCon(5);
            var sum = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_ADD, variable, constant);
            Assert.That(store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_ADD, variable, constant), Is.EqualTo(sum));
            var app = new VNFuncApp();
            Assert.That(store.GetVNFunc(sum, ref app), Is.True);
            for (var index = 0; index < 512; index++)
            {
                _ = store.VNForExpr(null, TYP_INT);
            }

            Assert.That(app.GetArg(0), Is.EqualTo(variable));
            Assert.That(app.GetArg(1), Is.EqualTo(constant));
            var reversed = store.VNForFuncNoFolding(TYP_INT, VNFunc.VNF_ADD, constant, variable);
            Assert.That(reversed, Is.Not.EqualTo(sum));
            var operand = ValueNumStore.NoVN;
            var addend = 0;
            Assert.That(store.IsVNBinFuncWithConst(reversed, VNFunc.VNF_ADD, ref operand, ref addend), Is.True);
            Assert.That(operand, Is.EqualTo(variable));
            Assert.That(addend, Is.EqualTo(5));
            Assert.That(store.IsVNBinFuncWithConst(reversed, VNFunc.VNF_SUB, ref operand, ref addend), Is.False);
            Assert.That(operand, Is.EqualTo(variable));
            Assert.That(addend, Is.EqualTo(5));
            Assert.That(store.GetVNFunc(constant, ref app), Is.False);
            Assert.That(app.GetArg(0), Is.EqualTo(variable));
            var bounds = AssertionDsc.CreateNoThrowArrBnd(compiler, sum, bound);
            var assertion = compiler.optAddAssertion(bounds);
            Assert.That(compiler.optAddAssertion(bounds), Is.EqualTo(assertion));
            Assert.That(compiler.optAssertionHasAssertionsForVN(variable, false), Is.True);
            Assert.That(compiler.optAssertionHasAssertionsForVN(sum, false), Is.True);
            Assert.That(compiler.optAssertionHasAssertionsForVN(bound, false), Is.True);
            var nonNull = compiler.optAddAssertion(AssertionDsc.CreateVNNonNullAssertion(compiler, store.VNForExpr(null, TYP_REF)));
            compiler.optCreateComplementaryAssertion(nonNull);
            Assert.That(compiler.optFindComplementary(nonNull), Is.Not.Zero);
        }, local: false);
    }

    [Test]
    public static void LocalInsertionDeduplicatesAtCapacityAndResetsCopyDependencies()
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var copy = AssertionDsc.CreateLclvarCopy(compiler, 0, 1, true);
            Assert.That(compiler.optAddAssertion(copy), Is.EqualTo(1));
            Assert.That(BitOps.IsMember(traits, compiler.GetAssertionDep(1), 0), Is.True);
            for (var index = 1; index < 64; index++)
            {
                Assert.That(compiler.optAddAssertion(IntAssertion(compiler, index)), Is.EqualTo(index + 1));
            }

            Assert.That(compiler.optAddAssertion(IntAssertion(compiler, 63)), Is.EqualTo(64));
            Assert.That(compiler.optAddAssertion(IntAssertion(compiler, 64)), Is.Zero);
            compiler.optAssertionReset();
            Assert.That(compiler.AssertionCount, Is.Zero);
            Assert.That(BitOps.IsEmpty(traits, compiler.GetAssertionDep(0)), Is.True);
            Assert.That(BitOps.IsEmpty(traits, compiler.GetAssertionDep(1)), Is.True);
        }, crossBlock: false);
    }

    [TestCase(0, false, 16)]
    [TestCase(1, false, 20)]
    [TestCase(3, false, 15)]
    [TestCase(1, true, 20)]
    public static void StoresInvalidateParentAndFieldDependenciesInBothOrders(int local, bool emptyPreorder, long remaining)
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            compiler.lvaCount = 4;
            compiler.lvaTable = [
                new LclVarDsc { Type = TYP_STRUCT, lvPromoted = true, lvFieldLclStart = 1, lvFieldCnt = 2 },
                new LclVarDsc { Type = TYP_INT, lvIsStructField = true, lvParentLcl = 0 },
                new LclVarDsc { Type = TYP_INT, lvIsStructField = true, lvParentLcl = 0 },
                new LclVarDsc { Type = TYP_INT },
            ];
            var active = InstallAssertions(compiler, [
                AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, O2K_ZEROOBJ, ValueNumStore.NoVN, true),
                AssertionDsc.CreateConstLclVarAssertion(compiler, 1, ValueNumStore.NoVN, (nint)3, ValueNumStore.NoVN, true),
                AssertionDsc.CreateConstLclVarAssertion(compiler, 2, ValueNumStore.NoVN, (nint)4, ValueNumStore.NoVN, true),
                AssertionDsc.CreateLclvarCopy(compiler, 1, 2, true),
                AssertionDsc.CreateConstLclVarAssertion(compiler, 3, ValueNumStore.NoVN, (nint)5, ValueNumStore.NoVN, true),
            ]);
            compiler.apLocal = emptyPreorder ? BitOps.MakeEmpty(traits) : BitOps.MakeCopy(traits, active);
            compiler.apLocalPostorder = BitOps.MakeCopy(traits, active);
            compiler.fgKillDependentAssertions(local, compiler.gtNewLclvNode(TYP_INT, 3));
            Assert.That((long)compiler.apLocal[0], Is.EqualTo(emptyPreorder ? 0 : remaining));
            Assert.That((long)compiler.apLocalPostorder[0], Is.EqualTo(remaining));
            Assert.That((long)active[0], Is.EqualTo(31));
        });
    }

    [Test]
    public static void LocalQueriesRespectLiveSetsOrderingAndAccessWidth()
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }, new LclVarDsc { Type = TYP_INT }];
            var active = InstallAssertions(compiler, [
                IntAssertion(compiler, 2).Reverse(),
                IntAssertion(compiler, 4),
                AssertionDsc.CreateSubrange(compiler, 1, new(Zero, One)),
                AssertionDsc.CreateSubrange(compiler, 0, new(Zero, One)),
            ]);
            var tree = compiler.gtNewLclvNode(TYP_INT, 0);
            Assert.That(compiler.optLocalAssertionIsEqualOrNotEqual(Compiler.optOp1Kind.O1K_LCLVAR, 0, O2K_CONST_INT, 2, active), Is.EqualTo(1));
            Assert.That(compiler.optLocalAssertionIsEqualOrNotEqual(Compiler.optOp1Kind.O1K_LCLVAR, 0, O2K_CONST_INT, 3, active), Is.EqualTo(2));
            Assert.That(compiler.optAssertionIsSubrange(tree, new(Zero, IntMax), active), Is.EqualTo(4));
            BitOps.RemoveElemD(traits, active, 1);
            BitOps.RemoveElemD(traits, active, 3);
            Assert.That(compiler.optLocalAssertionIsEqualOrNotEqual(Compiler.optOp1Kind.O1K_LCLVAR, 0, O2K_CONST_INT, 3, active), Is.Zero);
            Assert.That(compiler.optAssertionIsSubrange(tree, new(Zero, IntMax), active), Is.Zero);

            var field = new LclVarDsc { Type = TYP_SHORT, lvIsStructField = true };
            Assert.That(Compiler.optAssertionProp_LclVarTypeCheck(tree, compiler.lvaTable[0], field), Is.False);
            field.lvIsStructField = false;
            Assert.That(Compiler.optAssertionProp_LclVarTypeCheck(tree, compiler.lvaTable[0], field), Is.True);
        });
    }

    private static nint[] InstallAssertions(Compiler compiler, Compiler.AssertionDsc[] assertions)
    {
        var table = typeof(Compiler).GetField("optAssertionTabPrivate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        var count = typeof(Compiler).GetField("optAssertionCount", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException();
        table.SetValue(compiler, assertions);
        count.SetValue(compiler, (ushort)assertions.Length);
        var traits = compiler.apTraits ?? throw new InvalidOperationException();
        var active = BitOps.MakeEmpty(traits);
        for (var index = 0; index < assertions.Length; index++)
        {
            BitOps.AddElemD(traits, active, index);
            BitOps.AddElemD(traits, compiler.GetAssertionDep(assertions[index].Op1.LclNum), index);
            if (assertions[index].Op2.KindIs(O2K_LCLVAR_COPY))
            {
                BitOps.AddElemD(traits, compiler.GetAssertionDep(assertions[index].Op2.LclNum), index);
            }
        }

        return active;
    }

    [Test]
    public static void DependenciesExpandWithoutDroppingExistingBits()
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            var firstDep = compiler.GetAssertionDep(0);
            BitOps.AddElemD(traits, firstDep, 63);

            compiler.lvaCount = 201;
            Assert.That(BitOps.IsEmpty(traits, compiler.GetAssertionDep(200)), Is.True);
            Assert.That(compiler.GetAssertionDep(0), Is.SameAs(firstDep));
            Assert.That(BitOps.IsMember(traits, compiler.GetAssertionDep(0), 63), Is.True);
        }, crossBlock: false);
    }

    [TestCase(GTF_EMPTY)]
    [TestCase(GTF_ICON_CLASS_HDL)]
    [TestCase(GTF_ICON_HDL_MASK)]
    public static void IntegerFlagsRoundTripIncludingHighBit(GenTreeFlags flags)
    {
        WithCompiler(compiler => {
            var assertion = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN,
                (nint)42, ValueNumStore.NoVN, true, flags);
            Assert.That(assertion.Op2.IconFlag, Is.EqualTo(flags));
            Assert.That(assertion.Op2.HasIconFlag, Is.EqualTo(flags != GTF_EMPTY));
            Assert.That(assertion.Equals(IntAssertion(compiler, 42), false), Is.EqualTo(flags == GTF_EMPTY));
        });
    }

    [Test]
    public static void NullHintDistinguishesReferenceNullFromIntegralZero()
    {
        WithCompiler(compiler => {
            var nonNull = AssertionDsc.CreateLclNonNullAssertion(compiler, 0);
            Assert.That(nonNull.CanPropNonNull, Is.True);
            Assert.That(nonNull.Reverse().CanPropNonNull, Is.False);
            Assert.That(IntAssertion(compiler, 0).Reverse().CanPropNonNull, Is.False);
        });
    }

    [TestCase(0L, long.MinValue, false)]
    [TestCase(0L, 0L, true)]
    [TestCase(0x7ff8000000000001L, 0x7ff8000000000001L, true)]
    [TestCase(0x7ff8000000000001L, 0x7ff8000000000002L, false)]
    public static void DoubleEqualityIsBitExact(long left, long right, bool equal)
    {
        WithCompiler(compiler => {
            var first = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN,
                BitConverter.Int64BitsToDouble(left), ValueNumStore.NoVN, true);
            var second = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN,
                BitConverter.Int64BitsToDouble(right), ValueNumStore.NoVN, true);
            Assert.That(first.Equals(second, false), Is.EqualTo(equal));
        });
    }

    [TestCase(TYP_SIMD8)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    [TestCase(TYP_SIMD32)]
    [TestCase(TYP_SIMD64)]
    public static void VectorPayloadIsOwnedAndComparesOnlyActiveBytes(var_types type)
    {
        WithCompiler(compiler => {
            var firstNode = new GenTreeVecCon(type);
            var secondNode = new GenTreeVecCon(type);
            var size = type.Size;
            firstNode.SimdVal.AsSpan<byte>()[size - 1] = 0x80;
            secondNode.SimdVal.AsSpan<byte>()[size - 1] = 0x80;
            secondNode.SimdVal.AsSpan<byte>()[size..].Fill(0xFF);
            var first = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, firstNode, ValueNumStore.NoVN, true);
            var second = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, secondNode, ValueNumStore.NoVN, true);
            Assert.That(first.Equals(second, false), Is.True);
            firstNode.SimdVal.AsSpan<byte>()[size - 1] = 0;
            Assert.That(first.Op2.SimdConstant[size - 1], Is.EqualTo(0x80));
            var changed = AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, firstNode, ValueNumStore.NoVN, true);
            Assert.That(first.Equals(changed, false), Is.False);
            Assert.That(first.Reverse().Op2.SimdSize, Is.EqualTo(size));
        });
    }

    [Test]
    public static void GlobalDescriptorsRetainValueNumbersAndBoundFacts()
    {
        WithCompiler(compiler => {
            var nonNull = AssertionDsc.CreateVNNonNullAssertion(compiler, 0x100);
            Assert.That(nonNull.Op1.VN, Is.EqualTo(0x100));
            Assert.That(compiler.optAssertionHasAssertionsForVN(0x100, true), Is.False);
            Assert.That(compiler.optAssertionHasAssertionsForVN(0x100, false), Is.True);
            Assert.That(compiler.optAssertionHasAssertionsForVN(ValueNumStore.NoVN, false), Is.False);
            Assert.That(nonNull.Equals(AssertionDsc.CreateVNNonNullAssertion(compiler, 0x100), true), Is.True);
            var bounds = AssertionDsc.CreateNoThrowArrBnd(compiler, 0x200, 0x300);
            Assert.That(bounds.IsBoundsCheckNoThrow, Is.True);
            Assert.That(bounds.Reverse().IsBoundsCheckNoThrow, Is.False);
        }, local: false);
    }

#if DEBUG
    [Test]
    public static void AssertionDumpsMatchNativeFormattingAndIndexOrder()
    {
        WithCompiler(compiler => {
            var traits = compiler.apTraits ?? throw new InvalidOperationException();
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previous = s_jitstdout;
            try
            {
                s_jitstdout = writer;
                compiler.optPrintAssertion(AssertionDsc.CreateLclNonNullAssertion(compiler, 0), 1);
                compiler.optPrintAssertion(AssertionDsc.CreateLclvarCopy(compiler, 0, 1, true));
                compiler.optPrintAssertion(AssertionDsc.CreateSubrange(compiler, 0, new(Zero, One)));
                compiler.optPrintAssertion(AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, -0.0, ValueNumStore.NoVN, true));
                compiler.optPrintAssertion(AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, 1.0, ValueNumStore.NoVN, true));
                compiler.optPrintAssertion(AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, O2K_ZEROOBJ, ValueNumStore.NoVN, true));
                compiler.optPrintAssertionIndices(BitOps.MakeEmpty(traits));
                jitprintf("\n");
                var indices = BitOps.MakeEmpty(traits);
                BitOps.AddElemD(traits, indices, 63);
                BitOps.AddElemD(traits, indices, 0);
                compiler.optPrintAssertionIndices(indices);
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previous;
            }

            var expected =
                "#01 lclvar V00 != null\nlclvar V00 == lclvar V01\nlclvar V00 in [0..1]\n"
                + "lclvar V00 == -0.0\nlclvar V00 == 1.00000\nlclvar V00 == ZeroObj\n#NA\n#01 #64";
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(expected.Replace("\n", Environment.NewLine, StringComparison.Ordinal)));
        });
    }
#endif

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compMaxUncheckedOffsetForNullObject")]
    private static extern ref int MaxUncheckedOffset(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optAssertionPropagated")]
    private static extern ref bool AssertionPropagated(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "optAssertionPropagatedCurrentStmt")]
    private static extern ref bool AssertionPropagatedCurrentStmt(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "bbJtrueAssertionOut")]
    private static extern ref nint[][]? JtrueAssertionOut(Compiler compiler);

    private static AssertionDsc IntAssertion(Compiler compiler, nint value)
        => AssertionDsc.CreateConstLclVarAssertion(compiler, 0, ValueNumStore.NoVN, value, ValueNumStore.NoVN, true);

    private static AssertionInfo GenerateBounds(Compiler compiler, ValueNumStore store, VNFunc func, int left, int right)
    {
        var relop = compiler.gtNewBinaryNode(GT_LT, TYP_INT, compiler.gtNewIconNode(TYP_INT, 0), compiler.gtNewIconNode(TYP_INT, 0));
        relop._vnPair.SetBoth(store.VNForFuncNoFolding(TYP_INT, func, left, right));
        return compiler.optCreateJTrueBoundsAssertion(compiler.gtNewUnaryNode(GT_JTRUE, TYP_VOID, relop));
    }

    private static void SetField(object target, string name, object value)
    {
        var field = typeof(JitConfigValues).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(name);
        field.SetValue(target, value);
    }

    private static void WithCompiler(Action<Compiler> action, bool local = true, int tracked = 0, bool crossBlock = true)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previousCompiler = JitTls.Compiler;
        var previousConfig = JitConfig;
        object config = default(JitConfigValues);
        SetField(config, "_jitMaxLocalsToTrack", 1024);
        SetField(config, "_jitEnableCrossBlockLocalAssertionProp", crossBlock ? 1 : 0);
        JitConfig = (JitConfigValues)config;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaCount = Math.Max(2, tracked);
        compiler.lvaTrackedCount = tracked;
#if DEBUG
        compiler.info.compFullName = "AssertionTests";
#endif
        JitTls.Compiler = compiler;
        try
        {
            compiler.optAssertionInit(local);
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
            JitConfig = previousConfig;
        }
    }
}
