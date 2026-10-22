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

    private GenTree? _currentBuildNode
    {
        get
        {
            return Configuration.DEBUG ? Compiler.DebugData.Get(this).CurrentBuildNode : null;
        }

        set
        {
            if (Configuration.DEBUG)
            {
                Compiler.DebugData.Get(this).CurrentBuildNode = value;
            }
        }
    }

    internal RefPosition newRefPositionRaw(LsraLocation nodeLocation, GenTree? treeNode, RefType refType)
    {
        var newRefPosition = new RefPosition(_currentBlockNumber, nodeLocation, treeNode, refType
            , _currentBuildNode
        );
        refPositions.Add(newRefPosition);

        if (Configuration.DEBUG)
        {
            // Only the first reference for a build node prints that node in the allocation table.
            _currentBuildNode = null;
            newRefPosition.rpNum = (uint)(refPositions.Count - 1);

            if (!_enregisterLocalVars)
            {
                assert(refType is not RefType.RefTypeParamDef and not RefType.RefTypeZeroInit and
                    not RefType.RefTypeDummyDef and not RefType.RefTypeExpUse);
            }
        }
        return newRefPosition;
    }

    public static SingleTypeRegSet calleeSaveRegs(RegisterType registerType)
    {
        assert((uint)registerType < (uint)TYP_COUNT);

        // typelist.h chooses callee-save sets by this generated register classification.
        return registerType.Register switch
        {
            VTR_INT when Configuration.TARGET_WASM => RBM_INT_CALLEE_SAVED.IntRegSet,
            VTR_INT => TargetRuntimeMetadata.IntegerCalleeSavedMask,
            VTR_FLOAT when Configuration.TARGET_WASM => RBM_FLT_CALLEE_SAVED.FltRegSet,
            VTR_FLOAT => TargetRuntimeMetadata.FloatingCalleeSavedMask,
            VTR_MASK when Configuration.FEATURE_MASKED_HW_INTRINSICS => TargetRuntimeMetadata.MaskCalleeSavedMask,
            _ => InvalidRegisterType(),
        };
        static SingleTypeRegSet InvalidRegisterType()
        {
            fatal(CORJIT_INTERNALERROR);
            return SRBM_NONE;
        }
    }
}
