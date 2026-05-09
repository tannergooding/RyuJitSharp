// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// This takes:
// - a length value
// - an index value, and
// - the label to jump to if the index is out of range.
// - the "kind" of the throw block to branch to on failure
// It generates no result.
public sealed class GenTreeBoundsChk : GenTreeOp
{
    private readonly SpecialCodeKind _throwKind;

    // Store some information about the array element type that was in the GT_INDEX_ADDR node before morphing.
    // Note that this information is also stored in the ARR_ADDR node of the morphed tree, but that can be hard
    // to find.
    private readonly var_types _inxType; 

    public GenTreeBoundsChk(GenTree index, GenTree length, SpecialCodeKind kind)
        : base(GT_BOUNDS_CHECK, TYP_VOID, index, length)
    {
        _throwKind = kind;
        _inxType = TYP_UNKNOWN;
        Flags |= GTF_EXCEPT;
    }

    /// <summary>If this check is against GT_ARR_LENGTH or GT_MDARR_LENGTH, returns array reference, else null.</summary>
    public GenTree? Array
    {
        get
        {
            var arrayLength = ArrayLength;

            if (arrayLength.Oper.IsArrLength)
            {
                return arrayLength.AsArrCommon().ArrRef;
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>An expression for the length.</summary>
    public GenTree ArrayLength => Op2;

    /// <summary>The index expression.</summary>
    public GenTree Index => Op1;

    /// <summary>The array element type.</summary>
    public var_types InxType => _inxType;

    /// <summary>Kind of throw block to branch to on failure</summary>
    public SpecialCodeKind ThrowKind => _throwKind;
}
