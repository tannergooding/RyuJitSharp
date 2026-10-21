// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class CodeGen
{
    private void genCatchArg(GenTree treeNode)
    {
        assert(treeNode.Oper is GT_CATCH_ARG);

        // The catch argument is the third Wasm function parameter.
        Emitter.emitIns_I(INS_local_get, EA_GCREF, unchecked((nint)2));
        WasmProduceReg(treeNode);
    }

    private unsafe void genStoreAsyncContinuationGlobal()
    {
        // The continuation is already on the Wasm operand stack.
        var asyncContinuation = unchecked((nint)_compiler.eeGetWasmWellKnownGlobals().asyncContinuation);
        GetEmitter().emitIns_I(INS_global_set, EA_SET_FLG(EA_GCREF, EA_CNS_RELOC_FLG), asyncContinuation);
    }

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
        switch (type)
        {
            case WasmValueType.I32 or WasmValueType.F32:
            {
                return EA_4BYTE;
            }

            case WasmValueType.I64 or WasmValueType.F64:
            {
                return EA_8BYTE;
            }

            case WasmValueType.V128:
            {
                return EA_16BYTE;
            }

            case WasmValueType.ExnRef:
            {
                return EA_PTRSIZE;
            }

            default:
            {
                FatalJitException.ThrowFatalJitException(CORJIT_INTERNALERROR, $"Unexpected Wasm value type: {type}.");
                return default!;
            }
        }
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
