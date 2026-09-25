// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.InfoAccessType;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenProfilingEntryTests
{
    [Test]
    public static void UnhookedEntryDoesNotChangeInstructionsOrScratchState()
    {
        WithEntry((compiler, codeGen) =>
        {
            ConfigureArguments(compiler, [(TYP_REF, REG_RCX)]);
            var zeroed = true;

            codeGen.genProfilingEnterCallback(REG_RAX, ref zeroed);

            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(zeroed, Is.True);
            Assert.That(compiler.info.compProfilerCallback, Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public static void EntryHomesRegisterArgumentsCallsProfilerAndReloadsInNativeOrder(bool indirect)
    {
        WithEntry((compiler, codeGen) =>
        {
            ConfigureArguments(compiler, [
                (TYP_REF, REG_RCX), (TYP_DOUBLE, REG_XMM1),
                (TYP_SIMD8, REG_R8), (TYP_STRUCT, REG_R9), (TYP_INT, REG_NA),
            ]);
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHndIndirected = indirect;
            compiler.compProfilerMethHnd = (void*)0x12345678;
            codeGen.IsFramePointerUsed = true;
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);
            var zeroed = true;

            codeGen.genProfilingEnterCallback(REG_RAX, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(11));
            regNumber[] regs = [REG_RCX, REG_XMM1, REG_R8, REG_R9];
            instruction[] instructions = [INS_mov, INS_movsd_simd, INS_mov, INS_mov];
            emitAttr[] widths = [EA_8BYTE, EA_8BYTE, EA_8BYTE, EA_8BYTE];
            for (var i = 0; i < regs.Length; i++)
            {
                AssertStack(ids[i], true, instructions[i], widths[i], i, regs[i]);
                AssertStack(ids[i + 7], false, instructions[i], widths[i], i, regs[i]);
            }

            Assert.That(ids[4].idIns(), Is.EqualTo(INS_mov));
            Assert.That(ids[4].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(ids[4].idIsDspReloc(), Is.EqualTo(indirect));
            if (indirect)
            {
                Assert.That(Displacement(codeGen.Emitter, ids[4]), Is.EqualTo((nint)0x12345678));
            }
            else
            {
                Assert.That(InstructionConstant(codeGen.Emitter, ids[4]), Is.EqualTo((nint)0x12345678));
            }

            Assert.That(ids[5].idIns(), Is.EqualTo(INS_lea));
            Assert.That(ids[5].idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(ids[5].idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RBP));
            Assert.That(ids[5].idAddr().iiaAddrMode.amDisp,
                Is.EqualTo(-compiler.lvaToCallerSPRelativeOffset(0, isFpBased: true)));
            Assert.That(ids[6].idIns(), Is.EqualTo(INS_call));
            Assert.That(ids[6].idIsNoGC(), Is.True);
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RBX));
            Assert.That(zeroed, Is.False);
            Assert.That(compiler.info.compProfilerCallback, Is.False);
        });
    }

    [TestCase(TYP_FLOAT, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, EA_8BYTE)]
    public static void VarargsReuseHomedArgumentsAndRestoreFloatingIntegerShadow(
        var_types type, emitAttr width)
    {
        WithEntry((compiler, codeGen) =>
        {
            ConfigureArguments(compiler, [(type, REG_XMM0), (TYP_LONG, REG_RDX)]);
            compiler.info.compIsVarArgs = true;
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHnd = (void*)0x1234;
            codeGen.IsFramePointerUsed = true;
            var zeroed = true;

            codeGen.genProfilingEnterCallback(REG_RBX, ref zeroed);

            var ids = Descriptors(codeGen);
            Assert.That(ids, Has.Count.EqualTo(6));
            Assert.That(ids[0].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(ids[1].idIns(), Is.EqualTo(INS_lea));
            Assert.That(ids[2].idIns(), Is.EqualTo(INS_call));
            AssertStack(ids[3], false, type == TYP_FLOAT ? INS_movss : INS_movsd_simd,
                width, 0, REG_XMM0);
            Assert.That(ids[4].idIns(), Is.EqualTo(INS_movd64));
            Assert.That(ids[4].idOpSize(), Is.EqualTo(width));
            Assert.That(ids[4].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(ids[4].idReg2(), Is.EqualTo(REG_XMM0));
            AssertStack(ids[5], false, INS_mov, EA_8BYTE, 1, REG_RDX);
            Assert.That(zeroed, Is.True);
        });
    }

    [TestCase(REG_RAX, false)]
    [TestCase(REG_RCX, false)]
    [TestCase(REG_RDX, false)]
    [TestCase(REG_R11, false)]
    [TestCase(REG_RBX, true)]
    [TestCase(REG_RSI, true)]
    public static void ScratchZeroStateReflectsProfilerCallClobbers(regNumber scratch, bool remainsZero)
    {
        WithEntry((compiler, codeGen) =>
        {
            ConfigureArguments(compiler, []);
            ProfilerHookNeeded(compiler) = true;
            compiler.compProfilerMethHnd = (void*)0x1234;
            codeGen.IsFramePointerUsed = true;
            var zeroed = true;

            codeGen.genProfilingEnterCallback(scratch, ref zeroed);

            Assert.That(zeroed, Is.EqualTo(remainsZero));
            Assert.That(Descriptors(codeGen).Last().idIns(), Is.EqualTo(INS_call));
        });
    }

    private static void ConfigureArguments(Compiler compiler, (var_types Type, regNumber Register)[] args)
    {
        compiler.info.compArgsCount = args.Length;
        compiler.lvaCount = args.Length + 1;
        compiler.lvaTable = new LclVarDsc[args.Length + 1];
        compiler.lvaParameterPassingInfo = new AbiPassingInformation[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            var (type, reg) = args[i];
            var descriptor = new LclVarDsc
            {
                Type = type,
                lvIsParam = true,
                lvOnFrame = true,
                lvFramePointerBased = true,
                RegNum = REG_STK,
            };
            if (type == TYP_STRUCT)
            {
                descriptor.Layout = new ClassLayout(8);
            }

            descriptor.StackOffset = 16 + (i * 8);
            compiler.lvaTable[i] = descriptor;

            var size = type == TYP_STRUCT ? 8 : type.Size;
            var segment = reg == REG_NA
                ? AbiPassingSegment.OnStack(i * 8, 0, size)
                : AbiPassingSegment.InRegister(reg, 0, size);
            compiler.lvaParameterPassingInfo[i] = AbiPassingInformation.FromSegment(compiler, false, segment);
        }

        compiler.lvaTable[args.Length] = new LclVarDsc
        {
            Type = TYP_STRUCT,
            Layout = new ClassLayout(32),
            lvOnFrame = true,
            StackOffset = 0,
            RegNum = REG_STK,
        };
        compiler.lvaOutgoingArgSpaceVar = args.Length;
        compiler.lvaOutgoingArgSpaceSize.Value = 32;
    }

    private static void WithEntry(Action<Compiler, CodeGen> action)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMatchedVM = true;
            compiler.lvaDoneFrameLayout = Compiler.FINAL_FRAME_LAYOUT;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            group.igFlags |= InsGroupFlags.Prolog;
            action(compiler, codeGen);
        });
    }

    private static void AssertStack(Emitter.instrDesc id, bool store, instruction ins,
        emitAttr size, int variable, regNumber reg)
    {
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(store ? Emitter.insFormat.IF_SWR_RRD : Emitter.insFormat.IF_RWR_SRD));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(variable));
        Assert.That(id.idReg1(), Is.EqualTo(reg));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        lookup->accessType = IAT_VALUE;
        lookup->addr = (void*)0x1234;
        return lookup->addr;
    }
}
