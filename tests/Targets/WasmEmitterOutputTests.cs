// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root.

#if TARGET_WASM
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
                () => codeGen.instGen_Set_Reg_To_Zero(EA_PTRSIZE, REG_NA));

            Assert.That(failure?.Message, Does.Contain("requires a fixed register set"));
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
            codeGen.genZeroInitFrame(40, 8, REG_NA, ref zeroed);

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
                () => codeGen.genGetZeroReg(REG_NA, ref zeroed));
            var floatingRegistersFailure = Assert.Throws<FatalJitException>(
                () => codeGen.genZeroInitFltRegs(default, default, REG_NA));

            Assert.That(zeroRegisterFailure?.Message, Does.Contain("no architectural integer zero register"));
            Assert.That(floatingRegistersFailure?.Message, Does.Contain("no architectural floating-point registers"));
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
                () => loadMethod.Invoke(codeGen, [REG_NA, 0]));
            var storeFailure = Assert.Throws<TargetInvocationException>(
                () => storeMethod.Invoke(codeGen, [REG_NA, (nint)0]));

            Assert.That(loadFailure?.InnerException, Is.TypeOf<FatalJitException>());
            Assert.That(loadFailure?.InnerException?.Message,
                Does.Contain("Native generic-context loading is unavailable on Wasm."));
            Assert.That(storeFailure?.InnerException, Is.TypeOf<FatalJitException>());
            Assert.That(storeFailure?.InnerException?.Message,
                Does.Contain("xarch generic-context storage is unavailable."));
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
            comparison.RegNum = REG_NA;
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

#if DEBUG
    [Test]
    public static void EmitterUnitTestsExerciseAllWasmSimdInstructionFormats()
    {
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
                compiler.opts.dspCode = true;
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
#endif

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
        compiler.opts.SetMinOpts(true);
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
            codeGen.Emitter.emitBegFN(false
#if DEBUG
                , false
#endif
                );
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

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "emitCurIGfreeBase")]
    private static extern ref List<Emitter.instrDesc>? CurrentDescriptors(Emitter emitter);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_UseBlockInit")]
    private static extern void SetUseBlockInit(CodeGen codeGen, bool value);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genCompareInt")]
    private static extern void GenCompareInt(CodeGen codeGen, GenTreeOp comparison);

#if DEBUG
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genEmitWasmEmitterTestSkipBlock")]
    private static extern void EmitWasmEmitterTestSkipBlock(CodeGen codeGen);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "genWasmEmitterUnitTestsSimd")]
    private static extern void EmitWasmEmitterUnitTestsSimd(CodeGen codeGen);
#endif

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
            descriptor.idInsFmt(insFormat.IF_F64);
            return descriptor;
        }
    }
}
#endif
