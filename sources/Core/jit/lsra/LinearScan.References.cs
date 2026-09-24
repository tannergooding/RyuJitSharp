// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    internal readonly List<RefPosition> refPositions = [];
    internal Interval?[]? localVarIntervals;

    private uint _currentBlockNumber;

#if DEBUG
    private GenTree? _currentBuildNode;
#endif

    internal RefPosition newRefPositionRaw(LsraLocation nodeLocation, GenTree? treeNode, RefType refType)
    {
        var newRefPosition = new RefPosition(_currentBlockNumber, nodeLocation, treeNode, refType
#if DEBUG
            , _currentBuildNode
#endif
        );
        refPositions.Add(newRefPosition);

#if DEBUG
        // Only the first reference for a build node prints that node in the allocation table.
        _currentBuildNode = null;
        newRefPosition.rpNum = (uint)(refPositions.Count - 1);

        if (!_enregisterLocalVars)
        {
            assert(refType is not RefType.RefTypeParamDef and not RefType.RefTypeZeroInit and
                not RefType.RefTypeDummyDef and not RefType.RefTypeExpUse);
        }
#endif
        return newRefPosition;
    }

    public static SingleTypeRegSet calleeSaveRegs(RegisterType registerType)
    {
#if TARGET_AMD64
        // typelist.h chooses callee-save sets by this generated register classification.
        return registerType.Register switch
        {
            VTR_INT => SRBM_INT_CALLEE_SAVED,
            VTR_FLOAT => SRBM_FLT_CALLEE_SAVED,
#if FEATURE_MASKED_HW_INTRINSICS
            VTR_MASK => SRBM_MSK_CALLEE_SAVED,
#endif
            _ => InvalidRegisterType(),
        };
        static SingleTypeRegSet InvalidRegisterType()
        {
            fatal(CORJIT_INTERNALERROR);
            return SRBM_NONE;
        }
#else
        NYI("LinearScan.calleeSaveRegs outside AMD64");
        fatal(CORJIT_IMPLLIMITATION);
        return SRBM_NONE;
#endif
    }
}
