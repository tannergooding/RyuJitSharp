// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
#if DEBUG
using System.Globalization;
#endif
using static RyuJitSharp.BBKinds;
using static RyuJitSharp.genTreeOps;

namespace RyuJitSharp;

#if DEBUG
[DebuggerDisplay("[{info.compFullName} ({DebuggerMethodHash})]")]
public partial class Compiler
{
    private string DebuggerMethodHash => unchecked((uint)info.compMethodHash()).ToString("x8", CultureInfo.InvariantCulture);
}
#endif

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public partial class BasicBlock
{
    private string DebuggerDisplayText => Kind switch
    {
        BBJ_ALWAYS or BBJ_LEAVE or BBJ_EHCATCHRET or BBJ_CALLFINALLY or BBJ_CALLFINALLYRET or BBJ_EHFILTERRET
            when _anonymous1 is FlowEdge targetEdge =>
                $"BB{bbNum}->BB{targetEdge.DestinationBlock.bbNum}; {Kind}",
        BBJ_COND when _anonymous1 is FlowEdge trueEdge && bbFalseEdge is FlowEdge falseEdge =>
            $"BB{bbNum}-> (BB{trueEdge.DestinationBlock.bbNum}(T),BB{falseEdge.DestinationBlock.bbNum}(F)) ; {Kind}",
        BBJ_SWITCH when _anonymous1 is BBswtDesc switchTargets =>
            $"BB{bbNum}; {Kind}; {switchTargets.Cases.Length} cases",
        BBJ_EHFINALLYRET when bbEhfTargets is not null =>
            $"BB{bbNum}; {Kind}; {bbEhfTargets.Succs.Length} succs",
        _ => $"BB{bbNum}; {Kind}",
    };
}

#if DEBUG
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed partial class FlowEdge
{
    private string DebuggerDisplayText
    {
        get
        {
            var display = $"BB{_sourceBlock.bbNum}->BB{_destBlock.bbNum} ({_likelihood:g})";
            return (_dupCount is not 1) ? $"{display} (dup {_dupCount})" : display;
        }
    }
}
#endif

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public partial class GenTree
{
    private string DebuggerDisplayText
    {
        get
        {
            var valueNumbers = $"VNP=[L: {_vnPair.Liberal:x}, C: {_vnPair.Conservative:x}]";

#if DEBUG
            var prefix = $"{TreeId}: ";
#else
            var prefix = "";
#endif

            if (Oper is GT_CNS_STR)
            {
                return $"CNS_STR, {valueNumbers}";
            }

            if (Oper is GT_CNS_VEC)
            {
                return $"CNS_VEC, {valueNumbers}";
            }

            if (this is GenTreeIntConCommon integerConstant)
            {
                if (Oper is GT_CNS_LNG)
                {
                    return $"{prefix}[LngCon={integerConstant.IntegralValue}], {valueNumbers}";
                }

                return $"{prefix}[IntCon={integerConstant.IconValue}], {valueNumbers}";
            }

            if (this is GenTreeDblCon doubleConstant)
            {
                return $"{prefix}[DblCon={doubleConstant.DconVal:g}], {valueNumbers}";
            }

            if (this is GenTreeLclVarCommon local)
            {
                if (this is GenTreeLclFld localField)
                {
                    return $"{prefix}[{Oper.Name}, {Type.Name} V{local.LclNum}[+{localField.LclOffs}]], {valueNumbers}";
                }

                return $"{prefix}[{Oper.Name}, {Type.Name} V{local.LclNum}], {valueNumbers}";
            }

            if (this is GenTreeCast cast)
            {
                return $"{prefix}[{cast.CastType.Name} <- {cast.CastOp.Type.Name}], {valueNumbers}";
            }

            if (this is GenTreeHWIntrinsic intrinsic)
            {
                return $"{prefix}[{intrinsic.HWIntrinsicId}, {Type.Name}], {valueNumbers}";
            }

            return $"{prefix}[{Oper.Name}, {Type.Name}], {valueNumbers}";
        }
    }
}

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public partial struct LclVarDsc
{
#if DEBUG
    private readonly string DebuggerDisplayText => string.IsNullOrEmpty(lvReason)
        ? $"[V{lvSlotNum}: {Type.Name}]"
        : $"[V{lvSlotNum}: {Type.Name}-{lvReason}]";
#else
    private readonly string DebuggerDisplayText => $"[V{lvSlotNum}: {Type.Name}]";
#endif
}

#if DEBUG
[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed partial class Interval
{
    private string DebuggerDisplayText
    {
        get
        {
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
            if (isUpperVector)
            {
                return $"[U{relatedInterval?.varNum ?? varNum}, #{intervalIndex}, reg={physReg}]";
            }
#endif

            if (isLocalVar)
            {
                return $"[V{varNum}, #{intervalIndex}, reg={physReg}]";
            }

            if (isConstant)
            {
                return $"[C{intervalIndex}, reg={physReg}]";
            }

            return $"[I{intervalIndex}, reg={physReg}]";
        }
    }
}

[DebuggerDisplay("[#{rpNum} - {refType}]")]
public sealed partial class RefPosition
{
}
#endif

[DebuggerDisplay("{DebuggerDisplayText,nq}")]
public sealed partial class insGroup
{
    private string DebuggerDisplayText
    {
        get
        {
            var display = $"IG{GetDisplayId()}, size={igSize}, offset={igOffs}";
            return ((igFlags & InsGroupFlags.Extend) != 0) ? $"{display} [extend]" : display;
        }
    }
}
