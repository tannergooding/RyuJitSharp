#if TARGET_WASM
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using static RyuJitSharp.CorInfoWasmType;
using static RyuJitSharp.Globals;
using static RyuJitSharp.genTreeOps;
using static RyuJitSharp.regNumber;
using static RyuJitSharp.var_types;

namespace RyuJitSharp.UnitTests;

internal static unsafe class WasmGenStructReturnTests
{
    [Test]
    public static void StructReturnConsumesItsSingleField()
    {
        WithCompiler(compiler =>
        {
            compiler.info.compCallConv = CorInfoCallConvExtension.Managed;
            compiler.compRetTypeDesc.InitializeReturnType(
                compiler, TYP_STRUCT, (CORINFO_CLASS_STRUCT_*)(nuint)CORINFO_WASM_TYPE_I32,
                compiler.info.compCallConv);

            var value = new GenTreeIntCon(TYP_INT, 42);
            var fields = new GenTreeFieldList();
            fields.AddField(compiler, value, 0, TYP_INT);
            var tree = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, fields);

            var codeGen = new CodeGen(compiler);
            compiler.codeGen = codeGen;
            codeGen.genPrepForCompiler();
            codeGen.genStructReturn(tree);
        });
    }

    [Test]
    public static void StructReturnWithoutAFieldListTerminatesAtTheUnsupportedBoundary()
    {
        WithCompiler(compiler =>
        {
            compiler.info.compCallConv = CorInfoCallConvExtension.Managed;
            compiler.compRetTypeDesc.InitializeReturnType(
                compiler, TYP_STRUCT, (CORINFO_CLASS_STRUCT_*)(nuint)CORINFO_WASM_TYPE_I32,
                compiler.info.compCallConv);

            var tree = new GenTreeUnOp(GT_RETURN, TYP_STRUCT, new GenTreeIntCon(TYP_INT, 42));
            var codeGen = new CodeGen(compiler);

            var failure = Assert.Throws<FatalJitException>(() => codeGen.genStructReturn(tree)) ??
                throw new AssertionException("Missing Wasm struct-return dependency failure.");

            Assert.That(failure.Message,
                Is.EqualTo("Wasm genStructReturn non-fieldlist cases is not ported for GT_RETURN."));
        });
    }

    [Test]
    public static void StructReturnUsesAHiddenBufferForLargerAggregates()
    {
        WithCompiler(compiler =>
        {
            var classHandle = (CORINFO_CLASS_STRUCT_*)(nuint)CORINFO_WASM_TYPE_I32;
            var type = compiler.GetReturnTypeForStruct(classHandle, CorInfoCallConvExtension.Managed,
                out var kind, structSize: 16);

            Assert.That(type, Is.EqualTo(TYP_UNKNOWN));
            Assert.That(kind, Is.EqualTo(Compiler.SPK_ByReference));
        });
    }

    private static void WithCompiler(Action<Compiler> action)
    {
        ICorJitInfo.Vtbl<ICorJitInfo> vtable = default;
        vtable.Base.Base.getClassSize = &GetClassSize;
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
    private static int GetClassSize(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* classHandle)
    {
        return sizeof(int);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
    private static CorInfoWasmType GetWasmLowering(ICorJitInfo* self, CORINFO_CLASS_STRUCT_* classHandle)
    {
        return CORINFO_WASM_TYPE_I32;
    }
}
#endif
