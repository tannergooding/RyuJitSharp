// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Runtime.CompilerServices;

namespace RyuJitSharp;

/// <summary>Patchpoint info is passed back and forth across the interface but is opaque.</summary>
public struct PatchpointInfo
{
    private ulong m_calleeSaveRegisters;
    private nuint m_tier0Version;
    private uint m_numberOfLocals;
    private int m_totalFrameSize;
    private int m_genericContextArgOffset;
    private int m_keptAliveThisOffset;
    private int m_securityCookieOffset;
    private int m_monitorAcquiredOffset;
    private int m_asyncExecutionContextOffset;
    private int m_asyncSynchronizationContextOffset;
    private int m_offsetAndExposureData;

    /// <summary>Number of locals in the original method (including special locals)</summary>
    public readonly uint NumberOfLocals => m_numberOfLocals;

    /// <summary>Total frame size of the original method</summary>
    public readonly int TotalFrameSize => m_totalFrameSize;

    /// <summary>Determine how much storage is needed to hold this info</summary>
    /// <param name="localCount"></param>
    /// <returns></returns>
    public static unsafe uint ComputeSize(uint localCount)
    {
        var baseSize = (uint)(sizeof(PatchpointInfo));
        var variableSize = localCount * sizeof(int);
        var totalSize = baseSize + variableSize;
        return totalSize;
    }

    public void Initialize(uint localCount, int totalFrameSize)
    {
        m_calleeSaveRegisters = 0;
        m_tier0Version = 0;
        m_numberOfLocals = localCount;
        m_totalFrameSize = totalFrameSize;
        m_genericContextArgOffset = -1;
        m_keptAliveThisOffset = -1;
        m_securityCookieOffset = -1;
        m_monitorAcquiredOffset = -1;
    }

    // Copy
    public unsafe void Copy(PatchpointInfo* original)
    {
        m_calleeSaveRegisters = original->m_calleeSaveRegisters;
        m_tier0Version = original->m_tier0Version;
        m_genericContextArgOffset = original->m_genericContextArgOffset;
        m_keptAliveThisOffset = original->m_keptAliveThisOffset;
        m_securityCookieOffset = original->m_securityCookieOffset;
        m_monitorAcquiredOffset = original->m_monitorAcquiredOffset;

        for (uint i = 0; i < original->m_numberOfLocals; i++)
        {
            Unsafe.Add(ref m_offsetAndExposureData, i) = Unsafe.Add(ref original->m_offsetAndExposureData, i);
        }
    }
}
