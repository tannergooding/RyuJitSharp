// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public sealed partial class BasicBlock
{
    public static void CloneBlockState(Compiler compiler, BasicBlock to, BasicBlock from)
    {
        assert(to.FirstStmt is null);
        to.CopyFlags(from);
        to.bbWeight = from.bbWeight;
        to.copyEHRegion(from);
        to.CatchType = from.CatchType;
        to.bbStkTempsIn = from.bbStkTempsIn;
        to.bbStkTempsOut = from.bbStkTempsOut;
        to.bbCodeOffs = from.bbCodeOffs;
        to.bbCodeOffsEnd = from.bbCodeOffsEnd;
#if DEBUG
        to.bbTgtStkDepth = from.bbTgtStkDepth;
#endif

        foreach (var statement in from.Statements)
        {
            var expression = compiler.gtCloneExpr(statement.RootNode)
                ?? throw new FatalJitException("A block statement must have a cloneable root.");
            compiler.fgInsertStmtAtEnd(to, compiler.fgNewStmtFromTree(expression, di: statement.DebugInfo));
        }
    }
}
