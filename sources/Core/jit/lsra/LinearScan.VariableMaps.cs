// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class LinearScan
{
    private uint _regMapCount;
    private regNumberSmall[]?[]? _inVarToRegMaps;
    private regNumberSmall[]?[]? _outVarToRegMaps;
    private regNumberSmall[]? _sharedCriticalVarToRegMap;

    // Block sequencing captures this boundary before resolution inserts split blocks.
    private uint _bbNumMaxBeforeResolution;
    private Dictionary<uint, SplitEdgeInfo>? _splitBBNumToTargetBBNumMap;

    internal struct SplitEdgeInfo
    {
        public uint fromBBNum;
        public uint toBBNum;
    }

    internal Dictionary<uint, SplitEdgeInfo> getSplitBBNumToTargetBBNumMap()
    {
        _splitBBNumToTargetBBNumMap ??= [];

        return _splitBBNumToTargetBBNumMap;
    }

    internal void initVarRegMaps()
    {
        if (!_enregisterLocalVars)
        {
            _inVarToRegMaps = null;
            _outVarToRegMaps = null;
            return;
        }

        assert(_compiler.lvaTrackedFixed);
        var varCount = _compiler.lvaTrackedCount;

        // Retain the native allocator's int-sized rounding, including padding entries.
        // Variable maps use byte-sized regNumberSmall storage, independent of regNumber.
        _regMapCount = (uint)roundUp(varCount, sizeof(int));
        var bbCount = _compiler.fgBBNumMax + 1;
        _inVarToRegMaps = new regNumberSmall[bbCount][];
        _outVarToRegMaps = new regNumberSmall[bbCount][];

        if (varCount > 0)
        {
            // Resolution initializes this scratch map before reading it.
            _sharedCriticalVarToRegMap = new regNumberSmall[_regMapCount];
            for (var blockNumber = 0; blockNumber < bbCount; blockNumber++)
            {
                var inMap = new regNumberSmall[_regMapCount];
                var outMap = new regNumberSmall[_regMapCount];
                Array.Fill(inMap, (regNumberSmall)REG_STK);
                Array.Fill(outMap, (regNumberSmall)REG_STK);
                _inVarToRegMaps[blockNumber] = inMap;
                _outVarToRegMaps[blockNumber] = outMap;
            }
        }
        else
        {
            _sharedCriticalVarToRegMap = null;
        }
    }

    internal void setInVarRegForBB(uint bbNum, uint varNum, regNumber reg)
    {
        assert(_enregisterLocalVars);
        assert((uint)reg < byte.MaxValue && varNum < _compiler.lvaCount);
        assert(_inVarToRegMaps is not null);
        var map = _inVarToRegMaps[bbNum];
        assert(map is not null);
        map[_compiler.lvaTable[varNum]._varIndex] = (regNumberSmall)reg;
    }

    private SplitEdgeInfo getSplitEdgeInfo(uint bbNum)
    {
        assert(_enregisterLocalVars);
        assert(bbNum <= _compiler.fgBBNumMax);
        assert(bbNum > _bbNumMaxBeforeResolution);
        assert(_splitBBNumToTargetBBNumMap is not null);
        var info = _splitBBNumToTargetBBNumMap[bbNum];
        assert(info.toBBNum <= _bbNumMaxBeforeResolution);
        assert(info.fromBBNum <= _bbNumMaxBeforeResolution);

        return info;
    }

    internal regNumberSmall[]? getInVarToRegMap(uint bbNum)
    {
        assert(_enregisterLocalVars);
        assert(bbNum <= _compiler.fgBBNumMax);
        assert(_inVarToRegMaps is not null);
        assert(_outVarToRegMaps is not null);

        if (bbNum > _bbNumMaxBeforeResolution)
        {
            var info = getSplitEdgeInfo(bbNum);
            if (info.fromBBNum == 0)
            {
                assert(info.toBBNum != 0);
                return _inVarToRegMaps[info.toBBNum];
            }

            return _outVarToRegMaps[info.fromBBNum];
        }

        return _inVarToRegMaps[bbNum];
    }

    internal regNumberSmall[]? getOutVarToRegMap(uint bbNum)
    {
        assert(_enregisterLocalVars);
        assert(bbNum <= _compiler.fgBBNumMax);
        if (bbNum == 0)
        {
            return null;
        }

        assert(_inVarToRegMaps is not null);
        assert(_outVarToRegMaps is not null);
        if (bbNum > _bbNumMaxBeforeResolution)
        {
            var info = getSplitEdgeInfo(bbNum);
            if (info.toBBNum == 0)
            {
                assert(info.fromBBNum != 0);
                return _outVarToRegMaps[info.fromBBNum];
            }

            return _inVarToRegMaps[info.toBBNum];
        }

        return _outVarToRegMaps[bbNum];
    }

    internal void setVarReg(regNumberSmall[] map, uint trackedVarIndex, regNumber reg)
    {
        assert(trackedVarIndex < _compiler.lvaTrackedCount);
        var regSmall = (regNumberSmall)reg;
        assert((regNumber)regSmall == reg);
        map[trackedVarIndex] = regSmall;
    }

    internal regNumber getVarReg(regNumberSmall[] map, uint trackedVarIndex)
    {
        assert(_enregisterLocalVars);
        assert(trackedVarIndex < _compiler.lvaTrackedCount);

        return (regNumber)map[trackedVarIndex];
    }
}
