// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class RefPosition
{
    public Referenceable? referent;
    public RefPosition? nextRefPosition;
    public GenTree? treeNode;
    public SingleTypeRegSet registerAssignment;
    public regMaskTP killedRegisters;
    public uint bbNum;
    public LsraLocation nodeLocation;
    public RefType refType;
    public bool regOptional;
    public byte multiRegIdx;

#if TARGET_ARM64
    public bool needsConsecutive;
    public byte regCount;
#endif

    public bool lastUse;
    public bool reload;
    public bool spillAfter;
    public bool singleDefSpill;
    public bool writeThru;
    public bool copyReg;
    public bool moveReg;
    public bool isPhysRegRef;
    public bool isFixedRegRef;
    public bool isLocalDefUse;
    public bool delayRegFree;
    public bool outOfOrder;

#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
    public bool skipSaveRestore;
    public bool liveVarUpperSave;
#endif

#if DEBUG
    public uint minRegCandidateCount;
    public uint rpNum;
    public GenTree? buildNode;
#endif

    public RefPosition(uint bbNum, LsraLocation nodeLocation, GenTree? treeNode, RefType refType, GenTree? buildNode = null)
    {
        this.bbNum = bbNum;
        this.nodeLocation = nodeLocation;
        this.treeNode = treeNode;
        this.refType = refType;
        registerAssignment = SRBM_NONE;
        killedRegisters = RBM_NONE;
        multiRegIdx = 0;

#if DEBUG
        minRegCandidateCount = 1;
        rpNum = 0;
        this.buildNode = buildNode;
#endif
    }

    public Interval getInterval()
    {
        assert(!isPhysRegRef);
        assert(referent is Interval);
        return (Interval)referent;
    }

    public void setInterval(Interval? interval)
    {
        referent = interval;
        isPhysRegRef = false;
    }

    public RegRecord getReg()
    {
        assert(isPhysRegRef);
        assert(referent is RegRecord);
        return (RegRecord)referent;
    }

    public void setReg(RegRecord register)
    {
        referent = register;
        isPhysRegRef = true;
        registerAssignment = genSingleTypeRegMask(register.regNum);
    }

    public regNumber assignedReg()
    {
        if (registerAssignment == SRBM_NONE)
        {
            return REG_NA;
        }

        return genRegNumFromMask(registerAssignment, getRegisterType());
    }

    public RegisterType getRegisterType()
    {
        assert(referent is not null);
        return referent.registerType;
    }

    public regMaskTP getKilledRegisters()
    {
        assert(refType is RefType.RefTypeKill);
        return killedRegisters;
    }

    public bool IsActualRef()
    {
        switch (refType)
        {
            case RefType.RefTypeDef:
            case RefType.RefTypeUse:
#if FEATURE_PARTIAL_SIMD_CALLEE_SAVE
            case RefType.RefTypeUpperVectorSave:
            case RefType.RefTypeUpperVectorRestore:
#endif
            {
                return true;
            }

            case RefType.RefTypeExpUse:
            case RefType.RefTypeParamDef:
            case RefType.RefTypeDummyDef:
            case RefType.RefTypeZeroInit:
            {
                assert(RegOptional());
                return false;
            }

            default:
            {
                return false;
            }
        }
    }

    public bool IsPhysRegRef() => refType is RefType.RefTypeFixedReg;

    public void setRegOptional(bool value) => regOptional = value;

    public bool RegOptional() => regOptional && !copyReg && !moveReg;

    public void setMultiRegIdx(uint index)
    {
        multiRegIdx = (byte)(index & 0x3);
        assert(multiRegIdx == index);
    }

    public uint getMultiRegIdx() => multiRegIdx;

    public LsraLocation getRefEndLocation() => delayRegFree ? unchecked(nodeLocation + 1) : nodeLocation;

    public RefPosition getRangeEndRef()
    {
        if (lastUse || (nextRefPosition is null) || spillAfter)
        {
            return this;
        }

        return nextRefPosition;
    }

    public LsraLocation getRangeEndLocation() => getRangeEndRef().getRefEndLocation();

    public bool isIntervalRef() => !IsPhysRegRef() && (referent is not null);

    public bool isFixedRefOfRegMask(SingleTypeRegSet registerMask)
    {
        assert(genMaxOneBit(registerMask));
        return registerAssignment == registerMask;
    }

    public bool isFixedRefOfReg(regNumber reg) => isFixedRefOfRegMask(genSingleTypeRegMask(reg));

#if TARGET_ARM64
    public bool isFirstRefPositionOfConsecutiveRegisters() => needsConsecutive && (regCount != 0);
#endif

    public bool IsExtraUpperVectorSave()
    {
        assert(refType is RefType.RefTypeUpperVectorSave);
        return (nextRefPosition is null) || (nextRefPosition.refType is not RefType.RefTypeUpperVectorRestore);
    }
}
