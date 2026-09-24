// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
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

    private static void WithCompiler(Action<Compiler> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaTable = new LclVarDsc[1];
        compiler.lvaCount = 1;
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
