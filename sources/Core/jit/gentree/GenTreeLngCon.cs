// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class GenTreeLngCon : GenTreeIntConCommon
{
    public GenTreeLngCon(long val)
        : base(GT_CNS_NATIVELONG, TYP_LONG)
    {
        LngValue = val;
    }

    public new bool FitsInI32 => Globals.FitsInI32(_value.Lcon);

    public int HiVal => unchecked((int)(_value.Lcon >>> 32));

    public long LconValue
    {
        get
        {
            return _value.Lcon;
        }

        set
        {
            _value.Lcon = value;
        }
    }

    public int LoVal => unchecked((int)(_value.Lcon));
}
