// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class GenTree
{
    public const ValueNumberUpdate CLEAR_VN = ValueNumberUpdate.CLEAR_VN;
    public const ValueNumberUpdate PRESERVE_VN = ValueNumberUpdate.PRESERVE_VN;

    public void SetVNsFromNode(GenTree tree)
    {
        _vnPair = tree._vnPair;
    }

    public ValueNum GetVN(ValueNumKind vnk)
    {
        return _vnPair[vnk];
    }

    public void SetVN(ValueNumKind vnk, ValueNum vn)
    {
        _vnPair[vnk] = vn;
    }

    public void SetVNs(ValueNumPair vnp)
    {
        _vnPair = vnp;
    }

    public void ClearVN()
    {
        _vnPair = new ValueNumPair();
    }

    public enum ValueNumberUpdate
    {
        /// <summary>Clear value number</summary>
        CLEAR_VN,

        /// <summary>Preserve value number</summary>
        PRESERVE_VN,
    }
}
