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
    private regNumber[]?[]? _inVarToRegMaps;
    private regNumber[]?[]? _outVarToRegMaps;
    private regNumber[]? _sharedCriticalVarToRegMap;

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
        // regNumber's byte-sized managed representation also represents regNumberSmall.
        _regMapCount = (uint)roundUp(varCount, sizeof(int));
        var bbCount = _compiler.fgBBNumMax + 1;
        _inVarToRegMaps = new regNumber[bbCount][];
        _outVarToRegMaps = new regNumber[bbCount][];

        if (varCount > 0)
        {
            // Resolution initializes this scratch map before reading it.
            _sharedCriticalVarToRegMap = new regNumber[_regMapCount];
            for (var blockNumber = 0; blockNumber < bbCount; blockNumber++)
            {
                var inMap = new regNumber[_regMapCount];
                var outMap = new regNumber[_regMapCount];
                Array.Fill(inMap, REG_STK);
                Array.Fill(outMap, REG_STK);
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
        map[_compiler.lvaTable[varNum]._varIndex] = reg;
    }

    internal void setOutVarRegForBB(uint bbNum, uint varNum, regNumber reg)
    {
        assert(_enregisterLocalVars);
        assert((uint)reg < byte.MaxValue && varNum < _compiler.lvaCount);
        assert(_outVarToRegMaps is not null);
        var map = _outVarToRegMaps[bbNum];
        assert(map is not null);
        map[_compiler.lvaTable[varNum]._varIndex] = reg;
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

    internal regNumber[]? getInVarToRegMap(uint bbNum)
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

    internal regNumber[]? getOutVarToRegMap(uint bbNum)
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

    internal void setVarReg(regNumber[] map, uint trackedVarIndex, regNumber reg)
    {
        assert(trackedVarIndex < _compiler.lvaTrackedCount);
        var regSmall = (byte)reg;
        assert((regNumber)regSmall == reg);
        map[trackedVarIndex] = (regNumber)regSmall;
    }

    internal regNumber getVarReg(regNumber[] map, uint trackedVarIndex)
    {
        assert(_enregisterLocalVars);
        assert(trackedVarIndex < _compiler.lvaTrackedCount);

        return map[trackedVarIndex];
    }

    internal regNumber[]? setInVarToRegMap(uint bbNum, regNumber[]? srcVarToRegMap)
    {
        assert(_enregisterLocalVars);
        assert(_inVarToRegMaps is not null);
        var inMap = _inVarToRegMaps[bbNum];
        if (_regMapCount != 0)
        {
            assert(inMap is not null);
            assert(srcVarToRegMap is not null);

            // Native lsra.cpp copies regMapCount * sizeof(regNumber) bytes from
            // regNumberSmall storage. Copy only the allocated logical entries,
            // including padding, without replacing maps aliased by split blocks.
            Array.Copy(srcVarToRegMap, inMap, _regMapCount);
        }

        return inMap;
    }
}
