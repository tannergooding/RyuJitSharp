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
    private SsaNumInfo _ssaNum;

    protected GenTreeLclVarCommon(genTreeOps oper, var_types type, int lclNum)
        : base(oper, type, op1: null)
    {
        LclNum = lclNum;
    }

    protected GenTreeLclVarCommon(genTreeOps oper, var_types type, int lclNum, GenTree data)
        : base(oper, type, data)
    {
        assert(oper.IsLocalStore);
        LclNum = lclNum;
    }

    protected GenTreeLclVarCommon(genTreeOps oper, var_types type, int lclNum, GenTree? data, GenTree source, NodeThreading threading)
        : base(oper, type, data, source, threading)
    {
        LclNum = lclNum;
    }

    public new GenTree Data
    {
        get
        {
            assert(Debugger.IsAttached || Oper.IsLocalStore);
            return Op1!;
        }
    }

    public new ref GenTree DataRef
    {
        get
        {
            assert(Debugger.IsAttached || Oper.IsLocalStore);
            return ref Op1Ref;
        }
    }

    public bool HasCompositeSsaName => _ssaNum.IsComposite;

    public bool HasSsaIdentity => !_ssaNum.IsInvalid;

    public bool HasSsaName => SsaNum != SsaConfig.RESERVED_SSA_NUM;

    public int LclNum
    {
        get
        {
            return _lclNum;
        }

        set
        {
            _lclNum = value;
            _ssaNum = new SsaNumInfo();
        }
    }

    /// <summary>if `this` is a field or a field address it returns offset of the field inside the struct, for not a field it returns 0.</summary>
    public ushort LclOffs => Oper.IsLocalField ? AsLclFld().LclOffs : (ushort)(0);

    public int SsaNum
    {
        get
        {
            return _ssaNum.IsSimple ? _ssaNum.Num : SsaConfig.RESERVED_SSA_NUM;
        }

        set
        {
            _ssaNum = SsaNumInfo.Simple(value);
        }
    }

    /// <summary>Get the struct layout for a local node of struct type.</summary>
    /// <param name="compiler">the compiler instance</param>
    /// <returns>If "this" is a local field node, the layout stored in the node, otherwise the layout of local itself.</returns>
    public new ClassLayout? GetLayout(Compiler compiler)
    {
        assert(varTypeIsStruct(Type));

        if (Oper is GT_LCL_VAR or GT_STORE_LCL_VAR)
        {
            return compiler.lvaGetDesc(LclNum).Layout;
        }

        assert(Oper is GT_LCL_FLD or GT_STORE_LCL_FLD);
        return AsLclFld().Layout;
    }

    public int GetSsaNum(Compiler compiler, int index)
    {
        return _ssaNum.IsComposite ? _ssaNum.GetNum(compiler, index) : SsaConfig.RESERVED_SSA_NUM;
    }

    public void SetSsaNum(Compiler compiler, int index, int ssaNum)
    {
        _ssaNum = SsaNumInfo.Composite(_ssaNum, compiler, _lclNum, index, ssaNum);
    }
}
