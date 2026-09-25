// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
#if DEBUG
using System.IO;
using System.Text;
#endif
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ICorDebugInfo.VarLocType;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class VariableLiveKeeperTests
{
    [TestCase(false, false)]
    [TestCase(true, true)]
    public static void NeitherAndSimultaneousBirthDeathLeaveTheRangeUnchanged(bool born, bool dying)
    {
        WithCompiler((compiler, codeGen) =>
        {
            var keeper = codeGen.getVariableLiveKeeper();
            keeper.siStartOrCloseVariableLiveRange(in compiler.lvaTable[0], 0, born, dying);
            Assert.That(keeper.getLiveRangesForVarForBody(0), Is.Empty);

            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            keeper.siStartOrCloseVariableLiveRange(in compiler.lvaTable[0], 0, born, dying);
            Assert.That(keeper.getLiveRangesForVarForBody(0), Has.Count.EqualTo(1));
            Assert.That(keeper.getLiveRangesForVarForBody(0)[0].m_EndEmitLocation.Valid(), Is.False);
        });
    }

    [Test]
    public static void BirthAndDeathCaptureHalfOpenInstructionLocations()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var keeper = codeGen.getVariableLiveKeeper();
            EmitInstruction(codeGen.Emitter, 3);
            keeper.siStartOrCloseVariableLiveRange(in compiler.lvaTable[0], 0, true, false);
            EmitInstruction(codeGen.Emitter, 5);
            keeper.siStartOrCloseVariableLiveRange(in compiler.lvaTable[0], 0, false, true);
            NextGroup(codeGen.Emitter, false);

            var range = keeper.getLiveRangesForVarForBody(0)[0];
            Assert.That(range.m_StartEmitLocation.CodeOffset(codeGen.Emitter), Is.EqualTo(3u));
            Assert.That(range.m_EndEmitLocation.CodeOffset(codeGen.Emitter), Is.EqualTo(8u));
            Assert.That(range.m_VarLocation.vlType, Is.EqualTo(VLT_STK));
            Assert.That(range.m_VarLocation.vlStk.vlsBaseReg, Is.EqualTo(ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP));
            Assert.That(range.m_VarLocation.vlStk.vlsOffset, Is.EqualTo(16));
        });
    }

    [TestCase(0, 2)]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    public static void SameHomeCoalescesOnlyAcrossTheImmediatelyPreviousInstruction(int gapInstructions, int count)
    {
        WithCompiler((compiler, codeGen) =>
        {
            var keeper = codeGen.getVariableLiveKeeper();
            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            EmitInstruction(codeGen.Emitter, 2);
            keeper.siEndVariableLiveRange(0);
            for (var index = 0; index < gapInstructions; index++)
            {
                EmitInstruction(codeGen.Emitter, 3);
            }
            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);

            var ranges = keeper.getLiveRangesForVarForBody(0);
            Assert.That(ranges, Has.Count.EqualTo(count));
            Assert.That(ranges[^1].m_EndEmitLocation.Valid(), Is.False);
            if (count == 2)
            {
                Assert.That(ranges[0].m_EndEmitLocation.Valid(), Is.True);
            }
        });
    }

    [Test]
    public static void HomeChangesPreserveZeroLengthRangesAndClosedLocationValues()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var keeper = codeGen.getVariableLiveKeeper();
            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            compiler.lvaTable[0].StackOffset = 24;
            keeper.siUpdateVariableLiveRange(in compiler.lvaTable[0], 0);
            keeper.siEndVariableLiveRange(0);
            NextGroup(codeGen.Emitter, false);

            var ranges = keeper.getLiveRangesForVarForBody(0);
            Assert.That(ranges, Has.Count.EqualTo(2));
            Assert.That(ranges[0].m_VarLocation.vlStk.vlsOffset, Is.EqualTo(16));
            Assert.That(ranges[1].m_VarLocation.vlStk.vlsOffset, Is.EqualTo(24));
            foreach (var range in ranges)
            {
                Assert.That(range.m_StartEmitLocation.CodeOffset(codeGen.Emitter), Is.Zero);
                Assert.That(range.m_EndEmitLocation.CodeOffset(codeGen.Emitter), Is.Zero);
            }
        });
    }

    [Test]
    public static void JitTemporariesAndUnallocatedLocalsAreNotReported()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var keeper = codeGen.getVariableLiveKeeper();
            keeper.siStartVariableLiveRange(in compiler.lvaTable[3], 3);
            keeper.siEndVariableLiveRange(3);
            keeper.siUpdateVariableLiveRange(in compiler.lvaTable[3], 3);
            compiler.lvaTable[0].lvOnFrame = false;
            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);

            Assert.That(keeper.getLiveRangesCount(), Is.EqualTo((nuint)0));
            Assert.That(keeper.getLiveRangesForVarForBody(0), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void DebugInfoRatherThanScopeInfoControlsReporting(bool scopeInfo)
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.opts.compScopeInfo = scopeInfo;
            compiler.opts.compDbgInfo = false;
            codeGen.initializeVariableLiveKeeper();
            var keeper = codeGen.getVariableLiveKeeper();
            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            keeper.siEndVariableLiveRange(0);
            keeper.siUpdateVariableLiveRange(in compiler.lvaTable[0], 0);
            Assert.That(keeper.getLiveRangesCount(), Is.EqualTo((nuint)0));

            compiler.opts.compDbgInfo = true;
            codeGen.initializeVariableLiveKeeper();
            keeper = codeGen.getVariableLiveKeeper();
            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            Assert.That(keeper.getLiveRangesForVarForBody(0), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public static void PrologAndBodyRangesAreIndependentAndUnknownIlLocalsAreNotCounted()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var keeper = codeGen.getVariableLiveKeeper();
            var location = new CodeGen.siVarLoc();
            location.storeVariableInRegisters(REG_RCX, REG_NA);
            keeper.psiStartVariableLiveRange(location, 0);
            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            compiler.lvaOutgoingArgSpaceVar = 2;
            keeper.siStartVariableLiveRange(in compiler.lvaTable[2], 2);
            keeper.psiClosePrologVariableRanges();

            Assert.That(keeper.getLiveRangesForVarForProlog(0)[0].m_EndEmitLocation.Valid(), Is.True);
            Assert.That(keeper.getLiveRangesForVarForBody(0)[0].m_EndEmitLocation.Valid(), Is.False);
            Assert.That(keeper.getLiveRangesForVarForBody(2), Has.Count.EqualTo(1));
            Assert.That(keeper.getLiveRangesCount(), Is.EqualTo((nuint)2));
        });
    }

    [Test]
    public static void TrackedSetsUseTrackedIndexOrderAndFinalCloseSuppressesLaterEmitterAccess()
    {
        WithCompiler((compiler, codeGen) =>
        {
            var keeper = codeGen.getVariableLiveKeeper();
            var variables = VarSetOps.MakeEmpty(compiler);
            VarSetOps.AddElemD(compiler, variables, 0);
            VarSetOps.AddElemD(compiler, variables, 2);
            keeper.siStartOrCloseVariableLiveRanges(variables, true, false);
            Assert.That(keeper.getLiveRangesForVarForBody(0), Is.Empty);
            Assert.That(keeper.getLiveRangesForVarForBody(1), Has.Count.EqualTo(1));
            Assert.That(keeper.getLiveRangesForVarForBody(2), Has.Count.EqualTo(1));

            keeper.siEndAllVariableLiveRange(variables);
            codeGen.Emitter.emitCurIG = null;
            keeper.siEndVariableLiveRange(1);
            keeper.siUpdateVariableLiveRange(in compiler.lvaTable[1], 1);
            Assert.That(keeper.getLiveRangesForVarForBody(1)[0].m_EndEmitLocation.Valid(), Is.True);
            Assert.That(keeper.getLiveRangesForVarForBody(2)[0].m_EndEmitLocation.Valid(), Is.True);
        });
    }

    [Test]
    public static void MinoptsWithoutTrackedLocalsClosesAllRanges()
    {
        WithCompiler((compiler, codeGen) =>
        {
            compiler.lvaTrackedCount = 0;
            var keeper = codeGen.getVariableLiveKeeper();
            keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            keeper.siStartVariableLiveRange(in compiler.lvaTable[1], 1);
            keeper.siEndAllVariableLiveRange([]);

            Assert.That(keeper.getLiveRangesForVarForBody(0)[0].m_EndEmitLocation.Valid(), Is.True);
            Assert.That(keeper.getLiveRangesForVarForBody(1)[0].m_EndEmitLocation.Valid(), Is.True);
        });
    }

    [TestCase(REG_RAX, VLT_REG, 0)]
    [TestCase(REG_R15, VLT_REG, 15)]
    [TestCase(REG_XMM0, VLT_REG_FP, 16)]
    [TestCase(REG_XMM15, VLT_REG_FP, 31)]
    [TestCase(REG_XMM16, VLT_INVALID, 0)]
    [TestCase(REG_K1, VLT_INVALID, 0)]
    public static void RegisterHomesUseTheNativeDebugEncoding(regNumber reg, ICorDebugInfo.VarLocType type, int number)
    {
        var location = new CodeGen.siVarLoc();
        location.storeVariableInRegisters(reg, REG_NA);

        Assert.That(location.vlType, Is.EqualTo(type));
        if (type != VLT_INVALID)
        {
            Assert.That((int)location.vlReg.vlrReg, Is.EqualTo(number));
        }
    }

    [TestCase(REG_RAX, REG_XMM15, VLT_REG_REG)]
    [TestCase(REG_XMM0, REG_XMM15, VLT_REG_REG)]
    [TestCase(REG_RAX, REG_XMM16, VLT_INVALID)]
    [TestCase(REG_K1, REG_RAX, VLT_INVALID)]
    public static void RegisterPairsRejectUnencodableMembers(regNumber first, regNumber second,
        ICorDebugInfo.VarLocType type)
    {
        var location = new CodeGen.siVarLoc();
        location.storeVariableInRegisters(first, second);

        Assert.That(location.vlType, Is.EqualTo(type));
        if (type == VLT_REG_REG)
        {
            Assert.That(location.vlRegReg.vlrrReg1, Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(first)));
            Assert.That(location.vlRegReg.vlrrReg2, Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(second)));
        }
    }

    [TestCase(TYP_INT, REG_R8, VLT_REG)]
    [TestCase(TYP_BYREF, REG_RCX, VLT_REG)]
    [TestCase(TYP_DOUBLE, REG_XMM15, VLT_REG_FP)]
    [TestCase(TYP_FLOAT, REG_XMM16, VLT_INVALID)]
    public static void LocalDescriptorsProduceRegisterHomes(var_types type, regNumber reg,
        ICorDebugInfo.VarLocType expected)
    {
        WithCompiler((compiler, codeGen) =>
        {
            ref var local = ref compiler.lvaTable[0];
            local.Type = type;
            local.lvLRACandidate = true;
            local.RegNum = reg;
            var keeper = codeGen.getVariableLiveKeeper();
            keeper.siStartVariableLiveRange(in local, 0);

            var location = keeper.getLiveRangesForVarForBody(0)[0].m_VarLocation;
            Assert.That(location.vlType, Is.EqualTo(expected));
            if (expected != VLT_INVALID)
            {
                Assert.That(location.vlReg.vlrReg, Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(reg)));
            }
        });
    }

    [TestCase(false, false, false, ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP, 43)]
    [TestCase(false, true, false, ICorDebugInfo.RegNum.REGNUM_RSP, 43)]
    [TestCase(true, true, false, ICorDebugInfo.RegNum.REGNUM_RBP, 19)]
    [TestCase(false, false, true, ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP, 43)]
    public static void StackHomesPreserveFrameBiasAndImplicitByref(bool fpBased, bool fpUsed, bool implicitByref,
        ICorDebugInfo.RegNum baseReg, int offset)
    {
        WithCompiler((compiler, codeGen) =>
        {
            ref var local = ref compiler.lvaTable[0];
            local.lvFramePointerBased = fpBased;
            local.IsImplicitByRef = implicitByref;
            local.lvIsParam = implicitByref;
            local.Type = implicitByref ? TYP_BYREF : TYP_LONG;
            var location = codeGen.getSiVarLoc(in local, 3, 24);

            Assert.That(location.vlType, Is.EqualTo(implicitByref ? VLT_STK_BYREF : VLT_STK));
            Assert.That(location.vlStk.vlsBaseReg, Is.EqualTo(baseReg));
            Assert.That(location.vlStk.vlsOffset, Is.EqualTo(offset));
        }, fpUsed);
    }

    [Test]
    public static void LocationEqualityIgnoresInactiveUnionFieldsAndPreservesNullSemantics()
    {
        var left = new CodeGen.siVarLoc();
        left.storeVariableInRegisters(REG_RAX, REG_NA);
        var right = left;
        right.vlStk.vlsOffset = 42;
        Assert.That(CodeGen.siVarLoc.Equals(left, right), Is.True);
        right.storeVariableInRegisters(REG_RCX, REG_NA);
        Assert.That(CodeGen.siVarLoc.Equals(left, right), Is.False);
        Assert.That(CodeGen.siVarLoc.Equals(null, null), Is.True);
        Assert.That(CodeGen.siVarLoc.Equals(left, null), Is.False);
        Assert.That(Unsafe.SizeOf<CodeGen.siVarLoc>(), Is.EqualTo(Unsafe.SizeOf<ICorDebugInfo.VarLoc>()));
    }

#if DEBUG
    [Test]
    public static void RangeDiagnosticsPreserveNativeMessagesAndDumperBarrier()
    {
        WithCompiler((compiler, codeGen) =>
        {
            EmitInstruction(codeGen.Emitter, 1);
            compiler.verbose = true;
            var keeper = codeGen.getVariableLiveKeeper();
            var output = Capture(() =>
            {
                keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
                keeper.siEndVariableLiveRange(0);
                EmitInstruction(codeGen.Emitter, 1);
                keeper.siStartVariableLiveRange(in compiler.lvaTable[0], 0);
            });
            Assert.That(output, Is.EqualTo(
                $"Debug: New V00 debug range: first{Environment.NewLine}" +
                $"Debug: Closing V00 debug range.{Environment.NewLine}" +
                $"Debug: Extending V00 debug range...{Environment.NewLine}"));

            compiler.verbose = false;
            var block = new BasicBlock(null, null) { bbNum = 3 };
            var firstDump = Capture(() => keeper.dumpBlockVariableLiveRanges(block));
            var secondDump = Capture(() => keeper.dumpBlockVariableLiveRanges(block));
            Assert.That(secondDump, Is.EqualTo(firstDump));
            Assert.That(firstDump, Does.Contain("rsp'[16] (1 slot)"));
            keeper.siEndVariableLiveRange(0);
            _ = Capture(() => keeper.dumpBlockVariableLiveRanges(block));
            var emptyDump = Capture(() => keeper.dumpBlockVariableLiveRanges(block));
            Assert.That(emptyDump, Does.EndWith($"..None..{Environment.NewLine}"));
        });
    }

    private static string Capture(Action action)
    {
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        var previous = s_jitstdout;
        try
        {
            s_jitstdout = writer;
            action();
            writer.Flush();
        }
        finally
        {
            s_jitstdout = previous;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
#endif

    private static void WithCompiler(Action<Compiler, CodeGen> action, bool framePointerUsed = false)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        CORINFO_METHOD_INFO methodInfo = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
        compiler.opts.compDbgInfo = true;
        compiler.info.compMethodInfo = &methodInfo;
        compiler.info.compRetBuffArg = BAD_VAR_NUM;
        compiler.info.compTypeCtxtArg = BAD_VAR_NUM;
        compiler.lvaAsyncContinuationArg = BAD_VAR_NUM;
        compiler.lvaOutgoingArgSpaceVar = BAD_VAR_NUM;
        compiler.info.compLocalsCount = 3;
        compiler.info.compArgsCount = 1;
        compiler.lvaCount = 4;
        compiler.lvaTable = new LclVarDsc[4];
        for (var index = 0; index < compiler.lvaTable.Length; index++)
        {
            compiler.lvaTable[index] = new LclVarDsc
            {
                Type = TYP_INT,
                lvOnFrame = true,
                RegNum = REG_STK,
                StackOffset = 16,
            };
        }
        compiler.lvaTrackedCount = 3;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [2, 0, 1];
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.IsFramePointerUsed = framePointerUsed;
            codeGen.initializeVariableLiveKeeper();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(framePointerUsed
#if DEBUG
                , false
#endif
                );
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    private static void EmitInstruction(Emitter emitter, uint size)
    {
        var allocator = typeof(Emitter).GetMethod("emitNewInstr", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var descriptor = (Emitter.instrDesc)allocator.Invoke(emitter, [EA_4BYTE])!;
        descriptor.idIns(INS_nop);
        descriptor.idCodeSize(size);
        CurrentSize(emitter) += checked((int)size);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitNxtIG")]
    private static extern void NextGroup(Emitter emitter, bool extend);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);
}
