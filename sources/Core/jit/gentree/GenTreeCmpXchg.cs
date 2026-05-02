// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeCmpXchg : GenTreeIndir
{
    private GenTree _comparand;

    public GenTreeCmpXchg(var_types type, GenTree loc, GenTree val, GenTree comparand)
        : base(GT_CMPXCHG, type, loc, val)
    {
        _comparand = comparand;
        Flags |= comparand.Flags & GTF_ALL_EFFECT;
    }

    public GenTree Comparand
    {
        get
        {
            return _comparand;
        }

        set
        {
            _comparand = value;
        }
    }

#nullable disable
    public ref GenTree ComparandRef => ref _comparand;
#nullable restore
}
