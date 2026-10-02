// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if FEATURE_HW_INTRINSICS && TARGET_ARM64
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.insCond;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class Arm64HardwareIntrinsicCodegenTests
{
    private static readonly List<string?> s_assertions = [];

    [TestCase(TYP_BYTE, EA_8BYTE, INS_OPTS_8B)]
    [TestCase(TYP_UBYTE, EA_8BYTE, INS_OPTS_8B)]
    [TestCase(TYP_SHORT, EA_8BYTE, INS_OPTS_4H)]
    [TestCase(TYP_USHORT, EA_8BYTE, INS_OPTS_4H)]
    [TestCase(TYP_INT, EA_8BYTE, INS_OPTS_2S)]
    [TestCase(TYP_UINT, EA_8BYTE, INS_OPTS_2S)]
    [TestCase(TYP_FLOAT, EA_8BYTE, INS_OPTS_2S)]
    [TestCase(TYP_LONG, EA_8BYTE, INS_OPTS_1D)]
    [TestCase(TYP_ULONG, EA_8BYTE, INS_OPTS_1D)]
    [TestCase(TYP_DOUBLE, EA_8BYTE, INS_OPTS_1D)]
    [TestCase(TYP_BYTE, EA_16BYTE, INS_OPTS_16B)]
    [TestCase(TYP_UBYTE, EA_16BYTE, INS_OPTS_16B)]
    [TestCase(TYP_SHORT, EA_16BYTE, INS_OPTS_8H)]
    [TestCase(TYP_USHORT, EA_16BYTE, INS_OPTS_8H)]
    [TestCase(TYP_INT, EA_16BYTE, INS_OPTS_4S)]
    [TestCase(TYP_UINT, EA_16BYTE, INS_OPTS_4S)]
    [TestCase(TYP_FLOAT, EA_16BYTE, INS_OPTS_4S)]
    [TestCase(TYP_LONG, EA_16BYTE, INS_OPTS_2D)]
    [TestCase(TYP_ULONG, EA_16BYTE, INS_OPTS_2D)]
    [TestCase(TYP_DOUBLE, EA_16BYTE, INS_OPTS_2D)]
    public static void FixedSimdOptionsPreserveNativeLaneCount(var_types type, emitAttr size, insOpts expected)
    {
        Assert.That(SimdOptions(null, size, type), Is.EqualTo(expected));
    }

    [TestCase(8, TYP_BYTE, INS_add, INS_OPTS_8B)]
    [TestCase(16, TYP_BYTE, INS_add, INS_OPTS_16B)]
    [TestCase(8, TYP_USHORT, INS_add, INS_OPTS_4H)]
    [TestCase(16, TYP_USHORT, INS_add, INS_OPTS_8H)]
    [TestCase(8, TYP_INT, INS_add, INS_OPTS_2S)]
    [TestCase(16, TYP_INT, INS_add, INS_OPTS_4S)]
    [TestCase(16, TYP_LONG, INS_add, INS_OPTS_2D)]
    [TestCase(8, TYP_FLOAT, INS_fadd, INS_OPTS_2S)]
    [TestCase(16, TYP_FLOAT, INS_fadd, INS_OPTS_4S)]
    public static void TableDrivenBinaryRetainsRegistersWidthAndOptions(
        int size, var_types baseType, instruction ins, insOpts opt)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var type = Compiler.GetSimdTypeForSize(size);
            var node = new GenTreeHWIntrinsic(type, NI_AdvSimd_Add, baseType, (byte)size,
                Operand(REG_V1, type), Operand(REG_V2, type)) { RegNum = REG_V0 };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var id = SingleDescriptor(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idOpSize(), Is.EqualTo(type.EmitActualSize));
            Assert.That(id.idInsOpt(), Is.EqualTo(opt));
            Assert.That(id.idReg1(), Is.EqualTo(REG_V0));
            Assert.That(id.idReg2(), Is.EqualTo(REG_V1));
            Assert.That(id.idReg3(), Is.EqualTo(REG_V2));
        });
    }

    [TestCase(REG_V1, INS_bsl, REG_V2, REG_V3, 1)]
    [TestCase(REG_V2, INS_bif, REG_V3, REG_V1, 1)]
    [TestCase(REG_V3, INS_bit, REG_V2, REG_V1, 1)]
    [TestCase(REG_V0, INS_bsl, REG_V2, REG_V3, 2)]
    public static void BitwiseSelectChoosesAllocatedDestructiveOperand(
        regNumber target, instruction ins, regNumber source1, regNumber source2, int count)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AdvSimd_BitwiseSelect, TYP_INT, 16,
                Operand(REG_V1, TYP_SIMD16), Operand(REG_V2, TYP_SIMD16), Operand(REG_V3, TYP_SIMD16)) {
                RegNum = target
            };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(count));
            if (count == 2)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(target));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_V1));
            }
            var id = descriptors[^1];
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idReg1(), Is.EqualTo(target));
            Assert.That(id.idReg2(), Is.EqualTo(source1));
            Assert.That(id.idReg3(), Is.EqualTo(source2));
        });
    }

    [TestCase(NI_ArmBase_Arm64_MultiplyLongAdd, TYP_LONG, INS_smaddl)]
    [TestCase(NI_ArmBase_Arm64_MultiplyLongAdd, TYP_ULONG, INS_umaddl)]
    [TestCase(NI_ArmBase_Arm64_MultiplyLongSub, TYP_LONG, INS_smsubl)]
    [TestCase(NI_ArmBase_Arm64_MultiplyLongSub, TYP_ULONG, INS_umsubl)]
    public static void ScalarMultiplyLongSelectsSignedOpcode(
        NamedIntrinsic intrinsic, var_types baseType, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(baseType, intrinsic, baseType, 0,
                Operand(REG_R1, TYP_INT), Operand(REG_R2, TYP_INT), Operand(REG_R3, baseType)) {
                RegNum = REG_R0
            };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var id = SingleDescriptor(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(id.idReg1(), Is.EqualTo(REG_R0));
            Assert.That(id.idReg2(), Is.EqualTo(REG_R1));
            Assert.That(id.idReg3(), Is.EqualTo(REG_R2));
            Assert.That(id.idReg4(), Is.EqualTo(REG_R3));
        });
    }

    [TestCase(NI_Fp16_CompareEqual, INS_COND_EQ)]
    [TestCase(NI_Fp16_CompareGreaterThan, INS_COND_GT)]
    [TestCase(NI_Fp16_CompareGreaterThanOrEqual, INS_COND_GE)]
    [TestCase(NI_Fp16_CompareLessThan, INS_COND_MI)]
    [TestCase(NI_Fp16_CompareLessThanOrEqual, INS_COND_LS)]
    [TestCase(NI_Fp16_CompareNotEqual, INS_COND_NE)]
    public static void HalfComparisonRetainsUnorderedSensitiveCondition(NamedIntrinsic intrinsic, insCond cond)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_INT, intrinsic, TYP_USHORT, 16,
                Operand(REG_V1, TYP_SIMD16), Operand(REG_V2, TYP_SIMD16)) { RegNum = REG_R0 };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_fcmp));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_2BYTE));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_V1));
            Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_V2));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_cset));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(EA_4BYTE));
            Assert.That(descriptors[1].idSmallCns() & 0xF, Is.EqualTo((int)cond));
        });
    }

    [TestCase(NI_AdvSimd_CompareLessThan, TYP_INT, INS_cmgt)]
    [TestCase(NI_AdvSimd_CompareLessThanOrEqual, TYP_INT, INS_cmge)]
    [TestCase(NI_AdvSimd_CompareLessThan, TYP_FLOAT, INS_fcmgt)]
    [TestCase(NI_AdvSimd_CompareLessThanOrEqual, TYP_FLOAT, INS_fcmge)]
    public static void LessThanReversesSources(NamedIntrinsic intrinsic, var_types baseType, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD16, intrinsic, baseType, 16,
                Operand(REG_V1, TYP_SIMD16), Operand(REG_V2, TYP_SIMD16)) { RegNum = REG_V0 };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var id = SingleDescriptor(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idReg2(), Is.EqualTo(REG_V2));
            Assert.That(id.idReg3(), Is.EqualTo(REG_V1));
        });
    }

    [TestCase(-1)]
    [TestCase(4)]
    public static void InvalidVectorElementIndexLeavesEmissionToPriorThrowingRangeCheck(int index)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var immediate = Immediate(index);
            var node = new GenTreeHWIntrinsic(TYP_INT, NI_Vector_GetElement, TYP_INT, 16,
                Operand(REG_V1, TYP_SIMD16), immediate) { RegNum = REG_R0 };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    [TestCase(NI_Sve_TransposeEven, INS_sve_trn1)]
    [TestCase(NI_Sve_UnzipOdd, INS_sve_uzp2)]
    [TestCase(NI_Sve_ZipHigh, INS_sve_zip2)]
    public static void ScalablePermutationUsesUnpredicatedRegisters(NamedIntrinsic intrinsic, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_SIMD, intrinsic, TYP_INT, 0,
                Operand(REG_V1, TYP_SIMD), Operand(REG_V2, TYP_SIMD)) { RegNum = REG_V0 };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var id = SingleDescriptor(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_SCALABLE));
            Assert.That(id.idInsOpt(), Is.EqualTo(INS_OPTS_SCALABLE_S));
            Assert.That(id.idReg2(), Is.EqualTo(REG_V1));
            Assert.That(id.idReg3(), Is.EqualTo(REG_V2));
        });
    }

    [TestCase(TYP_LONG, INS_sve_whilelt)]
    [TestCase(TYP_ULONG, INS_sve_whilelo)]
    public static void WhileMaskUsesAuxiliaryScalarWidthAndSignedness(var_types auxiliaryType, instruction expected)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var node = new GenTreeHWIntrinsic(TYP_MASK, NI_Sve_CreateWhileLessThanMaskInt32, TYP_INT, 0,
                Operand(REG_R1, auxiliaryType), Operand(REG_R2, auxiliaryType)) {
                RegNum = REG_P0,
                AuxiliaryType = auxiliaryType
            };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var id = SingleDescriptor(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(expected));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(id.idInsOpt(), Is.EqualTo(INS_OPTS_SCALABLE_S));
            Assert.That(id.idReg1(), Is.EqualTo(REG_P0));
        });
    }

    [TestCase(REG_V0, REG_V0, 1, INS_sve_abs)]
    [TestCase(REG_V1, REG_V0, 2, INS_sve_sel)]
    [TestCase(REG_V0, REG_V8, 2, INS_sve_abs)]
    public static void EmbeddedUnaryPreservesInactiveLanesAndSourceOverlap(
        regNumber target, regNumber falseReg, int count, instruction finalIns)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var embedded = new GenTreeHWIntrinsic(TYP_SIMD, NI_Sve_Abs, TYP_INT, 0,
                Operand(REG_V1, TYP_SIMD));
            embedded.Flags |= GTF_CONTAINED | GTF_HW_EM_OP;
            var node = new GenTreeHWIntrinsic(TYP_SIMD, NI_Sve_ConditionalSelect, TYP_INT, 0,
                Operand(REG_P1, TYP_MASK), embedded, Operand(falseReg, TYP_SIMD)) { RegNum = target };
            EnableIsa(compiler, node);
            codeGen.genHWIntrinsic(node);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(count));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(finalIns));
            Assert.That(descriptors[^1].idReg1(), Is.EqualTo(target));
            if (target != falseReg && target != REG_V1)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_sve_movprfx));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(falseReg));
            }
        });
    }

    [TestCase(REG_V1)]
    [TestCase(REG_V2)]
    [TestCase(REG_V3)]
    public static void EmbeddedFmaReachesGenuineFiveRegisterBoundaryForEachDestructiveChoice(regNumber target)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var embedded = new GenTreeHWIntrinsic(TYP_SIMD, NI_Sve_FusedMultiplyAdd, TYP_FLOAT, 0,
                Operand(REG_V1, TYP_SIMD), Operand(REG_V2, TYP_SIMD), Operand(REG_V3, TYP_SIMD));
            embedded.Flags |= GTF_CONTAINED | GTF_HW_EM_OP;
            var node = new GenTreeHWIntrinsic(TYP_SIMD, NI_Sve_ConditionalSelect, TYP_FLOAT, 0,
                Operand(REG_P1, TYP_MASK), embedded, Operand(target, TYP_SIMD)) { RegNum = target };
            EnableIsa(compiler, node);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genHWIntrinsic(node)) ??
                throw new AssertionException("Missing five-register dependency failure.");
            Assert.That(failure.Message, Is.EqualTo("ARM64 emitInsSve_R_R_R_R_R recording is not ported."));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }

    internal static GenTreePhysReg Operand(regNumber reg, var_types type)
    {
        return new GenTreePhysReg(reg, type) { RegNum = reg };
    }

    internal static GenTreeIntCon Immediate(nint value)
    {
        return new GenTreeIntCon(TYP_INT, value) { Flags = GTF_CONTAINED, RegNum = REG_NA };
    }

    internal static void EnableIsa(Compiler compiler, GenTreeHWIntrinsic node)
    {
        var isa = HWIntrinsicInfo.lookupIsa(node.HWIntrinsicId);
        compiler.opts.compSupportsISAReported.AddInstructionSet(isa);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(isa);
        compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Vector64);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Vector64);
        compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_Vector128);
        compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_Vector128);
        compiler.opts.setSupportedISAs(compiler.opts.compSupportsISAExactly);
    }

    internal static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
#if DEBUG
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = new() { doAssert = &RecordAssertion };
        ICorJitInfo ee = new() { lpVtbl = &vtable };
        using var tls = new JitTls(&ee);
        s_assertions.Clear();
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.opts.SetMinOpts(true);
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
            LifeUpdater(codeGen) = new TreeLifeUpdater(compiler, forCodeGen: true);
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.Init();
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , true
#endif
                );
            action(compiler, codeGen);
#if DEBUG
            Assert.That(s_assertions, Is.Empty, "Unexpected JIT assertions: " + string.Join(" | ", s_assertions));
#endif
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static int RecordAssertion(ICorJitInfo* self, byte* file, int line, byte* expression)
    {
        s_assertions.Add(Marshal.PtrToStringUTF8((nint)expression));
        return 0;
    }

    internal static List<Emitter.instrDesc> Descriptors(CodeGen codeGen)
    {
        return CurrentDescriptors(codeGen.Emitter) ?? throw new AssertionException("Missing descriptor buffer.");
    }

    internal static Emitter.instrDesc SingleDescriptor(CodeGen codeGen)
    {
        var descriptors = Descriptors(codeGen);
        Assert.That(descriptors, Has.Count.EqualTo(1));

        return descriptors[0];
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "treeLifeUpdater")]
    private static extern ref TreeLifeUpdater? LifeUpdater(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "genGetSimdInsOpt")]
    private static extern insOpts SimdOptions(CodeGen? codeGen, emitAttr size, var_types type);
}
#endif
