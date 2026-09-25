// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.Globals;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenJmpArgumentTests
{
    [TestCase(false)]
    [TestCase(true)]
    public static void ArgumentCyclesSpillBeforeReloadAndKeepAllocatedHomes(bool profile)
    {
        WithJmp((compiler, codeGen) =>
        {
            Configure(compiler, codeGen,
                [TYP_REF, TYP_BYREF, TYP_LONG, TYP_DOUBLE, TYP_INT],
                [REG_RDX, REG_RCX, REG_R10, REG_XMM2, REG_STK],
                [REG_RCX, REG_RDX, REG_NA, REG_XMM3, REG_NA]);
            if (profile)
            {
                ProfilerHookNeeded(compiler) = true;
                compiler.lvaOutgoingArgSpaceVar = 5;
                compiler.lvaOutgoingArgSpaceSize.Value = 32;
                compiler.compProfilerMethHnd = unchecked((void*)(nint)(-1));
                compiler.compProfilerMethHndIndirected = false;
            }
            var originalHomes = compiler.lvaTable.Take(5).Select(local => local.RegNum).ToArray();
            var originalLife = (nint[])compiler.compCurLife.Clone();

            codeGen.genJmpPlaceArgs(new GenTreeVal(GT_JMP, TYP_VOID, 0x1234));

            var descriptors = Descriptors(codeGen);
            var reloadIndex = profile ? 7 : 4;
            Assert.That(descriptors, Has.Count.EqualTo(reloadIndex + 3));
            AssertStack(descriptors[0], true, INS_mov, EA_8BYTE, 0, REG_RDX);
            AssertStack(descriptors[1], true, INS_mov, EA_8BYTE, 1, REG_RCX);
            AssertStack(descriptors[2], true, INS_mov, EA_8BYTE, 2, REG_R10);
            AssertStack(descriptors[3], true, INS_movsd_simd, EA_8BYTE, 3, REG_XMM2);
            if (profile)
            {
                Assert.That(descriptors[4].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[5].idIns(), Is.EqualTo(INS_lea));
                Assert.That(descriptors[6].idIns(), Is.EqualTo(INS_call));
                Assert.That(descriptors[6].idIsNoGC(), Is.True);
                var callbackRoots = EmitterCallInstructionTests.CallView.Variables(descriptors[6]);
                Assert.That(VarSetOps.IsMember(compiler, callbackRoots, 0), Is.True);
                Assert.That(VarSetOps.IsMember(compiler, callbackRoots, 1), Is.True);
                Assert.That(compiler.info.compProfilerCallback, Is.True);
            }
            AssertStack(descriptors[reloadIndex], false, INS_mov, EA_8BYTE, 0, REG_RCX);
            AssertStack(descriptors[reloadIndex + 1], false, INS_mov, EA_8BYTE, 1, REG_RDX);
            AssertStack(descriptors[reloadIndex + 2], false, INS_movsd_simd, EA_8BYTE, 3, REG_XMM3);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(REG_RCX) | Mask(REG_RDX) | Mask(REG_XMM3)));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(REG_RCX)));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(Mask(REG_RDX)));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.False);
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 1), Is.False);
            Assert.That(compiler.lvaTable.Take(5).Select(local => local.RegNum), Is.EqualTo(originalHomes));
            Assert.That(compiler.compCurLife, Is.EqualTo(originalLife));
        });
    }

    [TestCase(REG_STK, 1)]
    [TestCase(REG_RCX, 2)]
    [TestCase(REG_R8, 2)]
    public static void AlreadyCorrectAndSpilledHomesStillFollowNativeReloadProtocol(regNumber home, int count)
    {
        WithJmp((compiler, codeGen) =>
        {
            Configure(compiler, codeGen, [TYP_BYTE], [home], [REG_RCX]);

            codeGen.genJmpPlaceArgs(new GenTreeVal(GT_JMP, TYP_VOID, 0));

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(count));
            if (home != REG_STK)
            {
                AssertStack(descriptors[0], true, INS_mov, EA_4BYTE, 0, home);
            }
            AssertStack(descriptors[^1], false, INS_mov, EA_4BYTE, 0, REG_RCX);
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(home));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(REG_RCX)));
        });
    }

    [TestCase(TYP_FLOAT, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, EA_8BYTE)]
    public static void FixedFloatingVarargsPreserveNativeMoveWidthWithoutOpeningANoGcRegion(
        var_types type, emitAttr width)
    {
        WithJmp((compiler, codeGen) =>
        {
            Configure(compiler, codeGen, [type, type, type, type, TYP_INT],
                [REG_STK, REG_STK, REG_STK, REG_STK, REG_STK],
                [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3, REG_NA]);
            compiler.info.compIsVarArgs = true;
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            var oldFlags = group.igFlags;

            codeGen.genJmpPlaceVarArgs();

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(4));
            regNumber[] integers = [REG_RCX, REG_RDX, REG_R8, REG_R9];
            regNumber[] floats = [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3];
            for (var i = 0; i < 4; i++)
            {
                // Native deliberately selects MOVD64 from TYP_LONG even when the explicit size is four.
                Assert.That(descriptors[i].idIns(), Is.EqualTo(INS_movd64));
                Assert.That(descriptors[i].idOpSize(), Is.EqualTo(width));
                Assert.That(descriptors[i].idReg1(), Is.EqualTo(integers[i]));
                Assert.That(descriptors[i].idReg2(), Is.EqualTo(floats[i]));
            }
            Assert.That(codeGen.Emitter.emitCurIG, Is.SameAs(group));
            Assert.That(group.igFlags, Is.EqualTo(oldFlags));
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
            Assert.That(ForceNewGroup(codeGen.Emitter), Is.False);
        });
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public static void UnknownVarargsRestoreBothRegisterClassesFromCallerHomesInsideNoGc(
        bool framePointer, bool fixedArgs)
    {
        WithJmp((compiler, codeGen) =>
        {
            Configure(compiler, codeGen,
                fixedArgs ? [TYP_FLOAT, TYP_LONG] : [],
                fixedArgs ? [REG_STK, REG_STK] : [],
                fixedArgs ? [REG_XMM1, REG_R9] : []);
            compiler.info.compIsVarArgs = true;
            codeGen.IsFramePointerUsed = framePointer;
            codeGen.instGen(INS_nop);
            var before = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            var refs = codeGen.GCInfo.gcRegGCrefSetCur;
            var byrefs = codeGen.GCInfo.gcRegByrefSetCur;

            codeGen.genJmpPlaceVarArgs();

            var noGcGroup = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing NoGC group.");
            Assert.That(noGcGroup, Is.Not.SameAs(before));
            Assert.That(noGcGroup.igFlags & InsGroupFlags.NoGCInterrupt,
                Is.EqualTo(InsGroupFlags.NoGCInterrupt));
            var saved = before.igData ?? throw new AssertionException("Missing pre-NoGC instructions.");
            Assert.That(saved.Select(id => id.idIns()),
                Is.EqualTo(fixedArgs ? new[] { INS_nop, INS_movd64 } : [INS_nop]));
            regNumber[] integers = fixedArgs ? [REG_RCX, REG_R8] : [REG_RCX, REG_RDX, REG_R8, REG_R9];
            regNumber[] floats = fixedArgs ? [REG_XMM0, REG_XMM2] : [REG_XMM0, REG_XMM1, REG_XMM2, REG_XMM3];
            int[] homes = fixedArgs ? [0, 16] : [0, 8, 16, 24];
            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(integers.Length * 2));
            for (var i = 0; i < integers.Length; i++)
            {
                var load = descriptors[2 * i];
                Assert.That(load.idIns(), Is.EqualTo(INS_mov));
                Assert.That(load.idOpSize(), Is.EqualTo(EA_8BYTE));
                Assert.That(load.idReg1(), Is.EqualTo(integers[i]));
                Assert.That(load.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(framePointer ? REG_RBP : REG_RSP));
                Assert.That(load.idAddr().iiaAddrMode.amDisp, Is.EqualTo(homes[i] + (framePointer ? 16 : 88)));
                var copy = descriptors[(2 * i) + 1];
                Assert.That(copy.idIns(), Is.EqualTo(INS_movd64));
                Assert.That(copy.idOpSize(), Is.EqualTo(EA_8BYTE));
                Assert.That(copy.idReg1(), Is.EqualTo(floats[i]));
                Assert.That(copy.idReg2(), Is.EqualTo(integers[i]));
            }
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(refs));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(byrefs));
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
            Assert.That(ForceNewGroup(codeGen.Emitter), Is.True);

            codeGen.instGen(INS_nop);

            var after = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing post-NoGC group.");
            Assert.That(after, Is.Not.SameAs(noGcGroup));
            Assert.That(after.igFlags & InsGroupFlags.NoGCInterrupt,
                Is.EqualTo(InsGroupFlags.None));
        });
    }

    [Test]
    public static void JumpPlacementInvokesVarargsAfterReloadingFixedArguments()
    {
        WithJmp((compiler, codeGen) =>
        {
            Configure(compiler, codeGen, [TYP_DOUBLE], [REG_XMM2], [REG_XMM0]);
            compiler.info.compIsVarArgs = true;
            var before = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");

            codeGen.genJmpPlaceArgs(new GenTreeVal(GT_JMP, TYP_VOID, 0));

            var saved = before.igData ?? throw new AssertionException("Missing fixed argument group.");
            Assert.That(saved.Select(id => id.idIns()),
                Is.EqualTo([INS_movsd_simd, INS_movsd_simd, INS_movd64]));
            Assert.That(saved[0].idReg1(), Is.EqualTo(REG_XMM2));
            Assert.That(saved[1].idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(saved[2].idReg1(), Is.EqualTo(REG_RCX));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(6));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(REG_XMM0)));
        });
    }

    [TestCase(TYP_REF, REG_RCX, 0, 8, false, TYP_REF)]
    [TestCase(TYP_BYREF, REG_RCX, 0, 8, false, TYP_BYREF)]
    [TestCase(TYP_BYTE, REG_RCX, 0, 1, false, TYP_INT)]
    [TestCase(TYP_FLOAT, REG_XMM0, 0, 4, false, TYP_FLOAT)]
    [TestCase(TYP_STRUCT, REG_XMM0, 0, 8, false, TYP_DOUBLE)]
    [TestCase(TYP_STRUCT, REG_RCX, 0, 8, false, TYP_REF)]
    [TestCase(TYP_STRUCT, REG_RCX, 8, 8, false, TYP_BYREF)]
    [TestCase(TYP_STRUCT, REG_RCX, 16, 8, false, TYP_I_IMPL)]
    [TestCase(TYP_STRUCT, REG_RCX, 17, 1, false, TYP_INT)]
    [TestCase(TYP_STRUCT, REG_RCX, 17, 1, true, TYP_UBYTE)]
    public static void StackTypeSelectionRetainsGcSlotsRegisterClassesAndRounding(
        var_types type, regNumber reg, int offset, int size, bool swift, var_types expected)
    {
        WithJmp((compiler, codeGen) =>
        {
            var builder = new ClassLayoutBuilder(compiler, 24);
            builder.SetGCPtrType(0, TYP_REF);
            builder.SetGCPtrType(1, TYP_BYREF);
            var descriptor = new LclVarDsc { Type = type };
            if (type == TYP_STRUCT)
            {
                descriptor.Layout = ClassLayout.Create(compiler, builder);
            }
            if (swift)
            {
                compiler.info.compCallConv = CorInfoCallConvExtension.Swift;
            }
            var segment = AbiPassingSegment.InRegister(reg, offset, size);

            Assert.That(codeGen.genParamStackType(in descriptor, in segment), Is.EqualTo(expected));
        });
    }

#if DEBUG
    [TestCase(false)]
    [TestCase(true)]
    public static void D005RejectsBeforeSpillsProfilerGcAndGroupMutation(bool varargsEntry)
    {
        WithJmp((compiler, codeGen) =>
        {
            Configure(compiler, codeGen, [TYP_REF], [REG_RDX], [REG_RCX]);
            compiler.info.compIsVarArgs = true;
            var jump = new GenTreeVal(GT_JMP, TYP_VOID, 0);
            var group = codeGen.Emitter.emitCurIG ?? throw new AssertionException("Missing instruction group.");
            var mask = codeGen.RegSet.GetMaskVars();
            var stackRoots = (nint[])codeGen.GCInfo.gcVarPtrSetCur.Clone();
            compiler.opts.dspCode = true;

            void Generate()
            {
                if (varargsEntry)
                {
                    codeGen.genJmpPlaceVarArgs();
                }
                else
                {
                    codeGen.genJmpPlaceArgs(jump);
                }
            }

            var exception = Assert.Throws<FatalJitException>(Generate);
            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(codeGen.Emitter.emitCurIG, Is.SameAs(group));
            Assert.That(NoGcRequests(codeGen.Emitter), Is.Zero);
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(mask));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(Mask(REG_RDX)));
            Assert.That(codeGen.GCInfo.gcVarPtrSetCur, Is.EqualTo(stackRoots));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_RDX));
            Assert.That(compiler.info.compProfilerCallback, Is.False);

            compiler.opts.dspCode = false;
            Generate();
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(6));
        });
    }
#endif

    private static void Configure(Compiler compiler, CodeGen codeGen,
        var_types[] types, regNumber[] homes, regNumber[] abiRegisters)
    {
        var count = types.Length;
        compiler.info.compArgsCount = count;
        compiler.lvaCount = count + 1;
        compiler.lvaTrackedCount = count;
        compiler.lvaTrackedCountInSizeTUnits = 1;
        compiler.lvaTrackedToVarNum = [.. Enumerable.Range(0, count)];
        compiler.lvaTable = new LclVarDsc[count + 1];
        compiler.lvaParameterPassingInfo = new AbiPassingInformation[count];
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcVarPtrSetCur = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeEmpty(compiler);
        codeGen.GCInfo.gcRegGCrefSetCur = default;
        codeGen.GCInfo.gcRegByrefSetCur = default;
        codeGen.RegSet.ClearMaskVars();

        for (var i = 0; i < count; i++)
        {
            compiler.lvaTable[i] = new LclVarDsc
            {
                Type = types[i],
                RegNum = homes[i],
                lvIsParam = true,
                lvIsRegArg = abiRegisters[i] != REG_NA,
                lvTracked = true,
                lvOnFrame = true,
                lvFramePointerBased = true,
                StackOffset = 16 + (i * 8),
                _varIndex = checked((ushort)i),
            };
            var segment = abiRegisters[i] == REG_NA
                ? AbiPassingSegment.OnStack(i * 8, 0, types[i].Size)
                : AbiPassingSegment.InRegister(abiRegisters[i], 0, types[i].Size);
            compiler.lvaParameterPassingInfo[i] = AbiPassingInformation.FromSegment(compiler, false, segment);
            VarSetOps.AddElemD(compiler, compiler.compCurLife, i);
            if (homes[i] != REG_STK)
            {
                codeGen.RegSet.AddMaskVars(Mask(homes[i]));
                codeGen.GCInfo.gcMarkRegPtrVal(homes[i], types[i]);
            }
            else if (compiler.lvaIsGCTracked(in compiler.lvaTable[i]))
            {
                VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcVarPtrSetCur, i);
            }
        }
        compiler.lvaTable[count] = new LclVarDsc
        {
            Type = TYP_STRUCT, Layout = new ClassLayout(32), lvOnFrame = true,
            lvFramePointerBased = false, StackOffset = 0, RegNum = REG_STK,
        };
    }

    private static void WithJmp(Action<Compiler, CodeGen> action)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            compiler.compJmpOpUsed = true;
            compiler.compCalleeRegsPushed = 2;
            compiler.compLclFrameSize = 64;
            compiler.compHndBBtab = [];
            _ = compiler.fgCreateFunclets();
            action(compiler, codeGen);
        });
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static void AssertStack(Emitter.instrDesc id, bool store, instruction ins,
        emitAttr size, int variable, regNumber reg)
    {
        Assert.That(id.idIns(), Is.EqualTo(ins));
        Assert.That(id.idInsFmt(), Is.EqualTo(store ? Emitter.insFormat.IF_SWR_RRD : Emitter.insFormat.IF_RWR_SRD));
        Assert.That(id.idOpSize(), Is.EqualTo(size));
        Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.EqualTo(variable));
        Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.Zero);
        Assert.That(id.idReg1(), Is.EqualTo(reg));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "compProfilerHookNeeded")]
    private static extern ref bool ProfilerHookNeeded(Compiler compiler);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitNoGCRequestCount")]
    private static extern ref int NoGcRequests(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitForceNewIG")]
    private static extern ref bool ForceNewGroup(Emitter emitter);
}
