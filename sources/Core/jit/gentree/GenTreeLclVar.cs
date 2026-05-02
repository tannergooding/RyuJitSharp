// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed class GenTreeLclVar : GenTreeLclVarCommon
{
    private _otherRegInlineArray _otherReg;

    private MultiRegSpillFlags _spillFlags;

#if DEBUG
    private IL_OFFSET _lclIlOffs = BAD_IL_OFFSET;
#endif

    public GenTreeLclVar(genTreeOps oper, var_types type, uint lclNum)
        : base(oper, type, lclNum)
    {
        assert(oper.IsScalarLocal);
    }

    public GenTreeLclVar(var_types type, uint lclNum, GenTree data)
        : base(GT_STORE_LCL_VAR, type, lclNum, data)
    {
    }

    public bool IsMultiReg => (Flags & GTF_VAR_MULTIREG) != 0;

#if DEBUG
    /// <summary>instr offset of ref (only for JIT dumps)</summary>
    public IL_OFFSET LclIlOffs
    {
        get
        {
            return _lclIlOffs;
        }

        set
        {
            _lclIlOffs = value;
        }
    }
#endif

    public void ClearMultiReg()
    {
        Flags &= ~GTF_VAR_MULTIREG;
    }

    /// <summary>clear GTF_* flags associated with gtOtherRegs</summary>
    public void ClearOtherRegFlags()
    {
        _spillFlags = 0;
    }

    /// <summary>copy GTF_* flags associated with gtOtherRegs from the given LclVar node.</summary>
    /// <param name="from">GenTreeLclVar node from which to copy</param>
    public void CopyOtherRegFlags(GenTreeLclVar from)
    {
        _spillFlags = from._spillFlags;
    }

    /// <summary>Return the register count for a multi-reg lclVar.</summary>
    /// <param name="compiler">the current Compiler instance.</param>
    /// <returns>Returns the number of registers defined by this node.</returns>
    /// <remarks>This must be a multireg lclVar.</remarks>
    public uint GetFieldCount(Compiler compiler)
    {
        assert(IsMultiReg);
        ref var varDsc = ref compiler.lvaGetDesc(LclNum);
        return varDsc.lvFieldCnt;
    }

    /// <summary>Get a specific register's type, based on regIndex, that is produced by this multi-reg node.</summary>
    /// <param name="compiler">the current Compiler instance.</param>
    /// <param name="idx">which register type to return.</param>
    /// <returns>The register type assigned to this index for this node.</returns>
    /// <remarks>This must be a multireg lclVar and 'regIndex' must be a valid index for this node.</remarks>
    public var_types GetFieldTypeByIndex(Compiler compiler, uint idx)
    {
        assert(IsMultiReg);

        ref var varDsc = ref compiler.lvaGetDesc(LclNum);
        ref var fieldVarDsc = ref compiler.lvaGetDesc(varDsc.lvFieldLclStart + idx);

        // Don't expect struct fields.
        assert(fieldVarDsc.lvType is not TYP_STRUCT);

        return fieldVarDsc.lvType;
    }

    public regNumber GetRegNumByIdx(byte regIndex)
    {
        assert(regIndex < MAX_MULTIREG_COUNT);
        return (regIndex == 0) ? RegNum : _otherReg[regIndex - 1];
    }

    public GenTreeFlags GetRegSpillFlagByIdx(byte idx) => GetMultiRegSpillFlagsByIdx(_spillFlags, idx);

    /// <summary>Gets true if the lcl var is never negative; otherwise false.</summary>
    /// <param name="compiler">the compiler instance</param>
    /// <returns>true if the lcl var is never negative; otherwise false.</returns>
    public bool IsNeverNegative(Compiler compiler)
    {
        assert(Oper is GT_LCL_VAR);
        return compiler.lvaGetDesc(LclNum).IsNeverNegative;
    }

#if DEBUG
    public void ResetLclILOffs()
    {
        _lclIlOffs = BAD_IL_OFFSET;
    }
#endif

    public void SetMultiReg()
    {
        Flags |= GTF_VAR_MULTIREG;
        ClearOtherRegFlags();
    }

    public void SetRegNumByIdx(regNumber reg, byte regIndex)
    {
        assert(regIndex < MAX_MULTIREG_COUNT);

        if (regIndex == 0)
        {
            RegNum = reg;
        }
        else
        {
            _otherReg[regIndex - 1] = reg;
        }
    }

    public void SetRegSpillFlagByIdx(GenTreeFlags flags, byte idx)
    {
        _spillFlags = SetMultiRegSpillFlagsByIdx(_spillFlags, flags, idx);
    }

    [InlineArray(MAX_MULTIREG_COUNT - 1)]
    private struct _otherRegInlineArray
    {
        public regNumber e0;
    }
}
