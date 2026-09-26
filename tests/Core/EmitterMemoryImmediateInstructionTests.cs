// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.Globals;
using static RyuJitSharp.Emitter.insFormat;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.insOpts;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class EmitterMemoryImmediateInstructionTests
{
    [TestCase(0, false, INS_pshufd, 7, 0, 5u, 16)]
    [TestCase(0, false, INS_pslld, 16, 16, 8u, 24)]
    [TestCase(0, false, INS_pshufd, -128, 65536, 9u, 32)]
    [TestCase(1, false, INS_pshufd, 7, 0, 9u, 16)]
    [TestCase(1, false, INS_pslld, 7, 16, 11u, 24)]
    [TestCase(1, false, INS_pshufd, 16, 16, 9u, 32)]
    [TestCase(2, false, INS_pshufd, 7, 0, 6u, 16)]
    [TestCase(2, false, INS_pslld, 16, 0, 8u, 24)]
    [TestCase(0, true, INS_shufps, 7, 0, 5u, 16)]
    [TestCase(1, true, INS_shufps, 16, 16, 9u, 32)]
    [TestCase(2, true, INS_shufps, 16, 0, 6u, 24)]
    public static void MemoryImmediatesPreserveOpcodeChoiceFormatsAndDescriptorShapes(
        int memoryKind, bool twoRegisters, instruction ins, int value, int offset, uint size, int logicalSize)
    {
        WithEmitter((compiler, emitter) =>
        {
            EmitImmediate(compiler, emitter, memoryKind, twoRegisters, ins, EA_16BYTE, offset, value);
            var id = Last(emitter);
            var format = memoryKind switch
            {
                0 => twoRegisters ? IF_RWR_RRD_ARD_CNS : IF_RWR_ARD_CNS,
                1 => twoRegisters ? IF_RWR_RRD_MRD_CNS : IF_RWR_MRD_CNS,
                _ => twoRegisters ? IF_RWR_RRD_SRD_CNS : IF_RWR_SRD_CNS,
            };

            Assert.That(id.idInsFmt(), Is.EqualTo(format));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM0));
            Assert.That(Constant(emitter, id), Is.EqualTo((nint)value));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
            Assert.That(id.NativeLogicalSize, Is.EqualTo(logicalSize));
            Assert.That(CurrentSize(emitter), Is.EqualTo((int)size));
            Assert.That(CurrentCount(emitter), Is.EqualTo(1));
            if (twoRegisters)
            {
                Assert.That(id.idReg2(), Is.EqualTo(REG_XMM1));
            }

            switch (memoryKind)
            {
                case 0:
                {
                    Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_RAX));
                    Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)offset));
                    break;
                }

                case 1:
                {
                    Assert.That(id.idIsDspReloc(), Is.True);
                    Assert.That(id.idAddr().iiaGetJitDataOffset(), Is.EqualTo(64));
                    Assert.That(FieldDisplacement(emitter, id), Is.EqualTo((nint)offset));
                    break;
                }

                default:
                {
                    Assert.That(id.idAddr().iiaLclVar.lvaVarNum(), Is.Zero);
                    Assert.That(id.idAddr().iiaLclVar.lvaOffset(), Is.EqualTo((uint)offset));
#if DEBUG
                    var info = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");
                    Assert.That(info.idVarRefOffs, Is.EqualTo(0x1234u));
#endif
                    break;
                }
            }
        });
    }

    [TestCase(0, false, 8u, 3u)]
    [TestCase(1, false, 11u, 1u)]
    [TestCase(2, false, 8u, 3u)]
    [TestCase(0, true, 8u, 3u)]
    [TestCase(1, true, 11u, 1u)]
    [TestCase(2, true, 8u, 3u)]
    public static void EveryMemoryImmediateFormRetainsBroadcastMasksAndCompression(
        int memoryKind, bool twoRegisters, uint size, uint broadcastContext)
    {
        WithEmitter((compiler, emitter) =>
        {
            EmitImmediate(compiler, emitter, memoryKind, twoRegisters, twoRegisters ? INS_shufps : INS_pshufd,
                EA_64BYTE, memoryKind == 2 ? 0 : 16, 7,
                INS_OPTS_EVEX_eb | INS_OPTS_EVEX_em_k3 | INS_OPTS_EVEX_em_zero);
            var id = Last(emitter);

            Assert.That(id.idGetEvexbContext(), Is.EqualTo(broadcastContext));
            Assert.That(id.idGetEvexAaaContext(), Is.EqualTo(3u));
            Assert.That(id.idIsEvexZContextSet(), Is.True);
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public static void GlobalFieldImmediatesRetainOnlyExplicitRelocationFlags(bool twoRegisters, bool relocation)
    {
        WithEmitter((_, emitter) =>
        {
            var attr = relocation ? EA_16BYTE | EA_DSP_RELOC_FLG : EA_16BYTE;
            if (twoRegisters)
            {
                emitter.emitIns_R_R_C_I(INS_shufps, attr, REG_XMM0, REG_XMM1, FLD_GLOBAL_DS, -4, 7);
            }
            else
            {
                emitter.emitIns_R_C_I(INS_pshufd, attr, REG_XMM0, FLD_GLOBAL_DS, -4, 7);
            }

            var id = Last(emitter);
            Assert.That(id.idIsDspReloc(), Is.EqualTo(relocation));
            Assert.That((nuint)id.idAddr().iiaFieldHnd, Is.EqualTo((nuint)FLD_GLOBAL_DS));
            Assert.That(FieldDisplacement(emitter, id), Is.EqualTo((nint)(-4)));
            Assert.That(id.idCodeSize(), Is.EqualTo(9u));
        });
    }

    [TestCase(0, 9u)]
    [TestCase(1, 9u)]
    [TestCase(2, 8u)]
    public static void AbsoluteAddressRelocationAndHandleDiagnosticsReachNewMemoryForms(int form, uint size)
    {
        WithEmitter((compiler, emitter) =>
        {
            compiler.opts.compReloc = true;
            var constant = new GenTreeIntCon(TYP_I_IMPL, (nint)0x12345678)
            {
                RegNum = REG_NA,
                IsContained = true,
            };
            constant.Flags |= GTF_ICON_FIELD_HDL;
#if DEBUG
            constant.TargetHandle = unchecked((nint)0xFEDCBA9876543210UL);
#endif
            var indir = new GenTreeIndir(GT_IND, TYP_SIMD16, constant);
            if (form == 0)
            {
                emitter.emitIns_R_A_I(INS_pshufd, EA_16BYTE, REG_XMM0, indir, 7);
            }
            else if (form == 1)
            {
                emitter.emitIns_R_R_A_I(INS_shufps, EA_16BYTE, REG_XMM0, REG_XMM1, indir, 7, IF_RWR_RRD_ARD_CNS);
            }
            else
            {
                emitter.emitIns_R_R_A(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, indir);
            }

            var id = Last(emitter);
            Assert.That(id.idAddr().iiaAddrMode.amBaseReg, Is.EqualTo(REG_NA));
            Assert.That(id.idAddr().iiaAddrMode.amIndxReg, Is.EqualTo(REG_NA));
            Assert.That(id.idIsDspReloc(), Is.True);
            Assert.That(AddressDisplacement(emitter, id), Is.EqualTo((nint)0x12345678));
            Assert.That(id.idCodeSize(), Is.EqualTo(size));
#if DEBUG
            var info = id.idDebugOnlyInfo() ?? throw new AssertionException("Missing debug information.");
            Assert.That(info.idFlags, Is.EqualTo(constant.Flags));
            Assert.That(info.idMemCookie, Is.EqualTo(constant.TargetHandle));
#endif
        });
    }

    [Test]
    public static void SharedMemoryRecordingKeepsMulxAndTheNativeApxFormatSelection()
    {
        WithEmitter((compiler, emitter) =>
        {
            var indir = Address(compiler, 0);
            emitter.emitIns_R_R_A(INS_mulx, EA_8BYTE, REG_R8, REG_R9, indir);
            Assert.That(Last(emitter).idInsFmt(), Is.EqualTo(IF_RWR_RWR_ARD));
            Assert.That(Last(emitter).idCodeSize(), Is.EqualTo(5u));

            emitter.UsePromotedEvexEncodings = true;
            emitter.emitIns_R_R_A(INS_add, EA_8BYTE, REG_R8, REG_R9, indir, INS_OPTS_EVEX_nd | INS_OPTS_EVEX_nf);
            var id = Last(emitter);
            Assert.That(id.idInsFmt(), Is.EqualTo(IF_RRW_RRD_ARD));
            Assert.That(id.idIsEvexNdContextSet(), Is.True);
            Assert.That(id.idIsEvexNfContextSet(), Is.True);
            Assert.That(id.idCodeSize(), Is.EqualTo(6u));
        });
    }

    [TestCase(false, false, REG_XMM0, 1, 3)]
    [TestCase(false, false, REG_XMM1, 2, 6)]
    [TestCase(false, true, REG_XMM1, 1, 4)]
    [TestCase(true, false, REG_XMM0, 1, 4)]
    [TestCase(true, false, REG_XMM1, 2, 7)]
    [TestCase(true, true, REG_XMM1, 1, 5)]
    public static void SimdMemoryWrappersCopyOnlyForLegacyDistinctDestinations(
        bool stack, bool vex, regNumber source, int count, int size)
    {
        WithEmitter((compiler, emitter) =>
        {
            emitter.UseEvexEncodings = vex;
            emitter.UseVexEncodings = vex;
            if (stack)
            {
                emitter.emitIns_SIMD_R_R_S(INS_addps, EA_16BYTE, REG_XMM0, source, 0, 0, INS_OPTS_NONE);
            }
            else
            {
                emitter.emitIns_SIMD_R_R_A(INS_addps, EA_16BYTE, REG_XMM0, source, Address(compiler, 0), INS_OPTS_NONE);
            }

            Assert.That(CurrentCount(emitter), Is.EqualTo(count));
            Assert.That(CurrentSize(emitter), Is.EqualTo(size));
            var id = Last(emitter);
            Assert.That(id.idIns(), Is.EqualTo(INS_addps));
            Assert.That(id.idReg1(), Is.EqualTo(REG_XMM0));
            if (vex)
            {
                Assert.That(id.idReg2(), Is.EqualTo(source));
            }
            else if (count == 2)
            {
                var descriptors = Buffer(emitter) ?? throw new AssertionException("Missing instruction buffer.");
                Assert.That(descriptors[0].idIns(), Is.EqualTo(INS_movaps));
                Assert.That(descriptors[0].idReg2(), Is.EqualTo(source));
            }
        });
    }

    [TestCase(INS_pslld, false, REG_XMM0, 1, 5, IF_RWR_CNS)]
    [TestCase(INS_pslld, false, REG_XMM1, 2, 8, IF_RWR_CNS)]
    [TestCase(INS_pslld, true, REG_XMM1, 1, 5, IF_RWR_RRD_CNS)]
    [TestCase(INS_pshufd, false, REG_XMM1, 1, 5, IF_RWR_RRD_CNS)]
    public static void SimdImmediateWrappersRetainLegacyCopiesAndIndependentDestinationExceptions(
        instruction ins, bool vex, regNumber source, int count, int size, Emitter.insFormat format)
    {
        WithEmitter((_, emitter) =>
        {
            emitter.UseEvexEncodings = vex;
            emitter.UseVexEncodings = vex;
            emitter.emitIns_SIMD_R_R_I(ins, EA_16BYTE, REG_XMM0, source, 7, INS_OPTS_NONE);

            Assert.That(CurrentCount(emitter), Is.EqualTo(count));
            Assert.That(CurrentSize(emitter), Is.EqualTo(size));
            Assert.That(Last(emitter).idInsFmt(), Is.EqualTo(format));
            Assert.That(Constant(emitter, Last(emitter)), Is.EqualTo((nint)7));
            if (format == IF_RWR_RRD_CNS)
            {
                Assert.That(Last(emitter).idReg2(), Is.EqualTo(source));
            }
        });
    }

    [TestCase(INS_add, EA_8BYTE, 127, false, 8u)]
    [TestCase(INS_add, EA_8BYTE, 128, false, 11u)]
    [TestCase(INS_add, EA_2BYTE, 128, false, 9u)]
    [TestCase(INS_add, EA_4BYTE, 3, true, 10u)]
    [TestCase(INS_mov, EA_4BYTE, 1, false, 10u)]
    public static void FieldImmediateSizingRetainsScalarWidthAndRelocationRules(
        instruction ins, emitAttr attr, int value, bool relocation, uint size)
    {
        WithEmitter((_, emitter) =>
        {
            var id = EmitterInstructionAllocationTests.Allocate(emitter, attr);
            id.idIns(ins);
            id.idInsFmt(IF_MRW_CNS);
            if (relocation)
            {
                id.idSetIsCnsReloc();
            }
            Assert.That(emitter.emitInsSizeCV(id, insCodeMI(ins), value), Is.EqualTo(size));
        });
    }

#if DEBUG
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    [TestCase(10)]
    public static void DisassemblyRecordsMemoryImmediateOperandsAndLegacyCopies(int entrypoint)
    {
        WithEmitter((compiler, emitter) =>
        {
            var address = Address(compiler, 0);
            var field = Compiler.eeFindJitDataOffs(64);
            compiler.opts.dspCode = true;
            if (entrypoint >= 8)
            {
                emitter.UseEvexEncodings = false;
                emitter.UseVexEncodings = false;
            }
            var used = Used(emitter);
            var diagnostic = InstructionRecordingTestSupport.Capture(() =>
            {
                switch (entrypoint)
                {
                    case 0:
                    {
                        emitter.emitIns_R_A_I(INS_pshufd, EA_16BYTE, REG_XMM0, address, 7);
                        break;
                    }

                    case 1:
                    {
                        emitter.emitIns_R_C_I(INS_pshufd, EA_16BYTE, REG_XMM0, field, 0, 7);
                        break;
                    }

                    case 2:
                    {
                        emitter.emitIns_R_S_I(INS_pshufd, EA_16BYTE, REG_XMM0, 0, 0, 7);
                        break;
                    }

                    case 3:
                    {
                        emitter.emitIns_R_R_A_I(INS_shufps, EA_16BYTE, REG_XMM0, REG_XMM1, address, 7, IF_RWR_RRD_ARD_CNS);
                        break;
                    }

                    case 4:
                    {
                        emitter.emitIns_R_R_C_I(INS_shufps, EA_16BYTE, REG_XMM0, REG_XMM1, field, 0, 7);
                        break;
                    }

                    case 5:
                    {
                        emitter.emitIns_R_R_S_I(INS_shufps, EA_16BYTE, REG_XMM0, REG_XMM1, 0, 0, 7);
                        break;
                    }

                    case 6:
                    {
                        emitter.emitIns_R_R_A(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, address);
                        break;
                    }

                    case 7:
                    {
                        emitter.emitIns_R_A(INS_addps, EA_16BYTE, REG_XMM0, address);
                        break;
                    }

                    case 8:
                    {
                        emitter.emitIns_SIMD_R_R_I(INS_pslld, EA_16BYTE, REG_XMM0, REG_XMM1, 7, INS_OPTS_NONE);
                        break;
                    }

                    case 9:
                    {
                        emitter.emitIns_SIMD_R_R_A(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, address, INS_OPTS_NONE);
                        break;
                    }

                    default:
                    {
                        emitter.emitIns_SIMD_R_R_S(INS_addps, EA_16BYTE, REG_XMM0, REG_XMM1, 0, 0, INS_OPTS_NONE);
                        break;
                    }
                }
            });

            Assert.That(Used(emitter), Is.GreaterThan(used));
            Assert.That(CurrentCount(emitter), Is.GreaterThan(0));
            Assert.That(CurrentSize(emitter), Is.GreaterThan(0));
            Assert.That(LastInstruction(emitter), Is.Not.Null);
            Assert.That(diagnostic, Does.Contain(entrypoint switch
            {
                0 or 1 or 2 => "pshufd",
                3 or 4 or 5 => "shufps",
                8 => "pslld",
                _ => "addps",
            }));
        });
    }
#endif

    private static void EmitImmediate(Compiler compiler, Emitter emitter, int memoryKind, bool twoRegisters,
        instruction ins, emitAttr attr, int offset, int value, insOpts options = INS_OPTS_NONE)
    {
        if (memoryKind == 0)
        {
            var address = Address(compiler, offset);
            if (twoRegisters)
            {
                emitter.emitIns_R_R_A_I(ins, attr, REG_XMM0, REG_XMM1, address, value, IF_RWR_RRD_ARD_CNS, options);
            }
            else
            {
                emitter.emitIns_R_A_I(ins, attr, REG_XMM0, address, value, options);
            }
        }
        else if (memoryKind == 1)
        {
            var field = Compiler.eeFindJitDataOffs(64);
            if (twoRegisters)
            {
                emitter.emitIns_R_R_C_I(ins, attr, REG_XMM0, REG_XMM1, field, offset, value, options);
            }
            else
            {
                emitter.emitIns_R_C_I(ins, attr, REG_XMM0, field, offset, value, options);
            }
        }
        else if (twoRegisters)
        {
            emitter.emitIns_R_R_S_I(ins, attr, REG_XMM0, REG_XMM1, 0, offset, value, options);
        }
        else
        {
            emitter.emitIns_R_S_I(ins, attr, REG_XMM0, 0, offset, value, options);
        }
    }

    private static GenTreeIndir Address(Compiler compiler, int offset)
    {
        var reg = compiler.gtNewLclvNode(TYP_I_IMPL, 0);
        reg.RegNum = REG_RAX;
        GenTree address = reg;
        if (offset != 0)
        {
            address = new GenTreeAddrMode(TYP_BYREF, reg, null, 0, offset)
            {
                RegNum = REG_NA,
                IsContained = true,
            };
        }

        return new GenTreeIndir(GT_IND, TYP_SIMD16, address);
    }

    private static void WithEmitter(Action<Compiler, Emitter> action)
    {
        CodeGenSpillVariableTests.WithCompiler(TYP_LONG, REG_RAX, (compiler, codeGen, _) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.getRelocTypeHint = &GetRelocTypeHint;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.info.compMatchedVM = true;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
#if DEBUG
            compiler.opts.compEnablePCRelAddr = true;
            codeGen.Emitter.emitVarRefOffs = 0x1234;
#endif
            action(compiler, codeGen.Emitter);
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoReloc GetRelocTypeHint(ICorJitInfo* _, void* address) => CorInfoReloc.RELATIVE32;

    private static Emitter.instrDesc Last(Emitter emitter) =>
        LastInstruction(emitter) ?? throw new AssertionException("No instruction was recorded.");

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsCns")]
    private static extern nint Constant(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsAmdAny")]
    private static extern nint AddressDisplacement(Emitter emitter, Emitter.instrDesc descriptor);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitGetInsDsp")]
    private static extern nint FieldDisplacement(Emitter emitter, Emitter.instrDesc descriptor);

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
