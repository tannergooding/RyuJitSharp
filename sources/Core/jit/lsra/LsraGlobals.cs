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

    public const int RegisterTypeCount = 2;

    public static bool copyOrMoveRegInUse(RefPosition reference, LsraLocation location)
    {
        if (!reference.copyReg && !reference.moveReg)
        {
            return false;
        }

        if (reference.getRefEndLocation() >= location)
        {
            return true;
        }

        var interval = reference.getInterval();
        var nextReference = interval.getNextRefPosition();
        if ((nextReference is not null) && ReferenceEquals(nextReference.treeNode, reference.treeNode) &&
            (nextReference.getRefEndLocation() >= location))
        {
            return true;
        }

        return false;
    }

    public static void lsraAssignRegToTree(GenTree tree, regNumber reg, uint regIdx)
    {
        if (regIdx == 0)
        {
            tree.RegNum = reg;
        }
#if !TARGET_64BIT
        else if (tree.Oper.IsMultiRegOp)
        {
            assert(regIdx == 1);
            tree.AsMultiRegOp().OtherReg = reg;
        }
#endif
#if FEATURE_MULTIREG_RET
        else if (tree.Oper is GT_COPY)
        {
            assert(regIdx == 1);
            tree.AsCopyOrReload().SetRegNumByIdx(reg, 1);
        }
#endif
#if FEATURE_HW_INTRINSICS
        else if (tree.Oper is GT_HWINTRINSIC)
        {
            tree.AsHWIntrinsic().SetRegNumByIdx(reg, checked((byte)regIdx));
        }
#endif
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
#if TARGET_X86
        var bits = unchecked((uint)registers);
#else
        var bits = unchecked((ulong)registers);
#endif
        return (bits & unchecked(bits - 1)) == 0;
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

#if TARGET_X86
        var reg = (regNumber)BitOperations.Log2(unchecked((uint)registers));
#else
        var reg = (regNumber)BitOperations.Log2(unchecked((ulong)registers));
#endif
        assert(genSingleTypeRegMask(reg) == registers);

#if HAS_MORE_THAN_64_REGISTERS
        if (varTypeIsMask(registerType))
        {
            reg = (regNumber)(64 + (int)reg);
        }
#endif
        return reg;
    }

    public static RegisterType regType(var_types type)
    {
        if (varTypeUsesIntReg(type))
        {
            return TYP_INT;
        }

#if (TARGET_XARCH || TARGET_ARM64) && FEATURE_SIMD
        if (varTypeUsesMaskReg(type))
        {
            return TYP_MASK;
        }
#endif

        assert(varTypeUsesFloatReg(type));
        return TYP_FLOAT;
    }

    public static bool useFloatReg(var_types type) => regType(type) == TYP_FLOAT;

    public static bool RefTypeIsUse(RefType refType) =>
        ((byte)refType & (byte)RefType.RefTypeUse) == (byte)RefType.RefTypeUse;

    public static bool RefTypeIsDef(RefType refType) =>
        ((byte)refType & (byte)RefType.RefTypeDef) == (byte)RefType.RefTypeDef;
}
