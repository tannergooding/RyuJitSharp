// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public partial class Liveness<TLiveness>
    where TLiveness : ILivenessPolicy
{
    private readonly Compiler _compiler;

    public Liveness(Compiler compiler)
    {
        _compiler = compiler;
    }

    public void SelectTrackedLocals()
    {
        _compiler.lvaTrackedCount = 0;
        _compiler.lvaTrackedCountInSizeTUnits = 0;

#if DEBUG
        VarSetOps.AssignNoCopy(_compiler, ref _compiler.lvaTrackedVars, VarSetOps.MakeEmpty(_compiler));
#endif
        if (_compiler.lvaCount == 0)
        {
            return;
        }

        // The managed reverse-map array carries the native allocation capacity.
        var trackedCandidates = _compiler.lvaTrackedToVarNum;
        if ((trackedCandidates is null) || (trackedCandidates.Length < _compiler.lvaCount))
        {
            trackedCandidates = new int[_compiler.lvaCount];
            _compiler.lvaTrackedToVarNum = trackedCandidates;
        }

        var trackedCandidateCount = 0;
        for (var localNumber = 0; localNumber < _compiler.lvaCount; localNumber++)
        {
            ref var descriptor = ref _compiler.lvaGetDesc(localNumber);
            var isTracked = true;
#if DEBUG
            descriptor.lvTrackedWithoutIndex = false;
#endif
            if (descriptor.lvRefCnt(_compiler.lvaRefCountState) == 0)
            {
                isTracked = false;
                descriptor.setLvRefCntWtd(0, _compiler.lvaRefCountState);
            }

            if (!TLiveness.TrackAddressExposedLocals && descriptor.IsAddressExposed)
            {
                isTracked = false;
            }

            if (descriptor.lvPromoted)
            {
                isTracked = false;
            }

            // Pinned locals have invisible runtime uses; tracking would make
            // observable null stores appear dead.
            if (descriptor.lvPinned)
            {
                isTracked = false;
            }

            descriptor.lvTracked = isTracked;
            if (isTracked)
            {
                trackedCandidates[trackedCandidateCount++] = localNumber;
            }
        }

        _compiler.lvaTrackedCount = (int)uint.Min((uint)trackedCandidateCount, unchecked((uint)JitConfig.JitMaxLocalsToTrack));
        if (!TLiveness.IsEarly || (_compiler.lvaTrackedCount < trackedCandidateCount))
        {
            var candidates = trackedCandidates.AsSpan(0, trackedCandidateCount);
            if (_compiler.compCodeOpt is Compiler.SMALL_CODE)
            {
                SortTrackedCandidates(candidates, new SmallCodeLess(_compiler));
            }
            else
            {
                SortTrackedCandidates(candidates, new BlendedCodeLess(_compiler));
            }
        }

        JITDUMP($"Tracked variable ({_compiler.lvaTrackedCount} out of {_compiler.lvaCount}) table:\n");
        for (var variableIndex = 0; variableIndex < _compiler.lvaTrackedCount; variableIndex++)
        {
            ref var descriptor = ref _compiler.lvaGetDesc(trackedCandidates[variableIndex]);
            assert(descriptor.lvTracked);
            descriptor._varIndex = unchecked((ushort)variableIndex);

#if DEBUG
            if (_compiler.verbose)
            {
                _compiler.gtDispLclVar(trackedCandidates[variableIndex]);
            }
            JITDUMP($" [{descriptor.Type.Name,6}]: refCnt = {descriptor.lvRefCnt(_compiler.lvaRefCountState),4}, refCntWtd = {refCntWtd2str(descriptor.lvRefCntWtd(_compiler.lvaRefCountState), true),6}\n");
#endif
        }

        JITDUMP("\n");
        for (var variableIndex = _compiler.lvaTrackedCount; variableIndex < trackedCandidateCount; variableIndex++)
        {
            ref var descriptor = ref _compiler.lvaGetDesc(trackedCandidates[variableIndex]);
            assert(descriptor.lvTracked);
            descriptor.lvTracked = false;
        }

        _compiler.lvaCurEpoch = unchecked(_compiler.lvaCurEpoch + 1);
        var bitsPerWord = (uint)(Unsafe.SizeOf<nint>() * 8);
        _compiler.lvaTrackedCountInSizeTUnits = (int)(((uint)_compiler.lvaTrackedCount + bitsPerWord - 1) / bitsPerWord);
#if DEBUG
        VarSetOps.AssignNoCopy(_compiler, ref _compiler.lvaTrackedVars, VarSetOps.MakeFull(_compiler));
#endif
    }
}
