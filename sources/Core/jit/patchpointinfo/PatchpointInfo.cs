// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

// Describes information needed to make an OSR transition
//  - location of IL-visible locals and other important state on the
//    original (Tier0) method frame, with respect to top of frame
//    (hence these offsets will be negative as stack grows down)
//  - total size of the original frame
//  - callee save registers saved on the original (Tier0) frame
//
// Currently the patchpoint info is independent of the IL offset of the patchpoint.
//
// This data is produced when jitting a Tier0 method with OSR enabled, and consumed
// by the Tier1/OSR jit request.
public partial struct PatchpointInfo
{
    private const int OFFSET_SHIFT = 0x1;
    private const int EXPOSURE_MASK = 0x1;

    // Total size of this patchpoint info record, in bytes
    public readonly int PatchpointInfoSize => ComputeSize(_numberOfLocals);

    // Original method caller SP offset for generic context arg
    public int GenericContextArgOffset
    {
        readonly get
        {
            return _genericContextArgOffset;
        }

        set
        {
            _genericContextArgOffset = value;
        }
    }

    public readonly bool HasGenericContextArgOffset => _genericContextArgOffset is not -1;

    // Original method FP relative offset for kept-alive this
    public int KeptAliveThisOffset
    {
        readonly get
        {
            return _keptAliveThisOffset;
        }

        set
        {
            _keptAliveThisOffset = value;
        }
    }

    public readonly bool HasKeptAliveThis => _keptAliveThisOffset is not -1;

    // Original method FP relative offset for security cookie
    public int SecurityCookieOffset
    {
        readonly get
        {
            return _securityCookieOffset;
        }

        set
        {
            _securityCookieOffset = value;
        }
    }

    public readonly bool HasSecurityCookie => _securityCookieOffset is not -1;

    // Original method FP relative offset for monitor acquired flag
    public int MonitorAcquiredOffset
    {
        readonly get
        {
            return _monitorAcquiredOffset;
        }

        set
        {
            _monitorAcquiredOffset = value;
        }
    }

    public readonly bool HasMonitorAcquired => _monitorAcquiredOffset is not -1;

    // Original method FP relative offset for async contexts
    public int AsyncExecutionContextOffset
    {
        readonly get
        {
            return _asyncExecutionContextOffset;
        }

        set
        {
            _asyncExecutionContextOffset = value;
        }
    }

    public readonly bool HasAsyncExecutionContextOffset => _asyncExecutionContextOffset is not -1;

    public int AsyncSynchronizationContextOffset
    {
        readonly get
        {
            return _asyncSynchronizationContextOffset;
        }

        set
        {
            _asyncSynchronizationContextOffset = value;
        }
    }

    public readonly bool HasAsyncSynchronizationContextOffset => _asyncSynchronizationContextOffset is not -1;

    // True if this local was address exposed in the original method
    public readonly bool IsExposed(int localNum)
    {
        return (Unsafe.Add(ref Unsafe.AsRef(in _offsetAndExposureData), localNum) & EXPOSURE_MASK) != 0;
    }

    // FP relative offset of this local in the original method
    public readonly int Offset(int localNum)
    {
        return (Unsafe.Add(ref Unsafe.AsRef(in _offsetAndExposureData), localNum) >> OFFSET_SHIFT);
    }

    public void SetOffsetAndExposure(int localNum, int offset, bool isExposed)
    {
        Unsafe.Add(ref _offsetAndExposureData, localNum) = (offset << OFFSET_SHIFT) | (isExposed ? EXPOSURE_MASK : 0);
    }

    // Callee save registers saved by the original method.
    // Includes all saves that must be restored (eg includes pushed RBP on x64).
    public long CalleeSaveRegisters
    {
        readonly get
        {
            return _calleeSaveRegisters;
        }

        set
        {
            _calleeSaveRegisters = value;
        }
    }

    public nint GetTier0EntryPoint
    {
        readonly get
        {
            return _tier0Version;
        }

        set
        {
            _tier0Version = value;
        }
    }
}
