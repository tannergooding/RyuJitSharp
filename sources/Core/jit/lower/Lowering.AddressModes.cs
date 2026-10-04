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
        if ((addr.Oper is not GT_ADD) || addr.HasOverflowCheck)
        {
            return false;
        }

#if TARGET_ARM64
        if (parent.Oper.IsIndir && parent.AsIndir().IsVolatile &&
            !CompilerInstance.compOpportunisticallyDependsOn(InstructionSet_Rcpc2))
        {
            // LDAR/STLR require a register address; RCPC2 adds unscaled addressing.
            return false;
        }

        if (parent.Type is TYP_MASK or TYP_SIMD)
        {
            return false;
        }
#endif

        var targetType = parent.Oper.IsIndir ? parent.Type : TYP_UNDEF;
#if TARGET_ARM64
        var naturalMul = targetType.Size;
#else
        var naturalMul = 0;
#endif
        var codeGen = CompilerInstance.codeGen;
        assert(codeGen is not null);
        var doAddrMode = codeGen.genCreateAddrMode(addr.AsOp(), true, naturalMul, out _, out var baseAddress,
            out var index, out var scale, out var offset);
#if TARGET_ARM64
        if (parent.Oper.IsIndir && parent.AsIndir().IsVolatile)
        {
            assert(CompilerInstance.compIsaSupportedDebugOnly(InstructionSet_Rcpc2));
            if ((scale > 1) || !Emitter.emitIns_valid_imm_for_unscaled_ldst_offset(offset) || (index is not null))
            {
                return false;
            }
        }
#endif
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

#if TARGET_ARM64
        if (index is not null)
        {
            if ((index.Oper is GT_CAST) && (scale == 1) && (offset == 0) && varTypeIsByte(targetType))
            {
                if (IsInvariantInRange(index, parent))
                {
                    // A contained index requires its LEA to be contained through the parent.
                    index.AsCast().CastOp.IsContained = false;
                    MakeSrcContained(addrMode, index);
                }
            }
            else if ((index.Oper is GT_BFIZ) && (index.AsOp().Op1.Oper is GT_CAST) &&
                index.AsOp().Op2.Oper.IsCnsIntOrI && !varTypeIsStruct(targetType))
            {
                var cast = index.AsOp().Op1.AsCast();
                assert(cast.IsContained);
                var shiftBy = unchecked((uint)index.AsOp().Op2.AsIntCon().IconValue);

                // SXTW/UXTW consumes the sole scale/offset slot and must match the access width.
                if ((cast.CastOp.Type is TYP_INT) && (cast.Type is TYP_LONG) &&
                    ((uint)targetType.Size == (1U << (int)shiftBy)) && (scale == 1) && (offset == 0))
                {
                    if (IsInvariantInRange(index, parent))
                    {
                        MakeSrcContained(addrMode, index);
                    }
                }
            }
        }
#elif TARGET_RISCV64
        if (index is not null)
        {
            assert(baseAddress is not null);
            assert(scale <= 1);

            var addition = CompilerInstance.gtNewBinaryNode(GT_ADD, addrMode.Type, baseAddress, index);
            BlockRange().InsertBefore(addrMode, addition);
            addrMode.BaseAddress = addition;
            addrMode.Index = null;
            _ = LowerAdd(addition);
        }
#endif

        JITDUMP("New addressing mode node:\n  ");
        DISPNODE(addrMode);
        JITDUMP("\n");
        return true;
    }
}
