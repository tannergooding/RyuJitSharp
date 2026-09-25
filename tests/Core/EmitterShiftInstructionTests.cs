// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static class EmitterShiftInstructionTests
{
    [Test]
    public static void ShiftWrappersRetainImplicitOneAndImmediateFormats(
        [Values(INS_rcl_1, INS_rcr_1, INS_rol_1, INS_ror_1, INS_shl_1, INS_shr_1, INS_sar_1)] instruction one,
        [Values(1, 3)] int count,
        [Values(false, true)] bool ndd, [Values(false, true)] bool sameRegister)
    {
        var many = one switch
        {
            INS_rcl_1 => INS_rcl_N,
            INS_rcr_1 => INS_rcr_N,
            INS_rol_1 => INS_rol_N,
            INS_ror_1 => INS_ror_N,
            INS_shl_1 => INS_shl_N,
            INS_shr_1 => INS_shr_N,
            _ => INS_sar_N,
        };
        WithApxNdd(ndd, () => WithEmitter((_, emitter) =>
        {
            emitter.UsePromotedEvexEncodings = ndd;
            var source = sameRegister ? REG_RAX : REG_RCX;
            var ins = count == 1 ? one : many;
            emitter.emitIns_BASE_R_R_I(ins, EA_8BYTE, REG_RAX, source, count);

            var descriptors = Descriptors(emitter);
            var useNdd = ndd && !sameRegister;
            var needsCopy = !useNdd && !sameRegister;
            var id = descriptors[^1];
            var instructionSize = useNdd ? (count == 1 ? 6u : 7u) : (count == 1 ? 3u : 4u);
            Assert.That(descriptors, Has.Count.EqualTo(needsCopy ? 2 : 1));
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(id.idIsEvexNdContextSet(), Is.EqualTo(useNdd));
            Assert.That(id.idInsFmt(), Is.EqualTo(useNdd
                ? (count == 1 ? IF_RWR_RRD : IF_RWR_RRD_SHF)
                : (count == 1 ? IF_RRW : IF_RRW_SHF)));
            Assert.That(id.idCodeSize(), Is.EqualTo(instructionSize));
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)instructionSize + (needsCopy ? 3 : 0)));
            if (useNdd)
            {
                Assert.That(id.idReg2(), Is.EqualTo(source));
            }
            if (count != 1)
            {
                Assert.That(Constant(emitter, id), Is.EqualTo((nint)count));
            }
            if (needsCopy)
            {
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_mov));
                Assert.That(descriptors[0].idReg1(), Is.EqualTo(REG_RAX));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(source));
            }
        }));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void NonShiftOneRemainsAnImmediateAndNddRequiresPromotion(bool enabled, bool promoted)
    {
        WithApxNdd(enabled, () => WithEmitter((_, emitter) =>
        {
            emitter.UsePromotedEvexEncodings = promoted;
            emitter.emitIns_BASE_R_R_I(INS_add, EA_8BYTE, REG_RAX, REG_RCX, 1);

            var id = Last(emitter);
            var ndd = enabled && promoted;
            Assert.That(Descriptors(emitter), Has.Count.EqualTo(ndd ? 1 : 2));
            Assert.That(id.idInsFmt(), Is.EqualTo(ndd ? IF_RWR_RRD_CNS : IF_RRW_CNS));
            Assert.That(id.idIsEvexNdContextSet(), Is.EqualTo(ndd));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)1));
        }));
    }

    [TestCase(INS_rcl)]
    [TestCase(INS_rcr)]
    [TestCase(INS_rol)]
    [TestCase(INS_ror)]
    [TestCase(INS_shl)]
    [TestCase(INS_shr)]
    [TestCase(INS_sar)]
    public static void ShiftClassificationIncludesClForms(instruction ins)
    {
        Assert.That(Emitter.IsShiftInstruction(ins), Is.True);
        Assert.That(Emitter.IsShiftInstruction(INS_shlx), Is.False);
        Assert.That(Emitter.IsShiftInstruction(INS_add), Is.False);
    }

    [TestCase(REG_RCX, REG_NA, 1u, 0, 3u)]
    [TestCase(REG_RBP, REG_NA, 1u, 0, 4u)]
    [TestCase(REG_RCX, REG_RDX, 2u, 24, 5u)]
    [TestCase(REG_RCX, REG_R8, 4u, 65536, 8u)]
    [TestCase(REG_NA, REG_RDX, 8u, 0, 8u)]
    public static void IndexedLeaRetainsAllAddressFieldsAndDisplacementWidths(
        regNumber baseReg, regNumber indexReg, uint scale, int displacement, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.emitIns_R_ARX(INS_lea, EA_8BYTE, REG_RAX, baseReg, indexReg, scale, displacement);
            var id = Last(emitter);

            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_ARD));
            Assert.That(id.idReg1(), Is.EqualTo(REG_RAX));
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(baseReg));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(indexReg));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(scale switch
            {
                1 => 0u,
                2 => 1u,
                4 => 2u,
                _ => 3u,
            }));
            Assert.That(Displacement(emitter, id), Is.EqualTo((nint)displacement));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)size));
        });
    }

    [TestCase(INS_lea, REG_RAX, REG_NA, 0, 0)]
    [TestCase(INS_lea, REG_RCX, REG_NA, 0, 1)]
    [TestCase(INS_lea, REG_RAX, REG_RDX, 0, 1)]
    [TestCase(INS_lea, REG_RAX, REG_NA, 1, 1)]
    [TestCase(INS_mov, REG_RAX, REG_NA, 0, 1)]
    public static void OnlyIdentityLeaElidesTheDescriptor(
        instruction ins, regNumber baseReg, regNumber indexReg, int displacement, int count)
    {
        WithEmitter((_, emitter) =>
        {
            var used = Used(emitter);
            emitter.emitIns_R_ARX(ins, EA_8BYTE, REG_RAX, baseReg, indexReg, 1, displacement);
            Assert.That(CurrentCount(emitter), Is.EqualTo(count));
            if (count == 0)
            {
                Assert.That(Used(emitter), Is.EqualTo(used));
                Assert.That(CurrentSize(emitter), Is.Zero);
                Assert.That(LastInstruction(emitter), Is.Null);
            }
        });
    }

    [TestCase(false, REG_XMM0, EA_16BYTE, 3u)]
    [TestCase(true, REG_XMM0, EA_16BYTE, 4u)]
    [TestCase(true, REG_XMM16, EA_16BYTE, 6u)]
    [TestCase(true, REG_XMM0, EA_64BYTE, 6u)]
    public static void IndexedSimdLoadsUseLegacyVexAndEvexSizing(
        bool vex, regNumber destination, emitAttr attr, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseVexEncodings = vex;
            emitter.UseEvexEncodings = vex;
            emitter.emitIns_R_ARX(INS_movups, attr, destination, REG_RAX, REG_NA, 1, 0);
            var id = Last(emitter);
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RWR_ARD));
            Assert.That(id.idReg1(), Is.EqualTo(destination));
            Assert.That(id.idOpSize(), Is.EqualTo(attr));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
        });
    }

    [Test]
    public static void MemoryShiftImmediatesRetainMaskingAndStackOrAddressFormats(
        [Values(INS_rcl_N, INS_rcr_N, INS_rol_N, INS_ror_N, INS_shl_N, INS_shr_N, INS_sar_N)] instruction ins,
        [Values(0, 1, 2)] int addressKind, [Values(-1, 3, 128)] int count)
    {
        WithEmitter((compiler, emitter) =>
        {
            var source = compiler.gtNewIconNode(TYP_INT, count);
            source.IsContained = true;
            var store = Store(compiler, addressKind, source);
            emitter.emitInsRMW(ins, EA_8BYTE, store, source);

            var id = Last(emitter);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(addressKind == 0 ? IF_SRW_SHF : IF_ARW_SHF));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)(count & 0x7F)));
            Assert.That(id.idCodeSize(), Is.EqualTo(addressKind switch { 0 => 5u, 1 => 4u, _ => 6u }));
            CheckAddress(emitter, id, addressKind);
        });
    }

    [Test]
    public static void UnaryMemoryRmwRetainsImplicitOneClAndNonShiftForms(
        [Values(INS_shl_1, INS_shr_1, INS_sar, INS_rol, INS_neg, INS_not)] instruction ins,
        [Values(0, 1, 2)] int addressKind)
    {
        WithEmitter((compiler, emitter) =>
        {
            var store = Store(compiler, addressKind, compiler.gtNewIconNode(TYP_INT, 0));
            emitter.emitInsRMW(ins, EA_8BYTE, store);

            var id = Last(emitter);
            Assert.That(id.idIns(), Is.EqualTo(ins));
            Assert.That(id.idInsFmt(), Is.EqualTo(addressKind == 0 ? IF_SRW : IF_ARW));
            Assert.That(id.idCodeSize(), Is.EqualTo(addressKind switch { 0 => 4u, 1 => 3u, _ => 5u }));
            CheckAddress(emitter, id, addressKind);
        });
    }

    [Test]
    public static void BinaryMemoryRmwRetainsRegisterOrFullWidthImmediate(
        [Values(false, true)] bool immediate, [Values(127, 128)] int value, [Values(0, 1, 2)] int addressKind)
    {
        WithEmitter((compiler, emitter) =>
        {
            var source = compiler.gtNewIconNode(TYP_INT, value);
            if (immediate)
            {
                source.IsContained = true;
            }
            else
            {
                source.RegNum = REG_RCX;
            }
            var store = Store(compiler, addressKind, source);
            emitter.emitInsRMW(INS_add, EA_8BYTE, store, source);

            var id = Last(emitter);
            Assert.That(id.idInsFmt(), Is.EqualTo(addressKind == 0
                ? (immediate ? IF_SRW_CNS : IF_SRW_RRD)
                : (immediate ? IF_ARW_CNS : IF_ARW_RRD)));
            var size = addressKind switch { 0 => 4u, 1 => 3u, _ => 5u };
            if (immediate)
            {
                Assert.That(Constant(emitter, id), Is.EqualTo((nint)value));
                size += value == 127 ? 1u : 4u;
            }
            else
            {
                Assert.That(id.idReg1(), Is.EqualTo(REG_RCX));
            }
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            CheckAddress(emitter, id, addressKind);
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public static void D005RejectsBeforeCopiesAllocationAndIdentityElision(int entrypoint)
    {
        WithEmitter((compiler, emitter) =>
        {
            var source = compiler.gtNewIconNode(TYP_INT, 3);
            source.IsContained = true;
            var store = Store(compiler, entrypoint == 5 ? 0 : 1, source);
            var used = Used(emitter);
            compiler.opts.dspCode = true;

            var exception = Assert.Throws<FatalJitException>(() =>
            {
                switch (entrypoint)
                {
                    case 0:
                    {
                        emitter.emitInsRMW(INS_shl_N, EA_8BYTE, store, source);
                        break;
                    }
                    case 1:
                    case 5:
                    {
                        emitter.emitInsRMW(INS_shl_1, EA_8BYTE, store);
                        break;
                    }
                    case 2:
                    {
                        emitter.emitIns_R_ARX(INS_lea, EA_8BYTE, REG_RAX, REG_RAX, REG_NA, 1, 0);
                        break;
                    }
                    case 3:
                    {
                        emitter.emitIns_R_ARX(INS_lea, EA_8BYTE, REG_RAX, REG_RCX, REG_NA, 1, 0);
                        break;
                    }
                    default:
                    {
                        emitter.emitIns_BASE_R_R_I(INS_shl_N, EA_8BYTE, REG_RAX, REG_RCX, 3);
                        break;
                    }
                }
            });

            Assert.That(exception, Has.Property(nameof(FatalJitException.Result)).EqualTo(CorJitResult.CORJIT_SKIPPED));
            Assert.That(Used(emitter), Is.EqualTo(used));
            Assert.That(CurrentCount(emitter), Is.Zero);
            Assert.That(CurrentSize(emitter), Is.Zero);
            Assert.That(LastInstruction(emitter), Is.Null);
        });
    }
#endif

    private static GenTreeStoreInd Store(Compiler compiler, int kind, GenTree source)
    {
        GenTree address;
        if (kind == 0)
        {
            address = new GenTreeLclFld(GT_LCL_ADDR, TYP_BYREF, 0, 0) { IsContained = true };
        }
        else
        {
            address = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
            address.RegNum = REG_RAX;
            if (kind == 2)
            {
                var index = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
                index.RegNum = REG_RDX;
                address = new GenTreeAddrMode(TYP_BYREF, address, index, 4, 24) { IsContained = true };
            }
        }

        return new GenTreeStoreInd(TYP_LONG, address, source);
    }

    private static void CheckAddress(Emitter emitter, Emitter.instrDesc id, int kind)
    {
        if (kind == 0)
        {
            Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
            Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.Zero);
        }
        else
        {
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(kind == 2 ? REG_RDX : REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amScale, Is.EqualTo(kind == 2 ? 2u : 0u));
            Assert.That(Displacement(emitter, id), Is.EqualTo(kind == 2 ? (nint)24 : 0));
        }
    }

    private static void WithApxNdd(bool enabled, Action action)
    {
        var saved = ApxNdd(ref JitConfig);
        ApxNdd(ref JitConfig) = enabled ? 1 : 0;
        try
        {
            action();
        }
        finally
        {
            ApxNdd(ref JitConfig) = saved;
        }
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            action(compiler, codeGen.Emitter);
        });
    }

    private static List<Emitter.instrDesc> Descriptors(Emitter emitter)
        => Buffer(emitter) ?? throw new AssertionException("Missing descriptor buffer.");

    private static Emitter.instrDesc Last(Emitter emitter)
        => LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint Displacement(Emitter emitter, Emitter.instrDesc id);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint Constant(Emitter emitter, Emitter.instrDesc id);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_enableApxNDD")]
    private static extern ref int ApxNdd(ref JitConfigValues values);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? Buffer(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitLastIns")]
    private static extern ref Emitter.instrDesc? LastInstruction(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGsize")]
    private static extern ref int CurrentSize(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGinsCnt")]
    private static extern ref int CurrentCount(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeNext")]
    private static extern ref nuint Used(Emitter emitter);
}
