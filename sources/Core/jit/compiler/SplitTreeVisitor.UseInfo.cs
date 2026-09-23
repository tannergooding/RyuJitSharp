// Copyright © Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial struct SplitTreeVisitor
{
    private readonly struct UseInfo(GenTree? user, int operandIndex)
    {
        public GenTree? User => user;

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
}
