// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    public struct SSAName
    {
        public uint m_lvNum;
        public uint m_ssaNum;

        public SSAName(uint lvNum, uint ssaNum)
        {
            m_lvNum = lvNum;
            m_ssaNum = ssaNum;
        }

        public static uint GetHashCode(SSAName ssaNm)
        {
            return (ssaNm.m_lvNum << 16) | ssaNm.m_ssaNum;
        }

        public static bool Equals(SSAName ssaNm1, SSAName ssaNm2)
        {
            return (ssaNm1.m_lvNum == ssaNm2.m_lvNum) && (ssaNm1.m_ssaNum == ssaNm2.m_ssaNum);
        }
    }
}
