// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    private struct HandlerLiveVariablesVisitor : IBasicBlockVisitor<HandlerLiveVariablesVisitor>
    {
        private readonly Compiler _compiler;
        private readonly nint[] _handlerLiveVars;
        private MemoryKindSet _memoryLiveness;

        public HandlerLiveVariablesVisitor(Compiler compiler, nint[] handlerLiveVars, MemoryKindSet memoryLiveness)
        {
            _compiler = compiler;
            _handlerLiveVars = handlerLiveVars;
            _memoryLiveness = memoryLiveness;
        }

        public readonly MemoryKindSet MemoryLiveness => _memoryLiveness;

        public BasicBlockVisit Visit(BasicBlock successor)
        {
            VarSetOps.UnionD(_compiler, _handlerLiveVars, successor.bbLiveIn);
            _memoryLiveness |= successor.bbMemoryLiveIn;
            return BasicBlockVisit.Continue;
        }
    }

    public void fgAddHandlerLiveVars(BasicBlock block, nint[] ehHandlerLiveVars, ref MemoryKindSet memoryLiveness)
    {
        assert(block.HasPotentialEHSuccs(this));
        var visitor = new HandlerLiveVariablesVisitor(this, ehHandlerLiveVars, memoryLiveness);
        _ = block.VisitEHSuccs(this, ref visitor);
        memoryLiveness = visitor.MemoryLiveness;
    }
}
