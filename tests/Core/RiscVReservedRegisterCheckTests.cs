// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_RISCV64
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class RiscVReservedRegisterCheckTests
{
    [TestCase(new byte[] { 0xE1, 0xAA }, new byte[] { 0xAA }, 1)]
    [TestCase(new byte[] { 0xE2, 0x01, 0x02, 0xAA }, new byte[] { 0xAA }, 3)]
    [TestCase(new byte[] { 0xAA }, new byte[] { 0xAA }, 0)]
    [TestCase(new byte[] { 0xAA, 0xBB }, new byte[] { 0xBB }, -1)]
    public static void PrologFrameCodeMatchesTheCorrespondingEpilogTail(
        byte[] prologCodes,
        byte[] epilogCodes,
        int expectedMatchIndex)
    {
        var unwindPrologCodesType = typeof(UnwindInfo).GetNestedType("UnwindPrologCodes", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("UnwindPrologCodes type was not found.");
        var unwindEpilogCodesType = typeof(UnwindInfo).GetNestedType("UnwindEpilogCodes", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("UnwindEpilogCodes type was not found.");
        var unwindEpilogInfoType = typeof(UnwindInfo).GetNestedType("UnwindEpilogInfo", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("UnwindEpilogInfo type was not found.");
        var constructorFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var unwindPrologCodes = Activator.CreateInstance(unwindPrologCodesType, constructorFlags, null, [null], null)
            ?? throw new InvalidOperationException("UnwindPrologCodes construction failed.");
        var epilogInfo = Activator.CreateInstance(unwindEpilogInfoType, constructorFlags, null, [null], null)
            ?? throw new InvalidOperationException("UnwindEpilogInfo construction failed.");
        var epilogStorage = unwindEpilogInfoType.GetField("epiCodes", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(epilogInfo)
            ?? throw new InvalidOperationException("UnwindEpilogInfo.epiCodes was not found.");
        var addCodeFlags = BindingFlags.Instance | BindingFlags.Public;
        var prologAddCode = unwindPrologCodesType.GetMethod(
            "AddCode", addCodeFlags, null, [typeof(byte)], null)
            ?? throw new InvalidOperationException("UnwindPrologCodes.AddCode was not found.");
        var epilogAddCode = unwindEpilogCodesType.GetMethod(
            "AddCode", addCodeFlags, null, [typeof(byte)], null)
            ?? throw new InvalidOperationException("UnwindEpilogCodes.AddCode was not found.");
        var finalizeCodes = unwindEpilogInfoType.GetMethod("FinalizeCodes", addCodeFlags)
            ?? throw new InvalidOperationException("UnwindEpilogInfo.FinalizeCodes was not found.");
        var match = unwindPrologCodesType.GetMethod(
            "Match", addCodeFlags, null, [unwindEpilogInfoType], null)
            ?? throw new InvalidOperationException("UnwindPrologCodes.Match was not found.");

        for (var index = prologCodes.Length - 1; index >= 0; index--)
        {
            _ = prologAddCode.Invoke(unwindPrologCodes, [prologCodes[index]]);
        }

        foreach (var code in epilogCodes)
        {
            _ = epilogAddCode.Invoke(epilogStorage, [code]);
        }

        _ = finalizeCodes.Invoke(epilogInfo, null);

        Assert.That(match.Invoke(unwindPrologCodes, [epilogInfo]), Is.EqualTo(expectedMatchIndex));
    }

    [Test]
    public static void ReservedRegisterCheckAdvancesFrameLayoutAndReservesTheRegister()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            CORINFO_METHOD_INFO methodInfo = default;
            compiler.info.compMethodInfo = &methodInfo;
            compiler.lvaTable =
            [
                new LclVarDsc { Type = TYP_INT, lvOnFrame = true, RegNum = REG_STK },
                new LclVarDsc
                {
                    Type = TYP_STRUCT,
                    lvOnFrame = true,
                    RegNum = REG_STK,
                    Layout = new ClassLayout(0),
                },
            ];
            compiler.lvaCount = 2;
            compiler.lvaOutgoingArgSpaceVar = 1;
            compiler.lvaOutgoingArgSpaceSize.ResetWritePhase();
            compiler.lvaOutgoingArgSpaceSize.Value = 0;
            compiler.lvaRetAddrVar = BAD_VAR_NUM;
            compiler.lvaMonAcquired = BAD_VAR_NUM;
            compiler.lvaResumedIndicator = BAD_VAR_NUM;
            compiler.lvaAsyncThreadObjectVar = BAD_VAR_NUM;
            compiler.lvaAsyncExecutionContextVar = BAD_VAR_NUM;
            compiler.lvaAsyncSynchronizationContextVar = BAD_VAR_NUM;
            compiler.lvaGSSecurityCookie = BAD_VAR_NUM;
            compiler.compLclFrameSize = 0;
            codeGen.IsFramePointerUsed = true;

            Assert.That(compiler.compRsvdRegCheck(Compiler.REGALLOC_FRAME_LAYOUT), Is.True);
            Assert.That(compiler.lvaDoneFrameLayout, Is.EqualTo(Compiler.REGALLOC_FRAME_LAYOUT));
        });
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        using var tls = new JitTls(null);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(false);
        compiler.lvaCount = 1;
        compiler.lvaTable = [new LclVarDsc()];
        compiler.lvaTrackedCount = 1;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.osPageSize = 4096;
        compiler.lvaOutgoingArgSpaceSize.Value = 0;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.RegSet.rsClearRegsModified();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );

            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }
}
#endif
