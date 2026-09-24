// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public partial class Compiler
{
#if TARGET_X86
    private bool fgCastRequiresHelper(var_types fromType, var_types toType, bool overflow = false)
#else
    private static bool fgCastRequiresHelper(var_types fromType, var_types toType, bool overflow = false)
#endif
    {
        if (overflow && varTypeIsFloating(fromType))
        {
            assert(varTypeIsIntegral(toType));
            return true;
        }

#if TARGET_X86 || TARGET_ARM
        if ((varTypeIsLong(fromType) && varTypeIsFloating(toType))
            || (varTypeIsFloating(fromType) && varTypeIsLong(toType)))
        {
#if TARGET_X86
            return !compOpportunisticallyDependsOn(InstructionSet_AVX512);
#else
            return true;
#endif
        }
#endif
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
#if DEBUG
            _callDebugFlags = GTF_CALL_MD_EMPTY,
            _inlineObservation = InlineObservation.CALLSITE_IS_CALL_TO_HELPER,
            _callSig = default,
#endif
        };
        call.ClearInlineInfo();
#if UNIX_X86_ABI
        call.Flags |= GTF_CALL_POP_ARGS;
#endif
#if FEATURE_READYTORUN
        call._entryPoint = new CORINFO_CONST_LOOKUP { accessType = IAT_VALUE };
#endif
#if FEATURE_MULTIREG_RET
        call._returnTypeDesc.Reset();
        call.ClearOtherRegs();
#if !TARGET_64BIT
        if (varTypeIsLong(tree.Type))
        {
            call._returnTypeDesc.InitializeLongReturnType();
        }
#endif
#endif
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
            call.Args.PushFront(NewCallArg.CreateForPrimitive(arg2));
            call.AddAllEffectsFlags(arg2);
        }
        if (arg1 is not null)
        {
            call.Args.PushFront(NewCallArg.CreateForPrimitive(arg1));
            call.AddAllEffectsFlags(arg1);
        }

        GenTree result = call;
        if (morphArgs)
        {
            using var scope = new SharedTempsScope(this);
            result = fgMorphArgs(call);
        }
        result.SetMorphed(this);
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
#if FEATURE_HW_INTRINSICS || TARGET_WASM
                if (!tree.HasOverflowCheck)
                {
                    (double Minimum, double Maximum) limits = destinationType switch {
                        TYP_BYTE => (sbyte.MinValue, sbyte.MaxValue),
                        TYP_UBYTE => (0, byte.MaxValue),
                        TYP_SHORT => (short.MinValue, short.MaxValue),
                        TYP_USHORT => (0, ushort.MaxValue),
                        _ => throw new System.Diagnostics.UnreachableException(),
                    };

                    // Native min/max on these targets preserves NaN through
                    // the clamp; the subsequent floating-to-int cast yields zero.
#if FEATURE_HW_INTRINSICS && !TARGET_WASM
                    operand = gtNewSimdMinMaxNativeNode(sourceType, gtNewDconNode(sourceType, limits.Minimum),
                        operand, sourceType, 0, isMax: true);
                    operand = gtNewSimdMinMaxNativeNode(sourceType, gtNewDconNode(sourceType, limits.Maximum),
                        operand, sourceType, 0, isMax: false);
#else
                    operand = new GenTreeIntrinsic(sourceType, gtNewDconNode(sourceType, limits.Minimum),
                        operand, NI_System_Math_MaxNative, null);
                    operand = new GenTreeIntrinsic(sourceType, gtNewDconNode(sourceType, limits.Maximum),
                        operand, NI_System_Math_MinNative, null);
#endif
                }
#elif TARGET_ARM || TARGET_RISCV64 || TARGET_LOONGARCH64
                // These targets clamp in the integer domain after NaN becomes zero.
#else
#error New target requires floating-to-small-integer saturation support.
#endif
                operand = gtNewCastNode(TYP_INT, operand, false, TYP_INT);
                operand.Flags |= tree.Flags & (GTF_OVERFLOW | GTF_EXCEPT);

#if TARGET_ARM || TARGET_RISCV64 || TARGET_LOONGARCH64
                if (!tree.HasOverflowCheck)
                {
                    var intrinsic = destinationType switch {
                        TYP_BYTE => NI_PRIMITIVE_SaturateToInt8,
                        TYP_UBYTE => NI_PRIMITIVE_SaturateToUInt8,
                        TYP_SHORT => NI_PRIMITIVE_SaturateToInt16,
                        TYP_USHORT => NI_PRIMITIVE_SaturateToUInt16,
                        _ => throw new System.Diagnostics.UnreachableException(),
                    };
                    operand = new GenTreeIntrinsic(TYP_INT, operand, intrinsic, null);
                }
#endif
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

                var helper = tree.HasOverflowCheck
                    ? destinationType switch {
                        TYP_INT => CORINFO_HELP_DBL2INT_OVF,
                        TYP_UINT => CORINFO_HELP_DBL2UINT_OVF,
                        TYP_LONG => CORINFO_HELP_DBL2LNG_OVF,
                        TYP_ULONG => CORINFO_HELP_DBL2ULNG_OVF,
                        _ => throw new System.Diagnostics.UnreachableException(),
                    }
                    : destinationType switch {
                        TYP_LONG => CORINFO_HELP_DBL2LNG,
                        TYP_ULONG => CORINFO_HELP_DBL2ULNG,
                        _ => throw new System.Diagnostics.UnreachableException(),
                    };
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
#if !TARGET_64BIT
        else if (varTypeIsLong(sourceType))
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
#endif
#if TARGET_AMD64
        else if (tree.IsUnsigned && varTypeIsInt(sourceType) && varTypeIsFloating(destinationType)
            && !compOpportunisticallyDependsOn(InstructionSet_AVX512))
        {
            // Without EVEX unsigned conversion, widen uint to long first.
            operand = gtNewCastNode(TYP_LONG, operand, true, TYP_LONG);
            operand.Flags |= tree.Flags & (GTF_OVERFLOW | GTF_EXCEPT);
            tree.Flags &= ~GTF_UNSIGNED;
            tree.Op1 = operand;
        }
#endif
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
                if ((right.Oper is GT_CNS_NATIVELONG) && ((right.AsIntConCommon().LngValue >> maximumWidth) == 0))
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
