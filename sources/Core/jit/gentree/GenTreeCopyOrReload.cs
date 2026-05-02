// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed class GenTreeCopyOrReload : GenTreeUnOp
{
    // State required to support copy/reload of a multi-reg call node.
    // The first register is always given by GetRegNum().
    private _otherRegsInlineArray _otherRegs;

    public GenTreeCopyOrReload(genTreeOps oper, var_types type, GenTree op1)
        : base(oper, type, op1)
    {
        assert((type is not TYP_STRUCT) || op1.IsMultiRegNode);
        RegNum = REG_NA;
        ClearOtherRegs();
    }

    public byte RegCount
    {
        get
        {
            // We need to return the highest index for which we have a valid register.
            // Note that the gtOtherRegs array is off by one (the 0th register is GetRegNum()).
            // If there's no valid register in gtOtherRegs, GetRegNum() must be valid.
            // Note that for most nodes, the set of valid registers must be contiguous,
            // but for COPY or RELOAD there is only a valid register for the register positions
            // that must be copied or reloaded.

            for (byte i = MAX_MULTIREG_COUNT; i > 1; i--)
            {
                if (_otherRegs[i - 2] != REG_NA)
                {
                    return i;
                }
            }

            // We should never have a COPY or RELOAD with no valid registers.
            assert(RegNum != REG_NA);
            return 1;
        }
    }

    /// <summary>set gtOtherRegs to REG_NA.</summary>
    public void ClearOtherRegs()
    {
        for (byte i = 0; i < MAX_MULTIREG_COUNT - 1; i++)
        {
            _otherRegs[i] = REG_NA;
        }
    }

    /// <summary>copy multi-reg state from the given copy/reload node to this node.</summary>
    /// <param name="from">GenTree node from which to copy multi-reg state</param>
    public void CopyOtherRegs(GenTreeCopyOrReload from)
    {
        assert(Oper == from.Oper);

        // TODO-ARM: Implement this routine for Arm64 and Arm32
        // TODO-X86: Implement this routine for x86

#if UNIX_AMD64_ABI
        for (byte i = 0; i < MAX_MULTIREG_COUNT - 1; i++)
        {
            _otherRegs[i] = from._otherRegs[i];
        }
#endif
    }

    /// <summary>Get regNumber of i'th position.</summary>
    /// <param name="idx">register position.</param>
    /// <returns>Returns regNumber assigned to i'th position.</returns>
    public regNumber GetRegNumByIdx(byte idx)
    {
        assert(idx < MAX_MULTIREG_COUNT);

        if (idx == 0)
        {
            return RegNum;
        }

        return _otherRegs[idx - 1];
    }

    /// <summary>Set the regNumber for i'th position.</summary>
    /// <param name="reg">reg number</param>
    /// <param name="idx">register position.</param>
    public void SetRegNumByIdx(regNumber reg, byte idx)
    {
        assert(idx < MAX_MULTIREG_COUNT);

        if (idx == 0)
        {
            RegNum = reg;
        }
        else
        {
            _otherRegs[idx - 1] = reg;
        }
    }

    [InlineArray(MAX_MULTIREG_COUNT - 1)]
    private struct _otherRegsInlineArray
    {
        public regNumber e0;
    }
}
