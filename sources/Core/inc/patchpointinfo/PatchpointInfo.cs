// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

/// <summary>Patchpoint info is passed back and forth across the interface but is opaque.</summary>
public partial struct PatchpointInfo
{
    private long _calleeSaveRegisters;
    private nint _tier0Version;
    private int _numberOfLocals;
    private int _totalFrameSize;
    private int _genericContextArgOffset;
    private int _keptAliveThisOffset;
    private int _securityCookieOffset;
    private int _monitorAcquiredOffset;
    private int _asyncThreadObjectOffset;
    private int _asyncExecutionContextOffset;
    private int _asyncSynchronizationContextOffset;
    private int _offsetAndExposureData;

    /// <summary>Number of locals in the original method (including special locals)</summary>
    public readonly int NumberOfLocals => _numberOfLocals;

    /// <summary>Total frame size of the original method</summary>
    public readonly int TotalFrameSize => _totalFrameSize;

    /// <summary>Determine how much storage is needed to hold this info</summary>
    /// <param name="localCount"></param>
    /// <returns></returns>
    public static unsafe int ComputeSize(int localCount)
    {
        var baseSize = sizeof(PatchpointInfo);
        var variableSize = localCount * sizeof(int);
        var totalSize = baseSize + variableSize;
        return totalSize;
    }

    public void Initialize(int localCount, int totalFrameSize)
    {
        _calleeSaveRegisters = 0;
        _tier0Version = 0;
        _numberOfLocals = localCount;
        _totalFrameSize = totalFrameSize;
        _genericContextArgOffset = -1;
        _keptAliveThisOffset = -1;
        _securityCookieOffset = -1;
        _monitorAcquiredOffset = -1;
        _asyncThreadObjectOffset = -1;
        _asyncExecutionContextOffset = -1;
        _asyncSynchronizationContextOffset = -1;
    }

    // Copy
    public unsafe void Copy(PatchpointInfo* original)
    {
        _calleeSaveRegisters = original->_calleeSaveRegisters;
        _tier0Version = original->_tier0Version;
        _genericContextArgOffset = original->_genericContextArgOffset;
        _keptAliveThisOffset = original->_keptAliveThisOffset;
        _securityCookieOffset = original->_securityCookieOffset;
        _monitorAcquiredOffset = original->_monitorAcquiredOffset;
        _asyncThreadObjectOffset = original->_asyncThreadObjectOffset;
        _asyncExecutionContextOffset = original->_asyncExecutionContextOffset;
        _asyncSynchronizationContextOffset = original->_asyncSynchronizationContextOffset;

        for (var i = 0; i < original->_numberOfLocals; i++)
        {
            Unsafe.Add(ref _offsetAndExposureData, i) = Unsafe.Add(ref original->_offsetAndExposureData, i);
        }
    }
}
