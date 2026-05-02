// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.CompilerServices;
using static RyuJitSharp.GenTree.genRegTag;

namespace RyuJitSharp;

public abstract partial class GenTree
{
    private readonly genTreeOps _oper;

    private readonly var_types _type;

#if DEBUG
    // Only used to save gtOper when we destroy a node, to aid debugging.
    private genTreeOps _operSave;
#endif

    /// <summary>0 or the CSE index (negated if def)</summary>
    /// <remarks>valid only for CSE expressions</remarks>
    private sbyte _cseNum;

    /// <summary>Used for nodes that are in LIR. See LIR::Flags in lir.h for the various flags.</summary>
    private LIR.Flags _lirFlags;

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
    private GenTreeDebugFlags _debugFlags;

    private ushort _morphCount;
#endif

    private ValueNumPair _vnPair;

    private GenTree? _next;

    private GenTree? _prev;

#if DEBUG
    private int _treeId;

    /// <summary>liveness traversal order within the current statement</summary>
    private uint _seqNum;

    /// <summary>use-ordered traversal within the function</summary>
    private int _useNum;
#endif

    protected GenTree(genTreeOps oper, var_types type)
    {
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
            assert(Debugger.IsAttached || _costsInitialized);
            return _costEx;
        }
    }

    public byte CostSz
    {
        get
        {
            assert(Debugger.IsAttached ||_costsInitialized);
            return _costSz;
        }
    }

    public GenTree EffectiveVal
    {
        get
        {
            var result = this;

            while (result._oper is GT_COMMA)
            {
                var comma = result.AsOp();
                assert(comma.Op2 is not null);
                result = comma.Op2;
            }

            return result;
        }
    }

    public bool GeneratesAssertion => _assertionInfo.HasAssertion;

    public bool HasOverflowCheck
    {
        get
        {
            assert(Debugger.IsAttached || _oper.MayOverflow);
            return ((_flags & GTF_OVERFLOW) != 0);
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
            assert(Debugger.IsAttached || IsLirOp);
            var result = ((_flags & GTF_CONTAINED) != 0);

#if DEBUG
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
                var op1 = AsOp().Op1;
                assert(op1 is not null);
                result = op1.IsMultiRegCall;
            }

            return result;
        }
    }

    public bool IsIconHandle
    {
        get
        {
            assert(_oper.IsCnsIntOrI);
            return (Flags & GTF_ICON_HDL_MASK) != 0;
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
#if !TARGET_64BIT
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

                if (value != long.MinValue)
                {
                    value = long.Abs(value);
                }

                result = ulong.IsPow2((ulong)(value));
            }

            return result;
        }
    }

    /// <summary>Determines whether an integral constant is the power of 2.</summary>
    public bool IsIntegralConstPow2 => _oper.IsIntegralConst && long.IsPow2(AsIntConCommon().IntegralValue);

    /// <summary>Determines whether the unsigned value of an integral constant is the power of 2.</summary>
    public bool IsIntegralConstUnsignedPow2 => _oper.IsIntegralConst && ulong.IsPow2((ulong)(AsIntConCommon().IntegralValue));

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

#if FEATURE_MULTIREG_RET && !TARGET_64BIT
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

    public bool IsUnusedValue => (_lirFlags & LIR.Flags.UnusedValue) != 0;

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

    public GenTree? Previous
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
            // TODO-Cleanup: get rid of the NONE case, and fix everyplace that reads undefined values
            assert((_regTag is GT_REGTAG_NONE) || genIsValidReg(_regNum) || (_regNum is REG_NA));
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

    public GenTree SkipCopyOrReload
    {
        get
        {
            var result = this;

            if (_oper.IsCopyOrReload)
            {
                var op1 = AsOp().Op1;

                assert(op1 is not null);
                assert(!op1._oper.IsCopyOrReload);

                result = op1;
            }

            return result;
        }
    }

#if DEBUG
    public int TreeId => _treeId;
#endif

    public var_types Type => _type;

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
        // TODO: Port GenTree::Compare
        return false;
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

#if !TARGET_64BIT
    public GenTreeMultiRegOp AsMultiRegOp() => Unsafe.As<GenTreeMultiRegOp>(this);
#endif

    public void ClearAssertion() => _assertionInfo.Clear();

    [Conditional("DEBUG")]
    public void ClearMorphed()
    {
        _debugFlags &= ~GTF_DEBUG_NODE_MORPHED;
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
        assert(tree._costsInitialized);
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
#if DEBUG
        _costsInitialized = true;
#endif

        _costEx = tree._costEx;
        _costSz = tree._costSz;
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
                var op2 = AsOp().Op2;
                assert(op2 is not null);
                return op2.GetLayout(compiler);
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
                var inlineCandidate = AsRetExpr().InlineCandidate;
                assert(inlineCandidate is not null);
                structHnd = inlineCandidate.RetClsHnd;
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
        assert(def is not null);

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
                            ref var arrInds = ref arrElemNode.ArrInds;

                            for (byte i = 0; i < arrElemNode.ArrRank; i++)
                            {
                                if (def == arrInds[i])
                                {
                                    use = ref arrInds[i];
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

            var copyOrReloadOp1 = copyOrReload.Op1;
            assert(copyOrReloadOp1 is not null);

            var call = copyOrReloadOp1.AsCall();
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

    /// <summary>Replace a given operand to this node with a new operand.</summary>
    /// <param name="useEdge">the use edge that points to the operand to be replaced.</param>
    /// <param name="replacement">the replacement node.</param>
    /// <remarks>If the current node is a call node, this will also update the call argument table if necessary.</remarks>
    public void ReplaceOperand(ref GenTree useEdge, GenTree replacement)
    {
        assert(Unsafe.AreSame(in GetUseRefOrNullRef(useEdge), in useEdge));
        useEdge = replacement;
    }

    // Set the costs. They are always both set at the same time.
    // Don't use the "put" property: force calling this function, to make it more obvious in the few places that set the values.
    // Note that costs are only set in gtSetEvalOrder() and its callees.
    public void SetCosts(uint costEx, uint costSz)
    {
        assert(costEx != uint.MaxValue); // looks bogus
        assert(costSz != uint.MaxValue); // looks bogus

#if DEBUG
        _costsInitialized = true;
#endif

        _costEx = (costEx > MAX_COST) ? MAX_COST : (byte)(costEx);
        _costSz = (costSz > MAX_COST) ? MAX_COST : (byte)(costSz);
    }
}
