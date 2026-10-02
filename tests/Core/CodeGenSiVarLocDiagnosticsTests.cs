// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.ICorDebugInfo.VarLocType;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static class CodeGenSiVarLocDiagnosticsTests
{
    [Test]
    public static void VariableLocationDiagnosticsPreserveNativeFormatting()
    {
        var codeGen = (CodeGen)RuntimeHelpers.GetUninitializedObject(typeof(CodeGen));

        var integerRegister = new CodeGen.siVarLoc { vlType = VLT_REG };
        integerRegister.vlReg.vlrReg = (ICorDebugInfo.RegNum)REG_INT_FIRST;
        AssertOutput(codeGen, integerRegister, REG_INT_FIRST.Name);

        integerRegister.vlType = VLT_REG_BYREF;
        AssertOutput(codeGen, integerRegister, $"{REG_INT_FIRST.Name} byref");

        var floatingRegister = new CodeGen.siVarLoc { vlType = VLT_REG_FP };
#if TARGET_AMD64 || TARGET_ARM64
        floatingRegister.vlReg.vlrReg = ICorDebugInfo.RegNum.REGNUM_FP_FIRST;
#else
        floatingRegister.vlReg.vlrReg = (ICorDebugInfo.RegNum)0;
#endif
        AssertOutput(codeGen, floatingRegister, REG_FP_FIRST.Name);

        var stackLocation = new CodeGen.siVarLoc { vlType = VLT_STK };
        stackLocation.vlStk.vlsBaseReg = ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP;
        stackLocation.vlStk.vlsOffset = -12;
        AssertOutput(codeGen, stackLocation, $"{STR_SPBASE}'[-12] (1 slot)");

        stackLocation.vlType = VLT_STK_BYREF;
        AssertOutput(codeGen, stackLocation, $"{STR_SPBASE}'[-12] (1 slot) byref");

        var registerPair = new CodeGen.siVarLoc { vlType = VLT_REG_REG };
        registerPair.vlRegReg.vlrrReg1 = (ICorDebugInfo.RegNum)REG_INT_FIRST;
#if TARGET_AMD64 || TARGET_ARM64
        registerPair.vlRegReg.vlrrReg2 = ICorDebugInfo.RegNum.REGNUM_FP_FIRST;
        AssertOutput(codeGen, registerPair, $"{REG_INT_FIRST.Name}-{REG_FP_FIRST.Name}");
#else
        var secondIntegerRegister = (regNumber)((int)REG_INT_FIRST + 1);
        registerPair.vlRegReg.vlrrReg2 = (ICorDebugInfo.RegNum)secondIntegerRegister;
        AssertOutput(codeGen, registerPair, $"{REG_INT_FIRST.Name}-{secondIntegerRegister.Name}");
#endif

#if !TARGET_AMD64
        var registerStack = new CodeGen.siVarLoc { vlType = VLT_REG_STK };
        registerStack.vlRegStk.vlrsReg = (ICorDebugInfo.RegNum)REG_INT_FIRST;
        registerStack.vlRegStk.vlrsStk.vlrssBaseReg = ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP;
        registerStack.vlRegStk.vlrsStk.vlrssOffset = 20;
        AssertOutput(codeGen, registerStack, $"{REG_INT_FIRST.Name}-{STR_SPBASE}'[20]");

        registerStack.vlRegStk.vlrsStk.vlrssBaseReg = (ICorDebugInfo.RegNum)REG_FPBASE;
        registerStack.vlRegStk.vlrsStk.vlrssOffset = -8;
        AssertOutput(codeGen, registerStack, $"{REG_INT_FIRST.Name}-{REG_FPBASE.Name}[-8]");

        var twoStackSlots = new CodeGen.siVarLoc { vlType = VLT_STK2 };
        twoStackSlots.vlStk2.vls2BaseReg = ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP;
        twoStackSlots.vlStk2.vls2Offset = 16;
        AssertOutput(codeGen, twoStackSlots, $"{STR_SPBASE}'[16] (2 slots)");

        twoStackSlots.vlStk2.vls2BaseReg = (ICorDebugInfo.RegNum)REG_FPBASE;
        AssertOutput(codeGen, twoStackSlots, $"{REG_FPBASE.Name}[16] (2 slots)");

        var fpStack = new CodeGen.siVarLoc { vlType = VLT_FPSTK };
        fpStack.vlFPstk.vlfReg = 3;
        AssertOutput(codeGen, fpStack, "ST(L-3)");

        var fixedVarArg = new CodeGen.siVarLoc { vlType = VLT_FIXED_VA };
        fixedVarArg.vlFixedVarArg.vlfvOffset = 24;
        AssertOutput(codeGen, fixedVarArg, "fxd_va[24]");
#endif
    }

    [Test]
    public static void RegisterLocationEncodingPreservesTargetDebugRegisterNumbers()
    {
        Assert.That(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_INT_FIRST),
            Is.EqualTo((ICorDebugInfo.RegNum)REG_INT_FIRST));
        Assert.That(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_NA),
            Is.EqualTo(ICorDebugInfo.RegNum.REGNUM_COUNT));

#if TARGET_AMD64
        var highestEncodedRegister = (regNumber)((int)REG_FP_FIRST + 15);
        Assert.That(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_FP_FIRST),
            Is.EqualTo(ICorDebugInfo.RegNum.REGNUM_FP_FIRST));
        Assert.That(CodeGen.siVarLoc.mapRegNumToDebugRegNum(highestEncodedRegister),
            Is.EqualTo((ICorDebugInfo.RegNum)((int)ICorDebugInfo.RegNum.REGNUM_FP_FIRST + 15)));
        Assert.That(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_XMM16),
            Is.EqualTo(ICorDebugInfo.RegNum.REGNUM_COUNT));
#elif TARGET_ARM64
        Assert.That(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_FP_FIRST),
            Is.EqualTo(ICorDebugInfo.RegNum.REGNUM_FP_FIRST));
        Assert.That(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_FP_LAST),
            Is.EqualTo((ICorDebugInfo.RegNum)((int)ICorDebugInfo.RegNum.REGNUM_FP_FIRST + 31)));
#else
        Assert.That(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_FP_FIRST), Is.EqualTo((ICorDebugInfo.RegNum)0));
        Assert.That(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_FP_LAST),
            Is.EqualTo((ICorDebugInfo.RegNum)((int)REG_FP_LAST - (int)REG_FP_FIRST)));
#endif
    }

    [Test]
    public static void RegisterLocationsPreserveTargetRegisterPairRules()
    {
        var location = new CodeGen.siVarLoc();
        location.storeVariableInRegisters(REG_INT_FIRST, REG_NA);
        Assert.That(location.vlType, Is.EqualTo(VLT_REG));
        Assert.That(location.vlReg.vlrReg, Is.EqualTo((ICorDebugInfo.RegNum)REG_INT_FIRST));

        location.storeVariableInRegisters(REG_FP_FIRST, REG_NA);
        Assert.That(location.vlType, Is.EqualTo(VLT_REG_FP));
        Assert.That(location.vlReg.vlrReg, Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_FP_FIRST)));

        var secondIntegerRegister = (regNumber)((int)REG_INT_FIRST + 1);
        location.storeVariableInRegisters(REG_INT_FIRST, secondIntegerRegister);
        Assert.That(location.vlType, Is.EqualTo(VLT_REG_REG));
        Assert.That(location.vlRegReg.vlrrReg1,
            Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_INT_FIRST)));
        Assert.That(location.vlRegReg.vlrrReg2,
            Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(secondIntegerRegister)));

        location.storeVariableInRegisters(REG_INT_FIRST, REG_FP_FIRST);
#if TARGET_AMD64 || TARGET_ARM64
        Assert.That(location.vlType, Is.EqualTo(VLT_REG_REG));
        Assert.That(location.vlRegReg.vlrrReg2,
            Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_FP_FIRST)));
#else
        Assert.That(location.vlType, Is.EqualTo(VLT_INVALID));
#endif

        location.storeVariableInRegisters(REG_NA, REG_NA);
        Assert.That(location.vlType, Is.EqualTo(VLT_INVALID));
    }

    [Test]
    public static void VariableLocationConstructionPreservesTargetStackRepresentations()
    {
        var stackLocal = new LclVarDsc { Type = TYP_DOUBLE };
        var location = new CodeGen.siVarLoc(in stackLocal, REG_SPBASE, 24, isFramePointerUsed: false);
#if TARGET_64BIT
        Assert.That(location.vlType, Is.EqualTo(VLT_STK));
        Assert.That(location.vlStk.vlsBaseReg, Is.EqualTo(ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP));
        Assert.That(location.vlStk.vlsOffset, Is.EqualTo(24));
#else
        Assert.That(location.vlType, Is.EqualTo(VLT_STK2));
        Assert.That(location.vlStk2.vls2BaseReg, Is.EqualTo(ICorDebugInfo.RegNum.REGNUM_AMBIENT_SP));
        Assert.That(location.vlStk2.vls2Offset, Is.EqualTo(24));
#endif

        var floatingLocal = new LclVarDsc
        {
            Type = TYP_FLOAT,
            lvLRACandidate = true,
            RegNum = REG_FP_FIRST,
        };
        location = new CodeGen.siVarLoc(in floatingLocal, REG_SPBASE, 8, isFramePointerUsed: true);
#if TARGET_64BIT
        Assert.That(location.vlType, Is.EqualTo(VLT_REG_FP));
        Assert.That(location.vlReg.vlrReg,
            Is.EqualTo(CodeGen.siVarLoc.mapRegNumToDebugRegNum(REG_FP_FIRST)));
#else
        Assert.That(location.vlType, Is.EqualTo(VLT_FPSTK));
        Assert.That(location.vlFPstk.vlfReg, Is.EqualTo((int)REG_FP_FIRST));
#endif
    }

    private static void AssertOutput(CodeGen codeGen, CodeGen.siVarLoc location, string expected)
    {
        var output = Capture(() => codeGen.dumpSiVarLoc(in location));
        Assert.That(output, Is.EqualTo(expected));
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
}
#endif
