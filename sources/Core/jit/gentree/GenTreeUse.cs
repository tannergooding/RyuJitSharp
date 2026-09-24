// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

// Record an owning slot without retaining a managed byref across visitor callbacks.
// Operand lists must retain their shape while a use is recorded.
internal readonly struct GenTreeUse(GenTree? user, int operandIndex)
{
    public GenTree? User => user;

    public static GenTreeUse FromUse(ref GenTree use, GenTree? user)
    {
        if (user is null)
        {
            return new GenTreeUse(null, -1);
        }

        var index = 0;

        foreach (ref var operand in user.UseEdges)
        {
            if (System.Runtime.CompilerServices.Unsafe.AreSame(ref operand, ref use))
            {
                return new GenTreeUse(user, index);
            }

            index++;
        }

        throw new System.InvalidOperationException("The visited operand does not belong to its user.");
    }

    public ref GenTree GetUse(Statement statement)
    {
        if (user is null)
        {
            return ref statement.RootNodeRef;
        }

        var index = 0;

        foreach (ref var operand in user.UseEdges)
        {
            if (index++ == operandIndex)
            {
                return ref operand;
            }
        }

        throw new System.InvalidOperationException("The recorded operand is no longer present.");
    }
}
