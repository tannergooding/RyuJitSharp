// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT).
// See License.md in the repository root.

#if TARGET_WASM
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using static RyuJitSharp.instruction;

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
#if DEBUG
        using var tls = new JitTls(null);
#endif
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
