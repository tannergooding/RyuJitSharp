// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

using System;
using System.Collections.Generic;

namespace RyuJitSharp;

public sealed partial class Lowering
{
    private bool AreSourcesPossiblyModifiedLocals(GenTree addr, GenTree? baseAddress, GenTree? index)
    {
        SideEffectSet baseSideEffects = default;
        if (baseAddress is not null)
        {
            if (baseAddress.Oper.IsLocalRead)
            {
                baseSideEffects.AddNode(CompilerInstance, baseAddress);
            }
            else
            {
                baseAddress = null;
            }
        }

        SideEffectSet indexSideEffects = default;
        if (index is not null)
        {
            if (index.Oper.IsLocalRead)
            {
                indexSideEffects.AddNode(CompilerInstance, index);
            }
            else
            {
                index = null;
            }
        }

        for (var cursor = addr; ; cursor = cursor.Prev)
        {
            assert(cursor is not null);
            if (cursor == baseAddress)
            {
                baseAddress = null;
            }
            if (cursor == index)
            {
                index = null;
            }
            if ((baseAddress is null) && (index is null))
            {
                return false;
            }

            _scratchSideEffects.Clear();
            _scratchSideEffects.AddNode(CompilerInstance, cursor);
            if ((baseAddress is not null) && _scratchSideEffects.InterferesWith(in baseSideEffects, false))
            {
                return true;
            }
            if ((index is not null) && _scratchSideEffects.InterferesWith(in indexSideEffects, false))
            {
                return true;
            }
        }
    }

    private bool TryCreateAddrMode(ref GenTree addr, bool isContainable, GenTree parent)
    {
#if TARGET_XARCH
        if ((addr.Oper is not GT_ADD) || addr.HasOverflowCheck)
        {
            return false;
        }

        var codeGen = CompilerInstance.codeGen;
        assert(codeGen is not null);
        var doAddrMode = codeGen.genCreateAddrMode(addr.AsOp(), true, 0, out _, out var baseAddress,
            out var index, out var scale, out var offset);
        if (scale == 0)
        {
            scale = 1;
        }

        if (!isContainable)
        {
            if ((index is null) || ((scale == 1) && (offset == 0)))
            {
                return false;
            }
        }

        if (!doAddrMode || AreSourcesPossiblyModifiedLocals(addr, baseAddress, index))
        {
            JITDUMP("No addressing mode:\n  ");
            DISPNODE(addr);
            return false;
        }

        JITDUMP("Addressing mode:\n");
        JITDUMP("  Base\n    ");
        DISPNODE(baseAddress);
        if (index is not null)
        {
            JITDUMP($"  + Index * {scale} + {offset}\n    ");
            DISPNODE(index);
        }
        else
        {
            JITDUMP($"  + {offset}\n");
        }

        var unusedStack = new Stack<GenTree>();
        unusedStack.Push(addr.AsOp().Op1);
        unusedStack.Push(addr.AsOp().Op2);

        var addrMode = new GenTreeAddrMode(addr.Type, baseAddress, index, (byte)scale, unchecked((int)offset),
            addr, NodeThreading.LIR);
        addrMode.Flags &= ~GTF_ALL_EFFECT;
        BlockRange().ReplaceNode(addr, addrMode);
        addr = addrMode;

        baseAddress?.IsContained = false;
        index?.IsContained = false;

        while (unusedStack.TryPop(out var unused))
        {
            if ((unused != baseAddress) && (unused != index))
            {
                JITDUMP("Removing unused node:\n  ");
                DISPNODE(unused);
                BlockRange().Remove(unused);
                foreach (var operand in unused.Operands)
                {
                    unusedStack.Push(operand);
                }
            }
        }

        JITDUMP("New addressing mode node:\n  ");
        DISPNODE(addrMode);
        JITDUMP("\n");
        return true;
#else
        throw new NotImplementedException("Non-xarch address-mode lowering is not ported.");
#endif
    }
}
