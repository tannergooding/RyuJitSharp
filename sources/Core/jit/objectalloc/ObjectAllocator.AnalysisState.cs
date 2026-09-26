// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class ObjectAllocator
{
    private bool _analysisDone;
    private int _bvCount;
    private BitVecTraits _bitVecTraits;
    private int _unknownSourceIndex = BAD_VAR_NUM;
    private nint[] _escapingPointers = BitVecOps.UninitVal();
    private nint[] _definitelyUsedPointers = BitVecOps.UninitVal();
    private nint[] _possiblyStackPointingPointers = BitVecOps.UninitVal();
    private nint[] _definitelyStackPointingPointers = BitVecOps.UninitVal();
    private nint[][]? _connGraphAdjacencyMatrix;
    private int _nextLocalIndex;
    private int _firstPseudoIndex = BAD_VAR_NUM;
    private int _numPseudos;
    private int _maxPseudos;
    private int _regionsToClone;
    private readonly int _initialMaxBlockID;
    private readonly Dictionary<int, CloneInfo> _cloneMap = [];
    private readonly Dictionary<GenTree, StoreInfo> _storeAddressToIndexMap = [];
    private readonly Dictionary<int, int> _enumeratorLocalToPseudoIndexMap = [];
    private bool _trackFields;

    private sealed class StoreInfo(int index, bool connected = false)
    {
        public int Index = index;
        public bool Connected = connected;
    }

    private bool CanHavePseudos() => _maxPseudos > 0;

#if DEBUG
    private static int TreeIdForDump(GenTree tree) => tree.TreeId;
#else
    private static int TreeIdForDump(GenTree tree) => 0;
#endif

    private bool IsTrackedType(var_types type)
    {
        var isTrackableScalar = type is TYP_REF or TYP_BYREF;
        var isTrackableStruct = (type is TYP_STRUCT) && _trackFields;

        return isTrackableScalar || isTrackableStruct;
    }

    private bool IsTrackedLocal(int lclNum)
    {
        var compiler = CompilerInstance;
        assert(lclNum < compiler.lvaCount);
        return compiler.lvaGetDesc(lclNum).lvTracked;
    }

    private int LocalToIndex(int lclNum)
    {
        assert(IsTrackedLocal(lclNum));
        var result = CompilerInstance.lvaGetDesc(lclNum)._varIndex;
        assert(result < _bvCount);
        return result;
    }

    private int IndexToLocal(int bvIndex)
    {
        assert(bvIndex < _bvCount);
        var result = BAD_VAR_NUM;

        if (bvIndex < _firstPseudoIndex)
        {
            var trackedToVarNum = CompilerInstance.lvaTrackedToVarNum;
            assert(trackedToVarNum is not null);
            result = trackedToVarNum[bvIndex];
            assert(IsTrackedLocal(result));
        }

        return result;
    }

#if DEBUG
    private void DumpIndex(int bvIndex)
    {
        if (bvIndex < _firstPseudoIndex)
        {
            jitprintf($" V{IndexToLocal(bvIndex):D2}");
            return;
        }

        if (bvIndex < _unknownSourceIndex)
        {
            jitprintf($" P{bvIndex:D2}");
            return;
        }

        if (bvIndex == _unknownSourceIndex)
        {
            jitprintf($" U{bvIndex:D2}");
            return;
        }

        jitprintf($" ?{bvIndex:D2}");
    }
#endif

    private bool CanIndexEscape(int index)
    {
        return BitVecOps.IsMember(_bitVecTraits, _escapingPointers, index);
    }

    private bool CanLclVarEscape(int lclNum)
    {
        if (!IsTrackedLocal(lclNum))
        {
            return true;
        }

        return CanIndexEscape(LocalToIndex(lclNum));
    }

    private bool IsIndexUsed(int index)
    {
        return BitVecOps.IsMember(_bitVecTraits, _definitelyUsedPointers, index);
    }

    private bool IsLclVarUsed(int lclNum)
    {
        if (!IsTrackedLocal(lclNum))
        {
            return true;
        }

        return IsIndexUsed(LocalToIndex(lclNum));
    }

    private bool MayIndexPointToStack(int index)
    {
        assert(_analysisDone);
        return BitVecOps.IsMember(_bitVecTraits, _possiblyStackPointingPointers, index);
    }

    private bool MayLclVarPointToStack(int lclNum)
    {
        assert(_analysisDone);
        if (!IsTrackedLocal(lclNum))
        {
            return false;
        }

        return MayIndexPointToStack(LocalToIndex(lclNum));
    }

    private bool DoesIndexPointToStack(int index)
    {
        assert(_analysisDone);
        return BitVecOps.IsMember(_bitVecTraits, _definitelyStackPointingPointers, index);
    }

    private bool DoesLclVarPointToStack(int lclNum)
    {
        assert(_analysisDone);
        if (!IsTrackedLocal(lclNum))
        {
            return false;
        }

        return DoesIndexPointToStack(LocalToIndex(lclNum));
    }
}
