// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Numerics;

namespace RyuJitSharp;

public static class LsraGlobals
{
    public const LsraLocation MinLocation = 0;
    public const LsraLocation MaxLocation = uint.MaxValue;

    // A node can require at most eight internal registers in addition to its uses and defs.
    public const int MaxInternalRegisters = 8;

    public static void lsraAssignRegToTree(GenTree tree, regNumber reg, uint regIdx)
    {
        if (regIdx == 0)
        {
            tree.RegNum = reg;
        }
        else if (tree.Oper.IsMultiRegOp)
        {
            assert(regIdx == 1);
            tree.AsMultiRegOp().OtherReg = reg;
        }
        else if (Configuration.FEATURE_MULTIREG_RET && (tree.Oper is GT_COPY))
        {
            assert(regIdx == 1);
            tree.AsCopyOrReload().SetRegNumByIdx(reg, 1);
        }
        else if (Configuration.FEATURE_HW_INTRINSICS && (tree.Oper is GT_HWINTRINSIC))
        {
            tree.AsHWIntrinsic().SetRegNumByIdx(reg, checked((byte)regIdx));
        }
        else if (tree.Oper is GT_LCL_VAR or GT_STORE_LCL_VAR)
        {
            tree.AsLclVar().SetRegNumByIdx(reg, checked((byte)regIdx));
        }
        else
        {
            assert(tree.IsMultiRegCall);
            tree.AsCall().SetRegNumByIdx(reg, checked((byte)regIdx));
        }
    }

    public static SingleTypeRegSet genAndNot(SingleTypeRegSet registers, SingleTypeRegSet excluded) => registers & ~excluded;

    public static bool genMaxOneBit(SingleTypeRegSet registers)
    {
        if (Configuration.TARGET_X86)
        {
            var bits32 = unchecked((uint)registers);
            return (bits32 & unchecked(bits32 - 1)) == 0;
        }

        var bits64 = unchecked((ulong)registers);
        return (bits64 & unchecked(bits64 - 1)) == 0;
    }

    public static bool genExactlyOneBit(SingleTypeRegSet registers)
    {
        return (registers != SRBM_NONE) && genMaxOneBit(registers);
    }

    public static SingleTypeRegSet genSingleTypeRegMask(regNumber reg)
    {
        return reg.SingleTypeMask;
    }

    public static regNumber genRegNumFromMask(SingleTypeRegSet registers, RegisterType registerType)
    {
        assert(genExactlyOneBit(registers));

        regNumber reg;
        if (Configuration.TARGET_X86)
        {
            reg = (regNumber)BitOperations.Log2(unchecked((uint)registers));
        }
        else
        {
            reg = (regNumber)BitOperations.Log2(unchecked((ulong)registers));
        }

        assert(genSingleTypeRegMask(reg) == registers);

        if (Configuration.HAS_MORE_THAN_64_REGISTERS && varTypeIsMask(registerType))
        {
            reg = (regNumber)(64 + (int)reg);
        }

        return reg;
    }

    public static RegisterType regType(var_types type)
    {
        if (varTypeUsesIntReg(type))
        {
            return TYP_INT;
        }

        if ((Configuration.TARGET_XARCH || Configuration.TARGET_ARM64) &&
            Configuration.FEATURE_SIMD && varTypeUsesMaskReg(type))
        {
            return TYP_MASK;
        }

        assert(varTypeUsesFloatReg(type));
        return TYP_FLOAT;
    }

    public static bool RefTypeIsUse(RefType refType) =>
        ((byte)refType & (byte)RefType.RefTypeUse) == (byte)RefType.RefTypeUse;

    public static bool RefTypeIsDef(RefType refType) =>
        ((byte)refType & (byte)RefType.RefTypeDef) == (byte)RefType.RefTypeDef;
}
