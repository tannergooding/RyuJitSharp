// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class GenTree
{
    internal genTreeOps _oper;

    internal var_types _type;

#if DEBUG
    // Only used to save gtOper when we destroy a node, to aid debugging.
    internal genTreeOps _operSave;
#endif

    /// <summary>0 or the CSE index (negated if def)</summary>
    /// <remarks>valid only for CSE expressions</remarks>
    private sbyte _cseNum;

    /// <summary>Used for nodes that are in LIR. See LIR.Flags in lir.h for the various flags.</summary>
    internal LIR.Flags _lirFlags;

    private AssertionInfo _assertionInfo;

#if DEBUG
    // You are not allowed to read the cost values before they have been set in gtSetEvalOrder().
    // Keep track of whether the costs have been initialized, and assert if they are read before being initialized.
    // Obviously, this information does need to be initialized when a node is created.
    // This is public so the dumpers can see it.
    private bool _costsInitialized;
#endif

    /// <summary>estimate of expression execution cost</summary>
    private byte _costEx;

    /// <summary>estimate of expression code size cost</summary>
    private byte _costSz;

#if DEBUG
    /// <summary>What is in _gtRegNum?</summary>
    private genRegTag _regTag;
#endif

    /// <summary>This stores the register assigned to the node. If a register is not assigned, _gtRegNum is set to REG_NA.</summary>
    private regNumber _regNum;

    private GenTreeFlags _flags;

#if DEBUG
    internal GenTreeDebugFlags _debugFlags;

    internal ushort _morphCount;
#endif

    internal ValueNumPair _vnPair;

    private GenTree? _next;

    private GenTree? _prev;

#if DEBUG
    private int _treeId;

    /// <summary>liveness traversal order within the current statement</summary>
    internal int _seqNum;

    /// <summary>use-ordered traversal within the function</summary>
    private int _useNum;
#endif

    protected internal GenTree(genTreeOps oper, var_types type)
    {
        assert(type.ActualType == type);

        _oper = oper;
        _type = type;

        RegNum = REG_NA;

#if COUNT_AST_OPERS
        Interlocked.Increment(ref s_gtNodeCounts[oper]);
#endif

#if DEBUG
        var compiler = JitTls.Compiler;
        assert(compiler is not null);

        _useNum = -1;
        _treeId = compiler.compGenTreeID++;
        _vnPair.SetBoth(ValueNumStore.NoVN);
#endif
    }

    public AssertionInfo AssertionInfo
    {
        get
        {
            return _assertionInfo;
        }

        set
        {
            _assertionInfo = value;
        }
    }

#if DEBUG
    /// <summary>check whether this tree node may be a subcomponent of its parent for purposes of code generation.</summary>
    public bool CanBeContained
    {
        get
        {
            assert(Debugger.IsAttached || IsLirOp);
            var result = true;

            if (IsMultiRegLclVar)
            {
                result = false;
            }
            else if (HasReg(compiler: null))
            {
                result = false;
            }
            else if (!IsValue || ((_oper.DebugKind & DBK_NOCONTAIN) != 0))
            {
                // It is not possible for nodes that do not produce values or that are not containable values to be contained.
                result = false;
            }
            else if (_oper.IsHWIntrinsic)
            {
                result = IsContainableHWIntrinsic;
            }
            return result;
        }
    }
#endif

    public byte CostEx
    {
        get
        {
#if DEBUG
            assert(Debugger.IsAttached || _costsInitialized);
#endif
            return _costEx;
        }
    }

    public byte CostSz
    {
        get
        {
#if DEBUG
            assert(Debugger.IsAttached || _costsInitialized);
#endif
            return _costSz;
        }
    }

    public GenTree Data
    {
        get
        {
            if (_oper.IsStore)
            {
                if (_oper.IsLocalStore)
                {
                    return AsLclVarCommon().Data;
                }
                else
                {
                    return AsIndir().Data;
                }
            }
            else
            {
                assert(Debugger.IsAttached);
                return null!;
            }
        }
    }

    public ref GenTree DataRef
    {
        get
        {
            if (_oper.IsStore)
            {
                if (_oper.IsLocalStore)
                {
                    return ref AsLclVarCommon().DataRef;
                }
                else
                {
                    return ref AsIndir().DataRef;
                }
            }
            else
            {
                assert(Debugger.IsAttached);
                return ref Unsafe.NullRef<GenTree>();
            }
        }
    }

    public GenTree EffectiveVal
    {
        get
        {
            var result = this;

            while (result._oper is GT_COMMA)
            {
                result = result.AsOp().Op2;
            }

            return result;
        }
    }

    public bool GeneratesAssertion => _assertionInfo.HasAssertion;

    public bool HasOrderingSideEffect
    {
        get
        {
            assert(Debugger.IsAttached || SupportsOrderingSideEffect());
            return (_flags & GTF_ORDER_SIDEEFF) != 0;
        }

        set
        {
            assert(SupportsOrderingSideEffect());
            _flags |= GTF_ORDER_SIDEEFF;
        }
    }

    public bool HasOverflowCheck
    {
        get
        {
            assert(Debugger.IsAttached || _oper.MayOverflow);
            return (_flags & GTF_OVERFLOW) != 0;
        }
    }

    public GenTree IndirOrArrMetaDataAddr
    {
        get
        {
            if (_oper.IsIndir)
            {
                return AsIndir().Addr;
            }
            else if (_oper.IsArrMetadata)
            {
                return AsArrCommon().ArrRef;
            }
            else
            {
                assert(Debugger.IsAttached);
                return null!;
            }
        }
    }

    public bool IsCnsNonZeroFltOrDbl => _oper.IsCnsFltOrDbl && AsDblCon().IsBitwiseEqual(0);

#if FEATURE_HW_INTRINSICS
    // TODO: Port isContainableHWIntrinsic
    public bool IsContainableHWIntrinsic => false;
#else
    public bool IsContainableHWIntrinsic => false;
#endif

    /// <summary>check whether this tree node is a subcomponent of its parent for codegen purposes</summary>
    /// <returns>Returns true if there is no code generated explicitly for this node.</returns>
    /// <remarks>
    ///   <para>true essentially means it will be rolled into the code generation for the parent.</para>
    ///   <para>This method relies upon the value of the GTF_CONTAINED flag, therefore this method is only valid after Lowering.</para>
    ///   <para>Also note that register allocation or other subsequent phases may cause nodes to become contained (or not) and therefore this property may change.</para>
    /// </remarks>
    public bool IsContained
    {
        get
        {
            var result = ((_flags & GTF_CONTAINED) != 0);

#if DEBUG
            assert(Debugger.IsAttached || IsLirOp);

            if (!CanBeContained)
            {
                assert(!result);
            }

            if (result)
            {
                assert(!IsUnusedValue);
            }
#endif

            return result;
        }

        set
        {
            assert(IsValue);

            if (value)
            {
                _flags |= GTF_CONTAINED;
            }
            else
            {
                _flags &= ~GTF_CONTAINED;
                IsRegOptional = false;
            }

            assert(IsContained == value);
        }
    }

    public bool IsContainedFltOrDblImmed => IsContained && _oper.IsCnsFltOrDbl;

    public bool IsContainedIndir => IsContained && _oper.IsIndir;

    public bool IsContainedIntOrIImmed => IsContained && _oper.IsCnsIntOrI && !IsUsedFromSpillTemp;

    public bool IsContainedVecImmed => IsContained && _oper.IsCnsVec;

    /// <summary>whether this is a GT_COPY or GT_RELOAD of a multi-reg</summary>
    public bool IsCopyOrReloadOfMultiRegCall
    {
        get
        {
            var result = false;

            if (_oper.IsCopyOrReload)
            {
                result = AsUnOp().Op1.IsMultiRegCall;
            }
            return result;
        }
    }

    public bool IsIndirAddrMode
    {
        get
        {
            var result = false;

            if (_oper.IsIndir)
            {
                var indirAddr = AsIndir().Addr;
                result = indirAddr.Oper.IsAddrMode && indirAddr.IsContained;
            }

            return result;
        }
    }

    /// <summary>Is this node an integer constant that fits in a 32-bit signed integer</summary>
#if TARGET_32BIT
    public bool IsIntCnsFitsInI32 => _oper.IsCnsIntOrI && AsIntCon().FitsInI32;
#else
    public bool IsIntCnsFitsInI32 => _oper.IsCnsIntOrI;
#endif

    /// <summary>Determines whether the absolute value of an integral constant is the power of 2.</summary>
    public bool IsIntegralConstAbsPow2
    {
        get
        {
            var result = false;

            if (_oper.IsIntegralConst)
            {
                var value = AsIntConCommon().IntegralValue;
                result = (value == long.MinValue) || long.IsPow2(long.Abs(value));
            }

            return result;
        }
    }

    /// <summary>Determines whether an integral constant is the power of 2.</summary>
    public bool IsIntegralConstPow2 => _oper.IsIntegralConst && long.IsPow2(AsIntConCommon().IntegralValue);

    /// <summary>Determines whether the unsigned value of an integral constant is the power of 2.</summary>
    public bool IsIntegralConstUnsignedPow2 => _oper.IsIntegralConst && ulong.IsPow2(AsIntConCommon().UnsignedIntegralValue);

    public bool IsLclVarAddr => (_oper is GT_LCL_ADDR) && (AsLclFld().LclOffs == 0);

#if DEBUG
    public bool IsLirOp
    {
        get
        {
            bool result;

            if (_oper is GT_NOP)
            {
                // NOPs may only be present in LIR if they do not produce a value.
                result = IsNothingNode;
            }
            else
            {
                result = (_oper.DebugKind & DBK_NOTLIR) == 0;
            }

            return result;
        }
    }
#endif

    /// <summary>Indicates whether it is a memory op.</summary>
    /// <remarks>Right now it includes Indir and LclField ops.</remarks>
    public bool IsMemoryOp => _oper.IsTrueIndir || _oper.IsLclField;

    /// <summary>whether a call node returns its value in more than one register</summary>
    public bool IsMultiRegCall => _oper.IsCall && AsCall().HasMultiRegRetVal;

    /// <summary>whether a node returning its value in more than one register</summary>
    /// <remarks>
    ///   <para>All targets that support multi-reg ops of any kind also support multi-reg return values for calls.</para>
    ///   <para>Should that change with a future target, this method will need to change accordingly.</para>
    /// </remarks>
    public bool IsMultiRegNode
    {
        get
        {
#if FEATURE_MULTIREG_RET
            if (IsMultiRegCall)
            {
                return true;
            }
#endif

#if FEATURE_MULTIREG_RET && TARGET_32BIT
            if (OperIsMultiRegOp())
            {
                return AsMultiRegOp().RegCount > 1;
            }
#endif

            if (_oper.IsCopyOrReload)
            {
                return true;
            }

#if FEATURE_HW_INTRINSICS
            if (_oper.IsHWIntrinsic)
            {
                return HWIntrinsicInfo.IsMultiReg(AsHWIntrinsic().HWIntrinsicId);
            }
#endif

            return IsMultiRegLclVar;
        }
    }

    /// <summary>whether a local var node defines multiple registers</summary>
    public bool IsMultiRegLclVar => _oper.IsScalarLocal && AsLclVar().IsMultiReg;

    public bool IsNothingNode => (_oper is GT_NOP) && (_type is TYP_VOID);

    public bool IsPhiDefn
    {
        get
        {
            var result = false;

            if (_oper is GT_STORE_LCL_VAR)
            {
                result = AsLclVar().Data.Oper is GT_PHI;
            }
            return result;
        }
    }

    /// <summary>indicates that codegen can still generate code even if it isn't allocated a register.</summary>
    public bool IsRegOptional
    {
        get
        {
            return (_lirFlags & LIR.Flags.RegOptional) != 0;
        }

        set
        {
            _lirFlags = (_lirFlags & ~LIR.Flags.RegOptional) | (value ? LIR.Flags.RegOptional : 0);
        }
    }

    public bool IsReverseOp
    {
        get
        {
            return (_flags & GTF_REVERSE_OPS) != 0;
        }

        set
        {
            _flags = (_flags & ~GTF_REVERSE_OPS) | (value ? GTF_REVERSE_OPS : GTF_EMPTY);
        }
    }

    public bool IsUnusedValue
    {
        get
        {
            return (_lirFlags & LIR.Flags.UnusedValue) != 0;
        }

        set
        {
            if (value)
            {
                _lirFlags |= LIR.Flags.UnusedValue;
                IsContained = false;
            }
            else
            {
                _lirFlags &= ~LIR.Flags.UnusedValue;
            }
        }
    }

    public bool IsUsedFromMemory
    {
        get
        {
            var result = false;

            if (IsContained)
            {
                var oper = _oper;

                if (IsMemoryOp)
                {
                    result = true;
                }
                else if ((oper is GT_LCL_VAR) || oper.IsCnsFltOrDbl)
                {
                    result = true;
                }

#if FEATURE_SIMD
                else if (_oper.IsCnsVec)
                {
                    result = true;
                }
#endif
#if FEATURE_MASKED_HW_INTRINSICS
                else if (_oper.IsCnsMsk)
                {
                    result = true;
                }
#endif
            }

            if (!result)
            {
                result = IsUsedFromSpillTemp;
            }
            return result;
        }
    }

    public bool IsUsedFromReg => !IsContained && !IsUsedFromSpillTemp;

    // If spilled and no reg at use, then it is used from the spill temp location rather than being reloaded.
    public bool IsUsedFromSpillTemp => (_flags & (GTF_SPILLED | GTF_NOREG_AT_USE)) == (GTF_SPILLED | GTF_NOREG_AT_USE);

    public bool IsValue
    {
        get
        {
            var result = true;
            var oper = _oper;

            if ((oper.Kind & GTK_NOVALUE) != 0)
            {
                result = false;
            }
            else if (_type is TYP_VOID)
            {
                // These are the only operators which can produce either VOID or non-VOID results.
                assert((oper is GT_NOP or GT_COMMA) || oper.IsCall || oper.IsCompare || oper.IsLong || oper.IsHWIntrinsic || oper.IsCnsVec || oper.IsCnsMsk);
                result = false;
            }

            return result;
        }
    }

    public GenTree? Next
    {
        get
        {
            return _next;
        }

        set
        {
            _next = value;
        }
    }

    public genTreeOps Oper => _oper;

    public GenTreeOperandsList Operands => new GenTreeOperandsList(this);

    public GenTree? Prev
    {
        get
        {
            return _prev;
        }

        set
        {
            _prev = value;
        }
    }

    public regNumber RegNum
    {
        get
        {
#if DEBUG
            // TODO-Cleanup: get rid of the NONE case, and fix everyplace that reads undefined values
            assert((_regTag is GT_REGTAG_NONE) || genIsValidReg(_regNum) || (_regNum is REG_NA));
#endif
            return _regNum;
        }

        set
        {
            genIsValidReg(value);
            _regNum = value;

#if DEBUG
            _regTag = GT_REGTAG_REG;
#endif
        }
    }

#if DEBUG
    public genRegTag RegTag
    {
        get
        {
            assert(_regTag is GT_REGTAG_NONE or GT_REGTAG_REG);
            return _regTag;
        }
    }
#endif

    public bool RequiresAsgFlag => _oper switch {
        GT_STORE_LCL_VAR => true,
        GT_STORE_LCL_FLD => true,
        GT_MEMORYBARRIER => true,
        GT_LOCKADD => true,
        GT_XAND => true,
        GT_XORR => true,
        GT_XADD => true,
        GT_XCHG => true,
        GT_CMPXCHG => true,
        GT_STOREIND => true,
        GT_STORE_BLK => true,
        GT_CALL => AsCall().IsOptimizingRetBufAsLocal,
        GT_HWINTRINSIC => AsHWIntrinsic().IsMemoryStoreOrBarrier,
        _ => false,
    };

    public GenTree SkipCopyOrReload
    {
        get
        {
            var result = this;

            if (_oper.IsCopyOrReload)
            {
                var op1 = AsUnOp().Op1;
                assert(!op1._oper.IsCopyOrReload);
                result = op1;
            }

            return result;
        }
    }

    /// <summary>Check whether the operation supports the GTF_ORDER_SIDEEFF flag.</summary>
    /// <returns>True if the given operator supports GTF_ORDER_SIDEEFF.</returns>
    /// <remarks>A node will still have this flag set if an operand has it set, even if the parent does not support it. This situation indicates that reordering the parent may be ok as long as it does not break ordering dependencies of the operand.</remarks>
    public bool SupportsOrderingSideEffect()
    {
        if (_type is TYP_BYREF)
        {
            // Forming byrefs may only be legal due to previous checks.
            return true;
        }

        return _oper switch {
            GT_ARR_LENGTH => true,
            GT_MDARR_LENGTH => true,
            GT_MDARR_LOWER_BOUND => true,
            GT_ARR_ADDR => true,
            GT_BOUNDS_CHECK => true,
            GT_IND => true,
            GT_BLK => true,
            GT_STOREIND => true,
            GT_NULLCHECK => true,
            GT_STORE_BLK => true,
            GT_XADD => true,
            GT_XORR => true,
            GT_XAND => true,
            GT_XCHG => true,
            GT_LOCKADD => true,
            GT_CMPXCHG => true,
            GT_MEMORYBARRIER => true,
            GT_CATCH_ARG => true,
            GT_ASYNC_CONTINUATION => true,
            GT_RETURN_SUSPEND => true,
            GT_PATCHPOINT => true,
            GT_PATCHPOINT_FORCED => true,
            GT_NONLOCAL_JMP => true,
#if SWIFT_SUPPORT
            GT_SWIFT_ERROR => true,
#endif
            _ => false,
        };
    }

#if DEBUG
    public int TreeId => _treeId;
#endif

    public var_types Type
    {
        get
        {
            return _type;
        }

        set
        {
            _type = value;
        }
    }

    public GenTreeUseEdgesList UseEdges => new GenTreeUseEdgesList(this);

#if DEBUG
    public bool WasMorphed => (_debugFlags & GTF_DEBUG_NODE_MORPHED) != 0;
#endif

    protected internal GenTreeFlags Flags
    {
        get
        {
            return _flags;
        }

        set
        {
            _flags = value;
        }
    }

    public static bool Compare(GenTree op1, GenTree op2, bool swapOk = false)
    {
        // TODO: Port GenTree.Compare
        return false;
    }

    public static void InitNodeSize()
    {
        // TODO: Port GenTree.InitNodeSize
    }

    public GenTreeUnOp AsUnOp() => Unsafe.As<GenTreeUnOp>(this);

    public GenTreeOp AsOp() => Unsafe.As<GenTreeOp>(this);

    public GenTreeVal AsVal() => Unsafe.As<GenTreeVal>(this);

    public GenTreeIntConCommon AsIntConCommon() => Unsafe.As<GenTreeIntConCommon>(this);

    public GenTreeIntCon AsIntCon() => Unsafe.As<GenTreeIntCon>(this);

    public GenTreeLngCon AsLngCon() => Unsafe.As<GenTreeLngCon>(this);

    public GenTreeDblCon AsDblCon() => Unsafe.As<GenTreeDblCon>(this);

    public GenTreeStrCon AsStrCon() => Unsafe.As<GenTreeStrCon>(this);

#if FEATURE_SIMD
    public GenTreeVecCon AsVecCon() => Unsafe.As<GenTreeVecCon>(this);
#endif

#if FEATURE_MASKED_HW_INTRINSICS
    public GenTreeMskCon AsMskCon() => Unsafe.As<GenTreeMskCon>(this);
#endif

    public GenTreeLclVarCommon AsLclVarCommon() => Unsafe.As<GenTreeLclVarCommon>(this);

    public GenTreeLclVar AsLclVar() => Unsafe.As<GenTreeLclVar>(this);

    public GenTreeLclFld AsLclFld() => Unsafe.As<GenTreeLclFld>(this);

    public GenTreeCast AsCast() => Unsafe.As<GenTreeCast>(this);

    public GenTreeBox AsBox() => Unsafe.As<GenTreeBox>(this);

    public GenTreeFieldAddr AsFieldAddr() => Unsafe.As<GenTreeFieldAddr>(this);

    public GenTreeCall AsCall() => Unsafe.As<GenTreeCall>(this);

    public GenTreeFieldList AsFieldList() => Unsafe.As<GenTreeFieldList>(this);

    public GenTreeColon AsColon() => Unsafe.As<GenTreeColon>(this);

    public GenTreeFptrVal AsFptrVal() => Unsafe.As<GenTreeFptrVal>(this);

    public GenTreeIntrinsic AsIntrinsic() => Unsafe.As<GenTreeIntrinsic>(this);

    public GenTreeIndexAddr AsIndexAddr() => Unsafe.As<GenTreeIndexAddr>(this);

#if FEATURE_HW_INTRINSICS
    public GenTreeMultiOp AsMultiOp() => Unsafe.As<GenTreeMultiOp>(this);
#endif

    public GenTreeBoundsChk AsBoundsChk() => Unsafe.As<GenTreeBoundsChk>(this);

    public GenTreeArrCommon AsArrCommon() => Unsafe.As<GenTreeArrCommon>(this);

    public GenTreeArrLen AsArrLen() => Unsafe.As<GenTreeArrLen>(this);

    public GenTreeMDArr AsMDArr() => Unsafe.As<GenTreeMDArr>(this);

    public GenTreeArrElem AsArrElem() => Unsafe.As<GenTreeArrElem>(this);

    public GenTreeRetExpr AsRetExpr() => Unsafe.As<GenTreeRetExpr>(this);

    public GenTreeILOffset AsILOffset() => Unsafe.As<GenTreeILOffset>(this);

    public GenTreeCopyOrReload AsCopyOrReload() => Unsafe.As<GenTreeCopyOrReload>(this);

    public GenTreeAddrMode AsAddrMode() => Unsafe.As<GenTreeAddrMode>(this);

    public GenTreeQmark AsQmark() => Unsafe.As<GenTreeQmark>(this);

    public GenTreePhiArg AsPhiArg() => Unsafe.As<GenTreePhiArg>(this);

    public GenTreePhi AsPhi() => Unsafe.As<GenTreePhi>(this);

    public GenTreeIndir AsIndir() => Unsafe.As<GenTreeIndir>(this);

    public GenTreeBlk AsBlk() => Unsafe.As<GenTreeBlk>(this);

    public GenTreeStoreInd AsStoreInd() => Unsafe.As<GenTreeStoreInd>(this);

    public GenTreeCmpXchg AsCmpXchg() => Unsafe.As<GenTreeCmpXchg>(this);

    public GenTreeConditional AsConditional() => Unsafe.As<GenTreeConditional>(this);

    public GenTreePutArgStk AsPutArgStk() => Unsafe.As<GenTreePutArgStk>(this);

    public GenTreePhysReg AsPhysReg() => Unsafe.As<GenTreePhysReg>(this);

#if FEATURE_HW_INTRINSICS
    public GenTreeHWIntrinsic AsHWIntrinsic() => Unsafe.As<GenTreeHWIntrinsic>(this);
#endif

    public GenTreeAllocObj AsAllocObj() => Unsafe.As<GenTreeAllocObj>(this);

    public GenTreeRuntimeLookup AsRuntimeLookup() => Unsafe.As<GenTreeRuntimeLookup>(this);

    public GenTreeArrAddr AsArrAddr() => Unsafe.As<GenTreeArrAddr>(this);

    public GenTreeCC AsCC() => Unsafe.As<GenTreeCC>(this);

#if TARGET_ARM64 || TARGET_AMD64
    public GenTreeCCMP AsCCMP() => Unsafe.As<GenTreeCCMP>(this);
#endif

    public GenTreeOpCC AsOpCC() => Unsafe.As<GenTreeOpCC>(this);

#if TARGET_32BIT
    public GenTreeMultiRegOp AsMultiRegOp() => Unsafe.As<GenTreeMultiRegOp>(this);
#endif

    public void ClearAssertion() => _assertionInfo.Clear();

    [Conditional("DEBUG")]
    public void ClearMorphed()
    {
#if DEBUG
        _debugFlags &= ~GTF_DEBUG_NODE_MORPHED;
#endif
    }

    public void ClearRegNum()
    {
        _regNum = REG_NA;

#if DEBUG
        _regTag = GT_REGTAG_NONE;
#endif
    }

    /// <summary>Optimized copy function, to avoid the SetCosts() function comparisons, and make it more clear that a node copy is happening.</summary>
    /// <param name="tree"></param>
    public void CopyCosts(GenTree tree)
    {
#if DEBUG
        assert(tree._costsInitialized);
#endif
        CopyCostsRaw(tree);
    }

    /// <summary>Same as CopyCosts, but avoids asserts if the costs we are copying have not been initialized.</summary>
    /// <param name="tree"></param>
    /// <remarks>
    ///   <para>This is because the importer, for example, clones nodes, before these costs have been initialized.</para>
    ///   <para>Note that we directly access the 'tree' costs, not going through the accessor functions (either directly or through the properties).</para>
    /// </remarks>
    public void CopyCostsRaw(GenTree tree)
    {
        assert(tree._oper == _oper);

#if DEBUG
        _costsInitialized = true;
#endif

        _costEx = tree._costEx;
        _costSz = tree._costSz;
    }

    /// <summary>Copy the _gtRegNum/gtRegTag fields.</summary>
    /// <param name="tree">GenTree node from which to copy</param>
    public void CopyReg(GenTree tree)
    {
        assert(tree._oper == _oper);

        _regNum = tree._regNum;

#if DEBUG
        _regTag = tree._regTag;
#endif
    }

    /// <summary>Get exception set this tree may throw.</summary>
    /// <param name="comp">Compiler instance</param>
    /// <returns>A bit set of exceptions this tree may throw.</returns>
    /// <remarks>The ExceptionSetFlags.UnknownException must generally be handled specially by the consumer; when it is present it means we can say nothing precise about the thrown exceptions.</remarks>
    public ExceptionSetFlags Exceptions(Compiler comp)
    {
        switch (_oper)
        {
            case GT_ADD:
            case GT_SUB:
            case GT_MUL:
            case GT_CAST:
#if !TARGET_64BIT
            case GT_ADD_HI:
            case GT_SUB_HI:
#endif // !TARGET_64BIT
            {
                return HasOverflowCheck ? ExceptionSetFlags.OverflowException : ExceptionSetFlags.None;
            }

            case GT_MOD:
            case GT_DIV:
            case GT_UMOD:
            case GT_UDIV:
            {
                if (varTypeIsFloating(Type))
                {
                    return ExceptionSetFlags.None;
                }

                var exSetFlags = ExceptionSetFlags.None;
                var op = AsOp();

                if (((_flags & GTF_DIV_MOD_NO_BY_ZERO) == 0) && !op.Op2.SkipCopyOrReload.IsNeverZero())
                {
                    exSetFlags |= ExceptionSetFlags.DivideByZeroException;
                }

                if ((_oper is GT_DIV or GT_MOD) && op.CanDivOrModPossiblyOverflow(comp))
                {
                    exSetFlags |= ExceptionSetFlags.ArithmeticException;
                }
                return exSetFlags;
            }

            case GT_INTRINSIC:
            {
                // If this is an intrinsic that represents the object.GetType(), it can throw an NullReferenceException.
                // Currently, this is the only intrinsic that can throw an exception.
                if (AsIntrinsic().IntrinsicName == NI_System_Object_GetType)
                {
                    return ExceptionSetFlags.NullReferenceException;
                }
                return ExceptionSetFlags.None;
            }

            case GT_CALL:
            {
                var helper = AsCall().HelperNum;

                if (helper == CORINFO_HELP_UNDEF)
                {
                    return ExceptionSetFlags.UnknownException;
                }
                return helper.ThrownExceptions;
            }

            case GT_LOCKADD:
            case GT_XAND:
            case GT_XORR:
            case GT_XADD:
            case GT_XCHG:
            case GT_CMPXCHG:
            case GT_IND:
            case GT_STOREIND:
            case GT_BLK:
            case GT_NULLCHECK:
            case GT_STORE_BLK:
            case GT_ARR_LENGTH:
            case GT_MDARR_LENGTH:
            case GT_MDARR_LOWER_BOUND:
            {
                return IndirMayFault(comp) ? ExceptionSetFlags.NullReferenceException : ExceptionSetFlags.None;
            }

            case GT_ARR_ELEM:
            {
                if (comp.fgAddrCouldBeNull(AsArrElem().ArrObj))
                {
                    return ExceptionSetFlags.NullReferenceException | ExceptionSetFlags.IndexOutOfRangeException;
                }
                return ExceptionSetFlags.IndexOutOfRangeException;
            }

            case GT_FIELD_ADDR:
            {
                var fieldAddr = AsFieldAddr();

                if (fieldAddr.IsInstance && comp.fgAddrCouldBeNull(fieldAddr.FldObj))
                {
                    return ExceptionSetFlags.NullReferenceException;
                }
                return ExceptionSetFlags.None;
            }

            case GT_BOUNDS_CHECK:
            {
                return ExceptionSetFlags.IndexOutOfRangeException;
            }

            case GT_INDEX_ADDR:
            {
                return ExceptionSetFlags.NullReferenceException | ExceptionSetFlags.IndexOutOfRangeException;
            }

            case GT_CKFINITE:
            {
                return ExceptionSetFlags.ArithmeticException;
            }

#if TARGET_WASM
            case GT_WASM_THROW_REF:
            {
                return ExceptionSetFlags.UnknownException;
            }
#endif

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                var hwintrinsic = AsHWIntrinsic();

                if (hwintrinsic.IsUserCall)
                {
                    return ExceptionSetFlags.UnknownException;
                }

                var flags = ExceptionSetFlags.None;

                if (hwintrinsic.IsMemoryLoadOrStore)
                {
                    // TODO-CQ: We should use comp.fgAddrCouldBeNull on the address operand
                    // to determine if this can actually produce an NRE or not
                    flags |= ExceptionSetFlags.NullReferenceException;
                }

#if TARGET_XARCH
                var intrinsicId = hwintrinsic.HWIntrinsicId;

                if (intrinsicId is NI_Vector128_op_Division or NI_Vector256_op_Division or NI_Vector512_op_Division)
                {
                    // We currently don't try to avoid setting these flags and GTF_EXCEPT when
                    // we know that the operation in fact cannot overflow/divide by zero.
                    assert(varTypeIsInt(hwintrinsic.simdBaseType));
                    flags |= (ExceptionSetFlags.OverflowException | ExceptionSetFlags.DivideByZeroException);
                }
#endif

                return flags;
            }
#endif

            default:
            {
                assert(!_oper.MayOverflow && !_oper.IsIndirOrArrMetaData);
                return ExceptionSetFlags.None;
            }
        }
    }

    /// <summary>Get the struct layout for this node.</summary>
    /// <param name="compiler">The Compiler instance</param>
    /// <returns>The struct layout of this node; it must have one.</returns>
    /// <remarks>This is the "general" method for getting the layout, the more efficient node-specific ones should be used in case the node's oper is known.</remarks>
    public unsafe ClassLayout? GetLayout(Compiler compiler)
    {
        assert(varTypeIsStruct(_type));
        var structHnd = NO_CLASS_HANDLE;

        switch (_oper)
        {
            case GT_LCL_VAR:
            case GT_STORE_LCL_VAR:
            {
                return compiler.lvaGetDesc(AsLclVar().LclNum).Layout;
            }

            case GT_LCL_FLD:
            case GT_STORE_LCL_FLD:
            {
                return AsLclFld().Layout;
            }

            case GT_BLK:
            case GT_STORE_BLK:
            {
                return AsBlk().Layout;
            }

            case GT_COMMA:
            {
                return AsOp().Op2.GetLayout(compiler);
            }

#if FEATURE_HW_INTRINSICS
            case GT_HWINTRINSIC:
            {
                return AsHWIntrinsic().GetLayout(compiler);
            }
#endif

            case GT_CALL:
            {
                structHnd = AsCall().RetClsHnd;
                break;
            }

            case GT_RET_EXPR:
            {
                structHnd = AsRetExpr().InlineCandidate.RetClsHnd;
                break;
            }

            default:
            {
                unreached();
                break;
            }
        }
        return compiler.typGetObjLayout(structHnd);
    }

    /// <summary>Get the use edge for an operand of this tree.</summary>
    /// <param name="def">the node to find the use for</param>
    /// <returns>On success, the use edge for <paramref name="def" />, in which case it can be used to replace <paramref name="def" /> with another node; otherwise a <c>null</c> reference.</returns>
#nullable disable
    public ref GenTree GetUseRefOrNullRef(GenTree def)
    {
        ref var use = ref Unsafe.NullRef<GenTree>();
        var oper = _oper;

        if (!oper.IsLeaf)
        {
            if (oper.IsSpecial)
            {
                switch (_oper)
                {
                    case GT_PHI:
                    {
                        foreach (var phiUse in AsPhi().Uses)
                        {
                            if (def == phiUse.Node)
                            {
                                use = ref phiUse.NodeRef;
                            }
                        }
                        break;
                    }

                    case GT_CMPXCHG:
                    {
                        var cmpXchgNode = AsCmpXchg();

                        if (def == cmpXchgNode.Addr)
                        {
                            use = ref cmpXchgNode.AddrRef;
                        }
                        else if (def == cmpXchgNode.Data)
                        {
                            use = ref cmpXchgNode.DataRef;
                        }
                        else if (def == cmpXchgNode.Comparand)
                        {
                            use = ref cmpXchgNode.ComparandRef;
                        }
                        break;
                    }

                    case GT_SELECT:
#if TARGET_ARM64
                    case GT_SELECT_NEG:
                    case GT_SELECT_INV:
                    case GT_SELECT_INC:
#endif
                    {
                        var conditionalNode = AsConditional();

                        if (def == conditionalNode.Cond)
                        {
                            use = ref conditionalNode.CondRef;
                        }
                        else if (def == conditionalNode.Op1)
                        {
                            use = ref conditionalNode.Op1Ref;
                        }
                        else if (def == conditionalNode.Op2)
                        {
                            use = ref conditionalNode.Op2Ref;
                        }
                        break;
                    }

#if FEATURE_HW_INTRINSICS
                    case GT_HWINTRINSIC:
                    {
                        foreach (ref var multiOpUse in AsMultiOp().Operands)
                        {
                            if (def == multiOpUse)
                            {
                                use = ref multiOpUse;
                            }
                        }
                        break;
                    }
#endif

                    case GT_ARR_ELEM:
                    {
                        var arrElemNode = AsArrElem();

                        if (def == arrElemNode.ArrObj)
                        {
                            use = ref arrElemNode.ArrObjRef;
                        }
                        else
                        {
                            var arrInds = arrElemNode.ArrInds;

                            foreach (ref var arrInd in arrInds[..arrElemNode.ArrRank])
                            {
                                if (def == arrInd)
                                {
                                    use = ref arrInd;
                                }
                            }
                        }
                        break;
                    }

                    case GT_CALL:
                    {
                        var callNode = AsCall();

                        if (def == callNode.ControlExpr)
                        {
                            use = ref callNode.ControlExprRef;
                        }
                        else
                        {
                            foreach (var arg in callNode.Args.Args)
                            {
                                if (def == arg.EarlyNode)
                                {
                                    use = ref arg.EarlyNodeRef;
                                }
                                else if (def == arg.LateNode)
                                {
                                    use = ref arg.LateNodeRef;
                                }
                            }
                        }
                        break;
                    }

                    case GT_FIELD_LIST:
                    {
                        foreach (var fieldListUse in AsFieldList().Uses)
                        {
                            if (def == fieldListUse.Node)
                            {
                                use = ref fieldListUse.NodeRef;
                            }
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
                assert(oper.IsUnary || oper.IsBinary);
                var opNode = AsOp();

                if (def == opNode.Op1)
                {
                    use = ref opNode.Op1Ref;
                }
                else if (_oper.IsBinary && (def == opNode.Op2))
                {
                    use = ref opNode.Op2Ref;
                }
            }
        }
        return ref use;
    }
#nullable restore

    /// <summary>Whether node been assigned a register by LSRA</summary>
    /// <param name="compiler">Compiler instance. Required for multi-reg lcl var; ignored otherwise.</param>
    /// <returns>Returns true if the node was assigned a register.</returns>
    /// <remarks>
    ///   <para>In case of multi-reg call nodes, it is considered having a reg if regs are allocated for ALL its return values.</para>
    ///   <para>REVIEW: why is this ALL and the other cases are ANY? Explain.</para>
    ///   <para>In case of GT_COPY or GT_RELOAD of a multi-reg call, GT_COPY/GT_RELOAD is considered having a reg if it has a reg assigned to ANY of its positions.</para>
    ///   <para>In case of multi-reg local vars, it is considered having a reg if it has a reg assigned for ANY of its positions.</para>
    /// </remarks>
    public bool HasReg(Compiler? compiler)
    {
        var result = false;

        if (IsMultiRegCall)
        {
            var call = AsCall();
            var regCount = call.ReturnTypeDesc.ReturnRegCount;

            // A Multi-reg call node is said to have regs, if it has reg assigned to each of its result registers.
            for (byte i = 0; i < regCount; i++)
            {
                result = (call.GetRegNumByIdx(i) != REG_NA);

                if (!result)
                {
                    break;
                }
            }
        }
        else if (IsCopyOrReloadOfMultiRegCall)
        {
            var copyOrReload = AsCopyOrReload();
            var call = copyOrReload.Op1.AsCall();

            var regCount = call.ReturnTypeDesc.ReturnRegCount;

            // A Multi-reg copy or reload node is said to have regs, if it has valid regs in any of the positions.
            for (byte i = 0; i < regCount; i++)
            {
                result = (copyOrReload.GetRegNumByIdx(i) != REG_NA);

                if (result)
                {
                    break;
                }
            }
        }
        else if (IsMultiRegLclVar)
        {
            assert(compiler is not null);

            var lclNode = AsLclVar();
            var regCount = lclNode.GetFieldCount(compiler);

            // A Multi-reg local vars is said to have regs, if it has valid regs in any of the positions.
            for (byte i = 0; i < regCount; i++)
            {
                result = (lclNode.GetRegNumByIdx(i) != REG_NA);

                if (result)
                {
                    break;
                }
            }
        }
        else
        {
            result = (RegNum != REG_NA);
        }
        return result;
    }

    /// <summary>May this indirection-like node throw an NRE?</summary>
    /// <param name="compiler">the compiler instance</param>
    /// <returns>Whether this node's address may be null.</returns>
    public bool IndirMayFault(Compiler compiler)
    {
        assert(_oper.IsIndirOrArrMetaData);
        return ((_flags & GTF_IND_NONFAULTING) == 0) && compiler.fgAddrCouldBeNull(IndirOrArrMetaDataAddr);
    }

    public bool IsIntegralConst(nint value)
    {
        var result = false;

        if (_oper.IsIntegralConst)
        {
            result = AsIntConCommon().IsIntegralConst(value);
        }
        return result;
    }

    public bool IsNeverNegative(Compiler comp)
    {
        assert(varTypeIsIntegral(_type));

        if (_oper.IsIntegralConst)
        {
            return AsIntConCommon().IntegralValue >= 0;
        }

        if (_oper is GT_LCL_VAR)
        {
            if (AsLclVar().IsNeverNegative(comp))
            {
                // This is an early exit, it doesn't cover all cases
                return true;
            }
        }

        // TODO: Port GenTree.IsNeverNegative
        // if (IntegralRange.ForNode(const_cast<GenTree*>(this), comp).IsNonNegative())
        // {
        //     return true;
        // }
        // 
        // if ((comp.vnStore is not null) && comp.vnStore->IsVNNeverNegative(gtVNPair.GetConservative()))
        // {
        //     return true;
        // }

        return false;
    }

    public bool IsNeverNegativeOne(Compiler comp)
    {
        assert(varTypeIsIntegral(_type));
        var result = false;

        if (IsNeverNegative(comp))
        {
            result = true;
        }
        else if (_oper.IsIntegralConst)
        {
            result = !AsIntConCommon().IsIntegralConst(-1);
        }
        return result;
    }

    public bool IsNeverZero()
    {
        assert(varTypeIsIntegral(_type));
        var result = false;

        if (_oper.IsIntegralConst)
        {
            result = !AsIntConCommon().IsIntegralConst(0);
        }
        return result;
    }

    /// <summary>Check whether the operation may throw.</summary>
    /// <param name="comp">Compiler instance</param>
    /// <returns>True if the given operator may cause an exception</returns>
    public bool MayThrow(Compiler comp)
        => Exceptions(comp) != ExceptionSetFlags.None;

    public bool Precedes(GenTree other)
    {
        assert(other is not null);

        for (var node = _next; node is not null; node = node._next)
        {
            if (node == other)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Replace a given operand to this node with a new operand.</summary>
    /// <param name="useEdge">the use edge that points to the operand to be replaced.</param>
    /// <param name="replacement">the replacement node.</param>
    /// <remarks>If the current node is a call node, this will also update the call argument table if necessary.</remarks>
    public void ReplaceOperand(ref GenTree useEdge, GenTree replacement)
    {
        assert(Unsafe.AreSame(in GetUseRefOrNullRef(useEdge), in useEdge));
        useEdge = replacement;
    }

    /// <summary>Check whether the operation requires GTF_CALL flag regardless of the children's flags.</summary>
    /// <param name="comp"></param>
    /// <returns></returns>
    public bool RequiresCallFlag(Compiler comp) => _oper switch {
        GT_ASYNC_CONTINUATION => true,
        GT_GCPOLL => true,
        GT_INTRINSIC => comp.IsIntrinsicImplementedByUserCall(AsIntrinsic().IntrinsicName),
        GT_KEEPALIVE => true,
        GT_NONLOCAL_JMP => true,
#if FEATURE_HW_INTRINSICS
        GT_HWINTRINSIC => AsHWIntrinsic().RequiresCallFlag(),
#endif
        GT_CALL => true,
        GT_RETURN_SUSPEND => true,
        GT_PATCHPOINT => true,
        GT_PATCHPOINT_FORCED => true,
#if SWIFT_SUPPORT
        GT_SWIFT_ERROR => true,
#endif
#if FEATURE_FIXED_OUT_ARGS && !TARGET_64BIT
        // Variable shifts of a long end up being helper calls, so mark the tree as such in morph.
        // This is potentially too conservative, since they'll get treated as having side effects.
        // It is important to mark them as calls so if they are part of an argument list,
        // they will get sorted and processed properly (for example, it is important to handle
        // all nested calls before putting struct arguments in the argument registers). We
        // could mark the trees just before argument processing, but it would require a full
        // tree walk of the argument tree, so we just do it when morphing, instead, even though we'll
        // mark non-argument trees (that will still get converted to calls, anyway).
        case GT_LSH => TypeIs(TYP_LONG) && !gtGetOp2()->OperIs(GT_CNS_INT)
        case GT_RSH => TypeIs(TYP_LONG) && !gtGetOp2()->OperIs(GT_CNS_INT)
        case GT_RSZ => TypeIs(TYP_LONG) && !gtGetOp2()->OperIs(GT_CNS_INT)
#endif
        _ => false,
    };

    public void SetAllEffectsFlags(GenTree source)
        => SetAllEffectsFlags(source._flags & GTF_ALL_EFFECT);

    public void SetAllEffectsFlags(GenTree source1, GenTree source2)
        => SetAllEffectsFlags((source1._flags | source2._flags) & GTF_ALL_EFFECT);

    public void SetAllEffectsFlags(GenTreeFlags sourceFlags)
    {
        assert((sourceFlags & ~GTF_ALL_EFFECT) == 0);
        _flags = (_flags & ~GTF_ALL_EFFECT) | sourceFlags;
    }

    // Set the costs. They are always both set at the same time.
    // Don't use the "put" property: force calling this function, to make it more obvious in the few places that set the values.
    // Note that costs are only set in gtSetEvalOrder() and its callees.
    public void SetCosts(byte costEx, byte costSz)
    {
#if DEBUG
        _costsInitialized = true;
#endif

        _costEx = costEx;
        _costSz = costSz;
    }

    /// <summary>Set GTF_EXCEPT and GTF_IND_NONFAULTING flags as appropriate on an indirection or an array length node.</summary>
    /// <param name="compiler"></param>
    public void SetIndirExceptionFlags(Compiler compiler)
    {
        assert(_oper.IsIndirOrArrMetaData && (_oper.IsSimple || (_oper is GT_CMPXCHG)));

        if (IndirMayFault(compiler))
        {
            _flags |= GTF_EXCEPT;
        }
        else
        {
            var addr = IndirOrArrMetaDataAddr;

            _flags |= GTF_IND_NONFAULTING;
            _flags &= ~GTF_EXCEPT;
            _flags |= (addr._flags & GTF_EXCEPT);

            if (_oper.IsBinary)
            {
                _flags |= AsOp().Op2._flags & GTF_EXCEPT;
            }
            else if (Oper is GT_CMPXCHG)
            {
                var cmpXchg = AsCmpXchg();

                _flags |= cmpXchg.Data._flags & GTF_EXCEPT;
                _flags |= cmpXchg.Comparand._flags & GTF_EXCEPT;
            }
        }
    }

    /// <summary>mark a node as having been morphed</summary>
    /// <param name="compiler">compiler instance</param>
    /// <param name="doChilren">recursive mark child nodes</param>
    /// <remarks>
    ///   <para>Does nothing outside of global morph.</para>
    ///   <para>Useful for morph post-order expansions / optimizations.</para>
    ///   <para>Use care when invoking this on an assignment (or when doChildren is true, on trees containing assignments) as those usually will also require local assertion updates.</para>
    /// </remarks>
    [Conditional("DEBUG")]
    public void SetMorphed(Compiler compiler, bool doChilren = false)
    {
#if DEBUG
        if (!compiler.fgGlobalMorph)
        {
            return;
        }

        if (doChilren)
        {
            var node = this;

            var visitor = new SetMorphedVisitor();
            visitor.WalkTree(ref node, user: null);

            assert(node == this);
        }
        else if (!WasMorphed)
        {
            _debugFlags |= GTF_DEBUG_NODE_MORPHED;
            _morphCount++;
        }
#endif
    }

    // Visits each operand of this node. The operand must be either a lambda, function, or functor with the signature
    // `GenTree::VisitResult VisitorFunction(GenTree* operand)`. Here is a simple example:
    //
    //     unsigned operandCount = 0;
    //     node->VisitOperands([&](GenTree* operand) -> GenTree::VisitResult)
    //     {
    //         operandCount++;
    //         return GenTree::VisitResult::Continue;
    //     });
    //
    // This function is generally more efficient that the operand iterator and should be preferred over that API for
    // hot code, as it affords better opportunities for inlining and achieves shorter dynamic path lengths when
    // deciding how operands need to be accessed.
    //
    // Note that this function does not respect `GTF_REVERSE_OPS`. This is always safe in LIR, but may be dangerous
    // in HIR if for some reason you need to visit operands in the order in which they will execute.
    public VisitResult VisitOperands(GenTreeVisitorFunc visitor)
    {
        return VisitOperandUses((ref use) => visitor(use));
    }

    public VisitResult VisitOperandUses(GenTreeUseVisitorFunc visitor)
    {
        var result = VisitResult.Continue;
        var oper = _oper;

        if (oper.IsLeaf)
        {
            // Nothing to handle
        }
        else if (oper.IsBinary)
        {
            var op = AsOp();

            if (op.Op1 is not null)
            {
                result = visitor(ref op.Op1Ref);
            }
            else
            {
#if DEBUG
                assert(op.IsNullOp1Legal);
#endif
            }

            // We can have null op1 and non-null op2 for some nodes, such as GT_LEA

            if ((result is not VisitResult.Abort) && (op.Op2 is not null))
            {
                result = visitor(ref op.Op2Ref);
            }
            else
            {
#if DEBUG
                assert(op.IsNullOp2Legal);
#endif
            }
        }
        else if (oper.IsUnary)
        {
            var unOp = AsUnOp();

            if (unOp.Op1 is not null)
            {
                result = visitor(ref unOp.Op1Ref);
            }
            else
            {
#if DEBUG
                assert(unOp.IsNullOp1Legal);
#endif
            }
        }
        else
        {
            assert(oper.IsSpecial);

            switch (oper)
            {
                case GT_PHI:
                {
                    var phi = AsPhi();

                    foreach (var use in phi.Uses)
                    {
                        result = visitor(ref use.NodeRef);

                        if (result is VisitResult.Abort)
                        {
                            break;
                        }
                    }
                    break;
                }

                case GT_CMPXCHG:
                {
                    var cmpXchg = AsCmpXchg();
                    result = visitor(ref cmpXchg.AddrRef);

                    if (result is not VisitResult.Abort)
                    {
                        result = visitor(ref cmpXchg.DataRef);

                        if (result is not VisitResult.Abort)
                        {
                            result = visitor(ref cmpXchg.ComparandRef);
                        }
                    }
                    break;
                }

                case GT_SELECT:
                {
                    var conditional = AsConditional();
                    result = visitor(ref conditional.CondRef);

                    if (result is not VisitResult.Abort)
                    {
                        result = visitor(ref conditional.Op1Ref);

                        if (result is not VisitResult.Abort)
                        {
                            result = visitor(ref conditional.Op2Ref);
                        }
                    }
                    break;
                }

#if FEATURE_HW_INTRINSICS
                case GT_HWINTRINSIC:
                {
                    var hwintrinsic = AsHWIntrinsic();

                    foreach (ref var operand in hwintrinsic.Operands)
                    {
                        result = visitor(ref operand);

                        if (result is VisitResult.Abort)
                        {
                            break;
                        }
                    }
                    break;
                }
#endif

                case GT_ARR_ELEM:
                {
                    var arrElem = AsArrElem();
                    result = visitor(ref arrElem.ArrObjRef);

                    if (result is not VisitResult.Abort)
                    {
                        var arrInds = arrElem.ArrInds;

                        foreach (ref var arrInd in arrInds[..arrElem.ArrRank])
                        {
                            result = visitor(ref arrInd);

                            if (result is VisitResult.Abort)
                            {
                                break;
                            }
                        }
                    }
                    break;
                }

                case GT_CALL:
                {
                    var call = AsCall();
                    ref var args = ref call.Args;

                    foreach (var arg in args.EarlyArgs)
                    {
                        result = visitor(ref arg.EarlyNodeRef);

                        if (result is VisitResult.Abort)
                        {
                            break;
                        }
                    }

                    if (result != VisitResult.Abort)
                    {
                        foreach (var arg in args.LateArgs)
                        {
                            result = visitor(ref arg.LateNodeRef);

                            if (result is VisitResult.Abort)
                            {
                                break;
                            }
                        }

                        if ((result is not VisitResult.Abort) && (call.ControlExpr is not null))
                        {
                            result = visitor(ref call.ControlExprRef);
                        }
                    }
                    break;
                }

                case GT_FIELD_LIST:
                {
                    var fieldList = AsFieldList();

                    foreach (var use in fieldList.Uses)
                    {
                        result = visitor(ref use.NodeRef);

                        if (result is VisitResult.Abort)
                        {
                            break;
                        }
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
        return result;
    }
}
