// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.IPmappingDscKind;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenIPMappingTests
{
    [Test]
    public static void MappingIdentityIncludesFlagsAndKindButNotLabelsOrNativePositions()
    {
        WithMappings((compiler, codeGen) =>
        {
            var di = Info(4, ICorDebugInfo.STACK_EMPTY);
            codeGen.genIPmappingAdd(Normal, di, false);
            codeGen.instGen(INS_nop);
            codeGen.genIPmappingAdd(Normal, di, true);
            codeGen.genIPmappingAdd(Normal, Info(4, ICorDebugInfo.CALL_INSTRUCTION), true);
            codeGen.genIPmappingAdd(NoMapping, default, false);
            codeGen.genIPmappingAdd(NoMapping, default, true);
            codeGen.genIPmappingAdd(Prolog, default, true);
            codeGen.genIPmappingAdd(Prolog, default, true);
            codeGen.genIPmappingAdd(Epilog, default, true);
            codeGen.genIPmappingAdd(Epilog, default, true);
            codeGen.genIPmappingAddToFront(Prolog, default, true);

            var mappings = compiler.genIPmappings.ToArray();
            IPmappingDscKind[] expected = [Prolog, Normal, Normal, NoMapping, Prolog, Prolog, Epilog, Epilog];
            Assert.That(mappings.Select(mapping => mapping.ipmdKind), Is.EqualTo(expected));
            Assert.That(mappings[0].ipmdNativeLoc.GetInsNum(), Is.EqualTo(1));
            Assert.That(mappings[1].ipmdNativeLoc.GetInsNum(), Is.Zero);
            Assert.That(mappings[1].ipmdIsLabel, Is.False);
            Assert.That(mappings[2].ipmdLoc.SourceTypes, Is.EqualTo(ICorDebugInfo.CALL_INSTRUCTION));
            Assert.That(mappings[2].ipmdNativeLoc.GetInsNum(), Is.EqualTo(1));
            Assert.That(mappings[3].ipmdLoc.IsValid, Is.False);
            Assert.That(mappings[3].ipmdIsLabel, Is.False);
        });
    }

    [TestCase(false, false, false, 0)]
    [TestCase(true, false, false, 1)]
    [TestCase(true, true, false, 1)]
    [TestCase(true, false, true, 0)]
    public static void DebugPaddingRequiresTheSameUnadvancedLocation(
        bool debugCode, bool alreadyEmitted, bool differentFlags, int expectedCount)
    {
        WithMappings((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = debugCode;
            var di = Info(4, ICorDebugInfo.STACK_EMPTY);
            codeGen.genIPmappingAdd(Normal, di, true);
            if (alreadyEmitted)
            {
                codeGen.instGen(INS_nop);
            }
            var query = differentFlags ? Info(4, ICorDebugInfo.CALL_INSTRUCTION) : di;

            codeGen.genEnsureCodeEmitted(query);
            codeGen.genEnsureCodeEmitted(query);

            Assert.That(CurrentCount(codeGen.Emitter), Is.EqualTo(expectedCount));
            Assert.That(CurrentSize(codeGen.Emitter), Is.EqualTo(expectedCount));
            Assert.That(compiler.genIPmappings, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void DisabledMappingAndInvalidOrUnreportedLocationsLeaveNoRecordsOrCode()
    {
        WithMappings((compiler, codeGen) =>
        {
            compiler.opts.compDbgInfo = false;
            compiler.opts.compDbgCode = true;
            codeGen.genIPmappingAdd(Normal, default, true);
            codeGen.genIPmappingAddToFront(Normal, default, true);
            codeGen.genEnsureCodeEmitted(default);
            codeGen.genEnsureCodeEmitted(Info(4, ICorDebugInfo.STACK_EMPTY));

            Assert.That(compiler.genIPmappings, Is.Empty);
            Assert.That(CurrentCount(codeGen.Emitter), Is.Zero);
        });
    }

#if DEBUG
    [Test]
    public static void MappingDiagnosticsPreserveFlagsLocationsAndDuplicateMessages()
    {
        WithMappings((compiler, codeGen) =>
        {
            compiler.verbose = true;
            var di = Info(4, ICorDebugInfo.STACK_EMPTY | ICorDebugInfo.CALL_INSTRUCTION | ICorDebugInfo.ASYNC);
            var group = codeGen.Emitter.emitCurIG;
            assert(group is not null);
            var nativeLocation = $"({codeGen.Emitter.emitLabelString(group)},ins#0,ofs#0)";
            var output = CodeGenLifeTransitionTests.Capture(() =>
            {
                codeGen.genIPmappingAdd(Normal, di, true);
                codeGen.genIPmappingAdd(Normal, di, false);
                codeGen.genIPmappingAddToFront(Prolog, default, true);
                codeGen.genIPmappingListDisp();
            });

            Assert.That(output, Is.EqualTo(
                $"Added IP mapping: 0x0004 STACK_EMPTY CALL_INSTRUCTION ASYNC {nativeLocation} label{Environment.NewLine}" +
                $"genIPmappingAdd: ignoring duplicate IL offset 0x4{Environment.NewLine}" +
                $"Added IP mapping to front: PROLOG {nativeLocation} label{Environment.NewLine}" +
                $"0: PROLOG {nativeLocation} label{Environment.NewLine}" +
                $"1: 0x0004 STACK_EMPTY CALL_INSTRUCTION ASYNC {nativeLocation} label{Environment.NewLine}"));
        });
    }
#endif

    private static DebugInfo Info(int offset, ICorDebugInfo.SourceTypes sourceTypes)
    {
        var context = (InlineContext)RuntimeHelpers.GetUninitializedObject(typeof(InlineContext));
        return new DebugInfo(context, new ILLocation(offset, sourceTypes));
    }

    private static void WithMappings(Action<Compiler, CodeGen> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_INT, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.opts.compDbgInfo = true;
            compiler.info.compILCodeSize = 32;
            compiler.genIPmappings = [];
            action(compiler, codeGen);
        });
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);
}
