// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

#if TARGET_WASM
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoWasmType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmClassifierTests
{
    [TestCase(CORINFO_WASM_TYPE_I32, TYP_INT)]
    [TestCase(CORINFO_WASM_TYPE_I64, TYP_LONG)]
    [TestCase(CORINFO_WASM_TYPE_F32, TYP_FLOAT)]
    [TestCase(CORINFO_WASM_TYPE_F64, TYP_DOUBLE)]
    [TestCase(CORINFO_WASM_TYPE_V128, TYP_SIMD16)]
    public static void WasmTypesMapToTheirJitTypes(CorInfoWasmType wasmType, var_types jitType)
    {
        Assert.That(WasmClassifier.ToJitType(wasmType), Is.EqualTo(jitType));
    }

    [TestCase(TYP_INT, WasmValueType.I32)]
    [TestCase(TYP_LONG, WasmValueType.I64)]
    [TestCase(TYP_FLOAT, WasmValueType.F32)]
    [TestCase(TYP_DOUBLE, WasmValueType.F64)]
    [TestCase(TYP_REF, WasmValueType.I)]
    [TestCase(TYP_BYREF, WasmValueType.I)]
    [TestCase(TYP_SIMD8, WasmValueType.V128)]
    [TestCase(TYP_SIMD12, WasmValueType.V128)]
    [TestCase(TYP_SIMD16, WasmValueType.V128)]
    public static void JitTypesMapToTheirWasmTypes(var_types type, WasmValueType wasmType)
    {
        Assert.That(regNumberExtensions.TypeToWasmValueType(type), Is.EqualTo(wasmType));
    }

    [TestCase(0u, WasmValueType.I32)]
    [TestCase(19u, WasmValueType.I64)]
    [TestCase(100_000u, WasmValueType.V128)]
    public static void FramePointerRegisterIndexUsesTheWasmLocalIndex(uint index, WasmValueType type)
    {
        WithCompiler(compiler => {
            compiler.compFuncInfos = [new FuncInfoDsc {
                funFramePointerReg = regNumberExtensions.MakeWasmReg(index, type),
            }];
            compiler.compFuncInfoCount = 1;
            compiler.compCurrFuncIdx = 0;

            var codeGen = new CodeGen(compiler);

            Assert.That(codeGen.GetFramePointerRegIndex(), Is.EqualTo(index));
        });
    }

    [TestCase(TYP_BYTE, WasmValueType.I32)]
    [TestCase(TYP_UBYTE, WasmValueType.I32)]
    [TestCase(TYP_SHORT, WasmValueType.I32)]
    [TestCase(TYP_USHORT, WasmValueType.I32)]
    [TestCase(TYP_INT, WasmValueType.I32)]
    [TestCase(TYP_LONG, WasmValueType.I64)]
    [TestCase(TYP_FLOAT, WasmValueType.F32)]
    [TestCase(TYP_DOUBLE, WasmValueType.F64)]
    [TestCase(TYP_REF, WasmValueType.I)]
    [TestCase(TYP_BYREF, WasmValueType.I)]
    [TestCase(TYP_SIMD8, WasmValueType.V128)]
    [TestCase(TYP_SIMD12, WasmValueType.V128)]
    [TestCase(TYP_SIMD16, WasmValueType.V128)]
    public static void ActualJitTypesMapToTheirWasmTypes(var_types type, WasmValueType wasmType)
    {
        Assert.That(regNumberExtensions.ActualTypeToWasmValueType(type), Is.EqualTo(wasmType));
    }

    [Test]
    public static void PackedRegistersPreserveLargeLocalIndicesAndTypes()
    {
        const uint index = 100_000;
        var register = regNumberExtensions.MakeWasmReg(index, WasmValueType.F64);

        Assert.That((uint)register, Is.EqualTo((4u << 29) | index));
        Assert.That(regNumberExtensions.WasmRegToIndex(register), Is.EqualTo(index));
        Assert.That(regNumberExtensions.WasmRegToType(register), Is.EqualTo(WasmValueType.F64));
        Assert.That(regNumberExtensions.IsValidWasmReg(register), Is.True);
        Assert.That(regNumberExtensions.GetWasmRegName(register), Is.EqualTo("$<too large to print>"));
        Assert.That(regNumberExtensions.GetWasmRegName(REG_NA), Is.EqualTo("NA"));
    }

    [TestCase(WasmValueType.I32, true, false)]
    [TestCase(WasmValueType.I64, true, false)]
    [TestCase(WasmValueType.F32, false, true)]
    [TestCase(WasmValueType.F64, false, true)]
    [TestCase(WasmValueType.V128, false, true)]
    [TestCase(WasmValueType.ExnRef, false, false)]
    public static void RegisterValidityPredicatesMatchTheirWasmTypes(
        WasmValueType type, bool isInteger, bool isFloat)
    {
        var register = regNumberExtensions.MakeWasmReg(1, type);

        Assert.That(genIsValidReg(register), Is.True);
        Assert.That(genIsValidIntReg(register), Is.EqualTo(isInteger));
        Assert.That(genIsValidIntOrFakeReg(register), Is.EqualTo(isInteger));
        Assert.That(genIsValidFloatReg(register), Is.EqualTo(isFloat));
        Assert.That(regNumberExtensions.IsValidWasmReg(register), Is.True);
        Assert.That(regNumberExtensions.IsValidWasmIntReg(register), Is.EqualTo(isInteger));
        Assert.That(regNumberExtensions.IsValidWasmFloatReg(register), Is.EqualTo(isFloat));
    }

    [TestCase(WasmValueType.Invalid)]
    [TestCase(WasmValueType.Count)]
    public static void InvalidPackedTypesAreNotValidRegisters(WasmValueType type)
    {
        var register = (regNumber)(((uint)type << 29) | 5u);

        Assert.That(genIsValidReg(register), Is.False);
        Assert.That(genIsValidIntReg(register), Is.False);
        Assert.That(genIsValidFloatReg(register), Is.False);
        Assert.That(regNumberExtensions.IsValidWasmReg(register), Is.False);
        Assert.That(regNumberExtensions.IsValidWasmIntReg(register), Is.False);
        Assert.That(regNumberExtensions.IsValidWasmFloatReg(register), Is.False);
        Assert.That(regNumberExtensions.GetWasmRegName(register), Is.EqualTo("$5"));
    }

    [TestCase(0u, "$0")]
    [TestCase(19u, "$19")]
    [TestCase(20u, "$<too large to print>")]
    public static void SmallWasmLocalsUseTheirNativeRegisterNames(uint index, string expectedName)
    {
        var register = regNumberExtensions.MakeWasmReg(index, WasmValueType.I32);

        Assert.That(regNumberExtensions.GetWasmRegName(register), Is.EqualTo(expectedName));
    }

    [TestCase(TYP_BYTE, 1, WasmValueType.I32)]
    [TestCase(TYP_SHORT, 2, WasmValueType.I32)]
    [TestCase(TYP_INT, 4, WasmValueType.I32)]
    [TestCase(TYP_LONG, 8, WasmValueType.I64)]
    [TestCase(TYP_FLOAT, 4, WasmValueType.F32)]
    [TestCase(TYP_DOUBLE, 8, WasmValueType.F64)]
    [TestCase(TYP_REF, 4, WasmValueType.I32)]
    [TestCase(TYP_BYREF, 4, WasmValueType.I32)]
    [TestCase(TYP_SIMD8, 8, WasmValueType.V128)]
    [TestCase(TYP_SIMD12, 12, WasmValueType.V128)]
    [TestCase(TYP_SIMD16, 16, WasmValueType.V128)]
    public static void ArgumentsUseActualWasmTypesAndOriginalSizes(
        var_types type, int size, WasmValueType wasmType)
    {
        WithCompiler(compiler => {
            var classifier = new WasmClassifier(new ClassifierInfo());
            var layout = varTypeIsStruct(type) ? NewLayout((uint)size, CORINFO_WASM_TYPE_V128) : null;
            var info = classifier.Classify(compiler, type, layout, WellKnownArg.None);

            Assert.That(info.IsPassedByReference, Is.False);
            Assert.That(info.NumSegments, Is.EqualTo(1));
            Assert.That(info.Segments[0].Size, Is.EqualTo(size));
            Assert.That(regNumberExtensions.WasmRegToType(info.Segments[0].Register), Is.EqualTo(wasmType));
            Assert.That(regNumberExtensions.WasmRegToIndex(info.Segments[0].Register), Is.Zero);
        });
    }

    [Test]
    public static void StructsUseTheLoweredTypeAndRetainTheOriginalSegmentSize()
    {
        WithCompiler(compiler => {
            var classifier = new WasmClassifier(new ClassifierInfo());
            var layout = NewLayout(2, CORINFO_WASM_TYPE_I32);
            var info = classifier.Classify(compiler, TYP_STRUCT, layout, WellKnownArg.None);

            Assert.That(info.IsPassedByReference, Is.False);
            Assert.That(info.NumSegments, Is.EqualTo(1));
            Assert.That(regNumberExtensions.WasmRegToType(info.Segments[0].Register), Is.EqualTo(WasmValueType.I32));
            Assert.That(info.Segments[0].Size, Is.EqualTo(2));
            Assert.That(regNumberExtensions.WasmRegToIndex(info.Segments[0].Register), Is.Zero);
        });
    }

    [Test]
    public static void WiderStructsUseConsecutiveLoweredSegments()
    {
        WithCompiler(compiler => {
            var classifier = new WasmClassifier(new ClassifierInfo());
            var info = classifier.Classify(
                compiler, TYP_STRUCT, NewLayout(16, CORINFO_WASM_TYPE_I64), WellKnownArg.None);

            Assert.That(info.IsPassedByReference, Is.False);
            Assert.That(info.NumSegments, Is.EqualTo(2));
            AssertSegment(info, 0, 0, 8, 0);
            AssertSegment(info, 1, 8, 8, 1);
        });
    }

    [Test]
    public static void UnlowerableStructUsesAnImplicitByReferencePointer()
    {
        WithCompiler(compiler => {
            var classifier = new WasmClassifier(new ClassifierInfo());
            var info = classifier.Classify(
                compiler, TYP_STRUCT, NewLayout(24, CORINFO_WASM_TYPE_VOID), WellKnownArg.None);

            Assert.That(info.IsPassedByReference, Is.True);
            Assert.That(info.NumSegments, Is.EqualTo(1));
            Assert.That(info.Segments[0].Size, Is.EqualTo(TARGET_POINTER_SIZE));
            Assert.That(regNumberExtensions.WasmRegToType(info.Segments[0].Register),
                Is.EqualTo(WasmValueType.I));
            Assert.That(regNumberExtensions.WasmRegToIndex(info.Segments[0].Register), Is.Zero);
        });
    }

    private static void AssertSegment(
        AbiPassingInformation info, int segmentIndex, int offset, int size, uint localIndex)
    {
        var segment = info.Segments[segmentIndex];
        Assert.That(segment.Offset, Is.EqualTo(offset));
        Assert.That(segment.Size, Is.EqualTo(size));
        Assert.That(regNumberExtensions.WasmRegToType(segment.Register), Is.EqualTo(WasmValueType.I64));
        Assert.That(regNumberExtensions.WasmRegToIndex(segment.Register), Is.EqualTo(localIndex));
    }

    private static ClassLayout NewLayout(uint size, CorInfoWasmType wasmType)
    {
        var handle = (CORINFO_CLASS_STRUCT_*)(nuint)wasmType;
        return new ClassLayout(handle, true, size, TYP_STRUCT, "WasmStruct", "WasmStruct");
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getWasmLowering = &GetWasmLowering;
        ICorJitInfo jitInfo = new() { lpVtbl = &vtable };
#if DEBUG
        using var tls = new JitTls(&jitInfo);
#endif
        var previous = JitTls.Compiler;
        var compiler = (Compiler)RuntimeHelpers.GetUninitializedObject(typeof(Compiler));
        compiler.info.compCompHnd = &jitInfo;
        JitTls.Compiler = compiler;

        try
        {
            action(compiler);
        }
        finally
        {
            JitTls.Compiler = previous;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoWasmType GetWasmLowering(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* classHandle)
    {
        return (CorInfoWasmType)(nuint)classHandle;
    }
}
#endif
