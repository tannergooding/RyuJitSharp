// Copyright (c) Tanner Gooding and Contributors. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
//
// Based on the RyuJIT compiler from dotnet/runtime.
// Original source is Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License (MIT).

namespace RyuJitSharp;

public partial class Compiler
{
    internal static GenTree fgGetFirstNode(GenTree tree)
    {
        while (true)
        {
            var operands = tree.Operands.GetEnumerator();
            if (!operands.MoveNext())
            {
                return tree;
            }
            tree = operands.Current;
        }
    }

    public ParameterRegisterLocalMapping? FindParameterRegisterLocalMappingByRegister(regNumber register)
    {
        if (_paramRegLocalMappings is not null)
        {
            for (var index = 0; index < _paramRegLocalMappings.Count; index++)
            {
                var mapping = _paramRegLocalMappings[index];
                if (mapping.RegisterSegment.Register == register)
                {
                    return mapping;
                }
            }
        }
        return null;
    }

    public bool fgSimpleLowerCastOfSmpOp(LIR.Range range, GenTreeCast cast)
    {
        var operand = cast.CastOp;
        var castType = cast.CastType;
        var sourceType = operand.Type;
        assert(operand.Oper.IsSimple);
        if (opts.OptimizationDisabled || cast.HasOverflowCheck ||
            (operand.Oper.MayOverflow && operand.HasOverflowCheck) ||
            !varTypeIsSmall(castType) || !varTypeIsIntegral(sourceType))
        {
            return false;
        }
        if (operand.Oper is not GT_ADD and not GT_SUB and not GT_MUL and not GT_AND and not GT_XOR and
            not GT_OR and not GT_NOT and not GT_NEG)
        {
            return false;
        }

        // Removing input normalization is safe in LIR, but not in HIR where
        // normalized-on-load locals still participate in value numbering.
        var changed = false;
        var first = operand.AsUnOp().Op1;
        if (first.Oper is GT_CAST)
        {
            var input = first.AsCast();
            if (!input.HasOverflowCheck && (input.CastOp.Type.ActualType == sourceType.ActualType) && (input.CastType == castType))
            {
                operand.AsUnOp().Op1 = input.CastOp;
                range.Remove(input);
                changed = true;
            }
        }
        if (operand.Oper.IsBinary && (operand.AsOp().Op2.Oper is GT_CAST))
        {
            var input = operand.AsOp().Op2.AsCast();
            if (!input.HasOverflowCheck && (input.CastOp.Type.ActualType == sourceType.ActualType) && (input.CastType == castType))
            {
                operand.AsOp().Op2 = input.CastOp;
                range.Remove(input);
                changed = true;
            }
        }
#if DEBUG
        if (changed)
        {
            JITDUMP($"Lower - Cast of Simple Op {cast.Oper.Name}:\n");
            DISPTREE(cast);
        }
#endif
        return changed;
    }

    public bool fgSimpleLowerBswap16(LIR.Range range, GenTree node)
    {
        assert(node.Oper is GT_BSWAP16);
        if (opts.OptimizationDisabled)
        {
            return false;
        }
        var changed = false;
        var operand = node.AsUnOp().Op1;
        if (operand.Oper is GT_CAST)
        {
            var cast = operand.AsCast();
            if (!cast.HasOverflowCheck && (cast.CastType.Size >= 2) && (cast.CastOp.Type.ActualType is TYP_INT))
            {
                node.AsUnOp().Op1 = cast.CastOp;
                range.Remove(cast);
                changed = true;
            }
        }
#if DEBUG
        if (changed)
        {
            JITDUMP($"Lower - Downcast of Small Op {node.Oper.Name}:\n");
            DISPTREE(node);
        }
#endif
        return changed;
    }
}
