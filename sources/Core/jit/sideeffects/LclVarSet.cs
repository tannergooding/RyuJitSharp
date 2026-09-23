// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

/// <summary>A local-number set optimized for a single element.</summary>
public struct LclVarSet
{
    private HashSet<int>? _locals;
    private int _lclNum;
    private bool _hasAnyLcl;

    // Preserve the pinned native single-element behavior; see B052.
    public readonly bool IsEmpty => !_hasAnyLcl || (_locals is null) || (_locals.Count == 0);

    public void Add(Compiler compiler, int lclNum)
    {
        if (!_hasAnyLcl)
        {
            _lclNum = lclNum;
            _hasAnyLcl = true;
        }
        else
        {
            _locals ??= [_lclNum];
            _ = _locals.Add(lclNum);
        }
    }

    public readonly bool Intersects(in LclVarSet other)
    {
        if (!_hasAnyLcl || !other._hasAnyLcl)
        {
            return false;
        }
        if (_locals is null)
        {
            return other._locals is null ? _lclNum == other._lclNum : other._locals.Contains(_lclNum);
        }
        if (other._locals is null)
        {
            return _locals.Contains(other._lclNum);
        }

        return _locals.Overlaps(other._locals);
    }

    public readonly bool Contains(int lclNum)
    {
        if (!_hasAnyLcl)
        {
            return false;
        }

        return _locals is null ? _lclNum == lclNum : _locals.Contains(lclNum);
    }

    public void Clear()
    {
        if (_locals is not null)
        {
            assert(_hasAnyLcl);
            _locals.Clear();
        }
        else if (_hasAnyLcl)
        {
            _hasAnyLcl = false;
        }
    }
}
