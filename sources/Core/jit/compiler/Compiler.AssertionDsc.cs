// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics;

namespace RyuJitSharp;

public partial class Compiler
{
    public enum optAssertionKind : byte
    {
        OAK_EQUAL,
        OAK_NOT_EQUAL,
        OAK_LT,
        OAK_LT_UN,
        OAK_LE,
        OAK_LE_UN,
        OAK_GT,
        OAK_GT_UN,
        OAK_GE,
        OAK_GE_UN,
        OAK_SUBRANGE,
    }

    public enum optOp1Kind : byte
    {
        O1K_LCLVAR,
        O1K_VN,
        O1K_EXACT_TYPE,
        O1K_SUBTYPE,
    }

    public enum optOp2Kind : byte
    {
        O2K_LCLVAR_COPY,
        O2K_CONST_INT,
        O2K_CONST_DOUBLE,
        O2K_VN_ADD_CNS,
        O2K_ZEROOBJ,
        O2K_SUBRANGE,
        O2K_CONST_VEC,
    }

    public sealed class AssertionDsc
    {
        public readonly struct AssertionDscOp1
        {
#if DEBUG
            private readonly Compiler _compiler;
#endif
            private readonly int _value;

            internal AssertionDscOp1(Compiler compiler, optOp1Kind kind, int value)
            {
#if DEBUG
                _compiler = compiler;
#endif
                Kind = kind;
                _value = value;
            }

            public optOp1Kind Kind { get; }

            public bool KindIs(optOp1Kind kind) => Kind == kind;

            public bool KindIs(optOp1Kind first, optOp1Kind second) => KindIs(first) || KindIs(second);

            public bool KindIs(optOp1Kind first, optOp1Kind second, optOp1Kind third)
                => KindIs(first, second) || KindIs(third);

            public ValueNum VN
            {
                get
                {
#if DEBUG
                    assert(!_compiler.optLocalAssertionProp);
#endif
                    assert(KindIs(optOp1Kind.O1K_VN, optOp1Kind.O1K_EXACT_TYPE, optOp1Kind.O1K_SUBTYPE));
                    assert(_value != ValueNumStore.NoVN);
                    return _value;
                }
            }

            public int LclNum
            {
                get
                {
#if DEBUG
                    assert(_compiler.optLocalAssertionProp);
#endif
                    assert(_value != BAD_VAR_NUM);
                    assert(KindIs(optOp1Kind.O1K_LCLVAR));
                    return _value;
                }
            }
        }

        public struct AssertionDscOp2
        {
#if DEBUG
            private readonly Compiler _compiler;
#endif
            private readonly ValueNum _vn;
            private readonly nint _iconVal;
            private readonly double _dconVal;
            private readonly int _lclNum;
            private readonly IntegralRange _range;
            private readonly byte[]? _simdValue;
            private readonly bool _isVNNeverNegative;
            private ushort _encodedIconFlags;
            private FieldSeq? _fieldSeq;

            internal AssertionDscOp2(Compiler compiler, optOp2Kind kind, ValueNum vn,
                nint iconVal = 0, double dconVal = 0, int lclNum = 0, IntegralRange range = default,
                byte[]? simdValue = null, bool isVNNeverNegative = false,
                GenTreeFlags iconFlags = GTF_EMPTY, FieldSeq? fieldSeq = null)
            {
#if DEBUG
                _compiler = compiler;
#endif
                Kind = kind;
                _vn = vn;
                _iconVal = iconVal;
                _dconVal = dconVal;
                _lclNum = lclNum;
                _range = range;
                _simdValue = simdValue;
                _isVNNeverNegative = isVNNeverNegative;
                _encodedIconFlags = 0;
                _fieldSeq = null;

                if (kind is optOp2Kind.O2K_CONST_INT)
                {
                    SetIconFlag(iconFlags, fieldSeq);
                }
                else
                {
                    assert(iconFlags == GTF_EMPTY);
                    assert(fieldSeq is null);
                }
            }

            public readonly optOp2Kind Kind { get; }

            public readonly bool KindIs(optOp2Kind kind) => Kind == kind;

            public readonly bool KindIs(optOp2Kind first, optOp2Kind second) => KindIs(first) || KindIs(second);

            public readonly int LclNum
            {
                get
                {
#if DEBUG
                    assert(_compiler.optLocalAssertionProp);
#endif
                    assert(KindIs(optOp2Kind.O2K_LCLVAR_COPY));
                    return _lclNum;
                }
            }

            public readonly double DoubleConstant
            {
                get
                {
                    assert(KindIs(optOp2Kind.O2K_CONST_DOUBLE));
                    return _dconVal;
                }
            }

            public readonly ReadOnlySpan<byte> SimdConstant
            {
                get
                {
                    assert(KindIs(optOp2Kind.O2K_CONST_VEC));
                    assert(_simdValue is not null);
                    return _simdValue;
                }
            }

            public readonly int SimdSize => SimdConstant.Length;

            public readonly nint IntConstant
            {
                get
                {
                    assert(KindIs(optOp2Kind.O2K_CONST_INT));
                    return _iconVal;
                }
            }

            public readonly IntegralRange Range
            {
                get
                {
#if DEBUG
                    assert(_compiler.optLocalAssertionProp);
#endif
                    assert(KindIs(optOp2Kind.O2K_SUBRANGE));
                    return _range;
                }
            }

            // Local assertions retain this VN only as a nullness hint, not as a VN identity.
            public readonly bool IsNullConstant => KindIs(optOp2Kind.O2K_CONST_INT) && (_vn == ValueNumStore.VNForNull());

            public readonly ValueNum VN
            {
                get
                {
#if DEBUG
                    assert(!_compiler.optLocalAssertionProp);
#endif
                    assert(IsConstant || KindIs(optOp2Kind.O2K_VN_ADD_CNS));
                    assert(_vn != ValueNumStore.NoVN);
                    return _vn;
                }
            }

            public readonly int Cns
            {
                get
                {
#if DEBUG
                    assert(!_compiler.optLocalAssertionProp);
#endif
                    assert(KindIs(optOp2Kind.O2K_VN_ADD_CNS));
                    assert(FitsInI32(_iconVal));
                    return (int)_iconVal;
                }
            }

            public readonly bool IsVNNeverNegative
            {
                get
                {
#if DEBUG
                    assert(!_compiler.optLocalAssertionProp);
#endif
                    assert(KindIs(optOp2Kind.O2K_VN_ADD_CNS));
                    return _isVNNeverNegative;
                }
            }

            public readonly bool HasIconFlag
            {
                get
                {
                    assert(KindIs(optOp2Kind.O2K_CONST_INT));
                    assert(_encodedIconFlags <= 0xFF);
                    return _encodedIconFlags != 0;
                }
            }

            public readonly GenTreeFlags IconFlag
            {
                get
                {
                    assert(KindIs(optOp2Kind.O2K_CONST_INT));
                    return unchecked((GenTreeFlags)((uint)_encodedIconFlags << 24));
                }
            }

            public void SetIconFlag(GenTreeFlags flags, FieldSeq? fieldSeq = null)
            {
                assert(KindIs(optOp2Kind.O2K_CONST_INT));
                assert((flags & ~GTF_ICON_HDL_MASK) == 0);
                _encodedIconFlags = (ushort)(unchecked((uint)flags) >> 24);
                _fieldSeq = fieldSeq;
            }

            public readonly FieldSeq? IconFieldSeq
            {
                get
                {
                    assert(KindIs(optOp2Kind.O2K_CONST_INT));
                    return _fieldSeq;
                }
            }

            public readonly bool IsConstant => Kind is optOp2Kind.O2K_CONST_INT or optOp2Kind.O2K_CONST_DOUBLE
                or optOp2Kind.O2K_ZEROOBJ or optOp2Kind.O2K_CONST_VEC;
        }

        private readonly AssertionDscOp1 _op1;
        private readonly AssertionDscOp2 _op2;

        private AssertionDsc(optAssertionKind kind, AssertionDscOp1 op1, AssertionDscOp2 op2)
        {
            Kind = kind;
            _op1 = op1;
            _op2 = op2;
        }

        public optAssertionKind Kind { get; }

        public ref readonly AssertionDscOp1 Op1 => ref _op1;

        public ref readonly AssertionDscOp2 Op2 => ref _op2;

        public bool KindIs(optAssertionKind kind) => Kind == kind;

        public bool KindIs(optAssertionKind first, optAssertionKind second) => KindIs(first) || KindIs(second);

        public bool IsCopyAssertion => KindIs(optAssertionKind.OAK_EQUAL) && Op1.KindIs(optOp1Kind.O1K_LCLVAR)
            && Op2.KindIs(optOp2Kind.O2K_LCLVAR_COPY);

        public bool IsConstantInt32Assertion => CanPropEqualOrNotEqual && Op2.KindIs(optOp2Kind.O2K_CONST_INT)
            && Op1.KindIs(optOp1Kind.O1K_LCLVAR, optOp1Kind.O1K_VN) && FitsInI32(Op2.IntConstant);

        public bool CanPropLclVar => KindIs(optAssertionKind.OAK_EQUAL) && Op1.KindIs(optOp1Kind.O1K_LCLVAR, optOp1Kind.O1K_VN);

        public bool CanPropEqualOrNotEqual => KindIs(optAssertionKind.OAK_EQUAL, optAssertionKind.OAK_NOT_EQUAL);

        public bool CanPropNonNull => KindIs(optAssertionKind.OAK_NOT_EQUAL)
            && Op1.KindIs(optOp1Kind.O1K_LCLVAR, optOp1Kind.O1K_VN) && Op2.IsNullConstant;

        public bool CanPropSubRange
        {
            get
            {
                if (KindIs(optAssertionKind.OAK_SUBRANGE))
                {
                    assert(Op1.KindIs(optOp1Kind.O1K_LCLVAR));
                    return true;
                }

                return false;
            }
        }

        public bool IsBoundsCheckNoThrow => Op1.KindIs(optOp1Kind.O1K_VN) && KindIs(optAssertionKind.OAK_LT_UN)
            && Op2.KindIs(optOp2Kind.O2K_VN_ADD_CNS) && (Op2.Cns == 0) && Op2.IsVNNeverNegative;

        public static optAssertionKind FromVNFunc(VNFunc func) => func switch {
            VNF_EQ => optAssertionKind.OAK_EQUAL,
            VNF_NE => optAssertionKind.OAK_NOT_EQUAL,
            VNF_LT => optAssertionKind.OAK_LT,
            VNF_LE => optAssertionKind.OAK_LE,
            VNF_GT => optAssertionKind.OAK_GT,
            VNF_GE => optAssertionKind.OAK_GE,
            VNF_LT_UN => optAssertionKind.OAK_LT_UN,
            VNF_LE_UN => optAssertionKind.OAK_LE_UN,
            VNF_GT_UN => optAssertionKind.OAK_GT_UN,
            VNF_GE_UN => optAssertionKind.OAK_GE_UN,
            _ => throw new UnreachableException(),
        };

        public static genTreeOps ToCompareOper(optAssertionKind kind, out bool isUnsigned)
        {
            isUnsigned = kind is optAssertionKind.OAK_LT_UN or optAssertionKind.OAK_LE_UN
                or optAssertionKind.OAK_GT_UN or optAssertionKind.OAK_GE_UN;
            return kind switch {
                optAssertionKind.OAK_EQUAL => GT_EQ,
                optAssertionKind.OAK_NOT_EQUAL => GT_NE,
                optAssertionKind.OAK_LT or optAssertionKind.OAK_LT_UN => GT_LT,
                optAssertionKind.OAK_LE or optAssertionKind.OAK_LE_UN => GT_LE,
                optAssertionKind.OAK_GT or optAssertionKind.OAK_GT_UN => GT_GT,
                optAssertionKind.OAK_GE or optAssertionKind.OAK_GE_UN => GT_GE,
                _ => throw new UnreachableException(),
            };
        }

        public static optAssertionKind ReverseKind(optAssertionKind kind) => kind switch {
            optAssertionKind.OAK_EQUAL => optAssertionKind.OAK_NOT_EQUAL,
            optAssertionKind.OAK_NOT_EQUAL => optAssertionKind.OAK_EQUAL,
            optAssertionKind.OAK_LT => optAssertionKind.OAK_GE,
            optAssertionKind.OAK_LT_UN => optAssertionKind.OAK_GE_UN,
            optAssertionKind.OAK_LE => optAssertionKind.OAK_GT,
            optAssertionKind.OAK_LE_UN => optAssertionKind.OAK_GT_UN,
            optAssertionKind.OAK_GT => optAssertionKind.OAK_LE,
            optAssertionKind.OAK_GT_UN => optAssertionKind.OAK_LE_UN,
            optAssertionKind.OAK_GE => optAssertionKind.OAK_LT,
            optAssertionKind.OAK_GE_UN => optAssertionKind.OAK_LT_UN,
            _ => throw new UnreachableException(),
        };

        public static bool IsReversible(optAssertionKind kind) => kind is not optAssertionKind.OAK_SUBRANGE;

        public AssertionDsc Reverse()
        {
            assert(IsReversible(Kind));
            return new(ReverseKind(Kind), _op1, _op2);
        }

        public bool IsRelop => Kind is optAssertionKind.OAK_LT or optAssertionKind.OAK_LT_UN
            or optAssertionKind.OAK_LE or optAssertionKind.OAK_LE_UN or optAssertionKind.OAK_GT
            or optAssertionKind.OAK_GT_UN or optAssertionKind.OAK_GE or optAssertionKind.OAK_GE_UN;

        public static bool ComplementaryKind(optAssertionKind kind, optAssertionKind other)
            => IsReversible(kind) && (ReverseKind(kind) == other);

        public bool HasSameOp1(AssertionDsc other, bool vnBased)
        {
            if (!Op1.KindIs(other.Op1.Kind))
            {
                return false;
            }

            if (Op1.KindIs(optOp1Kind.O1K_VN, optOp1Kind.O1K_EXACT_TYPE, optOp1Kind.O1K_SUBTYPE))
            {
                assert(vnBased);
                return Op1.VN == other.Op1.VN;
            }

            assert(!vnBased);
            return Op1.LclNum == other.Op1.LclNum;
        }

        public bool HasSameOp2(AssertionDsc other, bool vnBased)
        {
            if (!Op2.KindIs(other.Op2.Kind))
            {
                return false;
            }

            return Op2.Kind switch {
                optOp2Kind.O2K_CONST_INT => (Op2.IntConstant == other.Op2.IntConstant) && (Op2.IconFlag == other.Op2.IconFlag),
                // Bit identity preserves signed zero and NaN payloads, unlike double equality.
                optOp2Kind.O2K_CONST_DOUBLE => BitConverter.DoubleToInt64Bits(Op2.DoubleConstant)
                    == BitConverter.DoubleToInt64Bits(other.Op2.DoubleConstant),
                optOp2Kind.O2K_CONST_VEC => Op2.SimdConstant.SequenceEqual(other.Op2.SimdConstant),
                optOp2Kind.O2K_ZEROOBJ => true,
                optOp2Kind.O2K_VN_ADD_CNS => (Op2.VN == other.Op2.VN) && (Op2.Cns == other.Op2.Cns)
                    && (Op2.IsVNNeverNegative == other.Op2.IsVNNeverNegative),
                optOp2Kind.O2K_LCLVAR_COPY => Op2.LclNum == other.Op2.LclNum,
                optOp2Kind.O2K_SUBRANGE => Op2.Range.Equals(other.Op2.Range),
                _ => throw new UnreachableException(),
            };
        }

        public bool Complementary(AssertionDsc other, bool vnBased)
            => ComplementaryKind(Kind, other.Kind) && HasSameOp1(other, vnBased) && HasSameOp2(other, vnBased);

        public bool Equals(AssertionDsc other, bool vnBased)
            => KindIs(other.Kind) && HasSameOp1(other, vnBased) && HasSameOp2(other, vnBased);

        private static AssertionDscOp1 ConstantOp1(Compiler comp, int lclNum, ValueNum vn)
        {
            if (comp.optLocalAssertionProp)
            {
                assert(lclNum != BAD_VAR_NUM);
                return new(comp, optOp1Kind.O1K_LCLVAR, lclNum);
            }

            assert(vn != ValueNumStore.NoVN);
            return new(comp, optOp1Kind.O1K_VN, vn);
        }

        private static ValueNum ConstantVN(Compiler comp, ValueNum vn)
        {
            assert(comp.optLocalAssertionProp || (vn != ValueNumStore.NoVN));
            return comp.optLocalAssertionProp && (vn != ValueNumStore.VNForNull()) ? ValueNumStore.NoVN : vn;
        }

        public static AssertionDsc CreateConstLclVarAssertion(Compiler comp, int lclNum, ValueNum vn,
            nint cns, ValueNum cnsVN, bool equals, GenTreeFlags iconFlags = GTF_EMPTY, FieldSeq? fieldSeq = null)
            => new(equals ? optAssertionKind.OAK_EQUAL : optAssertionKind.OAK_NOT_EQUAL,
                ConstantOp1(comp, lclNum, vn),
                new(comp, optOp2Kind.O2K_CONST_INT, ConstantVN(comp, cnsVN), iconVal: cns, iconFlags: iconFlags, fieldSeq: fieldSeq));

        public static AssertionDsc CreateConstLclVarAssertion(Compiler comp, int lclNum, ValueNum vn,
            double cns, ValueNum cnsVN, bool equals)
            => new(equals ? optAssertionKind.OAK_EQUAL : optAssertionKind.OAK_NOT_EQUAL,
                ConstantOp1(comp, lclNum, vn), new(comp, optOp2Kind.O2K_CONST_DOUBLE, ConstantVN(comp, cnsVN), dconVal: cns));

        public static AssertionDsc CreateConstLclVarAssertion(Compiler comp, int lclNum, ValueNum vn,
            optOp2Kind cns, ValueNum cnsVN, bool equals)
        {
            assert(cns is optOp2Kind.O2K_ZEROOBJ);
            return new(equals ? optAssertionKind.OAK_EQUAL : optAssertionKind.OAK_NOT_EQUAL,
                ConstantOp1(comp, lclNum, vn), new(comp, cns, ConstantVN(comp, cnsVN)));
        }

#if FEATURE_SIMD
        public static AssertionDsc CreateConstLclVarAssertion(Compiler comp, int lclNum, ValueNum vn,
            GenTreeVecCon cns, ValueNum cnsVN, bool equals)
        {
            assert(varTypeIsSimd(cns.Type));
            var size = cns.Type.Size;
#if TARGET_ARM64
            if (cns.Type is TYP_SIMD)
            {
                throw new NotImplementedException("ARM64 scalable assertion constants are not yet ported.");
            }
#endif
            // Own the active bytes: later node mutations must not change an assertion.
            var payload = cns.SimdVal.AsSpan<byte>()[..size].ToArray();
            return new(equals ? optAssertionKind.OAK_EQUAL : optAssertionKind.OAK_NOT_EQUAL,
                ConstantOp1(comp, lclNum, vn), new(comp, optOp2Kind.O2K_CONST_VEC, ConstantVN(comp, cnsVN), simdValue: payload));
        }
#endif

        public static AssertionDsc CreateLclNonNullAssertion(Compiler comp, int lclNum)
        {
            assert(comp.optLocalAssertionProp);
            return CreateConstLclVarAssertion(comp, lclNum, ValueNumStore.NoVN, (nint)0, ValueNumStore.VNForNull(), false);
        }

        public static AssertionDsc CreateVNNonNullAssertion(Compiler comp, ValueNum vn)
        {
            assert(!comp.optLocalAssertionProp);
            return CreateConstLclVarAssertion(comp, BAD_VAR_NUM, vn, (nint)0, ValueNumStore.VNForNull(), false);
        }

        public static AssertionDsc CreateLclvarCopy(Compiler comp, int lclNum1, int lclNum2, bool equals)
        {
            assert(comp.optLocalAssertionProp);
            assert((lclNum1 != BAD_VAR_NUM) && (lclNum2 != BAD_VAR_NUM));
            return new(equals ? optAssertionKind.OAK_EQUAL : optAssertionKind.OAK_NOT_EQUAL,
                new(comp, optOp1Kind.O1K_LCLVAR, lclNum1), new(comp, optOp2Kind.O2K_LCLVAR_COPY, 0, lclNum: lclNum2));
        }

        public static AssertionDsc CreateSubrange(Compiler comp, int lclNum, IntegralRange range)
        {
            assert(comp.optLocalAssertionProp);
            assert(lclNum != BAD_VAR_NUM);
            return new(optAssertionKind.OAK_SUBRANGE, new(comp, optOp1Kind.O1K_LCLVAR, lclNum),
                new(comp, optOp2Kind.O2K_SUBRANGE, 0, range: range));
        }

        public static AssertionDsc CreateNoThrowArrBnd(Compiler comp, ValueNum idxVN, ValueNum lenVN)
        {
            assert((idxVN != ValueNumStore.NoVN) && (lenVN != ValueNumStore.NoVN));
            return new(optAssertionKind.OAK_LT_UN, new(comp, optOp1Kind.O1K_VN, idxVN),
                new(comp, optOp2Kind.O2K_VN_ADD_CNS, lenVN, isVNNeverNegative: true));
        }

        public static AssertionDsc CreateInt32ConstantVNAssertion(Compiler comp, ValueNum op1VN, ValueNum op2VN, bool equals)
        {
            assert((op1VN != ValueNumStore.NoVN) && (op2VN != ValueNumStore.NoVN));
            assert(comp.vnStore is not null);
            assert(comp.vnStore.IsVNInt32Constant(op2VN) && !comp.vnStore.IsVNHandle(op2VN));
            assert(!comp.optLocalAssertionProp);
            return new(equals ? optAssertionKind.OAK_EQUAL : optAssertionKind.OAK_NOT_EQUAL,
                new(comp, optOp1Kind.O1K_VN, op1VN),
                new(comp, optOp2Kind.O2K_CONST_INT, op2VN, iconVal: comp.vnStore.ConstantValue<int>(op2VN)));
        }

        public static AssertionDsc CreateSubtype(Compiler comp, ValueNum objVN, ValueNum typeHndVN, bool exact)
        {
            assert(comp.vnStore is not null);
            assert((objVN != ValueNumStore.NoVN) && comp.vnStore.IsVNTypeHandle(typeHndVN));
            return new(optAssertionKind.OAK_EQUAL, new(comp, exact ? optOp1Kind.O1K_EXACT_TYPE : optOp1Kind.O1K_SUBTYPE, objVN),
                new(comp, optOp2Kind.O2K_CONST_INT, typeHndVN, iconVal: comp.vnStore.CoercedConstantValue<nint>(typeHndVN)));
        }

        public static AssertionDsc CreateConstantBound(Compiler comp, VNFunc relop, ValueNum op1VN, ValueNum cnsVN)
        {
            assert(op1VN != ValueNumStore.NoVN);
            assert(comp.vnStore is not null);
            var isConstant = comp.vnStore.IsVNIntegralConstant(cnsVN, out nint constant);
            assert(isConstant);
            return new(FromVNFunc(relop), new(comp, optOp1Kind.O1K_VN, op1VN),
                new(comp, optOp2Kind.O2K_CONST_INT, cnsVN, iconVal: constant));
        }

        public static AssertionDsc CreateCompareCheckedBound(Compiler comp, VNFunc relop, ValueNum op1VN,
            ValueNum checkedBndVN, int cns, bool isVNNeverNegative = false)
        {
            assert((op1VN != ValueNumStore.NoVN) && (checkedBndVN != ValueNumStore.NoVN));
            assert(comp.vnStore is not null);
            return new(FromVNFunc(relop), new(comp, optOp1Kind.O1K_VN, op1VN),
                new(comp, optOp2Kind.O2K_VN_ADD_CNS, checkedBndVN, iconVal: cns,
                    isVNNeverNegative: isVNNeverNegative || comp.vnStore.IsVNNeverNegative(checkedBndVN)));
        }

        public static AssertionDsc CreateRelopVN(Compiler comp, VNFunc relop, ValueNum op1VN, ValueNum op2VN)
        {
            assert(!comp.optLocalAssertionProp);
            assert((op1VN != ValueNumStore.NoVN) && (op2VN != ValueNumStore.NoVN) && (op1VN != op2VN));
            assert(comp.vnStore is not null);
            return new(FromVNFunc(relop), new(comp, optOp1Kind.O1K_VN, op1VN),
                new(comp, optOp2Kind.O2K_VN_ADD_CNS, op2VN, isVNNeverNegative: comp.vnStore.IsVNNeverNegative(op2VN)));
        }
    }
}
