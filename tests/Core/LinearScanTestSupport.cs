// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.

using NUnit.Framework;

namespace RyuJitSharp.UnitTests;

internal static class LinearScanTestSupport
{
    public static void SetOutVarRegForBB(
        Compiler compiler, LinearScan allocator, uint bbNum, uint varNum, regNumber reg)
    {
        var map = allocator.getOutVarToRegMap(bbNum)
            ?? throw new AssertionException("Missing outgoing register map.");
        allocator.setVarReg(map, compiler.lvaTable[varNum]._varIndex, reg);
    }
}
