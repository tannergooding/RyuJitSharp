// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public static partial class genTreeOpsExtensions
{
    private static ReadOnlySpan<genTreeOps> s_reversedRelops => [
        GT_NE,          // GT_EQ
        GT_EQ,          // GT_NE
        GT_GE,          // GT_LT
        GT_GT,          // GT_LE
        GT_LT,          // GT_GE
        GT_LE,          // GT_GT
        GT_TEST_NE,     // GT_TEST_EQ
        GT_TEST_EQ,     // GT_TEST_NE
        GT_BITTEST_NE,  // GT_BITTEST_EQ
        GT_BITTEST_EQ,  // GT_BITTEST_NE
    ];

    private static ReadOnlySpan<genTreeOps> s_swappedRelops => [
        GT_EQ,          // GT_EQ
        GT_NE,          // GT_NE
        GT_GT,          // GT_LT
        GT_GE,          // GT_LE
        GT_LE,          // GT_GE
        GT_LT,          // GT_GT
        GT_TEST_EQ,     // GT_TEST_EQ
        GT_TEST_NE,     // GT_TEST_NE
        GT_BITTEST_EQ,  // GT_BITTEST_EQ
        GT_BITTEST_NE,  // GT_BITTEST_NE
    ];

    extension(genTreeOps oper)
    {
        public bool ConsumesFlags
        {
            get
            {
                if (Configuration.TARGET_ARM64)
                {
                    return oper is GT_JCC or GT_SETCC or GT_SELECTCC or GT_CCMP
                        or GT_SELECT_INCCC or GT_SELECT_INVCC or GT_SELECT_NEGCC;
                }

                if (Configuration.TARGET_AMD64)
                {
                    assert(AreContiguous(GT_JCC, GT_SETCC, GT_SELECTCC, GT_CCMP));
                    return oper is >= GT_JCC and <= GT_CCMP;
                }

                if (Configuration.TARGET_32BIT)
                {
                    assert(AreContiguous(GT_JCC, GT_SETCC, GT_SELECTCC));
                    return oper is (>= GT_JCC and <= GT_SELECTCC) or GT_ADD_HI or GT_SUB_HI or GT_CCMP;
                }

                assert(AreContiguous(GT_JCC, GT_SETCC, GT_SELECTCC));
                return oper is (>= GT_JCC and <= GT_SELECTCC) or GT_CCMP;
            }
        }

        public GenTreeDebugOperKind DebugKind
        {
            get
            {
                if (!Configuration.DEBUG)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.TreeOperationDiagnosticsAreOnlyAvailableInDebugBuilds);
                }

                assert(s_debugKinds.Length == (int)(GT_COUNT));
                return s_debugKinds[(int)(oper)];
            }
        }

        public bool IsAddrMode => oper is GT_LEA;

        public bool IsAnyLocal
        {
            get
            {
                assert(AreContiguous(GT_PHI_ARG, GT_LCL_VAR, GT_LCL_FLD, GT_STORE_LCL_VAR, GT_STORE_LCL_FLD, GT_LCL_ADDR));
                return oper is >= GT_PHI_ARG and <= GT_LCL_ADDR;
            }
        }

        public bool IsArrLength => oper is GT_ARR_LENGTH or GT_MDARR_LENGTH;

        /// <summary>Is this an access of an SZ array length, MD array length, or MD array lower bounds?</summary>
        /// <remarks>Valid oper kinds for <see cref="GenTreeArrCommon" />.</remarks>
        public bool IsArrMetadata
        {
            get
            {
                assert(AreContiguous(GT_ARR_LENGTH, GT_MDARR_LENGTH, GT_MDARR_LOWER_BOUND));
                return oper is >= GT_ARR_LENGTH and <= GT_MDARR_LOWER_BOUND;
            }
        }

        public bool IsAtomic
        {
            get
            {
                assert(AreContiguous(GT_LOCKADD, GT_XAND, GT_XORR, GT_XADD, GT_XCHG, GT_CMPXCHG));
                return oper is >= GT_LOCKADD and <= GT_CMPXCHG;
            }
        }

        public bool IsBinary => (oper.Kind & GTK_BINOP) != 0;

        public bool IsBlk
        {
            get
            {
                var result = oper is GT_BLK or GT_STORE_BLK;
                assert(result == ((oper is GT_BLK) || oper.IsStoreBlk));
                return result;
            }
        }

        public bool IsCall => oper is GT_CALL;

        public bool IsCC => oper is GT_JCC or GT_SETCC;

        /// <summary>Oper is a compare that generates a cmp instruction (as opposed to a test instruction).</summary>
        public bool IsCmpCompare
        {
            get
            {
                assert(AreContiguous(GT_EQ, GT_NE, GT_LT, GT_LE, GT_GE, GT_GT));
                return oper is >= GT_EQ and <= GT_GT;
            }
        }

        public bool IsCnsFltOrDbl => oper is GT_CNS_DBL;

        public bool IsCnsIntOrI => oper is GT_CNS_INT;

        public bool IsCnsMsk => Configuration.FEATURE_MASKED_HW_INTRINSICS && (oper is GT_CNS_MSK);

        public bool IsCnsVec => Configuration.FEATURE_SIMD && (oper is GT_CNS_VEC);

        public bool IsCommutative => (oper.Kind & GTK_COMMUTE) != 0;

        public bool IsCompare
        {
            get
            {
                // Note that only GT_EQ to GT_GT are HIR nodes, GT_TEST and GT_BITTEST nodes are backend nodes only.
                assert(AreContiguous(GT_EQ, GT_NE, GT_LT, GT_LE, GT_GE, GT_GT, GT_TEST_EQ, GT_TEST_NE, GT_BITTEST_EQ, GT_BITTEST_NE));
                return Configuration.TARGET_XARCH
                    ? oper is >= GT_EQ and <= GT_BITTEST_NE
                    : oper is >= GT_EQ and <= GT_TEST_NE;
            }
        }

        public bool IsConditional =>
            oper is GT_SELECT ||
            (Configuration.TARGET_ARM64 && (oper is GT_SELECT_INC or GT_SELECT_INV or GT_SELECT_NEG));

        public bool IsConditionalJump
        {
            get
            {
                assert(AreContiguous(GT_JCMP, GT_JTEST, GT_JCC));
                return oper is GT_JTRUE or (>= GT_JCMP and <= GT_JCC) ||
                    (Configuration.TARGET_WASM && (oper is GT_WASM_JEXCEPT));
            }
        }

        public bool IsConst
        {
            get
            {
                assert(AreContiguous(GT_CNS_INT, GT_CNS_LNG, GT_CNS_DBL, GT_CNS_STR, GT_CNS_VEC, GT_CNS_MSK));
                if (Configuration.FEATURE_SIMD)
                {
                    if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
                    {
                        return oper is >= GT_CNS_INT and <= GT_CNS_MSK;
                    }

                    return oper is >= GT_CNS_INT and <= GT_CNS_VEC;
                }

                return oper is >= GT_CNS_INT and <= GT_CNS_STR;
            }
        }

        public bool IsCopyOrReload => oper is GT_COPY or GT_RELOAD;

        public bool IsExOp => (oper.Kind & GTK_EXOP) != 0;

        public bool IsFieldList => oper is GT_FIELD_LIST;

        public bool IsHWIntrinsic => Configuration.FEATURE_HW_INTRINSICS && (oper is GT_HWINTRINSIC);

        public bool IsIndir
        {
            get
            {
                assert(AreContiguous(GT_LOCKADD, GT_XAND, GT_XORR, GT_XADD, GT_XCHG, GT_CMPXCHG, GT_IND, GT_STOREIND, GT_BLK, GT_STORE_BLK, GT_NULLCHECK));
                return oper is >= GT_LOCKADD and <= GT_NULLCHECK;
            }
        }

        public bool IsIndirOrArrMetaData
        {
            get
            {
                assert(AreContiguous(GT_LOCKADD, GT_XAND, GT_XORR, GT_XADD, GT_XCHG, GT_CMPXCHG, GT_IND, GT_STOREIND, GT_BLK, GT_STORE_BLK, GT_NULLCHECK, GT_ARR_LENGTH, GT_MDARR_LENGTH, GT_MDARR_LOWER_BOUND));
                return oper is >= GT_LOCKADD and <= GT_MDARR_LOWER_BOUND;
            }
        }

        public bool IsInitVal => oper is GT_INIT_VAL;

        public bool IsIntegralConst => Configuration.TARGET_32BIT
            ? oper is GT_CNS_INT or GT_CNS_LNG
            : oper is GT_CNS_INT;

        public bool IsCnsNativeLong => Configuration.TARGET_64BIT
            ? oper is GT_CNS_INT
            : oper is GT_CNS_LNG;

        public bool IsInvariant => oper.IsConst || (oper is GT_LCL_ADDR or GT_FTN_ADDR);

        public bool IsLclField => oper is GT_LCL_FLD or GT_STORE_LCL_FLD;

        public bool IsLeaf => (oper.Kind & GTK_LEAF) != 0;

        public bool IsLoad => oper is GT_IND or GT_BLK;

        public bool IsLocal
        {
            get
            {
                assert(AreContiguous(GT_PHI_ARG, GT_LCL_VAR, GT_LCL_FLD, GT_STORE_LCL_VAR, GT_STORE_LCL_FLD));
                return oper is >= GT_PHI_ARG and <= GT_STORE_LCL_FLD;
            }
        }

        public bool IsLocalField => oper is GT_LCL_FLD or GT_STORE_LCL_FLD or GT_LCL_ADDR;

        public bool IsLocalRead
        {
            get
            {
                assert(AreContiguous(GT_PHI_ARG, GT_LCL_VAR, GT_LCL_FLD));
                var result = oper is >= GT_PHI_ARG and <= GT_LCL_FLD;

                assert(result == (oper.IsLocal && !oper.IsLocalStore));
                return result;
            }
        }

        public bool IsLocalStore => oper is GT_STORE_LCL_VAR or GT_STORE_LCL_FLD;

        public bool IsLong => Configuration.TARGET_32BIT && (oper is GT_CNS_LNG);

        public bool IsMdArr => oper is GT_MDARR_LENGTH or GT_MDARR_LOWER_BOUND;

        public bool IsMul => oper is GT_MUL or GT_MULHI ||
            ((Configuration.TARGET_32BIT || Configuration.TARGET_ARM64) && (oper is GT_MUL_LONG));

        public bool IsMultiOp => Configuration.FEATURE_HW_INTRINSICS && (oper is GT_HWINTRINSIC);

        public bool IsMultiRegOp => Configuration.TARGET_32BIT && (oper is GT_MUL_LONG);

        public bool IsNonPhiLocal
        {
            get
            {
                assert(AreContiguous(GT_LCL_VAR, GT_LCL_FLD, GT_STORE_LCL_VAR, GT_STORE_LCL_FLD));
                var result = oper is >= GT_LCL_VAR and <= GT_STORE_LCL_FLD;

                assert(result == (oper.IsLocal && (oper is not GT_PHI_ARG)));
                return result;
            }
        }

        public bool IsPutArg => oper is GT_PUTARG_REG or GT_PUTARG_STK;

        public bool IsPutArgReg => oper is GT_PUTARG_REG;

        public bool IsPutArgStk => oper is GT_PUTARG_STK;

        public bool IsRmwMemOp
        {
            get
            {
                if (!Configuration.TARGET_XARCH)
                {
                    return false;
                }

                assert(AreContiguous(GT_OR, GT_XOR, GT_AND, GT_LSH, GT_RSH, GT_RSZ, GT_ROL, GT_ROR));
                return oper is GT_NOT or GT_NEG or GT_ADD or GT_SUB or (>= GT_OR and <= GT_ROR);
            }
        }

        public bool IsRotate => oper is GT_ROL or GT_ROR;

        public bool IsScalarLocal => oper is GT_LCL_VAR or GT_STORE_LCL_VAR;

        public bool IsShift
        {
            get
            {
                assert(AreContiguous(GT_LSH, GT_RSH, GT_RSZ));
                return oper is >= GT_LSH and <= GT_RSZ;
            }
        }

        public bool IsShiftLong => Configuration.TARGET_32BIT && (oper is GT_LSH_HI or GT_RSH_LO);

        public bool IsShiftOrRotate
        {
            get
            {
                if (Configuration.TARGET_32BIT)
                {
                    assert(AreContiguous(GT_LSH, GT_RSH, GT_RSZ, GT_ROL, GT_ROR));
                    var longResult = oper is (>= GT_LSH and <= GT_ROR) or GT_LSH_HI or GT_RSH_LO;

                    assert(longResult == (oper.IsShift || oper.IsRotate || oper.IsShiftLong));
                    return longResult;
                }

                assert(AreContiguous(GT_LSH, GT_RSH, GT_RSZ, GT_ROL, GT_ROR));
                var result = oper is >= GT_LSH and <= GT_ROR;

                assert(result == (oper.IsShift || oper.IsRotate));
                return result;
            }
        }

        public bool IsSimple => (oper.Kind & GTK_SMPOP) != 0;

        public bool IsSpecial => (oper.Kind & GTK_KINDMASK) == GTK_SPECIAL;

        public bool IsSsaDef => oper is GT_STORE_LCL_VAR or GT_STORE_LCL_FLD or GT_CALL;

        public bool IsStore => (oper.Kind & GTK_STORE) != 0;

        public bool IsStoreBlk => oper is GT_STORE_BLK;

        /// <summary>This returns true only for GT_IND and GT_STOREIND, and is used in contexts where a "true" indirection is expected (i.e. either a load to or a store from a single register).</summary>
        /// <remarks><see cref="get_IsIndir"/> returns true also for indirection nodes such as GT_BLK, etc. as well as GT_NULLCHECK.</remarks>
        public bool IsTrueIndir => oper is GT_IND or GT_STOREIND;

        public bool IsUnary => (oper.Kind & GTK_UNOP) != 0;

        public GenTreeOperKind Kind
        {
            get
            {
                assert(s_kinds.Length == (int)(GT_COUNT));
                return s_kinds[(int)(oper)];
            }
        }

        public bool MayOverflow
        {
            get
            {
                assert(AreContiguous(GT_ADD, GT_SUB, GT_MUL));
                return oper is (>= GT_ADD and <= GT_MUL) or GT_CAST ||
                    (Configuration.TARGET_32BIT && (oper is GT_ADD_HI or GT_SUB_HI));
            }
        }

        public string Name
        {
            get
            {
                if (Configuration.DEBUG || (JitConfig.DisplayMemStats != 0))
                {
                    assert(NameTable.s_names.Length == (int)(GT_COUNT));
                    return NameTable.s_names[(int)(oper)];
                }

                return oper.ToString();
            }
        }

        public genTreeOps ReverseRelop
        {
            get
            {
                assert(oper.IsCompare);
                return s_reversedRelops[oper - GT_EQ];
            }
        }

        public string StructName => oper.StructType.Name;

        public Type StructType
        {
            get
            {
                if (!Configuration.DEBUG && !Configuration.MEASURE_NODE_SIZE)
                {
                    JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.OperatorStructTypeMetadataIsOnlyAvailableInDebugOrNodeSizeMeasurementBuilds);
                }

                assert(StructTypeTable.s_structTypes.Length == (int)(GT_COUNT));
                return StructTypeTable.s_structTypes[(int)(oper)];
            }
        }

        public genTreeOps SwapRelop
        {
            get
            {
                assert(oper.IsCompare);
                return s_swappedRelops[oper - GT_EQ];
            }
        }
    }
}
