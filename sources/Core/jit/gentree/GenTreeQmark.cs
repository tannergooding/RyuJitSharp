// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeQmark : GenTreeOp
{
    private byte _thenLikelihood;

    public GenTreeQmark(var_types type, GenTree cond, GenTreeColon colon, byte thenLikelihood = 50)
        : base(GT_QMARK, type, cond, colon)
    {
        _thenLikelihood = thenLikelihood;

        // These must follow a specific form.
        assert(cond.Type is TYP_INT);
        assert(colon.Oper is GT_COLON);
    }

    public GenTree Cond => Op1;

    public GenTreeColon Colon => Op2.AsColon();

    public GenTree ElseNode => Colon.ElseNode;

    public byte ElseNodeLikelihood
    {
        get
        {
            assert(_thenLikelihood <= 100);
            return (byte)(100 - _thenLikelihood);
        }
    }

    public bool IsEarlyExpandableQmark
    {
        get
        {
            return (Flags & GTF_QMARK_EARLY_EXPAND) != 0;
        }

        set
        {
            Flags = (Flags & ~GTF_QMARK_EARLY_EXPAND) | (value ? GTF_QMARK_EARLY_EXPAND : 0);
        }
    }

    public GenTree ThenNode => Colon.ThenNode;

    public byte ThenNodeLikelihood
    {
        get
        {
            assert(_thenLikelihood <= 100);
            return _thenLikelihood;
        }

        set
        {
            assert(value <= 100);
            _thenLikelihood = value;
        }
    }
}
