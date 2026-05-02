// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public abstract class GenTreeLclVarCommon : GenTreeUnOp
{
    // The local number. An index into the Compiler::lvaTable array.
    private uint _lclNum;

    // The SSA info.
    // private SsaNumInfo _ssaNum;

    protected GenTreeLclVarCommon(genTreeOps oper, var_types type, uint lclNum)
        : base(oper, type)
    {
        LclNum = lclNum;
    }

    protected GenTreeLclVarCommon(genTreeOps oper, var_types type, uint lclNum, GenTree data)
        : base(oper, type, data)
    {
        assert(oper.IsLocalStore);
        LclNum = lclNum;
    }

    public uint LclNum
    {
        get
        {
            return _lclNum;
        }

        set
        {
            _lclNum = value;
            // _ssaNum = new SsaNumInfo();
        }
    }

    // TODO: Port GenTreeLclVarCommon.GetSsaNum
    public uint SsaNum => 0;
}
