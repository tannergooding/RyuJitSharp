// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class SsaBuilder
{
    internal static Statement? GetIncrementalPhiNode(BasicBlock block, int lclNum)
    {
        return GetPhiNode(block, lclNum);
    }

    internal static Statement InsertIncrementalPhi(Compiler compiler, BasicBlock block, int lclNum)
    {
        return InsertPhi(compiler, block, lclNum);
    }

    internal static void AddIncrementalPhiArg(Compiler compiler, BasicBlock block, Statement statement,
        GenTreePhi phi, int lclNum, int ssaNum, BasicBlock pred)
    {
        AddNewPhiArg(compiler, block, statement, phi, lclNum, ssaNum, pred);
    }
}
