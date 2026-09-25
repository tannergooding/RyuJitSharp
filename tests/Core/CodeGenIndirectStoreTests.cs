// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoHelpFunc;
using static RyuJitSharp.CORINFO_InstructionSet;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.Globals;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.RmwStatus;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;

namespace RyuJitSharp.UnitTests;

internal static unsafe class CodeGenIndirectStoreTests
{
    private static CorInfoHelpFunc s_helper;

    [TestCase(TYP_BYTE, INS_mov, EA_1BYTE)]
    [TestCase(TYP_SHORT, INS_mov, EA_2BYTE)]
    [TestCase(TYP_INT, INS_mov, EA_4BYTE)]
    [TestCase(TYP_LONG, INS_mov, EA_8BYTE)]
    [TestCase(TYP_FLOAT, INS_movss, EA_4BYTE)]
    [TestCase(TYP_DOUBLE, INS_movsd_simd, EA_8BYTE)]
    [TestCase(TYP_BYREF, INS_mov, EA_8BYTE)]
    public static void ScalarStoresRetainMemoryWidthAndConsumeOperands(
        var_types type, instruction ins, emitAttr size)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var address = Register(compiler, TYP_BYREF, REG_RAX);
            var data = Register(compiler, type.ActualType, varTypeIsFloating(type) ? REG_XMM1 : REG_RDX);
            var store = new GenTreeStoreInd(type, address, data);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RBX, TYP_REF);

            codeGen.genCodeForStoreInd(store);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(1));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(ins));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(size));
            Assert.That(descriptors[0].idReg1(), Is.EqualTo(data.RegNum));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(RBM_NONE));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(RBM_RBX));
#if DEBUG
            Assert.That(address._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.Not.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
            Assert.That(data._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.Not.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
#endif
        });
    }

    [Test]
    public static void GcStoresSelectCheckedOrUncheckedHelpersAndCopyArgumentsWithoutInterference(
        [Values(false, true)] bool knownHeap, [Values(false, true)] bool alreadyInArguments)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.getHelperFtn = &GetHelperFtn;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMatchedVM = true;
            s_helper = CORINFO_HELP_UNDEF;
            var address = Register(compiler, TYP_BYREF, alreadyInArguments ? REG_WRITE_BARRIER_DST : REG_RDX);
            var data = Register(compiler, TYP_REF, alreadyInArguments ? REG_WRITE_BARRIER_SRC : REG_R8);
            var store = new GenTreeStoreInd(TYP_REF, address, data)
            {
                Flags = knownHeap ? GTF_IND_TGT_HEAP : GTF_EMPTY,
            };

            codeGen.genCodeForStoreInd(store);

            var descriptors = Descriptors(codeGen);
            Assert.That(s_helper, Is.EqualTo(knownHeap ? CORINFO_HELP_ASSIGN_REF : CORINFO_HELP_CHECKED_ASSIGN_REF));
            Assert.That(descriptors, Has.Count.EqualTo(alreadyInArguments ? 1 : 3));
            Assert.That(descriptors[^1].idIns(), Is.EqualTo(INS_call));
            Assert.That(descriptors[^1].idIsNoGC(), Is.True);
            if (!alreadyInArguments)
            {
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_WRITE_BARRIER_DST));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(REG_RDX));
                Assert.That(descriptors[1].idReg1(), Is.EqualTo(REG_WRITE_BARRIER_SRC));
                Assert.That(descriptors[1].idReg2(), Is.EqualTo(REG_R8));
            }
#if DEBUG
            Assert.That(codeGen.genWriteBarrierUsed, Is.True);
#endif
        });
    }

    [Test]
    public static void NullAndKnownNonHeapStoresDoNotCallWriteBarriers([Values(false, true)] bool nullValue)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var data = compiler.gtNewIconNode(TYP_REF, nullValue ? 0 : 7);
            data.RegNum = nullValue ? REG_NA : REG_RDX;
            data.IsContained = nullValue;
            var store = new GenTreeStoreInd(TYP_REF, Register(compiler, TYP_BYREF, REG_RAX), data)
            {
                Flags = nullValue ? GTF_IND_TGT_HEAP : GTF_IND_TGT_NOT_HEAP,
            };

            codeGen.genCodeForStoreInd(store);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(INS_mov));
#if DEBUG
            Assert.That(codeGen.genWriteBarrierUsed, Is.False);
#endif
        });
    }

    [TestCase(GT_NEG, 0, false, INS_neg)]
    [TestCase(GT_NOT, 0, false, INS_not)]
    [TestCase(GT_ADD, 1, false, INS_inc)]
    [TestCase(GT_ADD, -1, true, INS_dec)]
    [TestCase(GT_ADD, 9, true, INS_add)]
    [TestCase(GT_SUB, 9, false, INS_sub)]
    [TestCase(GT_AND, 127, false, INS_and)]
    [TestCase(GT_OR, 127, true, INS_or)]
    [TestCase(GT_XOR, 127, false, INS_xor)]
    [TestCase(GT_LSH, 1, false, INS_shl_1)]
    [TestCase(GT_RSH, 4, false, INS_sar_N)]
    [TestCase(GT_RSZ, 4, false, INS_shr_N)]
    [TestCase(GT_ROL, 4, false, INS_rol_N)]
    [TestCase(GT_ROR, 4, false, INS_ror_N)]
    public static void ReadModifyWriteStoresPreserveOperatorAndDestinationOperand(
        genTreeOps oper, int value, bool secondOperand, instruction ins)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.compDbgCode = true;
            var address = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0) { IsContained = true };
            var readAddress = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0) { IsContained = true };
            var read = new GenTreeIndir(GT_IND, TYP_INT, readAddress) { IsContained = true };
            var source = compiler.gtNewIconNode(TYP_INT, value);
            source.IsContained = true;
            GenTree operation = oper.IsUnary
                ? new GenTreeUnOp(oper, TYP_INT, read)
                : new GenTreeOp(oper, TYP_INT, secondOperand ? source : read, secondOperand ? read : source);
            operation.IsContained = true;
            var store = new GenTreeStoreInd(TYP_INT, address, operation)
            {
                RmwStatus = secondOperand ? STOREIND_RMW_DST_IS_OP2 : STOREIND_RMW_DST_IS_OP1,
            };

            codeGen.genCodeForStoreInd(store);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(ins));
            Assert.That(Descriptors(codeGen)[0].idAddr().iiaLclVar.lvaOffset(), Is.Zero);
        });
    }

    [Test]
    public static void ContainedByteSwapsSelectApxForExtendedDataOrAddressRegisters(
        [Values(0, 1, 2, 3)] int extendedOperand, [Values(false, true)] bool shortStore)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            codeGen.Emitter.UseRex2Encodings = true;
            codeGen.Emitter.UsePromotedEvexEncodings = true;
            var data = new GenTreeUnOp(shortStore ? GT_BSWAP16 : GT_BSWAP, TYP_INT,
                Register(compiler, TYP_INT, extendedOperand == 1 ? REG_R20 : REG_RDX))
            {
                IsContained = true,
            };
            GenTree address = extendedOperand == 3
                ? new GenTreeAddrMode(TYP_BYREF, Register(compiler, TYP_BYREF, REG_RAX),
                    Register(compiler, TYP_I_IMPL, REG_R20), 4, 0) { IsContained = true }
                : Register(compiler, TYP_BYREF, extendedOperand == 2 ? REG_R21 : REG_RAX);
            var store = new GenTreeStoreInd(shortStore ? TYP_SHORT : TYP_INT, address, data);

            codeGen.genCodeForStoreInd(store);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(extendedOperand == 0 ? INS_movbe : INS_movbe_apx));
            Assert.That(Descriptors(codeGen)[0].idOpSize(), Is.EqualTo(shortStore ? EA_2BYTE : EA_4BYTE));
        });
    }

    [Test]
    public static void Simd12StoresWriteExactlyEightThenFourBytes(
        [Values(0, 1, 2, 3)] int addressKind, [Values(false, true)] bool zero,
        [Values(false, true)] bool vex)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.getRelocTypeHint = &GetRelocTypeHint;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
#endif
            codeGen.Emitter.UseVexEncodings = vex;
            codeGen.Emitter.UseEvexEncodings = vex;
            GenTree address = addressKind switch
            {
                0 => Register(compiler, TYP_BYREF, REG_RAX),
                1 => new GenTreeAddrMode(TYP_BYREF, Register(compiler, TYP_BYREF, REG_RAX), null, 0, 4),
                2 => compiler.gtNewIconNode(TYP_I_IMPL, 0x100),
                _ => new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 4),
            };
            address.IsContained = addressKind != 0;
            var data = new GenTreeVecCon(TYP_SIMD12) { RegNum = REG_XMM1 };
            data.SimdVal.u32[0] = zero ? 0u : 1u;
            var store = new GenTreeStoreInd(TYP_SIMD12, address, data);

            codeGen.genCodeForStoreInd(store);

            var descriptors = Descriptors(codeGen);
            Assert.That(descriptors, Has.Count.EqualTo(2));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movsd_simd));
            Assert.That(descriptors[0].idOpSize(), Is.EqualTo(EA_8BYTE));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(zero ? INS_movss : INS_extractps));
            Assert.That(descriptors[1].idOpSize(), Is.EqualTo(zero ? EA_4BYTE : EA_16BYTE));
            if (!zero)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, descriptors[1]), Is.EqualTo((nint)2));
            }
            if (addressKind == 0)
            {
                Assert.That(store.Addr.AsAddrMode().BaseAddress, Is.SameAs(address));
                Assert.That(store.Addr.AsAddrMode().Offset, Is.EqualTo(8));
            }
            else
            {
                Assert.That(store.Addr, Is.SameAs(address));
                if (addressKind == 1)
                {
                    Assert.That(store.Addr.AsAddrMode().Offset, Is.EqualTo(12));
                }
                else if (addressKind == 2)
                {
                    Assert.That(store.Addr.AsIntConCommon().IconValue, Is.EqualTo((nint)0x108));
                }
                else
                {
                    Assert.That(descriptors[1].idAddr().iiaLclVar.lvaOffset(), Is.EqualTo(12u));
                }
            }
        });
    }

    [TestCase(NI_X86Base_ConvertToInt32, TYP_INT, 16, TYP_INT, INS_movd32, EA_4BYTE)]
    [TestCase(NI_X86Base_X64_ConvertToInt64, TYP_LONG, 16, TYP_LONG, INS_movd64, EA_8BYTE)]
    [TestCase(NI_AVX512_ConvertToVector128Byte, TYP_INT, 16, TYP_SIMD16, INS_vpmovdb, EA_16BYTE)]
    public static void ContainedScalarAndNarrowingIntrinsicsRetainNativeStoreAttributes(
        NamedIntrinsic intrinsicId, var_types baseType, int simdSize, var_types resultType,
        instruction ins, emitAttr attr)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            var source = new GenTreeVecCon(Compiler.GetSimdTypeForSize(simdSize)) { RegNum = REG_XMM1 };
            var data = new GenTreeHWIntrinsic(resultType, intrinsicId, baseType, (byte)simdSize, source)
            {
                IsContained = true,
            };
            var store = new GenTreeStoreInd(resultType, Register(compiler, TYP_BYREF, REG_RAX), data);

            codeGen.genCodeForStoreInd(store);

            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(ins));
            Assert.That(Descriptors(codeGen)[0].idOpSize(), Is.EqualTo(attr));
        });
    }

    [Test]
    public static void ExtractionStoresSignExtendUnsignedImmediatesAndUseVexCompatibleOpcodes(
        [Values(127, 128, 255)] int value, [Values(false, true)] bool evex)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            if (evex)
            {
                compiler.opts.compSupportsISA.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAExactly.AddInstructionSet(InstructionSet_AVX512);
                compiler.opts.compSupportsISAReported.AddInstructionSet(InstructionSet_AVX512);
            }
            var source = new GenTreeVecCon(TYP_SIMD32) { RegNum = REG_XMM1 };
            var immediate = compiler.gtNewIconNode(TYP_INT, value);
            immediate.IsContained = true;
            var data = new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX_ExtractVector128, TYP_DOUBLE, 32, source, immediate)
            {
                IsContained = true,
            };
            var store = new GenTreeStoreInd(TYP_SIMD16, Register(compiler, TYP_BYREF, REG_RAX), data);

            codeGen.genCodeForStoreInd(store);

            Assert.That(immediate.IconValue, Is.EqualTo((nint)unchecked((sbyte)value)));
            Assert.That(Descriptors(codeGen), Has.Count.EqualTo(1));
            Assert.That(Descriptors(codeGen)[0].idIns(), Is.EqualTo(INS_vextractf32x4));
            Assert.That(Descriptors(codeGen)[0].idOpSize(), Is.EqualTo(EA_32BYTE));
            Assert.That(InstructionConstant(codeGen.Emitter, Descriptors(codeGen)[0]),
                Is.EqualTo((nint)unchecked((sbyte)value)));
        });
    }

#if DEBUG
    [TestCase(TYP_REF)]
    [TestCase(TYP_SIMD12)]
    [TestCase(TYP_SIMD16)]
    public static void D005RejectsBeforeWriteBarrierSelectionAndOperandOrAddressMutation(var_types type)
    {
        EmitterCallInstructionTests.WithEmitter((compiler, codeGen) =>
        {
            var address = Register(compiler, TYP_BYREF, REG_RAX);
            var immediate = compiler.gtNewIconNode(TYP_INT, 255);
            immediate.IsContained = true;
            GenTree data = type switch
            {
                TYP_REF => Register(compiler, TYP_REF, REG_RDX),
                TYP_SIMD12 => new GenTreeVecCon(TYP_SIMD12) { RegNum = REG_XMM1 },
                _ => new GenTreeHWIntrinsic(TYP_SIMD16, NI_AVX_ExtractVector128, TYP_DOUBLE, 32,
                    new GenTreeVecCon(TYP_SIMD32) { RegNum = REG_XMM1 }, immediate) { IsContained = true },
            };
            var store = new GenTreeStoreInd(type, address, data);
            compiler.opts.dspCode = true;

            var exception = Assert.Throws<FatalJitException>(() => codeGen.genCodeForStoreInd(store));

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(codeGen.genWriteBarrierUsed, Is.False);
            Assert.That(store.Addr, Is.SameAs(address));
            Assert.That(immediate.IconValue, Is.EqualTo((nint)255));
            Assert.That(address._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
            Assert.That(data._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
                Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
            Assert.That(Descriptors(codeGen), Is.Empty);
        });
    }
#endif

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void* GetHelperFtn(ICorJitInfo* self, CorInfoHelpFunc helper,
        CORINFO_CONST_LOOKUP* lookup, CORINFO_METHOD_STRUCT_** method)
    {
        s_helper = helper;
        lookup->accessType = InfoAccessType.IAT_VALUE;
        lookup->addr = (void*)0x1234;

        return lookup->addr;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* self, void* address)
    {
        return CorInfoReloc.NONE;
    }
}
