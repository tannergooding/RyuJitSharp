// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;

namespace RyuJitSharp;

public sealed partial class RefPosition
{
    public uint minRegCandidateCount
    {
        get
        {
            return GetDebugData().MinRegCandidateCount;
        }

        set
        {
            GetDebugData().MinRegCandidateCount = value;
        }
    }

    public uint rpNum
    {
        get
        {
            return GetDebugData().ReferencePositionNumber;
        }

        set
        {
            GetDebugData().ReferencePositionNumber = value;
        }
    }

    public GenTree? buildNode
    {
        get
        {
            return GetDebugData().BuildNode;
        }

        set
        {
            GetDebugData().BuildNode = value;
        }
    }

    private Compiler.DebugData.RefPositionDebugData GetDebugData()
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.RefPositionDebugDataIsOnlyAvailableInDebugBuilds);
            return default!;
        }

        return Compiler.DebugData.Get(this);
    }

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

    public bool needsConsecutive;
    public byte regCount;

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

    public bool skipSaveRestore;
    public bool liveVarUpperSave;

    public RefPosition(uint bbNum, LsraLocation nodeLocation, GenTree? treeNode, RefType refType, GenTree? buildNode = null)
    {
        this.bbNum = bbNum;
        this.nodeLocation = nodeLocation;
        this.treeNode = treeNode;
        this.refType = refType;
        registerAssignment = SRBM_NONE;
        killedRegisters = RBM_NONE;
        multiRegIdx = 0;

        if (Configuration.DEBUG)
        {
            var debugData = GetDebugData();
            debugData.MinRegCandidateCount = 1;
            debugData.ReferencePositionNumber = 0;
            debugData.BuildNode = buildNode;
        }
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
            case RefType.RefTypeUpperVectorSave:
            case RefType.RefTypeUpperVectorRestore:
            {
                return Configuration.FEATURE_PARTIAL_SIMD_CALLEE_SAVE ||
                    refType is not (RefType.RefTypeUpperVectorSave or RefType.RefTypeUpperVectorRestore);
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

    public bool isFirstRefPositionOfConsecutiveRegisters() =>
        Configuration.TARGET_ARM64 && needsConsecutive && (regCount != 0);

    public bool isLiveAtConsecutiveRegistersLoc(LsraLocation consecutiveLocation)
    {
        if (!Configuration.DEBUG)
        {
            JitExceptionHelpers.ThrowInvalidOperationException(FatalJitErrorCode.ConsecutiveRegisterDiagnosticsAreOnlyAvailableInDebugBuilds);
        }

        if (!Configuration.TARGET_ARM64)
        {
            return false;
        }

        if (needsConsecutive)
        {
            return true;
        }

        var atConsecutiveLocation = consecutiveLocation == nodeLocation;
        var treeNeedsConsecutive = (treeNode?.Oper is GT_HWINTRINSIC) &&
            HWIntrinsicInfo.NeedsConsecutiveRegisters(treeNode.AsHWIntrinsic().HWIntrinsicId);
        if (refType is RefType.RefTypeDef)
        {
            return treeNeedsConsecutive;
        }
        if (refType is RefType.RefTypeUse)
        {
            return isIntervalRef() && getInterval().isInternal
                ? treeNeedsConsecutive
                : atConsecutiveLocation;
        }
        return refType is RefType.RefTypeUpperVectorRestore && atConsecutiveLocation;
    }

    public bool IsExtraUpperVectorSave()
    {
        assert(refType is RefType.RefTypeUpperVectorSave);
        return (nextRefPosition is null) || (nextRefPosition.refType is not RefType.RefTypeUpperVectorRestore);
    }
}
