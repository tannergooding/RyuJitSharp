// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Diagnostics.CodeAnalysis;
using static RyuJitSharp.Globals;

namespace RyuJitSharp;

public sealed class SsaRenameState
{
    private sealed class StackNode
    {
        // Previous definition of this local or memory kind.
        public StackNode? StackPrev;
        // Previous stack in the cross-stack push list, used to unwind one block.
        public Stack? ListPrev;
        public BasicBlock Block;
        public int SsaNum;

        public StackNode(Stack? listPrev, BasicBlock block, int ssaNum)
        {
            ListPrev = listPrev;
            Block = block;
            SsaNum = ssaNum;
        }

        public void Reset(Stack? listPrev, BasicBlock block, int ssaNum)
        {
            ListPrev = listPrev;
            Block = block;
            SsaNum = ssaNum;
        }
    }

    private sealed class Stack
    {
        public StackNode? Top { get; private set; }

        public void Push(StackNode node)
        {
            node.StackPrev = Top;
            Top = node;
        }

        public StackNode Pop()
        {
            var top = Top ?? throw new InvalidOperationException("Cannot pop an empty SSA rename stack.");
            Top = top.StackPrev;

            return top;
        }
    }

    private readonly Compiler _compiler;
    private readonly int _lvaCount;
    private Stack[]? _stacks;
    private Stack? _stackListTail;
    private readonly Stack[] _memoryStacks;
    private readonly Stack _freeStack = new();

    public SsaRenameState(Compiler compiler)
    {
        _compiler = compiler;
        _lvaCount = compiler.lvaCount;
        _memoryStacks = new Stack[(int)MemoryKindCount];

        for (var kind = 0; kind < _memoryStacks.Length; kind++)
        {
            _memoryStacks[kind] = new Stack();
        }
    }

    public int Top(int lclNum)
    {
        noway_assert(_stacks is not null);
        var top = _stacks[lclNum].Top;
        noway_assert(top is not null);

#if DEBUG
        if (_compiler.verboseSsa)
        {
            logf($"[SsaRenameState::Top] {FMT_BB(top.Block.bbNum)}, V{lclNum:D2}, ssaNum = {top.SsaNum}\n");
        }
#endif

        return top.SsaNum;
    }

    public void Push(BasicBlock block, int lclNum, int ssaNum)
    {
#if DEBUG
        if (_compiler.verboseSsa)
        {
            logf($"[SsaRenameState::Push] {FMT_BB(block.bbNum)}, V{lclNum:D2}, ssaNum = {ssaNum}\n");
        }
#endif

        EnsureStacks();
        Push(_stacks[lclNum], block, ssaNum);
    }

    public void PopBlockStacks(BasicBlock block)
    {
#if DEBUG
        if (_compiler.verboseSsa)
        {
            logf($"[SsaRenameState::PopBlockStacks] {FMT_BB(block.bbNum)}\n");
        }
#endif

        while (_stackListTail is Stack stack)
        {
            var top = stack.Top ?? throw new InvalidOperationException("The active SSA rename stack is empty.");
            if (!ReferenceEquals(top.Block, block))
            {
                break;
            }

            stack.Pop();
#if DEBUG
            DumpStack(stack);
#endif
            _stackListTail = top.ListPrev;
            _freeStack.Push(top);
        }

#if DEBUG
        if (_stacks is not null)
        {
            for (var i = 0; i < _lvaCount; i++)
            {
                if (_stacks[i].Top is StackNode top)
                {
                    assert(!ReferenceEquals(top.Block, block));
                }
            }
        }
#endif
    }

    public int TopMemory(MemoryKind memoryKind)
    {
        var top = _memoryStacks[(int)memoryKind].Top
            ?? throw new InvalidOperationException("The memory SSA rename stack is empty.");

        return top.SsaNum;
    }

    public void PushMemory(MemoryKind memoryKind, BasicBlock block, int ssaNum)
    {
        Push(_memoryStacks[(int)memoryKind], block, ssaNum);
    }

    [MemberNotNull(nameof(_stacks))]
    private void EnsureStacks()
    {
        if (_stacks is null)
        {
            _stacks = new Stack[_lvaCount];
            for (var i = 0; i < _lvaCount; i++)
            {
                _stacks[i] = new Stack();
            }
        }
    }

    private StackNode AllocStackNode(Stack? listPrev, BasicBlock block, int ssaNum)
    {
        if (_freeStack.Top is not null)
        {
            var node = _freeStack.Pop();
            node.Reset(listPrev, block, ssaNum);

            return node;
        }

        return new StackNode(listPrev, block, ssaNum);
    }

    private void Push(Stack stack, BasicBlock block, int ssaNum)
    {
        var top = stack.Top;
        if ((top is null) || !ReferenceEquals(top.Block, block))
        {
            stack.Push(AllocStackNode(_stackListTail, block, ssaNum));
            _stackListTail = stack;
        }
        else
        {
            // A block has one node per stack; replacing its number makes a single pop
            // restore the previous block's definition, not an earlier same-block definition.
            top.SsaNum = ssaNum;
        }

#if DEBUG
        DumpStack(stack);
#endif
    }

#if DEBUG
    private void DumpStack(Stack stack)
    {
        if (!_compiler.verboseSsa)
        {
            return;
        }

        if (ReferenceEquals(stack, _memoryStacks[(int)ByrefExposed]))
        {
            jitprintf("ByrefExposed: ");
        }
        else if (ReferenceEquals(stack, _memoryStacks[(int)GcHeap]))
        {
            jitprintf("GcHeap: ");
        }
        else
        {
            noway_assert(_stacks is not null);
            jitprintf($"V{Array.IndexOf(_stacks, stack):D2}: ");
        }

        for (var node = stack.Top; node is not null; node = node.StackPrev)
        {
            jitprintf($"{(ReferenceEquals(node, stack.Top) ? "" : ", ")}<{FMT_BB(node.Block.bbNum)}, {node.SsaNum}>");
        }
        jitprintf("\n");
    }
#endif
}
