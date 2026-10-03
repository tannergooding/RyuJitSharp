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
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForLclAddr));
    }

    private void genCodeForLclFld(GenTreeLclFld tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForLclFld));
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
        WasmCodegenDependencyNotPorted(tree, nameof(genCodeForIndexAddr));
    }

    private void genLeaInstruction(GenTreeAddrMode lea)
    {
        WasmCodegenDependencyNotPorted(lea, nameof(genLeaInstruction));
    }

    private void genCodeForStoreBlk(GenTreeBlk node)
    {
        WasmCodegenDependencyNotPorted(node, nameof(genCodeForStoreBlk));
    }

    private void genIntrinsic(GenTreeIntrinsic tree)
    {
        WasmCodegenDependencyNotPorted(tree, nameof(genIntrinsic));
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
