// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.NamedIntrinsic;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using static RyuJitSharp.UnitTests.CodeGenShiftTests;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterIndirectStoreTests
{
    [TestCase(false, 0, 2)]
    [TestCase(false, 8192, -128)]
    [TestCase(true, 0, -1)]
    [TestCase(true, -8192, 2)]
    public static void AddressRegisterImmediateWrapperUsesItsExplicitSourceAndPreservesDisplacements(
        bool storeNode, int offset, int immediate)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var address = new GenTreeAddrMode(TYP_BYREF, Physical(REG_RAX), Physical(REG_RCX), 2, offset)
            {
                IsContained = true,
            };
            var indir = storeNode
                ? new GenTreeStoreInd(TYP_SIMD16, address, Physical(REG_XMM1, TYP_SIMD16))
                : new GenTreeIndir(GT_IND, TYP_SIMD16, address);

            codeGen.Emitter.emitIns_A_R_I(INS_extractps, EA_16BYTE, indir, REG_XMM2, immediate);

            var id = Only(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(INS_extractps));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_AWR_RRD_CNS));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM2));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_RCX));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(1u));
            Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)offset));
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)immediate));
            Assert.That(id.idCodeSize(), Is.EqualTo(offset == 0 ? 7u : 11u));
            Assert.That(compiler.compCurLifeTree, Is.Null);
        });
    }

#if DEBUG
    [Test]
    public static void D005RejectsTheAddressRegisterImmediateWrapperBeforeAllocation()
    {
        WithEmitter((compiler, codeGen) =>
        {
            var store = new GenTreeStoreInd(TYP_SIMD16, Physical(REG_RAX), Physical(REG_XMM2, TYP_SIMD16));
            var used = Used(codeGen.Emitter);
            compiler.opts.dspCode = true;

            var exception = Assert.Throws<FatalJitException>(() =>
                codeGen.Emitter.emitIns_A_R_I(INS_extractps, EA_16BYTE, store, REG_XMM2, 2));

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(Used(codeGen.Emitter), Is.EqualTo(used));
            Assert.That(CurrentSize(codeGen.Emitter), Is.Zero);
            Assert.That(compiler.compCurLifeTree, Is.Null);
        });
    }
#endif

    [TestCase(TYP_BYTE, EA_1BYTE, INS_mov, REG_RDX, 2u)]
    [TestCase(TYP_BYTE, EA_1BYTE, INS_mov, REG_RSI, 3u)]
    [TestCase(TYP_SHORT, EA_2BYTE, INS_mov, REG_RDX, 3u)]
    [TestCase(TYP_INT, EA_4BYTE, INS_mov, REG_RDX, 2u)]
    [TestCase(TYP_INT, EA_4BYTE, INS_mov, REG_R16, 4u)]
    [TestCase(TYP_LONG, EA_8BYTE, INS_mov, REG_RDX, 3u)]
    [TestCase(TYP_REF, EA_GCREF, INS_mov, REG_RDX, 3u)]
    [TestCase(TYP_BYREF, EA_BYREF, INS_mov, REG_RDX, 3u)]
    [TestCase(TYP_FLOAT, EA_4BYTE, INS_movss, REG_XMM2, 4u)]
    [TestCase(TYP_DOUBLE, EA_8BYTE, INS_movsd_simd, REG_XMM2, 4u)]
    public static void RegisterStoresRetainWidthGcClassificationAndNativeSizes(
        var_types type, emitAttr attr, instruction ins, regNumber source, uint size)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var data = Physical(source, type.ActualType);
            var store = new GenTreeStoreInd(type, Physical(REG_RAX, TYP_BYREF), data);
            codeGen.GCInfo.gcMarkRegPtrVal(REG_RAX, TYP_BYREF);
            if (varTypeIsGC(type))
            {
                codeGen.GCInfo.gcMarkRegPtrVal(source, type);
            }
            var refs = codeGen.GCInfo.gcRegGCrefSetCur;
            var byrefs = codeGen.GCInfo.gcRegByrefSetCur;

            codeGen.Emitter.emitInsStoreInd(ins, attr, store);

            var id = Only(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_AWR_RRD));
            Assert.That(id.idReg1(), Is.EqualTo(source));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_SIZE(attr)));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.idGCref(), Is.EqualTo(type == TYP_REF ? GCInfo.GCtype.GCT_GCREF :
                type == TYP_BYREF ? GCInfo.GCtype.GCT_BYREF : GCInfo.GCtype.GCT_NONE));
            Assert.That(codeGen.GCInfo.gcRegGCrefSetCur, Is.EqualTo(refs));
            Assert.That(codeGen.GCInfo.gcRegByrefSetCur, Is.EqualTo(byrefs));
            Assert.That(compiler.compCurLifeTree, Is.Null);
            AssertNotConsumed(data);
        });
    }

    [TestCase(EA_1BYTE, 127L, 3u)]
    [TestCase(EA_2BYTE, 32767L, 5u)]
    [TestCase(EA_4BYTE, 1L, 6u)]
    [TestCase(EA_4BYTE, 4294967297L, 6u)]
    [TestCase(EA_8BYTE, -1L, 7u)]
    [TestCase(EA_GCREF, 0L, 7u)]
    public static void ContainedImmediatesRetainNativeIntTruncationAndStoreWidth(
        emitAttr attr, long value, uint size)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var data = compiler.gtNewIconNode(TYP_LONG, unchecked((nint)value));
            data.IsContained = true;
            var store = new GenTreeStoreInd(TYP_LONG, Physical(REG_RAX), data);

            codeGen.Emitter.emitInsStoreInd(INS_mov, attr, store);

            var id = Only(codeGen);
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_AWR_CNS));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_SIZE(attr)));
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)unchecked((int)value)));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(data.IconValue, Is.EqualTo(unchecked((nint)value)));
            Assert.That(compiler.compCurLifeTree, Is.Null);
        });
    }

    [TestCase(false, 0)]
    [TestCase(false, 8191)]
    [TestCase(false, 8192)]
    [TestCase(false, -8191)]
    [TestCase(false, -8192)]
    [TestCase(true, 16)]
    [TestCase(true, 8192)]
    public static void IndexedStoresKeepScaleDisplacementAndConstantStorageSeparate(bool immediate, int offset)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var address = new GenTreeAddrMode(TYP_BYREF, Physical(REG_R8), Physical(REG_RCX), 4, offset)
            {
                IsContained = true,
            };
            GenTree data = Physical(REG_RDX);
            if (immediate)
            {
                data = compiler.gtNewIconNode(TYP_INT, 12345);
                data.IsContained = true;
            }
            var store = new GenTreeStoreInd(TYP_LONG, address, data);

            codeGen.Emitter.emitInsStoreInd(INS_mov, EA_8BYTE, store);

            var id = Only(codeGen);
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_R8));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_RCX));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(2u));
            Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)offset));
            Assert.That(id.idIsLargeDsp(), Is.EqualTo(offset is < -8191 or > 8191));
            Assert.That(id.idInsFmt(), Is.EqualTo(immediate ? IF_AWR_CNS : IF_AWR_RRD));
            if (immediate)
            {
                Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)12345));
            }
            Assert.That(address.Offset, Is.EqualTo(offset));
            Assert.That(compiler.compCurLifeTree, Is.Null);
        });
    }

    [Test]
    public static void IndexOnlyStoresRetainTheirMissingBaseAndDisp32Encoding()
    {
        WithEmitter((compiler, codeGen) =>
        {
            var address = new GenTreeAddrMode(TYP_BYREF, Physical(REG_RAX), Physical(REG_RCX), 8, 32)
            {
                IsContained = true,
                BaseAddress = null,
            };
            var store = new GenTreeStoreInd(TYP_LONG, address, Physical(REG_RDX));

            codeGen.Emitter.emitInsStoreInd(INS_mov, EA_8BYTE, store);

            var id = Only(codeGen);
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_RCX));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(3u));
            Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)32));
            Assert.That(id.idCodeSize(), Is.EqualTo(8u));
            Assert.That(compiler.compCurLifeTree, Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void AbsoluteStoresRetainRelocationsAndHandleDebugMetadata(bool relocation, bool immediate)
    {
        WithEmitter((compiler, codeGen) =>
        {
            compiler.opts.compReloc = relocation;
            var address = compiler.gtNewIconNode(TYP_I_IMPL, 0x1234);
            address.IsContained = true;
            if (relocation)
            {
                address.Flags |= GTF_ICON_STATIC_HDL;
#if DEBUG
                address.TargetHandle = 0x5678;
#endif
            }
            GenTree data = Physical(REG_RDX);
            if (immediate)
            {
                data = compiler.gtNewIconNode(TYP_INT, 7);
                data.IsContained = true;
            }
            var store = new GenTreeStoreInd(TYP_INT, address, data);

            codeGen.Emitter.emitInsStoreInd(INS_mov, EA_4BYTE, store);

            var id = Only(codeGen);
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_NA));
            Assert.That(Displacement(codeGen.Emitter, id), Is.EqualTo((nint)0x1234));
            Assert.That(id.idIsDspReloc(), Is.EqualTo(relocation));
            Assert.That(id.idCodeSize(), Is.EqualTo((relocation ? 6u : 7u) + (immediate ? 4u : 0u)));
#if DEBUG
            if (relocation)
            {
                var info = id.idDebugOnlyInfo();
                assert(info is not null);
                Assert.That(info.idMemCookie, Is.EqualTo((nint)0x5678));
                Assert.That(info.idFlags & GTF_ICON_HDL_MASK, Is.EqualTo(GTF_ICON_STATIC_HDL));
            }
#endif
        }, relocation);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void LocalStoresUpdateTheStoreLifetimeAndGcStackHome(bool immediate, bool dying)
    {
        WithEmitter((compiler, codeGen) =>
        {
            ref var local = ref compiler.lvaTable[0];
            local.Type = TYP_REF;
            local.RegNum = REG_STK;
            codeGen.GCInfo.gcTrkStkPtrLcls = VarSetOps.MakeSingleton(compiler, 0);
            if (dying)
            {
                VarSetOps.AddElemD(compiler, compiler.compCurLife, 0);
                VarSetOps.AddElemD(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0);
            }
            var address = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0)
            {
                Flags = dying ? GTF_VAR_DEATH : GTF_VAR_DEF,
                IsContained = true,
            };
            GenTree data = Physical(REG_RDX, TYP_REF);
            if (immediate)
            {
                data = compiler.gtNewIconNode(TYP_REF, 0);
                data.IsContained = true;
            }
            var store = new GenTreeStoreInd(TYP_REF, address, data);

            codeGen.Emitter.emitInsStoreInd(INS_mov, EA_GCREF, store);

            var id = Only(codeGen);
            Assert.That(id.idInsFmt(), Is.EqualTo(immediate ? IF_SWR_CNS : IF_SWR_RRD));
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.Zero);
            Assert.That(id.idGCref(), Is.EqualTo(GCInfo.GCtype.GCT_GCREF));
            Assert.That(compiler.compCurLifeTree, Is.SameAs(store));
            Assert.That(VarSetOps.IsMember(compiler, compiler.compCurLife, 0), Is.EqualTo(!dying));
            Assert.That(VarSetOps.IsMember(compiler, codeGen.GCInfo.gcVarPtrSetCur, 0), Is.EqualTo(!dying));
        });
    }

    [TestCase(GT_BSWAP, EA_4BYTE, REG_RDX, INS_movbe, false)]
    [TestCase(GT_BSWAP, EA_4BYTE, REG_RDX, INS_movbe, true)]
    [TestCase(GT_BSWAP16, EA_2BYTE, REG_RDX, INS_movbe, false)]
    [TestCase(GT_BSWAP16, EA_2BYTE, REG_RDX, INS_movbe, true)]
    [TestCase(GT_BSWAP, EA_8BYTE, REG_R16, INS_movbe_apx, false)]
    [TestCase(GT_BSWAP, EA_8BYTE, REG_R16, INS_movbe_apx, true)]
    public static void ContainedByteSwapsUseTheirOperandRatherThanTheContainedNodeRegister(
        genTreeOps oper, emitAttr attr, regNumber source, instruction ins, bool local)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var data = new GenTreeUnOp(oper, TYP_LONG, Physical(source)) { IsContained = true };
            var store = new GenTreeStoreInd(TYP_LONG, Address(local), data);

            codeGen.Emitter.emitInsStoreInd(ins, attr, store);

            var id = Only(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idReg1(), Is.EqualTo(source));
            Assert.That(id.idOpSize(), Is.EqualTo(attr));
            Assert.That(id.idInsFmt(), Is.EqualTo(local ? IF_SWR_RRD : IF_AWR_RRD));
            Assert.That(data.RegNum, Is.EqualTo(REG_NA));
            Assert.That(data.IsContained, Is.True);
            Assert.That(compiler.compCurLifeTree, local ? Is.SameAs(store) : Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void UncontainedByteSwapsAndIntrinsicsStoreTheirAssignedResults(bool local, bool intrinsic)
    {
        WithEmitter((compiler, codeGen) =>
        {
            GenTree data = intrinsic
                ? new GenTreeHWIntrinsic(TYP_INT, NI_X86Base_ConvertToInt32, TYP_INT, 16,
                    Physical(REG_XMM2, TYP_SIMD16))
                : new GenTreeUnOp(GT_BSWAP, TYP_INT, Physical(REG_RCX, TYP_INT));
            data.RegNum = REG_RDX;
            var store = new GenTreeStoreInd(TYP_INT, Address(local), data);

            codeGen.Emitter.emitInsStoreInd(INS_mov, EA_4BYTE, store);

            var id = Only(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(INS_mov));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RDX));
            Assert.That(id.idInsFmt(), Is.EqualTo(local ? IF_SWR_RRD : IF_AWR_RRD));
            Assert.That(compiler.compCurLifeTree, local ? Is.SameAs(store) : Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void OneOperandIntrinsicsRetainScalarStoresAndMaskedNarrowing(bool local, bool narrowing)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var vector = Physical(REG_XMM2, narrowing ? TYP_SIMD64 : TYP_SIMD16);
            var data = new GenTreeHWIntrinsic(narrowing ? TYP_SIMD16 : TYP_INT,
                narrowing ? NI_AVX512_ConvertToVector128Byte : NI_X86Base_ConvertToInt32,
                TYP_INT, narrowing ? (byte)64 : (byte)16, vector) { IsContained = true };
            var store = new GenTreeStoreInd(data.Type, Address(local), data);
            var ins = narrowing ? INS_vpmovdb : INS_movd32;
            var attr = narrowing ? EA_64BYTE : EA_4BYTE;
            var options = narrowing ? INS_OPTS_EVEX_em_k3 : INS_OPTS_NONE;

            codeGen.Emitter.emitInsStoreInd(ins, attr, store, options);

            var id = Only(codeGen);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM2));
            Assert.That(id.idOpSize(), Is.EqualTo(attr));
            Assert.That(id.idInsFmt(), Is.EqualTo(local ? IF_SWR_RRD : IF_AWR_RRD));
            Assert.That(id.idGetEvexAaaContext(), Is.EqualTo(narrowing ? 3u : 0u));
            Assert.That(data.RegNum, Is.EqualTo(REG_NA));
            Assert.That(compiler.compCurLifeTree, local ? Is.SameAs(store) : Is.Null);
        });
    }

    [TestCase(false, 2)]
    [TestCase(false, -1)]
    [TestCase(true, 2)]
    [TestCase(true, -1)]
    public static void TwoOperandIntrinsicsPreserveTheSignedImmediateAndSourceVector(bool local, int immediate)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var icon = compiler.gtNewIconNode(TYP_INT, immediate);
            icon.IsContained = true;
            var data = new GenTreeHWIntrinsic(TYP_FLOAT, NI_Vector_GetElement, TYP_FLOAT, 16,
                Physical(REG_XMM2, TYP_SIMD16), icon) { IsContained = true };
            var store = new GenTreeStoreInd(TYP_FLOAT, Address(local), data);

            codeGen.Emitter.emitInsStoreInd(INS_extractps, EA_16BYTE, store);

            var id = Only(codeGen);
            Assert.That(id.idInsFmt(), Is.EqualTo(local ? IF_SWR_RRD_CNS : IF_AWR_RRD_CNS));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM2));
            Assert.That(id.idOpSize(), Is.EqualTo(EA_16BYTE));
            Assert.That(InstructionConstant(codeGen.Emitter, id), Is.EqualTo((nint)immediate));
            Assert.That(id.idCodeSize(), Is.EqualTo(local ? 7u : 6u));
            Assert.That(icon.IconValue, Is.EqualTo((nint)immediate));
            Assert.That(compiler.compCurLifeTree, local ? Is.SameAs(store) : Is.Null);
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void VectorStoresRetainLegacyVexAndEvexMaskedForms(bool local, bool vex)
    {
        WithEmitter((compiler, codeGen) =>
        {
            codeGen.Emitter.UseVexEncodings = vex;
            codeGen.Emitter.UseEvexEncodings = vex;
            var store = new GenTreeStoreInd(TYP_SIMD16, Address(local), Physical(REG_XMM2, TYP_SIMD16));
            codeGen.Emitter.emitInsStoreInd(INS_movups, EA_16BYTE, store);
            var id = Only(codeGen);
            Assert.That(id.idCodeSize(), Is.EqualTo((vex ? 4u : 3u) + (local ? 1u : 0u)));

            if (vex)
            {
                codeGen.Emitter.emitInsStoreInd(INS_movups, EA_16BYTE, store, INS_OPTS_EVEX_em_k7);
                var masked = Descriptors(codeGen)[1];
                Assert.That(masked.idGetEvexAaaContext(), Is.EqualTo(7u));
                Assert.That(masked.idCodeSize(), Is.EqualTo(local ? 10u : 6u));
            }
        });
    }

#if DEBUG
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void D005RejectsBeforeDescriptorsAndLocalLivenessChange(bool local, bool immediate)
    {
        WithEmitter((compiler, codeGen) =>
        {
            var address = Address(local);
            if (local)
            {
                address.Flags |= GTF_VAR_DEF;
            }
            GenTree data = Physical(REG_RDX);
            if (immediate)
            {
                data = compiler.gtNewIconNode(TYP_INT, 7);
                data.IsContained = true;
            }
            var store = new GenTreeStoreInd(TYP_INT, address, data);
            var used = Used(codeGen.Emitter);
            compiler.opts.dspCode = true;

            var exception = Assert.Throws<FatalJitException>(() =>
                codeGen.Emitter.emitInsStoreInd(INS_mov, EA_4BYTE, store));

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(Descriptors(codeGen), Is.Empty);
            Assert.That(Used(codeGen.Emitter), Is.EqualTo(used));
            Assert.That(CurrentSize(codeGen.Emitter), Is.Zero);
            Assert.That(compiler.compCurLifeTree, Is.Null);
            Assert.That(VarSetOps.IsEmpty(compiler, compiler.compCurLife), Is.True);
        });
    }
#endif

    private static GenTreePhysReg Physical(regNumber reg, var_types type = TYP_I_IMPL)
        => new(reg, type) { RegNum = reg };

    private static GenTree Address(bool local) => local
        ? new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 4) { IsContained = true }
        : Physical(REG_RAX, TYP_BYREF);

    private static Emitter.instrDesc Only(CodeGen codeGen)
    {
        var descriptors = Descriptors(codeGen);
        Assert.That(descriptors, Has.Count.EqualTo(1));
        var id = descriptors[0];
        Assert.That(CurrentSize(codeGen.Emitter), Is.EqualTo((int)id.idCodeSize()));
        return id;
    }

    private static void AssertNotConsumed(GenTree tree)
    {
#if DEBUG
        Assert.That(tree._debugFlags & GenTreeDebugFlags.GTF_DEBUG_NODE_CG_CONSUMED,
            Is.EqualTo(GenTreeDebugFlags.GTF_DEBUG_NONE));
#else
        Assert.That(tree.RegNum, Is.Not.EqualTo(REG_NA));
#endif
    }

    private static void WithEmitter(Action<Compiler, CodeGen> action, bool relocation = false)
    {
        CodeGenBinaryTests.WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            if (relocation)
            {
                vtable.getRelocTypeHint = &RelativeRelocation;
            }
            else
            {
                vtable.getRelocTypeHint = &NoRelocation;
            }
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMatchedVM = true;
            codeGen.Emitter.UseRex2Encodings = true;
            codeGen.Emitter.UsePromotedEvexEncodings = true;
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
#endif
            action(compiler, codeGen);
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc NoRelocation(ICorJitInfo* jitInfo, void* address) => CorInfoReloc.NONE;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc RelativeRelocation(ICorJitInfo* jitInfo, void* address) => CorInfoReloc.RELATIVE32;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc id);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);
}
