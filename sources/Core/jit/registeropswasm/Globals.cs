// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public static partial class Globals
{
    private static readonly string[] s_wasmValueTypeNames = [
        "Invalid",
        "i32",
        "i64",
        "f32",
        "f64",
        "v128",
        "exnref",
    ];

    public static string WasmValueTypeName(WasmValueType type)
    {
        assert((uint)s_wasmValueTypeNames.Length == (uint)WasmValueType.Count);
        assert((type >= WasmValueType.Invalid) && (type < WasmValueType.Count));
        return s_wasmValueTypeNames[(uint)type];
    }

    internal static ref WasmValueType IncrementWasmValueType(ref WasmValueType type)
    {
        type = unchecked((WasmValueType)((uint)type + 1));
        return ref type;
    }
}
#endif
