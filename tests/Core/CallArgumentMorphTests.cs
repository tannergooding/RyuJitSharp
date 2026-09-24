// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.GenTreeCallFlags;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class CallArgumentMorphTests
{
    [TestCase(0)]
    [TestCase(2)]
    public static void EffectiveUsesUpdateTheirOwningSlotWithoutDiscardingCommas(int depth)
    {
        WithCompiler(compiler => {
            GenTree root = compiler.gtNewIconNode(TYP_INT, 1);
            for (var index = 0; index < depth; index++)
            {
                root = compiler.gtNewCommaNode(TYP_INT, compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null), root);
            }
            var originalRoot = root;
            var replacement = compiler.gtNewLclvNode(TYP_INT, 1);
            ref var use = ref GenTree.EffectiveUse(ref root);
            use = replacement;
            Assert.That(root.EffectiveVal, Is.SameAs(replacement));
            Assert.That(root, Is.SameAs(depth == 0 ? replacement : originalRoot));
            if (depth != 0)
            {
                Assert.That(root.Flags & GTF_CALL, Is.EqualTo(GTF_CALL));
            }
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void PostMorphImplicitByrefsPreserveAddressAndOffsetOutputs(bool load, bool implicitByref)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = TYP_BYREF;
            compiler.lvaTable[0].lvIsParam = true;
            compiler.lvaTable[0].IsImplicitByRef = implicitByref;
            var local = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var address = compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF,
                compiler.gtNewBinaryNode(GT_ADD, TYP_BYREF, local, compiler.gtNewIconNode(TYP_I_IMPL, 8)),
                compiler.gtNewIconNode(TYP_I_IMPL, 12));
            GenTree node = load ? compiler.gtNewIndir(TYP_INT, address) : local;
            GenTree? resultAddress = local;
            long offset = 99;
            var result = node.IsImplicitByrefParameterValuePostMorph(compiler, ref resultAddress, ref offset);
            Assert.That(result, Is.SameAs(load && implicitByref ? local : null));
            Assert.That(resultAddress, Is.SameAs(load ? address : local));
            Assert.That(offset, Is.EqualTo(load ? 20 : 99));
        });
    }

    [Test]
    public static void PrimitiveIndirectionReplacementPreservesLogicalIdentityAndLoadFlags()
    {
        WithCompiler(compiler => {
            var address = compiler.gtNewLclvNode(TYP_BYREF, 0);
            var original = compiler.gtNewBlkIndir(address, new ClassLayout(8));
            original.Flags |= GTF_IND_VOLATILE | GTF_IND_UNALIGNED;
            original._vnPair.SetBoth(42);
#if DEBUG
            var nextId = compiler.compGenTreeID;
#endif
            var replacement = new GenTreeIndir(GT_IND, TYP_LONG, address, null, original, NodeThreading.None) {
                Flags = original.Flags,
            };
            Assert.That(replacement.Oper, Is.EqualTo(GT_IND));
            Assert.That(replacement.Addr, Is.SameAs(address));
            Assert.That(replacement.Flags, Is.EqualTo(original.Flags));
            Assert.That(replacement._vnPair.Liberal, Is.EqualTo(ValueNumStore.NoVN));
            Assert.That(original.Oper, Is.EqualTo(GT_BLK));
            Assert.That(original._vnPair.Liberal, Is.EqualTo(42));
#if DEBUG
            Assert.That(replacement.TreeId, Is.EqualTo(original.TreeId));
            Assert.That(compiler.compGenTreeID, Is.EqualTo(nextId));
#endif
        });
    }

    [Test]
    public static void HelperEquivalenceUsesTheInlineRootWithoutChangingFailedLookupOutputs()
    {
        WithCompiler(compiler => {
            var helper = Compiler.eeFindHelper(CorInfoHelpFunc.CORINFO_HELP_ARRADDR_ST);
            var managed = (CORINFO_METHOD_STRUCT_*)0x1234;
            var result = managed;
            Assert.That(compiler.HelperToManagedMapLookup(helper, ref result), Is.False);
            Assert.That((nuint)result, Is.EqualTo((nuint)managed));

            HelperMap(compiler) = new() { [helper] = managed };
            var inlinee = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
            inlinee.impInlineInfo = new InlineInfo { InlineRoot = compiler };

            var call = compiler.gtNewCallNode(TYP_VOID, CT_HELPER, helper);
            Assert.That(call.IsHelperCallOrUserEquivalent(inlinee, CorInfoHelpFunc.CORINFO_HELP_ARRADDR_ST), Is.True);
            call._callType = CT_USER_FUNC;
            call._callMethHnd = managed;
            Assert.That(call.IsHelperCallOrUserEquivalent(inlinee, CorInfoHelpFunc.CORINFO_HELP_ARRADDR_ST), Is.True);
            Assert.That(call.IsHelperCallOrUserEquivalent(inlinee, CorInfoHelpFunc.CORINFO_HELP_THROW), Is.False);
            call._callType = CT_INDIRECT;
            Assert.That(call.IsHelperCallOrUserEquivalent(inlinee, CorInfoHelpFunc.CORINFO_HELP_ARRADDR_ST), Is.False);

            Assert.That(compiler.HelperToManagedMapLookup(Compiler.eeFindHelper(CorInfoHelpFunc.CORINFO_HELP_THROW), ref result), Is.False);
            Assert.That((nuint)result, Is.EqualTo((nuint)managed));
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_helperToManagedMap")]
    private static extern ref Dictionary<Pointer<CORINFO_METHOD_STRUCT_>, Pointer<CORINFO_METHOD_STRUCT_>>? HelperMap(Compiler compiler);

    [TestCase(0, 32)]
    [TestCase(4, 32)]
    [TestCase(5, 40)]
    [TestCase(6, 48)]
    public static void OutgoingArgumentsPreserveWindowsSlotsAndShadowSpace(int count, int expectedStackSize)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            for (var i = 0; i < count; i++)
            {
                var value = (i & 1) == 0
                    ? (GenTree)compiler.gtNewIconNode(TYP_INT, i)
                    : compiler.gtNewDconNode(TYP_DOUBLE, i);
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            }

            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            Assert.That(call.Args.IsAbiInformationDetermined, Is.True);
            Assert.That(call.Args.AreArgsComplete, Is.False);
            Assert.That(call.Args.HasRegArgs, Is.EqualTo(count > 0));
            Assert.That(call.Args.HasStackArgs, Is.EqualTo(count > 4));
            Assert.That(call.Args.OutgoingArgsStackSize, Is.EqualTo(expectedStackSize));
            Assert.That(call.HasNonStandardAddedArgs(compiler), Is.False);
            Assert.That(call.GetNonStandardAddedArgCount(compiler), Is.Zero);
            Assert.That(compiler.compFloatingPointUsed, Is.EqualTo(count > 1));

            regNumber[] registers = [REG_RCX, REG_XMM1, REG_R8, REG_XMM3];
            var position = 0;
            foreach (var argument in call.Args.Args)
            {
                Assert.That(argument.AbiInfo.NumSegments, Is.EqualTo(1));
                ref readonly var segment = ref argument.AbiInfo.Segments[0];
                if (position < registers.Length)
                {
                    Assert.That(segment.Register, Is.EqualTo(registers[position]));
                }
                else
                {
                    Assert.That(segment.IsPassedOnStack, Is.True);
                    Assert.That(segment.StackOffset, Is.EqualTo(position * 8));
                }
                position++;
            }
        });
    }

    [Test]
    public static void SignatureTypesAndPseudoArgumentsDriveClassification()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var small = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 1), TYP_UBYTE));
            var pseudo = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 2))
                .WithWellKnownArg(WellKnownArg.AsyncAwaiter));
            var custom = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_I_IMPL, 3))
                .WithWellKnownArg(WellKnownArg.DispatchIndirectCallTarget));
            var last = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewDconNode(TYP_DOUBLE, 4)));

            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            Assert.That(small.AbiInfo.Segments[0].Size, Is.EqualTo(1));
            Assert.That(small.AbiInfo.Segments[0].Register, Is.EqualTo(REG_RCX));
            Assert.That(pseudo.AbiInfo.NumSegments, Is.Zero);
            Assert.That(pseudo.AbiInfo.Segments.Length, Is.Zero);
            Assert.That(CallArgs.IsNonStandard(compiler, call, pseudo), Is.False);
            Assert.That(custom.AbiInfo.Segments[0].Register, Is.EqualTo(REG_RAX));
            Assert.That(CallArgs.IsNonStandard(compiler, call, custom), Is.True);
            Assert.That(last.AbiInfo.Segments[0].Register, Is.EqualTo(REG_XMM1));
            Assert.That(call.Args.OutgoingArgsStackSize, Is.EqualTo(32));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void VirtualStubCellsCanBeResetAndReclassifiedWithoutDuplicatingArguments(bool indirect)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, indirect ? CT_INDIRECT : CT_USER_FUNC, null);
            call.Flags |= GTF_CALL_VIRT_STUB;
            Assert.That(call.HasNonStandardAddedArgs(compiler), Is.True);
            Assert.That(call.GetNonStandardAddedArgCount(compiler), Is.EqualTo(1));
            if (indirect)
            {
                call.ControlExpr = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            }
            else
            {
                call._callMoreFlags |= GTF_CALL_M_VIRTSTUB_REL_INDIRECT;
                call.StubCallStubAddr = (void*)0x1234;
            }

            var thisArg = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_REF, 0))
                .WithWellKnownArg(WellKnownArg.ThisPointer));
            var value = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 10)));

            for (var pass = 0; pass < 2; pass++)
            {
                call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
                call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
                Assert.That(call.Args.CountArgs(), Is.EqualTo(3));
                Assert.That(call.Args.ThisArg, Is.SameAs(thisArg));
                var stub = call.Args.FindWellKnownArg(WellKnownArg.VirtualStubCell)
                    ?? throw new InvalidOperationException("Missing virtual stub argument.");
                Assert.That(thisArg.Next, Is.SameAs(stub));
                Assert.That(stub.Next, Is.SameAs(value));
                Assert.That(stub.AbiInfo.Segments[0].Register, Is.EqualTo(REG_R11));
                Assert.That(stub.Node.AsIntCon().IconValue, Is.EqualTo((nint)0x1234));
                Assert.That(value.AbiInfo.Segments[0].Register, Is.EqualTo(REG_RDX));
                if (indirect)
                {
                    Assert.That(stub.Node, Is.Not.SameAs(call.ControlExpr));
                }

                call.Args.ResetFinalArgsAndAbiInfo();
                Assert.That(call.Args.CountArgs(), Is.EqualTo(2));
                Assert.That(call.Args.IsAbiInformationDetermined, Is.False);
                Assert.That(thisArg.Next, Is.SameAs(value));
            }
        });
    }

    [TestCase(false, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    public static void ReadyToRunCellsAreAddedOnlyForNonDelegateFastTailCalls(
        bool tailCall, bool delegateInvoke, bool expectedCell)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            call._entryPoint.accessType = InfoAccessType.IAT_PVALUE;
            call._entryPoint.addr = (void*)0x4567;
            if (tailCall)
            {
                call._callMoreFlags |= GTF_CALL_M_TAILCALL;
            }
            if (delegateInvoke)
            {
                call._callMoreFlags |= GTF_CALL_M_DELEGATE_INV;
            }

            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            var cell = call.Args.FindWellKnownArg(WellKnownArg.R2RIndirectionCell);
            Assert.That(cell is not null, Is.EqualTo(expectedCell));
            if (cell is not null)
            {
                Assert.That(cell.Node.AsIntCon().IconValue, Is.EqualTo((nint)0x4567));
                Assert.That(cell.AbiInfo.Segments[0].Register, Is.EqualTo(REG_R2R_INDIRECT_PARAM));
                call.Args.ResetFinalArgsAndAbiInfo();
                Assert.That(call.Args.IsEmpty, Is.True);
            }
        });
    }

    [Test]
    public static void ReclassificationDoesNotChangeTreesAndCloningPreservesStackSize()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var address = compiler.gtNewLclAddrNode(TYP_BYREF, 0, 0);
            var first = call.Args.PushBack(NewCallArg.CreateForPrimitive(address));
            for (var i = 0; i < 4; i++)
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, i)));
            }

            call.Args.DetermineAbiInfo(compiler, call);
            Assert.That(address.Type, Is.EqualTo(TYP_BYREF));
            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            Assert.That(address.Type, Is.EqualTo(TYP_I_IMPL));
            Assert.That(first.Node, Is.SameAs(address));
            Assert.That(call.Args.OutgoingArgsStackSize, Is.EqualTo(40));

            var clone = compiler.gtCloneExpr(call).AsCall();
            Assert.That(clone.Args.OutgoingArgsStackSize, Is.EqualTo(40));
            Assert.That(clone.Args.IsAbiInformationDetermined, Is.True);
            Assert.That(clone.Args.GetArgByIndex(0), Is.Not.SameAs(first));
        });
    }

    [TestCase(CT_USER_FUNC, 0, false, true)]
    [TestCase(CT_INDIRECT, 0, false, true)]
    [TestCase(CT_HELPER, 0, false, false)]
    [TestCase(CT_USER_FUNC, GTF_CALL_M_NOGCCHECK, false, false)]
    [TestCase(CT_INDIRECT, GTF_CALL_M_NOGCCHECK, false, true)]
    [TestCase(CT_USER_FUNC, GTF_CALL_M_TAILCALL, false, false)]
    [TestCase(CT_USER_FUNC, GTF_CALL_M_SPECIAL_INTRINSIC, false, false)]
    [TestCase(CT_INDIRECT, GTF_CALL_M_SUPPRESS_GC_TRANSITION, true, false)]
    [TestCase(CT_USER_FUNC, GTF_CALL_M_SUPPRESS_GC_TRANSITION, true, false)]
    public static void SafepointsRespectTailCallsIntrinsicsAndSuppressedTransitions(
        gtCallTypes callType, GenTreeCallFlags flags, bool unmanaged, bool expected)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            call._callType = callType;
            call._callMoreFlags |= flags;
            if (unmanaged)
            {
                call.Flags |= GTF_CALL_UNMANAGED;
            }

            Assert.That(Compiler.IsGcSafePoint(call), Is.EqualTo(expected));
        });
    }

    [TestCase(TYP_INT, false)]
    [TestCase(TYP_LONG, true)]
    public static void PartialDefinitionsRetainUseFlags(var_types localType, bool partial)
    {
        WithCompiler(compiler => {
            compiler.lvaTable[0].Type = localType;
            var store = new GenTreeLclFld(TYP_INT, 0, 0, compiler.gtNewIconNode(TYP_INT, 42), null);
            compiler.fgAssignSetVarDef(store);
            Assert.That(store.Flags & GTF_VAR_DEF, Is.EqualTo(GTF_VAR_DEF));
            Assert.That((store.Flags & GTF_VAR_USEASG) != 0, Is.EqualTo(partial));
        });
    }

    [Test]
    public static void StoresSpillOnlyInterferingEarlierValues()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var constant = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 5)));
            var read = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 0)));
            var unrelated = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 1)));
            var store = compiler.gtNewStoreLclVarNode(0, compiler.gtNewIconNode(TYP_INT, 10));
            var value = compiler.gtNewBinaryNode(GT_COMMA, TYP_INT, store, compiler.gtNewLclvNode(TYP_INT, 0));
            var assignment = call.Args.PushBack(NewCallArg.CreateForPrimitive(value));
            var last = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 0)));

            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            call.Args.ArgsComplete(compiler, call);
            Assert.That(call.Args.AreArgsComplete, Is.True);
            Assert.That(call.Args.NeedsTemps, Is.True);
            Assert.That(constant.NeedTmp, Is.False);
            Assert.That(read.NeedTmp, Is.True);
            Assert.That(unrelated.NeedTmp, Is.False);
            Assert.That(assignment.NeedTmp, Is.True);
            Assert.That(last.NeedTmp, Is.False);
        });
    }

    [Test]
    public static void NestedCallsPreserveEarlierEffectsAndDeferStackPlacement()
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var local = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 0)));
            var read = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                compiler.gtNewIndir(TYP_INT, compiler.gtNewLclvNode(TYP_BYREF, 1))));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 2)));
            _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 3)));
            var stack = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 4)));
            var nested = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewCallNode(TYP_INT, CT_USER_FUNC, null)));

            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            call.Args.ArgsComplete(compiler, call);
            Assert.That(local.NeedTmp, Is.False);
            Assert.That(read.NeedTmp, Is.True);
            Assert.That(stack.NeedTmp, Is.False);
            Assert.That(stack.NeedPlace, Is.True);
            Assert.That(nested.NeedTmp, Is.True);
        });
    }

    [TestCase(TYP_INT, false)]
    [TestCase(TYP_FLOAT, true)]
    public static void SingleFloatingCallArgumentsStillRequireTemps(var_types type, bool needsTemp)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewCallNode(type, CT_USER_FUNC, null)));
            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            call.Args.ArgsComplete(compiler, call);
            Assert.That(argument.NeedTmp, Is.EqualTo(needsTemp));
            Assert.That(call.Args.NeedsTemps, Is.EqualTo(needsTemp));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DifferentExceptionsPreserveAllEarlierThrowingArguments(bool different)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var first = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                compiler.gtNewIndir(TYP_INT, compiler.gtNewLclvNode(TYP_BYREF, 0))));
            var second = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                compiler.gtNewIndir(TYP_INT, compiler.gtNewLclvNode(TYP_BYREF, 1))));
            var lastNode = different
                ? (GenTree)NewOverflowingAdd(compiler)
                : compiler.gtNewIndir(TYP_INT, compiler.gtNewLclvNode(TYP_BYREF, 2));
            var last = call.Args.PushBack(NewCallArg.CreateForPrimitive(lastNode));

            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            call.Args.ArgsComplete(compiler, call);
            Assert.That(first.NeedTmp, Is.EqualTo(different));
            Assert.That(second.NeedTmp, Is.EqualTo(different));
            Assert.That(last.NeedTmp, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DebugInlineThrowsRespectOutgoingStackArguments(bool debugCode)
    {
        WithCompiler(compiler => {
            compiler.opts.compDbgCode = debugCode;
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var throwing = call.Args.PushBack(NewCallArg.CreateForPrimitive(NewOverflowingAdd(compiler)));
            for (var i = 0; i < 4; i++)
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, i)));
            }

            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            call.Args.ArgsComplete(compiler, call);
            Assert.That(throwing.NeedTmp, Is.EqualTo(debugCode));
        });
    }

    [TestCase(1, false)]
    [TestCase(5, true)]
    public static void LocallocArgumentsPrecedeStackPlacement(int count, bool needsTemp)
    {
        WithCompiler(compiler => {
            compiler.compLocallocUsed = true;
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var allocation = compiler.gtNewUnaryNode(GT_LCLHEAP, TYP_I_IMPL, compiler.gtNewIconNode(TYP_I_IMPL, 32));
            var argument = call.Args.PushBack(NewCallArg.CreateForPrimitive(allocation));
            for (var i = 1; i < count; i++)
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, i)));
            }

            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            call.Args.ArgsComplete(compiler, call);
            Assert.That(argument.NeedTmp, Is.EqualTo(needsTemp));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void ControlFlowGuardDefersTargetNullChecksUntilAfterArgumentEffects(bool enabled)
    {
        WithCompiler(compiler => {
            if (enabled)
            {
                compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_ENABLE_CFG);
            }

            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            call._callMoreFlags |= GTF_CALL_M_DELEGATE_INV;
            var receiver = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_REF, 0))
                .WithWellKnownArg(WellKnownArg.ThisPointer));
            var effect = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                compiler.gtNewIndir(TYP_INT, compiler.gtNewLclvNode(TYP_BYREF, 1))));
            var constant = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 2)));
            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            call.Args.ArgsComplete(compiler, call);
            Assert.That(receiver.NeedTmp, Is.EqualTo(enabled));
            Assert.That(effect.NeedTmp, Is.EqualTo(enabled));
            Assert.That(constant.NeedTmp, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SortingPreservesNativePartitionsAndMinOptsSelection(bool minOpts)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var firstConstant = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 1)));
            var firstLocal = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 0)));
            var multiply = compiler.gtNewBinaryNode(GT_MUL, TYP_INT,
                compiler.gtNewBinaryNode(GT_MUL, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewLclvNode(TYP_INT, 1)),
                compiler.gtNewBinaryNode(GT_MUL, TYP_INT,
                    compiler.gtNewLclvNode(TYP_INT, 1), compiler.gtNewLclvNode(TYP_INT, 2)));
            var expensive = call.Args.PushBack(NewCallArg.CreateForPrimitive(multiply));
            var nested = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewCallNode(TYP_INT, CT_USER_FUNC, null)));
            var temporary = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                compiler.gtNewUnaryNode(GT_NEG, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 1))));
            var lastConstant = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, 2)));
            var lastLocal = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_INT, 1)));
            var cheap = call.Args.PushBack(NewCallArg.CreateForPrimitive(
                compiler.gtNewUnaryNode(GT_NEG, TYP_INT, compiler.gtNewLclvNode(TYP_INT, 2))));
            CallArg[] original = [.. call.Args.Args];
            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            call.Args.SetNeedsTemp(temporary);
            call.Args.ArgsComplete(compiler, call);

            var sorted = new CallArg[original.Length];
            call.Args.SortArgs(compiler, call, sorted);
            CallArg[] expected = [
                nested, temporary, minOpts ? cheap : expensive, minOpts ? expensive : cheap,
                lastLocal, firstLocal, firstConstant, lastConstant,
            ];
            Assert.That(sorted, Is.EqualTo(expected));
            Assert.That(sorted, Is.All.Matches<CallArg>(argument => argument.Processed));
            Assert.That((CallArg[])[.. call.Args.Args], Is.EqualTo(original));
            Assert.That(call.Args.LateHead, Is.Null);
        }, minOpts);
    }

    [TestCase(1)]
    [TestCase(4)]
    public static void AllConstantSortingHandlesTheExhaustedEndPartition(int count)
    {
        WithCompiler(compiler => {
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            for (var i = 0; i < count; i++)
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, i)));
            }

            CallArg[] original = [.. call.Args.Args];
            call.Args.AddFinalArgsAndDetermineAbiInfo(compiler, call);
            call.Args.ArgsComplete(compiler, call);
            var sorted = new CallArg[count];
            call.Args.SortArgs(compiler, call, sorted);
            Assert.That(sorted, Is.EqualTo(original));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void SharedTemporaryScopesRestoreOuterLifetimes(bool exceptional)
    {
        WithCompiler(compiler => {
            var availableField = typeof(Compiler).GetField("fgAvailableOutgoingArgTemps", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing outgoing temporary pool.");
            var usedField = typeof(Compiler).GetField("fgUsedSharedTemps", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing outgoing temporary stack.");
            var available = new hashBv();
            var previous = new Stack<int>();
            availableField.SetValue(compiler, available);
            usedField.SetValue(compiler, previous);

            using (var outer = new Compiler.SharedTempsScope(compiler))
            {
                var outerTemps = usedField.GetValue(compiler) as Stack<int>
                    ?? throw new InvalidOperationException("Missing outer scope.");
                outerTemps.Push(1);
                if (exceptional)
                {
                    _ = Assert.Throws<NotSupportedException>(() => {
                        using var inner = new Compiler.SharedTempsScope(compiler);
                        var innerTemps = usedField.GetValue(compiler) as Stack<int>
                            ?? throw new InvalidOperationException("Missing inner scope.");
                        innerTemps.Push(2);
                        throw new NotSupportedException();
                    });
                }
                else
                {
                    using var inner = new Compiler.SharedTempsScope(compiler);
                    var innerTemps = usedField.GetValue(compiler) as Stack<int>
                        ?? throw new InvalidOperationException("Missing inner scope.");
                    innerTemps.Push(2);
                }

                Assert.That(usedField.GetValue(compiler), Is.SameAs(outerTemps));
                Assert.That(available.testBit(2), Is.True);
                Assert.That(available.testBit(1), Is.False);
            }

            Assert.That(usedField.GetValue(compiler), Is.SameAs(previous));
            Assert.That(available.testBit(1), Is.True);
            Assert.That(previous, Is.Empty);
        });
    }

    [TestCase(0, 16, true)]
    [TestCase(0, 8, false)]
    [TestCase(4, 16, false)]
    [TestCase(0, 9, true)]
    public static void PromotedFieldsMatchEveryRegisterAndRoundedStackSlot(int registerOffset, int stackSize, bool matches)
    {
        WithCompiler(compiler => {
            compiler.lvaTable = new LclVarDsc[4];
            compiler.lvaCount = 4;
            ref var parent = ref compiler.lvaTable[0];
            parent.Type = TYP_STRUCT;
            parent.Layout = new ClassLayout(24);
            parent.lvPromoted = true;
            parent.lvFieldLclStart = 1;
            parent.lvFieldCnt = 3;
            for (var i = 1; i < 4; i++)
            {
                ref var field = ref compiler.lvaTable[i];
                field.Type = TYP_LONG;
                field.lvIsStructField = true;
                field.lvParentLcl = 0;
                field.lvFldOffset = (byte)((i - 1) * 8);
            }

            var abi = new AbiPassingInformation(2);
            abi.Segments[0] = AbiPassingSegment.InRegister(REG_RCX, registerOffset, 8);
            abi.Segments[1] = AbiPassingSegment.OnStack(32, 8, stackSize);
            Assert.That(abi.HasAnyStackSegment, Is.True);
            Assert.That(abi.IsSplitAcrossRegistersAndStack, Is.True);
            Assert.That(abi.HasExactlyOneRegisterSegment, Is.False);
            Assert.That(abi.CountRegsAndStackSlots(), Is.EqualTo(1 + ((stackSize + 7) / 8)));
            Assert.That(compiler.FieldsMatchAbi(parent, abi), Is.EqualTo(matches));

            var original = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            GenTreeFieldList.Use[] fields = [.. compiler.fgMorphLclToFieldList(original).Uses];
            Assert.That(fields.Length, Is.EqualTo(3));
            for (var i = 0; i < fields.Length; i++)
            {
                Assert.That(fields[i].Node.AsLclVar().LclNum, Is.EqualTo(i + 1));
                Assert.That(fields[i].Offset, Is.EqualTo(i * 8));
                Assert.That(fields[i].Type, Is.EqualTo(TYP_LONG));
            }
            Assert.That(original.Oper, Is.EqualTo(GT_LCL_VAR));
        });
    }

    [TestCase(32, 4, true)]
    [TestCase(32, 5, false)]
    [TestCase(39, 5, true)]
    [TestCase(40, 5, true)]
    public static void FastTailCallsRespectIncomingArgumentSpace(int incomingSize, int count, bool allowed)
    {
        WithCompiler(compiler => {
            compiler.opts.compFastTailCalls = true;
            compiler.lvaParameterStackSize = incomingSize;
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            for (var i = 0; i < count; i++)
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewIconNode(TYP_INT, i)));
            }

            Assert.That(compiler.fgCanFastTailCall(call, out var reason), Is.EqualTo(allowed));
            Assert.That(reason, Is.EqualTo(allowed ? null : "Not enough incoming arg space"));
            Assert.That(call.Args.IsAbiInformationDetermined, Is.True);
            Assert.That(call.Args.AreArgsComplete, Is.False);
            Assert.That(call.Args.OutgoingArgsStackSize, Is.EqualTo(count > 4 ? 40 : 32));
        });
    }

    [TestCase(false, true, true, true, "Configuration doesn't allow fast tail calls")]
    [TestCase(true, true, true, true, "Localloc used")]
    [TestCase(true, false, true, true, "Uses NextCallReturnAddress intrinsic")]
    [TestCase(true, false, false, true, "Callee has RetBuf but caller does not.")]
    [TestCase(true, false, false, false, null)]
    public static void FastTailCallRejectionsPreserveNativePrecedence(
        bool enabled, bool localloc, bool nextReturnAddress, bool returnBuffer, string? expectedReason)
    {
        WithCompiler(compiler => {
            compiler.opts.compFastTailCalls = enabled;
            compiler.compLocallocUsed = localloc;
            compiler.info.compHasNextCallRetAddr = nextReturnAddress;
            compiler.info.compRetBuffArg = BAD_VAR_NUM;
            compiler.lvaParameterStackSize = 32;
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            if (returnBuffer)
            {
                _ = call.Args.PushBack(NewCallArg.CreateForPrimitive(compiler.gtNewLclvNode(TYP_BYREF, 0))
                    .WithWellKnownArg(WellKnownArg.RetBuffer));
            }

            Assert.That(compiler.fgCanFastTailCall(call, out var reason), Is.EqualTo(expectedReason is null));
            Assert.That(reason, Is.EqualTo(expectedReason));
            Assert.That(call.Args.IsAbiInformationDetermined, Is.True);
        });
    }

    [TestCase(false, false, false, false, true)]
    [TestCase(false, false, false, true, false)]
    [TestCase(true, false, false, true, true)]
    [TestCase(false, true, false, true, true)]
    [TestCase(false, false, true, false, true)]
    [TestCase(false, false, true, true, false)]
    public static void FastTailCallsCannotRetainLocalStructCopies(
        bool minOpts, bool promoted, bool undonePromotion, bool allDying, bool mustCopy)
    {
        WithCompiler(compiler => {
            compiler.opts.compFastTailCalls = true;
            compiler.lvaParameterStackSize = 32;
            var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)0x1234, true, 24, TYP_STRUCT, "TailCallStruct", "TailCallStruct");
            var layouts = new ClassLayoutTable();
            _ = layouts.AddObjLayout(compiler, layout);
            var layoutTableField = typeof(Compiler).GetField("_classLayoutTable", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing class layout table.");
            layoutTableField.SetValue(compiler, layouts);
            ref var parameter = ref compiler.lvaTable[0];
            parameter.Type = TYP_STRUCT;
            parameter.Layout = layout;
            parameter.lvIsParam = true;
            parameter.IsImplicitByRef = true;
            parameter.lvPromoted = promoted;
            if (undonePromotion)
            {
                parameter.lvFieldLclStart = 2;
                compiler.lvaTable[2].lvPromoted = true;
                compiler.lvaTable[2].lvFieldCnt = 2;
            }

            var local = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            local.Flags |= allDying
                ? (undonePromotion ? compiler.lvaTable[2].AllFieldDeathFlags : GTF_VAR_DEATH)
                : (undonePromotion ? GTF_VAR_DEATH : GTF_EMPTY);
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var argument = call.Args.PushBack(NewCallArg.CreateForStruct(local, TYP_STRUCT, layout));
            Assert.That(local.IsImplicitByrefParameterValuePreMorph(compiler), Is.SameAs(local));
            Assert.That(compiler.fgCanFastTailCall(call, out var reason), Is.EqualTo(!mustCopy));
            Assert.That(argument.AbiInfo.IsPassedByReference, Is.True);
            Assert.That(compiler.fgCallArgWillPointIntoLocalFrame(call, argument), Is.EqualTo(mustCopy));
            Assert.That(reason, Is.EqualTo(mustCopy ? "Callee has a byref parameter" : null));
            Assert.That(argument.Node, Is.SameAs(local));
        }, minOpts);
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void FastTailCallReportsPreserveNativeText(bool enabled)
    {
        WithCompiler(compiler => {
            compiler.opts.compFastTailCalls = enabled;
            compiler.lvaParameterStackSize = 32;
            compiler.info.compFullName = "TailCaller";
            var call = compiler.gtNewCallNode(TYP_VOID, CT_INDIRECT, null);
            call.ControlExpr = compiler.gtNewIconNode(TYP_I_IMPL, 1);
            var reportField = typeof(JitConfigValues).GetField("_jitReportFastTailCallDecisions", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Missing fast-tail-call reporting setting.");
            var previousConfig = JitConfig;
            object config = previousConfig;
            reportField.SetValue(config, 1);
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            var previousWriter = s_jitstdout;
            try
            {
                JitConfig = (JitConfigValues)config;
                s_jitstdout = writer;
                Assert.That(compiler.fgCanFastTailCall(call, out _), Is.EqualTo(enabled));
                writer.Flush();
            }
            finally
            {
                s_jitstdout = previousWriter;
                JitConfig = previousConfig;
            }

            var decision = enabled ? "Will fast tailcall" : "Will not fast tailcall (Configuration doesn't allow fast tail calls)";
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(
                $"[Fast tailcall decision]: Caller: TailCaller{Environment.NewLine}" +
                $"[Fast tailcall decision]: Callee: IndirectCall -- Decision: {decision}" +
                $" (CallerArgStackSize: 32, CalleeArgStackSize: 32){Environment.NewLine}{Environment.NewLine}"));
        });
    }
#endif

    private static GenTreeOp NewOverflowingAdd(Compiler compiler)
    {
        var node = compiler.gtNewBinaryNode(GT_ADD, TYP_INT,
            compiler.gtNewLclvNode(TYP_INT, 0), compiler.gtNewLclvNode(TYP_INT, 1));
        node.Flags |= GTF_OVERFLOW | GTF_EXCEPT;
        return node;
    }

    private static void WithCompiler(Action<Compiler> action, bool minOpts = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(minOpts);
        compiler.lvaTable = new LclVarDsc[3];
        compiler.lvaCount = 3;
        compiler.virtualStubParamInfo = new Compiler.VirtualStubParamInfo();
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
