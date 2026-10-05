// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

#if TARGET_WASM
public sealed partial class Lowering
{
    private void AfterLowerBlocksWasm()
    {
        // Stackification runs before liveness so any newly introduced locals participate in register allocation.
        var stackifier = new Stackifier(this);
        foreach (var block in CompilerInstance.Blocks)
        {
            stackifier.StackifyBlock(block);
        }
    }

    private sealed class Temporary(int localNumber)
    {
        public int LocalNumber { get; } = localNumber;
        public Temporary? Previous { get; set; }
    }

    private sealed class Stackifier
    {
        private readonly Lowering _lowering;
        private readonly Compiler _compiler;
        private readonly ArrayStack<StackifierUse> _uses = new();
        private readonly int _minimumTempLocalNumber;
        private readonly Temporary?[] _availableTemps = new Temporary?[(int)TYP_COUNT];
        private readonly Temporary?[] _inUseTemps = new Temporary?[(int)TYP_COUNT];
        private bool _anyChanges;

        public Stackifier(Lowering lowering)
        {
            _lowering = lowering;
            _compiler = lowering.CompilerInstance;
            _minimumTempLocalNumber = _compiler.lvaCount;
        }

        public void StackifyBlock(BasicBlock block)
        {
            _anyChanges = false;
            _lowering._block = block;

            for (var node = block.LastNode; node is not null;)
            {
                assert(IsDataFlowRoot(node));
                node = StackifyTree(node);
                ReleaseTemporaries();
            }

            _lowering._block = null;
            JITDUMP($"{FMT_BB(block.bbNum)}: {(_anyChanges ? "stackified with some changes" : "already in WASM value stack order")}\n");
        }

        private GenTree? StackifyTree(GenTree root)
        {
            var initialDepth = _uses.Height();
            _uses.Push(default);

            var lastStackified = root.Next;
            while (_uses.Height() != initialDepth)
            {
                var use = _uses.Pop();
                var node = use.IsRoot ? root : use.GetValue();
                var previous = lastStackified is not null ? lastStackified.Prev : root;

                while (node != previous)
                {
                    if ((previous is not null) && IsDataFlowRoot(previous))
                    {
                        previous = StackifyTree(previous) ??
                            throw new InvalidOperationException("A Wasm stackification root has no preceding node.");
                        continue;
                    }

                    var insertionPoint = previous ??
                        throw new InvalidOperationException("A Wasm stackification use has no preceding node.");
                    if (CanMoveForward(node, out var reason))
                    {
                        MoveForward(node, insertionPoint, reason);
                    }
                    else
                    {
                        node = ReplaceWithTemporary(use, ref root, insertionPoint);
                    }

                    _anyChanges = true;
                    break;
                }

                _ = node.VisitOperandUses((ref GenTree operand) =>
                {
                    _uses.Push(StackifierUse.FromUse(ref operand, node));
                    return GenTree.VisitResult.Continue;
                });
                lastStackified = node;
            }

            var lastNode = lastStackified ??
                throw new InvalidOperationException("Wasm stackification did not process a node.");
            return lastNode.Prev;
        }

        private static bool IsDataFlowRoot(GenTree node)
        {
            return !node.IsValue || node.IsUnusedValue;
        }

        private bool CanMoveForward(GenTree node, out string reason)
        {
            if (node.IsInvariant)
            {
                reason = "invariant";
                return true;
            }

            if (node.IsContained)
            {
                reason = "contained";
                return true;
            }

            if ((node.Oper is GT_LCL_VAR) &&
                !_compiler.lvaGetDesc(node.AsLclVarCommon().LclNum).IsAddressExposed)
            {
                reason = "local";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private void MoveForward(GenTree node, GenTree previous, string reason)
        {
#if DEBUG
            JITDUMP($"Stackifier moving [{node.TreeId:D6}] after [{previous.TreeId:D6}]: {reason}\n");
#endif
            var endExclusive = previous.Next
                ?? throw new InvalidOperationException("A Wasm stackifier move requires an exclusive range endpoint.");
            assert(_lowering.IsInvariantInRange(node, endExclusive));
            _lowering.BlockRange().Remove(node);
            _lowering.BlockRange().InsertAfter(previous, node);
        }

        private GenTreeLclVar ReplaceWithTemporary(StackifierUse use, ref GenTree root, GenTree previous)
        {
            var node = use.IsRoot ? root : use.GetValue();
            var localType = genActualType(node.Type);
            var localNumber = RequestTemporary(node.Type);
            var localStore = _compiler.gtNewStoreLclVarNode(localNumber, node);
            var localNode = _compiler.gtNewLclVarNode(localType, localNumber);

            _lowering.BlockRange().InsertAfter(node, localStore);
            _lowering.BlockRange().InsertAfter(previous, localNode);

            if (use.IsRoot)
            {
                root = localNode;
            }
            else
            {
                ref var edge = ref use.GetUse();
                var lirUse = new LIR.Use(_lowering.BlockRange(), ref edge, use.GetUser());
                lirUse.ReplaceWith(localNode);
            }

#if DEBUG
            JITDUMP($"Replaced [{node.TreeId:D6}] with a temporary:\n");
#endif
            DISPNODE(node);
            DISPNODE(localNode);

            if ((node._lirFlags & LIR.Flags.MultiplyUsed) == LIR.Flags.MultiplyUsed)
            {
                JITDUMP("Transferring multiply-used flag from old node to new temporary.\n");
                node._lirFlags &= ~LIR.Flags.MultiplyUsed;
                SetMultiplyUsed(localNode
#if DEBUG
                    , "Transferred flag during stackification"
#endif
                );
            }

            return localNode;
        }

        private int RequestTemporary(var_types type)
        {
            assert(varTypeIsEnregisterable(type));

            var actualType = genActualType(type);
            var typeIndex = (int)actualType;
            var local = Remove(ref _availableTemps[typeIndex]);
            int localNumber;
            if (local is not null)
            {
                localNumber = local.LocalNumber;
                assert(_compiler.lvaGetDesc(localNumber).Type == actualType);
            }
            else
            {
                localNumber = _compiler.lvaGrabTemp(true, "Stackifier temporary");
                assert(localNumber >= _minimumTempLocalNumber);
                ref var variableDescriptor = ref _compiler.lvaGetDesc(localNumber);
                variableDescriptor.Type = actualType;

                local = new Temporary(localNumber);
            }

            Append(ref _inUseTemps[typeIndex], local);
            JITDUMP($"Temporary V{localNumber:D2} is now in use\n");
            return localNumber;
        }

        private void ReleaseTemporaries()
        {
            if (_minimumTempLocalNumber == _compiler.lvaCount)
            {
                return;
            }

            assert(_minimumTempLocalNumber < _compiler.lvaCount);
            JITDUMP("Releasing stackifier temporaries:\n");
            for (var typeIndex = 0; typeIndex < (int)TYP_COUNT; typeIndex++)
            {
                while (_inUseTemps[typeIndex] is not null)
                {
                    var temporary = Remove(ref _inUseTemps[typeIndex]) ??
                        throw new InvalidOperationException("A stackifier temporary disappeared while in use.");
                    assert(temporary.LocalNumber >= _minimumTempLocalNumber);
                    Append(ref _availableTemps[typeIndex], temporary);
                    JITDUMP($"Temporary V{temporary.LocalNumber:D2} is now available\n");
                }
            }
        }

        private static Temporary? Remove(ref Temporary? temporaries)
        {
            var temporary = temporaries;
            if (temporary is not null)
            {
                temporaries = temporary.Previous;
            }

            return temporary;
        }

        private static void Append(ref Temporary? temporaries, Temporary temporary)
        {
            temporary.Previous = temporaries;
            temporaries = temporary;
        }
    }

    private readonly struct StackifierUse
    {
        private readonly int _operandIndex;

        private StackifierUse(GenTree user, int operandIndex)
        {
            User = user;
            _operandIndex = operandIndex;
        }

        public GenTree? User { get; }
        public bool IsRoot => User is null;
        public GenTree GetUser() => User ?? throw new InvalidOperationException("A root use has no owning node.");

        public static StackifierUse FromUse(ref GenTree use, GenTree user)
        {
            var index = 0;
            foreach (ref var operand in user.UseEdges)
            {
                if (System.Runtime.CompilerServices.Unsafe.AreSame(ref operand, ref use))
                {
                    return new StackifierUse(user, index);
                }

                index++;
            }

            throw new InvalidOperationException("The visited operand does not belong to its user.");
        }

        public ref GenTree GetUse()
        {
            var user = User ?? throw new InvalidOperationException("A root use has no operand edge.");
            var index = 0;
            foreach (ref var operand in user.UseEdges)
            {
                if (index++ == _operandIndex)
                {
                    return ref operand;
                }
            }

            throw new InvalidOperationException("The recorded operand is no longer present.");
        }

        public GenTree GetValue()
        {
            return GetUse();
        }
    }

#if FEATURE_HW_INTRINSICS
    private GenTree? LowerHWIntrinsicWasm(GenTreeHWIntrinsic node)
    {
        var intrinsic = node.HWIntrinsicId;
        var category = HWIntrinsicInfo.lookupCategory(intrinsic);
        var hasImmediateOperand = HWIntrinsicInfo.HasImmediateOperand(intrinsic);

        if (node.IsMemoryLoad(out var address) || node.IsMemoryStore(out address))
        {
            SetMultiplyUsed(address
#if DEBUG
                , "LowerHWIntrinsic memory address (null check)"
#endif
            );
        }

        switch (intrinsic)
        {
            case NI_Vector_ConditionalSelect:
            {
                return LowerHWIntrinsicCndSelWasm(node);
            }

            case NI_Vector_Create:
            case NI_Vector_CreateScalar:
            {
                return LowerHWIntrinsicCreateWasm(node);
            }

            case NI_Vector_CreateScalarUnsafe:
            {
                node.ChangeHWIntrinsicId(NI_PackedSimd_Splat);
                return LowerNode(node);
            }

            case NI_Vector_op_Equality:
            {
                assert(category is HW_Category_Helper);
                return LowerHWIntrinsicCmpOpWasm(node, GT_EQ);
            }

            case NI_Vector_op_Inequality:
            {
                assert(category is HW_Category_Helper);
                return LowerHWIntrinsicCmpOpWasm(node, GT_NE);
            }

            case NI_PackedSimd_CompareLessThan:
            case NI_PackedSimd_CompareLessThanOrEqual:
            case NI_PackedSimd_CompareGreaterThan:
            case NI_PackedSimd_CompareGreaterThanOrEqual:
            {
                if (node.SimdBaseType is TYP_ULONG)
                {
                    return LowerHWIntrinsicCompareUnsignedLongWasm(node);
                }

                break;
            }

            case NI_Vector_GetElement:
            {
                node.ChangeHWIntrinsicId(NI_PackedSimd_ExtractScalar);
                return LowerHWIntrinsicWithImmWasm(node);
            }

            case NI_Vector_ToScalar:
            {
                var index = CompilerInstance.gtNewIconNode(TYP_INT, 0);
                BlockRange().InsertBefore(node, index);
                _ = LowerNode(index);
                node.ResetHWIntrinsicId(NI_PackedSimd_ExtractScalar, node.GetOp(1), index);
                return LowerHWIntrinsicWithImmWasm(node);
            }

            case NI_Vector_WithElement:
            {
                node.ChangeHWIntrinsicId(NI_PackedSimd_ReplaceScalar);
                return LowerHWIntrinsicWithImmWasm(node);
            }

            case NI_PackedSimd_ExtractScalar:
            case NI_PackedSimd_ReplaceScalar:
            case NI_PackedSimd_LoadScalarAndInsert:
            case NI_PackedSimd_StoreSelectedScalar:
            {
                assert(hasImmediateOperand);
                return LowerHWIntrinsicWithImmWasm(node);
            }

            case NI_PackedSimd_Shuffle:
            {
                return LowerHWIntrinsicNativeShuffleWasm(node);
            }

            case NI_PackedSimd_LoadScalarAndSplatVector128:
            case NI_PackedSimd_LoadScalarVector128:
            case NI_PackedSimd_LoadWideningVector128:
            {
                assert(!hasImmediateOperand);
                break;
            }

            case NI_PackedSimd_Swizzle:
            {
                assert(category is HW_Category_SIMD);
                LowerHWIntrinsicSwizzleWasm(node);
                return node.Next;
            }

            default:
            {
                assert(category is HW_Category_SIMD);
                break;
            }
        }

        ContainCheckHWIntrinsicWasm(node);
        return node.Next;
    }

    private GenTree? LowerHWIntrinsicWithImmWasm(GenTreeHWIntrinsic node)
    {
        var immediateOperand = node.GetOp(node.Operands.Length);
        assert(varTypeIsIntegral(immediateOperand.Type));

        if (!immediateOperand.Oper.IsCnsIntOrI)
        {
            // The non-constant index expands to a nested-block jump table; its operands must be locals.
            for (var operandIndex = 1; operandIndex <= node.Operands.Length; operandIndex++)
            {
                var operand = node.GetOp(operandIndex);
                SetMultiplyUsed(operand
#if DEBUG
                    , "Non-constant imm op needs jump table fallback"
#endif
                );
            }
        }

        ContainCheckHWIntrinsicWasm(node);
        return node.Next;
    }

    private void LowerHWIntrinsicSwizzleWasm(GenTreeHWIntrinsic node)
    {
        assert(node.HWIntrinsicId is NI_PackedSimd_Swizzle);
        var source = node.GetOp(1);
        var maskNode = node.GetOp(2);

        // A constant in-range mask can use i8x16.shuffle, which consumes the source twice.
        if (maskNode.Oper.IsCnsVec)
        {
            var mask = maskNode.AsVecCon().SimdVal;
            var allInRange = true;
            for (var lane = 0; lane < 16; lane++)
            {
                if (mask.u8[lane] >= 16)
                {
                    allInRange = false;
                    break;
                }
            }

            if (allInRange)
            {
                MakeSrcContained(node, maskNode);
                SetMultiplyUsed(source
#if DEBUG
                    , "i8x16.shuffle reuses the source as both operands"
#endif
                );
            }
        }

        ContainCheckHWIntrinsicWasm(node);
    }

    private GenTree? LowerHWIntrinsicCompareUnsignedLongWasm(GenTreeHWIntrinsic node)
    {
        assert(node.SimdBaseType is TYP_ULONG);
        assert(node.SimdSize is 16);

        var op1 = node.GetOp(1);
        var op2 = node.GetOp(2);

        // Flipping the sign bit maps unsigned ordering to signed i64 ordering.
        var signMaskA = CompilerInstance.gtNewVconNode(TYP_SIMD16);
        signMaskA.SimdVal.u64[0] = 0x8000000000000000UL;
        signMaskA.SimdVal.u64[1] = 0x8000000000000000UL;

        var signMaskB = CompilerInstance.gtNewVconNode(TYP_SIMD16);
        signMaskB.SimdVal.u64[0] = 0x8000000000000000UL;
        signMaskB.SimdVal.u64[1] = 0x8000000000000000UL;

        var xorA = CompilerInstance.gtNewSimdHWIntrinsicNode(
            TYP_SIMD16, NI_PackedSimd_Xor, TYP_LONG, 16, op1, signMaskA);
        var xorB = CompilerInstance.gtNewSimdHWIntrinsicNode(
            TYP_SIMD16, NI_PackedSimd_Xor, TYP_LONG, 16, op2, signMaskB);

        BlockRange().InsertAfter(op1, signMaskA, xorA);
        BlockRange().InsertAfter(op2, signMaskB, xorB);

        _ = LowerNode(signMaskA);
        _ = LowerNode(signMaskB);
        _ = LowerNode(xorA);
        _ = LowerNode(xorB);

        node.SetOp(1, xorA);
        node.SetOp(2, xorB);
        node.SimdBaseType = TYP_LONG;

        ContainCheckHWIntrinsicWasm(node);
        return node.Next;
    }

    private GenTree? LowerHWIntrinsicCmpOpWasm(GenTreeHWIntrinsic node, genTreeOps compareOperation)
    {
        var intrinsicId = node.HWIntrinsicId;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        var simdType = Compiler.GetSimdTypeForSize(simdSize);

        assert((intrinsicId is NI_Vector_op_Equality) || (intrinsicId is NI_Vector_op_Inequality));
        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(simdBaseType));
        assert(simdSize != 0);
        assert(node.Type is TYP_INT);
        assert((compareOperation is GT_EQ) || (compareOperation is GT_NE));

        var compareIntrinsic = NI_PackedSimd_CompareEqual;
        var reduceIntrinsic = NI_PackedSimd_AllTrue;
        if (intrinsicId is NI_Vector_op_Inequality)
        {
            compareIntrinsic = NI_PackedSimd_CompareNotEqual;
            reduceIntrinsic = NI_PackedSimd_AnyTrue;
        }

        // Equality is reduced with AllTrue; inequality is reduced with AnyTrue.
        var comparison = CompilerInstance.gtNewSimdHWIntrinsicNode(
            simdType, compareIntrinsic, simdBaseType, simdSize, node.GetOp(1), node.GetOp(2));
        BlockRange().InsertBefore(node, comparison);
        _ = LowerNode(comparison);

        node.Type = TYP_INT;
        node.ResetHWIntrinsicId(reduceIntrinsic, comparison);
        if (simdBaseType is TYP_FLOAT)
        {
            node.SimdBaseType = TYP_INT;
        }
        else if (simdBaseType is TYP_DOUBLE)
        {
            node.SimdBaseType = TYP_LONG;
        }
        else
        {
            assert(varTypeIsIntegral(simdBaseType));
        }

        return LowerNode(node);
    }

    private GenTree? LowerHWIntrinsicCndSelWasm(GenTreeHWIntrinsic node)
    {
        var simdType = node.Type;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;

        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(simdBaseType));
        assert(simdSize != 0);

        var condition = node.GetOp(1);
        var selectTrue = node.GetOp(2);
        var selectFalse = node.GetOp(3);

        if (selectFalse.IsVectorZero)
        {
            BlockRange().Remove(selectFalse);
            node.ResetHWIntrinsicId(NI_PackedSimd_And, condition, selectTrue);
        }
        else if (selectTrue.IsVectorZero)
        {
            BlockRange().Remove(selectTrue);
            node.ResetHWIntrinsicId(NI_PackedSimd_AndNot, selectFalse, condition);
        }
        else
        {
            node.ResetHWIntrinsicId(NI_PackedSimd_BitwiseSelect, selectTrue, selectFalse, condition);
        }

        return LowerNode(node);
    }

    private GenTree? LowerHWIntrinsicCreateWasm(GenTreeHWIntrinsic node)
    {
        var intrinsicId = node.HWIntrinsicId;
        var simdType = node.Type;
        var simdBaseType = node.SimdBaseType;
        var simdSize = node.SimdSize;
        simd_t simdValue = default;

        assert(varTypeIsSimd(simdType));
        assert(varTypeIsArithmetic(simdBaseType));
        assert(simdSize != 0);

        var isConstant = GenTreeVecCon.IsHWIntrinsicCreateConstant(node, ref simdValue);
        var isCreateScalar = HWIntrinsicInfo.IsVectorCreateScalar(intrinsicId);
        var argumentCount = node.Operands.Length;

        if (isConstant)
        {
            foreach (var argument in node.Operands)
            {
                BlockRange().Remove(argument);
            }

            var vectorConstant = CompilerInstance.gtNewVconNode(simdType);
            vectorConstant.SimdVal = simdValue;
            BlockRange().InsertBefore(node, vectorConstant);
            if (BlockRange().TryGetUse(node, out var use))
            {
                use.ReplaceWith(vectorConstant);
            }
            else
            {
                vectorConstant.IsUnusedValue = true;
            }

            BlockRange().Remove(node);
            return LowerNode(vectorConstant);
        }

        if (argumentCount == 1)
        {
            if (isCreateScalar)
            {
                var operand = node.GetOp(1);
                var zero = CompilerInstance.gtNewZeroConNode(simdType);
                BlockRange().InsertBefore(operand, zero);
                _ = LowerNode(zero);

                var index = CompilerInstance.gtNewIconNode(TYP_INT, 0);
                BlockRange().InsertAfter(zero, index);
                _ = LowerNode(index);

                node.ResetHWIntrinsicId(NI_PackedSimd_ReplaceScalar, zero, index, operand);
                return LowerNode(node);
            }

            node.ChangeHWIntrinsicId(NI_PackedSimd_Splat);
            return LowerNode(node);
        }

        var temporary = InsertNewSimdCreateScalarUnsafeNode(simdType, node.GetOp(1), simdBaseType, simdSize);
        var lane = 1;
        for (; lane < argumentCount - 1; lane++)
        {
            var operand = node.GetOp(lane + 1);
            var insertionPoint = LIR.LastNode(temporary, operand);
            var index = CompilerInstance.gtNewIconNode(TYP_INT, lane);
            temporary = CompilerInstance.gtNewSimdHWIntrinsicNode(
                simdType, NI_PackedSimd_ReplaceScalar, simdBaseType, simdSize, temporary, index, operand);
            BlockRange().InsertAfter(insertionPoint, index, temporary);
            _ = LowerNode(temporary);
        }

        assert(lane == argumentCount - 1);
        var lastOperand = node.GetOp(argumentCount);
        var lastIndex = CompilerInstance.gtNewIconNode(TYP_INT, lane);
        BlockRange().InsertBefore(lastOperand, lastIndex);
        node.ResetHWIntrinsicId(NI_PackedSimd_ReplaceScalar, temporary, lastIndex, lastOperand);

        return LowerNode(node);
    }

    private GenTree? LowerHWIntrinsicNativeShuffleWasm(GenTreeHWIntrinsic node)
    {
        assert(node.HWIntrinsicId is NI_PackedSimd_Shuffle);

        var firstVector = node.GetOp(1);
        var shuffleMask = node.GetOp(3);
        var resultType = node.Type;

        // An in-range constant mask can be emitted directly as i8x16.shuffle.
        if (shuffleMask.Oper.IsCnsVec)
        {
            var mask = shuffleMask.AsVecCon().SimdVal;
            var allInRange = true;
            for (var lane = 0; lane < 16; lane++)
            {
                if (mask.u8[lane] >= 32)
                {
                    allInRange = false;
                    break;
                }
            }

            if (allInRange)
            {
                ContainCheckHWIntrinsicWasm(node);
                return node.Next;
            }
        }

        var compiler = CompilerInstance;
        var secondVectorUse = new LIR.Use(BlockRange(), ref node.GetOpRef(2), node);
        _ = secondVectorUse.ReplaceWithLclVar(compiler);
        var secondVectorReload = node.GetOp(2);
        BlockRange().Remove(secondVectorReload);

        var shuffleMaskUse = new LIR.Use(BlockRange(), ref node.GetOpRef(3), node);
        var shuffleMaskLocal = shuffleMaskUse.ReplaceWithLclVar(compiler);
        var maskReloadForCheck = node.GetOp(3);

        var bound = compiler.gtNewIconNode(TYP_INT, 32);
        BlockRange().InsertBefore(node, bound);
        _ = LowerNode(bound);

        // Swizzle masks have an observable out-of-range exception that the shuffle opcode lacks.
        var boundsCheck = new GenTreeBoundsChk(maskReloadForCheck, bound, SCK_ARG_RNG_EXCPN);
        BlockRange().InsertBefore(node, boundsCheck);
        _ = LowerNode(boundsCheck);

        var maskReload1 = compiler.gtNewLclVarNode(shuffleMask.Type, shuffleMaskLocal);
        BlockRange().InsertBefore(node, maskReload1);
        _ = LowerNode(maskReload1);

        var swizzle1 = compiler.gtNewSimdHWIntrinsicNode(
            resultType, NI_PackedSimd_Swizzle, TYP_BYTE, 16, firstVector, maskReload1);
        BlockRange().InsertBefore(node, swizzle1);
        _ = LowerNode(swizzle1);

        BlockRange().InsertBefore(node, secondVectorReload);
        _ = LowerNode(secondVectorReload);

        var maskReload2 = compiler.gtNewLclVarNode(shuffleMask.Type, shuffleMaskLocal);
        BlockRange().InsertBefore(node, maskReload2);
        _ = LowerNode(maskReload2);

        var upperBound = compiler.gtNewVconNode(shuffleMask.Type);
        upperBound.EvaluateBroadcastInPlace(TYP_BYTE, 16L);
        BlockRange().InsertBefore(node, upperBound);
        _ = LowerNode(upperBound);

        var upperMask = compiler.gtNewSimdHWIntrinsicNode(
            shuffleMask.Type, NI_PackedSimd_Subtract, TYP_BYTE, 16, maskReload2, upperBound);
        BlockRange().InsertBefore(node, upperMask);
        _ = LowerNode(upperMask);

        var swizzle2 = compiler.gtNewSimdHWIntrinsicNode(
            resultType, NI_PackedSimd_Swizzle, TYP_BYTE, 16, secondVectorReload, upperMask);
        BlockRange().InsertBefore(node, swizzle2);
        _ = LowerNode(swizzle2);

        var result = compiler.gtNewSimdHWIntrinsicNode(
            resultType, NI_PackedSimd_Or, TYP_BYTE, 16, swizzle1, swizzle2);
        BlockRange().InsertBefore(node, result);

        if (BlockRange().TryGetUse(node, out var use))
        {
            use.ReplaceWith(result);
        }
        else
        {
            result.IsUnusedValue = true;
        }

        BlockRange().Remove(node);
        return LowerNode(result);
    }

    private void ContainCheckHWIntrinsicWasm(GenTreeHWIntrinsic node)
    {
        var intrinsicId = node.HWIntrinsicId;
        if (HWIntrinsicInfo.HasImmediateOperand(intrinsicId))
        {
            var immediateOperand = node.GetOp(node.Operands.Length);
            if (immediateOperand.Oper.IsCnsIntOrI)
            {
                MakeSrcContained(node, immediateOperand);
            }
        }
        else if ((intrinsicId is NI_PackedSimd_Shuffle) && node.GetOp(3).Oper.IsCnsVec)
        {
            MakeSrcContained(node, node.GetOp(3));
        }
    }
#endif
}
#endif
