// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genLoadLocalIntoReg(regNumber reg, int localNumber)
    {
        ref var local = ref _compiler.lvaGetDesc(localNumber);
        var type = local.GetRegisterType();

        GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
        GetEmitter().emitIns_S(ins_Load(type), type.EmitActualSize, localNumber, 0);
        GetEmitter().emitIns_I(
            INS_local_set, type.EmitActualSize,
            unchecked((nint)regNumberExtensions.WasmRegToIndex(reg)));
    }
}
#endif
