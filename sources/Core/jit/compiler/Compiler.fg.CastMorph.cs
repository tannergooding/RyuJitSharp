// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
    private bool fgCastRequiresHelper(var_types fromType, var_types toType, bool overflow = false)
    {
        if (overflow && varTypeIsFloating(fromType))
        {
            assert(varTypeIsIntegral(toType));
            return true;
        }

        if ((Configuration.TARGET_X86 || Configuration.TARGET_ARM) &&
            ((varTypeIsLong(fromType) && varTypeIsFloating(toType))
            || (varTypeIsFloating(fromType) && varTypeIsLong(toType))))
        {
            return !Configuration.TARGET_X86 || !compOpportunisticallyDependsOn(InstructionSet_AVX512);
        }

        return false;
    }

    private GenTree fgMorphCastIntoHelper(GenTree tree, CorInfoHelpFunc helper, GenTree operand)
    {
        if (operand.Oper.IsConst)
        {
            var original = tree;
            tree = gtFoldExprConst(tree);
            if (tree != original)
            {
                return fgMorphTree(tree);
            }
            if (tree.Oper.IsConst)
            {
                return fgMorphConst(tree);
            }

            noway_assert((tree.Oper is GT_CAST) && (tree.AsCast().CastOp == operand));
        }

        return fgMorphIntoHelperCall(tree, helper, morphArgs: true, operand);
    }

    private unsafe GenTree fgMorphIntoHelperCall(GenTree tree, CorInfoHelpFunc helper, bool morphArgs,
        GenTree? arg1 = null, GenTree? arg2 = null)
    {
        // The call has the original expression's value and logical identity,
        // but needs a new managed node with its own argument storage.
        var call = new GenTreeCall(tree) {
            _callType = CT_HELPER,
            _returnType = tree.Type,
            _callMethHnd = eeFindHelper(helper),
            _retClsHnd = null,
            _callMoreFlags = GTF_CALL_M_EMPTY,
            _controlExpr = null,
        };
        if (Configuration.DEBUG)
        {
            call._callDebugFlags = GTF_CALL_MD_EMPTY;
            call._inlineObservation = InlineObservation.CALLSITE_IS_CALL_TO_HELPER;
            call._callSig = default;
        }
        call.ClearInlineInfo();
        if (Configuration.UNIX_X86_ABI)
        {
            call.Flags |= GTF_CALL_POP_ARGS;
        }

        if (Configuration.FEATURE_READYTORUN)
        {
            call._entryPoint = new CORINFO_CONST_LOOKUP { accessType = IAT_VALUE };
        }
        call.ResetReturnType();
        call.ClearOtherRegs();

        if (Configuration.TARGET_32BIT && varTypeIsLong(tree.Type))
        {
            call.InitializeLongReturnType();
        }

        if (call.MayThrow(this))
        {
            call.Flags |= GTF_EXCEPT;
        }
        else
        {
            call.Flags &= ~GTF_EXCEPT;
        }
        call.Flags |= GTF_CALL;

        if (arg2 is not null)
        {
            _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(arg2));
            call.AddAllEffectsFlags(arg2);
        }
        if (arg1 is not null)
        {
            _ = call.Args.PushFront(NewCallArg.CreateForPrimitive(arg1));
            call.AddAllEffectsFlags(arg1);
        }

        GenTree result = call;
        if (morphArgs)
        {
            using var scope = new SharedTempsScope(this);
            result = fgMorphArgs(call);
        }

        return result;
    }

    private unsafe GenTree? fgMorphExpandCast(GenTreeCast tree)
    {
        var operand = tree.CastOp;
        var sourceType = operand.Type.ActualType;
        var destinationType = tree.CastType;
        if (varTypeIsFloating(sourceType) && varTypeIsIntegral(destinationType))
        {
            if (varTypeIsSmall(destinationType))
            {
                // Saturate before the truncating small-type cast. Checked casts
                // must instead retain the original value so overflow can throw.
                if (Configuration.FEATURE_HW_INTRINSICS || Configuration.TARGET_WASM)
                {
                    if (!tree.HasOverflowCheck)
                    {
                        (double Minimum, double Maximum) __jitExceptionResult161;
                        switch (destinationType)
                        {
                            case TYP_BYTE:
                                __jitExceptionResult161 = (sbyte.MinValue, sbyte.MaxValue);
                                break;
                            case TYP_UBYTE:
                                __jitExceptionResult161 = (0, byte.MaxValue);
                                break;
                            case TYP_SHORT:
                                __jitExceptionResult161 = (short.MinValue, short.MaxValue);
                                break;
                            case TYP_USHORT:
                                __jitExceptionResult161 = (0, ushort.MaxValue);
                                break;
                            default:
                                JitExceptionHelpers.ThrowUnreachableException();
                                __jitExceptionResult161 = default;
                                break;
                        }
                        var(minimum, maximum) = __jitExceptionResult161;

                        // Native min/max on these targets preserves NaN through
                        // the clamp; the subsequent floating-to-int cast yields zero.
                        if (Configuration.FEATURE_HW_INTRINSICS && !Configuration.TARGET_WASM)
                        {
                            operand = gtNewSimdMinMaxNativeNode(sourceType,
                                gtNewDconNode(sourceType, minimum), operand, sourceType, 0, isMax: true);
                            operand = gtNewSimdMinMaxNativeNode(sourceType,
                                gtNewDconNode(sourceType, maximum), operand, sourceType, 0, isMax: false);
                        }
                        else
                        {
                            operand = new GenTreeIntrinsic(sourceType,
                                gtNewDconNode(sourceType, minimum), operand, NI_System_Math_MaxNative, null);
                            operand = new GenTreeIntrinsic(sourceType,
                                gtNewDconNode(sourceType, maximum), operand, NI_System_Math_MinNative, null);
                        }
                    }
                }
                else if (Configuration.TARGET_ARM || Configuration.TARGET_RISCV64 ||
                         Configuration.TARGET_LOONGARCH64)
                {
                    // These targets clamp in the integer domain after NaN becomes zero.
                }
                else
                {
                    FatalJitException.ThrowFatalJitException(FatalJitErrorCode.NewTargetRequiresFloatingToSmallIntegerSaturationSupport);
                    return default!;
                }

                operand = gtNewCastNode(TYP_INT, operand, false, TYP_INT);
                operand.Flags |= tree.Flags & (GTF_OVERFLOW | GTF_EXCEPT);

                if ((Configuration.TARGET_ARM || Configuration.TARGET_RISCV64 ||
                     Configuration.TARGET_LOONGARCH64) &&
                    !tree.HasOverflowCheck)
                {
                    global::RyuJitSharp.NamedIntrinsic __jitExceptionResult162;
                    switch (destinationType)
                    {
                        case TYP_BYTE:
                            __jitExceptionResult162 = NI_PRIMITIVE_SaturateToInt8;
                            break;
                        case TYP_UBYTE:
                            __jitExceptionResult162 = NI_PRIMITIVE_SaturateToUInt8;
                            break;
                        case TYP_SHORT:
                            __jitExceptionResult162 = NI_PRIMITIVE_SaturateToInt16;
                            break;
                        case TYP_USHORT:
                            __jitExceptionResult162 = NI_PRIMITIVE_SaturateToUInt16;
                            break;
                        default:
                            JitExceptionHelpers.ThrowUnreachableException();
                            __jitExceptionResult162 = default;
                            break;
                    }
                    var intrinsic = __jitExceptionResult162;
                    operand = new GenTreeIntrinsic(TYP_INT, operand, intrinsic, null);
                }

                tree.Op1 = operand;
                // The new int operand is signed even when the target is unsigned.
                assert(!tree.IsUnsigned);
            }
            else if (fgCastRequiresHelper(sourceType, destinationType, tree.HasOverflowCheck))
            {
                if (sourceType is TYP_FLOAT)
                {
                    operand = gtNewCastNode(TYP_DOUBLE, operand, false, TYP_DOUBLE);
                }

                global::RyuJitSharp.CorInfoHelpFunc __jitExceptionResult163;
                bool __jitExceptionCondition164;
                __jitExceptionCondition164 = tree.HasOverflowCheck;
                if (__jitExceptionCondition164)
                {
                    switch (destinationType)
                    {
                        case TYP_INT:
                            __jitExceptionResult163 = CORINFO_HELP_DBL2INT_OVF;
                            break;
                        case TYP_UINT:
                            __jitExceptionResult163 = CORINFO_HELP_DBL2UINT_OVF;
                            break;
                        case TYP_LONG:
                            __jitExceptionResult163 = CORINFO_HELP_DBL2LNG_OVF;
                            break;
                        case TYP_ULONG:
                            __jitExceptionResult163 = CORINFO_HELP_DBL2ULNG_OVF;
                            break;
                        default:
                            JitExceptionHelpers.ThrowUnreachableException();
                            __jitExceptionResult163 = default;
                            break;
                    }
                }
                else
                {
                    switch (destinationType)
                    {
                        case TYP_LONG:
                            __jitExceptionResult163 = CORINFO_HELP_DBL2LNG;
                            break;
                        case TYP_ULONG:
                            __jitExceptionResult163 = CORINFO_HELP_DBL2ULNG;
                            break;
                        default:
                            JitExceptionHelpers.ThrowUnreachableException();
                            __jitExceptionResult163 = default;
                            break;
                    }
                }
                var helper = __jitExceptionResult163;
                return fgMorphCastIntoHelper(tree, helper, operand);
            }
        }
        else if ((sourceType is TYP_DOUBLE) && (destinationType is TYP_FLOAT) && (operand.Oper is GT_CAST)
            && !varTypeIsLong(operand.AsCast().CastOp.Type))
        {
            // The inner conversion to double was lossless.
            operand.Type = TYP_FLOAT;
            operand.AsCast().CastType = TYP_FLOAT;
            return fgMorphTree(operand);
        }
        else if (Configuration.TARGET_32BIT && varTypeIsLong(sourceType))
        {
            if (varTypeIsSmall(destinationType))
            {
                operand = gtNewCastNode(TYP_I_IMPL, operand, tree.IsUnsigned, TYP_I_IMPL);
                operand.Flags |= tree.Flags & (GTF_OVERFLOW | GTF_EXCEPT);
                tree.Flags &= ~GTF_UNSIGNED;
                tree.Op1 = operand;
            }
            else if (fgCastRequiresHelper(sourceType, destinationType))
            {
                CorInfoHelpFunc helper;
                if (destinationType is TYP_FLOAT)
                {
                    helper = tree.IsUnsigned ? CORINFO_HELP_ULNG2FLT : CORINFO_HELP_LNG2FLT;
                }
                else
                {
                    assert(destinationType is TYP_DOUBLE);
                    helper = tree.IsUnsigned ? CORINFO_HELP_ULNG2DBL : CORINFO_HELP_LNG2DBL;
                }
                return fgMorphCastIntoHelper(tree, helper, operand);
            }
        }
        else if (Configuration.TARGET_AMD64 && tree.IsUnsigned &&
                 varTypeIsInt(sourceType) && varTypeIsFloating(destinationType)
            && !compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            // Without EVEX unsigned conversion, widen uint to long first.
            operand = gtNewCastNode(TYP_LONG, operand, true, TYP_LONG);
            operand.Flags |= tree.Flags & (GTF_OVERFLOW | GTF_EXCEPT);
            tree.Flags &= ~GTF_UNSIGNED;
            tree.Op1 = operand;
        }
        else if (varTypeIsGC(sourceType) != varTypeIsGC(destinationType))
        {
            noway_assert(!varTypeIsGC(destinationType), "How can we have a cast to a GCRef here?");
            // Materialize a non-GC temporary: changing the original's type alone
            // would leave the emitter with inconsistent GC tracking.
            var localNumber = lvaGrabTemp(true, "Cast away GC");
            operand.Type = TYP_I_IMPL;
            var store = gtNewTempStore(localNumber, operand);
            operand.Type = sourceType;
            var cast = gtNewCastNode(tree.Type, gtNewLclvNode(TYP_I_IMPL, localNumber), false, destinationType);
            operand = gtNewBinaryNode(GT_COMMA, tree.Type, store, cast);
            return fgMorphTree(operand);
        }

        if ((sourceType is TYP_LONG) && (destinationType is TYP_INT or TYP_UINT))
        {
            if (tree.HasOverflowCheck && (operand.Oper is GT_AND))
            {
                var right = operand.AsOp().Op2;
                var maximumWidth = destinationType is TYP_UINT ? 32 : 31;
                if (right.Oper.IsCnsNativeLong && ((right.AsIntConCommon().LngValue >> maximumWidth) == 0))
                {
                    tree.Flags &= ~GTF_OVERFLOW;
                    tree.SetAllEffectsFlags(operand);
                }
            }

            if (fgGlobalMorph && !tree.HasOverflowCheck && !operand.HasOverflowCheckEx)
            {
                var canPushCast = operand.Oper is GT_ADD or GT_SUB or GT_MUL or GT_AND or GT_OR or GT_XOR or GT_NOT or GT_NEG;
                if (operand.Oper is GT_LSH)
                {
                    var shiftAmount = gtFoldExpr(operand.AsOp().Op2);
                    operand.AsOp().Op2 = shiftAmount;
                    if (shiftAmount.Oper.IsIntegralConst)
                    {
                        var value = shiftAmount.AsIntCon().IconValue;
                        if ((value >= 64) || (value < 0))
                        {
                            assert(!canPushCast);
                        }
                        else if (value >= 32)
                        {
                            // Masked long and int shifts disagree here. Only
                            // discard the subtree when none of it has effects.
                            if ((tree.Flags & GTF_ALL_EFFECT) == 0)
                            {
                                return fgMorphTree(gtNewZeroConNode(TYP_INT));
                            }
                            canPushCast = false;
                        }
                        else
                        {
                            canPushCast = true;
                        }
                    }
                    else
                    {
                        assert(!canPushCast);
                    }
                }

                if (canPushCast)
                {
                    var first = operand.AsUnOp().Op1;
                    var second = operand.Oper.IsBinary ? operand.AsOp().Op2 : null;
                    canPushCast = !varTypeIsGC(first.Type) && ((second is null) || !varTypeIsGC(second.Type));
                }
                if (canPushCast)
                {
                    operand.AsUnOp().Op1 = gtNewCastNode(TYP_INT, operand.AsUnOp().Op1, false, destinationType);
                    if (operand.Oper.IsBinary)
                    {
                        operand.AsOp().Op2 = gtNewCastNode(TYP_INT, operand.AsOp().Op2, false, destinationType);
                    }
                    if (operand.Oper is GT_MUL)
                    {
                        operand.Flags &= ~GTF_MUL_64RSLT;
                    }
                    operand.Type = TYP_INT;
                    return fgMorphTree(operand);
                }
            }
        }
        return null;
    }
}
