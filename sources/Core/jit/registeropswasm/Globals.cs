// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public static partial class Globals
{
    private static class WasmValueTypeNameData
    {
        internal static readonly string[] Names = [
            "Invalid",
            "i32",
            "i64",
            "f32",
            "f64",
            "v128",
            "exnref",
        ];
    }

    public static string WasmValueTypeName(WasmValueType type)
    {
        Configuration.EnsureWasmTarget();
        var names = WasmValueTypeNameData.Names;
        assert((uint)names.Length == (uint)WasmValueType.Count);
        assert((type >= WasmValueType.Invalid) && (type < WasmValueType.Count));
        return names[(uint)type];
    }
}
