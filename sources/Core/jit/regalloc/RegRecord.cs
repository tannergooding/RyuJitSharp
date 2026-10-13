// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;

namespace RyuJitSharp;

#if DEBUG
[DebuggerDisplay("[reg={regNum}, type={registerType}]")]
#endif
public sealed partial class RegRecord : Referenceable
{
    public RegRecord()
        : base(TYP_INT)
    {
        regNum = REG_NA;
        regOrder = byte.MaxValue;
    }

    public Interval? assignedInterval;
    public Interval? previousInterval;
    public regNumber regNum;
    public bool isCalleeSave;
    public byte regOrder;

    public void init(regNumber reg)
    {
        if (Configuration.TARGET_ARM64 && (reg is REG_ARM64_ZR or REG_ARM64_SP))
        {
            regNum = reg;
            registerType = TYP_INT;
            isCalleeSave = false;
            return;
        }

        if (genIsValidIntReg(reg))
        {
            assert(registerType is TYP_INT);
        }
        else if (genIsValidFloatReg(reg))
        {
            registerType = TYP_FLOAT;
        }
        else if (Configuration.FEATURE_MASKED_HW_INTRINSICS)
        {
            assert(genIsValidMaskReg(reg) || (Configuration.TARGET_ARM64 && (reg == REG_FFR)));
            registerType = TYP_MASK;
        }

        regNum = reg;
        isCalleeSave = (LinearScan.calleeSaveRegs(registerType) & genSingleTypeRegMask(reg)) != SRBM_NONE;
    }

#if DEBUG
    public void dump()
    {
        tinyDump();
    }

    public void tinyDump()
    {
        jitprintf($"<Reg:{regNum.Name,-3}> ");
    }
#endif
}
