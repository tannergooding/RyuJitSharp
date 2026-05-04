// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

#if TARGET_64BIT
namespace RyuJitSharp;

public sealed class GenTreeMultiRegOp : GenTreeOp
{
    private readonly regNumber _otherReg;

    // GTF_SPILL or GTF_SPILLED flag on a multi-reg node indicates that one or
    // more of its result regs are in that state. The spill flag of each of the
    // return register is stored here. We only need 2 bits per returned register,
    // so this is treated as a 2-bit array. No architecture needs more than 8 bits.

    private MultiRegSpillFlags _spillFlags;

    public GenTreeMultiRegOp(genTreeOps oper, var_types type, GenTree op1, GenTree op2)
        : base(oper, type, op1, op2)
    {
        _otherReg = REG_NA;
    }

    public regNumber OtherReg => _otherReg;

    public byte RegCount => (byte)((Type is TYP_LONG) ? 2 : 1);

    /// <summary>clear GTF_* flags associated with gtOtherRegs</summary>
    public void ClearOtherRegFlags()
    {
        _spillFlags = 0;
    }

    /// <summary>get i'th register allocated to this struct argument.</summary>
    /// <param name="idx">index of the register</param>
    /// <returns>Return regNumber of i'th register of this register argument</returns>
    public regNumber GetRegNumByIdx(byte idx)
    {
        assert(idx < 2);

        if (idx is 0)
        {
            return RegNum;
        }
        return _otherReg;
    }

    public GenTreeFlags GetRegSpillFlagByIdx(byte idx) => GetMultiRegSpillFlagsByIdx(_spillFlags, idx);

    /// <summary> Get var_type of the register specified by index.</summary>
    /// <param name="index">Index of the register.</param>
    /// <returns>var_type of the register specified by its index.</returns>
    public var_types GetRegType(byte index)
    {
        // The type of register is usually the same as GenTree type, since GenTreeMultiRegOp usually defines a single reg.
        // The special case is when we have TYP_LONG, which may be a MUL_LONG, or a DOUBLE arg passed as LONG, in which case we need to separate them into int for each index.

        assert(index < 2);

        var type = Type;
        return (type is TYP_LONG) ? TYP_INT : type;
    }

    public void SetRegSpillFlagByIdx(GenTreeFlags flags, byte idx)
    {
#if FEATURE_MULTIREG_RET
        _spillFlags = SetMultiRegSpillFlagsByIdx(_spillFlags, flags, idx);
#endif
    }
}
#endif
