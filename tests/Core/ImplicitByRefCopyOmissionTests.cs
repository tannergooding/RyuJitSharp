// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.gtCallTypes;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class ImplicitByRefCopyOmissionTests
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void MarksTheEffectiveLastUseAndReusesAbiClassification(bool comma, bool classified)
    {
        WithCall(16, (compiler, call, local, arg) => {
            if (comma)
            {
                arg.EarlyNode = compiler.gtNewCommaNode(TYP_STRUCT, compiler.gtNewIconNode(TYP_INT, 1), local);
            }
            if (classified)
            {
                call.Args.DetermineAbiInfo(compiler, call);
            }
            Append(compiler, call);

            Assert.That(MarkCandidates(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaTable[0].lvIsLastUseCopyOmissionCandidate, Is.True);
            Assert.That(call.Args.IsAbiInformationDetermined, Is.True);
            Assert.That(arg.AbiInfo.IsPassedByReference, Is.True);
            Assert.That(local.Flags & GTF_GLOB_REF, Is.EqualTo(GTF_EMPTY));
            Assert.That(MarkCandidates(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
        });
    }

    [TestCase("no-liveness")]
    [TestCase("no-call")]
    [TestCase("not-last-use")]
    [TestCase("promoted")]
    [TestCase("field-local")]
    [TestCase("implicit-byref")]
    [TestCase("already-marked")]
    public static void IneligibleUsesDoNotTriggerAbiClassification(string reason)
    {
        WithCall(16, (compiler, call, local, _) => {
            compiler.fgDidEarlyLiveness = reason != "no-liveness";
            compiler.lvaTable[0].lvPromoted = reason == "promoted";
            compiler.lvaTable[0].lvIsStructField = reason == "field-local";
            compiler.lvaTable[0].IsImplicitByRef = reason == "implicit-byref";
            compiler.lvaTable[0].lvIsLastUseCopyOmissionCandidate = reason == "already-marked";
            if (reason == "not-last-use")
            {
                local.Flags &= ~GTF_VAR_DEATH;
            }
            if (reason == "no-call")
            {
                call.Flags &= ~GTF_CALL;
            }
            Append(compiler, call);

            Assert.That(MarkCandidates(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaTable[0].lvIsLastUseCopyOmissionCandidate, Is.EqualTo(reason == "already-marked"));
            Assert.That(call.Args.IsAbiInformationDetermined, Is.False);
        });
    }

    [Test]
    public static void RegisterPassedStructIsNotACopyOmissionCandidate()
    {
        WithCall(8, (compiler, call, _, arg) => {
            Append(compiler, call);
            Assert.That(MarkCandidates(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(call.Args.IsAbiInformationDetermined, Is.True);
            Assert.That(arg.AbiInfo.IsPassedByReference, Is.False);
            Assert.That(compiler.lvaTable[0].lvIsLastUseCopyOmissionCandidate, Is.False);
        });
    }

    [Test]
    public static void FindsNestedCallsUnderPrimitiveArguments()
    {
        WithCall(16, (compiler, inner, _, _) => {
            inner.Type = TYP_INT;
            var outer = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            _ = outer.Args.PushBack(NewCallArg.CreateForPrimitive(inner));
            Append(compiler, outer);

            Assert.That(MarkCandidates(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaTable[0].lvIsLastUseCopyOmissionCandidate, Is.True);
            Assert.That(inner.Args.IsAbiInformationDetermined, Is.True);
            Assert.That(outer.Args.IsAbiInformationDetermined, Is.False);
        });
    }

    [Test]
    public static void FieldReadsUseTheirOwningLocalDescriptor()
    {
        WithCall(16, (compiler, call, _, arg) => {
            var field = compiler.gtNewLclFldNode(TYP_STRUCT, 0, 0, compiler.lvaTable[0].Layout);
            field.Flags |= GTF_VAR_DEATH;
            arg.EarlyNode = field;
            Append(compiler, call);

            Assert.That(MarkCandidates(compiler), Is.EqualTo(PhaseStatus.MODIFIED_NOTHING));
            Assert.That(compiler.lvaTable[0].lvIsLastUseCopyOmissionCandidate, Is.True);
        });
    }

    private static void Append(Compiler compiler, GenTree root)
    {
        var block = BasicBlock.New(compiler, BBJ_RETURN);
        compiler.fgFirstBB = block;
        compiler.fgLastBB = block;
        var statement = compiler.gtNewStmt(root);
        compiler.fgInsertStmtAtEnd(block, statement);
        compiler.fgSequenceLocals(statement);
    }

    private static void WithCall(uint size, Action<Compiler, GenTreeCall, GenTreeLclVar, CallArg> action)
    {
        FlowGraphCleanupTests.WithCompiler(NodeThreading.AllLocals, compiler => {
            var layout = new ClassLayout((CORINFO_CLASS_STRUCT_*)0x1234, true, size, TYP_STRUCT, "Value", "Value");
            var layouts = new ClassLayoutTable();
            _ = layouts.AddObjLayout(compiler, layout);
            LayoutTable(compiler) = layouts;
            compiler.fgDidEarlyLiveness = true;
            compiler.lvaTable[0].Type = TYP_STRUCT;
            compiler.lvaTable[0].Layout = layout;
            var local = compiler.gtNewLclvNode(TYP_STRUCT, 0);
            local.Flags |= GTF_VAR_DEATH;
            var call = compiler.gtNewCallNode(TYP_VOID, CT_USER_FUNC, null);
            var arg = call.Args.PushBack(NewCallArg.CreateForStruct(local, TYP_STRUCT, layout));
            action(compiler, call, local, arg);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "fgMarkImplicitByRefCopyOmissionCandidates")]
    private static extern PhaseStatus MarkCandidates(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_classLayoutTable")]
    private static extern ref ClassLayoutTable? LayoutTable(Compiler compiler);
}
