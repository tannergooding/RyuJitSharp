// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if DEBUG
using System.Runtime.CompilerServices;
#endif

#if TARGET_WASM
namespace RyuJitSharp;

public sealed partial class CodeGen
{
    // Native Wasm codegen currently builds without WASM_THREAD_SUPPORT.
    private const bool WasmThreadSupport = false;

#if TARGET_64BIT
    private const instruction INS_I_load = INS_i64_load;
    private const instruction INS_I_store = INS_i64_store;
    private const instruction INS_I_and = INS_i64_and;
    private const instruction INS_I_eqz = INS_i64_eqz;
    private const instruction INS_I_mul = INS_i64_mul;
    private const instruction INS_I_sub = INS_i64_sub;
    private const instruction INS_I_le_u = INS_i64_le_u;
    private const instruction INS_I_ge_u = INS_i64_ge_u;
    private const instruction INS_I_gt_u = INS_i64_gt_u;
#else
    private const instruction INS_I_load = INS_i32_load;
    private const instruction INS_I_store = INS_i32_store;
    private const instruction INS_I_and = INS_i32_and;
    private const instruction INS_I_eqz = INS_i32_eqz;
    private const instruction INS_I_mul = INS_i32_mul;
    private const instruction INS_I_sub = INS_i32_sub;
    private const instruction INS_I_le_u = INS_i32_le_u;
    private const instruction INS_I_ge_u = INS_i32_ge_u;
    private const instruction INS_I_gt_u = INS_i32_gt_u;
#endif

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
                NYIRAW(treeNode.Oper.ToString());
#else
                NYI_WASM("Opcode not implemented");
#endif
                break;
            }
        }
    }

#if FEATURE_HW_INTRINSICS
    private readonly struct WasmHWIntrinsic
    {
        private readonly GenTreeHWIntrinsic _node;

        public NamedIntrinsic Id { get; }
        public HWIntrinsicCategory Category { get; }
        public var_types BaseType { get; }
        public int NumOperands { get; }

        public WasmHWIntrinsic(GenTreeHWIntrinsic node)
        {
            assert(node is not null);
            _node = node;
            Id = node.HWIntrinsicId;
            Category = HWIntrinsicInfo.lookupCategory(Id);
            NumOperands = node.Operands.Length;
            BaseType = node.SimdBaseType;

            assert(HWIntrinsicInfo.RequiresCodegen(Id));
            assert(NumOperands <= 3);

            if (BaseType == TYP_UNKNOWN)
            {
                assert(Category is HW_Category_Scalar or HW_Category_Special);

                if (HWIntrinsicInfo.BaseTypeFromFirstArg(Id))
                {
                    assert(NumOperands >= 1);
                    BaseType = node.GetOp(1).Type;
                }
                else if (HWIntrinsicInfo.BaseTypeFromSecondArg(Id))
                {
                    assert(NumOperands >= 2);
                    BaseType = node.GetOp(2).Type;
                }
                else
                {
                    BaseType = node.Type;
                }

                if (Category is HW_Category_Scalar)
                {
                    BaseType = BaseType.ActualType;
                }
            }
        }

        public bool CodeGenIsTableDriven =>
            (Category is not HW_Category_Helper) && !HWIntrinsicInfo.HasSpecialCodegen(Id);

        public bool NeedsJumpTableFallback =>
            HWIntrinsicInfo.HasImmediateOperand(Id) && !GetImmediateOperand().Oper.IsCnsIntOrI;

        public GenTree GetImmediateOperand()
        {
            HWIntrinsicInfo.GetImmOpsPositions(Id, out var immediatePosition, out _);
            assert((immediatePosition > 0) && (immediatePosition <= NumOperands));

            return _node.GetOp(immediatePosition);
        }

        public byte GetImmediateLaneOperand()
        {
            assert(Category is HW_Category_IMM or HW_Category_MemoryLoad or HW_Category_MemoryStore);

            var immediate = GetImmediateOperand();
            assert(immediate.Oper.IsCnsIntOrI);

            var lane = immediate.AsIntCon().IconValue;
            assert((lane >= byte.MinValue) && (lane <= byte.MaxValue));

            return unchecked((byte)lane);
        }
    }

    private void genEmitWasmIntrinsicLane(instruction ins, byte laneIdx)
    {
        WasmCodegenDependencyNotPorted($"Emitter.emitIns_Lane for {ins}, lane {laneIdx}");
    }

    private void genEmitWasmDepthInstruction(instruction ins, uint depth)
    {
        WasmCodegenDependencyNotPorted($"Emitter.emitIns_J for {ins}, depth {depth}, without a block target");
    }

    private void genHWIntrinsic(GenTreeHWIntrinsic node)
    {
        var info = new WasmHWIntrinsic(node);
        genConsumeMultiOpOperands(node);

        if (info.CodeGenIsTableDriven)
        {
            var ins = HWIntrinsicInfo.lookupIns(info.Id, info.BaseType, _compiler);
            assert(ins != INS_invalid);

            switch (info.Category)
            {
                case HW_Category_SIMD:
                {
                    if (info.Id is NI_PackedSimd_Shuffle)
                    {
                        var mask = node.GetOp(3);
                        assert(mask.IsContained);
                        GetEmitter().emitIns_V128Imm(ins, mask.AsVecCon().SimdVal.AsSpan<byte>()[..16]);
                    }
                    else if ((info.Id is NI_PackedSimd_Swizzle) && node.GetOp(2).IsContained)
                    {
                        // i8x16.shuffle reads two vectors, while the contained mask was not materialized.
                        var src = node.GetOp(1);
                        var srcReg = GetMultiUseOperandReg(src);
                        genEmitLocalGet(srcReg, src.Type);
                        GetEmitter().emitIns_V128Imm(
                            INS_i8x16_shuffle, node.GetOp(2).AsVecCon().SimdVal.AsSpan<byte>()[..16]);
                    }
                    else
                    {
                        GetEmitter().emitIns(ins);
                    }
                    break;
                }

                case HW_Category_IMM:
                {
                    if (info.NeedsJumpTableFallback)
                    {
                        genHWIntrinsicJumpTableFallback(node, info);
                    }
                    else
                    {
                        genEmitWasmIntrinsicLane(ins, info.GetImmediateLaneOperand());
                    }
                    break;
                }

                case HW_Category_MemoryStore:
                case HW_Category_MemoryLoad:
                {
                    var elemSize = node.SimdBaseType.EmitActualSize;
                    var isMem = node.IsMemoryLoad(out var addr) || node.IsMemoryStore(out addr);
                    assert(isMem && addr is not null);

                    var memoryAddress = addr
                        ?? throw new FatalJitException(
                            CORJIT_INTERNALERROR, "Wasm hardware intrinsic memory node has no address.");
                    var addrReg = GetMultiUseOperandReg(memoryAddress);
                    genEmitNullCheck(addrReg, memoryAddress.Type);

                    if (info.NeedsJumpTableFallback)
                    {
                        genHWIntrinsicJumpTableFallback(node, info);
                    }
                    else if (HWIntrinsicInfo.HasImmediateOperand(info.Id))
                    {
                        GetEmitter().emitIns_MemargLane(
                            ins, elemSize, 0, info.GetImmediateLaneOperand());
                    }
                    else
                    {
                        GetEmitter().emitIns_I(ins, elemSize, 0);
                    }
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }
        else
        {
            switch (info.Id)
            {
                case NI_Vector_AsVector128Unsafe:
                case NI_Vector_AsVector2:
                case NI_Vector_AsVector3:
                {
                    // Wasm SIMD values already occupy a full v128 on the value stack.
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }
        }

        WasmProduceReg(node);
    }

    private void genHWIntrinsicJumpTableFallback(GenTreeHWIntrinsic node, WasmHWIntrinsic info)
    {
        assert(info.Category is HW_Category_IMM or HW_Category_MemoryLoad or HW_Category_MemoryStore);

        var simdSize = node.SimdSize;
        var ins = HWIntrinsicInfo.lookupIns(info.Id, info.BaseType, _compiler);
        var immUpperBound = HWIntrinsicInfo.lookupImmUpperBound(info.Id, simdSize, info.BaseType);
        var resultType = WasmValueType.Invalid;
        if (node.Type != TYP_VOID)
        {
            resultType = regNumberExtensions.ActualTypeToWasmValueType(node.Type.ActualType);
        }

        var immOp = info.GetImmediateOperand();
        var immReg = GetMultiUseOperandReg(immOp);

        // Drop the original operands; each switch-table case re-materializes its operands from locals.
        for (var i = 0; i < info.NumOperands; i++)
        {
            GetEmitter().emitIns(INS_drop);
        }

        void GetNonImmediateOperands()
        {
            for (var i = 1; i <= info.NumOperands; i++)
            {
                var operand = node.GetOp(i);
                if (operand != immOp)
                {
                    // Lowering marks these operands multiply used so register allocation keeps their locals.
                    var reg = GetMultiUseOperandReg(operand);
                    genEmitLocalGet(reg, operand.Type);
                }
            }
        }

        // Each nested block is a case target for br_table; the inner void block is the invalid-index target.
        // The cases remain one inline macro-instruction; adding calls or safepoints would require restructuring.
        genEmitBeginBlock(resultType);
        genEmitBeginBlock();
        for (var i = 0; i <= immUpperBound; i++)
        {
            genEmitBeginBlock();
        }

        genEmitLocalGet(immReg, immOp.Type);

        var caseCount = immUpperBound + 1;
        GetEmitter().emitIns_I(INS_br_table, EA_4BYTE, caseCount);
        for (var caseNum = 0; caseNum <= immUpperBound; caseNum++)
        {
            genEmitWasmDepthInstruction(INS_label, unchecked((uint)caseNum));
        }
        genEmitWasmDepthInstruction(INS_label, unchecked((uint)(immUpperBound + 1)));

        assert((immUpperBound >= 0) && (immUpperBound <= byte.MaxValue));
        for (var i = 0; i <= immUpperBound; i++)
        {
            genEmitEndBlock();
            GetNonImmediateOperands();

            switch (info.Category)
            {
                case HW_Category_IMM:
                {
                    genEmitWasmIntrinsicLane(ins, unchecked((byte)i));
                    break;
                }

                case HW_Category_MemoryLoad:
                case HW_Category_MemoryStore:
                {
                    var elemSize = node.SimdBaseType.EmitActualSize;
                    GetEmitter().emitIns_MemargLane(ins, elemSize, 0, unchecked((byte)i));
                    break;
                }

                default:
                {
                    unreached();
                    break;
                }
            }

            // The inner invalid-index block adds one level to the depth needed to branch to the result block.
            var branchDepth = unchecked((uint)(immUpperBound + 1 - i));
            genEmitWasmDepthInstruction(INS_br, branchDepth);
        }

        genEmitEndBlock();
        GetEmitter().emitIns(INS_unreachable);
        genEmitEndBlock();
    }
#endif

    private void genCodeForCompare(GenTreeOp tree)
    {
        assert(tree.OperIsCmpCompare());

        var op1 = tree.Op1;
        var op1Type = op1.Type;

        if (varTypeIsFloating(op1Type))
        {
            genCompareFloat(tree);
        }
        else
        {
            genCompareInt(tree);
        }
    }

    private void genCompareInt(GenTreeOp treeNode)
    {
        assert(treeNode.OperIsCmpCompare());

        var op1 = treeNode.Op1;
        var op2 = treeNode.Op2;
        GenTree? value = null;

        if (treeNode.OperIs(GT_EQ, GT_NE))
        {
            if (op1.IsContained && op1.IsIntegralConst(0))
            {
                value = op2;
            }
            else if (op2.IsContained && op2.IsIntegralConst(0))
            {
                value = op1;
            }
        }

        genConsumeOperands(treeNode);

        if (value is not null)
        {
            var canUseValueDirectly = treeNode.OperIs(GT_NE) &&
                ((treeNode.Flags & GTF_RELOP_JMP_USED) != 0) &&
                (genActualType(value.Type) == TYP_INT);
            if (!canUseValueDirectly)
            {
                GetEmitter().emitIns(genActualType(value.Type) == TYP_LONG ? INS_i64_eqz : INS_i32_eqz);
                if (treeNode.OperIs(GT_NE))
                {
                    GetEmitter().emitIns(INS_i32_eqz);
                }
            }

            WasmProduceReg(treeNode);
            return;
        }

        var type = genActualType(op1.Type);
        instruction ins;
        switch ((treeNode.Oper, type))
        {
            case (GT_EQ, TYP_INT):
            {
                ins = INS_i32_eq;
                break;
            }

            case (GT_EQ, TYP_LONG):
            {
                ins = INS_i64_eq;
                break;
            }

            case (GT_NE, TYP_INT):
            {
                ins = INS_i32_ne;
                break;
            }

            case (GT_NE, TYP_LONG):
            {
                ins = INS_i64_ne;
                break;
            }

            case (GT_LT, TYP_INT):
            {
                ins = treeNode.IsUnsigned ? INS_i32_lt_u : INS_i32_lt_s;
                break;
            }

            case (GT_LT, TYP_LONG):
            {
                ins = treeNode.IsUnsigned ? INS_i64_lt_u : INS_i64_lt_s;
                break;
            }

            case (GT_LE, TYP_INT):
            {
                ins = treeNode.IsUnsigned ? INS_i32_le_u : INS_i32_le_s;
                break;
            }

            case (GT_LE, TYP_LONG):
            {
                ins = treeNode.IsUnsigned ? INS_i64_le_u : INS_i64_le_s;
                break;
            }

            case (GT_GE, TYP_INT):
            {
                ins = treeNode.IsUnsigned ? INS_i32_ge_u : INS_i32_ge_s;
                break;
            }

            case (GT_GE, TYP_LONG):
            {
                ins = treeNode.IsUnsigned ? INS_i64_ge_u : INS_i64_ge_s;
                break;
            }

            case (GT_GT, TYP_INT):
            {
                ins = treeNode.IsUnsigned ? INS_i32_gt_u : INS_i32_gt_s;
                break;
            }

            case (GT_GT, TYP_LONG):
            {
                ins = treeNode.IsUnsigned ? INS_i64_gt_u : INS_i64_gt_s;
                break;
            }

            default:
            {
                throw new FatalJitException(CORJIT_INTERNALERROR, "Unsupported Wasm integer comparison.");
            }
        }

        GetEmitter().emitIns(ins);
        WasmProduceReg(treeNode);
    }

    private void genCompareFloat(GenTreeOp treeNode)
    {
        assert(treeNode.OperIsCmpCompare());

        var op = treeNode.Oper;
        var invertSense = false;

        if ((treeNode.Flags & GTF_RELOP_NAN_UN) != 0)
        {
            // CIL has no unordered GT_EQ comparison.
            assert(op != GT_EQ);

            // Wasm comparisons other than "fne" return false for NaNs, so unordered
            // comparisons can use the reversed ordered comparison and invert its result.
            if (op != GT_NE)
            {
                op = op.ReverseRelop;
                invertSense = true;
            }
        }
        else
        {
            // CIL has no ordered GT_NE comparison.
            assert(op != GT_NE);
        }

        genConsumeOperands(treeNode);

        instruction ins;
        switch ((op, treeNode.Op1.Type))
        {
            case (GT_EQ, TYP_FLOAT):
            {
                ins = INS_f32_eq;
                break;
            }

            case (GT_EQ, TYP_DOUBLE):
            {
                ins = INS_f64_eq;
                break;
            }

            case (GT_NE, TYP_FLOAT):
            {
                ins = INS_f32_ne;
                break;
            }

            case (GT_NE, TYP_DOUBLE):
            {
                ins = INS_f64_ne;
                break;
            }

            case (GT_LT, TYP_FLOAT):
            {
                ins = INS_f32_lt;
                break;
            }

            case (GT_LT, TYP_DOUBLE):
            {
                ins = INS_f64_lt;
                break;
            }

            case (GT_LE, TYP_FLOAT):
            {
                ins = INS_f32_le;
                break;
            }

            case (GT_LE, TYP_DOUBLE):
            {
                ins = INS_f64_le;
                break;
            }

            case (GT_GE, TYP_FLOAT):
            {
                ins = INS_f32_ge;
                break;
            }

            case (GT_GE, TYP_DOUBLE):
            {
                ins = INS_f64_ge;
                break;
            }

            case (GT_GT, TYP_FLOAT):
            {
                ins = INS_f32_gt;
                break;
            }

            case (GT_GT, TYP_DOUBLE):
            {
                ins = INS_f64_gt;
                break;
            }

            default:
            {
                throw new FatalJitException(CORJIT_INTERNALERROR, "Unsupported Wasm floating-point comparison.");
            }
        }

        GetEmitter().emitIns(ins);

        if (invertSense)
        {
            GetEmitter().emitIns(INS_i32_eqz);
        }

        WasmProduceReg(treeNode);
    }

    private void genCodeForLclAddr(GenTreeLclFld tree)
    {
        assert(tree.OperIs(GT_LCL_ADDR));

        var lclNum = tree.LclNum;
        var lclOffset = tree.LclOffs;

        GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));

        if (((tree._lirFlags & LIR.Flags.FoldedAddr) == LIR.Flags.None) &&
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
        var frameOffset = _compiler.lvaFrameAddress(tree.LclNum, out var fpBased) + tree.LclOffs;
        noway_assert(frameOffset >= 0);
        assert(fpBased);

        // Vector3 uses a v128 with its upper lane loaded separately.
        var fpIndex = GetFramePointerRegIndex();
        var emitter = GetEmitter();

        emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)fpIndex));
        emitter.emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)fpIndex));
        emitter.emitIns_I(INS_v128_load64_zero, EA_8BYTE, frameOffset);
        emitter.emitIns_MemargLane(INS_v128_load32_lane, EA_4BYTE, frameOffset + 8, 2);
    }

    private void genCodeForLclVar(GenTreeLclVar tree)
    {
        assert(tree.OperIs(GT_LCL_VAR) && !tree.IsMultiReg);
        ref var varDsc = ref _compiler.lvaGetDesc(tree.LclNum);

        // Wasm cannot reload at the point of use without inserting into an emitted instruction group.
        // Lowering orders nodes to obey the value-stack constraints, so only non-candidates need WasmProduceReg.
        if (!varDsc.lvIsRegCandidate)
        {
            var type = varDsc.GetRegisterType(tree);

            if (type is TYP_SIMD12)
            {
                genLoadLclTypeSimd12(tree);
            }
            else
            {
                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
                GetEmitter().emitIns_S(ins_Load(type), type.EmitSize, tree.LclNum, 0);
            }

            WasmProduceReg(tree);
        }
        else
        {
            assert(genIsValidReg(varDsc.RegNum));
            var type = varDsc.GetRegisterType(tree);
            var wasmLclIndex = regNumberExtensions.WasmRegToIndex(varDsc.RegNum);

            GetEmitter().emitIns_I(INS_local_get, type.EmitSize, unchecked((nint)wasmLclIndex));

            // A register local may have a different type than the tree, so truncate when needed.
            if (tree.Type is TYP_INT && varDsc.Type is TYP_LONG)
            {
                GetEmitter().emitIns(INS_i32_wrap_i64);
            }
        }
    }

    private void genCodeForStoreLclVar(GenTreeLclVar tree)
    {
        assert(tree.Oper is GT_STORE_LCL_VAR);
        var op1 = tree.Op1;
        assert(!op1.IsMultiRegNode);
        genConsumeRegs(op1);

        // Stack stores are rewritten to STOREIND because their address must be first on the Wasm operand stack.
        ref var varDsc = ref _compiler.lvaGetDesc(tree.LclNum);
        var targetReg = tree.RegNum;
        var type = varDsc.GetRegisterType(tree);
        assert(genIsValidReg(targetReg) && varDsc.lvIsRegCandidate);

        var wasmLclIndex = regNumberExtensions.WasmRegToIndex(targetReg);
        GetEmitter().emitIns_I(INS_local_set, type.EmitSize, unchecked((nint)wasmLclIndex));
        genUpdateLifeStore(tree, targetReg, ref varDsc);
    }

    private void genUpdateLifeStore(GenTree tree, regNumber targetReg, ref LclVarDsc varDsc)
    {
        if (targetReg != REG_NA)
        {
            genProduceReg(tree);
        }
        else
        {
            genUpdateLife(tree);
            varDsc.RegNum = REG_STK;
        }
    }

    private void genCodeForPhysReg(GenTreePhysReg tree)
    {
        assert(genIsValidReg(tree.SrcReg));
        var wasmLclIndex = regNumberExtensions.WasmRegToIndex(tree.SrcReg);
        GetEmitter().emitIns_I(INS_local_get, tree.Type.EmitActualSize, unchecked((nint)wasmLclIndex));
        WasmProduceReg(tree);
    }

    private void genCodeForFrameSize(GenTree tree)
    {
        assert(tree.Oper is GT_FRAME_SIZE);
        GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)_compiler.compLclFrameSize));
        WasmProduceReg(tree);
    }

    private void genCodeForIndir(GenTreeIndir tree)
    {
        assert(tree.Oper is GT_IND);
        var type = tree.Type;
        var addr = tree.Addr;

        genConsumeAddress(addr);

        if ((tree.Flags & GTF_IND_NONFAULTING) == 0)
        {
            // The base is the address itself unless this is a contained address mode, which is never materialized.
            var baseNode = tree.Base
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "GT_IND base is unavailable for null-check codegen.");
            genEmitNullCheck(GetMultiUseOperandReg(baseNode), baseNode.Type);
        }

        // TODO-WASM: Memory barriers

        if (addr.IsContained && addr.Oper is not GT_LEA)
        {
            assert(addr.IsIconHandle() && type is not TYP_SIMD12);
            assert(addr.AsIntConCommon().ImmedValNeedsReloc(_compiler));
            WasmCodegenDependencyNotPorted(tree, "Emitter.emitImageBase and Emitter.emitIns_MemargAddress");
        }
        else if (type is TYP_SIMD12)
        {
            genLoadIndTypeSimd12(tree);
        }
        else
        {
            GetEmitter().emitIns_I(ins_Load(type), type.EmitActualSize, genWasmMemargOffset(addr));
        }

        WasmProduceReg(tree);
    }

    private void genLoadIndTypeSimd12(GenTreeIndir tree)
    {
        var emitter = GetEmitter();

        // The multiply-used address is reloaded for the upper lane.
        genEmitLocalGet(GetMultiUseOperandReg(tree.Addr), WasmValueType.I);
        emitter.emitIns_I(INS_v128_load64_zero, EA_8BYTE, 0);
        emitter.emitIns_MemargLane(INS_v128_load32_lane, EA_4BYTE, 8, 2);
    }

    private nint genWasmMemargOffset(GenTree addr)
    {
        if (addr.IsContained && addr.Oper is GT_LEA)
        {
            return addr.AsAddrMode().Offset;
        }

        if (addr.Oper is GT_LCL_ADDR && (addr._lirFlags & LIR.Flags.FoldedAddr) != LIR.Flags.None)
        {
            var lclVar = addr.AsLclVarCommon();
            var offset = _compiler.lvaFrameAddress(lclVar.LclNum, out var fpBased) + lclVar.LclOffs;
            noway_assert(offset >= 0);
            assert(fpBased);
            return offset;
        }

        return 0;
    }

    private void genCodeForStoreInd(GenTreeStoreInd tree)
    {
        var data = tree.Data;
        var addr = tree.Addr;

        assert(!addr.IsContained || addr.Oper is GT_LEA);
        var offset = genWasmMemargOffset(addr);

        // Consume the address before the data to update liveness in execution order.
        genConsumeAddress(addr);
        genConsumeRegs(data);

        if ((tree.Flags & GTF_IND_NONFAULTING) == 0)
        {
            // The base is the address itself unless this is a contained address mode, which is never materialized.
            var baseNode = tree.Base
                ?? throw new FatalJitException(
                    CORJIT_INTERNALERROR,
                    "GT_STOREIND base is unavailable for null-check codegen.");
            genEmitNullCheck(GetMultiUseOperandReg(baseNode), baseNode.Type);
        }

        var writeBarrierForm = GCInfo.gcIsWriteBarrierCandidate(tree);
        if (writeBarrierForm is not GCInfo.WriteBarrierForm.WBF_NoBarrier)
        {
            genGCWriteBarrierWasm(writeBarrierForm);
        }
        else // A normal store, not a write-barrier store
        {
            var type = tree.Type;

            // TODO-WASM: Memory barriers
            if (type is TYP_SIMD8)
            {
                // The stack is [address, value]; store the low 8 bytes.
                WasmCodegenDependencyNotPorted(tree, "Emitter.emitIns_MemargLane");
            }
            else if (type is TYP_SIMD12)
            {
                genStoreIndTypeSimd12(tree);
            }
            else
            {
                GetEmitter().emitIns_I(ins_Store(type), type.EmitActualSize, offset);
            }
        }

        genUpdateLife(tree);
    }

    private void genStoreIndTypeSimd12(GenTreeStoreInd tree)
    {
        var emitter = GetEmitter();
        var addr = tree.Addr;
        ref var internalRegs = ref _internalRegisters.GetAll(tree);
        assert(internalRegs.Count == 1);
        var valueReg = _internalRegisters.Extract(tree);
        var wasmValueIndex = regNumberExtensions.WasmRegToIndex(valueReg);

        // The incoming stack is [addr, value]; tee value so it survives the first lane store.
        emitter.emitIns_I(INS_local_tee, EA_16BYTE, unchecked((nint)wasmValueIndex));
        emitter.emitIns_MemargLane(INS_v128_store64_lane, EA_8BYTE, 0, 0);

        if (addr.Oper is GT_LCL_ADDR)
        {
            var lclVar = addr.AsLclVarCommon();
            var frameOffset = _compiler.lvaFrameAddress(lclVar.LclNum, out var fpBased) + lclVar.LclOffs;
            noway_assert(frameOffset >= 0);
            assert(fpBased);

            emitter.emitIns_I(
                INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
            emitter.emitIns_I(INS_local_get, EA_16BYTE, unchecked((nint)wasmValueIndex));
            emitter.emitIns_MemargLane(INS_v128_store32_lane, EA_4BYTE, frameOffset + 8, 2);
        }
        else
        {
            genEmitLocalGet(GetMultiUseOperandReg(addr), WasmValueType.I);
            emitter.emitIns_I(INS_local_get, EA_16BYTE, unchecked((nint)wasmValueIndex));
            emitter.emitIns_MemargLane(INS_v128_store32_lane, EA_4BYTE, 8, 2);
        }
    }

    private void genCall(GenTreeCall call)
    {
        var thisArg = call.NeedsNullCheck
            ? call.Args.ThisArg
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Call requiring a null check has no this argument.")
            : null;
        var thisReg = REG_NA;

        if (thisArg is not null)
        {
            thisReg = GetMultiUseOperandReg(thisArg.Node);
        }

        foreach (var arg in call.Args.EarlyArgs)
        {
            var earlyNode = arg.EarlyNode
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Early call argument has no node.");
            genConsumeRegs(earlyNode);
        }

        foreach (var arg in call.Args.LateArgs)
        {
            var lateNode = arg.LateNode
                ?? throw new FatalJitException(CORJIT_INTERNALERROR, "Late call argument has no node.");
            genConsumeRegs(lateNode);
        }

        if (thisArg is not null)
        {
            genEmitNullCheck(thisReg, thisArg.Node.Type);
        }

        genCallInstruction(call);
        WasmProduceReg(call);
    }

    private unsafe void genCallInstruction(GenTreeCall call)
    {
        ensureCurrentFuncIsUnwindable();

        var parameters = new EmitCallParams
        {
            isJump = call.IsFastTailCall,
            hasAsyncRet = call.IsAsync,
            returnValueCall = call,
        };

#if DEBUG
        if (!call.IsHelperCall())
        {
            parameters.sigInfo = new StrongBox<CORINFO_SIG_INFO>(call._callSig);
        }
#endif

        var target = getCallTarget(call, out parameters.methHnd);
        var typeStack = new ArrayStack<CorInfoWasmType>();

        // Fast tailcalls overwrite the node type with TYP_VOID, but the return_call_indirect
        // signature must still match the current function's return type.
        var callRetType = call.IsFastTailCall ? call._returnType : genActualType(call);
        if (call.ShouldHaveRetBufArg || (callRetType is TYP_VOID))
        {
            typeStack.Push(CORINFO_WASM_TYPE_VOID);
        }
        else if (callRetType is TYP_STRUCT)
        {
            var retWasmType = _compiler.info.compCompHnd->getWasmLowering(call.RetClsHnd);
            // A wider struct is returned through a hidden buffer and must take the branch above.
            assert(retWasmType is not CORINFO_WASM_TYPE_VOID);
            assert(_compiler.info.compCompHnd->getClassSize(call.RetClsHnd) <=
                genTypeSize(WasmClassifier.ToJitType(retWasmType)));
            typeStack.Push(retWasmType);
        }
        else
        {
            // Normalize small integer return types.
            typeStack.Push(WasmValueTypeToCorInfoWasmType(
                regNumberExtensions.ActualTypeToWasmValueType(callRetType)));
        }

        foreach (var arg in call.Args.Args)
        {
            foreach (ref readonly var segment in arg.AbiInfo.Segments)
            {
                assert(segment.IsPassedInRegister);
                var wasmType = regNumberExtensions.WasmRegToType(segment.Register);
                assert(wasmType < WasmValueType.Count);
                typeStack.Push(WasmValueTypeToCorInfoWasmType(wasmType));
            }
        }

        // Report managed call signatures to R2R so it can generate Wasm thunks.
        if (!call.IsHelperCall() && !call.IsUnmanaged)
        {
            CORINFO_SIG_INFO sigInfoLocal = default;
            CORINFO_SIG_INFO* sigInfoCall = null;

            if ((call._callSig.pSig is not null) || (call._callSig.methodSignature is not null))
            {
                sigInfoLocal = call._callSig;
                sigInfoCall = &sigInfoLocal;
            }

            if ((sigInfoCall is null) &&
                (parameters.methHnd != NO_METHOD_HANDLE) &&
                (Compiler.eeGetHelperNum(parameters.methHnd) is CORINFO_HELP_UNDEF))
            {
                _compiler.eeGetMethodSig(parameters.methHnd, out sigInfoLocal);
                sigInfoCall = &sigInfoLocal;

                if ((callRetType is TYP_REF) &&
                    (sigInfoLocal.retType is CORINFO_TYPE_VOID) &&
                    (sigInfoLocal.callConv is CORINFO_CALLCONV_HASTHIS))
                {
                    var methodFlags = _compiler.info.compCompHnd->getMethodAttribs(parameters.methHnd);
                    var stringClass = _compiler.info.compCompHnd->getBuiltinClass(CLASSID_STRING);

                    if (((methodFlags & CORINFO_FLG_CONSTRUCTOR) != 0) &&
                        (stringClass != NO_CLASS_HANDLE) &&
                        (_compiler.info.compCompHnd->getMethodClass(parameters.methHnd) == stringClass))
                    {
                        // String constructors are emitted as static string-returning calls.
                        sigInfoLocal.retType = CORINFO_TYPE_CLASS;
                        sigInfoLocal.callConv = CORINFO_CALLCONV_DEFAULT;
                    }
                }
            }

            if (sigInfoCall is not null)
            {
                _compiler.info.compCompHnd->recordWasmManagedCallSig(sigInfoCall);
            }
        }

        CORINFO_WASM_TYPE_SYMBOL_HANDLE wasmSignature;
        fixed (CorInfoWasmType* types = typeStack.Data())
        {
            wasmSignature = _compiler.info.compCompHnd->getWasmTypeSymbol(types, typeStack.Height());
        }

        // SuppressGCTransition skips the P/Invoke prolog that normally publishes the shadow SP.
        // Publish it here so the native callee allocates below this frame.
        if (call.IsUnmanaged && call.IsSuppressGCTransition)
        {
            GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((int)GetStackPointerRegIndex()));
            var stackPointer = unchecked((nint)_compiler.eeGetWasmWellKnownGlobals().stackPointer);
            GetEmitter().emitIns_I(INS_global_set, EA_HANDLE_CNS_RELOC, stackPointer);
        }

        if (target is not null)
        {
            // Wasm targets are table indices and must be consumed for call_indirect.
            genConsumeReg(target);

            parameters.callType = EC_INDIR_R;
            genEmitWasmCallWithCurrentGC(call, wasmSignature, ref parameters);
        }
        else
        {
            assert(call.IsHelperCall() || (call._callType is CT_USER_FUNC));

            if (call.IsHelperCall())
            {
                assert(!call.IsFastTailCall);

                if (call._directCallAddress is not null)
                {
                    parameters.addr = call._directCallAddress;
                }
                else
                {
                    var helperNum = Compiler.eeGetHelperNum(parameters.methHnd);
                    noway_assert(helperNum is not CORINFO_HELP_UNDEF);
                    var helperLookup = _compiler.compGetHelperFtn(helperNum);
                    assert(helperLookup.accessType is IAT_VALUE);
                    parameters.addr = helperLookup.addr;
                }
            }
            else
            {
                parameters.addr = call._directCallAddress;
            }

            parameters.callType = EC_FUNC_TOKEN;
            genEmitWasmCallWithCurrentGC(call, wasmSignature, ref parameters);
        }
    }

    private unsafe void genEmitWasmCallWithCurrentGC(
        GenTreeCall call, CORINFO_WASM_TYPE_SYMBOL_HANDLE wasmSignature, ref EmitCallParams parameters)
    {
        parameters.ptrVars = GCInfo.gcVarPtrSetCur;
        parameters.gcrefRegs = GCInfo.gcRegGCrefSetCur;
        parameters.byrefRegs = GCInfo.gcRegByrefSetCur;
        _ = wasmSignature;
        // The managed Wasm emitter has no call-instruction entry point yet.
        WasmCodegenDependencyNotPorted(call, "Emitter.emitIns_Call");
    }

    private unsafe void genEmitWasmCallWithCurrentGC(
        CORINFO_WASM_TYPE_SYMBOL_HANDLE wasmSignature, ref EmitCallParams parameters)
    {
        parameters.ptrVars = GCInfo.gcVarPtrSetCur;
        parameters.gcrefRegs = GCInfo.gcRegGCrefSetCur;
        parameters.byrefRegs = GCInfo.gcRegByrefSetCur;
        _ = wasmSignature;
        WasmCodegenDependencyNotPorted("Emitter.emitIns_Call");
    }

    // Keep Wasm helper signatures and target setup separate from the common helper-call NYI.
    private unsafe void genEmitHelperCallWasm(
        CorInfoHelpFunc helper, int argSize, emitAttr retSize, regNumber callTargetReg = REG_NA)
    {
        ensureCurrentFuncIsUnwindable();

        var parameters = new EmitCallParams();
        var helperFunction = _compiler.compGetHelperFtn(helper);
        parameters.ireg = callTargetReg;

        if (helperFunction.accessType is IAT_VALUE)
        {
            parameters.callType = EC_FUNC_TOKEN;
            parameters.addr = helperFunction.addr;
        }
        else
        {
            assert(helperFunction.accessType is IAT_PVALUE);
            parameters.addr = null;
            parameters.callType = EC_INDIR_R;
        }

        parameters.methHnd = Compiler.eeFindHelper(helper);
        parameters.argSize = argSize;
        parameters.retSize = retSize;

#pragma warning disable IDE0007
        CorInfoWasmType* types = stackalloc CorInfoWasmType[4];
#pragma warning restore IDE0007
        nint typeCount = 0;
        var helperIsManaged = false;

#if TARGET_64BIT
        const CorInfoWasmType wasmPointerType = CORINFO_WASM_TYPE_I64;
#else
        const CorInfoWasmType wasmPointerType = CORINFO_WASM_TYPE_I32;
#endif

        switch (helper)
        {
            // Managed throw helpers have no explicit arguments; their stack and PEP arguments follow the result.
            case CORINFO_HELP_RNGCHKFAIL:
            case CORINFO_HELP_OVERFLOW:
            case CORINFO_HELP_THROWDIVZERO:
            case CORINFO_HELP_THROWNULLREF:
            case CORINFO_HELP_THROW_ARGUMENTEXCEPTION:
            case CORINFO_HELP_THROW_ARGUMENTOUTOFRANGEEXCEPTION:
            case CORINFO_HELP_THROW_NOT_IMPLEMENTED:
            case CORINFO_HELP_THROW_PLATFORM_NOT_SUPPORTED:
            case CORINFO_HELP_THROW_TYPE_NOT_SUPPORTED:
            {
                types[0] = CORINFO_WASM_TYPE_VOID;
                types[1] = wasmPointerType;
                types[2] = wasmPointerType;
                typeCount = 3;
                helperIsManaged = true;
                break;
            }

            // RhpAssignRef and RhpCheckedAssignRef.
            case CORINFO_HELP_ASSIGN_REF:
            case CORINFO_HELP_CHECKED_ASSIGN_REF:
            {
                types[0] = CORINFO_WASM_TYPE_VOID;
                types[1] = wasmPointerType;
                types[2] = wasmPointerType;
                typeCount = 3;
                break;
            }

            // RhBulkMoveWithWriteBarrier helpers.
            case CORINFO_HELP_BULK_WRITEBARRIER:
            case CORINFO_HELP_BULK_WRITEBARRIER_SMALL:
            {
                types[0] = CORINFO_WASM_TYPE_VOID;
                types[1] = wasmPointerType;
                types[2] = wasmPointerType;
                types[3] = wasmPointerType;
                typeCount = 4;
                break;
            }

            default:
            {
                JITDUMP(
                    $"Helper '{_compiler.eeGetMethodFullName(parameters.methHnd)}' has no hard-coded signature\n");
                throw new FatalJitException(
                    CORJIT_INTERNALERROR, $"Wasm helper {helper} has no hard-coded signature.");
            }
        }

        // The managed helper signature includes PEP as its last parameter.
        var helperUsesPep = helperIsManaged &&
            _compiler.opts.jitFlags->IsSet(JitFlags.JIT_FLAG_PORTABLE_ENTRY_POINTS);
        if (helperIsManaged && !helperUsesPep)
        {
            typeCount--;
        }

        var wasmSignature = _compiler.info.compCompHnd->getWasmTypeSymbol(types, typeCount);

        if (helperUsesPep)
        {
            if (helperFunction.accessType is IAT_VALUE)
            {
                // Direct same-image managed helpers do not need a portable entrypoint.
                GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, 0);
            }
            else
            {
                // Push the PEP address from the helper's indirection cell.
                assert(helperFunction.accessType is IAT_PVALUE);
                GetEmitter().emitAddressConstant(unchecked((nint)helperFunction.addr));
                GetEmitter().emitIns_I(INS_I_load, EA_PTRSIZE, 0);
            }
        }

        if (parameters.callType is EC_INDIR_R)
        {
            // Push the call target by dereferencing the cell and then the PEP address.
            assert(helperFunction.accessType is IAT_PVALUE);
            GetEmitter().emitAddressConstant(unchecked((nint)helperFunction.addr));
            GetEmitter().emitIns_I(INS_I_load, EA_PTRSIZE, 0);
            GetEmitter().emitIns_I(INS_I_load, EA_PTRSIZE, 0);
        }

        genEmitWasmCallWithCurrentGC(wasmSignature, ref parameters);
    }

    private void genGCWriteBarrierWasm(GCInfo.WriteBarrierForm writeBarrierForm)
    {
        var helper = genWriteBarrierHelperForWriteBarrierForm(writeBarrierForm);
        genEmitHelperCallWasm(helper, 0, EA_PTRSIZE);
    }

    private static CorInfoWasmType WasmValueTypeToCorInfoWasmType(WasmValueType type)
    {
        return type switch
        {
            WasmValueType.I32 => CORINFO_WASM_TYPE_I32,
            WasmValueType.I64 => CORINFO_WASM_TYPE_I64,
            WasmValueType.F32 => CORINFO_WASM_TYPE_F32,
            WasmValueType.F64 => CORINFO_WASM_TYPE_F64,
            WasmValueType.V128 => CORINFO_WASM_TYPE_V128,
            // ExnRef is a valid Wasm type code but is not named in CorInfoWasmType.
            WasmValueType.ExnRef => unchecked((CorInfoWasmType)0x69),
            _ => throw new FatalJitException(CORJIT_INTERNALERROR, "Invalid WebAssembly value type."),
        };
    }

    private void genCodeForNullCheck(GenTreeIndir tree)
    {
        genConsumeAddress(tree.Addr);

        if ((tree.Flags & GTF_IND_NONFAULTING) == 0)
        {
            genEmitNullCheck(REG_NA, tree.Addr.Type);
        }
        else
        {
            GetEmitter().emitIns(INS_drop);
        }
    }

    private void genEmitNullCheck(regNumber reg, var_types refType)
    {
        var emitter = GetEmitter();

        if (reg is not REG_NA)
        {
            genEmitLocalGet(reg, WasmValueType.I);
        }

        if (refType is TYP_REF)
        {
            emitter.emitIns(INS_I_eqz);
        }
        else
        {
            assert(refType is TYP_BYREF || varTypeIsIntOrI(refType));
            emitter.emitIns_I(
                INS_I_const, EA_PTRSIZE, unchecked((nint)_compiler.compMaxUncheckedOffsetForNullObject));
            emitter.emitIns(INS_I_le_u);
        }

        genJumpToThrowHlpBlk(SCK_NULL_CHECK);
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
        assert(tree.OperIs(GT_LCLHEAP));
        assert(_compiler.compLocallocUsed);
        assert(IsFramePointerUsed);

        var needsZeroing = _compiler.info.compInitMem;
        var size = tree.AsOp().Op1;
        nuint reservedSpace = STACK_ALIGN;

        if (size.IsContainedIntOrIImmed)
        {
            var amount = unchecked((nuint)size.AsIntCon().IconValue);
            if (amount == 0)
            {
                GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, 0);
            }
            else
            {
                var alignmentMask = unchecked((nuint)(STACK_ALIGN - 1));
                amount = unchecked((amount + reservedSpace + alignmentMask) & ~alignmentMask);

                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
                GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)amount));
                GetEmitter().emitIns(INS_I_sub);
                GetEmitter().emitIns_I(INS_local_set, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));

                if (needsZeroing)
                {
                    GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
                    GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, 0);
                    GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)amount));
                    GetEmitter().emitIns_I(INS_memory_fill, EA_4BYTE, LINEAR_MEMORY_INDEX);
                }

                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
                GetEmitter().emitIns_I(ins_Store(TYP_I_IMPL), EA_PTRSIZE, TARGET_POINTER_SIZE);

                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
                GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, 0);
                GetEmitter().emitIns_I(ins_Store(TYP_I_IMPL), EA_PTRSIZE, 0);

                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
                GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)reservedSpace));
                GetEmitter().emitIns(INS_I_add);
            }
        }
        else
        {
            _ = genConsumeReg(size);

            if (genTypeSize(genActualType(size.Type)) < TARGET_POINTER_SIZE)
            {
                assert(TARGET_POINTER_SIZE == 8);
                GetEmitter().emitIns(INS_i64_extend_u_i32);
            }

            var internalRegisterCount = GetWasmInternalRegisterCount(tree);
            assert(internalRegisterCount == 1);
            var sizeReg = ExtractWasmInternalRegister(tree);
            assert(regNumberExtensions.WasmRegToType(sizeReg) == regNumberExtensions.TypeToWasmValueType(TYP_I_IMPL));

            GetEmitter().emitIns_I(
                INS_local_tee,
                EA_PTRSIZE,
                unchecked((nint)regNumberExtensions.WasmRegToIndex(sizeReg)));
            GetEmitter().emitIns(INS_I_eqz);
            genEmitIf(WasmValueType.I);
            GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, 0);
            GetEmitter().emitIns(INS_else);

            GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
            GetEmitter().emitIns_I(
                INS_local_get,
                EA_PTRSIZE,
                unchecked((nint)regNumberExtensions.WasmRegToIndex(sizeReg)));
            GetEmitter().emitIns_I(
                INS_I_const,
                EA_PTRSIZE,
                unchecked((nint)(reservedSpace + (nuint)STACK_ALIGN - (nuint)1)));
            GetEmitter().emitIns(INS_I_add);
            GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)~(nint)(STACK_ALIGN - 1)));
            GetEmitter().emitIns(INS_I_and);

            if (needsZeroing)
            {
                GetEmitter().emitIns_I(
                    INS_local_tee,
                    EA_PTRSIZE,
                    unchecked((nint)regNumberExtensions.WasmRegToIndex(sizeReg)));
            }

            GetEmitter().emitIns(INS_I_sub);
            GetEmitter().emitIns_I(INS_local_set, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));

            if (needsZeroing)
            {
                GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
                GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, 0);
                GetEmitter().emitIns_I(
                    INS_local_get,
                    EA_PTRSIZE,
                    unchecked((nint)regNumberExtensions.WasmRegToIndex(sizeReg)));
                GetEmitter().emitIns_I(INS_memory_fill, EA_4BYTE, LINEAR_MEMORY_INDEX);
            }

            GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
            GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetFramePointerRegIndex()));
            GetEmitter().emitIns_I(ins_Store(TYP_I_IMPL), EA_PTRSIZE, TARGET_POINTER_SIZE);

            GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
            GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, 0);
            GetEmitter().emitIns_I(ins_Store(TYP_I_IMPL), EA_PTRSIZE, 0);

            GetEmitter().emitIns_I(INS_local_get, EA_PTRSIZE, unchecked((nint)GetStackPointerRegIndex()));
            GetEmitter().emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)reservedSpace));
            GetEmitter().emitIns(INS_I_add);
            genEmitEndIf();
        }

        WasmProduceReg(tree);
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

    private unsafe void genCodeForStoreBlk(GenTreeBlk blkOp)
    {
        assert(blkOp.OperIs(GT_STORE_BLK));

        var isCopyBlk = blkOp.IsCopyBlkOp;
        var isNativeOp = blkOp._kind is GenTreeBlk.BlkOpKindNativeOpcode;
        var dstOnStack = blkOp.IsAddressNotOnHeap(_compiler);

        if (blkOp._kind is GenTreeBlk.BlkOpKindLoop)
        {
            assert(!isCopyBlk);
            genCodeForInitBlkLoop(blkOp);
            genUpdateLife(blkOp);
            return;
        }

        // Stack destinations do not need write barriers and must use a native memory operation.
        assert(!dstOnStack || isNativeOp);

#if DEBUG
        // Without a native memory operation, this path must be copying a type with GC pointers.
        assert(isNativeOp || blkOp.Layout.HasGCPtr);
#endif

        var nullCheckDest = (blkOp.Flags & GTF_IND_NONFAULTING) == 0;
        var nullCheckSrc = false;
        var dest = blkOp.Addr;
        var destType = TYP_UNKNOWN;
        var src = blkOp.Data;
        var srcType = TYP_UNKNOWN;
        var destReg = REG_NA;
        var srcReg = REG_NA;
        uint destOffset = 0;
        uint srcOffset = 0;

        // Unwrap a contained GT_INIT_VAL so its fill value, not the wrapper, is pushed onto the value stack.
        var srcForConsume = src.Oper is GT_INIT_VAL ? src.AsUnOp().Op1 : src;
        assert(src.Oper is not GT_INIT_VAL || src.IsContained);

        genConsumeRegs(dest);
        genConsumeRegs(srcForConsume);

        if (src.Oper is GT_IND)
        {
            // A byref or pointer source is a GT_IND; unwrap it to get the address to load from.
            nullCheckSrc = (src.Flags & GTF_IND_NONFAULTING) == 0;
            src = src.AsUnOp().Op1;

            // Match lowering by fetching a register only when this source is expected to need one.
            if (!isNativeOp || nullCheckSrc)
            {
                srcReg = GetMultiUseOperandReg(src);
                srcType = src.Type;
            }

            assert(!src.IsContained);
        }
        else if (src.OperIs(GT_CNS_INT, GT_INIT_VAL))
        {
            if (src.Oper is GT_INIT_VAL)
            {
                src = src.AsUnOp().Op1;
            }

            assert(!src.IsContained);
            assert(!isCopyBlk);
            assert(isNativeOp);
        }
        else
        {
            assert(src.OperIs(GT_LCL_VAR, GT_LCL_FLD));
            var lclVar = src.AsLclVarCommon();
            srcReg = GetFramePointerReg(_compiler.funCurrentFuncIdx());
            var frameOffset = _compiler.lvaFrameAddress(lclVar.LclNum, out var framePointerBased);
            srcOffset = unchecked((uint)(frameOffset + lclVar.LclOffs));
            srcType = TYP_BYREF;
            assert(framePointerBased);
        }

        if (dest.Oper is GT_LCL_ADDR)
        {
            var lclVar = dest.AsLclVarCommon();
            destReg = GetFramePointerReg(_compiler.funCurrentFuncIdx());
            var frameOffset = _compiler.lvaFrameAddress(lclVar.LclNum, out var framePointerBased);
            destOffset = unchecked((uint)(frameOffset + lclVar.LclOffs));
            destType = TYP_BYREF;
            assert(framePointerBased);
        }
        else if (isNativeOp && !nullCheckDest)
        {
            // Native memory.fill with no null check has no multiply-used destination register to fetch.
        }
        else if (isCopyBlk || nullCheckDest)
        {
            destReg = GetMultiUseOperandReg(dest);
            destType = dest.Type;
        }
        else
        {
            assert(isNativeOp);
        }

        if (nullCheckDest)
        {
            assert(destType is not TYP_UNKNOWN);
            genEmitNullCheck(destReg, destType);
        }

        if (nullCheckSrc)
        {
            assert(srcType is not TYP_UNKNOWN);
            genEmitNullCheck(srcReg, srcType);
        }

        var emit = GetEmitter();

        if (isNativeOp)
        {
            if (src.IsContained)
            {
                assert(isCopyBlk);
                assert(srcReg != REG_NA);
                // A contained source may not be on the value stack, so manufacture its address.
                genEmitLocalGet(srcReg, WasmValueType.I);
                if (srcOffset != 0)
                {
                    emit.emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)srcOffset));
                    emit.emitIns(INS_I_add);
                }
            }

            emit.emitIns_I(INS_i32_const, EA_4BYTE, unchecked((nint)blkOp.Size));
            emit.emitIns_I(isCopyBlk ? INS_memory_copy : INS_memory_fill, EA_4BYTE, LINEAR_MEMORY_INDEX);
            genUpdateLife(blkOp);
            return;
        }

        assert(!dest.IsContained);
        // The operands may be on the evaluation stack, but cannot be reliably used here, so drop them.
        emit.emitIns(INS_drop);
        if (!src.IsContained)
        {
            emit.emitIns(INS_drop);
        }

        if (blkOp.IsVolatile)
        {
            // TODO-WASM: Memory barrier
        }

        var layout = blkOp.Layout;
        var slots = layout.SlotCount;
        var gcPtrCount = unchecked((uint)layout.GCPtrCount);
        var i = 0;

        while (i < slots)
        {
            if (!layout.IsGCPtr(i))
            {
                // Copy non-GC slots with pointer-sized loads and stores.
                genEmitLocalGet(destReg, WasmValueType.I);
                genEmitLocalGet(srcReg, WasmValueType.I);
                emit.emitIns_I(INS_I_load, EA_PTRSIZE, unchecked((nint)srcOffset));
                emit.emitIns_I(INS_I_store, EA_PTRSIZE, unchecked((nint)destOffset));
            }
            else
            {
                // Compute the slot address and use a write barrier for each GC pointer.
                genEmitLocalGet(destReg, WasmValueType.I);
                emit.emitIns_I(INS_I_const, EA_PTRSIZE, unchecked((nint)destOffset));
                emit.emitIns(INS_I_add);
                genEmitLocalGet(srcReg, WasmValueType.I);
                emit.emitIns_I(INS_I_load, EA_PTRSIZE, unchecked((nint)srcOffset));
                // The helper omits SP/PEP, so only the destination and reference go on the stack.
                genEmitHelperCallWasm(CORINFO_HELP_CHECKED_ASSIGN_REF, 0, EA_PTRSIZE);
                gcPtrCount = unchecked(gcPtrCount - 1u);
            }

            i++;
            destOffset = unchecked(destOffset + TARGET_POINTER_SIZE);
            srcOffset = unchecked(srcOffset + TARGET_POINTER_SIZE);
        }

        assert(gcPtrCount == 0);

        if (blkOp.IsVolatile)
        {
            // TODO-WASM: Memory barrier
        }

        genUpdateLife(blkOp);
    }

    private void genCodeForInitBlkLoop(GenTreeBlk blkOp)
    {
        // TODO-WASM: In multi-threaded Wasm we will need to generate a for loop that atomically zeroes one GC ref
        //  at a time. Right now we're single-threaded, so we can just use memory.fill.
        assert(!WasmThreadSupport);

        // FIXME-WASM: We're missing a null check here.

        genConsumeOperands(blkOp);
        // Emit the value constant expected by the memory.fill opcode (zero)
        GetEmitter().emitIns_I(INS_i32_const, EA_4BYTE, 0);
        // Emit the size constant expected by the memory.copy and memory.fill opcodes
        GetEmitter().emitIns_I(INS_i32_const, EA_4BYTE, unchecked((nint)blkOp.Size));
        GetEmitter().emitIns_I(INS_memory_fill, EA_8BYTE, LINEAR_MEMORY_INDEX);
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
