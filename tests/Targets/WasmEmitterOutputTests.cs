// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.GenTreeFlags;
using static RyuJitSharp.emitAttr;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.instruction;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;
using VarSetOps = RyuJitSharp.BitSetOps<RyuJitSharp.Compiler, RyuJitSharp.TrackedVarBitSetTraits>;

namespace RyuJitSharp.UnitTests;

[NonParallelizable]
internal static unsafe class WasmEmitterOutputTests
{
    [SetUp]
    public static void RequireWasmTarget()
    {
        Assume.That(Configuration.TARGET_WASM, "This fixture requires the Wasm target.");
    }

    [TestCase(CorInfoReloc.WASM_FUNCTION_INDEX_LEB)]
    [TestCase(CorInfoReloc.WASM_TABLE_INDEX_SLEB)]
    public static void FuncletRelocationPreservesCallbackWrittenAddend(CorInfoReloc relocationType)
    {
        WithEmitter((compiler, emitter) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var callbacks = new CallbackContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            };

            compiler.info.compMatchedVM = true;
            emitter.emitCmpHandle = &callbacks.JitInfo;

            var buffer = stackalloc byte[16];
            new Span<byte>(buffer, 16).Fill(0xA5);
            emitter.writeableOffset = 8;
            emitter.emitCodeBlock = buffer;

            var descriptor = TestEmitter.Constant(7);
            var destination = buffer + 2;
            var size = EmitOutputConstantFunclet(emitter, destination, descriptor, relocationType);

            Assert.That(size, Is.EqualTo((nuint)5));
            Assert.That(callbacks.Relocations, Is.EqualTo(1));
            Assert.That((nuint)callbacks.Location, Is.EqualTo((nuint)destination));
            Assert.That((nuint)callbacks.WritableLocation, Is.EqualTo((nuint)(buffer + 10)));
            Assert.That((nuint)callbacks.Target, Is.EqualTo((nuint)buffer));
            Assert.That(callbacks.RelocationType, Is.EqualTo(relocationType));
            Assert.That(callbacks.AdditionalDelta, Is.EqualTo(7));
            Assert.That(new ReadOnlySpan<byte>(buffer + 10, 5).ToArray(),
                Is.EqualTo(new byte[] { 0x87, 0x80, 0x80, 0x80, 0x00 }));
        });
    }

    [Test]
    public static void FrameIdentityInstructionRecordsMethodRelocation()
    {
        WithEmitter((compiler, emitter) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var callbacks = new CallbackContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            };

            var method = (CORINFO_METHOD_STRUCT_*)0x1234;
            compiler.info.compMethodHnd = method;
            compiler.info.compMatchedVM = true;
            emitter.emitCmpHandle = &callbacks.JitInfo;

            var buffer = stackalloc byte[16];
            new Span<byte>(buffer, 16).Fill(0xA5);
            emitter.writeableOffset = 8;
            emitter.emitCodeBlock = buffer;

            var descriptor = TestEmitter.Constant(0);
            descriptor.idIns(INS_i32_const_frameidentity);
            descriptor.idInsFmt(Emitter.insFormat.IF_WASM_FRAMEIDENTITY);
            var destination = buffer + 2;
            var next = destination;
            _ = EmitOutputInstrWasm(emitter, new insGroup(), descriptor, &next);

            Assert.That((nuint)next, Is.EqualTo((nuint)(destination + 6)));
            Assert.That(buffer[10], Is.EqualTo((byte)0x41));
            Assert.That(callbacks.Relocations, Is.EqualTo(1));
            Assert.That((nuint)callbacks.Location, Is.EqualTo((nuint)(destination + 1)));
            Assert.That((nuint)callbacks.WritableLocation, Is.EqualTo((nuint)(buffer + 11)));
            Assert.That((nuint)callbacks.Target, Is.EqualTo((nuint)method));
            Assert.That(callbacks.RelocationType, Is.EqualTo(CorInfoReloc.WASM_METHOD_FRAME_IDENTITY_SLEB));
            Assert.That(callbacks.AdditionalDelta, Is.Zero);
            Assert.That(new ReadOnlySpan<byte>(buffer + 11, 5).ToArray(),
                Is.EqualTo(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x00 }));
        });
    }

    [TestCase(INS_i8x16_relaxed_swizzle, 0x100)]
    [TestCase(INS_i32x4_relaxed_trunc_f32x4_s, 0x101)]
    [TestCase(INS_i32x4_relaxed_trunc_f32x4_u, 0x102)]
    [TestCase(INS_i32x4_relaxed_trunc_f64x2_s_zero, 0x103)]
    [TestCase(INS_i32x4_relaxed_trunc_f64x2_u_zero, 0x104)]
    [TestCase(INS_f32x4_relaxed_madd, 0x105)]
    [TestCase(INS_f32x4_relaxed_nmadd, 0x106)]
    [TestCase(INS_f64x2_relaxed_madd, 0x107)]
    [TestCase(INS_f64x2_relaxed_nmadd, 0x108)]
    [TestCase(INS_i8x16_relaxed_laneselect, 0x109)]
    [TestCase(INS_i16x8_relaxed_laneselect, 0x10A)]
    [TestCase(INS_i32x4_relaxed_laneselect, 0x10B)]
    [TestCase(INS_i64x2_relaxed_laneselect, 0x10C)]
    [TestCase(INS_f32x4_relaxed_min, 0x10D)]
    [TestCase(INS_f32x4_relaxed_max, 0x10E)]
    [TestCase(INS_f64x2_relaxed_min, 0x10F)]
    [TestCase(INS_f64x2_relaxed_max, 0x110)]
    [TestCase(INS_i16x8_relaxed_q15mulr_s, 0x111)]
    [TestCase(INS_i16x8_relaxed_dot_i8x16_i7x16_s, 0x112)]
    [TestCase(INS_i32x4_relaxed_dot_i8x16_i7x16_add_s, 0x113)]
    public static void RelaxedSimdInstructionsUseTheirPinnedPrefixedOpcodes(instruction ins, int opcode)
    {
        WithEmitter((compiler, emitter) =>
        {
            var buffer = stackalloc byte[4];
            new Span<byte>(buffer, 4).Fill(0xA5);
            emitter.writeableOffset = 0;
            emitter.emitCodeBlock = buffer;

            var descriptor = TestEmitter.Constant(0);
            descriptor.idIns(ins);
            descriptor.idInsFmt(Emitter.insFormat.IF_WASM_OPCODE);
            var next = buffer;
            _ = EmitOutputInstrWasm(emitter, new insGroup(), descriptor, &next);

            Assert.That((nuint)next, Is.EqualTo((nuint)(buffer + 3)));
            Assert.That(new ReadOnlySpan<byte>(buffer, 3).ToArray(),
                Is.EqualTo(new byte[] { 0xFD, unchecked((byte)((opcode & 0x7F) | 0x80)), unchecked((byte)(opcode >> 7)) }));
        });
    }

    [TestCase(true, INS_i32_const)]
    [TestCase(false, INS_global_get)]
    public static void ContainedHandleIndirectionUsesTheTargetAddressBase(
        bool nativeAot, instruction expectedBaseInstruction)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getWasmWellKnownGlobals = &GetWasmWellKnownGlobals;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            compiler.eeInfoInitialized = true;
            compiler.eeInfo.targetAbi = nativeAot
                ? CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI
                : CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            compiler.opts.compReloc = true;

            var address = compiler.gtNewIconHandleNode(0x1234, Globals.GTF_ICON_CLASS_HDL);
            address.IsContained = true;
            var indir = new GenTreeIndir(GT_IND, TYP_INT, address)
            {
                RegNum = regNumberExtensions.MakeWasmReg(1, WasmValueType.I32),
            };
            indir.Flags |= GTF_IND_NONFAULTING;

            GenCodeForIndir(codeGen, indir);

            var descriptors = CurrentDescriptors(codeGen.Emitter)
                ?? throw new AssertionException("Missing Wasm emitter instruction buffer.");
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expectedBaseInstruction));
            Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_i32_load));
            Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_local_tee));
        });
    }

    [TestCase(true, CorInfoReloc.WASM_MEMORY_ADDR_SLEB, CorInfoReloc.WASM_MEMORY_ADDR_LEB)]
    [TestCase(false, CorInfoReloc.WASM_MEMORY_ADDR_REL_SLEB, CorInfoReloc.WASM_MEMORY_ADDR_REL_LEB)]
    public static void MemoryAddressRelocationsFollowTheNativeAotAddressModel(
        bool nativeAot, CorInfoReloc expectedSigned, CorInfoReloc expectedUnsigned)
    {
        WithEmitter((compiler, emitter) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var callbacks = new CallbackContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            };
            ConfigureAddressModel(compiler, nativeAot);
            compiler.info.compMatchedVM = true;
            compiler.info.compCompHnd = &callbacks.JitInfo;
            emitter.emitCmpHandle = &callbacks.JitInfo;

            var buffer = stackalloc byte[32];
            new Span<byte>(buffer, 32).Fill(0xA5);
            emitter.writeableOffset = 8;
            emitter.emitCodeBlock = buffer;

            AssertMemoryAddressRelocation(
                emitter,
                buffer,
                INS_i32_const_address,
                Emitter.insFormat.IF_WASM_MEMADDR);
            Assert.That(callbacks.RelocationType, Is.EqualTo(expectedSigned));

            AssertMemoryAddressRelocation(
                emitter,
                buffer,
                INS_i32_load,
                Emitter.insFormat.IF_WASM_MEMARG);
            Assert.That(callbacks.RelocationType, Is.EqualTo(expectedUnsigned));
        });
    }

    [TestCase(true, CorInfoReloc.WASM_MEMORY_ADDR_SLEB)]
    [TestCase(false, CorInfoReloc.WASM_MEMORY_ADDR_REL_SLEB)]
    public static void DataOffsetRelocationsFollowTheNativeAotAddressModel(
        bool nativeAot, CorInfoReloc expectedRelocation)
    {
        WithEmitter((compiler, emitter) =>
        {
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.recordRelocation = &RecordRelocation;
            var callbacks = new CallbackContext
            {
                JitInfo = new ICorJitInfo { lpVtbl = &vtable },
            };
            ConfigureAddressModel(compiler, nativeAot);
            compiler.info.compMatchedVM = true;
            compiler.info.compCompHnd = &callbacks.JitInfo;
            emitter.emitCmpHandle = &callbacks.JitInfo;

            var data = stackalloc byte[64];
            var chunk = stackalloc AllocMemChunk[1];
            var offsets = stackalloc int[1];
            chunk[0] = new AllocMemChunk { size = 64, block = data };
            offsets[0] = 0;
            emitter.emitConsDsc.dsdOffs = 64;
            emitter.emitDataChunks = chunk;
            emitter.emitDataChunkOffsets = offsets;
            emitter.emitNumDataChunks = 1;

            var buffer = stackalloc byte[16];
            new Span<byte>(buffer, 16).Fill(0xA5);
            emitter.writeableOffset = 8;
            emitter.emitCodeBlock = buffer;

            var descriptor = TestEmitter.Constant(32);
            descriptor.idSetIsCnsReloc();
            descriptor.idIns(INS_i32_const_dataoffs);
            descriptor.idInsFmt(Emitter.insFormat.IF_WASM_DATAOFFS);
            var destination = buffer + 2;
            var next = destination;
            _ = EmitOutputInstrWasm(emitter, new insGroup(), descriptor, &next);

            Assert.That(callbacks.Relocations, Is.EqualTo(1));
            Assert.That(callbacks.RelocationType, Is.EqualTo(expectedRelocation));
            Assert.That((nuint)callbacks.Target, Is.EqualTo((nuint)(data + 32)));
        });
    }

    [TestCase(true, 1, INS_i32_const_address)]
    [TestCase(false, 3, INS_global_get)]
    public static void AddressConstantUsesAnImageBaseOnlyForReadyToRun(
        bool nativeAot, int expectedInstructionCount, instruction expectedFirstInstruction)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getWasmWellKnownGlobals = &GetWasmWellKnownGlobals;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            ConfigureAddressModel(compiler, nativeAot);

            emitter.emitAddressConstant(0x1234);

            var descriptors = CurrentDescriptors(emitter)
                ?? throw new AssertionException("Missing Wasm emitter instruction buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(expectedInstructionCount));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expectedFirstInstruction));
            if (!nativeAot)
            {
                Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_i32_const_address));
                Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_i32_add));
            }
        });
    }

    [TestCase(true, 1, INS_i32_const_dataoffs)]
    [TestCase(false, 3, INS_global_get)]
    public static void DataOffsetConstantUsesAnImageBaseOnlyForReadyToRun(
        bool nativeAot, int expectedInstructionCount, instruction expectedFirstInstruction)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getWasmWellKnownGlobals = &GetWasmWellKnownGlobals;
            var jitInfo = new ICorJitInfo { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            ConfigureAddressModel(compiler, nativeAot);

            emitter.emitDataOffsetConstant(32);

            var descriptors = CurrentDescriptors(emitter)
                ?? throw new AssertionException("Missing Wasm emitter instruction buffer.");
            Assert.That(descriptors, Has.Count.EqualTo(expectedInstructionCount));
            Assert.That(descriptors[0].idIns(), Is.EqualTo(expectedFirstInstruction));
            if (!nativeAot)
            {
                Assert.That(descriptors[1].idIns(), Is.EqualTo(INS_i32_const_dataoffs));
                Assert.That(descriptors[2].idIns(), Is.EqualTo(INS_i32_add));
            }
        });
    }

    [Test]
    public static void FloatingDiagnosticsUseInvariantCrtFormatting()
    {
        WithEmitter((_, emitter) =>
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

                AssertFloatingDiagnostic(emitter, 1.25, "1.250000");
                AssertFloatingDiagnostic(emitter, double.PositiveInfinity, "inf");
                AssertFloatingDiagnostic(emitter, double.NegativeInfinity, "-inf");

                var nan = BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000000));
                AssertFloatingDiagnostic(emitter, nan, OperatingSystem.IsWindows() ? "-nan(ind)" : "-nan");
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        });
    }

    [Test]
    public static void FixedRegisterZeroingFailsExplicitlyOnWasm()
    {
        WithCodeGen((_, codeGen) =>
        {
            var failure = Assert.Throws<FatalJitException>(
                () => codeGen.instGen_Set_Reg_To_Zero(Globals.EA_PTRSIZE, Globals.REG_NA));

            Assert.That(
                failure?.ErrorCode,
                Is.EqualTo(FatalJitErrorCode.RegisterZeroingRequiresAFixedRegisterSet));
            Assert.That(
                failure?.Message,
                Is.EqualTo(
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? "Register zeroing requires a fixed register set."
                        : "RJS-0523"));
        });
    }

    [Test]
    public static void PrologZeroInitializationUsesMemoryFill()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            SetUseBlockInit(codeGen, true);
            compiler.compFuncInfos = [new FuncInfoDsc
            {
                funFramePointerReg = regNumberExtensions.MakeWasmReg(0, WasmValueType.I),
            }];
            compiler.compFuncInfoCount = 1;
            compiler.compCurrFuncIdx = 0;

            var group = codeGen.Emitter.emitCurIG ??
                throw new AssertionException("Missing current instruction group.");
            group.igFlags |= InsGroupFlags.Prolog;

            var descriptors = CurrentDescriptors(codeGen.Emitter) ??
                throw new AssertionException("Missing Wasm instruction descriptors.");
            var initialCount = descriptors.Count;
            var zeroed = false;
            codeGen.genZeroInitFrame(40, 8, Globals.REG_NA, ref zeroed);

            var pointerConst = Configuration.TARGET_64BIT ? INS_i64_const : INS_i32_const;
            var pointerAdd = Configuration.TARGET_64BIT ? INS_i64_add : INS_i32_add;
            var expected = new instruction[]
            {
                INS_local_get, pointerConst, pointerAdd, INS_i32_const, pointerConst, INS_memory_fill,
            };
            var actual = new instruction[descriptors.Count - initialCount];
            for (var index = 0; index < actual.Length; index++)
            {
                actual[index] = descriptors[initialCount + index].idIns();
            }

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(zeroed, Is.False);
        });
    }

    [Test]
    public static void NativeRegisterZeroingFailsExplicitlyOnWasm()
    {
        WithCodeGen((_, codeGen) =>
        {
            var zeroed = false;
            var zeroRegisterFailure = Assert.Throws<FatalJitException>(
                () => codeGen.genGetZeroReg(Globals.REG_NA, ref zeroed));
            var floatingRegistersFailure = Assert.Throws<FatalJitException>(
                () => codeGen.genZeroInitFltRegs(default, default, Globals.REG_NA));

            Assert.That(
                zeroRegisterFailure?.ErrorCode,
                Is.EqualTo(FatalJitErrorCode.WasmHasNoArchitecturalIntegerZeroRegister));
            Assert.That(
                zeroRegisterFailure?.Message,
                Is.EqualTo(
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? "Wasm has no architectural integer zero register."
                        : "RJS-0685"));
            Assert.That(
                floatingRegistersFailure?.ErrorCode,
                Is.EqualTo(FatalJitErrorCode.WasmHasNoArchitecturalFloatingPointRegisters));
            Assert.That(
                floatingRegistersFailure?.Message,
                Is.EqualTo(
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? "Wasm has no architectural floating-point registers."
                        : "RJS-0686"));
        });
    }

    [Test]
    public static void NativeGenericContextEmissionAdaptersFailExplicitlyOnWasm()
    {
        WithCodeGen((_, codeGen) =>
        {
            var loadMethod = typeof(CodeGen).GetMethod(
                "genLoadGenericContextArgFromStack",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertionException("Native generic-context loading is unavailable.");
            var storeMethod = typeof(CodeGen).GetMethod(
                "genStoreGenericContextArgXarch",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new AssertionException("xarch generic-context storage is unavailable.");

            var loadFailure = Assert.Throws<TargetInvocationException>(
                () => loadMethod.Invoke(codeGen, [Globals.REG_NA, 0]));
            var storeFailure = Assert.Throws<TargetInvocationException>(
                () => storeMethod.Invoke(codeGen, [Globals.REG_NA, (nint)0]));

            Assert.That(loadFailure?.InnerException, Is.TypeOf<FatalJitException>());
            var loadException = (FatalJitException)loadFailure!.InnerException!;
            Assert.That(loadException.ErrorCode, Is.EqualTo(FatalJitErrorCode.NativeGenericContextLoadingIsUnavailableOnWasm));
            Assert.That(
                loadException.Message,
                Is.EqualTo(
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? "Native generic-context loading is unavailable on Wasm."
                        : "RJS-0695"));
            Assert.That(storeFailure?.InnerException, Is.TypeOf<FatalJitException>());
            var storeException = (FatalJitException)storeFailure!.InnerException!;
            Assert.That(storeException.ErrorCode, Is.EqualTo(FatalJitErrorCode.XarchGenericContextStorageIsUnavailable));
            Assert.That(
                storeException.Message,
                Is.EqualTo(
                    Configuration.DETAILED_FATAL_ERROR_MESSAGES
                        ? "xarch generic-context storage is unavailable."
                        : "RJS-0696"));
        });
    }

    [TestCase(GT_EQ, TYP_INT, false, false)]
    [TestCase(GT_EQ, TYP_INT, true, false)]
    [TestCase(GT_NE, TYP_INT, false, false)]
    [TestCase(GT_NE, TYP_INT, false, true)]
    [TestCase(GT_EQ, TYP_LONG, false, false)]
    [TestCase(GT_NE, TYP_LONG, true, true)]
    public static void ComparisonAgainstZeroUsesWasmEqz(
        genTreeOps oper, var_types type, bool zeroOnFirst, bool isBranch)
    {
        WithCodeGen((compiler, codeGen) =>
        {
            var emitter = codeGen.Emitter;
            var descriptors = CurrentDescriptors(emitter) ??
                throw new AssertionException("Missing Wasm instruction descriptors.");
            var firstDescriptor = descriptors.Count;
            var constantInstruction = type is TYP_INT ? INS_i32_const : INS_i64_const;
            emitter.emitIns_I(constantInstruction, type.EmitSize, 1);

            var zero = compiler.gtNewIconNode(type, 0);
            zero.IsContained = true;
            var value = compiler.gtNewIconNode(type, 1);
            value.IsContained = true;
            var comparison = compiler.gtNewBinaryNode(oper, TYP_INT,
                zeroOnFirst ? zero : value, zeroOnFirst ? value : zero).AsOp();
            comparison.RegNum = Globals.REG_NA;
            if (isBranch)
            {
                comparison.Flags |= GTF_RELOP_JMP_USED;
            }

            GenCompareInt(codeGen, comparison);

            var expected = new List<instruction> { constantInstruction };
            if (oper is GT_EQ)
            {
                expected.Add(type is TYP_LONG ? INS_i64_eqz : INS_i32_eqz);
            }
            else if (!isBranch || (type is not TYP_INT))
            {
                expected.Add(type is TYP_LONG ? INS_i64_eqz : INS_i32_eqz);
                expected.Add(INS_i32_eqz);
            }

            var actual = new instruction[descriptors.Count - firstDescriptor];
            for (var index = 0; index < actual.Length; index++)
            {
                actual[index] = descriptors[firstDescriptor + index].idIns();
            }

            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    [Test]
    public static void NativeAotTryIntervalsEmitPlainBlocks()
    {
        WithCodeGen((compiler, codeGen) =>
        {
            compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
            ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
            vtable.Base.Base.getEEInfo = &GetNativeAotEEInfo;
            ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
            compiler.info.compCompHnd = &jitInfo;
            Assert.That(compiler.IsNativeAot, Is.True);
            var start = new BasicBlock(null, null) { bbPreorderNum = 0 };
            var end = new BasicBlock(null, null) { bbPreorderNum = 1 };
            compiler.fgIndexToBlockMap = [start, end];
            compiler.fgWasmIntervals = [new WasmInterval(0, 1, WasmInterval.Kind.Try)];
            WasmControlFlowStack(codeGen) = new ArrayStack<WasmInterval>();

            EmitStartBlockWasm(codeGen, start);

            var emittedBlock = false;
            var emittedTryTable = false;
            var emittedInstructions = new List<instruction>();
            for (var group = FirstInstructionGroup(codeGen.Emitter); group is not null; group = group.igNext)
            {
                var descriptors = group.igData;
                if (descriptors is null)
                {
                    continue;
                }

                foreach (var descriptor in descriptors)
                {
                    emittedInstructions.Add(descriptor.idIns());
                }
            }

            var currentDescriptors = CurrentDescriptors(codeGen.Emitter);
            if (currentDescriptors is not null)
            {
                foreach (var descriptor in currentDescriptors)
                {
                    emittedInstructions.Add(descriptor.idIns());
                }
            }

            foreach (var instruction in emittedInstructions)
            {
                emittedBlock |= instruction is INS_block;
                emittedTryTable |= instruction is INS_try_table;
            }

            Assert.That(emittedBlock, Is.True, $"Emitted instructions: {string.Join(", ", emittedInstructions)}");
            Assert.That(emittedTryTable, Is.False);
        });
    }

    [Test]
    public static void EmitterUnitTestsExerciseAllWasmSimdInstructionFormats()
    {
        Assume.That(TestConfiguration.IsDebug);

        WithEmitter((compiler, _) =>
        {
            if (compiler.codeGen is not CodeGen codeGen)
            {
                throw new InvalidOperationException("Compiler has no code generator.");
            }

            var emitter = codeGen.Emitter;
            emitter.emitBegCG(compiler, default);
            compiler.compCurBB = new BasicBlock(null, null);
            var previousWriter = Globals.s_jitstdout;
            using var stream = new MemoryStream();
            using var writer = new JitTextWriter(stream, leaveOpen: true);
            try
            {
                Globals.s_jitstdout = writer;
                TestConfiguration.SetDspCode(compiler, true);
                emitter.emitBegFN(false, false);

                EmitWasmEmitterTestSkipBlock(codeGen);
                EmitWasmEmitterUnitTestsSimd(codeGen);
                emitter.emitIns(INS_end);

                writer.Flush();
                var diagnostic = Encoding.UTF8.GetString(stream.ToArray());
                Assert.That(diagnostic, Does.Contain("block"));
                Assert.That(diagnostic, Does.Contain("br"));
                Assert.That(diagnostic, Does.Contain("v128.const"));
                Assert.That(diagnostic, Does.Contain("i8x16.shuffle"));
                Assert.That(diagnostic, Does.Contain("v128.load8_lane"));
                Assert.That(diagnostic, Does.Contain("v128.store64_lane"));
                Assert.That(diagnostic, Does.Contain("i32x4.trunc_sat_s_f32x4"));
                Assert.That(diagnostic, Does.Contain("end"));
            }
            finally
            {
                Globals.s_jitstdout = previousWriter;
            }
        });
    }

    private static void AssertFloatingDiagnostic(Emitter emitter, double value, string expected)
    {
        var previousWriter = Globals.s_jitstdout;
        using var stream = new MemoryStream();
        using var writer = new JitTextWriter(stream, leaveOpen: true);
        try
        {
            Globals.s_jitstdout = writer;
            emitter.emitDispIns(TestEmitter.FloatConstant(value), true, false, false);
            writer.Flush();
            Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Does.Contain($" {expected}"));
        }
        finally
        {
            Globals.s_jitstdout = previousWriter;
        }
    }

    private static void ConfigureAddressModel(Compiler compiler, bool nativeAot)
    {
        compiler.eeInfoInitialized = true;
        compiler.eeInfo.targetAbi = nativeAot
            ? CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI
            : CORINFO_RUNTIME_ABI.CORINFO_CORECLR_ABI;
        compiler.opts.jitFlags->Set(JitFlags.JIT_FLAG_AOT);
        compiler.opts.compReloc = true;
    }

    private static void AssertMemoryAddressRelocation(
        Emitter emitter,
        byte* buffer,
        instruction ins,
        Emitter.insFormat format)
    {
        var descriptor = TestEmitter.Constant(0x1234);
        descriptor.idSetIsCnsReloc();
        descriptor.idIns(ins);
        descriptor.idInsFmt(format);
        var destination = buffer + 2;
        var next = destination;
        _ = EmitOutputInstrWasm(emitter, new insGroup(), descriptor, &next);
    }

    private static void WithEmitter(Action<Compiler, TestEmitter> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            var emitter = new TestEmitter(codeGen);
            emitter.emitBegCG(compiler, default);
            action(compiler, emitter);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    private static void WithCodeGen(Action<Compiler, CodeGen> action)
    {
        using var tls = TestConfiguration.CreateJitTls();
        var previousCompiler = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        JitFlags flags = default;
        compiler.opts.jitFlags = &flags;
        compiler.SetMinOpts(true);
        compiler.lvaTable = [new LclVarDsc { Type = TYP_INT }];
        compiler.lvaCount = 1;
        compiler.compCurLife = VarSetOps.MakeEmpty(compiler);
        compiler.fgNodeThreading = NodeThreading.LIR;
        compiler.compCurBB = new BasicBlock(null, null);
        JitTls.Compiler = compiler;

        try
        {
            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.genPrepForCompiler();
            codeGen.Emitter.emitBegCG(compiler, default);
            codeGen.Emitter.emitBegFN(false, false);
            action(compiler, codeGen);
        }
        finally
        {
            JitTls.Compiler = previousCompiler;
        }
    }

    private struct CallbackContext
    {
        public ICorJitInfo JitInfo;
        public int Relocations;
        public void* Location;
        public void* WritableLocation;
        public void* Target;
        public CorInfoReloc RelocationType;
        public int AdditionalDelta;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void RecordRelocation(ICorJitInfo* self, void* location, void* locationRW, void* target,
        CorInfoReloc relocationType, int additionalDelta)
    {
        var callbacks = (CallbackContext*)self;
        callbacks->Relocations++;
        callbacks->Location = location;
        callbacks->WritableLocation = locationRW;
        callbacks->Target = target;
        callbacks->RelocationType = relocationType;
        callbacks->AdditionalDelta = additionalDelta;

        var writable = (byte*)locationRW;
        var value = unchecked((uint)additionalDelta);
        for (var index = 0; index < 4; index++)
        {
            writable[index] = unchecked((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        writable[4] = unchecked((byte)value);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitOutputConstantFunclet")]
    private static extern nuint EmitOutputConstantFunclet(Emitter emitter, byte* destination,
        Emitter.instrDesc descriptor, CorInfoReloc relocationType);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "emitOutputInstrWasm")]
    private static extern nuint EmitOutputInstrWasm(Emitter emitter, insGroup group,
        Emitter.instrDesc descriptor, byte** destination);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_UseBlockInit")]
    private static extern void SetUseBlockInit(CodeGen codeGen, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitIGlist")]
    private static extern ref insGroup? FirstInstructionGroup(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genCompareInt")]
    private static extern void GenCompareInt(CodeGen codeGen, GenTreeOp comparison);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genEmitStartBlockWasm")]
    private static extern void EmitStartBlockWasm(CodeGen codeGen, BasicBlock block);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "wasmControlFlowStack")]
    private static extern ref ArrayStack<WasmInterval>? WasmControlFlowStack(CodeGen codeGen);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetNativeAotEEInfo(ICorJitInfo* self, CORINFO_EE_INFO* info)
    {
        *info = default;
        info->targetAbi = CORINFO_RUNTIME_ABI.CORINFO_NATIVEAOT_ABI;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static void GetWasmWellKnownGlobals(
        ICorJitInfo* self, CORINFO_WASM_WELLKNOWN_GLOBALS* result)
    {
        _ = self;
        *result = default;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genCodeForIndir")]
    private static extern void GenCodeForIndir(CodeGen codeGen, GenTreeIndir indir);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genEmitWasmEmitterTestSkipBlock")]
    private static extern void EmitWasmEmitterTestSkipBlock(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genWasmEmitterUnitTestsSimd")]
    private static extern void EmitWasmEmitterUnitTestsSimd(CodeGen codeGen);

    private sealed class TestEmitter(CodeGen codeGen) : Emitter(codeGen)
    {
        internal static Emitter.instrDesc Constant(nint value)
        {
            var descriptor = new instrDescCns { idcCnsVal = value };
            descriptor.idSetIsLargeCns();
            return descriptor;
        }

        internal static Emitter.instrDesc FloatConstant(double value)
        {
            var descriptor = new instrDescCns
            {
                idcCnsVal = unchecked((nint)BitConverter.DoubleToInt64Bits(value)),
            };
            descriptor.idSetIsLargeCns();
            descriptor.idIns(INS_f64_const);
            descriptor.idInsFmt(insFormat.IF_WASM_F64);
            return descriptor;
        }
    }
}
