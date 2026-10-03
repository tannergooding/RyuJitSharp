// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // Native Wasm codegen currently builds without WASM_THREAD_SUPPORT.
    private const bool WasmThreadSupport = false;

    private unsafe void genCodeForTreeNodeWasm(GenTree treeNode)
    {
#if DEBUG
        lastConsumedNode = null;
        if (_compiler.verbose)
        {
            _compiler.gtDispLIRNode(treeNode, "Generating: ");
        }
#endif

        assert(!treeNode.IsReuseRegVal);
        if (treeNode.IsContained)
        {
            return;
        }

#if FEATURE_HW_INTRINSICS
        if (treeNode.Oper.IsHWIntrinsic)
        {
            genHWIntrinsic(treeNode.AsHWIntrinsic());
            return;
        }
#endif

        switch (treeNode.Oper)
        {
            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            case GT_OR:
            case GT_XOR:
            case GT_AND:
            {
                genCodeForBinary(treeNode.AsOp());
                break;
            }

            case GT_DIV:
            case GT_MOD:
            case GT_UDIV:
            case GT_UMOD:
            {
                genCodeForDivMod(treeNode.AsOp());
                break;
            }

            case GT_LSH:
            case GT_RSH:
            case GT_RSZ:
            case GT_ROL:
            case GT_ROR:
            {
                genCodeForShift(treeNode);
                break;
            }

            case GT_EQ:
            case GT_NE:
            case GT_LT:
            case GT_LE:
            case GT_GE:
            case GT_GT:
            {
                genCodeForCompare(treeNode.AsOp());
                break;
            }

            case GT_LCL_ADDR:
            {
                genCodeForLclAddr(treeNode.AsLclFld());
                break;
            }

            case GT_LCL_FLD:
            {
                genCodeForLclFld(treeNode.AsLclFld());
                break;
            }

            case GT_LCL_VAR:
            {
                genCodeForLclVar(treeNode.AsLclVar());
                break;
            }

            case GT_STORE_LCL_VAR:
            {
                genCodeForStoreLclVar(treeNode.AsLclVar());
                break;
            }

            case GT_PHYSREG:
            {
                genCodeForPhysReg(treeNode.AsPhysReg());
                break;
            }

            case GT_FRAME_SIZE:
            {
                genCodeForFrameSize(treeNode);
                break;
            }

            case GT_JTRUE:
            {
                genCodeForJTrue(treeNode.AsUnOp());
                break;
            }

            case GT_SWITCH:
            {
                genTableBasedSwitch(treeNode);
                break;
            }

            case GT_RETURN:
            case GT_RETFILT:
            {
                genReturn(treeNode);
                break;
            }

            case GT_IL_OFFSET:
            {
                // Debug-info marker; it emits no code.
                break;
            }

            case GT_NOP:
            {
                break;
            }

            case GT_NO_OP:
            {
                GetEmitter().emitIns(INS_nop);
                break;
            }

            case GT_CNS_INT:
            case GT_CNS_LNG:
            case GT_CNS_DBL:
            {
                genCodeForConstant(treeNode);
                break;
            }

            case GT_CAST:
            {
                genCodeForCast(treeNode.AsCast());
                break;
            }

            case GT_BITCAST:
            {
                genCodeForBitCast(treeNode.AsUnOp());
                break;
            }

            case GT_NEG:
            case GT_NOT:
            {
                genCodeForNegNot(treeNode.AsUnOp());
                break;
            }

            case GT_IND:
            {
                genCodeForIndir(treeNode.AsIndir());
                break;
            }

            case GT_STOREIND:
            {
                genCodeForStoreInd(treeNode.AsStoreInd());
                break;
            }

            case GT_CALL:
            {
                genCall(treeNode.AsCall());
                break;
            }

            case GT_NULLCHECK:
            {
                genCodeForNullCheck(treeNode.AsIndir());
                break;
            }

            case GT_BOUNDS_CHECK:
            {
                genRangeCheck(treeNode);
                break;
            }

            case GT_KEEPALIVE:
            {
                // Remove KEEPALIVE after GC info generation is implemented.
                genConsumeRegs(treeNode.AsOp().Op1);
                GetEmitter().emitIns(INS_drop);
                break;
            }

            case GT_LCLHEAP:
            {
                genLclHeap(treeNode);
                break;
            }

            case GT_INDEX_ADDR:
            {
                genCodeForIndexAddr(treeNode.AsIndexAddr());
                break;
            }

            case GT_LEA:
            {
                genLeaInstruction(treeNode.AsAddrMode());
                break;
            }

            case GT_STORE_BLK:
            {
                genCodeForStoreBlk(treeNode.AsBlk());
                break;
            }

            case GT_MEMORYBARRIER:
            {
                // Wasm codegen is single-threaded, so this is a no-op.
                assert(!WasmThreadSupport);
                JITDUMP("Ignoring GT_MEMORYBARRIER; single-threaded codegen\n");
                break;
            }

            case GT_INTRINSIC:
            {
                genIntrinsic(treeNode.AsIntrinsic());
                break;
            }

            case GT_WASM_JEXCEPT:
            {
                // The marker does not emit code.
                break;
            }

            case GT_WASM_THROW_REF:
            {
                // Reload and rethrow the exnref stashed at the catch_ref landing.
                var exnRefIndex = _compiler.funCurrentFunc().funWasmExnRefLocalIndex;
                assert(exnRefIndex != uint.MaxValue);
                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)exnRefIndex));
                GetEmitter().emitIns(INS_throw_ref);
                break;
            }

            case GT_CATCH_ARG:
            {
                genCatchArg(treeNode);
                break;
            }

            case GT_CKFINITE:
            {
                genCkfinite(treeNode);
                break;
            }

#if FEATURE_SIMD
            case GT_CNS_VEC:
            {
                genCodeForVectorConstant(treeNode);
                break;
            }
#endif

            case GT_ASYNC_CONTINUATION:
            {
                genCodeForAsyncContinuation(treeNode);
                break;
            }

            case GT_RETURN_SUSPEND:
            {
                genReturnSuspend(treeNode.AsUnOp());
                break;
            }

            case GT_ASYNC_RESUME_INFO:
            {
                genAsyncResumeInfo(treeNode.AsVal());
                break;
            }

            case GT_RECORD_ASYNC_RESUME:
            {
                genRecordAsyncResume(treeNode.AsVal());
                break;
            }

            default:
            {
#if DEBUG
                if (JitConfig.JitWasmNyiToR2RUnsupported > 0)
                {
                    NYI_WASM("Opcode not implemented");
                }
                else
                {
                    NYIRAW(treeNode.Oper.ToString());
                }
#else
                NYI_WASM("Opcode not implemented");
#endif
                break;
            }
        }
    }

#if FEATURE_HW_INTRINSICS
    private void genHWIntrinsic(GenTreeHWIntrinsic node)
    {
        WasmCodegenDependencyNotPorted(node, nameof(genHWIntrinsic));
    }
#endif

    private void genCodeForCompare(GenTreeOp tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForCompare));
    }

    private void genCodeForLclAddr(GenTreeLclFld tree)
    {
        assert(tree.OperIs(GT_LCL_ADDR));

        var lclNum = tree.LclNum;
        var lclOffset = tree.LclOffs;

        // This matches the Wasm-only LIR::Flags::FoldedAddr bit in src/coreclr/jit/lir.h.
        const LIR.Flags WasmFoldedAddr = (LIR.Flags)0x10;

        GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));

        if (((tree._lirFlags & WasmFoldedAddr) == LIR.Flags.None) &&
            ((lclOffset != 0) || (_compiler.lvaFrameAddress(lclNum, out _) != 0)))
        {
            GetEmitter().emitIns_S(INS_I_const, EA_PTRSIZE, lclNum, lclOffset);
            GetEmitter().emitIns(INS_I_add);
        }

        WasmProduceReg(tree);
    }

    private void genCodeForLclFld(GenTreeLclFld tree)
    {
        assert(tree.OperIs(GT_LCL_FLD));
        _ = _compiler.lvaGetDesc(tree.LclNum);

        var type = tree.Type;
        if (type is TYP_SIMD12)
        {
            genLoadLclTypeSimd12(tree);
        }
        else
        {
            GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
            GetEmitter().emitIns_S(ins_Load(type), type.EmitSize, tree.LclNum, tree.LclOffs);
        }

        WasmProduceReg(tree);
    }

    private void genLoadLclTypeSimd12(GenTreeLclVarCommon tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genLoadLclTypeSimd12));
    }

    private void genCodeForLclVar(GenTreeLclVar tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForLclVar));
    }

    private void genCodeForStoreLclVar(GenTreeLclVar tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForStoreLclVar));
    }

    private void genCodeForPhysReg(GenTreePhysReg tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForPhysReg));
    }

    private void genCodeForFrameSize(GenTree tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForFrameSize));
    }

    private void genCodeForIndir(GenTreeIndir tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForIndir));
    }

    private void genCodeForStoreInd(GenTreeStoreInd tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForStoreInd));
    }

    private void genCall(GenTreeCall call)
    {
        WasmCodegenDependencyNotPorted(call, nameof(genCall));
    }

    private void genCodeForNullCheck(GenTreeIndir tree)
    {
        genConsumeAddress(tree.Addr);

        if ((tree.Flags & GTF_IND_NONFAULTING) == 0)
        {
            genEmitNullCheck(REG_NA);
        }
        else
        {
            GetEmitter().emitIns(INS_drop);
        }
    }

    private void genEmitNullCheck(regNumber reg)
    {
        WasmCodegenDependencyNotPorted(nameof(genEmitNullCheck));
    }

    private void genRangeCheck(GenTree tree)
    {
        assert(tree.Oper is GT_BOUNDS_CHECK);
        var boundsCheck = tree.AsBoundsChk();

        // Incoming stack operands are index, then length (top of stack).
        genConsumeOperands(boundsCheck);
#if FEATURE_SIMD
        if (varTypeIsSimd(boundsCheck.Index.Type))
        {
            GetEmitter().emitIns(INS_i8x16_splat);
            GetEmitter().emitIns(INS_i8x16_ge_u);
            GetEmitter().emitIns(INS_v128_any_true);
        }
        else
#endif
        {
            GetEmitter().emitIns(INS_I_ge_u);
        }

        genJumpToThrowHlpBlk(boundsCheck.ThrowKind);
    }

    private void genLclHeap(GenTree tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genLclHeap));
    }

    private void genCodeForIndexAddr(GenTreeIndexAddr tree)
    {
        genConsumeOperands(tree);

        var baseAddress = tree.Arr;
        var index = tree.Index;

        assert(varTypeIsIntegral(index.Type));
        var indexType = genActualType(index.Type);

        // Generate the bounds check if necessary.
        if (tree.IsBoundsChecked)
        {
            var baseReg = GetMultiUseOperandReg(baseAddress);
            var indexReg = GetMultiUseOperandReg(index);

            // Fetch the index, then the array length.
            genEmitLocalGet(indexReg, index.Type);
            genEmitLocalGet(baseReg, WasmValueType.I);
            GetEmitter().emitIns_I(ins_Load(TYP_INT), EA_4BYTE, tree.LenOffset);

            // If the index type is long, extend the array length.
            if (indexType == TYP_LONG)
            {
                GetEmitter().emitIns(INS_i64_extend_u_i32);
            }

            GetEmitter().emitIns(indexType == TYP_LONG ? INS_i64_ge_u : INS_i32_ge_u);
            genJumpToThrowHlpBlk(SCK_RNGCHK_FAIL);
        }

        // Zero extend the index if necessary.
        if (indexType != TYP_I_IMPL)
        {
            GetEmitter().emitIns(INS_i64_extend_u_i32);
        }

        // The result is the address of the array element.
        var scale = tree.ElemSize;
        if (scale > 1)
        {
            GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, scale);
            GetEmitter().emitIns(INS_I_mul);
        }

        GetEmitter().emitIns(INS_I_add);
        GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, tree.ElemOffset);
        GetEmitter().emitIns(INS_I_add);
        WasmProduceReg(tree);
    }

    private void genLeaInstruction(GenTreeAddrMode lea)
    {
        genConsumeOperands(lea);
        assert(lea.HasIndex || lea.HasBaseAddress);

        if (lea.HasIndex)
        {
            var scale = lea.Scale;

            if (scale > 1)
            {
                GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, scale);
                GetEmitter().emitIns(INS_I_mul);
            }

            if (lea.HasBaseAddress)
            {
                GetEmitter().emitIns(INS_I_add);
            }
        }

        var offset = lea.Offset;
        if (offset != 0)
        {
            GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, offset);
            GetEmitter().emitIns(INS_I_add);
        }

        WasmProduceReg(lea);
    }

    private void genCodeForStoreBlk(GenTreeBlk node)
    {
        WasmCodegenDependencyNotPorted(node, nameof(genCodeForStoreBlk));
    }

    private static uint PackIntrinsicAndType(NamedIntrinsic intrinsic, var_types type)
    {
        if (type is TYP_BYREF or TYP_REF)
        {
            type = TYP_I_IMPL;
        }

        // Reserve enough low bits for every var_types value, matching ConstLog2<TYP_COUNT>::value + 1.
        var shift = System.Numerics.BitOperations.Log2((uint)TYP_COUNT) + 1;
        return unchecked(((uint)intrinsic << shift) | (uint)type);
    }

    private void genIntrinsic(GenTreeIntrinsic tree)
    {
        genConsumeOperands(tree);

        var ins = INS_invalid;
        var canHaveMixedTypes = false;
        var intrinsicAndType = PackIntrinsicAndType(tree.IntrinsicName, tree.Type);

        // Native case labels use constexpr calls; guarded patterns preserve those packed-key comparisons.
        switch (intrinsicAndType)
        {
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Abs, TYP_FLOAT):
            {
                ins = INS_f32_abs;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Abs, TYP_DOUBLE):
            {
                ins = INS_f64_abs;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Ceiling, TYP_FLOAT):
            {
                ins = INS_f32_ceil;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Ceiling, TYP_DOUBLE):
            {
                ins = INS_f64_ceil;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Floor, TYP_FLOAT):
            {
                ins = INS_f32_floor;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Floor, TYP_DOUBLE):
            {
                ins = INS_f64_floor;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Max, TYP_FLOAT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_System_Math_MaxNative, TYP_FLOAT):
            {
                ins = INS_f32_max;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Max, TYP_DOUBLE) ||
                intrinsicAndType == PackIntrinsicAndType(NI_System_Math_MaxNative, TYP_DOUBLE):
            {
                ins = INS_f64_max;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Min, TYP_FLOAT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_System_Math_MinNative, TYP_FLOAT):
            {
                ins = INS_f32_min;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Min, TYP_DOUBLE) ||
                intrinsicAndType == PackIntrinsicAndType(NI_System_Math_MinNative, TYP_DOUBLE):
            {
                ins = INS_f64_min;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Round, TYP_FLOAT):
            {
                ins = INS_f32_nearest;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Round, TYP_DOUBLE):
            {
                ins = INS_f64_nearest;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Sqrt, TYP_FLOAT):
            {
                ins = INS_f32_sqrt;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Sqrt, TYP_DOUBLE):
            {
                ins = INS_f64_sqrt;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Truncate, TYP_FLOAT):
            {
                ins = INS_f32_trunc;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_System_Math_Truncate, TYP_DOUBLE):
            {
                ins = INS_f64_trunc;
                break;
            }
            case var _ when intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_LeadingZeroCount, TYP_INT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_LeadingZeroCount, TYP_LONG) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_TrailingZeroCount, TYP_INT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_TrailingZeroCount, TYP_LONG) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_PopCount, TYP_INT) ||
                intrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_PopCount, TYP_LONG):
            {
                canHaveMixedTypes = true;
                break;
            }
            default:
            {
                assert(false, "genIntrinsic: Unsupported intrinsic");
                unreached();
                break;
            }
        }

        var needsTruncation = false;
        var needsExtension = false;

        if (canHaveMixedTypes)
        {
            var treeType = tree.Type;
            var operandType = genActualType(tree.Op1.Type);

            needsTruncation = (operandType == TYP_LONG) && (treeType == TYP_INT);
            needsExtension = (operandType == TYP_INT) && (treeType == TYP_LONG);

            var operandIntrinsicAndType = PackIntrinsicAndType(tree.IntrinsicName, operandType);
            switch (operandIntrinsicAndType)
            {
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_LeadingZeroCount, TYP_INT):
                {
                    ins = INS_i32_clz;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_LeadingZeroCount, TYP_LONG):
                {
                    ins = INS_i64_clz;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_TrailingZeroCount, TYP_INT):
                {
                    ins = INS_i32_ctz;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_TrailingZeroCount, TYP_LONG):
                {
                    ins = INS_i64_ctz;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_PopCount, TYP_INT):
                {
                    ins = INS_i32_popcnt;
                    break;
                }
                case var _ when operandIntrinsicAndType == PackIntrinsicAndType(NI_PRIMITIVE_PopCount, TYP_LONG):
                {
                    ins = INS_i64_popcnt;
                    break;
                }
                default:
                {
                    unreached();
                    break;
                }
            }
        }

        GetEmitter().emitIns(ins);

        if (needsTruncation)
        {
            GetEmitter().emitIns(INS_i32_wrap_i64);
        }
        else if (needsExtension)
        {
            GetEmitter().emitIns(INS_i64_extend_u_i32);
        }

        WasmProduceReg(tree);
    }

    private void genCodeForAsyncContinuation(GenTree tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForAsyncContinuation));
    }

    private void genReturnSuspend(GenTreeUnOp tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genReturnSuspend));
    }

    private static void WasmCodegenDependencyNotPorted(GenTree tree, string dependency)
    {
        throw new FatalJitException(CORJIT_SKIPPED, $"Wasm {dependency} is not ported for {tree.Oper}.");
    }

    private static void WasmCodegenDependencyNotPorted(string dependency)
    {
        throw new FatalJitException(CORJIT_SKIPPED, $"Wasm {dependency} is not ported.");
    }
}
#endif
