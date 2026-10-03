// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void WasmProduceReg(GenTree node)
    {
        assert(!genIsRegCandidateLocal(node)); // Candidate liveness is handled by genConsumeReg.

        var reg = node.RegNum;
        if (genIsValidReg(reg))
        {
            Emitter.emitIns_I(
                INS_local_tee, node.Type.EmitActualSize,
                unchecked((nint)regNumberExtensions.WasmRegToIndex(reg)));
        }

        genProduceReg(node);
        if (node.IsUnusedValue)
        {
            Emitter.emitIns(INS_drop);
        }
    }

    private regNumber GetMultiUseOperandReg(GenTree operand)
    {
        if (genIsRegCandidateLocal(operand))
        {
            ref var varDsc = ref _compiler.lvaGetDesc(operand.AsLclVarCommon().LclNum);
            assert(varDsc.lvIsInReg);
            return varDsc.RegNum;
        }

        // Non-candidate operands use the temporary register initialized by WasmProduceReg.
        var reg = operand.RegNum;
        assert(genIsValidReg(reg));
        return reg;
    }

    private static emitAttr WasmValueTypeToEmitAttr(WasmValueType type)
    {
        return type switch {
            WasmValueType.I32 or WasmValueType.F32 => EA_4BYTE,
            WasmValueType.I64 or WasmValueType.F64 => EA_8BYTE,
            WasmValueType.V128 => EA_16BYTE,
            WasmValueType.ExnRef => EA_PTRSIZE,
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, $"Unexpected Wasm value type: {type}."),
        };
    }

    private void genEmitLocalGet(regNumber reg, WasmValueType expectedType)
    {
        var actualType = regNumberExtensions.WasmRegToType(reg);
        Emitter.emitIns_I(
            INS_local_get, WasmValueTypeToEmitAttr(actualType),
            unchecked((nint)regNumberExtensions.WasmRegToIndex(reg)));

        if (actualType != expectedType)
        {
            // Wasm locals have fixed types, so narrowing an i64 local needs an explicit wrap.
            assert(actualType is WasmValueType.I64);
            assert(expectedType is WasmValueType.I32);
            Emitter.emitIns(INS_i32_wrap_i64);
        }
    }

    private void genEmitLocalGet(regNumber reg, var_types expectedType)
    {
        genEmitLocalGet(reg, regNumberExtensions.ActualTypeToWasmValueType(genActualType(expectedType)));
    }
}
#endif
