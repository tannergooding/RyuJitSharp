// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenUpperLaneTests
{
    [Test]
    public static void RegisterSavesExtractTheUpperHalfAndProduceOnlyTheSaveNode(
        [Values(false, true)] bool restore, [Values(false, true)] bool evex,
        [Values(false, true)] bool lastUse)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var local = ConfigureLocal(compiler, codeGen, TYP_SIMD32, evex);
            if (lastUse)
            {
                local.Flags |= GTF_VAR_DEATH;
            }
            var node = new GenTreeIntrinsic(restore ? TYP_SIMD32 : TYP_SIMD16, local,
                restore ? NI_SIMD_UpperRestore : NI_SIMD_UpperSave, null)
            {
                RegNum = REG_XMM7,
            };

            Emit(codeGen, node, restore);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            var id = descriptors[0];
            Assert.That(id.idIns(), Is.EqualTo(restore ? INS_vinsertf32x4 : INS_vextractf32x4));
            Assert.That(id.idInsFmt(), Is.EqualTo(restore ? IF_RWR_RRD_RRD_CNS : IF_RWR_RRD_CNS));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_32BYTE));
            Assert.That(id.idReg1(), Is.EqualTo(restore ? REG_XMM6 : REG_XMM7));
            Assert.That(id.idReg2(), Is.EqualTo(REG_XMM6));
            if (restore)
            {
                Assert.That(id.idReg3(), Is.EqualTo(REG_XMM7));
            }
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)1));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_XMM6));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.EqualTo(!lastUse));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(lastUse ? default : Mask(REG_XMM6)));
            AssertLifetimeMarkers(local, node, produced: !restore);
        });
    }

    [TestCase(TYP_SIMD32, false, INS_vextractf32x4, IF_SWR_RRD_CNS, 16u)]
    [TestCase(TYP_SIMD32, true, INS_vinsertf32x4, IF_RWR_RRD_SRD_CNS, 16u)]
    [TestCase(TYP_SIMD64, false, INS_movups, IF_SWR_RRD, 0u)]
    [TestCase(TYP_SIMD64, true, INS_movups, IF_RWR_SRD, 0u)]
    public static void StackHomesUseUpperSixteenBytesForYmmAndTheWholeZmm(
        var_types type, bool restore, instruction ins, Emitter.insFormat format, uint offset)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var local = ConfigureLocal(compiler, codeGen, type, evex: type == TYP_SIMD64);
            var node = new GenTreeIntrinsic(restore ? type : TYP_SIMD16, local,
                restore ? NI_SIMD_UpperRestore : NI_SIMD_UpperSave, null)
            {
                RegNum = REG_NA,
                Flags = restore ? GTF_NOREG_AT_USE : GTF_SPILL,
            };

            Emit(codeGen, node, restore);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            var id = descriptors[0];
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idOpSize(), Is.EqualTo(type.EmitSize));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM6));
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(offset));
            Assert.That(id.idCodeSize(), Is.GreaterThan(0u));
            if (type == TYP_SIMD32)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)1));
                if (restore)
                {
                    Assert.That(id.idReg2(), Is.EqualTo(REG_XMM6));
                }
            }
            Assert.That(node.Flags & (GTF_SPILL | GTF_NOREG_AT_USE),
                Is.EqualTo(restore ? GTF_NOREG_AT_USE : GTF_SPILL));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_XMM6));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(REG_XMM6)));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.True);
            AssertLifetimeMarkers(local, node, produced: false);
        });
    }

    [Test]
    public static void OperandConsumptionCopiesTheLocalHomeBeforeAccessingItsUpperLane(
        [Values(false, true)] bool restore)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var local = ConfigureLocal(compiler, codeGen, TYP_SIMD32, evex: false);
            local.RegNum = REG_XMM8;
            var node = new GenTreeIntrinsic(restore ? TYP_SIMD32 : TYP_SIMD16, local,
                restore ? NI_SIMD_UpperRestore : NI_SIMD_UpperSave, null)
            {
                RegNum = REG_XMM7,
            };

            Emit(codeGen, node, restore);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movaps));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_32BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_XMM8));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_XMM6));
            Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_XMM8));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_XMM6));
        });
    }

    [Test]
    public static void SpilledLocalReloadPrecedesUpperLaneAccess(
        [Values(false, true)] bool restore)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var local = ConfigureLocal(compiler, codeGen, TYP_SIMD32, evex: false);
            compiler.lvaTable[0].RegNum = REG_STK;
            codeGen.RegSet.ClearMaskVars();
            local.Flags |= GTF_SPILLED;
            var node = new GenTreeIntrinsic(restore ? TYP_SIMD32 : TYP_SIMD16, local,
                restore ? NI_SIMD_UpperRestore : NI_SIMD_UpperSave, null)
            {
                RegNum = REG_XMM7,
            };

            Emit(codeGen, node, restore);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idInsFmt(), Is.EqualTo(IF_RWR_SRD));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_32BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_XMM6));
            Assert.That(descriptors[0].idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(descriptors[1].idIns(), Is.EqualTo(restore ? INS_vinsertf32x4 : INS_vextractf32x4));
            Assert.That(local.Flags & GTF_SPILLED, Is.EqualTo(GTF_EMPTY));
            Assert.That(compiler.lvaTable[0].RegNum, Is.EqualTo(REG_XMM6));
            Assert.That(codeGen.RegSet.GetMaskVars(), Is.EqualTo(Mask(REG_XMM6)));
            AssertLifetimeMarkers(local, node, produced: !restore);
        });
    }

#if DEBUG
    [Test]
    public static void DisassemblyRecordsUpperLaneTransfersAndLifetimeChanges(
        [Values(false, true)] bool restore, [Values(false, true)] bool memory)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var local = ConfigureLocal(compiler, codeGen, TYP_SIMD32, evex: false);
            compiler.lvaTable[0].RegNum = REG_STK;
            codeGen.RegSet.ClearMaskVars();
            local.Flags |= GTF_SPILLED | GTF_VAR_DEATH;
            var node = new GenTreeIntrinsic(restore ? TYP_SIMD32 : TYP_SIMD16, local,
                restore ? NI_SIMD_UpperRestore : NI_SIMD_UpperSave, null)
            {
                RegNum = memory ? REG_NA : REG_XMM7,
            };
            compiler.opts.dspCode = true;

            var diagnostic = InstructionRecordingTestSupport.Capture(() => Emit(codeGen, node, restore));

            Assert.That(Descriptors(codeGen), Is.Not.Empty);
            Assert.That(diagnostic, Is.Not.Empty);
            Assert.That(local._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED));
        });
    }
#endif

    private static GenTreeLclVar ConfigureLocal(Compiler compiler, CodeGen codeGen, var_types type, bool evex)
    {
        EnableAvx2(compiler);
        codeGen.Emitter.UseVexEncodings = true;
        codeGen.Emitter.UseEvexEncodings = evex;
        if (evex)
        {
            compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
            compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
        }

        ref var local = ref compiler.lvaTable[0];
        local.Type = type;
        local.lvLRACandidate = true;
        local.RegNum = REG_XMM6;
        local.StackOffset = -128;
        local.lvOnFrame = true;
        codeGen.RegSet.SetMaskVars(Mask(REG_XMM6));
        VarSetOps.AddElemD(compiler, compiler.compCurLife, 0);

        return new GenTreeLclVar(type, 0) { RegNum = REG_XMM6 };
    }

    private static void Emit(CodeGen codeGen, GenTreeIntrinsic node, bool restore)
    {
        if (restore)
        {
            codeGen.genSimdUpperRestore(node);
        }
        else
        {
            codeGen.genSimdUpperSave(node);
        }
    }

    private static regMaskTP Mask(regNumber reg) => regMaskTP.CreateFromRegNum(reg, reg.SingleTypeMask);

    private static void AssertLifetimeMarkers(GenTree local, GenTree node, bool produced)
    {
#if DEBUG
        Assert.That(local._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
            Is.Not.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
        Assert.That((node._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_PRODUCED) != 0, Is.EqualTo(produced));
#else
        Assert.That(local.RegNum, Is.Not.EqualTo(REG_NA));
        if (produced)
        {
            Assert.That(node.RegNum, Is.Not.EqualTo(REG_NA));
        }
#endif
    }
}
