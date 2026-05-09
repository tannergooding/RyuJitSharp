// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

public abstract class GenTreeLclVarCommon : GenTreeUnOp
{
    // The local number. An index into the Compiler.lvaTable array.
    private int _lclNum;

    // The SSA info.
    // private SsaNumInfo _ssaNum;

    protected GenTreeLclVarCommon(genTreeOps oper, var_types type, int lclNum)
        : base(oper, type)
    {
        LclNum = lclNum;
    }

    protected GenTreeLclVarCommon(genTreeOps oper, var_types type, int lclNum, GenTree data)
        : base(oper, type, data)
    {
        assert(oper.IsLocalStore);
        LclNum = lclNum;
    }

    public GenTree Data
    {
        get
        {
            assert(Debugger.IsAttached || Oper.IsLocalStore);
            return Op1!;
        }
    }

    public int LclNum
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
    public int SsaNum => 0;
}
