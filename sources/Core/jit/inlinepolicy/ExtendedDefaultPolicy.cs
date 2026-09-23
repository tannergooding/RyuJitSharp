// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.IO;

namespace RyuJitSharp;

public class ExtendedDefaultPolicy : DefaultPolicy
{
    protected double _profileFrequency;
    protected uint _binaryExprWithCns;
    protected uint _argCasted;
    protected uint _argIsStructByValue;
    protected uint _fldAccessOverArgStruct;
    protected uint _foldableBox;
    protected uint _intrinsic;
    protected uint _backwardJump;
    protected uint _throwBlock;
    protected uint _argIsExactCls;
    protected uint _argIsExactClsSigIsNot;
    protected uint _argIsConst;
    protected uint _argIsBoxedAtCallsite;
    protected uint _foldableIntrinsic;
    protected uint _foldableExpr;
    protected uint _foldableExprUn;
    protected uint _foldableBranch;
    protected uint _foldableSwitch;
    protected uint _unrollableMemop;
    protected uint _switch;
    protected uint _divByCns;
    protected uint _argUnbox;
    protected uint _argUnboxExact;
    protected bool _returnsStructByValue;
    protected bool _isFromValueClass;
    protected bool _nonGenericCallsGeneric;
    protected bool _isCallsiteInNoReturnRegion;
    protected bool _hasProfileWeights;
    protected bool _mayReturnSmallArray;

    public ExtendedDefaultPolicy(Compiler compiler, bool isPrejitRoot)
        : base(compiler, isPrejitRoot)
    {
    }

    public override bool RequiresPreciseScan => true;

    public override int EstimatedTotalILSize
    {
        get
        {
            long codeSize = unchecked((uint)_codeSize);

            // Each foldable branch removes an estimated 35 IL bytes. For example,
            // a typeof(TKey) == typeof(byte) branch can remove 52 bytes when false.
            codeSize -= (long)_foldableBranch * 35;

            // Switches usually eliminate more code; cap total folding at 70%.
            codeSize -= (long)_foldableSwitch * 70;
            codeSize = Math.Max(codeSize, (long)(unchecked((uint)_codeSize) * 0.3));

            return unchecked((int)codeSize);
        }
    }

    public override void NoteBool(InlineObservation observation, bool value)
    {
        unchecked
        {
            switch (observation)
            {
                case InlineObservation.CALLEE_RETURNS_STRUCT:
                {
                    _returnsStructByValue = value;
                    break;
                }

                case InlineObservation.CALLEE_CLASS_VALUETYPE:
                {
                    _isFromValueClass = value;
                    break;
                }

                case InlineObservation.CALLSITE_NONGENERIC_CALLS_GENERIC:
                {
                    _nonGenericCallsGeneric = value;
                    break;
                }

                case InlineObservation.CALLEE_BINARY_EXRP_WITH_CNS:
                {
                    _binaryExprWithCns++;
                    break;
                }

                case InlineObservation.CALLEE_ARG_STRUCT:
                {
                    _argIsStructByValue++;
                    break;
                }

                case InlineObservation.CALLEE_ARG_STRUCT_FIELD_ACCESS:
                {
                    _fldAccessOverArgStruct++;
                    break;
                }

                case InlineObservation.CALLEE_ARG_FEEDS_CAST:
                {
                    _argCasted++;
                    break;
                }

                case InlineObservation.CALLEE_FOLDABLE_BOX:
                {
                    _foldableBox++;
                    break;
                }

                case InlineObservation.CALLEE_INTRINSIC:
                {
                    _intrinsic++;
                    break;
                }

                case InlineObservation.CALLEE_BACKWARD_JUMP:
                {
                    _backwardJump++;
                    break;
                }

                case InlineObservation.CALLEE_THROW_BLOCK:
                {
                    _throwBlock++;
                    break;
                }

                case InlineObservation.CALLSITE_ARG_EXACT_CLS:
                {
                    _argIsExactCls++;
                    break;
                }

                case InlineObservation.CALLSITE_ARG_BOXED:
                {
                    _argIsBoxedAtCallsite++;
                    break;
                }

                case InlineObservation.CALLSITE_ARG_CONST:
                {
                    _argIsConst++;
                    break;
                }

                case InlineObservation.CALLSITE_ARG_EXACT_CLS_SIG_IS_NOT:
                {
                    _argIsExactClsSigIsNot++;
                    break;
                }

                case InlineObservation.CALLSITE_FOLDABLE_INTRINSIC:
                {
                    _foldableIntrinsic++;
                    break;
                }

                case InlineObservation.CALLSITE_FOLDABLE_EXPR:
                {
                    _foldableExpr++;
                    break;
                }

                case InlineObservation.CALLSITE_FOLDABLE_EXPR_UN:
                {
                    _foldableExprUn++;
                    break;
                }

                case InlineObservation.CALLSITE_FOLDABLE_BRANCH:
                {
                    _foldableBranch++;
                    break;
                }

                case InlineObservation.CALLSITE_FOLDABLE_SWITCH:
                {
                    _foldableSwitch++;
                    break;
                }

                case InlineObservation.CALLSITE_UNROLLABLE_MEMOP:
                {
                    _unrollableMemop++;
                    break;
                }

                case InlineObservation.CALLEE_HAS_SWITCH:
                {
                    _switch++;
                    break;
                }

                case InlineObservation.CALLSITE_DIV_BY_CNS:
                {
                    _divByCns++;
                    break;
                }

                case InlineObservation.CALLSITE_HAS_PROFILE_WEIGHTS:
                {
                    _hasProfileWeights = value;
                    break;
                }

                case InlineObservation.CALLSITE_IN_NORETURN_REGION:
                {
                    _isCallsiteInNoReturnRegion = value;
                    break;
                }

                case InlineObservation.CALLEE_UNBOX_ARG:
                {
                    _argUnbox++;
                    break;
                }

                case InlineObservation.CALLSITE_UNBOX_EXACT_ARG:
                {
                    _argUnboxExact++;
                    break;
                }

                case InlineObservation.CALLEE_MAY_RETURN_SMALL_ARRAY:
                {
                    _mayReturnSmallArray = true;
                    break;
                }

                default:
                {
                    base.NoteBool(observation, value);
                    break;
                }
            }
        }
    }

    public override void NoteInt(InlineObservation observation, int value)
    {
        unchecked
        {
            switch (observation)
            {
                case InlineObservation.CALLEE_IL_CODE_SIZE:
                {
                    assert(IsForceInlineKnown);
                    assert(value != 0);
                    _codeSize = value;
                    var maxCodeSize = (uint)JitConfig.JitExtDefaultPolicyMaxIL;

                    // Static profiles may be the generic profile bundled with the runtime.
                    if (_hasProfileWeights && _rootCompiler.fgHaveTrustedProfileWeights)
                    {
                        JITDUMP("Callee and root has trusted profile\n");
                        maxCodeSize = (uint)JitConfig.JitExtDefaultPolicyMaxILProf;
                    }
                    else if (_rootCompiler.fgHaveSufficientProfileWeights)
                    {
                        // Without inlinee instrumentation, aggressive inlining can lose
                        // useful profile data in Tier1+Instr and OSR.
                        var isTier1Instr = _rootCompiler.opts.IsInstrumentedAndOptimized;
                        var isOSR = _rootCompiler.opts.IsOSR;

                        if (isTier1Instr || isOSR)
                        {
                            JITDUMP($"Root has sufficient profile. Leaving max IL size at {maxCodeSize} for Tier1+Instr or OSR\n");
                        }
                        else
                        {
                            maxCodeSize = (uint)JitConfig.JitExtDefaultPolicyMaxILRoot;
                            JITDUMP($"Root has sufficient profile. Boosting max IL size to {maxCodeSize}\n");
                        }
                    }
                    else
                    {
                        JITDUMP($"Callee has {(_hasProfileWeights ? "untrusted" : "no")} profile\n");
                    }

                    uint alwaysInlineSize = InlineStrategy.ALWAYS_INLINE_SIZE;

                    if (InsideThrowBlock)
                    {
                        // Only small callees (normally at most 8 IL bytes) in throw blocks.
                        JITDUMP("Call site in throw block\n");
                        alwaysInlineSize /= 2;
                        maxCodeSize = Math.Min(alwaysInlineSize + 1, maxCodeSize);
                    }

                    if (IsForceInline)
                    {
                        SetCandidate(InlineObservation.CALLEE_IS_FORCE_INLINE);
                    }
                    else if ((uint)_codeSize <= alwaysInlineSize)
                    {
                        SetCandidate(InlineObservation.CALLEE_BELOW_ALWAYS_INLINE_SIZE);
                    }
                    else if ((uint)_codeSize <= maxCodeSize)
                    {
                        SetCandidate(InlineObservation.CALLEE_IS_DISCRETIONARY_INLINE);
                    }
                    else
                    {
                        JITDUMP($"Callee IL size {(uint)_codeSize} exceeds maxCodeSize {maxCodeSize}\n");
                        SetNever(InlineObservation.CALLEE_TOO_MUCH_IL);
                    }

                    break;
                }

                case InlineObservation.CALLEE_NUMBER_OF_BASIC_BLOCKS:
                {
                    if (!IsForceInline && IsNoReturn && (value == 1))
                    {
                        SetNever(InlineObservation.CALLEE_DOES_NOT_RETURN);
                    }
                    else if (!IsForceInline && !_hasProfileWeights && !ConstArgFeedsIsKnownConst && !ArgFeedsIsKnownConst)
                    {
                        var bbLimit = (uint)JitConfig.JitExtDefaultPolicyMaxBB;

                        if (_isPrejitRoot)
                        {
                            // Prejit roots cannot recognize argument-specific foldable branches.
                            bbLimit += 5 + (_switch * 10);
                        }

                        bbLimit += _foldableBranch + (_foldableSwitch * 10) + (_unrollableMemop * 2);

                        if ((uint)value > bbLimit)
                        {
                            JITDUMP($"Callee BB count {(uint)value} exceeds bbLimit {bbLimit}\n");
                            SetNever(InlineObservation.CALLEE_TOO_MANY_BASIC_BLOCKS);
                        }
                    }

                    break;
                }

                default:
                {
                    base.NoteInt(observation, value);
                    break;
                }
            }
        }
    }

    public override void NoteDouble(InlineObservation observation, double value)
    {
        assert(observation == InlineObservation.CALLSITE_PROFILE_FREQUENCY);
        _profileFrequency = value;
    }

    protected override double DetermineMultiplier()
    {
        unchecked
        {
            var multiplier = 0.0;

            if (IsInstanceCtor)
            {
                multiplier += 1.5;
                JITDUMP($"\nmultiplier in instance constructors increased to {FormatGeneral(multiplier)}.");
            }

            if (_isFromValueClass)
            {
                multiplier += 3.0;
                JITDUMP($"\nmultiplier in methods of struct increased to {FormatGeneral(multiplier)}.");
            }

            if (_returnsStructByValue)
            {
                // Inlining can eliminate expensive by-value struct copies.
                multiplier += 2.0;
                JITDUMP($"\nInline candidate returns a struct by value.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }
            else if (_argIsStructByValue > 0)
            {
                multiplier += 2.0;
                JITDUMP($"\n{(int)_argIsStructByValue} arguments are structs passed by value.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }
            else if (_fldAccessOverArgStruct > 0)
            {
                // ldfld/stfld are cheap for promotable structs.
                multiplier += 1.0;
                JITDUMP($"\n{(int)_fldAccessOverArgStruct} ldfld or stfld over arguments which are structs.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (LooksLikeWrapperMethod)
            {
                multiplier += 1.0;
                JITDUMP($"\nInline candidate looks like a wrapper method.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (MethodIsMostlyLoadStore)
            {
                multiplier += 3.0;
                JITDUMP($"\nInline candidate is mostly loads and stores.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_argFeedsRangeCheck > 0)
            {
                multiplier += 1.0;
                JITDUMP($"\nInline candidate has arg that feeds range check.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_nonGenericCallsGeneric)
            {
                multiplier += 2.0;
                JITDUMP($"\nInline candidate is generic and caller is not.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_foldableBranch > 0)
            {
                // Includes typeof(T), ISA support queries, and tests of constant arguments.
                multiplier += 3.0 + _foldableBranch;
                JITDUMP($"\nInline candidate has {(int)_foldableBranch} foldable branches.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }
            else if (_constantArgFeedsConstantTest > 0)
            {
                multiplier += 3.0;
                JITDUMP($"\nInline candidate has const arg that feeds a conditional.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }
            else if ((_argIsConst > 0) && (_foldableExpr < 1))
            {
                // TODO: Recognize if (SomeMethod(constArg)) in fgFindJumpTargets.
                multiplier += 3.0;
                JITDUMP($"\nCallsite passes a constant.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if ((_foldableBox > 0) && _nonGenericCallsGeneric)
            {
                // BOX+ISINST+BR or BOX+UNBOX patterns recognized by impBoxPatternMatch.
                multiplier += 3.0;
                JITDUMP($"\nInline has {(int)_foldableBox} foldable BOX ops.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

#if FEATURE_SIMD
            if (HasSimd)
            {
                multiplier += JitConfig.JitInlineSIMDMultiplier;
                JITDUMP($"\nInline candidate has SIMD type args, locals or return value.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }
#endif

            if (_intrinsic > 0)
            {
                // Most such intrinsics lower to a single CPU instruction.
                multiplier += 1.0 + (_intrinsic * 0.3);
                JITDUMP($"\nInline has {(int)_intrinsic} intrinsics.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_argIsBoxedAtCallsite > 0)
            {
                // Inlining may eliminate boxing, for example when the callee ignores its argument.
                multiplier += 0.5 * _argIsBoxedAtCallsite;
                JITDUMP($"\nCallsite is going to box {(int)_argIsBoxedAtCallsite} arguments.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_argIsExactClsSigIsNot > 0)
            {
                // An exact callsite type can enable devirtualization inside the callee.
                multiplier += 2.5;
                JITDUMP($"\nCallsite passes {(int)_argIsExactClsSigIsNot} arguments of exact classes while callee accepts non-exact ones.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_foldableIntrinsic > 0)
            {
                // For example typeof(T1) == typeof(T2), Math.Abs(constArg), or PopCount(10).
                multiplier += 1.0 + _foldableIntrinsic;
                JITDUMP($"\nInline has {(int)_foldableIntrinsic} foldable intrinsics.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_foldableExpr > 0)
            {
                multiplier += 1.0 + _foldableExpr;
                JITDUMP($"\nInline has {(int)_foldableExpr} foldable binary expressions.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_foldableExprUn > 0)
            {
                multiplier += _foldableExprUn;
                JITDUMP($"\nInline has {(int)_foldableExprUn} foldable unary expressions.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_divByCns > 0)
            {
                // A constant divisor at the callsite avoids an expensive DIV instruction.
                multiplier += 3.0;
                JITDUMP($"\nInline has {(int)_divByCns} Div-by-constArg expressions.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_binaryExprWithCns > 0)
            {
                // Potential constant tests may include calls not recognized as foldable.
                multiplier += _binaryExprWithCns * 0.5;
                JITDUMP($"\nInline candidate has {(int)_binaryExprWithCns} binary expressions with constants.  Multiplier increased to {FormatGeneral(multiplier)}.");

                // Prejit roots cannot see callsites; optimistically assume constant arguments.
                if (_isPrejitRoot)
                {
                    multiplier += _binaryExprWithCns;
                }
            }

            if (_argFeedsConstantTest > 0)
            {
                multiplier += _isPrejitRoot ? 3.0 : 1.0;
                JITDUMP($"\nInline candidate has an arg that feeds a constant test.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }
            else if (_isPrejitRoot && (_argFeedsTest > 0))
            {
                multiplier += 3.0;
                JITDUMP($"\nPrejit root candidate has arg that feeds a conditional.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_argUnboxExact > 0)
            {
                multiplier += 4.0;
                JITDUMP($"\nInline candidate has {(int)_argUnboxExact} exact arg unboxes.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_argUnbox > 0)
            {
                // Assume prejit-root callers might supply an exact boxed type.
                multiplier += _isPrejitRoot ? 4.0 : 1.0;
                JITDUMP($"\nInline candidate has {(int)_argUnboxExact} arg unboxes.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            switch (_callsiteFrequency)
            {
                case InlineCallsiteFrequency.RARE:
                {
                    // Rarity replaces the multiplier rather than adding to it.
                    multiplier = 1.3;
                    JITDUMP($"\nInline candidate callsite is rare.  Multiplier limited to {FormatGeneral(multiplier)}.");
                    break;
                }

                case InlineCallsiteFrequency.BORING:
                {
                    multiplier += 1.3;
                    JITDUMP($"\nInline candidate callsite is boring.  Multiplier increased to {FormatGeneral(multiplier)}.");
                    break;
                }

                case InlineCallsiteFrequency.WARM:
                {
                    multiplier += 2.0;
                    JITDUMP($"\nInline candidate callsite is warm.  Multiplier increased to {FormatGeneral(multiplier)}.");
                    break;
                }

                case InlineCallsiteFrequency.LOOP:
                {
                    multiplier += 3.0;
                    JITDUMP($"\nInline candidate callsite is in a loop.  Multiplier increased to {FormatGeneral(multiplier)}.");
                    break;
                }

                case InlineCallsiteFrequency.HOT:
                {
                    multiplier += 3.0;
                    JITDUMP($"\nInline candidate callsite is hot.  Multiplier increased to {FormatGeneral(multiplier)}.");
                    break;
                }

                default:
                {
                    assert(false, "Unexpected callsite frequency");
                    break;
                }
            }

            if (_unrollableMemop > 0)
            {
                multiplier += _unrollableMemop;
                JITDUMP($"\nInline candidate has {(int)_unrollableMemop} unrollable memory operations.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_foldableSwitch > 0)
            {
                multiplier += 6.0;
                JITDUMP($"\nInline candidate has {(int)_foldableSwitch} foldable switches.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }
            else if (_switch > 0)
            {
                if (_isPrejitRoot)
                {
                    // Optimistically assume switches can fold in prejit-root mode.
                    multiplier += 6.0;
                    JITDUMP($"\nPrejit root candidate has {(int)_switch} switches.  Multiplier increased to {FormatGeneral(multiplier)}.");
                }
                else
                {
                    multiplier = 0.0;
                    JITDUMP($"\nInline candidate has {(int)_switch} switches.  Multiplier limited to {FormatGeneral(multiplier)}.");
                }
            }

            if (_mayReturnSmallArray)
            {
                multiplier += 4.0;
                JITDUMP($"\nInline candidate may return small known-size array.  Multiplier increased to {FormatGeneral(multiplier)}.");
            }

            if (_hasProfileWeights)
            {
                // Profiles lack calling-context sensitivity and may be stale; cold inlines
                // can still benefit caller-wide type and escape analysis.
                var profileTrustCoef = JitConfig.JitExtDefaultPolicyProfTrust / 10.0;
                var profileScale = JitConfig.JitExtDefaultPolicyProfScale / 10.0;
                double profileBoost;

                if (_rootCompiler.fgHaveTrustedProfileWeights)
                {
                    profileBoost = (1.0 - profileTrustCoef) + ((1.0 < _profileFrequency ? 1.0 : _profileFrequency) * profileScale);
                }
                else
                {
                    profileBoost = (1.0 < _profileFrequency ? 1.0 : _profileFrequency) * profileScale;
                }

                if ((profileBoost < 1.0) && IsIntrinsicType)
                {
                    // Intrinsic types such as Span<T> and Vector<T> rely on inlining for code quality.
                    profileBoost = 1.0;
                }

                multiplier *= profileBoost;
                JITDUMP($"\nCallsite has profile data: {FormatGeneral(_profileFrequency)}.  Multiplier limited to {FormatGeneral(multiplier)}.");
            }

            if (_rootCompiler.lvaTable.Length > 64)
            {
                // For example, 512 locals with a tracking limit of 1024 halves the benefit.
                var ratio = (double)_rootCompiler.lvaTable.Length / JitConfig.JitMaxLocalsToTrack;
                var lclFullness = ratio < 1.0 ? ratio : 1.0;
                multiplier *= 1.0 - lclFullness;
                JITDUMP($"\nCaller has {_rootCompiler.lvaTable.Length} locals.  Multiplier decreased to {FormatGeneral(multiplier)}.");
            }

            if (_backwardJump != 0)
            {
                multiplier *= 0.7;
                JITDUMP($"\nInline has {(int)_backwardJump} backward jumps (loops?).  Multiplier decreased to {FormatGeneral(multiplier)}.");
            }

            if (_isCallsiteInNoReturnRegion)
            {
                // Avoid expanding calls used only on throw paths, e.g. exception-message construction.
                multiplier = 1.0;
                JITDUMP($"\nCallsite is in a no-return region.  Multiplier limited to {FormatGeneral(multiplier)}.");
            }

#if DEBUG
            var additionalMultiplier = JitConfig.JitInlineAdditionalMultiplier;

            if (additionalMultiplier != 0)
            {
                multiplier += additionalMultiplier;
                JITDUMP($"\nmultiplier increased via JitInlineAdditionalMultiplier={additionalMultiplier} to {FormatGeneral(multiplier)}.");
            }

            if (_rootCompiler.compInlineStress())
            {
                multiplier += 10;
                JITDUMP($"\nmultiplier increased via inline stress to {FormatGeneral(multiplier)}.");
            }
#endif

            return multiplier;
        }
    }

#if DEBUG
    public override string Name => nameof(ExtendedDefaultPolicy);

    public override void OnDumpXml(StreamWriter stream, int indent = 0)
    {
        unchecked
        {
            base.OnDumpXml(stream, indent);
            XATTR_R8(stream, _profileFrequency, "m_ProfileFrequency");
            XATTR_I4(stream, (int)_binaryExprWithCns, "m_BinaryExprWithCns");
            XATTR_I4(stream, (int)_argCasted, "m_ArgCasted");
            XATTR_I4(stream, (int)_argIsStructByValue, "m_ArgIsStructByValue");
            XATTR_I4(stream, (int)_fldAccessOverArgStruct, "m_FldAccessOverArgStruct");
            XATTR_I4(stream, (int)_foldableBox, "m_FoldableBox");
            XATTR_I4(stream, (int)_intrinsic, "m_Intrinsic");
            XATTR_I4(stream, (int)_backwardJump, "m_BackwardJump");
            XATTR_I4(stream, (int)_throwBlock, "m_ThrowBlock");
            XATTR_I4(stream, (int)_argIsExactCls, "m_ArgIsExactCls");
            XATTR_I4(stream, (int)_argIsExactClsSigIsNot, "m_ArgIsExactClsSigIsNot");
            XATTR_I4(stream, (int)_argIsConst, "m_ArgIsConst");
            XATTR_I4(stream, (int)_argIsBoxedAtCallsite, "m_ArgIsBoxedAtCallsite");
            XATTR_I4(stream, (int)_foldableIntrinsic, "m_FoldableIntrinsic");
            XATTR_I4(stream, (int)_foldableExpr, "m_FoldableExpr");
            XATTR_I4(stream, (int)_foldableExprUn, "m_FoldableExprUn");
            XATTR_I4(stream, (int)_foldableBranch, "m_FoldableBranch");
            XATTR_I4(stream, (int)_foldableSwitch, "m_FoldableSwitch");
            XATTR_I4(stream, (int)_unrollableMemop, "m_UnrollableMemop");
            XATTR_I4(stream, (int)_switch, "m_Switch");
            XATTR_I4(stream, (int)_divByCns, "m_DivByCns");
            XATTR_B(stream, _returnsStructByValue, "m_ReturnsStructByValue");
            XATTR_B(stream, _isFromValueClass, "m_IsFromValueClass");
            XATTR_B(stream, _nonGenericCallsGeneric, "m_NonGenericCallsGeneric");
            XATTR_B(stream, _isCallsiteInNoReturnRegion, "m_IsCallsiteInNoReturnRegion");
            XATTR_B(stream, _hasProfileWeights, "m_HasProfileWeights");
            XATTR_B(stream, InsideThrowBlock, "m_InsideThrowBlock");
            XATTR_B(stream, _mayReturnSmallArray, "m_MayReturnSmallArray");
        }
    }
#endif

    private static string FormatGeneral(double value) => formatFloat(value, "g6");
}
