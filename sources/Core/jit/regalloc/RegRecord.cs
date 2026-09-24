// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class RegRecord : Referenceable
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
#if TARGET_ARM64
        if (reg is REG_ZR or REG_SP)
        {
            regNum = reg;
            registerType = TYP_INT;
            isCalleeSave = false;
            return;
        }
#endif

        if (genIsValidIntReg(reg))
        {
            assert(registerType is TYP_INT);
        }
        else if (genIsValidFloatReg(reg))
        {
            registerType = TYP_FLOAT;
        }
#if FEATURE_MASKED_HW_INTRINSICS
        else
        {
#if TARGET_ARM64
            assert(genIsValidMaskReg(reg) || (reg is REG_FFR));
#else
            assert(genIsValidMaskReg(reg));
#endif
            registerType = TYP_MASK;
        }
#endif

        regNum = reg;
        isCalleeSave = (LinearScan.calleeSaveRegs(registerType) & genSingleTypeRegMask(reg)) != SRBM_NONE;
    }

#if DEBUG
    public void tinyDump()
    {
        jitprintf($"<Reg:{regNum.Name,-3}> ");
    }
#endif
}
