// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

internal interface IValueNumVisitor<TSelf>
    where TSelf : struct, IValueNumVisitor<TSelf>
{
    ValueNumStore.VNVisit Visit(ValueNum valueNum);
}
