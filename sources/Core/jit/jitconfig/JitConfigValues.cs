// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RyuJitSharp;

public sealed unsafe partial class JitConfigValues
{
    // TODO: Port jitconfigvalues.h

    private bool m_isInitialized;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool isInitialized()
    {
        return m_isInitialized;
    }

    public void initialize(ICorJitHost* host)
    {
        Debug.Assert(!m_isInitialized);

        // TODO: initialize jitconfigvalues.h

        m_isInitialized = true;
    }

    public void destroy(ICorJitHost* host)
    {
        if (!m_isInitialized)
        {
            return;
        }

        // TODO: destroy jitconfigvalues.h

        m_isInitialized = false;
    }
}
