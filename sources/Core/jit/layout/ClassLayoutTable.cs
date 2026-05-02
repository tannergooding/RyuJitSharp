// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed class ClassLayoutTable
{
    /// <summary>Get the layout for the specified class handle.</summary>
    /// <param name="compiler"></param>
    /// <param name="classHandle"></param>
    /// <returns></returns>
    public unsafe ClassLayout GetObjLayout(Compiler compiler, CORINFO_CLASS_HANDLE classHandle)
    {
        // TODO: Port ClassLayoutTable.GetObjLayout
        return null!;
    }
}
